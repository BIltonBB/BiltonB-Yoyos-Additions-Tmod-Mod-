using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Common
{
	// Exact-percent drop rule (75/85/90 have no plain-Common equivalent)
	// that also reports itself so Boss Checklist displays the real rates.
	public class PercentDropRule : IItemDropRule
	{
		public List<IItemDropRuleChainAttempt> ChainedRules { get; } = new();

		private readonly int itemId;
		private readonly int percent;
		private readonly int minStack;
		private readonly int maxStack;

		public PercentDropRule(int itemId, int percent, int minStack = 1, int maxStack = 1)
		{
			this.itemId = itemId;
			this.percent = percent;
			this.minStack = minStack;
			this.maxStack = maxStack;
		}

		public bool CanDrop(DropAttemptInfo info)
		{
			return true;
		}

		public ItemDropAttemptResult TryDroppingItem(DropAttemptInfo info)
		{
			ItemDropAttemptResult result = default;
			if (info.rng.Next(100) >= percent) {
				result.State = ItemDropAttemptResultState.FailedRandomRoll;
				return result;
			}
			int stack = info.rng.Next(minStack, maxStack + 1);
			Item.NewItem(new EntitySource_Loot(info.npc), info.npc.Center, itemId, stack);
			result.State = ItemDropAttemptResultState.Success;
			return result;
		}

		public void ReportDroprates(List<DropRateInfo> drops, DropRateInfoChainFeed rates)
		{
			drops.Add(new DropRateInfo(itemId, minStack, maxStack, percent / 100f, null));
		}
	}
}
