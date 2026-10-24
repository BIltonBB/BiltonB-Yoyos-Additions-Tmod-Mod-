using BiltonsYoyoMod.Items.TierEmblems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;

namespace BiltonsYoyoMod.Systems
{
    public class TierEmblemRecipes : ModSystem
    {
        // "Either evil boss defeated" — NPC.downedBoss2 is true for BoC OR EoW
        public static readonly Condition DownedEvilBoss = new Condition(
            "Mods.BiltonsYoyoMod.Conditions.DownedEvilBoss",
            () => NPC.downedBoss2);

        // "At least 2 mechanical bosses defeated"
        public static readonly Condition DownedTwoMechBosses = new Condition(
            "Mods.BiltonsYoyoMod.Conditions.DownedTwoMechBosses",
            () => (NPC.downedMechBoss1 ? 1 : 0) + (NPC.downedMechBoss2 ? 1 : 0) + (NPC.downedMechBoss3 ? 1 : 0) >= 2);

        public override void AddRecipes()
        {
            // T0
            Recipe.Create(ModContent.ItemType<T0Emblem>())
                .AddIngredient(ItemID.Gel, 10)
                .AddIngredient(ModContent.ItemType<TierEmblemMold>(), 1)
                .AddTile(TileID.WorkBenches)
                .AddCondition(Condition.DownedKingSlime)
                .Register();

            // T1
            Recipe.Create(ModContent.ItemType<T1Emblem>())
                .AddIngredient(ModContent.ItemType<T0Emblem>(), 1)
                .AddRecipeGroup("BiltonsYoyoMod:AnyEvilBar", 10)
                .AddIngredient(ModContent.ItemType<TierEmblemMold>(), 1)
                .AddTile(TileID.WorkBenches)
                .AddCondition(Condition.DownedEyeOfCthulhu)
                .Register();

            // T2
            Recipe.Create(ModContent.ItemType<T2Emblem>())
                .AddIngredient(ModContent.ItemType<T1Emblem>(), 1)
                .AddRecipeGroup("BiltonsYoyoMod:AnyEvilMaterial", 10)
                .AddIngredient(ModContent.ItemType<TierEmblemMold>(), 1)
                .AddTile(TileID.WorkBenches)
                .AddCondition(DownedEvilBoss) // BoC or EoW
                .Register();

            // T3
            Recipe.Create(ModContent.ItemType<T3Emblem>())
                .AddIngredient(ModContent.ItemType<T2Emblem>(), 1)
                .AddIngredient(ItemID.BeeWax, 15)
                .AddIngredient(ModContent.ItemType<TierEmblemMold>(), 1)
                .AddTile(TileID.Anvils)
                .AddCondition(Condition.DownedQueenBee)
                .Register();

            // T4
            Recipe.Create(ModContent.ItemType<T4Emblem>())
                .AddIngredient(ModContent.ItemType<T3Emblem>(), 1)
                .AddIngredient(ItemID.Bone, 30)
                .AddIngredient(ModContent.ItemType<TierEmblemMold>(), 1)
                .AddTile(TileID.Anvils)
                .AddCondition(Condition.DownedSkeletron)
                .Register();

            // T5
            Recipe.Create(ModContent.ItemType<T5Emblem>())
                .AddIngredient(ModContent.ItemType<T4Emblem>(), 1)
                .AddIngredient(ItemID.HellstoneBar, 15)
                .AddIngredient(ModContent.ItemType<TierEmblemMold>(), 1)
                .AddTile(TileID.Hellforge)
                .AddCondition(Condition.Hardmode)
                .Register();

            // T6
            Recipe.Create(ModContent.ItemType<T6Emblem>())
                .AddIngredient(ModContent.ItemType<T5Emblem>(), 1)
                .AddIngredient(ItemID.SoulofMight, 5)
                .AddIngredient(ItemID.SoulofSight, 5)
                .AddIngredient(ItemID.SoulofFright, 5)
                .AddIngredient(ModContent.ItemType<TierEmblemMold>(), 1)
                .AddTile(TileID.MythrilAnvil)
                .AddCondition(DownedTwoMechBosses)
                .Register();

            // T7 ---- (auto alt-ore via recipe groups)
            Recipe.Create(ModContent.ItemType<T7Emblem>())
                .AddIngredient(ModContent.ItemType<T6Emblem>(), 1)
                .AddRecipeGroup("BiltonsYoyoMod:AnyHardmetalBar", 12)
                .AddRecipeGroup("BiltonsYoyoMod:AnyMythrilBar", 12)
                .AddRecipeGroup("BiltonsYoyoMod:AnyPalladiumBar", 12)
                .AddIngredient(ModContent.ItemType<TierEmblemMold>(), 1)
                .AddTile(TileID.AdamantiteForge)
                .AddCondition(Condition.DownedPlantera)
                .Register();

            // T8
            Recipe.Create(ModContent.ItemType<T8Emblem>())
                .AddIngredient(ItemID.ChlorophyteBar, 20)
                .AddIngredient(ItemID.BeetleHusk, 5)
                .AddIngredient(ItemID.LifeFruit, 1)
                .AddIngredient(ModContent.ItemType<Items.Materials.AdvancedYoyoParts>(), 5)
                .AddIngredient(ModContent.ItemType<AdvancedTierEmblemMold>(), 1)
                .AddTile(TileID.AdamantiteForge)
                .AddCondition(Condition.DownedGolem)
                .Register();

            // T9
            Recipe.Create(ModContent.ItemType<T9Emblem>())
                .AddRecipeGroup("BiltonsYoyoMod:AnyCelestialFragment", 5)
                .AddIngredient(ModContent.ItemType<Items.Materials.GuardianCoreM>(), 5)
                .AddIngredient(ModContent.ItemType<AdvancedTierEmblemMold>(), 1)
                .AddTile(TileID.LunarCraftingStation) // Ancient Manipulator
                .AddCondition(Condition.DownedCultist)
                .Register();

            // T10
            Recipe.Create(ModContent.ItemType<T10Emblem>())
                .AddIngredient(ItemID.LunarBar, 20)
                .AddIngredient(ModContent.ItemType<Items.Materials.PureEmblemY>(), 1)
                .AddIngredient(ModContent.ItemType<AdvancedTierEmblemMold>(), 1)
                .AddTile(TileID.LunarCraftingStation)
                .AddCondition(Condition.DownedMoonLord)
                .Register();
        }
    }
}