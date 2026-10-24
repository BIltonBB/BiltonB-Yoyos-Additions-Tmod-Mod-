using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class MartianYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 140;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 24;
            Item.useAnimation = 24;
            Item.knockBack = 4f;

            Item.width = 30;
            Item.height = 26;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.MartianYoyoProj>();
            Item.shootSpeed = 15f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.buyPrice(gold: 25);
        }
    }
}
