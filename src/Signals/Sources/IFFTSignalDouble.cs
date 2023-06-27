/*
 * Created by SharpDevelop.
 * User: TF
 * Date: 02.05.2015
 * Time: 12:09
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using Lomont;
using VL.Lib.Collections;

namespace VL.Audio
{
    public class IFFTPullBufferDouble : CircularPullBuffer
    {
        public IFFTPullBufferDouble(SigParam<IReadOnlyList<double>> fftDataReal, SigParam<IReadOnlyList<double>> fftDataImag, int size, WindowFunction window)
            : base(size)
        {
            FFFTDataReal = fftDataReal;
            FFFTDataImag = fftDataImag;
            RealImagData = new double[size];
            TimeDomain = new float[size];
            Window = AudioUtils.CreateWindowDouble(size, window);
            WindowFunc = window;
            WindowFunc = window;
            PullCount = size;
        }

        readonly SigParam<IReadOnlyList<double>> FFFTDataReal;
        readonly SigParam<IReadOnlyList<double>> FFFTDataImag;
        readonly double[] RealImagData;
        readonly double[] Window;
        public readonly WindowFunction WindowFunc;
        readonly float[] TimeDomain;
        readonly LomontFFT FFFT = new LomontFFT();

        public double Gain = 1;

        public override void Pull(int count)
        {
            var real = FFFTDataReal.Value ?? Spread<double>.Empty;
            var imag = FFFTDataImag.Value ?? Spread<double>.Empty;

            var copyCount = Math.Min(count / 2, Math.Max(real.Count, imag.Count));
            var j = 0;

            if (real.Count > imag.Count)
            {
                if (imag.Count == 0)
                {
                    for (int i = 0; i < copyCount; i++)
                    {
                        RealImagData[j++] = real[i];
                        RealImagData[j++] = 0.0;
                    }
                }
                else
                {
                    for (int i = 0; i < copyCount; i++)
                    {
                        RealImagData[j++] = real[i];
                        RealImagData[j++] = imag[AudioUtils.Zmod(i, imag.Count)];
                    }
                }
            }
            else if(real?.Count == imag?.Count)
            {
                for (int i = 0; i < copyCount; i++)
                {
                    RealImagData[j++] = real[i];
                    RealImagData[j++] = imag[i];
                }
            }
            else
            {
                if (real.Count == 0)
                {
                    for (int i = 0; i < copyCount; i++)
                    {
                        RealImagData[j++] = 0.0;
                        RealImagData[j++] = imag[i];
                    }
                }
                else
                {
                    for (int i = 0; i < copyCount; i++)
                    {
                        RealImagData[j++] = real[AudioUtils.Zmod(i, real.Count)];
                        RealImagData[j++] = imag[i];
                    }
                }
            }

            if(copyCount < count/2)
            {

                for (int i = copyCount; i < count/2; i++)
                {
                    RealImagData[j++] = 0;
                    RealImagData[j++] = 0;
                }
            }

            FFFT.RealFFT(RealImagData, false);

            //RealImagData[0] = RealImagData[1];

            TimeDomain.WriteDoubleWindowed(RealImagData, Window, 0, count, Gain);

                Write(TimeDomain, 0, count);
        }
    }

    /// <summary>
    /// Description of IFFTSignal.
    /// </summary>
    public class IFFTSignalDouble : AudioSignal
    {
        SigParam<IReadOnlyList<double>> FFTDataReal = new SigParam<IReadOnlyList<double>>("FFT Data Real");
        SigParam<IReadOnlyList<double>> FFTDataImag = new SigParam<IReadOnlyList<double>>("FFT Data Imaginary");
        SigParam<WindowFunction> FWindowFunc = new SigParam<WindowFunction>("Window Function");
        SigParam<double> FGain = new SigParam<double>("Gain", 0.5);
        SigParam<int> FBufferSize = new SigParam<int>("IFFT Buffer Size", true);
        
        public IFFTSignalDouble()
        {
        }

        IFFTPullBufferDouble FIFFTBuffer;

        int NextPow2(int val)
        {
            var result = 2;
            while(result < val)
            {
                result *= 2;
            }

            return result;
        }

        protected override void FillBuffer(float[] buffer, int offset, int count)
        {
            //recreate ring buffer?
            var size = Math.Max(NextPow2(FFTDataReal.Value?.Count ?? 0), NextPow2(FFTDataImag.Value?.Count ?? 0)) * 2;

            if (size < count)
            {
                FBufferSize.Value = 0;
                return;
            }

            if(FIFFTBuffer == null || size != FIFFTBuffer.PullCount || FWindowFunc.Value != FIFFTBuffer.WindowFunc)
            {
                FIFFTBuffer = new IFFTPullBufferDouble(FFTDataReal, FFTDataImag, size, FWindowFunc.Value);
                FBufferSize.Value = size;
            }

            FIFFTBuffer.Gain = FGain.Value;

            //perform IFFT
            FIFFTBuffer.Read(buffer, offset, count);
        }
    }
}
