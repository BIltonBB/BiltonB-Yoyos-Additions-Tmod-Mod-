using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Index
{
	/// <summary>
	/// Registry of every yoyo item type (modded + vanilla). Built lazily after
	/// content is loaded, so it never depends on load order. Bug-proof: any
	/// failure yields an empty list, never a crash.
	/// </summary>
	public static class YoyoIndexRegistry
	{
		private static List<int> cached;

		public static List<int> AllYoyoTypes()
		{
			if (cached != null)
				return cached;
			var list = new List<int>();
			try {
				// Vanilla yoyos via the vanilla set.
				for (int t = 0; t < ItemID.Count; t++) {
					try {
						if (ItemID.Sets.Yoyo[t])
							list.Add(t);
					}
					catch { }
				}
				// Modded yoyos via shape check (covers this mod + any other mod's yoyos).
				for (int t = ItemID.Count; t < ItemLoader.ItemCount; t++) {
					try {
						var tmp = new Item();
						tmp.SetDefaults(t);
						if (!tmp.IsAir && Content.NPCs.Town.YoyoTownShared.IsYoyoShape(tmp))
							list.Add(t);
					}
					catch { }
				}
			}
			catch { }
			// Sort by value so progression reads naturally; stable fallback by type.
			list.Sort((a, b) => {
				int va = 0, vb = 0;
				try {
					var ia = new Item(); ia.SetDefaults(a); va = ia.value;
					var ib = new Item(); ib.SetDefaults(b); vb = ib.value;
				}
				catch { }
				int c = va.CompareTo(vb);
				return c != 0 ? c : a.CompareTo(b);
			});
			cached = list;
			return cached;
		}

		public static void Invalidate() => cached = null;

		/// <summary>Short source hint. Never invents a wrong source: unknown = guidebook pointer.</summary>
		public static string SourceHint(int itemType)
		{
			// Vanilla: generic pointer (dozens of sources, all documented in the Yoyos book).
			if (itemType < ItemID.Count)
				return "Vanilla yoyo - craft / chest / drop (see Guidebook: Yoyos)";
			// Modded: try the item's own tooltip first line? Too fragile; use the book pointer.
			try {
				string name = Lang.GetItemNameValue(itemType);
				if (!string.IsNullOrEmpty(name))
					return $"{name} - see Guidebook: Yoyos for source";
			}
			catch { }
			return "See Guidebook: Yoyos for source";
		}
	}
}
