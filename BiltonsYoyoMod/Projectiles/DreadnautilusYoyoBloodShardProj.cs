using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class DreadnautilusYoyoBloodShardProj : ModProjectile, IYoyoInstantHit
    {
        private readonly List<Vector2> ribbonTrail = new List<Vector2>();
        private bool spawnFxDone;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = true;

            Projectile.timeLeft = 200;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M11: abyssal lift so shards read against dark blood-moon skies.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(220, 110, 120), 0.3f) * Projectile.Opacity;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, angle.ToRotationVector2() * 2.4f, 100, default, 1.1f);
                    dust.noGravity = true;
                }
                // M12: nose streak thrown ahead along the volley bearing.
                Dust nose = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Projectile.velocity * 0.4f, 100, default, 1f);
                nose.noGravity = true;
                Lighting.AddLight(Projectile.Center, 1f, 0.2f, 0.2f);
            }

            Lighting.AddLight(Projectile.Center, 0.7f, 0.1f, 0.1f);

            Projectile.velocity.Y += 0.25f;
            if (Projectile.velocity.Y > 14f)
                Projectile.velocity.Y = 14f;

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            MiniYoyoFx.RibbonPush(ribbonTrail, Projectile.Center);

            // Lean gravity-shard trail: one directional mote every other tick.
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, -Projectile.velocity * 0.08f, 120, default, 0.9f);
                trail.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            MiniYoyoFx.RibbonDrawSoft(ribbonTrail, new Color(200, 30, 40), new Color(255, 150, 150), 5f);
            MiniYoyoFx.DrawGlow(Projectile.Center, 16f, new Color(220, 50, 60), 0.4f);
            // M13: tail knot + head ticks so fast volleys read as comets, not dots.
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 9f, 10f, new Color(180, 30, 40), 0.35f);
                Vector2 screen = Projectile.Center - Main.screenPosition;
                Vector2 side = new Vector2(-back.Y, back.X);
                Color tick = new Color(255, 170, 170, 140) * 0.5f;
                MiniYoyoFx.DrawLine(screen - back * 5f, screen + back * 5f, 1.5f, tick);
                MiniYoyoFx.DrawLine(screen - side * 5f, screen + side * 5f, 1.5f, tick);
            }
            return true;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi * i / 8f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, angle.ToRotationVector2() * 2.6f, 100, default, 1.1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.15f, 0.15f);
        }
    }
}
