using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.ID;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class GolemYoyoProj : ModProjectile
    {
        // HAND ATTACK STATE

        public enum HandAttackState
        {
            Orbit,
            LeftAttacking,
            BothAttacking,
            Returning
        }

        private HandAttackState handState =
            HandAttackState.Orbit;


        // TARGET

        // Locked target for the current hand attack.
        private int targetNPC = -1;


        // HAND HIT STATE

        private bool leftHandHit;
        private bool rightHandHit;


        // ATTACK TIMER

        private int handAttackTimer;


        // COOLDOWN

        // 5 seconds.
        private const int HandCooldown = 300;

        private int handCooldownTimer;


        // HAND SETTINGS

        private const float HandAttackRange = 500f;

        // 1 second.
        private const int LeftToRightDelay = 60;

        // Maximum 5 seconds.
        private const int HandAttackLifetime = 300;


        // CORE STATE

        private bool handsSpawned;


        // CORE I-FRAMES

        // 0.5 second of i-frames after a hostile projectile hits.
        private const int CoreTriggerIFrameDuration = 30;

        private int coreTriggerIFrameTimer;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.DamageType =
                DamageClass.Melee;

            Projectile.tileCollide = false;

            Projectile.timeLeft = 9999;

            Projectile.aiStyle =
                ProjAIStyleID.Yoyo;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;


            ProjectileID.Sets.YoyosMaximumRange[
                Projectile.type
            ] = 340f;


            ProjectileID.Sets.YoyosTopSpeed[
                Projectile.type
            ] = 12f;


            ProjectileID.Sets.YoyosLifeTimeMultiplier[
                Projectile.type
            ] = -1f;
        }


        // AI

        public override void AI()
        {
            Lighting.AddLight(
                Projectile.Center,
                0.20f,
                0.45f,
                0.65f
            );


            /*
             * Only the owner creates helper projectiles and
             * controls gameplay state.
             */
            if (
                Main.myPlayer !=
                Projectile.owner
            )
            {
                return;
            }


            // CORE I-FRAMES

            if (
                coreTriggerIFrameTimer >
                0
            )
            {
                coreTriggerIFrameTimer--;
            }


            // HANDS

            SpawnHands();


            HandleHandAttack();


            // CORE TRIGGER

            CheckForCoreTrigger();
        }


        // HAND SPAWN

        private void SpawnHands()
        {
            if (handsSpawned)
            {
                return;
            }


            handsSpawned = true;


            // LEFT
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<
                    GolemFistYoyoProj>(),
                Math.Max(
                    1,
                    Projectile.damage
                ),
                Projectile.knockBack,
                Projectile.owner,
                0f
            );


            // RIGHT
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<
                    GolemFistYoyoProj>(),
                Math.Max(
                    1,
                    Projectile.damage
                ),
                Projectile.knockBack,
                Projectile.owner,
                1f
            );
        }


        // CORE TRIGGER

        private void CheckForCoreTrigger()
        {
            /*
             * Never spawn a second Core while one already exists.
             */
            if (CoreExists())
            {
                return;
            }


            /*
             * Do not allow another hostile projectile to trigger
             * the Core while the yoyo is in its i-frame period.
             */
            if (
                coreTriggerIFrameTimer >
                0
            )
            {
                return;
            }


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
                    other.whoAmI ==
                    Projectile.whoAmI
                )
                {
                    continue;
                }


                /*
                 * Only hostile projectiles can hit the yoyo.
                 *
                 * NPCs are not projectiles, so they naturally
                 * do not trigger this.
                 */
                if (!other.hostile)
                {
                    continue;
                }


                /*
                 * Prevent accidentally using a projectile that is
                 * friendly AND hostile.
                 */
                if (other.friendly)
                {
                    continue;
                }


                if (
                    !Projectile.Hitbox.Intersects(
                        other.Hitbox
                    )
                )
                {
                    continue;
                }


                Vector2 hitPosition =
                    Projectile.Center;


                /*
                 * IMPORTANT:
                 *
                 * The hostile projectile is NOT destroyed.
                 * It continues doing whatever it normally does.
                 */


                /*
                 * Give the yoyo i-frames immediately after
                 * being hit.
                 */
                coreTriggerIFrameTimer =
                    CoreTriggerIFrameDuration;


                // Visual hit effect.
                SpawnCoreTriggerEffect(
                    hitPosition
                );


                // Spawn Core.
                SpawnCore(
                    hitPosition
                );


                return;
            }
        }


        // CORE EXISTENCE

        private bool CoreExists()
        {
            int coreType =
                ModContent.ProjectileType<
                    GolemYoyoProjC>();


            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile other =
                    Main.projectile[i];


                if (
                    !other.active
                )
                {
                    continue;
                }


                if (
                    other.type !=
                    coreType
                )
                {
                    continue;
                }


                if (
                    other.owner !=
                    Projectile.owner
                )
                {
                    continue;
                }


                return true;
            }


            return false;
        }


        // SPAWN CORE

        private void SpawnCore(
            Vector2 position)
        {
            /*
             * Double-check immediately before spawning.
             */
            if (CoreExists())
            {
                return;
            }


            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                position,
                Vector2.Zero,
                ModContent.ProjectileType<
                    GolemYoyoProjC>(),
                Math.Max(
                    1,
                    Projectile.damage
                ),
                Projectile.knockBack,
                Projectile.owner
            );
        }


        // CORE TRIGGER EFFECT

        private void SpawnCoreTriggerEffect(
            Vector2 position)
        {
            for (
                int i = 0;
                i < 30;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Electric,
                        Main.rand.NextVector2Circular(
                            4.5f,
                            4.5f
                        ),
                        70,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.4f
                        )
                    );


                dust.noGravity = true;
            }


            for (
                int i = 0;
                i < 12;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Smoke,
                        Main.rand.NextVector2Circular(
                            2.5f,
                            2.5f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.4f,
                            0.8f
                        )
                    );


                dust.noGravity = true;
            }


            Terraria.Audio.SoundEngine.PlaySound(
                SoundID.Item93,
                position
            );
        }


        // HAND ATTACK

        private void HandleHandAttack()
        {
            // COOLDOWN

            if (
                handCooldownTimer >
                0
            )
            {
                handCooldownTimer--;


                handState =
                    HandAttackState.Returning;


                return;
            }


            // RETURNING

            if (
                handState ==
                HandAttackState.Returning
            )
            {
                handState =
                    HandAttackState.Orbit;


                handAttackTimer = 0;

                targetNPC = -1;

                leftHandHit = false;
                rightHandHit = false;
            }


            // ACTIVE ATTACK

            if (
                handState ==
                    HandAttackState.LeftAttacking ||
                handState ==
                    HandAttackState.BothAttacking
            )
            {
                handAttackTimer++;


                /*
                 * IMPORTANT:
                 *
                 * We NEVER search for a new target here.
                 */
                if (
                    !IsTargetValid()
                )
                {
                    StartHandCooldown();
                    return;
                }


                if (
                    handAttackTimer >=
                    HandAttackLifetime
                )
                {
                    StartHandCooldown();
                    return;
                }


                // LEFT → RIGHT AFTER 1 SECOND
                if (
                    handState ==
                        HandAttackState.LeftAttacking &&
                    handAttackTimer >=
                        LeftToRightDelay
                )
                {
                    handState =
                        HandAttackState.BothAttacking;


                    Projectile.netUpdate =
                        true;
                }


                return;
            }


            // ORBIT

            if (
                handState !=
                HandAttackState.Orbit
            )
            {
                return;
            }


            /*
             * Only here is a new target selected.
             */
            NPC target =
                FindNearestTarget();


            if (target == null)
            {
                return;
            }


            targetNPC =
                target.whoAmI;


            handAttackTimer = 0;

            leftHandHit = false;
            rightHandHit = false;


            // LEFT FIRES FIRST
            handState =
                HandAttackState.LeftAttacking;


            Projectile.netUpdate =
                true;
        }


        // TARGET CHECK

        public bool IsTargetValid()
        {
            if (
                targetNPC < 0 ||
                targetNPC >= Main.maxNPCs
            )
            {
                return false;
            }


            NPC target =
                Main.npc[targetNPC];


            return
                target.active &&
                target.CanBeChasedBy();
        }


        public NPC GetAttackTarget()
        {
            if (
                !IsTargetValid()
            )
            {
                return null;
            }


            return Main.npc[
                targetNPC
            ];
        }


        public HandAttackState GetHandState()
        {
            return handState;
        }


        // HAND HIT

        public bool HasHandHit(
            int side)
        {
            if (side == 0)
            {
                return leftHandHit;
            }


            return rightHandHit;
        }


        public void ReportHandHit(
            int side)
        {
            if (side == 0)
            {
                if (leftHandHit)
                {
                    return;
                }


                leftHandHit = true;
            }
            else
            {
                if (rightHandHit)
                {
                    return;
                }


                rightHandHit = true;
            }


            /*
             * Right hand finishes the complete attack.
             */
            if (side == 1)
            {
                StartHandCooldown();
            }
        }


        // START HAND COOLDOWN

        private void StartHandCooldown()
        {
            handCooldownTimer =
                HandCooldown;


            handState =
                HandAttackState.Returning;


            handAttackTimer = 0;

            targetNPC = -1;

            leftHandHit = false;
            rightHandHit = false;


            Projectile.netUpdate =
                true;
        }


        public void EndHandAttack()
        {
            StartHandCooldown();
        }


        // FIND TARGET

        private NPC FindNearestTarget()
        {
            NPC best = null;


            float bestDistance =
                HandAttackRange *
                HandAttackRange;


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
                    !npc.CanBeChasedBy()
                )
                {
                    continue;
                }


                float distance =
                    Vector2.DistanceSquared(
                        Projectile.Center,
                        npc.Center
                    );


                if (
                    distance <
                    bestDistance
                )
                {
                    bestDistance =
                        distance;

                    best =
                        npc;
                }
            }


            return best;
        }
    }
}