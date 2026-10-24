using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Corpse-vigil wisp: static, dim, mourning sparkles. ai[0] = packed RGB.
    // Visual-only, dies alone after 10s.
    public class YoyoVigilProj : ModProjectile
    {
        public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = false;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 600;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            int packed = (int)Projectile.ai[0];
            Color tint = new Color((packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
            Lighting.AddLight(Projectile.Center, tint.R / 255f * 0.3f, tint.G / 255f * 0.3f, tint.B / 255f * 0.3f);
            if (Main.GameUpdateCount % 12 == 0)
            {
                float ang = Main.GameUpdateCount * 0.05f + Projectile.whoAmI;
                Dust mote = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * 14f, DustID.Smoke, new Vector2(0f, -0.6f), 140, tint, 0.9f);
                mote.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int packed = (int)Projectile.ai[0];
            Color tint = new Color((packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 120f, 0f, 1f);
            MiniYoyoFx.DrawGlow(Projectile.Center, 26f, tint, 0.25f * fade);
            for (int i = 0; i < 4; i++)
            {
                float ang = Main.GameUpdateCount * 0.05f + MathHelper.TwoPi * i / 4f + Projectile.whoAmI;
                MiniYoyoFx.DrawGlow(Projectile.Center + ang.ToRotationVector2() * 14f, 6f, tint, 0.4f * fade);
            }
            return false;
        }
    }
}
