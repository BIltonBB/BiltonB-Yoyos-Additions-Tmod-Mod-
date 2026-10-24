using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class RandomYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 50;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 32;
            Item.useAnimation = 32;
            Item.knockBack = 3f;

            Item.width = 24;
            Item.height = 24;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.RandomYoyoProj>();
            Item.shootSpeed = 10f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Gray;
            Item.value = Item.buyPrice(copper: 69);
        }
    }
}
