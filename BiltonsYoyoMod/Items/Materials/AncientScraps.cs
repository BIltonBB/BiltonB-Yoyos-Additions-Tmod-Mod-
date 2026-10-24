using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Materials
{
    public class AncientScraps : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(silver: 50);
        }

        public override void AddRecipes()
        {
            // Imbue ordinary parts with fallen-star magic, post-Skeletron.
            CreateRecipe(2)
                .AddIngredient<YoyoParts>(3)
                .AddIngredient(ItemID.FallenStar, 1)
                .AddTile(TileID.Anvils)
                .AddCondition(Condition.DownedSkeletron)
                .Register();
        }
    }
}
