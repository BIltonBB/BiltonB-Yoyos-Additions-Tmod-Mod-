using System.Collections.Generic;

namespace BiltonsYoyoMod.Content.Customization
{
	// Tuning changes physical behavior: rebounds, movement, orbit, return paths, stored momentum.
	// Normal Tuning (0-9) and Extended Tuning (100-107) share one slot.
	public class YoyoTuningDef
	{
		public int Id;
		public string Name;
		public string Description;
		public bool Extended;
		public string Tier => Extended ? "Extended Tuning" : "Tuning";
	}

	public static class YoyoTuningDefs
	{
		public const int ExtendedOffset = 100;

		public static readonly List<YoyoTuningDef> All = new()
		{
			new YoyoTuningDef { Id = 0, Name = "Snapback", Description = "Bounce or snap-turn to bank tension. Your next hit spends it for bonus damage." },
			new YoyoTuningDef { Id = 1, Name = "Gyre", Description = "Hitting an enemy starts a brief orbit around it. Walk in or out to tighten or widen the ring." },
			new YoyoTuningDef { Id = 2, Name = "Deadweight", Description = "Let the yoyo hover slow to anchor it: breaks weak shots, then release for an outward burst." },
			new YoyoTuningDef { Id = 3, Name = "Tension Break", Description = "Throw far, drag back, fling out: the release flies ~25% faster. Hoarded tension decays." },
			new YoyoTuningDef { Id = 4, Name = "Forkline", Description = "A hard rebound splits a short-lived ghost copy along a new line. It can't split again." },
			new YoyoTuningDef { Id = 5, Name = "Return Vector", Description = "Steer the return trip through an already-hit enemy to smack it again on the way home." },
			new YoyoTuningDef { Id = 6, Name = "Springwake", Description = "Every bounce flips the line: a fast dart, then a long glide. Keep bouncing to alternate." },
			new YoyoTuningDef { Id = 7, Name = "Countermass", Description = "Every few seconds a weight swings out opposite the yoyo and hits flankers." },
			new YoyoTuningDef { Id = 8, Name = "Needlepoint", Description = "Fly straight corridors to build bonus damage. Wandering bleeds it away." },
			new YoyoTuningDef { Id = 9, Name = "Slipphase", Description = "After a cooldown it phases through one solid block instead of bouncing." },
			new YoyoTuningDef { Id = 100, Extended = true, Name = "Pinball Heart", Description = "Land 5 rebounds with alternating sharpness for a heavy final hit. Wrong angle resets." },
			new YoyoTuningDef { Id = 101, Extended = true, Name = "Gravity Well", Description = "While flying fast, nearby enemies get tugged toward the yoyo's path." },
			new YoyoTuningDef { Id = 102, Extended = true, Name = "Afterimage Drive", Description = "Fly one full loop to record it. Later, an afterimage replays part of it." },
			new YoyoTuningDef { Id = 103, Extended = true, Name = "Outbound Engine", Description = "The yoyo stays out much longer, but steering gets heavy over time." },
			new YoyoTuningDef { Id = 104, Extended = true, Name = "Prey Circuit", Description = "Hit a different enemy than your last to empower the next link (+45%)." },
			new YoyoTuningDef { Id = 105, Extended = true, Name = "Flywheel", Description = "Distance flown banks power (capped). Your hits spend it for bonus damage." },
			new YoyoTuningDef { Id = 106, Extended = true, Name = "Crossfire Array", Description = "Remembers struck enemies. Fly a true return to re-hit them in reverse order." },
			new YoyoTuningDef { Id = 107, Extended = true, Name = "Living Mechanism", Description = "Every few seconds it auto-tunes speed, range, or airtime using charges." },
		};

		public static YoyoTuningDef ById(int id)
		{
			foreach (var d in All) {
				if (d.Id == id)
					return d;
			}
			return null;
		}

		public static bool IsExtendedId(int id) => id >= ExtendedOffset;
	}
}
