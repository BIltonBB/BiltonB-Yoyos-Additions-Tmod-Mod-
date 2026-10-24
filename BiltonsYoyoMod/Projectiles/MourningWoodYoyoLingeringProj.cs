using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class MourningWoodYoyoLingeringProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            // Infinite enemy pierce; tile bounces are counted separately in ai[0]
            // (sharing the counter with enemy hits made embers fizzle early).
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;

            Projectile.timeLeft = 300;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        // M14: ember lift that respects the pre-death fade.
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
                Lighting.AddLight(Projectile.Center, 1f, 0.55f, 0.15f);
            }

            Lighting.AddLight(Projectile.Center, 0.85f, 0.4f, 0.1f);

            // Planted ember (ai[1] == 1): stuck fast, throbbing until the ai[0]
            // fuse runs out, then erupting. Still damaging the whole time.
            if (Projectile.ai[1] == 1f)
            {
                Projectile.velocity = Vector2.Zero;
                float throb = 0.5f + 0.3f * (float)System.Math.Sin(Main.GameUpdateCount * 0.25f);
                Lighting.AddLight(Projectile.Center, 0.9f * throb + 0.3f, 0.45f * throb + 0.15f, 0.12f);
                if (Main.GameUpdateCount % 4 == 0)
                {
                    Dust ember = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.Torch, new Vector2(0f, -1.4f), 120, default, 0.8f);
                    ember.noGravity = true;
                }
                if (Main.rand.NextBool(12))
                {
                    Dust ash = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -0.8f), 120, default, 0.9f);
                    ash.noGravity = true;
                }
                // Planted drips: the stuck ember weeps burning drips that fall.
                if (Main.GameUpdateCount % 12 == 0)
                {
                    Dust drip = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), 0f), DustID.Torch, new Vector2(0f, 0.5f), 120, default, 0.85f);
                    drip.noGravity = false;
                }
                if (Projectile.timeLeft <= Projectile.ai[0])
                {
                    // MAJOR C (eruption half): crowned burst — venom ring + hot core + soot column.
                    // MAJOR C (eruption half): crowned burst — venom ring + hot core + soot column.
                    for (int i = 0; i < 10; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 10f;
                        Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, angle.ToRotationVector2() * 3f, 100, default, 1.1f);
                        burst.noGravity = true;
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 6f + MathHelper.PiOver4;
                        Dust core = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, angle.ToRotationVector2() * 2f, 100, default, 1f);
                        core.noGravity = true;
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        Dust column = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-4f, 4f), -4f), DustID.Smoke, new Vector2(0f, -1.8f), 120, default, 1f);
                        column.noGravity = true;
                    }
                    Lighting.AddLight(Projectile.Center, 1f, 0.55f, 0.15f);
                    // Synced-style sparkles, spawned locally per client (AI runs server-side).
                    Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: true, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                        new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = Projectile.Center });
                    YoyoParticles.SpawnBloom(Projectile.Center, 8f, 30f, 24, new Color(255, 180, 110), 0.55f);
                    Projectile.Kill();
                    return;
                }

                if (Projectile.timeLeft < 60)
                    Projectile.alpha = (int)(255f * (1f - Projectile.timeLeft / 60f));
                return;
            }

            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 12f)
                Projectile.velocity.Y = 12f;

            Projectile.rotation += 0.15f;

            if (Projectile.timeLeft < 60)
                Projectile.alpha = (int)(255f * (1f - Projectile.timeLeft / 60f));

            // Lean ember trail: one rising mote every 3rd tick (was ~80/s).
            if (Projectile.timeLeft % 3 == 0)
            {
                Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, new Vector2(0f, -0.8f), 120, default, 0.7f);
                trail.noGravity = true;
            }
            if (Projectile.timeLeft % 6 == 0)
            {
                Dust wisp = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -1.2f), 120, default, 0.7f);
                wisp.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Planted: throbbing halo + pulse ring over the ember.
            if (Projectile.ai[1] == 1f)
            {
                float pulse = 0.5f + 0.5f * (float)System.Math.Sin(Main.GameUpdateCount * 0.25f);
                MiniYoyoFx.DrawGlow(Projectile.Center, 22f + 8f * pulse, new Color(255, 140, 60), 0.4f + 0.2f * pulse);
                MiniYoyoFx.DrawRing(Projectile.Center, 12f + 5f * pulse, 2f, new Color(255, 180, 120) * (0.35f + 0.35f * pulse), 20);
                // M15: ember ground shadow so the planted mine sits on the floor.
                MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, 8f), 24f, new Color(90, 40, 15), 0.2f);
                // MAJOR C (fuse half): contracting doom ring — the eruption telegraph.
                // ai[0] is the timeLeft mark for detonation; the ring closes as it nears.
                float remaining = Projectile.timeLeft - Projectile.ai[0];
                if (remaining > 0f)
                {
                    float t = MathHelper.Clamp(1f - remaining / 75f, 0f, 1f);
                    MiniYoyoFx.DrawRing(Projectile.Center, MathHelper.Lerp(24f, 10f, t), 2f, new Color(255, 190, 110) * (0.3f + 0.5f * t), 20);
                    // Fuse strobe quickens as detonation nears.
                    float strobe = (float)System.Math.Sin(Main.GameUpdateCount * (0.3f + 1.1f * t));
                    if (strobe > 0.55f)
                        MiniYoyoFx.DrawGlow(Projectile.Center, 26f, new Color(255, 225, 175), 0.4f * Projectile.Opacity);
                }
                return true;
            }
            // MAJOR C (arming half): tumbling embers wear a bounce-count ring that
            // tightens as they settle (ai[0] counts tile touches toward planting).
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f, new Color(255, 140, 60), 0.45f);
            MiniYoyoFx.DrawRing(Projectile.Center, 14f - 3f * MathHelper.Clamp(Projectile.ai[0], 0f, 1f), 1.5f, new Color(255, 170, 100) * 0.35f, 16);
            // Bounce flash: ai[1] counts down 10 ticks from each tile hit.
            if (Projectile.ai[1] > 0f)
            {
                float f = Projectile.ai[1] / 10f;
                MiniYoyoFx.DrawRing(Projectile.Center, 12f + 18f * (1f - f), 2f, new Color(255, 190, 120) * (0.6f * f), 20);
            }
            return true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Already planted: hold fast, don't count further touches.
            if (Projectile.ai[1] == 1f)
                return false;

            // Bounce with energy loss
            if (oldVelocity.Y != Projectile.velocity.Y)
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
            if (oldVelocity.X != Projectile.velocity.X)
                Projectile.velocity.X = -oldVelocity.X * 0.7f;

            // Bounce counter in ai[0]: two bounces, then plant with a 75-tick
            // fuse (stored back in ai[0] as a timeLeft mark). Enemy hits don't
            // consume bounces; the planted ember keeps damaging until it pops.
            Projectile.ai[0] += 1f;
            if (Projectile.ai[0] >= 2f)
            {
                Projectile.ai[1] = 1f;
                Projectile.ai[0] = Projectile.timeLeft - 75;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Dust plant = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, angle.ToRotationVector2() * 2.2f, 100, default, 1f);
                    plant.noGravity = true;
                }
                for (int i = 0; i < 3; i++)
                {
                    Dust ash = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1f), 120, default, 1f);
                    ash.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.5f, 0.15f);
                return false;
            }

            for (int i = 0; i < 2; i++)
            {
                Dust puff = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Main.rand.NextVector2Circular(1.2f, 1.2f), 100, default, 0.85f);
                puff.noGravity = true;
            }

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, angle.ToRotationVector2() * 2.2f, 120, default, 0.9f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.5f, 0.15f);
        }
    }
}
