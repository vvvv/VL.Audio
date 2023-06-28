﻿using System;
using System.Runtime.InteropServices;
using FftSharp;
using NAudio.Utils;

namespace VL.Audio
{
    public enum FFTWindowFunction
    {
        None,
        Hamming,
        Hann,
        BlackmannHarris
    }

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
        public double[] FFTOutRealVal = new double[2];
        public double[] FFTOutImagVal = new double[2];
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
                    FFTOutRealVal = new double[fftSize / 2];
                    FFTOutImagVal = new double[fftSize / 2];
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
                FFTOutRealVal[n] = complex[n].Real;
                FFTOutImagVal[n] = complex[n].Imaginary;
            }
        }
    }
}




