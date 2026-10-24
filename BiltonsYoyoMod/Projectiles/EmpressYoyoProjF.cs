using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using Terraria.ID;

namespace BiltonsYoyoMod.Projectiles
{
    public class EmpressYoyoProjF : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = 10;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
        }

        public override void AI()
        {
            int targetID = (int)Projectile.ai[0];
            if (targetID >= 0 && targetID < Main.maxNPCs)
            {
                NPC npc = Main.npc[targetID];
                if (npc.active)
                {
                    Vector2 dir = (npc.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 12f, 0.08f);
                }
            }

            EmpressGlow(Projectile.Center, 2.4f);
        }

        // 🔥 REPLACED GLOW
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