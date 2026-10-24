// TensionBearingAHPSSGlobalProjectile.cs
// HIGH PRECISION STAINLESS STEEL

using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items.Players;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class TensionBearingAHPSSGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private bool initialized;
        private bool forcedReturn;
        private bool explosionChecked;

        private int originalTimeLeft;
        private int baseDamage;
        private float baseKnockback;

        private const float LifetimeMultiplier = 1.35f;
        private const float KnockbackMultiplier = 1.05f;
        private const float DamageMultiplier = 1.05f;

        // No custom velocity manipulation.
        // Vanilla/YoyosMod controls the yoyo normally.

        private const float ReturnSpeedMultiplier = 1.75f;
        private const float ReturnDistance = 20f;

        private const float ExplosionChance = 0.05f;
        private const int ExplosionDamage = 50;

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

            if (!player.GetModPlayer<
                    TensionBearingAHPSSPlayer>()
                .TensionBearingAHPSS)
            {
                return;
            }

            // +35% LIFETIME
            // +5% KNOCKBACK
            // +5% DAMAGE

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

                projectile.knockBack =
                    baseKnockback *
                    KnockbackMultiplier;

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
                TrySnapExplosion(
                    projectile,
                    player
                );

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

            Player player =
                Main.player[projectile.owner];

            if (!player.active)
                return true;

            if (!player.GetModPlayer<
                    TensionBearingAHPSSPlayer>()
                .TensionBearingAHPSS)
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

        private void TrySnapExplosion(
            Projectile projectile,
            Player player)
        {
            if (explosionChecked)
                return;

            explosionChecked = true;

            if (Main.netMode ==
                NetmodeID.MultiplayerClient &&
                projectile.owner != Main.myPlayer)
            {
                return;
            }

            if (Main.rand.NextFloat() >=
                ExplosionChance)
            {
                return;
            }

            CreateSnapExplosionVisual(
                projectile.Center
            );

            player.Hurt(
                PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(player.name + " was caught by the snapping bearing.")),
                ExplosionDamage,
                0
            );
        }

        private void CreateSnapExplosionVisual(
            Vector2 center)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(
                SoundID.Item14,
                center
            );

            for (int i = 0; i < 35; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        5f,
                        5f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Smoke,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            1.1f,
                            1.9f
                        )
                    );

                dust.noGravity = true;
            }

            for (int i = 0; i < 24; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        7f,
                        7f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Torch,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.4f
                        )
                    );

                dust.noGravity = true;
            }

            Lighting.AddLight(
                center,
                1.2f,
                0.75f,
                0.3f
            );
        }
    }
}