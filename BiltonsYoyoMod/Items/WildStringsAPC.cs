using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items
{
    public class WildStringsAPC : ModItem
    {
        public override string Texture => "BiltonsYoyoMod/Items/WildStringsAPC";

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.accessory = true;
            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.LightRed;
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<WildStringsPlayer>().PolyCotton = true;
        }
    }
}
