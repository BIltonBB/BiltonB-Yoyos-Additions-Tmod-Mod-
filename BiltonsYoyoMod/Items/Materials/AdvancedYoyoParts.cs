using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Materials
{
    public class AdvancedYoyoParts : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(silver: 10);
        }

        public override void AddRecipes()
        {
            // Machine ordinary parts up to Hardmode spec.
            CreateRecipe()
                .AddIngredient<YoyoParts>(5)
                .AddRecipeGroup("BiltonsYoyoMod:AnyMythrilBar", 5)
                .AddTile(TileID.MythrilAnvil)
                .AddCondition(Condition.Hardmode)
                .Register();
        }
    }
}
