using BiltonsYoyoMod.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Systems
{
    public class LordYoyoAltRecipe : ModSystem
    {
        public override void AddRecipes()
        {
            Recipe.Create(ModContent.ItemType<LordYoyo>())
                // Pre-Hardmode yoyos
                .AddIngredient(ModContent.ItemType<RoyalYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<EyeYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<DeerYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<SkeletronYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<BeeYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<CrimsonYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<CorruptionYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<FleshYoyo>(), 1)
                // Hardmode yoyos
                .AddIngredient(ModContent.ItemType<HallowYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<DestroyerYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<TwinsYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<PrimeYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<PlanteraYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<GolemYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<DukeYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<EmpressYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<LunaticYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<SolarYoyo>(), 1)
                .AddIngredient(ModContent.ItemType<Items.Materials.GuardianCoreM>(), 10)
                .AddIngredient(ModContent.ItemType<Items.Materials.PureEmblemY>(), 2)
                .AddCondition(Condition.DownedMoonLord)
                .AddTile(TileID.LunarCraftingStation) // Ancient Manipulator
                .Register();
        }
    }
}