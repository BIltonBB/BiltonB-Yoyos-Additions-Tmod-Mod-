using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class LooseBearingsAMI : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;

            Item.accessory = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 3);
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            LooseBearingLoadoutPlayer loadout =
                player.GetModPlayer<
                    LooseBearingLoadoutPlayer>();

            loadout.meteorite = true;

            player.GetModPlayer<
                LooseBearingPlayer>()
                .looseBearingsEquipped = true;
        }
    }
}