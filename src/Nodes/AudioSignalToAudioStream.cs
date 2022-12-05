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
        private readonly Subject<AudioFrame<float>> _audioStream = new Subject<AudioFrame<float>>();
        private readonly BehaviorSubject<AudioFrame<float>> _silentAudioStream = new BehaviorSubject<AudioFrame<float>>(AudioFrame<float>.Empty);

        private string _metadata;

        public IReadOnlyList<AudioSignal> Update(IReadOnlyList<AudioSignal> input, string metadata, out IObservable<AudioFrame<float>> audioStream)
        {
            if (input.Count != FOutputCount)
            {
                SetOutputCount(input.Count);
                FNeedsRead = true;
            }

            Input = input;
            _metadata = metadata;

            audioStream = input.Count > 0 ? _audioStream : _silentAudioStream;

            return Outputs;
        }

        protected override void FillBuffers(float[][] buffers, int offset, int sampleCount)
        {
            var inputs = Input;

            using var memoryOwner = MemoryOwner<float>.Allocate(buffers.Length * sampleCount);
            var memory = memoryOwner.Memory.AsMemory2D(buffers.Length, sampleCount);
            var dst = memory.Span;
            for (int i = 0; i < buffers.Length; i++)
            {
                inputs[i].Read(buffers[i], offset, sampleCount);
                var src = new Span<float>(buffers[i], offset, sampleCount);
                src.CopyTo(dst.GetRowSpan(i));
            }

            _audioStream.OnNext(new AudioFrame<float>(memory, SampleRate, IsInterleaved: false, Metadata: _metadata));
        }
    }
}
