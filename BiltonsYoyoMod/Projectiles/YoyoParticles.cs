using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Lightweight Calamity-style particle pools (NOT dust-capped): directional
    // sparks with drag/gravity/fade, and bloom orbs that grow then fade out.
    // Simulated per-client in YoyoParticleSystem (visual only, never synced).
    // Drawn batched from projectile PreDraws with a same-frame guard, so a crowd
    // of yoyos can never multiply-draw the pool. Server-safe: spawns no-op on
    // dedicated servers and Draw is never reached there (PreDraw is client-only).
    public static class YoyoParticles
    {
        private struct Spark
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public Color Color;
            public float Scale;
            public float Drag;
            public float Gravity;
            public int Life;
            public int MaxLife;
            // Particle v2 fields (from 15 AI notes):
            public float Rotation;
            public float Spin;
            public int Variant;
            public bool AffectedByLight;
        }

        private struct Bloom
        {
            public Vector2 Pos;
            public float StartR;
            public float EndR;
            public Color Color;
            public float Alpha;
            public int Life;
            public int MaxLife;
            // Vertical squash (1 = round, <1 = flat ground decal / directional flash).
            public float Squash;
            // Optional drift (smoke puffs): velocity + per-tick drag. Zero = static.
            public Vector2 Vel;
            public float Drag;
        }

        private struct Ring
        {
            public Vector2 Pos;
            public float StartR;
            public float EndR;
            public Color Color;
            public float Width;
            public int Life;
            public int MaxLife;
        }

        // Instant line-slash: bright straight streak across the victim, drawn
        // the same tick as the hit (no projectile spawn, no 1-tick delay).
        private struct SlashLine
        {
            public Vector2 Pos;
            public float Angle;
            public float Length;
            public Color Color;
            public float Width;
            public int Life;
            public int MaxLife;
        }

        // Ground-decal family (generalization of GroundScorch, vault L2#6):
        // static flat ground marks that fade by alpha over life — never a
        // round growing circle. Style picks the shape.
        public enum DecalStyle
        {
            Soft,  // squashed soft blotch (scorch-family read)
            Crack  // jagged stress fracture with a tinted seam
        }

        private struct Decal
        {
            public Vector2 Pos;
            public float Angle;   // lying direction of the mark
            public float Size;    // length scale
            public Color Color;   // seam / tint color
            public float Alpha;
            public DecalStyle Style;
            public int Life;
            public int MaxLife;
            public int Seed;
        }

        private const int MaxSparks = 400;
        private const int MaxBlooms = 64;
        private const int MaxRings = 24;
        private const int MaxSlashes = 48;
        private const int MaxDecals = 24;

        private static readonly List<Spark> sparks = new List<Spark>();
        private static readonly List<Bloom> blooms = new List<Bloom>();
        private static readonly List<Ring> rings = new List<Ring>();
        private static readonly List<SlashLine> slashes = new List<SlashLine>();
        private static readonly List<Decal> decals = new List<Decal>();
        private static long drawnFrame = -1;

        public static int ActiveCount => sparks.Count + blooms.Count + rings.Count + slashes.Count + decals.Count;

        // Everglow-style quality tier: Low halves pooled particle density.
        internal static bool LowQuality()
        {
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                return cfg != null && cfg.Visuals.Quality == Configuration.YoyoVisualQuality.Low;
            }
            catch
            {
                return false;
            }
        }

        // Particle amount slider (Calamity ParticleLimit-style): scales spawn
        // density for every pooled type. Low quality halves on top of it.
        internal static float Density()
        {
            float d = 1f;
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                if (cfg != null)
                    d = MathHelper.Clamp(cfg.Visuals.ParticleAmount, 0.25f, 2f);
            }
            catch
            {
            }
            if (LowQuality())
                d *= 0.5f;
            return d;
        }

        public static void SpawnSpark(Vector2 pos, Vector2 vel, Color color, float scale = 1f, int life = 30, float drag = 0.96f, float gravity = 0f, float rotation = 0f, float spin = 0f, int variant = 0, bool affectedByLight = true)
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            if (Main.rand.NextFloat() > Density())
                return;
            if (sparks.Count >= MaxSparks)
                sparks.RemoveAt(0);
            sparks.Add(new Spark
            {
                Pos = pos,
                Vel = vel,
                Color = color,
                Scale = scale,
                Drag = drag,
                Gravity = gravity,
                Life = life,
                MaxLife = life,
                Rotation = rotation == 0f ? Main.rand.NextFloat(MathHelper.TwoPi) : rotation,
                Spin = spin,
                Variant = variant == 0 ? Main.rand.Next(3) : variant,
                AffectedByLight = affectedByLight
            });
        }

        // Circular burst helper (directional debris without per-call loops).
        public static void SpawnBurst(Vector2 pos, int count, float speed, Color color, float scale = 1f, int life = 30, float gravity = 0f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            count = System.Math.Max(2, (int)(count * Density()));
            for (int i = 0; i < count; i++)
            {
                float angle = MathHelper.TwoPi * i / count;
                SpawnSpark(pos, angle.ToRotationVector2() * speed, color, scale, life, 0.94f, gravity);
            }
        }

        // The fade-out glow bloom: soft orb growing startR -> endR while fading.
        // squash < 1 flattens it into a ground decal / directional flash (no circle read).
        public static void SpawnBloom(Vector2 pos, float startR, float endR, int life, Color color, float alpha = 0.6f, float squash = 1f, Vector2 vel = default(Vector2), float drag = 0f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            if (Main.rand.NextFloat() > Density())
                return;
            if (blooms.Count >= MaxBlooms)
                blooms.RemoveAt(0);
            blooms.Add(new Bloom
            {
                Pos = pos,
                StartR = startR,
                EndR = endR,
                Color = color,
                Alpha = alpha,
                Life = life,
                MaxLife = life,
                Squash = squash,
                Vel = vel,
                Drag = drag
            });
        }

        // Expanding shockwave ring from spawn-side code (kills, breaches).
        public static void SpawnRing(Vector2 pos, float startR, float endR, int life, Color color, float width = 2.5f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            if (Main.rand.NextFloat() > Density())
                return;
            if (rings.Count >= MaxRings)
                rings.RemoveAt(0);
            rings.Add(new Ring
            {
                Pos = pos,
                StartR = startR,
                EndR = endR,
                Color = color,
                Width = width,
                Life = life,
                MaxLife = life
            });
        }

        // Instant hit line-slash: white-hot straight streak across the victim.
        public static void SpawnSlashLine(Vector2 pos, float angle, float length, Color color, float width = 3f, int life = 8)
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            if (slashes.Count >= MaxSlashes)
                slashes.RemoveAt(0);
            slashes.Add(new SlashLine
            {
                Pos = pos,
                Angle = angle,
                Length = length,
                Color = color,
                Width = width,
                Life = life,
                MaxLife = life
            });
        }

        // Ground-decal family entry point: crack / soft blotch marks that fade
        // by alpha over life (2-6 s living decals are the sweet spot). Shapes
        // are static and lying flat — only alpha moves.
        public static void SpawnDecal(Vector2 pos, DecalStyle style, Color color, float size = 1f, float alpha = 0.5f, int life = 240, float angle = 0f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            if (Main.rand.NextFloat() > Density())
                return;
            if (decals.Count >= MaxDecals)
                decals.RemoveAt(0);
            decals.Add(new Decal
            {
                Pos = pos,
                Angle = angle,
                Size = size,
                Color = color,
                Alpha = alpha,
                Style = style,
                Life = life,
                MaxLife = life,
                Seed = Main.rand.Next(1, 1 << 24)
            });
        }

        internal static void Update()
        {
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                Spark s = sparks[i];
                s.Life--;
                if (s.Life <= 0)
                {
                    sparks.RemoveAt(i);
                    continue;
                }
                s.Vel *= s.Drag;
                s.Vel.Y += s.Gravity;
                s.Pos += s.Vel;
                s.Rotation += s.Spin;
                sparks[i] = s;
            }
            for (int i = blooms.Count - 1; i >= 0; i--)
            {
                Bloom b = blooms[i];
                b.Life--;
                if (b.Life <= 0)
                {
                    blooms.RemoveAt(i);
                    continue;
                }
                // Optional drift (smoke puffs). Zero by default, so pre-existing
                // spawns keep their exact old static behavior.
                if (b.Drag > 0f)
                    b.Vel *= b.Drag;
                b.Pos += b.Vel;
                blooms[i] = b;
            }
            for (int i = rings.Count - 1; i >= 0; i--)
            {
                Ring r = rings[i];
                r.Life--;
                if (r.Life <= 0)
                    rings.RemoveAt(i);
                else
                    rings[i] = r;
            }
            for (int i = slashes.Count - 1; i >= 0; i--)
            {
                SlashLine s = slashes[i];
                s.Life--;
                if (s.Life <= 0)
                    slashes.RemoveAt(i);
                else
                    slashes[i] = s;
            }
            for (int i = decals.Count - 1; i >= 0; i--)
            {
                Decal d = decals[i];
                d.Life--;
                if (d.Life <= 0)
                    decals.RemoveAt(i);
                else
                    decals[i] = d;
            }
        }

        internal static void Clear()
        {
            sparks.Clear();
            blooms.Clear();
            rings.Clear();
            slashes.Clear();
            decals.Clear();
            drawnFrame = -1;
        }

        // Batched world-space draw. Safe to call from any PreDraw/PostDraw; the
        // same-frame guard makes repeat calls within one frame free.
        public static void Draw()
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            if (drawnFrame == (long)Main.GameUpdateCount)
                return;
            drawnFrame = (long)Main.GameUpdateCount;
            if (sparks.Count == 0 && blooms.Count == 0 && rings.Count == 0 && slashes.Count == 0 && decals.Count == 0)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Texture2D glow = MiniYoyoFx.GlowTexture;

            // Ground decals first (they lie under everything else).
            for (int i = 0; i < decals.Count; i++)
            {
                Decal d = decals[i];
                float t = 1f - d.Life / (float)d.MaxLife;
                // Quick fade-in, long tail: a mark, not a flash.
                float a = d.Alpha * MathHelper.Clamp(t / 0.06f, 0f, 1f) * (1f - t * t);
                if (a <= 0.01f)
                    continue;
                Vector2 pos = d.Pos - Main.screenPosition;
                if (d.Style == DecalStyle.Soft)
                {
                    float w = 40f * d.Size;
                    Main.spriteBatch.Draw(glow, pos, null, d.Color * a, 0f, glow.Size() * 0.5f,
                        new Vector2(w / glow.Width, w * 0.38f / glow.Width), SpriteEffects.None, 0f);
                    continue;
                }
                // Crack: 5 deterministic jagged segments, tapering, with a tinted
                // seam inside and a spur at two joints. Static, lying flat.
                float baseAng = d.Angle;
                float segLen = 12f * d.Size;
                Vector2 dir = baseAng.ToRotationVector2();
                Vector2 start = pos - dir * (segLen * 2.5f);
                Vector2 p = start;
                float ang = baseAng;
                Color dark = new Color(15, 11, 8) * (a * 0.95f);
                Color seam = d.Color * (a * 0.5f);
                for (int s = 0; s < 5; s++)
                {
                    float jit = (((d.Seed >> (s * 3)) & 7) / 7f - 0.5f) * 0.6f;
                    ang = MathHelper.Clamp(ang + jit, baseAng - 0.7f, baseAng + 0.7f);
                    Vector2 q = p + ang.ToRotationVector2() * segLen;
                    Vector2 mid = (p + q) * 0.5f;
                    float segAng = (q - p).ToRotation();
                    float w = (2.8f - s * 0.3f) * d.Size;
                    Main.spriteBatch.Draw(pixel, mid, new Rectangle(0, 0, 1, 1), dark, segAng,
                        new Vector2(0.5f, 0.5f), new Vector2(segLen + 1.5f, w), SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(pixel, mid, new Rectangle(0, 0, 1, 1), seam, segAng,
                        new Vector2(0.5f, 0.5f), new Vector2(segLen * 0.8f, w * 0.4f), SpriteEffects.None, 0f);
                    if (s == 1 || s == 3)
                    {
                        float spurAng = ang + (s == 1 ? 0.85f : -0.95f);
                        Vector2 spurEnd = q + spurAng.ToRotationVector2() * (segLen * 0.6f);
                        Vector2 spurMid = (q + spurEnd) * 0.5f;
                        Main.spriteBatch.Draw(pixel, spurMid, new Rectangle(0, 0, 1, 1), dark * 0.85f, spurAng,
                            new Vector2(0.5f, 0.5f), new Vector2(segLen * 0.6f, w * 0.7f), SpriteEffects.None, 0f);
                    }
                    p = q;
                }
                // Faint tinted glints at both fracture tips (seep read).
                float tip = 9f / glow.Width;
                Main.spriteBatch.Draw(glow, start, null, d.Color * (a * 0.55f), 0f, glow.Size() * 0.5f, tip, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(glow, p, null, d.Color * (a * 0.4f), 0f, glow.Size() * 0.5f, tip * 0.8f, SpriteEffects.None, 0f);
            }

            for (int i = slashes.Count - 1; i >= 0; i--)
            {
                SlashLine s = slashes[i];
                float fade = s.Life / (float)s.MaxLife;
                float len = s.Length * (0.5f + 0.5f * fade) + 4f;
                // Layered radial lenses instead of a flat 1x1 MagicPixel quad:
                // wide dim halo, tinted body, white-hot core. Every edge is a
                // gradient, so the streak reads as a stroke of light rather than
                // one solid coloured bar.
                Color hot = Color.Lerp(s.Color, Color.White, 0.75f);
                MiniYoyoFx.DrawSoftRay(s.Pos, s.Angle, len * 1.12f, s.Width * 3.4f, s.Color, fade * 0.30f);
                MiniYoyoFx.DrawSoftRay(s.Pos, s.Angle, len, s.Width * 1.50f, s.Color, fade * 0.75f);
                MiniYoyoFx.DrawSoftRay(s.Pos, s.Angle, len * 0.90f, s.Width * 0.55f, hot, fade * 0.85f);
            }

            for (int i = 0; i < blooms.Count; i++)
            {
                Bloom b = blooms[i];
                float t = 1f - b.Life / (float)b.MaxLife;
                float diameter = MathHelper.Lerp(b.StartR, b.EndR, t) * 2f;
                if (diameter <= 0.5f)
                    continue;
                Vector2 pos = b.Pos - Main.screenPosition;
                Main.spriteBatch.Draw(glow, pos, null, b.Color * (b.Alpha * (1f - t)), 0f,
                    glow.Size() * 0.5f, new Vector2(diameter / glow.Width, diameter * b.Squash / glow.Width), SpriteEffects.None, 0f);
            }

            for (int i = 0; i < sparks.Count; i++)
            {
                Spark s = sparks[i];
                float fade = s.Life / (float)s.MaxLife;
                float speed = s.Vel.Length();
                Color drawColor = s.Color;
                if (s.AffectedByLight)
                    drawColor = drawColor.MultiplyRGB(Lighting.GetColor(s.Pos.ToTileCoordinates()));
                // Life-state animation (L2#5): pop in over 3 ticks, flutter while
                // alive, then shrink + darken over the last 4 ticks so motes die
                // out instead of switching off. Phase is position-derived (stable
                // per mote, no struct changes).
                float age = s.MaxLife - s.Life;
                float growIn = MathHelper.Clamp(age / 3f, 0f, 1f);
                float dying = MathHelper.Clamp(s.Life / 4f, 0f, 1f);
                float flicker = 0.9f + 0.1f * (float)System.Math.Sin((s.Pos.X + s.Pos.Y) * 0.35f + Main.GameUpdateCount * 0.7f);
                float sizeMul = (0.4f + 0.6f * growIn) * (0.3f + 0.7f * dying);
                float rgbMul = 0.4f + 0.6f * dying;
                drawColor = drawColor.MultiplyRGB(new Color(rgbMul, rgbMul, rgbMul));
                float rot = s.Rotation + (speed > 0.05f ? s.Vel.ToRotation() * 0.3f : 0f);
                // A MOTE IS A LENS, NOT A QUAD. This draw used to be a stretched
                // 1x1 MagicPixel whose alpha was multiplied by `flicker` - a hard
                // cornered bar flickering on a sine, and because sparks are shed
                // by every attack in the mod it was a rectangle on all of them,
                // lightning or not. Length, thickness, rotation and fade are
                // bit-for-bit the same; only the corners are gone (two lenses
                // now, so a mote reads as a streak of light instead of a chip).
                float len = (5f * s.Scale * fade + 2f) * sizeMul;
                float wide = 2.2f * s.Scale * sizeMul;
                float life = fade * growIn * (0.45f + 0.55f * dying) * flicker;
                MiniYoyoFx.DrawSoftRay(s.Pos, rot, len, wide * 2.6f, drawColor, life * 0.55f);
                MiniYoyoFx.DrawSoftRay(s.Pos, rot, len * 0.8f, wide * 1.2f,
                    Color.Lerp(drawColor, Color.White, 0.35f), life * 0.7f);
            }

            for (int i = 0; i < rings.Count; i++)
            {
                Ring r = rings[i];
                float t = 1f - r.Life / (float)r.MaxLife;
                float radius = MathHelper.Lerp(r.StartR, r.EndR, t);
                if (radius <= 0.5f)
                    continue;
                MiniYoyoFx.DrawRing(r.Pos, radius, r.Width, r.Color * (1f - t));
                // Comet rider: one bright bead with a fading trail instead of
                // static orbiting dots — the ring reads as motion, not decor.
                float comet = t * 5f;
                for (int k = 0; k < 3; k++)
                {
                    float lag = k * 0.35f;
                    float ca = comet - lag;
                    if (ca < 0f)
                        continue;
                    Vector2 cpos = r.Pos + ca.ToRotationVector2() * radius;
                    MiniYoyoFx.DrawGlow(cpos, 10f - k * 2.5f, r.Color, (0.65f - k * 0.18f) * (1f - t));
                }
            }
        }
    }

    public class YoyoParticleSystem : ModSystem
    {
        public override void PostUpdateDusts()
        {
            YoyoParticles.Update();
        }

        public override void OnWorldUnload()
        {
            YoyoParticles.Clear();
        }
    }
}
