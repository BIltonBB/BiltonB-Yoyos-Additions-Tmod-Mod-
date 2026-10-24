using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Customization
{
	// Per-mod difficulty stars (1-3): stronger, more build-warping mods are harder
	// to earn in minigames and hungrier in resources. Single source of truth so
	// browse costs, minigame tuning and picker display always agree.
	public static class YoyoDifficulty
	{
		// kind: 0 tuning, 1 calibration, 2 mastery.
		public static int Stars(int kind, int id)
		{
			if (kind == 0) {
				if (id >= 100) return id switch {
					100 => 3, 101 => 2, 102 => 3, 103 => 2,
					104 => 2, 105 => 3, 106 => 3, _ => 2,
				};
				return id switch {
					0 => 1, 1 => 2, 2 => 2, 3 => 2, 4 => 2,
					5 => 1, 6 => 1, 7 => 2, 8 => 1, _ => 3,
				};
			}
			if (kind == 1) {
				if (id >= 100) return id switch {
					100 => 2, 101 => 2, 102 => 3, 103 => 2,
					104 => 3, 105 => 1, 106 => 3, _ => 1,
				};
				return id switch {
					0 => 1, 1 => 2, 2 => 2, 3 => 2, 4 => 1,
					5 => 1, 6 => 2, 7 => 3, 8 => 1, _ => 2,
				};
			}
			if (id >= 100) return id switch {
				100 => 3, 101 => 2, 102 => 3, 103 => 3,
				104 => 3, 105 => 2, 106 => 3, _ => 3,
			};
			return id switch {
				0 => 2, 1 => 1, 2 => 2, 3 => 2, 4 => 2,
				5 => 2, 6 => 3, 7 => 2, 8 => 1, _ => 2,
			};
		}

		public static string StarString(int stars) => stars >= 3 ? "***" : stars == 2 ? "**" : "*";

		// Resource requirement per family (browse purchases; minigame wins waive it).
		// Returns (itemType, count). Count scales with stars.
		public static (int type, int count) CostMat(int kind, bool extendedOrTrue, int stars)
		{
			if (kind == 0) {
				if (extendedOrTrue)
					return (ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>(), 2 + 2 * stars);
				return (ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 4 + 4 * stars);
			}
			if (kind == 1) {
				if (extendedOrTrue)
					return (Terraria.ID.ItemID.SoulofSight, 1 + stars);
				return (Terraria.ID.ItemID.FallenStar, 1 + stars);
			}
			if (extendedOrTrue)
				return (Terraria.ID.ItemID.Ectoplasm, 2 + 2 * stars);
			return (Terraria.ID.ItemID.CrystalShard, 3 + 3 * stars);
		}
	}
}
