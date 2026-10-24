using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items
{
    public class WildStringsAN : ModItem
    {
        public override string Texture =>
            "BiltonsYoyoMod/Items/WildStringsAN";

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;

            Item.accessory = true;

            Item.value =
                Item.sellPrice(
                    silver: 75
                );

            Item.rare =
                ItemRarityID.LightPurple;
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.GetModPlayer<WildStringsPlayer>()
                .Nylon = true;
        }
    }
}