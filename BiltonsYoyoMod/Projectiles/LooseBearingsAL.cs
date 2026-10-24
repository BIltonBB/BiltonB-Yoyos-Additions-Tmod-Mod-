using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class LooseBearingsAL : ModProjectile
    {
        // HOMING

        // Extremely weak homing.
        private const float HomingStrength = 0.012f;

        private const float HomingRange = 400f;

        // Prevent excessive movement speed.
        private const float MaxSpeed = 18f;

        // BOUNCE

        // Keep almost all momentum after bouncing.
        private const float HorizontalBounceRetention = 0.98f;
        private const float VerticalBounceRetention = 0.97f;

        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.friendly = true;
            Projectile.hostile = false;

            // Luminite never breaks from enemy hits.
            Projectile.penetrate = -1;

            Projectile.timeLeft = 600;

            Projectile.DamageType =
                DamageClass.Melee;

            Projectile.aiStyle = 0;
        }

        // AI

        public override void AI()
        {
            // GRAVITY

            Projectile.velocity.Y += 0.35f;

            if (Projectile.velocity.Y > 18f)
                Projectile.velocity.Y = 18f;

            // VERY WEAK SMOOTH HOMING

            NPC target =
                FindClosestEnemy();

            if (target != null)
            {
                Vector2 velocityBeforeHoming =
                    Projectile.velocity;

                float speed =
                    velocityBeforeHoming.Length();

                if (speed > 0.001f)
                {
                    Vector2 currentDirection =
                        velocityBeforeHoming / speed;

                    Vector2 toTarget =
                        target.Center -
                        Projectile.Center;

                    if (toTarget.LengthSquared() >
                        0.001f)
                    {
                        toTarget.Normalize();

                        float currentAngle =
                            currentDirection.ToRotation();

                        float targetAngle =
                            toTarget.ToRotation();

                        float angleDifference =
                            MathHelper.WrapAngle(
                                targetAngle -
                                currentAngle);

                        float turnAmount =
                            angleDifference *
                            HomingStrength;

                        float newAngle =
                            currentAngle +
                            turnAmount;

                        Vector2 newDirection =
                            newAngle.ToRotationVector2();

                        // Preserve the exact current speed.
                        Projectile.velocity =
                            newDirection *
                            speed;
                    }
                }
            }

            // SPEED LIMIT

            float currentSpeed =
                Projectile.velocity.Length();

            if (currentSpeed > MaxSpeed)
            {
                Projectile.velocity =
                    Vector2.Normalize(
                        Projectile.velocity) *
                    MaxSpeed;
            }

            // ROTATION

            Projectile.rotation +=
                Projectile.velocity.X *
                0.18f;
        }

        // FIND CLOSEST ENEMY

        private NPC FindClosestEnemy()
        {
            NPC closest = null;

            float closestDistanceSquared =
                HomingRange *
                HomingRange;

            for (
                int i = 0;
                i < Main.maxNPCs;
                i++
            )
            {
                NPC npc =
                    Main.npc[i];

                if (!npc.active)
                    continue;

                if (npc.friendly)
                    continue;

                if (npc.dontTakeDamage)
                    continue;

                if (npc.life <= 0)
                    continue;

                float distanceSquared =
                    Vector2.DistanceSquared(
                        Projectile.Center,
                        npc.Center);

                if (
                    distanceSquared >=
                    closestDistanceSquared
                )
                {
                    continue;
                }

                closestDistanceSquared =
                    distanceSquared;

                closest = npc;
            }

            return closest;
        }

        // TILE COLLISION

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            // HORIZONTAL BOUNCE

            if (
                Projectile.velocity.X !=
                oldVelocity.X
            )
            {
                Projectile.velocity.X =
                    -oldVelocity.X *
                    HorizontalBounceRetention;
            }

            // VERTICAL BOUNCE

            if (
                Projectile.velocity.Y !=
                oldVelocity.Y
            )
            {
                Projectile.velocity.Y =
                    -oldVelocity.Y *
                    VerticalBounceRetention;
            }

            return false;
        }

        // ENEMY

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            // Luminite bearings do not break from enemy hits.
        }
    }
}