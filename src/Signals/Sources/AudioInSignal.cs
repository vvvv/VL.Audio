using System;

namespace VL.Audio
{
    public class AudioInSignal : AudioSignal
    {
        protected AudioEngine FEngine;

        protected int FIndex;

        public AudioInSignal(AudioEngine engine, int index)
        {
            FEngine = engine;
            FIndex = index;
            FEngine.RecordingRequestedStack.Push(new object());
        }

        protected override void FillBuffer(float[] buffer, int offset, int count)
        {
            Array.Copy(FEngine.InputBuffers[FIndex], offset, buffer, offset, count);
        }

        public override void Dispose()
        {
            FEngine.RecordingRequestedStack.Pop();
            base.Dispose();
        }
    }
}




