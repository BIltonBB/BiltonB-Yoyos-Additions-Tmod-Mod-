using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Materials
{
    public class GuardianCoreM : ModItem
    {
        // Item PNG lives at the mod root (BiltonsYoyoMod/Items/Materials/GuardianCoreM.png).
        public override string Texture => "BiltonsYoyoMod/Items/Materials/GuardianCoreM";

        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(silver: 25);
        }

        public override void AddRecipes()
        {
            // Bind dungeon spirits into a guardian-grade core, post-Plantera.
            CreateRecipe()
                .AddIngredient(ItemID.Ectoplasm, 3)
                .AddTile(TileID.MythrilAnvil)
                .AddCondition(Condition.DownedPlantera)
                .Register();
        }
    }
}
