using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Materials
{
    public class YoyoParts : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.White;
            Item.value = Item.buyPrice(silver: 1);
        }

        public override void AddRecipes()
        {
            // Salvage whole parts out of scrap.
            CreateRecipe()
                .AddIngredient<YoyoScraps>(10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
