using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Materials
{
    public class PureEmblemY : ModItem
    {
        public override string Texture => "BiltonsYoyoMod/Items/Materials/PureEmblemY";

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 5;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 2);
        }

        public override void AddRecipes()
        {
            // Refine guardian cores into a pure emblem, post-Plantera.
            CreateRecipe()
                .AddIngredient<GuardianCoreM>(3)
                .AddTile(TileID.TinkerersWorkbench)
                .AddCondition(Condition.DownedPlantera)
                .Register();
        }
    }
}