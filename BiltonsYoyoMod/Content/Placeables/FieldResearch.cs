using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Field research (#9): the game itself files reports — kills, bosses,
	// biome surveys, fresh ores, new finds, yoyo test flights and catches
	// all trickle small point gains. Amounts stay low on purpose.
	//
	// Routing: every report goes to the NEAREST live engine that owns the
	// matching kit node (doubled with Seasoned Researcher). Silent when
	// no lab qualifies. Server-side events (kills, mining) apply directly
	// like the mod's other kill rewards; owner-side events (pickups,
	// throws, surveys, catches) apply optimistically and packet the
	// server, converging through WorldData.
	public static class FieldResearch
	{
		public const int EnemyPts = 1;
		public const int BossPts = 25;
		public const int BiomePts = 10;
		public const int OrePts = 3;
		public const int ItemPts = 2;
		public const int YoyoPts = 5;
		public const int FishPts = 3;

		public static Player NearestPlayer(Vector2 pos)
		{
			Player best = null;
			float bestD2 = float.MaxValue;
			foreach (Player p in Main.player) {
				if (p == null || !p.active || p.dead)
					continue;
				float d2 = Vector2.DistanceSquared(pos, p.Center);
				if (d2 < bestD2) {
					bestD2 = d2;
					best = p;
				}
			}
			return best;
		}

		public static bool Award(Player player, string nodeId, int amount)
		{
			if (player == null || !player.active || string.IsNullOrEmpty(nodeId) || amount <= 0)
				return false;
			Point pc = player.Center.ToTileCoordinates();
			Point best = default;
			ResearchData bestData = null;
			float bestD2 = float.MaxValue;
			bool found = false;
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				if (!ResearchTech.TechOn(kv.Value, nodeId))
					continue;
				float dx = kv.Key.X - pc.X;
				float dy = kv.Key.Y - pc.Y;
				float d2 = dx * dx + dy * dy;
				if (!found || d2 < bestD2) {
					best = kv.Key;
					bestData = kv.Value;
					bestD2 = d2;
					found = true;
				}
			}
			if (!found)
				return false;
			if (ResearchTech.TechOn(bestData, ResearchTech.FieldMaster))
				amount *= 2;
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				ResearchPoints.Add(bestData, amount);
				PlaceableNet.SendResearchField(best, amount);
			}
			else {
				ResearchPoints.Add(bestData, amount);
			}
			return true;
		}

		// One-line summary of unlocked field sources for the Research tab.
		public static string ActiveSources(ResearchData d)
		{
			if (d == null)
				return "";
			var parts = new List<string>();
			if (ResearchTech.TechOn(d, ResearchTech.FieldCombat))
				parts.Add("hunts");
			if (ResearchTech.TechOn(d, ResearchTech.FieldSurvey))
				parts.Add("surveys+ores");
			if (ResearchTech.TechOn(d, ResearchTech.FieldCatalogue))
				parts.Add("finds");
			if (ResearchTech.TechOn(d, ResearchTech.FieldPilot))
				parts.Add("test flights");
			if (ResearchTech.TechOn(d, ResearchTech.FieldAngler))
				parts.Add("catches");
			if (parts.Count == 0)
				return "";
			string s = string.Join(", ", parts);
			if (ResearchTech.TechOn(d, ResearchTech.FieldMaster))
				s += " (x2)";
			return s;
		}
	}

	// Per-player first-time tracking (session-only): biomes, item types,
	// yoyo types, ore types. Each client tracks its own player.
	public class FieldResearchPlayer : ModPlayer
	{
		public HashSet<string> Biomes = new();
		public HashSet<int> Items = new();
		public HashSet<int> Yoyos = new();
		public HashSet<int> Ores = new();

		private int scanTimer;

		public override void PostUpdate()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;
			if (--scanTimer > 0)
				return;
			scanTimer = 600;
			ScanBiomes();
		}

		private void ScanBiomes()
		{
			(string key, bool here)[] zones = {
				("Desert", Player.ZoneDesert),
				("Snow", Player.ZoneSnow),
				("Jungle", Player.ZoneJungle),
				("Corruption", Player.ZoneCorrupt),
				("Crimson", Player.ZoneCrimson),
				("Hallow", Player.ZoneHallow),
				("Dungeon", Player.ZoneDungeon),
				("Underworld", Player.ZoneUnderworldHeight),
				("Caverns", Player.ZoneRockLayerHeight),
				("Sky", Player.ZoneSkyHeight),
				("Beach", Player.ZoneBeach),
				("Rain", Player.ZoneRain),
				("Sandstorm", Player.ZoneSandstorm),
			};
			foreach (var z in zones) {
				if (z.here && Biomes.Add(z.key))
					FieldResearch.Award(Player, ResearchTech.FieldSurvey, FieldResearch.BiomePts);
			}
		}
	}

	public class FieldResearchNPC : GlobalNPC
	{
		public override void OnKill(NPC npc)
		{
			// Server/single-side rewards (mod pattern: no client double-pay).
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			if (npc == null || npc.friendly || npc.lifeMax <= 5)
				return;
			if (npc.type >= NPCID.None && npc.type < NPCID.Count && NPCID.Sets.CountsAsCritter[npc.type])
				return;
			Player player = FieldResearch.NearestPlayer(npc.Center);
			if (player == null)
				return;
			if (npc.boss)
				FieldResearch.Award(player, ResearchTech.FieldCombat, FieldResearch.BossPts);
			else
				FieldResearch.Award(player, ResearchTech.FieldCombat, FieldResearch.EnemyPts);
		}
	}

	public class FieldResearchTiles : GlobalTile
	{
		public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
		{
			// Server/single-side rewards (mining is validated server-side).
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			if (fail || effectOnly)
				return;
			// Spelunker-highlighted tiles: ores, gems and other precious
			// finds (covers modded ores too).
			if (type < 0 || type >= TileID.Count || !Main.tileSpelunker[type])
				return;
			Player player = FieldResearch.NearestPlayer(new Vector2(i * 16f + 8f, j * 16f + 8f));
			if (player == null)
				return;
			var fp = player.GetModPlayer<FieldResearchPlayer>();
			if (fp.Ores.Add(type))
				FieldResearch.Award(player, ResearchTech.FieldSurvey, FieldResearch.OrePts);
		}
	}

	public class FieldResearchItems : GlobalItem
	{
		public override bool OnPickup(Item item, Player player)
		{
			if (item != null && !item.IsAir && player != null && player.active && player.whoAmI == Main.myPlayer) {
				// Coins are pocket change, not discoveries.
				if (item.type < ItemID.CopperCoin || item.type > ItemID.PlatinumCoin) {
					var fp = player.GetModPlayer<FieldResearchPlayer>();
					if (fp.Items.Add(item.type))
						FieldResearch.Award(player, ResearchTech.FieldCatalogue, FieldResearch.ItemPts);
				}
			}
			return base.OnPickup(item, player);
		}

		// CaughtFishStack fires once per fished catch, local client only,
		// so Main.LocalPlayer is the catcher. (There is no OnCatchFish
		// hook in tModLoader; this is the documented catch path.)
		public override void CaughtFishStack(int type, ref int stack)
		{
			Player player = Main.LocalPlayer;
			if (player != null && player.active)
				FieldResearch.Award(player, ResearchTech.FieldAngler, FieldResearch.FishPts);
		}
	}

	public class FieldResearchProjectiles : GlobalProjectile
	{
		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (projectile == null || projectile.aiStyle != ProjAIStyleID.Yoyo)
				return;
			// Owner side only: net-spawned copies must not re-award.
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers || projectile.owner != Main.myPlayer)
				return;
			Player player = Main.player[projectile.owner];
			if (player == null || !player.active)
				return;
			var fp = player.GetModPlayer<FieldResearchPlayer>();
			if (fp.Yoyos.Add(projectile.type))
				FieldResearch.Award(player, ResearchTech.FieldPilot, FieldResearch.YoyoPts);
		}
	}
}
