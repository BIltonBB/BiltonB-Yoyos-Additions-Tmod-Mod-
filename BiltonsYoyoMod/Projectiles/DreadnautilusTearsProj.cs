using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class DreadnautilusTearsProj : ModProjectile, IYoyoInstantHit
    {
        private const float OrbitRadius = 70f;
        private const int OrbitDuration = 120;

        private bool spawnFxDone;

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;

            Projectile.timeLeft = 180;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
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
                Lighting.AddLight(Projectile.Center, 1f, 0.2f, 0.2f);
            }

            Lighting.AddLight(Projectile.Center, 0.7f, 0.1f, 0.12f);

            Projectile.localAI[0]++;

            // Nearest-first tracking: the slot may hold a stale/returning yoyo after
            // a recast, which dragged the orbit off-center. The live yoyo being
            // watched is (almost) always the nearest one — slot retired.
            int dreadType = ModContent.ProjectileType<DreadnautilusYoyoProj>();
            Projectile yoyo = null;
            float bestDistSq = 800f * 800f;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == dreadType && p.owner == Projectile.owner)
                {
                    float dSq = Vector2.DistanceSquared(Projectile.Center, p.Center);
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        yoyo = p;
                    }
                }
            }

            // Rotation speed starts 0.15 rad/tick and slows to 0 over 2s.
            // Shared rate for the whole trio (no per-tear personality): identical
            // increments keep the 120° formation exact, so the orbit can't drift
            // lopsided. Lane index below is visuals-only (tint), never position.
            float t = Projectile.localAI[0] / OrbitDuration;
            if (t > 1f)
                t = 1f;
            float rotSpeed = 0.15f * (1f - t);

            Projectile.ai[1] += rotSpeed;
            Projectile.rotation = Projectile.ai[1];

            // Maturation: the tear visibly swells as the squid nears.
            Projectile.scale = 1f + t * 0.25f;

            // Glow pulse synced to the slowdown: brightest while spinning fast, dim as it settles.
            float glowPulse = 0.35f * (1f - t);
            Lighting.AddLight(Projectile.Center, 0.35f + glowPulse, 0.05f + glowPulse * 0.3f, 0.06f + glowPulse * 0.3f);

            if (yoyo != null)
            {
                // Shared orbit: tightens from the summon ring onto the trio circle
                // as rotation settles. Same radius for all three (formation exact).
                float orbitR = MathHelper.Lerp(OrbitRadius, 64f, t);
                Projectile.Center = yoyo.Center + Projectile.ai[1].ToRotationVector2() * orbitR;
            }

            // Lean orbit trail: one mote every other tick (was 1/4 box + 1/tick cloud).
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Vector2.Zero, 120, default, 0.85f);
                trail.noGravity = true;
            }

            // After rotation ends: transform into squid
            if (Projectile.localAI[0] >= OrbitDuration)
            {
                // Tight conversion flash as the tear becomes a squid (was 12 random).
                for (int i = 0; i < 8; i++)
                {
                    float angle = MathHelper.TwoPi * i / 8f;
                    Dust flash = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, angle.ToRotationVector2() * 2.8f, 100, default, 1.2f);
                    flash.noGravity = true;
                }
                // MAJOR B (conversion half): ink crowns the flash as the squid breaches.
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                    Dust ink = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, angle.ToRotationVector2() * 2f, 120, default, 1f);
                    ink.noGravity = true;
                }
                // Ink shroud: slow black hang that veils the newborn squid (~0.5s).
                for (int i = 0; i < 4; i++)
                {
                    Dust shroud = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), DustID.Smoke, Main.rand.NextVector2Circular(0.4f, 0.4f), 140, default, 1.5f);
                    shroud.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.3f, 0.3f);
                if (Main.myPlayer == Projectile.owner)
                {
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<DreadnautilusHomingBloodSquids>(),
                        Projectile.damage,
                        Projectile.knockBack,
                        Projectile.owner);
                }
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // MAJOR B (maturation half): lane-tinted glow + a contracting progress ring
            // that telegraphs the squid conversion. Orbit ring stays on the yoyo
            // (avoids 3x overdraw from 3 tears). Glow settles as rotation settles.
            float t = MathHelper.Clamp(Projectile.localAI[0] / OrbitDuration, 0f, 1f);
            Color lane = (Projectile.whoAmI % 3) switch
            {
                0 => new Color(220, 50, 60),
                1 => new Color(200, 40, 90),
                _ => new Color(235, 90, 100),
            };
            MiniYoyoFx.DrawRing(Projectile.Center, MathHelper.Lerp(26f, 12f, t), 1.5f, lane * (0.25f + 0.45f * t), 20);
            MiniYoyoFx.DrawGlow(Projectile.Center, 20f * (1.2f - 0.4f * t), lane, 0.4f * (1f - 0.4f * t));
            return true;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, angle.ToRotationVector2() * 2.2f, 100, default, 1.1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.15f, 0.15f);
        }
    }
}
