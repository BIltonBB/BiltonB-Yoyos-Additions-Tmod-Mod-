using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class PumpkingYoyoFireProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 300;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        // M14: harvest lift that respects the pre-death fade.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(255, 205, 160), 0.3f) * Projectile.Opacity;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Dust fx = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 2.2f, 0, default, 1f);
                    fx.noGravity = true;
                }
                for (int i = 0; i < 2; i++)
                {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1f), 0, default, 0.9f);
                    smoke.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.6f, 0.2f);
            }

            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.timeLeft < 60)
            {
                Projectile.alpha += 4;
                if (Projectile.alpha > 255)
                    Projectile.alpha = 255;
            }

            Lighting.AddLight(Projectile.Center, 0.9f, 0.45f, 0.1f);

            // Lean ember trail: every other tick (was 2/tick x ~50 alive).
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, -Projectile.velocity * 0.06f, 0, default, 0.8f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // M15: harvest comet — head + neck + tail knots with head ticks.
            float fade = Projectile.Opacity;
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f, new Color(255, 150, 60), 0.45f * fade);
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 8f, 12f, new Color(230, 110, 40), 0.35f * fade);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 15f, 8f, new Color(190, 80, 25), 0.28f * fade);
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
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f;
                Dust pop = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 2.4f, 0, default, 0.9f);
                pop.noGravity = true;
            }
            for (int i = 0; i < 3; i++)
            {
                Dust wisp = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1f), 0, default, 0.9f);
                wisp.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.6f, 0.2f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.01f)
                Projectile.velocity.X = -oldVelocity.X * 0.7f;
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.01f)
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
            return false;
        }
    }
}
