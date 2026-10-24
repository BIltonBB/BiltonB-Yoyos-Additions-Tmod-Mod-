using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class ResponsePadsAS : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;

            Item.accessory = true;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 7);
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.GetModPlayer<ResponsePadsPlayer>()
                .ResponsePadsAS = true;
        }
    }
}