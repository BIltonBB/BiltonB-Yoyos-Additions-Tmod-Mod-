using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class MourningWoodYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 85;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 26;
            Item.useAnimation = 26;
            Item.knockBack = 5f;

            Item.width = 30;
            Item.height = 26;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.MourningWoodYoyoProj>();
            Item.shootSpeed = 13f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 12);
        }
    }
}
