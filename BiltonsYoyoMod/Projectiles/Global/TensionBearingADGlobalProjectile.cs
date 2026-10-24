// TensionBearingADGlobalProjectile.cs
// DRY BEARING

using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items.Players;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class TensionBearingADGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private bool initialized;
        private bool forcedReturn;

        private int auraTimer;
        private int visualTimer;
        private int originalTimeLeft;

        private const float LifetimeMultiplier = 0.70f;

        // Every 0.4 seconds.
        private const int AuraInterval = 24;

        // Keep the enlarged aura.
        private const float AuraRadius = 32f;

        private const float AuraDamageMultiplier = 0.25f;

        // Dry return.
        private const float ReturnSpeedMultiplier = 1.50f;
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

            TensionBearingADPlayer modPlayer =
                player.GetModPlayer<TensionBearingADPlayer>();

            if (!modPlayer.TensionBearingAD)
                return;

            // -30% LIFETIME

            if (!initialized)
            {
                initialized = true;

                // Infinite-lifetime yoyos remain infinite.
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

            // AURA DAMAGE

            auraTimer++;

            if (auraTimer >= AuraInterval)
            {
                auraTimer = 0;
                ShredNearbyEnemies(projectile);
            }

            // AURA VISUAL

            visualTimer++;

            if (visualTimer >= 4)
            {
                visualTimer = 0;
                CreateAuraDust(projectile);
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

        private void ShredNearbyEnemies(
            Projectile projectile)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient &&
                projectile.owner != Main.myPlayer)
            {
                return;
            }

            int auraDamage = Math.Max(
                1,
                (int)Math.Round(
                    projectile.damage *
                    AuraDamageMultiplier
                )
            );

            float radiusSquared =
                AuraRadius * AuraRadius;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active ||
                    npc.friendly ||
                    npc.dontTakeDamage)
                {
                    continue;
                }

                Vector2 closestPoint =
                    npc.Hitbox.ClosestPointInRect(
                        projectile.Center
                    );

                if (Vector2.DistanceSquared(
                        closestPoint,
                        projectile.Center) >
                    radiusSquared)
                {
                    continue;
                }

                int hitDirection =
                    npc.Center.X >= projectile.Center.X
                        ? 1
                        : -1;

                npc.StrikeNPC(
                    new NPC.HitInfo
                    {
                        Damage = auraDamage,
                        Knockback = 0f,
                        HitDirection = hitDirection,
                        Crit = false
                    }
                );
            }
        }

        private void CreateAuraDust(
            Projectile projectile)
        {
            if (Main.dedServ)
                return;

            // EXACTLY 12 dusts.
            const int DustCount = 12;

            float baseAngle =
                projectile.whoAmI * 0.12f +
                Main.GlobalTimeWrappedHourly * 2f;

            for (int i = 0; i < DustCount; i++)
            {
                float angle =
                    baseAngle +
                    MathHelper.TwoPi *
                    i /
                    DustCount;

                Vector2 radialDirection =
                    angle.ToRotationVector2();

                Vector2 position =
                    projectile.Center +
                    radialDirection *
                    AuraRadius;

                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Smoke,
                        radialDirection * 0.25f,
                        100,
                        default,
                        0.7f
                    );

                dust.noGravity = true;
            }
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

            if (!player.GetModPlayer<TensionBearingADPlayer>()
                .TensionBearingAD)
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