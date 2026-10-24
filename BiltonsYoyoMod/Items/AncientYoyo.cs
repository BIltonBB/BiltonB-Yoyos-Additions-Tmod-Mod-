using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class AncientYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 115;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.knockBack = 4f;

            Item.width = 26;
            Item.height = 26;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.AncientYoyoProj>();
            Item.shootSpeed = 17f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(gold: 10);
        }
    }
}
