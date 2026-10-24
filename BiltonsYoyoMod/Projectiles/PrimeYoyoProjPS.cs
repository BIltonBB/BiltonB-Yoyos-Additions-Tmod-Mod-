using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;

namespace BiltonsYoyoMod.Projectiles
{
    public class PrimeYoyoProjPS : ModProjectile
    {
        private const float StationaryRadius = 45f;

        private const float DetectionRadius =
            PrimeYoyoHelper.DetectionRadius;

        private const float MaxSpeed = 7f;
        private const float Acceleration = 0.07f;

        // 1 second.
        private const int HitRecoveryTime = 60;


        private int targetIndex = -1;

        private Vector2[] afterimages =
            new Vector2[PrimeYoyoVisualHelper.AfterimageCount];

        private float afterimageDistance;
        private bool afterimagesInitialized;


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


            // HIT RECOVERY

            if (Projectile.localAI[0] > 0f)
            {
                Projectile.localAI[0]--;

                Projectile.velocity =
                    Vector2.Zero;

                /*
                 * Stay completely still for the recovery period.
                 */
                if (Projectile.localAI[0] <= 0f)
                {
                    Projectile.localAI[0] = 0f;
                }

                TrackVisuals();
                return;
            }


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
                        -StationaryRadius
                    );

                Projectile.velocity =
                    Vector2.Zero;

                /*
                 * Idle directional rotation.
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

                TrackVisuals();
                return;
            }


            // CHASING

            if (PrimeYoyoHelper.ValidTarget(targetIndex))
            {
                NPC target =
                    Main.npc[targetIndex];

                Vector2 direction =
                    target.Center -
                    Projectile.Center;

                if (direction.LengthSquared() > 0.01f)
                {
                    direction.Normalize();

                    Vector2 desiredVelocity =
                        direction * MaxSpeed;

                    Projectile.velocity =
                        Vector2.Lerp(
                            Projectile.velocity,
                            desiredVelocity,
                            Acceleration
                        );

                    Projectile.Center +=
                        Projectile.velocity;
                }

                /*
                 * PS now FACES the enemy.
                 *
                 * No more spinning while chasing.
                 */
                if (direction.LengthSquared() > 0.01f)
                {
                    float desiredRotation =
                        direction.ToRotation();

                    Projectile.rotation =
                        SmoothAngle(
                            Projectile.rotation,
                            desiredRotation,
                            0.18f
                        );
                }


                if (Projectile.velocity.LengthSquared() > 4f &&
                    Main.rand.NextBool(3))
                {
                    Dust dust =
                        Dust.NewDustPerfect(
                            Projectile.Center,
                            DustID.SilverCoin,
                            -Projectile.velocity * 0.05f,
                            100,
                            default,
                            0.7f
                        );

                    dust.noGravity = true;
                }
            }
            else
            {
                Projectile.velocity *= 0.9f;
                Projectile.Center +=
                    Projectile.velocity;
            }

            TrackVisuals();
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


        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            /*
             * Slow down on impact.
             */
            Projectile.velocity *= 0.35f;

            /*
             * Then completely stop for one second.
             */
            Projectile.localAI[0] =
                HitRecoveryTime;


            for (int i = 0; i < 10; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.SilverCoin,
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


        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/PrimeYoyoProjPS"
                ).Value;

            PrimeYoyoVisualHelper.DrawAfterimages(
                texture,
                afterimages,
                Projectile.rotation,
                Projectile.scale,
                new Color(220, 220, 220)
            );

            PrimeYoyoVisualHelper.DrawGlow(
                texture,
                Projectile.Center,
                Projectile.rotation,
                Projectile.scale,
                new Color(230, 230, 240),
                0.32f
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