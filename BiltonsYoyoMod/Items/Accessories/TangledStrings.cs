using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class TangledStrings : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;

            Item.accessory = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(gold: 1);

            Item.stringColor = global::BiltonsYoyoMod.Content.NPCs.Town.YoyoTownShared.SilverStringShader;
        }

        public override void UpdateAccessory(
            Player player,
            bool hideVisual)
        {
            player.yoyoString = true;
            player.stringColor = Item.stringColor;

            player.GetModPlayer<RevampPlayer>().Tangled = true;
        }

        public override void AddRecipes()
        {
            // Early alternative to the 50% Cowboy drop: evil-boss proof
            // (ShadowScale/TissueSample) plus desert rags and scraps.
            // Gated post-evil-boss (pre-Hardmode String tier).
            CreateRecipe()
                .AddIngredient(ItemID.Cobweb, 30)
                .AddIngredient<Materials.YoyoScraps>(10)
                .AddRecipeGroup("BiltonsYoyoMod:AnyEvilMaterial", 5)
                .AddTile(TileID.Loom)
                .AddCondition(Systems.TierEmblemRecipes.DownedEvilBoss)
                .Register();
        }
    }
}