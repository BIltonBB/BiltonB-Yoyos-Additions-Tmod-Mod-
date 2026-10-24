using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class DreadnautilusBloodOrbProj : ModProjectile, IYoyoInstantHit
    {
        private const float Accel = 0.08f;
        private const float MaxSpeed = 11f;

        private bool spawnFxDone;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;

            Projectile.timeLeft = 300;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M14: abyssal lift so orbs read in dark water.
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
                // M15 (launch half): nose streak ahead along the shed bearing.
                Dust nose = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Projectile.velocity * 0.4f, 100, default, 1f);
                nose.noGravity = true;
                Lighting.AddLight(Projectile.Center, 1f, 0.2f, 0.25f);
            }

            Lighting.AddLight(Projectile.Center, 0.8f, 0.1f, 0.15f);

            // Accelerate from slow start up to max, then stop accelerating
            float speed = Projectile.velocity.Length();
            if (speed < MaxSpeed && speed > 0.01f)
            {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                speed += Accel;
                if (speed > MaxSpeed)
                    speed = MaxSpeed;
                Projectile.velocity = dir * speed;
            }

            // At full speed the orb throws one motion streak behind it.
            if (speed >= MaxSpeed - 0.01f && Projectile.velocity.LengthSquared() > 0.01f && Projectile.timeLeft % 2 == 0)
            {
                Dust streak = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, -Projectile.velocity * 0.15f, 120, default, 0.95f);
                streak.noGravity = true;
            }

            Projectile.rotation += 0.2f;

            // Lean accelerating-orb trail: alternating ember, no box-spawn cloud.
            // Dash-born orbs (ai[0] == 1) burn darker and denser.
            bool born = Projectile.ai[0] == 1f;
            if (Projectile.timeLeft % (born ? 1 : 2) == 0)
            {
                Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, -Projectile.velocity * 0.06f, 120, default, born ? 1f : 0.9f);
                trail.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            bool born = Projectile.ai[0] == 1f;
            MiniYoyoFx.DrawGlow(Projectile.Center, born ? 26f : 22f, born ? new Color(170, 25, 35) : new Color(220, 50, 60), 0.45f);
            // M15 (comet half): tail knot + head ticks so accelerating orbs streak.
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 10f, 12f, born ? new Color(140, 20, 30) : new Color(190, 40, 50), 0.35f);
                Vector2 screen = Projectile.Center - Main.screenPosition;
                Vector2 side = new Vector2(-back.Y, back.X);
                Color tick = new Color(255, 170, 170, 140) * 0.45f;
                MiniYoyoFx.DrawLine(screen - back * 6f, screen + back * 6f, 1.5f, tick);
                MiniYoyoFx.DrawLine(screen - side * 6f, screen + side * 6f, 1.5f, tick);
            }
            return true;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 10; i++)
            {
                float angle = MathHelper.TwoPi * i / 10f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, angle.ToRotationVector2() * 2.8f, 100, default, 1.1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.15f, 0.2f);
        }
    }
}
