using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class YoyoAxleGlobalProjectile : GlobalProjectile
    {
        // PER-PROJECTILE STATE

        public override bool InstancePerEntity => true;

        private bool initialized;
        private bool isYoyo;

        // Snapshot axle state when this yoyo is created.
        private bool uwActive;
        private bool owActive;
        private bool lwActive;
        private bool twActive;

        // UNDER WRAPPED

        private bool underReleased;

        // Actual speed stat of the held yoyo.
        private float underYoyoSpeed;

        // Current UW movement speed.
        private float underCurrentSpeed;

        // OVER WRAPPED

        private bool overReturn;

        private int overTimer;

        private int overMaximumLifetime;

        // TIGHT WRAPPED

        private bool tightReturn;

        private int tightReturnTimer;

        private int tightReturnDelay;

        // LOOSE WRAPPED

        private int looseBounceCount;

        // CONSTANTS

        // UNDER WRAPPED

        private const float UnderDamageMultiplier = 1.20f;

        private const float UnderAcceleration = 2.50f;

        private const float UnderDeceleration = 3.00f;

        private const float UnderMouseStopDistance = 8f;

        private const float UnderMinimumFollowSpeed = 0.5f;

        private const int UnderInfiniteLifetime = 2;

        // OVER WRAPPED

        private const float OverLifetimeMultiplier = 0.20f;

        private const float OverReturnDamageMultiplier = 3.00f;

        // LOOSE WRAPPED

        private const float LooseBounceSpeedMultiplier = 1.50f;

        private const int LooseLifetimePerBounce = 180;

        // TIGHT WRAPPED

        private const float TightDamageMultiplier = 1.20f;

        private const float TightReturnSpeedMultiplier = 1.25f;

        private const int TightEarlyMinimum = 5 * 60;
        private const int TightEarlyMaximum = 10 * 60;

        private const int TightMidMinimum = 10 * 60;
        private const int TightMidMaximum = 15 * 60;

        private const int TightLateMinimum = 15 * 60;
        private const int TightLateMaximum = 30 * 60;

        private const int TightVeryLateMinimum = 30 * 60;
        private const int TightVeryLateMaximum = 100 * 60;

        // FORCED RETURN

        private const float MinimumReturnSpeed = 8f;

        private const float ReturnPlayerDistance = 12f;

        // YOYO DETECTION

        private static bool IsYoyoProjectile(
            Projectile projectile)
        {
            if (
                projectile == null ||
                !projectile.active
            )
            {
                return false;
            }

            return projectile.aiStyle ==
                   ProjAIStyleID.Yoyo;
        }

        // INFINITE-LIFETIME YOYO

        private static bool IsInfiniteLifetimeYoyo(
            Projectile projectile)
        {
            if (
                projectile == null ||
                projectile.type < ProjectileID.None ||
                projectile.type >=
                    ProjectileID.Sets.YoyosLifeTimeMultiplier.Length
            )
            {
                return false;
            }

            return ProjectileID
                .Sets
                .YoyosLifeTimeMultiplier[
                    projectile.type
                ] < 0f;
        }

        // OWNER

        private static Player GetOwner(
            Projectile projectile)
        {
            if (
                projectile == null ||
                projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers
            )
            {
                return null;
            }

            Player player =
                Main.player[
                    projectile.owner
                ];

            if (
                player == null ||
                !player.active
            )
            {
                return null;
            }

            return player;
        }

        // UW EQUIPPED CHECK

        private static bool IsUnderWrappedEquipped(
            Player player)
        {
            if (
                player == null ||
                !player.active
            )
            {
                return false;
            }

            YoyoAxlePlayer axle =
                player.GetModPlayer<YoyoAxlePlayer>();

            return axle.UnderWrapped;
        }

        // GET ACTUAL YOYO SPEED

        private static float GetYoyoSpeed(
            Player player)
        {
            if (
                player == null ||
                !player.active
            )
            {
                return UnderMinimumFollowSpeed;
            }

            Item heldItem =
                player.HeldItem;

            if (
                heldItem == null ||
                heldItem.IsAir ||
                heldItem.shoot <= ProjectileID.None
            )
            {
                return UnderMinimumFollowSpeed;
            }

            /*
             * The yoyo's actual base speed comes from the item's
             * shootSpeed.
             *
             * Yoyos use melee speed to modify their velocity/range,
             * so include the player's effective weapon attack speed.
             */
            float yoyoSpeed =
                heldItem.shootSpeed;

            float attackSpeed =
                player.GetWeaponAttackSpeed(
                    heldItem
                );

            if (attackSpeed <= 0f)
            {
                attackSpeed = 1f;
            }

            /*
             * This produces the speed that corresponds to the
             * currently held yoyo rather than whatever velocity
             * happens to be inside the projectile.
             */
            yoyoSpeed *= attackSpeed;

            return Math.Max(
                UnderMinimumFollowSpeed,
                yoyoSpeed
            );
        }

        // INITIALIZATION

        private void Initialize(
            Projectile projectile)
        {
            if (initialized)
            {
                return;
            }

            if (!IsYoyoProjectile(projectile))
            {
                initialized = true;
                isYoyo = false;
                return;
            }

            Player player =
                GetOwner(projectile);

            if (player == null)
            {
                return;
            }

            initialized = true;
            isYoyo = true;

            YoyoAxlePlayer axle =
                player.GetModPlayer<YoyoAxlePlayer>();

            // SNAPSHOT AXLE STATE

            uwActive = axle.UnderWrapped;
            owActive = axle.OverWrapped;
            lwActive = axle.LooseWrapped;
            twActive = axle.TightWrapped;

            // YOYO SPEED

            /*
             * IMPORTANT:
             *
             * Do NOT use projectile.velocity here.
             *
             * UW uses the held yoyo's actual speed stat.
             */
            underYoyoSpeed =
                GetYoyoSpeed(player);

            underCurrentSpeed =
                underYoyoSpeed;

            // OVER WRAPPED

            if (
                owActive &&
                !IsInfiniteLifetimeYoyo(projectile)
            )
            {
                float multiplier =
                    ProjectileID
                        .Sets
                        .YoyosLifeTimeMultiplier[
                            projectile.type
                        ];

                if (multiplier > 0f)
                {
                    int vanillaLifetime =
                        Math.Max(
                            1,
                            (int)(
                                multiplier * 60f
                            )
                        );

                    overMaximumLifetime =
                        Math.Max(
                            1,
                            (int)(
                                vanillaLifetime *
                                OverLifetimeMultiplier
                            )
                        );
                }
                else
                {
                    owActive = false;
                }
            }

            // TIGHT WRAPPED

            if (twActive)
            {
                tightReturnTimer = 0;

                tightReturnDelay =
                    GetWeightedTightReturnDelay();
            }
        }

        // TW RANDOM TIMER

        private static int GetWeightedTightReturnDelay()
        {
            float roll =
                Main.rand.NextFloat();

            if (roll < 0.60f)
            {
                return Main.rand.Next(
                    TightEarlyMinimum,
                    TightEarlyMaximum + 1
                );
            }

            if (roll < 0.85f)
            {
                return Main.rand.Next(
                    TightMidMinimum,
                    TightMidMaximum + 1
                );
            }

            if (roll < 0.95f)
            {
                return Main.rand.Next(
                    TightLateMinimum,
                    TightLateMaximum + 1
                );
            }

            return Main.rand.Next(
                TightVeryLateMinimum,
                TightVeryLateMaximum + 1
            );
        }

        // RELEASED UW CHECK

        private static bool HasReleasedUnderYoyo(
            Projectile current)
        {
            if (
                current == null ||
                !current.active
            )
            {
                return false;
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
                    other == null ||
                    !other.active ||
                    other.whoAmI ==
                        current.whoAmI
                )
                {
                    continue;
                }

                if (
                    other.owner !=
                    current.owner
                )
                {
                    continue;
                }

                if (
                    !IsYoyoProjectile(other)
                )
                {
                    continue;
                }

                YoyoAxleGlobalProjectile
                    otherGlobal =
                        other.GetGlobalProjectile<
                            YoyoAxleGlobalProjectile
                        >();

                if (
                    otherGlobal == null ||
                    !otherGlobal.initialized ||
                    !otherGlobal.isYoyo
                )
                {
                    continue;
                }

                if (
                    otherGlobal.underReleased
                )
                {
                    return true;
                }
            }

            return false;
        }

        // PRE AI

        public override bool PreAI(
            Projectile projectile)
        {
            if (
                projectile == null ||
                !projectile.active
            )
            {
                return false;
            }

            Initialize(projectile);

            if (!isYoyo)
            {
                return true;
            }

            Player player =
                GetOwner(projectile);

            if (player == null)
            {
                return true;
            }

            // UW UNEQUIPPED

            if (
                uwActive &&
                !IsUnderWrappedEquipped(player)
            )
            {
                projectile.Kill();

                return false;
            }

            // UNDER WRAPPED

            if (
                uwActive &&
                !overReturn &&
                !tightReturn
            )
            {
                // HOLDING

                if (player.channel)
                {
                    if (
                        !underReleased &&
                        HasReleasedUnderYoyo(projectile)
                    )
                    {
                        projectile.Kill();

                        return false;
                    }

                    /*
                     * Continuously read the held yoyo's speed stat.
                     *
                     * This means UW follows the actual yoyo being
                     * held, rather than reading projectile velocity.
                     */
                    underYoyoSpeed =
                        GetYoyoSpeed(player);

                    return true;
                }

                // RELEASED

                if (!underReleased)
                {
                    if (
                        HasReleasedUnderYoyo(projectile)
                    )
                    {
                        projectile.Kill();

                        return false;
                    }

                    underReleased = true;

                    /*
                     * Copy the held yoyo's actual speed stat.
                     *
                     * This is NOT projectile.velocity.Length().
                     */
                    underCurrentSpeed =
                        underYoyoSpeed;

                    projectile.netUpdate =
                        true;
                }

                /*
                 * UW now moves using the copied yoyo speed.
                 */
                UpdateUnderReleased(
                    projectile,
                    player
                );

                return false;
            }

            // OVER WRAPPED

            if (
                owActive &&
                !overReturn
            )
            {
                if (
                    !IsInfiniteLifetimeYoyo(projectile)
                )
                {
                    overTimer++;

                    if (
                        overTimer >=
                        overMaximumLifetime
                    )
                    {
                        BeginOverReturn(projectile);

                        UpdateForcedReturn(
                            projectile,
                            player,
                            1f
                        );

                        return false;
                    }
                }
            }

            // OW RETURN

            if (overReturn)
            {
                UpdateForcedReturn(
                    projectile,
                    player,
                    1f
                );

                return false;
            }

            // TIGHT WRAPPED

            if (
                twActive &&
                !tightReturn
            )
            {
                tightReturnTimer++;

                if (
                    tightReturnTimer >=
                    tightReturnDelay
                )
                {
                    BeginTightReturn(projectile);

                    UpdateForcedReturn(
                        projectile,
                        player,
                        TightReturnSpeedMultiplier
                    );

                    return false;
                }
            }

            // TW RETURN

            if (tightReturn)
            {
                UpdateForcedReturn(
                    projectile,
                    player,
                    TightReturnSpeedMultiplier
                );

                return false;
            }

            return true;
        }

        // AI

        public override void AI(
            Projectile projectile)
        {
            if (
                projectile == null ||
                !projectile.active ||
                !isYoyo
            )
            {
                return;
            }

            Player player =
                GetOwner(projectile);

            if (player == null)
            {
                return;
            }

            // UW UNEQUIPPED

            if (
                uwActive &&
                !IsUnderWrappedEquipped(player)
            )
            {
                projectile.Kill();

                return;
            }

            // UW

            if (
                uwActive &&
                underReleased
            )
            {
                /*
                 * Keep the released UW yoyo alive.
                 *
                 * Movement is handled only by PreAI().
                 */
                projectile.timeLeft =
                    UnderInfiniteLifetime;
            }
        }

        // POST AI

        public override void PostAI(
            Projectile projectile)
        {
            /*
             * Intentionally empty.
             *
             * OW no longer relies on timeLeft.
             */
        }

        // UW MOUSE FOLLOW

        private void UpdateUnderReleased(
            Projectile projectile,
            Player player)
        {
            if (
                player == null ||
                !player.active ||
                player.dead
            )
            {
                projectile.Kill();

                return;
            }

            Vector2 target =
                Main.MouseWorld;

            Vector2 difference =
                target -
                projectile.Center;

            float distance =
                difference.Length();

            // CLOSE TO MOUSE

            if (
                distance <=
                UnderMouseStopDistance
            )
            {
                underCurrentSpeed =
                    Math.Max(
                        0f,
                        underCurrentSpeed -
                        UnderDeceleration
                    );

                if (
                    underCurrentSpeed <=
                    UnderMinimumFollowSpeed
                )
                {
                    projectile.velocity *=
                        0.80f;
                }
                else
                {
                    Vector2 velocityDirection =
                        projectile.velocity;

                    if (
                        velocityDirection.LengthSquared() >
                        0.001f
                    )
                    {
                        velocityDirection =
                            velocityDirection.SafeNormalize(
                                Vector2.Zero
                            );

                        projectile.velocity =
                            velocityDirection *
                            underCurrentSpeed;
                    }
                }

                projectile.timeLeft =
                    UnderInfiniteLifetime;

                return;
            }

            Vector2 direction =
                difference /
                distance;

            // TARGET SPEED

            /*
             * This is the speed taken from the held yoyo's stats.
             *
             * It is NOT obtained from the UW projectile velocity.
             */
            float targetSpeed =
                underYoyoSpeed;

            if (
                targetSpeed <
                UnderMinimumFollowSpeed
            )
            {
                targetSpeed =
                    UnderMinimumFollowSpeed;
            }

            // ACCELERATION

            if (
                underCurrentSpeed <
                targetSpeed
            )
            {
                underCurrentSpeed =
                    Math.Min(
                        targetSpeed,
                        underCurrentSpeed +
                        UnderAcceleration
                    );
            }

            // DECELERATION

            else if (
                underCurrentSpeed >
                targetSpeed
            )
            {
                underCurrentSpeed =
                    Math.Max(
                        targetSpeed,
                        underCurrentSpeed -
                        UnderDeceleration
                    );
            }

            // MOVE

            projectile.velocity =
                direction *
                underCurrentSpeed;

            projectile.timeLeft =
                UnderInfiniteLifetime;
        }

        // DAMAGE

        public override void ModifyHitNPC(
            Projectile projectile,
            NPC target,
            ref NPC.HitModifiers modifiers)
        {
            if (!isYoyo)
            {
                return;
            }

            if (uwActive)
            {
                modifiers.FinalDamage *=
                    UnderDamageMultiplier;
            }

            if (twActive)
            {
                modifiers.FinalDamage *=
                    TightDamageMultiplier;
            }

            if (overReturn)
            {
                modifiers.FinalDamage *=
                    OverReturnDamageMultiplier;
            }
        }

        // TILE COLLISION

        public override bool OnTileCollide(
            Projectile projectile,
            Vector2 oldVelocity)
        {
            if (!isYoyo)
            {
                return true;
            }

            // LOOSE WRAPPED

            if (lwActive)
            {
                float incomingSpeed =
                    oldVelocity.Length();

                if (
                    incomingSpeed <
                    0.01f
                )
                {
                    incomingSpeed =
                        projectile.velocity.Length();
                }

                if (
                    incomingSpeed <
                    0.01f
                )
                {
                    incomingSpeed = 1f;
                }

                bool hitX =
                    Math.Abs(
                        projectile.velocity.X -
                        oldVelocity.X
                    ) > 0.001f;

                bool hitY =
                    Math.Abs(
                        projectile.velocity.Y -
                        oldVelocity.Y
                    ) > 0.001f;

                if (
                    !hitX &&
                    !hitY
                )
                {
                    if (
                        Math.Abs(oldVelocity.X) >=
                        Math.Abs(oldVelocity.Y)
                    )
                    {
                        hitX = true;
                    }
                    else
                    {
                        hitY = true;
                    }
                }

                Vector2 bounce =
                    oldVelocity;

                if (hitX)
                {
                    bounce.X =
                        -bounce.X;
                }

                if (hitY)
                {
                    bounce.Y =
                        -bounce.Y;
                }

                if (
                    bounce.LengthSquared() <
                    0.001f
                )
                {
                    bounce =
                        -oldVelocity;
                }

                bounce =
                    bounce.SafeNormalize(
                        Vector2.UnitY
                    ) *
                    (
                        incomingSpeed *
                        LooseBounceSpeedMultiplier
                    );

                projectile.velocity =
                    bounce;

                projectile.timeLeft +=
                    LooseLifetimePerBounce;

                looseBounceCount++;

                projectile.netUpdate =
                    true;

                return false;
            }

            // UW

            if (
                uwActive &&
                underReleased
            )
            {
                return false;
            }

            // FORCED RETURN

            if (
                overReturn ||
                tightReturn
            )
            {
                return false;
            }

            return true;
        }

        // BEGIN OW RETURN

        private void BeginOverReturn(
            Projectile projectile)
        {
            if (overReturn)
            {
                return;
            }

            overReturn = true;

            projectile.timeLeft =
                999999;

            projectile.netUpdate =
                true;
        }

        // BEGIN TW RETURN

        private void BeginTightReturn(
            Projectile projectile)
        {
            if (tightReturn)
            {
                return;
            }

            tightReturn = true;

            projectile.timeLeft =
                999999;

            projectile.netUpdate =
                true;
        }

        // FORCED RETURN

        private void UpdateForcedReturn(
            Projectile projectile,
            Player player,
            float speedMultiplier)
        {
            if (
                player == null ||
                !player.active ||
                player.dead
            )
            {
                projectile.Kill();

                return;
            }

            Vector2 difference =
                player.MountedCenter -
                projectile.Center;

            float distance =
                difference.Length();

            if (
                distance <=
                ReturnPlayerDistance
            )
            {
                projectile.Center =
                    player.MountedCenter;

                projectile.velocity =
                    Vector2.Zero;

                projectile.Kill();

                return;
            }

            Vector2 direction =
                difference /
                distance;

            float currentSpeed =
                projectile.velocity.Length();

            if (
                currentSpeed <
                MinimumReturnSpeed
            )
            {
                currentSpeed =
                    MinimumReturnSpeed;
            }

            float returnSpeed =
                currentSpeed *
                speedMultiplier;

            projectile.velocity =
                direction *
                returnSpeed;
        }
    }
}