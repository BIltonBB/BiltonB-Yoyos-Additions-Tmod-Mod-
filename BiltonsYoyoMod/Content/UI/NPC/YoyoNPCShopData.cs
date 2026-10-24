using System;
using System.Collections.Generic;
using Terraria;

using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Systems;
using BiltonsYoyoMod.Items;
using BiltonsYoyoMod.Items.Ancient;
using BiltonsYoyoMod.Items.BlackMarket;
using BiltonsYoyoMod.Items.Accessories;
using BiltonsYoyoMod.Items.Materials;
using BiltonsYoyoMod.Items.Consumables;
using BiltonsYoyoMod.Items.TierEmblems;

namespace BiltonsYoyoMod.Content.UI.YoyoNPC
{
	// One purchasable shop entry. LockReason returns null/"" when buyable.
	public class YoyoShopOffer
	{
		public int ItemType;
		public int PriceCopper;
		public Func<Player, string> LockReason;
		public string Note = "";
		// Non-null for daily-cycling slots: every item type that can appear here.
		public List<int> CyclePool;
	}

	// All revamp-NPC shop inventories in one place, shared by the custom UI panel.
	// (Vanilla NPCShop registration was removed: these NPCs sell through the panel.)
	public static class YoyoNPCShopData
	{
		// Rolled once per panel session so rare stock doesn't flicker while browsing.
		public static bool SessionRareStock;
		public static bool SessionLuckyStock;
		public static bool SessionMaterialStock;

		public static bool IsTownMerchant(int npcType)
		{
			return npcType == ModContent.NPCType<Content.NPCs.Town.YoyoMaster>()
				|| npcType == ModContent.NPCType<Content.NPCs.Town.YoyoTinkerer>()
				|| npcType == ModContent.NPCType<Content.NPCs.Town.AncientYoyoist>();
		}

		public static int AdjustedPrice(Player player, int npcType, int basePrice)
		{
			// Town merchants honor happiness discounts; wandering merchants charge full price.
			if (IsTownMerchant(npcType))
				return (int)(basePrice * player.currentShoppingSettings.PriceAdjustment);
			return basePrice;
		}

		private static YoyoShopOffer Offer(int type, int price, Func<Player, string> lockReason = null, string note = "", List<int> cyclePool = null)
		{
			return new YoyoShopOffer { ItemType = type, PriceCopper = price, LockReason = lockReason, Note = note, CyclePool = cyclePool };
		}

		public static string CyclePreview(List<int> pool, int shownType, int max = 4)
		{
			if (pool == null || pool.Count == 0)
				return "";
			var names = new List<string>();
			foreach (int type in pool) {
				if (type == shownType)
					continue;
				names.Add(Lang.GetItemNameValue(type));
				if (names.Count >= max)
					break;
			}
			string text = string.Join(", ", names);
			if (pool.Count - 1 > max)
				text += $", +{pool.Count - 1 - max} more";
			return text;
		}

		// Progression-gated daily-cycle pools

		public static List<int> CyclePoolStrings()
		{
			return new List<int> {
				ItemID.RedString, ItemID.OrangeString, ItemID.YellowString, ItemID.LimeString,
				ItemID.GreenString, ItemID.TealString, ItemID.CyanString, ItemID.SkyBlueString,
				ItemID.BlueString, ItemID.PurpleString, ItemID.VioletString, ItemID.PinkString,
				ItemID.BlackString, ItemID.BrownString,
			};
		}

		public static List<int> CyclePoolAxles()
		{
			var pool = new List<int> { ModContent.ItemType<YoyoAxleOW>(), ModContent.ItemType<YoyoAxleUW>() };
			if (NPC.downedBoss3) { // Post-Skeletron winding techniques.
				pool.Add(ModContent.ItemType<YoyoAxleLW>());
				pool.Add(ModContent.ItemType<YoyoAxleTW>());
			}
			return pool;
		}

		public static List<int> CyclePoolBearings()
		{
			// Wooden -> meteorite (evil boss) -> hellstone (Skeletron) -> mythril
			// (Hardmode) -> hallowed/chlorophyte (mechs) -> luminite (Moon Lord).
			var pool = new List<int> { ModContent.ItemType<LooseBearingsAW>() };
			if (NPC.downedBoss2)
				pool.Add(ModContent.ItemType<LooseBearingsAMI>());
			if (NPC.downedBoss3)
				pool.Add(ModContent.ItemType<LooseBearingsAHE>());
			if (Main.hardMode)
				pool.Add(ModContent.ItemType<LooseBearingsAM>());
			if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3) {
				pool.Add(ModContent.ItemType<LooseBearingsAHA>());
				pool.Add(ModContent.ItemType<LooseBearingsACH>());
			}
			if (NPC.downedMoonlord)
				pool.Add(ModContent.ItemType<LooseBearingsAL>());
			return pool;
		}

