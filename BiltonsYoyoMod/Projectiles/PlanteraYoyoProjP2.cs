using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class PlanteraYoyoProjP2 : ModProjectile
    {
        // EATER SETTINGS

        private const float IdleRadius = 60f;

        private const float RadialOscillationDistance = 16f;

        private const float TangentOscillationDistance = 22f;

        private const float RadialOscillationSpeed = 0.085f;

        private const float TangentOscillationSpeed = 0.105f;


        // TARGETING

        private const float TargetDetectionRadius = 400f;

        private const float ChaseAcceleration = 0.12f;

        private const float ChaseSpeed = 12f;

        private const float LatchDistance = 24f;


        // LATCH

        private const int LatchDuration = 1200;

        private const float MaximumTargetSpeed = 18f;

        private const int FailedLatchIdleTime = 300;


        // SEEDLER

        private const int SeedlerCooldown = 60;

        private int seedlerTimer;


        // STATES

        private const int StateIdle = 0;

        private const int StateChasing = 1;

        private const int StateLatched = 2;

        private const int StateRecovering = 3;


        // STATE DATA

        private int currentState =
            StateIdle;

        private int latchTimer;

        private int recoveryTimer;

        private int targetNPC = -1;

        private Vector2 previousTargetPosition;


        // ORBIT DATA

        private float designatedAngle;

        private float radialPhase;

        private float tangentPhase;

        private bool initialized;


        // PARENT

        private int parentIndex = -1;


        // VISUAL

        private const float VisualScale = 0.5f;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.tileCollide = false;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.timeLeft = 9999;

            Projectile.scale =
                VisualScale;
        }


        // AI

        public override void AI()
        {
            // GET PARENT

            if (parentIndex == -1)
            {
                parentIndex =
                    FindMainYoyo();
            }


            if (
                parentIndex < 0 ||
                parentIndex >= Main.maxProjectiles
            )
            {
                Projectile.Kill();
                return;
            }


            Projectile parent =
                Main.projectile[parentIndex];


            if (
                !parent.active ||
                parent.type !=
                ModContent.ProjectileType<
                    PlanteraYoyoProj>() ||
                parent.owner != Projectile.owner
            )
            {
                Projectile.Kill();
                return;
            }


            Projectile.timeLeft = 9999;


            Projectile.damage =
                parent.damage;


            // INITIALIZE

            if (!initialized)
            {
                designatedAngle =
                    Projectile.ai[0];

                radialPhase =
                    (Projectile.whoAmI % 7) *
                    0.9f;

                tangentPhase =
                    (Projectile.whoAmI % 11) *
                    0.63f;


                Projectile.Center =
                    parent.Center +
                    designatedAngle.ToRotationVector2() *
                    IdleRadius;


                initialized = true;
            }


            // RECOVERY

            if (
                currentState ==
                StateRecovering
            )
            {
                recoveryTimer--;

                /*
                 * Allow seedlers during recovery.
                 */
                seedlerTimer++;

                if (
                    seedlerTimer >= SeedlerCooldown &&
                    Main.myPlayer == Projectile.owner
                )
                {
                    seedlerTimer = 0;

                    FireSeedler();
                }


                IdleMovement(parent);

                UpdateRotation(parent);


                if (recoveryTimer <= 0)
                {
                    currentState =
                        StateIdle;
                }


                return;
            }


            // LATCHED

            if (
                currentState ==
                StateLatched
            )
            {
                /*
                 * Absolutely no seedlers while latched.
                 *
                 * Reset the timer so a seedler isn't immediately fired
                 * the instant the latch finishes.
                 */
                seedlerTimer = 0;

                UpdateLatched();

                UpdateRotation(parent);

                return;
            }


            // SEEDLER

            /*
             * Seedlers are allowed in:
             *
             * - Idle
             * - Chasing
             * - Recovering
             *
             * They are NOT allowed while latched.
             */
            seedlerTimer++;


            if (
                seedlerTimer >= SeedlerCooldown &&
                Main.myPlayer == Projectile.owner
            )
            {
                seedlerTimer = 0;

                FireSeedler();
            }


            // FIND NEAREST TARGET

            int nearestTarget =
                FindNearestTarget(
                    parent.Center,
                    TargetDetectionRadius
                );


            // IDLE

            if (
                currentState ==
                StateIdle
            )
            {
                if (nearestTarget != -1)
                {
                    BeginChasing(
                        nearestTarget
                    );

                    ChaseTarget();

                    UpdateRotation(parent);

                    return;
                }


                IdleMovement(parent);

                UpdateRotation(parent);

                return;
            }


            // CHASING

            if (
                currentState ==
                StateChasing
            )
            {
                /*
                 * Always prefer the nearest enemy around the yoyo.
                 */
                if (nearestTarget != -1)
                {
                    targetNPC =
                        nearestTarget;
                }


                if (!ValidStoredTarget())
                {
                    BeginRecovery();

                    IdleMovement(parent);

                    UpdateRotation(parent);

                    return;
                }


                ChaseTarget();


                // LATCH

                if (
                    Vector2.Distance(
                        Projectile.Center,
                        Main.npc[targetNPC].Center
                    ) <= LatchDistance
                )
                {
                    BeginLatch();

                    UpdateRotation(parent);

                    return;
                }


                UpdateRotation(parent);

                return;
            }
        }


        // IDLE MOVEMENT

        private void IdleMovement(
            Projectile parent)
        {
            Vector2 radialDirection =
                designatedAngle.ToRotationVector2();


            Vector2 tangentDirection =
                new Vector2(
                    -radialDirection.Y,
                    radialDirection.X
                );


            float radialOscillation =
                (float)Math.Sin(
                    Main.GameUpdateCount *
                    RadialOscillationSpeed +
                    radialPhase
                );


            float tangentOscillation =
                (float)Math.Sin(
                    Main.GameUpdateCount *
                    TangentOscillationSpeed +
                    tangentPhase
                );


            Vector2 targetPosition =
                parent.Center +
                radialDirection *
                (
                    IdleRadius +
                    radialOscillation *
                    RadialOscillationDistance
                ) +
                tangentDirection *
                (
                    tangentOscillation *
                    TangentOscillationDistance
                );


            Vector2 difference =
                targetPosition -
                Projectile.Center;


            if (
                difference.LengthSquared() >
                1000000f
            )
            {
                Projectile.Center =
                    targetPosition;

                Projectile.velocity =
                    Vector2.Zero;

                return;
            }


            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    difference * 0.18f,
                    0.20f
                );


            Projectile.Center +=
                Projectile.velocity;
        }


        // BEGIN CHASE

        private void BeginChasing(
            int target)
        {
            targetNPC =
                target;

            currentState =
                StateChasing;
        }


        // CHASE TARGET

        private void ChaseTarget()
        {
            if (!ValidStoredTarget())
                return;


            NPC target =
                Main.npc[targetNPC];


            Vector2 difference =
                target.Center -
                Projectile.Center;


            if (
                difference.LengthSquared() <
                0.01f
            )
            {
                return;
            }


            Vector2 direction =
                difference.SafeNormalize(
                    Vector2.UnitY
                );


            Vector2 desiredVelocity =
                direction *
                ChaseSpeed;


            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    desiredVelocity,
                    ChaseAcceleration
                );


            Projectile.Center +=
                Projectile.velocity;
        }


        // BEGIN LATCH

        private void BeginLatch()
        {
            if (!ValidStoredTarget())
            {
                BeginRecovery();
                return;
            }


            currentState =
                StateLatched;


            latchTimer =
                LatchDuration;


            previousTargetPosition =
                Main.npc[targetNPC].Center;


            /*
             * Reset seedler timer when the latch starts.
             */
            seedlerTimer = 0;


            Projectile.velocity =
                Vector2.Zero;
        }


        // UPDATE LATCH

        private void UpdateLatched()
        {
            if (!ValidStoredTarget())
            {
                BeginRecovery();
                return;
            }


            NPC target =
                Main.npc[targetNPC];


            // TARGET SPEED CHECK

            Vector2 targetMovement =
                target.Center -
                previousTargetPosition;


            float targetSpeed =
                targetMovement.Length();


            previousTargetPosition =
                target.Center;


            /*
             * Moving faster than 18 pixels/tick breaks the latch.
             */
            if (
                targetSpeed >
                MaximumTargetSpeed
            )
            {
                BeginRecovery();
                return;
            }


            // REMAIN ATTACHED

            Projectile.Center =
                target.Center;


            Projectile.velocity =
                target.velocity;


            latchTimer--;


            // LATCH COMPLETE

            if (latchTimer <= 0)
            {
                currentState =
                    StateIdle;

                targetNPC =
                    -1;

                /*
                 * Keep the timer at zero.
                 *
                 * It will begin counting again normally on the next
                 * AI tick.
                 */
                seedlerTimer = 0;

                Projectile.velocity =
                    Vector2.Zero;
            }
        }


        // BEGIN RECOVERY

        private void BeginRecovery()
        {
            currentState =
                StateRecovering;


            recoveryTimer =
                FailedLatchIdleTime;


            latchTimer =
                0;


            targetNPC =
                -1;


            seedlerTimer = 0;


            Projectile.velocity =
                Vector2.Zero;
        }


        // FIND MAIN YOYO

        private int FindMainYoyo()
        {
            int mainType =
                ModContent.ProjectileType<
                    PlanteraYoyoProj>();


            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile projectile =
                    Main.projectile[i];


                if (
                    projectile.active &&
                    projectile.type == mainType &&
                    projectile.owner == Projectile.owner
                )
                {
                    return i;
                }
            }


            return -1;
        }


        // FIND NEAREST TARGET

        private int FindNearestTarget(
            Vector2 center,
            float radius)
        {
            int closest =
                -1;


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


                if (!ValidTarget(npc))
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


        // VALID STORED TARGET

        private bool ValidStoredTarget()
        {
            if (
                targetNPC < 0 ||
                targetNPC >= Main.maxNPCs
            )
            {
                return false;
            }


            return ValidTarget(
                Main.npc[targetNPC]
            );
        }


        // VALID TARGET

        private bool ValidTarget(
            NPC npc)
        {
            return
                npc != null &&
                npc.active &&
                !npc.friendly &&
                !npc.dontTakeDamage &&
                npc.lifeMax > 5;
        }


        // ROTATION

        private void UpdateRotation(
            Projectile parent)
        {
            Vector2 facingDirection;


            /*
             * Chase:
             *
             * Face toward the target.
             */
            if (
                currentState ==
                StateChasing &&
                ValidStoredTarget()
            )
            {
                facingDirection =
                    Main.npc[targetNPC].Center -
                    Projectile.Center;
            }

            /*
             * Latch:
             *
             * Face away from the yoyo.
             */
            else
            {
                facingDirection =
                    Projectile.Center -
                    parent.Center;
            }


            if (
                facingDirection.LengthSquared() >
                0.01f
            )
            {
                Projectile.rotation =
                    facingDirection.ToRotation();
            }
        }


        // FIRE SEEDLER

        private void FireSeedler()
        {
            if (
                Main.myPlayer !=
                Projectile.owner
            )
            {
                return;
            }


            /*
             * Shoot DIRECTLY in front of the eater.
             *
             * Projectile.rotation is already updated to represent
             * the direction the eater is facing.
             */
            Vector2 direction =
                Projectile.rotation.ToRotationVector2();


            if (
                direction.LengthSquared() <
                0.01f
            )
            {
                direction =
                    Vector2.UnitX;
            }


            /*
             * Spawn slightly in front of the eater rather than inside
             * its hitbox.
             */
            Vector2 spawnPosition =
                Projectile.Center +
                direction * 8f;


            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                spawnPosition,
                direction * 6f,
                ModContent.ProjectileType<
                    PlanteraYoyoProjSeedler>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner
            );
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            int parentIndex =
                this.parentIndex;


            if (
                parentIndex < 0 ||
                parentIndex >= Main.maxProjectiles
            )
            {
                return false;
            }


            Projectile parent =
                Main.projectile[parentIndex];


            if (!parent.active)
                return false;


            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/PlanteraYoyoProjP2"
                ).Value;


            // STRING

            Vector2 start =
                parent.Center -
                Main.screenPosition;


            Vector2 end =
                Projectile.Center -
                Main.screenPosition;


            Vector2 difference =
                end -
                start;


            float length =
                difference.Length();


            if (
                length > 2f &&
                length < 1000f
            )
            {
                Vector2 direction =
                    difference /
                    length;


                Vector2 perpendicular =
                    new Vector2(
                        -direction.Y,
                        direction.X
                    );


                Texture2D pixel =
                    TextureAssets
                        .MagicPixel
                        .Value;


                // MAIN GREEN STRING

                Vector2 midpoint =
                    (start + end) *
                    0.5f;


                float rotation =
                    direction.ToRotation();


                Main.spriteBatch.Draw(
                    pixel,
                    midpoint,
                    new Rectangle(
                        0,
                        0,
                        1,
                        1
                    ),
                    new Color(
                        35,
                        100,
                        35,
                        255
                    ),
                    rotation,
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    new Vector2(
                        length,
                        3f
                    ),
                    SpriteEffects.None,
                    0f
                );


                // BRIGHT GREEN CORE

                Main.spriteBatch.Draw(
                    pixel,
                    midpoint,
                    new Rectangle(
                        0,
                        0,
                        1,
                        1
                    ),
                    new Color(
                        110,
                        220,
                        80,
                        255
                    ),
                    rotation,
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    new Vector2(
                        length,
                        1f
                    ),
                    SpriteEffects.None,
                    0f
                );


                // CLOSE JAGGED EDGE

                /*
                 * These are now only about 1 pixel from the centerline
                 * instead of 3.5 pixels away, so they visually belong
                 * to the string instead of looking like particles.
                 */

                const int JaggedDots = 12;

                for (
                    int i = 1;
                    i < JaggedDots;
                    i++
                )
                {
                    float progress =
                        i /
                        (float)JaggedDots;


                    Vector2 position =
                        Vector2.Lerp(
                            start,
                            end,
                            progress
                        );


                    float wave =
                        (i % 2 == 0)
                            ? 1.1f
                            : -1.1f;


                    position +=
                        perpendicular *
                        wave;


                    Main.spriteBatch.Draw(
                        pixel,
                        position,
                        new Rectangle(
                            0,
                            0,
                            1,
                            1
                        ),
                        new Color(
                            90,
                            190,
                            65,
                            255
                        ),
                        0f,
                        new Vector2(
                            0.5f,
                            0.5f
                        ),
                        new Vector2(
                            1.2f,
                            1.2f
                        ),
                        SpriteEffects.None,
                        0f
                    );
                }
            }


            // EATER

            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                texture.Size() * 0.5f,
                VisualScale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}