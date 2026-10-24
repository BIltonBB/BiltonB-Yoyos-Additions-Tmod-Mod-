using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Players
{
    public class TensionBearingADPlayer : ModPlayer
    {
        public bool TensionBearingAD;

        public override void ResetEffects()
        {
            TensionBearingAD = false;
        }
    }
}