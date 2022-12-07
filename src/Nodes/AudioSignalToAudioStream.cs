using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using VL.Core;
using VL.Lib.Basics.Audio;
using VL.Lib.Basics.Resources;

namespace VL.Audio.Nodes
{
    public sealed class AudioSignalToAudioStream : MultiChannelInputSignal
    {
        private readonly Subject<IResourceProvider<AudioFrame>> _frames = new Subject<IResourceProvider<AudioFrame>>();

        private readonly AudioStream _audioStream;

        public AudioSignalToAudioStream()
        {
            _audioStream = new AudioStream(_frames);
        }

        private string _metadata;

        public IReadOnlyList<AudioSignal> Update(IReadOnlyList<AudioSignal> input, string metadata, out AudioStream audioStream)
        {
            if (input.Count != FOutputCount)
            {
                SetOutputCount(input.Count);
                FNeedsRead = true;
            }

            Input = input;
            _metadata = metadata;

            audioStream = input.Count > 0 ? _audioStream : null;

            return Outputs;
        }

        protected override void FillBuffers(float[][] buffers, int offset, int sampleCount)
        {
            var inputs = Input;

            var memoryOwner = MemoryOwner<float>.Allocate(buffers.Length * sampleCount);
            var memory = memoryOwner.Memory.AsMemory2D(buffers.Length, sampleCount);
            var dst = memory.Span;
            for (int i = 0; i < buffers.Length; i++)
            {
                inputs[i].Read(buffers[i], offset, sampleCount);
                var src = new Span<float>(buffers[i], offset, sampleCount);
                src.CopyTo(dst.GetRowSpan(i));
            }

            var audioFrame = new AudioFrame(memory, SampleRate, IsInterleaved: false, Metadata: _metadata);
            var frameProvider = ResourceProvider.Return(audioFrame, memoryOwner, static memoryOwner => memoryOwner.Dispose());
            using (frameProvider.GetHandle())
            {
                _frames.OnNext(frameProvider);
            }
        }
    }
}
