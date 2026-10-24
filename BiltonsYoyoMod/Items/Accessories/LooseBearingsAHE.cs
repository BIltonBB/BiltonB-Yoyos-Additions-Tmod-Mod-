using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class LooseBearingsAHE : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(silver: 75);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<LooseBearingPlayer>().looseBearingsEquipped = true;
            player.GetModPlayer<LooseBearingLoadoutPlayer>().hellstone = true;
        }
    }
}
