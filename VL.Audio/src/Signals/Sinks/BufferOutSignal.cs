
using System;
using System.Collections.Generic;

namespace VL.Audio
{
    
    public class BufferOutSignal : SinkSignal
    {
        public CircularBuffer Buffer = new CircularBuffer(512);
        
        public BufferOutSignal(AudioSignal input)
        {
            InputSignal.Value = input;
            Buffer.BufferFilled = BufferFilled;
        }
        
        public float[] bufferOut = new float[1];

        public IReadOnlyList<float> BufferOut => bufferOut;


        void BufferFilled(float[] buffer)
        {
            //copy the values from the circular buffer into the output array
            Array.Copy(buffer, 0, bufferOut, 0, Math.Min(bufferOut.Length, buffer.Length));
        }
        
        protected override void FillBuffer(float[] buffer, int offset, int count)
        {
            if(bufferOut.Length != Buffer.Size)
                bufferOut = new float[Buffer.Size];
            
            if (InputSignal.Value != null) 
            {
                InputSignal.Read(buffer, offset, count);
                Buffer.Write(buffer, offset, count);
            }
            else 
            {
                buffer.ReadSilence(offset, count);
                Buffer.Write(buffer, offset, count);
            }
        }
    }
}




