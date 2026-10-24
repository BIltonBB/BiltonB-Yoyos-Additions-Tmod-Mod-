using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.SubBosses;

namespace BiltonsYoyoMod.Content.Systems
{
    // Fix #6: replaces the invalid `Music = ...` / `SceneEffectPriority = ...` assignments that
    // used to live directly inside GiantBearing.SetDefaults() — ModNPC doesn't expose those as
    // settable fields, so that never actually worked. This mirrors the pattern
    // YoyoGuardianMusic.cs already uses for the other boss.
    public class GiantBearingMusic : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Content/Music/GB1");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override bool IsSceneEffectActive(Player player)
        {
            int type = ModContent.NPCType<GiantBearing>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == type && Vector2.Distance(player.Center, npc.Center) < 4000f)
                    return true;
            }

            return false;
        }
    }
}