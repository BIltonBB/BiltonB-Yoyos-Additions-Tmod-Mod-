using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Players
{
    public class WildStringsPlayer : ModPlayer
    {
        public bool Cotton;
        public bool PolyCotton;
        public bool Polyester;

        public bool Nylon;
        public bool Kevlar;

        // Stable identity for the current yoyo release.
        public int WildStringsCurrentReleaseId;

        public int WildStringsConsumedReleaseId = -1;

        // Cached orbit-string types (was 5 ModContent lookups per tick).
        private static int[] orbitTypes;

        // Throttled yoyo-presence scan (was a 1000-projectile sweep
        // every tick per player). Strings linger <1/6s extra at most.
        private int scanTimer;

        public override void ResetEffects()
        {
            Cotton = false;
            PolyCotton = false;
            Polyester = false;

            Nylon = false;
            Kevlar = false;
        }

        public override void PostUpdate()
        {
            if (++scanTimer < 10)
                return;
            scanTimer = 0;

            bool hasYoyo =
                Player.active &&
                !Player.dead &&
                HasAnyActiveYoyo(Player);

            /*
             * Player-following Wild Strings belong to the active
             * yoyo throw.
             *
             * The new Nylon/Kevlar strings are also normal
             * player-following Wild String projectiles, so this
             * cleanup only happens when the actual yoyo release
             * is completely gone.
             */
            if (!hasYoyo)
            {
                KillOwnedOrbitStrings();
            }
        }

        private static bool HasAnyActiveYoyo(
            Player player)
        {
            for (int i = 0;
                i < Main.maxProjectiles;
                i++)
            {
                Projectile projectile =
                    Main.projectile[i];

                if (!projectile.active ||
                    projectile.owner !=
                        player.whoAmI)
                {
                    continue;
                }

                if (WildStringsGlobalProjectile
                    .IsYoyoProjectile(
                        projectile))
                {
                    return true;
                }
            }

            return false;
        }

        private void KillOwnedOrbitStrings()
        {
            if (orbitTypes == null) {
                orbitTypes = new int[] {
                    ModContent.ProjectileType<
                        WildStringsACProj>(),
                    ModContent.ProjectileType<
                        WildStringsAPCProj>(),
                    ModContent.ProjectileType<
                        WildStringsAPProj>(),
                    ModContent.ProjectileType<
                        WildStringsANProj>(),
                    ModContent.ProjectileType<
                        WildStringsAKProj>(),
                };
            }

            for (int i = 0;
                i < Main.maxProjectiles;
                i++)
            {
                Projectile projectile =
                    Main.projectile[i];

                if (!projectile.active ||
                    projectile.owner !=
                        Player.whoAmI)
                {
                    continue;
                }

                int t = projectile.type;
                if (t == orbitTypes[0] ||
                    t == orbitTypes[1] ||
                    t == orbitTypes[2] ||
                    t == orbitTypes[3] ||
                    t == orbitTypes[4])
                {
                    projectile.Kill();
                }
            }
        }
    }
}