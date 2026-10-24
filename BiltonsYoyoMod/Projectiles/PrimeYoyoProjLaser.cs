using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class PrimeYoyoProjLaser : ModProjectile
    {
        private Vector2[] afterimages =
            new Vector2[PrimeYoyoVisualHelper.AfterimageCount];

        private float afterimageDistance;
        private bool afterimagesInitialized;


        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 4;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;
            Projectile.tileCollide = true;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.timeLeft = 120;

            Projectile.extraUpdates = 1;
        }


        public override void AI()
        {
            Projectile.rotation =
                Projectile.velocity.ToRotation();


            PrimeYoyoVisualHelper.TrackAfterimages(
                afterimages,
                ref afterimageDistance,
                ref afterimagesInitialized,
                Projectile.Center
            );


            if (Main.rand.NextBool(2))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.BlueTorch,
                        -Projectile.velocity * 0.04f +
                        Main.rand.NextVector2Circular(
                            0.8f,
                            0.8f
                        ),
                        120,
                        default,
                        0.8f
                    );

                dust.noGravity = true;
            }
        }


        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/PrimeYoyoProjLaser"
                ).Value;


            /*
             * Dedicated laser afterimages.
             *
             * No colored glow.
             * Uses actual world lighting + dark shadow.
             */
            PrimeYoyoVisualHelper.DrawLitAfterimages(
                texture,
                afterimages,
                Projectile.rotation,
                Projectile.scale
            );


            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                Color.White,
                Projectile.rotation,
                texture.Size() / 2f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}