using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;

namespace BiltonsYoyoMod.Projectiles
{
    public class PrimeYoyoProjPC : ModProjectile
    {
        private const float StationaryRadius = 45f;

        private const float DetectionRadius =
            PrimeYoyoHelper.DetectionRadius;

        // 160 pixels ABOVE the enemy.
        private const float TargetHeight = 160f;

        private const float MaxSpeed = 6f;
        private const float Acceleration = 0.10f;
        private const float Deceleration = 0.12f;

        // 5 seconds.
        private const int Cooldown = 300;

        private int targetIndex = -1;

        private Vector2[] afterimages =
            new Vector2[PrimeYoyoVisualHelper.AfterimageCount];

        private float afterimageDistance;
        private bool afterimagesInitialized;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 18;

            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.timeLeft = 10;

            // Half-size hand.
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


            // NO ENEMY

            if (targetIndex == -1)
            {
                Projectile.Center =
                    parent.Center +
                    new Vector2(
                        StationaryRadius,
                        StationaryRadius
                    );

                Projectile.velocity =
                    Vector2.Zero;


                float desiredRotation =
                    PrimeYoyoHelper.GetHandRotation(
                        parent.velocity
                    );


                Projectile.rotation =
                    SmoothAngle(
                        Projectile.rotation,
                        desiredRotation,
                        0.18f
                    );


                Projectile.localAI[0] = 0f;

                TrackVisuals();

                return;
            }


            // LOCK ON

            if (PrimeYoyoHelper.ValidTarget(targetIndex))
            {
                NPC target =
                    Main.npc[targetIndex];


                Vector2 targetPosition =
                    target.Center +
                    new Vector2(
                        0f,
                        -TargetHeight
                    );


                MoveSmoothly(
                    targetPosition
                );


                float desiredRotation =
                    PrimeYoyoHelper.GetHandRotation(
                        Projectile.velocity
                    );


                Projectile.rotation =
                    SmoothAngle(
                        Projectile.rotation,
                        desiredRotation,
                        0.18f
                    );
            }


            // BOMB COOLDOWN

            Projectile.localAI[0]++;


            if (Projectile.localAI[0] >= Cooldown)
            {
                Projectile.localAI[0] = 0f;


                if (Main.myPlayer == Projectile.owner)
                {
                    // FIRE 10 BOMBS

                    for (int i = 0; i < 10; i++)
                    {
                        float spread =
                            MathHelper.Lerp(
                                -MathHelper.PiOver4,
                                MathHelper.PiOver4,
                                i / 9f
                            );


                        Vector2 direction =
                            new Vector2(
                                (float)System.Math.Sin(
                                    spread
                                ),
                                -(float)System.Math.Cos(
                                    spread
                                )
                            );


                        direction.Normalize();


                        float speed =
                            Main.rand.NextFloat(
                                7f,
                                10f
                            );


                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            Projectile.Center,
                            direction * speed,
                            ModContent.ProjectileType<PrimeYoyoProjB>(),
                            Projectile.damage * 2 / 3,
                            1f,
                            Projectile.owner
                        );
                    }


                    // FIRING PARTICLES

                    for (int i = 0; i < 18; i++)
                    {
                        Dust dust =
                            Dust.NewDustPerfect(
                                Projectile.Center,
                                DustID.YellowTorch,
                                Main.rand.NextVector2Circular(
                                    3.5f,
                                    3.5f
                                ),
                                120,
                                default,
                                Main.rand.NextFloat(
                                    0.7f,
                                    1.2f
                                )
                            );


                        dust.noGravity = true;
                    }


                    // FIRING LIGHT

                    Lighting.AddLight(
                        Projectile.Center,
                        1.0f,
                        0.65f,
                        0.2f
                    );
                }
            }


            TrackVisuals();
        }


        // SMOOTH TARGET MOVEMENT

        private void MoveSmoothly(
            Vector2 destination)
        {
            Vector2 difference =
                destination -
                Projectile.Center;


            float distance =
                difference.Length();


            if (distance < 8f)
            {
                Projectile.velocity *=
                    1f - Deceleration;


                Projectile.Center +=
                    Projectile.velocity;


                if (Projectile.velocity.LengthSquared() < 0.04f)
                {
                    Projectile.velocity =
                        Vector2.Zero;
                }


                return;
            }


            difference.Normalize();


            Vector2 desiredVelocity =
                difference * MaxSpeed;


            if (distance < 100f)
            {
                float slowdown =
                    MathHelper.Clamp(
                        distance / 100f,
                        0.15f,
                        1f
                    );


                desiredVelocity *=
                    slowdown;
            }


            float smoothing =
                Projectile.velocity.Length() >
                desiredVelocity.Length()
                ? Deceleration
                : Acceleration;


            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    desiredVelocity,
                    smoothing
                );


            Projectile.Center +=
                Projectile.velocity;
        }


        // SMOOTH ROTATION

        private float SmoothAngle(
            float current,
            float target,
            float amount)
        {
            float difference =
                MathHelper.WrapAngle(
                    target - current
                );


            return current +
                   difference * amount;
        }


        // VISUAL TRACKING

        private void TrackVisuals()
        {
            PrimeYoyoVisualHelper.TrackAfterimages(
                afterimages,
                ref afterimageDistance,
                ref afterimagesInitialized,
                Projectile.Center
            );
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/PrimeYoyoProjPC"
                ).Value;


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


            PrimeYoyoVisualHelper.DrawGlow(
                texture,
                Projectile.Center,
                Projectile.rotation,
                Projectile.scale,
                new Color(
                    255,
                    190,
                    70
                ),
                0.45f
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