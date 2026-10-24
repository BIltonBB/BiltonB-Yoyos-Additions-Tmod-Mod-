// TensionBearingALGlobalProjectile.cs
// LUBED BEARING

using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items.Players;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class TensionBearingALGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private bool initialized;
        private bool forcedReturn;

        private int originalTimeLeft;
        private int baseDamage;
        private float baseKnockback;

        private const float LifetimeMultiplier = 1.25f;
        private const float KnockbackMultiplier = 0.85f;
        private const float DamageMultiplier = 1.05f;

        // IMPORTANT:
        // No projectile velocity manipulation here.
        // Vanilla/YoyosMod controls yoyo movement normally.

        private const float ReturnSpeedMultiplier = 1.50f;
        private const float ReturnDistance = 20f;

        public override void OnSpawn(
            Projectile projectile,
            IEntitySource source)
        {
            if (projectile.aiStyle != ProjAIStyleID.Yoyo)
                return;

            originalTimeLeft = projectile.timeLeft;
            baseDamage = projectile.damage;
            baseKnockback = projectile.knockBack;
        }

        public override void AI(
            Projectile projectile)
        {
            if (!projectile.active ||
                projectile.aiStyle != ProjAIStyleID.Yoyo)
            {
                return;
            }

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return;
            }

            Player player = Main.player[projectile.owner];

            if (!player.active)
                return;

            if (!player.GetModPlayer<TensionBearingALPlayer>()
                .TensionBearingAL)
            {
                return;
            }

            // +25% LIFETIME

            if (!initialized)
            {
                initialized = true;

                if (originalTimeLeft > 0)
                {
                    projectile.timeLeft = Math.Max(
                        1,
                        (int)Math.Round(
                            originalTimeLeft *
                            LifetimeMultiplier
                        )
                    );
                }

                // -15% knockback.
                projectile.knockBack =
                    baseKnockback *
                    KnockbackMultiplier;

                // +5% damage.
                projectile.damage = Math.Max(
                    1,
                    (int)Math.Round(
                        baseDamage *
                        DamageMultiplier
                    )
                );
            }

            // RETURN

            if (!forcedReturn)
                return;

            Vector2 toPlayer =
                player.MountedCenter -
                projectile.Center;

            float distance = toPlayer.Length();

            if (distance <= ReturnDistance)
            {
                projectile.Kill();
                return;
            }

            if (distance > 0.01f)
            {
                toPlayer /= distance;

                projectile.velocity =
                    toPlayer *
                    GetCurrentYoyoSpeed(player) *
                    ReturnSpeedMultiplier;

                projectile.netUpdate = true;
            }
        }

        private float GetCurrentYoyoSpeed(
            Player player)
        {
            Item heldItem = player.HeldItem;

            if (!heldItem.IsAir &&
                heldItem.shootSpeed > 0f)
            {
                return heldItem.shootSpeed;
            }

            return 1f;
        }

        public override bool OnTileCollide(
            Projectile projectile,
            Vector2 oldVelocity)
        {
            if (!projectile.active ||
                projectile.aiStyle != ProjAIStyleID.Yoyo)
            {
                return true;
            }

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return true;
            }

            Player player = Main.player[projectile.owner];

            if (!player.active)
                return true;

            if (!player.GetModPlayer<TensionBearingALPlayer>()
                .TensionBearingAL)
            {
                return true;
            }

            forcedReturn = true;

            Vector2 toPlayer =
                player.MountedCenter -
                projectile.Center;

            if (toPlayer.LengthSquared() > 0.001f)
            {
                toPlayer.Normalize();

                projectile.velocity =
                    toPlayer *
                    GetCurrentYoyoSpeed(player) *
                    ReturnSpeedMultiplier;
            }

            projectile.netUpdate = true;

            return false;
        }
    }
}