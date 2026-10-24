using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;
using BiltonsYoyoMod.Content.UI.YoyoNPC;

namespace BiltonsYoyoMod.Content.Customization
{
	// Curse rolling at the Ancient Yoyo-ist. Costs Magical Essence material (not coins).
	// Odds shown in UI come from YoyoCurseDefs.OddsForEssence; rolls use RollCurse (same source).
	public static class YoyoCurseActions
	{
		public static int SelectedEssence = 3;

		public static void RollCurse(Player player)
		{
			Item held = YoyoNPCActions.HeldYoyo(player);
			if (held == null) {
				YoyoNPCUISystem.SetMessage("Hold a yoyo in your hand first. Only yoyos. Nothing else.");
				return;
			}
			if (!NPC.downedPlantBoss) {
				YoyoNPCUISystem.SetMessage("Curses require Plantera defeated.");
				return;
			}
			int essenceType = ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.MagicalEssence>();
			int need = SelectedEssence;
			int have = YoyoTownShared.CountInInventory(player, essenceType);
			if (have < need) {
				YoyoNPCUISystem.SetMessage($"Needs {need} Magical Essence (you have {have}).");
				return;
			}
			// Consume first (each roll consumes essence, success or not).
			YoyoTownShared.TryConsumeFromInventory(player, essenceType, need);
			bool got = YoyoCurseDefs.RollCurse(need, out int curseId);
			if (!got) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				YoyoCurseDefs.OddsForEssence(YoyoCurseDefs.NormalizedEssence(need), out float chance, out float high);
				YoyoNPCUISystem.SetMessage($"The ritual fizzles. ({chance:0%} curse chance.) Essence consumed. Existing curse kept.", 480);
				return;
			}
			var cust = held.GetGlobalItem<YoyoCustomizationGlobalItem>();
			string replaced = cust.CurseId >= 0 ? YoyoCurseDefs.ById(cust.CurseId)?.Name : null;
			cust.CurseId = curseId; // New curse replaces old; other modifiers untouched.
			YoyoCustomizationGlobalItem.RefreshNameOverride(held);
			var def = YoyoCurseDefs.ById(curseId);
			SoundEngine.PlaySound(SoundID.Item8);
			YoyoNPCUISystem.SetMessage(replaced == null
				? $"Your {held.Name} is now cursed: [{def.Name}]! {def.Description}"
				: $"Replaced {replaced} with [{def.Name}] on {held.Name}. Other modifiers kept. {def.Description}", 480);
		}

		public static void StripCurse(Player player)
		{
			Item held = YoyoNPCActions.HeldYoyo(player);
			if (held == null) {
				YoyoNPCUISystem.SetMessage("Hold a yoyo in your hand first.");
				return;
			}
			var cust = held.GetGlobalItem<YoyoCustomizationGlobalItem>();
			if (cust.CurseId < 0) {
				YoyoNPCUISystem.SetMessage("That yoyo carries no curse.");
				return;
			}
			int price = Item.buyPrice(gold: 1);
			if (!player.CanAfford(price)) {
				YoyoNPCUISystem.SetMessage("Cleansing costs 1 gold.");
				return;
			}
			player.PayCurrency(price);
			cust.CurseId = -1;
			YoyoCustomizationGlobalItem.RefreshNameOverride(held);
			SoundEngine.PlaySound(SoundID.MenuTick);
			YoyoNPCUISystem.SetMessage($"Cleansed {held.Name}. Other modifiers kept.");
		}
	}
}
