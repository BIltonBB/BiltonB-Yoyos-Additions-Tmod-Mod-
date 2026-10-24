// Easing curves adapted from Luminance (MIT) - vendored minimal subset.
// Credit: LucilleKarma/Luminance (MIT). Used for impact rings, charge-ups,
// beam grow-in, shake decay, camera zoom. Replaces linear lerps.
using System;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Content.Utils
{
    public static class YoyoEasing
    {
        public static float In(float t, int kind = 2) { t = MathHelper.Clamp(t, 0f, 1f); return (float)Math.Pow(t, kind); }
        public static float Out(float t, int kind = 2) { t = MathHelper.Clamp(t, 0f, 1f); return 1f - (float)Math.Pow(1f - t, kind); }
        public static float InOut(float t, int kind = 2) { t = MathHelper.Clamp(t, 0f, 1f); if (t < 0.5f) return (float)Math.Pow(t * 2f, kind) * 0.5f; return 1f - (float)Math.Pow((1f - t) * 2f, kind) * 0.5f; }
        public static float SineInOut(float t) { t = MathHelper.Clamp(t, 0f, 1f); return (float)(0.5 - 0.5 * Math.Cos(t * Math.PI)); }
        public static float ElasticOut(float t) { t = MathHelper.Clamp(t, 0f, 1f); if (t == 0f || t == 1f) return t; float p = 0.3f; return (float)(Math.Pow(2f, -10f * t) * Math.Sin((t - p / 4f) * (2f * Math.PI) / p) + 1f); }
        public static float Remap(float v, float fromMin, float fromMax, float toMin, float toMax, bool clamped = true)
        {
            if (clamped) v = MathHelper.Clamp(v, fromMin, fromMax);
            if (Math.Abs(fromMax - fromMin) < 1e-6f) return toMin;
            float n = (v - fromMin) / (fromMax - fromMin);
            return toMin + n * (toMax - toMin);
        }
    }
}
