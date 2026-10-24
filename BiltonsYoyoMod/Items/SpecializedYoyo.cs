using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class SpecializedYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 34;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 22;
            Item.useAnimation = 22;
            Item.knockBack = 2f;

            Item.width = 24;
            Item.height = 24;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.SpecializedYoyoProj>();
            Item.shootSpeed = 15f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(gold: 2);
        }

        public override void AddRecipes()
        {
            // Pity recipe for the 5% Cowboy drop: black-market parts only
            // flow once Six Stringer unlocks (Cowboys + Eye defeated).
            CreateRecipe()
                .AddIngredient<BlackMarket.IllegalYoyoParts>(10)
                .AddRecipeGroup("BiltonsYoyoMod:AnyEvilBar", 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
