using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Saucer missile: homing (max turn 15deg/tick, speed 10), fire/exhaust dust + light,
    /// small explosion on hit.
    /// </summary>
    public class MartianYoyoMissilesProj : ModProjectile, IYoyoInstantHit
    {
        private bool hasExploded;
        private bool spawnFxDone;
        private bool closeIn; // proximity fuse read (visual only; recomputed locally)
        private readonly List<Vector2> exhaust = new List<Vector2>();

        public override void SetStaticDefaults()
        {
            // Texture is a vertical strip of 3 rocket frames (14x28 each):
            // identical body, flickering exhaust flame. Vanilla draws the current
            // frame automatically since PreDraw returns true — previously the whole
            // 84px strip drew as one giant sprite.
            Main.projFrames[Projectile.type] = 3;
        }

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 600;
            // DIAGNOSTIC PARITY TEST with GYStarProj (the Guardian star that hits
            // for you): identical collision flags. aiStyle -1, global immunity
            // (no local-immunity lines), unscaled. Homing AI and visuals untouched.
            Projectile.aiStyle = -1;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        // M13: saucer lift so rockets read against the night sky.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(200, 255, 225), 0.3f) * Projectile.Opacity;
        }

        public override void AI()
        {
            // Spawn flash: tight ignition (was 12 random).
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                Lighting.AddLight(Projectile.Center, 0.8f, 1.4f, 0.6f);
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, angle.ToRotationVector2() * 2.4f, 100, default, 1f);
                    dust.noGravity = true;
                }
                for (int i = 0; i < 2; i++)
                {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, Main.rand.NextVector2Circular(1.5f, 1.5f), 100, default, 0.9f);
                    dust.noGravity = true;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 1.0f, 0.3f);

            NPC target = FindNearestEnemy(800f);
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            // MAJOR C (fuse half): proximity fuse arms inside 120px of the mark.
            closeIn = target != null && Vector2.Distance(Projectile.Center, target.Center) <= 120f;

            if (target != null)
            {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                float current = dir.ToRotation();
                float desired = want.ToRotation();
                float diff = MathHelper.WrapAngle(desired - current);
                float maxTurn = MathHelper.ToRadians(15f);
                dir = (current + MathHelper.Clamp(diff, -maxTurn, maxTurn)).ToRotationVector2();
                // Proximity speed control: slow near the target so the turn radius
                // collapses and the missile connects instead of orbiting forever.
                float dist = Vector2.Distance(Projectile.Center, target.Center);
                float closingSpeed = MathHelper.Clamp(dist * 0.06f, 2.5f, 10f);
                Projectile.velocity = dir * closingSpeed;
            }
            else if (Projectile.velocity.LengthSquared() < 0.01f)
            {
                Projectile.velocity = Vector2.UnitX * 10f;
            }
            else
            {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 10f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Exhaust flicker animation (3-frame strip, runs on all clients).
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % 3;
            }

            MiniYoyoFx.RibbonPush(exhaust, Projectile.Center, 8f, 10);

            // Lean exhaust: every other tick (was 1/tick x60 alive).
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, -Projectile.velocity * 0.1f, 120, default, 0.75f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Rocket read: exhaust ribbon + hot nose.
            // M13: saucer lift + MAJOR C (nose half): proximity-fused nose that
            // burns triple-bright inside 120px, with a V-tick warhead mark.
            MiniYoyoFx.RibbonDrawSoft(exhaust, new Color(60, 140, 110), new Color(200, 255, 220), 5f);
            float fuseBoost = closeIn ? 1f : 0.45f;
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f, new Color(120, 255, 170), fuseBoost);
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 8f, 11f, new Color(90, 200, 140), 0.35f);
                Vector2 nose = Projectile.Center - Main.screenPosition;
                Vector2 side = new Vector2(-back.Y, back.X);
                Vector2 vTip = nose + back * 7f;
                Color vTick = new Color(220, 255, 230, 150) * (closeIn ? 0.9f : 0.5f);
                MiniYoyoFx.DrawLine(nose - side * 5f, vTip, 1.5f, vTick);
                MiniYoyoFx.DrawLine(nose + side * 5f, vTip, 1.5f, vTick);
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Explode();
        }

        public override void OnKill(int timeLeft)
        {
            Explode();
        }

        private void Explode()
        {
            if (hasExploded)
                return;

            hasExploded = true;

            // Tight saucer pop: ember core + smoke + electric ring (was 28 random).
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, angle.ToRotationVector2() * 2.6f, 100, default, 1f);
                dust.noGravity = true;
            }

            for (int i = 0; i < 4; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1f), 120, default, 0.9f);
                dust.noGravity = true;
            }

            // Flash ring: expanding electric-green arc ring (visual only).
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, angle.ToRotationVector2() * 3.6f, 100, default, 1f);
                dust.noGravity = true;
            }

            // M15: ion risers over the pop.
            for (int i = 0; i < 2; i++)
            {
                Dust riser = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f), DustID.Electric, new Vector2(0f, -2.5f), 100, default, 0.9f);
                riser.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.9f, 1.4f, 0.5f);

            // Synced vanilla slash sparkles over the pop (free MP sync).
            Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = Projectile.Center }, Projectile.owner);
            YoyoParticles.SpawnBloom(Projectile.Center, 6f, 22f, 20, new Color(140, 255, 180), 0.55f);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
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
