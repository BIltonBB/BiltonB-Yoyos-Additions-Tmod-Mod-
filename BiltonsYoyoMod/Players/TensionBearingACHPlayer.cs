using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Players
{
    public class TensionBearingACHPlayer : ModPlayer
    {
        public bool TensionBearingACH;

        public override void ResetEffects()
        {
            TensionBearingACH = false;
        }

        public override void PostUpdate()
        {
            if (!TensionBearingACH)
            {
                MaxSpinActive = false;
                MaxSpinTimer = 0;
                return;
            }

            // Timer is handled by the global yoyo projectile.
        }

        public bool MaxSpinActive;
        public int MaxSpinTimer;
    }
}