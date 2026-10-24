using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace BiltonsYoyoMod.Items.Utility
{
	[Flags]
	public enum YoyoRole
	{
		None = 0,
		Fast = 1 << 0,
		Long = 1 << 1,
		Heavy = 1 << 2,
		HighDamage = 1 << 3,
		Pierce = 1 << 4,
	}

	/// <summary>
	/// Conservative, data-derived role tags for the Splicer search. Tags are
	/// relative to the yoyo population rather than hard-coded per item, so a
	/// modded yoyo is judged by the same rules as a vanilla one. Thresholds are
	/// deliberately strict: a tag should mean "this is one of the fast ones",
	/// not merely "this is not the slowest".
	/// </summary>
	public static class YoyoSpliceRoles
	{
		private static readonly Dictionary<int, YoyoRole> cache = new();
		private static bool built;
		private static bool failed;

		private static int useCut = 24;
		private static float damageCut = 30f;
		private static float rangeCut = 300f;
		private static float knockbackCut = 5f;

		public static YoyoRole Of(int type)
		{
			EnsureBuilt();
			return cache.TryGetValue(type, out YoyoRole role) ? role : YoyoRole.None;
		}

		public static bool Has(int type, YoyoRole role)
			=> role == YoyoRole.None || (Of(type) & role) == role;

		public static string TagText(int type)
		{
			YoyoRole role = Of(type);
			if (role == YoyoRole.None)
				return "";
			var tags = new List<string>(5);
			if ((role & YoyoRole.Fast) != 0)
				tags.Add("FAST");
			if ((role & YoyoRole.Long) != 0)
				tags.Add("LONG");
			if ((role & YoyoRole.Heavy) != 0)
				tags.Add("HEAVY");
			if ((role & YoyoRole.HighDamage) != 0)
				tags.Add("HIGH DMG");
			if ((role & YoyoRole.Pierce) != 0)
				tags.Add("PIERCE");
			return string.Join("  ", tags);
		}

		/// <summary>
		/// Parses the Splicer query. Plain words match the item name; tokens
		/// written as role:fast (and its aliases) are AND requirements. An
		/// unknown role token matches nothing so a typo is visible instead of
		/// silently widening the search.
		/// </summary>
		public static bool MatchesQuery(int type, string query)
		{
			if (string.IsNullOrWhiteSpace(query))
				return true;

			string needle = "";
			YoyoRole required = YoyoRole.None;
			foreach (string raw in query.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
				if (raw.StartsWith("role:", StringComparison.OrdinalIgnoreCase)) {
					if (!TryParseRole(raw.Substring(5), out YoyoRole role))
						return false;
					required |= role;
				}
				else {
					if (needle.Length > 0)
						needle += " ";
					needle += raw;
				}
			}

			if (needle.Length > 0) {
				string name = StringSplicerPlayer.ItemName(type);
				if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
					return false;
			}
			return required == YoyoRole.None || (Of(type) & required) == required;
		}

		private static bool TryParseRole(string raw, out YoyoRole role)
		{
			role = YoyoRole.None;
			if (string.IsNullOrWhiteSpace(raw))
				return false;
			string key = raw.Trim().Replace("-", "").Replace("_", "").Replace(" ", "")
				.ToLowerInvariant();
			switch (key) {
				case "fast":
				case "speed":
					role = YoyoRole.Fast;
					return true;
				case "long":
				case "range":
					role = YoyoRole.Long;
					return true;
				case "heavy":
				case "kb":
				case "knockback":
					role = YoyoRole.Heavy;
					return true;
				case "dmg":
				case "damage":
				case "highdmg":
				case "highdamage":
				case "power":
					role = YoyoRole.HighDamage;
					return true;
				case "pierce":
				case "piercing":
					role = YoyoRole.Pierce;
					return true;
				default:
					return false;
			}
		}

		private static void EnsureBuilt()
		{
			if (built || failed)
				return;
			built = true;
			try {
				if (Main.dedServ)
					return;
				BuildCache();
			}
			catch {
				// Role search is a convenience. If content samples are not
				// available yet, leave the cache empty rather than breaking
				// the wheel.
				cache.Clear();
				failed = true;
			}
		}

		private static void BuildCache()
		{
			var samples = new List<(int Type, float Damage, int Use, float Range, float Knockback)>();
			foreach (KeyValuePair<int, Item> pair in ContentSamples.ItemsByType) {
				Item item = pair.Value;
				if (item == null || item.IsAir || !Content.NPCs.Town.YoyoTownShared.IsYoyoShape(item))
					continue;
				if (item.damage <= 0 || item.useTime <= 0)
					continue;
				float range = 0f;
				try {
					if (item.shoot > ProjectileID.None
						&& item.shoot < ProjectileID.Sets.YoyosMaximumRange.Length)
						range = ProjectileID.Sets.YoyosMaximumRange[item.shoot];
				}
				catch { }
				samples.Add((item.type, item.damage, item.useTime, range, item.knockBack));
			}

			if (samples.Count == 0)
				return;

			var useTimes = new List<float>(samples.Count);
			var damages = new List<float>(samples.Count);
			var ranges = new List<float>(samples.Count);
			var knockbacks = new List<float>(samples.Count);
			foreach (var s in samples) {
				useTimes.Add(s.Use);
				damages.Add(s.Damage);
				if (s.Range > 0f)
					ranges.Add(s.Range);
				knockbacks.Add(s.Knockback);
			}

			// A fast yoyo must also be genuinely quick, not merely the fastest
			// of a slow set. The same absolute floor keeps HEAVY meaningful
			// when every yoyo has modest knockback.
			useCut = Math.Min(24, (int)MathF.Round(Percentile(useTimes, 0.30f)));
			damageCut = Math.Max(30f, Percentile(damages, 0.70f));
			rangeCut = Math.Max(300f, Percentile(ranges, 0.70f));
			knockbackCut = Math.Max(5f, Percentile(knockbacks, 0.70f));

			foreach (var s in samples) {
				YoyoRole role = YoyoRole.None;
				if (s.Use > 0 && s.Use <= useCut)
					role |= YoyoRole.Fast;
				if (s.Range > 0f && s.Range >= rangeCut)
					role |= YoyoRole.Long;
				if (s.Knockback >= knockbackCut)
					role |= YoyoRole.Heavy;
				if (s.Damage >= damageCut)
					role |= YoyoRole.HighDamage;
				if (Pierces(s.Type))
					role |= YoyoRole.Pierce;
				if (role != YoyoRole.None)
					cache[s.Type] = role;
			}
		}

		private static bool Pierces(int itemType)
		{
			try {
				if (!ContentSamples.ItemsByType.TryGetValue(itemType, out Item item)
					|| item == null || item.shoot <= ProjectileID.None)
					return false;
				var sample = new Projectile();
				sample.SetDefaults(item.shoot);
				// -1 is the normal yoyo "stays alive" convention, not a
				// piercing hit count. Only explicit finite multi-hit counts
				// qualify as PIERCE here.
				return sample.penetrate > 1;
			}
			catch {
				return false;
			}
		}

		private static float Percentile(List<float> values, float percentile)
		{
			if (values == null || values.Count == 0)
				return 0f;
			values.Sort();
			int index = (int)MathF.Round((values.Count - 1) * MathHelper.Clamp(percentile, 0f, 1f));
			return values[Math.Clamp(index, 0, values.Count - 1)];
		}
	}
}
