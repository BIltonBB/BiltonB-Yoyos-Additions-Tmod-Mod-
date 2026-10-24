using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class DarkMageYoyoMagicProj : ModProjectile, IYoyoInstantHit
    {
        private readonly List<Vector2> ribbonTrail = new List<Vector2>();
        private bool spawnFxDone;

        // Arcane star (Visuals.DarkMageStar style): a short-lived smooth flare
        // stamped at each hit point. Off = vanilla Excalibur sparkle instead.
        // Life bumped 16 -> 24 ticks so the richer crowns (slash sweep, vortex
        // funnel, sigil draw-in...) have room to read.
        private const int ArcaneStarLife = 24;
        private int arcaneStarTicks;
        private Vector2 arcaneStarPos;
        private float arcaneStarSpin;

        private static Configuration.ArcaneStarStyle GetArcaneStarStyle()
        {
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                return cfg == null ? Configuration.ArcaneStarStyle.Variant1 : cfg.Visuals.DarkMageStar;
            }
            catch { return Configuration.ArcaneStarStyle.Variant1; }
        }

        public override void SetStaticDefaults()
        {
            // Afterimage ghosts: record recent positions/rotations (see Basic Projectile wiki).
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;

            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;

            Projectile.timeLeft = 300;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M7: keep the bolt readable in dark caves without fullbrighting it.
        public override Color? GetAlpha(Color lightColor)
        {
            Color lifted = Color.Lerp(lightColor, new Color(220, 150, 255), 0.3f);
            return lifted * Projectile.Opacity;
        }

        public override void AI()
        {
            // Spawn flash: compact rune pop, mostly radial + a push along flight dir.
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, angle.ToRotationVector2() * 2.6f, 100, default, 1.1f);
                    burst.noGravity = true;
                }
                for (int i = 0; i < 2; i++)
                {
                    Dust push = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, Projectile.velocity * (0.15f + 0.12f * i) + Main.rand.NextVector2Circular(0.6f, 0.6f), 100, default, 0.9f + 0.15f * i);
                    push.noGravity = true;
                }
                Dust nose = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, Projectile.velocity * 0.4f, 100, default, 1f);
                nose.noGravity = true;
                Lighting.AddLight(Projectile.Center, 0.7f, 0.35f, 0.9f);
            }

            // Face movement direction
            if (Projectile.velocity.LengthSquared() > 0.01f)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            MiniYoyoFx.RibbonPush(ribbonTrail, Projectile.Center);

            // Slight homing toward nearest valid enemy (max ~8 degrees per tick)
            NPC target = FindNearestEnemy(700f);
            if (target != null && Projectile.velocity.LengthSquared() > 0.01f)
            {
                float currentAngle = Projectile.velocity.ToRotation();
                float targetAngle = (target.Center - Projectile.Center).ToRotation();
                float maxTurn = MathHelper.ToRadians(8f);
                float diff = MathHelper.WrapAngle(targetAngle - currentAngle);
                float clamped = MathHelper.Clamp(diff, -maxTurn, maxTurn);
                float newAngle = currentAngle + clamped;
                // Proximity speed control: collapse the turn radius near the target
                // so the bolt connects instead of orbiting forever.
                float dist = Vector2.Distance(Projectile.Center, target.Center);
                float boltSpeed = MathHelper.Clamp(dist * 0.06f, 2.5f, 10f);
                Projectile.velocity = newAngle.ToRotationVector2() * boltSpeed;
            }

            // Thinned trail: one directional ember every other tick + rare sparkle.
            // Was: 1 dust every tick + 1 glint every 3 ticks (~80/s per bolt). Now ~35/s.
            if (Projectile.timeLeft % 2 == 0)
            {
                // Wobble offset perpendicular to flight so the tail feels alive, not laser-straight.
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 side = new Vector2(-dir.Y, dir.X);
                float wobble = (float)Math.Sin(Main.GameUpdateCount * 0.35f + Projectile.whoAmI) * 2f;
                Dust trail = Dust.NewDustPerfect(Projectile.Center + side * wobble, DustID.PurpleTorch, -Projectile.velocity * 0.14f + Main.rand.NextVector2Circular(0.3f, 0.3f), 120, default, 0.9f);
                trail.noGravity = true;
            }
            if (Main.rand.NextBool(6))
            {
                Dust glint = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(3f, 3f), DustID.MagicMirror, -Projectile.velocity * 0.05f, 100, default, 0.7f);
                glint.noGravity = true;
            }

            // M11: end-of-life fade so bolts wink out instead of popping.
            if (Projectile.timeLeft < 30)
                Projectile.alpha = (int)(255f * (1f - Projectile.timeLeft / 30f));
            else
                Projectile.alpha = 0;

            if (arcaneStarTicks > 0)
                arcaneStarTicks--;

            Lighting.AddLight(Projectile.Center, 0.5f, 0.2f, 0.6f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // MAJOR C (with M8/M9): arcane comet anatomy.
            // - Dual ribbon (wide dim outer + hot core) breathing with speed.
            // - Vanilla oldPos afterimage ghosts (ExampleMod pattern).
            // - Hot head + paler neck + dim tail knot along -vel.
            // - Cross-flare ticks through the head (Calamity HolyLight-inspired, 2 lines).
            float speed = Projectile.velocity.Length();
            MiniYoyoFx.RibbonDrawSoft(ribbonTrail, new Color(175, 55, 215), new Color(255, 208, 245), MathHelper.Clamp(5f + speed * 0.18f, 5f, 8f));

            DrawAfterimages(lightColor);

            float opacity = Projectile.Opacity;
            MiniYoyoFx.DrawGlow(Projectile.Center, 22f, new Color(220, 120, 255), 0.4f * opacity);
            if (speed * speed > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 10f, 14f, new Color(200, 130, 245), 0.35f * opacity);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 20f, 9f, new Color(170, 90, 220), 0.3f * opacity);

                // Cross-flare: two short bright ticks through the head, aligned + perpendicular to flight.
                Vector2 screen = Projectile.Center - Main.screenPosition;
                Vector2 fwd = back;
                Vector2 side = new Vector2(-fwd.Y, fwd.X);
                Color flare = new Color(255, 220, 255, 140) * (0.45f * opacity);
                MiniYoyoFx.DrawLine(screen - fwd * 7f, screen + fwd * 7f, 1.5f, flare);
                MiniYoyoFx.DrawLine(screen - side * 7f, screen + side * 7f, 1.5f, flare);
            }

            // Arcane star stamped at the last hit (style read live, so changing
            // the config style applies immediately). Off draws nothing here.
            if (arcaneStarTicks > 0)
            {
                Configuration.ArcaneStarStyle starStyle = GetArcaneStarStyle();
                if (starStyle != Configuration.ArcaneStarStyle.Off)
                {
                    float t = arcaneStarTicks / (float)ArcaneStarLife;   // 1 -> 0 across the flare's life
                    float starAlpha = MathHelper.Clamp(t * 1.4f, 0f, 1f);
                    float starScale = 0.80f + 0.72f * t;
                    // Life progress (0 -> 1) drives the style's own motion envelope
                    // (snap in, then a per-style pulse); arcaneStarSpin is now just
                    // the per-stamp orientation, so nothing spins continuously.
                    MiniYoyoFx.DrawArcaneStar(arcaneStarPos, starScale, starAlpha, 1f - t, starStyle, arcaneStarSpin);
                }
            }
            return true;
        }

        private void DrawAfterimages(Color lightColor)
        {
            if (Projectile.oldPos == null || Projectile.oldPos.Length < 2)
                return;
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 halfHitbox = new Vector2(Projectile.width, Projectile.height) * 0.5f;
            Vector2 origin = tex.Size() * 0.5f;
            int ghosts = Math.Min(4, Projectile.oldPos.Length - 1);
            for (int k = ghosts; k >= 1; k--)
            {
                Vector2 oldCenter = Projectile.oldPos[k] + halfHitbox;
                if (oldCenter.LengthSquared() < 1f)
                    continue;
                float age = 1f - k / (float)(ghosts + 1);
                Color color = (GetAlpha(lightColor) ?? lightColor) * (0.3f * age);
                float rot = (Projectile.oldRot != null && k < Projectile.oldRot.Length) ? Projectile.oldRot[k] : Projectile.rotation;
                Main.EntitySpriteDraw(tex, oldCenter - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                    null, color, rot, origin, Projectile.scale, SpriteEffects.None, 0);
            }
        }

        // M10: tile-kiss sparks. Return true so vanilla tile behavior (stop/die) is preserved.
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            for (int i = 0; i < 4; i++)
            {
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, Main.rand.NextVector2Circular(2f, 2f), 100, default, 0.9f);
                spark.noGravity = true;
            }
            if (GetArcaneStarStyle() != Configuration.ArcaneStarStyle.Off)
                MiniYoyoFx.FlashFX(Projectile.Center, new Color(190, 120, 255), 0.8f);
            Lighting.AddLight(Projectile.Center, 0.5f, 0.25f, 0.7f);
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Hit star. With Visuals.DarkMageStar set to a variant, stamp the
            // mod's smooth arcane flare (drawn by this bolt, so it never depends
            // on the vanilla particle orchestrator). Off requests the synced
            // vanilla Excalibur sparkle instead.
            Configuration.ArcaneStarStyle starStyle = GetArcaneStarStyle();
            // The 37-41 pulse set must not read as a shockwave, so it opts out of
            // EVERY expanding element on the hit: the pooled expanding ring
            // (YoyoParticles.SpawnRing) AND the growing bloom orb
            // (YoyoParticles.SpawnBloom). Those two were shared by all styles,
            // which is exactly why 37-41 kept looking like they had a shockwave
            // no matter what the crown body drew. Its stamp is dust / small
            // gravity-free droplets instead (SpawnArcaneStarBurst).
            bool pulseStar = (int)starStyle >= (int)Configuration.ArcaneStarStyle.Variant37;
            if (starStyle != Configuration.ArcaneStarStyle.Off)
            {
                Color accent = MiniYoyoFx.ArcaneStarAccent(starStyle);
                arcaneStarTicks = ArcaneStarLife;
                arcaneStarPos = target.Center;
                arcaneStarSpin = (float)Main.rand.NextDouble() * MathHelper.TwoPi;
                if (!pulseStar)
                {
                    // Stamp beat: one pooled ring + brighter flash so the crown
                    // lands on the hit frame instead of only fading in after.
                    YoyoParticles.SpawnRing(target.Center, 8f, 46f, 18, accent, 2.2f);
                    MiniYoyoFx.FlashFX(target.Center, Color.Lerp(accent, Color.White, 0.4f), 1.1f);
                }
                else
                {
                    // Pulse set: spark kiss only, no ring.
                    MiniYoyoFx.FlashFX(target.Center, Color.Lerp(accent, Color.White, 0.45f), 0.8f);
                }
                // Extra particle layer per style ("more VFX" without changing the
                // crown's shape): a style-matched spark/mote burst at the stamp.
                MiniYoyoFx.SpawnArcaneStarBurst(target.Center, starStyle, 1f);
            }
            else
            {
                Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                    new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = target.Center }, Projectile.owner);
            }
            if (!pulseStar)
                YoyoParticles.SpawnBloom(target.Center, 5f, 22f, 20, new Color(220, 150, 255), 0.55f);
            // Contact tick: 4 alternating sparks snapped to the hit frame.
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + Main.GameUpdateCount * 0.1f;
                int dustType = i % 2 == 0 ? DustID.MagicMirror : DustID.PurpleTorch;
                Dust kiss = Dust.NewDustPerfect(target.Center, dustType, angle.ToRotationVector2() * 2.2f, 100, default, 0.9f);
                kiss.noGravity = true;
            }
            Lighting.AddLight(target.Center, 0.6f, 0.3f, 0.8f);
        }

        public override void OnKill(int timeLeft)
        {
            // Tight detonation: pink core + purple edge, directional + light flash.
            // (No DrawGlow here: drawing is only safe in PreDraw/PostDraw.)
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, angle.ToRotationVector2() * 2.4f, 100, default, 1.1f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, angle.ToRotationVector2() * 1.6f, 100, default, 1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.7f, 0.35f, 0.9f);
        }

        private NPC FindNearestEnemy(float maxRange)
        {
            NPC nearestNPC = null;
            float maxRangeSq = maxRange * maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (MiniYoyoFx.IsAttackable(npc))
                {
                    float distanceSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                    if (distanceSq <= maxRangeSq)
                    {
                        maxRangeSq = distanceSq;
                        nearestNPC = npc;
                    }
                }
            }

            return nearestNPC;
        }
    }
}
