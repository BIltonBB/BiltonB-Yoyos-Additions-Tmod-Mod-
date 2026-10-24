using Terraria;
using Terraria.ID;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public static class PrimeYoyoVisualHelper
    {
        // GENERAL SETTINGS

        public const float AfterimageSpacing = 8f;
        public const int AfterimageCount = 5;

        public const int YoyoAfterimageCount = 7;


        // RED HAND VISUAL SETTINGS

        private const float RedLightStrength = 0.45f;
        private const float RedDustSpacing = 10f;


        // LIGHTING

        private const float LightingInfluence = 0.20f;


        // GET LIGHTING

        private static Color GetBrightLighting(
            Vector2 position)
        {
            Color lighting =
                Lighting.GetColor(
                    (int)(position.X / 16f),
                    (int)(position.Y / 16f)
                );

            return Color.Lerp(
                Color.White,
                lighting,
                LightingInfluence
            );
        }


        // NORMAL AFTERIMAGE TRACKING

        public static void TrackAfterimages(
            Vector2[] positions,
            ref float distance,
            ref bool initialized,
            Vector2 currentPosition)
        {
            if (!initialized)
            {
                for (
                    int i = 0;
                    i < positions.Length;
                    i++
                )
                {
                    positions[i] =
                        currentPosition;
                }

                initialized = true;
                distance = 0f;

                return;
            }


            float moved =
                Vector2.Distance(
                    currentPosition,
                    positions[0]
                );


            distance += moved;


            if (
                distance >=
                AfterimageSpacing
            )
            {
                for (
                    int i =
                        positions.Length - 1;
                    i > 0;
                    i--
                )
                {
                    positions[i] =
                        positions[i - 1];
                }


                positions[0] =
                    currentPosition;


                distance = 0f;
            }
        }


        // MAIN YOYO AFTERIMAGE TRACKING

        public static void TrackYoyoAfterimages(
            Vector2[] positions,
            ref float distance,
            ref bool initialized,
            Vector2 currentPosition)
        {
            if (!initialized)
            {
                for (
                    int i = 0;
                    i < positions.Length;
                    i++
                )
                {
                    positions[i] =
                        currentPosition;
                }

                initialized = true;
                distance = 0f;

                return;
            }


            float moved =
                Vector2.Distance(
                    currentPosition,
                    positions[0]
                );


            distance += moved;


            const float YoyoSpacing = 12f;


            if (
                distance >=
                YoyoSpacing
            )
            {
                for (
                    int i =
                        positions.Length - 1;
                    i > 0;
                    i--
                )
                {
                    positions[i] =
                        positions[i - 1];
                }


                positions[0] =
                    currentPosition;


                distance = 0f;
            }
        }


        // NORMAL AFTERIMAGES

        public static void DrawAfterimages(
            Texture2D texture,
            Vector2[] positions,
            float rotation,
            float scale,
            Color glowColor)
        {
            for (
                int i =
                    positions.Length - 1;
                i >= 0;
                i--
            )
            {
                Vector2 position =
                    positions[i];


                float fade =
                    0.30f *
                    (
                        1f -
                        i /
                        (float)positions.Length
                    );


                Color lighting =
                    GetBrightLighting(
                        position
                    );


                Color color =
                    Color.Lerp(
                        Color.White,
                        lighting,
                        0.25f
                    );


                color *=
                    fade;


                Main.spriteBatch.Draw(
                    texture,
                    position -
                    Main.screenPosition,
                    null,
                    color,
                    rotation,
                    texture.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }


        // LASER AFTERIMAGES

        public static void DrawLitAfterimages(
            Texture2D texture,
            Vector2[] positions,
            float rotation,
            float scale)
        {
            for (
                int i =
                    positions.Length - 1;
                i >= 0;
                i--
            )
            {
                Vector2 position =
                    positions[i];


                float fade =
                    0.42f *
                    (
                        1f -
                        i /
                        (float)positions.Length
                    );


                Color litColor =
                    GetBrightLighting(
                        position
                    );


                litColor =
                    Color.Lerp(
                        Color.White,
                        litColor,
                        0.15f
                    );


                litColor *=
                    fade;


                Main.spriteBatch.Draw(
                    texture,
                    position -
                    Main.screenPosition,
                    null,
                    litColor,
                    rotation,
                    texture.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }


        // MAIN YOYO ABILITY AFTERIMAGES

        public static void DrawYoyoAfterimages(
            Texture2D texture,
            Vector2[] positions,
            float rotation,
            float scale)
        {
            for (
                int i =
                    positions.Length - 1;
                i >= 0;
                i--
            )
            {
                Vector2 position =
                    positions[i];


                float fade =
                    0.40f *
                    (
                        1f -
                        i /
                        (float)positions.Length
                    );


                Color litColor =
                    GetBrightLighting(
                        position
                    );


                litColor =
                    Color.Lerp(
                        Color.White,
                        litColor,
                        0.10f
                    );


                litColor *=
                    fade;


                Main.spriteBatch.Draw(
                    texture,
                    position -
                    Main.screenPosition,
                    null,
                    litColor,
                    rotation,
                    texture.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }


        // PRIME HAND RED GLOW

        public static void DrawGlow(
            Texture2D texture,
            Vector2 position,
            float rotation,
            float scale,
            Color glowColor,
            float strength = 0.3f)
        {
            /*
             * IMPORTANT:
             *
             * "texture" is intentionally NOT drawn here.
             *
             * The glow is completely independent from the hand
             * texture. This prevents the old oversized/faded hand
             * copy from appearing around the real hand.
             *
             * The actual hand should be drawn separately by the
             * projectile's PreDraw method.
             */

            Texture2D glowTexture =
                TextureAssets.Sun2.Value;


            // Prevent unused-parameter warnings from becoming annoying.
            _ = texture;
            _ = rotation;
            _ = scale;
            _ = glowColor;


            // LARGE SOFT RED HALO

            Main.spriteBatch.Draw(
                glowTexture,
                position -
                Main.screenPosition,
                null,
                new Color(
                    255,
                    25,
                    25,
                    150
                ) *
                strength,
                0f,
                glowTexture.Size() * 0.5f,
                0.16f,
                SpriteEffects.None,
                0f
            );


            // SMALLER BRIGHT CORE

            Main.spriteBatch.Draw(
                glowTexture,
                position -
                Main.screenPosition,
                null,
                new Color(
                    255,
                    70,
                    45,
                    190
                ) *
                (
                    strength *
                    0.65f
                ),
                0f,
                glowTexture.Size() * 0.5f,
                0.075f,
                SpriteEffects.None,
                0f
            );
        }


        // RED DUST

        public static void SpawnRedDust(
            Vector2 position,
            Vector2 velocity,
            ref float distance)
        {
            distance +=
                velocity.Length();


            if (
                distance <
                RedDustSpacing
            )
            {
                return;
            }


            distance = 0f;


            Vector2 dustPosition =
                position +
                Main.rand.NextVector2Circular(
                    3f,
                    3f
                );


            Vector2 dustVelocity =
                -velocity *
                0.05f;


            dustVelocity +=
                Main.rand.NextVector2Circular(
                    0.35f,
                    0.35f
                );


            Dust dust =
                Dust.NewDustPerfect(
                    dustPosition,
                    DustID.Torch,
                    dustVelocity,
                    100,
                    new Color(
                        255,
                        45,
                        35
                    ),
                    Main.rand.NextFloat(
                        0.55f,
                        0.85f
                    )
                );


            dust.noGravity = true;
        }


        // RED LIGHT

        public static void AddRedLight(
            Vector2 position)
        {
            Lighting.AddLight(
                position,
                RedLightStrength,
                0.035f,
                0.02f
            );
        }
    }
}