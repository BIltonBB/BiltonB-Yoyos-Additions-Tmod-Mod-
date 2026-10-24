using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjG : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
            Projectile.hide = true;
        }

        public override void AI()
        {
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, Main.rand.NextVector2Circular(2f, 2f), 90, Color.White, 0.7f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.05f, 0.25f, 0.55f);
        }
    }
}
