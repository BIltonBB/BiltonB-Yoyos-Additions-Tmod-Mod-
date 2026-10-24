using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
    public class TensionBearingPlayer : ModPlayer
    {
        public bool TensionBearing;

        public override void ResetEffects()
        {
            TensionBearing = false;
        }
    }
}