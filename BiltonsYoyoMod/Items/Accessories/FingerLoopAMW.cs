using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class FingerLoopAMW : ModItem
    {
        public override string Texture =>
            "BiltonsYoyoMod/Items/Accessories/FingerLoopAMW";

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;

            Item.accessory = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(gold: 8);
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.GetModPlayer<FingerLoopPlayer>()
                .MasterfullyWrapped = true;
        }

        // Existing recipe intentionally omitted.
    }
}