		public static List<int> CyclePoolPads()
		{
			var pool = new List<int> { ModContent.ItemType<ResponsePadsAB>() };
			if (NPC.downedBoss2) {
				pool.Add(ModContent.ItemType<ResponsePadsAF>());
				pool.Add(ModContent.ItemType<ResponsePadsAS>());
			}
			if (Main.hardMode)
				pool.Add(ModContent.ItemType<ResponsePadsAR>());
			if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3)
				pool.Add(ModContent.ItemType<ResponsePadsACB>());
			return pool;
		}

		public static List<YoyoShopOffer> GetOffers(int npcType, Player player)
		{
			if (npcType == ModContent.NPCType<Content.NPCs.Town.YoyoMaster>()) {
				var master = MasterOffers();
				// The Trial is sold ONLY post-Plantera: hidden entirely
				// before that, not just locked.
				if (!NPC.downedPlantBoss)
					master.RemoveAll(o => o.ItemType == ModContent.ItemType<Content.Items.TrialOfTheGuardian>());
				return master;
			}
			if (npcType == ModContent.NPCType<Content.NPCs.Town.YoyoTinkerer>())
				return TinkererOffers();
			if (npcType == ModContent.NPCType<Content.NPCs.Town.AncientYoyoist>())
				return AncientOffers();
			if (npcType == ModContent.NPCType<Content.NPCs.Town.SixStringer>())
				return SixStringerOffers();
			if (npcType == ModContent.NPCType<Content.NPCs.Town.YoyoCultist>())
				return CultistOffers();
			return new List<YoyoShopOffer>();
		}

		// Yoyo Master

		private static List<YoyoShopOffer> MasterOffers()
		{
			var list = new List<YoyoShopOffer> {
				Offer(ItemID.WhiteString, Item.buyPrice(silver: 5)),
				Offer(ItemID.WoodYoyo, Item.buyPrice(silver: 20)),
				Offer(DailyCounterweight(), Item.buyPrice(gold: 1), note: "Daily stock"),
				Offer(ItemID.YoYoGlove, Item.buyPrice(platinum: 5), player => Main.hardMode ? "Only sold before Hardmode" : null),
				Offer(ItemID.YoYoGlove, Item.buyPrice(gold: 50), player => !Main.hardMode ? "Only sold in Hardmode" : null),
				Offer(ModContent.ItemType<YoyoCaseA>(), Item.buyPrice(gold: 2)),
				Offer(ModContent.ItemType<YoyoDualCaseA>(), Item.buyPrice(gold: 5), player => !NPC.downedBoss2 ? "Defeat an evil boss" : null),
				Offer(ModContent.ItemType<YoyoBriefCaseA>(), Item.buyPrice(gold: 8), player => !NPC.downedBoss3 ? "Defeat Skeletron" : null),
				Offer(ModContent.ItemType<YoyoCollectionCaseA>(), Item.buyPrice(gold: 15), player => !NPC.downedBoss3 ? "Defeat Skeletron" : null),
				Offer(ModContent.ItemType<YoyoBackPackA>(), Item.buyPrice(gold: 25), player => !Main.hardMode ? "Only sold in Hardmode" : null),
				Offer(ModContent.ItemType<YoyoTrickTS>(), Item.buyPrice(gold: 10), player => !NPC.downedBoss3 ? "Defeat Skeletron" : null, "Trick: Sleeper"),
				Offer(ModContent.ItemType<YoyoTrickAWTD>(), Item.buyPrice(gold: 20), player => !Main.hardMode ? "Only sold in Hardmode" : null, "Trick: Walk the Dog"),
				Offer(ModContent.ItemType<YoyoTrickATE>(), Item.buyPrice(gold: 40), player => !(NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3) ? "Defeat a mechanical boss" : null, "Trick: Elevator"),
				Offer(ModContent.ItemType<YoyoTrickAET>(), Item.buyPrice(gold: 60), player => !NPC.downedPlantBoss ? "Defeat Plantera" : null, "Trick: Eiffel Tower"),
				Offer(ModContent.ItemType<YoyoTrickADNA>(), Item.buyPrice(platinum: 1, gold: 50), player => !NPC.downedAncientCultist ? "Defeat the Lunatic Cultist" : null, "Trick: DNA"),
				Offer(ModContent.ItemType<YoyoTrickABB>(), Item.buyPrice(platinum: 1, gold: 25), player => !NPC.downedGolemBoss ? "Defeat Golem" : null, "Trick: Boingy Boing"),
				Offer(ModContent.ItemType<Content.Items.TrialOfTheGuardian>(), Item.buyPrice(gold: 25), player => !NPC.downedPlantBoss ? "Defeat Plantera" : null, "Summons the Yoyo Guardian"),
			};
			AddEmblemOffer(list, ModContent.ItemType<T0Emblem>(), Item.buyPrice(gold: 10));
			AddEmblemOffer(list, ModContent.ItemType<T1Emblem>(), Item.buyPrice(gold: 20));
			AddEmblemOffer(list, ModContent.ItemType<T2Emblem>(), Item.buyPrice(gold: 30));
			AddEmblemOffer(list, ModContent.ItemType<T3Emblem>(), Item.buyPrice(gold: 40));
			AddEmblemOffer(list, ModContent.ItemType<T4Emblem>(), Item.buyPrice(gold: 50));
			AddEmblemOffer(list, ModContent.ItemType<T5Emblem>(), Item.buyPrice(gold: 60));
			AddEmblemOffer(list, ModContent.ItemType<T6Emblem>(), Item.buyPrice(gold: 70));
			AddEmblemOffer(list, ModContent.ItemType<T7Emblem>(), Item.buyPrice(gold: 80));
			AddEmblemOffer(list, ModContent.ItemType<T8Emblem>(), Item.buyPrice(gold: 90));
			AddEmblemOffer(list, ModContent.ItemType<T9Emblem>(), Item.buyPrice(platinum: 1));
			AddEmblemOffer(list, ModContent.ItemType<T10Emblem>(), Item.buyPrice(platinum: 1, gold: 10));
			return list;
		}

		private static void AddEmblemOffer(List<YoyoShopOffer> list, int emblemType, int price)
		{
			int captured = emblemType;
			list.Add(Offer(captured, price, player => !YoyoTownSystem.IsEmblemUnlocked(captured) ? "Hold 3 of this emblem at once to unlock" : null, "Tier Emblem"));
		}

		private static int DailyCounterweight()
		{
			int[] weights = { ItemID.RedCounterweight, ItemID.BlueCounterweight, ItemID.GreenCounterweight, ItemID.PurpleCounterweight, ItemID.YellowCounterweight, ItemID.BlackCounterweight };
			return Content.NPCs.Town.YoyoTownShared.PickDaily(weights);
		}

		// Yoyo Tinkerer (parts only; modules install via Mods tab)

		private static List<YoyoShopOffer> TinkererOffers()
		{
			var stringPool = CyclePoolStrings();
			var axlePool = CyclePoolAxles();
			var bearingPool = CyclePoolBearings();
			var padPool = CyclePoolPads();
			return new List<YoyoShopOffer> {
				Offer(ItemID.IronBar, Item.buyPrice(silver: 1)),
				Offer(ItemID.LeadBar, Item.buyPrice(silver: 1)),
				Offer(ItemID.Wire, Item.buyPrice(silver: 2)),
				Offer(ItemID.Cobweb, Item.buyPrice(copper: 5)),
				Offer(ItemID.Silk, Item.buyPrice(silver: 2)),
				Offer(ItemID.WhiteString, Item.buyPrice(silver: 5)),
				Offer(Content.NPCs.Town.YoyoTownShared.PickDaily(stringPool), Item.buyPrice(gold: 25), note: "Daily stock", cyclePool: stringPool),
				Offer(ModContent.ItemType<YoyoScraps>(), Item.buyPrice(silver: 25)),
				Offer(ItemID.YoYoGlove, Item.buyPrice(gold: 50)),
				Offer(Content.NPCs.Town.YoyoTownShared.PickDaily(axlePool), Item.buyPrice(gold: 20), note: "Daily stock", cyclePool: axlePool),
				Offer(Content.NPCs.Town.YoyoTownShared.PickDaily(bearingPool), Item.buyPrice(gold: 20), note: "Daily stock", cyclePool: bearingPool),
				Offer(Content.NPCs.Town.YoyoTownShared.PickDaily(padPool), Item.buyPrice(gold: 30), note: "Daily stock", cyclePool: padPool),
				Offer(ModContent.ItemType<BearingOil>(), Item.buyPrice(silver: 75), note: "Yoyo tonic"),
				Offer(ModContent.ItemType<StringWax>(), Item.buyPrice(gold: 1), note: "Yoyo tonic"),
			};
		}

		// Ancient Yo-yoist (reagents only; spells cast via Spells tab)

		private static List<YoyoShopOffer> AncientOffers()
		{
			return new List<YoyoShopOffer> {
				Offer(ItemID.FallenStar, Item.buyPrice(silver: 50)),
				Offer(ItemID.BottledWater, Item.buyPrice(silver: 10)),
				Offer(ItemID.Daybloom, Item.buyPrice(silver: 20)),
				Offer(ItemID.Fireblossom, Item.buyPrice(silver: 75)),
				Offer(ItemID.Deathweed, Item.buyPrice(silver: 75)),
				Offer(ItemID.Blinkroot, Item.buyPrice(silver: 25)),
				Offer(ItemID.Moonglow, Item.buyPrice(silver: 40)),
				Offer(ItemID.Waterleaf, Item.buyPrice(silver: 30)),
				Offer(ItemID.Sunflower, Item.buyPrice(silver: 50)),
				Offer(ItemID.ManaPotion, Item.buyPrice(gold: 1)),
				Offer(ItemID.GreaterManaPotion, Item.buyPrice(gold: 3)),
				Offer(ItemID.SpellTome, Item.buyPrice(gold: 10), player => !NPC.downedQueenSlime ? "Defeat Queen Slime" : null),
				Offer(ItemID.CrystalBall, Item.buyPrice(gold: 10), player => !NPC.downedQueenSlime ? "Defeat Queen Slime" : null),
				Offer(ModContent.ItemType<YoyoOfInsight>(), Item.buyPrice(gold: 1), player => Main.dayTime ? "Only sold at night" : null),
			};
		}

		// Six Stringer

		private static List<YoyoShopOffer> SixStringerOffers()
		{
			return new List<YoyoShopOffer> {
				Offer(ModContent.ItemType<IllegalYoyoParts>(), Item.buyPrice(silver: 50), note: "Illegal Yoyo Parts"),
				Offer(ModContent.ItemType<GoldYoyo>(), Item.buyPrice(gold: 8)),
				Offer(ModContent.ItemType<GoldenString>(), Item.buyPrice(gold: 10)),
				Offer(ModContent.ItemType<GoldenCounterweight>(), Item.buyPrice(gold: 12)),
				Offer(ItemID.YoYoGlove, Item.buyPrice(gold: 50)),
				Offer(ModContent.ItemType<YoyoCaseA>(), Item.buyPrice(gold: 4), player => !Main.hardMode ? "Hardmode stock" : null),
				Offer(ModContent.ItemType<YoyoDualCaseA>(), Item.buyPrice(gold: 8), player => !Main.hardMode ? "Hardmode stock" : null),
				Offer(ModContent.ItemType<YoyoBriefCaseA>(), Item.buyPrice(gold: 12), player => !Main.hardMode ? "Hardmode stock" : null),
				Offer(ModContent.ItemType<LuckyYoyo>(), Item.buyPrice(platinum: 1), LuckyLock(), "Lucky find"),
				Offer(ModContent.ItemType<IllegalYoyo>(), Item.buyPrice(platinum: 2), player => !Main.hardMode ? "Hardmode stock" : null),
				Offer(ModContent.ItemType<StringersSpecial>(), Item.buyPrice(platinum: 3), RareLock("Hardmode rare stock"), "Six Stringer-exclusive"),
				Offer(ModContent.ItemType<SmugglersGyroHarness>(), Item.buyPrice(platinum: 2, gold: 50), RareLock("Hardmode rare stock"), "Black-market rig"),
				Offer(ModContent.ItemType<SuperGoldYoyo>(), Item.buyPrice(platinum: 5), RareLock("Hardmode rare stock"), "Rare stock"),
			};
		}

		private static Func<Player, string> RareLock(string hardmodeText)
		{
			return player => {
				if (!Main.hardMode)
					return hardmodeText;
				if (!SessionRareStock)
					return "Not in stock tonight";
				return null;
			};
		}

		// Lucky Yoyo: sold at any time, but only on very lucky nights.
		private static Func<Player, string> LuckyLock()
		{
			return player => {
				if (!SessionLuckyStock)
					return "No luck tonight - come back another night";
				return null;
			};
		}

		// Scrap shipments come and go: materials are randomly in stock per visit.
		private static Func<Player, string> MaterialLock(string hardmodeText = null)
		{
			return player => {
				if (hardmodeText != null && !Main.hardMode)
					return hardmodeText;
				if (!SessionMaterialStock)
					return "No shipment tonight - check back later";
				return null;
			};
		}

		// Yoyo Cultist

		private static List<YoyoShopOffer> CultistOffers()
		{
			return new List<YoyoShopOffer> {
				Offer(ModContent.ItemType<AncientCounterweight>(), Item.buyPrice(gold: 5), player => !player.HasItem(ModContent.ItemType<EnchantedYoyo>()) ? "Requires an Enchanted Yoyo in inventory" : null),
				Offer(ModContent.ItemType<AncientYoyoCase>(), Item.buyPrice(gold: 15), player => !player.HasItem(ModContent.ItemType<YoyoCollectionCaseA>()) ? "Requires a Yoyo Collection Case in inventory" : null),
				Offer(ModContent.ItemType<EnchantedYoyo>(), Item.buyPrice(gold: 20)),
				Offer(ModContent.ItemType<AncientString>(), Item.buyPrice(gold: 5), player => !player.ZoneJungle ? "Only stocks this in the Jungle" : null),
				Offer(ModContent.ItemType<CultistYoyo>(), Item.buyPrice(gold: 30), player => !Main.hardMode ? "Hardmode relic" : null),
				Offer(ModContent.ItemType<ForbiddenYoyo>(), Item.buyPrice(gold: 50), player => !Main.hardMode ? "Hardmode relic" : null),
				Offer(ModContent.ItemType<AncientYoyoAccessory>(), Item.buyPrice(gold: 25), player => !Main.hardMode ? "Hardmode relic" : (!(NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3) ? "Requires a defeated mechanical boss" : null)),
				Offer(ModContent.ItemType<AncientYoyoGlove>(), Item.buyPrice(gold: 40), player => !Main.hardMode ? "Hardmode relic" : (!NPC.downedPlantBoss ? "Requires Plantera defeated" : null)),
				Offer(ModContent.ItemType<CelestialYoyoAccessory>(), Item.buyPrice(platinum: 1), player => !Main.hardMode ? "Hardmode relic" : null),
				Offer(ModContent.ItemType<AncientYoyoRelic>(), Item.buyPrice(platinum: 2), player => !Main.hardMode ? "Hardmode relic" : null),
				Offer(ModContent.ItemType<SeleneCoilYoyo>(), Item.buyPrice(platinum: 3), player => !Main.hardMode ? "Hardmode relic" : null, "Rare yoyo"),
				Offer(ModContent.ItemType<AncientScraps>(), Item.buyPrice(gold: 2), note: "Crafting material"),
				Offer(ModContent.ItemType<YoyoScraps>(), Item.buyPrice(silver: 50), MaterialLock(), "Crafting material"),
				Offer(ModContent.ItemType<YoyoParts>(), Item.buyPrice(gold: 2), MaterialLock(), "Crafting material"),
				Offer(ModContent.ItemType<AdvancedYoyoParts>(), Item.buyPrice(gold: 10), MaterialLock("Hardmode relic"), "Crafting material"),
				Offer(ModContent.ItemType<MagicalEssence>(), Item.buyPrice(gold: 5), player => !Main.hardMode ? "Hardmode relic" : null, "Crafting material"),
				Offer(ModContent.ItemType<CultistsDraught>(), Item.buyPrice(gold: 1), note: "Risky tonic"),
				Offer(ModContent.ItemType<MoonlightFlask>(), Item.buyPrice(gold: 2), note: "Moon tonic"),
				Offer(ModContent.ItemType<Content.Items.AncientWantedPaper>(), Item.buyPrice(gold: 10), player => !NPC.downedGolemBoss ? "Defeat Golem" : null, "Summons the Duelist"),
				Offer(ModContent.ItemType<RandomYoyo>(), Item.buyPrice(gold: 2)),
			};
		}

		// Buying

		public static bool TryBuy(Player player, int npcType, YoyoShopOffer offer, out string result)
		{
			string locked = offer.LockReason?.Invoke(player);
			if (!string.IsNullOrEmpty(locked)) {
				result = locked;
				SoundEngine.PlaySound(SoundID.MenuTick);
				return false;
			}
			int price = AdjustedPrice(player, npcType, offer.PriceCopper);
			if (!player.CanAfford(price)) {
				result = "Not enough coins.";
				SoundEngine.PlaySound(SoundID.MenuTick);
				return false;
			}
			player.PayCurrency(price);
			player.QuickSpawnItem(player.GetSource_Misc("yoyo_npc_shop"), offer.ItemType);
			SoundEngine.PlaySound(SoundID.CoinPickup);
			result = "";
			return true;
		}
	}

	// Yoyo Master item-for-item trades.
	public static class YoyoNPCTrade
	{
		public class TradeRow
		{
			public int A;
			public int B;
			public string Label;
		}

		public static List<TradeRow> GetPairs()
		{
			return new List<TradeRow> {
				new TradeRow { A = ModContent.ItemType<CorruptionYoyo>(), B = ModContent.ItemType<CrimsonYoyo>(), Label = "Evil yoyos" },
				new TradeRow { A = ItemID.WhiteString, B = ItemID.BlackString, Label = "Strings" },
				new TradeRow { A = ItemID.RedString, B = ItemID.CyanString, Label = "Red <> Cyan" },
				new TradeRow { A = ItemID.OrangeString, B = ItemID.BlueString, Label = "Orange <> Blue" },
				new TradeRow { A = ItemID.YellowString, B = ItemID.PurpleString, Label = "Yellow <> Purple" },
				new TradeRow { A = ItemID.LimeString, B = ItemID.PinkString, Label = "Lime <> Pink" },
				new TradeRow { A = ItemID.GreenString, B = ItemID.VioletString, Label = "Green <> Violet" },
				new TradeRow { A = ItemID.TealString, B = ItemID.SkyBlueString, Label = "Teal <> Sky Blue" },
				new TradeRow { A = ModContent.ItemType<YoyoShimmerBackPackA>(), B = ModContent.ItemType<YoyoBackPackA>(), Label = "Backpack" },
				new TradeRow { A = ModContent.ItemType<YoyoShimmerVaultA>(), B = ModContent.ItemType<YoyoVaultA>(), Label = "Vault" },
				new TradeRow { A = ModContent.ItemType<YoyoShimmerRuckSackA>(), B = ModContent.ItemType<YoyoRuckSackA>(), Label = "Rucksack" },
				new TradeRow { A = ItemID.WarriorEmblem, B = ModContent.ItemType<YoyoSubEmblem>(), Label = "Emblems" },
			};
		}

		public static bool Swap(Player player, int from, int to)
		{
			if (Content.NPCs.Town.YoyoTownShared.TryConsumeFromInventory(player, from, 1)) {
				player.QuickSpawnItem(player.GetSource_Misc("yoyo_trade"), to);
				SoundEngine.PlaySound(SoundID.CoinPickup);
				return true;
			}
			return false;
		}

		public static int TradeAll(Player player)
		{
			int done = 0;
			foreach (var row in GetPairs()) {
				int countA = Content.NPCs.Town.YoyoTownShared.CountInInventory(player, row.A);
				int countB = Content.NPCs.Town.YoyoTownShared.CountInInventory(player, row.B);
				if (countA > 0 && countA >= countB) {
					if (Swap(player, row.A, row.B)) done++;
				}
				else if (countB > 0) {
					if (Swap(player, row.B, row.A)) done++;
				}
			}
			return done;
		}

		public static bool ConvertHeldAccessory(Player player, out string result)
		{
			Item held = player.HeldItem;
			if (held == null || held.IsAir || Content.NPCs.Town.YoyoTownShared.IsYoyo(held) || !held.accessory || held.ModItem == null || held.ModItem.Mod != ModContent.GetInstance<BiltonsYoyoMod>()) {
				result = "Hold a modded yoyo accessory in your hand first.";
				return false;
			}
			int value = held.value;
			string name = held.Name;
			held.TurnToAir();
			YoyoTownSystem.GiveLifePotions(player, value);
			SoundEngine.PlaySound(SoundID.CoinPickup);
			result = $"Traded {name} for healing potions.";
			return true;
		}
	}
}
