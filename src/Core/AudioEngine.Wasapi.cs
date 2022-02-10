
using System;
using System.Collections.Generic;
using System.Linq;

using NAudio.Wave;
using NAudio.Wave.Asio;
using NAudio.CoreAudioApi;
using System.Reactive.Subjects;
using System.Diagnostics;

namespace VL.Audio
{
    public partial class AudioEngine
    {
        private void PreviewWASAPIDriver(string driverName, string wasapiRecordingName)
        {
            AnalyzeWASAPIDevices(driverName, wasapiRecordingName, out var inputDevice, out var outputDevice, out var isLoopback);

            if (WasapiDevice == null || WasapiDevice.MMOutDevice?.FriendlyName != driverName
                || WasapiDevice.MMInDevice?.FriendlyName != wasapiRecordingName)
            {
                //dispose device if necessary
                Cleanup();

                //create new driver
                WasapiDevice = new WasapiInOut(outputDevice, inputDevice, isLoopback);

                CurrentDevice = WasapiDevice.Output;
                driverInitialized = false;

                //trigger to update the dynamic SampleRate enum
                SettingsChanged.OnNext(driverName);
            }
        }

      
        private void ChangeWASAPIDriverSettings(string driverName, string wasapiRecordingName, int sampleRate, int inputChannels, int inputChannelOffset, int outputChannels, int outputChannelOffset)
        {
            AnalyzeWASAPIDevices(driverName, wasapiRecordingName, out var inputDevice, out var outputDevice, out var isLoopback);

            if (!driverInitialized || WasapiDevice == null || WasapiDevice.MMOutDevice?.FriendlyName != driverName
                || WasapiDevice.MMInDevice?.FriendlyName != wasapiRecordingName
                || MasterWaveProvider.WaveFormat.SampleRate != sampleRate)
            {
                //dispose device if necessary
                Cleanup();

                //create new driver
                WasapiDevice = new WasapiInOut(outputDevice, inputDevice, isLoopback);

                //set channel offset
                //WasapiOut.ChannelOffset = outputChannelOffset;
                //WasapiOut.InputChannelOffset = inputChannelOffset;

                //init driver
                MasterWaveProvider.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, outputChannels);
                this.WasapiDevice.InitRecordAndPlayback(MasterWaveProvider, inputChannels, sampleRate);

                //get actual samplerate
                sampleRate = WasapiDevice.Output.OutputWaveFormat.SampleRate;
                MasterWaveProvider.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, outputChannels);

                //register for recording
                recordingBuffers = new float[WasapiDevice.DriverInputChannelCount][];
                tempInputBuffers = new float[WasapiDevice.DriverInputChannelCount][];
                wasapiInputBuffers.Clear();
                for (int i = 0; i < recordingBuffers.Length; i++)
                {
                    recordingBuffers[i] = new float[1];
                    tempInputBuffers[i] = new float[1];
                    wasapiInputBuffers.Add(new CircularBufferWasapi(3));

                }
                WasapiDevice.Input.DataAvailable += WasapiAudioAvailable;
                

                Settings.SampleRate = sampleRate;
                Timer.SampleRate = sampleRate;

                Settings.SampleRate = sampleRate;

                NeedsReset = false;

                CurrentDevice = WasapiDevice.Output;
                CurrentDriverName = WasapiPrefix + driverName;

                driverInitialized = true;

