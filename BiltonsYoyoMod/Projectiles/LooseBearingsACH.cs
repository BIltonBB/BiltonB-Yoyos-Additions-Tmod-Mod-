using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class LooseBearingsACH : ModProjectile
    {
        private const float Gravity = 0.35f;
        private const float MaxFallSpeed = 16f;

        // Gentle curvature.
        private const float HomingStrength = 0.045f;

        private const float HomingRange = 500f;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;

            Projectile.DamageType =
                DamageClass.Melee;

            Projectile.aiStyle = 0;
        }

        public override void AI()
        {
            // GRAVITY

            Projectile.velocity.Y += Gravity;

            if (Projectile.velocity.Y > MaxFallSpeed)
            {
                Projectile.velocity.Y =
                    MaxFallSpeed;
            }

            // SMOOTH HOMING

            NPC target =
                FindClosestEnemy();

            if (target != null)
            {
                Vector2 toTarget =
                    target.Center -
                    Projectile.Center;

                if (toTarget.LengthSquared() >
                    0.001f)
                {
                    toTarget.Normalize();

                    Vector2 velocity =
                        Projectile.velocity;

                    float speed =
                        velocity.Length();

                    if (speed > 0.001f)
                    {
                        Vector2 currentDirection =
                            velocity / speed;

                        // Gradually rotate the current trajectory
                        // toward the target instead of blending
                        // the directions directly.
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

                        Projectile.velocity =
                            newDirection *
                            speed;
                    }
                }
            }

            // ROTATION

            Projectile.rotation +=
                Projectile.velocity.X *
                0.08f;
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
                i++)
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
                        npc.Center
                    );

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

        // TILE

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            Projectile.Kill();
            return false;
        }

        // ENEMY

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            Projectile.Kill();
        }
    }
}