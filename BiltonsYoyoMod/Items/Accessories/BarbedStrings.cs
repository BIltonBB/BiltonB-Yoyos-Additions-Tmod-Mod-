using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class BarbedStrings : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;

            Item.accessory = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 4);

            Item.stringColor = global::BiltonsYoyoMod.Content.NPCs.Town.YoyoTownShared.BlackStringShader;
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.yoyoString = true;
            player.stringColor = Item.stringColor;

            player.GetModPlayer<RevampPlayer>().Barbed = true;

            player.GetArmorPenetration(
                DamageClass.MeleeNoSpeed
            ) += 5f;
        }

        public override void AddRecipes()
        {
            // Was completely unobtainable: stronger Tangled Strings for
            // post-early-Hardmode - Hardmode souls are the tier proof.
            CreateRecipe()
                .AddIngredient(ItemID.Cobweb, 40)
                .AddIngredient<Materials.YoyoParts>(10)
                .AddIngredient(ItemID.SoulofNight, 5)
                .AddIngredient(ItemID.SoulofLight, 5)
                .AddTile(TileID.Loom)
                .AddCondition(Condition.Hardmode)
                .Register();
        }
    }
}