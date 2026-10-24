using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class HallowYoyoProj : ModProjectile
    {
        // SLIME SETTINGS

        private const int SlimeCount = 3;

        private const float SlimeOrbitRadius = 60f;

        // 300 ticks = 5 seconds
        private const int SlimeRegenerationTime = 300;


        // SKY PROJECTILE SETTINGS

        // 60 ticks = 1 second
        private const int SkyShotTimerMax = 60;

        // 45 degree spread
        private const float SkyShotSpread =
            MathHelper.PiOver4;

        // Launch speed
        private const float SkyShotSpeed = 10f;


        // PLAYER HIT DETECTION

        private int lastPlayerLife;

        private bool playerLifeInitialized;


        // SLIME RESPAWN

        private readonly int[] slimeRespawnTimers =
            new int[SlimeCount];

        private bool slimesInitialized;


        // SKY SHOT TIMER

        private int skyShotTimer;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle =
                ProjAIStyleID.Yoyo;

            Projectile.friendly = true;

            Projectile.penetrate = -1;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosMaximumRange[
                Projectile.type
            ] = 320f;

            ProjectileID.Sets.YoyosTopSpeed[
                Projectile.type
            ] = 10f;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[
                Projectile.type
            ] = -1f;
        }


        // AI

        public override void AI()
        {
            Lighting.AddLight(
                Projectile.Center,
                0.70f,
                0.85f,
                1.00f
            );


            // Only the owner controls the slime and sky-shot systems.
            if (
                Main.myPlayer !=
                Projectile.owner
            )
            {
                return;
            }


            Player player =
                Main.player[Projectile.owner];


            if (
                player == null ||
                !player.active
            )
            {
                return;
            }


            // SKY PROJECTILE TIMER

            skyShotTimer++;


            if (
                skyShotTimer >=
                SkyShotTimerMax
            )
            {
                skyShotTimer = 0;

                FireRandomSkyProjectile();
            }


            // INITIALIZE PLAYER LIFE

            if (!playerLifeInitialized)
            {
                lastPlayerLife =
                    player.statLife;

                playerLifeInitialized = true;
            }


            // DETECT PLAYER DAMAGE

            if (
                player.statLife <
                lastPlayerLife
            )
            {
                TriggerSlimeAttack();
            }

            lastPlayerLife =
                player.statLife;


            // SLIME INITIALIZATION

            if (!slimesInitialized)
            {
                slimesInitialized = true;

                for (
                    int slot = 0;
                    slot < SlimeCount;
                    slot++
                )
                {
                    slimeRespawnTimers[slot] = 0;
                }
            }


            // UPDATE SLIMES

            UpdateSlimeSpawning();
        }


        // RANDOM SKY PROJECTILE

        private void FireRandomSkyProjectile()
        {
            /*
             * 50/50 chance:
             *
             * true  = HallowYoyoCSProj
             * false = HallowYoyoCProj
             */

            bool fireCS =
                Main.rand.NextBool();


            int projectileType;


            if (fireCS)
            {
                projectileType =
                    ModContent.ProjectileType<
                        HallowYoyoCSProj>();
            }
            else
            {
                projectileType =
                    ModContent.ProjectileType<
                        HallowYoyoCProj>();
            }


            // RANDOM UPWARD ANGLE

            /*
             * Start pointing straight upward.
             *
             * Then randomly rotate between:
             *
             * -45 degrees
             *    |
             *    0
             * +45 degrees
             */

            float randomAngle =
                Main.rand.NextFloat(
                    -SkyShotSpread,
                    SkyShotSpread
                );


            Vector2 direction =
                -Vector2.UnitY;


            direction =
                direction.RotatedBy(
                    randomAngle
                );


            Vector2 velocity =
                direction *
                SkyShotSpeed;


            // DAMAGE

            int damage =
                Math.Max(
                    1,
                    Projectile.damage / 2
                );


            // SPAWN

            Projectile shot =
                Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    projectileType,
                    damage,
                    Projectile.knockBack,
                    Projectile.owner
                );


            shot.netUpdate = true;


            // SMALL LAUNCH EFFECT

            for (int i = 0; i < 8; i++)
            {
                Vector2 dustVelocity =
                    Main.rand.NextVector2Circular(
                        2f,
                        2f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkCrystalShard,
                        dustVelocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1.1f
                        )
                    );


                dust.noGravity = true;
            }


            Lighting.AddLight(
                Projectile.Center,
                0.8f,
                0.35f,
                0.9f
            );
        }


        // UPDATE SLIME SPAWNING

        private void UpdateSlimeSpawning()
        {
            for (
                int slot = 0;
                slot < SlimeCount;
                slot++
            )
            {
                int slimeIndex =
                    FindSlime(slot);


                // SLIME EXISTS

                if (slimeIndex != -1)
                {
                    continue;
                }


                // REGENERATION TIMER

                if (
                    slimeRespawnTimers[slot] > 0
                )
                {
                    slimeRespawnTimers[slot]--;

                    continue;
                }


                // SPAWN

                SpawnSlime(slot);
            }
        }


        // SPAWN SLIME

        private void SpawnSlime(int slot)
        {
            if (
                slot < 0 ||
                slot >= SlimeCount
            )
            {
                return;
            }


            if (
                FindSlime(slot) != -1
            )
            {
                return;
            }


            float angle =
                MathHelper.TwoPi *
                slot /
                SlimeCount;


            Vector2 spawnPosition =
                Projectile.Center +
                angle.ToRotationVector2() *
                SlimeOrbitRadius;


            Projectile slime =
                Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    spawnPosition,
                    Vector2.Zero,
                    ModContent.ProjectileType<
                        HallowYoyoProjS>(),
                    Math.Max(
                        1,
                        Projectile.damage / 2
                    ),
                    Projectile.knockBack,
                    Projectile.owner,
                    Projectile.whoAmI,
                    slot
                );


            slime.netUpdate = true;
        }


        // FIND SLIME

        public int FindSlime(int slot)
        {
            int slimeType =
                ModContent.ProjectileType<
                    HallowYoyoProjS>();


            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile slime =
                    Main.projectile[i];


                if (
                    !slime.active ||
                    slime.type != slimeType ||
                    slime.owner != Projectile.owner
                )
                {
                    continue;
                }


                if (
                    (int)slime.ai[0] !=
                    Projectile.whoAmI
                )
                {
                    continue;
                }


                if (
                    (int)slime.ai[1] !=
                    slot
                )
                {
                    continue;
                }


                return i;
            }


            return -1;
        }


        // PLAYER HIT → SLIME ATTACK

        private void TriggerSlimeAttack()
        {
            int nearestTarget =
                FindNearestTarget(
                    Projectile.Center,
                    500f
                );


            if (nearestTarget == -1)
            {
                return;
            }


            for (
                int slot = 0;
                slot < SlimeCount;
                slot++
            )
            {
                int slimeIndex =
                    FindSlime(slot);


                if (slimeIndex == -1)
                {
                    continue;
                }


                Projectile slimeProjectile =
                    Main.projectile[slimeIndex];


                if (
                    slimeProjectile.ModProjectile
                    is HallowYoyoProjS slime
                )
                {
                    slime.StartAttack(
                        nearestTarget
                    );
                }
            }
        }


        // FIND NEAREST TARGET

        private int FindNearestTarget(
            Vector2 center,
            float radius)
        {
            int closest = -1;

            float closestDistance = radius;


            for (
                int i = 0;
                i < Main.maxNPCs;
                i++
            )
            {
                NPC npc =
                    Main.npc[i];


                // BASIC VALIDATION

                if (
                    npc == null ||
                    !npc.active ||
                    npc.friendly ||
                    npc.dontTakeDamage
                )
                {
                    continue;
                }


                // BOSS + NORMAL ENEMY SUPPORT
                //
                // There is intentionally NO:
                //
                //     npc.lifeMax <= 5
                //
                // check here.
                //
                // Bosses are completely valid targets.
                //

                Vector2 targetCenter =
                    npc.Center;


                float distance =
                    Vector2.Distance(
                        center,
                        targetCenter
                    );


                if (
                    distance <
                    closestDistance
                )
                {
                    closestDistance =
                        distance;

                    closest =
                        i;
                }
            }


            return closest;
        }


        // SLIME DESTROYED

        public void NotifySlimeDestroyed(int slot)
        {
            if (
                Main.myPlayer !=
                Projectile.owner
            )
            {
                return;
            }


            if (
                slot < 0 ||
                slot >= SlimeCount
            )
            {
                return;
            }


            /*
             * The slime has exploded.
             *
             * This ALWAYS starts the regeneration timer,
             * regardless of whether it attacked:
             *
             * - a normal enemy
             * - an elite enemy
             * - a miniboss
             * - a boss
             */

            slimeRespawnTimers[slot] =
                SlimeRegenerationTime;
        }


        // DEBUG / OPTIONAL

        public int GetSlimeRespawnTimer(int slot)
        {
            if (
                slot < 0 ||
                slot >= SlimeCount
            )
            {
                return 0;
            }


            return slimeRespawnTimers[slot];
        }
    }
}