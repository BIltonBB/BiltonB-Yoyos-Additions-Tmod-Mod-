using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.NPCs
{
    public class RevampStringGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public int StringHitCooldown;

        public override void PostAI(NPC npc)
        {
            if (StringHitCooldown > 0)
                StringHitCooldown--;
        }
    }
}
