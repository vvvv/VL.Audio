
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
        //this mixes multiple sample providers from the graph to a waveprovider which is set to
        MasterWaveProvider MasterWaveProvider;

        //the driver wrapper
        bool driverInitialized = false;
        public AsioOut AsioDevice;
        public WasapiInOut WasapiDevice;
        public IWavePlayer CurrentDevice;

        public const string ASIOPrefix = "ASIO: ";
        public const string WasapiPrefix = "WASAPI: ";
        public const string WasapiSystemDevice = "Current System Device";
        public const string WasapiLoopbackPrefix = "Loopback: ";
        
        public string CurrentDriverName = WasapiPrefix + WasapiSystemDevice;
        public string CurrentWasapiInputName = WasapiPrefix + WasapiSystemDevice;
        public int CurrentDesiredInputChannels = 2;
        public int CurrentDesiredOutputChannels = 2;

        /// <summary>
        /// The buffer number that is requested, can be used by signals to check whether the current buffer was filled already.
        /// </summary>
        public ulong BufferNumber;

        internal AudioEngine()
        {
            Settings = new AudioEngineSettings { SampleRate = 44100, BufferSize = 512 };
            Timer = new AudioEngineTimer(Settings.SampleRate);
            var format = WaveFormat.CreateIeeeFloatWaveFormat(Settings.SampleRate, 1);
            MasterWaveProvider = new MasterWaveProvider(format, OnStartedReading, OnFinishedReading);
        }

        private void OnStartedReading(int samples)
        {
            Settings.BufferSize = samples;

            //lock(FTimerLock) //needed?
            {
                Timer.Progress(samples);
            }

            //copy wasapi input buffer into recording buffer of correct size
            if (RecordingRequestedStack.Count > 0 && WasapiDevice != null)
            {
                var channels = WasapiDevice.Input.WaveFormat.Channels;

                //create buffers if neccessary
                if (recordingBuffers[0].Length < samples)
                {
                    for (int i = 0; i < channels; i++)
                    {
                        recordingBuffers[i] = new float[samples];
                    }
                }

                for (int i = 0; i < channels; i++)
                {
                    wasapiInputBuffers[i].ReadFromLastPosition(recordingBuffers[i], 0, samples);
                }
            }
        }

        public AudioEngineSettings Settings
        {
            get;
            private set;
        }
        
        private object FTimerLock = new object();
        public AudioEngineTimer Timer
        {
            get;
            private set;
        }
        
        private bool FPlay;
        public bool Play
        {
            set
            {
                FPlay = value;
                if (FPlay) CurrentDevice.Play();
                else CurrentDevice.Pause();
            }
            
            get
            {
                return FPlay;
            }
            
        }
        
        public void Stop()
        {
            CurrentDevice.Stop();
        }

        //tells the subscribers to prepare for the next frame
        public event EventHandler FinishedReading;
        
        protected void OnFinishedReading(int calledSamples)
        {
            FinishedReading?.Invoke(this, new EventArgs());

            BufferNumber++;
        }

        //add/remove outputs
        public void AddOutput(IEnumerable<MasterChannel> provider)
        {
            if(provider != null)
                foreach(var p in provider)
                    MasterWaveProvider.Add(p);
        }
        
        public void RemoveOutput(IEnumerable<MasterChannel> provider)
        {
            if(provider != null)
                foreach(var p in provider)
                    MasterWaveProvider.Remove(p);
        }
        
        //add/remove sinks
        public void AddSink(IAudioSink sink)
        {
            if (sink != null)
                MasterWaveProvider.AddSink(sink);
            
            System.Diagnostics.Debug.WriteLine("Sink Added: " + sink.GetType());
        }
        
        public void RemoveSink(IAudioSink sink)
        {
            if (sink != null)
                MasterWaveProvider.RemoveSink(sink);

            Debug.WriteLine("Sink Removed: " + sink.GetType());
        }

        public IObservable<string> DriverSettingsChanged => SettingsChanged;
        private Subject<string> SettingsChanged = new Subject<string>();

        private string FLastError;
        /// <summary>
        /// Shows the last error (if any) that happened when changing driver settings
        /// </summary>
        public string LastError => FLastError;

        private bool FDriverIsDefaultSelection;
        /// <summary>
        /// Whether or not the current driver is chosen by the engine because the "Default" driver was requested
        /// </summary>
        public bool DriverIsDefaultSelection => FDriverIsDefaultSelection;

        private bool FWasapiInputIsDefaultSelection;
        /// <summary>
        /// Whether or not the current wasapi input is chosen by the engine because the "Default" input was requested
        /// </summary>
        public bool WasapiInputIsDefaultSelection => FWasapiInputIsDefaultSelection;

        public void ValidateSelections(ref string driverName, ref string wasapiRecordingName)
        {
            var drivers = AudioDeviceDefinition.Instance;
            var wasapiInputs = WasapiInputDeviceDefinition.Instance;

            //make sure selected driver is currently available
            if (!drivers.Entries.Contains(driverName))
                driverName = AudioDevice.DefaultEntry;

            if (!wasapiInputs.Entries.Contains(wasapiRecordingName))
                wasapiRecordingName = WasapiInputDevice.DefaultEntry;

            //if "Default" is selected, make an actual choice
            FDriverIsDefaultSelection = driverName == AudioDevice.DefaultEntry;
            if (FDriverIsDefaultSelection)
                driverName = drivers.GetDefaultDriver();

            FWasapiInputIsDefaultSelection = wasapiRecordingName == WasapiInputDevice.DefaultEntry;
            if (FWasapiInputIsDefaultSelection)
                wasapiRecordingName = wasapiInputs.GetDefaultDriver();
        }

        /// <summary>
        /// Initialize the driver in order to be able to read its SampleRate options
        /// </summary>
        /// <param name="driverName"></param>
        /// <param name="wasapiRecordingName"></param>
        public void PreviewDriver(string driverName, string wasapiRecordingName)
        {
            FLastError = "";

            ValidateSelections(ref driverName, ref wasapiRecordingName);

            try
            {
                if (driverName.StartsWith(WasapiPrefix))
                {
                    driverName = driverName.Replace(WasapiPrefix, "");

                    if (string.IsNullOrWhiteSpace(wasapiRecordingName))
                        wasapiRecordingName = WasapiSystemDevice;

                    PreviewWASAPIDriver(driverName, wasapiRecordingName);
                }
                else
                {
                    driverName = driverName.Replace(ASIOPrefix, "");
                    PreviewASIODriver(driverName);
                }
            }
            catch (Exception e)
            {
                FLastError = e.Message;
            }
        }

        /// <summary>
        /// Initializes the Audio Driver if necessary
        /// </summary>
        /// <param name="driverName"></param>
        /// <param name="wasapiRecordingName"></param>
        /// <param name="sampleRate"></param>
        /// <param name="inputChannels"></param>
        /// <param name="inputChannelOffset"></param>
        /// <param name="outputChannels"></param>
        /// <param name="outputChannelOffset"></param>
        public void ChangeDriverSettings(string driverName, string wasapiRecordingName, int sampleRate, int inputChannels, int inputChannelOffset, int outputChannels, int outputChannelOffset)
        {
            CurrentDesiredInputChannels = inputChannels;
            CurrentDesiredOutputChannels = outputChannels;

            FLastError = "";

            ValidateSelections(ref driverName, ref wasapiRecordingName);

            try
            {
                if (driverName.StartsWith(WasapiPrefix))
                {
                    driverName = driverName.Replace(WasapiPrefix, "");

                    if (string.IsNullOrWhiteSpace(wasapiRecordingName))
                        wasapiRecordingName = WasapiSystemDevice;

                    ChangeWASAPIDriverSettings(driverName, wasapiRecordingName, sampleRate, inputChannels, inputChannelOffset, outputChannels, outputChannelOffset);
                }
                else
                {
                    driverName = driverName.Replace(ASIOPrefix, "");
                    ChangeASIODriverSettings(driverName, sampleRate, inputChannels, inputChannelOffset, outputChannels, outputChannelOffset);
                }
            }
            catch (Exception e)
            {
                FLastError = e.Message;
            }
        }

        public bool IsSampleRateSupported(int sampleRate)
        {
            if (CurrentDevice is AsioOut asioOut)
            {
                return asioOut.IsSampleRateSupported(sampleRate);
            }
            else if (CurrentDevice is WasapiOut wasapiOut)
            {
                return wasapiOut.OutputWaveFormat.SampleRate == sampleRate;
            }
            return false;
        }

        public void GetSupportedChannels(out int inputChannels, out int outputChannels)
        {
            inputChannels = 2;
            outputChannels = 2;

            if (CurrentDevice is AsioOut asioOut)
            {
                inputChannels = asioOut.DriverInputChannelCount;
                outputChannels = asioOut.DriverOutputChannelCount;
            }
            else if (CurrentDevice is WasapiOut wasapiOut)
            {
                inputChannels = WasapiDevice.DriverInputChannelCount;
                outputChannels = wasapiOut.OutputWaveFormat.Channels;
            }
        }

        /// <summary>
        /// If this is true the engine driver should be reset
        /// </summary>
        public bool NeedsReset
        {
            get;
            private set;
        }

        //audio input
        protected float[][] recordingBuffers;

        /// <summary>
        /// The buffers from the audio input
        /// </summary>
        public float[][] InputBuffers
        {
            get
            {
                return recordingBuffers;
            }
        }

        public Stack<object> RecordingRequestedStack = new Stack<object>();
        public int SamplesCounter = 0;

        //close
        public void Dispose()
        {
            Cleanup();
        }
        
        //close ASIO
        private void Cleanup()
        {
            if (this.AsioDevice != null)
            {
                this.AsioDevice.DriverResetRequest -= AsioOut_DriverResetRequest;
                this.AsioDevice.AudioAvailable -= AsioAudioAvailable;
                this.AsioDevice.Dispose();
                this.AsioDevice = null;
            }
            if (this.WasapiDevice != null)
            {
                if (this.WasapiDevice.Input != null)
                    this.WasapiDevice.Input.DataAvailable -= WasapiAudioAvailable;
                this.WasapiDevice.Dispose();
                this.WasapiDevice = null;
            }

            CurrentDevice = null;
        }
    }
    
    public class BufferEventArgs : EventArgs
    {
        public float[] Buffer
        {
            get;
            set;
        }
        
        public string BufferName
        {
            get;
            set;
        }
    }
    
    public class BufferDictionary : Dictionary<string, float[]>
    {
        public BufferDictionary()
        {
            
        }
        
        public void SetBuffer(string key, float[] buffer)
        {
            this[key] = buffer;

            BufferSet?.Invoke(this, new BufferEventArgs { Buffer = this[key], BufferName = key });
        }
        
        public void RemoveBuffer(string key)
        {
            var buffer = this[key];
            this.Remove(key);

            BufferRemoved?.Invoke(this, new BufferEventArgs { Buffer = buffer, BufferName = key });
        }
        
        public event EventHandler<BufferEventArgs> BufferSet;
        public event EventHandler<BufferEventArgs> BufferRemoved;
    }
}