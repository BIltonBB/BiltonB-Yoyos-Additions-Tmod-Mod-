using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class TorchGodYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 30;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.knockBack = 3f;

            Item.width = 30;
            Item.height = 26;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.TorchGodYoyoProj>();
            Item.shootSpeed = 15f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(gold: 3);
        }

        public override void AddRecipes()
        {
            // The Torch God drops no loot, so its yoyo is crafted with fire.
            CreateRecipe()
                .AddIngredient(ItemID.Torch, 99)
                .AddIngredient(ItemID.Obsidian, 10)
                .AddTile(TileID.Anvils)
                .AddCondition(Condition.DownedSkeletron)
                .Register();
        }
    }
}
