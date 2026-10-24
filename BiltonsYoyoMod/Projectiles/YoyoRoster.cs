using System.Collections.Generic;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // One-line rosters so the "special" and "vibe" lineups are editable here.
    // Specials: animated inventory icons + depth echo sonar.
    // Vibe: death-vigil yoyos (the mournful ones).
    public static class YoyoRoster
    {
        private static HashSet<int> specials;
        private static HashSet<int> vibes;

        public static bool IsSpecial(int type)
        {
            if (specials == null)
            {
                specials = new HashSet<int>();
                try
                {
                    specials.Add(ModContent.ProjectileType<DeveloperYoyoProj>());
                    specials.Add(ModContent.ProjectileType<BetsyYoyoProj>());
                    specials.Add(ModContent.ProjectileType<MartianYoyoProj>());
                    specials.Add(ModContent.ProjectileType<DarkMageYoyoProj>());
                    specials.Add(ModContent.ProjectileType<DreadnautilusYoyoProj>());
                }
                catch
                {
                }
            }
            return specials.Contains(type);
        }

        public static bool HasVibe(int type)
        {
            if (vibes == null)
            {
                vibes = new HashSet<int>();
                try
                {
                    vibes.Add(ModContent.ProjectileType<DeveloperYoyoProj>());
                    vibes.Add(ModContent.ProjectileType<SantaYoyoProj>());
                    vibes.Add(ModContent.ProjectileType<DreadnautilusYoyoProj>());
                    vibes.Add(ModContent.ProjectileType<DarkMageYoyoProj>());
                    vibes.Add(ModContent.ProjectileType<BetsyYoyoProj>());
                }
                catch
                {
                }
            }
            return vibes.Contains(type);
        }
    }
}
