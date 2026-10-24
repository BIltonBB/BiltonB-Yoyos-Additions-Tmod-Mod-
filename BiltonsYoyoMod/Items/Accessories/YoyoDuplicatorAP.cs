using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class YoyoDuplicatorAP : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.buyPrice(gold: 12);
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            YoyoDuplicatorPlayer duplicator =
                player.GetModPlayer<YoyoDuplicatorPlayer>();

            if (duplicator.DuplicatorLevel < 4)
                duplicator.DuplicatorLevel = 4;
        }
    }
}