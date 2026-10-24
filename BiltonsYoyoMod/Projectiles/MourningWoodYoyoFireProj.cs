using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class MourningWoodYoyoFireProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;
        private bool apexFlashed; // stall flash at the top of the arc (visual only)

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = true;

            Projectile.timeLeft = 300;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M10: ember lift that respects the pre-death fade.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(255, 200, 150), 0.3f) * Projectile.Opacity;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 5; i++)
                {
                    float angle = MathHelper.TwoPi * i / 5f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, angle.ToRotationVector2() * 2f, 100, default, 1f);
                    dust.noGravity = true;
                }
                // M11: nose streak thrown ahead along the launch bearing.
                Dust nose = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Projectile.velocity * 0.45f, 100, default, 1f);
                nose.noGravity = true;
                Lighting.AddLight(Projectile.Center, 1f, 0.6f, 0.2f);
            }

            Lighting.AddLight(Projectile.Center, 0.9f, 0.5f, 0.1f);

            Projectile.velocity.Y += 0.2f;
            if (Projectile.velocity.Y > 12f)
                Projectile.velocity.Y = 12f;

            // MAJOR B (apex half): stall flash where the climb dies into the fall.
            if (!apexFlashed && Projectile.velocity.Y >= 0f)
            {
                apexFlashed = true;
                for (int i = 0; i < 3; i++)
                {
                    float angle = MathHelper.TwoPi * i / 3f;
                    Dust stall = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, angle.ToRotationVector2() * 1.8f, 100, default, 1f);
                    stall.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.65f, 0.25f);
            }

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Projectile.timeLeft < 60)
                Projectile.alpha = (int)(255f * (1f - Projectile.timeLeft / 60f));

            // Lean flame shedding: one directional ember every 3rd tick.
            // Was 1/tick x ~50 alive = 3000 dust/s. Now ~1000/s.
            if (Projectile.timeLeft % 3 == 0)
            {
                Dust shed = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, -Projectile.velocity * 0.08f, 120, default, 0.8f);
                shed.noGravity = true;
            }

            // M13: fast splinters shed a stretched spark behind the head.
            if (Projectile.velocity.LengthSquared() > 100f && Main.rand.NextBool(12))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust streak = Dust.NewDustPerfect(Projectile.Center - back * 6f, DustID.Torch, -back * 1.5f, 120, default, 0.8f);
                streak.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // M12: burning-splinter comet — flickering head + neck + tail knots +
            // head ticks (sprite draws on top).
            float flicker = 1f + (float)System.Math.Sin(Main.GameUpdateCount * 0.4f + Projectile.whoAmI) * 0.1f;
            float fade = Projectile.Opacity;
            MiniYoyoFx.DrawGlow(Projectile.Center, 20f * flicker, new Color(255, 150, 60), 0.5f * fade);
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 7f, 14f, new Color(220, 100, 30), 0.35f * fade);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 14f, 10f, new Color(190, 80, 25), 0.28f * fade);
                Vector2 screen = Projectile.Center - Main.screenPosition;
                Vector2 side = new Vector2(-back.Y, back.X);
                Color tick = new Color(255, 210, 150, 140) * (0.5f * fade);
                MiniYoyoFx.DrawLine(screen - back * 5f, screen + back * 5f, 1.5f, tick);
                MiniYoyoFx.DrawLine(screen - side * 5f, screen + side * 5f, 1.5f, tick);
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 120);

            // Tight scorch ring at contact (was 5 random).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust scorch = Dust.NewDustPerfect(target.Center, DustID.GoldFlame, angle.ToRotationVector2() * 2.2f, 100, default, 0.9f);
                scorch.noGravity = true;
            }
            Lighting.AddLight(target.Center, 1f, 0.6f, 0.2f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, angle.ToRotationVector2() * 2.4f, 100, default, 1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.55f, 0.15f);
            return true;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, angle.ToRotationVector2() * 2f, 120, default, 0.9f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 3; i++)
            {
                Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1f), 120, default, 0.7f);
                smoke.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.5f, 0.15f);
        }
    }
}
