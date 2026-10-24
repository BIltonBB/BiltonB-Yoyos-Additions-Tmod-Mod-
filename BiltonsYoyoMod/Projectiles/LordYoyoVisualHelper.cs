using Terraria;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public static class LordYoyoVisualHelper
    {
        public static void CachePosition(List<Vector2> positions, Vector2 position, float spacing, int maxCount)
        {
            if (positions.Count == 0 || Vector2.DistanceSquared(position, positions[positions.Count - 1]) >= spacing * spacing)
                positions.Add(position);

            while (positions.Count > maxCount)
                positions.RemoveAt(0);
        }

        public static void DrawNeonTrail(
            Texture2D texture,
            List<Vector2> positions,
            float rotation,
            float scale,
            Color fallbackLight,
            Vector2 screenPosition,
            float strength = 1f)
        {
            if (positions.Count < 2)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // Positions are oldest -> newest.  Draw only tapered segments,
            // so the trail is distance-based rather than a stack of sprites.
            for (int i = 1; i < positions.Count; i++)
            {
                Vector2 startWorld = positions[i - 1];
                Vector2 endWorld = positions[i];
                Vector2 delta = endWorld - startWorld;
                float length = delta.Length();
                if (length <= 0.01f)
                    continue;

                // Newest segment is brightest; older segments fade away.
                float ageFraction = (i - 1f) / Math.Max(1f, positions.Count - 1f);
                float alpha = MathHelper.Lerp(0.025f, 0.58f, ageFraction) * strength;
                float width = MathHelper.Lerp(0.55f, 2.4f, ageFraction) * Math.Max(0.8f, scale);

                Vector2 midpointWorld = (startWorld + endWorld) * 0.5f;
                Color local = Lighting.GetColor(
                    (int)(midpointWorld.X / 16f),
                    (int)(midpointWorld.Y / 16f));

                Color neon = Color.Lerp(
                    local,
                    new Color(40, 190, 255),
                    0.78f);

                Color core = Color.Lerp(neon, Color.White, 0.70f);

                Main.spriteBatch.Draw(
                    pixel,
                    midpointWorld - screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    neon * (alpha * 0.72f),
                    delta.ToRotation(),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(length, width * 1.9f),
                    SpriteEffects.None,
                    0f);

                Main.spriteBatch.Draw(
                    pixel,
                    midpointWorld - screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    core * alpha,
                    delta.ToRotation(),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(length, width),
                    SpriteEffects.None,
                    0f);
            }
        }

        public static void DrawNeonSprite(
            Texture2D texture,
            Vector2 position,
            float rotation,
            float scale,
            Color lightColor,
            Vector2 screenPosition,
            float glowStrength = 1f)
        {
            Color local = Lighting.GetColor((int)(position.X / 16f), (int)(position.Y / 16f));
            Color baseColor = Color.Lerp(local, lightColor, 0.45f);
            Color neon = Color.Lerp(baseColor, new Color(70, 210, 255), 0.80f);

            Main.EntitySpriteDraw(
                texture,
                position - screenPosition,
                null,
                neon * (0.30f * glowStrength),
                rotation,
                texture.Size() * 0.5f,
                scale * 1.18f,
                SpriteEffects.None,
                0f);

            Main.EntitySpriteDraw(
                texture,
                position - screenPosition,
                null,
                neon * (0.68f * glowStrength),
                rotation,
                texture.Size() * 0.5f,
                scale * 1.05f,
                SpriteEffects.None,
                0f);

            Main.EntitySpriteDraw(
                texture,
                position - screenPosition,
                null,
                Color.Lerp(neon, Color.White, 0.80f) * (0.98f * glowStrength),
                rotation,
                texture.Size() * 0.5f,
                scale,
                SpriteEffects.None,
                0f);
        }

        public static void AddBlueLight(Vector2 position, float strength = 1f)
        {
            Lighting.AddLight(position, 0.08f * strength, 0.42f * strength, 1.0f * strength);
        }

        public static void SpawnBlueSpark(Vector2 position, float speed = 2f, float scale = 0.8f)
        {
            if (!Main.rand.NextBool(2))
                return;

            Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(speed * 0.5f, speed * 1.5f);
            Dust dust = Dust.NewDustPerfect(
                position,
                Terraria.ID.DustID.IceTorch,
                velocity,
                90,
                Color.White,
                Main.rand.NextFloat(scale * 0.7f, scale * 1.3f));
            dust.noGravity = true;
        }
    }
}
