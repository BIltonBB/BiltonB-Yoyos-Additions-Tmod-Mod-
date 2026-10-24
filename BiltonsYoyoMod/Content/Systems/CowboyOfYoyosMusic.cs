using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Systems
{
    public class CowboyOfYoyosMusic : ModSceneEffect
    {
        public override int Music =>
            MusicLoader.GetMusicSlot(
                Mod,
                "Content/Music/TCOYP0"
            );

        public override SceneEffectPriority Priority =>
            SceneEffectPriority.BossLow;

        public override bool IsSceneEffectActive(Player player)
        {
            int cowboy1 =
                ModContent.NPCType<
                    NPCs.SubBosses.CowboyOfYoyos1
                >();

            int cowboy2 =
                ModContent.NPCType<
                    NPCs.SubBosses.CowboyOfYoyos2
                >();

            int cowboy3 =
                ModContent.NPCType<
                    NPCs.SubBosses.CowboyOfYoyos3
                >();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active)
                    continue;

                if (npc.type == cowboy1 ||
                    npc.type == cowboy2 ||
                    npc.type == cowboy3)
                {
                    return true;
                }
            }

            return false;
        }
    }
}