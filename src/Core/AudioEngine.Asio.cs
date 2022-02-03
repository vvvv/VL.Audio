
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
        private void PreviewASIODriver(string driverName)
        {
            if (AsioDevice == null || AsioDevice.DriverName != driverName)
            {
                //dispose device if necessary
                Cleanup();

                //create new driver
                AsioDevice = new AsioOut(driverName);

                CurrentDevice = AsioDevice;
                driverInitialized = false;

                //trigger to update the dynamic SampleRate enum
                SettingsChanged.OnNext(driverName);
            }
        }

        private void ChangeASIODriverSettings(string driverName, int sampleRate, int inputChannels, int inputChannelOffset, int outputChannels, int outputChannelOffset)
        {
            if (!driverInitialized || AsioDevice == null || AsioDevice.DriverName != driverName
                           || MasterWaveProvider.WaveFormat.SampleRate != sampleRate
                           || AsioDevice.NumberOfInputChannels != inputChannels
                           || AsioDevice.InputChannelOffset != inputChannelOffset
                           || AsioDevice.NumberOfOutputChannels != outputChannels
                           || AsioDevice.ChannelOffset != outputChannelOffset)
            {
                //dispose device if necessary
                Cleanup();

                //create new driver
                this.AsioDevice = new AsioOut(driverName);

                //set channel offset
                AsioDevice.ChannelOffset = outputChannelOffset;
                AsioDevice.InputChannelOffset = inputChannelOffset;

                //init driver
                MasterWaveProvider.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, outputChannels);
                this.AsioDevice.InitRecordAndPlayback(MasterWaveProvider, inputChannels, sampleRate);

                //register for recording
                FRecordBuffers = new float[AsioDevice.DriverInputChannelCount][];
                for (int i = 0; i < FRecordBuffers.Length; i++)
                {
                    FRecordBuffers[i] = new float[512];
                }
                this.AsioDevice.AudioAvailable += AsioAudioAvailable;

                Settings.SampleRate = sampleRate;
                Settings.BufferSize = AsioDevice.FramesPerBuffer;
                Timer.SampleRate = sampleRate;
                Timer.FillBeatBuffer(AsioDevice.FramesPerBuffer);

                this.AsioDevice.DriverResetRequest += AsioOut_DriverResetRequest;

                this.Settings.SampleRate = sampleRate;

                NeedsReset = false;

                CurrentDevice = AsioDevice;
                CurrentDriverName = ASIOPrefix + driverName;

                driverInitialized = true;

                SettingsChanged.OnNext(driverName);
            }
        }

        protected void AsioAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            if (RecordingRequestedStack.Count <= 0)
                return;
                
            //create buffers if neccessary
            if (FRecordBuffers[0].Length != e.SamplesPerBuffer)
            {
                for (int i = 0; i < FRecordBuffers.Length; i++)
                {
                    FRecordBuffers[i] = new float[e.SamplesPerBuffer];
                }
            }
            
            //fill and convert buffers
            GetInputBuffersAsio(FRecordBuffers, e);

            SamplesCounter += e.SamplesPerBuffer;
        }

        void AsioOut_DriverResetRequest(object sender, EventArgs e)
        {
            NeedsReset = true;
        }


        /// <summary>
        /// Converts all the recorded audio into a buffer of 32 bit floating point samples
        /// </summary>
        /// <samples>The samples as 32 bit floating point, interleaved</samples>
        public int GetInputBuffersAsio(float[][] samples, AsioAudioAvailableEventArgs e)
        {
            int channels = e.InputBuffers.Length;
            unsafe
            {
                if (e.AsioSampleType == AsioSampleType.Int32LSB)
                {
                    for (int ch = 0; ch < channels; ch++)
                    {
                        for (int n = 0; n < e.SamplesPerBuffer; n++)
                        {
                            samples[ch][n] = *((int*)e.InputBuffers[ch] + n) / (float)Int32.MaxValue;
                        }
                    }
                }
                else if (e.AsioSampleType == AsioSampleType.Int16LSB)
                {
                    for (int ch = 0; ch < channels; ch++)
                    {
                        for (int n = 0; n < e.SamplesPerBuffer; n++)
                        {
                            samples[ch][n] = *((short*)e.InputBuffers[ch] + n) / (float)Int16.MaxValue;
                        }
                    }
                }
                else if (e.AsioSampleType == AsioSampleType.Int24LSB)
                {
                    for (int ch = 0; ch < channels; ch++)
                    {
                        for (int n = 0; n < e.SamplesPerBuffer; n++)
                        {
                            byte *pSample = ((byte*)e.InputBuffers[ch] + n * 3);

                            //int sample = *pSample + *(pSample+1) << 8 + (sbyte)*(pSample+2) << 16;
                            int sample = pSample[0] | (pSample[1] << 8) | ((sbyte)pSample[2] << 16);
                            samples[ch][n] = sample / 8388608.0f;
                        }
                    }
                }
                else if (e.AsioSampleType == AsioSampleType.Float32LSB)
                {
                    for (int ch = 0; ch < channels; ch++)
                    {
                        for (int n = 0; n < e.SamplesPerBuffer; n++)
                        {
                            samples[ch][n] = *((float*)e.InputBuffers[ch] + n);
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException(string.Format("ASIO Sample Type {0} not supported", e.AsioSampleType));
                }
            }
            return e.SamplesPerBuffer*channels;
        }
    }
}