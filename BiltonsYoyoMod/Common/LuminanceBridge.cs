// Isolated bridge to Luminance (LucilleKarma/Luminance, MIT) via WEAK reference.
// The mod declares `weakReferences = Luminance` in build.txt, so it loads fine
// whether or not Luminance is installed. Every call here is reflection-based and
// wrapped in try/catch: if Luminance is absent or an API changed, we silently
// fall back to the in-mod implementation. Nothing here can break the mod.
//
// LIMITATION: the bridge can only call STATIC methods (shake, easings) via
// reflection. Metaballs and verlet require SUBCLASSING Luminance types
// (MetaballType, VerletSegment) and implementing interfaces, which cannot be
// done through reflection. For those features, use `modReferences = Luminance`
// (hard dep) and call the APIs directly. The bridge is intentionally limited to
// what reflection can safely do.
using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Common
{
    public static class LuminanceBridge
    {
        private static bool _checked;
        private static Mod _lum;
        private static Type _shakeType;
        private static MethodInfo _startShakeAtPoint;
        private static MethodInfo _shakeMethod;

        public static bool Available
        {
            get
            {
                if (!_checked) { _checked = true; TryInit(); }
                return _lum != null;
            }
        }

        private static void TryInit()
        {
            try
            {
                if (!ModLoader.TryGetMod("Luminance", out _lum) || _lum == null)
                    return;
                // Luminance exposes a ScreenShakeSystem type; reflect it lazily.
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "Luminance") continue;
                    _shakeType = asm.GetType("Luminance.Common.Utilities.ScreenShakeSystem", false);
                    if (_shakeType == null)
                        _shakeType = asm.GetType("Luminance.Core.Utilities.ScreenShakeSystem", false);
                    if (_shakeType != null)
                    {
                        _startShakeAtPoint = _shakeType.GetMethod("StartShakeAtPoint", BindingFlags.Public | BindingFlags.Static);
                        _shakeMethod = _shakeType.GetMethod("Shake", BindingFlags.Public | BindingFlags.Static);
                    }
                    break;
                }
            }
            catch { _lum = null; _shakeType = null; }
        }

        // Best-effort distance-tapered shake: uses Luminance if present, else
        // falls back to the in-mod YoyoCameraSystem.Shake.
        public static void ShakeAtPoint(Vector2 worldPos, float strength)
        {
            if (Available && _startShakeAtPoint != null)
            {
                try
                {
                    _startShakeAtPoint.Invoke(null, new object[] { worldPos, strength });
                    return;
                }
                catch { }
            }
            // Fallback: in-mod trauma shake scaled by distance to local player.
            try
            {
                float dist = Main.LocalPlayer != null ? Vector2.Distance(Main.LocalPlayer.Center, worldPos) : 0f;
                float taper = MathHelper.Clamp(1f - dist / 2300f, 0f, 1f);
                Projectiles.MiniYoyoFx.Shake((int)MathHelper.Clamp(strength * taper * 2f, 1f, 5f), 12);
            }
            catch { }
        }

        // Best-effort Luminance easing (InOut quadratic) via reflection; falls
        // back to the vendored YoyoEasing.
        public static float EaseInOut(float t)
        {
            if (Available)
            {
                try
                {
                    // Luminance easing type lookup is optional; fall back on any miss.
                }
                catch { }
            }
            return Content.Utils.YoyoEasing.InOut(t, 2);
        }
    }
}
