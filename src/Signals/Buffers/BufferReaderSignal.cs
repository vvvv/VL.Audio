#region usings
using System;
using NAudio.Utils;
#endregion
namespace VL.Audio
{
    public class BufferReaderSignal : BufferAudioSignal
    {
        public float Speed { get; set; }

        public BufferReaderSignal(string bufferKey) : base(bufferKey)
        {
        }

        public bool DoRead;

        public int ReadPosition;

        public int PreviewSize;

        double FFractionalAccum;

        float[] FTempBuffer = new float[1];
        //gathers frac error
        BufferWiseResampler FResampler = new BufferWiseResampler();

        protected override void FillBuffer(float[] buffer, int offset, int sampleCount)
        {
            if (DoRead)
            {
                if (ReadPosition >= FBufferSize)
                    ReadPosition %= FBufferSize;

                if (Speed == 1.0)
                {
                    //copy to output buffer
                    var copyCount = Math.Min(FBufferSize - ReadPosition, sampleCount);
                    Array.Copy(FBuffer, ReadPosition, buffer, 0, copyCount);
                    if (copyCount < sampleCount)//copy rest from front
                    {
                        Array.Copy(FBuffer, 0, buffer, copyCount, sampleCount - copyCount);
                    }

                    ReadPosition += sampleCount;
                }
                else//resample
                {
                    var channels = 1;
                    var blockAlign = 1;//FAudioFile.OriginalFileFormat.BlockAlign;
                    int samplesToRead;

                    var desiredSamples = sampleCount * channels * Speed;
                    //ideal value
                    samplesToRead = (int)Math.Truncate(desiredSamples);
                    //can only read that much
                    var rem = samplesToRead % blockAlign;
                    samplesToRead -= rem;
                    FFractionalAccum += (desiredSamples - samplesToRead);
                    //gather error
                    //correct error
                    if (FFractionalAccum >= blockAlign)
                    {
                        samplesToRead += blockAlign;
                        FFractionalAccum -= blockAlign;
                    }
                    
                    FTempBuffer = BufferHelpers.Ensure(FTempBuffer, samplesToRead);
                    var copyCount = Math.Min(FBufferSize - ReadPosition, samplesToRead);
                    Array.Copy(FBuffer, ReadPosition, FTempBuffer, 0, copyCount);
                    if (copyCount < samplesToRead)//copy rest from front
                    {
                        Array.Copy(FBuffer, 0, FTempBuffer, copyCount, samplesToRead - copyCount);
                    }

                    FResampler.ResampleChannel(FTempBuffer, buffer, samplesToRead / channels, sampleCount, 0, channels);
                    ReadPosition += samplesToRead;
                }
                
            }
            else
            {
                buffer.ReadSilence(offset, sampleCount);
            }
        }
    }
}




