using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Items
{
    public class TwinsYoyo : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 50;
            Item.DamageType = DamageClass.MeleeNoSpeed;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 25;
            Item.useAnimation = 25;

            Item.knockBack = 2.5f;

            Item.width = 24;
            Item.height = 24;

            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.channel = true;

            // Default value only.
            // Shoot() below decides which yoyo becomes the main one.
            Item.shoot =
                ModContent.ProjectileType<
                    Projectiles.RetinazerTwinsYoyoProj>();

            Item.shootSpeed = 16f;

            Item.UseSound = SoundID.Item1;

            Item.rare =
                ItemRarityID.Blue;

            Item.value =
                Item.buyPrice(
                    silver: 50
                );
        }


        public override bool Shoot(
            Player player,
            EntitySource_ItemUse_WithAmmo source,
            Vector2 position,
            Vector2 velocity,
            int type,
            int damage,
            float knockBack)
        {
            int mainType;


            // 50 / 50 MAIN YOYO SELECTION

            if (Main.rand.NextBool())
            {
                mainType =
                    ModContent.ProjectileType<
                        Projectiles.RetinazerTwinsYoyoProj>();
            }
            else
            {
                mainType =
                    ModContent.ProjectileType<
                        Projectiles.SpezmatismTwinsYoyoProj>();
            }


            Projectile.NewProjectile(
                source,
                position,
                velocity,
                mainType,
                damage,
                knockBack,
                player.whoAmI
            );


            // We manually spawned the projectile.
            return false;
        }
    }
}