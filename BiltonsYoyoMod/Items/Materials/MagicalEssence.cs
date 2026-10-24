using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Materials
{
    public class MagicalEssence : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(gold: 1);
        }

        public override void AddRecipes()
        {
            // Bottle starlight and light souls into essence, Hardmode.
            CreateRecipe(2)
                .AddIngredient(ItemID.FallenStar, 5)
                .AddIngredient(ItemID.SoulofLight, 3)
                .AddTile(TileID.Bottles)
                .AddCondition(Condition.Hardmode)
                .Register();
        }
    }
}
