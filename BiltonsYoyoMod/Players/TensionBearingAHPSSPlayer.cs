using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Players
{
    public class TensionBearingAHPSSPlayer : ModPlayer
    {
        public bool TensionBearingAHPSS;

        public override void ResetEffects()
        {
            TensionBearingAHPSS = false;
        }
    }
}