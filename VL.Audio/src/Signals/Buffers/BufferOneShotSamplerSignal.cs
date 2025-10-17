using System;
using NAudio.Utils;

namespace VL.Audio
{
    public class BufferOneShotSamplerSignal : BufferAudioSignal
    {
        public int ReadPosition = 0;

        private double fractionalAccum = 0;
        private bool isPlaying = false;
        private int seekPosition = 0;

        private float[] tempBuffer = new float[1];
        private BufferWiseResampler resampler = new BufferWiseResampler();

        public float Speed { get; set; } = 1.0f;



        /// <summary>
        /// Set the target position for the next Play() call.
        /// </summary>
        public int SeekPosition
        {
            get => seekPosition;
            set
            {
                seekPosition = Math.Max(0, Math.Min(FBufferSize - 1, value));
            }
        }

        public BufferOneShotSamplerSignal(string bufferKey) : base(bufferKey){}

        /// <summary>
        /// Triggers playback from the seek position, even if currently playing (retrigger behavior)
        /// </summary>
        public void Play()
        {
            ReadPosition = SeekPosition;
            fractionalAccum = 0;
            isPlaying = true;
        }

        /// <summary>
        /// Stops playback immediately and outputs silence until retriggered
        /// </summary>
        public void StopPlayback()
        {
            isPlaying = false;
        }

        public bool IsPlaying => isPlaying;

        protected override void FillBuffer(float[] buffer, int offset, int sampleCount)
        {
            if (isPlaying)
            {
                if (Speed == 1.0f)
                {
                    int remaining = FBufferSize - ReadPosition;
                    int toCopy = Math.Min(sampleCount, remaining);

                    Array.Copy(FBuffer, ReadPosition, buffer, offset, toCopy);
                    ReadPosition += toCopy;

                    if (toCopy < sampleCount)
                    {
                        buffer.ReadSilence(offset + toCopy, sampleCount - toCopy);
                        isPlaying = false;
                    }
                }
                else
                {
                    // Resampling logic (mimics BufferReaderSignal)
                    int channels = 1;
                    int blockAlign = 1; // adjust if multi-channel
                    double desiredSamples = sampleCount * channels * Speed;
                    int samplesToRead = (int)Math.Truncate(desiredSamples);

                    int rem = samplesToRead % blockAlign;
                    samplesToRead -= rem;
                    fractionalAccum += (desiredSamples - samplesToRead);

                    if (fractionalAccum >= blockAlign)
                    {
                        samplesToRead += blockAlign;
                        fractionalAccum -= blockAlign;
                    }

                    tempBuffer = BufferHelpers.Ensure(tempBuffer, samplesToRead);
                    int available = Math.Max(0, FBufferSize - ReadPosition);
                    int copyCount = Math.Min(available, samplesToRead);

                    Array.Copy(FBuffer, ReadPosition, tempBuffer, 0, copyCount);

                    // If we run out of buffer, fill rest with zero
                    if (copyCount < samplesToRead)
                    {
                        Array.Clear(tempBuffer, copyCount, samplesToRead - copyCount);
                        isPlaying = false;
                    }

                    resampler.ResampleChannel(tempBuffer, buffer, samplesToRead / channels, sampleCount, 0, channels);
                    ReadPosition += samplesToRead;

                    // Stop if we reached the end
                    if (ReadPosition >= FBufferSize)
                        isPlaying = false;
                }
            }
            else
            {
                buffer.ReadSilence(offset, sampleCount);
            }
        }
    }
}