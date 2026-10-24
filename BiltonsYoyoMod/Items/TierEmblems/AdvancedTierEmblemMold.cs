using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.TierEmblems
{
    public class AdvancedTierEmblemMold : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.buyPrice(0, 10, 0, 0);
        }

        public override void AddRecipes()
        {
            Recipe.Create(Type)
                .AddIngredient(ItemID.ChlorophyteBar, 3)
                .AddIngredient(ItemID.HallowedBar, 3)
                .AddIngredient(ItemID.SoulofMight, 3)
                .AddIngredient(ItemID.SoulofSight, 3)
                .AddIngredient(ItemID.SoulofFright, 3)
                .AddTile(TileID.AdamantiteForge)
                .Register();
        }
    }
}