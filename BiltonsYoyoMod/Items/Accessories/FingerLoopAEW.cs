using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class FingerLoopAEW : ModItem
    {
        public override string Texture =>
            "BiltonsYoyoMod/Items/Accessories/FingerLoopAEW";

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;

            Item.accessory = true;
            Item.rare = ItemRarityID.LightPurple;
            Item.value = Item.buyPrice(gold: 3);
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.GetModPlayer<FingerLoopPlayer>()
                .ExpertlyWrapped = true;
        }

        // Existing recipe intentionally omitted.
    }
}