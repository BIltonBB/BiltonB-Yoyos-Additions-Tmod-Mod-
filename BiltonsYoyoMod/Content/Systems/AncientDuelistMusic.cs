using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Systems
{
	public class AncientDuelistMusic : ModSceneEffect
	{
		private static int musicSlot = -1;
		private static bool resolved;

		// Uses Content/Music/ADP0 when that file exists, falling back to YGP1
		// so a missing track can never silence (or crash) the fight.
		public override int Music {
			get {
				if (!resolved) {
					resolved = true;
					try {
						musicSlot = MusicLoader.GetMusicSlot(Mod, "Content/Music/ADP0");
					}
					catch {
						musicSlot = MusicLoader.GetMusicSlot(Mod, "Content/Music/YGP1");
					}
				}
				return musicSlot;
			}
		}

		public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;

		public override bool IsSceneEffectActive(Player player)
		{
			int type = ModContent.NPCType<NPCs.SubBosses.AncientDuelist>();
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc.active && npc.type == type)
					return true;
			}
			return false;
		}
	}
}
