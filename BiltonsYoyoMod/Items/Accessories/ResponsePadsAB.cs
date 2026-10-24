using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class ResponsePadsAB : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;

            Item.accessory = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(gold: 2);
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.GetModPlayer<ResponsePadsPlayer>()
                .ResponsePadsAB = true;
        }
    }
}