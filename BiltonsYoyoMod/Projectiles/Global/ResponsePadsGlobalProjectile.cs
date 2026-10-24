using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class ResponsePadsGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // PROJECTILE STATE

        private bool initialized;

        // Has the yoyo begun returning to the player?
        private bool returning;

        // Checkerboard can only trigger one rebound per projectile.
        private bool checkerboardReboundUsed;

        // Checkerboard is currently making its return pass.
        private bool checkerboardRebounding;

        // Target being rebounded toward.
        private int reboundNPC = -1;

        // 30 ticks = 0.5 second.
        private int reboundTimer;

        // BASE VALUES

        private float baseKnockback;

        // SETTINGS

        private const int YoyoAIStyle = 99;

        // AB

        private const float ABBonusKnockback = 1.25f;

        // AF

        private const float AFKnockback = 0.60f;

        private const float AFArmorPenetration = 10f;

        // AS

        private const float ASSpeedMultiplier = 1.10f;
        private const float ASReturnAcceleration = 1.15f;

        // AR

        private const float ARReturnAcceleration = 1.50f;
        private const float ARReturnHitDamage = 1.50f;

        // ACB

        // +20% knockback + +10% knockback = +30%.
        private const float ACBKnockback = 1.30f;

        // User requested 100%.
        private const float ACBReboundChance = 1.00f;

        // Half a second.
        private const int ACBReboundDuration = 30;

        // Make the rebound noticeably fast.
        private const float ACBReboundSpeedMultiplier = 2.00f;

        // Extra distance past the target.
        private const float ACBOvershootDistance = 80f;

        // SPAWN

        public override void OnSpawn(
            Projectile projectile,
            IEntitySource source)
        {
            if (projectile.aiStyle != YoyoAIStyle)
                return;

            baseKnockback = projectile.knockBack;
        }

        // AI

        public override void AI(
            Projectile projectile)
        {
            if (!projectile.active)
                return;

            if (projectile.aiStyle != YoyoAIStyle)
                return;

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return;
            }

            Player player =
                Main.player[projectile.owner];

            if (!player.active)
                return;

            ResponsePadsPlayer pads =
                player.GetModPlayer<ResponsePadsPlayer>();

            if (!HasAnyResponsePads(pads))
                return;

            // INITIALIZE

            if (!initialized)
            {
                initialized = true;

                ApplyKnockbackBonus(
                    projectile,
                    pads
                );

                /*
                 * Slimline's +10% yoyo speed is applied once to the
                 * initial projectile velocity.
                 *
                 * It does NOT continuously multiply velocity.
                 */
                if (pads.ResponsePadsAS &&
                    projectile.velocity.LengthSquared() > 0.001f)
                {
                    projectile.velocity *=
                        ASSpeedMultiplier;
                }
            }

            // CHECKERBOARD REBOUND

            if (checkerboardRebounding)
            {
                UpdateCheckerboardRebound(
                    projectile
                );

                return;
            }

            // RECALL DETECTION

            UpdateRecallState(
                projectile,
                player,
                pads
            );
        }

        // KNOCKBACK

        private void ApplyKnockbackBonus(
            Projectile projectile,
            ResponsePadsPlayer pads)
        {
            float multiplier = 1f;

            if (pads.ResponsePadsAB)
            {
                multiplier *=
                    ABBonusKnockback;
            }

            if (pads.ResponsePadsAF)
            {
                multiplier *=
                    AFKnockback;
            }

            if (pads.ResponsePadsACB)
            {
                multiplier *=
                    ACBKnockback;
            }

            projectile.knockBack =
                baseKnockback *
                multiplier;
        }

        // AF ARMOR PENETRATION

        public override void ModifyHitNPC(
            Projectile projectile,
            NPC target,
            ref NPC.HitModifiers modifiers)
        {
            if (!projectile.active)
                return;

            if (projectile.aiStyle != YoyoAIStyle)
                return;

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return;
            }

            Player player =
                Main.player[projectile.owner];

            if (!player.active)
                return;

            ResponsePadsPlayer pads =
                player.GetModPlayer<ResponsePadsPlayer>();

            // AF: +10 ARMOR PENETRATION

            if (pads.ResponsePadsAF)
            {
                /*
                 * This modifies THIS YOYO HIT only.
                 *
                 * It no longer modifies the player's global melee
                 * armor penetration.
                 */
                modifiers.ArmorPenetration +=
                    AFArmorPenetration;
            }

            // AR: 1.5× DAMAGE WHILE RETURNING

            if (pads.ResponsePadsAR &&
                returning)
            {
                modifiers.FinalDamage *=
                    ARReturnHitDamage;
            }
        }

        // RECALL DETECTION

        private void UpdateRecallState(
            Projectile projectile,
            Player player,
            ResponsePadsPlayer pads)
        {
            /*
             * Once the player releases the yoyo, Terraria's yoyo AI
             * begins its own recall behavior.
             *
             * We only modify the velocity once at the transition.
             */
            if (returning)
                return;

            if (player.channel)
                return;

            returning = true;

            ApplyRecallAcceleration(
                projectile,
                player,
                pads
            );
        }

        // RECALL ACCELERATION

        private void ApplyRecallAcceleration(
            Projectile projectile,
            Player player,
            ResponsePadsPlayer pads)
        {
            Vector2 toPlayer =
                player.MountedCenter -
                projectile.Center;

            if (toPlayer.LengthSquared() <= 0.001f)
                return;

            toPlayer.Normalize();

            float currentSpeed =
                projectile.velocity.Length();

            if (currentSpeed <= 0.01f)
                return;

            float accelerationMultiplier = 1f;

            // AS: +15% return acceleration.
            if (pads.ResponsePadsAS)
            {
                accelerationMultiplier *=
                    ASReturnAcceleration;
            }

            // AR: 1.5× snap acceleration.
            if (pads.ResponsePadsAR)
            {
                accelerationMultiplier *=
                    ARReturnAcceleration;
            }

            projectile.velocity =
                toPlayer *
                currentSpeed *
                accelerationMultiplier;

            projectile.netUpdate = true;
        }

        // NPC HIT

        public override void OnHitNPC(
            Projectile projectile,
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            if (!projectile.active)
                return;

            if (projectile.aiStyle != YoyoAIStyle)
                return;

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return;
            }

            Player player =
                Main.player[projectile.owner];

            if (!player.active)
                return;

            ResponsePadsPlayer pads =
                player.GetModPlayer<ResponsePadsPlayer>();

            // CHECKERBOARD

            if (!pads.ResponsePadsACB)
                return;

            // Must actually be returning.
            if (!returning)
                return;

            // Only one rebound per yoyo.
            if (checkerboardReboundUsed)
                return;

            /*
             * 100% chance by design.
             *
             * Keep the random check so changing the constant later
             * still works normally.
             */
            if (Main.rand.NextFloat() >=
                ACBReboundChance)
            {
                return;
            }

            checkerboardReboundUsed = true;
            checkerboardRebounding = true;

            reboundNPC =
                target.whoAmI;

            reboundTimer =
                ACBReboundDuration;

            StartCheckerboardRebound(
                projectile,
                target
            );
        }

        // START CHECKERBOARD REBOUND

        private void StartCheckerboardRebound(
            Projectile projectile,
            NPC target)
        {
            Vector2 toTarget =
                target.Center -
                projectile.Center;

            if (toTarget.LengthSquared() <= 0.001f)
            {
                StopCheckerboardRebound(
                    projectile
                );

                return;
            }

            toTarget.Normalize();

            float speed =
                projectile.velocity.Length();

            if (speed < 6f)
                speed = 6f;

            /*
             * Double the current speed so the rebound is clearly
             * noticeable.
             */
            speed *=
                ACBReboundSpeedMultiplier;

            projectile.velocity =
                toTarget *
                speed;

            projectile.netUpdate = true;
        }

        // CHECKERBOARD REBOUND

        private void UpdateCheckerboardRebound(
            Projectile projectile)
        {
            if (reboundNPC < 0 ||
                reboundNPC >= Main.maxNPCs)
            {
                StopCheckerboardRebound(
                    projectile
                );

                return;
            }

            NPC target =
                Main.npc[reboundNPC];

            if (!target.active ||
                target.dontTakeDamage)
            {
                StopCheckerboardRebound(
                    projectile
                );

                return;
            }

            // Continue moving toward the target, but don't constantly
            // overwrite the speed. This prevents orbiting behavior.
            Vector2 toTarget =
                target.Center -
                projectile.Center;

            if (toTarget.LengthSquared() <= 0.001f)
            {
                reboundTimer--;

                if (reboundTimer <= 0)
                {
                    StopCheckerboardRebound(
                        projectile
                    );
                }

                return;
            }

            toTarget.Normalize();

            float speed =
                projectile.velocity.Length();

            if (speed < 8f)
                speed = 8f;

            /*
             * Keep the yoyo aimed toward the enemy while preserving
             * its fast rebound speed.
             */
            projectile.velocity =
                Vector2.Lerp(
                    projectile.velocity,
                    toTarget * speed,
                    0.35f
                );

            reboundTimer--;

            /*
             * The rebound intentionally lasts 0.5 seconds.
             *
             * The yoyo travels through/over the target rather than
             * stopping on it, creating the requested overshoot.
             */
            if (reboundTimer <= 0)
            {
                StopCheckerboardRebound(
                    projectile
                );
            }

            projectile.netUpdate = true;
        }

        // STOP REBOUND

        private void StopCheckerboardRebound(
            Projectile projectile)
        {
            checkerboardRebounding = false;

            reboundNPC = -1;
            reboundTimer = 0;

            /*
             * We do NOT manually aim the yoyo back here.
             *
             * Terraria's normal yoyo AI resumes control.
             */
        }

        // HELPER

        private bool HasAnyResponsePads(
            ResponsePadsPlayer pads)
        {
            return pads.ResponsePadsAB ||
                   pads.ResponsePadsAF ||
                   pads.ResponsePadsAS ||
                   pads.ResponsePadsAR ||
                   pads.ResponsePadsACB;
        }
    }
}