using System;
using System.Collections.Generic;
using VL.Audio.Utils;

namespace VL.Audio
{
    public enum NoiseSelection
    {
        WhiteNoise,
        PinkNoise
    }

    public class NoiseSignal : AudioSignal
    {
        SigParam<NoiseSelection> Noise = new SigParam<NoiseSelection>("Noise");
        SigParam<float> Gain = new SigParam<float>("Gain", 0.1f);
        
        public NoiseSignal()
        {
        }
        
        protected override void Engine_SampleRateChanged(object sender, EventArgs e)
        {
            base.Engine_SampleRateChanged(sender, e);
        }
        
        const double FWhiteNoiseScale = 2.0f / 0xffffffff;
        uint FWhiteNoiseX1 = 0x67452301;
        uint FWhiteNoiseX2 = 0xefcdab89;
        
        void WhiteNoise(float[] buffer, int count, double gain)
        {
            gain *= FWhiteNoiseScale;
            
            for (int i = 0; i < count; i++)
            {
                FWhiteNoiseX1 ^= FWhiteNoiseX2;
                
                buffer[i] = (float)(FWhiteNoiseX2 * gain);
                
                unchecked
                {
                    FWhiteNoiseX2 += FWhiteNoiseX1;
                }
            }
        }
        
        double b0p, b1p, b2p;
        void PinkNoise(float[] buffer, int count, double gain)
        {
            gain *= FWhiteNoiseScale;
            
            for (int i = 0; i < count; i++)
            {
                FWhiteNoiseX1 ^= FWhiteNoiseX2;
                
                var white = (FWhiteNoiseX2 * gain);
                
                unchecked
                {
                    FWhiteNoiseX2 += FWhiteNoiseX1;
                }
                
                b0p = 0.99765 * b0p + white * 0.0990460;
                b1p = 0.96300 * b1p + white * 0.2965164;
                b2p = 0.57000 * b2p + white * 1.0526913;
                buffer[i] = (float)((b0p + b1p + b2p + white * 0.1848) * 0.0333333333333333333);
            }
        }

        protected override void FillBuffer(float[] buffer, int offset, int count)
        {
            if(Noise.Value == NoiseSelection.WhiteNoise)
            {
                WhiteNoise(buffer, count, Gain.Value*0.5f);
            }
            else if(Noise.Value == NoiseSelection.PinkNoise)
            {
                PinkNoise(buffer, count, Gain.Value*0.5f);
            }
        }
    }
}




