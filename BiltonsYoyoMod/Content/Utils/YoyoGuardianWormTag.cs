using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Utils
{
    // Marks worm segments spawned by the Guardian so the 2x-yoyo-damage rule can find them cheaply.
    public class YoyoGuardianWormTag : GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public bool isGuardianWorm;
    }
}
