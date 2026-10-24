using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace BiltonsYoyoMod.Content.NPCs.Town
{
	// The 20 spells cast by the Ancient Yo-yoist. Spells stack with normal
	// reforges and Tinkerer modifications.
	public class YoyoSpellDef
	{
		public int Id;
		public string Name;
		public string Description;
		public bool Hardmode;
		public bool Combined;
		// Vanilla buff(s) applied to enemies on hit. Empty = handled specially.
		public int[] Debuffs = new int[0];
	}

	public static class YoyoSpellDefs
	{
		public static readonly List<YoyoSpellDef> All = new()
		{
			new YoyoSpellDef { Id = 0, Name = "Poisoned", Description = "Inflicts Poisoned.", Debuffs = new[] { BuffID.Poisoned } },
			new YoyoSpellDef { Id = 1, Name = "On Fire!", Description = "Sets enemies On Fire!", Debuffs = new[] { BuffID.OnFire } },
			new YoyoSpellDef { Id = 2, Name = "Confused", Description = "Inflicts Confused.", Debuffs = new[] { BuffID.Confused } },
			new YoyoSpellDef { Id = 3, Name = "Frostburn", Description = "Inflicts Frostburn.", Debuffs = new[] { BuffID.Frostburn } },
			new YoyoSpellDef { Id = 4, Name = "Bleeding", Description = "Inflicts Bleeding.", Debuffs = new[] { BuffID.Bleeding } },
			new YoyoSpellDef { Id = 5, Name = "Slow", Description = "Inflicts Slow.", Debuffs = new[] { BuffID.Slow } },
			new YoyoSpellDef { Id = 6, Name = "Electrified", Description = "Inflicts Electrified.", Debuffs = new[] { BuffID.Electrified } },
			new YoyoSpellDef { Id = 7, Name = "Midas", Description = "Inflicts Midas (enemies drop more money).", Debuffs = new[] { BuffID.Midas } },
			new YoyoSpellDef { Id = 8, Name = "Shadowflame", Description = "Inflicts Shadowflame.", Debuffs = new[] { BuffID.ShadowFlame } },
			new YoyoSpellDef { Id = 9, Name = "Cursed Flames", Description = "Inflicts Cursed Inferno.", Debuffs = new[] { BuffID.CursedInferno } },
			new YoyoSpellDef { Id = 10, Name = "Ichor", Description = "Inflicts Ichor (lowers enemy defense).", Hardmode = true, Debuffs = new[] { BuffID.Ichor } },
			new YoyoSpellDef { Id = 11, Name = "Cursed Inferno", Description = "Inflicts a stronger Cursed Inferno.", Hardmode = true, Debuffs = new[] { BuffID.CursedInferno } },
			new YoyoSpellDef { Id = 12, Name = "Acid Venom", Description = "Inflicts Acid Venom.", Hardmode = true, Debuffs = new[] { BuffID.Venom } },
			new YoyoSpellDef { Id = 13, Name = "Alchemical", Description = "Combined spell: Poisoned, Acid Venom and Bleeding.", Hardmode = true, Combined = true, Debuffs = new[] { BuffID.Poisoned, BuffID.Venom, BuffID.Bleeding } },
			new YoyoSpellDef { Id = 14, Name = "Kinetic", Description = "Combined spell: Confused, Electrified, Midas and Slow.", Hardmode = true, Combined = true, Debuffs = new[] { BuffID.Confused, BuffID.Electrified, BuffID.Midas, BuffID.Slow } },
			new YoyoSpellDef { Id = 15, Name = "Elemental", Description = "Combined spell: On Fire!, Cursed Inferno, Shadowflame, Frostburn and Hellfire.", Hardmode = true, Combined = true, Debuffs = new[] { BuffID.OnFire, BuffID.CursedInferno, BuffID.ShadowFlame, BuffID.Frostburn, BuffID.OnFire3 } },
			new YoyoSpellDef { Id = 16, Name = "Hellfire", Description = "Inflicts Hellfire (On Fire! + extra burn).", Hardmode = true, Debuffs = new[] { BuffID.OnFire3 } },
			new YoyoSpellDef { Id = 17, Name = "Penetration", Description = "Combined spell: Betsy's Curse, Oiled and Ichor.", Hardmode = true, Combined = true, Debuffs = new[] { BuffID.BetsysCurse, BuffID.Oiled, BuffID.Ichor } },
			new YoyoSpellDef { Id = 18, Name = "Betsy's Curse", Description = "Inflicts Betsy's Curse (lowers enemy defense heavily).", Hardmode = true, Debuffs = new[] { BuffID.BetsysCurse } },
			new YoyoSpellDef { Id = 19, Name = "Daybroken", Description = "Extremely rare. Inflicts Daybroken, the fury of the sun.", Hardmode = true, Debuffs = new[] { BuffID.Daybreak } },
			new YoyoSpellDef { Id = 20, Name = "Stunned", Description = "Combined spell: Confused and Slow.", Combined = true, Debuffs = new[] { BuffID.Confused, BuffID.Slow } },
			new YoyoSpellDef { Id = 21, Name = "Plagued", Description = "Combined spell: Acid Venom and Cursed Inferno.", Hardmode = true, Combined = true, Debuffs = new[] { BuffID.Venom, BuffID.CursedInferno } },
			new YoyoSpellDef { Id = 22, Name = "Solar Flare", Description = "Combined spell: Hellfire and Ichor.", Hardmode = true, Combined = true, Debuffs = new[] { BuffID.OnFire3, BuffID.Ichor } },
			new YoyoSpellDef { Id = 23, Name = "Withered", Description = "Combined spell: Bleeding and Poisoned.", Combined = true, Debuffs = new[] { BuffID.Bleeding, BuffID.Poisoned } },
			new YoyoSpellDef { Id = 24, Name = "Stormfire", Description = "Combined spell: Electrified and Cursed Inferno.", Hardmode = true, Combined = true, Debuffs = new[] { BuffID.Electrified, BuffID.CursedInferno } },
		};

		public static YoyoSpellDef ById(int id)
		{
			foreach (var def in All) {
				if (def.Id == id)
					return def;
			}
			return null;
		}

		// Casting tier prices (copper): Basic, Improved, Advanced, Greater, Master.
		public static readonly int[] TierPrices = { 10000, 50000, 150000, 500000, 2000000 };

		public static readonly string[] TierNames = { "Basic", "Improved", "Advanced", "Greater", "Master" };

		// Higher tiers shift weight toward rare/combined spells. Never guaranteed.
		public static int RollSpell(bool hardmode, int tier)
		{
			var pool = new List<(int id, float weight)>();
			foreach (var def in All) {
				if (def.Hardmode && !hardmode)
					continue;
				float weight = 100f;
				if (def.Combined)
					weight = 8f + tier * 6f; // 8..32
				else if (def.Hardmode)
					weight = 40f + tier * 10f; // 40..80
				else
					weight = System.Math.Max(12f, 110f - tier * 18f); // 110..38
				if (def.Id == 19)
					weight = 0.0001f * (1 + tier); // ~1 in 1,000,000, slightly better at high tiers.
				pool.Add((def.Id, weight));
			}
			float total = 0f;
			foreach (var entry in pool)
				total += entry.weight;
			float roll = (float)Main.rand.NextDouble() * total;
			foreach (var entry in pool) {
				roll -= entry.weight;
				if (roll <= 0f)
					return entry.id;
			}
			return 0;
		}
	}
}
