using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjIB : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 220;
            Projectile.height = 220;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 20;
            Projectile.hide = true;
        }

        public override void AI()
        {
            for (int i = 0; i < 5; i++)
            {
                Vector2 dir = Main.rand.NextVector2Unit();
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, dir * Main.rand.NextFloat(2f, 6f), 70, Color.White, 0.8f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.2f, 0.7f, 1f);
            Projectile.scale += 0.04f;
        }
    }
}
