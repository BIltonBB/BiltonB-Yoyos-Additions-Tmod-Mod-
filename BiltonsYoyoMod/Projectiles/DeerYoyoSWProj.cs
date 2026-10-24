using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class DeerYoyoSWProj : ModProjectile
    {
        // SETTINGS

        private const int Lifetime = 420;
        private const int FadeTime = 60;

        // KEEPING THE ORIGINAL DEER RADIUS.
        private const float CircleRadius = 30f;

        // 75% transparent = 25% visible.
        private const float CircleOpacity = 0.25f;

        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.timeLeft = Lifetime;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.penetrate = -1;
        }

        // AI

        public override void AI()
        {
            // OUTER ICE PARTICLES

            if (Main.rand.NextBool(2))
            {
                float angle =
                    Main.rand.NextFloat(
                        MathHelper.TwoPi
                    );

                Vector2 direction =
                    angle.ToRotationVector2();

                Vector2 position =
                    Projectile.Center +
                    direction *
                    (
                        CircleRadius +
                        Main.rand.NextFloat(
                            -2f,
                            4f
                        )
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.IceTorch,
                        direction *
                        Main.rand.NextFloat(
                            0.15f,
                            0.9f
                        ),
                        70,
                        default,
                        Main.rand.NextFloat(
                            0.65f,
                            1.2f
                        )
                    );

                dust.noGravity = true;
            }

            // INNER FROST PARTICLES

            if (Main.rand.NextBool(3))
            {
                Vector2 direction =
                    Main.rand.NextVector2Unit();

                Vector2 position =
                    Projectile.Center +
                    direction *
                    Main.rand.NextFloat(
                        5f,
                        CircleRadius - 3f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Frost,
                        Main.rand.NextVector2Circular(
                            0.35f,
                            0.35f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.4f,
                            0.9f
                        )
                    );

                dust.noGravity = true;
            }

            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.08f,
                0.55f,
                0.95f
            );
        }

        // HIT NPC

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            target.AddBuff(
                BuffID.Frostburn,
                180
            );
        }

        // FADE

        private void GetVisualValues(
            out float scale,
            out float alpha)
        {
            // Fade in.
            if (Projectile.timeLeft > 360)
            {
                float progress =
                    (
                        Lifetime -
                        Projectile.timeLeft
                    ) /
                    (float)FadeTime;

                scale =
                    MathHelper.Lerp(
                        0.05f,
                        1f,
                        progress
                    );

                alpha = progress;
            }

            // Fade out.
            else if (Projectile.timeLeft <= 60)
            {
                float progress =
                    Projectile.timeLeft /
                    (float)FadeTime;

                scale =
                    MathHelper.Lerp(
                        0.05f,
                        1f,
                        progress
                    );

                alpha = progress;
            }

            // Fully visible.
            else
            {
                scale = 1f;
                alpha = 1f;
            }
        }

        // DRAW FILLED CIRCLE

        private void DrawFilledCircle(
            Texture2D pixel,
            Vector2 center,
            float radius,
            float opacity)
        {
            int minY =
                (int)Math.Floor(-radius);

            int maxY =
                (int)Math.Ceiling(radius);

            Color circleColor =
                new Color(
                    150,
                    225,
                    255,
                    255
                ) * opacity;

            for (int y = minY; y <= maxY; y++)
            {
                float ySquared =
                    y * y;

                float remaining =
                    radius * radius -
                    ySquared;

                if (remaining <= 0f)
                    continue;

                float halfWidth =
                    (float)Math.Sqrt(
                        remaining
                    );

                Main.spriteBatch.Draw(
                    pixel,
                    center +
                    new Vector2(
                        -halfWidth,
                        y
                    ),
                    new Rectangle(
                        0,
                        0,
                        1,
                        1
                    ),
                    circleColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(
                        halfWidth * 2f,
                        1f
                    ),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D pixel =
                TextureAssets.MagicPixel.Value;

            Vector2 center =
                Projectile.Center -
                Main.screenPosition;

            float scale;
            float fadeAlpha;

            GetVisualValues(
                out scale,
                out fadeAlpha
            );

            float radius =
                CircleRadius *
                scale;

            // 75% TRANSPARENT LIGHT-BLUE CIRCLE

            // Base opacity = 25% visible.
            //
            // fadeAlpha then controls the fade-in/fade-out.
            float finalOpacity =
                CircleOpacity *
                fadeAlpha;

            DrawFilledCircle(
                pixel,
                center,
                radius,
                finalOpacity
            );

            return false;
        }
    }
}