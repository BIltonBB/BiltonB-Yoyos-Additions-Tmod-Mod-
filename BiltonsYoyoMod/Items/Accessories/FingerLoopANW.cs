using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class FingerLoopANW : ModItem
    {
        public override string Texture =>
            "BiltonsYoyoMod/Items/Accessories/FingerLoopANW";

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;

            Item.accessory = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 1);
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.GetModPlayer<FingerLoopPlayer>()
                .NicelyWrapped = true;
        }

        // Existing recipe intentionally omitted.
    }
}