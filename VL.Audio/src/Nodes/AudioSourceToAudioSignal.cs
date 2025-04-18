using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VL.Core;
using VL.Lib.Basics.Audio;

namespace VL.Audio.Nodes
{
    public sealed class AudioSourceToAudioSignal : MultiChannelSignal
    {
        private AudioSignal[] _outputs;
        private IAudioSource _audioSource;
        private Optional<int> _channelCount;

        public AudioSourceToAudioSignal()
        {
            _outputs = Outputs.ToArray();
        }

        public IReadOnlyList<AudioSignal> Update(IAudioSource audioSource, Optional<int> channelCount)
        {
            _audioSource = audioSource;
            _channelCount = channelCount;
            return _outputs;
        }

        protected override void FillBuffers(float[][] buffers, int offset, int sampleCount)
        {
            var channelCount = _channelCount.HasValue ? _channelCount.Value : 0;

            using var audioFrameHandle = _audioSource?
                .GrabAudioFrame(sampleCount, SampleRate, channelCount, interleaved: false)?
                .GetHandle();

            var audioFrame = audioFrameHandle?.Resource;
            if (audioFrame is null)
            {
                foreach (var buffer in buffers)
                    buffer.ReadSilence(offset, sampleCount);
            }
            else
            {
                if (audioFrame.ChannelCount != OutputCount)
                {
                    SetOutputCount(audioFrame.ChannelCount);
                    ManageBuffers(sampleCount);
                    buffers = FReadBuffers;
                    _outputs = Outputs.ToArray();
                }

                for (int i = 0; i < buffers.Length; i++)
                    audioFrame.CopyChannelTo(i, new Span<float>(buffers[i], offset, sampleCount));
            }
        }
    }
}
