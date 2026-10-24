using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
    public class TierEmblemPlayer : ModPlayer
    {
        // Helper so other code (tooltips, shop, etc.) can query progression safely.
        public static bool HasDefeated(int requiredTier) => requiredTier switch
        {
            0 => NPC.downedBoss1,
            1 => NPC.downedBoss1,
            2 => NPC.downedBoss2,
            3 => NPC.downedQueenBee,
            4 => NPC.downedBoss3,
            5 => Main.hardMode,
            6 => NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3,
            7 => NPC.downedPlantBoss,
            8 => NPC.downedGolemBoss,
            9 => NPC.downedAncientCultist,
            10 => NPC.downedMoonlord,
            _ => true
        };
    }
}