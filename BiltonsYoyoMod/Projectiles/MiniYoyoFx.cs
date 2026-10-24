using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Shared non-dust visual primitives for the mini-boss yoyos: soft additive-style
    // glows (from a generated radial-gradient texture) and hard-geometry rings/lines.
    // All helpers take WORLD coordinates and draw in the current sprite batch, so they
    // are safe to call from PreDraw/PostDraw. Visuals only, no gameplay effect.
    public static class MiniYoyoFx
    {
        private static Texture2D glowTexture;
        public static Texture2D GlowTexture
        {
            get
            {
                if (glowTexture == null || glowTexture.IsDisposed)
                    glowTexture = ModContent.Request<Texture2D>("BiltonsYoyoMod/Projectiles/MiniYoyoGlow").Value;
                return glowTexture;
            }
        }

        private static Effect yoyoGlowEffect;
        private static bool yoyoGlowTried;

        // Shader pipeline (pilot): compiled .xnb at Effects/YoyoGlow.xnb.
        // Falls back to a plain invoke on any failure (runtime or load) so the
        // mod never breaks if the SDL_GPU renderer rejects the .xnb (issue #5162).
        //
        // `mode` = pulse style (0 default / 1 slow fire / 2 white-hot crit /
        // 3 churn / 5 disco hue-cycle). The shipped .xnb only reads
        // uColor/uTime/uIntensity, so modes are realised by what we feed it -
        // pulse rate, intensity boost and tint cycling - no shader rebuild.
        // Config is read live on every call (was sticky: once the first frame
        // ran with shaders off the plain path stuck until reload).
        public static void DrawWithShader(int mode, Color tint, float intensity, Action draw)
        {
            var cfg = GetYoyoConfig();
            if (cfg != null && !cfg.Visuals.UseShaders)
            {
                try { draw?.Invoke(); } catch { }
                return;
            }
            float pulseSpeed = mode switch
            {
                1 => 0.7f,
                2 => 1.7f,
                3 => 1.25f,
                5 => 2.1f,
                _ => 1f
            };
            float boost = mode == 2 ? 1.25f : mode == 3 ? 1.1f : 1f;
            // Photosensitivity: no fast throbbing, no hue-cycling disco.
            if (cfg != null && cfg.Visuals.Photosensitivity)
            {
                pulseSpeed = 1f;
                boost = MathHelper.Min(boost, 1f);
                mode = 0;
            }
            if (mode == 5)
                tint = Main.hslToRgb((Main.GlobalTimeWrappedHourly * 0.6f) % 1f, 0.85f, 0.62f);
            if (!yoyoGlowTried)
            {
                yoyoGlowTried = true;
                try { yoyoGlowEffect = ModContent.Request<Effect>("BiltonsYoyoMod/Effects/YoyoGlow", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value; }
                catch { yoyoGlowEffect = null; }
            }
            if (yoyoGlowEffect == null)
            {
                try { draw?.Invoke(); } catch { }
                return;
            }
            var sb = Main.spriteBatch;
            bool hadBatch = false;
            try { sb.End(); hadBatch = true; } catch { }
            try
            {
                try { yoyoGlowEffect.Parameters["uColor"]?.SetValue(tint.ToVector4()); } catch { }
                try { yoyoGlowEffect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * pulseSpeed); } catch { }
                try { yoyoGlowEffect.Parameters["uIntensity"]?.SetValue(intensity * boost); } catch { }
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, yoyoGlowEffect,
                    Main.GameViewMatrix.TransformationMatrix);
                draw?.Invoke();
            }
            catch
            {
                // Effect swap failed mid-frame: fall back to a plain window + draw.
                try { sb.End(); } catch { }
                try
                {
                    sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                        DepthStencilState.None, Main.Rasterizer, null,
                        Main.GameViewMatrix.TransformationMatrix);
                    draw?.Invoke();
                }
                catch { }
            }
            finally
            {
                try { sb.End(); } catch { }
                if (hadBatch)
                {
                    try
                    {
                        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                            DepthStencilState.None, Main.Rasterizer, null,
                            Main.GameViewMatrix.TransformationMatrix);
                    }
                    catch { }
                }
            }
        }

        // Soft round glow billboard, diameter in pixels.
        public static void DrawGlow(Vector2 worldCenter, float diameter, Color color, float alpha = 0.55f)
        {
            // Flush the shared particle pool here: DrawGlow runs constantly from
            // active PreDraws, and the same-frame guard inside makes this free.
            YoyoParticles.Draw();
            Texture2D tex = GlowTexture;
            Vector2 pos = worldCenter - Main.screenPosition;
            Main.spriteBatch.Draw(tex, pos, null, color * alpha, 0f, tex.Size() * 0.5f,
                diameter / tex.Width, SpriteEffects.None, 0f);
        }

        // Star-flare sprite (own YoyoStar art; was vanilla SharpTears).
        // Two crossed flares: bright large + dim small. For swing tips, charge peaks.
        public static void DrawStarSparkle(Vector2 worldCenter, Color color, float scale, float rotation = 0f, float alpha = 1f)
        {
            Texture2D tex = ModContent.Request<Texture2D>("BiltonsYoyoMod/Projectiles/YoyoStar").Value;
            Vector2 pos = worldCenter - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            // Alpha is deliberately NOT forced to zero: `color * alpha` already
            // carries the whole multiplier, and a zero alpha channel multiplies
            // the sprite out entirely (under the alpha blend every one of these
            // four draws used to be invisible).
            Color big = color * alpha * 0.5f;
            Color small = color * alpha * 0.25f;
            Main.EntitySpriteDraw(tex, pos, null, big, MathHelper.PiOver2 + rotation, origin, new Vector2(scale * 0.5f, scale), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, big, rotation, origin, new Vector2(scale * 0.5f, scale), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, small, MathHelper.PiOver2 + rotation, origin, new Vector2(scale * 0.5f, scale) * 0.6f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, small, rotation, origin, new Vector2(scale * 0.5f, scale) * 0.6f, SpriteEffects.None, 0);
        }

        // ---- Arcane star variants (DarkMage ritual bolt hit crown) ------------
        // The first pass built this from 1px MagicPixel ticks, which stretched into
        // hard aliased "rough lines". Major mods avoid exactly that: Calamity's
        // HolyLight blooms, Thorium's flares and Luminance-style anamorphic stars
        // all bake flares as SOFT radial sprites stretched into lenses, so every
        // edge is a gradient and there is no pixel geometry to alias. Same idea
        // here: a ray is the shared radial MiniYoyoGlow stretched along an angle,
        // never a line. All three styles share the helper and only differ in ray
        // count/angle/colour, so later attack sites can borrow a style for free.
        // Shared soft-lens primitive: the radial MiniYoyoGlow stretched `length` x
        // `width` along `angle`. Used by the arcane star styles above and by the
        // slash arc + slash streaks below - one gradient-edged art primitive, so no
        // slash ever has to fall back to a flat MagicPixel quad.
        //
        // Thin draws are handed off to DrawSoftBar. MiniYoyoGlow is 64x64 with NO
        // mipmaps (tML uploads PNGs as a single level), so when one axis is squashed
        // to a couple of pixels the texture is minified 20-60x and the sampler reads
        // a handful of isolated texels: the gradient collapses into flat, abruptly
        // cut steps - the translucent square ends seen on the tips of thin streaks,
        // slash echoes and comet tails. A 1-D bar cannot lose that way, so anything
        // past roughly 3:1 hands over, cross-fading over 3:1 -> 5:1 so nothing pops
        // mid-animation.
        public static void DrawSoftRay(Vector2 worldCenter, float angle, float length, float width, Color color, float alpha)
        {
            float major = MathHelper.Max(length, width);
            float minor = MathHelper.Max(MathHelper.Min(length, width), 0.001f);
            float t = MathHelper.Clamp((major / minor - 3f) * 0.5f, 0f, 1f);
            if (t < 1f)
                DrawSoftRayRadial(worldCenter, angle, length, width, color, alpha * (1f - t));
            if (t > 0f)
                DrawSoftBar(worldCenter, angle, length, width, color, alpha * t);
        }

        // Soft-ended bar: constant thickness across, one smooth alpha ramp along its
        // length. This is the shape a stretched radial cannot be when it is thin -
        // DrawStripe keeps its thickness but ends in a hard square, and the radial
        // keeps its taper but loses it to minification. Use it for streaks, beams,
        // tails, echoes and whiskers. Chained segments overlap smoothly as long as
        // each one is over-run by about its own width, same as the radial chains.
        public static void DrawSoftBar(Vector2 worldCenter, float angle, float length, float width, Color color, float alpha)
        {
            if (length <= 0.01f || width <= 0.01f || alpha <= 0f)
                return;
            if (!EnsureShapeTextures())
            {
                DrawSoftRayRadial(worldCenter, angle, length, width, color, alpha);
                return;
            }
            Texture2D tex = shapeBar;
            Main.spriteBatch.Draw(tex, worldCenter - Main.screenPosition, null, color * alpha, angle,
                tex.Size() * 0.5f, new Vector2(length / tex.Width, width / tex.Height), SpriteEffects.None, 0f);
        }

        private static void DrawSoftRayRadial(Vector2 worldCenter, float angle, float length, float width, Color color, float alpha)
        {
            Texture2D glow = GlowTexture;
            Vector2 pos = worldCenter - Main.screenPosition;
            Main.spriteBatch.Draw(glow, pos, null, color * alpha, angle, glow.Size() * 0.5f,
                new Vector2(length / glow.Width, width / glow.Height), SpriteEffects.None, 0f);
        }

        // Flat stripe of exact length x thickness (a 1x1 pixel stretched). Used to
        // build beam bodies, which must keep their thickness constant along their
        // whole length - something a single stretched radial glow cannot do, since
        // MiniYoyoGlow's alpha is a linear cone that tapers over half the sprite.
        public static void DrawStripe(Texture2D pixel, Vector2 worldMid, float rotation, float length, float thickness, Color color, float alpha)
        {
            Main.spriteBatch.Draw(pixel, worldMid - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                color * alpha, rotation, new Vector2(0.5f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
        }

        // ---- Solid shape primitives ------------------------------------------
        // SpriteBatch can only stretch rectangles and radial gradients, so a
        // genuinely SOLID shape (a disc, a tooth, a shard, a hard-capped bar) has
        // to come from a texture. Rather than ship art for each, the four masks
        // below are generated once on first use. That keeps every fill on the
        // normal SpriteBatch path - same batch and blend state as the caller (so
        // an additive window stays additive), no device state, no vertex buffers,
        // and nothing the SDL_GPU renderer can reject (the same concern that
        // guards the .xnb shader path). If generation is unavailable every helper
        // degrades to the soft-glow look it replaces, so callers never branch.
        //
        // Convention note: DrawSoftRay / DrawStripe / these all take WORLD
        // coordinates. DrawLine (below) is the odd one out - it takes SCREEN
        // coordinates and is kept only for legacy callers; prefer DrawLineWorld.
        private static Texture2D shapeDisc;
        private static Texture2D shapeWedge;
        private static Texture2D shapeLens;
        private static Texture2D shapeTriL;
        private static Texture2D shapeTriR;
        private static Texture2D shapeBar;
        private static bool shapeTexturesFailed;
        private static uint shapeRetryAt;

        private static bool EnsureShapeTextures()
        {
            if (shapeDisc != null && !shapeDisc.IsDisposed
                && shapeWedge != null && !shapeWedge.IsDisposed
                && shapeLens != null && !shapeLens.IsDisposed
                && shapeTriL != null && !shapeTriL.IsDisposed
                && shapeTriR != null && !shapeTriR.IsDisposed
                && shapeBar != null && !shapeBar.IsDisposed)
                return true;
            // A failed build must not permanently downgrade every shape, but it also
            // must not be retried every frame. Back off one second at a time.
            if (shapeTexturesFailed && Main.GameUpdateCount < shapeRetryAt)
                return false;
            shapeTexturesFailed = false;
            try
            {
                GraphicsDevice device = Main.instance.GraphicsDevice;
                const int N = 128;
                float[] a = new float[N * N];

                // Solid disc: opaque inside, one-pixel antialiased rim.
                float r = N * 0.5f;
                for (int y = 0; y < N; y++)
                {
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) - r;
                        float dy = (y + 0.5f) - r;
                        a[y * N + x] = MathHelper.Clamp((1f - (float)Math.Sqrt(dx * dx + dy * dy) / r) * r + 0.5f, 0f, 1f);
                    }
                }
                shapeDisc = MakeMask(device, N, N, a);

                // Isoceles wedge: apex at the top centre, base on the bottom edge.
                for (int y = 0; y < N; y++)
                {
                    float half = (y + 0.5f) / N * 0.5f;
                    for (int x = 0; x < N; x++)
                        a[y * N + x] = MathHelper.Clamp((half - Math.Abs((x + 0.5f) / N - 0.5f)) * N + 0.5f, 0f, 1f);
                }
                shapeWedge = MakeMask(device, N, N, a);

                // Right triangle, right angle at the top-left corner (legs +x, +y).
                for (int y = 0; y < N; y++)
                {
                    float v = (y + 0.5f) / N;
                    for (int x = 0; x < N; x++)
                        a[y * N + x] = MathHelper.Clamp((1f - v - (x + 0.5f) / N) * N + 0.5f, 0f, 1f);
                }
                shapeTriL = MakeMask(device, N, N, a);

                // Its mirror, right angle at the top-right corner.
                for (int y = 0; y < N; y++)
                {
                    float v = (y + 0.5f) / N;
                    for (int x = 0; x < N; x++)
                        a[y * N + x] = MathHelper.Clamp((1f - v - (N - x - 0.5f) / N) * N + 0.5f, 0f, 1f);
                }
                shapeTriR = MakeMask(device, N, N, a);

                // Almond / lens: pointed at both canthi, parabolic lid lines. This is
                // the eye sclera, and any pointed oval (leaf, seed, blade eye). Built
                // as one mask so it is a single smooth draw - overlapping capsules was
                // what made the eye look blocky.
                for (int y = 0; y < N; y++)
                {
                    float v = (y + 0.5f) / N * 2f - 1f;
                    for (int x = 0; x < N; x++)
                    {
                        float u = (x + 0.5f) / N * 2f - 1f;
                        a[y * N + x] = MathHelper.Clamp(((1f - u * u) - Math.Abs(v)) * (N * 0.5f) + 0.5f, 0f, 1f);
                    }
                }
                shapeLens = MakeMask(device, N, N, a);

                // Soft bar: a constant-thickness stroke whose alpha is a full-length
                // ramp (triangle, the 1-D twin of the radial's own taper) with a
                // plateau cross-section and soft outer edges. A 1-D mask puts its
                // whole texture budget into the ramp that runs along the draw, which
                // is stretched at any sane size, so the tips always run to a point;
                // across the width it is near-flat, so a hairline draw keeps its full
                // brightness instead of losing the gradient to minification.
                const int BW = 256;
                const int BH = 32;
                for (int y = 0; y < BH; y++)
                {
                    float v = (y + 0.5f) / BH;
                    float cv = MathHelper.Clamp((0.5f - Math.Abs(v - 0.5f)) / 0.15f, 0f, 1f);
                    for (int x = 0; x < BW; x++)
                    {
                        float u = (x + 0.5f) / BW;
                        a[y * BW + x] = MathHelper.Clamp(1f - Math.Abs(u - 0.5f) * 2f, 0f, 1f) * cv;
                    }
                }
                shapeBar = MakeMask(device, BW, BH, a);
                return true;
            }
            catch
            {
                shapeTexturesFailed = true;
                shapeRetryAt = Main.GameUpdateCount + 60;
                return false;
            }
        }

        // Releases the cached shape masks. Called from Mod.Unload: static texture
        // references would otherwise survive a hot reload and pin the old assembly,
        // which tML reports as "mod class still using memory".
        public static void DisposeShapeTextures()
        {
            DisposeMask(ref shapeDisc);
            DisposeMask(ref shapeWedge);
            DisposeMask(ref shapeLens);
            DisposeMask(ref shapeTriL);
            DisposeMask(ref shapeTriR);
            DisposeMask(ref shapeBar);
            DisposeMask(ref softTrailTex);
            softTrailTried = false;
            shapeTexturesFailed = false;
            shapeRetryAt = 0;
        }

        private static void DisposeMask(ref Texture2D mask)
        {
            if (mask != null && !mask.IsDisposed)
                mask.Dispose();
            mask = null;
        }

        private static Texture2D MakeMask(GraphicsDevice device, int w, int h, float[] coverage)
        {
            var tex = new Texture2D(device, w, h);
            var data = new Color[w * h];
            for (int i = 0; i < data.Length; i++)
                data[i] = new Color(255, 255, 255, (byte)(MathHelper.Clamp(coverage[i], 0f, 1f) * 255f));
            tex.SetData(data);
            return tex;
        }

        // Solid filled disc (hard edge). Falls back to a soft lens.
        public static void DrawSolidDisc(Vector2 worldCenter, float diameter, Color color, float alpha)
        {
            if (diameter <= 0.01f || alpha <= 0f)
                return;
            if (!EnsureShapeTextures())
            {
                DrawSoftRay(worldCenter, 0f, diameter, diameter, color, alpha * 0.8f);
                return;
            }
            Texture2D tex = shapeDisc;
            Main.spriteBatch.Draw(tex, worldCenter - Main.screenPosition, null, color * alpha, 0f,
                tex.Size() * 0.5f, diameter / tex.Width, SpriteEffects.None, 0f);
        }

        public static void DrawSolidEllipse(Vector2 worldCenter, float width, float height, float rotation, Color color, float alpha)
        {
            if (width <= 0.01f || height <= 0.01f || alpha <= 0f)
                return;
            if (!EnsureShapeTextures())
            {
                DrawSoftRay(worldCenter, rotation, width, height, color, alpha * 0.8f);
                return;
            }
            Texture2D tex = shapeDisc;
            Main.spriteBatch.Draw(tex, worldCenter - Main.screenPosition, null, color * alpha, rotation,
                tex.Size() * 0.5f, new Vector2(width / tex.Width, height / tex.Height), SpriteEffects.None, 0f);
        }

        // Solid almond / lens: pointed at both ends, one smooth draw. Eyes, leaves,
        // seeds, spearheads, blade eyes - anything that is a pointed oval.
        public static void DrawSolidLens(Vector2 worldCenter, float width, float height, float rotation,
            Color color, float alpha)
        {
            if (width <= 0.01f || height <= 0.01f || alpha <= 0f)
                return;
            if (!EnsureShapeTextures())
            {
                DrawSoftRay(worldCenter, rotation, width, height, color, alpha * 0.8f);
                return;
            }
            Texture2D tex = shapeLens;
            Main.spriteBatch.Draw(tex, worldCenter - Main.screenPosition, null, color * alpha, rotation,
                tex.Size() * 0.5f, new Vector2(width / tex.Width, height / tex.Height), SpriteEffects.None, 0f);
        }

        // Solid isoceles triangle: the apex, and the centre of the base it drops
        // onto. Covers teeth, shards, quills, feathers, cone funnels, plumes.
        public static void DrawWedge(Vector2 apex, Vector2 baseCentre, float halfWidth, Color color, float alpha)
        {
            if (halfWidth <= 0.01f || alpha <= 0f)
                return;
            Vector2 d = baseCentre - apex;
            float len = d.Length();
            if (len < 0.01f)
                return;
            if (!EnsureShapeTextures())
            {
                DrawSoftRay((apex + baseCentre) * 0.5f, d.ToRotation(), len, halfWidth * 2f, color, alpha * 0.8f);
                return;
            }
            Texture2D tex = shapeWedge;
            Main.spriteBatch.Draw(tex, (apex + baseCentre) * 0.5f - Main.screenPosition, null, color * alpha,
                d.ToRotation() - MathHelper.PiOver2, tex.Size() * 0.5f,
                new Vector2(halfWidth * 2f / tex.Width, len / tex.Height), SpriteEffects.None, 0f);
        }

        // Solid right triangle: right angle at `corner`, legs out to `legA` and
        // `legB`. Which of the two mirrored masks to use is decided by the sign of
        // the cross product, so no SpriteEffects flip is needed and the pivot
        // stays exactly on `corner`.
        public static void DrawRightTri(Vector2 corner, Vector2 legA, Vector2 legB, Color color, float alpha)
        {
            if (alpha <= 0f)
                return;
            Vector2 va = legA - corner;
            Vector2 vb = legB - corner;
            float la = va.Length();
            float lb = vb.Length();
            if (la < 0.01f || lb < 0.01f)
                return;
            if (!EnsureShapeTextures())
            {
                DrawLineWorld(corner, legA, 2f, color, alpha * 0.7f);
                DrawLineWorld(corner, legB, 2f, color, alpha * 0.7f);
                DrawLineWorld(legA, legB, 2f, color, alpha * 0.7f);
                return;
            }
            bool plus = va.X * vb.Y - va.Y * vb.X >= 0f;      // legB is +90 deg from legA
            Texture2D tex = plus ? shapeTriL : shapeTriR;
            Vector2 origin = plus ? Vector2.Zero : new Vector2(tex.Width, 0f);
            float rot = plus ? va.ToRotation() : va.ToRotation() + MathHelper.Pi;
            Main.spriteBatch.Draw(tex, corner - Main.screenPosition, null, color * alpha, rot, origin,
                new Vector2(la / tex.Width, lb / tex.Height), SpriteEffects.None, 0f);
        }

        // Any triangle: split about the longest edge so both halves are right
        // triangles. (Obtuse inputs clamp the foot to the edge, which loses the
        // sliver outside it - fine for teeth/shard shapes, not for exact geometry.)
        public static void DrawSolidTriangle(Vector2 a, Vector2 b, Vector2 c, Color color, float alpha)
        {
            float ab = Vector2.DistanceSquared(a, b);
            float bc = Vector2.DistanceSquared(b, c);
            float ca = Vector2.DistanceSquared(c, a);
            Vector2 p, q, r;
            if (ab >= bc && ab >= ca) { p = a; q = b; r = c; }
            else if (bc >= ca) { p = b; q = c; r = a; }
            else { p = c; q = a; r = b; }
            Vector2 pq = q - p;
            float denom = Vector2.Dot(pq, pq);
            float t = denom < 0.0001f ? 0.5f : MathHelper.Clamp(Vector2.Dot(r - p, pq) / denom, 0f, 1f);
            Vector2 foot = p + pq * t;
            DrawRightTri(foot, p, r, color, alpha);
            DrawRightTri(foot, q, r, color, alpha);
        }

        public static void DrawSolidQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, float alpha)
        {
            DrawSolidTriangle(a, b, c, color, alpha);
            DrawSolidTriangle(a, c, d, color, alpha);
        }

        // Constant-thickness stroke with capped ends: the thing DrawStripe cannot
        // do (square ends) and DrawSoftRay cannot do (it tapers). softCaps swaps
        // the solid end discs for gradient glows.
        public static void DrawCapsule(Vector2 start, Vector2 end, float width, Color color, float alpha, bool softCaps = false)
        {
            if (alpha <= 0f || width <= 0f)
                return;
            Vector2 diff = end - start;
            if (diff.LengthSquared() < 0.01f)
            {
                if (softCaps)
                    DrawSoftRay(start, 0f, width, width, color, alpha);
                else
                    DrawSolidDisc(start, width, color, alpha);
                return;
            }
            DrawStripe(TextureAssets.MagicPixel.Value, (start + end) * 0.5f, diff.ToRotation(), diff.Length(), width, color, alpha);
            float cap = width;
            if (softCaps)
            {
                DrawSoftRay(start, 0f, cap, cap, color, alpha);
                DrawSoftRay(end, 0f, cap, cap, color, alpha);
            }
            else
            {
                DrawSolidDisc(start, cap, color, alpha);
                DrawSolidDisc(end, cap, color, alpha);
            }
        }

        // ---- Arcs, lines, paths ----------------------------------------------
        // World-space hard line. The one to use; DrawLine below is screen-space.
        public static void DrawLineWorld(Vector2 start, Vector2 end, float width, Color color, float alpha)
        {
            Vector2 diff = end - start;
            if (diff.LengthSquared() < 0.0001f || alpha <= 0f)
                return;
            DrawStripe(TextureAssets.MagicPixel.Value, (start + end) * 0.5f, diff.ToRotation(), diff.Length(), width, color, alpha);
        }

        // Hard arc band: a partial ring of chained constant-thickness segments.
        // widthTaper 0 = even width, 1 = hairline at `from` growing to full at
        // `to`; alphaFrom/alphaTo ramp opacity over the same axis (0.2 -> 1 is
        // the classic swing fade). This is the shared home for what were two
        // private copies (the Ogre club arc and the Pumpking blade arc) plus the
        // hand-rolled loops in the star styles.
        public static void DrawArcBand(Vector2 worldCenter, float radius, float from, float to, float width,
            Color color, float alpha, int segments = 16, float widthTaper = 0f, float alphaFrom = 1f, float alphaTo = 1f)
        {
            if (alpha <= 0f || width <= 0f || radius <= 0f || segments < 2 || Math.Abs(to - from) < 0.001f)
                return;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                Vector2 p0 = worldCenter + MathHelper.Lerp(from, to, t0).ToRotationVector2() * radius;
                Vector2 p1 = worldCenter + MathHelper.Lerp(from, to, t1).ToRotationVector2() * radius;
                float w = width * MathHelper.Lerp(1f - widthTaper, 1f, t1);
                DrawLineWorld(p0, p1, w, color, alpha * MathHelper.Lerp(alphaFrom, alphaTo, t1));
            }
        }

        // Gradient-edged arc: each segment is a soft lens laid along the tangent.
        // What the Crescent / Soundwave / Web bows actually want.
        public static void DrawSoftArc(Vector2 worldCenter, float radius, float from, float to,
            float width, Color color, float alpha, int segments = 12)
        {
            if (alpha <= 0f || width <= 0f || radius <= 0f || segments < 1)
                return;
            float step = (to - from) / segments;
            float segLen = Math.Abs(step) * radius;
            for (int i = 0; i < segments; i++)
            {
                float ang = from + step * (i + 0.5f);
                DrawSoftRay(worldCenter + ang.ToRotationVector2() * radius, ang + MathHelper.PiOver2,
                    segLen * 2.2f, width, color, alpha);
            }
        }

        private static Vector2 EllipseOffset(float angle, float rx, float ry, float rotation)
        {
            var v = new Vector2((float)Math.Cos(angle) * rx, (float)Math.Sin(angle) * ry);
            return v.RotatedBy(rotation);
        }

        // Point on an ellipse: useful for laying anything out around a rim.
        public static Vector2 PointOnEllipse(Vector2 worldCenter, float rx, float ry, float rotation, float angle)
        {
            return worldCenter + EllipseOffset(angle, rx, ry, rotation);
        }

        // HARD elliptical band - the missing sibling of DrawSoftEllipseRing. A
        // circle band cannot be squashed by scaling (SpriteBatch has no shear), so
        // the ring is chained from points placed on the ellipse itself. This is
        // what a plasma ring, an orbit, a portal rim or a tilted halo needs.
        public static void DrawEllipseBand(Vector2 worldCenter, float rx, float ry, float rotation,
            float from, float to, float width, Color color, float alpha, int segments = 28, float widthTaper = 0f)
        {
            if (alpha <= 0f || width <= 0f || (rx <= 0.01f && ry <= 0.01f) || segments < 2)
                return;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                Vector2 p0 = worldCenter + EllipseOffset(MathHelper.Lerp(from, to, t0), rx, ry, rotation);
                Vector2 p1 = worldCenter + EllipseOffset(MathHelper.Lerp(from, to, t1), rx, ry, rotation);
                DrawLineWorld(p0, p1, width * MathHelper.Lerp(1f - widthTaper, 1f, t1), color, alpha);
            }
        }

        // Two-colour stroke: the colour ramps along the stroke. Vanilla's own
        // Utils.DrawLine has this overload; it is how a beam reads as fading.
        public static void DrawLineGradientWorld(Vector2 start, Vector2 end, float width,
            Color startColor, Color endColor, float alpha, int segments = 6)
        {
            if (segments < 1 || alpha <= 0f)
                return;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                Vector2 p0 = Vector2.Lerp(start, end, t0);
                Vector2 p1 = Vector2.Lerp(start, end, t1);
                DrawLineWorld(p0, p1, width, Color.Lerp(startColor, endColor, (t0 + t1) * 0.5f), alpha);
            }
        }

        // ---- More fills, strokes and rings -------------------------------------
        // Arbitrary convex polygon: fan from the centroid, so winding order does
        // not matter. n triangles for n points.
        public static void DrawSolidPolygon(Vector2[] points, Color color, float alpha)
        {
            if (points == null || points.Length < 3 || alpha <= 0f)
                return;
            Vector2 centre = Vector2.Zero;
            for (int i = 0; i < points.Length; i++)
                centre += points[i];
            centre /= points.Length;
            for (int i = 0; i < points.Length; i++)
                DrawSolidTriangle(centre, points[i], points[(i + 1) % points.Length], color, alpha);
        }

        // Stroke whose width changes along its length. This used to be a hard
        // trapezoid (DrawSolidQuad per segment). Along a path - which is how
        // DrawPathBand and the worm's body chain it - a flat quad leaves miter
        // spikes on the outside of every bend and flat facets on the inside, so
        // the fix is the same one DrawPathBand got: a soft gradient bar with the
        // same taper, over-run at both ends so joints weld instead of butting.
        // Thick strokes keep a solid core inside that gradient, because a worm's
        // body must still read as matter rather than as a glow.
        public static void DrawTaperedStroke(Vector2 start, float startWidth, Vector2 end, float endWidth,
            Color color, float alpha, bool caps = false)
        {
            Vector2 diff = end - start;
            float len = diff.Length();
            if (len < 0.01f || alpha <= 0f)
                return;
            Vector2 mid = (start + end) * 0.5f;
            float rot = diff.ToRotation();
            float w = (startWidth + endWidth) * 0.5f;
            if (w > 7f)
            {
                DrawSoftBar(mid, rot, len + w * 0.9f, w * 1.25f, color, alpha * 0.8f);
                DrawStripe(TextureAssets.MagicPixel.Value, mid, rot, len + w * 0.5f, w * 0.72f, color, alpha);
            }
            else
            {
                DrawSoftBar(mid, rot, len + w, w, color, alpha);
            }
            if (caps)
            {
                if (startWidth > 0.2f)
                    DrawSolidDisc(start, startWidth, color, alpha);
                if (endWidth > 0.2f)
                    DrawSolidDisc(end, endWidth, color, alpha);
            }
        }

        // Dashed stroke; dash and gap are world units measured along the line.
        public static void DrawDashedLine(Vector2 start, Vector2 end, float width,
            float dashLength, float gapLength, Color color, float alpha)
        {
            Vector2 diff = end - start;
            float len = diff.Length();
            if (len < 0.01f || alpha <= 0f || dashLength <= 0.01f)
                return;
            Vector2 dir = diff / len;
            float stride = dashLength + MathHelper.Max(gapLength, 0.01f);
            for (float d = 0f; d < len; d += stride)
                DrawLineWorld(start + dir * d, start + dir * MathHelper.Min(d + dashLength, len), width, color, alpha);
        }

        // Dashed elliptical band: a broken seal, a circuit ring, a dashed orbit.
        // `duty` is the lit fraction of each dash slot.
        public static void DrawDashedEllipseBand(Vector2 worldCenter, float rx, float ry, float rotation,
            float width, Color color, float alpha, int dashes = 16, float duty = 0.55f, int segments = 2)
        {
            if (alpha <= 0f || dashes < 1 || width <= 0f)
                return;
            float step = MathHelper.TwoPi / dashes;
            float lit = step * MathHelper.Clamp(duty, 0.05f, 1f);
            for (int i = 0; i < dashes; i++)
            {
                float a0 = i * step;
                DrawEllipseBand(worldCenter, rx, ry, rotation, a0, a0 + lit, width, color, alpha, System.Math.Max(segments, 1));
            }
        }

        // Hard ring with a segment count chosen for its size. DrawRing already does
        // the chaining; at any normal radius the chord sagitta is sub-pixel, so this
        // wraps it with sane defaults plus an alpha argument. A ring MASK texture
        // would buy nothing here and would fix the band/radius ratio, which is worse.
        public static void DrawSolidRing(Vector2 worldCenter, float radius, float width, Color color,
            float alpha, int segments = -1)
        {
            if (radius <= 0.01f || width <= 0f || alpha <= 0f)
                return;
            if (segments <= 0)
                segments = (int)MathHelper.Clamp((float)Math.Ceiling(radius * 0.55f), 12f, 72f);
            DrawRing(worldCenter, radius, width, color * alpha, segments);
        }

        // Ring whose colour ramps around the circle (heat rings, charge rings,
        // polar gradients) and whose width can taper over the sweep.
        public static void DrawRingGradient(Vector2 worldCenter, float radius, float width,
            Color from, Color to, float alpha, int segments = 32, float widthTaper = 0f)
        {
            if (radius <= 0.01f || width <= 0f || alpha <= 0f || segments < 2)
                return;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                Vector2 p0 = worldCenter + FromPolar(radius, MathHelper.TwoPi * t0);
                Vector2 p1 = worldCenter + FromPolar(radius, MathHelper.TwoPi * t1);
                DrawLineWorld(p0, p1, width * MathHelper.Lerp(1f - widthTaper, 1f, t1),
                    Color.Lerp(from, to, (t0 + t1) * 0.5f), alpha);
            }
        }

        // Chromatic aberration: the same bar in cyan and magenta either side of the
        // core. The standard glitch/CRT tell, in one zero-allocation call.
        public static void DrawChromaticSplit(Vector2 worldCenter, float rotation, float length, float thickness,
            Color core, float alpha, float split = 2f)
        {
            if (alpha <= 0f)
                return;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            DrawStripe(pixel, worldCenter - new Vector2(split, 0f), rotation, length, thickness,
                Color.Lerp(core, new Color(80, 240, 255), 0.55f), alpha);
            DrawStripe(pixel, worldCenter + new Vector2(split, 0f), rotation, length, thickness,
                Color.Lerp(core, new Color(255, 70, 190), 0.55f), alpha);
            DrawStripe(pixel, worldCenter, rotation, length, thickness, core, alpha);
        }

        // ---- Easing ------------------------------------------------------------
        public enum FxEase
        {
            Linear,
            Smooth,
            OutQuad,
            OutCubic,
            InOutCubic,
            OutBack,
            OutElastic,
            OutBounce
        }

        // Shape a normalised 0..1 value. OutBack / OutElastic / OutBounce overshoot
        // or bounce on purpose, so the result is NOT clamped back into 0..1.
        public static float Ease(FxEase curve, float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            switch (curve)
            {
                case FxEase.Smooth:
                    return t * t * (3f - 2f * t);
                case FxEase.OutQuad:
                    return 1f - (1f - t) * (1f - t);
                case FxEase.OutCubic:
                {
                    float u = 1f - t;
                    return 1f - u * u * u;
                }
                case FxEase.InOutCubic:
                    return t < 0.5f
                        ? 4f * t * t * t
                        : 1f - 4f * (1f - t) * (1f - t) * (1f - t);
                case FxEase.OutBack:
                {
                    const float c1 = 1.70158f;
                    float u = t - 1f;
                    return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
                }
                case FxEase.OutElastic:
                {
                    if (t <= 0f || t >= 1f)
                        return t;
                    float c4 = MathHelper.TwoPi / 3f;
                    return (float)(System.Math.Pow(2d, -10d * t) * System.Math.Sin((t * 10f - 0.75f) * c4) + 1d);
                }
                case FxEase.OutBounce:
                {
                    const float n1 = 7.5625f;
                    const float d1 = 2.75f;
                    if (t < 1f / d1)
                        return n1 * t * t;
                    if (t < 2f / d1)
                    {
                        t -= 1.5f / d1;
                        return n1 * t * t + 0.75f;
                    }
                    if (t < 2.5f / d1)
                    {
                        t -= 2.25f / d1;
                        return n1 * t * t + 0.9375f;
                    }
                    t -= 2.625f / d1;
                    return n1 * t * t + 0.984375f;
                }
                default:
                    return t;
            }
        }

        // Elapsed-time wrapper: Duration is the full 0..1 run, Curve shapes it.
        // Value() is the eased 0..1 progress, Life() is the complementary fade.
        public struct FxClock
        {
            public float Duration;
            public FxEase Curve;

            public FxClock(float duration, FxEase curve = FxEase.Linear)
            {
                Duration = duration <= 0.0001f ? 0.0001f : duration;
                Curve = curve;
            }

            public float Normalized(float elapsed) => MathHelper.Clamp(elapsed / Duration, 0f, 1f);

            public float Value(float elapsed) => Ease(Curve, Normalized(elapsed));

            public float Life(float elapsed) => 1f - Value(elapsed);
        }

        // ---- Polar coordinates --------------------------------------------------
        public static Vector2 FromPolar(float radius, float angle) => angle.ToRotationVector2() * radius;

        public static Vector2 ToPolar(Vector2 worldOffset) => new Vector2(worldOffset.Length(), worldOffset.ToRotation());

        // ---- Quantisers ---------------------------------------------------------
        // Posterise 0..1 into `steps` levels: 0, 1/(steps-1), ... 1.
        public static float StepCurve(float t, int steps)
        {
            if (steps < 1)
                return MathHelper.Clamp(t, 0f, 1f);
            if (steps == 1)
                return t >= 0.5f ? 1f : 0f;
            float q = (float)System.Math.Floor(MathHelper.Clamp(t, 0f, 1f) * steps) / (steps - 1);
            return MathHelper.Clamp(q, 0f, 1f);
        }

        // Round a value onto a grid of stepSize.
        public static float Snap(float value, float stepSize)
        {
            if (stepSize <= 0.0001f)
                return value;
            return (float)System.Math.Round(value / stepSize) * stepSize;
        }

        // ---- Palette ramp -------------------------------------------------------
        // Sample an unordered gradient by t: the palette entry index interpolates, so
        // a 3-colour ramp behaves like low -> mid -> high.
        public static Color Sample(Color[] palette, float t)
        {
            if (palette == null || palette.Length == 0)
                return Color.White;
            if (palette.Length == 1)
                return palette[0];
            float s = MathHelper.Clamp(t, 0f, 1f) * (palette.Length - 1);
            int i = (int)s;
            if (i >= palette.Length - 1)
                return palette[palette.Length - 1];
            return Color.Lerp(palette[i], palette[i + 1], s - i);
        }

        // ---- Lightning / fracture paths ----------------------------------------
        // Midpoint displacement into a caller-supplied buffer, so draw code can run
        // every frame without allocating. Deterministic: the same seed and endpoints
        // give the same path, so a bolt sits still instead of crawling.
        // jitter = perpendicular offset as a fraction of segment length.
        // generations = g gives 2^g + 1 points (g <= 6 -> 65 points).
        private static readonly Vector2[] LightningBuffer = new Vector2[65];

        public static int BuildLightning(Vector2 start, Vector2 end, Vector2[] into,
            float jitter = 0.30f, int generations = 4, int seed = 0)
        {
            if (into == null || into.Length < 2)
                return 0;
            generations = (int)MathHelper.Clamp(generations, 0, 6);
            if ((1 << generations) + 1 > into.Length)
                generations = 0;
            int count = (1 << generations) + 1;
            into[0] = start;
            into[count - 1] = end;
            if (generations > 0)
                Displace(into, 0, count - 1, jitter, seed, generations);
            return count;
        }

        private static void Displace(Vector2[] pts, int i0, int i1, float jitter, int seed, int depth)
        {
            if (depth <= 0 || i1 - i0 < 2)
                return;
            int mid = (i0 + i1) >> 1;
            Vector2 a = pts[i0];
            Vector2 b = pts[i1];
            Vector2 d = b - a;
            float len = d.Length();
            float h = Hash(seed, mid, depth) * 2f - 1f;
            Vector2 n = len > 0.0001f ? new Vector2(-d.Y, d.X) / len : Vector2.Zero;
            pts[mid] = (a + b) * 0.5f + n * (h * jitter * len);
            Displace(pts, i0, mid, jitter, seed, depth - 1);
            Displace(pts, mid, i1, jitter, seed, depth - 1);
        }

        private static float Hash(int seed, int index, int depth)
        {
            uint h = (uint)(seed * 374761393 + index * 668265263 + depth * 2246822519);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }

        // Stroke a bolt. taper thins and dims it toward both ends, which is what makes
        // a lightning path read as a discharge instead of a polyline.
        public static void DrawLightning(Vector2 start, Vector2 end, float width, Color color, float alpha,
            float jitter = 0.30f, int generations = 4, int seed = 0, bool taper = true)
        {
            if (alpha <= 0f || width <= 0f)
                return;
            int count = BuildLightning(start, end, LightningBuffer, jitter, generations, seed);
            // Two soft passes over the SAME path (same seed, so they overlay
            // exactly): a wide dim halo for depth and a tight hot core.
            StrokeLightningPath(count, width * 2.4f, color, alpha * 0.32f, taper);
            StrokeLightningPath(count, width, color, alpha * 0.85f, taper);
        }

        /// <summary>
        /// Draw a lightning buffer as a chain of SOFT bars.
        ///
        /// This is the fix for bolts being drawn as rectangles. The old body
        /// stroked every segment with DrawLineWorld, which is one stretched 1x1
        /// MagicPixel - a hard-edged bar with square ends - and a BuildLightning
        /// segment is routinely 20-40px long, so every bolt was a chain of hard
        /// bars about two tiles long. Re-jittered every frame, those bars are the
        /// "random rectangles that pulse" (seen on the cursor arsenal's lightning
        /// and on the arcane-star styles). DrawSoftBar is the exact same length
        /// and thickness with a gradient bar mask and round tips, and each
        /// segment is over-run by half its width so a tapering chain still sums
        /// into one jointless strand instead of a row of dashes.
        /// </summary>
        private static void StrokeLightningPath(int count, float width, Color color, float alpha, bool taper)
        {
            for (int i = 1; i < count; i++)
            {
                float f = i / (float)count;
                float profile = taper ? (float)System.Math.Sin(f * System.Math.PI) : 1f;
                float w = width * (taper ? 0.45f + 0.55f * profile : 1f);
                if (w <= 0.05f)
                    continue;
                Vector2 a = LightningBuffer[i - 1];
                Vector2 b = LightningBuffer[i];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f)
                    continue;
                DrawSoftBar((a + b) * 0.5f, (b - a).ToRotation(), len + w, w, color,
                    alpha * (taper ? 0.55f + 0.45f * profile : 1f));
            }
        }

        // ---- Bezier paths -----------------------------------------------------
        // There is no Bez1 because it is just Vector2.Lerp. Bez2 (quadratic) came
        // first - the slash blade and the bramble tendrils use it. Bez3 (cubic)
        // and the two tangents are here so a curve can be drawn as an ORIENTED
        // ribbon (width along the normal) instead of a chain of round dots.
        public static Vector2 Bez2(Vector2 a, Vector2 b, Vector2 c, float f)
        {
            float u = 1f - f;
            return u * u * a + 2f * u * f * b + f * f * c;
        }

        public static Vector2 Bez3(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float f)
        {
            float u = 1f - f;
            return u * u * u * a + 3f * u * u * f * b + 3f * u * f * f * c + f * f * f * d;
        }

        public static Vector2 Bez2Tangent(Vector2 a, Vector2 b, Vector2 c, float f)
        {
            Vector2 t = 2f * (1f - f) * (b - a) + 2f * f * (c - b);
            return t.LengthSquared() < 0.0001f ? Vector2.UnitX : Vector2.Normalize(t);
        }

        public static Vector2 Bez3Tangent(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float f)
        {
            float u = 1f - f;
            Vector2 t = 3f * u * u * (b - a) + 6f * u * f * (c - b) + 3f * f * f * (d - c);
            return t.LengthSquared() < 0.0001f ? Vector2.UnitX : Vector2.Normalize(t);
        }

        // ---- Paths, spirals, waves and radial repeats -------------------------
        // The last shapes the styles kept hand-rolling: a strand through a point
        // list, a ribbon that changes width along a curve, a sine wave, a spiral,
        // a burst of spikes and an N-fold repeat around a centre. They are all
        // built from the primitives above, so they inherit the same cached masks,
        // the same blend passes and the same fallbacks. The sampling buffer is
        // static like LightningBuffer, so none of them allocate while drawing;
        // DrawSpiral and DrawWaveStroke share it and must not nest inside each
        // other (DrawPolyline reads the caller's array and is safe anywhere).
        private static readonly Vector2[] PathBuffer = new Vector2[129];

        // Continuous stroke through `points`. Each segment is over-run by half the
        // width at both ends, which is why a chain of tapered bars sums to a flat,
        // jointless strand - the trick every hand-rolled strand was re-deriving.
        // softEnds = false chains crisp square-capped lines instead, for hardware
        // traces where a taper would read as a glow.
        public static void DrawPolyline(Vector2[] points, int count, float width, Color color, float alpha,
            bool softEnds = true)
        {
            if (points == null || width <= 0f || alpha <= 0f)
                return;
            count = System.Math.Min(count, points.Length);
            if (count < 2)
                return;
            for (int i = 1; i < count; i++)
            {
                Vector2 a = points[i - 1];
                Vector2 b = points[i];
                float len = Vector2.Distance(a, b);
                if (len < 0.01f)
                    continue;
                if (softEnds)
                    DrawSoftBar((a + b) * 0.5f, (b - a).ToRotation(), len + width, width, color, alpha);
                else
                    DrawLineWorld(a, b, width, color, alpha);
            }
        }

        // Ribbon whose width changes along an arbitrary path - the general case of
        // DrawTaperedStroke (one straight segment) and the band helpers (constant
        // width, circular/elliptical only). Width is interpolated across the whole
        // path by index and shared exactly at each joint, so the segments meet
        // seamlessly; caps = true rounds only the two outer ends.
        //
        // Every segment is laid as a SOFT gradient bar, over-run by half its own
        // end width so neighbours share their joint instead of butting ends. It
        // used to chain hard trapezoids (DrawSolidQuad): along a curve that
        // leaves miter spikes on the outside of every bend and flat facets on
        // the inside, which is exactly the "blocky" read the sine-wave and
        // spiral weavers had. Soft bars also mean the band's silhouette falls
        // off instead of ending on a cut edge.
        public static void DrawPathBand(Vector2[] points, int count, float startWidth, float endWidth,
            Color color, float alpha, bool caps = false)
        {
            if (points == null || alpha <= 0f)
                return;
            count = System.Math.Min(count, points.Length);
            if (count < 2)
                return;
            if (caps) {
                if (startWidth > 0.2f)
                    DrawSoftRay(points[0], 0f, startWidth, startWidth, color, alpha * 0.9f);
            }
            for (int i = 1; i < count; i++)
            {
                float t0 = (i - 1) / (float)(count - 1);
                float t1 = i / (float)(count - 1);
                Vector2 a = points[i - 1];
                Vector2 b = points[i];
                Vector2 d = b - a;
                float len = d.Length();
                if (len < 0.01f)
                    continue;
                float w0 = MathHelper.Lerp(startWidth, endWidth, t0);
                float w1 = MathHelper.Lerp(startWidth, endWidth, t1);
                // Over-run each segment by half its end width: the joint is
                // shared, never cut, so no miter can poke out of a bend.
                DrawSoftBar((a + b) * 0.5f, d.ToRotation(), len + w1, (w0 + w1) * 0.5f, color, alpha);
            }
            if (caps && endWidth > 0.2f)
                DrawSoftRay(points[count - 1], 0f, endWidth, endWidth, color, alpha * 0.9f);
        }

        // Sine ribbon along a straight axis: the displacement is perpendicular to
        // start->end, so it reads as a wave instead of a jagged bolt. DrawLightning
        // is the random-displacement sibling; this one is smooth and fully
        // determined by frequency and phase, so phase is what animates it.
        // frequency is cycles over the whole span, phase is in cycles too.
        // points = 0 picks a density from the span length.
        public static void DrawWaveStroke(Vector2 start, Vector2 end, float amplitude, float frequency,
            float phase, float width, Color color, float alpha, int points = 0)
        {
            Vector2 axis = end - start;
            float len = axis.Length();
            if (len < 0.01f || width <= 0f || alpha <= 0f)
                return;
            Vector2 dir = axis / len;
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            int n = points > 0 ? points : (int)MathHelper.Clamp(len * 0.25f, 6f, PathBuffer.Length - 1);
            n = System.Math.Min(System.Math.Max(n, 2), PathBuffer.Length - 1);
            for (int i = 0; i < n; i++)
            {
                float f = i / (float)(n - 1);
                float offset = (float)System.Math.Sin((f * frequency + phase) * MathHelper.TwoPi) * amplitude;
                PathBuffer[i] = start + axis * f + perp * offset;
            }
            DrawPolyline(PathBuffer, n, width, color, alpha);
        }

        // Archimedean spiral: the radius lerps with the angle, so `turns` full loops
        // unwind from startRadius to endRadius. Stroked as one soft strand, which is
        // the look Orbit / Vortex / Bramble tendrils were faking with per-arc draws.
        public static void DrawSpiral(Vector2 centre, float startRadius, float endRadius, float startAngle,
            float turns, float width, Color color, float alpha, int points = 0)
        {
            if (width <= 0f || alpha <= 0f || turns <= 0f)
                return;
            int n = points > 0 ? points : (int)MathHelper.Clamp(turns * 20f, 8f, PathBuffer.Length - 1);
            n = System.Math.Min(System.Math.Max(n, 2), PathBuffer.Length - 1);
            float sweep = turns * MathHelper.TwoPi;
            for (int i = 0; i < n; i++)
            {
                float f = i / (float)(n - 1);
                PathBuffer[i] = centre
                    + (startAngle + sweep * f).ToRotationVector2() * MathHelper.Lerp(startRadius, endRadius, f);
            }
            DrawPolyline(PathBuffer, n, width, color, alpha);
        }

        // N-fold repeat around a centre: the callback receives the index and the
        // angle, so a rosette, a mandala rim, a bead ring, a tooth circle or a petal
        // fan are all the same loop. This is where the per-style twelve ticks / six
        // beads / eighteen teeth loops belong.
        public static void DrawRadialRepeat(Vector2 centre, int count, float startAngle, Action<int, float> drawAt)
        {
            if (drawAt == null || count < 1)
                return;
            for (int i = 0; i < count; i++)
                drawAt(i, startAngle + MathHelper.TwoPi * i / count);
        }

        // Burst of tapered spikes. The jitter is a deterministic hash of the spike
        // index (the Hash used by the bolts), so the burst sits still frame to frame;
        // step `seed`, `rotation` or `phase` with time to animate it. Spikes point
        // outwards: apex at the tip, base on the inner circle.
        public static void DrawStarburst(Vector2 centre, float innerRadius, float outerRadius, int spikes,
            float spikeWidth, Color color, float alpha, float rotation = 0f, float jitter = 0f, int seed = 0)
        {
            if (spikes < 1 || spikeWidth <= 0f || alpha <= 0f || outerRadius <= innerRadius)
                return;
            float slot = MathHelper.TwoPi / spikes;
            for (int i = 0; i < spikes; i++)
            {
                float k = Hash(seed, i, 0);
                float ang = rotation + slot * i + (k - 0.5f) * jitter * slot;
                Vector2 dir = ang.ToRotationVector2();
                float reach = outerRadius * MathHelper.Lerp(1f, 1f - jitter * 0.5f, k);
                DrawWedge(centre + dir * reach, centre + dir * innerRadius,
                    spikeWidth * (0.6f + 0.8f * k), color, alpha);
            }
        }

        // ---- Blend-pass selector ---------------------------------------------
        // Styles are currently hard-wired to the additive window, which means a
        // "dark" element (a void slit, a maw throat, a black glitch block) can
        // only ADD a dim colour - it can never actually darken. SubtractiveDraw
        // already existed for smoke/ink/ooze; this exposes pass choice to a style.
        public enum FxPass
        {
            Alpha,
            Additive,
            Subtractive
        }

        public static void WithPass(FxPass pass, Action draw)
        {
            switch (pass)
            {
                case FxPass.Additive:
                    AdditiveDraw(draw);
                    break;
                case FxPass.Subtractive:
                    SubtractiveDraw(draw);
                    break;
                default:
                    try { draw?.Invoke(); } catch { }
                    break;
            }
        }

        // Variant 1: cool violet/white "lunar" 8-point sparkle - the ORIGINAL
        // look. Four soft lenses make eight points; the points telescope in and
        // out of phase (that is the pulse) and nothing rotates; a hot white
        // cross and a round core sit in the middle. The top-tier pass over-built
        // this into an indigo multi-layer orb, so it is restored here. The only
        // additions now are the stamp particles (SpawnArcaneStarBurst) and a
        // hair of extra point length.
        private static void ArcaneStarLunar(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color violet = new Color(186, 132, 255);
            Color core = new Color(244, 236, 255);
            DrawSoftRay(c, 0f, 38f * scale, 38f * scale, violet, 0.22f * alpha);            // halo bloom
            for (int i = 0; i < 4; i++)                                                      // 4 lenses -> 8 points
            {
                bool major = i % 2 == 0;
                // Points breathe out of phase, so the star twinkles instead of spinning.
                float breathe = 0.78f + 0.22f * (float)System.Math.Sin(progress * 18f + i * 1.7f);
                float len = (major ? 58f : 35f) * (major ? breathe : 1f);
                DrawSoftRay(c, settle + MathHelper.PiOver4 * i, len * scale, 5f * scale,
                    violet, (major ? 0.55f : 0.36f) * alpha);
            }
            DrawSoftRay(c, settle, 27f * scale, 3.4f * scale, core, 0.70f * alpha);          // hot core cross
            DrawSoftRay(c, settle + MathHelper.PiOver2, 27f * scale, 3.4f * scale, core, 0.70f * alpha);
            DrawSoftRay(c, 0f, 13f * scale, 13f * scale, core, 0.90f * alpha);               // core bead
        }

        // Variant 2: warm "solar" anamorphic cross flare - the ORIGINAL look: one
        // long gold slit plus a shorter perpendicular one over a warm halo, six
        // faint rays between them and a white-hot core. The floating tip orbs
        // stay gone (user: "why does solar have unrelated balls"). The ember
        // ghosts / 8-spoke fan / snap ring from the top-tier pass are removed.
        private static void ArcaneStarSolar(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color gold = new Color(255, 198, 96);
            Color ember = new Color(255, 132, 54);
            Color core = new Color(255, 248, 220);
            float flicker = 0.86f + 0.14f * (float)System.Math.Sin(progress * 22f);
            DrawSoftRay(c, 0f, 44f * scale, 44f * scale, ember, 0.20f * alpha);              // warm halo
            DrawSoftRay(c, 0f, 28f * scale, 28f * scale, gold, 0.18f * alpha);
            DrawSoftRay(c, settle, 80f * flicker * scale, 3f * scale, gold, 0.55f * alpha);  // long slit
            DrawSoftRay(c, settle + MathHelper.PiOver2, 48f * flicker * scale, 2.8f * scale, gold, 0.45f * alpha);
            for (int i = 0; i < 6; i++)                                                      // six faint rays
                DrawSoftRay(c, settle + MathHelper.TwoPi * i / 6f + 0.3f, 30f * scale, 3.2f * scale, ember, 0.18f * alpha);
            DrawSoftRay(c, 0f, 15f * scale, 15f * scale, core, 0.90f * alpha);               // white-hot core
            DrawSoftRay(c, 0f, 8f * scale, 8f * scale, Color.White, 0.85f * alpha);
        }

        // Variant 3: magenta "rift" - the ORIGINAL look: two needles that slice
        // OPEN over the life (length grows with progress) over a plum halo, with
        // four short spokes between. The abyss under-layer, magenta bleed edges,
        // eight-spoke fan and contracting seal ring from the top-tier pass are
        // removed; the needles simply reach a hair further now.
        private static void ArcaneStarVoid(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color magenta = new Color(214, 74, 240);
            Color plum = new Color(96, 26, 128);
            Color needle = new Color(255, 236, 255);
            float open = MathHelper.Clamp(progress / 0.55f, 0f, 1f);
            open = 1f - (1f - open) * (1f - open);
            float halo = (18f + 32f * open) * scale;
            DrawSoftRay(c, 0f, halo, halo, plum, 0.34f * alpha * open);                      // plum halo
            float needleLen = (16f + 72f * open) * scale;
            DrawSoftRay(c, settle, needleLen, 2.4f * scale, needle, 0.62f * alpha);
            DrawSoftRay(c, settle + MathHelper.PiOver2, needleLen * 0.92f, 2.2f * scale, needle, 0.56f * alpha);
            for (int i = 0; i < 4; i++)                                                      // four short spokes
                DrawSoftRay(c, settle + MathHelper.PiOver4 + MathHelper.PiOver2 * i,
                    (10f + 24f * open) * scale, 5.5f * scale, magenta, 0.34f * alpha);
            DrawSoftRay(c, 0f, 15f * scale, 15f * scale, needle, 0.80f * alpha);
        }

        // Variant 4: "nova" detonation - the ORIGINAL look: a multi-lobe bloom
        // (four concentric soft falls), an expanding thin shockwave ring and a
        // 4-point spike cross. The second echo ring, the diagonal needle pair and
        // the flung ember debris from the top-tier pass are removed.
        private static void ArcaneStarNova(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color hot = new Color(255, 236, 190);
            Color amber = new Color(255, 168, 72);
            Color deep = new Color(220, 96, 30);
            float bloom = 0.85f + 0.15f * (float)System.Math.Sin(progress * 14f);
            DrawSoftRay(c, 0f, 62f * scale, 62f * scale, deep, 0.12f * alpha);
            DrawSoftRay(c, 0f, 44f * bloom * scale, 44f * bloom * scale, amber, 0.22f * alpha);
            DrawSoftRay(c, 0f, 28f * scale, 28f * scale, amber, 0.34f * alpha);
            DrawSoftRay(c, 0f, 15f * scale, 15f * scale, hot, 0.55f * alpha);
            float ring = MathHelper.Clamp(progress / 0.75f, 0f, 1f);                          // expanding shock ring
            DrawRing(c, (12f + 42f * ring) * scale, 2.4f * scale, amber * (alpha * (1f - ring) * 0.8f), 30);
            for (int i = 0; i < 2; i++)                                                      // 4-point spike cross
                DrawSoftRay(c, settle + MathHelper.PiOver2 * i, 56f * bloom * scale, 4.5f * scale, hot, 0.42f * alpha);
            DrawSoftRay(c, 0f, 11f * scale, 11f * scale, Color.White, 0.90f * alpha);
        }

        // Variant 5: "prism" chromatic flare - the ORIGINAL look: magenta/cyan
        // anamorphic axes, a red/blue split rim offset from centre (cheap
        // chromatic aberration) and a diamond core of two crossed 45-degree
        // lenses. No ghost orbs anywhere. The lime third axis, three-tone halo,
        // cyan rim echo and snapping ring from the top-tier pass are removed.
        private static void ArcaneStarPrism(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color magenta = new Color(255, 96, 214);
            Color cyan = new Color(120, 236, 255);
            Color red = new Color(255, 96, 96);
            Color blue = new Color(96, 132, 255);
            float breathe = 0.86f + 0.14f * (float)System.Math.Sin(progress * 20f);
            DrawSoftRay(c, 0f, 30f * scale, 30f * scale, magenta, 0.16f * alpha);
            DrawSoftRay(c, settle, 76f * breathe * scale, 2.8f * scale, magenta, 0.46f * alpha);
            DrawSoftRay(c, settle + MathHelper.PiOver2, 80f * breathe * scale, 2.8f * scale, cyan, 0.46f * alpha);
            float off = 8f * scale;                                                          // chromatic split rim
            Vector2 axis = settle.ToRotationVector2();
            DrawSoftRay(c + axis * off, settle, 50f * scale, 5f * scale, red, 0.24f * alpha);
            DrawSoftRay(c - axis * off, settle + MathHelper.PiOver2, 46f * scale, 5f * scale, blue, 0.24f * alpha);
            DrawSoftRay(c, settle + MathHelper.PiOver4, 22f * scale, 6.5f * scale, Color.White, 0.60f * alpha);
            DrawSoftRay(c, settle - MathHelper.PiOver4, 22f * scale, 6.5f * scale, Color.White, 0.60f * alpha);
            DrawSoftRay(c, 0f, 12f * scale, 12f * scale, Color.White, 0.90f * alpha);
        }

        // Soft elliptical ring built from tangential soft lenses (used by the
        // slam / vortex / halo signatures). A squashed glow alone reads as a
        // filled disc, and DrawRing is always circular - this gives a perspective
        // ring with gradient edges and no pixel geometry to alias.
        private static void DrawSoftEllipseRing(Vector2 worldCenter, float radius, float flatten, float thickness, Color color, float alpha, int segments = 24)
        {
            if (radius < 1f || alpha <= 0.004f || segments < 3)
                return;
            float step = MathHelper.TwoPi / segments;
            float arcLen = radius * step * 1.15f;
            for (int i = 0; i < segments; i++)
            {
                float ang = step * i;
                float ca = (float)System.Math.Cos(ang);
                float sa = (float)System.Math.Sin(ang);
                Vector2 p = worldCenter + new Vector2(ca * radius, sa * radius * flatten);
                float rot = (float)System.Math.Atan2(sa * flatten, -ca);
                DrawSoftRay(p, rot, arcLen, thickness, color, alpha);
            }
        }

        // ---- Wildcard impact signatures (Variant6-15) --------------------------
        // Ten extra hit-crown styles that are deliberately NOT stars. Each one is
        // a literal, immediately readable archetype (a real crescent cut, a real
        // ground slam, a real spear thrust...) and each carries its OWN colour
        // palette, because these are placeholders for future attack sites rather
        // than a family that has to match the DarkMage violet. They still share
        // the soft-lens / soft-ring / stripe / line primitives, so they inherit
        // the flare language and stay edge-safe. `progress` runs 0 (stamp) -> 1
        // (expiry); `settle` is the per-stamp orientation.

        // Variant 6: "crescent" - a MOON cut. (This used to be labelled "slash",
        // but a symmetric arc is a crescent, not a slash; the real directional
        // slash is Variant16.) Refined: a fatter blade, horns that open outward,
        // a bright lip tracking the concave side and a soft interior fill so it
        // no longer reads as a thin ring segment. Own palette: silver-blue.
        private static void ArcaneStarCrescent(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color body = new Color(200, 226, 255);
            Color edge = new Color(246, 252, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float open = MathHelper.Clamp(t / 0.30f, 0f, 1f);
            open = 1f - (1f - open) * (1f - open);
            float life = 1f - MathHelper.Clamp((t - 0.55f) / 0.45f, 0f, 1f);
            if (life <= 0.01f)
                return;
            float sweep = MathHelper.Lerp(1.2f, 2.3f, open);                                 // horns open outward
            float radius = 42f * scale;
            const int segs = 16;
            float arcLen = sweep * radius / segs;
            for (int i = 0; i < segs; i++)
            {
                float f = (i + 0.5f) / segs;
                float a = settle - MathHelper.PiOver2 + (f - 0.5f) * sweep;
                float taper = (float)System.Math.Sin(f * System.Math.PI);                    // thin horns, fat middle
                float thick = (3f + 11f * taper) * scale;
                Vector2 p = c + a.ToRotationVector2() * radius;
                Vector2 dirOut = a.ToRotationVector2();
                DrawSoftRay(p, a + MathHelper.PiOver2, arcLen * 2.4f, thick, body, 0.30f * alpha * life);
                // Bright lip hugging the concave side - this is what reads as a moon.
                DrawSoftRay(p - dirOut * (thick * 0.42f), a + MathHelper.PiOver2, arcLen * 1.7f, 2.2f * scale, edge, 0.75f * alpha * life * taper);
            }
            DrawSoftRay(c + settle.ToRotationVector2() * (radius * 0.30f), 0f, radius * 1.05f, radius * 1.05f, body, 0.14f * alpha * life);
            for (int i = 0; i <= 1; i++)                                                     // horn points
            {
                float a = settle - MathHelper.PiOver2 + (i - 0.5f) * sweep;
                DrawSoftRay(c + a.ToRotationVector2() * radius, a + MathHelper.PiOver2, 20f * scale, 2.2f * scale, edge, 0.60f * alpha * life);
            }
            DrawSoftRay(c, 0f, 12f * scale, 12f * scale, edge, 0.45f * alpha * life);
        }

        // Variant 7: "slam" - the ground impact, refined. A flat dust flash, two
        // perspective shockwave rings racing outward, a bright rim, eight crack
        // spokes lying on the floor plane and a lifting dust plume. Own palette:
        // ochre / dust, so it can never be confused with the arcane stars.
        private static void ArcaneStarSlam(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color dust = new Color(214, 176, 120);
            Color hot = new Color(255, 244, 220);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float ring = MathHelper.Clamp(t / 0.60f, 0f, 1f);
            float ease = 1f - (1f - ring) * (1f - ring);
            float r = MathHelper.Lerp(10f, 64f, ease) * scale;
            float fade = 1f - ring;
            DrawSoftRay(c, 0f, r * 2.6f, r * 0.55f, dust, 0.20f * alpha * fade);             // flat ground flash
            DrawSoftEllipseRing(c, r, 0.42f, 6f * scale, dust, 0.55f * alpha * fade, 26);
            DrawSoftEllipseRing(c, r * 1.08f, 0.42f, 2.4f * scale, hot, 0.70f * alpha * fade, 30);
            DrawSoftEllipseRing(c, r * 0.55f, 0.42f, 2f * scale, hot, 0.35f * alpha * fade, 22);
            for (int i = 0; i < 8; i++)                                                      // floor crack spokes
            {
                float a = settle + MathHelper.TwoPi * i / 8f + MathHelper.PiOver4 * 0.5f;
                Vector2 dir = new Vector2((float)System.Math.Cos(a), (float)System.Math.Sin(a) * 0.42f).SafeNormalize(Vector2.UnitX);
                DrawSoftRay(c + dir * (r * 0.85f), dir.ToRotation(), r * 0.7f, 2.6f * scale, hot, 0.45f * alpha * fade);
            }
            for (int i = 0; i < 4; i++)                                                      // lifting dust plume
            {
                float drop = MathHelper.Clamp((t - i * 0.05f) / 0.30f, 0f, 1f);
                if (drop <= 0f)
                    continue;
                float y = c.Y - (30f - 26f * drop) * (1f + i * 0.22f) * scale;
                DrawSoftRay(new Vector2(c.X + (i - 1.5f) * 5f * scale, y), -MathHelper.PiOver2,
                    24f * scale, (7f - i) * scale, dust, 0.28f * alpha * (1f - drop));
            }
            DrawSoftRay(c + new Vector2(0f, 3f * scale), -MathHelper.PiOver2, 34f * scale, 11f * scale, hot, 0.22f * alpha * fade);
        }

        // Variant 8: "lance" - a THROWN SPEAR, not a beam. The whole weapon (shaft,
        // leaf-shaped head, cross-guard, tassel) travels through the hit along the
        // stamp orientation, leaving a motion trail, so it reads as an object
        // rather than light - which is exactly what separates it from the Ray.
        // Own palette: cold steel-white.
        private static void ArcaneStarLance(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color steel = new Color(176, 208, 255);
            Color wood = new Color(122, 132, 166);
            Color hot = new Color(248, 252, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float fly = MathHelper.Clamp(t / 0.60f, 0f, 1f);
            float life = 1f - MathHelper.Clamp((t - 0.45f) / 0.55f, 0f, 1f);
            if (life <= 0.01f)
                return;
            Vector2 dir = settle.ToRotationVector2();
            Vector2 side = new Vector2(-dir.Y, dir.X);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float reach = 74f * scale;
            float travel = MathHelper.Lerp(-0.75f, 0.95f, fly) * reach;                      // passes through
            Vector2 centre = c + dir * travel;
            Vector2 tip = c + dir * (travel + reach * 0.5f);
            Vector2 butt = c + dir * (travel - reach * 0.5f);
            DrawSoftRay(butt - dir * (22f * scale), settle, 60f * scale, 7f * scale, steel, 0.18f * alpha * life); // trail
            DrawStripe(pixel, centre, settle, reach * 0.86f, 5f * scale, wood, 0.60f * alpha * life);              // shaft
            DrawStripe(pixel, centre, settle, reach * 0.86f, 2f * scale, hot, 0.70f * alpha * life);
            Vector2 shoulder = tip - dir * (22f * scale);
            for (int i = -1; i <= 1; i += 2)                                                  // leaf head
            {
                Vector2 wing = shoulder + side * (i * 10f * scale);
                Vector2 seg = tip - wing;
                DrawSoftRay((wing + tip) * 0.5f, seg.ToRotation(), seg.Length() * 1.15f, 5.5f * scale, steel, 0.50f * alpha * life);
                DrawSoftRay((wing + tip) * 0.5f, seg.ToRotation(), seg.Length() * 0.9f, 2f * scale, hot, 0.80f * alpha * life);
            }
            DrawSoftRay(tip, 0f, 14f * scale, 14f * scale, hot, 0.70f * alpha * life);
            Vector2 guard = centre - dir * (reach * 0.30f);                                   // cross-guard + tassel
            DrawStripe(pixel, guard, settle + MathHelper.PiOver2, 20f * scale, 3.2f * scale, steel, 0.60f * alpha * life);
            for (int i = -1; i <= 1; i++)
                DrawSoftRay(guard - dir * (6f * scale) + side * (i * 4f * scale), settle + MathHelper.PiOver2 + i * 0.35f,
                    16f * scale, 3f * scale, steel, 0.35f * alpha * life);
        }

        // Variant 9: "vortex" - an unmistakable whirlpool (the old one read as
        // just another violet burst). Three spiral arms wind into a spinning eye
        // as the funnel tightens, framed by an accretion ring around the core.
        // Own palette: deep teal / cyan, which sets it apart from the family.
        private static void ArcaneStarVortex(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color deep = new Color(16, 120, 150);
            Color swirl = new Color(96, 226, 235);
            Color core = new Color(232, 255, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float bite = 1f - (1f - t) * (1f - t);
            for (int arm = 0; arm < 3; arm++)                                                // three spiral arms
            {
                for (int i = 0; i < 7; i++)
                {
                    float f = i / 7f;
                    float rad = MathHelper.Lerp(60f, 10f, (f + bite) % 1f) * scale;
                    float a = settle + arm * MathHelper.TwoPi / 3f + t * 4.2f + f * 1.7f;
                    Vector2 p = c + a.ToRotationVector2() * rad;
                    float w = MathHelper.Lerp(6f, 2.5f, f);
                    DrawSoftRay(p, a + MathHelper.PiOver2, 18f * scale, w * scale, swirl, 0.40f * alpha * (1f - f * 0.5f));
                }
            }
            DrawSoftEllipseRing(c, MathHelper.Lerp(60f, 11f, bite) * scale, 1f, 4.5f * scale, deep, 0.30f * alpha * (1f - t * 0.45f), 26);
            DrawSoftEllipseRing(c, (10f + 4f * bite) * scale, 1f, 3f * scale, core, 0.55f * alpha, 20); // accretion ring
            DrawSoftRay(c, 0f, (10f + 14f * bite) * scale, (10f + 14f * bite) * scale, deep, 0.25f * alpha);
            DrawSoftRay(c, 0f, (6f + 8f * bite) * scale, (6f + 8f * bite) * scale, core, 0.85f * alpha);
        }

        // Variant 10: "shatter" - hard crystal, not soft blobs. Nine angular
        // shards fan out on a deterministic golden-angle spread, each one a soft
        // body PLUS two hard facet lines meeting at a point, plus eight hard
        // containment ticks flung outward. Own palette: ice blue / mint.
        private static void ArcaneStarShatter(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color crystal = new Color(150, 220, 255);
            Color edge = new Color(226, 250, 255);
            Color mint = new Color(140, 255, 220);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float fly = 1f - (1f - t) * (1f - t);
            float life = 1f - t;
            if (life <= 0.01f)
                return;
            float rr = MathHelper.Lerp(14f, 50f, fly) * scale;                               // broken containment
            for (int i = 0; i < 8; i++)
            {
                float a = settle + MathHelper.TwoPi * i / 8f;
                DrawSoftRay(c + a.ToRotationVector2() * rr, a + MathHelper.PiOver2, 11f * scale, 2.2f * scale, edge, 0.55f * alpha * life);
            }
            for (int i = 0; i < 9; i++)
            {
                float a = settle + i * 2.3999632f;
                float dist = (16f + 34f * fly * (0.55f + 0.45f * (i % 3) / 2f)) * scale;
                Vector2 dirv = a.ToRotationVector2();
                Vector2 p = c + dirv * dist;
                float len = (12f + 9f * (i % 2)) * scale * (1f - t * 0.35f);
                float wid = (3.4f + (i % 3) * 0.6f) * scale;
                Vector2 side = new Vector2(-dirv.Y, dirv.X);
                DrawSoftRay(p, a, len, wid * 1.5f, i % 3 == 0 ? mint : crystal, 0.30f * alpha * life);
                Vector2 tipS = (p + dirv * (len * 0.5f)) - Main.screenPosition;              // hard facet edges
                Vector2 leftS = (p - dirv * (len * 0.5f) + side * wid) - Main.screenPosition;
                Vector2 rightS = (p - dirv * (len * 0.5f) - side * wid) - Main.screenPosition;
                DrawLine(tipS, leftS, 1.4f * scale, edge * (0.70f * alpha * life));
                DrawLine(tipS, rightS, 1.4f * scale, edge * (0.70f * alpha * life));
            }
            DrawSoftRay(c, 0f, 16f * scale, 16f * scale, crystal, 0.20f * alpha * life);
            DrawSoftRay(c, 0f, 8f * scale, 8f * scale, Color.White, 0.75f * alpha * life);
        }

        // Variant 11: "bloom" - a petal burst, made explicitly petal-shaped: six
        // long soft petals unfurl out of a tight bud (each fat in the middle and
        // rounded at the tip) around a small counter-whorl and a bright heart.
        // Own palette: rose / pink.
        private static void ArcaneStarBloom(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color petal = new Color(255, 150, 200);
            Color blush = new Color(255, 208, 232);
            Color heart = new Color(255, 252, 250);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float unfurl = MathHelper.Clamp(t / 0.40f, 0f, 1f);
            unfurl = 1f - (1f - unfurl) * (1f - unfurl);
            float life = 1f - MathHelper.Clamp((t - 0.55f) / 0.45f, 0f, 1f);
            if (life <= 0.01f)
                return;
            const int petals = 6;
            for (int i = 0; i < petals; i++)
            {
                float a = settle + MathHelper.TwoPi * i / petals + t * 0.22f;
                Vector2 root = c + a.ToRotationVector2() * (MathHelper.Lerp(4f, 16f, unfurl) * scale);
                float pl = (28f + 16f * unfurl) * scale;
                Vector2 mid = root + a.ToRotationVector2() * (pl * 0.5f);
                DrawSoftRay(mid, a, pl, 16f * scale * unfurl, petal, 0.30f * alpha * life);
                DrawSoftRay(mid, a, pl * 0.8f, 7f * scale * unfurl, blush, 0.42f * alpha * life);
                DrawSoftRay(root + a.ToRotationVector2() * (pl * 0.42f), a, pl * 0.5f, 2.6f * scale, heart, 0.50f * alpha * life);
            }
            for (int i = 0; i < petals; i++)                                                 // inner counter-whorl
            {
                float a = settle + MathHelper.Pi / petals + MathHelper.TwoPi * i / petals - t * 0.18f;
                DrawSoftRay(c + a.ToRotationVector2() * (MathHelper.Lerp(3f, 10f, unfurl) * scale), a, 14f * scale, 6f * scale, blush, 0.28f * alpha * life);
            }
            DrawSoftRay(c, 0f, 20f * scale, 20f * scale, petal, 0.24f * alpha * life);
            DrawSoftRay(c, 0f, 11f * scale, 11f * scale, heart, 0.85f * alpha * life);
        }

        // Variant 12: "arc" - a forked chain of jagged plasma strokes through the
        // hit, with soft lens nodes at every joint. Refined: a saturated electric
        // blue with a triple line stack (halo / plasma / white-hot core) so the
        // bolt reads as lightning rather than a glow smear. Own palette: electric
        // blue.
        private static void ArcaneStarArc(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color plasma = new Color(110, 160, 255);
            Color core = new Color(240, 248, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float life = 1f - MathHelper.Clamp((t - 0.25f) / 0.75f, 0f, 1f);
            if (life <= 0.01f)
                return;
            Vector2 dir = settle.ToRotationVector2();
            Vector2 side = new Vector2(-dir.Y, dir.X);
            const int segs = 7;
            float len = 76f * scale;
            Vector2 start = c - dir * (len * 0.5f);
            Vector2 prev = start;
            for (int i = 1; i <= segs; i++)
            {
                float f = i / (float)segs;
                float jitter = (float)System.Math.Sin(i * 2.7f + t * 6f) * 10f * scale * (float)System.Math.Sin(f * System.Math.PI);
                Vector2 p = start + dir * (len * f) + side * jitter;
                Vector2 a = prev - Main.screenPosition;
                Vector2 b = p - Main.screenPosition;
                DrawLine(a, b, 7f * scale, plasma * (0.22f * alpha * life));
                DrawLine(a, b, 3.2f * scale, plasma * (0.60f * alpha * life));
                DrawLine(a, b, 1.3f * scale, core * (0.95f * alpha * life));
                DrawSoftRay(p, 0f, 8f * scale, 8f * scale, core, 0.35f * alpha * life);
                prev = p;
            }
            // Two forked branches off the middle joints.
            for (int k = 0; k < 2; k++)
            {
                float forkAng = k == 0 ? 0.9f : -1.05f;
                Vector2 forkDir = new Vector2((float)System.Math.Cos(settle + forkAng), (float)System.Math.Sin(settle + forkAng));
                Vector2 forkStart = start + dir * (len * (0.35f + 0.3f * k))
                    + side * ((float)System.Math.Sin((3 + 2 * k) * 2.7f + t * 6f) * 8f * scale);
                Vector2 prevFork = forkStart;
                for (int i = 1; i <= 3; i++)
                {
                    float f = i / 3f;
                    float wobble = (float)System.Math.Sin(i * 3.1f + k) * 4f * scale * f;
                    Vector2 p = forkStart + forkDir * (26f * scale * f) + new Vector2(-forkDir.Y, forkDir.X) * wobble;
                    Vector2 a = prevFork - Main.screenPosition;
                    Vector2 b = p - Main.screenPosition;
                    DrawLine(a, b, 2.4f * scale, plasma * (0.60f * alpha * life));
                    DrawLine(a, b, 1f * scale, core * (0.85f * alpha * life));
                    prevFork = p;
                }
            }
            DrawSoftRay(c, 0f, 13f * scale, 13f * scale, core, 0.65f * alpha * life);
        }

        // Variant 13: "sigil" - a cast magic CIRCLE, redrawn to read as magic
        // being drawn rather than an explosion: a crisp outer ring and inner
        // ring, an inscribed hexagram of two counter-rotating triangles, twelve
        // glyph ticks around the rim and a bright heart. Own palette: emerald.
        private static void ArcaneStarSigil(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color line = new Color(110, 235, 170);
            Color rune = new Color(228, 255, 240);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float draw = MathHelper.Clamp(t / 0.30f, 0f, 1f);
            draw = 1f - (1f - draw) * (1f - draw);
            float life = 1f - MathHelper.Clamp((t - 0.60f) / 0.40f, 0f, 1f);
            if (life <= 0.01f)
                return;
            float r = 32f * scale * draw;
            float spin = settle + t * 0.35f;
            DrawSoftRay(c, 0f, r * 2.4f, r * 2.4f, line, 0.14f * alpha * life);
            DrawRing(c, r, 2.2f * scale, line * (0.80f * alpha * life), 36);
            DrawRing(c, r * 0.72f, 1.4f * scale, rune * (0.55f * alpha * life), 28);
            for (int tri = 0; tri < 2; tri++)                                                // inscribed hexagram
            {
                float baseA = spin * (tri == 0 ? 1f : -1.2f) + (tri == 0 ? 0f : MathHelper.Pi / 3f);
                for (int i = 0; i < 3; i++)
                {
                    float a0 = baseA + MathHelper.TwoPi * i / 3f;
                    float a1 = baseA + MathHelper.TwoPi * ((i + 1) % 3) / 3f;
                    Vector2 p0 = c + a0.ToRotationVector2() * (r * 0.68f) - Main.screenPosition;
                    Vector2 p1 = c + a1.ToRotationVector2() * (r * 0.68f) - Main.screenPosition;
                    DrawLine(p0, p1, 1.6f * scale, rune * (0.80f * alpha * life));
                }
            }
            for (int i = 0; i < 12; i++)                                                     // twelve glyph ticks
            {
                float a = spin + MathHelper.TwoPi * i / 12f;
                Vector2 dir = a.ToRotationVector2();
                bool major = i % 3 == 0;
                DrawLine(c + dir * (r * (major ? 0.80f : 0.86f)) - Main.screenPosition,
                    c + dir * (r * 1.10f) - Main.screenPosition,
                    (major ? 2.4f : 1.4f) * scale, rune * ((major ? 0.90f : 0.55f) * alpha * life));
            }
            DrawSoftRay(c, 0f, 10f * scale, 10f * scale, rune, 0.85f * alpha * life);
        }

        // Variant 14: "ray" - a piercing RAY of light: a long straight beam that
        // runs THROUGH the hit (it extends both ways from it) with constant-
        // thickness stripes, lens-flare endpoints and a balanced flare cross. It
        // never moves and has no head, which is what separates it from the
        // travelling Lance. Own palette: hot white-green.
        private static void ArcaneStarRay(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color glow = new Color(90, 240, 170);
            Color body = new Color(190, 255, 220);
            Color core = new Color(255, 255, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float grow = MathHelper.Clamp(t / 0.20f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float life = 1f - MathHelper.Clamp((t - 0.45f) / 0.55f, 0f, 1f);
            if (life <= 0.01f)
                return;
            Vector2 dir = settle.ToRotationVector2();
            float half = (34f + 62f * grow) * scale;
            float len = half * 2f;
            Vector2 a = c - dir * half;
            Vector2 b = c + dir * half;
            DrawSoftRay(a, 0f, 26f * scale, 26f * scale, glow, 0.24f * alpha * life);        // endpoint flares
            DrawSoftRay(b, 0f, 26f * scale, 26f * scale, glow, 0.24f * alpha * life);
            DrawSoftRay(a, 0f, 12f * scale, 12f * scale, core, 0.40f * alpha * life);
            DrawSoftRay(b, 0f, 12f * scale, 12f * scale, core, 0.40f * alpha * life);
            // Beam bands: soft bars, so the body keeps its constant thickness but no
            // longer terminates in a square slab sticking out past the endpoint flare.
            DrawSoftBar(c, settle, len, 17f * scale, glow, 0.10f * alpha * life);
            DrawSoftBar(c, settle, len, 7f * scale, body, 0.24f * alpha * life);
            DrawSoftBar(c, settle, len, 2.6f * scale, core, 0.65f * alpha * life);
            DrawSoftBar(c, settle, 40f * scale, 1.6f * scale, body, 0.55f * alpha * life);      // flare cross
            DrawSoftBar(c, settle + MathHelper.PiOver2, 40f * scale, 1.6f * scale, body, 0.55f * alpha * life);

            // Energy pulse running along the beam. The beam stays ANCHORED - that is
            // what separates it from the travelling Lance - but it is visibly live.
            float pulse = t * 2.4f % 1f;
            DrawSoftBar(a + dir * (len * pulse), settle, 16f * scale, 5f * scale, core, 0.35f * alpha * life);
        }

        // Variant 15: "halo" - an orbiting crown, refined: a tilted perspective
        // ring, six phase-locked beads riding it, a tighter counter-rotating
        // second halo and a bright heart. Own palette: gold.
        private static void ArcaneStarHalo(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color ring = new Color(255, 214, 120);
            Color bead = new Color(255, 250, 230);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float spin = settle + t * 2.6f;
            float open = MathHelper.Clamp(t / 0.25f, 0f, 1f);
            open = 1f - (1f - open) * (1f - open);
            float life = 1f - MathHelper.Clamp((t - 0.50f) / 0.50f, 0f, 1f);
            if (life <= 0.01f)
                return;
            float r = (32f + 8f * open) * scale;
            DrawSoftEllipseRing(c, r, 0.34f, 4.5f * scale, ring, 0.40f * alpha * life, 28);
            DrawRing(c, r, 1.8f * scale, ring * (0.45f * alpha * life), 32);
            for (int i = 0; i < 6; i++)
            {
                float a = spin + MathHelper.TwoPi * i / 6f;
                Vector2 p = c + new Vector2((float)System.Math.Cos(a) * r, (float)System.Math.Sin(a) * r * 0.34f);
                DrawSoftRay(p, 0f, 7f * scale, 7f * scale, bead, 0.75f * alpha * life);
            }
            DrawSoftEllipseRing(c, r * 0.6f, 0.5f, 3f * scale, bead, 0.32f * alpha * life, 22);
            DrawSoftRay(c, 0f, 15f * scale, 15f * scale, ring, 0.26f * alpha * life);
            DrawSoftRay(c, 0f, 9f * scale, 9f * scale, bead, 0.85f * alpha * life);
        }

        // ---- Second wildcard set (Variant16-26) --------------------------------
        // Eleven more placeholder signatures. Variant16 is the redesigned SLASH
        // (the old Variant6 arc was renamed Crescent); 17-26 are brand-new, each
        // a distinct archetype with its own palette. Same rules as the first set:
        // pure functions of (centre, scale, alpha, progress, settle), built from
        // the shared primitives, drawn additively, spawning nothing.

        // Variant 16: "slash" - the real thing: a fast DIRECTIONAL blade cut, not
        // an arc. A long, slightly bowed blade sweeps along the stamp orientation,
        // heavy at the leading edge and thinning to a fine tail, with a white-hot
        // core, parallel speed echoes and a bright tip clang. Own palette:
        // steel-white.
        private static void ArcaneStarSlash(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color glow = new Color(150, 200, 255);
            Color blade = new Color(222, 236, 255);
            Color hot = new Color(255, 255, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float open = MathHelper.Clamp(t / 0.14f, 0f, 1f);                                // snaps out
            open = 1f - (1f - open) * (1f - open);
            float life = 1f - MathHelper.Clamp((t - 0.34f) / 0.66f, 0f, 1f);                 // quick cut, fast fade
            if (life <= 0.01f)
                return;
            Vector2 dir = settle.ToRotationVector2();
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            float reach = (36f + 92f * open) * scale;
            Vector2 start = c - dir * (reach * 0.55f);
            Vector2 end = c + dir * (reach * 0.95f);
            Vector2 ctrl = c + dir * (reach * 0.20f) + perp * (14f * scale);                 // shallow bow
            const int segs = 7;
            Vector2 prev = start;
            for (int i = 1; i <= segs; i++)
            {
                float f = i / (float)segs;
                Vector2 p = Bez2(start, ctrl, end, f);
                Vector2 segv = p - prev;
                if (segv.LengthSquared() > 0.01f)
                {
                    float taper = (float)System.Math.Sin(f * System.Math.PI) * (0.55f + 0.75f * f); // fat head, fine tail
                    float w = (2.5f + 11f * taper) * scale;
                    Vector2 mid = (prev + p) * 0.5f;
                    float rot = segv.ToRotation();
                    DrawSoftRay(mid, rot, segv.Length() + w * 2.2f, w * 2.6f, glow, 0.16f * alpha * life);
                    DrawSoftRay(mid, rot, segv.Length() + w * 1.1f, w, blade, 0.55f * alpha * life);
                    DrawLine(prev - Main.screenPosition, p - Main.screenPosition, 1.6f * scale * (0.5f + taper), hot * (0.85f * alpha * life));
                }
                prev = p;
            }
            for (int k = 1; k <= 2; k++)                                                     // parallel speed echoes
            {
                float off = k * 6f * scale;
                for (int i = 3; i <= segs; i++)
                {
                    float f1 = i / (float)segs;
                    float f0 = (i - 1) / (float)segs;
                    Vector2 p = Bez2(start, ctrl, end, f1) + perp * (off * (0.4f + 0.6f * f1));
                    Vector2 q = Bez2(start, ctrl, end, f0) + perp * (off * (0.4f + 0.6f * f0));
                    Vector2 sv = p - q;
                    if (sv.LengthSquared() > 0.01f)
                        DrawSoftRay((p + q) * 0.5f, sv.ToRotation(), sv.Length() * 1.2f, 2f * scale * f1, blade, 0.30f * alpha * life);
                }
            }
            DrawSoftRay(end, 0f, 16f * scale, 16f * scale, hot, 0.70f * alpha * life);        // tip clang
            DrawSoftRay(end, settle, 30f * scale, 2.2f * scale, hot, 0.55f * alpha * life);
            DrawSoftRay(end, settle + MathHelper.PiOver2, 14f * scale, 1.8f * scale, hot, 0.45f * alpha * life);
        }

        // Variant 17: "crosscut" - two thin blades crossing into an X, offset so
        // each one is a comet-style cut. Own palette: pale gold.
        private static void ArcaneStarCrosscut(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color gold = new Color(255, 224, 150);
            Color hot = new Color(255, 250, 235);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float open = MathHelper.Clamp(t / 0.16f, 0f, 1f);
            open = 1f - (1f - open) * (1f - open);
            float life = 1f - MathHelper.Clamp((t - 0.42f) / 0.58f, 0f, 1f);
            if (life <= 0.01f)
                return;
            float len = (30f + 86f * open) * scale;
            for (int i = 0; i < 2; i++)
            {
                float a = settle + MathHelper.PiOver2 * i;
                Vector2 offset = c + a.ToRotationVector2() * (len * 0.18f);
                DrawSoftRay(offset, a, len * 1.05f, 19f * scale, gold, 0.18f * alpha * life);
                DrawSoftRay(offset, a, len, 8f * scale, gold, 0.50f * alpha * life);
                DrawSoftBar(offset, a, len * 0.9f, 2f * scale, hot, 0.80f * alpha * life);
            }
            DrawSoftRay(c, 0f, 14f * scale, 14f * scale, hot, 0.60f * alpha * life);
        }

        // Variant 18: "ripple" - concentric vertical shockwave rings expanding at
        // staggered times, with a thin bright leading rim. Own palette: cyan.
        private static void ArcaneStarRipple(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color wave = new Color(120, 235, 255);
            Color hot = new Color(240, 255, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            for (int k = 0; k < 3; k++)
            {
                float e0 = MathHelper.Clamp((t - k * 0.10f) / 0.75f, 0f, 1f);
                if (e0 <= 0f)
                    continue;
                float e = 1f - (1f - e0) * (1f - e0);
                float rad = MathHelper.Lerp(8f, 30f + k * 14f, e) * scale;
                float fade = 1f - e0;
                DrawRing(c, rad, (2.6f - k * 0.5f) * scale, wave * (alpha * fade * 0.75f), 32);
                if (k == 0)
                    DrawRing(c, rad * 1.06f, 1.2f * scale, hot * (alpha * fade * 0.55f), 28);
            }
            DrawSoftRay(c, 0f, 20f * scale, 20f * scale, wave, 0.20f * alpha * (1f - t));
            DrawSoftRay(c, 0f, 9f * scale, 9f * scale, hot, 0.70f * alpha * (1f - t * 0.6f));
        }

        // Variant 19: "quills" - a hard radial spike urchin: twelve straight
        // hard-edged quills out of a dark core (hard lines, not soft lenses).
        // Own palette: deep violet.
        private static void ArcaneStarQuills(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color quill = new Color(196, 150, 255);
            Color dark = new Color(70, 30, 110);
            Color hot = new Color(255, 244, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float grow = MathHelper.Clamp(t / 0.22f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float life = 1f - MathHelper.Clamp((t - 0.45f) / 0.55f, 0f, 1f);
            if (life <= 0.01f)
                return;
            DrawSoftRay(c, 0f, 26f * scale, 26f * scale, dark, 0.30f * alpha * life);
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float a = settle + MathHelper.TwoPi * i / n;
                Vector2 dir = a.ToRotationVector2();
                float r0 = (7f + 4f * grow) * scale;
                float r1 = (20f + 30f * grow) * scale * (0.85f + 0.15f * (i % 2));
                DrawLine(c + dir * r0 - Main.screenPosition, c + dir * r1 - Main.screenPosition, 3.4f * scale, quill * (0.60f * alpha * life));
                DrawLine(c + dir * (r0 + (r1 - r0) * 0.45f) - Main.screenPosition, c + dir * r1 - Main.screenPosition, 1.3f * scale, hot * (0.80f * alpha * life));
            }
            DrawSoftRay(c, 0f, 12f * scale, 12f * scale, hot, 0.65f * alpha * life);
        }

        // Variant 20: "comet" - a shooting star passing through the hit: a bright
        // head bloom with a long tapering tail and trailing embers that translate
        // along the stamp orientation. Own palette: icy white-blue.
        private static void ArcaneStarComet(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color tail = new Color(140, 205, 255);
            Color head = new Color(225, 245, 255);
            Color core = new Color(255, 255, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float fly = MathHelper.Clamp(t / 0.65f, 0f, 1f);
            float life = 1f - MathHelper.Clamp((t - 0.55f) / 0.45f, 0f, 1f);
            if (life <= 0.01f)
                return;
            Vector2 dir = settle.ToRotationVector2();
            Vector2 side = new Vector2(-dir.Y, dir.X);
            Vector2 headPos = c + dir * MathHelper.Lerp(-18f, 44f, fly) * scale;
            DrawSoftRay(headPos - dir * (40f * scale), settle, 96f * scale, 16f * scale, tail, 0.20f * alpha * life);
            DrawSoftRay(headPos - dir * (34f * scale), settle, 70f * scale, 8f * scale, tail, 0.34f * alpha * life);
            DrawSoftRay(headPos, 0f, 22f * scale, 22f * scale, head, 0.45f * alpha * life);
            DrawSoftRay(headPos, 0f, 10f * scale, 10f * scale, core, 0.90f * alpha * life);
            for (int i = 0; i < 5; i++)
            {
                float f = (i + 1) / 6f;
                Vector2 p = headPos - dir * (46f * f * scale) + side * ((float)System.Math.Sin(i * 2.1f) * 7f * scale);
                DrawSoftRay(p, settle, 10f * scale, 4f * scale, tail, 0.34f * alpha * life * (1f - f));
            }
        }

        // Variant 21: "eruption" - a vertical fire plume (world-up, not along the
        // stamp orientation): a base flash, a tapering column that grows upward and
        // drifting embers. Own palette: orange / red.
        private static void ArcaneStarEruption(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color deep = new Color(196, 58, 28);
            Color mid = new Color(255, 148, 58);
            Color core = new Color(255, 242, 196);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float up = MathHelper.Clamp(t / 0.32f, 0f, 1f);
            up = 1f - (1f - up) * (1f - up);
            float life = 1f - MathHelper.Clamp((t - 0.55f) / 0.45f, 0f, 1f);
            if (life <= 0.01f)
                return;
            DrawSoftRay(c + new Vector2(0f, 5f * scale), 0f, 42f * scale, 20f * scale, mid, 0.30f * alpha * life);
            DrawSoftRay(c + new Vector2(0f, 5f * scale), 0f, 22f * scale, 12f * scale, core, 0.45f * alpha * life);
            for (int i = 0; i < 6; i++)
            {
                float f = i / 5f;
                float y = -(6f + f * 48f * up) * scale;
                Vector2 p = c + new Vector2((float)System.Math.Sin(f * 3.1f) * 4f * scale * (1f - f), y);
                DrawSoftRay(p, -MathHelper.PiOver2, (24f - 9f * f) * scale, (17f - 8f * f) * scale,
                    f > 0.6f ? core : mid, (0.34f - 0.12f * f) * alpha * life * up);
            }
            for (int i = 0; i < 4; i++)
            {
                float f = (i + 1) / 5f;
                Vector2 p = c + new Vector2((i - 1.5f) * 5f * scale, -(30f + f * 40f * up) * scale);
                DrawSoftRay(p, -MathHelper.PiOver2, 9f * scale, 4f * scale, deep, 0.30f * alpha * life * (1f - f));
            }
        }

        // Variant 22: "tempest" - a ring of short jagged bolts firing outward from
        // a dark eye (radial lightning, not a single forked arc). Own palette:
        // deep blue / white.
        private static void ArcaneStarTempest(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color bolt = new Color(120, 170, 255);
            Color eye = new Color(40, 30, 90);
            Color hot = new Color(235, 245, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float grow = MathHelper.Clamp(t / 0.20f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float life = 1f - MathHelper.Clamp((t - 0.35f) / 0.65f, 0f, 1f);
            if (life <= 0.01f)
                return;
            // Socket, not a glow: a solid dark disc with the core sitting inside it.
            DrawSolidDisc(c, 30f * scale, eye, 0.42f * alpha * life);
            const int n = 8;
            for (int i = 0; i < n; i++)
            {
                float a = settle + MathHelper.TwoPi * i / n + 0.25f;
                Vector2 dir = FromPolar(1f, a);
                float reach = (24f + 22f * grow) * scale * (0.80f + 0.20f * (i % 3) / 2f);
                Vector2 root = c + dir * (8f * scale);
                Vector2 tip = c + dir * (8f * scale + reach);
                // Same seed twice, so the hot core follows the wide bolt exactly.
                // The seed steps with time so the bolt re-strikes: a static path made
                // the whole style freeze once the grow-in finished.
                int seed = i * 37 + 11 + (int)(t * 14f) * 101;
                DrawLightning(root, tip, 2.4f * scale, bolt, 0.55f * alpha * life, 0.22f, 3, seed);
                DrawLightning(root, tip, 1f * scale, hot, 0.85f * alpha * life, 0.22f, 3, seed);
                DrawSolidDisc(tip, 7f * scale, hot, 0.45f * alpha * life);
            }
            DrawSolidDisc(c, 11f * scale, hot, 0.75f * alpha * life);
        }

        // Variant 23: "bramble" - five thorned tendrils curling outward, each a
        // curved vine with alternating spikes and a bright bud at the tip. Own
        // palette: green.
        private static void ArcaneStarBramble(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color vine = new Color(110, 215, 130);
            Color dark = new Color(28, 78, 42);
            Color thorn = new Color(220, 255, 210);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float grow = MathHelper.Clamp(t / 0.35f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float life = 1f - MathHelper.Clamp((t - 0.60f) / 0.40f, 0f, 1f);
            if (life <= 0.01f)
                return;
            const int arms = 5;
            const int segs = 6;
            for (int arm = 0; arm < arms; arm++)
            {
                float ang = settle + MathHelper.TwoPi * arm / arms;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 side = new Vector2(-dir.Y, dir.X);
                Vector2 ctrl = c + dir * (18f * grow * scale) + side * (10f * scale);
                Vector2 endP = c + dir * (34f * grow * scale) + side * (16f * scale);
                Vector2 prev = c;
                for (int i = 1; i <= segs; i++)
                {
                    float f = i / (float)segs;
                    Vector2 p = Bez2(c, ctrl, endP, f);
                    Vector2 sv = p - prev;
                    if (sv.LengthSquared() > 0.01f)
                    {
                        float w = (4.5f - 2.2f * f) * scale;
                        DrawSoftRay((prev + p) * 0.5f, sv.ToRotation(), sv.Length() + w * 1.6f, w, i % 2 == 0 ? vine : dark, 0.42f * alpha * life);
                        if (i % 2 == 1)
                        {
                            Vector2 midp = (prev + p) * 0.5f;
                            Vector2 tdir = (sv.ToRotation() + MathHelper.PiOver2).ToRotationVector2();
                            DrawLine(midp - Main.screenPosition, midp + tdir * (7f * scale) - Main.screenPosition, 1.6f * scale, thorn * (0.65f * alpha * life));
                        }
                    }
                    prev = p;
                }
                DrawSoftRay(endP, 0f, 9f * scale, 9f * scale, thorn, 0.45f * alpha * life);
            }
            DrawSoftRay(c, 0f, 12f * scale, 12f * scale, thorn, 0.40f * alpha * life);
        }

        // Variant 24: "nebula" - a soft cloud of overlapping dim blooms with tiny
        // embedded stars. No hard geometry at all, so it reads as gas. Own palette:
        // violet / indigo.
        private static void ArcaneStarNebula(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color cloudA = new Color(168, 112, 240);
            Color cloudB = new Color(96, 62, 188);
            Color star = new Color(255, 252, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float bloom = MathHelper.Clamp(t / 0.55f, 0f, 1f);
            bloom = 1f - (1f - bloom) * (1f - bloom);
            float life = 1f - MathHelper.Clamp((t - 0.60f) / 0.40f, 0f, 1f);
            if (life <= 0.01f)
                return;
            float r = (12f + 30f * bloom) * scale;
            DrawSoftRay(c, 0f, r * 1.8f, r * 1.8f, cloudB, 0.14f * alpha * life);
            for (int i = 0; i < 5; i++)
            {
                float a = settle + MathHelper.TwoPi * i / 5f + t * 0.3f;
                Vector2 p = c + a.ToRotationVector2() * (r * 0.42f);
                DrawSoftRay(p, 0f, r * 1.5f, r * 1.5f, i % 2 == 0 ? cloudA : cloudB, 0.16f * alpha * life);
            }
            for (int i = 0; i < 7; i++)
            {
                float a = settle + i * 2.3999632f;
                float dist = (6f + 22f * bloom * (0.4f + 0.3f * (i % 3))) * scale;
                Vector2 p = c + a.ToRotationVector2() * dist;
                float s = (4f + (i % 2) * 2f) * scale;
                DrawSoftRay(p, 0f, s * 3f, s * 0.8f, star, 0.55f * alpha * life);
                DrawSoftRay(p, MathHelper.PiOver2, s * 3f, s * 0.8f, star, 0.55f * alpha * life);
            }
            DrawSoftRay(c, 0f, 10f * scale, 10f * scale, star, 0.45f * alpha * life);
        }

        // Variant 25: "tear" - a standing void rip: a dark vertical slit with two
        // bright lips and magenta bleed, opening over the first quarter of its
        // life. Own palette: white / magenta on void.
        private static void ArcaneStarTear(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color lip = new Color(255, 120, 220);
            Color voidCol = new Color(24, 4, 42);
            Color hot = new Color(255, 244, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float open = MathHelper.Clamp(t / 0.25f, 0f, 1f);
            open = 1f - (1f - open) * (1f - open);
            float life = 1f - MathHelper.Clamp((t - 0.55f) / 0.45f, 0f, 1f);
            if (life <= 0.01f)
                return;
            float h = (26f + 62f * open) * scale;
            float w = (2f + 8f * open) * scale;
            DrawSoftRay(c, -MathHelper.PiOver2, h * 1.15f, w * 2.8f, lip, 0.20f * alpha * life);
            DrawSoftRay(c, -MathHelper.PiOver2, h * 0.95f, w * 1.5f, voidCol, 0.55f * alpha * life);
            for (int i = -1; i <= 1; i += 2)
                DrawSoftRay(c + new Vector2(i * (w * 0.85f), 0f), -MathHelper.PiOver2, h * 1.05f, 2.4f * scale, hot, 0.75f * alpha * life);
            DrawSoftRay(c + new Vector2(0f, -h * 0.5f), 0f, 10f * scale, 10f * scale, hot, 0.55f * alpha * life);
            DrawSoftRay(c + new Vector2(0f, h * 0.5f), 0f, 10f * scale, 10f * scale, hot, 0.55f * alpha * life);
        }

        // Variant 26: "salvo" - three thin strike beams converging down into the
        // point from above at staggered times, each with an impact flash. Own
        // palette: yellow-white.
        private static void ArcaneStarSalvo(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color beam = new Color(255, 226, 140);
            Color hot = new Color(255, 252, 230);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float life = 1f - MathHelper.Clamp((t - 0.45f) / 0.55f, 0f, 1f);
            if (life <= 0.01f)
                return;
            for (int i = 0; i < 3; i++)
            {
                float grow = MathHelper.Clamp((t - i * 0.07f) / 0.22f, 0f, 1f);
                if (grow <= 0f)
                    continue;
                grow = 1f - (1f - grow) * (1f - grow);
                float ang = -MathHelper.PiOver2 + (i - 1) * 0.32f + settle * 0.15f;
                Vector2 dir = ang.ToRotationVector2();
                float len = (26f + 66f * grow) * scale;
                Vector2 start = c - dir * len;
                Vector2 mid = (start + c) * 0.5f;
                DrawSoftBar(mid, ang, len, 5.5f * scale, beam, 0.30f * alpha * life);
                DrawSoftBar(mid, ang, len, 1.8f * scale, hot, 0.75f * alpha * life);
            }
            // Convergence flash: a small core with long spikes and a shock ring, so
            // three strikes resolve into a STRIKE instead of a bright ball. The old
            // per-strike impact discs are gone - they all landed within ~2px of each
            // other and merged into exactly the orb this pass is removing.
            DrawSolidDisc(c, 9f * scale, hot, 0.70f * alpha * life);
            for (int k = 0; k < 6; k++)
            {
                float sa = k * (MathHelper.Pi / 3f) + 0.4f;
                DrawCapsule(c + sa.ToRotationVector2() * (5f * scale), c + sa.ToRotationVector2() * (24f * scale),
                    2.2f * scale, hot, 0.50f * alpha * life);
            }
            DrawSolidRing(c, (16f + 16f * t) * scale, 1.8f * scale, beam, 0.45f * alpha * life);
        }

        // ---- Third wildcard set (Variant27-36) ---------------------------------
        // Ten more placeholders, and this wave deliberately leaves the
        // slash / lightning / orb / star families alone: aurora curtains, a
        // spider web, a biting maw, an eye, a sawblade, a magnetic field, a
        // sonic burst, confetti, digital glitch blocks and a portal gate. Same
        // contract as the rest - pure functions of (centre, scale, alpha,
        // progress, settle), built from the shared primitives, additive, and
        // spawning nothing. Palettes are per-style as usual.
        //
        // Revision: every style in this wave now builds its silhouette from a
        // SOLID shape primitive (DrawSolidDisc / DrawSolidTriangle / DrawWedge /
        // DrawCapsule / DrawEllipseBand) instead of a stack of soft radial rays.
        // That is what stopped them all reading as orbs with a bright centre.

        // Variant 27: "aurora" - vertical curtains of light that ripple: tall
        // soft ribbons whose height, lean and tint wave over the life, with a
        // brighter hem along the bottom. No radial geometry at all, so it reads
        // as a sky effect rather than an impact. Own palette: green -> teal ->
        // violet.
        private static readonly Color[] AuroraRamp =
        {
            new Color(110, 255, 190),
            new Color(90, 220, 235),
            new Color(170, 130, 255)
        };

        private static void ArcaneStarAurora(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color low = new Color(110, 255, 190);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float rise = Ease(FxEase.OutQuad, t / 0.30f);
            float life = 1f - MathHelper.Clamp((t - 0.50f) / 0.50f, 0f, 1f);
            if (life <= 0.01f)
                return;
            const int ribbons = 6;
            for (int i = 0; i < ribbons; i++)
            {
                float f = i / (float)(ribbons - 1);
                float x = (f - 0.5f) * 74f * scale;
                float wob = (float)System.Math.Sin(f * 5.4f + t * 5.0f) * 0.30f;
                float h = (34f + 46f * (0.55f + 0.45f * (float)System.Math.Cos(f * 4.1f))) * scale * rise;
                float w = (9f + 6f * (float)System.Math.Sin(f * 7.3f + t * 3.0f)) * scale;
                Vector2 baseP = c + new Vector2(x, 12f * scale);
                Vector2 topP = c + new Vector2(x + wob * 26f * scale, 12f * scale - h);
                Color col = Sample(AuroraRamp, f);
                DrawSoftRay(baseP, -MathHelper.PiOver2 + wob, h, w, col, 0.20f * alpha * life);
                DrawSoftRay(topP, -MathHelper.PiOver2 + wob, h * 0.55f, w * 0.6f, col, 0.16f * alpha * life);
                // Vertical striations: the thing that makes a curtain read as aurora
                // rather than as a glowing column. Tapered so they dissolve upward.
                for (int s = -1; s <= 1; s++)
                {
                    float sx = s * 3.2f * scale;
                    DrawTaperedStroke(baseP + new Vector2(sx, 0f), 1.5f * scale,
                        baseP + new Vector2(sx + wob * 22f * scale, -h * (0.72f - 0.16f * (float)System.Math.Abs(s))),
                        0.4f * scale, col, 0.34f * alpha * life);
                }
                // Frosted foot: a small solid bead, not a round glow.
                DrawSolidEllipse(baseP, 13f * scale, 4.5f * scale, 0f, col, 0.30f * alpha * life);
            }
            // Horizon line under the curtains. This replaced a wide soft blob,
            // which is exactly what was making the sky styles read as orbs.
            DrawCapsule(c + new Vector2(-38f * scale, 11f * scale), c + new Vector2(38f * scale, 11f * scale),
                3.2f * scale, low, 0.26f * alpha * life, true);
        }

        // Variant 28: "web" - a spider's net snapping over the hit: eight hard
        // anchor threads radiating from a tight hub, four capture strands bowed
        // INWARD between adjacent anchors, and dew beads on the crossings. A mesh,
        // not a ring or a rune - and no centre glow, so it cannot read as an orb.
        private static void ArcaneStarWeb(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color silk = new Color(226, 236, 250);
            Color bead = new Color(255, 255, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float spin = MathHelper.Clamp(t / 0.35f, 0f, 1f);
            spin = 1f - (1f - spin) * (1f - spin);
            float life = 1f - MathHelper.Clamp((t - 0.55f) / 0.45f, 0f, 1f);
            if (life <= 0.01f)
                return;
            const int spokes = 8;
            float rad = (18f + 34f * spin) * scale;

            // Off-centre hub and slightly irregular anchors: a perfect 8-fold radial
            // mesh reads as a wheel / mandala, and real webs are lopsided.
            Vector2 hub = c + new Vector2(4f, -3f) * scale * spin;
            float Ang(int i)
            {
                i %= spokes;
                // The per-spoke jitter also breathes, so the whole net shivers like
                // something alive instead of holding perfectly still after the spin-in.
                return settle + MathHelper.TwoPi * i / spokes
                    + 0.11f * (float)System.Math.Sin(i * 2.3f)
                        * (1f + 0.18f * (float)System.Math.Sin(t * 17f + i));
            }

            // Anchor threads: hard silk from the hub outward.
            for (int i = 0; i < spokes; i++)
            {
                Vector2 dir = Ang(i).ToRotationVector2();
                DrawLineWorld(hub + dir * (rad * 0.11f), hub + dir * rad, 1.4f * scale, silk, 0.30f * alpha * life);
            }

            // Capture strands: chords bowed inward, which is what makes the mesh
            // look caught rather than concentric.
            for (int ring = 1; ring <= 4; ring++)
            {
                float rr = rad * (0.24f + ring * 0.18f);
                for (int i = 0; i < spokes; i++)
                {
                    Vector2 p0 = hub + Ang(i).ToRotationVector2() * rr;
                    Vector2 p1 = hub + Ang(i + 1).ToRotationVector2() * rr;
                    // Wrap-safe midpoint; averaging the two angles breaks across 0.
                    float mid = settle + MathHelper.TwoPi * (i + 0.5f) / spokes;
                    Vector2 ctrl = hub + mid.ToRotationVector2() * (rr * 0.80f);
                    Vector2 prev = p0;
                    for (int s = 1; s <= 4; s++)
                    {
                        Vector2 p = Bez2(p0, ctrl, p1, s / 4f);
                        DrawLineWorld(prev, p, 1.05f * scale, silk, 0.24f * alpha * life);
                        prev = p;
                    }
                }
            }

            // Dew beads hanging on the crossings.
            for (int ring = 1; ring <= 4; ring++)
            {
                float rr = rad * (0.24f + ring * 0.18f);
                for (int i = 0; i < spokes; i++)
                    DrawSolidDisc(hub + Ang(i).ToRotationVector2() * rr,
                        (1.8f + 2.4f * ring / 4f) * scale, bead, 0.30f * alpha * life);
            }

            // Hub knot.
            DrawSolidRing(hub, 5f * scale, 1.2f * scale, silk, 0.50f * alpha * life);
            DrawSolidDisc(hub, 3.4f * scale, bead, 0.45f * alpha * life);
        }

        // Variant 29: "fangs" - a maw biting shut: two solid jaws of interlocking
        // teeth closing over the hit, with gum ridges and a deep red throat. Every
        // tooth is a real solid triangle now - the old version only outlined them
        // with hairlines, which is why they never read as teeth.
        // Own palette: bone white on dark red.
        private static void ArcaneStarFangs(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color gum = new Color(122, 22, 38);
            Color mouth = new Color(58, 10, 20);
            Color fang = new Color(242, 240, 228);
            Color root = new Color(178, 168, 150);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float bite = MathHelper.Clamp(t / 0.30f, 0f, 1f);
            bite = 1f - (1f - bite) * (1f - bite);
            float life = 1f - MathHelper.Clamp((t - 0.45f) / 0.55f, 0f, 1f);
            if (life <= 0.01f)
                return;
            Vector2 axis = settle.ToRotationVector2();
            Vector2 side = new Vector2(-axis.Y, axis.X);
            float rad = (23f + 15f * bite) * scale;          // half-mouth span
            float gap = (34f - 24f * bite) * scale;          // jaw separation
            const int n = 7;

            // Maw + throat as solid mass, so the mouth has an actual interior.
            DrawSolidEllipse(c, rad * 2.05f, gap * 0.95f, settle, gum, 0.30f * alpha * life);
            DrawSolidEllipse(c, rad * 1.05f, gap * 0.46f, settle, mouth, 0.42f * alpha * life);

            // Tooth spacing is shared by both jaws so the rows can interlock instead
            // of colliding into a bright knot at the bite line.
            float span = rad * 0.88f;
            float sp = (span * 2f) / n;
            for (int row = -1; row <= 1; row += 2)
            {
                Vector2 jawLine = c + axis * (row * gap * 0.5f);
                DrawCapsule(jawLine - side * rad, jawLine + side * rad, 5f * scale, gum, 0.55f * alpha * life);
                // The lower jaw sits half a tooth across, so the rows interdigitate.
                float shift = row > 0 ? 0f : sp * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    float u = -span + (i + 0.5f) * sp + shift;
                    // Canines at the corners, smaller teeth toward the middle.
                    float f = (float)System.Math.Abs(i - (n - 1) / 2f) / ((n - 1) / 2f);
                    float len = (9f + 9f * bite) * scale * (0.62f + 0.56f * f);
                    // Cap the reach so the rows interlock across the bite line instead
                    // of fusing into a white knot; canines may cross deeper than the
                    // rest, which is what makes a bite look like a bite.
                    len = MathHelper.Min(len, gap * 0.5f + (4f + 6f * f) * scale);
                    Vector2 baseP = jawLine + side * u;
                    Vector2 apex = baseP - axis * (row * len);
                    DrawWedge(apex, baseP, sp * 0.45f, fang, 0.85f * alpha * life);
                    DrawCapsule(baseP - side * (sp * 0.45f), baseP + side * (sp * 0.45f), 1.6f * scale, root, 0.40f * alpha * life);
                }
            }
        }

        // Variant 30: "eye" - an eye snapping open: a real almond sclera built from
        // vertical slabs whose half-height follows a lid curve, a solid amber iris
        // with spokes, a dark pupil with a catchlight and veins at the corners. The
        // sclera used to be a wide soft wash, which is what made it read as an orb.
        // Own palette: amber on white.
        private static void ArcaneStarEye(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color sclera = new Color(248, 246, 242);
            Color iris = new Color(255, 176, 74);
            Color deep = new Color(146, 72, 10);
            Color pupil = new Color(14, 10, 18);
            Color vein = new Color(198, 50, 50);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float open = MathHelper.Clamp(t / 0.20f, 0f, 1f);
            open = 1f - (1f - open) * (1f - open);
            float life = 1f - MathHelper.Clamp((t - 0.50f) / 0.50f, 0f, 1f);
            if (life <= 0.01f)
                return;
            Vector2 dir = settle.ToRotationVector2();
            Vector2 side = new Vector2(-dir.Y, dir.X);
            float w = 42f * scale;                    // half-width of the almond
            float h = 21f * scale * open;             // half-height at the centre

            // Sclera: one lens mask. Fifteen overlapping vertical capsules gave a
            // scalloped lid line plus additive banding - that was the blockiness.
            DrawSolidLens(c, w * 2f, h * 2f, settle, sclera, 0.62f * alpha * life);

            // Iris / pupil / catchlight: solid discs, not stacked glows. The iris is
            // ~60% of the lid height; at 95% the eye read as a bulging ball. The whole
            // assembly TRACKS on a slow figure-eight, so the iris is not a fixed
            // target painted onto the sclera.
            float irisD = h * 1.25f;
            Vector2 gaze = c + (dir * ((float)System.Math.Sin(t * 3.1f) * 0.10f)
                              + side * ((float)System.Math.Sin(t * 2.3f) * 0.07f)) * h;
            DrawSolidDisc(gaze, irisD * 1.16f, deep, 0.75f * alpha * life);
            DrawSolidDisc(gaze, irisD, iris, 0.85f * alpha * life);
            DrawSolidDisc(gaze, irisD * 0.50f, pupil, 0.95f * alpha * life);
            DrawSolidDisc(gaze - (dir + side) * (h * 0.34f), h * 0.38f, Color.White, 0.95f * alpha * life);

            for (int i = 0; i < 12; i++)
            {
                Vector2 a = (MathHelper.TwoPi * i / 12f).ToRotationVector2();
                DrawLineWorld(gaze + a * (irisD * 0.30f), gaze + a * (irisD * 0.50f), 1.1f * scale, deep, 0.60f * alpha * life);
            }

            // Veins running in from both canthi.
            for (int k = -1; k <= 1; k += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 root = c + dir * (k * w * 0.92f);
                    Vector2 mid = c + dir * (k * w * (0.82f - 0.10f * i)) + side * ((i - 1) * h * 0.42f);
                    DrawLineWorld(root, mid, (1.7f - 0.3f * i) * scale, vein, 0.45f * alpha * life);
                }
            }
        }

        // Variant 31: "sawblade" - a circular saw spinning up: a solid steel plate,
        // fourteen raked triangle teeth around the rim, expansion slots, a bright
        // rim track and a hub, throwing bite sparks outward. The old version was
        // two soft discs plus hairline teeth, which is why it looked like a
        // fuzzball. The only continuously rotating style in the set.
        // Own palette: steel grey with ochre sparks.
        private static void ArcaneStarSawblade(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color plate = new Color(118, 126, 146);
            Color steel = new Color(206, 214, 228);
            Color hot = new Color(255, 250, 236);
            Color spark = new Color(255, 196, 90);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float grow = MathHelper.Clamp(t / 0.22f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float life = 1f - MathHelper.Clamp((t - 0.45f) / 0.55f, 0f, 1f);
            if (life <= 0.01f)
                return;
            float spin = settle + t * 9f;
            float rad = (16f + 22f * grow) * scale;
            const int teeth = 14;
            float step = MathHelper.TwoPi / teeth;

            // Solid plate (this is the silhouette now).
            DrawSolidDisc(c, rad * 2f, plate, 0.55f * alpha * life);

            // Teeth: exact triangles rooted on the rim, raked forward like a saw.
            for (int i = 0; i < teeth; i++)
            {
                float a = spin + i * step;
                Vector2 out1 = a.ToRotationVector2();
                Vector2 tangent = new Vector2(-out1.Y, out1.X);
                Vector2 rootL = c + out1 * (rad * 0.92f) - tangent * (rad * step * 0.48f);
                Vector2 rootR = c + out1 * (rad * 0.92f) + tangent * (rad * step * 0.48f);
                Vector2 tip = c + (a + 0.44f * step).ToRotationVector2() * (rad * 1.34f);
                DrawSolidTriangle(rootL, rootR, tip, steel, 0.95f * alpha * life);
            }

            // Rim track + hub.
            DrawSolidRing(c, rad * 0.98f, 1.6f * scale, hot, 0.50f * alpha * life);
            DrawSolidDisc(c, rad * 0.40f, steel, 0.85f * alpha * life);
            DrawSolidDisc(c, rad * 0.18f, plate, 0.95f * alpha * life);

            // Bite sparks. Two things matter here and both were wrong before:
            // each spark runs its OWN clock, so they leave the rim at staggered
            // times instead of sitting at seven fixed angles and turning with the
            // blade; and the trail is beads, not a capsule. The old version locked
            // all seven to `spin` and gave each a 16 px capsule streak, which is
            // exactly seven thin bright lines spinning around the outside of the
            // saw - the "spinning outer lines". Now a spark pops at the rim,
            // travels outward and dies before it wraps. (The five curved
            // "expansion slots" removed earlier were the other rotating lines.)
            const int sparks = 7;
            float drift = spin * 0.30f;
            for (int i = 0; i < sparks; i++)
            {
                float ph = (t * 2.4f + i * 0.417f) % 1f;
                Vector2 dir = (drift + i * (MathHelper.TwoPi / sparks) + 0.55f).ToRotationVector2();
                Vector2 tang = new Vector2(-dir.Y, dir.X);
                float burst = 1f - ph;
                burst *= burst;
                if (burst <= 0.01f)
                    continue;
                float reach = MathHelper.Lerp(rad * 1.02f, rad * 2.05f, Ease(FxEase.OutCubic, ph));
                Vector2 p = c + dir * reach;
                // Trail: shrinking beads behind the spark, so the outward travel is
                // legible without putting anything line-shaped outside the plate.
                for (int k = 1; k <= 3; k++)
                {
                    float back = reach - 5f * scale * k;
                    if (back <= rad)
                        continue;
                    DrawSolidDisc(c + dir * back, (2.6f - 0.6f * k) * scale, spark,
                        0.45f * alpha * life * burst * (1f - 0.22f * k));
                }
                // The chip itself, tumbling as it flies.
                Vector2 chip = p + tang * ((float)System.Math.Sin(ph * 9f + i * 1.7f) * 4f * scale);
                DrawSolidTriangle(chip - dir * (5f * scale),
                    chip + dir * (3f * scale) + tang * (2.6f * scale),
                    chip + dir * (3f * scale) - tang * (2.6f * scale), hot, 0.60f * alpha * life * burst);
            }
        }

        // Variant 32: "magnet" - a bar magnet: two solid pole blocks (cobalt north,
        // red south) either side of a seam, and field loops that leave the N face
        // and return to the S face while bulging AWAY from the bar - the classic
        // horseshoe pattern - with iron filings beaded along the flux. The old
        // version bulged its loops along the bar, which is why it read as a circle.
        // Own palette: cobalt north, red south.
        private static void ArcaneStarMagnet(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color body = new Color(58, 64, 82);
            Color sheen = new Color(220, 232, 250);
            Color north = new Color(88, 132, 226);
            Color south = new Color(214, 78, 78);
            Color seam = new Color(226, 234, 248);
            Color field = new Color(150, 200, 255);
            Color filing = new Color(226, 234, 246);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float grow = MathHelper.Clamp(t / 0.30f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float life = 1f - MathHelper.Clamp((t - 0.55f) / 0.45f, 0f, 1f);
            if (life <= 0.01f)
                return;
            Vector2 side = settle.ToRotationVector2();       // along the bar, S -> N
            Vector2 perp = new Vector2(-side.Y, side.X);     // across the bar
            float sep = 27f * scale;                         // centre -> pole face
            float half = 8.5f * scale;                       // bar half-thickness
            Vector2 nEnd = c + side * sep;
            Vector2 sEnd = c - side * sep;

            // Body: ONE rounded capsule, so the pole faces are not cut off square.
            // Two flat rectangles meeting at the middle is what read as "red and
            // blue blocks". The pole bands share the body's radius and endpoints,
            // so they round with it instead of stepping out of it.
            DrawCapsule(sEnd, nEnd, half * 2f, body, 0.72f * alpha * life);
            DrawCapsule(c + side * (sep * 0.70f), nEnd, half * 2f, north, 0.62f * alpha * life);
            DrawCapsule(c - side * (sep * 0.70f), sEnd, half * 2f, south, 0.62f * alpha * life);

            // Bevel along the lit edge plus the centre seam: enough shading to read
            // as a metal bar rather than as two swatches.
            DrawCapsule(sEnd + perp * (half * 0.50f), nEnd + perp * (half * 0.50f),
                half * 0.32f, sheen, 0.34f * alpha * life);
            DrawCapsule(c - perp * half, c + perp * half, 1.4f * scale, seam, 0.55f * alpha * life);

            // Field loops, bulging away from the bar on both sides, each carrying a
            // chevron so the direction (N face out, round, into the S face) is read
            // rather than inferred.
            for (int k = 1; k <= 3; k++)
            {
                float bulge = (15f + 14f * k) * scale * grow
                    * (1f + 0.10f * (float)System.Math.Sin(t * 7f + k * 1.3f));
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 ctrl = c + perp * (s * bulge);
                    Vector2 prev = nEnd;
                    for (int i = 1; i <= 8; i++)
                    {
                        Vector2 p = Bez2(nEnd, ctrl, sEnd, i / 8f);
                        DrawLineWorld(prev, p, 1.5f * scale * (1f - 0.16f * k), field, 0.34f * alpha * life);
                        if (i % 2 == 0)
                        {
                            // Beads light in sequence along the loop: reads as current
                            // flowing through the flux rather than as static dots.
                            float flow = 0.5f + 0.5f * (float)System.Math.Sin(i * 1.9f + t * 9f);
                            DrawSolidDisc(p, 2.6f * scale, filing, (0.30f + 0.55f * flow) * alpha * life);
                        }
                        if (i == 4)
                        {
                            Vector2 tan = Bez2Tangent(nEnd, ctrl, sEnd, 0.5f);
                            float len = tan.Length();
                            if (len > 0.001f)
                            {
                                tan /= len;
                                Vector2 nrm = new Vector2(-tan.Y, tan.X);
                                DrawCapsule(p, p + tan * (6f * scale) + nrm * (3.4f * scale),
                                    1.6f * scale, field, 0.42f * alpha * life);
                                DrawCapsule(p, p + tan * (6f * scale) - nrm * (3.4f * scale),
                                    1.6f * scale, field, 0.42f * alpha * life);
                            }
                        }
                        prev = p;
                    }
                }
            }
        }

        // Variant 33: "soundwave" - a sonic burst: three hard wavefront arcs opening
        // to each side of the emitter, a fan of equaliser bars standing out of the
        // rim and a solid musical note in the middle. Own palette: magenta / cyan.
        private static void ArcaneStarSoundwave(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color wave = new Color(255, 92, 200);
            Color alt = new Color(90, 230, 255);
            Color hot = new Color(255, 245, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float out1 = MathHelper.Clamp(t / 0.40f, 0f, 1f);
            out1 = 1f - (1f - out1) * (1f - out1);
            float life = 1f - MathHelper.Clamp((t - 0.50f) / 0.50f, 0f, 1f);
            if (life <= 0.01f)
                return;

            // Emitted wavefronts: hard arcs to either side of the emitter.
            for (int r = 0; r < 3; r++)
            {
                float rr = (18f + r * 13f + 20f * out1) * scale;
                float spread = 0.62f + 0.16f * r;
                Color col = r % 2 == 0 ? wave : alt;
                for (int k = -1; k <= 1; k += 2)
                {
                    // Flank arcs, ~75 degrees off the emitter, so they and the
                    // equaliser bars in front stop occupying the same wedge.
                    float mid = settle + k * 1.30f;
                    DrawArcBand(c, rr, mid - spread * 0.45f, mid + spread * 0.45f,
                        (3.6f - 0.8f * r) * scale, col, 0.60f * alpha * life * (1f - 0.22f * r), 9);
                }
            }

            // Equaliser bars standing out of the rim, fanned in front of the emitter.
            const int bars = 9;
            for (int i = 0; i < bars; i++)
            {
                float f = i / (bars - 1f) - 0.5f;
                Vector2 dir = (settle + f * 1.0f).ToRotationVector2();
                float len = (5f + 18f * (0.30f + 0.70f * (float)System.Math.Abs(System.Math.Sin(f * 6.2f + t * 4f)))) * scale;
                Vector2 baseP = c + dir * (24f * scale);
                DrawCapsule(baseP, baseP + dir * len, 3f * scale, i % 2 == 0 ? wave : alt, 0.70f * alpha * life);
            }

            // Note: solid head, stem and flag.
            Vector2 noteHead = c + new Vector2(-6f, 7f) * scale;
            DrawSolidEllipse(noteHead, 15f * scale, 11f * scale, -0.40f, hot, 0.85f * alpha * life);
            Vector2 stemTop = noteHead + new Vector2(6.4f, -19f) * scale;
            DrawCapsule(noteHead + new Vector2(5.5f, 0f) * scale, stemTop, 2.2f * scale, hot, 0.85f * alpha * life);
            DrawCapsule(stemTop, stemTop + new Vector2(7f, 5f) * scale, 2.4f * scale, hot, 0.70f * alpha * life);
        }

        // Variant 34: "confetti" - a party burst: a scatter of small coloured
        // flakes that tumble outward and flutter down under their own gravity,
        // plus a bright pop behind them. The only many-small-bits style. Own
        // palette: full rainbow (it is meant to read as celebration).
        private static void ArcaneStarConfetti(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color[] palette =
            {
                new Color(255, 96, 120), new Color(255, 190, 70), new Color(120, 240, 140),
                new Color(110, 200, 255), new Color(190, 140, 255), new Color(255, 255, 255)
            };
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float life = 1f - MathHelper.Clamp((t - 0.35f) / 0.65f, 0f, 1f);
            if (life <= 0.01f)
                return;
            const int flakes = 18;
            for (int i = 0; i < flakes; i++)
            {
                float seed = i * 2.3999632f;
                float dist = (10f + 52f * t * (0.55f + 0.45f * (i % 5) / 4f)) * scale;
                Vector2 p = c + (settle + seed).ToRotationVector2() * dist + new Vector2(0f, 26f * t * t * scale);
                float wob = 0.6f + 0.4f * (float)System.Math.Sin(seed + t * 7f);
                DrawStripe(pixel, p, seed + t * (2.2f + i % 4), 7f * scale, MathHelper.Max(1.2f, 3.6f * wob) * scale,
                    palette[i % palette.Length], 0.85f * alpha * life);
            }
            // Pop: a short hard ring plus a party-popper starburst, not a soft blob.
            DrawRing(c, (10f + 13f * t) * scale, 2.2f * scale, new Color(255, 230, 160) * (0.50f * alpha * life), 20);
            DrawSolidDisc(c, 10f * scale, Color.White, 0.50f * alpha * life);
            for (int k = 0; k < 6; k++)
            {
                Vector2 dir = (k * MathHelper.TwoPi / 6f + 0.3f).ToRotationVector2();
                DrawCapsule(c + dir * (5f * scale), c + dir * ((16f + 10f * t) * scale), 2.4f * scale,
                    palette[k % palette.Length], 0.60f * alpha * life);
            }
        }

        // Variant 35: "glitch" - corrupt data: hard rectangular blocks that snap
        // outward, each drawn three times with the cyan and magenta copies offset
        // from the white core (a chromatic-split look), over drifting scanline
        // bands and a stepped, stuttering motion. The only flat-rectangle style,
        // deliberately. Own palette: cyan / magenta / white.
        private static void ArcaneStarGlitch(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color cyan = new Color(80, 240, 255);
            Color magenta = new Color(255, 70, 190);
            Color white = new Color(255, 255, 255);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float life = 1f - MathHelper.Clamp((t - 0.20f) / 0.80f, 0f, 1f);
            if (life <= 0.01f)
                return;
            // 8 held frames of animation; StepCurve(t, 8) * 7 == floor(t * 8) for t < 1.
            float step = StepCurve(t, 8) * 7f;
            for (int i = 0; i < 12; i++)
            {
                float seed = i * 37.77f;
                float ang = settle + (float)System.Math.Sin(seed) * MathHelper.Pi;
                float dist = (6f + 46f * t) * scale * (0.40f + 0.60f * (float)System.Math.Abs(System.Math.Cos(seed * 1.7f)));
                Vector2 p = c + ang.ToRotationVector2() * dist
                    + new Vector2((float)System.Math.Sin(seed + step * 1.9f), (float)System.Math.Cos(seed * 1.3f + step)) * 4f * scale;
                float w = (5f + 12f * (float)System.Math.Abs(System.Math.Sin(seed * 2.3f))) * scale;
                float h = (3f + 7f * (float)System.Math.Abs(System.Math.Cos(seed * 3.1f))) * scale;
                float rot = step * 0.5f % MathHelper.PiOver2;
                DrawStripe(pixel, p - new Vector2(2f, 0f) * scale, rot, w, h, cyan, 0.55f * alpha * life);
                DrawStripe(pixel, p + new Vector2(2f, 0f) * scale, rot, w, h, magenta, 0.55f * alpha * life);
                DrawStripe(pixel, p, rot, w, h, white, 0.30f * alpha * life);
            }
            for (int i = 0; i < 4; i++)
                DrawStripe(pixel, c + new Vector2(0f, ((i - 1.5f) * 12f + step % 3f * 2f) * scale), 0f,
                    (30f + 10f * i) * scale, 1.6f * scale, white, 0.25f * alpha * life);
            // Central corrupt block: flat, stepped, chromatically split. A round
            // glow here is what made the "corrupt data" style read as an orb.
            float coreRot = step * 0.5f % MathHelper.PiOver2;
            // One call instead of three hand-offset copies.
            DrawChromaticSplit(c, coreRot, 19f * scale, 4f * scale, white, 0.55f * alpha * life, 1.8f * scale);
            DrawSolidQuad(c + new Vector2(-4f, -4f) * scale, c + new Vector2(4f, -4f) * scale,
                c + new Vector2(4f, 4f) * scale, c + new Vector2(-4f, 4f) * scale, white, 0.55f * alpha * life);
        }

        // Variant 36: "portal" - a gate opening: a PORTRAIT elliptical rim with a
        // thick crown across the top, a keystone, a threshold, a throat built from
        // vertical shimmer bands clipped by the ellipse's own profile, and five arms
        // spiralling into it. The old version was a symmetric glow inside a ring,
        // which is an eclipse by definition; a taller-than-wide rim plus real
        // architecture does not read that way.
        // Own palette: violet throat on electric cyan rim.
        private static void ArcaneStarPortal(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color rim = new Color(96, 226, 255);
            Color rimHot = new Color(236, 250, 255);
            Color throat = new Color(150, 96, 255);
            Color throatDeep = new Color(66, 40, 170);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float open = MathHelper.Clamp(t / 0.26f, 0f, 1f);
            open = 1f - (1f - open) * (1f - open);
            float life = 1f - MathHelper.Clamp((t - 0.55f) / 0.45f, 0f, 1f);
            if (life <= 0.01f)
                return;
            float rx = (17f + 23f * open) * scale;            // half-width
            float ry = rx * 1.34f;                            // half-height: PORTRAIT
            Vector2 ax = new Vector2((float)System.Math.Cos(settle), (float)System.Math.Sin(settle));
            Vector2 ay = new Vector2(-ax.Y, ax.X);

            // Throat: vertical bands across the minor axis, each clipped by the
            // ellipse's own half-height profile, so the inside reads as a surface
            // with depth (a shimmering gate) rather than a glowing ball.
            const int bands = 11;
            for (int i = 0; i < bands; i++)
            {
                float u = ((i + 0.5f) / bands) * 2f - 1f;
                float half = ry * (float)System.Math.Sqrt(MathHelper.Clamp(1f - u * u, 0f, 1f));
                if (half < 0.5f)
                    continue;
                float shimmer = 0.5f + 0.5f * (float)System.Math.Sin(u * 7.5f + t * 6.5f);
                Vector2 mid = c + ax * (u * rx);
                DrawCapsule(mid - ay * half, mid + ay * half, (rx / bands) * 2.2f,
                    Color.Lerp(throatDeep, throat, shimmer), 0.32f * alpha * life);
            }

            // Scanline sweeping across the throat. The rim is a fixed gate, so this
            // and the rotating crown below are what make the portal read as RUNNING
            // rather than as a painted ellipse.
            float sweep = (float)System.Math.Sin(t * 5.0f);
            float shalf = ry * (float)System.Math.Sqrt(MathHelper.Clamp(1f - sweep * sweep, 0f, 1f));
            Vector2 smid = c + ax * (sweep * rx);
            DrawCapsule(smid - ay * shalf, smid + ay * shalf, (rx / bands) * 2.4f, throat,
                0.45f * alpha * life * (1f - 0.35f * (float)System.Math.Abs(sweep)));

            // Rim: two hard elliptical bands plus a heavy crown that sweeps round.
            float crown = t * 1.1f;
            DrawEllipseBand(c, rx, ry, settle, 0f, MathHelper.TwoPi, 2.4f * scale, rim,
                0.55f * alpha * life * (0.88f + 0.12f * (float)System.Math.Sin(t * 16f)), 26);
            DrawEllipseBand(c, rx * 0.86f, ry * 0.86f, settle, 0f, MathHelper.TwoPi, 1.4f * scale, rim, 0.28f * alpha * life, 22);
            DrawEllipseBand(c, rx, ry, settle, -MathHelper.Pi * 0.90f + crown, -MathHelper.Pi * 0.10f + crown,
                5f * scale, rimHot, 0.45f * alpha * life, 14);

            // Keystone and threshold.
            Vector2 top = c - ay * ry;
            DrawSolidTriangle(top + ax * (7f * scale), top - ax * (7f * scale), top - ay * (9f * scale), rimHot, 0.60f * alpha * life);
            Vector2 bot = c + ay * ry;
            DrawCapsule(bot - ax * (rx * 0.55f), bot + ax * (rx * 0.55f), 3f * scale, rim, 0.50f * alpha * life);

            // Five arms spiralling into the throat.
            for (int i = 0; i < 5; i++)
            {
                float a0 = settle + MathHelper.TwoPi * i / 5f + t * 2.4f;
                Vector2 prev = c + a0.ToRotationVector2() * (rx * 0.82f);
                for (int s = 1; s <= 4; s++)
                {
                    float f = s / 4f;
                    Vector2 p = c + (a0 + f * 1.9f).ToRotationVector2() * MathHelper.Lerp(rx * 0.82f, rx * 0.15f, f);
                    DrawCapsule(prev, p, (2.8f * (1f - 0.55f * f)) * scale, rim, 0.42f * alpha * life);
                    prev = p;
                }
            }

            // Rim sparks, travelling outward on a loop instead of sitting still.
            for (int i = 0; i < 4; i++)
            {
                float phase = (t * 2.2f + i * 0.25f) % 1f;
                Vector2 dir = (settle + MathHelper.TwoPi * i / 4f + 0.4f).ToRotationVector2();
                Vector2 on = c + new Vector2(dir.X * rx, dir.Y * ry);
                float fade = 1f - phase;
                Vector2 root = on + dir * (phase * 14f * scale);
                DrawCapsule(root, root + dir * (9f * scale * fade), 2.4f * scale * fade, rimHot,
                    0.65f * alpha * life * fade);
            }
        }

        // ---- Pulse set (Variant37-41) ------------------------------------------
        // Five crowns built to answer "do any of the stars pulse without
        // spinning - and make them unique". Every one holds the per-stamp
        // `settle` orientation for its whole life (no angle advances with time)
        // and none of them draws an expanding ring / shockwave: the old circle
        // was replaced with dust motes and small gravity-free fluid droplets.
        // Each variant has its own point count and its own secondary motion, so
        // the five read as five different crowns. Picker contract stays "37-41
        // never spin"; a rotating beat belongs in 6-36.

        // Shared soft beat shaper: a 1-tick attack falling off over `width`.
        private static float PulseThump(float frac, float at, float width)
        {
            float d = (float)System.Math.Abs(frac - at);
            float k = 1f - d / width;
            return k <= 0f ? 0f : k * k;
        }

        // The "noticeable star": a 2*points spike crown built from soft rays
        // (long tips alternating with shorter inner spikes), a soft hub and a
        // bright core. `pulse` scales the whole crown so each variant can
        // breathe/snap it; the orientation (`settle`) never changes.
        private static void DrawStarCrown(Vector2 c, float scale, float settle, int points,
            float outer, float inner, float pulse, Color rayCol, Color innerCol, Color coreCol, float alpha)
        {
            int spikes = points * 2;
            for (int i = 0; i < spikes; i++)
            {
                bool tip = (i & 1) == 0;
                float ang = settle + MathHelper.TwoPi * i / spikes;
                float len = (tip ? outer : inner) * (1f + pulse * (tip ? 1f : 0.55f)) * scale;
                float wid = (tip ? 4.4f : 3.2f) * scale * (1f + 0.12f * pulse);
                DrawSoftRay(c, ang, len, wid, tip ? rayCol : innerCol, (tip ? 0.60f : 0.34f) * alpha);
            }
            float hub = outer * 0.30f * scale * (1f + pulse);
            DrawSoftRay(c, 0f, hub, hub, rayCol, 0.26f * alpha);
            DrawSoftRay(c, 0f, outer * 0.15f * scale, outer * 0.15f * scale, coreCol, 0.80f * alpha);
        }

        // Dust specks: fixed bearings, per-mote pulse, no gravity, no ring.
        private static void DrawMoteCloud(Vector2 c, float scale, float settle, int count, float radius,
            float spread, float phase, Color color, float alpha, float size)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = settle + MathHelper.TwoPi * i / count;
                float bl = 0.5f + 0.5f * (float)System.Math.Sin(phase + i * 2.1f);
                Vector2 p = c + ang.ToRotationVector2() * ((radius + spread * bl) * scale);
                float s = size * scale * (0.55f + 0.9f * bl);
                DrawSoftRay(p, 0f, s, s, color, 0.55f * alpha * (0.35f + 0.65f * bl));
            }
        }

        // Non-gravity mini metaballs: a tinted halo, a bright core and a solid
        // bead on fixed bearings, drifting with the beat. Never a circle.
        private static void DrawFluidDrops(Vector2 c, float scale, float settle, int count, float radius,
            float drift, float phase, float size, Color tint, Color core, float alpha)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = settle + MathHelper.TwoPi * (i + 0.5f) / count;
                float bl = 0.5f + 0.5f * (float)System.Math.Sin(phase + i * 1.7f);
                Vector2 p = c + ang.ToRotationVector2() * ((radius + drift * bl) * scale);
                float s = size * scale * (0.7f + 0.6f * bl);
                DrawSoftRay(p, 0f, s * 2.3f, s * 2.3f, tint, 0.20f * alpha * (0.4f + 0.6f * bl));
                DrawSoftRay(p, 0f, s * 1.2f, s * 1.2f, core, 0.42f * alpha * bl);
                DrawSolidDisc(p, s * 0.85f, core, 0.30f * alpha * bl);
            }
        }

        // Variant 37: "heartbeat" - bold 4-point star (8 spikes) with a
        // double-beat thump. On each beat the crown swells and a burst of
        // gravity-free droplets puffs outward (no shockwave circle). Rose/plum.
        private static void ArcaneStarPulseHeart(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color rose = new Color(255, 96, 138);
            Color deep = new Color(158, 32, 74);
            Color core = new Color(255, 238, 246);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float cycle = t * 3f;
            float frac = cycle - (float)System.Math.Floor(cycle);
            float beat = MathHelper.Clamp(PulseThump(frac, 0.08f, 0.17f) + 0.72f * PulseThump(frac, 0.34f, 0.20f), 0f, 1f);

            DrawStarCrown(c, scale, settle, 4, 58f, 26f, 0.07f + 0.24f * beat, rose, deep, core, alpha);
            DrawFluidDrops(c, scale, settle, 6, 24f, 16f * beat, frac * MathHelper.TwoPi * 2f, 4.6f,
                deep, rose, alpha * (0.45f + 0.55f * beat));
            DrawSoftRay(c, 0f, (11f + 7f * beat) * scale, (11f + 7f * beat) * scale, core, 0.8f * alpha);
        }

        // Variant 38: "breath" - 6-point star (12 spikes) that inhales and
        // exhales as one whole crown; motes lift on the crest. No ring. Sea-mint.
        private static void ArcaneStarPulseBreath(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color mint = new Color(138, 255, 216);
            Color deep = new Color(26, 120, 106);
            Color core = new Color(236, 255, 250);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float breath = 0.5f - 0.5f * (float)System.Math.Cos(t * MathHelper.TwoPi);   // 0 -> 1 -> 0

            DrawStarCrown(c, scale, settle, 6, 50f, 22f, 0.02f + 0.18f * breath, mint, deep, core, alpha);
            // Lifting motes (non-gravity), strongest on the exhale.
            Vector2 lifted = c - Vector2.UnitY * (10f + 14f * breath) * scale;
            DrawMoteCloud(lifted, scale, settle, 8, 18f, 16f * breath, t * MathHelper.TwoPi, core,
                alpha * (0.45f + 0.55f * breath), 3.0f);
            DrawSoftRay(lifted, MathHelper.PiOver2, 7f * scale, 4.5f * scale, mint, 0.35f * alpha * breath);
            DrawSoftRay(c, 0f, (9f + 5f * breath) * scale, (9f + 5f * breath) * scale, core, 0.8f * alpha);
        }

        // Variant 39: "strobe" - 8-point star (16 spikes) snapping between two
        // sizes on a hard electric gate; static studs blink at the tips and
        // micro-dust jitters. No ring. Photosensitivity softens the gate to a
        // triangle (no full-black frames).
        private static void ArcaneStarPulseStrobe(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color blue = new Color(140, 190, 255);
            Color ice = new Color(220, 240, 255);
            Color deep = new Color(60, 80, 190);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float f = t * 6f;
            float seg = f - (float)System.Math.Floor(f);
            var cfg = GetYoyoConfig();
            bool soft = cfg != null && cfg.Visuals.Photosensitivity;
            float strobe = soft
                ? 0.6f + 0.4f * (1f - (float)System.Math.Abs(seg * 2f - 1f))   // gentle triangle
                : (seg < 0.30f ? 1f : 0.35f);                                   // hard gate
            float strobeAlt = 1f - strobe;
            float snap = MathHelper.Clamp((strobe - 0.35f) / 0.65f, 0f, 1f);    // 0 = dim, 1 = flash

            DrawStarCrown(c, scale, settle, 8, 54f, 20f, 0.30f * snap, blue, deep, ice, alpha);
            // Blinking studs + micro-dust on fixed bearings (no circle).
            DrawMoteCloud(c, scale, settle + MathHelper.PiOver4, 8, 32f, 5f * strobeAlt, t * 26f, ice,
                0.75f * alpha, 3.6f);
        }

        // Variant 40: "drift" - 5-point star (10 spikes) firing three staggered
        // outward waves of dust and gravity-free droplets. The waves chase each
        // other so it still reads as a ripple, but nothing draws a circle.
        private static void ArcaneStarPulseRipple(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color pink = new Color(228, 140, 255);
            Color deep = new Color(96, 40, 150);
            Color white = new Color(246, 236, 255);
            float t = MathHelper.Clamp(progress, 0f, 1f);

            DrawStarCrown(c, scale, settle, 5, 54f, 24f,
                0.04f + 0.10f * (float)System.Math.Sin(t * MathHelper.TwoPi * 2f), pink, deep, white, alpha);
            for (int i = 0; i < 3; i++)
            {
                float wp = t * 2.4f + i * (1f / 3f);
                wp -= (float)System.Math.Floor(wp);
                float fade = (1f - wp) * (1f - wp);
                float radius = 14f + 50f * wp;
                DrawMoteCloud(c, scale, settle + wp * 1.3f, 10, radius, 5f, wp * MathHelper.TwoPi, pink,
                    0.6f * alpha * fade, 3.4f);
                DrawFluidDrops(c, scale, settle + wp * 1.3f, 4, radius, 3f, wp * MathHelper.TwoPi, 3.4f,
                    deep, white, 0.5f * alpha * fade);
            }
            DrawSoftRay(c, 0f, 11f * scale, 11f * scale, white, 0.8f * alpha);
        }

        // Variant 41: "bloom" - 8-point petal star (16 spikes) breathing in
        // counter-phase pairs, with pollen motes rising in a slow golden-angle
        // column (non-gravity). No ring. Leaf/gold palette.
        private static void ArcaneStarPulseBloom(Vector2 c, float scale, float alpha, float progress, float settle)
        {
            Color leaf = new Color(186, 255, 128);
            Color gold = new Color(255, 214, 110);
            Color core = new Color(250, 255, 236);
            float t = MathHelper.Clamp(progress, 0f, 1f);
            float pairA = 0.5f + 0.5f * (float)System.Math.Sin(t * MathHelper.TwoPi * 1.5f);

            DrawStarCrown(c, scale, settle, 8, 52f, 28f, 0.04f + 0.14f * pairA, leaf, gold, core, alpha);
            // Rising pollen (non-gravity): golden-angle bearings, no ring.
            for (int i = 0; i < 10; i++)
            {
                float seed = i * 2.399f;
                float lift = t * 1.15f + i * 0.1f;
                lift -= (float)System.Math.Floor(lift);
                Vector2 dir = (settle + seed).ToRotationVector2();
                Vector2 p = c + dir * (14f + 18f * (0.5f + 0.5f * (float)System.Math.Sin(seed * 3f))) * scale
                    - Vector2.UnitY * (lift * 34f) * scale;
                float bl = 0.5f + 0.5f * (float)System.Math.Sin(seed * 5f + t * 9f);
                float psz = 3.2f * scale * (0.6f + 0.7f * bl);
                DrawSoftRay(p, 0f, psz, psz, i % 3 == 0 ? gold : leaf, 0.5f * alpha * bl);
            }
            DrawSoftRay(c, 0f, (9f + 4f * pairA) * scale, (9f + 4f * pairA) * scale, core, 0.8f * alpha);
        }

        // Dispatches the configured arcane star style. World-space; call from a
        // PreDraw/PostDraw (the batch must already be open). `progress` is 0 when
        // the flare is stamped and 1 at the end of its life.
        //
        // Motion envelope (this is what replaced the old spin): the flare SNAPS in
        // over the first ~22% of its life, then holds size while each style pulses
        // in its own way (telescoping points, flickering axes, needles unfurling,
        // a ring expanding, a chromatic split breathing, a slash sweeping, a
        // funnel tightening...). `baseRotation` is the per-stamp orientation; a
        // small settle offset unwinds during the pop and then stops, so nothing
        // rotates continuously.
        //
        // Render path - deliberately split, because the two halves have different
        // contracts:
        //   * Variants 1-5 are the ORIGINAL soft-flare stars and still draw in the
        //     caller's alpha-blended batch, exactly the brightness they always
        //     had. Their only change this pass is the extra stamp particles
        //     (SpawnArcaneStarBurst).
        //   * Variants 6-41 are the wildcard signatures and the pulse set; they
        //     run through one additive window (the same pattern as DrawBeamV2),
        //     so every lens becomes emitted light with real falloff. The pooled
        //     particles are flushed first so the window cannot recolour them.
        // Both halves share a per-frame draw budget that degrades gracefully to a
        // compact core if a ritual storm stamps more crowns than a frame affords.
        private static long arcaneStarFrame = -1;
        private static int arcaneStarBudget;
        private const int MaxArcaneStarsPerFrame = 24;

        // Colour identity per style. The stamp burst and any future attack site
        // can ask for "the sparks that match this look".
        public static Color ArcaneStarAccent(Configuration.ArcaneStarStyle style)
        {
            switch (style)
            {
                case Configuration.ArcaneStarStyle.Variant2: return new Color(255, 158, 72);
                case Configuration.ArcaneStarStyle.Variant3: return new Color(214, 74, 240);
                case Configuration.ArcaneStarStyle.Variant4: return new Color(255, 178, 84);
                case Configuration.ArcaneStarStyle.Variant5: return new Color(255, 96, 214);
                case Configuration.ArcaneStarStyle.Variant6: return new Color(150, 236, 255);
                case Configuration.ArcaneStarStyle.Variant7: return new Color(214, 176, 120);
                case Configuration.ArcaneStarStyle.Variant8: return new Color(176, 208, 255);
                case Configuration.ArcaneStarStyle.Variant9: return new Color(96, 226, 235);
                case Configuration.ArcaneStarStyle.Variant10: return new Color(150, 220, 255);
                case Configuration.ArcaneStarStyle.Variant11: return new Color(255, 150, 200);
                case Configuration.ArcaneStarStyle.Variant12: return new Color(110, 160, 255);
                case Configuration.ArcaneStarStyle.Variant13: return new Color(110, 235, 170);
                case Configuration.ArcaneStarStyle.Variant14: return new Color(90, 240, 170);
                case Configuration.ArcaneStarStyle.Variant15: return new Color(255, 214, 120);
                case Configuration.ArcaneStarStyle.Variant16: return new Color(222, 236, 255);
                case Configuration.ArcaneStarStyle.Variant17: return new Color(255, 224, 150);
                case Configuration.ArcaneStarStyle.Variant18: return new Color(120, 235, 255);
                case Configuration.ArcaneStarStyle.Variant19: return new Color(196, 150, 255);
                case Configuration.ArcaneStarStyle.Variant20: return new Color(150, 210, 255);
                case Configuration.ArcaneStarStyle.Variant21: return new Color(255, 148, 58);
                case Configuration.ArcaneStarStyle.Variant22: return new Color(120, 170, 255);
                case Configuration.ArcaneStarStyle.Variant23: return new Color(120, 220, 130);
                case Configuration.ArcaneStarStyle.Variant24: return new Color(168, 112, 240);
                case Configuration.ArcaneStarStyle.Variant25: return new Color(255, 120, 220);
                case Configuration.ArcaneStarStyle.Variant26: return new Color(255, 226, 140);
                case Configuration.ArcaneStarStyle.Variant27: return new Color(110, 255, 190);
                case Configuration.ArcaneStarStyle.Variant28: return new Color(222, 232, 246);
                case Configuration.ArcaneStarStyle.Variant29: return new Color(240, 238, 226);
                case Configuration.ArcaneStarStyle.Variant30: return new Color(255, 176, 74);
                case Configuration.ArcaneStarStyle.Variant31: return new Color(190, 196, 210);
                case Configuration.ArcaneStarStyle.Variant32: return new Color(92, 140, 255);
                case Configuration.ArcaneStarStyle.Variant33: return new Color(255, 92, 200);
                case Configuration.ArcaneStarStyle.Variant34: return new Color(255, 190, 70);
                case Configuration.ArcaneStarStyle.Variant35: return new Color(80, 240, 255);
                case Configuration.ArcaneStarStyle.Variant36: return new Color(168, 110, 255);
                case Configuration.ArcaneStarStyle.Variant37: return new Color(255, 96, 138);
                case Configuration.ArcaneStarStyle.Variant38: return new Color(138, 255, 216);
                case Configuration.ArcaneStarStyle.Variant39: return new Color(160, 200, 255);
                case Configuration.ArcaneStarStyle.Variant40: return new Color(228, 140, 255);
                case Configuration.ArcaneStarStyle.Variant41: return new Color(196, 255, 140);
                default: return new Color(186, 132, 255);
            }
        }

        // The "more particles" half of the star pass: spawned ONCE per hit from
        // gameplay code (never from a PreDraw), matched to the style's palette, so
        // the crown itself keeps exactly the shape it always had. All of these are
        // pooled and no-op on a dedicated server.
        public static void SpawnArcaneStarBurst(Vector2 worldPos, Configuration.ArcaneStarStyle style, float scale = 1f)
        {
            if (style == Configuration.ArcaneStarStyle.Off)
                return;
            Color accent = ArcaneStarAccent(style);
            Color hot = Color.Lerp(accent, Color.White, 0.75f);
            bool wildcard = style >= Configuration.ArcaneStarStyle.Variant6;
            int sparks = wildcard ? 9 : 6;
            for (int i = 0; i < sparks; i++)
            {
                float a = MathHelper.TwoPi * i / sparks + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 dir = a.ToRotationVector2();
                YoyoParticles.SpawnSpark(worldPos + dir * (4f * scale), dir * Main.rand.NextFloat(2.4f, 4.6f) * scale,
                    i % 3 == 0 ? hot : accent, 1f, wildcard ? 24 : 20, 0.94f);
            }
            YoyoParticles.SpawnBurst(worldPos, 4, 2.4f * scale, accent, 0.9f, 18);
            for (int i = 0; i < 3; i++)
                YoyoParticles.SpawnSpark(worldPos + Main.rand.NextVector2Circular(5f, 5f) * scale,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.6f, -0.6f)),
                    accent, 0.8f, 34, 0.97f, -0.01f);
            // Pulse set (37-41): the crown has no ring/shockwave, so the stamp
            // must not imply one either. Each variant gets its OWN small puff
            // instead of one generic layer: dust for most, and - only on 37 and
            // 41 - a couple of TINY gravity-free fluid blobs. Scale 0.18-0.20
            // gives ~8-9 px blobs (diam = 46 * Scale) instead of the default
            // ~46 px; noGravity keeps them hanging instead of sinking.
            // MetaballSystem still honours Visuals.Metaballs / MetaballAmount,
            // and LowQuality skips the blobs entirely.
            if (style >= Configuration.ArcaneStarStyle.Variant37)
            {
                bool low = YoyoParticles.LowQuality();
                switch (style)
                {
                    case Configuration.ArcaneStarStyle.Variant37: // heartbeat: droplet puff
                        for (int i = 0; i < 4; i++)
                        {
                            float a = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(-0.3f, 0.3f);
                            Vector2 dir = a.ToRotationVector2();
                            YoyoParticles.SpawnSpark(worldPos + dir * (5f * scale), dir * Main.rand.NextFloat(0.7f, 1.5f) * scale,
                                accent, 0.7f, 20, 0.9f);
                        }
                        if (!low)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                Vector2 dir = Main.rand.NextVector2Unit();
                                Content.Systems.MetaballSystem.SpawnMetaball(
                                    worldPos + dir * (6f * scale), dir * Main.rand.NextFloat(0.2f, 0.6f),
                                    0.20f, 0.45f, 30, accent, noGravity: true);
                            }
                        }
                        break;
                    case Configuration.ArcaneStarStyle.Variant38: // breath: motes lift
                        for (int i = 0; i < 6; i++)
                        {
                            YoyoParticles.SpawnSpark(worldPos + Main.rand.NextVector2Circular(6f, 4f) * scale,
                                -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.3f) + Main.rand.NextVector2Circular(0.3f, 0.3f),
                                accent, 0.6f, 26, 0.92f);
                        }
                        break;
                    case Configuration.ArcaneStarStyle.Variant39: // strobe: hard ticks
                        for (int i = 0; i < 8; i++)
                        {
                            float a = MathHelper.TwoPi * i / 8f;
                            YoyoParticles.SpawnSpark(worldPos + a.ToRotationVector2() * (4f * scale),
                                a.ToRotationVector2() * Main.rand.NextFloat(1.6f, 2.6f) * scale,
                                accent, 0.8f, 16, 0.95f);
                        }
                        break;
                    case Configuration.ArcaneStarStyle.Variant40: // drift: outward dust
                        for (int i = 0; i < 10; i++)
                        {
                            float a = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(-0.2f, 0.2f);
                            YoyoParticles.SpawnSpark(worldPos + a.ToRotationVector2() * (3f * scale),
                                a.ToRotationVector2() * Main.rand.NextFloat(0.6f, 1.8f) * scale,
                                accent, 0.7f, 24, 0.9f);
                        }
                        break;
                    default: // 41 bloom: rising pollen + two tiny floating blobs
                        for (int i = 0; i < 6; i++)
                        {
                            YoyoParticles.SpawnSpark(worldPos + Main.rand.NextVector2Circular(5f, 5f) * scale,
                                -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.1f), accent, 0.6f, 28, 0.9f);
                        }
                        if (!low)
                        {
                            for (int i = 0; i < 2; i++)
                            {
                                Vector2 dir = Main.rand.NextVector2Unit();
                                Content.Systems.MetaballSystem.SpawnMetaball(
                                    worldPos + dir * (5f * scale), dir * Main.rand.NextFloat(0.15f, 0.45f),
                                    0.18f, 0.42f, 34, accent, noGravity: true);
                            }
                        }
                        break;
                }
            }
            Lighting.AddLight(worldPos, accent.R / 255f * 0.5f, accent.G / 255f * 0.5f, accent.B / 255f * 0.5f);
        }

        public static void DrawArcaneStar(Vector2 worldCenter, float scale, float alpha, float progress, Configuration.ArcaneStarStyle style, float baseRotation = 0f)
        {
            if (style == Configuration.ArcaneStarStyle.Off)
                return;
            float p = MathHelper.Clamp(progress, 0f, 1f);
            float grow = MathHelper.Clamp(p / 0.22f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float pop = 0.78f + 0.22f * grow;
            float settle = baseRotation + (1f - grow) * 0.30f;
            float a = alpha * (0.45f + 0.55f * grow);

            // Per-frame budget: the 2s ritual cadence can stamp a lot of crowns.
            long frame = (long)Main.GameUpdateCount;
            if (frame != arcaneStarFrame)
            {
                arcaneStarFrame = frame;
                arcaneStarBudget = YoyoParticles.LowQuality() ? 10 : MaxArcaneStarsPerFrame;
            }
            bool full = arcaneStarBudget > 0;
            if (full)
                arcaneStarBudget--;

            // Variants 1-5: the original stars, in the caller's batch, unmodified.
            if (style < Configuration.ArcaneStarStyle.Variant6 || !EffectsEnabled())
            {
                if (full)
                    DrawArcaneStarBody(worldCenter, scale * pop, a, p, style, settle);
                else
                    DrawArcaneStarFallback(worldCenter, scale * pop, a);
                return;
            }
            // Variants 6+ (wildcards and the 37-41 pulse set): flush pooled
            // particles first (same-frame guard makes this free if a DrawGlow
            // already ran), then draw the crown additively.
            YoyoParticles.Draw();
            AdditiveDraw(() =>
            {
                if (full)
                    DrawArcaneStarBody(worldCenter, scale * pop, a, p, style, settle);
                else
                    DrawArcaneStarFallback(worldCenter, scale * pop, a);
            });
        }

        // Budget-exhausted degenerate: two lenses, still readable as an impact.
        private static void DrawArcaneStarFallback(Vector2 c, float scale, float alpha)
        {
            DrawSoftRay(c, 0f, 26f * scale, 26f * scale, new Color(186, 132, 255), 0.30f * alpha);
            DrawSoftRay(c, 0f, 13f * scale, 13f * scale, Color.White, 0.80f * alpha);
        }

        private static void DrawArcaneStarBody(Vector2 worldCenter, float scale, float alpha, float progress, Configuration.ArcaneStarStyle style, float settle)
        {
            switch (style)
            {
                case Configuration.ArcaneStarStyle.Variant2:
                    ArcaneStarSolar(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant3:
                    ArcaneStarVoid(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant4:
                    ArcaneStarNova(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant5:
                    ArcaneStarPrism(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant6:
                    ArcaneStarCrescent(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant7:
                    ArcaneStarSlam(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant8:
                    ArcaneStarLance(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant9:
                    ArcaneStarVortex(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant10:
                    ArcaneStarShatter(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant11:
                    ArcaneStarBloom(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant12:
                    ArcaneStarArc(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant13:
                    ArcaneStarSigil(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant14:
                    ArcaneStarRay(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant15:
                    ArcaneStarHalo(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant16:
                    ArcaneStarSlash(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant17:
                    ArcaneStarCrosscut(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant18:
                    ArcaneStarRipple(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant19:
                    ArcaneStarQuills(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant20:
                    ArcaneStarComet(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant21:
                    ArcaneStarEruption(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant22:
                    ArcaneStarTempest(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant23:
                    ArcaneStarBramble(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant24:
                    ArcaneStarNebula(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant25:
                    ArcaneStarTear(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant26:
                    ArcaneStarSalvo(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant27:
                    ArcaneStarAurora(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant28:
                    ArcaneStarWeb(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant29:
                    ArcaneStarFangs(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant30:
                    ArcaneStarEye(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant31:
                    ArcaneStarSawblade(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant32:
                    ArcaneStarMagnet(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant33:
                    ArcaneStarSoundwave(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant34:
                    ArcaneStarConfetti(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant35:
                    ArcaneStarGlitch(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant36:
                    ArcaneStarPortal(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant37:
                    ArcaneStarPulseHeart(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant38:
                    ArcaneStarPulseBreath(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant39:
                    ArcaneStarPulseStrobe(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant40:
                    ArcaneStarPulseRipple(worldCenter, scale, alpha, progress, settle);
                    break;
                case Configuration.ArcaneStarStyle.Variant41:
                    ArcaneStarPulseBloom(worldCenter, scale, alpha, progress, settle);
                    break;
                default:
                    ArcaneStarLunar(worldCenter, scale, alpha, progress, settle);
                    break;
            }
        }

        // Hard-geometry ring (segmented thick line), radius/width in pixels.
        public static void DrawRing(Vector2 worldCenter, float radius, float width, Color color, int segments = 40)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 center = worldCenter - Main.screenPosition;
            for (int i = 0; i < segments; i++)
            {
                float a0 = MathHelper.TwoPi * i / segments;
                float a1 = MathHelper.TwoPi * (i + 1) / segments;
                Vector2 p0 = center + a0.ToRotationVector2() * radius;
                Vector2 p1 = center + a1.ToRotationVector2() * radius;
                DrawLine(p0, p1, width, color);
            }
        }

        // Thick screen-space line between two SCREEN positions.
        public static void DrawLine(Vector2 screenStart, Vector2 screenEnd, float width, Color color)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 diff = screenEnd - screenStart;
            if (diff.LengthSquared() < 0.01f)
                return;
            Main.spriteBatch.Draw(pixel, (screenStart + screenEnd) * 0.5f, new Rectangle(0, 0, 1, 1), color,
                diff.ToRotation(), new Vector2(0.5f, 0.5f), new Vector2(diff.Length() + 1f, width), SpriteEffects.None, 0f);
        }

        // Dummy-compatible targeting (in-mod precedent: YoyoTrickCloneProjectile
        // deliberately avoids CanBeChasedBy so Target Dummies can be targeted).
        // Primary: real chaseable enemies. Fallback: immortal test dummies
        // (vanilla Target Dummy is immortal, which CanBeChasedBy excludes).
        public static bool IsTestDummy(NPC npc)
        {
            // Anything hostile and damageable but invisible to normal targeting
            // (immortal dummies, unchaseable customs) counts as a test dummy.
            return npc.active && (npc.immortal || !npc.chaseable) && !npc.friendly
                && !npc.townNPC && npc.lifeMax > 5 && !npc.dontTakeDamage;
        }

        // State-change invalidation (Vexel B3): call when an attack begins/ends so
        // a dash does not stretch the ribbon into a straight smear (the next
        // RibbonPush then restarts the ribbon from the new position).
        public static void RibbonClear(List<Vector2> trail)
        {
            trail.Clear();
        }

        // Motion ribbon trail (spacing/teleport-safe/fading).
        //
        // Rewritten again for "small disconnected trails when barely moving or
        // idle, proper ones only when fast". Root cause: every earlier version
        // decided whether to shed/retract the tail from a PER-TICK speed test.
        // Whenever the body crept slower than that test - or hovered with idle
        // jitter - the tail was eaten faster than the spacing-based appends
        // could feed it, so the ribbon collapsed into a couple of short
        // fragments that blinked in and out. That is the whole symptom.
        //
        // Current contract (no speed-based shedding anywhere):
        //  * trail[0..n-2] are frozen history points, roughly `spacing` apart;
        //    trail[n-1] is a LIVE HEAD that follows the body EVERY tick, so the
        //    ribbon never steps in spacing-sized jumps.
        //  * Growing history is only capped at `max` points - nothing is ever
        //    shed for moving slowly, so the ribbon is always one continuous
        //    strip of about spacing * (max - 1) px of path at any speed.
        //  * The ONLY shedding is for a body that is genuinely parked (moved
        //    under 0.5 px this tick): one retired point every 3rd tick, so a
        //    stopped yoyo's ribbon fades out instead of freezing in the air.
        //    Because the gate is "not moving at all" and not a speed test, a
        //    slow-but-moving trail still cannot be starved.
        //  * A jump over 250 px restarts the trail instead of smearing.
        // The dev yoyo deliberately keeps the original quantised trail - see
        // RibbonPushLegacy.
        public static void RibbonPush(List<Vector2> trail, Vector2 worldPos, float spacing = 8f, int max = 14, bool reset = false)
        {
            if (reset || trail.Count == 0)
            {
                trail.Clear();
                trail.Add(worldPos);
                return;
            }

            int last = trail.Count - 1;
            float moved = Vector2.Distance(worldPos, trail[last]);
            if (moved > 250f) // teleported: restart instead of streaking
            {
                trail.Clear();
                trail.Add(worldPos);
                return;
            }

            if (trail.Count == 1)
            {
                // A lone point cannot be a live head - measuring it against the
                // body it is following would never accumulate, so the ribbon
                // could never grow. Keep it frozen until the body has travelled
                // a full spacing, then history begins.
                if (moved >= spacing)
                    trail.Add(worldPos);
                return;
            }

            // Live head: follows the body every tick.
            trail[last] = worldPos;
            if (Vector2.DistanceSquared(trail[last], trail[last - 1]) >= spacing * spacing)
            {
                // The head has drifted a full spacing from the last frozen
                // point: freeze a copy of it as history and start a fresh live
                // head on the body. Only the count is capped - nothing is shed
                // early, which is what keeps slow trails full and continuous.
                trail.Add(worldPos);
                while (trail.Count > max)
                    trail.RemoveAt(0);
            }
            else if (moved < 0.5f)
            {
                // Body is genuinely parked this tick (not creeping). Retire the
                // oldest point every few ticks so a stopped yoyo's ribbon fades
                // away like the dev yoyo's instead of freezing in the air. The
                // gate is "did not move at all", NOT a speed test - that is the
                // difference between this and the starvation bug above.
                if (Main.GameUpdateCount % 3 == 0 && trail.Count > 1)
                    trail.RemoveAt(0);
            }
        }

        // The pre-rewrite RibbonPush, kept verbatim for the dev yoyo (user
        // request: "keep dev yoyo trail"). Quantised head + idle shedding and
        // all - do NOT reuse this for the family yoyos.
        public static void RibbonPushLegacy(List<Vector2> trail, Vector2 worldPos, float spacing = 8f, int max = 14, bool reset = false)
        {
            if (reset)
            {
                trail.Clear();
                trail.Add(worldPos);
                return;
            }
            if (trail.Count == 0)
            {
                trail.Add(worldPos);
                return;
            }
            if (Vector2.DistanceSquared(worldPos, trail[trail.Count - 1]) > 62500f)
            {
                trail.Clear();
                trail.Add(worldPos);
                return;
            }
            if (trail.Count == 1)
            {
                if (Vector2.DistanceSquared(worldPos, trail[0]) >= spacing * spacing)
                    trail.Add(worldPos);
                return;
            }
            if (Vector2.DistanceSquared(worldPos, trail[trail.Count - 2]) >= spacing * spacing)
            {
                trail.Add(worldPos);
                while (trail.Count > max)
                    trail.RemoveAt(0);
            }
            else
            {
                bool moving = Vector2.DistanceSquared(worldPos, trail[trail.Count - 1]) > 0.25f;
                if (moving)
                    trail[trail.Count - 1] = worldPos;
                if (Main.GameUpdateCount % 3 == 0)
                {
                    if (trail.Count > 2)
                        trail.RemoveAt(0);
                    else if (!moving)
                        trail.RemoveAt(0);
                }
            }
        }

         public static void RibbonDraw(List<Vector2> trail, Color edge, Color core, float maxWidth = 8f, int type = -1)
         {
             if (trail.Count < 2)
                 return;
             Texture2D pixel = TextureAssets.MagicPixel.Value;
             int n = trail.Count;
             bool queueCores = EffectsEnabled();
             for (int i = 0; i < n - 1; i++)
             {
                 Vector2 p0 = trail[Math.Max(0, i - 1)];
                 Vector2 p1 = trail[i];
                 Vector2 p2 = trail[i + 1];
                 Vector2 p3 = trail[Math.Min(n - 1, i + 2)];
                 const int steps = 4;
                 for (int s = 0; s < steps; s++)
                 {
                     float u = (float)s / steps;
                     float u2 = u * u;
                     float u3 = u2 * u;
                     Vector2 a = 0.5f * ((2f * p1) + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
                     float uNext = (float)(s + 1) / steps;
                     float u2n = uNext * uNext;
                     float u3n = u2n * uNext;
                     Vector2 b = 0.5f * ((2f * p1) + (-p0 + p2) * uNext + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2n + (-p0 + 3f * p1 - 3f * p2 + p3) * u3n);
                     Vector2 aS = a - Main.screenPosition;
                     Vector2 bS = b - Main.screenPosition;
                     Vector2 diff = bS - aS;
                     float len = diff.Length();
                     if (len <= 0.01f)
                         continue;
                     // Stable gradient: distance behind the moving head over a
                     // fixed span, so the fade does not re-scale when the point
                     // count breathes (that re-scaling was the ribbon flicker).
                     float t = 1f - Vector2.Distance((a + b) * 0.5f, trail[n - 1]) / MathHelper.Max(24f, maxWidth * 14f);
                     if (t < 0f) t = 0f;
                     if (t > 1f) t = 1f;
                     float alpha = 0.6f * t * t;
                     float w = maxWidth * (0.15f + 0.85f * (float)Math.Sqrt(t));
                     Vector2 mid = (aS + bS) * 0.5f;
                     float rot = diff.ToRotation();
                     Main.spriteBatch.Draw(pixel, mid, new Rectangle(0, 0, 1, 1), edge * alpha, rot,
                         new Vector2(0.5f, 0.5f), new Vector2(len + 1f, w * 1.6f), SpriteEffects.None, 0f);
                     Main.spriteBatch.Draw(pixel, mid, new Rectangle(0, 0, 1, 1), core * alpha, rot,
                         new Vector2(0.5f, 0.5f), new Vector2(len + 1f, w), SpriteEffects.None, 0f);
                     if (queueCores)
                     {
                         float hotK = 0.5f * alpha;
                         if (hotK > 0.01f && trailCores.Count < MaxTrailCores)
                             trailCores.Add(new TrailCore
                             {
                                 Mid = mid,
                                 Rot = rot,
                                 Len = len,
                                 Width = Math.Max(2f, w * 1.1f),
                                 Color = core,
                                 Alpha = hotK
                             });
                     }
                 }
             }
         }

        // ---- Soft streak ribbon (the family yoyo trails) -----------------------
        // The old ribbon was solid 1x1-pixel rectangles: hard edges and one
        // alpha step per sub-segment, which reads as "blocky" and "fades block
        // by block". This resamples the Catmull centreline to ~1.4 px and draws
        // one soft-edged quad per resampled segment:
        //   * alpha and width come from the TRUE arc length behind the head,
        //     with alpha using a gentler ^1.4 ramp and averaged over the two
        //     ends, so neighbouring quads differ by ~3/255 per step - the fade
        //     reads as continuous instead of as a ladder of bands;
        //   * the cross-section is a soft lens, so there is no hard silhouette;
        //   * the body colour lerps from the family edge colour at the tail to
        //     its core colour at the head (heat gradient), the width carries a
        //     small flare at the head, and one additive bloom is queued at the
        //     head - the ribbon reads as pouring out of the yoyo;
        //   * each cross-line is widened by its miter factor (1/cos of half the
        //     local turn) so the quads meeting at a point stay joined: no wedge
        //     gap on the outside of a corner, and no lengthwise rectangle
        //     extension stacking into brighter patches.
        // Every family trail and both dev yoyo trails use this renderer; the
        // original hard ribbon survives only as the SoftTrails=false fallback.
        private const int SoftTrailH = 64;
        private const int SoftMaxPts = 320;
        private static Texture2D softTrailTex;
        private static bool softTrailTried;
        private static readonly Vector2[] softPts = new Vector2[SoftMaxPts];
        private static readonly float[] softDist = new float[SoftMaxPts];
        private static readonly float[] softFade = new float[SoftMaxPts];
        private static readonly float[] softWidth = new float[SoftMaxPts];
        private static readonly float[] softHeat = new float[SoftMaxPts];

        // 1 x SoftTrailH premultiplied white with a lens falloff across the
        // ribbon: 1 at the centre, 0 at the edges. No longitudinal component -
        // the length fade is per-quad, which is what keeps it smooth without
        // sampling sub-rectangles (whose origin convention differs between
        // renderers). The exponent is deliberately gentle (1.25) so the edge
        // feathers over several pixels instead of stepping off - a soft skirt
        // is what stops neighbouring quads from reading as separate blocks.
        private static Texture2D GetSoftTrailTexture()
        {
            if (!softTrailTried)
            {
                softTrailTried = true;
                try
                {
                    var data = new Color[SoftTrailH];
                    for (int y = 0; y < SoftTrailH; y++)
                    {
                        float w = Math.Abs((y + 0.5f) / SoftTrailH * 2f - 1f); // 0 centre, 1 edge
                        float a = (float)Math.Pow(MathHelper.Clamp(1f - w * w, 0f, 1f), 1.25f);
                        byte b = (byte)MathHelper.Clamp(a * 255f, 0f, 255f);
                        data[y] = new Color(b, b, b, b); // premultiplied white
                    }
                    softTrailTex = new Texture2D(Main.graphics.GraphicsDevice, 1, SoftTrailH);
                    softTrailTex.SetData(data);
                }
                catch { softTrailTex = null; }
            }
            return softTrailTex;
        }

        private static Vector2 CatmullPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float u)
        {
            float u2 = u * u;
            float u3 = u2 * u;
            return 0.5f * ((2f * p1) + (-p0 + p2) * u
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        private static Vector2 UnitDir(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float l = d.Length();
            return l > 0.001f ? d / l : Vector2.Zero;
        }

        // 1/cos(half the turn) at a resampled point: how much the cross-line
        // there has to grow so the two quads meeting at that point stay joined.
        // Clamped so a near-reversal cannot launch it to infinity.
        private static float MiterScale(int k, int m)
        {
            if (k <= 0 || k >= m - 1)
                return 1f;
            Vector2 dIn = UnitDir(softPts[k - 1], softPts[k]);
            Vector2 dOut = UnitDir(softPts[k], softPts[k + 1]);
            if (dIn == Vector2.Zero || dOut == Vector2.Zero)
                return 1f;
            float turn = Math.Abs(MathHelper.WrapAngle(dOut.ToRotation() - dIn.ToRotation()));
            if (turn < 0.03f)
                return 1f;
            float cos = (float)Math.Cos(Math.Min(turn, MathHelper.Pi * 0.92f) * 0.5f);
            return 1f / Math.Max(cos, 0.28f);
        }

        public static bool SoftTrailsAllowed()
        {
            var cfg = GetYoyoConfig();
            return cfg == null || cfg.Visuals.SoftTrails;
        }

        // Profile-driven soft ribbon (the overload the 13 families call with
        // Projectile.type). Same table as the old path, soft renderer.
        public static void RibbonDrawSoft(List<Vector2> trail, int projectileType, float widthOverride = -1f)
        {
            TrailProfile p = GetTrailProfile(projectileType);
            if (widthOverride > 0f)
                p.MaxWidth = widthOverride;
            RibbonDrawSoft(trail, p.Edge, p.Core, p.MaxWidth);
        }

        // Soft streak: `maxWidth` is the full ribbon width where it is widest.
        public static void RibbonDrawSoft(List<Vector2> trail, Color edge, Color core, float maxWidth = 8f)
        {
            if (!SoftTrailsAllowed())
            {
                RibbonDraw(trail, edge, core, maxWidth);
                return;
            }
            if (trail.Count < 2 || maxWidth <= 0.01f)
                return;
            Texture2D tex = GetSoftTrailTexture();
            if (tex == null)
            {
                RibbonDraw(trail, edge, core, maxWidth);
                return;
            }

            // 1. Smooth + resample the centreline to ~1.4 px (tail -> head).
            int m = 1;
            softPts[0] = trail[0];
            int np = Math.Min(trail.Count, 64);
            for (int i = 0; i < np - 1 && m < SoftMaxPts; i++)
            {
                Vector2 p0 = trail[Math.Max(0, i - 1)];
                Vector2 p1 = trail[i];
                Vector2 p2 = trail[i + 1];
                Vector2 p3 = trail[Math.Min(np - 1, i + 2)];
                float segLen = Vector2.Distance(p1, p2);
                int steps = (int)MathHelper.Clamp((float)Math.Ceiling(segLen / 1.4f), 1f, 12f);
                for (int s = 1; s <= steps && m < SoftMaxPts; s++)
                    softPts[m++] = CatmullPoint(p0, p1, p2, p3, s / (float)steps);
            }
            if (m < 3)
                return;

            // 2. TRUE arc length behind the head for every resampled point. The
            //    old code lerped the fade across each source segment, which
            //    warped the ramp at every joint; measuring it directly keeps the
            //    gradient a straight function of distance along the ribbon.
            softDist[m - 1] = 0f;
            for (int k = m - 2; k >= 0; k--)
                softDist[k] = softDist[k + 1] + Vector2.Distance(softPts[k + 1], softPts[k]);
            if (softDist[0] < 1f)
                return;

            // Fade span: fixed per width so the fade never re-scales when the
            // point count breathes (that re-scaling was the old flicker).
            float span = MathHelper.Clamp(maxWidth * 14f, 40f, 160f);
            for (int k = 0; k < m; k++)
            {
                float t = MathHelper.Clamp(1f - softDist[k] / span, 0f, 1f);
                softHeat[k] = t;
                // ^1.4 instead of ^2: the gentler ramp halves the alpha step
                // between neighbouring quads at the opaque end, which is where
                // banding actually shows up.
                softFade[k] = 0.05f + 0.80f * (float)Math.Pow(t, 1.4);
                softWidth[k] = maxWidth * (0.15f + 0.85f * (float)Math.Sqrt(t));
                // Head flare: a soft bulge in the last stretch so the stroke
                // visibly feeds out of the yoyo instead of ending in a blunt
                // cap. t^6 keeps it entirely local to the head.
                softWidth[k] *= 1f + 0.35f * t * t * t * t * t * t;
            }

            bool glow = EffectsEnabled();
            var cfg = GetYoyoConfig();
            float coreK = (cfg != null && cfg.Visuals.Photosensitivity) ? 0.45f : 1f;
            Vector2 origin = tex.Size() * 0.5f;

            // 3. One rotated quad per resampled segment. Width is the average
            //    of the two ends, each end already widened by its miter factor,
            //    so the two quads meeting at a point share a cross-line: no
            //    wedge gap at corners and no lengthwise rectangle extension to
            //    stack into brighter bands. Alpha is likewise the average of
            //    the two ends, so the step at each joint is half of what a
            //    one-end value would give.
            for (int k = 0; k < m - 1; k++)
            {
                Vector2 diff = softPts[k + 1] - softPts[k];
                float seg = diff.Length();
                if (seg <= 0.01f)
                    continue;
                float rot = diff.ToRotation();
                float w = (softWidth[k] * MiterScale(k, m) + softWidth[k + 1] * MiterScale(k + 1, m)) * 0.5f;
                float fade = (softFade[k] + softFade[k + 1]) * 0.5f;
                Vector2 mid = (softPts[k] + softPts[k + 1]) * 0.5f - Main.screenPosition;
                // Heat gradient: the body lerps from the family's edge colour at
                // the tail toward its core colour at the head, so the ribbon
                // reads as heat building up where it is emitted (and it costs
                // nothing - it is the same draw).
                Color body = Color.Lerp(edge, core, (softHeat[k] + softHeat[k + 1]) * 0.5f * 0.55f);
                // 1.25x: the lens falloff means only ~75% of the drawn height
                // reads as ribbon, so this keeps the visible weight of the old
                // hard ribbon while staying soft.
                Main.spriteBatch.Draw(tex, mid, null, body * fade, rot, origin,
                    new Vector2(seg / tex.Width, w * 1.25f / tex.Height), SpriteEffects.None, 0f);
                // Additive hot core, every other sub-step (the soft glow texture
                // bridges the gap) so a long trail does not cost 300 draws.
                // Adaptive stride, so a long or numerous ribbon gets a SPARSER
                // core instead of a truncated one (the old "(k & 1) == 0" with a
                // hard cap is what broke the core into dashes past the budget).
                int coreStride = 2 + trailCores.Count / 1024;
                if (glow && (k % coreStride) == 0 && trailCores.Count < MaxTrailCores)
                {
                    float hotK = 0.30f * fade * coreK;
                    if (hotK > 0.01f)
                        trailCores.Add(new TrailCore
                        {
                            Mid = mid,
                            Rot = rot,
                            Len = seg * 2f,
                            Width = Math.Max(2f, w * 0.6f),
                            Color = core,
                            Alpha = hotK
                        });
                }
            }

            // Emission bloom: one soft additive puff right where the ribbon
            // leaves the yoyo, so the stroke reads as being poured out of the
            // head rather than merely drawn behind it. Single draw per trail,
            // master-switched by EffectsEnabled and dimmed for Photosensitivity.
            if (glow && softDist[0] > 6f && additiveGlowQueue.Count < 512)
                additiveGlowQueue.Add((softPts[m - 1], maxWidth * 2.6f, core, 0.20f * coreK));
        }

        // Single-flush additive glow queue (perf-safe: one Begin/End per frame).
        private static readonly List<(Vector2 pos, float diam, Color color, float alpha)> additiveGlowQueue
            = new List<(Vector2, float, Color, float)>();

        // Additive hot-core quads for the Catmull-Rom ribbon path (the strip path
        // gets its core from the shader instead). Queued per sub-segment, drawn
        // in the same single additive window as the glows above.
        private struct TrailCore
        {
            public Vector2 Mid;   // screen-space midpoint
            public float Rot;
            public float Len;
            public float Width;   // full soft-line width (pixels)
            public Color Color;
            public float Alpha;   // final additive multiplier
        }

        private static readonly List<TrailCore> trailCores = new List<TrailCore>();
        // BUDGET, NOT A CLIFF. This used to be 768 with a hard "stop queuing
        // once the list is this long" check. A long trail - a cursor held far
        // away, a barrage of ribbons at once - therefore lost its hot cores
        // from the point the budget ran out ONWARD: the bright core faded out
        // toward the head and the ribbon read as a broken, dashed, blocky line
        // that got worse the further out it went. That is the "trails to the
        // cursor are blocky the further it is" report. The stride in the
        // queueing loop below now WIDENS as the budget fills, so the core gets
        // sparser instead of stopping, and this floor is high enough that
        // normal play never reaches it at all.
        private const int MaxTrailCores = 4096;

        // Queued STAR FLARES. DrawStarSparkle stamps art straight into the live
        // sprite batch and may only run from a PreDraw; this is its queued
        // sibling for AI / OnHit / OnKill code - it only appends here and the
        // frame's additive window draws it, with the exact same crossed-flare
        // geometry, so both paths read identically.
        private struct QueuedFlare
        {
            public Vector2 World;
            public float Scale;
            public float Rot;
            public Color Color;
            public float Alpha;
        }

        private static readonly List<QueuedFlare> queuedFlares = new List<QueuedFlare>();
        private static Texture2D flareTexture;

        public static void DrawGlowAdditive(Vector2 worldCenter, float diameter, Color color, float alpha = 0.55f)
        {
            additiveGlowQueue.Add((worldCenter, diameter, color, alpha));
        }

        /// <summary>AI-safe sibling of <see cref="DrawStarSparkle"/>: queues a
        /// star flare for the frame's additive window instead of drawing into
        /// the caller's batch. Calling DrawStarSparkle from AI throws "Draw was
        /// called, but Begin has not yet been called" - which aborted the rest
        /// of that tick's AI (the hop, the trail record, everything after it),
        /// so a hop-lightning bolt visibly misbehaved.</summary>
        public static void QueueStarSparkle(Vector2 worldCenter, Color color, float scale, float rotation = 0f, float alpha = 1f)
        {
            if (alpha <= 0.01f || scale <= 0.01f || color.A == 0)
                return;
            if (queuedFlares.Count >= 256)
                return;
            queuedFlares.Add(new QueuedFlare {
                World = worldCenter, Scale = scale, Rot = rotation, Color = color, Alpha = alpha
            });
        }

        /// <summary>True while glows/trail cores/flares are waiting to be drawn.</summary>
        public static bool HasQueuedAdditiveGlows => additiveGlowQueue.Count > 0 || trailCores.Count > 0 || queuedFlares.Count > 0;

        /// <summary>Draws the queued additive glows into the batch that is
        /// CURRENTLY open, with no Begin/End of its own - the caller owns the
        /// batch. Callers that open their own additive window (MetaballSystem)
        /// MUST use this rather than <see cref="FlushAdditiveGlows"/>: that one
        /// closes the caller's batch through AdditiveDraw, whose speculative
        /// End() throws "End was called, but Begin has not yet been called"
        /// whenever no batch is live - a first-chance exception tModLoader logs.</summary>
        public static void DrawQueuedAdditiveGlowsInBatch()
        {
            if (!HasQueuedAdditiveGlows) return;
            var sb = Main.spriteBatch;
            Texture2D tex = GlowTexture;
            foreach (var g in additiveGlowQueue)
            {
                Vector2 pos = g.pos - Main.screenPosition;
                sb.Draw(tex, pos, null, g.color * g.alpha, 0f, tex.Size() * 0.5f,
                    g.diam / tex.Width, SpriteEffects.None, 0f);
            }
            foreach (var c in trailCores)
            {
                // Alpha channel forced to 255: additive blend multiplies rgb by
                // src alpha, and c.Alpha already carries the final multiplier.
                Color tint = c.Color * c.Alpha;
                tint.A = 255;
                sb.Draw(tex, c.Mid, null, tint, c.Rot, tex.Size() * 0.5f,
                    new Vector2((c.Len + 2f) / tex.Width, c.Width / tex.Height), SpriteEffects.None, 0f);
            }
            if (queuedFlares.Count > 0)
            {
                Texture2D star = flareTexture;
                if (star == null || star.IsDisposed)
                    flareTexture = star = ModContent.Request<Texture2D>("BiltonsYoyoMod/Projectiles/YoyoStar").Value;
                foreach (var f in queuedFlares)
                {
                    Vector2 pos = f.World - Main.screenPosition;
                    Vector2 origin = star.Size() * 0.5f;
                    // Alpha channel forced to 255: additive blend multiplies rgb
                    // by src alpha, and Alpha already carries the multiplier.
                    Color big = f.Color * f.Alpha * 0.5f;
                    big.A = 255;
                    Color small = f.Color * f.Alpha * 0.25f;
                    small.A = 255;
                    Vector2 wide = new Vector2(f.Scale * 0.5f, f.Scale);
                    sb.Draw(star, pos, null, big, MathHelper.PiOver2 + f.Rot, origin, wide, SpriteEffects.None, 0f);
                    sb.Draw(star, pos, null, big, f.Rot, origin, wide, SpriteEffects.None, 0f);
                    sb.Draw(star, pos, null, small, MathHelper.PiOver2 + f.Rot, origin, wide * 0.6f, SpriteEffects.None, 0f);
                    sb.Draw(star, pos, null, small, f.Rot, origin, wide * 0.6f, SpriteEffects.None, 0f);
                }
                queuedFlares.Clear();
            }
            additiveGlowQueue.Clear();
            trailCores.Clear();
        }

        /// <summary>Drops queued glows without drawing them, so that a failed
        /// batch open can never let the queues grow without bound.</summary>
        public static void ClearQueuedAdditiveGlows()
        {
            additiveGlowQueue.Clear();
            trailCores.Clear();
            queuedFlares.Clear();
        }

        /// <summary>Self-contained flush for call sites that are already inside a
        /// live batch and want the glows drawn additive without disturbing it.</summary>
        public static void FlushAdditiveGlows()
        {
            if (!HasQueuedAdditiveGlows) return;
            AdditiveDraw(DrawQueuedAdditiveGlowsInBatch);
        }

        // Motion readability \(Vexel C2\): 0/13 rotate toward velocity today.
        // Call once per AI tick, right after RibbonPush. Visual-only: draw-safe
        // rotation lag + subtle speed stretch. Never touches damage/AI timing.
        // rotationLag: Utils.AngleTowards step per tick (0.35 default).
        public static void ApplyMotionReadability(Projectile p, float rotationLag = 0.35f)
        {
            if (p.velocity.LengthSquared() > 0.04f)
                p.rotation = Utils.AngleTowards(p.rotation, p.velocity.ToRotation(), rotationLag);
            // Subtle speed response (draw-leaning; hitbox untouched since width/height fixed).
            // Kept tiny (<=8%) so 16px bodies never pop.
            float spd = MathHelper.Clamp(p.velocity.Length() * 0.02f, 0f, 1f);
            p.scale = 1f + spd * 0.08f;
        }

        // Additive window (hot cores, glows, energy). Safe at call sites where
        // vanilla has already ended its batch (e.g. right after DrawProjectiles,
        // which Begins/Ends its own batch internally): the restore Begin only
        // runs if we actually closed a live batch. Stranding a stray Begin here
        // desyncs vanilla and crashes the engine on its next Begin (fixed crash).
        public static void AdditiveDraw(Action draw)
        {
            var sb = Main.spriteBatch;
            bool hadBatch = false;
            try { sb.End(); hadBatch = true; } catch { }
            try
            {
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null,
                    Main.GameViewMatrix.TransformationMatrix);
                draw?.Invoke();
            }
            catch { }
            finally
            {
                try { sb.End(); } catch { }
                if (hadBatch)
                {
                    try
                    {
                        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                            DepthStencilState.None, Main.Rasterizer, null,
                            Main.GameViewMatrix.TransformationMatrix);
                    }
                    catch { }
                }
            }
        }

        // Subtractive window (dark materials: smoke, ink, void, char, ooze).
        // Same state-safe pattern as AdditiveDraw above (restore only if we
        // actually closed a live batch).
        public static void SubtractiveDraw(Action draw)
        {
            var sb = Main.spriteBatch;
            bool hadBatch = false;
            try { sb.End(); hadBatch = true; } catch { }
            try
            {
                BlendState sub = new BlendState
                {
                    ColorSourceBlend = Blend.SourceAlpha,
                    ColorDestinationBlend = Blend.One,
                    ColorBlendFunction = BlendFunction.ReverseSubtract
                };
                sb.Begin(SpriteSortMode.Deferred, sub, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null,
                    Main.GameViewMatrix.TransformationMatrix);
                draw?.Invoke();
            }
            catch { }
            finally
            {
                try { sb.End(); } catch { }
                if (hadBatch)
                {
                    try
                    {
                        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                            DepthStencilState.None, Main.Rasterizer, null,
                            Main.GameViewMatrix.TransformationMatrix);
                    }
                    catch { }
                }
            }
        }
        // TrailProfile: per-yoyo trail identity (width/color functions).
        public struct TrailProfile
        {
            public float MaxWidth;
            public Color Edge;
            public Color Core;
            public bool Smooth; // Catmull-Rom vs linear
            public TrailProfile(float w, Color e, Color c, bool smooth = true)
            { MaxWidth = w; Edge = e; Core = c; Smooth = smooth; }
        }

        // ---- VertexStrip trail path (EXPERIMENTAL, off by default) -------------
        // Real primitive strip (Terraria.Graphics.VertexStrip) for the boss-body
        // trails. The shader is self-contained (own projection + cross-section
        // shading + hot core); any failure falls back to the Catmull-Rom ribbon.
        // OFF by default because the strip pinches to zero width wherever the
        // path reverses direction, and a yoyo's path reverses constantly, so it
        // only looked right while the yoyo was dashing. Opt in via Config
        // Visuals.VertexStripTrailsExperimental (read live).
        private static readonly Terraria.Graphics.VertexStrip stripCache = new Terraria.Graphics.VertexStrip();
        private static Effect yoyoStripEffect;
        private static bool yoyoStripTried;
        private static TrailProfile stripDrawProfile;

        // Strip output is premultiplied by the shader.
        private static readonly BlendState StripBlendState = new BlendState
        {
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.InverseSourceAlpha,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.InverseSourceAlpha
        };

        private static Configuration.YoyoConfig GetYoyoConfig()
        {
            try { return ModContent.GetInstance<Configuration.YoyoConfig>(); }
            catch { return null; }
        }

        // Master extra-FX gate (additive trail cores, shockwave, hit flash...).
        public static bool EffectsEnabled()
        {
            var cfg = GetYoyoConfig();
            return cfg == null || cfg.Visuals.Effects;
        }

        // Strip needs its shader on AND the (experimental, default-off) toggle
        // left on (both live). Off by default: the strip pinches at direction
        // reversals, so the family yoyos use the Catmull-Rom ribbon instead.
        private static bool VertexStripAllowed()
        {
            var cfg = GetYoyoConfig();
            return cfg != null && cfg.Visuals.UseShaders && cfg.Visuals.VertexStripTrailsExperimental;
        }

        private static Effect GetStripEffect()
        {
            if (!yoyoStripTried)
            {
                yoyoStripTried = true;
                try { yoyoStripEffect = ModContent.Request<Effect>("BiltonsYoyoMod/Effects/YoyoStrip", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value; }
                catch { yoyoStripEffect = null; }
            }
            return yoyoStripEffect;
        }

        // Static delegates (cached; no per-frame closures). The active profile is
        // parked in stripDrawProfile for the duration of one Prepare call.
        //
        // Gradient stability: the callbacks receive `progress` as the index
        // fraction along the trail, and the point count breathes every few ticks
        // (points are added at spacing steps and shed while slow), so an
        // index-based fade re-scales the whole ribbon on those ticks - that was
        // the visible flicker. stripCum holds the true distance behind the head
        // for each kept point, so the fade/width are mapped to a stable span in
        // space instead (MaxWidth * 14 px, constant per yoyo type).
        private static readonly float[] stripCum = new float[64];
        private static int stripCount;
        private static float stripSpan = 96f;

        private static float StripHeadT(float progress)
        {
            if (stripCount <= 1)
                return MathHelper.Clamp(progress, 0f, 1f);
            float idx = MathHelper.Clamp(progress, 0f, 1f) * (stripCount - 1);
            int i0 = (int)idx;
            int i1 = Math.Min(i0 + 1, stripCount - 1);
            float dist = MathHelper.Lerp(stripCum[i0], stripCum[i1], idx - i0);
            return MathHelper.Clamp(1f - dist / stripSpan, 0f, 1f);
        }

        private static Color StripColorStatic(float progress)
        {
            float t = StripHeadT(progress);
            float fade = 0.06f + 0.64f * t * t;
            return new Color(stripDrawProfile.Edge.R, stripDrawProfile.Edge.G, stripDrawProfile.Edge.B,
                (byte)MathHelper.Clamp(255f * fade, 0f, 255f));
        }

        private static float StripWidthStatic(float progress)
        {
            return stripDrawProfile.MaxWidth * (0.15f + 0.85f * (float)Math.Sqrt(StripHeadT(progress)));
        }

        private static readonly Terraria.Graphics.VertexStrip.StripColorFunction stripColorFn = StripColorStatic;
        private static readonly Terraria.Graphics.VertexStrip.StripHalfWidthFunction stripWidthFn = StripWidthStatic;

        public static void RibbonDrawVertexStrip(List<Vector2> trail, TrailProfile profile)
        {
            if (trail.Count < 2) return;
            Effect fx = VertexStripAllowed() ? GetStripEffect() : null;
            if (fx == null)
            {
                RibbonDrawSoft(trail, profile.Edge, profile.Core, profile.MaxWidth);
                return;
            }
            bool drew = false;
            try
            {
                // De-dup consecutive points closer than 0.5 px (the smooth head
                // can park on a point while the body is nearly still) - a
                // zero-length segment breaks the strip's padding math.
                int n = 0;
                Vector2 prev = Vector2.Zero;
                for (int i = 0; i < trail.Count; i++)
                {
                    Vector2 p = trail[i];
                    if (i > 0 && Vector2.DistanceSquared(p, prev) < 0.25f)
                        continue;
                    prev = p;
                    n++;
                }
                if (n < 2)
                    return;
                if (n > stripCum.Length)
                    n = stripCum.Length;
                Vector2[] positions = new Vector2[n];
                float[] rotations = new float[n];
                int w = 0;
                prev = Vector2.Zero;
                for (int i = 0; i < trail.Count && w < n; i++)
                {
                    Vector2 p = trail[i];
                    if (i > 0 && Vector2.DistanceSquared(p, prev) < 0.25f)
                        continue;
                    positions[w] = p;
                    prev = p;
                    w++;
                }
                for (int i = 0; i < n; i++)
                {
                    rotations[i] = (i < n - 1)
                        ? (positions[i + 1] - positions[i]).ToRotation()
                        : rotations[Math.Max(0, i - 1)];
                }
                // Distance-behind-head ladder for the stable gradient.
                stripCount = n;
                stripCum[n - 1] = 0f;
                for (int i = n - 2; i >= 0; i--)
                    stripCum[i] = stripCum[i + 1] + Vector2.Distance(positions[i + 1], positions[i]);
                stripSpan = MathHelper.Clamp(profile.MaxWidth * 14f, 28f, 140f);
                stripDrawProfile = profile;
                stripCache.PrepareStripWithProceduralPadding(
                    positions, rotations, stripColorFn, stripWidthFn,
                    offsetForAllPositions: -Main.screenPosition,
                    includeBacksides: false,
                    tryStoppingOddBug: true);

                GraphicsDevice gd = Main.graphics.GraphicsDevice;
                Viewport vp = gd.Viewport;
                Matrix projection = Main.GameViewMatrix.TransformationMatrix *
                    Matrix.CreateOrthographicOffCenter(0f, vp.Width, vp.Height, 0f, 0f, 1f);
                try { fx.Parameters["uProjection"]?.SetValue(projection); } catch { }
                try { fx.Parameters["uCoreColor"]?.SetValue(profile.Core.ToVector4()); } catch { }
                try { fx.Parameters["uCoreBoost"]?.SetValue(EffectsEnabled() ? 1.15f : 0f); } catch { }

                BlendState prevBlend = gd.BlendState;
                try
                {
                    gd.BlendState = StripBlendState;
                    fx.CurrentTechnique.Passes[0].Apply();
                    stripCache.DrawTrail();
                    drew = true;
                }
                finally
                {
                    // Restore the world batch's blend and default pixel shader
                    // (vanilla trail recipe: re-apply Main.pixelShader).
                    try { gd.BlendState = prevBlend; } catch { }
                    try { Main.pixelShader.CurrentTechnique.Passes[0].Apply(); } catch { }
                }
            }
            catch
            {
                // Swallow; fall back to the Catmull-Rom ribbon below.
            }
            if (!drew)
                RibbonDrawSoft(trail, profile.Edge, profile.Core, profile.MaxWidth);
        }

        // Profile-driven trail draw: per-yoyo identity from the table (notes:
        // "all 13 yoyos pass a profile instead of 2 colors"). Routes to the soft
        // streak ribbon; widthOverride keeps phase-dynamic widths.
        public static void RibbonDraw(List<Vector2> trail, int projectileType, float widthOverride = -1f)
        {
            TrailProfile p = GetTrailProfile(projectileType);
            if (widthOverride > 0f)
                p.MaxWidth = widthOverride;
            if (VertexStripAllowed())
            {
                RibbonDrawVertexStrip(trail, p);
                return;
            }
            RibbonDrawSoft(trail, p.Edge, p.Core, p.MaxWidth);
        }

        // Afterimage helper: draw oldPos/oldRot ghosts with lighting response.
        // Call from PreDraw. Cache oldPos/oldRot in AI() every 2 ticks.
        public static void DrawAfterimages(Projectile p, Color color, int count, float scale = 1f)
        {
            if (p.oldPos == null) return;
            for (int i = p.oldPos.Length - 1; i >= 0; i--)
            {
                if (p.oldPos[i] == Vector2.Zero) continue;
                Vector2 oldCenter = p.oldPos[i] + p.Size * 0.5f;
                float distFromCurrent = Vector2.Distance(p.Center, oldCenter);
                if (distFromCurrent < 4f || distFromCurrent > 128f) continue;
                Vector2 drawPos = oldCenter - Main.screenPosition;
                float progress = (float)i / p.oldPos.Length;
                float alpha = (1f - progress) * 0.4f;
                float rotation = p.oldRot[i];
                Color tint = color * alpha;
                tint = tint.MultiplyRGB(Lighting.GetColor(oldCenter.ToTileCoordinates()));
                Main.EntitySpriteDraw(TextureAssets.Projectile[p.type].Value, drawPos, null, tint, rotation,
                    TextureAssets.Projectile[p.type].Value.Size() * 0.5f, scale * (1f - progress * 0.15f), SpriteEffects.None, 0f);
            }
        }
        // TrailProfile table: per-yoyo trail identity (width/color functions).
        private static readonly Dictionary<int, TrailProfile> trailProfiles = new Dictionary<int, TrailProfile>();
        public static TrailProfile GetTrailProfile(int type)
        {
            if (trailProfiles.Count == 0)
            {
                try
                {
                    trailProfiles[ModContent.ProjectileType<DarkMageYoyoProj>()] = new TrailProfile(8f, new Color(110, 45, 190), new Color(215, 180, 255));
                    trailProfiles[ModContent.ProjectileType<TorchGodYoyoProj>()] = new TrailProfile(8f, new Color(190, 90, 30), new Color(255, 215, 140));
                    trailProfiles[ModContent.ProjectileType<DutchmanYoyoProj>()] = new TrailProfile(7f, new Color(90, 110, 120), new Color(210, 225, 230));
                    trailProfiles[ModContent.ProjectileType<OgreYoyoProj>()] = new TrailProfile(7f, new Color(45, 110, 55), new Color(190, 240, 190));
                    trailProfiles[ModContent.ProjectileType<DreadnautilusYoyoProj>()] = new TrailProfile(9f, new Color(180, 35, 45), new Color(235, 110, 120));
                    trailProfiles[ModContent.ProjectileType<MourningWoodYoyoProj>()] = new TrailProfile(8f, new Color(150, 70, 25), new Color(255, 200, 130));
                    trailProfiles[ModContent.ProjectileType<SantaYoyoProj>()] = new TrailProfile(7f, new Color(180, 50, 45), new Color(255, 220, 170));
                    trailProfiles[ModContent.ProjectileType<PumpkingYoyoProj>()] = new TrailProfile(7f, new Color(150, 70, 25), new Color(255, 200, 130));
                    trailProfiles[ModContent.ProjectileType<EverscreamYoyoProj>()] = new TrailProfile(7f, new Color(45, 120, 60), new Color(220, 240, 190));
                    // Everscream ordinance: needle keeps a short pine streak,
                    // the ornament balls roll a fatter ribbon (were table-dead).
                    trailProfiles[ModContent.ProjectileType<EverscreamYoyoNeedleProj>()] = new TrailProfile(4f, new Color(45, 120, 60), new Color(200, 255, 200));
                    trailProfiles[ModContent.ProjectileType<EverscreamYoyoBallProj>()] = new TrailProfile(5.5f, new Color(170, 55, 65), new Color(255, 205, 195));
                    trailProfiles[ModContent.ProjectileType<IceQueenYoyoProj>()] = new TrailProfile(7f, new Color(140, 210, 255), new Color(185, 230, 255));
                    trailProfiles[ModContent.ProjectileType<MartianYoyoProj>()] = new TrailProfile(7f, new Color(40, 160, 120), new Color(210, 255, 230));
                    trailProfiles[ModContent.ProjectileType<BetsyYoyoProj>()] = new TrailProfile(8.5f, new Color(255, 120, 40), new Color(255, 200, 130));
                    trailProfiles[ModContent.ProjectileType<DeveloperYoyoProj>()] = new TrailProfile(7f, new Color(120, 80, 160), new Color(255, 100, 255));
                }
                catch { }
            }
            if (trailProfiles.TryGetValue(type, out var p)) return p;
            return new TrailProfile(7f, Color.Gray, Color.White);
        }
        public static bool IsAttackable(NPC npc)
        {
            return npc.CanBeChasedBy() || IsTestDummy(npc);
        }

        // B4: an aimed attack that found nothing still needs a beat: a dim
        // spark kick + soft bloom, so a whiff reads as "searching the dark",
        // not as nothing happening. Visual only - the old chirp was removed
        // (it re-fired with every whiffing volley: noise pollution).
        public static void StyledMiss(Vector2 pos, Color color, float scale = 1f)
        {
            var cfg = GetYoyoConfig();
            if (cfg != null && !cfg.Visuals.StyledMisses)
                return;
            for (int i = 0; i < 3; i++)
            {
                Vector2 dir = Main.rand.NextVector2Unit();
                YoyoParticles.SpawnSpark(pos + dir * 6f, dir * 1.1f, color * 0.8f, 0.6f * scale, 18, 0.9f);
            }
            YoyoParticles.SpawnBloom(pos, 2f * scale, 9f * scale, 12, color * 0.5f, 0.22f);
        }

        // K1: a faint dark forecast lane (with deterministic jitter - no
        // Main.rand, so it reads as one stable lane) previews where the next
        // aimed release will go. Drawn under the body while the tell runs.
        public static void DrawForecastLane(Vector2 from, Vector2 to, Color color, float alpha)
        {
            if (alpha <= 0.01f)
                return;
            var cfg = GetYoyoConfig();
            if (cfg != null && !cfg.Visuals.ForecastLanes)
                return;
            Vector2 a = from - Main.screenPosition;
            Vector2 b = to - Main.screenPosition;
            Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
            Vector2 side = new Vector2(-dir.Y, dir.X);
            const int steps = 6;
            for (int layer = 0; layer < 2; layer++)
            {
                Color c = layer == 0 ? new Color(4, 6, 10) * (alpha * 0.7f) : color * (alpha * 0.45f);
                float w = layer == 0 ? 3.2f : 1.4f;
                for (int i = 0; i < steps; i++)
                {
                    float t0 = i / (float)steps;
                    float t1 = (i + 1) / (float)steps;
                    float j0 = (float)System.Math.Sin(i * 12.9898f + Main.GameUpdateCount * 0.17f) * 2.2f * MathHelper.Min(t0 * 6f, 1f);
                    Vector2 p0 = Vector2.Lerp(a, b, t0) + side * j0;
                    Vector2 p1 = Vector2.Lerp(a, b, t1) + side * j0 * 0.7f;
                    DrawLine(p0, p1, w, c);
                }
            }
            DrawGlow(to, 10f, color, alpha * 0.5f);
        }

        // =====================================================================
        // IMPACT TAXONOMY (B1): one shared hit vocabulary across all 13 families.
        // SHEAR = glancing cut, CRUSH = head-on mass, PIERCE = skewered through,
        // DETONATE = payload pop, CHANNEL = sustained contact (beams/rituals).
        // Baseline comes from what the projectile IS; geometry (relative motion
        // vs the victim) refines melee bodies between CRUSH and SHEAR.
        // =====================================================================
        public enum ImpactClass
        {
            Shear,
            Crush,
            Pierce,
            Detonate,
            Channel
        }

        public static ImpactClass BaselineImpactClass(int type)
        {
            // Blades / scythes / tentacle sweeps: cuts.
            if (type == ModContent.ProjectileType<PumpkingYoyoBladeProj>()
                || type == ModContent.ProjectileType<PumpkingYoyoScytheProj>())
                return ImpactClass.Shear;
            // Heavy rammers.
            if (type == ModContent.ProjectileType<OgreYoyoClubProj>()
                || type == ModContent.ProjectileType<SantaYoyoSpikyBallProj>())
                return ImpactClass.Crush;
            // Thin, fast, penetrating.
            if (type == ModContent.ProjectileType<EverscreamYoyoNeedleProj>()
                || type == ModContent.ProjectileType<IceQueenYoyoShardProj>()
                || type == ModContent.ProjectileType<MartianYoyoLaserProj>()
                || type == ModContent.ProjectileType<MartianYoyoMissilesProj>()
                || type == ModContent.ProjectileType<SantaYoyoBulletProj>()
                || type == ModContent.ProjectileType<DreadnautilusYoyoBloodShardProj>()
                || type == ModContent.ProjectileType<DarkMageYoyoMagicProj>())
                return ImpactClass.Pierce;
            // Payloads that pop.
            if (type == ModContent.ProjectileType<BetsyYoyoFireBallProj>()
                || type == ModContent.ProjectileType<EverscreamYoyoBallProj>()
                || type == ModContent.ProjectileType<SantaYoyoPresentsProj>()
                || type == ModContent.ProjectileType<SantaYoyoMissilesProj>()
                || type == ModContent.ProjectileType<DutchManYoyoCannonProj>()
                || type == ModContent.ProjectileType<PumpkingYoyoFireProj>()
                || type == ModContent.ProjectileType<MourningWoodYoyoFireProj>()
                || type == ModContent.ProjectileType<DreadnautilusBloodOrbProj>()
                || type == ModContent.ProjectileType<OgreYoyoSpitProj>()
                || type == ModContent.ProjectileType<TorchGodYoyoTorchProj>()
                || type == ModContent.ProjectileType<DevYoyoRBProj>())
                return ImpactClass.Detonate;
            // Sustained energy: beams and the ritual drain.
            if (type == ModContent.ProjectileType<MartianYoyoDeathRayProj>()
                || type == ModContent.ProjectileType<BetsyYoyoFireProj>()
                || type == ModContent.ProjectileType<DarkMageYoyoMagicRitual>())
                return ImpactClass.Channel;
            // Everything else (mains, squids, breath orbs): geometry decides.
            return ImpactClass.Shear;
        }

        public static ImpactClass ClassifyImpact(int type, Vector2 velocityDir, Vector2 fromToTarget)
        {
            ImpactClass baseline = BaselineImpactClass(type);
            if (baseline != ImpactClass.Shear)
                return baseline;
            // Melee bodies: head-on meets read as crush, edge clips as shear.
            float headOn = Vector2.Dot(velocityDir.SafeNormalize(Vector2.UnitX), fromToTarget.SafeNormalize(Vector2.UnitX));
            return headOn > 0.35f ? ImpactClass.Crush : ImpactClass.Shear;
        }

        // The accent layer: pooled particles only, 6-14 tick beats, drawn above
        // (never instead of) the family's own ImpactFX. Every accent carries the
        // hit direction so the set never collapses into generic bursts.
        public static void ImpactClassFX(Vector2 pos, Vector2 direction, ImpactClass cls, float scale, Color color)
        {
            Vector2 dir = direction.SafeNormalize(Vector2.UnitX);
            Vector2 side = new Vector2(-dir.Y, dir.X);
            float s = MathHelper.Clamp(scale, 0.7f, 2.5f);
            switch (cls)
            {
                case ImpactClass.Crush:
                {
                    // Debris pushed through + flattened shock + slow pips.
                    YoyoParticles.SpawnBloom(pos, 4f * s, 20f * s, 12, color, 0.35f, 0.45f);
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 jet = (dir + side * (i - 2.5f) * 0.42f).SafeNormalize(Vector2.UnitX);
                        YoyoParticles.SpawnSpark(pos, jet * (3.4f + Math.Abs(i - 2.5f)) * s, color, 1.15f, 24, 0.94f, 0.04f);
                    }
                    YoyoParticles.SpawnSpark(pos - side * 3f, -dir * 2.2f, Color.White, 0.85f, 14, 0.96f);
                    // L2: shape receipt - dark splat under a hot star.
                    MiniBossShapeBank.Spawn(ShapeId.SplatSoft, pos, dir.ToRotation(), 0.5f * s, new Color(10, 12, 18) * 0.55f, 14);
                    MiniBossShapeBank.Spawn(ShapeId.SplatStar, pos, dir.ToRotation(), 0.32f * s, color * 0.75f, 11, dir * 1.4f * s);
                    break;
                }
                case ImpactClass.Pierce:
                {
                    // Long skewer along the travel line: entry + exit read.
                    YoyoParticles.SpawnSlashLine(pos, dir.ToRotation(), 46f * s, color, 2.5f, 9);
                    YoyoParticles.SpawnSlashLine(pos, dir.ToRotation(), 34f * s, Color.White, 1.2f, 8);
                    YoyoParticles.SpawnSpark(pos + dir * (14f * s), dir * 2.4f, color, 0.8f, 14, 0.95f);
                    YoyoParticles.SpawnSpark(pos - dir * (10f * s), -dir * 1.8f, color, 0.8f, 14, 0.95f);
                    // L2: the skewer's long axis rides the travel line.
                    MiniBossShapeBank.Spawn(ShapeId.NeedleLong, pos, dir.ToRotation() - MathHelper.PiOver2, new Vector2(0.34f * s, 0.62f * s), color * 0.7f, 9, dir * 2.2f * s);
                    break;
                }
                case ImpactClass.Detonate:
                {
                    // Payload pop: flash pip + small front + radial debris.
                    YoyoParticles.SpawnBloom(pos, 3f * s, 15f * s, 10, color, 0.45f);
                    YoyoParticles.SpawnRing(pos, 5f * s, 26f * s, 13, color, 2f);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = MathHelper.TwoPi * i / 7f + dir.ToRotation();
                        YoyoParticles.SpawnSpark(pos, a.ToRotationVector2() * (2.6f + i % 3) * s, color, 1f, 20, 0.93f);
                    }
                    // L2: payload pop loses its own soft splat + star bits.
                    MiniBossShapeBank.Spawn(ShapeId.SplatSoft, pos, 0f, 0.55f * s, color * 0.55f, 16);
                    MiniBossShapeBank.SpawnFan(ShapeId.SplatStar, pos, color, 4, 2.6f * s, 14, 0.26f * s);
                    break;
                }
                case ImpactClass.Channel:
                {
                    // Beacon at the contact point: soft orb + jitter ticks only.
                    YoyoParticles.SpawnBloom(pos, 6f * s, 2f * s, 9, color, 0.3f);
                    for (int i = 0; i < 3; i++)
                        YoyoParticles.SpawnSpark(pos + side * Main.rand.NextFloat(-6f, 6f), dir * Main.rand.NextFloat(-0.8f, 0.8f), color, 0.75f, 12, 0.9f);
                    // L2: channel contacts stamp a slow rotating seal.
                    MiniBossShapeBank.Spawn(ShapeId.SealRune, pos, Main.rand.NextFloat(MathHelper.TwoPi), 0.28f * s, color * 0.55f, 18, Vector2.Zero, 0.03f);
                    break;
                }
                default: // Shear
                {
                    // Crossed cuts hugging the travel tangent.
                    float ang = dir.ToRotation();
                    YoyoParticles.SpawnSlashLine(pos, ang + 0.55f, 30f * s, color, 3f, 8);
                    YoyoParticles.SpawnSlashLine(pos, ang - 0.55f, 30f * s, color, 3f, 8);
                    YoyoParticles.SpawnSlashLine(pos, ang, 22f * s, Color.White, 1.4f, 7);
                    for (int i = -1; i <= 1; i++)
                        YoyoParticles.SpawnSpark(pos, (dir + side * i * 0.5f).SafeNormalize(Vector2.UnitX) * 3.2f * s, color, 0.9f, 16, 0.94f);
                    // L2: shear cuts drag a curled crescent off the tangent.
                    MiniBossShapeBank.Spawn(ShapeId.SmearHook, pos, ang, 0.42f * s, color * 0.7f, 10, dir * 1.6f * s, 0.09f);
                    break;
                }
            }
        }

        // Central per-hit entry (called from YoyoKillFX): picks the class from
        // the projectile + hit geometry and fires the accent. Config-gated.
        public static void ImpactClassAccent(NPC target, Projectile projectile, int damageDone, Color color)
        {
            var cfg = GetYoyoConfig();
            if (cfg != null && !cfg.Visuals.ImpactClasses)
                return;
            Vector2 vel = projectile.oldVelocity.LengthSquared() > 0.01f ? projectile.oldVelocity : projectile.velocity;
            Vector2 velDir = vel.SafeNormalize(Vector2.UnitX);
            Vector2 centerDir = (target.Center - projectile.Center).SafeNormalize(velDir);
            ImpactClass cls = ClassifyImpact(projectile.type, velDir, centerDir);
            ImpactClassFX(projectile.Center, velDir, cls, HitScale(damageDone), color);
        }

        // Expanding shockwave: ONE front. Texture halo carries softness.
        public static void DrawShockwaveRing(Vector2 worldCenter, float startR, float endR, float duration, float elapsed, Color color, float width = 3f)
        {
            float t = MathHelper.Clamp(elapsed / duration, 0f, 1f);
            t = 1f - (1f - t) * (1f - t) * (1f - t);
            float radius = MathHelper.Lerp(startR, endR, t);
            float alpha = 1f - t;
            Color hot = Color.Lerp(color, Color.White, 0.55f);

            // Haze body: broad soft light filling the wave.
            DrawGlow(worldCenter, radius * 2.2f, color, 0.10f * alpha);

            // Trailing band: fainter rings lagging behind the front give the wave
            // real THICKNESS. One drawn circle reads as a sticker; three stacked
            // falloffs read as a compression front moving through air.
            for (int i = 1; i <= 3; i++)
            {
                float back = i * 0.055f;
                if (t - back * 0.9f <= 0f)
                    continue;
                DrawRing(worldCenter, radius * (1f - back),
                    width * (0.5f + 0.5f * alpha) * (1f - back * 0.4f),
                    color * (alpha * 0.22f / i));
            }

            // The lip: thin, hot, and the brightest thing in the wave. This is the
            // edge the eye tracks, so it gets a white-hot core pass.
            DrawRing(worldCenter, radius, width * (0.5f + 0.5f * alpha), color * (0.3f + 0.7f * alpha));
            DrawRing(worldCenter, radius, MathHelper.Max(1f, width * 0.35f), hot * (0.15f + 0.85f * alpha));
        }

        // Victim blood (Overhaul/Calamity-style directional gore): kill-only.
        // Bright jets that SHOOT (high speed, long life, gravity arcs) plus a
        // fountain pop, back spatter, heavy chunks, drips, and one small mist
        // so red reads on dark ground. Respects the YoyoBlood config toggle.
        public static void VictimBloodFX(NPC target, Color accent, Vector2 from)
        {
            // Blood VFX fully removed per user request.
            return;
        }
        public static void HitBloodFX(NPC target, Color accent, Vector2 from)
        {
            // Blood VFX fully removed per user request.
            return;
        }
        public static void KillConfirm(NPC target, Color color, Vector2 from, bool blood = false)
        {
            // NOTE: no active check: during OnHitNPC the victim's life is
            // already <= 0 but active flips false only after hits resolve.
            if (target == null || target.life > 0)
                return;
            if (blood)
            {
                // Directional gore only (no circles): accent flash + full bleed + shake.
                YoyoParticles.SpawnSpark(target.Center, (target.Center - from).SafeNormalize(Vector2.UnitX) * 5f, color, 1f, 26, 0.95f, 0.06f);
                VictimBloodFX(target, color, from);
                Shake(3, 12);
            }
            else
            {
                YoyoParticles.SpawnBurst(target.Center, 8, 3.5f, color, 1f, 22);
                // Soul wisp: the victim's color floats up so clean kills read too.
                for (int i = 0; i < 3; i++)
                    YoyoParticles.SpawnSpark(target.Center + Main.rand.NextVector2Circular(6f, 6f), new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-3f, -1.5f)), color, 0.9f, 40, 0.97f, -0.02f);
                Shake(2, 10);
            }
            // Ash drift: every kill sheds gray ashes on the victim's momentum.
            for (int i = 0; i < 4; i++)
            {
                Dust ash = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Smoke, target.velocity * 0.3f + new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2f, -0.5f)), 140, new Color(140, 140, 140), 1f);
                ash.noGravity = true;
            }
            // Multikill chain: a kill within 1s of the last pops a bigger ring.
            // The chain chime was removed (it re-fired on every chain step -
            // only the 25-kill milestone still chimes).
            int nowTick = (int)Main.GameUpdateCount;
            bool chained = nowTick - lastKillTick < 60 && lastKillTick > 0;
            lastKillTick = nowTick;
            if (chained)
            {
                YoyoParticles.SpawnRing(target.Center, 10f, 80f, 26, Color.White, 2.5f);
            }
            // Kill milestone: every 25th kill pops bigger + chimes.
            // Milestones stay universal white-gold: they mark SPECIAL kills.
            killCount++;
            if (killCount % 25 == 0)
            {
                YoyoParticles.SpawnRing(target.Center, 14f, 110f, 30, Color.White, 3f);
                for (int i = 0; i < 8; i++)
                    YoyoParticles.SpawnSpark(target.Center, Main.rand.NextVector2Circular(4f, 4f), new Color(255, 220, 150), 1f, 24, 0.95f);
                Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item29, target.Center);
            }
        }

        internal static void ResetMilestones()
        {
            killCount = 0;
        }

        private static int killCount;
        private static int lastKillTick;

        // Ground scorch/ground decal family (Overhaul DecalSystem-style, pooled):
        // flattened scorch patch + stuck embers where fire dies. The bloom is
        // squashed flat (squash < 1) so it reads as a ground mark, never a disc.
        public static void GroundScorch(Vector2 pos, Color color, float scale = 1f)
        {
            const int seconds = 8;
            YoyoParticles.SpawnBloom(pos, 6f * scale, 24f * scale, seconds * 60, new Color(30, 12, 8), 0.45f, 0.40f);
            YoyoParticles.SpawnBloom(pos, 4f * scale, 18f * scale, (seconds * 60 * 2) / 3, color, 0.4f, 0.36f);
        }

        // Ground-decal family (generalizes GroundScorch, vault L2#6): pooled flat
        // ground marks that fade by alpha over life. Crack = the Ogre club-land
        // fracture (jagged, tinted seam). Call-site gated by OgreCrackDecal.
        public static void GroundCrack(Vector2 pos, Color seamColor, float size = 1f, int life = 240)
        {
            YoyoParticles.SpawnDecal(pos, YoyoParticles.DecalStyle.Crack, seamColor, size,
                0.55f, life, Main.rand.NextFloat(-0.35f, 0.35f));
        }

        // Broadside smoke-ring puffs (Flying Dutchman cannon volleys): a ring of
        // soft smoke that must OCCLUDE (alpha-blended, never additive) plus a
        // heavier heart puff blown downwind. Config: Visuals.DutchmanSmokeRings,
        // read live — untick to remove instantly.
        public static void SmokeRingPuffs(Vector2 muzzle, Vector2 dir, float scale = 1f)
        {
            if (Main.netMode == Terraria.ID.NetmodeID.Server)
                return;
            bool on = true;
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                on = cfg == null || cfg.Visuals.DutchmanSmokeRings;
            }
            catch { }
            if (!on)
                return;
            Vector2 d = dir.SafeNormalize(Vector2.UnitX);
            Vector2 side = new Vector2(-d.Y, d.X);
            const int puffs = 6;
            for (int i = 0; i < puffs; i++)
            {
                float a = MathHelper.TwoPi * i / puffs;
                Vector2 off = side * (float)System.Math.Cos(a) * 6f * scale + d * (float)System.Math.Sin(a) * 3.5f * scale;
                Vector2 vel = d * (0.9f + 0.5f * (float)System.Math.Abs(System.Math.Sin(a)))
                    + off.SafeNormalize(Vector2.Zero) * 0.55f + new Vector2(0f, -0.18f);
                Color col = i % 2 == 0 ? new Color(58, 58, 64) : new Color(104, 104, 112);
                YoyoParticles.SpawnBloom(muzzle + off, 5f * scale, (16f + 4f * (i % 3)) * scale,
                    40 + i * 3, col, 0.5f, 0.82f, vel, 0.9f);
            }
            YoyoParticles.SpawnBloom(muzzle + d * 3f, 7f * scale, 26f * scale, 54,
                new Color(70, 70, 78), 0.45f, 0.9f, d * 1.2f + new Vector2(0f, -0.22f), 0.9f);
        }

        // Damage-scaled shake routing (Overhaul NPCHitScreenShake-style):
        // light hits tremble, heavy hits punch. Call in OnHitNPC.
        public static void HitShake(int damageDone)
        {
            int s = (int)MathHelper.Clamp(damageDone / 60f, 1f, 5f);
            Shake(s, 10);
        }

        // Screen shake (Calamity-style trauma): call sites request punch, and
        // YoyoCameraSystem applies it in ModifyScreenPosition with decay.
        // Photo-safe: YoyoCameraSystem skips when disabled in config.
        private static float trauma;
        // Kill-style lookup: maps yoyo projectile types to (color, blood-flag).
        // Recreated after blood VFX removal; blood flag is now always false.
        public static bool TryGetKillStyle(int type, out Color color, out bool blood)
        {
            color = Color.White;
            blood = false;
            if (type == ModContent.ProjectileType<DarkMageYoyoProj>()) { color = new Color(150, 80, 255); return true; }
            if (type == ModContent.ProjectileType<TorchGodYoyoProj>()) { color = new Color(255, 150, 60); return true; }
            if (type == ModContent.ProjectileType<DutchmanYoyoProj>()) { color = new Color(210, 225, 230); return true; }
            if (type == ModContent.ProjectileType<OgreYoyoProj>()) { color = new Color(120, 220, 120); return true; }
            if (type == ModContent.ProjectileType<DreadnautilusYoyoProj>()) { color = new Color(235, 110, 120); return true; }
            if (type == ModContent.ProjectileType<MourningWoodYoyoProj>()) { color = new Color(255, 160, 70); return true; }
            if (type == ModContent.ProjectileType<SantaYoyoProj>()) { color = new Color(255, 200, 100); return true; }
            if (type == ModContent.ProjectileType<PumpkingYoyoProj>()) { color = new Color(255, 170, 80); return true; }
            if (type == ModContent.ProjectileType<EverscreamYoyoProj>()) { color = new Color(150, 230, 150); return true; }
            if (type == ModContent.ProjectileType<IceQueenYoyoProj>()) { color = new Color(170, 225, 255); return true; }
            if (type == ModContent.ProjectileType<MartianYoyoProj>()) { color = new Color(120, 255, 200); return true; }
            if (type == ModContent.ProjectileType<BetsyYoyoProj>()) { color = new Color(255, 140, 60); return true; }
            if (type == ModContent.ProjectileType<DeveloperYoyoProj>()) { color = new Color(180, 120, 220); return true; }
            if (type == ModContent.ProjectileType<DarkMageYoyoMagicProj>() || type == ModContent.ProjectileType<DarkMageYoyoMagicRitual>()) { color = new Color(150, 80, 255); return true; }
            if (type == ModContent.ProjectileType<TorchGodYoyoTorchProj>()) { color = new Color(255, 150, 60); return true; }
            if (type == ModContent.ProjectileType<DutchManYoyoCannonProj>()) { color = new Color(210, 225, 230); return true; }
            if (type == ModContent.ProjectileType<OgreYoyoClubProj>() || type == ModContent.ProjectileType<OgreYoyoSpitProj>()) { color = new Color(120, 220, 120); return true; }
            if (type == ModContent.ProjectileType<DreadnautilusYoyoBloodShardProj>() || type == ModContent.ProjectileType<DreadnautilusBloodOrbProj>() || type == ModContent.ProjectileType<DreadnautilusTearsProj>() || type == ModContent.ProjectileType<DreadnautilusHomingBloodSquids>()) { color = new Color(235, 110, 120); return true; }
            if (type == ModContent.ProjectileType<MourningWoodYoyoFireProj>() || type == ModContent.ProjectileType<MourningWoodYoyoLingeringProj>()) { color = new Color(255, 160, 70); return true; }
            if (type == ModContent.ProjectileType<SantaYoyoBulletProj>() || type == ModContent.ProjectileType<SantaYoyoSpikyBallProj>() || type == ModContent.ProjectileType<SantaYoyoMissilesProj>() || type == ModContent.ProjectileType<SantaYoyoPresentsProj>()) { color = new Color(255, 200, 100); return true; }
            if (type == ModContent.ProjectileType<PumpkingYoyoBladeProj>() || type == ModContent.ProjectileType<PumpkingYoyoScytheProj>() || type == ModContent.ProjectileType<PumpkingYoyoFireProj>()) { color = new Color(255, 170, 80); return true; }
            if (type == ModContent.ProjectileType<EverscreamYoyoNeedleProj>() || type == ModContent.ProjectileType<EverscreamYoyoBallProj>()) { color = new Color(150, 230, 150); return true; }
            if (type == ModContent.ProjectileType<IceQueenYoyoShardProj>()) { color = new Color(170, 225, 255); return true; }
            if (type == ModContent.ProjectileType<MartianYoyoLaserProj>() || type == ModContent.ProjectileType<MartianYoyoDeathRayProj>() || type == ModContent.ProjectileType<MartianYoyoMissilesProj>()) { color = new Color(120, 255, 200); return true; }
            if (type == ModContent.ProjectileType<BetsyYoyoFireProj>() || type == ModContent.ProjectileType<BetsyYoyoFireBallProj>()) { color = new Color(255, 140, 60); return true; }
            if (type == ModContent.ProjectileType<DevYoyoRBProj>()) { color = new Color(180, 120, 220); return true; }
            return false;
        }
        public static void Shake(int intensity, int duration)
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            float add = intensity * 0.12f + duration * 0.008f;
            trauma = MathHelper.Clamp(trauma + add, 0f, 1f);
        }

        // Shake v2: distance-tapered (Vexel T3 / Luminance pattern). Off-screen
        // events shake less. Falls back to untapered when sourcePos is zero.
        public static void Shake(int intensity, int duration, Vector2 sourcePos)
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            if (sourcePos == Vector2.Zero)
            {
                Shake(intensity, duration);
                return;
            }
            float dist = Main.LocalPlayer != null ? Vector2.Distance(Main.LocalPlayer.Center, sourcePos) : 0f;
            float taper = MathHelper.Clamp(1f - dist / 2300f, 0f, 1f);
            float add = (intensity * 0.12f + duration * 0.008f) * taper;
            trauma = MathHelper.Clamp(trauma + add, 0f, 1f);
        }

        internal static Vector2 ConsumeShakeOffset()
        {
            if (trauma <= 0.01f)
            {
                trauma = 0f;
                return Vector2.Zero;
            }
            float mag = trauma * trauma * 14f;
            Vector2 offset = Main.rand.NextVector2Circular(mag, mag);
            trauma = MathHelper.Max(0f, trauma - 0.08f);
            return offset;
        }

        // Moomoo-style custom yoyo string: per-yoyo colored line from the
        // owner's hand to the yoyo, drawn over the vanilla string in PostDraw.
        public static void DrawCustomString(Projectile proj, Color color, float width = 2f)
        {
            if (proj.owner < 0 || proj.owner >= Main.maxPlayers)
                return;
            Player owner = Main.player[proj.owner];
            if (owner == null || !owner.active)
                return;
            Vector2 start = owner.MountedCenter - Main.screenPosition;
            Vector2 end = proj.Center - Main.screenPosition;
            DrawLine(start, end, width, color * 0.9f);
        }

        // Ground scan: first solid unactuated tile below a point (for landing
        // markers and blood stains). Cheap one-shot scan, never per-frame.
        public static bool FindGroundY(Vector2 pos, out Vector2 ground, int maxDown = 220)
        {
            int tx = (int)(pos.X / 16f);
            int ty = (int)(pos.Y / 16f);
            for (int i = 0; i < maxDown; i++)
            {
                int y = ty + i;
                if (tx < 0 || tx >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                    break;
                Tile t = Main.tile[tx, y];
                if (t != null && t.HasTile && t.HasUnactuatedTile && Main.tileSolid[t.TileType])
                {
                    ground = new Vector2(pos.X, y * 16f);
                    return true;
                }
            }
            ground = pos;
            return false;
        }

        // Landing marker: static fading ring on the ground under something
        // falling straight from the sky. One pooled ring each.
        public static void MarkLanding(Vector2 airPos, Color color, int life = 260)
        {
            if (!FindGroundY(airPos, out Vector2 ground))
                return;
            YoyoParticles.SpawnRing(ground, 18f, 18f, life, color, 2.5f);
        }

        // Landing marker that actually lands where the incoming projectile is
        // heading: simulates the fall with the caller's velocity + constant
        // vertical acceleration and marks the touchdown column. (The IceQueen's
        // shard rain is tilted up to 0.4 rad, which could drift the impact
        // ~200 px off a straight-down reticle.) The ring's life is matched to
        // the simulated flight, so it fades exactly as the projectile arrives;
        // a second scan under the predicted column catches ledges that are
        // offset from the spawn column.
        public static void MarkLandingFalling(Vector2 airPos, Vector2 velocity, float accelY, Color color)
        {
            if (!FindGroundY(airPos, out Vector2 ground))
                return;
            Vector2 p = SimulateFall(airPos, velocity, accelY, ground.Y, out int ticks);
            if (FindGroundY(new Vector2(p.X, airPos.Y), out Vector2 ground2) && (int)ground2.Y != (int)ground.Y)
            {
                ground = ground2;
                p = SimulateFall(airPos, velocity, accelY, ground.Y, out ticks);
            }
            YoyoParticles.SpawnRing(new Vector2(p.X, ground.Y), 18f, 18f, ticks + 24, color, 2.5f);
        }

        // Same, for Santa's gift rain: the presents do not fall straight - they
        // sway (vx = sin(t * 0.05) * swayAmp) and ease toward their lane's fall
        // speed, so the reticle is simulated with that exact motion. swayAmp = 0
        // reproduces the straight-falling dud gifts.
        public static void MarkLandingSway(Vector2 airPos, float fallSpeed, float swayAmp, int swayStart, Color color)
        {
            if (!FindGroundY(airPos, out Vector2 ground))
                return;
            Vector2 p = SimulateSway(airPos, fallSpeed, swayAmp, swayStart, ground.Y, out int ticks);
            if (FindGroundY(new Vector2(p.X, airPos.Y), out Vector2 ground2) && (int)ground2.Y != (int)ground.Y)
            {
                ground = ground2;
                p = SimulateSway(airPos, fallSpeed, swayAmp, swayStart, ground.Y, out ticks);
            }
            YoyoParticles.SpawnRing(new Vector2(p.X, ground.Y), 18f, 18f, ticks + 24, color, 2.5f);
        }

        private static Vector2 SimulateFall(Vector2 start, Vector2 velocity, float accelY, float stopY, out int ticks)
        {
            Vector2 p = start;
            Vector2 v = velocity;
            ticks = 0;
            for (int i = 0; i < 900; i++)
            {
                p += v;
                v.Y += accelY;
                ticks++;
                if (p.Y >= stopY)
                    break;
            }
            return p;
        }

        private static Vector2 SimulateSway(Vector2 start, float fallSpeed, float swayAmp, int swayStart, float stopY, out int ticks)
        {
            Vector2 p = start;
            Vector2 v = Vector2.Zero;
            float t = swayStart;
            ticks = 0;
            for (int i = 0; i < 900; i++)
            {
                t += 1f;
                v.Y += (fallSpeed - v.Y) * 0.03f;
                v.X = swayAmp == 0f ? 0f : (float)System.Math.Sin(t * 0.05f) * swayAmp;
                p += v;
                ticks++;
                if (p.Y >= stopY)
                    break;
            }
            return p;
        }

        // Telegraph: ONE crisp contracting ring + soft textured halo + single bead.
        // Real method (Spirit Prim / Calamity AoE style, CPU-only): texture carries
        // softness, geometry stays single. No echoes, ticks, or stars.
        public static void DrawTelegraphRing(Vector2 worldCenter, float charge01, float maxR, Color color, int segments = 24)
        {
            float c = MathHelper.Clamp(charge01, 0f, 1f);
            float radius = MathHelper.Lerp(maxR, 14f, c);
            float alpha = 0.25f + 0.55f * c;
            DrawGlow(worldCenter, radius * 2.6f, color, 0.12f + 0.10f * c);
            DrawRing(worldCenter, radius, 2f, color * alpha, segments);
            float ca = Main.GameUpdateCount * 0.11f;
            Vector2 cpos = worldCenter + ca.ToRotationVector2() * radius;
            DrawGlow(cpos, 7f, color, 0.5f * alpha + 0.12f);
        }

        // Comet ring: ONE circle + single rider. No inner echo, no star.
        public static void DrawDashedRing(Vector2 worldCenter, float radius, float width, Color color, int dashes = 12, float rotation = 0f)
        {
            DrawRing(worldCenter, radius, width, color);
            Vector2 cpos = worldCenter + rotation.ToRotationVector2() * radius;
            DrawGlow(cpos, width * 4f, color, 0.55f);
        }

        // Muzzle flash (Stars Above-style, reimplemented): tight bloom +
        // spark kiss + light at a volley muzzle. Spawn side only.
        public static void FlashFX(Vector2 pos, Color color, float scale = 1f)
        {
            // Slash: sparks only (bloom circle removed).
            YoyoParticles.SpawnBurst(pos, 4, 2.5f * scale, color, 0.9f, 18);
            Lighting.AddLight(pos, color.R / 255f * 0.8f, color.G / 255f * 0.8f, color.B / 255f * 0.8f);
        }

        // Swing slash FX (Ogre club, Pumpking blade, Betsy dash ONLY): layered
        // cut in one call - colored edge line, white-hot core line, swept arc
        // behind, sparks off both tips. Owner-side spawn rules inherited.
        public static void SlashFX(Vector2 pos, Vector2 direction, Color color, float scale, int owner = -1)
        {
            float ang = direction.ToRotation();
            YoyoParticles.SpawnSlashLine(pos, ang, 34f * scale, color, 5f, 9);
            YoyoParticles.SpawnSlashLine(pos, ang, 30f * scale, Color.White, 2f, 8);
            if (owner >= 0 && Main.myPlayer == owner)
                SpawnSlash(pos, direction, color, scale, owner);
            Vector2 side = new Vector2(-direction.Y, direction.X);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector2 tip = pos + direction * (17f * scale) + side * (i * 4f);
                YoyoParticles.SpawnSpark(tip, direction * 3f + side * (i * 2f), Color.White, 0.8f, 16, 0.94f);
            }
        }

        // Heavy-hit slash arc (Stars Above Bladework-style, original art):
        // spawns a short-lived tinted crescent facing the hit direction.
        // Must be called owner-side (mirrors other NewProjectile call sites).
        public static void SpawnSlash(Vector2 pos, Vector2 direction, Color color, float scale = 1f, int owner = -1)
        {
            int packed = (color.R << 16) | (color.G << 8) | color.B;
            int idx = Projectile.NewProjectile(Projectile.GetSource_NaturalSpawn(), pos, Vector2.Zero,
                ModContent.ProjectileType<YoyoSlashArcProj>(), 0, 0f, owner,
                direction.ToRotation(), packed);
            if (idx >= 0 && idx < Main.maxProjectiles)
            {
                Main.projectile[idx].scale = scale;
                Main.projectile[idx].netUpdate = true;
            }
        }

        // Implosion telegraph (converge-to-charge): ring of sparks flying
        // INWARD to the charge point. Spawn side only.
        public static void ImplodeFX(Vector2 pos, Color color, float scale = 1f)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 rim = pos + angle.ToRotationVector2() * 26f * scale;
                YoyoParticles.SpawnSpark(rim, -angle.ToRotationVector2() * 3f * scale, color, 1f, 22, 0.94f);
            }
        }

        // Fury aura: red edge ring while the owner is under 25% life
        // (generalized Dutchman desperate read). Draw only.
        public static void DrawFuryAura(Projectile proj)
        {
            var cfg = GetYoyoConfig();
            if (cfg != null && (!cfg.Visuals.SignatureAuras || cfg.Visuals.Photosensitivity))
                return;
            if (proj.owner < 0 || proj.owner >= Main.maxPlayers)
                return;
            Player owner = Main.player[proj.owner];
            if (owner == null || !owner.active || owner.dead || owner.statLife > owner.statLifeMax2 / 4)
                return;
            // Distress flicker: dropout strobe with bright spikes at low HP.
            if (Main.rand.NextBool(6))
                return;
            float spike = Main.rand.NextBool(8) ? 0.9f : 0.5f;
            float pulse = spike + 0.2f * (float)System.Math.Sin(Main.GameUpdateCount * 0.3f);
            DrawRing(proj.Center, 20f, 2.5f, new Color(255, 60, 50) * pulse, 24);
            // Chase bead (the three keepers only - Ogre/Betsy/DarkMage).
            float a = Main.GameUpdateCount * 0.09f;
            DrawGlow(proj.Center + a.ToRotationVector2() * 20f, 7f, new Color(255, 90, 60), 0.5f * pulse);
        }

        // Damage-scaled hit size: chips pop small, crits carve huge.
        // 60 dmg -> 1.1x, 150 -> 1.7x, 270+ -> capped 2.5x.
        public static float HitScale(int damageDone)
        {
            return MathHelper.Clamp(0.7f + damageDone / 150f, 0.7f, 2.5f);
        }

        // Pooled impact kit (safe in AI/OnHitNPC: YoyoParticles no-ops on server).
        // Tick-hit mark ONLY: streak fan + small hot core. Slashes live on
        // swing attacks (SlashFX), never on yoyo body ticks.
        public static void ImpactFX(Vector2 pos, Color color, float scale = 1f, int owner = -1, float angle = 0f, int tier = 1)
        {
            Vector2 dir = angle.ToRotationVector2();
            Vector2 side = new Vector2(-dir.Y, dir.X);
            int sparks = tier == 0 ? 4 : tier == 1 ? 12 : tier == 2 ? 24 : 40;
            for (int i = 0; i < sparks; i++)
            {
                Vector2 jet = (dir + side * (i % 5 - 2) * 0.3f).SafeNormalize(Vector2.UnitX);
                YoyoParticles.SpawnSpark(pos, jet * (4f + Math.Abs(i % 5)) * scale, color, tier >= 2 ? 1.1f : 1f, tier >= 2 ? 28 : 22, 0.95f);
            }
            if (tier >= 2)
                YoyoParticles.SpawnRing(pos, 8f * scale, 30f * scale, 16, color, 2f);
            if (tier >= 3 && owner >= 0 && Main.myPlayer == owner)
                Shake(3, 12, pos);
        }

        // Dissolve fade (Spirit-style, reimplemented): collapsing bloom + inward
        // sparks for deaths and phase exits. Spawn side only.
        public static void DissolveFX(Vector2 pos, Color color, float scale = 1f)
        {
            // Dissolve: ring + inward sparks only (bloom circle removed).
            YoyoParticles.SpawnRing(pos, 30f * scale, 8f * scale, 22, color, 2f);
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Vector2 inward = -angle.ToRotationVector2() * 3f * scale;
                YoyoParticles.SpawnSpark(pos + angle.ToRotationVector2() * 20f * scale, inward, color, 1f, 24, 0.94f);
            }
        }

        // =====================================================================
        // FADE SHAPES (B2): four death reads so "it ended" is never ambiguous.
        // Dissolve = aged out (inward ring). Shatter = broke on terrain (cold
        // debris falling). Collapse = recalled / yanked home (implosion).
        // Pop = force-killed mid-flight (flash + front).
        // =====================================================================
        public enum FadeShape
        {
            Dissolve,
            Shatter,
            Collapse,
            Pop
        }

        public static void FadeShapeFX(Vector2 pos, Color color, FadeShape shape, float scale = 1f)
        {
            float s = MathHelper.Clamp(scale, 0.5f, 2.5f);
            switch (shape)
            {
                case FadeShape.Shatter:
                {
                    // Facet break: shard sparks with gravity + a crack ring.
                    for (int i = 0; i < 8; i++)
                    {
                        float a = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 vel = a.ToRotationVector2() * Main.rand.NextFloat(1.6f, 3.4f) * s + new Vector2(0f, 0.6f);
                        YoyoParticles.SpawnSpark(pos + Main.rand.NextVector2Circular(6f, 6f), vel, color, 0.95f, 20, 0.95f, 0.07f);
                    }
                    YoyoParticles.SpawnRing(pos, 4f * s, 16f * s, 12, color, 1.5f);
                    break;
                }
                case FadeShape.Collapse:
                {
                    // Yanked home: the charge point eats its own sparks.
                    ImplodeFX(pos, color, s);
                    YoyoParticles.SpawnBloom(pos, 16f * s, 2f * s, 10, color, 0.22f);
                    break;
                }
                case FadeShape.Pop:
                {
                    // Force-transition pop: hot pip + small front + wisps.
                    FlashFX(pos, color, s);
                    YoyoParticles.SpawnRing(pos, 5f * s, 22f * s, 14, color, 2f);
                    YoyoParticles.SpawnBloom(pos, 3f * s, 14f * s, 12, color, 0.4f);
                    break;
                }
                default:
                    DissolveFX(pos, color, s);
                    break;
            }
        }

        // Directional beam from origin to end (world coords). Width tapers from startW to endW.
        // Used for death rays, fire breath, and similar effects.
        public static void DrawBeam(Vector2 origin, Vector2 end, Color coreColor, Color outerColor, float startWidth = 12f, float endWidth = 4f, float outerAlpha = 0.4f)
        {
            Vector2 startScreen = origin - Main.screenPosition;
            Vector2 endScreen = end - Main.screenPosition;
            Vector2 diff = endScreen - startScreen;
            float len = diff.Length();
            if (len < 0.01f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // Outer glow pass (wider, dimmer)
            for (int i = 0; i < 4; i++)
            {
                float tt = i / 3f;
                float w = MathHelper.Lerp(startWidth, endWidth, tt) * 2.2f;
                Vector2 pos = Vector2.Lerp(startScreen, endScreen, tt);
                Main.spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), outerColor * outerAlpha * (1f - tt * 0.5f),
                    diff.ToRotation(), new Vector2(0.5f, 0.5f), new Vector2(len / 4f + 1f, w), SpriteEffects.None, 0f);
            }

            // Core pass (narrow, bright)
            for (int i = 0; i < 6; i++)
            {
                float tt = i / 5f;
                float w = MathHelper.Lerp(startWidth, endWidth, tt);
                Vector2 pos = Vector2.Lerp(startScreen, endScreen, tt);
                Main.spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), coreColor * (0.6f + 0.4f * (1f - tt)),
                    diff.ToRotation(), new Vector2(0.5f, 0.5f), new Vector2(len / 6f + 1f, w), SpriteEffects.None, 0f);
            }
        }

        // Beam v2 (P1.3 / L3 "beams as animated trails", notes-verified):
        // two-pass vanilla textured beam (ExampleLastPrism recipe, via Utils.DrawLaser)
        // + charge-in grow (0->1, never pops) + flowing striated core + end-cap
        // cross-flare. Call inside a PreDraw (batch active). DrawLaser passes run
        // in the live batch; the flow overlay opens its own additive window.
        public static void DrawBeamV2(Vector2 origin, Vector2 end, Color coreColor, Color outerColor, float width = 6f, float grow = 1f, float scroll = 1f)
        {
            grow = MathHelper.Clamp(grow, 0f, 1f);
            if (grow <= 0.01f)
                return;
            Vector2 diff = end - origin;
            float len = diff.Length();
            if (len < 6f)
                return;
            Vector2 dir = diff / len;
            Vector2 tip = origin + dir * (len * grow);

            float liveLen = (tip - origin).Length();
            if (liveLen < 4f)
                return;
            Vector2 mid = (origin + tip) * 0.5f;
            Vector2 side = new Vector2(-dir.Y, dir.X);
            float rot = dir.ToRotation();

            // Whole beam in one additive window (single Begin/End). This used to
            // lean on Utils.DrawLaser + DelegateMethods.RainbowLaserDraw with the
            // 1x1 MagicPixel, which drew nothing at all - the solid beam was
            // invisible and only the striations/end-cap below survived, so the ray
            // read as plain particles. It is now a soft-lens stack (the same
            // primitive the slash + star VFX use): broad halo / tinted body / hot
            // inner / white core, each a radial MiniYoyoGlow stretched along the
            // beam, so the edges fall off and the centre stays white-hot.
            AdditiveDraw(() =>
            {
                Color white = Color.Lerp(coreColor, Color.White, 0.85f);
                Texture2D pixel = TextureAssets.MagicPixel.Value;

                // Round glow caps at the two ends. A soft radial is right *here*
                // (the ends should taper); it is wrong for the body.
                DrawSoftRay(origin, 0f, width * 4.2f, width * 4.2f, outerColor, 0.22f);
                DrawSoftRay(tip, 0f, width * 4.2f, width * 4.2f, outerColor, 0.30f);

                // Body as layered flat stripes: constant thickness for the whole
                // ray. A single stretched radial is a *diamond* - its alpha is a
                // linear cone, so it tapers over half the length and the visible
                // bright part is only ~half the requested width; that is why the
                // soft-ray-only pass read as a thin line. Additive low-alpha bands
                // stack into a soft-edged beam with an exact, controllable width
                // (Martian passes ~6 -> ~19px outer / ~10px body / ~2px core).
                // Body as layered flat stripes: constant thickness for the whole
                // ray, banked square ends. Intentional. These are HARD on
                // purpose (this beam is the one place the mod wants a slab) - do
                // not route them through DrawSoftBar.
                DrawStripe(pixel, mid, rot, liveLen, width * 3.2f, outerColor, 0.10f);
                DrawStripe(pixel, mid, rot, liveLen, width * 1.7f, outerColor, 0.20f);
                DrawStripe(pixel, mid, rot, liveLen, width * 0.80f, coreColor, 0.40f);
                DrawStripe(pixel, mid, rot, liveLen, width * 0.30f, white, 0.90f);
                // Striations: short dashes crawling origin -> tip (energy/data flow).
                float travel0 = Main.GameUpdateCount * 0.035f * scroll;
                for (int i = 0; i < 5; i++)
                {
                    float travel = (i / 5f + travel0) % 1f;
                    float taper = 1f - Math.Abs(travel - 0.5f) * 1.5f;
                    if (taper <= 0.05f)
                        continue;
                    float lane = (i % 2 == 0 ? 1f : -1f) * width * 0.38f;
                    float dashLen = MathHelper.Clamp(liveLen * 0.12f, 8f, 64f);
                    Vector2 dashCenter = origin + dir * (travel * liveLen) + side * lane;
                    Main.spriteBatch.Draw(pixel, dashCenter - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                        coreColor * (0.45f * taper), rot, new Vector2(0.5f, 0.5f),
                        new Vector2(dashLen, width * 0.35f), SpriteEffects.None, 0f);
                }
                // End mushroom: a MUZZLE, not a pixel cross. The body above is
                // deliberately a hard slab (this is the one place the mod wants
                // one), but the tip has to terminate in LIGHT - so the old
                // three-stripe cross is gone. A wide radial bloom swallows the
                // cut end, a hot core sits in it, and a flattened bow-shock lens
                // rides a hair ahead of the tip so the ray reads as a discharge
                // instead of a bar that stops.
                float cap = (grow - 0.8f) / 0.2f;
                if (cap > 0f)
                {
                    cap = MathHelper.Clamp(cap, 0f, 1f);
                    float beat = 0.86f + 0.14f * MathF.Sin(Main.GameUpdateCount * 0.33f + scroll * 5f);
                    float pulse = cap * beat;
                    DrawSoftRay(tip, 0f, width * 8.5f * pulse, width * 8.5f * pulse, outerColor, 0.32f * pulse);
                    DrawSoftRay(tip, 0f, width * 3.9f * pulse, width * 3.9f * pulse, coreColor, 0.40f * pulse);
                    DrawSoftRay(tip, 0f, width * 1.8f * pulse, width * 1.8f * pulse, white, 0.55f * pulse);
                    // Bow shock: a wide, thin lens along the beam axis, just
                    // ahead of the tip, so the air in front of it reads as hit.
                    DrawSoftRay(tip + dir * (width * 1.1f), rot, width * 2.6f * pulse, width * 6.2f * pulse,
                        white, 0.20f * pulse);
                }
            });
        }

        // L3 heat-shimmer: two wavy additive ghost layers over the beam, echoing
        // hot-air distortion without any screen pass. Each layer is a soft glow
        // ripple crawling along the beam; ends taper so the ghosts never clip on.
        public static void DrawBeamHeatShimmer(Vector2 origin, Vector2 end, Color tint, float width = 6f, float grow = 1f)
        {
            if (!EffectsEnabled())
                return;
            grow = MathHelper.Clamp(grow, 0f, 1f);
            if (grow <= 0.05f)
                return;
            Vector2 diff = end - origin;
            float len = diff.Length();
            if (len < 6f)
                return;
            Vector2 dir = diff / len;
            Vector2 side = new Vector2(-dir.Y, dir.X);
            float rot = dir.ToRotation();
            float liveLen = len * grow;
            Texture2D glow = GlowTexture;
            float t0 = Main.GameUpdateCount * 0.12f;
            AdditiveDraw(() =>
            {
                const int segs = 14;
                for (int layer = 0; layer < 2; layer++)
                {
                    float phase = layer * 2.1f;
                    float amp = width * (0.55f + 0.25f * layer);
                    float alpha = 0.10f * grow * (1f - layer * 0.35f);
                    float w = width * (1.1f + 0.5f * layer);
                    for (int i = 0; i < segs; i++)
                    {
                        float t = (i + 0.5f) / segs;
                        float wave = (float)Math.Sin(t * 9.4f - t0 * (1.35f + 0.3f * layer) + phase);
                        float edge = MathHelper.Clamp(t * 6f, 0f, 1f) * MathHelper.Clamp((1f - t) * 6f, 0f, 1f);
                        Color c = tint * (alpha * edge);
                        c.A = 255; // additive blend multiplies rgb by src alpha; keep it single-applied
                        Vector2 mid = origin + dir * (t * liveLen) + side * wave * amp;
                        Main.spriteBatch.Draw(glow, mid - Main.screenPosition, null,
                            c, rot, glow.Size() * 0.5f,
                            new Vector2((liveLen / segs + 3f) / glow.Width, w / glow.Height), SpriteEffects.None, 0f);
                    }
                }
            });
        }

        // Blood drip particle: gravity-affected, short-lived, with tile collision splash.
        public static void SpawnBloodDrip(Vector2 position, Vector2 velocity, float scale = 1f)
        {
            Dust drip = Dust.NewDustPerfect(position, DustID.Blood, velocity, 0, default, scale);
            drip.noGravity = false;
            drip.velocity *= 0.6f;
        }

        // Impact burst: directional flash + particles + light + ring. Used on enemy hits.
        public static void DrawImpactBurst(Vector2 worldCenter, Color color, float radius = 24f)
        {
            DrawGlow(worldCenter, radius, color, 0.6f);
            DrawRing(worldCenter, radius * 0.7f, 2f, color * 0.8f);
        }
    }



}
