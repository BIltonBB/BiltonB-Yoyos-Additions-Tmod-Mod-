using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class PrimeYoyoProjPL : ModProjectile
    {
        private const float StationaryRadius = 45f;

        private const float DetectionRadius =
            PrimeYoyoHelper.DetectionRadius;

        // 160 pixels BELOW the enemy.
        private const float TargetHeight = 160f;

        private const float MaxSpeed = 6f;
        private const float Acceleration = 0.10f;
        private const float Deceleration = 0.12f;

        /*
         * 5 seconds.
         *
         * Terraria runs at approximately 60 ticks/sec.
         */
        private const int LaserCooldown = 300;

        /*
         * Fire each laser 10 ticks apart.
         */
        private const int LaserInterval = 10;

        private const int LaserCount = 5;

        private int targetIndex = -1;

        private Vector2[] afterimages =
            new Vector2[PrimeYoyoVisualHelper.AfterimageCount];

        private float afterimageDistance;
        private bool afterimagesInitialized;


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

            Projectile.scale =
                PrimeYoyoHelper.HandScale;
        }


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
                /*
                 * Instantly follow the yoyo.
                 */
                Projectile.Center =
                    parent.Center +
                    new Vector2(
                        -StationaryRadius,
                        StationaryRadius
                    );

                Projectile.velocity =
                    Vector2.Zero;

                /*
                 * Directional rotation ALWAYS applies to PL.
                 */
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

                /*
                 * Reset laser cycle.
                 */
                Projectile.localAI[0] = 0f;
                Projectile.localAI[1] = 0f;
                Projectile.localAI[2] = 0f;

                TrackVisuals();
                return;
            }


            // LOCK ON

            if (PrimeYoyoHelper.ValidTarget(targetIndex))
            {
                NPC target =
                    Main.npc[targetIndex];

                /*
                 * Stay 160 pixels BELOW the enemy.
                 */
                Vector2 targetPosition =
                    target.Center +
                    new Vector2(
                        0f,
                        TargetHeight
                    );

                MoveSmoothly(targetPosition);

                /*
                 * PL ALWAYS uses directional rotation.
                 */
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


            // LASER SYSTEM

            Projectile.localAI[0]++;

            int fired =
                (int)Projectile.localAI[1];


            /*
             * Five lasers have been fired.
             *
             * Wait for the 5-second cooldown.
             */
            if (fired >= LaserCount)
            {
                if (Projectile.localAI[0] >= LaserCooldown)
                {
                    Projectile.localAI[0] = 0f;
                    Projectile.localAI[1] = 0f;
                    Projectile.localAI[2] = 0f;
                }

                TrackVisuals();
                return;
            }


            /*
             * Count toward the next laser.
             */
            Projectile.localAI[2]++;

            if (Projectile.localAI[2] >= LaserInterval)
            {
                Projectile.localAI[2] = 0f;

                if (Main.myPlayer == Projectile.owner &&
                    PrimeYoyoHelper.ValidTarget(targetIndex))
                {
                    NPC target =
                        Main.npc[targetIndex];

                    Vector2 direction =
                        target.Center -
                        Projectile.Center;

                    if (direction.LengthSquared() > 0.01f)
                    {
                        direction.Normalize();

                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            Projectile.Center,
                            direction * 14f,
                            ModContent.ProjectileType<PrimeYoyoProjLaser>(),
                            Projectile.damage / 2,
                            1f,
                            Projectile.owner
                        );
                    }
                }

                Projectile.localAI[1]++;
            }


            // LASER ENERGY EFFECT

            if (Main.rand.NextBool(5))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.BlueTorch,
                        Main.rand.NextVector2Circular(
                            0.5f,
                            0.5f
                        ),
                        120,
                        default,
                        0.7f
                    );

                dust.noGravity = true;
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
                    Projectile.velocity = Vector2.Zero;

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

                desiredVelocity *= slowdown;
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


        private void TrackVisuals()
        {
            PrimeYoyoVisualHelper.TrackAfterimages(
                afterimages,
                ref afterimageDistance,
                ref afterimagesInitialized,
                Projectile.Center
            );
        }


        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/PrimeYoyoProjPL"
                ).Value;

            PrimeYoyoVisualHelper.DrawAfterimages(
                texture,
                afterimages,
                Projectile.rotation,
                Projectile.scale,
                new Color(60, 170, 255)
            );

            PrimeYoyoVisualHelper.DrawGlow(
                texture,
                Projectile.Center,
                Projectile.rotation,
                Projectile.scale,
                new Color(60, 170, 255),
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