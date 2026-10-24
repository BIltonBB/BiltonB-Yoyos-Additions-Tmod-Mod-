using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Utils
{
    // Shared purple trail for every mod projectile/mob.
    public static class PurpleTrailHelper
    {
        public static readonly Color TrailColor = new Color(150, 60, 230);

        public static void Cache(List<Vector2> positions, Vector2 position, float spacing = 10f, int maxCount = 20)
        {
            if (positions.Count == 0 || Vector2.DistanceSquared(position, positions[^1]) >= spacing * spacing)
                positions.Add(position);

            while (positions.Count > maxCount)
                positions.RemoveAt(0);
        }

        public static void Draw(List<Vector2> positions, float strength = 1f)
        {
            if (positions.Count < 2) return;

            for (int i = 1; i < positions.Count; i++)
            {
                Vector2 a = positions[i - 1];
                Vector2 b = positions[i];
                Vector2 delta = b - a;
                float length = delta.Length();
                if (length <= 0.01f) continue;

                float age = (i - 1f) / Math.Max(1f, positions.Count - 1f);
                float alpha = MathHelper.Lerp(0.03f, 0.6f, age) * strength;
                float width = MathHelper.Lerp(0.6f, 2.6f, age);

                Color core = Color.Lerp(TrailColor, Color.White, 0.55f);

                // Soft lenses instead of stretched pixels. This helper is shared
                // by every mod projectile and mob, and a 1x1 quad stretched to a
                // few pixels wide is a hard-cornered rectangle (segments run
                // 13-26px, so each one reads as a small bar). Same profile, same
                // width ramp, no corners.
                Vector2 mid = (a + b) * 0.5f;
                MiniYoyoFx.DrawSoftRay(mid, delta.ToRotation(), length, width * 2.4f, TrailColor, alpha * 0.55f);
                MiniYoyoFx.DrawSoftRay(mid, delta.ToRotation(), length * 0.94f, width, core, alpha * 0.8f);
            }
        }
    }
}
