using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using ModItems = BiltonsYoyoMod.Items;

namespace BiltonsYoyoMod.Content.Guidebook
{
	/// <summary>
	/// Assigns the real item plates under each authored page. Pages are matched
	/// by title, not by array position: adding a page must never shuffle the
	/// pictures onto the wrong entry (the event-yoyo page was the visible bug).
	/// Idempotent, never throws.
	/// </summary>
	public static class YoyoGuidebookIcons
	{
		private static bool ready;

		public static void Ensure()
		{
			if (ready)
				return;
			ready = true;
			try {
				// ---------------------------------------------------------- general
				SetByTitle("General", "Welcome, Spinner",
					I<Content.Items.Guidebooks.YoyoGuidebookGeneral>(),
					I<Content.Items.Guidebooks.YoyoGuidebookYoyos>(),
					I<Content.Items.Guidebooks.YoyoGuidebookAccessories>());
				SetByTitle("General", "How To Progress",
					I<ModItems.TierEmblems.T0Emblem>(),
					I<ModItems.TierEmblems.T1Emblem>(),
					I<ModItems.TierEmblems.T2Emblem>(),
					I<ModItems.TierEmblems.T3Emblem>(),
					I<ModItems.TierEmblems.T4Emblem>());
				SetByTitle("General", "Tech In One Page",
					I<ModItems.Tech.ThrowableYoyoTech>(),
					I<ModItems.Tech.SwingYoyoTech>(),
					I<ModItems.Tech.MagicalYoyoTech>(),
					I<ModItems.Tech.SummonYoyoTech>());
				SetByTitle("General", "Strings, Bearings, Cases",
					I<ModItems.Accessories.BarbedStrings>(),
					I<ModItems.Accessories.LooseBearingsAL>(),
					I<ModItems.YoyoCaseA>(),
					I<ModItems.YoyoVaultA>());
				SetByTitle("General", "Bosses & Parties",
					I<Content.Items.OldWantedPaper>(),
					I<Content.Items.BearingCaller>(),
					I<Content.Items.TrialOfTheGuardian>());
				SetByTitle("General", "Tips That Save Runs",
					I<ModItems.Utility.StringSplicer>(),
					I<ModItems.Utility.TonicSpool>(),
					I<ModItems.Utility.DowsingString>(),
					I<Content.Items.Index.YoyoIndex>());
				SetByTitle("General", "First Hour, Exact Order",
					I<Content.Items.Guidebooks.YoyoGuidebookGeneral>(),
					I<ModItems.TierEmblems.T0Emblem>(),
					I<ModItems.TierEmblems.T1Emblem>());
				SetByTitle("General", "Controls & Fixes",
					I<ModItems.Utility.YoyoHook>(),
					I<Content.Items.Index.YoyoIndex>(),
					I<ModItems.YoyoOfInsight>(),
					I<Content.Items.TravellersCaller>());
				SetByTitle("General", "If It Is Not Working",
					I<Content.Items.Guidebooks.YoyoGuidebookGeneral>(),
					I<Content.Items.OldWantedPaper>(),
					I<Content.Items.TravellersCaller>(),
					I<Content.Items.BearingCaller>());

				// ----------------------------------------------------------- yoyos
				SetByTitle("Yoyos", "What Counts As A Yoyo",
					I<ModItems.Tech.ThrowableYoyoTech>(),
					I<ModItems.Tech.SwingYoyoTech>(),
					I<ModItems.Tech.MagicalYoyoTech>(),
					I<ModItems.Tech.SummonYoyoTech>());
				SetByTitle("Yoyos", "Boss-Drop Yoyos I",
					I<ModItems.RoyalYoyo>(), I<ModItems.EyeYoyo>(),
					I<ModItems.CorruptionYoyo>(), I<ModItems.CrimsonYoyo>(),
					I<ModItems.BeeYoyo>(), I<ModItems.SkeletronYoyo>(),
					I<ModItems.DeerYoyo>());
				SetByTitle("Yoyos", "Boss-Drop Yoyos II",
					I<ModItems.FleshYoyo>(), I<ModItems.HallowYoyo>(),
					I<ModItems.DestroyerYoyo>(), I<ModItems.TwinsYoyo>(),
					I<ModItems.PrimeYoyo>(), I<ModItems.PlanteraYoyo>(),
					I<ModItems.GolemYoyo>());
				SetByTitle("Yoyos", "Boss-Drop Yoyos III",
					I<ModItems.DukeYoyo>(), I<ModItems.EmpressYoyo>(),
					I<ModItems.LunaticYoyo>(), I<ModItems.LordYoyo>(),
					I<ModItems.GuardianYoyo>(), I<ModItems.CGuardianYoyo>(),
					I<ModItems.SolarYoyo>());
				SetByTitle("Yoyos", "Mini-Boss Yoyos",
					I<ModItems.SpecializedYoyo>(), I<ModItems.HeavyYoyo>(),
					I<ModItems.AncientYoyo>(), I<ModItems.RandomYoyo>(),
					I<ModItems.BlackMarket.GoldYoyo>(), I<ModItems.BlackMarket.LuckyYoyo>(),
					I<ModItems.BlackMarket.IllegalYoyo>(), I<ModItems.BlackMarket.StringersSpecial>(),
					I<ModItems.BlackMarket.SuperGoldYoyo>(), I<ModItems.Ancient.SeleneCoilYoyo>());
				// Ten real event plates, in the order the page names them.
				SetByTitle("Yoyos", "Event Yoyos",
					I<ModItems.DarkMageYoyo>(), I<ModItems.OgreYoyo>(),
					I<ModItems.DutchmanYoyo>(), I<ModItems.MourningWoodYoyo>(),
					I<ModItems.EverscreamYoyo>(), I<ModItems.PumpkingYoyo>(),
					I<ModItems.SantaYoyo>(), I<ModItems.IceQueenYoyo>(),
					I<ModItems.MartianYoyo>(), I<ModItems.BetsyYoyo>());
				SetByTitle("Yoyos", "Early Boss Dossiers",
					I<ModItems.RoyalYoyo>(), I<ModItems.EyeYoyo>(),
					I<ModItems.CorruptionYoyo>(), I<ModItems.CrimsonYoyo>(),
					I<ModItems.BeeYoyo>(), I<ModItems.SkeletronYoyo>(),
					I<ModItems.DeerYoyo>());
				SetByTitle("Yoyos", "Hardmode Dossiers",
					I<ModItems.FleshYoyo>(), I<ModItems.HallowYoyo>(),
					I<ModItems.DestroyerYoyo>(), I<ModItems.TwinsYoyo>(),
					I<ModItems.PrimeYoyo>(), I<ModItems.PlanteraYoyo>(),
					I<ModItems.GolemYoyo>(), I<ModItems.DukeYoyo>(),
					I<ModItems.EmpressYoyo>(), I<ModItems.LunaticYoyo>(),
					I<ModItems.LordYoyo>(), I<ModItems.GuardianYoyo>(),
					I<ModItems.CGuardianYoyo>(), I<ModItems.SolarYoyo>());
				SetByTitle("Yoyos", "Event Dossiers",
					I<ModItems.DarkMageYoyo>(), I<ModItems.OgreYoyo>(),
					I<ModItems.DutchmanYoyo>(), I<ModItems.MourningWoodYoyo>(),
					I<ModItems.EverscreamYoyo>(), I<ModItems.PumpkingYoyo>(),
					I<ModItems.SantaYoyo>(), I<ModItems.IceQueenYoyo>(),
					I<ModItems.MartianYoyo>(), I<ModItems.BetsyYoyo>());
				SetByTitle("Yoyos", "Ancient & Black-Market Dossiers",
					I<ModItems.SpecializedYoyo>(), I<ModItems.HeavyYoyo>(),
					I<ModItems.AncientYoyo>(), I<ModItems.BlackMarket.GoldYoyo>(),
					I<ModItems.BlackMarket.StringersSpecial>(), I<ModItems.BlackMarket.SuperGoldYoyo>(),
					I<ModItems.Ancient.EnchantedYoyo>(), I<ModItems.Ancient.CultistYoyo>(),
					I<ModItems.Ancient.ForbiddenYoyo>(), I<ModItems.Ancient.SeleneCoilYoyo>());
				SetByTitle("Yoyos", "Tech Shell Dossier",
					I<ModItems.Tech.ThrowableYoyoTech>(), I<ModItems.Tech.SwingYoyoTech>(),
					I<ModItems.Tech.MagicalYoyoTech>(), I<ModItems.Tech.SummonYoyoTech>());

				// ------------------------------------------------------ accessories
				SetByTitle("Accessories", "How Accessories Stack",
					I<ModItems.TierEmblems.T0Emblem>(),
					I<ModItems.TierEmblems.T5Emblem>(),
					I<ModItems.TierEmblems.T10Emblem>());
				SetByTitle("Accessories", "Strings",
					I<ModItems.WildStringsAC>(), I<ModItems.WildStringsAPC>(),
					I<ModItems.WildStringsAP>(), I<ModItems.WildStringsAN>(),
					I<ModItems.WildStringsAK>(), I<ModItems.BlackMarket.GoldenString>(),
					I<ModItems.Ancient.AncientString>(), I<ModItems.Accessories.TangledStrings>(),
					I<ModItems.Accessories.BarbedStrings>());
				SetByTitle("Accessories", "Bearings & Axles",
					I<ModItems.Accessories.LooseBearingsAW>(), I<ModItems.Accessories.LooseBearingsAHE>(),
					I<ModItems.Accessories.LooseBearingsAL>(), I<ModItems.Accessories.TensionBearingAP>(),
					I<ModItems.Accessories.TensionBearingACH>(), I<ModItems.Accessories.YoyoAxleUW>(),
					I<ModItems.Accessories.YoyoAxleLW>(), I<ModItems.Accessories.YoyoAxleTW>());
				SetByTitle("Accessories", "Pads, Loops, Cutters",
					I<ModItems.Accessories.ResponsePadsAB>(), I<ModItems.Accessories.ResponsePadsAF>(),
					I<ModItems.Accessories.ResponsePadsACB>(), I<ModItems.Accessories.FingerLoopATW>(),
					I<ModItems.Accessories.FingerLoopAMW>(), I<ModItems.Accessories.StringCutterA>(),
					I<ModItems.Accessories.StringCutterAS>());
				SetByTitle("Accessories", "Cases & Duplicators",
					I<ModItems.YoyoCaseA>(), I<ModItems.YoyoDualCaseA>(),
					I<ModItems.YoyoBriefCaseA>(), I<ModItems.YoyoCollectionCaseA>(),
					I<ModItems.YoyoBackPackA>(), I<ModItems.YoyoRuckSackA>(),
					I<ModItems.YoyoVaultA>(), I<ModItems.Accessories.YoyoDuplicatorAP>());
				SetByTitle("Accessories", "Tricks & Emblems",
					I<ModItems.Accessories.YoyoTrickTS>(), I<ModItems.Accessories.YoyoTrickAWTD>(),
					I<ModItems.Accessories.YoyoTrickAET>(), I<ModItems.Accessories.YoyoTrickATE>(),
					I<ModItems.Accessories.YoyoTrickABB>(), I<ModItems.Accessories.YoyoTrickADNA>(),
					I<ModItems.TierEmblems.T0Emblem>(), I<ModItems.TierEmblems.T5Emblem>(),
					I<ModItems.TierEmblems.T10Emblem>());
				SetByTitle("Accessories", "String & Bearing Dossier",
					I<ModItems.WildStringsAC>(), I<ModItems.BlackMarket.GoldenString>(),
					I<ModItems.Ancient.AncientString>(), I<ModItems.Accessories.TangledStrings>(),
					I<ModItems.Accessories.BarbedStrings>(),
					I<ModItems.Accessories.LooseBearingsAL>(),
					I<ModItems.Accessories.TensionBearingACH>(),
					I<ModItems.Accessories.YoyoAxleTW>());
				SetByTitle("Accessories", "Pad, Loop & Case Dossier",
					I<ModItems.Accessories.ResponsePadsAB>(), I<ModItems.Accessories.ResponsePadsAF>(),
					I<ModItems.Accessories.ResponsePadsACB>(), I<ModItems.Accessories.FingerLoopATW>(),
					I<ModItems.Accessories.StringCutterAS>(), I<ModItems.YoyoCaseA>(),
					I<ModItems.YoyoVaultA>(), I<ModItems.Accessories.YoyoDuplicatorAP>());
				SetByTitle("Accessories", "Trick & Emblem Dossier",
					I<ModItems.Accessories.YoyoTrickTS>(), I<ModItems.Accessories.YoyoTrickAWTD>(),
					I<ModItems.Accessories.YoyoTrickAET>(), I<ModItems.Accessories.YoyoTrickATE>(),
					I<ModItems.Accessories.YoyoTrickABB>(), I<ModItems.Accessories.YoyoTrickADNA>(),
					I<ModItems.TierEmblems.T0Emblem>(), I<ModItems.TierEmblems.T5Emblem>(),
					I<ModItems.TierEmblems.T10Emblem>());

				// ----------------------------------------------------------- others
				SetByTitle("Others", "Town NPCs",
					I<Content.Items.Guidebooks.YoyoGuidebookOthers>(),
					I<ModItems.YoyoOfInsight>(), I<ModItems.YoyoWisdom>());
				SetByTitle("Others", "Night Visitors & Pets",
					I<Content.Items.TravellersCaller>(), I<Content.Items.AncientWantedPaper>(),
					I<ModItems.Consumables.CultistsDraught>(), I<Content.Items.BearingCaller>());
				SetByTitle("Others", "Mods (Tinkerer)",
					I<ModItems.Consumables.BearingOil>(), I<ModItems.Utility.TonicSpool>(),
					I<ModItems.Materials.YoyoScraps>(), I<ModItems.Materials.AdvancedYoyoParts>());
				SetByTitle("Others", "Spells (Ancient)",
					I<ModItems.YoyoOfInsight>(), I<ModItems.Materials.AncientScraps>(),
					I<ModItems.Ancient.AncientYoyoRelic>());
				SetByTitle("Others", "Utility Gadgets",
					I<ModItems.Utility.StringSplicer>(), I<ModItems.Utility.TonicSpool>(),
					I<ModItems.Utility.DowsingString>(), I<ModItems.Utility.YoyoHook>());
				SetByTitle("Others", "Stations & Devices",
					I<Content.Items.Index.YoyoIndex>(), I<ModItems.Utility.StringSplicer>(),
					I<ModItems.Utility.DowsingString>(), I<ModItems.Utility.TonicSpool>());
				SetByTitle("Others", "NPC & Visitor Dossier",
					I<Content.Items.Guidebooks.YoyoGuidebookOthers>(),
					I<Content.Items.OldWantedPaper>(), I<Content.Items.TravellersCaller>(),
					I<Content.Items.AncientWantedPaper>(), I<Content.Items.BearingCaller>());
				SetByTitle("Others", "Mod & Spell Dossier",
					I<ModItems.Consumables.BearingOil>(), I<ModItems.Materials.YoyoScraps>(),
					I<ModItems.Materials.AncientScraps>(), I<ModItems.Ancient.AncientYoyoRelic>());
				SetByTitle("Others", "Station Dossier",
					I<Content.Items.Index.YoyoIndex>(), I<ModItems.Utility.StringSplicer>(),
					I<ModItems.Utility.DowsingString>(), I<ModItems.Utility.TonicSpool>(),
					I<Content.Items.BearingCaller>());

				// ------------------------------------------------------ consumables
				SetByTitle("Consumables", "Tonics (Drink Up)",
					I<ModItems.Consumables.BearingOil>(), I<ModItems.Consumables.CultistsDraught>(),
					I<ModItems.Consumables.StringWax>(), I<ModItems.Consumables.MoonlightFlask>(),
					I<ModItems.Consumables.HoneySpool>());
				SetByTitle("Consumables", "Insight & Wisdom",
					I<ModItems.YoyoOfInsight>(), I<ModItems.YoyoWisdom>());
				SetByTitle("Consumables", "Summon Papers",
					I<Content.Items.OldWantedPaper>(), I<Content.Items.BearingCaller>(),
					I<Content.Items.TravellersCaller>(), I<Content.Items.AncientWantedPaper>(),
					I<Content.Items.TrialOfTheGuardian>());
				SetByTitle("Consumables", "Bags & Bait",
					I<ModItems.LootBags.UndergroundYGBag>(), I<ModItems.LootBags.UnderworldYGBag>(),
					I<Content.NPCs.Critters.GoldBearingItem>(),
					I<Content.NPCs.Critters.GoldenStringsItem>(),
					I<Content.NPCs.Critters.GemBearingItem>(),
					I<Content.NPCs.Critters.AllKnowingWormItem>());
				SetByTitle("Consumables", "Tonic Dossier",
					I<ModItems.Consumables.BearingOil>(), I<ModItems.Consumables.CultistsDraught>(),
					I<ModItems.Consumables.StringWax>(), I<ModItems.Consumables.MoonlightFlask>(),
					I<ModItems.Consumables.HoneySpool>());
				SetByTitle("Consumables", "Paper, Bag & Critter Dossier",
					I<Content.Items.OldWantedPaper>(), I<Content.Items.TrialOfTheGuardian>(),
					I<ModItems.LootBags.UndergroundYGBag>(),
					I<Content.NPCs.Critters.AllKnowingWormItem>());

				// ----------------------------------------------------- blocks/bosses
				SetByTitle("BlocksBosses", "Blocks That Work",
					I<Content.Items.Index.YoyoIndex>(), I<ModItems.Utility.DowsingString>(),
					I<ModItems.Utility.StringSplicer>(), I<Content.Items.BearingCaller>());
				SetByTitle("BlocksBosses", "Cowboys & Bearings",
					I<Content.Items.OldWantedPaper>(), I<ModItems.SpecializedYoyo>(),
					I<ModItems.HeavyYoyo>(), I<Content.Items.BearingCaller>());
				SetByTitle("BlocksBosses", "Duelist & Guardian",
					I<Content.Items.AncientWantedPaper>(), I<Content.Items.TrialOfTheGuardian>(),
					I<ModItems.AncientYoyo>(), I<ModItems.GuardianYoyo>(),
					I<ModItems.CGuardianYoyo>());
				SetByTitle("BlocksBosses", "Vanilla Boss Table",
					I<ModItems.RoyalYoyo>(), I<ModItems.EyeYoyo>(),
					I<ModItems.FleshYoyo>(), I<ModItems.HallowYoyo>(),
					I<ModItems.DestroyerYoyo>(), I<ModItems.PlanteraYoyo>(),
					I<ModItems.GolemYoyo>(), I<ModItems.DukeYoyo>(),
					I<ModItems.EmpressYoyo>(), I<ModItems.LordYoyo>());
				SetByTitle("BlocksBosses", "Miniboss Dossier",
					I<Content.Items.OldWantedPaper>(), I<ModItems.SpecializedYoyo>(),
					I<ModItems.HeavyYoyo>(), I<Content.Items.AncientWantedPaper>(),
					I<ModItems.AncientYoyo>());
				SetByTitle("BlocksBosses", "Guardian Dossier",
					I<Content.Items.TrialOfTheGuardian>(), I<ModItems.GuardianYoyo>(),
					I<ModItems.CGuardianYoyo>(), I<ModItems.Materials.GuardianCoreM>(),
					I<ModItems.Materials.PureEmblemY>());
				SetByTitle("BlocksBosses", "Block Dossier",
					I<Content.Items.Index.YoyoIndex>(), I<ModItems.Utility.DowsingString>(),
					I<ModItems.Utility.StringSplicer>(), I<Content.Items.BearingCaller>());
			}
			catch { }
		}

		private static int I<T>() where T : ModItem
		{
			return ModContent.ItemType<T>();
		}

		private static void SetByTitle(string book, string title, params int[] icons)
		{
			if (!YoyoGuidebookData.Books.TryGetValue(book, out var pages))
				return;
			foreach (GuidePage page in pages) {
				if (page.Title == title) {
					page.Icons = icons;
					return;
				}
			}
		}

		public static Color BookAccent(string bookId)
		{
			return bookId switch {
				"Yoyos" => new Color(255, 150, 90),
				"Accessories" => new Color(150, 230, 150),
				"Others" => new Color(200, 150, 255),
				"Consumables" => new Color(150, 200, 255),
				"BlocksBosses" => new Color(180, 180, 190),
				_ => new Color(200, 160, 90),
			};
		}
	}
}
