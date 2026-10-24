using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Common
{
    // Purple continuous-fading trail + telegraph-line helper for Guardian projectiles.
    //
    // The trail is based on the Lord Yoyo trail:
    // - Movement creates distance-based segments.
    // - Newest segments are brightest.
    // - Older segments are dimmer.
    // - When the projectile stops, the entire existing trail continuously fades away.
    public static class GYPurpleTrail
    {
        public static readonly Color TrailColor = new Color(150, 60, 230);
        public static readonly Color CoreColor =
            Color.Lerp(TrailColor, Color.White, 0.55f);

        private const float TeleportClearDist = 250f;

        // Current visual opacity for each trail list.
        private static readonly Dictionary<List<Vector2>, float> TrailOpacity =
            new Dictionary<List<Vector2>, float>();

        // How quickly a stationary trail disappears.
        // 0.025f = roughly 40 frames to completely disappear.
        private const float FadeSpeed = 0.025f;

        public static void Cache(
            List<Vector2> positions,
            Vector2 position,
            float spacing = 10f,
            int maxCount = 20)
        {
            if (!TrailOpacity.ContainsKey(positions))
                TrailOpacity[positions] = 1f;

            if (positions.Count == 0)
            {
                positions.Add(position);
                TrailOpacity[positions] = 1f;
                return;
            }

            float distSq =
                Vector2.DistanceSquared(
                    position,
                    positions[positions.Count - 1]);

            // Yoyo/projectile snapped somewhere else.
            // Clear the old trail instead of drawing a giant streak.
            if (distSq > TeleportClearDist * TeleportClearDist)
            {
                positions.Clear();
                positions.Add(position);
                TrailOpacity[positions] = 1f;
                return;
            }

            // Only add a new trail point after moving far enough.
            if (distSq >= spacing * spacing)
            {
                positions.Add(position);

                // Movement happened, so restore the trail's opacity.
                TrailOpacity[positions] = 1f;
            }

            while (positions.Count > maxCount)
                positions.RemoveAt(0);
        }

        public static void Clear(List<Vector2> positions)
        {
            positions.Clear();
            TrailOpacity.Remove(positions);
        }

        public static void Draw(
            List<Vector2> positions,
            float strength = 1f)
        {
            if (positions.Count < 2)
                return;

            if (!TrailOpacity.TryGetValue(
                positions,
                out float trailOpacity))
            {
                trailOpacity = 1f;
                TrailOpacity[positions] = trailOpacity;
            }

            if (trailOpacity <= 0f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            for (int i = 1; i < positions.Count; i++)
            {
                Vector2 a =
                    positions[i - 1] - Main.screenPosition;

                Vector2 b =
                    positions[i] - Main.screenPosition;

                Vector2 delta = b - a;
                float length = delta.Length();

                if (length <= 0.01f)
                    continue;

                // Oldest -> newest.
                // Newest part is brightest, just like the Lord trail.
                float age =
                    (i - 1f) /
                    MathHelper.Max(
                        1f,
                        positions.Count - 1f);

                float alpha =
                    MathHelper.Lerp(
                        0.03f,
                        0.6f,
                        age) *
                    trailOpacity *
                    strength;

                float width =
                    MathHelper.Lerp(
                        0.6f,
                        2.6f,
                        age);

                Vector2 center =
                    (a + b) * 0.5f;

                float rotation =
                    delta.ToRotation();

                // Purple outer glow.
                Main.spriteBatch.Draw(
                    pixel,
                    center,
                    new Rectangle(0, 0, 1, 1),
                    TrailColor * (alpha * 0.7f),
                    rotation,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(
                        length,
                        width * 1.8f),
                    SpriteEffects.None,
                    0f);

                // Bright purple-white core.
                Main.spriteBatch.Draw(
                    pixel,
                    center,
                    new Rectangle(0, 0, 1, 1),
                    CoreColor * alpha,
                    rotation,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(
                        length,
                        width),
                    SpriteEffects.None,
                    0f);
            }

            // Continuously fade the existing trail.
            //
            // Cache() only resets this when the projectile actually moves
            // far enough to create a new trail segment.
            trailOpacity -= FadeSpeed;

            if (trailOpacity <= 0f)
            {
                trailOpacity = 0f;
                positions.Clear();
            }

            TrailOpacity[positions] = trailOpacity;
        }

        // 1-second fade-in/out aim indicator.
        // halfLength = 1000f gives a 2000-pixel total line.
        public static void DrawTelegraph(
            Vector2 origin,
            float angle,
            float halfLength,
            float alpha)
        {
            if (alpha <= 0f || halfLength <= 0f)
                return;

            Texture2D pixel =
                TextureAssets.MagicPixel.Value;

            Vector2 dir =
                angle.ToRotationVector2();

            Vector2 start =
                origin - dir * halfLength;

            Vector2 end =
                origin + dir * halfLength;

            // Faint wide glow.
            DrawBeam(
                pixel,
                start,
                end,
                TrailColor * (alpha * 0.35f),
                5f);

            // Sharper purple line.
            DrawBeam(
                pixel,
                start,
                end,
                TrailColor * (alpha * 0.6f),
                2f);

            // Bright purple-white core.
            DrawBeam(
                pixel,
                start,
                end,
                CoreColor * alpha,
                1f);

            // Pulsing origin dot.
            float dot =
                8f +
                (float)System.Math.Sin(
                    Main.GameUpdateCount * 0.3f) * 2f;

            // The dot USED to be a scaled 1px quad with rotation 0f:
            //   spriteBatch.Draw(pixel, origin, rect(0,0,1,1), color, 0f,
            //                    new Vector2(0.5f), new Vector2(dot, dot))
            // A 1px texture scaled uniformly with no rotation is a literal
            // axis-aligned SQUARE - and because it was additive and pulsing it
            // read as a small glowing box sitting on the telegraph line. Every
            // "why does this effect draw squares?" report is one of these. A
            // soft radial glow is round at any size and takes no rotation
            // argument, so it is structurally incapable of squaring off.
            global::BiltonsYoyoMod.Projectiles.MiniYoyoFx.DrawGlowAdditive(
                origin, dot * 3.2f, TrailColor, alpha * 0.55f);
            global::BiltonsYoyoMod.Projectiles.MiniYoyoFx.DrawGlowAdditive(
                origin, dot * 1.5f, CoreColor, alpha * 0.85f);
        }

        private static void DrawBeam(
            Texture2D pixel,
            Vector2 worldA,
            Vector2 worldB,
            Color color,
            float width)
        {
            Vector2 a =
                worldA - Main.screenPosition;

            Vector2 b =
                worldB - Main.screenPosition;

            Vector2 delta = b - a;
            float length = delta.Length();

            if (length <= 0.01f)
                return;

            Main.spriteBatch.Draw(
                pixel,
                (a + b) * 0.5f,
                new Rectangle(0, 0, 1, 1),
                color,
                delta.ToRotation(),
                new Vector2(0.5f, 0.5f),
                new Vector2(length, width),
                SpriteEffects.None,
                0f);
        }
    }
}