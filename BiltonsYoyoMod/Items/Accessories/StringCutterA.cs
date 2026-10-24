using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class StringCutterA : ModItem
    {
        public override string Texture =>
            "BiltonsYoyoMod/Items/Accessories/StringCutterA";

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;

            Item.accessory = true;
            Item.rare = ItemRarityID.White;
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            StringCutterPlayer modPlayer =
                player.GetModPlayer<StringCutterPlayer>();

            modPlayer.StringCutterEquipped = true;
            modPlayer.StringCutterType =
                StringCutterType.Standard;
        }
    }

    public enum StringCutterType
    {
        Standard,
        Serrated,
        Heated
    }
}