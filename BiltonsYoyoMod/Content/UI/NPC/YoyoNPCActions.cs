using System;
using Terraria;

using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;
using BiltonsYoyoMod.Content.NPCs.Town;
using BiltonsYoyoMod.Content.Systems;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Content.UI.YoyoNPC
{
	// All panel button effects in one place.
	public static class YoyoNPCActions
	{
		public static Item HeldYoyo(Player player)
		{
			Item held = player.HeldItem;
			if (held != null && !held.IsAir && YoyoTownShared.IsYoyo(held) && YoyoTownShared.IsYoyoShape(held))
				return held;
			return null;
		}

		public static void InstallMod(Player player, YoyoModificationDef def)
		{
			Item held = HeldYoyo(player);
			if (held == null) {
				YoyoNPCUISystem.SetMessage("Hold a yoyo in your hand first.");
				return;
			}
			var tinker = player.GetModPlayer<YoyoTinkerPlayer>();
			int price = tinker.GetModificationPrice(def.Id, def.BaseCostCopper);
			if (!player.CanAfford(price)) {
				YoyoNPCUISystem.SetMessage($"Needs {YoyoNPCPanel.FormatCoins(price)}.");
				return;
			}
			player.PayCurrency(price);
			held.GetGlobalItem<YoyoEnhancementGlobalItem>().ModificationId = def.Id;
			YoyoEnhancementGlobalItem.RefreshNameOverride(held);
			SoundEngine.PlaySound(SoundID.CoinPickup);
			YoyoNPCUISystem.SetMessage($"Installed {def.Name} onto {held.Name}.");
		}

		// Strip the mod and/or spell off the held yoyo for 1 gold.
		// The Tinkerer strips mods, the Yo-yoist strips spells.
		public static void StripEnhancements(Player player, bool stripMod, bool stripSpell)
		{
			Item held = HeldYoyo(player);
			if (held == null) {
				YoyoNPCUISystem.SetMessage("Hold a yoyo in your hand first.");
				return;
			}
			var enh = held.GetGlobalItem<YoyoEnhancementGlobalItem>();
			bool hasMod = stripMod && enh.ModificationId >= 0;
			bool hasSpell = stripSpell && enh.SpellId >= 0;
			if (!hasMod && !hasSpell) {
				YoyoNPCUISystem.SetMessage("That yoyo has nothing to strip here.");
				return;
			}
			int price = Item.buyPrice(gold: 1);
			if (!player.CanAfford(price)) {
				YoyoNPCUISystem.SetMessage("Stripping costs 1 gold.");
				return;
			}
			player.PayCurrency(price);
			if (hasMod)
				enh.ModificationId = -1;
			if (hasSpell)
				enh.SpellId = -1;
			YoyoEnhancementGlobalItem.RefreshNameOverride(held);
			SoundEngine.PlaySound(SoundID.MenuTick);
			YoyoNPCUISystem.SetMessage($"Stripped {held.Name} clean.");
		}
		// Rerolling the price costs money: 10% of base cost (min 50 copper).
		public static int RerollFee(YoyoModificationDef def) => System.Math.Max(50, def.BaseCostCopper / 10);

		public static void RerollMod(Player player, YoyoModificationDef def)
		{
			int fee = RerollFee(def);
			if (!player.CanAfford(fee)) {
				YoyoNPCUISystem.SetMessage($"Rerolling costs {YoyoNPCPanel.FormatCoins(fee)}.");
				return;
			}
			player.PayCurrency(fee);
			int price = player.GetModPlayer<YoyoTinkerPlayer>().RerollModificationPrice(def.Id, def.BaseCostCopper);
			SoundEngine.PlaySound(SoundID.MenuTick);
			YoyoNPCUISystem.SetMessage($"{def.Name} price rerolled: {YoyoNPCPanel.FormatCoins(price)} (fee {YoyoNPCPanel.FormatCoins(fee)}).");
		}

		public static void CastSpell(Player player, int tier)
		{
			Item held = HeldYoyo(player);
			if (held == null) {
				YoyoNPCUISystem.SetMessage("Hold a yoyo in your hand first. Only yoyos. Nothing else.");
				return;
			}
			int price = YoyoSpellDefs.TierPrices[tier];
			if (!player.CanAfford(price)) {
				YoyoNPCUISystem.SetMessage($"The {YoyoSpellDefs.TierNames[tier].ToLower()} ritual costs {YoyoNPCPanel.FormatCoins(price)}.");
				return;
			}
			player.PayCurrency(price);
			int spellId = YoyoSpellDefs.RollSpell(Main.hardMode, tier);
			held.GetGlobalItem<YoyoEnhancementGlobalItem>().SpellId = spellId;
			YoyoEnhancementGlobalItem.RefreshNameOverride(held);
			var def = YoyoSpellDefs.ById(spellId);
			SoundEngine.PlaySound(SoundID.Item8);
			YoyoNPCUISystem.SetMessage($"Your {held.Name} now carries [{def.Name}]! {def.Description}", 480);
		}

		public static void SwapTrade(Player player, int from, int to)
		{
			if (YoyoNPCTrade.Swap(player, from, to))
				YoyoNPCUISystem.SetMessage("Traded.");
			else
				YoyoNPCUISystem.SetMessage("You don't have that item.");
		}

		public static void TradeAll(Player player)
		{
			int done = YoyoNPCTrade.TradeAll(player);
			YoyoNPCUISystem.SetMessage(done > 0 ? $"Exchanged {done} trade(s)." : "Nothing convertible in your inventory.");
		}

		public static void ConvertHeld(Player player)
		{
			if (!YoyoNPCTrade.ConvertHeldAccessory(player, out string result))
				YoyoNPCUISystem.SetMessage(result);
			else
				YoyoNPCUISystem.SetMessage(result);
		}

		public static void PetBilly()
		{
			YoyoNPCUISystem.TalkLine = Main.rand.NextBool() ? "*Bearing noises*" : "*Bearing noises #2*";
			SoundEngine.PlaySound(SoundID.MenuTick);
		}

		public static void InstallBillyType(Player player, int type)
		{
			if (YoyoTownSystem.BillyUpgrades.Contains(type))
				return;
			if (YoyoTownShared.TryConsumeFromInventory(player, type, 1)) {
				YoyoTownSystem.BillyUpgrades.Add(type);
				SoundEngine.PlaySound(SoundID.CoinPickup);
				if (Main.netMode == NetmodeID.MultiplayerClient)
					NetMessage.SendData(MessageID.WorldData);
				YoyoNPCUISystem.SetMessage($"Installed! ({Content.NPCs.Town.Pets.BillyTheBearing.UpgradeCount}/{Content.NPCs.Town.Pets.BillyTheBearing.MaxUpgrades})");
			}
			else {
				YoyoNPCUISystem.SetMessage("You don't have one of those.");
			}
		}

		// Click a fitted bearing to switch it off (red), click again to refit.
		public static void ToggleBillyType(int type)
		{
			if (!YoyoTownSystem.BillyUpgrades.Contains(type))
				return;
			if (YoyoTownSystem.BillyInactive.Contains(type)) {
				YoyoTownSystem.BillyInactive.Remove(type);
				YoyoNPCUISystem.SetMessage("Bearing refitted.");
			}
			else {
				YoyoTownSystem.BillyInactive.Add(type);
				YoyoNPCUISystem.SetMessage("Bearing switched off.");
			}
			SoundEngine.PlaySound(SoundID.MenuTick);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendData(MessageID.WorldData);
		}

		public static void InstallAllBilly(Player player)
		{
			int done = 0;
			foreach (int type in Content.NPCs.Town.Pets.BillyTheBearing.UpgradeTypeList()) {
				if (YoyoTownSystem.BillyUpgrades.Contains(type))
					continue;
				if (YoyoTownShared.TryConsumeFromInventory(player, type, 1)) {
					YoyoTownSystem.BillyUpgrades.Add(type);
					done++;
				}
			}
			if (Main.netMode == NetmodeID.MultiplayerClient && done > 0)
				NetMessage.SendData(MessageID.WorldData);
			SoundEngine.PlaySound(done > 0 ? SoundID.CoinPickup : SoundID.MenuTick);
			YoyoNPCUISystem.SetMessage(done > 0 ? $"Installed {done} bearing(s)." : "No new bearing types in your inventory.");
		}

		public static void PetMagical(Player player)
		{
			player.AddBuff(ModContent.BuffType<Content.Buffs.MagicalEssence>(), 60 * 60 * 8);
			YoyoNPCUISystem.TalkLine = Main.rand.NextBool() ? "You are granted with magic." : "The magic is being absorbed into your body.";
			SoundEngine.PlaySound(SoundID.Item8);
			YoyoNPCUISystem.SetMessage("Magical Essence: +5% yoyo damage for 8 hours.");
		}
	}
}