                SettingsChanged.OnNext(driverName);
            }
        }

        private static void AnalyzeWASAPIDevices(string driverName, string wasapiRecordingName, out MMDevice inputDevice, out MMDevice outputDevice, out bool isLoopback)
        {
            var mmDeviceEnumerator = new MMDeviceEnumerator();
            var allEndpoints = mmDeviceEnumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active);
            var renderDevices = allEndpoints.Where(d => d.DataFlow == DataFlow.Render);
            var captureDevices = allEndpoints.Where(d => d.DataFlow == DataFlow.Capture);

            //find output device
            if (driverName == WasapiSystemDevice)
            {
                if (mmDeviceEnumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    outputDevice = mmDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    driverName = outputDevice.FriendlyName;
                }
                else
                {
                    outputDevice = renderDevices.FirstOrDefault();
                    driverName = outputDevice?.FriendlyName;
                }
            }
            else
            {
                outputDevice = renderDevices.FirstOrDefault(d => d.FriendlyName == driverName);
            }

            //find input device
            isLoopback = false;
            if (wasapiRecordingName?.StartsWith(WasapiLoopbackPrefix) ?? false)
            {
                isLoopback = true;
                wasapiRecordingName = wasapiRecordingName.Replace(WasapiLoopbackPrefix, "");
            }

            if (wasapiRecordingName == WasapiSystemDevice)
            {
                var dataFlow = isLoopback ? DataFlow.Render : DataFlow.Capture;
                if (mmDeviceEnumerator.HasDefaultAudioEndpoint(dataFlow, Role.Multimedia))
                {
                    inputDevice = mmDeviceEnumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia);
                    wasapiRecordingName = inputDevice.FriendlyName;
                }
                else
                {
                    inputDevice = (isLoopback ? renderDevices : captureDevices).FirstOrDefault();
                    wasapiRecordingName = inputDevice?.FriendlyName;
                }
            }
            else
            {
                inputDevice = (isLoopback ? renderDevices : captureDevices).FirstOrDefault(d => d.FriendlyName == wasapiRecordingName);
            }
        }

        float[][] tempInputBuffers;
        public List<CircularBufferWasapi> wasapiInputBuffers = new List<CircularBufferWasapi>();

        private void WasapiAudioAvailable(object sender, WaveInEventArgs e)
        {
            if (RecordingRequestedStack.Count <= 0)
                return; // no input needed

            var bytes = e.BytesRecorded;
            var bytesPerSample = WasapiDevice.Input.WaveFormat.BitsPerSample / 8;
            var channels = WasapiDevice.Input.WaveFormat.Channels;
            var samples = bytes / (channels * bytesPerSample);

            if (tempInputBuffers[0].Length < samples)
            {
                for (int i = 0; i < channels; i++)
                {
                    tempInputBuffers[i] = new float[samples];
                    wasapiInputBuffers[i] = new CircularBufferWasapi(16384);
                }
            }

            //fill and convert buffers
            GetInputBuffersWasapi(tempInputBuffers, samples, e);

            for (int i = 0; i < channels; i++)
            {
                wasapiInputBuffers[i].Write(tempInputBuffers[i], 0, samples);
            }
        }

        WaveBuffer inputWaveBuffer = new WaveBuffer(1);
        public int GetInputBuffersWasapi(float[][] samples, int sampleCount, WaveInEventArgs e)
        {
            int channels = WasapiDevice.DriverInputChannelCount;
            unsafe
            {
                //if (e.AsioSampleType == AsioSampleType.Int32LSB)
                //{
                //    for (int ch = 0; ch < channels; ch++)
                //    {
                //        for (int n = 0; n < e.SamplesPerBuffer; n++)
                //        {
                //            samples[ch][n] = *((int*)e.InputBuffers[ch] + n) / (float)Int32.MaxValue;
                //        }
                //    }
                //}
                //else if (e.AsioSampleType == AsioSampleType.Int16LSB)
                //{
                //    for (int ch = 0; ch < channels; ch++)
                //    {
                //        for (int n = 0; n < e.SamplesPerBuffer; n++)
                //        {
                //            samples[ch][n] = *((short*)e.InputBuffers[ch] + n) / (float)Int16.MaxValue;
                //        }
                //    }
                //}
                //else if (e.AsioSampleType == AsioSampleType.Int24LSB)
                //{
                //    for (int ch = 0; ch < channels; ch++)
                //    {
                //        for (int n = 0; n < e.SamplesPerBuffer; n++)
                //        {
                //            byte* pSample = ((byte*)e.InputBuffers[ch] + n * 3);

                //            //int sample = *pSample + *(pSample+1) << 8 + (sbyte)*(pSample+2) << 16;
                //            int sample = pSample[0] | (pSample[1] << 8) | ((sbyte)pSample[2] << 16);
                //            samples[ch][n] = sample / 8388608.0f;
                //        }
                //    }
                //}
                //else
                if (WasapiDevice.Input.WaveFormat.BitsPerSample == 32 && WasapiDevice.Input.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    inputWaveBuffer.BindTo(e.Buffer);
                    var floatBuffer = inputWaveBuffer.FloatBuffer;
                    for (int ch = 0; ch < channels; ch++)
                    {
                        for (int n = 0; n < sampleCount; n++)
                        {
                            samples[ch][n] = floatBuffer[n * channels + ch];
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException("Input Format Not Supported: " + WasapiDevice.Input.WaveFormat);
                }
            }
            return sampleCount * channels;
        }
    }
}