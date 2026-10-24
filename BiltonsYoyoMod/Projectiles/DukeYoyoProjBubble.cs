using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class DukeYoyoProjBubble : ModProjectile
    {
        // SETTINGS

        private const int Lifetime = 180;

        private const float BaseScale = 0.85f;
        private const float BreathingAmount = 0.08f;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.penetrate = 1;

            Projectile.timeLeft = Lifetime;

            Projectile.alpha = 80;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }


        // AI

        public override void AI()
        {
            // FLOATING MOTION

            Projectile.velocity *= 0.98f;

            Projectile.velocity.Y -= 0.005f;


            // ROTATION

            Projectile.rotation +=
                Projectile.velocity.X * 0.015f;


            // BREATHING

            Projectile.scale =
                BaseScale +
                (
                    (float)Math.Sin(
                        Main.GameUpdateCount * 0.08f
                    ) *
                    BreathingAmount
                );


            // SMALL RANDOM WOBBLE

            Projectile.velocity.X +=
                (float)Math.Sin(
                    Main.GameUpdateCount * 0.045f +
                    Projectile.whoAmI
                ) * 0.0025f;


            // BUBBLE PARTICLES

            if (Main.rand.NextBool(10))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            4f,
                            4f
                        ),
                        DustID.Water,
                        Main.rand.NextVector2Circular(
                            0.3f,
                            0.3f
                        ),
                        150,
                        default,
                        Main.rand.NextFloat(
                            0.35f,
                            0.55f
                        )
                    );

                dust.noGravity = true;
            }


            // OCCASIONAL LITTLE AIR BUBBLE

            if (Main.rand.NextBool(30))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        new Vector2(
                            Main.rand.NextFloat(-3f, 3f),
                            3f
                        ),
                        DustID.Water,
                        new Vector2(
                            Main.rand.NextFloat(-0.2f, 0.2f),
                            Main.rand.NextFloat(-0.8f, -0.3f)
                        ),
                        170,
                        default,
                        Main.rand.NextFloat(
                            0.25f,
                            0.45f
                        )
                    );

                dust.noGravity = true;
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


            float radius =
                6f *
                Projectile.scale;


            // OUTER BUBBLE

            const int segments = 16;


            for (
                int i = 0;
                i < segments;
                i++
            )
            {
                float angle1 =
                    MathHelper.TwoPi *
                    i /
                    segments;


                float angle2 =
                    MathHelper.TwoPi *
                    (i + 1) /
                    segments;


                Vector2 start =
                    center +
                    angle1.ToRotationVector2() *
                    radius;


                Vector2 end =
                    center +
                    angle2.ToRotationVector2() *
                    radius;


                DrawLine(
                    pixel,
                    start,
                    end,
                    1.1f,
                    new Color(
                        170,
                        220,
                        235,
                        135
                    )
                );
            }


            // INNER FAINT RING

            float innerRadius =
                radius * 0.72f;


            for (
                int i = 0;
                i < segments;
                i++
            )
            {
                float angle1 =
                    MathHelper.TwoPi *
                    i /
                    segments;


                float angle2 =
                    MathHelper.TwoPi *
                    (i + 1) /
                    segments;


                Vector2 start =
                    center +
                    angle1.ToRotationVector2() *
                    innerRadius;


                Vector2 end =
                    center +
                    angle2.ToRotationVector2() *
                    innerRadius;


                DrawLine(
                    pixel,
                    start,
                    end,
                    0.45f,
                    new Color(
                        120,
                        190,
                        210,
                        45
                    )
                );
            }


            // BUBBLE HIGHLIGHT

            Vector2 highlight =
                center +
                new Vector2(
                    -radius * 0.38f,
                    -radius * 0.38f
                );


            DrawLine(
                pixel,
                highlight,
                highlight +
                new Vector2(
                    2.2f,
                    2.2f
                ),
                1.3f,
                new Color(
                    225,
                    245,
                    250,
                    165
                )
            );


            // Small secondary highlight.

            Vector2 smallHighlight =
                center +
                new Vector2(
                    radius * 0.25f,
                    -radius * 0.30f
                );


            DrawLine(
                pixel,
                smallHighlight,
                smallHighlight +
                new Vector2(
                    0.9f,
                    0.9f
                ),
                0.8f,
                new Color(
                    200,
                    235,
                    245,
                    100
                )
            );


            return false;
        }


        // POP EFFECT

        public override void OnKill(
            int timeLeft)
        {
            Vector2 center =
                Projectile.Center;


            // LARGE POP RING

            for (
                int i = 0;
                i < 20;
                i++
            )
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    20f;


                Vector2 direction =
                    angle.ToRotationVector2();


                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Water,
                        direction *
                        Main.rand.NextFloat(
                            2.5f,
                            4.5f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.3f
                        )
                    );


                dust.noGravity = true;
            }


            // RANDOM SPLASH DROPLETS

            for (
                int i = 0;
                i < 14;
                i++
            )
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        3.5f,
                        3.5f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Water,
                        velocity,
                        130,
                        default,
                        Main.rand.NextFloat(
                            0.5f,
                            1.1f
                        )
                    );


                dust.noGravity = true;
            }


            // SMALL INNER BURST

            for (
                int i = 0;
                i < 8;
                i++
            )
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        1.5f,
                        1.5f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Water,
                        velocity,
                        150,
                        default,
                        Main.rand.NextFloat(
                            0.3f,
                            0.65f
                        )
                    );


                dust.noGravity = true;
            }


            // POP SOUND

            Terraria.Audio.SoundEngine.PlaySound(
                SoundID.Item10,
                center
            );
        }


        // LINE

        private void DrawLine(
            Texture2D texture,
            Vector2 start,
            Vector2 end,
            float width,
            Color color)
        {
            Vector2 difference =
                end - start;


            float length =
                difference.Length();


            if (length <= 0.01f)
            {
                return;
            }


            Vector2 midpoint =
                (start + end) * 0.5f;


            Main.spriteBatch.Draw(
                texture,
                midpoint,
                new Rectangle(
                    0,
                    0,
                    1,
                    1
                ),
                color,
                difference.ToRotation(),
                new Vector2(
                    0.5f,
                    0.5f
                ),
                new Vector2(
                    length,
                    width
                ),
                SpriteEffects.None,
                0f
            );
        }
    }
}