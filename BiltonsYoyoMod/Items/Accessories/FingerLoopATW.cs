using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class FingerLoopATW : ModItem
    {
        public override string Texture =>
            "BiltonsYoyoMod/Items/Accessories/FingerLoopATW";

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;

            Item.accessory = true;
            Item.rare = ItemRarityID.White;
            Item.value = Item.buyPrice(silver: 50);
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.GetModPlayer<FingerLoopPlayer>()
                .TerriblyWrapped = true;
        }

        // Existing recipe intentionally omitted.
    }
}