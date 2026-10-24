using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class WildStringsGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // ACTIVATION STATE

        public bool WildStringsActivatedThisThrow { get; private set; }

        // All normal yoyo projectiles created by the same release
        // share this ID.
        public int WildStringsReleaseId { get; private set; } = -1;

        // Number of Wild String sets to spawn.
        //
        // Normal:
        // 1
        //
        // Yoyo Duplicator:
        // 2
        //
        // Advanced:
        // 3
        //
        // High Durability:
        // 4
        //
        // Precision:
        // 5
        public int WildStringsSpawnMultiplier { get; private set; } = 1;

        // True when this yoyo is treated as a duplicator yoyo.
        public bool IsYoyoDuplicatorDuplicate { get; private set; }

        // YOYO CHECK

        public static bool IsYoyoProjectile(
            Projectile projectile)
        {
            return projectile.active &&
                   projectile.aiStyle == ProjAIStyleID.Yoyo;
        }

        // YOYO SPAWN

        public override void OnSpawn(
            Projectile projectile,
            IEntitySource source)
        {
            if (!IsYoyoProjectile(projectile))
                return;

            WildStringsActivatedThisThrow = false;
            WildStringsSpawnMultiplier = 1;
            IsYoyoDuplicatorDuplicate = false;

            Player player =
                Main.player[projectile.owner];

            if (!player.active)
                return;

            bool anotherYoyoAlreadyExists = false;

            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++)
            {
                if (i == projectile.whoAmI)
                    continue;

                Projectile other =
                    Main.projectile[i];

                if (
                    !other.active ||
                    other.owner != projectile.owner
                )
                {
                    continue;
                }

                if (IsYoyoProjectile(other))
                {
                    anotherYoyoAlreadyExists = true;
                    break;
                }
            }

            if (!anotherYoyoAlreadyExists)
            {
                player.GetModPlayer<
                    WildStringsPlayer>()
                    .WildStringsCurrentReleaseId++;
            }

            WildStringsReleaseId =
                player.GetModPlayer<
                    WildStringsPlayer>()
                    .WildStringsCurrentReleaseId;

            // YOYO DUPLICATOR

            YoyoDuplicatorPlayer duplicator =
                player.GetModPlayer<
                    YoyoDuplicatorPlayer>();

            WildStringsSpawnMultiplier =
                duplicator.GetCopies();

            IsYoyoDuplicatorDuplicate =
                duplicator.DuplicatorLevel > 0;
        }

        // YOYO HIT

        public override void OnHitNPC(
            Projectile projectile,
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            if (!IsYoyoProjectile(projectile))
                return;

            TryActivateFromYoyoHit(
                projectile,
                target);
        }

        // ACTIVATE

        public static bool TryActivateFromYoyoHit(
            Projectile yoyoProjectile,
            NPC target)
        {
            if (
                !IsYoyoProjectile(yoyoProjectile) ||
                !yoyoProjectile.active
            )
            {
                return false;
            }

            if (
                yoyoProjectile.owner < 0 ||
                yoyoProjectile.owner >= Main.maxPlayers
            )
            {
                return false;
            }

            Player player =
                Main.player[yoyoProjectile.owner];

            if (
                !player.active ||
                player.dead ||
                target.friendly ||
                target.dontTakeDamage
            )
            {
                return false;
            }

            if (
                Main.netMode ==
                    NetmodeID.MultiplayerClient &&
                Main.myPlayer !=
                    yoyoProjectile.owner
            )
            {
                return false;
            }

            WildStringsPlayer wild =
                player.GetModPlayer<
                    WildStringsPlayer>();

            // CHECK ALL FIVE ACCESSORIES

            if (
                !wild.Cotton &&
                !wild.PolyCotton &&
                !wild.Polyester &&
                !wild.Nylon &&
                !wild.Kevlar
            )
            {
                return false;
            }

            WildStringsGlobalProjectile state =
                yoyoProjectile.GetGlobalProjectile<
                    WildStringsGlobalProjectile>();

            // EXACT PROJECTILE SAFETY GATE

            if (state.WildStringsActivatedThisThrow)
                return false;

            // NORMAL RELEASE GATE

            if (
                !state.IsYoyoDuplicatorDuplicate &&
                wild.WildStringsConsumedReleaseId ==
                    state.WildStringsReleaseId
            )
            {
                return false;
            }

            // ACTIVATE

            state.WildStringsActivatedThisThrow =
                true;

            if (!state.IsYoyoDuplicatorDuplicate)
            {
                wild.WildStringsConsumedReleaseId =
                    state.WildStringsReleaseId;
            }

            SpawnForAccessorySet(
                player,
                wild,
                state.WildStringsSpawnMultiplier);

            return true;
        }

        // YOYO DUPLICATOR

        public static void SetWildStringsSpawnMultiplier(
            Projectile duplicateYoyo,
            int multiplier)
        {
            if (!IsYoyoProjectile(duplicateYoyo))
                return;

            if (multiplier < 1)
                multiplier = 1;

            if (multiplier > 5)
                multiplier = 5;

            WildStringsGlobalProjectile state =
                duplicateYoyo.GetGlobalProjectile<
                    WildStringsGlobalProjectile>();

            state.WildStringsSpawnMultiplier =
                multiplier;

            state.IsYoyoDuplicatorDuplicate =
                multiplier > 1;

            state.WildStringsActivatedThisThrow =
                false;
        }

        // SPAWN ACCESSORY STRINGS

        private static void SpawnForAccessorySet(
            Player player,
            WildStringsPlayer wild,
            int multiplier)
        {
            // COTTON

            if (wild.Cotton)
            {
                SpawnRepeated(
                    player,
                    ModContent.ProjectileType<
                        WildStringsACProj>(),
                    5,
                    "WildStrings_Cotton",
                    multiplier);
            }

            // POLY-COTTON

            if (wild.PolyCotton)
            {
                SpawnRepeated(
                    player,
                    ModContent.ProjectileType<
                        WildStringsAPCProj>(),
                    25,
                    "WildStrings_PolyCotton",
                    multiplier);
            }

            // POLYESTER

            if (wild.Polyester)
            {
                SpawnRepeated(
                    player,
                    ModContent.ProjectileType<
                        WildStringsAPProj>(),
                    50,
                    "WildStrings_Polyester",
                    multiplier);
            }

            // NYLON

            if (wild.Nylon)
            {
                SpawnRepeated(
                    player,
                    ModContent.ProjectileType<
                        WildStringsANProj>(),
                    65,
                    "WildStrings_Nylon",
                    multiplier);
            }

            // KEVLAR

            if (wild.Kevlar)
            {
                SpawnRepeated(
                    player,
                    ModContent.ProjectileType<
                        WildStringsAKProj>(),
                    120,
                    "WildStrings_Kevlar",
                    multiplier);
            }
        }

        // REPEATED SPAWN

        private static void SpawnRepeated(
            Player player,
            int type,
            int damage,
            string sourceName,
            int count)
        {
            if (count < 1)
                count = 1;

            if (count > 5)
                count = 5;

            for (
                int i = 0;
                i < count;
                i++
            )
            {
                // The original string always spawns.
                // Every additional string created by the Yoyo
                // Duplicator has an independent 50% chance to spawn.
                if (i > 0 &&
                    Main.rand.NextFloat() >= 0.5f)
                {
                    continue;
                }

                int spawned =
                    Projectile.NewProjectile(
                        player.GetSource_Misc(
                            sourceName),
                        player.Center,
                        Vector2.Zero,
                        type,
                        damage,
                        0f,
                        player.whoAmI);

                if (
                    spawned >= 0 &&
                    spawned < Main.maxProjectiles
                )
                {
                    Main.projectile[spawned]
                        .netUpdate = true;
                }
            }
        }
    }
}