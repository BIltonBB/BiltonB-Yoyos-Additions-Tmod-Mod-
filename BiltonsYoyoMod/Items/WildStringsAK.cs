using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items
{
    public class WildStringsAK : ModItem
    {
        public override string Texture =>
            "BiltonsYoyoMod/Items/WildStringsAK";

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;

            Item.accessory = true;

            Item.value =
                Item.sellPrice(
                    gold: 1
                );

            Item.rare =
                ItemRarityID.Pink;
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.GetModPlayer<WildStringsPlayer>()
                .Kevlar = true;
        }
    }
}