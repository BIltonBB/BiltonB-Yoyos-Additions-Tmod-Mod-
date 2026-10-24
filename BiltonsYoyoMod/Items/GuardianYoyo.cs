using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class GuardianYoyo : ModItem
    {
        // Item PNG lives at the mod root (BiltonsYoyoMod/Items/GuardianYoyo.png).
        public override string Texture => "BiltonsYoyoMod/Items/GuardianYoyo";

        public override void SetDefaults()
        {
            Item.damage = 90;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.knockBack = 3f;

            Item.width = 24;
            Item.height = 24;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.GuardianYoyoProj>();
            Item.shootSpeed = 16f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.LightRed;
            Item.value = Item.buyPrice(gold: 5);
        }
    }
}
