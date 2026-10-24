using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class StringCutterFiberProjectile : ModProjectile
    {
        // SETTINGS

        /*
         * ACTUAL ONE-TIME SNAP RADIUS.
         *
         * 512 px radius
         * 1024 px diameter
         *
         * 16 px = 1 Terraria block
         *
         * 512 px = 32 blocks radius
         * 1024 px = 64 blocks across
         */
        private const float SnapRadius =
            128f;

        /*
         * The visual fiber itself still only travels 128 px.
         *
         * This value has NOTHING to do with the snap radius.
         */
        private const float MaximumTravelDistance =
            128f;

        private const int Lifetime =
            20;

        // STATE

        /*
         * Prevents the snap damage check from happening more than once
         * for this projectile instance.
         */
        private bool snapChecked;

        // DEFAULTS

        public override string Texture =>
            "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            /*
             * The fiber remains a tiny visual carrier.
             *
             * The actual snap uses a manual circular distance check,
             * so this does NOT need a giant collision box.
             */
            Projectile.width =
                4;

            Projectile.height =
                4;

            /*
             * This projectile does not use normal NPC collision.
             *
             * Its one-time snap is handled manually below.
             */
            Projectile.friendly =
                false;

            Projectile.hostile =
                false;

            Projectile.tileCollide =
                false;

            Projectile.ignoreWater =
                true;

            Projectile.penetrate =
                -1;

            Projectile.timeLeft =
                Lifetime;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            /*
             * We are doing our own NPC check, so local immunity isn't
             * needed for the fiber carrier.
             */
            Projectile.usesLocalNPCImmunity =
                false;

            Projectile.alpha =
                255;
        }

        // AI

        public override void AI()
        {
            // ONE-TIME SNAP CHECK

            if (!snapChecked)
            {
                snapChecked =
                    true;

                PerformSnapCheck();
            }

            // VISUAL FIBER MOVEMENT

            Projectile.ai[0] +=
                Projectile.velocity.Length();

            Projectile.velocity *=
                0.92f;

            Projectile.rotation =
                Projectile.velocity.ToRotation();

            SpawnFiberParticles();

            // VISUAL FIBER LIMIT

            if (
                Projectile.ai[0] >=
                MaximumTravelDistance
            )
            {
                SpawnOuterParticles();

                Projectile.Kill();
            }
        }

        // ONE-TIME SNAP CHECK

        private void PerformSnapCheck()
        {
            /*
             * In multiplayer, the owner/server should perform the
             * actual NPC modification.
             *
             * This prevents every multiplayer client from applying
             * its own copy of the effect.
             */
            if (
                Main.netMode ==
                NetmodeID.MultiplayerClient
            )
            {
                return;
            }

            float radiusSquared =
                SnapRadius *
                SnapRadius;

            /*
             * The projectile damage passed to this projectile should
             * be the original yoyo damage.
             *
             * AS deals exactly 2× that value.
             */
            int snapDamage =
                System.Math.Max(
                    1,
                    Projectile.damage * 2
                );

            // CHECK EVERY NPC ONCE

            for (
                int i = 0;
                i < Main.maxNPCs;
                i++
            )
            {
                NPC npc =
                    Main.npc[i];

                if (
                    !npc.active ||
                    npc.friendly ||
                    npc.dontTakeDamage
                )
                {
                    continue;
                }

                /*
                 * Circular radius check.
                 *
                 * This is NOT affected by the projectile's 4×4
                 * physical hitbox.
                 */
                float distanceSquared =
                    Vector2.DistanceSquared(
                        npc.Center,
                        Projectile.Center
                    );

                if (
                    distanceSquared >
                    radiusSquared
                )
                {
                    continue;
                }

                // APPLY DOUBLE YOYO DAMAGE

                NPC.HitInfo hitInfo =
                    new NPC.HitInfo
                    {
                        Damage = snapDamage,
                        Knockback = 1f,
                        HitDirection =
                            npc.Center.X >=
                            Projectile.Center.X
                                ? 1
                                : -1,
                        Crit = false
                    };

                npc.StrikeNPC(
                    hitInfo
                );

                // BLEEDING

                npc.AddBuff(
                    BuffID.Bleeding,
                    180
                );

                // HIT PARTICLES

                SpawnHitParticles(
                    npc.Center
                );
            }
        }

        // FIBER PARTICLES

        private void SpawnFiberParticles()
        {
            /*
             * Main strand particle.
             *
             * Always spawned around the current carrier position.
             */
            if (Main.rand.NextBool(2))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Blood,
                        Projectile.velocity *
                        0.12f,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.55f,
                            0.85f
                        )
                    );

                dust.noGravity =
                    true;
            }

            /*
             * Small nearby particles.
             */
            if (Main.rand.NextBool(4))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            3f,
                            3f
                        ),
                        DustID.Blood,
                        Main.rand.NextVector2Circular(
                            0.7f,
                            0.7f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.35f,
                            0.65f
                        )
                    );

                dust.noGravity =
                    true;
            }
        }

        // OUTER PARTICLES

        private void SpawnOuterParticles()
        {
            /*
             * These remain tied to the actual fiber's final position.
             *
             * They are NOT distributed using SnapRadius.
             */
            Vector2 direction =
                Projectile.velocity.SafeNormalize(
                    Vector2.UnitX
                );

            for (int i = 0; i < 2; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        direction *
                        Main.rand.NextFloat(
                            4f,
                            28f
                        ),
                        DustID.Blood,
                        direction *
                        Main.rand.NextFloat(
                            0.25f,
                            0.7f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.3f,
                            0.55f
                        )
                    );

                dust.noGravity =
                    true;
            }
        }

        // HIT PARTICLES

        private static void SpawnHitParticles(
            Vector2 position)
        {
            for (int i = 0; i < 4; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Blood,
                        Main.rand.NextVector2Circular(
                            1.5f,
                            1.5f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.5f,
                            0.9f
                        )
                    );

                dust.noGravity =
                    true;
            }
        }

        // NO CUSTOM DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            return false;
        }
    }
}