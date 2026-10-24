using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using Terraria.ID;

namespace BiltonsYoyoMod.Projectiles
{
    public class EmpressYoyoProjS : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.penetrate = 3;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            EmpressGlow(Projectile.Center, 3.0f);
        }

        // 🔥 REPLACED GLOW (STRONGEST)
        private void EmpressGlow(Vector2 pos, float power)
        {
            float t = (float)(Main.GameUpdateCount % 90) / 90f;
            float wave = (float)Math.Sin(t * MathHelper.TwoPi) * 0.5f + 0.5f;

            Color glowColor = Color.Lerp(Color.HotPink, Color.Yellow, wave);

            Lighting.AddLight(pos, glowColor.ToVector3() * power * 2.5f);
            Lighting.AddLight(pos + new Vector2(6, 0), glowColor.ToVector3() * power);
            Lighting.AddLight(pos + new Vector2(-6, 0), glowColor.ToVector3() * power);
            Lighting.AddLight(pos + new Vector2(0, 6), glowColor.ToVector3() * power);
            Lighting.AddLight(pos + new Vector2(0, -6), glowColor.ToVector3() * power);

            if (Main.rand.NextBool(3))
            {
                int d = Dust.NewDust(pos - Vector2.One * 4, 8, 8, DustID.PinkTorch);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.1f;
                Main.dust[d].scale = 1.3f + wave * 0.7f;
                Main.dust[d].color = glowColor;
            }
        }
    }
}