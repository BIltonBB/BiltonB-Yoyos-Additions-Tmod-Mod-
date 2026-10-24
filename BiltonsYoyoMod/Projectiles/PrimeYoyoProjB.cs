using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;

namespace BiltonsYoyoMod.Projectiles
{
    public class PrimeYoyoProjB : ModProjectile
    {
        private const float Gravity = 0.28f;

        private const int ExplosionTime = 24;


        private Vector2[] afterimages =
            new Vector2[PrimeYoyoVisualHelper.AfterimageCount];

        private float afterimageDistance;
        private bool afterimagesInitialized;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            // Bomb is visually 2x smaller.
            Projectile.scale = 0.5f;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;
            Projectile.tileCollide = true;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.timeLeft = 240;
        }


        // AI

        public override void AI()
        {
            // EXPLODING

            if (Projectile.localAI[0] == 1f)
            {
                Projectile.localAI[1]++;

                Projectile.velocity *= 0.75f;

                Projectile.friendly = false;


                // FIRE

                if (Main.rand.NextBool(2))
                {
                    Dust dust =
                        Dust.NewDustPerfect(
                            Projectile.Center,
                            DustID.Torch,
                            Main.rand.NextVector2Circular(
                                4f,
                                4f
                            ),
                            120,
                            default,
                            Main.rand.NextFloat(
                                0.8f,
                                1.4f
                            )
                        );

                    dust.noGravity = true;
                }


                // SMOKE

                if (Main.rand.NextBool(2))
                {
                    Dust dust =
                        Dust.NewDustPerfect(
                            Projectile.Center,
                            DustID.Smoke,
                            Main.rand.NextVector2Circular(
                                3f,
                                3f
                            ),
                            100,
                            default,
                            Main.rand.NextFloat(
                                1f,
                                1.5f
                            )
                        );

                    dust.noGravity = false;
                }


                // ELECTRIC SPARKS

                if (Main.rand.NextBool(2))
                {
                    Dust dust =
                        Dust.NewDustPerfect(
                            Projectile.Center,
                            DustID.Electric,
                            Main.rand.NextVector2Circular(
                                5f,
                                5f
                            ),
                            100,
                            default,
                            0.8f
                        );

                    dust.noGravity = true;
                }


                // LIGHTING

                Lighting.AddLight(
                    Projectile.Center,
                    0.9f,
                    0.55f,
                    0.15f
                );


                if (Projectile.localAI[1] >=
                    ExplosionTime)
                {
                    Projectile.Kill();
                }


                return;
            }


            // NORMAL FLIGHT

            Projectile.velocity.Y += Gravity;


            if (Projectile.velocity.LengthSquared() > 0.01f)
            {
                Projectile.rotation =
                    Projectile.velocity.ToRotation();
            }


            PrimeYoyoVisualHelper.TrackAfterimages(
                afterimages,
                ref afterimageDistance,
                ref afterimagesInitialized,
                Projectile.Center
            );


            // Bomb lighting while flying.
            Lighting.AddLight(
                Projectile.Center,
                0.65f,
                0.42f,
                0.12f
            );


            if (Main.rand.NextBool(3))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.YellowTorch,
                        -Projectile.velocity * 0.05f +
                        Main.rand.NextVector2Circular(
                            0.5f,
                            0.5f
                        ),
                        120,
                        default,
                        0.8f
                    );

                dust.noGravity = true;
            }
        }


        // TILE IMPACT

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            StartExplosion();

            return false;
        }


        // NPC IMPACT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            StartExplosion();
        }


        // START EXPLOSION

        private void StartExplosion()
        {
            if (Projectile.localAI[0] == 1f)
                return;


            Projectile.localAI[0] = 1f;
            Projectile.localAI[1] = 0f;


            Projectile.velocity =
                Vector2.Zero;


            Projectile.friendly =
                false;


            // SOUND

            SoundEngine.PlaySound(
                SoundID.Item14,
                Projectile.Center
            );


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                1.5f,
                0.9f,
                0.25f
            );


            // FIRE BURST

            for (int i = 0; i < 32; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Torch,
                        Main.rand.NextVector2Circular(
                            5.5f,
                            5.5f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.9f,
                            1.5f
                        )
                    );

                dust.noGravity = true;
            }


            // ELECTRIC BURST

            for (int i = 0; i < 18; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Electric,
                        Main.rand.NextVector2Circular(
                            6f,
                            6f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.1f
                        )
                    );

                dust.noGravity = true;
            }


            // SMOKE

            for (int i = 0; i < 14; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Smoke,
                        Main.rand.NextVector2Circular(
                            3f,
                            3f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            1f,
                            1.6f
                        )
                    );

                dust.noGravity = false;
            }
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/PrimeYoyoProjB"
                ).Value;


            // EXPLOSION

            if (Projectile.localAI[0] == 1f)
            {
                float progress =
                    Projectile.localAI[1] /
                    ExplosionTime;


                float alpha =
                    1f - progress;


                /*
                 * No MagicPixel rectangle.
                 *
                 * The explosion is entirely based on the bomb
                 * texture, preventing the old infinite rectangle
                 * artifact.
                 */

                float bombScale =
                    MathHelper.Lerp(
                        0.35f,
                        1.6f,
                        progress
                    );


                // OUTER GLOW

                Main.spriteBatch.Draw(
                    texture,
                    Projectile.Center -
                    Main.screenPosition,
                    null,
                    new Color(
                        255,
                        120,
                        30,
                        110
                    ) * alpha,
                    Projectile.rotation,
                    texture.Size() / 2f,
                    bombScale * 1.8f,
                    SpriteEffects.None,
                    0f
                );


                // INNER GLOW

                Main.spriteBatch.Draw(
                    texture,
                    Projectile.Center -
                    Main.screenPosition,
                    null,
                    new Color(
                        255,
                        210,
                        90,
                        180
                    ) * alpha,
                    Projectile.rotation,
                    texture.Size() / 2f,
                    bombScale * 1.25f,
                    SpriteEffects.None,
                    0f
                );


                // ACTUAL BOMB

                Main.spriteBatch.Draw(
                    texture,
                    Projectile.Center -
                    Main.screenPosition,
                    null,
                    new Color(
                        255,
                        240,
                        180,
                        255
                    ) * alpha,
                    Projectile.rotation,
                    texture.Size() / 2f,
                    bombScale,
                    SpriteEffects.None,
                    0f
                );


                return false;
            }


            // NORMAL BOMB

            PrimeYoyoVisualHelper.DrawAfterimages(
                texture,
                afterimages,
                Projectile.rotation,
                Projectile.scale,
                new Color(
                    255,
                    190,
                    70
                )
            );


            // Strong warm glow.
            PrimeYoyoVisualHelper.DrawGlow(
                texture,
                Projectile.Center,
                Projectile.rotation,
                Projectile.scale,
                new Color(
                    255,
                    190,
                    60
                ),
                0.95f
            );


            // Inner glow.
            PrimeYoyoVisualHelper.DrawGlow(
                texture,
                Projectile.Center,
                Projectile.rotation,
                Projectile.scale,
                new Color(
                    255,
                    230,
                    150
                ),
                0.45f
            );


            // Actual bomb.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                new Color(
                    255,
                    235,
                    150,
                    255
                ),
                Projectile.rotation,
                texture.Size() / 2f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}