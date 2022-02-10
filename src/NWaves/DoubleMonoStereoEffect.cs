using NWaves.Effects;
using NWaves.Effects.Base;
using NWaves.Effects.Stereo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VL.Audio.NWaves
{
    public abstract class GenericStereoEffect<T> : StereoEffect where T : AudioEffect
    {
        public readonly T L, R;
        public GenericStereoEffect(T effectL, T effectR)
        {
            L = effectL;
            R = effectR;
        }

        public override void Process(ref float left, ref float right)
        {
            left = L.Process(left);
            right = R.Process(right);
        }

        public override void Reset()
        {
            L.Reset();
            R.Reset();
        }
    }

    public class TubeDistortionStereo : GenericStereoEffect<TubeDistortionEffect>
    {
        private TubeDistortionStereo(TubeDistortionEffect effectL, TubeDistortionEffect effectR)
            : base(effectL, effectR)
        {
        }

        public static TubeDistortionStereo New()
        {
            return new TubeDistortionStereo(new TubeDistortionEffect(inputGain: 12, dist: 5), new TubeDistortionEffect(inputGain: 12, dist: 5));
        }
    }
}
