using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class DreadnautilusHomingBloodSquids : ModProjectile, IYoyoInstantHit
    {
        private const int MaxHP = 50;
        private const int IFrameDuration = 30;
        private const float HomeSpeed = 7f;
        private const float MaxTurnRadians = 0.087f; // ~5deg/tick

        private int hp = MaxHP;
        private int iframeTimer;
        private bool spawnFxDone;
        private readonly List<Vector2> wake = new List<Vector2>();

        // Spritesheet is 6 frames of 44x70 (verified by row scan: cuts at
        // 70/140/210/280/350 — NOT 10x44x42, which scrolled through mid-art).
        // Uniform anchor at the true art center; flip + bank locomotion.
        private static readonly Vector2 UniformOrigin = new Vector2(22f, 35f);

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 6;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;

            // Smaller by 1.5x: art reads cleaner while homing.
            Projectile.scale = 0.67f;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 2;
            Projectile.tileCollide = false;

            Projectile.timeLeft = 300;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 8; i++)
                {
                    float angle = MathHelper.TwoPi * i / 8f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, angle.ToRotationVector2() * 2.6f, 100, default, 1.1f);
                    dust.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.25f, 0.25f);
            }

            Lighting.AddLight(Projectile.Center, 0.75f, 0.1f, 0.12f);

            // Heartbeat pulse: beats faster as HP drops.
            float hpFrac = MathHelper.Clamp(hp / (float)MaxHP, 0f, 1f);
            float beat = (float)Math.Sin(Main.GlobalTimeWrappedHourly * (4f + (1f - hpFrac) * 6f)) * 0.15f;
            Lighting.AddLight(Projectile.Center, 0.2f + beat + (1f - hpFrac) * 0.2f, 0.03f, 0.03f);

            // Animate: 6 frames of 44x70, calm 10-tick pace so the pulse reads
            // as a swim instead of a scroll.
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 10)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= 6)
                    Projectile.frame = 0;
            }

            // Fade at lifetime end
            if (Projectile.timeLeft < 60)
                Projectile.alpha = (int)(255f * (1f - Projectile.timeLeft / 60f));

            if (iframeTimer > 0)
                iframeTimer--;

            // Vanilla-style HP: hostile projectiles within 24px deal damage
            if (iframeTimer <= 0 && Main.myPlayer == Projectile.owner)
            {
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile other = Main.projectile[i];
                    if (!other.active || !other.hostile || other.friendly)
                        continue;
                    if (Vector2.Distance(Projectile.Center, other.Center) <= 24f)
                    {
                        hp -= Math.Max(1, other.damage);
                        iframeTimer = IFrameDuration;
                        Projectile.netUpdate = true;
                        for (int d = 0; d < 6; d++)
                        {
                            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1f);
                            dust.noGravity = true;
                        }
                        break;
                    }
                }
            }

            if (hp <= 0)
            {
                BloodBurst();
                Projectile.Kill();
                return;
            }

            // Slow homing toward nearest enemy
            NPC target = FindNearestEnemy(700f);
            if (target != null)
            {
                // Proximity speed control: slow near the target so the wide turn
                // radius collapses and the squid connects instead of orbiting.
                float distToTarget = Vector2.Distance(Projectile.Center, target.Center);
                float chaseSpeed = MathHelper.Clamp(distToTarget * 0.05f, 1.5f, HomeSpeed);
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * chaseSpeed;
                if (Projectile.velocity.LengthSquared() < 0.01f)
                {
                    Projectile.velocity = desired;
                }
                else
                {
                    Vector2 current = Projectile.velocity;
                    float currentRot = current.ToRotation();
                    float desiredRot = desired.ToRotation();
                    // Rotate by clamped angle difference (~5deg/tick max)
                    float diff = MathHelper.WrapAngle(desiredRot - currentRot);
                    float clamped = MathHelper.Clamp(diff, -MaxTurnRadians, MaxTurnRadians);
                    float newRot = currentRot + clamped;
                    Projectile.velocity = newRot.ToRotationVector2() * chaseSpeed;
                }
            }
            else if (Projectile.velocity.LengthSquared() < 0.01f)
            {
                Projectile.velocity = Vector2.UnitX * HomeSpeed * 0.3f;
            }

            if (Projectile.velocity.LengthSquared() > 0.01f)
            {
                // Vanilla squid locomotion: side-view flip + banking tilt, never
                // a full spin (the vertical art reads as a spinning log otherwise).
                // PreDraw mirrors this; rotation is left at 0.
                Projectile.rotation = 0f;
            }

            // Lean squid trail: one directional mote every other tick.
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, -Projectile.velocity * 0.06f, 120, default, 0.9f);
                trail.noGravity = true;
            }

            // MAJOR C (wake half): motion ribbon following the hunt.
            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 8f, 12);

            // MAJOR C (frenzy half): bleeding-out squids drip as they hunt.
            if (hp < MaxHP / 2 && Main.rand.NextBool(8))
                MiniYoyoFx.SpawnBloodDrip(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), Main.rand.NextVector2Circular(0.5f, 0.5f), 0.9f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // MAJOR C (predator half): dark-red wake ribbon + frenzy glow.
            // Frenzy reads off local hp (same class of approximation as the HP bar).
            float speed = Projectile.velocity.Length();
            MiniYoyoFx.RibbonDrawSoft(wake, new Color(120, 20, 30), new Color(230, 90, 100), MathHelper.Clamp(4f + speed * 0.15f, 4f, 7f));
            bool frenzy = hp < MaxHP / 2;
            if (frenzy)
                MiniYoyoFx.DrawGlow(Projectile.Center, 26f, new Color(255, 70, 80), 0.4f);
            else
                MiniYoyoFx.DrawGlow(Projectile.Center, 18f, new Color(200, 50, 60), 0.3f);
            // Frame-history ghosts: past hunt poses on their own art centers.
            // Flip + bank matches the live draw below (vanilla squid locomotion).
            SpriteEffects flip = Projectile.velocity.X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float bank = MathHelper.Clamp(Projectile.velocity.Y * 0.02f, -0.25f, 0.25f);
            if (Projectile.oldPos != null && Projectile.oldPos.Length > 2)
            {
                Texture2D ghostTex = TextureAssets.Projectile[Projectile.type].Value;
                Vector2 halfBox = new Vector2(Projectile.width, Projectile.height) * 0.5f;
                for (int k = 2; k >= 1; k--)
                {
                    if (k >= Projectile.oldPos.Length)
                        continue;
                    Vector2 oldCenter = Projectile.oldPos[k] + halfBox;
                    if (oldCenter.LengthSquared() < 1f)
                        continue;
                    Rectangle ghostRect = ghostTex.Frame(1, 6, 0, Projectile.frame);
                    Main.EntitySpriteDraw(ghostTex, oldCenter - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                        ghostRect, lightColor * Projectile.Opacity * 0.2f, bank, UniformOrigin, Projectile.scale, flip, 0);
                }
            }
            // Frame-accurate sprite draw on the uniform art center.
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle frameRect = texture.Frame(1, 6, 0, Projectile.frame);
            Vector2 origin = UniformOrigin;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                frameRect, lightColor * Projectile.Opacity, bank, origin, Projectile.scale, flip, 0);
            return false; // vanilla animated sprite replaced by the above
        }

        public override void PostDraw(Color lightColor)
        {
            // Vanilla-style health bar above squid using MagicPixel
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 barPos = Projectile.Center - Main.screenPosition + new Vector2(-20f, -30f);
            int barWidth = 40;
            int barHeight = 5;

            float frac = MathHelper.Clamp(hp / (float)MaxHP, 0f, 1f);

            // Background (blinks red below quarter hull so the kill reads coming).
            Color bed = Color.Black * 0.8f;
            if (frac < 0.25f)
                bed = Color.Red * (0.5f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * 0.3f));
            Main.spriteBatch.Draw(pixel, new Rectangle((int)barPos.X, (int)barPos.Y, barWidth, barHeight), bed);
            // HP fill (red -> green not needed; use red)
            Main.spriteBatch.Draw(pixel, new Rectangle((int)barPos.X + 1, (int)barPos.Y + 1, (int)((barWidth - 2) * frac), barHeight - 2), Color.Red);
        }

        public override void OnKill(int timeLeft)
        {
            BloodBurst();
        }

        private void BloodBurst()
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = MathHelper.TwoPi * i / 12f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, angle.ToRotationVector2() * 3f, 100, default, 1.1f);
                dust.noGravity = true;
            }
            // Abyssal ink under the blood: 4 slow black risers.
            for (int i = 0; i < 4; i++)
            {
                Dust ink = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Smoke, new Vector2(0f, -1.2f), 120, default, 1f);
                ink.noGravity = true;
            }
            // Synced vanilla slash sparkles over the burst (free MP sync).
            Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = Projectile.Center }, Projectile.owner);
            YoyoParticles.SpawnBloom(Projectile.Center, 6f, 24f, 22, new Color(230, 90, 100), 0.55f, 0.35f);
            Lighting.AddLight(Projectile.Center, 1f, 0.15f, 0.15f);
        }

        private NPC FindNearestEnemy(float maxRange)
        {
            NPC best = null;
            float bestDistSq = maxRange * maxRange;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (MiniYoyoFx.IsAttackable(npc))
                {
                    float dSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                    if (dSq <= bestDistSq)
                    {
                        bestDistSq = dSq;
                        best = npc;
                    }
                }
            }
            return best;
        }
    }
}
