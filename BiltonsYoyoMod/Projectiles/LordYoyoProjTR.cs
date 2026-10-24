using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjTR : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.hide = true;
        }

        public override void AI()
        {
            Projectile.scale = MathHelper.Lerp(0.5f, 2.5f, 1f - Projectile.timeLeft / 60f);
            if (Projectile.timeLeft < 20)
                Projectile.scale *= Projectile.timeLeft / 20f;

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f), DustID.IceTorch,
                    Main.rand.NextVector2Circular(2f, 2f), 100, Color.White, Main.rand.NextFloat(0.8f, 1.6f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.65f, 1.1f);
        }
    }
}
