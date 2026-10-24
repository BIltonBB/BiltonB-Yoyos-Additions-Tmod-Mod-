using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class DestroyerYoyoProjLaser : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 8;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;

            Projectile.timeLeft = 120;

            Projectile.tileCollide = true;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }


        public override void AI()
        {
            // Strong Destroyer green lighting.
            Lighting.AddLight(
                Projectile.Center,
                0.8f,
                1.0f,
                0.25f
            );


            if (Projectile.velocity.LengthSquared() > 0.01f)
            {
                Projectile.rotation =
                    Projectile.velocity.ToRotation();
            }
        }
    }
}