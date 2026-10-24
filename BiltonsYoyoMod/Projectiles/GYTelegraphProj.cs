using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Projectiles
{
    // 1-second (60 tick) fade in/out 2000px aim indicator, then fires an
    // opposite pair of GYPointedProj along the telegraphed line.
    // Damage is carried in Projectile.damage (already scaled by the yoyo).
    public class GYTelegraphProj : ModProjectile
    {
        public override string Texture => "BiltonsYoyoMod/Common/GYPurpleTrail";

        public const int Lifetime = 60;
        public const float HalfLength = 1000f; // 2000px total line

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.aiStyle = -1;
            Projectile.alpha = 255; // invisible body — only the line is drawn
        }

        public override void AI()
        {
            // ai[0] = firing angle in radians.
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = Projectile.ai[0];
            Lighting.AddLight(Projectile.Center, 0.35f, 0.1f, 0.6f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Fade in over the first half, out over the second half.
            float progress = 1f - (float)Projectile.timeLeft / Lifetime;
            float alpha = MathHelper.Clamp((float)System.Math.Sin(progress * MathHelper.Pi), 0f, 1f);

            GYPurpleTrail.DrawTelegraph(Projectile.Center, Projectile.ai[0], HalfLength, alpha);
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            float angle = Projectile.ai[0];

            // Only the owner fires, so pairs never duplicate in multiplayer.
            if (Main.myPlayer != Projectile.owner)
                return;

            for (int k = 0; k < 2; k++)
            {
                float dir = angle + k * MathHelper.Pi;
                Vector2 vel = dir.ToRotationVector2() * GYPointedProj.FlySpeed;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    ModContent.ProjectileType<GYPointedProj>(), Projectile.damage, 0f, Projectile.owner);
            }
        }
    }
}
