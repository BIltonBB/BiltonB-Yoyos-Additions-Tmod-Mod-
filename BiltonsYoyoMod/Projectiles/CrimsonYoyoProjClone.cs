using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class CrimsonYoyoProjClone : ModProjectile
    {
        private const float CircleRadius = 65f;

        // Movement tuning
        private const float MoveSpeed = 8f;
        private const float IdleSpeed = 6f;
        private const float Acceleration = 0.12f;
        private const float IdleThreshold = 1.0f;

        // Used to create the slow overshooting motion.
        private const float OvershootAmount = 45f;
        private const float OscillationSpeed = 0.045f;

        // Base orbit speed, now 2x the previous speed.
        private const float IdleOrbitSpeed = 0.016f;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = 0; // Custom AI so vanilla yoyo AI doesn't overwrite variables or kill clone
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false; // Clones pass through tiles around main yoyo

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.scale = 1f;
            Projectile.alpha = 128; // 50% transparent
        }

        public override void AI()
        {
            int parentIndex = (int)Projectile.ai[1];

            // Verify main yoyo exists and is active
            if (parentIndex >= 0 && parentIndex < Main.maxProjectiles)
            {
                Projectile parent = Main.projectile[parentIndex];

                if (parent.active &&
                    parent.type == ModContent.ProjectileType<CrimsonYoyoProj>() &&
                    parent.owner == Projectile.owner)
                {
                    Projectile.timeLeft = 10;

                    /*
                     * ai[0] = clone's permanent random angle.
                     * ai[1] = parent projectile index.
                     * ai[2] = idle orbit angle.
                     *
                     * localAI[0] = moving oscillation phase.
                     * localAI[1] = initialized flag.
                     */

                    float assignedAngle = Projectile.ai[0];

                    // Every clone has a permanent point on the same circle.
                    Vector2 assignedOffset =
                        assignedAngle.ToRotationVector2() * CircleRadius;

                    Vector2 assignedPosition =
                        parent.Center + assignedOffset;

                    float parentSpeed = parent.velocity.Length();

                    if (Projectile.localAI[1] == 0f)
                    {
                        Projectile.localAI[1] = 1f;

                        // Start the clone at the main yoyo.
                        Projectile.Center = parent.Center;

                        // Start with no artificial movement.
                        Projectile.velocity = Vector2.Zero;

                        // Give every clone a different starting phase
                        // based on its permanent random angle.
                        Projectile.localAI[0] = assignedAngle;

                        // Start the orbit at its assigned point.
                        Projectile.ai[2] = assignedAngle;
                    }

                    float phase = Projectile.localAI[0];

                    if (parentSpeed > IdleThreshold)
                    {
                        /*
                         * The yoyo is moving.
                         *
                         * Create a moving target that goes:
                         *
                         * parent -> overshoot -> parent -> overshoot
                         */

                        Vector2 direction = parent.velocity;

                        if (direction.LengthSquared() > 0.001f)
                            direction.Normalize();
                        else
                            direction = Vector2.UnitX;

                        phase += OscillationSpeed;

                        if (phase > MathHelper.TwoPi)
                            phase -= MathHelper.TwoPi;

                        Projectile.localAI[0] = phase;

                        float wave =
                            (float)System.Math.Sin(phase);

                        Vector2 movingTarget =
                            parent.Center +
                            direction * (wave * OvershootAmount);

                        // Smoothly accelerate toward the moving target.
                        Vector2 desiredVelocity =
                            movingTarget - Projectile.Center;

                        if (desiredVelocity.LengthSquared() > 0.01f)
                        {
                            desiredVelocity.Normalize();
                            desiredVelocity *= MoveSpeed;
                        }

                        Projectile.velocity = Vector2.Lerp(
                            Projectile.velocity,
                            desiredVelocity,
                            Acceleration
                        );

                        Projectile.Center += Projectile.velocity;
                    }
                    else
                    {
                        /*
                         * The yoyo is idle/barely moving.
                         *
                         * First, smoothly move the clone to its current
                         * orbital position around the ORIGINAL YOYO.
                         */

                        float orbitAngle = Projectile.ai[2];

                        Vector2 orbitOffset =
                            orbitAngle.ToRotationVector2() * CircleRadius;

                        Vector2 orbitPosition =
                            parent.Center + orbitOffset;

                        Vector2 toOrbitPosition =
                            orbitPosition - Projectile.Center;

                        /*
                         * If the clone is not close enough to the circle,
                         * smoothly move toward its orbital position.
                         */
                        if (toOrbitPosition.Length() > 5f)
                        {
                            Vector2 desiredVelocity =
                                toOrbitPosition;

                            desiredVelocity.Normalize();
                            desiredVelocity *= IdleSpeed;

                            Projectile.velocity = Vector2.Lerp(
                                Projectile.velocity,
                                desiredVelocity,
                                Acceleration
                            );

                            Projectile.Center += Projectile.velocity;
                        }
                        else
                        {
                            /*
                             * The clone has reached the circular path.
                             *
                             * Give each clone a different orbit speed.
                             *
                             * The assigned angle is used to deterministically
                             * generate a different multiplier for each clone.
                             *
                             * No randomization happens here, so the speed
                             * stays consistent for the entire yoyo throw.
                             */
                            float speedMultiplier =
                                0.5f +
                                (assignedAngle % MathHelper.TwoPi)
                                / MathHelper.TwoPi;

                            float orbitSpeed =
                                IdleOrbitSpeed * speedMultiplier;

                            orbitAngle += orbitSpeed;

                            if (orbitAngle > MathHelper.TwoPi)
                                orbitAngle -= MathHelper.TwoPi;

                            Projectile.ai[2] = orbitAngle;

                            orbitOffset =
                                orbitAngle.ToRotationVector2() * CircleRadius;

                            orbitPosition =
                                parent.Center + orbitOffset;

                            /*
                             * Smoothly follow the orbital position.
                             *
                             * The orbit position is ALWAYS relative to
                             * parent.Center, so the clones orbit around
                             * the ORIGINAL YOYO, not themselves.
                             */
                            Vector2 orbitMovement =
                                orbitPosition - Projectile.Center;

                            Projectile.Center = Vector2.Lerp(
                                Projectile.Center,
                                orbitPosition,
                                0.08f
                            );

                            Projectile.velocity =
                                orbitMovement * 0.08f;
                        }
                    }

                    return;
                }
            }

            // Despawn clone if main yoyo returns or is destroyed
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Draw yoyo string connecting from player to this clone
            Player owner = Main.player[Projectile.owner];

            if (owner.active)
            {
                Vector2 mountedCenter = owner.MountedCenter;
                Vector2 cloneCenter = Projectile.Center;

                // Crimson-red string with 50% transparency
                Color stringColor =
                    new Color(200, 50, 50, 150) * 0.5f;

                Utils.DrawLine(
                    Main.spriteBatch,
                    mountedCenter,
                    cloneCenter,
                    stringColor,
                    stringColor,
                    1.5f
                );
            }

            return true; // Draw clone sprite
        }

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            target.AddBuff(BuffID.Ichor, 300);
        }
    }
}