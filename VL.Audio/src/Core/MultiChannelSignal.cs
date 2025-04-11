#region usings
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;

#endregion usings

namespace VL.Audio
{
    public class SingleSignal : AudioSignal
    {
        //the read method from the MultiChannelSignal
        protected Action<int, int> FRequestBufferFill;
        public SingleSignal(Action<int, int> read)
        {
            FRequestBufferFill = read;
        }

        public void SetBuffer(float[] buffer)
        {
            FBuffer = buffer;
        }
        
        float[] FBuffer;

        internal float[] Buffer => FBuffer;
        
        protected override void FillBuffer(float[] buffer, int offset, int count)
        {
            if (FRequestBufferFill != null)
            {
                FRequestBufferFill(offset, count);
                Array.Copy(FBuffer, offset, buffer, offset, count);
            }
        }
        
        public override void Dispose()
        {
            FRequestBufferFill = null;
            base.Dispose();
        }
    }
    
    /// <summary>
    /// Processes multiple audio signals
    /// </summary>
    public class MultiChannelSignal : AudioSignal
    {
        private ImmutableArray<SingleSignal> FOutputs = ImmutableArray<SingleSignal>.Empty;

        public MultiChannelSignal()
        {
            SetOutputCount(2);
        }
        
        public void SetOutputCount(int newCount)
        {
            if (newCount != FOutputs.Length)
            {
                foreach (var s in FOutputs)
                    s.Dispose();

                var outputs = ImmutableArray.CreateBuilder<SingleSignal>(newCount);
                for (int i = 0; i < newCount; i++)
                    outputs.Add(new SingleSignal(Read));
                FOutputs = outputs.MoveToImmutable();
                FNeedsRead = true;
            }
        }

        public IReadOnlyList<AudioSignal> Outputs => FOutputs;
        public int OutputCount => FOutputs.Length;

        protected float[][] FReadBuffers;
        protected void ManageBuffers(int count)
        {
            var outputs = FOutputs;
            if (!BuffersAreLargeEnough(outputs, count))
            {
                FReadBuffers = new float[outputs.Length][];
                for (int i = 0; i < FReadBuffers.Length; i++)
                {
                    FReadBuffers[i] = GC.AllocateArray<float>(count, pinned: true);
                    outputs[i].SetBuffer(FReadBuffers[i]);
                }
            }

            static bool BuffersAreLargeEnough(ImmutableArray<SingleSignal> outputs, int count)
            {
                foreach (var o in outputs)
                    if (!BufferIsLargeEnough(o.Buffer, count))
                        return false;
                return true;
            }

            static bool BufferIsLargeEnough(float[] buffer, int count)
            {
                return buffer != null && buffer.Length >= count;
            }
        }
        
        protected void Read(int offset, int count)
        {
            if (FNeedsRead && FOutputs.Length > 0)
            {
                ManageBuffers(count);
                FillBuffers(FReadBuffers, offset, count);
                FNeedsRead = false;
            }
            
            //since the buffers are already assigned to the SingleSignals nothing more to do
        }

        /// <summary>
        /// Does the actual work
        /// </summary>
        /// <param name="buffers">The buffers are allocated on the POC and therefor already pinned.</param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        protected virtual void FillBuffers(float[][] buffers, int offset, int count)
        {
            
        }

        public override void Dispose()
        {
            foreach (var signal in Outputs)
                signal.Dispose();

            base.Dispose();
        }
    }
}


