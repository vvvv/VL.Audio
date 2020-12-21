using System;
using System.Collections.Generic;
using System.Text;

namespace VL.Audio
{
    public class UseDllHack
    {
        // this makes sure the VVVV.Utils.dll gets copied to the output when exported from vvvv gamma
        public static double IgnoreMe() => VVVV.Utils.VMath.VMath.Random.NextDouble();

    }
}
