using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Systems
{
    public class AccessoryRecipes : ModSystem
    {
        public override void AddRecipeGroups()
        {
            RecipeGroup.RegisterGroup(
                "BiltonsYoyoMod:AnyIronBar",
                new RecipeGroup(() => Lang.GetItemNameValue(ItemID.IronBar),
                    ItemID.IronBar, ItemID.LeadBar));
        }

        public override void AddRecipes()
        {
            // YOYO TRICKS (Hi / Bookcases)
            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoTrickTS>())
                .AddIngredient(ItemID.Cobweb, 20)
                .AddIngredient(ItemID.Silk, 5)
                .AddIngredient(ItemID.Book, 1)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T0Emblem>(), 1)
                .AddTile(TileID.Bookcases)
                .Register();
            
            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoTrickAWTD>())
                .AddIngredient(ItemID.Cobweb, 20)
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddIngredient(ItemID.Silk, 5)
                .AddIngredient(ItemID.Book, 2)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T1Emblem>(), 1)
                .AddTile(TileID.Bookcases)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoTrickAET>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyEvilMaterial", 10)
                .AddIngredient(ItemID.Silk, 5)
                .AddIngredient(ItemID.Book, 3)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T3Emblem>(), 1)
                .AddTile(TileID.Bookcases)
                .Register();
            
            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoTrickATE>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyEvilBar", 20)
                .AddIngredient(ItemID.Silk, 5)
                .AddIngredient(ItemID.Book, 4)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T5Emblem>(), 1)
                .AddTile(TileID.Bookcases)
                .Register();
            
            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoTrickABB>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyHardmetalBar", 20)
                .AddIngredient(ItemID.Silk, 5)
                .AddIngredient(ItemID.ChlorophyteBar, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T8Emblem>(), 1)
                .AddTile(TileID.Bookcases)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoTrickADNA>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyCelestialFragment", 5)
                .AddIngredient(ItemID.BeetleHusk, 10)
                .AddIngredient(ItemID.ChlorophyteBar, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T9Emblem>(), 1)
                .AddTile(TileID.Bookcases)
                .Register();

            // WILD STRINGS (Loom)
            Recipe.Create(ModContent.ItemType<Items.WildStringsAC>())
                .AddIngredient(ItemID.Cobweb, 20)
                .AddIngredient(ItemID.Silk, 5)
                .AddIngredient(ItemID.WhiteString)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T0Emblem>(), 1)
                .AddTile(TileID.Loom)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.WildStringsAPC>())
                .AddIngredient(ItemID.Silk, 15)
                .AddIngredient(ItemID.Cobweb, 10)
                .AddRecipeGroup("BiltonsYoyoMod:AnyEvilMaterial", 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T2Emblem>(), 1)
                .AddTile(TileID.Loom)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.WildStringsAP>())
                .AddIngredient(ItemID.Silk, 20)
                .AddIngredient(ItemID.Cobweb, 10)
                .AddIngredient(ItemID.HellstoneBar, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T5Emblem>(), 1)
                .AddTile(TileID.Loom)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.WildStringsAN>())
                .AddIngredient(ItemID.Silk, 20)
                .AddIngredient(ItemID.Cobweb, 10)
                .AddIngredient(ItemID.ChlorophyteBar, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T7Emblem>(), 1)
                .AddTile(TileID.Loom)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.WildStringsAK>())
                .AddIngredient(ItemID.Silk, 20)
                .AddIngredient(ItemID.Cobweb, 10)
                .AddRecipeGroup("BiltonsYoyoMod:AnyCelestialFragment", 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T9Emblem>(), 1)
                .AddTile(TileID.Loom)
                .Register();

            // LOOSE BEARINGS (Anvil / Hellforge / Mech Anvil / AM)
            Recipe.Create(ModContent.ItemType<Items.Accessories.LooseBearingsAW>())
                .AddIngredient(ItemID.Wood, 20)
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T1Emblem>(), 1)
                .AddTile(TileID.Anvils)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.LooseBearingsAMI>())
                .AddIngredient(ItemID.MeteoriteBar, 15)
                .AddIngredient(ItemID.FallenStar, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T3Emblem>(), 1)
                .AddTile(TileID.Anvils)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.LooseBearingsAHE>())
                .AddIngredient(ItemID.HellstoneBar, 15)
                .AddIngredient(ItemID.Obsidian, 10)
                .AddIngredient(ModContent.ItemType<Items.Materials.YoyoParts>(), 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T4Emblem>(), 1)
                .AddTile(TileID.Hellforge)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.LooseBearingsAM>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyMythrilBar", 15)
                .AddIngredient(ItemID.SoulofMight, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T6Emblem>(), 1)
                .AddTile(TileID.MythrilAnvil)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.LooseBearingsAHA>())
                .AddIngredient(ItemID.HallowedBar, 15)
                .AddIngredient(ItemID.CrystalShard, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T6Emblem>(), 1)
                .AddTile(TileID.MythrilAnvil)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.LooseBearingsACH>())
                .AddIngredient(ItemID.ChlorophyteBar, 15)
                .AddIngredient(ItemID.BeetleHusk, 5)
                .AddIngredient(ModContent.ItemType<Items.Materials.AdvancedYoyoParts>(), 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T8Emblem>(), 1)
                .AddTile(TileID.AdamantiteForge)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.LooseBearingsAL>())
                .AddIngredient(ItemID.LunarBar, 15)
                .AddRecipeGroup("BiltonsYoyoMod:AnyCelestialFragment", 5)
                .AddIngredient(ModContent.ItemType<Items.Materials.GuardianCoreM>(), 5)
                .AddIngredient(ModContent.ItemType<Items.Materials.PureEmblemY>(), 1)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T10Emblem>(), 1)
                .AddTile(TileID.LunarCraftingStation)
                .Register();

            // FINGER LOOPS (Loom)
            Recipe.Create(ModContent.ItemType<Items.Accessories.FingerLoopATW>())
                .AddIngredient(ItemID.Cobweb, 15)
                .AddIngredient(ItemID.Silk, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T1Emblem>(), 1)
                .AddTile(TileID.Loom)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.FingerLoopANW>())
                .AddIngredient(ItemID.Silk, 15)
                .AddIngredient(ItemID.BeeWax, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T3Emblem>(), 1)
                .AddTile(TileID.Loom)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.FingerLoopAEW>())
                .AddIngredient(ItemID.Silk, 20)
                .AddIngredient(ItemID.SoulofSight, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T6Emblem>(), 1)
                .AddTile(TileID.Loom)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.FingerLoopAMW>())
                .AddIngredient(ItemID.Silk, 20)
                .AddRecipeGroup("BiltonsYoyoMod:AnyCelestialFragment", 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T9Emblem>(), 1)
                .AddTile(TileID.Loom)
                .Register();

            // YOYO AXLES
            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoAxleUW>())
                .AddIngredient(ItemID.Wood, 10)
                .AddIngredient(ItemID.Cobweb, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T1Emblem>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoAxleOW>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddIngredient(ItemID.Silk, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T3Emblem>(), 1)
                .AddTile(TileID.Anvils)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoAxleLW>())
                .AddIngredient(ItemID.HellstoneBar, 10)
                .AddIngredient(ItemID.Silk, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T5Emblem>(), 1)
                .AddTile(TileID.Hellforge)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoAxleTW>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyMythrilBar", 10)
                .AddIngredient(ItemID.Silk, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T6Emblem>(), 1)
                .AddTile(TileID.MythrilAnvil)
                .Register();

            // RESPONSE PADS
            Recipe.Create(ModContent.ItemType<Items.Accessories.ResponsePadsAB>())
                .AddIngredient(ItemID.Silk, 15)
                .AddIngredient(ItemID.BeeWax, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T3Emblem>(), 1)
                .AddTile(TileID.Loom)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.ResponsePadsAF>())
                .AddIngredient(ItemID.Silk, 15)
                .AddIngredient(ItemID.HellstoneBar, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T5Emblem>(), 1)
                .AddTile(TileID.Hellforge)
                .Register();
            
            Recipe.Create(ModContent.ItemType<Items.Accessories.ResponsePadsAS>())
                .AddIngredient(ItemID.Silk, 5)
                .AddIngredient(ItemID.Cobweb, 5)
                .AddRecipeGroup("BiltonsYoyoMod:AnyEvilMaterial", 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T4Emblem>(), 1)
                .AddTile(TileID.Hellforge)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.ResponsePadsAR>())
                .AddIngredient(ItemID.Silk, 15)
                .AddIngredient(ItemID.ChlorophyteBar, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T7Emblem>(), 1)
                .AddTile(TileID.AdamantiteForge)
                .Register();
            
            Recipe.Create(ModContent.ItemType<Items.Accessories.ResponsePadsACB>())
                .AddIngredient(ItemID.Silk, 30)
                .AddIngredient(ItemID.ChlorophyteBar, 10)
                .AddIngredient(ItemID.TurtleShell, 5)
                .AddIngredient(ItemID.BeetleHusk, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T9Emblem>(), 1)
                .AddTile(TileID.AdamantiteForge)
                .Register();

            // TENSION BEARINGS
            Recipe.Create(ModContent.ItemType<Items.Accessories.TensionBearingAP>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddIngredient(ItemID.Wire, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T4Emblem>(), 1)
                .AddTile(TileID.Anvils)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.TensionBearingAD>())
                .AddIngredient(ItemID.HellstoneBar, 15)
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T5Emblem>(), 1)
                .AddTile(TileID.Hellforge)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.TensionBearingAL>())
                .AddIngredient(ItemID.HellstoneBar, 10)
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddIngredient(ItemID.BottledWater, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T5Emblem>(), 1)
                .AddTile(TileID.Hellforge)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.TensionBearingAHPSS>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyHardmetalBar", 15)
                .AddIngredient(ItemID.Wire, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T7Emblem>(), 1)
                .AddTile(TileID.AdamantiteForge)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.TensionBearingACH>())
                .AddIngredient(ItemID.ChlorophyteBar, 10)
                .AddIngredient(ItemID.CrystalShard, 10)
                .AddRecipeGroup("BiltonsYoyoMod:AnyCelestialFragment", 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T9Emblem>(), 1)
                .AddTile(TileID.LunarCraftingStation)
                .Register();

            // STRING CUTTERS
            Recipe.Create(ModContent.ItemType<Items.Accessories.StringCutterA>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddIngredient(ItemID.Cobweb, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T4Emblem>(), 1)
                .AddTile(TileID.Anvils)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.StringCutterAH>())
                .AddIngredient(ItemID.HellstoneBar, 15)
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T5Emblem>(), 1)
                .AddTile(TileID.Hellforge)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.StringCutterAS>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyHardmetalBar", 10)
                .AddIngredient(ItemID.Wire, 10)
                .AddIngredient(ItemID.HellstoneBar, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T7Emblem>(), 1)
                .AddTile(TileID.AdamantiteForge)
                .Register();

            // YOYO CASES
            Recipe.Create(ModContent.ItemType<Items.YoyoCaseA>())
                .AddIngredient(ItemID.Wood, 20)
                .AddIngredient(ItemID.Silk, 10)
                .AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T0Emblem>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.YoyoDualCaseA>())
                .AddIngredient(ModContent.ItemType<Items.YoyoCaseA>(), 1)
                .AddIngredient(ItemID.Wood, 30)
                .AddIngredient(ItemID.Silk, 15)
                .AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 8)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T1Emblem>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.YoyoBriefCaseA>())
                .AddIngredient(ItemID.Wood, 20)
                .AddIngredient(ItemID.Silk, 20)
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T2Emblem>(), 1)
                .AddTile(TileID.WorkBenches)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.YoyoCollectionCaseA>())
                .AddIngredient(ModContent.ItemType<Items.YoyoDualCaseA>(), 1)
                .AddIngredient(ItemID.Silk, 20)
                .AddIngredient(ItemID.Bone, 15)
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddIngredient(ModContent.ItemType<Items.Materials.YoyoParts>(), 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T4Emblem>(), 1)
                .AddTile(TileID.Anvils)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.YoyoBackPackA>())
                .AddIngredient(ModContent.ItemType<Items.YoyoBriefCaseA>(), 1)
                .AddIngredient(ItemID.Silk, 25)
                .AddIngredient(ItemID.Leather, 10)
                .AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 10)
                .AddRecipeGroup("BiltonsYoyoMod:AnyEvilMaterial", 20)
                .AddIngredient(ItemID.HellstoneBar, 10)
                .AddIngredient(ModContent.ItemType<Items.Materials.YoyoParts>(), 8)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T4Emblem>(), 1)
                .AddTile(TileID.Anvils)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.YoyoRuckSackA>())
                .AddIngredient(ModContent.ItemType<Items.YoyoBackPackA>(), 1)
                .AddIngredient(ItemID.Silk, 30)
                .AddRecipeGroup("BiltonsYoyoMod:AnyMythrilBar", 10)
                .AddIngredient(ItemID.Leather, 10)
                .AddIngredient(ModContent.ItemType<Items.Materials.AdvancedYoyoParts>(), 4)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T6Emblem>(), 1)
                .AddTile(TileID.MythrilAnvil)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.YoyoVaultA>())
                .AddIngredient(ModContent.ItemType<Items.YoyoRuckSackA>(), 1)
                .AddIngredient(ItemID.ChlorophyteBar, 20)
                .AddIngredient(ItemID.BeetleHusk, 10)
                .AddIngredient(ItemID.Silk, 20)
                .AddIngredient(ModContent.ItemType<Items.Materials.AdvancedYoyoParts>(), 6)
                .AddIngredient(ModContent.ItemType<Items.Materials.PureEmblemY>(), 1)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T8Emblem>(), 1)
                .AddTile(TileID.AdamantiteForge)
                .Register();

            // YOYO DUPLICATORS
            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoDuplicatorA>())
                .AddIngredient(ItemID.HellstoneBar, 15)
                .AddIngredient(ItemID.Wire, 10)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T5Emblem>(), 1)
                .AddTile(TileID.Hellforge)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoDuplicatorAA>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyHardmetalBar", 15)
                .AddIngredient(ItemID.Wire, 15)
                .AddIngredient(ItemID.SoulofMight, 5)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T7Emblem>(), 1)
                .AddTile(TileID.AdamantiteForge)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoDuplicatorAHD>())
                .AddIngredient(ItemID.ChlorophyteBar, 15)
                .AddIngredient(ItemID.BeetleHusk, 10)
                .AddIngredient(ItemID.Wire, 15)
                .AddIngredient(ModContent.ItemType<Items.Materials.GuardianCoreM>(), 3)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T9Emblem>(), 1)
                .AddTile(TileID.LunarCraftingStation)
                .Register();

            Recipe.Create(ModContent.ItemType<Items.Accessories.YoyoDuplicatorAP>())
                .AddIngredient(ItemID.LunarBar, 20)
                .AddIngredient(ItemID.Wire, 20)
                .AddRecipeGroup("BiltonsYoyoMod:AnyCelestialFragment", 5)
                .AddIngredient(ModContent.ItemType<Items.Materials.GuardianCoreM>(), 8)
                .AddIngredient(ModContent.ItemType<Items.Materials.PureEmblemY>(), 1)
                .AddIngredient(ModContent.ItemType<Items.TierEmblems.T10Emblem>(), 1)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }
}