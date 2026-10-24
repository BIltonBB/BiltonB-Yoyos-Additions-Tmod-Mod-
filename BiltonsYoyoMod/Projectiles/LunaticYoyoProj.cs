using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.ID;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class LunaticYoyoProj : ModProjectile
    {
        // PHASES

        private enum Phase
        {
            Lightning,
            Ice,
            Clones
        }

        private const int PhaseLength = 300;


        // CLONE SETTINGS

        private const int MinimumCloneLifetime = 300;
        private const int MaximumCloneLifetime = 900;


        // STATE

        private int phaseTimer;

        private Phase currentPhase =
            Phase.Lightning;

        private Phase previousPhase =
            Phase.Lightning;

        private bool initialized;


        // PHASE SPAWN COUNTERS

        private int lightningSpawnCount;

        private int iceSpawnCount;

        private bool clonesSpawned;


        // SHARED CLONE LIFETIME

        private int cloneLifetime;


        // DEFAULTS

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

            Projectile.aiStyle =
                ProjAIStyleID.Yoyo;

            Projectile.timeLeft = 9999;


            ProjectileID.Sets.YoyosMaximumRange[
                Projectile.type
            ] = 360f;


            ProjectileID.Sets.YoyosTopSpeed[
                Projectile.type
            ] = 15f;


            ProjectileID.Sets.YoyosLifeTimeMultiplier[
                Projectile.type
            ] = -1f;
        }


        // AI

        public override void AI()
        {
            Lighting.AddLight(
                Projectile.Center,
                0.35f,
                0.15f,
                0.45f
            );


            // INITIALIZE

            if (!initialized)
            {
                initialized = true;

                phaseTimer = 0;

                currentPhase =
                    Phase.Lightning;

                previousPhase =
                    Phase.Lightning;

                lightningSpawnCount = 0;

                iceSpawnCount = 0;

                clonesSpawned = false;

                cloneLifetime = 0;
            }


            // OWNER

            if (
                Main.myPlayer !=
                Projectile.owner
            )
            {
                return;
            }


            // PERSONAL TIMER

            phaseTimer++;


            currentPhase =
                (Phase)(
                    (phaseTimer / PhaseLength) %
                    3
                );


            // PHASE CHANGE

            if (
                currentPhase !=
                previousPhase
            )
            {
                ResetPhaseState();

                previousPhase =
                    currentPhase;
            }


            // CURRENT PHASE

            switch (currentPhase)
            {
                case Phase.Lightning:
                    HandleLightningPhase();
                    break;

                case Phase.Ice:
                    HandleIcePhase();
                    break;

                case Phase.Clones:
                    HandleClonePhase();
                    break;
            }
        }


        // RESET PHASE

        private void ResetPhaseState()
        {
            lightningSpawnCount = 0;

            iceSpawnCount = 0;

            clonesSpawned = false;

            cloneLifetime = 0;


            KillPhaseProjectiles();
        }


        // LIGHTNING PHASE

        private void HandleLightningPhase()
        {
            int phaseTime =
                phaseTimer %
                PhaseLength;


            if (
                lightningSpawnCount == 0 &&
                phaseTime >= 1
            )
            {
                SpawnLightningPoint();

                lightningSpawnCount++;
            }


            if (
                lightningSpawnCount == 1 &&
                phaseTime >= 60
            )
            {
                SpawnLightningPoint();

                lightningSpawnCount++;
            }


            if (
                lightningSpawnCount == 2 &&
                phaseTime >= 120
            )
            {
                SpawnLightningPoint();

                lightningSpawnCount++;
            }
        }


        private void SpawnLightningPoint()
        {
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),

                Projectile.Center,

                Vector2.Zero,

                ModContent.ProjectileType<
                    LunaticYoyoProjLP>(),

                Math.Max(
                    1,
                    Projectile.damage / 4
                ),

                Projectile.knockBack,

                Projectile.owner,

                Projectile.whoAmI
            );
        }


        // ICE PHASE

        private void HandleIcePhase()
        {
            int phaseTime =
                phaseTimer %
                PhaseLength;


            if (
                iceSpawnCount == 0 &&
                phaseTime >= 1
            )
            {
                SpawnIcePoint();

                iceSpawnCount++;
            }


            if (
                iceSpawnCount == 1 &&
                phaseTime >= 60
            )
            {
                SpawnIcePoint();

                iceSpawnCount++;
            }


            if (
                iceSpawnCount == 2 &&
                phaseTime >= 120
            )
            {
                SpawnIcePoint();

                iceSpawnCount++;
            }
        }


        private void SpawnIcePoint()
        {
            Vector2 direction =
                Main.rand.NextVector2Unit();


            if (
                direction.LengthSquared() <
                0.001f
            )
            {
                direction =
                    Vector2.UnitX;
            }


            const float IcePointSpeed =
                1.5f;


            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),

                Projectile.Center,

                direction *
                IcePointSpeed,

                ModContent.ProjectileType<
                    LunaticYoyoProjIP>(),

                Math.Max(
                    1,
                    Projectile.damage / 4
                ),

                Projectile.knockBack,

                Projectile.owner,

                Projectile.whoAmI
            );
        }


        // CLONE PHASE

        private void HandleClonePhase()
        {
            /*
             * IMPORTANT:
             *
             * The clone wave is spawned only once.
             *
             * If the clones explode or expire, they will NOT
             * be spawned again until the next Clone phase.
             */
            if (clonesSpawned)
            {
                return;
            }


            Player player =
                Main.player[
                    Projectile.owner
                ];


            if (
                player == null ||
                !player.active
            )
            {
                clonesSpawned = true;

                return;
            }


            // HOW MANY CLONES?

            int wantedClones = 2;


            /*
             * At 50% HP or below:
             *
             * 4 clones + main yoyo.
             */
            if (
                player.statLife <=
                player.statLifeMax2 * 0.5f
            )
            {
                wantedClones = 4;
            }


            // SHARED LIFETIME

            cloneLifetime =
                Main.rand.Next(
                    MinimumCloneLifetime,
                    MaximumCloneLifetime + 1
                );


            // CLONE TYPE

            int cloneType =
                ModContent.ProjectileType<
                    LunaticYoyoProjClone
                >();


            // SPAWN CLONES

            for (
                int cloneSlot = 1;
                cloneSlot <= wantedClones;
                cloneSlot++
            )
            {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),

                    Projectile.Center,

                    Vector2.Zero,

                    cloneType,

                    Math.Max(
                        1,
                        Projectile.damage / 4
                    ),

                    Projectile.knockBack,

                    Projectile.owner,

                    cloneSlot,

                    Projectile.whoAmI,

                    cloneLifetime
                );
            }


            /*
             * LOCK the phase permanently.
             *
             * Killing clones does not reset this.
             */
            clonesSpawned = true;
        }


        // PHASE CLEANUP

        private void KillPhaseProjectiles()
        {
            int lpType =
                ModContent.ProjectileType<
                    LunaticYoyoProjLP
                >();

            int lType =
                ModContent.ProjectileType<
                    LunaticYoyoProjL
                >();

            int ipType =
                ModContent.ProjectileType<
                    LunaticYoyoProjIP
                >();


            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile other =
                    Main.projectile[i];


                if (
                    !other.active ||
                    other.owner !=
                    Projectile.owner
                )
                {
                    continue;
                }


                if (
                    (
                        other.type == lpType ||
                        other.type == ipType
                    ) &&
                    (int)other.ai[0] ==
                    Projectile.whoAmI
                )
                {
                    other.Kill();

                    continue;
                }


                if (
                    other.type == lType &&
                    (int)other.ai[2] ==
                    Projectile.whoAmI
                )
                {
                    other.Kill();
                }
            }
        }


        // YOYO DISAPPEARS

        public override void OnKill(
            int timeLeft)
        {
            KillAllChildren();
        }


        // DELETE ALL CHILDREN

        private void KillAllChildren()
        {
            int lpType =
                ModContent.ProjectileType<
                    LunaticYoyoProjLP
                >();

            int lType =
                ModContent.ProjectileType<
                    LunaticYoyoProjL
                >();

            int ipType =
                ModContent.ProjectileType<
                    LunaticYoyoProjIP
                >();

            int cloneType =
                ModContent.ProjectileType<
                    LunaticYoyoProjClone
                >();


            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile other =
                    Main.projectile[i];


                if (
                    !other.active ||
                    other.owner !=
                    Projectile.owner
                )
                {
                    continue;
                }


                if (
                    (
                        other.type == lpType ||
                        other.type == ipType
                    ) &&
                    (int)other.ai[0] ==
                    Projectile.whoAmI
                )
                {
                    other.Kill();

                    continue;
                }


                if (
                    other.type == cloneType &&
                    (int)other.ai[1] ==
                    Projectile.whoAmI
                )
                {
                    other.Kill();

                    continue;
                }


                if (
                    other.type == lType &&
                    (int)other.ai[2] ==
                    Projectile.whoAmI
                )
                {
                    other.Kill();
                }
            }
        }
    }
}