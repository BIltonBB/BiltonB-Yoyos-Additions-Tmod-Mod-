using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;

namespace BiltonsYoyoMod.Content.UI.YoyoNPC
{
	// Live "Current Mood" readout for the Mood tab: discount, nearby loved /
	// hated neighbours, current biome vs. tastes, and crowding.
	public static class YoyoNPCMood
	{
		public enum BiomeKind { Forest, Snow, Jungle, Ocean, Desert, Underground, Underworld, Dungeon }

		public class AffectionEntry
		{
			public int NpcType = -1; // -1 = biome entry.
			public BiomeKind Biome;
			public int Level; // 2 love, 1 like, -1 dislike, -2 hate.
			public string Name;
		}

		public class MoodInfo
		{
			public string Label = "Content";
			public Color Color = Color.LightBlue;
			public string Summary = "";
			public List<string> Factors = new();
			public int DiscountPct;
		}

		public static List<AffectionEntry> GetAffections(int npcType)
		{
			var list = new List<AffectionEntry>();
			void Npc(int type, int level, string name) => list.Add(new AffectionEntry { NpcType = type, Level = level, Name = name });
			void Biome(BiomeKind biome, int level, string name) => list.Add(new AffectionEntry { NpcType = -1, Biome = biome, Level = level, Name = name });

			if (npcType == ModContent.NPCType<YoyoMaster>()) {
				Biome(BiomeKind.Forest, 2, "Forest"); Npc(NPCID.DD2Bartender, 2, "Tavernkeep"); Npc(ModContent.NPCType<YoyoTinkerer>(), 2, "Yoyo Tinkerer");
				Biome(BiomeKind.Snow, 1, "Snow"); Biome(BiomeKind.Jungle, 1, "Jungle"); Npc(NPCID.GoblinTinkerer, 1, "Goblin Tinkerer"); Npc(NPCID.DyeTrader, 1, "Dye Trader");
				Biome(BiomeKind.Ocean, -1, "Ocean"); Npc(NPCID.Angler, -1, "Angler");
				Biome(BiomeKind.Underworld, -2, "Underworld"); Npc(NPCID.TaxCollector, -2, "Tax Collector"); Npc(NPCID.Stylist, -2, "Stylist"); Npc(NPCID.Clothier, -2, "Clothier");
			}
			else if (npcType == ModContent.NPCType<YoyoTinkerer>()) {
				Npc(NPCID.GoblinTinkerer, 2, "Goblin Tinkerer"); Npc(NPCID.Mechanic, 2, "Mechanic"); Biome(BiomeKind.Snow, 2, "Snow");
				Npc(NPCID.Stylist, 1, "Stylist");
				Biome(BiomeKind.Desert, -1, "Desert"); Npc(NPCID.ArmsDealer, -1, "Arms Dealer");
				Biome(BiomeKind.Underworld, -2, "Underworld"); Biome(BiomeKind.Jungle, -2, "Jungle"); Npc(NPCID.Painter, -2, "Painter");
			}
			else {
				Npc(NPCID.Wizard, 2, "Wizard"); Biome(BiomeKind.Forest, 2, "Forest");
				Biome(BiomeKind.Jungle, 1, "Jungle"); Npc(NPCID.WitchDoctor, 1, "Witch Doctor"); Npc(NPCID.Clothier, 1, "Clothier"); Biome(BiomeKind.Underground, 1, "Underground");
				Npc(NPCID.TaxCollector, -1, "Tax Collector"); Biome(BiomeKind.Snow, -1, "Snow"); Biome(BiomeKind.Underworld, -1, "Underworld");
				Npc(NPCID.Demolitionist, -2, "Demolitionist"); Biome(BiomeKind.Ocean, -2, "Ocean");
			}
			return list;
		}

		public static BiomeKind GetBiome(Vector2 worldPos)
		{
			// Evaluated at the viewer's position (the panel only opens next to the NPC).
			Player dummy = Main.LocalPlayer;
			bool snow = dummy.ZoneSnow, jungle = dummy.ZoneJungle, ocean = dummy.ZoneBeach;
			bool desert = dummy.ZoneDesert, dungeon = dummy.ZoneDungeon;
			bool underground = dummy.ZoneDirtLayerHeight || dummy.ZoneRockLayerHeight;
			bool underworld = dummy.ZoneUnderworldHeight;
			// Biome at the NPC's tile wins over the player's zone flags when they differ.
			// (Player stands next to the NPC, so their zones normally match.)
			if (dungeon) return BiomeKind.Dungeon;
			if (underworld) return BiomeKind.Underworld;
			if (ocean) return BiomeKind.Ocean;
			if (desert) return BiomeKind.Desert;
			if (jungle) return BiomeKind.Jungle;
			if (snow) return BiomeKind.Snow;
			if (underground) return BiomeKind.Underground;
			return BiomeKind.Forest;
		}

