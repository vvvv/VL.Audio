#region usings
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using NAudio.Utils;
using VL.Core.Utils;
#endregion
namespace VL.Audio
{
    public class FileStreamSignal : MultiChannelSignal
    {
        private readonly BlockingCollection<AudioFileReaderVVVV> FFileQueue = new(boundedCapacity: 1);
        private readonly BlockingCollection<float> FSeekQueue = new(boundedCapacity: 1);
        private readonly ManualResetEvent FInTheWorks = new ManualResetEvent(true);
        private AudioFileReaderVVVV FAudioFile;

        public bool FLoop;

        public bool FPlay = false;

        public TimeSpan LoopStartTime;

        public TimeSpan LoopEndTime;

        public TimeSpan FSeekTime;

        public double Speed { get; set; }

        public float Volume { get; set; } = 1f;

        public float Position => FAudioFile != null ? (float)FAudioFile.CurrentTime.TotalSeconds : 0f;

        public float Duration { get; private set; }

        public bool CanSeek { get; private set; }

        public int Channels { get; private set; }

        public int OriginalSampleRate { get; private set; }

        public string OriginalFormat { get; private set; }

        public void Seek(float seekTime)
        {
            if (!FSeekQueue.TryAddSafe(seekTime))
            {
                FSeekQueue.TryTake(out _);
                FSeekQueue.TryAddSafe(seekTime);
            }
        }

        public void OpenFile(string filename)
        {
            if (FFileQueue.TryTake(out var oldfile))
                oldfile?.Dispose();

            if (!string.IsNullOrEmpty(filename) && File.Exists(filename))
            {
                var audiofile = new AudioFileReaderVVVV(filename);
                //NOTE: switching between files of different output count may cause troubles here:
                SetOutputCount(audiofile.WaveFormat.Channels);
                FFileQueue.TryAddSafe(audiofile);

                Duration = (float)audiofile.TotalTime.TotalSeconds;
                CanSeek = audiofile.CanSeek;
                Channels = audiofile.WaveFormat.Channels;
                OriginalSampleRate = audiofile.WaveFormat.SampleRate;
                OriginalFormat = audiofile.OriginalFileFormat.ToString();
            }
            else 
            {
                SetOutputCount(0);
                FFileQueue.TryAddSafe(null);

                Duration = default;
                CanSeek = default;
                Channels = default;
                OriginalSampleRate = default;
                OriginalFormat = string.Empty;
            }
        }

        float[] FFileBuffer = new float[1];

        double FFractionalAccum;

        //gathers frac error
        BufferWiseResampler FResampler = new BufferWiseResampler();

        protected override void FillBuffers(float[][] buffer, int offset, int sampleCount)
        {
            if (FFileQueue.TryTake(out var newAudioFile))
            {
                if (FAudioFile != null)
                    FAudioFile.Dispose();
                FAudioFile = newAudioFile;
            }

            if(FAudioFile == null) return;
            
            FInTheWorks.Reset();
            try
            {
                if (FSeekQueue.TryTake(out var seekTime))
                    FAudioFile.CurrentTime = TimeSpan.FromSeconds(seekTime);

                FAudioFile.Volume = Volume;

                var channels = FAudioFile.WaveFormat.Channels;
                var blockAlign = FAudioFile.OriginalFileFormat.BlockAlign;
                int samplesToRead;
                var speed = Speed * FAudioFile.WaveFormat.SampleRate / AudioService.Engine.Settings.SampleRate;
                if (speed == 1.0) 
                {
                    samplesToRead = sampleCount * channels;
                }
                else 
                {
                    var desiredSamples = sampleCount * channels * speed;
                    //ideal value
                    samplesToRead = (int)Math.Truncate(desiredSamples);
                    //can only read that much
                    var rem = samplesToRead % blockAlign;
                    samplesToRead -= rem;
                    FFractionalAccum += (desiredSamples - samplesToRead);
                    //gather error
                    //correct error
                    if (FFractionalAccum >= blockAlign) 
                    {
                        samplesToRead += blockAlign;
                        FFractionalAccum -= blockAlign;
                    }
                }
                FFileBuffer = BufferHelpers.Ensure(FFileBuffer, samplesToRead);
                int samplesRead = 0;
                if (FPlay && samplesToRead > 0) 
                {
                    //if we're at a loop point, then make two reads: one to the LoopEnd, another at the LoopStart with the remaining samples to read
                    var needsTwoReads = false;
                    long currentSample = 0, loopEndSample = 0;
                    if (FLoop)
                    {
                        currentSample = FAudioFile.Position;
                        loopEndSample = FAudioFile.Length;
                        if (LoopEndTime.TotalSeconds >= 0)
                            loopEndSample = Math.Min(loopEndSample, (long) (LoopEndTime.TotalSeconds * FAudioFile.WaveFormat.SampleRate * FAudioFile.BlockAlign));
                        //if we're past the loopEndTime, then loop now
                        if (currentSample > loopEndSample)
                            loopEndSample = currentSample;

                        needsTwoReads = currentSample + (samplesToRead / channels) > loopEndSample;
                    }

                    if (needsTwoReads)
                    {
                        //first read samples remaining to loopEnd
                        var samplesToReadAtEndOfLoop = (int) Math.Min(samplesToRead, loopEndSample - currentSample);
                        samplesRead = FAudioFile.Read(FFileBuffer, offset * channels, samplesToReadAtEndOfLoop);
                        WriteFileBufferToOutputBuffer(buffer, 0, channels, samplesToReadAtEndOfLoop, sampleCount, speed);

                        //then make another read at the beginning of the loop
                        var samplesToReadAtStartOfLoop = samplesToRead - samplesToReadAtEndOfLoop;
                        FAudioFile.CurrentTime = LoopStartTime;
                        samplesRead = FAudioFile.Read(FFileBuffer, offset * channels, samplesToReadAtStartOfLoop);
                        WriteFileBufferToOutputBuffer(buffer, samplesToReadAtEndOfLoop, channels, samplesToReadAtEndOfLoop, sampleCount, speed);
                    }
                    else
                    {
                        samplesRead = FAudioFile.Read(FFileBuffer, offset * channels, samplesToRead);
                        WriteFileBufferToOutputBuffer(buffer, 0, channels, samplesToRead, sampleCount, speed);
                    }
                }
                else//silence
                    {
                    for (int i = 0; i < channels; i++) 
                    {
                        buffer[i].ReadSilence(offset, sampleCount);
                    }
                }
            }
            finally
            {
                FInTheWorks.Set();
            }
        }

        private void WriteFileBufferToOutputBuffer(float[][] buffer, int outputOffset, int channels, int samplesToRead, int sampleCount, double speed)
        {
            if (speed == 1.0)
            {
                //copy to output buffers
                for (int i = 0; i < channels; i++)
                {
                    for (int j = 0; j < sampleCount; j++)
                    {
                        buffer[i][j] = FFileBuffer[i + outputOffset + j * channels];
                    }
                }
            }
            else//resample
            {
                FResampler.ResampleDeinterleave(FFileBuffer, buffer, samplesToRead / channels, sampleCount, channels);
            }
        }

        public override void Dispose()
        {
            FFileQueue.CompleteAdding();
            foreach (var file in FFileQueue)
                file?.Dispose();

            FInTheWorks.WaitOne();
            FAudioFile?.Dispose();
            FAudioFile = null;

            base.Dispose();
        }
    }
}


