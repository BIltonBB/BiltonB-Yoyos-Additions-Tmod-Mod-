using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class HeavyYoyoShockwaveProj : ModProjectile
    {
        private const int Lifetime = 22;
        private const float MaxRadius = 90f;

        public override string Texture => "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            Projectile.width = 180;
            Projectile.height = 180;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 direction = Main.rand.NextVector2Unit();
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + direction * Main.rand.NextFloat(10f, MaxRadius),
                    DustID.Smoke,
                    direction * Main.rand.NextFloat(1f, 4f),
                    60,
                    default,
                    Main.rand.NextFloat(0.8f, 1.4f)
                );
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;

            float progress = 1f - Projectile.timeLeft / (float)Lifetime;
            float radius = MathHelper.Lerp(20f, MaxRadius, progress);
            float opacity = 0.35f * (Projectile.timeLeft / (float)Lifetime);
            Color circleColor = new Color(255, 150, 50) * opacity;

            int minY = (int)Math.Floor(-radius);
            int maxY = (int)Math.Ceiling(radius);
            for (int y = minY; y <= maxY; y++)
            {
                float remaining = radius * radius - y * y;
                if (remaining <= 0f)
                    continue;

                float halfWidth = (float)Math.Sqrt(remaining);
                Main.spriteBatch.Draw(
                    pixel,
                    center + new Vector2(-halfWidth, y),
                    new Rectangle(0, 0, 1, 1),
                    circleColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(halfWidth * 2f, 1f),
                    SpriteEffects.None,
                    0f
                );
            }

            return false;
        }
    }
}
