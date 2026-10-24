using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Progression tier of a yoyo, 0 (starter) .. 1 (endgame). Every Magical
	/// Tech release reads this through <c>MagicalAbilityContext.Tier01</c> and
	/// scales with it: damage, zone sizes, projectile counts and the weight of
	/// the release beat all climb with the yoyo that carried the ability, so a
	/// Wooden Yoyo's well is a quiet pool and a Terrarian's is an event.
	///
	/// The tier is derived from the yoyo item itself (value, rarity and raw
	/// damage, whichever reads the most advanced) - it follows the item's
	/// progression without needing a hand-maintained table, and works for
	/// other mods' yoyos too.
	/// </summary>
	internal static class MagicalTier
	{
		private static readonly Dictionary<int, float> tierBySource = new();
		private static Dictionary<int, int> itemByShoot;

		public static float AbilityTier01(int sourceProjType)
		{
			if (sourceProjType <= 0)
				return 0.5f;
			if (tierBySource.TryGetValue(sourceProjType, out float cached))
				return cached;

			float tier = 0.5f;
			try {
				EnsureShootMap();
				if (itemByShoot.TryGetValue(sourceProjType, out int itemType)
					&& ContentSamples.ItemsByType.TryGetValue(itemType, out Item item) && item != null) {
					// Three progression reads; the strongest one wins so a yoyo
					// with a low price but a huge damage roll still reads late.
					float byValue = (float)((Math.Log10(Math.Max(1000.0, item.value)) - 3.2) / 3.0);
					float byRare = (item.rare + 1) / 11f;
					float byDamage = (item.damage - 8f) / 220f;
					tier = MathHelper.Clamp(Math.Max(byValue, Math.Max(byRare, byDamage)), 0f, 1f);
				}
			}
			catch {
				// A tier probe must never break a cast: fall back to neutral.
			}
			tierBySource[sourceProjType] = tier;
			return tier;
		}

		/// <summary>Lazy shoot-projectile -> item map over every loaded item.</summary>
		private static void EnsureShootMap()
		{
			if (itemByShoot != null)
				return;
			itemByShoot = new Dictionary<int, int>();
			foreach (KeyValuePair<int, Item> pair in ContentSamples.ItemsByType) {
				Item item = pair.Value;
				if (item == null || item.shoot <= ProjectileID.None)
					continue;
				if (!itemByShoot.ContainsKey(item.shoot))
					itemByShoot[item.shoot] = pair.Key;
			}
		}
	}
}
