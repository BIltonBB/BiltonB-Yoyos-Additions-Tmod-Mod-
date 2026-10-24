using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items
{
    public class YoyoAdrenalineA : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(gold: 6);
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.GetModPlayer<YoyoAdrenalineAPlayer>().enabled = true;
        }

        public override void AddRecipes()
        {
            // Alternative to the 25% Queen Slime drop: slime + light souls
            // for early Hardmode.
            CreateRecipe()
                .AddIngredient(ItemID.Gel, 50)
                .AddIngredient(ItemID.SoulofLight, 8)
                .AddTile(TileID.MythrilAnvil)
                .AddCondition(Condition.Hardmode)
                .Register();
        }
    }
}