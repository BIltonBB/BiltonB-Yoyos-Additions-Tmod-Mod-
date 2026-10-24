using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Systems
{
    /// <summary>
    /// Native high-performance metaball + Verlet fluid engine.
    /// No external dependencies. Pooled additive radial sprites (no render target, no shader).
    /// Particles use Verlet integration (current/old position) with distance constraints.
    /// </summary>
    public struct MetaballParticle
    {
        public Vector2 Position;
        public Vector2 OldPosition;   // Verlet: velocity = Position - OldPosition
        public float Scale;
        public float Density;        // alpha density (0..1)
        public Color Tint;           // per-fluid colour (pale azure unless the spawner picks one)
        public int Life;              // ticks remaining
        public int MaxLife;
        public bool Active;
        public bool NoGravity;       // true = ignores the global gravity (floating fluid)
    }

    public class MetaballSystem : ModSystem
    {
        // ---- Pooled particle storage (no per-frame allocs) ----
        private const int MAX_PARTICLES = 256;
        private static readonly MetaballParticle[] pool = new MetaballParticle[MAX_PARTICLES];
        private static int activeCount;

        // ---- Gravity for Verlet (world-space, px/tick^2) ----
        // Deliberately light, with per-step velocity drag in UpdateParticles, so
        // the fluid drifts and settles instead of dropping like stones.
        private static readonly Vector2 Gravity = new Vector2(0f, 0.055f);
        private const float Drag = 0.985f;   // velocity retained per step

        // Fluid colour when a spawner does not ask for one (the pale azure the
        // old threshold shader used for its rim).
        private static readonly Color DefaultFluid = new Color(165, 205, 255);

        // ---- Public spawn API ----
        /// <summary>Spawns one fluid particle. <paramref name="tint"/> lets the
        /// caller colour the fluid (e.g. crimson off the Dreadnautilus shell);
        /// omit it for the default pale azure. Pass <paramref name="noGravity"/>
        /// = true for floating fluid (no downward pull) - used by the arcane
        /// star stamps.</summary>
        public static void SpawnMetaball(Vector2 pos, Vector2 vel, float scale, float density, int life, Color? tint = null, bool noGravity = false)
        {
            if (Main.netMode == Terraria.ID.NetmodeID.Server) return;
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                if (cfg != null && !cfg.Visuals.Metaballs) return;
                // Player-tunable lifetime multiplier (Visuals.MetaballLifetime).
                if (cfg != null && cfg.Visuals.MetaballLifetime != 1f)
                    life = Math.Max(1, (int)Math.Round(life * cfg.Visuals.MetaballLifetime));
                // Player-tunable spawn-rate multiplier (Visuals.MetaballAmount,
                // 0.1x-5x). The budget accumulates fractionally, so 0.1 emits
                // one blob per ten calls and 5 emits up to five per call; the
                // pool's recycle-oldest logic keeps even 5x from freezing.
                if (cfg != null && cfg.Visuals.MetaballAmount != 1f)
                {
                    metaballBudget += MathHelper.Clamp(cfg.Visuals.MetaballAmount, 0.1f, 5f);
                    int emit = (int)metaballBudget;
                    if (emit <= 0)
                        return;
                    metaballBudget -= emit;
                    emit = Math.Min(emit, 10);
                    for (int e = 0; e < emit; e++)
                        SpawnMetaballSlot(pos, vel, scale, density, life, tint, noGravity);
                    return;
                }
                metaballBudget = 0f;
            }
            catch { }
            SpawnMetaballSlot(pos, vel, scale, density, life, tint, noGravity);
        }

        // Spawn-rate budget for the MetaballAmount multiplier (live config).
        private static float metaballBudget;

        // One blob into the pool. Find a free slot, or recycle the one closest
        // to dying. Falling back to "reject spawn" here saturated the pool and
        // froze the cluster.
        private static void SpawnMetaballSlot(Vector2 pos, Vector2 vel, float scale, float density, int life, Color? tint, bool noGravity)
        {
            int slot = -1;
            int oldest = 0;
            int oldestLife = int.MaxValue;
            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                if (!pool[i].Active) { slot = i; break; }
                if (pool[i].Life < oldestLife) { oldestLife = pool[i].Life; oldest = i; }
            }
            bool fresh = slot >= 0;
            if (!fresh)
                slot = oldest;
            pool[slot].Position = pos;
            pool[slot].OldPosition = pos - vel;
            pool[slot].Scale = scale;
            pool[slot].Density = density;
            pool[slot].Tint = tint ?? DefaultFluid;
            pool[slot].Life = life;
            pool[slot].MaxLife = life;
            pool[slot].Active = true;
            pool[slot].NoGravity = noGravity;
            if (fresh)
                activeCount++;
        }

        public override void OnModLoad()
        {
            On_Main.DrawProjectiles += DrawMetaballs;
        }

        public override void Unload()
        {
            activeCount = 0;
            for (int i = 0; i < MAX_PARTICLES; i++) pool[i].Active = false;
        }

        public override void OnModUnload()
        {
            On_Main.DrawProjectiles -= DrawMetaballs;
        }

        public override void OnWorldUnload()
        {
            // Reset particles on world change.
            activeCount = 0;
            for (int i = 0; i < MAX_PARTICLES; i++) pool[i].Active = false;
        }

        /// <summary>Verlet integration step. Call once per frame before draw.</summary>
        public static void UpdateParticles()
        {
            if (Main.netMode == Terraria.ID.NetmodeID.Server) return;
            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                if (!pool[i].Active) continue;
                // Verlet: velocity = Position - OldPosition; apply drag + light
                // gravity (or none at all for floating "no gravity" fluid).
                Vector2 vel = pool[i].Position - pool[i].OldPosition;
                Vector2 gravity = pool[i].NoGravity ? Vector2.Zero : Gravity;
                Vector2 next = pool[i].Position + vel * Drag + gravity;
                // Tile collision: settle on solid ground instead of sinking through
                // the world. On contact the blob simply does not advance (it keeps
                // its last non-solid position and loses its velocity), then its life
                // fade dissolves it - so the fluid reads as landing, not clipping.
                if (Collision.SolidCollision(next - new Vector2(3f, 3f), 6, 6))
                {
                    // Settle: keep the last non-solid position and kill velocity.
                    pool[i].OldPosition = pool[i].Position;
                }
                else
                {
                    pool[i].OldPosition = pool[i].Position;
                    pool[i].Position = next;
                }
                // The life envelope is applied at draw time (see LifeFade), so
                // Density stays exactly the spawn value: the blob eases in, holds,
                // then dissolves to nothing instead of popping when Life hits 0.
                pool[i].Life--;
                if (pool[i].Life <= 0)
                {
                    pool[i].Active = false;
                    activeCount = Math.Max(0, activeCount - 1);
                }
            }
        }

        /// <summary>Life envelope 0 -> 1 -> 0 (opacity multiplier). A short ease-in
        /// over the first 15% of life, then a long ease-out across the last 45%, so
        /// a particle always reaches ~0 alpha just before it dies (no pop).</summary>
        private static float LifeFade(int life, int maxLife)
        {
            if (maxLife <= 0)
                return 0f;
            float t = MathHelper.Clamp((float)life / maxLife, 0f, 1f);    // 1 at spawn -> 0 at death
            float born = MathHelper.Clamp((1f - t) / 0.15f, 0f, 1f);      // fade in
            float die = MathHelper.Clamp(t / 0.45f, 0f, 1f);              // fade out
            return born * die;
        }

        /// <summary>Draws queued additive glows and the fluid in a single
        /// additive batch that this method owns.</summary>
        private void DrawMetaballs(On_Main.orig_DrawProjectiles orig, Main self)
        {
            UpdateParticles();
            orig(self);
            if (Main.netMode == Terraria.ID.NetmodeID.Server) return;

            bool hasGlows = MiniYoyoFx.HasQueuedAdditiveGlows;
            // Nothing to draw: no live blobs and no queued glows.
            if (activeCount <= 0 && !hasGlows) return;

            // DrawProjectiles internally Begins/Ends its own batch, so NO batch is
            // active once it returns. We therefore OWN the batch opened below and
            // only ever End() a batch our own Begin() opened - a speculative End()
            // of a foreign/dead batch is exactly what threw "End was called, but
            // Begin has not yet been called" on every fluid frame (tModLoader's
            // first-chance logger recorded it).
            //
            // The fluid is drawn as pooled additive radial sprites. This replaces
            // the old half-res RT + threshold-shader fullscreen composite, which
            // had two fatal problems:
            //   1. it fed the pooled WORLD positions into the RT as if they were
            //      screen positions (Position / 2 under an identity matrix), so the
            //      fluid never rendered where it actually was;
            //   2. it swapped the render target mid-frame - stomping screen-space
            //      pipelines other mods may have bound - and finished with a
            //      FULLSCREEN quad, so any failure in that pass (including the
            //      threshold uniforms landing on the GPU as 0, which makes
            //      smoothstep(0, 0, 0) a 0/0 NaN) painted opaque black over the
            //      world, tiles, NPCs and every projectile while the player -
            //      drawn immediately after this pass - stayed lit.
            // Additive sprites can only ever ADD light, need no render target and
            // no shader, so this can neither darken the screen nor disturb another
            // mod's screen-space pipeline.
            Texture2D glow = ModContent.Request<Texture2D>("BiltonsYoyoMod/Projectiles/MiniYoyoGlow").Value;
            bool begun = false;
            try
            {
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                begun = true;
                MiniYoyoFx.DrawQueuedAdditiveGlowsInBatch();
                if (activeCount > 0)
                    DrawFluid(glow);
            }
            catch { }
            finally
            {
                if (begun)
                {
                    try { Main.spriteBatch.End(); } catch { }
                }
                // If the batch never opened the glows were not drawn; drop them so
                // the queues cannot grow without bound.
                MiniYoyoFx.ClearQueuedAdditiveGlows();
            }
        }

        /// <summary>Draws every live fluid particle into the currently open
        /// additive batch. Three soft falls per blob (broad mist / body / small
        /// hot core) instead of one white ball: neighbours then sum into a fused
        /// silhouette, which is what the threshold shader was for, and the small
        /// core keeps each blob from reading as a hard sphere.</summary>
        private static void DrawFluid(Texture2D glow)
        {
            var sb = Main.spriteBatch;
            for (int i = 0; i < MAX_PARTICLES; i++)
            {
                if (!pool[i].Active) continue;
                float d = MathHelper.Clamp(pool[i].Density, 0f, 1f) * LifeFade(pool[i].Life, pool[i].MaxLife);
                if (d <= 0.002f) continue;
                float diam = 46f * MathHelper.Max(pool[i].Scale, 0.05f);
                Vector2 pos = pool[i].Position - Main.screenPosition;
                Color tint = pool[i].Tint;
                // Additive rgb is multiplied by the source alpha, so the density
                // rides in the rgb with alpha forced to 255 (same trick as
                // MiniYoyoFx.DrawQueuedAdditiveGlowsInBatch) - no double dip.
                Color mist = tint * (d * 0.26f);
                mist.A = 255;
                Color body = tint * (d * 0.52f);
                body.A = 255;
                Color core = Color.Lerp(tint, Color.White, 0.65f) * (d * 0.62f);
                core.A = 255;
                sb.Draw(glow, pos, null, mist, 0f, glow.Size() * 0.5f,
                    diam * 1.45f / glow.Width, SpriteEffects.None, 0f);
                sb.Draw(glow, pos, null, body, 0f, glow.Size() * 0.5f,
                    diam / glow.Width, SpriteEffects.None, 0f);
                sb.Draw(glow, pos, null, core, 0f, glow.Size() * 0.5f,
                    diam * 0.38f / glow.Width, SpriteEffects.None, 0f);
            }
        }

        /// <summary>Verlet distance constraint: keep two particles within [min,max] distance.</summary>
        public static void ApplyConstraint(ref MetaballParticle a, ref MetaballParticle b, float targetDist)
        {
            Vector2 diff = b.Position - a.Position;
            float dist = diff.Length();
            if (dist < 0.001f) return;
            float correction = (dist - targetDist) / dist * 0.5f;
            Vector2 move = diff * correction;
            a.Position += move;
            b.Position -= move;
        }
    }
}
