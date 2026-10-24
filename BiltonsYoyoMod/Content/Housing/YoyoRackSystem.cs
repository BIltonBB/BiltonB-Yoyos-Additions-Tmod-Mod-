using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Housing
{
	// Yoyo Rack housing buff: a rack buffs ONLY the Yoyo NPC whose assigned
	// housing contains it. Proximity to another NPC's rack never counts,
	// even for Yoyo NPCs. Visitors without houses and pets never qualify.
	// Bonus (per housed NPC, non-stacking): +25% damage, +10 defense,
	// +20% max life while a rack sits within 15 tiles of its home tile.
	public class YoyoRackSystem : ModSystem
	{
		public const float HomeRangePx = 15f * 16f;

		// Single source of truth for the buff line shown in messages.
		public const string BuffLine = "+25% damage, +10 defense, +20% max life";

		public static bool IsYoyoNPC(NPC npc)
		{
			if (!npc.active || npc.ModNPC == null || npc.boss)
				return false;
			if (npc.ModNPC.Mod != ModContent.GetInstance<BiltonsYoyoMod>())
				return false;
			// Housed town yoyo NPCs only (Master, Tinkerer, Ancient Yoyoist).
			return npc.townNPC && npc.friendly;
		}

		public static bool HomeHasRack(NPC npc)
		{
			if (npc.homeTileX <= 0 || npc.homeTileY <= 0)
				return false;
			Vector2 home = new(npc.homeTileX * 16f, npc.homeTileY * 16f);
			int rackType = ModContent.TileType<Tiles.Housing.YoyoRackTile>();
			int r = 16;
			int x0 = System.Math.Max(2, npc.homeTileX - r);
			int x1 = System.Math.Min(Main.maxTilesX - 2, npc.homeTileX + r);
			int y0 = System.Math.Max(2, npc.homeTileY - r);
			int y1 = System.Math.Min(Main.maxTilesY - 2, npc.homeTileY + r);
			for (int x = x0; x <= x1; x++) {
				for (int y = y0; y <= y1; y++) {
					Tile t = Framing.GetTileSafely(x, y);
					if (!t.HasTile || t.TileType != rackType)
						continue;
					if (t.TileFrameX != 0 || t.TileFrameY != 0)
						continue; // origin check below handles multitile
					Vector2 rack = new(x * 16f, y * 16f);
					if (Vector2.DistanceSquared(home, rack) <= HomeRangePx * HomeRangePx)
						return true;
				}
			}
			return false;
		}

		public override void PostUpdateEverything()
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || !IsYoyoNPC(npc))
					continue;
				var g = npc.GetGlobalNPC<YoyoRackGlobalNPC>();
				if (HomeHasRack(npc))
					g.ApplyRack(npc);
				else
					g.RemoveRack(npc);
			}
		}

		// Reverse lookup for messages: which housed Yoyo NPCs does the
		// rack at this top-left tile serve? Cheap (NPC loop + distance
		// only, no tile scan) so hover text can call it every frame.
		public static System.Collections.Generic.List<string> ServedNPCNames(Point rackTopLeft)
		{
			var names = new System.Collections.Generic.List<string>();
			Vector2 rack = new(rackTopLeft.X * 16f, rackTopLeft.Y * 16f);
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || !IsYoyoNPC(npc))
					continue;
				if (npc.homeTileX <= 0 || npc.homeTileY <= 0)
					continue;
				Vector2 home = new(npc.homeTileX * 16f, npc.homeTileY * 16f);
				if (Vector2.DistanceSquared(home, rack) <= HomeRangePx * HomeRangePx)
					names.Add(npc.FullName);
			}
			return names;
		}
	}

	public class YoyoRackGlobalNPC : GlobalNPC
	{
		public override bool InstancePerEntity => true;

		private bool racked;
		private int baseDamage;
		private int baseDefense;
		private int baseLifeMax;
		private bool hasBase;

		public void ApplyRack(NPC npc)
		{
			if (!hasBase) {
				baseDamage = npc.damage;
				baseDefense = npc.defense;
				baseLifeMax = npc.lifeMax;
				hasBase = true;
			}
			if (racked)
				return;
			racked = true;
			npc.damage = System.Math.Max(1, (int)(baseDamage * 1.25f));
			npc.defense = baseDefense + 10;
			int newMax = System.Math.Max(baseLifeMax + 1, (int)(baseLifeMax * 1.2f));
			int delta = newMax - npc.lifeMax;
			npc.lifeMax = newMax;
			npc.life = System.Math.Clamp(npc.life + System.Math.Max(0, delta), 1, npc.lifeMax);
		}

		public void RemoveRack(NPC npc)
		{
			if (!racked || !hasBase)
				return;
			racked = false;
			npc.damage = baseDamage;
			npc.defense = baseDefense;
			npc.life = System.Math.Clamp(npc.life, 1, baseLifeMax);
			npc.lifeMax = baseLifeMax;
		}

		public override void OnKill(NPC npc)
		{
			racked = false;
			hasBase = false;
		}
	}
}
