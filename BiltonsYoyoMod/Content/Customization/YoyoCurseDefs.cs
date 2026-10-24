using System.Collections.Generic;
using Terraria;

namespace BiltonsYoyoMod.Content.Customization
{
	// Curses from the Ancient Yoyo-ist (post-Plantera). One per yoyo. High is riskier than Low.
	// Penalties are temporary and understandable: never destroy items or brick yoyos.
	public class YoyoCurseDef
	{
		public int Id;
		public string Name;
		public string Description;
		public bool High;
		public string Tier => High ? "High Curse" : "Low Curse";
	}

	public static class YoyoCurseDefs
	{
		public const int HighOffset = 100;

		public static readonly List<YoyoCurseDef> All = new()
		{
			new YoyoCurseDef { Id = 0, Name = "Recoil Thread", Description = "Same-line hits build recoil (weaker at 3+). Vary trajectory to vent it." },
			new YoyoCurseDef { Id = 1, Name = "Brittle Momentum", Description = "+15% momentum until one sharp turn shatters it to -35% while rebuilding." },
			new YoyoCurseDef { Id = 2, Name = "Orbit Tax", Description = "Flying drains a reserve; empty kicks the yoyo home briefly. Redirect to refund." },
			new YoyoCurseDef { Id = 3, Name = "Borrowed Seconds", Description = "First hit +35%, then -25% hits for a few seconds while repaying." },
			new YoyoCurseDef { Id = 4, Name = "Magnetic Obsession", Description = "Yoyo bends toward nearby foes. Aim off to compensate, or hook corners." },
			new YoyoCurseDef { Id = 5, Name = "Echo Chamber", Description = "Replays a slice of your last path as a weak echo. Helpful or wild." },
			new YoyoCurseDef { Id = 6, Name = "Weight of Doubt", Description = "Swings alternate: heavy (+25%, sluggish) and light (-15%, nimble)." },
			new YoyoCurseDef { Id = 7, Name = "Fractured Attention", Description = "Same-angle spam fades to -30%. Rotate angles; constant jitter stalls you." },
			new YoyoCurseDef { Id = 8, Name = "Unbalanced Pendulum", Description = "Alternates favoring outward vs return. Pushing the wrong phase: -25%." },
			new YoyoCurseDef { Id = 9, Name = "Empty Orbit", Description = "Range and speed sag the longer you fly. A sharp rebound resets the drift." },
			new YoyoCurseDef { Id = 100, High = true, Name = "The Rebound Debt", Description = "Good rebounds bank huge bonus. A lazy one burns it into brief -15% recovery." },
			new YoyoCurseDef { Id = 101, High = true, Name = "Reality Shear", Description = "Periodically phases (passes blocks, drifts). Weird routes open; aim blurs." },
			new YoyoCurseDef { Id = 102, High = true, Name = "Momentum Bankruptcy", Description = "Speed banks a huge reserve. Overfill without a discharge hit: control craters." },
			new YoyoCurseDef { Id = 103, High = true, Name = "Critical Overpressure", Description = "Pushes past limits for +50% hits. Linger and it slams a hard slowdown." },
			new YoyoCurseDef { Id = 104, High = true, Name = "The Devouring Gyre", Description = "Locks hit foes in a tightening +35% orbit. Losing the mark punishes hard." },
			new YoyoCurseDef { Id = 105, High = true, Name = "Dissonant Crown", Description = "Resonance builds fast to +50%. A mistimed gap collapses it." },
			new YoyoCurseDef { Id = 106, High = true, Name = "Pendulum of Souls", Description = "Outward hits +30%, returns -30%. Time strikes with the swing." },
			new YoyoCurseDef { Id = 107, High = true, Name = "Chaotic Inheritance", Description = "Every rebound skews the next segment further. Ride it until reset." },
			new YoyoCurseDef { Id = 108, High = true, Name = "Hollow Throne", Description = "Strong bonus while varying angles. Spamming one angle strips it." },
			new YoyoCurseDef { Id = 109, High = true, Name = "The Last Contract", Description = "Bank damage into one 2.2x strike, then brief frailty. Missing wastes it." },
		};

		public static YoyoCurseDef ById(int id)
		{
			foreach (var d in All) {
				if (d.Id == id)
					return d;
			}
			return null;
		}

		public static bool IsHighId(int id) => id >= HighOffset;

		// Essence spend -> (curse chance, high share of successes). Configurable and shown verbatim in UI.
		// 1 / 3 / 5 / 10 essence. High curses always rarer than Low in absolute terms.
		public static void OddsForEssence(int essence, out float curseChance, out float highShare)
		{
			switch (essence) {
				case 1: curseChance = 0.40f; highShare = 0.10f; break;
				case 3: curseChance = 0.65f; highShare = 0.15f; break;
				case 5: curseChance = 0.85f; highShare = 0.20f; break;
				default: curseChance = 1.00f; highShare = 0.30f; break; // 10+
			}
		}

		public static int NormalizedEssence(int essence)
		{
			if (essence <= 1) return 1;
			if (essence <= 3) return 3;
			if (essence <= 5) return 5;
			return 10;
		}

		// Single source of truth for rolls. Displayed odds come from OddsForEssence above.
		public static bool RollCurse(int essenceSpent, out int curseId)
		{
			curseId = -1;
			int norm = NormalizedEssence(essenceSpent);
			OddsForEssence(norm, out float curseChance, out float highShare);
			if (Main.rand.NextFloat() > curseChance)
				return false;
			bool high = Main.rand.NextFloat() < highShare;
			var pool = new List<YoyoCurseDef>();
			foreach (var d in All) {
				if (d.High == high)
					pool.Add(d);
			}
			if (pool.Count == 0)
				return false;
			curseId = pool[Main.rand.Next(pool.Count)].Id;
			return true;
		}
	}
}
