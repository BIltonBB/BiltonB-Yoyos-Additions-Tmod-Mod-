using System.Collections.Generic;

namespace BiltonsYoyoMod.Content.Customization
{
	// Mastery rewards combat decisions; True Mastery fuses advanced skills.
	// Mastery (0-9) and True Mastery (100-107) share one slot.
	public class YoyoMasteryDef
	{
		public int Id;
		public string Name;
		public string Description;
		public bool TrueMastery;
		public string Tier => TrueMastery ? "True Mastery" : "Mastery";
	}

	public static class YoyoMasteryDefs
	{
		public const int TrueOffset = 100;

		public static readonly List<YoyoMasteryDef> All = new()
		{
			new YoyoMasteryDef { Id = 0, Name = "Encore", Description = "Keep hitting without long pauses. Each chained hit climbs +8% damage." },
			new YoyoMasteryDef { Id = 1, Name = "Last Word", Description = "Foes below ~1/3 HP get marked. Re-strike one soon for a finisher (bosses resist)." },
			new YoyoMasteryDef { Id = 2, Name = "Chain Reaction", Description = "Hits tag nearby foes. Hitting a tagged one spreads bonus hits through crowds." },
			new YoyoMasteryDef { Id = 3, Name = "Untouched", Description = "Take no damage for 10+ seconds to charge +25%. One hit wipes it." },
			new YoyoMasteryDef { Id = 4, Name = "Lockdown", Description = "Stay on one target to deepen bonus damage. Swapping spends it as a parting burst." },
			new YoyoMasteryDef { Id = 5, Name = "Misdirect", Description = "Land hits while bouncing around, then strike the marked foe for +50%." },
			new YoyoMasteryDef { Id = 6, Name = "Reversal", Description = "Let enemy shots fly past you to open a counter window. Hit back for +70%." },
			new YoyoMasteryDef { Id = 7, Name = "Momentum Waltz", Description = "Mix bounces, orbits, returns and swaps to build Flow damage. One trick never peaks." },
			new YoyoMasteryDef { Id = 8, Name = "Farpoint", Description = "Hits at nearly max range mark the target. Your next hit on it lands +50%." },
			new YoyoMasteryDef { Id = 9, Name = "Counterpattern", Description = "Do something different (new target or angle) for +20%." },
			new YoyoMasteryDef { Id = 100, TrueMastery = true, Name = "Circuit Breaker", Description = "Bank 4 different combat events, then discharge bonus damage along your route." },
			new YoyoMasteryDef { Id = 101, TrueMastery = true, Name = "Second Hand", Description = "Alternate slower and quicker hit rhythms to spawn an afterimage echo." },
			new YoyoMasteryDef { Id = 102, TrueMastery = true, Name = "Ghostline", Description = "Fly a lot to record your path, then a phantom replays part of it." },
			new YoyoMasteryDef { Id = 103, TrueMastery = true, Name = "Cathedral of Rebounds", Description = "Land 4 staged rebounds to toll a heavy bonus hit." },
			new YoyoMasteryDef { Id = 104, TrueMastery = true, Name = "Confluence", Description = "Build hits, rebounds and fresh angles separately, then unite all three at once." },
			new YoyoMasteryDef { Id = 105, TrueMastery = true, Name = "Angle Sovereign", Description = "Hit foes from 5 unique angles to seal a convergence strike." },
			new YoyoMasteryDef { Id = 106, TrueMastery = true, Name = "Resonant Execution", Description = "Hits build a damage pool that boosts you. Stalling too long drains it." },
			new YoyoMasteryDef { Id = 107, TrueMastery = true, Name = "Unbroken Continuum", Description = "Strike 5 different enemies to link them, then trigger the final strike." },
		};

		public static YoyoMasteryDef ById(int id)
		{
			foreach (var d in All) {
				if (d.Id == id)
					return d;
			}
			return null;
		}

		public static bool IsTrueId(int id) => id >= TrueOffset;
	}
}
