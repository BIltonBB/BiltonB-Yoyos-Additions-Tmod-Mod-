using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class GoldenYoyoSubEmblem : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.buyPrice(gold: 5);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetDamage(DamageClass.MeleeNoSpeed) += 0.10f; // 10% yoyo damage
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<YoyoSubEmblem>())
                .AddIngredient(ItemID.SoulofMight)
                .AddIngredient(ItemID.SoulofSight)
                .AddIngredient(ItemID.SoulofFright)
                .AddIngredient(ItemID.GoldBar, 10)
                .AddIngredient(ModContent.ItemType<Materials.AdvancedYoyoParts>(), 5)
                .AddTile(TileID.AdamantiteForge)
                .Register();
        }
    }
}