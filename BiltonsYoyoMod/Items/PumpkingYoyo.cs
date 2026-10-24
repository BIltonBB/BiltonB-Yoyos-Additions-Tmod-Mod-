using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class PumpkingYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 105;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 26;
            Item.useAnimation = 26;
            Item.knockBack = 6f;

            Item.width = 30;
            Item.height = 26;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.PumpkingYoyoProj>();
            Item.shootSpeed = 13f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.buyPrice(gold: 16);
        }
    }
}
