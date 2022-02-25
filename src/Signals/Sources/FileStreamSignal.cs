#region usings
using System;
using System.IO;
using NAudio.Utils;
#endregion
namespace VL.Audio
{
    public class FileStreamSignal : MultiChannelSignal
    {
        public bool FLoop;

        public bool FPlay = false;

        public TimeSpan LoopStartTime;

        public TimeSpan LoopEndTime;

        public TimeSpan FSeekTime;

        public AudioFileReaderVVVV FAudioFile;

        public double Speed 
        {
            get;
            set;
        }

        public void OpenFile(string filename)
        {
            if (FAudioFile != null) 
            {
                FAudioFile.Dispose();
                FAudioFile = null;
            }
            if (!string.IsNullOrEmpty(filename) && File.Exists(filename))
            {
                FAudioFile = new AudioFileReaderVVVV(filename);
                SetOutputCount(FAudioFile.WaveFormat.Channels);
            }
            else 
            {
                SetOutputCount(0);
            }
        }

        float[] FFileBuffer = new float[1];

        double FFractionalAccum;

        //gathers frac error
        BufferWiseResampler FResampler = new BufferWiseResampler();

        protected override void FillBuffers(float[][] buffer, int offset, int sampleCount)
        {
            if(FAudioFile == null) return;
            var channels = FAudioFile.WaveFormat.Channels;
            var blockAlign = FAudioFile.OriginalFileFormat.BlockAlign;
            int samplesToRead;
            if (Speed == 1.0) 
            {
                samplesToRead = sampleCount * channels;
            }
            else 
            {
                var desiredSamples = sampleCount * channels * Speed;
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
                int currentSample = 0, loopEndSample = 0;
                if (FLoop)
                {
                    currentSample = (int) (FAudioFile.CurrentTime.TotalSeconds * SampleRate);
                    var loopEndTime = FAudioFile.TotalTime.TotalSeconds;
                    if (LoopEndTime.TotalSeconds >= 0)
                        loopEndTime = Math.Min(loopEndTime, LoopEndTime.TotalSeconds);
                    loopEndSample = (int) (loopEndTime * SampleRate);
                    //if we're past the loopEndTime, then loop now
                    if (currentSample > loopEndSample)
                        loopEndSample = currentSample;

                    needsTwoReads = currentSample + (samplesToRead / channels) > loopEndSample;
                }

                if (needsTwoReads)
                {
                    //first read samples remaining to loopEnd
                    var samplesToReadAtEndOfLoop = Math.Min(samplesToRead, loopEndSample - currentSample);
                    samplesRead = FAudioFile.Read(FFileBuffer, offset * channels, samplesToReadAtEndOfLoop);
                    WriteFileBufferToOutputBuffer(buffer, 0, channels, samplesToReadAtEndOfLoop, samplesRead / channels);

                    //then make another read at the beginning of the loop
                    var samplesToReadAtStartOfLoop = samplesToRead - samplesToReadAtEndOfLoop;
                    FAudioFile.CurrentTime = LoopStartTime;
                    samplesRead = FAudioFile.Read(FFileBuffer, offset * channels, samplesToReadAtStartOfLoop);
                    WriteFileBufferToOutputBuffer(buffer, samplesToReadAtEndOfLoop, channels, samplesToReadAtEndOfLoop, samplesRead / channels);
                }
                else
                {
                    samplesRead = FAudioFile.Read(FFileBuffer, offset * channels, samplesToRead);
                    WriteFileBufferToOutputBuffer(buffer, 0, channels, samplesToRead, sampleCount);
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

        private void WriteFileBufferToOutputBuffer(float[][] buffer, int outputOffset, int channels, int samplesToRead, int sampleCount)
        {
            if (Speed == 1.0)
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
            FAudioFile?.Dispose();
            FAudioFile = null;
            base.Dispose();
        }
    }
}


