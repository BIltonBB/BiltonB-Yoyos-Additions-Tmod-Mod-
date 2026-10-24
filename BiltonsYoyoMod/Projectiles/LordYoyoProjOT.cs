using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjOT : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 9999;
            Projectile.hide = true;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active || player.dead || player.statLife <= 0)
            {
                Projectile.Kill();
                return;
            }
            if (Main.rand.NextBool(1))
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dir = angle.ToRotationVector2();
                Dust d = Dust.NewDustPerfect(player.Center + dir * Main.rand.NextFloat(45f, 95f), DustID.IceTorch,
                    dir * Main.rand.NextFloat(-0.75f, 0.75f), 120, Color.White, Main.rand.NextFloat(1.15f, 2.25f));
                d.noGravity = true;
            }
        }
    }
}
