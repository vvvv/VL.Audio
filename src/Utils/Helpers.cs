using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VL.Audio.Utils
{
    public static class Helpers
    {
        public static void ReadWaveForm(CancellationToken ct, AudioFileReaderVVVV audiofile, bool loop, float startTime, float endTime, bool toMono, int peakCount, float minValue, out float[][]outputBuffers)
        {
            var channels = audiofile.WaveFormat.Channels;
            long samples = (long)Math.Round(audiofile.TotalTime.TotalSeconds * audiofile.WaveFormat.SampleRate);
            long startSample = 0;
            if (loop)
            {
                startSample = (long)(startTime * audiofile.WaveFormat.SampleRate);
                samples = (long)((endTime - startTime) * audiofile.WaveFormat.SampleRate);
            }
            var localSpreadCount = (int)Math.Min(peakCount, samples);
            if (toMono)
            {
                outputBuffers = new float[1][];
                outputBuffers[0] = new float[localSpreadCount];
            }
            else
            {
                outputBuffers = new float[channels][];
                for (int i = 0; i < channels; i++)
                    outputBuffers[i] = new float[localSpreadCount];
            }

            int blockSize = (int)(samples / localSpreadCount);
            audiofile.Position = startSample * channels * 4;
            var bufferSize = blockSize * channels;
            var buffer = new float[bufferSize];
            var maxValue = 0.0f;
            for (int slice = 0; slice < localSpreadCount; slice++)
            {
                //read one interleaved block
                var samplesRead = audiofile.Read(buffer, 0, bufferSize);
                //split into channels and do the max
                for (int channel = 0; channel < channels; channel++)
                {
                    maxValue = minValue;
                    for (int i = 0; i < samplesRead; i += channels)
                        maxValue = Math.Max(maxValue, Math.Abs(buffer[i + channel]));

                    if (toMono)
                    {
                        outputBuffers[0][slice] = Math.Max(maxValue, outputBuffers[0][slice]);
                    }
                    else
                    {
                        outputBuffers[channel][slice] = maxValue;
                    }
                    ct.ThrowIfCancellationRequested();
                }
            }
        }

        public static void GenerateWaveForm(CancellationToken ct, float[] inputBuffer, int sampleRate, bool loop, float startTime, float endTime, int peakCount, float minValue, out float[] outputBuffer)
        {
            long samples = inputBuffer.LongLength;
            long startSample = 0;
            if (loop)
            {
                startSample = (long)(startTime * sampleRate);
                samples = (long)((endTime - startTime) * sampleRate);
            }
            var localSpreadCount = (int)Math.Min(peakCount, samples);
            outputBuffer = new float[localSpreadCount];

            int blockSize = (int)(samples / localSpreadCount);
            float maxValue;
            var samplesLeftToRead = samples;
            var totalSamplesRead = startSample;
            for (int slice = 0; slice < localSpreadCount; slice++)
            {
                //read one block
                samplesLeftToRead -= blockSize;
                var samplesRead = samplesLeftToRead >= 0 ? blockSize : blockSize + samplesLeftToRead;
                //do the max
                maxValue = minValue;
                for (int i = 0; i < samplesRead; i ++)
                    maxValue = Math.Max(maxValue, Math.Abs(inputBuffer[totalSamplesRead + i]));
                totalSamplesRead += blockSize;

                outputBuffer[slice] = maxValue;
                ct.ThrowIfCancellationRequested();
            }
        }

        public static void ReadBuffer(CancellationToken ct, AudioFileReaderVVVV audiofile, out float[][] outputBuffers)
        {
            var channels = audiofile.WaveFormat.Channels;
            long samples = (long)Math.Round(audiofile.TotalTime.TotalSeconds * audiofile.WaveFormat.SampleRate);

            outputBuffers = new float[channels][];
            for (int i = 0; i < channels; i++)
                outputBuffers[i] = new float[samples];

            int blockSize = (int)Math.Min(int.MaxValue / channels, samples);
            var bufferSize = blockSize * channels;
            var buffer = new float[bufferSize];
            var takes = (samples * channels) / bufferSize;
            for (int t = 0; t < takes; t++)
            {
                //read one interleaved block
                var samplesRead = audiofile.Read(buffer, 0, bufferSize);
                //split into channels
                for (int channel = 0; channel < channels; channel++)
                {
                    for (int i = 0; i < samplesRead / channels; i ++)
                        outputBuffers[channel][t * blockSize + i] = buffer[(i*channels) + channel];

                    ct.ThrowIfCancellationRequested();
                }
            }
        }
    }
}
