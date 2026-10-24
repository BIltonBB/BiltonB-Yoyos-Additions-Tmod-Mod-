using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class SolarYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 200;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.knockBack = 3.5f;

            Item.width = 24;
            Item.height = 24;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.SolarYoyoProj>();
            Item.shootSpeed = 16f;

            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(gold: 12);
            Item.UseSound = SoundID.Item1;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.WoodYoyo)          // Wooden Yoyo (vanilla)
                .AddIngredient(ItemID.FragmentSolar, 25) // 25 Solar Fragments
                .AddIngredient(ModContent.ItemType<Materials.GuardianCoreM>(), 5)
                .AddIngredient(ModContent.ItemType<Materials.PureEmblemY>(), 1)
                .AddTile(TileID.LunarCraftingStation)    // Makes sense for Solar tier
                .Register();
        }
    }
}