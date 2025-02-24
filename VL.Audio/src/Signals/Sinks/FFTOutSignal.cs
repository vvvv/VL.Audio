using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
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
            return entries;
        }

        protected override IObservable<object> GetEntriesChangedObservable()
        {
            //e.g.: return HardwareChangedEvents.HardwareChanged; //reports device (e.g. usb) addition/removal
            return Observable.Empty<object>();
        }

        protected override bool AutoSortAlphabetically => false;
    }

    public class FFTOutSignal : SinkSignal
    {
        protected CircularBuffer FRingBuffer = new CircularBuffer(512);

        private bool bufferReady = false;

        private void OnBufferReady(float[] buffer)
        {
            bufferReady = true;
        }

        public FFTOutSignal(AudioSignal input)
        {
            InputSignal.Value = input;
            FRingBuffer.BufferFilled = OnBufferReady;
        }

        public int Size
        {
            get
            {
                return FRingBuffer.Size;
            }
            set
            {
                bufferReady = false;
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
                    FFTOutInternal = new float[fftSize/2];
                    FWindow = AudioUtils.CreateWindowDouble(fftSize, WindowFunc);
                }

                //write to buffer
                FRingBuffer.Write(buffer, offset, count);
            }
        }

        public float[] CalcFFT()
        {
            int fftSize = FRingBuffer.Size;
            if(!bufferReady)
            {
                return new float[fftSize];
            }
            FRingBuffer.ReadDoubleWindowed(FFFTBuffer, FWindow, 0, fftSize);

            var complex = new Span<Complex>(FFFTComplexBuffer);
            Transform.MakeComplex(complex, new Span<double>(FFFTBuffer));

            // Calculate the FFT as an array of complex numbers
            Transform.FFT(complex);

            var halfSize = fftSize/2;
            FFTOutInternal[0] = 0;
            for (int n = 1; n < halfSize; n++)
            {
                var lastValue = FFTOutInternal[n];
                var newValue = (float)Decibels.LinearToDecibels(Math.Max(complex[n].MagnitudeSquared, FMindB)) / FdBRange + 1;
                FFTOutInternal[n] = newValue * (1 - Smoothing) + lastValue * Smoothing;
            }

            return FFTOutInternal;
        }
    }
}




