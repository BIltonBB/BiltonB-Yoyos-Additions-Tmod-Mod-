using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class TwinsYoyoProjLaser : ModProjectile
    {
        private const int MaxAfterimages = 8;

        private const float AfterimageSpacing = 7f;


        private Vector2[] afterimagePositions =
            new Vector2[MaxAfterimages];

        private bool[] afterimageValid =
            new bool[MaxAfterimages];

        private int afterimageCount;

        private Vector2 lastHistoryPosition;

        private bool historyInitialized;


        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 8;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;

            Projectile.timeLeft = 120;

            Projectile.tileCollide = true;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }


        public override void AI()
        {
            Projectile.rotation =
                Projectile.velocity.ToRotation();


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                1.0f,
                0.05f,
                0.02f
            );


            // HISTORY

            if (!historyInitialized)
            {
                lastHistoryPosition =
                    Projectile.Center;

                historyInitialized = true;
            }


            float distance =
                Vector2.Distance(
                    Projectile.Center,
                    lastHistoryPosition
                );


            if (
                distance >=
                AfterimageSpacing
            )
            {
                lastHistoryPosition =
                    Projectile.Center;


                for (
                    int i = MaxAfterimages - 1;
                    i > 0;
                    i--
                )
                {
                    afterimagePositions[i] =
                        afterimagePositions[i - 1];

                    afterimageValid[i] =
                        afterimageValid[i - 1];
                }


                afterimagePositions[0] =
                    Projectile.Center;

                afterimageValid[0] =
                    true;


                afterimageCount =
                    Math.Min(
                        afterimageCount + 1,
                        MaxAfterimages
                    );
            }
        }


        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/TwinsYoyoProjLaser"
                ).Value;


            // DISTANCE-BASED AFTERIMAGES

            for (
                int i = afterimageCount - 1;
                i >= 0;
                i--
            )
            {
                if (!afterimageValid[i])
                    continue;


                float age =
                    i /
                    (float)MaxAfterimages;


                float alpha =
                    MathHelper.Lerp(
                        0.30f,
                        0.03f,
                        age
                    );


                Vector2 position =
                    afterimagePositions[i];


                Color worldLight =
                    Lighting.GetColor(
                        (int)(position.X / 16f),
                        (int)(position.Y / 16f)
                    );


                Color afterimageColor =
                    Color.Lerp(
                        Color.White,
                        worldLight,
                        0.25f
                    );


                afterimageColor *=
                    alpha;


                // SHADOW

                Main.spriteBatch.Draw(
                    texture,
                    position -
                    Main.screenPosition +
                    new Vector2(
                        2f,
                        2f
                    ),
                    null,
                    new Color(
                        60,
                        10,
                        0,
                        150
                    ) * alpha,
                    Projectile.rotation,
                    texture.Size() * 0.5f,
                    Projectile.scale,
                    SpriteEffects.None,
                    0f
                );


                // AFTERIMAGE

                Main.spriteBatch.Draw(
                    texture,
                    position -
                    Main.screenPosition,
                    null,
                    afterimageColor,
                    Projectile.rotation,
                    texture.Size() * 0.5f,
                    Projectile.scale,
                    SpriteEffects.None,
                    0f
                );
            }


            // MAIN LASER

            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                Color.Lerp(
                    Color.White,
                    lightColor,
                    0.15f
                ),
                Projectile.rotation,
                texture.Size() * 0.5f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}