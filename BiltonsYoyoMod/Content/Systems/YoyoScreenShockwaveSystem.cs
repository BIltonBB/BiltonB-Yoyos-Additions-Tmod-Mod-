using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Systems
{
    // L5 screen shockwave: post-process filter that ripples the captured frame
    // with one thin expanding compression ring (radial refraction, no filled
    // disc). Client-only; skipped when Visuals.Effects or UseShaders is off, or
    // Photosensitivity is on. The envelope is driven from PostUpdateEverything
    // so the FilterManager's own +/-1/sec opacity ramp never takes over.
    public class YoyoScreenShockwaveSystem : ModSystem
    {
        public const string FilterName = "BiltonsYoyoMod:YoyoShock";

        private const float Duration = 34f; // ticks (~0.57 s)
        private const float MaxOpacity = 0.55f;

        private static Filter shockFilter;
        private static float shockTicks;
        private static float shockStrength;
        private static Color shockTint = new Color(255, 220, 170);

        public override void Load()
        {
            if (Main.dedServ)
                return;
            try
            {
                // AsyncLoad (not ImmediateLoad): requesting an Effect immediately
                // during mod load blocks the loader thread - tModLoader warns
                // "spent Nms blocking on asset loading". ScreenShaderData takes
                // the Asset<Effect> and resolves it lazily on first use.
                var effect = ModContent.Request<Effect>("BiltonsYoyoMod/Effects/YoyoShock", AssetRequestMode.AsyncLoad);
                shockFilter = new Filter(new ScreenShaderData(effect, "ShockPass"), EffectPriority.Medium);
                Filters.Scene[FilterName] = shockFilter;
            }
            catch
            {
                shockFilter = null;
            }
        }

        public override void Unload()
        {
            try { Filters.Scene.Deactivate(FilterName); } catch { }
            shockFilter = null;
            shockTicks = 0f;
        }

        public override void OnWorldUnload()
        {
            try { Filters.Scene.Deactivate(FilterName); } catch { }
            shockTicks = 0f;
        }

        // worldPos: blast origin. strength 0.15..0.55 caps the filter opacity.
        public static void Trigger(Vector2 worldPos, float strength, Color tint)
        {
            if (Main.dedServ || shockFilter == null)
                return;
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                if (cfg != null && (!cfg.Visuals.Effects || !cfg.Visuals.UseShaders || cfg.Visuals.Photosensitivity))
                    return;
            }
            catch
            {
            }
            // Distance taper: off-screen pounds must not ripple the whole frame.
            float dist = Main.LocalPlayer != null ? Vector2.Distance(Main.LocalPlayer.Center, worldPos) : 0f;
            float taper = MathHelper.Clamp(1f - dist / 2600f, 0f, 1f);
            if (taper <= 0.15f)
                return;
            strength = MathHelper.Clamp(strength * taper, 0.12f, MaxOpacity);
            // Overlapping triggers: strongest wins, no stacking.
            if (shockTicks > 0f && strength <= shockStrength)
                return;
            shockStrength = strength;
            shockTint = tint;
            shockTicks = Duration;
            try
            {
                shockFilter.Activate(worldPos);
                var shader = shockFilter.GetShader();
                shader.UseColor(tint);
                shader.UseProgress(0f);
                shader.UseGlobalOpacity(0f);
                shockFilter.Opacity = 0f;
            }
            catch
            {
            }
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || shockFilter == null || shockTicks <= 0f)
                return;
            shockTicks--;
            float t = 1f - shockTicks / Duration; // 0 -> 1 over the envelope
            // Fast rise (~6 ticks), long quadratic decay; overrides the manager ramp.
            float env = MathHelper.Clamp(t / 0.18f, 0f, 1f) * (1f - t) * (1f - t) * shockStrength;
            env = MathHelper.Clamp(env, 0f, MaxOpacity);
            try
            {
                var shader = shockFilter.GetShader();
                shader.UseProgress(t);
                shader.UseGlobalOpacity(env);
                shockFilter.Opacity = env;
                if (shockTicks <= 0f)
                    shockFilter.Deactivate();
            }
            catch
            {
            }
        }
    }
}
