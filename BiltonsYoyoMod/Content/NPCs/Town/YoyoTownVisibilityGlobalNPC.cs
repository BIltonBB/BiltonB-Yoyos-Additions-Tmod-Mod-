using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.NPCs.Town
{
	// Town folks must never blank out: clamps runaway alpha (except real
	// shimmer fade) and pins the frame inside the 25-frame sheets, whatever
	// animation state the AI requests.
	public class YoyoTownVisibilityGlobalNPC : GlobalNPC
	{
		public override void PostAI(NPC npc)
		{
			if (npc.ModNPC == null || npc.ModNPC.Mod != ModContent.GetInstance<BiltonsYoyoMod>())
				return;
			string ns = npc.ModNPC.GetType().Namespace ?? string.Empty;
			if (!ns.Contains("Town"))
				return;
			if (!npc.shimmerWet && npc.alpha > NPCID.None)
				npc.alpha = NPCID.None;
			int frames = npc.type >= NPCID.None && npc.type < Main.npcFrameCount.Length ? Main.npcFrameCount[npc.type] : 0;
			if (frames > 0 && npc.frame.Height > 0) {
				int maxY = (frames - 1) * npc.frame.Height;
				if (npc.frame.Y > maxY)
					npc.frame.Y = maxY;
				if (npc.frame.Y < 0)
					npc.frame.Y = 0;
			}
		}
	}
}
