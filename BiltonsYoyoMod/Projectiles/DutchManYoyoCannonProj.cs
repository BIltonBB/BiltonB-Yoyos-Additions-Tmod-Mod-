using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class DutchManYoyoCannonProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;
        private bool hasExploded;
        private readonly List<Vector2> fuse = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;

            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;

            Projectile.timeLeft = 240;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M11: lift the iron ball slightly out of darkness so the fuse reads at night.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(255, 205, 160), 0.25f) * Projectile.Opacity;
        }

        public override void AI()
        {
            // Spawn flash: tight muzzle pop along launch dir (was 10 random).
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 4; i++)
                {
                    Dust flash = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(1.2f, 1.2f), 100, default, 1f);
                    flash.noGravity = true;
                }
                for (int i = 0; i < 2; i++)
                {
                    Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.8f, 0.8f), 100, default, 1f);
                    spark.noGravity = true;
                }
                // M12: nose streak thrown ahead along the launch bearing.
                Dust nose = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, Projectile.velocity * 0.45f, 100, default, 1.1f);
                nose.noGravity = true;
                Lighting.AddLight(Projectile.Center, 0.9f, 0.7f, 0.5f);
            }

            // Gravity
            Projectile.velocity.Y += 0.1f;

            // Face movement direction
            if (Projectile.velocity.LengthSquared() > 0.01f)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            MiniYoyoFx.RibbonPush(fuse, Projectile.Center, 8f, 10);

            // Lean fuse trail: one alternating smoke/spark per tick + rare ember.
            // Was 2 dust every tick (~120/s per ball, x4 volley = 480/s).
            Dust trail = Dust.NewDustPerfect(Projectile.Center, Projectile.timeLeft % 2 == 0 ? DustID.Smoke : DustID.SolarFlare, -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.3f, 0.3f), 120, default, 0.85f);
            trail.noGravity = true;
            if (Main.rand.NextBool(8))
            {
                Dust ember = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, Main.rand.NextVector2Circular(0.8f, 0.8f), 100, default, 0.8f);
                ember.noGravity = true;
            }

            // M13: fast balls shed a stretched spark behind the head.
            if (Projectile.velocity.LengthSquared() > 81f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust streak = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.SolarFlare, -back * 1.6f, 100, default, 0.85f);
                streak.noGravity = true;
            }

            // M15: dying balls wheeze smoke instead of winking out mid-flight.
            if (Projectile.timeLeft < 30 && Projectile.timeLeft % 4 == 0)
            {
                Dust wheeze = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -0.8f), 120, default, 0.9f);
                wheeze.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.65f, 0.55f, 0.42f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // MAJOR B: cannonball comet anatomy — identity ribbon breathing with speed,
            // hot head + paler neck + dim tail knot along -vel, cross-flare ticks.
            // Desperate shot (ai[0] == 1): hotter, broader, angrier fuse.
            bool hot = Projectile.ai[0] == 1f;
            float speed = Projectile.velocity.Length();
            // Battery tint: port battery burns red, starboard burns green.
            Color sideTint = Projectile.ai[1] >= 0f ? new Color(120, 255, 150) : new Color(255, 130, 120);
            Color edge = hot ? new Color(140, 60, 50) : new Color(90, 90, 95);
            Color core = hot ? new Color(255, 150, 100) : new Color(255, 190, 120);
            edge = Color.Lerp(edge, sideTint, 0.35f);
            MiniYoyoFx.RibbonDrawSoft(fuse, edge, core, MathHelper.Clamp(4f + speed * 0.12f, 4f, 6.5f));

            float flicker = 1f + (float)Math.Sin(Main.GameUpdateCount * 0.5f + Projectile.whoAmI) * 0.12f;
            MiniYoyoFx.DrawGlow(Projectile.Center, (hot ? 22f : 18f) * flicker, hot ? new Color(255, 150, 90) : new Color(255, 180, 110), hot ? 0.55f : 0.45f);
            if (speed * speed > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 9f, 12f, core, 0.35f);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 17f, 8f, edge, 0.3f);

                Vector2 screen = Projectile.Center - Main.screenPosition;
                Vector2 side = new Vector2(-back.Y, back.X);
                Color flare = new Color(255, 225, 180, 140) * 0.5f;
                MiniYoyoFx.DrawLine(screen - back * 6f, screen + back * 6f, 1.5f, flare);
                MiniYoyoFx.DrawLine(screen - side * 6f, screen + side * 6f, 1.5f, flare);
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Explode();
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Explode();
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            if (!hasExploded)
            {
                Explode();
            }
        }

        private void Explode()
        {
            if (hasExploded)
                return;

            hasExploded = true;
            bool hot = Projectile.ai[0] == 1f;

            // Tight broadside burst: mixed core + single crisp ring (was 10 + 12).
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                int dustType = i % 2 == 0 ? DustID.Smoke : DustID.SolarFlare;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, dustType, angle.ToRotationVector2() * 2.6f, 100, default, 1.1f);
                dust.noGravity = true;
            }

            // Shockwave ring.
            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi * i / 8f;
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare, angle.ToRotationVector2() * 3.6f, 100, default, 1.1f);
                ring.noGravity = true;
            }

            // M14: identity-aware flash (desperate shots burn blood-orange).
            // No DrawGlow here: drawing is only safe in PreDraw/PostDraw.
            if (hot)
                Lighting.AddLight(Projectile.Center, 1f, 0.5f, 0.3f);
            else
                Lighting.AddLight(Projectile.Center, 1f, 0.8f, 0.6f);
            // Synced vanilla slash sparkles over the pop (free MP sync).
            Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = Projectile.Center }, Projectile.owner);
            YoyoParticles.SpawnBloom(Projectile.Center, 6f, 24f, 22, new Color(255, 190, 130), 0.55f);
            // Signature grave smoke over the pop (custom ModDust).
            for (int i = 0; i < 2; i++)
            {
                Dust grave = Dust.NewDustDirect(Projectile.Center, 8, 8, ModContent.DustType<global::BiltonsYoyoMod.Content.Dusts.GraveSmokeDust>(), 0f, -1f, 120, default, 1f);
                grave.noGravity = true;
            }
            Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item14, Projectile.Center);

            Projectile.Kill();
        }
    }
}
