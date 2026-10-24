using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // L1: the baked shape bank. Twelve effect shapes ship as PNGs under
    // Assets/Shapes (the primary source, per design); any shape whose PNG is
    // unavailable - or the whole bank when the config asks for Procedural - is
    // baked once at runtime from code, so the system never hard-depends on an
    // asset file. All shapes are white-on-transparent so they tint per family.
    //
    // Two ways to use it:
    //   Draw(...)        immediate, for spritebatch contexts (PreDraw/AI draws)
    //   Spawn/SpawnFan   queued bursts, for update contexts (OnKill/OnHit) and
    //                    for anything that should animate for a few ticks;
    //                    drawn by MiniBossShapeBankSystem after the tile pass.
    internal enum ShapeId
    {
        SmearCrescent,
        SmearHook,
        Chevron,
        SplatSoft,
        SplatStar,
        RingCracked,
        WispTaper,
        FlakeSix,
        NeedleLong,
        EmberCurl,
        SealRune,
        BoltZig,
    }

    internal static class MiniBossShapeBank
    {
        public const int Variants = 12;

        private static readonly string[] Names =
        {
            "SmearCrescent", "SmearHook", "Chevron", "SplatSoft", "SplatStar",
            "RingCracked", "WispTaper", "FlakeSix", "NeedleLong", "EmberCurl",
            "SealRune", "BoltZig",
        };

        private static readonly Texture2D[] png = new Texture2D[Variants];
        private static readonly Texture2D[] baked = new Texture2D[Variants];
        private static readonly bool[] pngChecked = new bool[Variants];

        private static Configuration.ShapeArtSource Source()
        {
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            return cfg?.Visuals.ShapeArt ?? Configuration.ShapeArtSource.Generated;
        }

        // ---------------- loading / baking ----------------

        internal static Texture2D Get(ShapeId id)
        {
            int i = (int)id;
            if (i < 0 || i >= Variants)
                return null;
            if (Source() == Configuration.ShapeArtSource.Generated)
            {
                if (!pngChecked[i])
                {
                    pngChecked[i] = true;
                    try
                    {
                        string path = "BiltonsYoyoMod/Assets/Shapes/" + Names[i];
                        if (ModContent.RequestIfExists<Texture2D>(path, out var asset) && asset?.Value != null)
                            png[i] = asset.Value;
                    }
                    catch
                    {
                        png[i] = null;
                    }
                }
                if (png[i] != null)
                    return png[i];
            }
            return GetBaked(i);
        }

        private static Texture2D GetBaked(int i)
        {
            if (baked[i] != null)
                return baked[i];
            if (Main.dedServ || Main.graphics == null)
                return null;
            try
            {
                baked[i] = Bake(i);
            }
            catch
            {
                baked[i] = null;
            }
            return baked[i];
        }

        // 64x64 white-on-transparent runtime bake of the same 12 shapes the
        // PNGs carry (the no-asset fallback path).
        private static Texture2D Bake(int i)
        {
            const int N = 64;
            Color[] px = new Color[N * N];

            void Disc(float cx, float cy, float r, float amount)
            {
                if (r <= 0f || amount <= 0f)
                    return;
                for (int y = 0; y < N; y++)
                {
                    float dy = y + 0.5f - cy;
                    for (int x = 0; x < N; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float d = MathF.Sqrt(dx * dx + dy * dy);
                        float cov = MathHelper.Clamp(r - d + 0.5f, 0f, 1f) * MathHelper.Clamp(amount, 0f, 1f);
                        if (cov <= 0f)
                            continue;
                        int idx = y * N + x;
                        int a = px[idx].A + (int)(cov * 255f);
                        px[idx] = new Color(255, 255, 255, (byte)Math.Clamp(a, 0, 255));
                    }
                }
            }

            void Cut(float cx, float cy, float r)
            {
                if (r <= 0f)
                    return;
                for (int y = 0; y < N; y++)
                {
                    float dy = y + 0.5f - cy;
                    for (int x = 0; x < N; x++)
                    {
                        float dx = x + 0.5f - cx;
                        float d = MathF.Sqrt(dx * dx + dy * dy);
                        float cov = MathHelper.Clamp(r - d + 0.5f, 0f, 1f);
                        if (cov <= 0f)
                            continue;
                        int idx = y * N + x;
                        px[idx] = new Color(255, 255, 255, (byte)(px[idx].A * (1f - cov)));
                    }
                }
            }

            void Chain(Func<float, Vector2> at, Func<float, float> rad, float amount = 1f, int steps = 40)
            {
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vector2 p = at(t);
                    Disc(p.X, p.Y, MathHelper.Max(rad(t), 0.55f), amount);
                }
            }

            float C = N * 0.5f;
            switch ((ShapeId)i)
            {
                case ShapeId.SmearCrescent:
                    Disc(C - 3f, C, 24f, 1f);
                    Cut(C + 10f, C - 7f, 22f);
                    break;

                case ShapeId.SmearHook:
                    Chain(t => new Vector2(C + MathF.Cos(-2.3f + t * 2.7f) * 17f, C + MathF.Sin(-2.3f + t * 2.7f) * 17f),
                          t => MathHelper.Lerp(7f, 1f, t));
                    break;

                case ShapeId.Chevron:
                    for (int k = 0; k < 2; k++)
                    {
                        float off = k * 14f;
                        Chain(t => new Vector2(MathHelper.Lerp(6f + off, 24f + off, t), MathHelper.Lerp(10f, 32f, t)), _ => 4.5f);
                        Chain(t => new Vector2(MathHelper.Lerp(24f + off, 6f + off, t), MathHelper.Lerp(32f, 54f, t)), _ => 4.5f);
                    }
                    break;

                case ShapeId.SplatSoft:
                    Disc(C, C, 22f, 0.30f);
                    Disc(C, C, 15f, 0.35f);
                    Disc(C, C, 8f, 0.40f);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = k * 1.2566f + 0.4f;
                        Disc(C + MathF.Cos(a) * 19f, C + MathF.Sin(a) * 19f, 4.5f - k * 0.3f, 0.22f);
                    }
                    break;

                case ShapeId.SplatStar:
                    for (int k = 0; k < 9; k++)
                    {
                        float a = k * (MathHelper.TwoPi / 9f) + (float)Math.Sin(k * 7.3f) * 0.13f;
                        Chain(t => new Vector2(C + MathF.Cos(a) * MathHelper.Lerp(3f, 27f, t), C + MathF.Sin(a) * MathHelper.Lerp(3f, 27f, t)),
                              t => MathHelper.Lerp(6f, 0.9f, t), 1f, 20);
                    }
                    Disc(C, C, 4.5f, 0.8f);
                    break;

                case ShapeId.RingCracked:
                    Chain(t =>
                    {
                        float seg = (t * 5f) % 1f;
                        if (seg > 0.82f)
                            return new Vector2(-99f, -99f);     // gap
                        float a = t * MathHelper.TwoPi;
                        return new Vector2(C + MathF.Cos(a) * 20f, C + MathF.Sin(a) * 20f);
                    }, _ => 3.4f, 1f, 90);
                    for (int k = 0; k < 3; k++)
                    {
                        float a = k * 2.1f + 0.5f;
                        Chain(t => new Vector2(C + MathF.Cos(a) * MathHelper.Lerp(20f, 27f, t), C + MathF.Sin(a) * MathHelper.Lerp(20f, 27f, t)),
                              t => MathHelper.Lerp(2f, 0.6f, t), 1f, 6);
                    }
                    break;

                case ShapeId.WispTaper:
                    Chain(t => new Vector2(MathHelper.Lerp(8f, 56f, t), C + MathF.Sin(t * 6.2f) * 9f * (1f - 0.35f * t)),
                          t => MathHelper.Lerp(5.5f, 0.8f, t));
                    break;

                case ShapeId.FlakeSix:
                    for (int k = 0; k < 6; k++)
                    {
                        float a = k * MathF.PI / 3f;
                        Chain(t => new Vector2(C + MathF.Cos(a) * MathHelper.Lerp(2f, 27f, t), C + MathF.Sin(a) * MathHelper.Lerp(2f, 27f, t)),
                              t => MathHelper.Lerp(4.6f, 0.9f, t), 1f, 20);
                    }
                    Disc(C, C, 4f, 0.7f);
                    break;

                case ShapeId.NeedleLong:
                    Chain(t => new Vector2(C, MathHelper.Lerp(5f, 59f, t)),
                          t => 2.6f * MathF.Sin(t * MathF.PI) + 0.7f);
                    break;

                case ShapeId.EmberCurl:
                    Chain(t =>
                    {
                        float a = t * 4.6f;
                        float rad = 5f + 17f * t;
                        return new Vector2(C + MathF.Cos(a) * rad, C + MathF.Sin(a) * rad);
                    }, t => MathHelper.Lerp(4.5f, 0.8f, t));
                    break;

                case ShapeId.SealRune:
                    Chain(t => new Vector2(C + MathF.Cos(t * MathHelper.TwoPi) * 20f, C + MathF.Sin(t * MathHelper.TwoPi) * 20f),
                          _ => 2.4f, 1f, 70);
                    Chain(t =>
                    {
                        float per = (t * 3f) % 1f;
                        int v = (int)(t * 3f);
                        float a0 = -MathHelper.PiOver2 + v * MathHelper.TwoPi / 3f;
                        float a1 = -MathHelper.PiOver2 + (v + 1) * MathHelper.TwoPi / 3f;
                        Vector2 p0 = new Vector2(C + MathF.Cos(a0) * 12f, C + MathF.Sin(a0) * 12f);
                        Vector2 p1 = new Vector2(C + MathF.Cos(a1) * 12f, C + MathF.Sin(a1) * 12f);
                        return Vector2.Lerp(p0, p1, per);
                    }, _ => 1.6f, 1f, 30);
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k * MathHelper.PiOver2 + 0.3f;
                        Chain(t => new Vector2(C + MathF.Cos(a) * MathHelper.Lerp(20f, 26f, t), C + MathF.Sin(a) * MathHelper.Lerp(20f, 26f, t)),
                              t => MathHelper.Lerp(1.8f, 0.7f, t), 1f, 5);
                    }
                    break;

                case ShapeId.BoltZig:
                {
                    Vector2[] pts =
                    {
                        new Vector2(32f, 4f), new Vector2(20f, 18f), new Vector2(40f, 30f),
                        new Vector2(18f, 44f), new Vector2(34f, 60f),
                    };
                    for (int s = 0; s < pts.Length - 1; s++)
                    {
                        int seg = s;
                        Chain(t => Vector2.Lerp(pts[seg], pts[seg + 1], t), _ => 2.6f, 1f, 12);
                    }
                    break;
                }
            }

            Texture2D tex = new Texture2D(Main.graphics.GraphicsDevice, N, N);
            tex.SetData(px);
            return tex;
        }

        // ---------------- immediate draw (spritebatch contexts) ----------------

        internal static void Draw(ShapeId id, Vector2 worldPos, float rotation, float scale, Color color)
        {
            Draw(id, worldPos, rotation, new Vector2(scale), color);
        }

        internal static void Draw(ShapeId id, Vector2 worldPos, float rotation, Vector2 scale, Color color)
        {
            if (color.A == 0 || Main.dedServ)
                return;
            Texture2D tex = Get(id);
            if (tex == null)
                return;
            Main.spriteBatch.Draw(tex, worldPos - Main.screenPosition, null, Occlude(worldPos, color), rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        // ---------------- queued bursts (update contexts) ----------------

        private struct Burst
        {
            public int Id;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Rot;
            public float RotVel;
            public Vector2 Scale;
            public float Grow;
            public Color Color;
            public int Life;
            public int MaxLife;
        }

        private const int BurstCap = 160;
        private static readonly List<Burst> bursts = new List<Burst>(BurstCap);

        internal static bool HasBursts => bursts.Count > 0;

        internal static void ClearBursts()
        {
            bursts.Clear();
        }

        internal static void Spawn(ShapeId id, Vector2 pos, float rot, float scale, Color color, int life, Vector2 vel = default, float rotVel = 0f, float grow = 0f)
        {
            Spawn(id, pos, rot, new Vector2(scale), color, life, vel, rotVel, grow);
        }

        internal static void Spawn(ShapeId id, Vector2 pos, float rot, Vector2 scale, Color color, int life, Vector2 vel = default, float rotVel = 0f, float grow = 0f)
        {
            if (Main.dedServ || life <= 0 || color.A == 0)
                return;
            if (bursts.Count >= BurstCap)
                return;
            bursts.Add(new Burst
            {
                Id = (int)id,
                Pos = pos,
                Vel = vel,
                Rot = rot,
                RotVel = rotVel,
                Scale = scale,
                Grow = grow,
                Color = color,
                Life = life,
                MaxLife = life,
            });
        }

        internal static void SpawnFan(ShapeId id, Vector2 pos, Color color, int count, float speed, int life, float scale, float baseRot = 0f, float spread = MathHelper.TwoPi)
        {
            for (int i = 0; i < count; i++)
            {
                float a = baseRot + spread * (i + 0.5f) / count + Main.rand.NextFloat(-0.25f, 0.25f);
                Spawn(id, pos, a + Main.rand.NextFloat(MathHelper.TwoPi),
                    new Vector2(scale * Main.rand.NextFloat(0.75f, 1.2f)), color,
                    life + Main.rand.Next(-3, 4),
                    a.ToRotationVector2() * speed * Main.rand.NextFloat(0.7f, 1.25f),
                    Main.rand.NextFloat(-0.12f, 0.12f), 0.02f);
            }
        }

        internal static void UpdateBursts()
        {
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                Burst b = bursts[i];
                b.Life--;
                if (b.Life <= 0)
                {
                    bursts.RemoveAt(i);
                    continue;
                }
                b.Pos += b.Vel;
                b.Vel *= 0.94f;
                b.Rot += b.RotVel;
                b.Scale += new Vector2(b.Grow);
                bursts[i] = b;
            }
        }

        // World-space draw (the caller has already opened the batch with the
        // game view transform).
        internal static void DrawBursts(SpriteBatch sb)
        {
            for (int i = 0; i < bursts.Count; i++)
            {
                Burst b = bursts[i];
                Texture2D tex = Get((ShapeId)b.Id);
                if (tex == null)
                    continue;
                float t = b.Life / (float)b.MaxLife;
                sb.Draw(tex, b.Pos, null, Occlude(b.Pos, b.Color * t), b.Rot, tex.Size() * 0.5f, b.Scale, SpriteEffects.None, 0f);
            }
        }

        // F5: unlit pockets dim the shapes instead of letting them glow
        // through the dark (hot cores of families stay untouched - this only
        // applies to the shape bank layer).
        private static Color Occlude(Vector2 worldPos, Color color)
        {
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            if (cfg != null && !cfg.Visuals.LightOcclusion)
                return color;
            Color lc = Lighting.GetColor((int)(worldPos.X / 16f), (int)(worldPos.Y / 16f));
            float lum = (lc.R + lc.G + lc.B) / 765f;
            return color * (0.6f + 0.4f * lum);
        }
    }

    // Drives the queued bursts: ticks them on the world update, draws them
    // after the tile pass in world space.
    public class MiniBossShapeBankSystem : ModSystem
    {
        private static bool s_drawFailed;

        public override void PostUpdateEverything()
        {
            MiniBossShapeBank.UpdateBursts();
        }

        public override void OnWorldUnload()
        {
            MiniBossShapeBank.ClearBursts();
        }

        public override void PostDrawTiles()
        {
            if (Main.dedServ || Main.gameMenu || s_drawFailed || !MiniBossShapeBank.HasBursts)
                return;
            try
            {
                SpriteBatch sb = Main.spriteBatch;
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                MiniBossShapeBank.DrawBursts(sb);
                sb.End();
            }
            catch
            {
                // A visual-only layer must never crash the draw loop; if the
                // batch state is not what we expect, disable quietly once.
                s_drawFailed = true;
            }
        }
    }
}
