using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjTS : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.hide = true;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
            {
                Projectile.Kill();
                return;
            }

            int timer = (int)Projectile.localAI[0]++;
            float p = MathHelper.Clamp(timer / 120f, 0f, 1f);
            float radius = MathHelper.Lerp(18f, 100f, p);

            for (int i = 0; i < 5; i++)
            {
                float a = Main.GameUpdateCount * 0.03f + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 dir = a.ToRotationVector2();
                Dust d = Dust.NewDustPerfect(player.Center + dir * (radius + Main.rand.NextFloat(-8f, 8f)), DustID.IceTorch,
                    dir * Main.rand.NextFloat(0.2f, 1.2f), 100, Color.White, Main.rand.NextFloat(0.6f, 1.3f));
                d.noGravity = true;
            }

            if (timer == 90)
            {
                for (int i = 0; i < 80; i++)
                {
                    Vector2 dir = Main.rand.NextVector2Unit();
                    Dust d = Dust.NewDustPerfect(player.Center + dir * 30f, DustID.IceTorch, dir * Main.rand.NextFloat(4f, 10f), 80, Color.White, Main.rand.NextFloat(0.8f, 1.6f));
                    d.noGravity = true;
                }
            }

            if (timer >= 120)
                Projectile.Kill();
        }
    }
}
