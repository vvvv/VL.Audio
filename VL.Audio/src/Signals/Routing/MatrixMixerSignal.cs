using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Utils;
using VL.Lib.Collections;
namespace VL.Audio
{
    public class MatrixMixerSignal : MultiChannelInputSignal
    {
        public Spread<float> GainMatrix { get; set; }

        public int OutputChannelCount
        {
            get
            {
                return this.OutputCount;
            }
            set
            {
                SetOutputCount(value);
            }
        }

        float[] FTempBuffer = new float[1];

        protected override void FillBuffers(float[][] buffer, int offset, int count)
        {
            if (FInput != null && FInput.Count != 0) 
            {
                var gainMatrix = GainMatrix;
                var inCount = FInput.Count();

                if (gainMatrix.Count >= inCount * buffer.Length)
                {
                    FTempBuffer = BufferHelpers.Ensure(FTempBuffer, count);
                    for (int outSlice = 0; outSlice < buffer.Length; outSlice++)
                    {
                        var outbuf = buffer[outSlice];
                        for (int inSlice = 0; inSlice < inCount; inSlice++)
                        {
                            var gain = gainMatrix[outSlice + inSlice * buffer.Length];
                            var inSig = FInput[inSlice];
                            if (inSig != null)
                            {
                                inSig.Read(FTempBuffer, offset, count);
                                if (inSlice == 0)
                                {
                                    for (int j = 0; j < count; j++)
                                    {
                                        outbuf[j] = FTempBuffer[j] * gain;
                                    }
                                }
                                else
                                {
                                    for (int j = 0; j < count; j++)
                                    {
                                        outbuf[j] += FTempBuffer[j] * gain;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (var b in buffer)
                    {
                        b.ReadSilence(offset, count);
                    }
                }
            }
        }
    }
}