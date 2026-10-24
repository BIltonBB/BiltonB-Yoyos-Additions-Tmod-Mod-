using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Players
{
    public class TensionBearingALPlayer : ModPlayer
    {
        public bool TensionBearingAL;

        public override void ResetEffects()
        {
            TensionBearingAL = false;
        }
    }
}