using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Common
{
    // Safe guard-clause bridge registration during load phase. Prints a chat
    // notice if Luminance is unavailable (fallback mode), so the user knows.
    public class LuminanceBridgeSystem : ModSystem
    {
        private bool announced;
        public override void PostSetupContent()
        {
            // Force init so Available reflects the real state.
            bool avail = LuminanceBridge.Available;
            if (!announced)
            {
                announced = true;
                if (Main.netMode != Terraria.ID.NetmodeID.Server)
                {
                    if (avail)
                        Main.NewText("Luminance bridge: ACTIVE (weak ref) - shake/easing via Luminance.", 120, 220, 120);
                    else
                        Main.NewText("Luminance bridge: fallback mode (Luminance not installed).", 220, 180, 120);
                }
            }
        }
    }
}
