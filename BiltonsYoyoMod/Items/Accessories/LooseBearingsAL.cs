using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class LooseBearingsAL : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.buyPrice(gold: 5);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<LooseBearingPlayer>().looseBearingsEquipped = true;
            player.GetModPlayer<LooseBearingLoadoutPlayer>().luminite = true;
        }
    }
}
