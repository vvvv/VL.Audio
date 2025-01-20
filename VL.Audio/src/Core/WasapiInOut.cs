#region usings
using System;

using NAudio.Wave;
using NAudio.CoreAudioApi;


#endregion usings

namespace VL.Audio
{
    public class WasapiInOut : IDisposable
    {
        public WasapiOut Output;
        public WasapiCapture Input;
        public MMDevice MMOutDevice;
        public MMDevice MMInDevice;

        private bool IsLoopback;

        public bool DeviceAvailable { get; }
        public bool OutputInitialized { get; private set; }
        public bool InputInitialized { get; private set; }

        public int DriverOutputChannelCount => Output? .OutputWaveFormat.Channels ?? 0;
        public int DriverInputChannelCount => Input?.WaveFormat.Channels ?? 0;
        public ISampleProvider InputSampleProvider { get; private set; }

        public WasapiInOut(MMDevice outputDevice, MMDevice inputDevice, bool isLoopback)
        {
            MMOutDevice = outputDevice;
            MMInDevice = inputDevice;
            IsLoopback = isLoopback;

            SetupDevices();
        }

        internal void SetupDevices()
        {
            if (MMInDevice != null)
            {
                var minPeriod = (int)Math.Ceiling(MMInDevice.AudioClient.MinimumDevicePeriod / 10000.0);
                Input = IsLoopback ? new VAudioWasapiLoopbackCapture(MMInDevice, true, minPeriod) : new WasapiCapture(MMInDevice, true, minPeriod);
            }

            if (MMOutDevice != null)
            {
                var minPeriod = (int)Math.Ceiling(MMOutDevice.AudioClient.MinimumDevicePeriod / 10000.0);
                Output = new WasapiOut(MMOutDevice, AudioClientShareMode.Shared, true, minPeriod);
            }
        }

        internal void InitRecordAndPlayback(MasterWaveProvider masterWaveProvider, int inputChannels, int sampleRate)
        {
            OutputInitialized = false;
            InputInitialized = false;

            try
            {
                Output?.Init(masterWaveProvider);
                OutputInitialized = Output != null;
            }
            catch (Exception)
            {
                Output?.Dispose();
            }

            try
            {
                Input?.StartRecording();
                InputInitialized = Input != null;
            }
            catch (Exception)
            {
                Input?.Dispose();
            }
        }

        public void Dispose()
        {
            Output?.Dispose();
            Input?.Dispose();
        }
    }
}