using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Martian saucer yoyo. 10s cycle: Missile phase (0-3s, volley of 5 left + 5 right
    /// homing missiles at cycle start) -> Laser phase (3-8s, fires at the target's recorded
    /// position AT FIRE TIME every 0.1s, no tracking after) -> DeathRay instant at cycle end.
    /// Major overhaul: phase transition shockwaves, enhanced beam rendering, holographic
    /// telegraphs, missile launch FX, laser muzzle flashes, death ray charge-up.
    /// </summary>
    public class MartianYoyoProj : ModProjectile
    {
        private const int MissileEnd = 180;
        private const int LaserEnd = 480;
        private const int CycleEnd = 600;

        private int cycleTimer;
        private readonly List<Vector2> wake = new List<Vector2>();
        private int hoverStill; // crop-circle timer (visual only)

        // Phase transition FX state
        private int phaseFxTimer;
        private Color phaseFxColor;

        // K2: ghost reticle memory at the last laser aim point (synced in AI).
        private Vector2 ghostTarget;
        private int ghostTimer;

        // K1: announced-volley lane (pre-fire aim preview).
        private Vector2 forecastTo;
        private int forecastTimer;

        public override void SetStaticDefaults()
        {
            // Afterimage ghosts for the saucer (ExampleMod Basic Projectile pattern).
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 300f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 15f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 16f;
        }

        // M1: saucer lift so the hull reads against the night sky.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(phaseFxTimer);
            writer.Write(phaseFxColor.R);
            writer.Write(phaseFxColor.G);
            writer.Write(phaseFxColor.B);
            writer.WriteVector2(ghostTarget);
            writer.Write(ghostTimer);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            phaseFxTimer = reader.ReadInt32();
            phaseFxColor = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            ghostTarget = reader.ReadVector2();
            ghostTimer = reader.ReadInt32();
        }

        public override void AI()
        {
            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 10f, 12);
            MiniYoyoFx.ApplyMotionReadability(Projectile);

            // M3: deterministic saucer spin (square hitbox; never affects collision).
            Projectile.rotation += 0.13f;

            // Phase transition FX timer
            if (phaseFxTimer > 0)
                phaseFxTimer--;
            if (ghostTimer > 0)
                ghostTimer--;
            if (forecastTimer > 0)
                forecastTimer--;

            bool missilePhase = cycleTimer < MissileEnd;
            bool laserPhase = cycleTimer >= MissileEnd && cycleTimer < LaserEnd;
            bool chargePhase = cycleTimer >= LaserEnd;

            // Phase-specific lighting (major overhaul: sci-fi identity)
            if (missilePhase)
                Lighting.AddLight(Projectile.Center, 0.2f, 0.8f, 0.6f);
            else if (laserPhase)
                Lighting.AddLight(Projectile.Center, 0.3f, 1.2f, 0.4f);
            else
                Lighting.AddLight(Projectile.Center, 0.8f, 1.5f, 1.2f);

            // Saucer aura phases: thin alternating spark every 4th tick (was ~40/s).
            // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
            if (Main.GameUpdateCount % 4 == 0)
            {
                int dustType = chargePhase ? DustID.MagicMirror : DustID.Electric;
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f), dustType, Vector2.Zero, 120, default, 0.85f);
                dust.noGravity = true;
            }
            if (Projectile.velocity.LengthSquared() < 1f)
            {
                hoverStill++;
            }
            else
            {
                hoverStill = 0;
            }

            // M5: fast-move ion spray kicked behind the saucer at flank speed.
            if (Projectile.velocity.LengthSquared() > 49f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust ion = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.Electric, -back * 1.4f + Main.rand.NextVector2Circular(0.4f, 0.4f), 120, default, 0.8f);
                ion.noGravity = true;
            }

            // M4: electric string shimmer along the player->saucer line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.Electric, new Vector2(0f, -0.4f), 140, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            // Charge phase: intensifying glow (major overhaul)
            if (chargePhase && Main.GameUpdateCount % 3 == 0)
            {
                Dust charge = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f), DustID.MagicMirror, Vector2.Zero, 80, default, 0.7f);
                charge.noGravity = true;
                Lighting.AddLight(Projectile.Center, 0.5f, 0.8f, 0.6f);
            }

            // M7: charge inhale streaks converging as the ray ripens.
            if (chargePhase && Main.GameUpdateCount % 6 == 0)
            {
                Vector2 edge = Projectile.Center + Main.rand.NextVector2Circular(30f, 30f);
                Dust inhale = Dust.NewDustPerfect(edge, DustID.MagicMirror, (Projectile.Center - edge) * 0.1f, 80, default, 0.9f);
                inhale.noGravity = true;
            }

            cycleTimer++;

            // Timer bookkeeping runs on ALL clients (spawns stay owner-gated below),
            // so remotes track phases exactly instead of drifting after cycle 1.
            if (cycleTimer == 1)
            {
                if (Main.myPlayer == Projectile.owner)
                    FireMissileVolley();
                // Phase transition FX: missile volley
                phaseFxTimer = 20;
                phaseFxColor = new Color(40, 200, 180);
                if (Main.myPlayer == Projectile.owner)
                    Projectile.netUpdate = true; // M6: remotes get the ring via ExtraAI
                MiniYoyoFx.Shake(1, 8);
                MiniYoyoFx.FlashFX(Projectile.Center, new Color(40, 200, 180));
            }
            else if (cycleTimer == MissileEnd - 10)
            {
                // E1/K1: the laser volley announces itself - rope tauts, aimed
                // lane previews (target is only read, never locked).
                YoyoStringHookSystem.AnnounceVolley(Projectile, 10);
                NPC forecast = FindNearestEnemy(900f);
                if (forecast != null)
                {
                    forecastTo = forecast.Center;
                    forecastTimer = 10;
                }
            }
            else if (cycleTimer == MissileEnd)
            {
                // Phase transition FX: laser phase begins
                phaseFxTimer = 25;
                phaseFxColor = new Color(50, 255, 120);
                MiniYoyoFx.Shake(2, 10);
                if (Main.myPlayer == Projectile.owner)
                {
                    Projectile.netUpdate = true; // M6: remotes get the ring via ExtraAI
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 8f;
                        Dust pulse = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, angle.ToRotationVector2() * 4f, 100, default, 1f);
                        pulse.noGravity = true;
                    }
                }
            }
            else if (cycleTimer == LaserEnd - 10)
            {
                // E1: the death ray gets the same tell, 10 ticks out.
                YoyoStringHookSystem.AnnounceVolley(Projectile, 10);
            }
            else if (cycleTimer == LaserEnd)
            {
                // Phase transition FX: death ray charge
                phaseFxTimer = 30;
                phaseFxColor = new Color(200, 255, 240);
                MiniYoyoFx.Shake(3, 15);
                MiniYoyoFx.FlashFX(Projectile.Center, new Color(200, 255, 240), 1.2f);
                MiniYoyoFx.ImplodeFX(Projectile.Center, new Color(100, 255, 220), 1.2f);
                Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item15, Projectile.Center);
                Lighting.AddLight(Projectile.Center, 1.5f, 2f, 1.8f);
                if (Main.myPlayer == Projectile.owner)
                    Projectile.netUpdate = true;
            }
            else if (cycleTimer >= MissileEnd && cycleTimer < LaserEnd && (cycleTimer - MissileEnd) % 6 == 0)
            {
                if (Main.myPlayer == Projectile.owner)
                    FireLaser();
            }
            else if (cycleTimer >= CycleEnd)
            {
                if (Main.myPlayer == Projectile.owner)
                {
                    FireDeathRay();
                    Projectile.netUpdate = true;
                }
                cycleTimer = 0;
            }
        }

        private void FireMissileVolley()
        {
            // Bias the fan toward a real target (Guardian-style directed fire);
            // pure random only with nothing attackable around.
            float baseAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            NPC aimTarget = null;
            float bestDistSq = 900f * 900f;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (MiniYoyoFx.IsAttackable(npc))
                {
                    float dSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        aimTarget = npc;
                    }
                }
            }
            if (aimTarget != null)
                baseAngle = (aimTarget.Center - Projectile.Center).ToRotation();
            int missileType = ModContent.ProjectileType<MartianYoyoMissilesProj>();
            int damage = Math.Max(1, Projectile.damage / 3);

            for (int i = 0; i < 5; i++)
            {
                // 45-degree spacing on each side; right side offset so all 10 differ.
                float leftAngle = baseAngle + i * MathHelper.PiOver4;
                float rightAngle = baseAngle + MathHelper.PiOver4 / 2f + i * MathHelper.PiOver4;

                // Missile launch FX: muzzle flash per missile (major overhaul)
                Vector2 leftPos = Projectile.Center + new Vector2(-30f, 0f);
                Vector2 rightPos = Projectile.Center + new Vector2(30f, 0f);
                Dust leftFlash = Dust.NewDustPerfect(leftPos, DustID.Electric, leftAngle.ToRotationVector2() * 3f, 80, default, 0.9f);
                leftFlash.noGravity = true;
                Dust rightFlash = Dust.NewDustPerfect(rightPos, DustID.Electric, rightAngle.ToRotationVector2() * 3f, 80, default, 0.9f);
                rightFlash.noGravity = true;

                Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(),
                    leftPos, leftAngle.ToRotationVector2() * 8f, missileType, damage, Projectile.knockBack, Projectile.owner);

                Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(),
                    rightPos, rightAngle.ToRotationVector2() * 8f, missileType, damage, Projectile.knockBack, Projectile.owner);
            }

            // H3 note: the missile fan used to speak here; removed because the
            // fan repeats every cycle (noise pollution).
        }

        private void FireLaser()
        {
            NPC target = FindNearestEnemy(900f);
            if (target == null)
            {
                // B4: no target = a dim search beat, not silence.
                MiniYoyoFx.StyledMiss(Projectile.Center, new Color(120, 255, 200));
                return;
            }

            // Record the target position AT FIRE TIME; the laser does not track after.
            Vector2 storedPosition = target.Center;
            Vector2 dir = (storedPosition - Projectile.Center).SafeNormalize(Vector2.UnitX);

            // K2: leave a decaying ghost reticle at the aim point (synced).
            ghostTarget = storedPosition;
            ghostTimer = 40;
            Projectile.netUpdate = true;

            // Laser muzzle flash (major overhaul)
            Dust muzzle = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, dir * 4f, 60, default, 1f);
            muzzle.noGravity = true;
            // M8: second longer spark + snap ring over the muzzle.
            Dust lance = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, dir * 7f, 60, default, 0.9f);
            lance.noGravity = true;
            Lighting.AddLight(Projectile.Center, 0.5f, 1f, 0.3f);

            Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, dir * 16f,
                ModContent.ProjectileType<MartianYoyoLaserProj>(),
                Math.Max(1, Projectile.damage / 3), Projectile.knockBack, Projectile.owner);

            // L2: the shot leaves a bank bolt drag + a dark muzzle anchor.
            MiniBossShapeBank.Spawn(ShapeId.BoltZig, Projectile.Center + dir * 14f, dir.ToRotation() - MathHelper.PiOver2,
                new Vector2(0.30f, 0.5f), new Color(120, 255, 200) * 0.8f, 9, dir * 3.4f);
            MiniBossShapeBank.Spawn(ShapeId.SplatSoft, Projectile.Center, 0f, 0.3f, new Color(10, 14, 12) * 0.5f, 10);
        }

        private void FireDeathRay()
        {
            // Beam is yoyo-attached: it rides the yoyo and sweeps with it.
            // Direction is random at spawn (ai1); the beam never travels.
            float beamAngle = Main.rand.NextFloat(MathHelper.TwoPi);

            // Death ray fire FX: massive energy discharge (major overhaul)
            if (Main.myPlayer == Projectile.owner)
            {
                MiniYoyoFx.Shake(5, 20);
                for (int i = 0; i < 16; i++)
                {
                    float angle = MathHelper.TwoPi * i / 16f;
                    Dust discharge = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, angle.ToRotationVector2() * 6f, 80, default, 1.3f);
                    discharge.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 2f, 2.5f, 2.2f);
            }

            Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<MartianYoyoDeathRayProj>(),
                Math.Max(1, Projectile.damage / 2), Projectile.knockBack, Projectile.owner, Projectile.whoAmI, beamAngle);
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
                    float dSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                    if (dSq <= maxRangeSq)
                    {
                        maxRangeSq = dSq;
                        nearest = npc;
                    }
                }
            }

            return nearest;
        }

        // M9: static tick for the saucer hull (dust + light only; damage untouched).
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MiniYoyoFx.ImpactFX(target.Center, new Color(150, 255, 190), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + Main.GameUpdateCount * 0.1f;
                Dust tick = Dust.NewDustPerfect(target.Center, DustID.Electric, angle.ToRotationVector2() * 2.6f, 100, default, 1f);
                tick.noGravity = true;
            }
            Lighting.AddLight(target.Center, 0.4f, 1f, 0.6f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Saucer wake + phase-tinted seat (missiles teal / lasers green / ray white-teal).
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type);
            DrawAfterimages(lightColor);
            bool laserPhase = cycleTimer >= MissileEnd && cycleTimer < LaserEnd;
            bool chargePhase = cycleTimer >= LaserEnd;
            Color seat = chargePhase ? new Color(200, 255, 235) : laserPhase ? new Color(90, 255, 150) : new Color(90, 220, 200);
            float seatPulse = 0.32f + 0.08f * (float)Math.Sin(Main.GameUpdateCount * 0.12f);
            MiniYoyoFx.DrawGlow(Projectile.Center, chargePhase ? 28f : 24f, seat, chargePhase ? 0.5f : seatPulse);

            // Deck presence: 4 phase-tinted lamps chasing in sequence while the
            // ray charges (brightness wave runs lamp to lamp, wall-clock synced).
            Color deckLamp = chargePhase ? new Color(220, 255, 240) : laserPhase ? new Color(110, 255, 150) : new Color(90, 220, 200);
            for (int i = 0; i < 4; i++)
            {
                float ang = Main.GameUpdateCount * 0.08f + MathHelper.TwoPi * i / 4f;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 lampPos = Projectile.Center + dir * 22f;
                float chase = chargePhase ? 0.3f + 0.35f * (0.5f + 0.5f * (float)System.Math.Sin(Main.GameUpdateCount * 0.25f - i * 1.5708f)) : 0.45f;
                MiniYoyoFx.DrawGlow(lampPos, 8f, deckLamp, chase);
                Vector2 side = new Vector2(-dir.Y, dir.X);
                MiniYoyoFx.DrawLine(lampPos - Main.screenPosition, lampPos - side * 12f - Main.screenPosition, 1.5f, deckLamp * 0.5f);
            }

            // Phase transition shockwave double ring (major overhaul).
            if (phaseFxTimer > 0)
            {
                float totalDuration = 30f;
                float elapsed = totalDuration - phaseFxTimer;
                MiniYoyoFx.DrawShockwaveRing(Projectile.Center, 8f, 70f, totalDuration, elapsed, phaseFxColor, 4f);
                MiniYoyoFx.DrawShockwaveRing(Projectile.Center, 5f, 45f, totalDuration * 0.7f, elapsed * 1.4f, Color.Lerp(Color.White, phaseFxColor, 0.4f), 2f);
                // Star flare crowning fresh transitions.
                if (phaseFxTimer > 20)
                    MiniYoyoFx.DrawStarSparkle(Projectile.Center, new Color(220, 255, 240), 1.4f, 0f, phaseFxTimer / 30f);
            }

            // K1: the announced laser lane (pre-fire intent, gone at release).
            if (forecastTimer > 0)
                MiniYoyoFx.DrawForecastLane(Projectile.Center, forecastTo, new Color(120, 255, 200), forecastTimer / 10f * 0.8f);

            // K2: post-fire memory - the last aim point lingers as a fading
            // dashed reticle, so a dodged laser reads as "aimed there".
            if (ghostTimer > 0)
            {
                float g = ghostTimer / 40f;
                MiniYoyoFx.DrawDashedRing(ghostTarget, 12f + 8f * g, 1.5f, new Color(120, 255, 200) * (0.55f * g), 8, Main.GameUpdateCount * 0.08f);
                MiniYoyoFx.DrawGlow(ghostTarget, 12f, new Color(120, 255, 200), 0.28f * g);
            }

            // Holographic telegraph during charge phase (major overhaul).
            // MAJOR A (countdown half): converging tick lines join the holo rings.
            if (chargePhase && cycleTimer > LaserEnd + 10)            {
                float chargeProgress = (float)(cycleTimer - LaserEnd) / (CycleEnd - LaserEnd);
                float ringRadius = 40f - 20f * chargeProgress;
                Color holoColor = new Color(100, 255, 220, (byte)(100 + 100 * chargeProgress));
                MiniYoyoFx.DrawRing(Projectile.Center, ringRadius, 2f, holoColor, 24);
                // Rotating inner ring
                MiniYoyoFx.DrawRing(Projectile.Center, ringRadius * 0.6f, 1.5f, holoColor * 0.7f, 20);
                for (int i = 0; i < 4; i++)
                {
                    float ang = Main.GameUpdateCount * 0.06f + MathHelper.TwoPi * i / 4f;
                    Vector2 outer = Projectile.Center + ang.ToRotationVector2() * ringRadius - Main.screenPosition;
                    Vector2 inner = Projectile.Center + ang.ToRotationVector2() * (ringRadius * (1f - 0.4f * chargeProgress)) - Main.screenPosition;
                    MiniYoyoFx.DrawLine(outer, inner, 1.5f, holoColor * (0.4f + 0.4f * chargeProgress));
                }
                // Searchlight sweep: twin beams rotate while the ray aims, then
                // park as the rings close (first half of charge only).
                if (chargeProgress < 0.5f)
                {
                    float sweep = Main.GameUpdateCount * 0.15f;
                    float sweepAlpha = 0.35f * (1f - chargeProgress * 2f);
                    for (int s = 0; s < 2; s++)
                    {
                        Vector2 tip = Projectile.Center + (sweep + s * MathHelper.Pi).ToRotationVector2() * 70f;
                        MiniYoyoFx.DrawLine(Projectile.Center - Main.screenPosition, tip - Main.screenPosition, 2f, new Color(100, 255, 220) * sweepAlpha);
                    }
                }
            }

            // MAJOR C (ports half): launch-port lights circling while the wolfpack
            // is out (wall-clock orbit; the fan bearings stay owner-side).
            if (!laserPhase && !chargePhase)
            {
                for (int i = 0; i < 10; i++)
                {
                    float ang = -Main.GameUpdateCount * 0.05f + MathHelper.TwoPi * i / 10f;
                    MiniYoyoFx.DrawGlow(Projectile.Center + ang.ToRotationVector2() * 30f, 6f, new Color(90, 220, 200), 0.35f);
                }
                // Side-pod ignition: twin muzzle glows + outward jets as the pack
                // leaves the pods (first 30 ticks; cycleTimer runs everywhere).
                if (cycleTimer < 30)
                {
                    float t = cycleTimer / 30f;
                    Vector2 portL = Projectile.Center + new Vector2(-30f, 0f);
                    Vector2 portR = Projectile.Center + new Vector2(30f, 0f);
                    MiniYoyoFx.DrawGlow(portL, 14f * (1f - t) + 4f, new Color(120, 255, 200), 0.55f * (1f - t) + 0.15f);
                    MiniYoyoFx.DrawGlow(portR, 14f * (1f - t) + 4f, new Color(120, 255, 200), 0.55f * (1f - t) + 0.15f);
                    Vector2 l0 = portL - Main.screenPosition;
                    Vector2 r0 = portR - Main.screenPosition;
                    Color jet = new Color(170, 255, 220, 150) * (0.6f * (1f - t) + 0.2f);
                    MiniYoyoFx.DrawLine(l0, l0 + new Vector2(-14f - 10f * t, 0f), 2f, jet);
                    MiniYoyoFx.DrawLine(r0, r0 + new Vector2(14f + 10f * t, 0f), 2f, jet);
                }
            }

            // Laser targeting telegraph (major overhaul): shows where lasers will fire
            if (laserPhase)
            {
                NPC target = FindNearestEnemy(900f);
                if (target != null)
                {
                    // Target indicator ring
                    Color targetColor = new Color(50, 255, 120, 80);
                    MiniYoyoFx.DrawRing(target.Center, 20f, 1.5f, targetColor, 20);
                    // Line from yoyo to target
                    MiniYoyoFx.DrawLine(Projectile.Center - Main.screenPosition, target.Center - Main.screenPosition, 1f, new Color(50, 255, 120, 40));
                }
            }

            return true;
        }

        public override void PostDraw(Color lightColor)
        {
            // No blob glow. Seat + lights + _Glow carry identity.
            // Aura budget (C1): fury ring cut - belongs to Ogre/Betsy/DarkMage now.
        }

        private void DrawAfterimages(Color lightColor)
        {
            // Same verified pattern as prior yoyos: TrailCacheLength ghosts, oldest->newest.
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
                Color color = (GetAlpha(lightColor) ?? lightColor) * (0.24f * age);
                float rot = (Projectile.oldRot != null && k < Projectile.oldRot.Length) ? Projectile.oldRot[k] : Projectile.rotation;
                Main.EntitySpriteDraw(tex, oldCenter - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                    null, color, rot, origin, Projectile.scale, SpriteEffects.None, 0);
            }
        }
    }
}
