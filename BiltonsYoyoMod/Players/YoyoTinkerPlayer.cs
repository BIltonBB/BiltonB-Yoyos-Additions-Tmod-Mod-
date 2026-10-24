using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Players
{
	// Per-player state for the Yoyo Tinkerer: rerolled installation prices.
	public class YoyoTinkerPlayer : ModPlayer
	{
		// modificationId -> current price in copper.
		public Dictionary<int, int> ModificationPrices = new();

		public int GetModificationPrice(int modificationId, int baseCost)
		{
			if (ModificationPrices.TryGetValue(modificationId, out int price))
				return price;
			return baseCost;
		}

		public void SetModificationPrice(int modificationId, int price)
		{
			ModificationPrices[modificationId] = price;
		}

		// Rerolls within [60%, 140%] of the base cost.
		public int RerollModificationPrice(int modificationId, int baseCost)
		{
			int min = (int)(baseCost * 0.6f);
			int max = (int)(baseCost * 1.4f);
			int price = Main.rand.Next(min, max + 1);
			ModificationPrices[modificationId] = price;
			return price;
		}

		public override void SaveData(TagCompound tag)
		{
			var list = new List<int>();
			foreach (var kv in ModificationPrices) {
				list.Add(kv.Key);
				list.Add(kv.Value);
			}
			tag["modPrices"] = list;
		}

		public override void LoadData(TagCompound tag)
		{
			ModificationPrices.Clear();
			if (!tag.ContainsKey("modPrices"))
				return;
			var list = tag.Get<List<int>>("modPrices");
			for (int i = 0; i + 1 < list.Count; i += 2)
				ModificationPrices[list[i]] = list[i + 1];
		}
	}
}
