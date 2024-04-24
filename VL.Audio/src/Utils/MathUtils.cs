using System;
using static System.Math;

namespace VL.Audio.Utils
{
    public sealed class MathUtils
    {
        // <summary>
        /// Clamp function, clamps an integer value into the range [min..max]
        /// </summary>
        /// <param name="x"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int Clamp(int x, int min, int max)
        {
            int minTemp = Min(min, max);
            int maxTemp = Max(min, max);
            return Min(Max(x, minTemp), maxTemp);
        }

        public static ulong Clamp(ulong x, ulong min, ulong max)
        {
            var r = x < min ? min : x;
            return r > max ? max : r;
        }

        /// <summary>
        /// Clamp function, clamps a floating point value into the range [min..max]
        /// </summary>
        /// <param name="x"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static float Clamp(float x, float min, float max)
        {
            float minTemp = Min(min, max);
            float maxTemp = Max(min, max);
            return Min(Max(x, minTemp), maxTemp);
        }

        /// <summary>
        /// Clamp function, clamps a floating point value into the range [min..max]
        /// </summary>
        /// <param name="x"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static double Clamp(double x, double min, double max)
        {
            double minTemp = Min(min, max);
            double maxTemp = Max(min, max);
            return Min(Max(x, minTemp), maxTemp);
        }
    }
}
