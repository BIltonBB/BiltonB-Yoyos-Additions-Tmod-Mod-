using System.Collections.Generic;

namespace BiltonsYoyoMod.Content.NPCs.Town
{
	// The 10 mechanical modifications installed by the Yoyo Tinkerer.
	// A modification is an extra property on a yoyo: it stacks with the normal
	// Terraria reforge AND with an Ancient Yo-yoist spell, e.g.
	// Quick [Poisoned] {Weighted Zinc Clutch} Wooden Yoyo.
	public class YoyoModificationDef
	{
		public int Id;
		public string Name;
		public string ShortName; // Used inside {braces}.
		public string Description;
		public int BaseCostCopper;
		public bool Hardmode;

		// Gameplay fields (read by Common.YoyoEnhancementGlobalItem).
		public int ExtraRangeTiles;
		public float SizeMultiplier = 1f;
		public int BonusDamage;
		public float DamageMultiplier = 1f;
		public float LaunchSpeedMultiplier = 1f;
		public float TopSpeedMultiplier = 1f;
		public float LifetimeMultiplier = 1f;
	}

	public static class YoyoModificationDefs
	{
		public static readonly List<YoyoModificationDef> All = new()
		{
			new YoyoModificationDef {
				Id = 0, Name = "High-Tensile Spool", ShortName = "High-Tensile Spool",
				Description = "Installs a micro-wound high-density spool inside the casing. +2 tiles maximum deployment range.",
				BaseCostCopper = 15000, Hardmode = false, ExtraRangeTiles = 2,
			},
			new YoyoModificationDef {
				Id = 1, Name = "Weighted Zinc Clutch", ShortName = "Weighted Zinc",
				Description = "+15% physical yoyo size, +3 damage, -10% launch speed.",
				BaseCostCopper = 30000, Hardmode = false, SizeMultiplier = 1.15f, BonusDamage = 3, LaunchSpeedMultiplier = 0.9f,
			},
			new YoyoModificationDef {
				Id = 2, Name = "Kinetic Snap-Spring", ShortName = "Snap-Spring",
				Description = "When the yoyo hits an enemy and changes direction, +30% attack-speed velocity for 1.5 seconds.",
				BaseCostCopper = 50000, Hardmode = false,
			},
			new YoyoModificationDef {
				Id = 3, Name = "Aerodynamic Wind Vanes", ShortName = "Wind Vanes",
				Description = "+25% extension speed and +25% retraction speed.",
				BaseCostCopper = 80000, Hardmode = false, TopSpeedMultiplier = 1.25f,
			},
			new YoyoModificationDef {
				Id = 4, Name = "Spoked Harpoon Axle", ShortName = "Harpoon Axle",
				Description = "During the first 30 tiles of its launch the yoyo behaves like a piercing spear (+30 armor penetration).",
				BaseCostCopper = 120000, Hardmode = false,
			},
			new YoyoModificationDef {
				Id = 5, Name = "Magnetic Tractor Wire", ShortName = "Tractor Wire",
				Description = "Pulls loose Coins, Hearts and Mana Stars toward the deployed yoyo (12 tiles).",
				BaseCostCopper = 200000, Hardmode = true,
			},
			new YoyoModificationDef {
				Id = 6, Name = "Orbi-Shield Counterweight", ShortName = "Orbi-Shield",
				Description = "A mechanical ring orbits the active yoyo and destroys minor enemy projectiles on contact.",
				BaseCostCopper = 350000, Hardmode = true,
			},
			new YoyoModificationDef {
				Id = 7, Name = "Centrifugal Shredder Flaps", ShortName = "Shredder Flaps",
				Description = "Critical Strike Chance rises while deployed, up to +15% after 4 seconds.",
				BaseCostCopper = 550000, Hardmode = true,
			},
			new YoyoModificationDef {
				Id = 8, Name = "Thrust Vector Thrusters", ShortName = "Vector Thrusters",
				Description = "WASD nudges the deployed yoyo (mouse stays dominant). +5% damage.",
				BaseCostCopper = 1000000, Hardmode = true, BonusDamage = 0,
			},
			new YoyoModificationDef {
				Id = 9, Name = "Dimensional Quantum Spool", ShortName = "Quantum Spool",
				Description = "Yoyo and string pass through blocks. Quantum Spool keybind teleports you to the yoyo. +30% damage inside blocks.",
				BaseCostCopper = 5000000, Hardmode = true,
			},
			new YoyoModificationDef {
				Id = 10, Name = "Heavy Core", ShortName = "Heavy Core",
				Description = "Pours molten ballast into the core. +18% damage, -15% launch speed.",
				BaseCostCopper = 120000, Hardmode = false, DamageMultiplier = 1.18f, LaunchSpeedMultiplier = 0.85f,
			},
			new YoyoModificationDef {
				Id = 11, Name = "Overclocked Bearings", ShortName = "Overclocked",
				Description = "Spins past factory safety limits. +20% top speed, -10% damage.",
				BaseCostCopper = 750000, Hardmode = true, DamageMultiplier = 0.9f, TopSpeedMultiplier = 1.2f,
			},
			new YoyoModificationDef {
				Id = 12, Name = "Echo Chamber", ShortName = "Echo",
				Description = "Hollow resonant shell. Emits a damaging shockwave around the yoyo every 2 seconds.",
				BaseCostCopper = 1500000, Hardmode = true,
			},
			new YoyoModificationDef {
				Id = 13, Name = "Second Wind", ShortName = "Second Wind",
				Description = "A reserve spool kicks in mid-flight. Yoyo stays deployed 50% longer.",
				BaseCostCopper = 90000, Hardmode = false, LifetimeMultiplier = 1.5f,
			},
		};

		public static YoyoModificationDef ById(int id)
		{
			foreach (var def in All) {
				if (def.Id == id)
					return def;
			}
			return null;
		}
	}
}
