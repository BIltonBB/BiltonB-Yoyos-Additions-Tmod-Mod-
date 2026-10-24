using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class PrimeYoyoProjPV : ModProjectile
    {
        // SETTINGS

        private const float StationaryRadius = 45f;

        private const float DetectionRadius =
            PrimeYoyoHelper.DetectionRadius;

        private const int LungeCooldown = 300;

        /*
         * Doubled from 13.
         *
         * Since the lunge deceleration is percentage based,
         * this also approximately doubles the travel distance.
         */
        private const float LungeSpeed = 26f;

        private const float LungeDeceleration = 0.08f;

        private const float ReturnSpeed = 1.2f;

        private const float ReturnAcceleration = 0.08f;

        private const float RotationSpeed = 0.18f;


        // STATES

        private const float StateWaiting = 0f;
        private const float StateLunging = 1f;
        private const float StateReturning = 2f;


        // VARIABLES

        private int targetIndex = -1;

        private Vector2[] afterimages =
            new Vector2[PrimeYoyoVisualHelper.AfterimageCount];

        private float afterimageDistance;
        private bool afterimagesInitialized;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.timeLeft = 10;

            Projectile.scale =
                PrimeYoyoHelper.HandScale;
        }


        // AI

        public override void AI()
        {
            int parentIndex =
                (int)Projectile.ai[0];

            if (parentIndex < 0 ||
                parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent =
                Main.projectile[parentIndex];

            if (!parent.active ||
                parent.type !=
                ModContent.ProjectileType<PrimeYoyoProj>() ||
                parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 10;


            targetIndex =
                PrimeYoyoHelper.FindNearbyTarget(
                    parent.Center,
                    DetectionRadius
                );


            // LUNGING

            if (Projectile.localAI[1] ==
                StateLunging)
            {
                Projectile.Center +=
                    Projectile.velocity;

                Projectile.velocity *=
                    1f - LungeDeceleration;


                /*
                 * Face the direction of the lunge.
                 */
                if (Projectile.velocity.LengthSquared() > 0.01f)
                {
                    float targetRotation =
                        Projectile.velocity.ToRotation();

                    Projectile.rotation =
                        MathHelper.WrapAngle(
                            MathHelper.Lerp(
                                MathHelper.WrapAngle(
                                    Projectile.rotation
                                ),
                                targetRotation,
                                RotationSpeed
                            )
                        );
                }


                if (Projectile.velocity.LengthSquared() > 4f &&
                    Main.rand.NextBool(2))
                {
                    Dust dust =
                        Dust.NewDustPerfect(
                            Projectile.Center,
                            DustID.Electric,
                            -Projectile.velocity * 0.08f +
                            Main.rand.NextVector2Circular(
                                0.8f,
                                0.8f
                            ),
                            100,
                            default,
                            0.9f
                        );

                    dust.noGravity = true;
                }


                if (Projectile.velocity.LengthSquared() < 1f)
                {
                    Projectile.velocity =
                        Vector2.Zero;

                    Projectile.localAI[1] =
                        StateReturning;

                    Projectile.localAI[0] = 0f;
                }

                TrackVisuals();
                return;
            }


            // RETURNING

            if (Projectile.localAI[1] ==
                StateReturning)
            {
                Vector2 homePosition =
                    parent.Center +
                    new Vector2(
                        StationaryRadius,
                        -StationaryRadius
                    );

                Vector2 toHome =
                    homePosition -
                    Projectile.Center;


                if (toHome.LengthSquared() < 16f)
                {
                    Projectile.Center =
                        homePosition;

                    Projectile.velocity =
                        Vector2.Zero;

                    Projectile.localAI[1] =
                        StateWaiting;

                    Projectile.localAI[0] = 0f;

                    TrackVisuals();
                    return;
                }


                if (toHome.LengthSquared() > 0.01f)
                {
                    toHome.Normalize();

                    Vector2 desiredVelocity =
                        toHome * ReturnSpeed;

                    Projectile.velocity =
                        Vector2.Lerp(
                            Projectile.velocity,
                            desiredVelocity,
                            ReturnAcceleration
                        );
                }

                Projectile.Center +=
                    Projectile.velocity;


                if (Projectile.velocity.LengthSquared() > 0.01f)
                {
                    float targetRotation =
                        Projectile.velocity.ToRotation();

                    Projectile.rotation =
                        MathHelper.WrapAngle(
                            MathHelper.Lerp(
                                MathHelper.WrapAngle(
                                    Projectile.rotation
                                ),
                                targetRotation,
                                RotationSpeed
                            )
                        );
                }


                if (Main.rand.NextBool(4))
                {
                    Dust dust =
                        Dust.NewDustPerfect(
                            Projectile.Center,
                            DustID.Electric,
                            -Projectile.velocity * 0.08f,
                            100,
                            default,
                            0.6f
                        );

                    dust.noGravity = true;
                }

                TrackVisuals();
                return;
            }


            // WAITING

            /*
             * Instantly follow the main yoyo.
             *
             * This fixes the bottom-hand lag while idle.
             */
            Projectile.Center =
                parent.Center +
                new Vector2(
                    StationaryRadius,
                    -StationaryRadius
                );

            Projectile.velocity =
                Vector2.Zero;

            Projectile.localAI[0]++;


            // IDLE ROTATION

            /*
             * Only apply directional rotation while there is
             * no target.
             *
             * Once PV launches, it uses its lunge direction instead.
             */
            if (targetIndex == -1)
            {
                float desiredRotation =
                    PrimeYoyoHelper.GetHandRotation(
                        parent.velocity
                    );

                Projectile.rotation =
                    MathHelper.WrapAngle(
                        MathHelper.Lerp(
                            MathHelper.WrapAngle(
                                Projectile.rotation
                            ),
                            desiredRotation,
                            0.18f
                        )
                    );
            }


            // LAUNCH

            if (Projectile.localAI[0] >= LungeCooldown &&
                PrimeYoyoHelper.ValidTarget(targetIndex) &&
                Main.myPlayer == Projectile.owner)
            {
                NPC target =
                    Main.npc[targetIndex];

                Vector2 direction =
                    target.Center -
                    Projectile.Center;

                if (direction.LengthSquared() > 0.01f)
                {
                    direction.Normalize();

                    Projectile.velocity =
                        direction * LungeSpeed;

                    Projectile.rotation =
                        direction.ToRotation();

                    Projectile.localAI[0] = 0f;

                    Projectile.localAI[1] =
                        StateLunging;


                    // Launch burst.
                    for (int i = 0; i < 16; i++)
                    {
                        Dust dust =
                            Dust.NewDustPerfect(
                                Projectile.Center,
                                DustID.Electric,
                                -direction *
                                Main.rand.NextFloat(
                                    1f,
                                    4f
                                ) +
                                Main.rand.NextVector2Circular(
                                    2f,
                                    2f
                                ),
                                100,
                                default,
                                Main.rand.NextFloat(
                                    0.8f,
                                    1.2f
                                )
                            );

                        dust.noGravity = true;
                    }

                    Projectile.netUpdate = true;
                }
            }

            TrackVisuals();
        }


        // AFTERIMAGES

        private void TrackVisuals()
        {
            PrimeYoyoVisualHelper.TrackAfterimages(
                afterimages,
                ref afterimageDistance,
                ref afterimagesInitialized,
                Projectile.Center
            );
        }


        // HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            /*
             * Slightly slow down but don't completely cancel
             * the lunge.
             */
            Projectile.velocity *= 0.65f;

            for (int i = 0; i < 10; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Electric,
                        Main.rand.NextVector2Circular(
                            2.5f,
                            2.5f
                        ),
                        100,
                        default,
                        0.9f
                    );

                dust.noGravity = true;
            }
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/PrimeYoyoProjPV"
                ).Value;

            PrimeYoyoVisualHelper.DrawAfterimages(
                texture,
                afterimages,
                Projectile.rotation,
                Projectile.scale,
                new Color(
                    80,
                    200,
                    255
                )
            );


            float glowStrength;

            if (Projectile.localAI[1] ==
                StateLunging)
            {
                glowStrength = 0.70f;
            }
            else if (Projectile.localAI[1] ==
                     StateReturning)
            {
                glowStrength = 0.52f;
            }
            else
            {
                glowStrength = 0.42f;
            }


            PrimeYoyoVisualHelper.DrawGlow(
                texture,
                Projectile.Center,
                Projectile.rotation,
                Projectile.scale,
                new Color(
                    80,
                    200,
                    255
                ),
                glowStrength
            );


            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                lightColor,
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