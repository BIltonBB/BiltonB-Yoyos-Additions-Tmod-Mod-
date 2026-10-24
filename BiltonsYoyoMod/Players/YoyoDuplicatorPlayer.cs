using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
    public class YoyoDuplicatorPlayer : ModPlayer
    {
        // 0 = no duplicator
        // 1 = Yoyo Duplicator       (+1)
        // 2 = Advanced Duplicator   (+2)
        // 3 = High Durability       (+3)
        // 4 = Precision Machined    (+4)

        public int DuplicatorLevel;

        public override void ResetEffects()
        {
            DuplicatorLevel = 0;
        }

        public int GetCopies()
        {
            return 1 + DuplicatorLevel;
        }
    }
}