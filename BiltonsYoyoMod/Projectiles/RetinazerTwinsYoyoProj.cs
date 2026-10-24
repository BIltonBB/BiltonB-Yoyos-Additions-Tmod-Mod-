using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class RetinazerTwinsYoyoProj : ModProjectile
    {
        // RETINAZER LASER SETTINGS

        // 5 seconds between bursts
        private const int BurstCooldown = 300;

        // 3 ticks between each laser
        private const int BurstShotDelay = 3;

        // 10 lasers per burst
        private const int BurstCount = 10;

        // Maximum targeting distance
        private const float TargetRange = 700f;

        // Laser speed
        private const float LaserSpeed = 14f;


        private int burstCooldown;
        private int burstTimer;
        private int shotsLeft;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosMaximumRange[
                Projectile.type
            ] = 320f;

            ProjectileID.Sets.YoyosTopSpeed[
                Projectile.type
            ] = 16f;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[
                Projectile.type
            ] = -1f;
        }


        // AI

        public override void AI()
        {
            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                1.0f,
                0.15f,
                0.10f
            );


            // SPAWN SPEZMATISM COMPANION

            if (
                Projectile.localAI[1] == 0f &&
                Main.myPlayer == Projectile.owner
            )
            {
                Projectile.localAI[1] = 1f;

                int companionType =
                    ModContent.ProjectileType<
                        SpezmatismTwinsYoyoCompanion>();

                int companion =
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        companionType,
                        Math.Max(
                            1,
                            Projectile.damage / 2
                        ),
                        0f,
                        Projectile.owner,
                        Projectile.whoAmI
                    );

                if (companion < 0)
                {
                    Projectile.localAI[1] = 0f;
                }
            }


            // ONLY OWNER CONTROLS THE ATTACK

            if (Main.myPlayer != Projectile.owner)
                return;


            // BURST COOLDOWN

            burstCooldown++;


            // START BURST

            if (
                burstCooldown >= BurstCooldown &&
                shotsLeft <= 0
            )
            {
                burstCooldown = 0;
                burstTimer = 0;
                shotsLeft = BurstCount;
            }


            // FIRE BURST

            if (shotsLeft > 0)
            {
                burstTimer++;

                if (burstTimer >= BurstShotDelay)
                {
                    burstTimer = 0;

                    // Try to fire at a target.
                    if (FireLaser())
                    {
                        // Only consume the shot if a laser
                        // was actually created.
                        shotsLeft--;
                    }
                }
            }
        }


        // FIND TARGET + FIRE LASER

        private bool FireLaser()
        {
            NPC target = FindClosestTarget();

            if (target == null)
                return false;


            Vector2 direction =
                target.Center -
                Projectile.Center;


            if (direction.LengthSquared() <= 0.01f)
                return false;


            direction.Normalize();


            // LASER DAMAGE

            int laserDamage =
                Math.Max(
                    1,
                    Projectile.damage
                );


            // CREATE LASER

            int laser =
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    direction * LaserSpeed,
                    ModContent.ProjectileType<
                        TwinsYoyoProjLaser>(),
                    laserDamage,
                    0f,
                    Projectile.owner
                );


            if (laser < 0)
                return false;


            // LASER SPAWN EFFECT

            for (int i = 0; i < 8; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.RedTorch,
                        Main.rand.NextVector2Circular(
                            1.5f,
                            1.5f
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


            return true;
        }


        // FIND CLOSEST VALID NPC

        private NPC FindClosestTarget()
        {
            NPC closest = null;

            float closestDistanceSquared =
                TargetRange * TargetRange;


            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];


                // Valid enemy?

                if (!npc.active)
                    continue;

                if (npc.friendly)
                    continue;

                if (npc.dontTakeDamage)
                    continue;

                if (npc.life <= 0)
                    continue;


                // Distance

                float distanceSquared =
                    Vector2.DistanceSquared(
                        Projectile.Center,
                        npc.Center
                    );


                if (
                    distanceSquared <
                    closestDistanceSquared
                )
                {
                    closestDistanceSquared =
                        distanceSquared;

                    closest = npc;
                }
            }


            return closest;
        }
    }
}