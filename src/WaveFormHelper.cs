using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VVVV.Audio;

namespace VL.Audio
{
    public static class WaveformHelper
    {
        public static void ReadIntoSpread(CancellationToken ct, AudioFileReaderVVVV audiofile, bool loop, double startTime, double endTime, bool toMono, int peakCount, float minValue, out double[][]outputBuffers)
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
                outputBuffers = new double[1][];
                outputBuffers[0] = new double[localSpreadCount];
            }
            else
            {
                outputBuffers = new double[channels][];
                for (int i = 0; i < channels; i++)
                {
                    outputBuffers[i] = new double[localSpreadCount];
                }
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
                    {
                        maxValue = Math.Max(maxValue, Math.Abs(buffer[i + channel]));
                    }

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
    }
}
