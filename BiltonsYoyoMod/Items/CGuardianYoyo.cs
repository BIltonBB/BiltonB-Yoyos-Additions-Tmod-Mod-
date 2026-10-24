using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class CGuardianYoyo : ModItem
    {
        // Item PNG lives at the mod root (BiltonsYoyoMod/Items/CGuardianYoyo.png).
        public override string Texture => "BiltonsYoyoMod/Items/CGuardianYoyo";

        public override void SetDefaults()
        {
            Item.damage = 150;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.knockBack = 3.5f;

            Item.width = 24;
            Item.height = 24;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.CGuardianYoyoProj>();
            Item.shootSpeed = 17f;

            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.buyPrice(gold: 10);
        }
    }
}
