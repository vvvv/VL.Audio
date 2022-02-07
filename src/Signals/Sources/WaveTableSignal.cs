
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace VL.Audio
{
    /// <summary>
    /// Class for LUT playback
    /// </summary>
    public class WaveTableSignal : AudioSignal
    {
        //constructor
        public WaveTableSignal()
        {
            tempLUT = new float[1024];
            playingLUT = new float[1024];
            LUTBuffer = new float[1024];
            FIndex = 1024;
            newTableAvailable = true;
            newTableCount = true;
        }

        double FFrequency;

        public double Frequency 
        {
            get 
            {
                return FFrequency;
            }
            set 
            {
                FFrequency = value;
            }
        }

        private double FIndex;

        private float[] tempLUT;
        private float[] playingLUT;

        public float[] LUTBuffer
        {
            get;
            set;
        }

        bool newTableAvailable;
        bool newTableCount;
        int tableCount;
        double tableDelta;

        // should be called from outside when new data present in the LUTBuffer
        public void SwapBuffers()
        {
            var tmp = tempLUT;
            tempLUT = LUTBuffer;

            // reuse old temp LUT if same size
            if (LUTBuffer.Length == tmp.Length)
            {
                LUTBuffer = tmp;
            }
            else
            {
                LUTBuffer = new float[LUTBuffer.Length];
                newTableCount = true;
            }

            newTableAvailable = true;
        }

        protected unsafe override void FillBuffer(float[] buffer, int offset, int count)
        {         
            for (int n = 0; n < count; n++)
            {
                if (FIndex >= tableCount)
                {
                    FIndex = 0;

                    if (newTableCount)
                    {
                        tableCount = tempLUT.Length;
                        tableDelta = (double)tableCount / WaveFormat.SampleRate;
                        
                        if (tableCount > 0)
                            playingLUT = new float[tableCount];

                        newTableCount = false;
                    }

                    if (newTableAvailable && tableCount > 0)
                    {
                        Array.Copy(tempLUT, playingLUT, tableCount);
                        newTableAvailable = false;
                    }
                }

                var index = (int)Math.Floor(FIndex);
                buffer[n + offset] = playingLUT[index];

                FIndex += FFrequency * tableDelta;
            }
        }
    }
}




