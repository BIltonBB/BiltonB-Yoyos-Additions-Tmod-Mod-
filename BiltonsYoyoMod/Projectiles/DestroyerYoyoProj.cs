using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class DestroyerYoyoProj : ModProjectile
    {
        // WORM 1 SETTINGS

        private const int MaxClones = 10;
        private const int CloneSpawnDelay = 6;

        private int spawnTimer;
        private int clonesSpawned;


        // WORM 1 TARGET

        public int CloneTarget = -1;
        public int CloneChaseTimer = 0;

        private const float CloneDetectionRadius = 360f;
        private const int PostHitChaseTime = 3600;


        // WORM 2

        /*
         * Worm 2's actual AI is handled by
         * DestroyerYoyoProjClone2.
         *
         * This value only tells Worm 2 that the second
         * chain has been spawned.
         */

        public bool Worm2Active = false;

        private bool splitTriggered;

        private const float SplitHealthThreshold = 0.50f;


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
            ] = 14f;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[
                Projectile.type
            ] = 60f;
        }


        // AI

        public override void AI()
        {
            // WORM 1 TARGET DETECTION

            if (CloneChaseTimer > 0)
                CloneChaseTimer--;


            int nearbyTarget =
                FindCloneTarget(
                    Projectile.Center,
                    CloneDetectionRadius
                );


            if (nearbyTarget != -1)
            {
                CloneTarget =
                    nearbyTarget;


                /*
                 * Keep Worm 1's target alive while an enemy
                 * remains close to the yoyo.
                 */
                if (CloneChaseTimer < 2)
                    CloneChaseTimer = 2;
            }


            // WORM 1 TARGET VALIDATION

            if (
                CloneTarget != -1 &&
                (
                    CloneTarget < 0 ||
                    CloneTarget >= Main.maxNPCs ||
                    !IsValidCloneTarget(
                        Main.npc[CloneTarget]
                    )
                )
            )
            {
                CloneTarget = -1;
            }


            // END WORM 1 CHASE

            if (
                CloneChaseTimer <= 0 &&
                nearbyTarget == -1
            )
            {
                CloneTarget = -1;
            }


            // WORM 1 SPAWNING

            if (
                clonesSpawned < MaxClones &&
                Main.myPlayer == Projectile.owner
            )
            {
                spawnTimer++;


                if (spawnTimer >= CloneSpawnDelay)
                {
                    spawnTimer = 0;


                    SpawnWorm1Clone(
                        clonesSpawned
                    );


                    clonesSpawned++;
                }
            }


            // WORM 2 SPLIT

            if (
                !splitTriggered &&
                Main.myPlayer == Projectile.owner
            )
            {
                Player owner =
                    Main.player[
                        Projectile.owner
                    ];


                /*
                 * Worm 2 activates at 50% HP OR BELOW.
                 *
                 * <= is intentional.
                 */

                if (
                    owner.active &&
                    owner.statLife <=
                    owner.statLifeMax2 *
                    SplitHealthThreshold
                )
                {
                    splitTriggered = true;

                    SpawnSecondWorm();
                }
            }
        }


        // SPAWN WORM 1 CLONE

        private void SpawnWorm1Clone(
            int cloneIndex)
        {
            if (
                cloneIndex < 0 ||
                cloneIndex >= MaxClones
            )
            {
                return;
            }


            Projectile clone =
                Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<
                        DestroyerYoyoProjClone>(),
                    Projectile.damage / 2,
                    1f,
                    Projectile.owner,
                    Projectile.whoAmI,
                    cloneIndex,
                    0f
                );


            clone.timeLeft = 10;

            clone.netUpdate = true;
        }


        // SPAWN WORM 2

        private void SpawnSecondWorm()
        {
            /*
             * Safety:
             * Never create Worm 2 twice.
             */

            if (Worm2Active)
                return;


            Worm2Active = true;


            // WORM 2 CHAIN

            /*
             * Worm 2 gets its own completely separate chain.
             *
             * Clone 0 = head
             * Clone 1 = body
             * Clone 2 = body
             * ...
             * Clone 9 = tail
             *
             * ai[0] = parent yoyo projectile index
             * ai[1] = Worm 2 clone index
             */

            for (
                int i = 0;
                i < MaxClones;
                i++
            )
            {
                Projectile clone =
                    Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<
                            DestroyerYoyoProjClone2>(),
                        Projectile.damage / 2,
                        1f,
                        Projectile.owner,
                        Projectile.whoAmI,
                        i
                    );


                clone.timeLeft = 10;

                clone.netUpdate = true;
            }


            // SPLIT VISUAL

            for (int i = 0; i < 30; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        5f,
                        5f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.GreenFairy,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.3f
                        )
                    );


                dust.noGravity = true;
            }


            Projectile.netUpdate = true;
        }


        // FIND TARGET

        private int FindCloneTarget(
            Vector2 center,
            float radius)
        {
            int closest = -1;

            float closestDistance =
                radius;


            for (
                int i = 0;
                i < Main.maxNPCs;
                i++
            )
            {
                NPC npc =
                    Main.npc[i];


                if (!IsValidCloneTarget(npc))
                    continue;


                float distance =
                    Vector2.Distance(
                        center,
                        npc.Center
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


        // VALID TARGET

        private bool IsValidCloneTarget(
            NPC npc)
        {
            return
                npc != null &&
                npc.active &&
                !npc.friendly &&
                !npc.dontTakeDamage &&
                npc.lifeMax > 5;
        }


        // WORM 1 HIT CALLBACK

        public void SetCloneHitTarget(
            int targetWhoAmI)
        {
            if (
                targetWhoAmI < 0 ||
                targetWhoAmI >= Main.maxNPCs
            )
            {
                return;
            }


            NPC target =
                Main.npc[targetWhoAmI];


            if (!IsValidCloneTarget(target))
                return;


            CloneTarget =
                targetWhoAmI;


            CloneChaseTimer =
                PostHitChaseTime;
        }
    }
}