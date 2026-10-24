using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
    public class ResponsePadsPlayer : ModPlayer
    {
        public bool ResponsePadsAB;
        public bool ResponsePadsAF;
        public bool ResponsePadsAS;
        public bool ResponsePadsAR;
        public bool ResponsePadsACB;

        public override void ResetEffects()
        {
            ResponsePadsAB = false;
            ResponsePadsAF = false;
            ResponsePadsAS = false;
            ResponsePadsAR = false;
            ResponsePadsACB = false;
        }
    }
}