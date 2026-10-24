using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace BiltonsYoyoMod.Systems
{
    public static class YoyoTrickSystem
    {
        // GET MAIN YOYO

        public static Projectile GetMainYoyo(
            Player player
        )
        {
            if (player == null ||
                !player.active)
            {
                return null;
            }

            // 1. TERRARIA'S HELD PROJECTILE
            //
            // This is the primary source of truth.
            //
            // Do NOT replace this with distance checks.

            int heldProj =
                player.heldProj;

            if (heldProj >= 0 &&
                heldProj < Main.maxProjectiles)
            {
                Projectile projectile =
                    Main.projectile[heldProj];

                if (IsValidHeldYoyo(
                        projectile,
                        player))
                {
                    return projectile;
                }
            }

            // 2. EXACT ITEM SHOOT TYPE FALLBACK
            //
            // If heldProj is temporarily unavailable, use the
            // projectile type fired by the player's held yoyo item.
            //
            // This prevents counterweights from being selected simply
            // because they also use yoyo AI.

            Item heldItem =
                player.HeldItem;

            if (heldItem == null ||
                heldItem.IsAir)
            {
                return null;
            }

            int expectedYoyoType =
                heldItem.shoot;

            if (expectedYoyoType <= 0)
                return null;

            for (int i = 0;
                 i < Main.maxProjectiles;
                 i++)
            {
                Projectile projectile =
                    Main.projectile[i];

                if (!IsValidExactYoyo(
                        projectile,
                        player,
                        expectedYoyoType))
                {
                    continue;
                }

                return projectile;
            }

            return null;
        }

        // HELD PROJECTILE VALIDATION

        private static bool IsValidHeldYoyo(
            Projectile projectile,
            Player player
        )
        {
            if (projectile == null ||
                !projectile.active)
            {
                return false;
            }

            if (projectile.owner !=
                player.whoAmI)
            {
                return false;
            }

            // The projectile Terraria explicitly says is held must
            // still actually be a yoyo.
            if (!IsYoyoProjectile(projectile))
                return false;

            return true;
        }

        // EXACT FALLBACK YOYO VALIDATION

        private static bool IsValidExactYoyo(
            Projectile projectile,
            Player player,
            int expectedYoyoType
        )
        {
            if (projectile == null ||
                !projectile.active)
            {
                return false;
            }

            if (projectile.owner !=
                player.whoAmI)
            {
                return false;
            }

            if (projectile.type !=
                expectedYoyoType)
            {
                return false;
            }

            if (!IsYoyoProjectile(projectile))
                return false;

            return true;
        }

        // YOYO CHECK

        public static bool IsYoyoProjectile(
            Projectile projectile
        )
        {
            if (projectile == null)
                return false;

            if (!projectile.active)
                return false;

            return projectile.aiStyle ==
                   ProjAIStyleID.Yoyo;
        }

        // COMPATIBILITY

        public static Projectile GetHeldYoyo(
            Player player
        )
        {
            return GetMainYoyo(player);
        }

        public static bool HasMainYoyo(
            Player player
        )
        {
            return GetMainYoyo(player) != null;
        }
    }
}