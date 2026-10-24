using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class TensionBearingAL : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;

            Item.accessory = true;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.buyPrice(gold: 4);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<TensionBearingALPlayer>().TensionBearingAL = true;
        }
    }
}