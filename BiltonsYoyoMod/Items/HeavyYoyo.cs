using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class HeavyYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 45;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 32;
            Item.useAnimation = 32;
            Item.knockBack = 7f;

            Item.width = 26;
            Item.height = 26;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.HeavyYoyoProj>();
            Item.shootSpeed = 10f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 5);
        }

        public override void AddRecipes()
        {
            // Alternative to the 25% Giant Bearing drop: bearing scrap and
            // advanced parts (Bearing Party needs Skeletron defeated).
            CreateRecipe()
                .AddIngredient<Materials.YoyoScraps>(25)
                .AddIngredient<Materials.AdvancedYoyoParts>(3)
                .AddTile(TileID.Anvils)
                .AddCondition(Condition.DownedSkeletron)
                .Register();
        }
    }
}
