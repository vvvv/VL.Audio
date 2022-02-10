/*
 * Created by SharpDevelop.
 * User: Tebjan Halm
 * Date: 08.10.2013
 * Time: 10:59
 * 
 * 
 */
using System;
using System.Diagnostics;

using NAudio.Wave;
using NAudio;
using VL.Audio.Utils;

namespace VL.Audio
{
    
    /// <summary>
    /// Simple circular wrapper around a float array
    /// </summary>
    public class CircularBufferWasapi
    {
        int size;
        float[] buffer;
        object bufferLock = new object();

        public CircularBufferWasapi(int size)
        {
            Size = size;
            timer = Stopwatch.StartNew();
        }
        
        public int Size 
        {
            get 
            {
                return size; 
            }
            set 
            { 
                if (size != value)
                {
                    buffer = new float[value];
                    size = value;
                    FWritePosition = 0;
                }
            }
        }

        // for debugging
        public ulong WritePosTotal;
        public ulong ReadPosTotal;
        public int FWritePosition;
        public int FReadPosition;
        public int AvailableSamples;
        Stopwatch timer;
        TimeSpan lastWriteTime;
        TimeSpan lastReadTime;
        int lastWriteCount;

        /// <summary>
        /// Writes new data after the latest ones
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void Write(float[] data, int offset, int count)
        {
            lastWriteTime = timer.Elapsed;
            lastWriteCount = count;
            count = Math.Min(count, buffer.Length);
            var samplesWritten = 0;

            // write to end
            int writeToEnd = Math.Min(buffer.Length - FWritePosition, count);
            Array.Copy(data, offset, buffer, FWritePosition, writeToEnd);
            FWritePosition += writeToEnd;
            FWritePosition %= buffer.Length;
            samplesWritten += writeToEnd;
            if (samplesWritten < count)
            {
                Debug.Assert(FWritePosition == 0);
                // must have wrapped round. Write to start
                Array.Copy(data, offset + samplesWritten, buffer, FWritePosition, count - samplesWritten);
                FWritePosition += (count - samplesWritten);
                samplesWritten = count;
            }
            AvailableSamples = AvailableSamples + samplesWritten;
            WritePosTotal += (ulong)samplesWritten;
        }
       
        /// <summary>
        /// Starts reading where the last Read call left off, but will not read further than the most recent sample.
        /// Will pad with 0 if there are not enough samples available.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public void ReadFromLastPosition(float[] data, int offset, int count)
        {
            var maxInOrOutBufferSize = (ulong)Math.Max(lastWriteCount, 2*count);
            if (WritePosTotal < maxInOrOutBufferSize)
                return;

            var minRead = WritePosTotal - maxInOrOutBufferSize;
            var maxRead = WritePosTotal - (ulong)count;

            var readTotal = MathUtils.Clamp(ReadPosTotal, minRead, maxRead);

            if (readTotal != ReadPosTotal)
            {
                FReadPosition = (int)(readTotal % (ulong)buffer.Length);
                ReadPosTotal = readTotal;
            }

            var readPosition = FReadPosition;

            var samplesRequested = count;
            count = Math.Min(samplesRequested, AvailableSamples);

            int samplesRead = 0;
            int readToEnd = Math.Min(buffer.Length - readPosition, count);
            Array.Copy(buffer, readPosition, data, offset, readToEnd);
            samplesRead += readToEnd;
            readPosition += readToEnd;
            readPosition %= buffer.Length;

            if (samplesRead < count)
            {
                // must have wrapped round. Read from start
                Debug.Assert(readPosition == 0);
                Array.Copy(buffer, readPosition, data, offset + samplesRead, count - samplesRead);
                readPosition += (count - samplesRead);
                samplesRead = count;
            }

            if (samplesRequested > samplesRead)
            {
                data.ReadSilence(samplesRead, samplesRequested - samplesRead);
                FReadPosition = readPosition;
            }
            else
            {
                FReadPosition = readPosition;
            }

            AvailableSamples -= samplesRead;
            ReadPosTotal += (ulong)samplesRead;
            Debug.Assert(AvailableSamples >= 0);
        }
    }
}
