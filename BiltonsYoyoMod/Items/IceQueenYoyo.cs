using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class IceQueenYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 110;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.knockBack = 5f;

            Item.width = 30;
            Item.height = 26;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.IceQueenYoyoProj>();
            Item.shootSpeed = 14f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Cyan; // Aqua
            Item.value = Item.buyPrice(gold: 18);
        }
    }
}
