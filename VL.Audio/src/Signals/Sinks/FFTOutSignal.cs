using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Threading;
using FftSharp;
using NAudio.Utils;
using VL.Core.CompilerServices;
using VL.Lib.Collections;

namespace VL.Audio
{
    public enum FFTWindowFunction
    {
        None,
        Hamming,
        Hann,
        BlackmannHarris
    }

    [Serializable]
    public class FFTBinCountEnum : DynamicEnumBase<FFTBinCountEnum, FFTBinCountEnumDefinition>
    {
        public FFTBinCountEnum(string value) : base(value)
        {
        }

        [CreateDefault]
        public static FFTBinCountEnum CreateDefault()
        {
            return CreateDefaultBase();
        }
    }

    public class FFTBinCountEnumDefinition : DynamicEnumDefinitionBase<FFTBinCountEnumDefinition>
    {
        //Return the current enum entries
        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var entries = new Dictionary<string, object>();
            entries.Add("64", 64);
            entries.Add("128", 128);
            entries.Add("256", 256);
            entries.Add("512", 512);
            entries.Add("1024", 1024);
            entries.Add("2048", 2048);
            entries.Add("4096", 4096);
            entries.Add("8192", 8192);
            entries.Add("16384", 16384);
            entries.Add("32768", 32768);
            return entries;
        }

        protected override IObservable<object> GetEntriesChangedObservable()
        {
            //e.g.: return HardwareChangedEvents.HardwareChanged; //reports device (e.g. usb) addition/removal
            return Observable.Empty<object>();
        }