		public static string BiomeName(BiomeKind biome)
		{
			return biome switch {
				BiomeKind.Snow => "Snow",
				BiomeKind.Jungle => "Jungle",
				BiomeKind.Ocean => "Ocean",
				BiomeKind.Desert => "Desert",
				BiomeKind.Underground => "Underground",
				BiomeKind.Underworld => "Underworld",
				BiomeKind.Dungeon => "Dungeon",
				_ => "Forest",
			};
		}

		private static string LevelWord(int level)
		{
			return level >= 2 ? "Loves" : level == 1 ? "Likes" : level == -1 ? "Dislikes" : "Hates";
		}

		public static MoodInfo Compute(Player player, NPC npc)
		{
			var info = new MoodInfo();
			float discount = 1f - (float)player.currentShoppingSettings.PriceAdjustment;
			info.DiscountPct = discount > 0.005f ? (int)(discount * 100f) : 0;

			var affections = GetAffections(npc.type);
			BiomeKind here = GetBiome(npc.Center);

			// Nearby housed neighbours.
			var nearby = new HashSet<int>();
			int crowd = 0;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC other = Main.npc[i];
				if (!other.active || !other.townNPC || other.whoAmI == npc.whoAmI)
					continue;
				float dist = Vector2.Distance(npc.Center, other.Center);
				if (dist < 500f)
					nearby.Add(other.type);
				if (dist < 400f)
					crowd++;
			}

			int score = System.Math.Min(2, info.DiscountPct / 5);
			var hits = new List<(AffectionEntry entry, bool active)>();
			foreach (var entry in affections) {
				bool active = entry.NpcType >= 0 ? nearby.Contains(entry.NpcType) : entry.Biome == here;
				if (active)
					score += entry.Level;
				hits.Add((entry, active));
			}

			bool crowded = crowd > 3;
			if (crowded)
				score -= 1;

			if (score >= 4) { info.Label = "Ecstatic"; info.Color = Color.Gold; }
			else if (score >= 2) { info.Label = "Happy"; info.Color = Color.LightGreen; }
			else if (score >= 0) { info.Label = "Content"; info.Color = Color.LightBlue; }
			else if (score >= -2) { info.Label = "Grumpy"; info.Color = Color.Orange; }
			else { info.Label = "Furious"; info.Color = Color.Tomato; }

			foreach (var (entry, active) in hits) {
				if (!active)
					continue;
				string kind = entry.NpcType >= 0 ? "neighbour" : "biome";
				info.Factors.Add($"{LevelWord(entry.Level)} this {kind}: {entry.Name}");
			}
			if (crowded)
				info.Factors.Add($"Crowded: {crowd} neighbours within shouting distance");
			if (info.DiscountPct > 0)
				info.Factors.Add($"Mood saves you {info.DiscountPct}% in the shop");
			else
				info.Factors.Add("No mood discount right now");

			if (info.Factors.Count == 0)
				info.Summary = "Nothing nearby stirs any strong feelings.";
			else if (score >= 2)
				info.Summary = "Life is good here. Training, tinkering and trading all go smoothly.";
			else if (score >= 0)
				info.Summary = "Getting by. A loved neighbour or biome nearby would lift the mood.";
			else
				info.Summary = "Something nearby is ruining the vibe. Consider relocating neighbours.";

			info.Summary = $"Lives in: {BiomeName(here)}. " + info.Summary;
			return info;
		}

		// Visitors don't do happiness mechanics; they get a flavour readout instead.
		public static MoodInfo ComputeVisitor(Player player, NPC npc, string displayName)
		{
			var info = new MoodInfo { Label = "Enigmatic", Color = new Color(200, 170, 255) };
			double nightLeft = 0;
			if (!Main.dayTime)
				nightLeft = (32400.0 - Main.time) / 3600.0;
			info.Summary = nightLeft > 0
				? $"Only visiting until dawn ({nightLeft:0.0}h of night left). Mood never changes visitor prices."
				: "Should already be gone... enjoy the extra time.";
			info.Factors.Add("Visitors always charge full price");
			info.Factors.Add("Leaves at dawn, like all sensible night folk");
			info.DiscountPct = 0;
			return info;
		}
	}
}
