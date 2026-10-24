// TensionBearingGlobalProjectile.cs
// PROFESSIONAL / AP

using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class TensionBearingGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private bool initialized;
        private bool forcedReturn;

        private int originalTimeLeft;

        private const float LifetimeMultiplier = 1.50f;
        private const float ReturnSpeedMultiplier = 1.25f;
        private const float ReturnDistance = 20f;

        public override void OnSpawn(
            Projectile projectile,
            IEntitySource source)
        {
            if (projectile.aiStyle == ProjAIStyleID.Yoyo)
            {
                originalTimeLeft = projectile.timeLeft;
            }
        }

        public override void AI(Projectile projectile)
        {
            if (!projectile.active)
                return;

            if (projectile.aiStyle != ProjAIStyleID.Yoyo)
                return;

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
                return;

            Player player = Main.player[projectile.owner];

            if (!player.active)
                return;

            if (!player.GetModPlayer<TensionBearingPlayer>().TensionBearing)
                return;

            // +50% LIFETIME

            if (!initialized)
            {
                initialized = true;

                // Preserve infinite-lifetime yoyos.
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
            }

            // AP TILE RETURN

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

        private float GetCurrentYoyoSpeed(Player player)
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

            if (!player.GetModPlayer<TensionBearingPlayer>().TensionBearing)
                return true;

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

            // Keep the yoyo alive.
            return false;
        }
    }
}