        protected override bool AutoSortAlphabetically => false;
    }

    [Obsolete("Use FFTOutSignalThreaded instead")]
    public class FFTOutSignal : SinkSignal
    {
        protected CircularBuffer FRingBuffer = new CircularBuffer(512);

        public FFTOutSignal(AudioSignal input)
        {
            InputSignal.Value = input;
            FRingBuffer.BufferFilled = CalcFFT;
        }

        public int Size
        {
            get
            {
                return FRingBuffer.Size;
            }
            set
            {
                FRingBuffer.Size = value;
            }
        }

        public float DBRange
        {
            get => FdBRange;
            set
            {
                FdBRange = value;
                FMindB = (float)Decibels.DecibelsToLinear(-FdBRange);
            }
        }

        float FMindB = (float)Decibels.DecibelsToLinear(-120);

        public float Smoothing { get; set; }

        WindowFunction windowFunc;

        public WindowFunction WindowFunc
        {
            get
            {
                return windowFunc;
            }
            set
            {
                if (windowFunc != value)
                {
                    windowFunc = value;
                    FWindow = AudioUtils.CreateWindowDouble(FRingBuffer.Size, WindowFunc);
                }
            }
        }

        double[] FFFTBuffer = new double[1];
        Complex[] FFFTComplexBuffer = new Complex[1];
        public float[] FFTOut = new float[2];
        double[] FWindow = new double[1];
        private float FdBRange;

        protected override void FillBuffer(float[] buffer, int offset, int count)
        {
            if (InputSignal.Value != null)
            {
                InputSignal.Read(buffer, offset, count);

                //calc fft
                var fftSize = FRingBuffer.Size;

                if (FFFTBuffer.Length != fftSize)
                {
                    FFFTBuffer = new double[fftSize];
                    FFFTComplexBuffer = new Complex[fftSize];
                    FFTOut = new float[fftSize/2];
                    FWindow = AudioUtils.CreateWindowDouble(fftSize, WindowFunc);
                }

                //write to buffer
                FRingBuffer.Write(buffer, offset, count);
            }
        }

        void CalcFFT(float[] ringbufferData)
        {
            var fftSize = FRingBuffer.Size;
            FRingBuffer.ReadDoubleWindowed(FFFTBuffer, FWindow, 0, fftSize);

            var complex = new Span<Complex>(FFFTComplexBuffer);
            Transform.MakeComplex(complex, new Span<double>(FFFTBuffer));

            // Calculate the FFT as an array of complex numbers
            Transform.FFT(complex);

            var halfSize = fftSize/2;
            FFTOut[0] = 0;
            for (int n = 1; n < halfSize; n++)
            {
                var lastValue = FFTOut[n];
                var newValue = (float)Decibels.LinearToDecibels(Math.Max(complex[n].MagnitudeSquared, FMindB)) / FdBRange + 1;
                FFTOut[n] = newValue * (1 - Smoothing) + lastValue * Smoothing;
            }
        }
    }

    public class FFTOutSignalThreaded : SinkSignal
    {
        protected CircularBufferThreadSafe FRingBuffer = new CircularBufferThreadSafe(512);
        private volatile bool bufferReady = false;

        private AutoResetEvent processingTrigger = new AutoResetEvent(false);
        private Thread processingThread;
        private bool stopFlag = false;

        private void OnBufferReady(float[] buffer)
        {
            bufferReady = true;
        }

        private void OnNewData(float[] data)
        {
            if (ContinuouslyCalculate)
            {
                processingTrigger.Set();
            }
        }

        public FFTOutSignalThreaded(AudioSignal input)
        {
            InputSignal.Value = input;
            FRingBuffer.BufferFilled = OnBufferReady;
            FRingBuffer.WriteCompleted = OnNewData;
            processingThread = new Thread(() =>
            {
                while (true)
                {
                    if (processingTrigger.WaitOne())
                    {
                        if (stopFlag) return;
                        CalcFFT();
                    }
                }
            });
            processingThread.Start();
        }

        public override void Dispose()
        {
            stopFlag = true;
            processingTrigger.Set();
            base.Dispose();
        }

        public int Size
        {
            get
            {
                return FRingBuffer.Size;
            }
            set
            {
                bufferReady = false; // Resizing the buffer means there's no longer enough data
                FRingBuffer.Size = value;
            }
        }

        public float DBRange
        {
            get => FdBRange;
            set
            {
                FdBRange = value;
                FMindB = (float)Decibels.DecibelsToLinear(-FdBRange);
            }
        }

        float FMindB = (float)Decibels.DecibelsToLinear(-120);

        public float Smoothing { get; set; }
        public bool ContinuouslyCalculate { get; set; } = false; // Either calculate on request, or as soon as new data is available

        WindowFunction windowFunc;

        public WindowFunction WindowFunc
        {
            get
            {
                return windowFunc;
            }
            set
            {
                if (windowFunc != value)
                {
                    windowFunc = value;
                    FWindow = AudioUtils.CreateWindowDouble(FRingBuffer.Size, WindowFunc);
                }
            }
        }

        double[] FFFTBuffer = new double[1];
        Complex[] FFFTComplexBuffer = new Complex[1];
        float[] FFTOutThreadInternal = new float[2];
        float[] FFTOutInternal = new float[2];
        double[] FWindow = new double[1];
        private float FdBRange;

        protected override void FillBuffer(float[] buffer, int offset, int count)
        {
            if (InputSignal.Value != null)
            {
                InputSignal.Read(buffer, offset, count);

                //calc fft
                var fftSize = FRingBuffer.Size;

                if (FFFTBuffer.Length != fftSize)
                {
                    bufferReady = false;
                    FFFTBuffer = new double[fftSize];
                    FFFTComplexBuffer = new Complex[fftSize];
                    FFTOutInternal = new float[fftSize / 2];
                    FFTOutThreadInternal = new float[fftSize / 2];
                    FWindow = AudioUtils.CreateWindowDouble(fftSize, WindowFunc);
                }

                //write to buffer
                FRingBuffer.Write(buffer, offset, count);
            }
        }

        // This is run by the processing thread
        private void CalcFFT()
        {
            int fftSize = FRingBuffer.Size;
            if (!bufferReady)
            {
                return;
            }
            FRingBuffer.ReadDouble(FFFTBuffer, 0, fftSize);
            for (int i = 0; i < FFFTBuffer.Length; i++)
            {
                FFFTBuffer[i] *= FWindow[i];
            }

            var complex = new Span<Complex>(FFFTComplexBuffer);
            Transform.MakeComplex(complex, new Span<double>(FFFTBuffer));

            // Calculate the FFT as an array of complex numbers
            Transform.FFT(complex);

            var halfSize = fftSize / 2;
            FFTOutThreadInternal[0] = 0;
            for (int n = 1; n < halfSize; n++)
            {
                var lastValue = FFTOutThreadInternal[n];
                var newValue = (float)Decibels.LinearToDecibels(Math.Max(complex[n].MagnitudeSquared, FMindB)) / FdBRange + 1;
                FFTOutThreadInternal[n] = newValue * (1 - Smoothing) + lastValue * Smoothing;
            }
            lock (FFTOutInternal)
            {
                Array.Copy(FFTOutThreadInternal, FFTOutInternal, halfSize);
            }
        }

        public float[] GetFFT()
        {
            if (!ContinuouslyCalculate) processingTrigger.Set(); // Request new data
            lock (FFTOutInternal)
            {
                return FFTOutInternal;
            }
        }
    }
}




