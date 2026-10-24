using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class SantaYoyoMissilesProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;
        private readonly List<Vector2> smokeTrail = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 450;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Dust fx = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, angle.ToRotationVector2() * 2.2f, 0, default, 1f);
                    fx.noGravity = true;
                }
                for (int i = 0; i < 2; i++)
                {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, Main.rand.NextVector2Circular(1.2f, 1.2f), 0, default, 1f);
                    smoke.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.5f, 0.25f);
            }

            if (Projectile.timeLeft < 45)
            {
                Projectile.alpha += 6;
                if (Projectile.alpha > 255)
                    Projectile.alpha = 255;
            }

            Lighting.AddLight(Projectile.Center, 0.7f, 0.35f, 0.15f);

            // M12: the parked rocket throbs while it aims (ai[] is synced).
            if (Projectile.ai[0] == 1f)
            {
                float hoverPulse = 1f + 0.2f * (float)Math.Sin(Main.GameUpdateCount * 0.4f);
                Lighting.AddLight(Projectile.Center, 0.2f * hoverPulse, 0.1f * hoverPulse, 0.05f * hoverPulse);
            }

            MiniYoyoFx.RibbonPush(smokeTrail, Projectile.Center, 8f, 10);

            // Lean exhaust: one alternating mote every other tick (was 2/tick).
            // MAJOR B (whistle half): cracker rockets (slot % 3 == 0) trail confetti.
            if (Projectile.timeLeft % 2 == 0)
            {
                bool cracker = Projectile.whoAmI % 3 == 0;
                int dustType;
                if (cracker)
                    dustType = (Projectile.timeLeft / 2 % 2 == 0) ? DustID.Confetti : DustID.RedTorch;
                else
                    dustType = (Projectile.timeLeft / 2 % 2 == 0) ? DustID.RedTorch : DustID.Smoke;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, dustType, -Projectile.velocity * 0.1f, 0, default, 0.8f);
                dust.noGravity = true;
            }

            if (Projectile.ai[0] == 0f)
            {
                Projectile.velocity *= 0.96f;
                if (Projectile.velocity.LengthSquared() > 0.01f)
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

                if (Projectile.velocity.Length() < 0.5f)
                {
                    Projectile.velocity = Vector2.Zero;
                    Projectile.ai[0] = 1f;
                    Projectile.ai[1] = 0f;
                    Projectile.netUpdate = true;
                }
            }
            else if (Projectile.ai[0] == 1f)
            {
                Projectile.velocity = Vector2.Zero;
                Projectile.ai[1]++;
                if (Projectile.ai[1] >= 60f)
                {
                    Projectile.ai[0] = 2f;
                    Projectile.ai[1] = 0f;
                    Projectile.netUpdate = true;
                    // MAJOR B (launch half): cracker-star flash as the hover breaks
                    // into the hunt (ai[] is synced, so every client flashes together).
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 3f;
                        Dust star = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, angle.ToRotationVector2() * 3f, 0, default, 1.1f);
                        star.noGravity = true;
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 3f + MathHelper.PiOver4;
                        Dust star = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, angle.ToRotationVector2() * 2.4f, 0, default, 1f);
                        star.noGravity = true;
                    }
                    Lighting.AddLight(Projectile.Center, 1f, 0.6f, 0.3f);
                }
            }
            else
            {
                NPC target = FindNearestEnemy(600f);
                if (target != null)
                {
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 9f;
                    float currentAngle = Projectile.velocity.LengthSquared() > 0.01f ? Projectile.velocity.ToRotation() : desired.ToRotation();
                    float targetAngle = desired.ToRotation();
                    float diff = MathHelper.WrapAngle(targetAngle - currentAngle);
                    float maxTurn = MathHelper.ToRadians(15f);
                    float turn = MathHelper.Clamp(diff, -maxTurn, maxTurn);
                    float newAngle = currentAngle + turn;
                    // Proximity speed control: collapse the turn radius near the
                    // target so the missile connects instead of orbiting forever.
                    float dist = Vector2.Distance(Projectile.Center, target.Center);
                    float closingSpeed = MathHelper.Clamp(dist * 0.06f, 2.5f, 9f);
                    Projectile.velocity = newAngle.ToRotationVector2() * closingSpeed;
                }
                else if (Projectile.velocity.Length() < 1f)
                {
                    Projectile.velocity = Main.rand.NextVector2Unit() * 9f;
                }
                else
                {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 9f;
                }

                if (Projectile.velocity.LengthSquared() > 0.01f)
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            ExplodeEffects();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Firework-rocket read: exhaust ribbon + hot nose glow.
            // Crackers (slot % 3 == 0, deterministic): red-white dress.
            bool cracker = Projectile.whoAmI % 3 == 0;
            Color edge = cracker ? new Color(200, 60, 60) : new Color(120, 60, 50);
            Color core = cracker ? new Color(255, 235, 235) : new Color(255, 200, 150);
            MiniYoyoFx.RibbonDrawSoft(smokeTrail, edge, core, cracker ? 6f : 5f);
            Vector2 drawPoint = Projectile.Center + new Vector2(0f, Projectile.gfxOffY);
            MiniYoyoFx.DrawGlow(drawPoint, 20f, cracker ? new Color(255, 120, 120) : new Color(255, 140, 90), 0.5f);
            // MAJOR B (streak half): homing rockets tow a bright chase-line.
            if (Projectile.ai[0] == 2f && Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(drawPoint - back * 10f, 12f, core, 0.35f);
                Vector2 head = drawPoint - Main.screenPosition;
                Vector2 tail = drawPoint - back * 18f - Main.screenPosition;
                MiniYoyoFx.DrawLine(head, tail, 2f, new Color(255, 220, 190, 150) * 0.6f);
            }
            // Hover kindle: swells during the 1s stop so the homing launch reads.
            if (Projectile.ai[0] == 1f)
            {
                float kindle = Projectile.ai[1] / 60f;
                MiniYoyoFx.DrawGlow(drawPoint, 16f + 22f * kindle, new Color(255, 200, 150), 0.3f + 0.35f * kindle);
            }
            return true;
        }

        public override void OnKill(int timeLeft)
        {
            ExplodeEffects();
        }

        private void ExplodeEffects()
        {
            // Tight festive pop: smoke ring + red core + gold sparks.
            // Crackers add confetti + a wider flash (same damage).
            bool cracker = Projectile.whoAmI % 3 == 0;
            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi * i / 8f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, angle.ToRotationVector2() * 2.6f, 0, default, 1f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, angle.ToRotationVector2() * 3f, 0, default, 1f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f + MathHelper.PiOver4;
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, angle.ToRotationVector2() * (cracker ? 4.2f : 3.4f), 0, default, 1f);
                ring.noGravity = true;
            }
            if (cracker)
            {
                for (int i = 0; i < 5; i++)
                {
                    Dust pop = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, Main.rand.NextVector2Circular(3.5f, 3.5f), 0, default, 1.1f);
                    pop.noGravity = true;
                }
            }
            // No DrawGlow here: drawing is only safe in PreDraw/PostDraw
            // (nose glow in PreDraw covers the flash frame).
            Lighting.AddLight(Projectile.Center, 1f, 0.6f, 0.3f);
            // Synced vanilla slash sparkles over the pop (free MP sync).
            Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = Projectile.Center }, Projectile.owner);
            YoyoParticles.SpawnBloom(Projectile.Center, 6f, 24f, 22, new Color(255, 150, 110), 0.55f);
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
        }

        private NPC FindNearestEnemy(float maxRange)
        {
            NPC nearest = null;
            float maxRangeSq = maxRange * maxRange;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (MiniYoyoFx.IsAttackable(npc))
                {
                    float distSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                    if (distSq <= maxRangeSq)
                    {
                        maxRangeSq = distSq;
                        nearest = npc;
                    }
                }
            }
            return nearest;
        }
    }
}
