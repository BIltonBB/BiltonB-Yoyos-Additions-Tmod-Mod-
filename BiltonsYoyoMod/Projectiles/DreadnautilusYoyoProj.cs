using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

using BiltonsYoyoMod.Content.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    public class DreadnautilusYoyoProj : ModProjectile
    {
        private int cycleTimer;
        private int cycleIndex;
        private int sprayTimer;

        // Dash state (active during cycleIndex == 1)
        private int dashCount;
        private int dashTimer;
        private int dashGapTimer;
        private Vector2 dashDirection = Vector2.Zero;
        private float orbDistanceAcc;
        private float trailAlpha;
        private int sprayCount; // alternates narrow / wide spray volleys

        // Blood wake ribbon (dead table profile at MiniYoyoFx:3428 -> wired).
        private readonly List<Vector2> wake = new List<Vector2>();

        // Phase transition FX state
        private int phaseFxTimer; // counts down from 30 on phase change
        private Color phaseFxColor = Color.Red;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 260f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 13f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 14f;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override bool PreAI()
        {
            // Dash leash: hold the range open BEFORE the vanilla yoyo code runs.
            // (Mod AI runs after vanilla each tick while YoyoBoostSystem resets the
            // set post-tick, so AI-time writes never reach vanilla in time — the
            // yoyo latched return mid-dash and sometimes flew home. PreAI fixes it
            // with no extra projectiles.) Capped at 900px so a sprinting player can
            // still outrun a spent dash and trigger a normal return.
            bool dashing = cycleIndex == 1 && dashCount < 3;
            float dist = 0f;
            if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player owner = Main.player[Projectile.owner];
                if (owner != null && owner.active)
                    dist = Vector2.Distance(owner.Center, Projectile.Center);
            }
            bool hold = dashing || (dist > 260f && dist < 900f);
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = hold ? 1200f : 260f;
            return true;
        }

        // M1: abyssal lift so the shell reads in dark blood-moon water.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        // Phase + dash state sync: the 50% tears roll and dash timers are
        // owner-decided, so remote clients need them for matching visuals.
        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(cycleTimer);
            writer.Write(cycleIndex);
            writer.Write(sprayCount);
            writer.Write(dashCount);
            writer.Write(dashTimer);
            writer.Write(dashGapTimer);
            writer.WriteVector2(dashDirection);
            writer.Write(orbDistanceAcc);
            writer.Write(phaseFxTimer);
            writer.Write(phaseFxColor.R);
            writer.Write(phaseFxColor.G);
            writer.Write(phaseFxColor.B);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            cycleTimer = reader.ReadInt32();
            cycleIndex = reader.ReadInt32();
            sprayCount = reader.ReadInt32();
            dashCount = reader.ReadInt32();
            dashTimer = reader.ReadInt32();
            dashGapTimer = reader.ReadInt32();
            dashDirection = reader.ReadVector2();
            orbDistanceAcc = reader.ReadSingle();
            phaseFxTimer = reader.ReadInt32();
            phaseFxColor = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            if (dashTimer > 0)
                trailAlpha = 1f;
        }

        public override void AI()
        {
            // Phase transition shockwave ring + screen shake (major overhaul)
            if (phaseFxTimer > 0)
                phaseFxTimer--;

            int effectiveCycle = cycleIndex == 3 ? 0 : cycleIndex;

            // M3: slow abyssal spin (square hitbox; never affects collision).
            Projectile.rotation += 0.12f;
            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 10f, 12);

            // Abyssal lighting shifts per phase (major overhaul)
            if (effectiveCycle == 0)
                Lighting.AddLight(Projectile.Center, 0.7f, 0.1f, 0.15f); // blood spray: crimson
            // Fluid metaball: blood blob streaming off the shell (spawn rate tuned
            // to the 256 pool: 40-tick life x 1/10 = ~240 max, so it keeps flowing
            // instead of saturating into a frozen cluster).
            if (Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.LengthSquared() > 1f ? -Projectile.velocity.SafeNormalize(Vector2.UnitX) : -Vector2.UnitY;
                Vector2 mbPos = Projectile.Center + back * 6f + Main.rand.NextVector2Circular(5f, 5f);
                Vector2 mbVel = back * Main.rand.NextFloat(0.4f, 1.6f) + Main.rand.NextVector2Circular(0.3f, 0.3f);
                MetaballSystem.SpawnMetaball(mbPos, mbVel, 1f, 0.7f, 40, new Color(205, 70, 95));
            }
            else if (effectiveCycle == 1)
                Lighting.AddLight(Projectile.Center, 0.9f, 0.05f, 0.1f); // dash: dark red
            else
                Lighting.AddLight(Projectile.Center, 0.6f, 0.15f, 0.2f); // tears: pink-red

            // Blood-mist aura: slow crimson haze drifting off the yoyo.
            if (Main.rand.NextBool(6))
            {
                Dust mist = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f), DustID.Blood, Main.rand.NextVector2Circular(0.8f, 0.8f), 120, default, Main.rand.NextFloat(0.8f, 1.4f));
                mist.noGravity = true;
                mist.velocity *= 0.4f;
            }

            // M9: the shell bleeds harder mid-dash.
            int dripRate = effectiveCycle == 1 ? 6 : 12;
            if (Main.rand.NextBool(dripRate))
                MiniYoyoFx.SpawnBloodDrip(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), Main.rand.NextVector2Circular(0.5f, 0.5f), 0.8f);

            // M2: blood string shimmer along the player->shell line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.Blood, new Vector2(0f, -0.4f), 140, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            bool owner = Main.myPlayer == Projectile.owner;

            // NOTE: dash range hold lives in PreAI (before vanilla); see there.

            // Cycle every 5s
            cycleTimer++;
            if (cycleTimer >= 300)
            {
                cycleTimer = 0;
                cycleIndex = (cycleIndex + 1) % 3;

                // Tears: 50% chance else treat as BloodSpray this cycle
                if (cycleIndex == 2 && owner && !Main.rand.NextBool(2))
                {
                    // Fall through to BloodSpray behavior without spawning tears
                    // Mark as spray by resetting to 0-equivalent handling via flag:
                    // Use cycleIndex = 3 to mean "spray fallback"
                    cycleIndex = 3;
                }

                if (cycleIndex == 1)
                {
                    // Reset dash state
                    dashCount = 0;
                    dashTimer = 0;
                    dashGapTimer = 0;
                    orbDistanceAcc = 0f;
                    if (owner)
                    {
                        StartDash();
                        MiniYoyoFx.FlashFX(Projectile.Center, new Color(220, 60, 70));
                        MiniYoyoFx.ImplodeFX(Projectile.Center, new Color(220, 60, 70));
                    }
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Roar, Projectile.Center);
                }
                else if (cycleIndex == 2)
                {
                    if (owner)
                        SpawnTears();
                }
                else
                {
                    sprayTimer = 0;
                }

                if (cycleIndex == 3)
                    sprayTimer = 0;

                // Phase-change blood pulse + shockwave ring + screen shake (major overhaul)
                phaseFxTimer = 30;
                phaseFxColor = cycleIndex == 1 ? new Color(180, 20, 30) : cycleIndex == 2 ? new Color(220, 60, 70) : new Color(160, 30, 40);
                if (owner)
                {
                    MiniYoyoFx.Shake(3, 15);
                    // Expanding shockwave dust ring
                    for (int i = 0; i < 16; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 16f;
                        Dust pulse = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, angle.ToRotationVector2() * 5f, 100, default, 1.3f);
                        pulse.noGravity = true;
                    }
                    // M10: abyssal ink ridges the pulse.
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                        Dust ink = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, angle.ToRotationVector2() * 3f, 120, default, 1.1f);
                        ink.noGravity = true;
                    }
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.15f, 0.2f);

                Projectile.netUpdate = true;
            }

            if (effectiveCycle == 0)
            {
                // Blood Spray: every 0.5s fire 3 shards
                if (trailAlpha > 0f)
                {
                    trailAlpha -= 0.08f;
                    if (trailAlpha < 0f)
                        trailAlpha = 0f;
                }

                sprayTimer++;
                if (sprayTimer >= 30)
                {
                    sprayTimer = 0;
                    if (owner)
                        FireBloodSpray();
                }
            }
            else if (effectiveCycle == 1)
            {
                UpdateDash(owner);
            }
            else
            {
                // Tears cycle: idle, tears act on their own
                if (trailAlpha > 0f)
                {
                    trailAlpha -= 0.08f;
                    if (trailAlpha < 0f)
                        trailAlpha = 0f;
                }
            }
        }

        private void FireBloodSpray()
        {
            NPC target = FindNearestEnemy(600f);
            if (target == null)
                return;

            // Alternating volley geometry: narrow fan, then wide fast fan.
            sprayCount++;
            Projectile.netUpdate = true;
            bool wide = sprayCount % 2 == 0;
            Vector2 baseDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            float baseAngle = baseDir.ToRotation();
            float spread = wide ? 25f : 15f;
            float speed = wide ? 16f : 14f;
            float[] offsets = { 0f, MathHelper.ToRadians(-spread), MathHelper.ToRadians(spread) };
            foreach (float off in offsets)
            {
                Vector2 vel = baseAngle.ToRotationVector2().RotatedBy(off) * speed;
                // Recompute per volley: baseAngle already re-aimed each volley
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    vel,
                    ModContent.ProjectileType<DreadnautilusYoyoBloodShardProj>(),
                    Projectile.damage / 2,
                    Projectile.knockBack,
                    Projectile.owner);
            }

            // Arterial spurt flash toward the target on every volley (tight, directional).
            int spurtCount = wide ? 6 : 4;
            for (int i = 0; i < spurtCount; i++)
            {
                Dust spurt = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, baseDir * Main.rand.NextFloat(6f, 10f) + Main.rand.NextVector2Circular(0.8f, 0.8f), 100, default, wide ? 1.2f : 1.1f);
                spurt.noGravity = true;
            }
            // M6: ink streaks ridge the spurt.
            for (int i = 0; i < 2; i++)
            {
                Dust ink = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, baseDir * Main.rand.NextFloat(4f, 7f), 120, default, 1f);
                ink.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.2f, 0.25f);
        }

        private void StartDash()
        {
            NPC target = FindNearestEnemy(600f);
            if (target != null)
                dashDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            else
                dashDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            dashTimer = 30;
            trailAlpha = 1f;

            // Abyssal launch burst: ink-black + blood ring as the dash ignites.
            for (int i = 0; i < 12; i++)
            {
                float angle = MathHelper.TwoPi * i / 12f;
                int burstType = i % 3 == 0 ? DustID.Smoke : DustID.Blood;
                Dust launch = Dust.NewDustPerfect(Projectile.Center, burstType, angle.ToRotationVector2() * 5f, 100, default, 1.3f);
                launch.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.9f, 0.1f, 0.15f);
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.netUpdate = true;
        }

        private void UpdateDash(bool owner)
        {
            if (dashCount >= 3)
            {
                if (trailAlpha > 0f)
                {
                    trailAlpha -= 0.08f;
                    if (trailAlpha < 0f)
                        trailAlpha = 0f;
                }
                return;
            }

            trailAlpha = 1f;

            if (dashTimer > 0)
            {
                // Ignition cone on the launch tick (all clients; dashTimer is
                // ExtraAI-synced, while the sound stays owner-side in StartDash).
                if (dashTimer == 30 && dashDirection != Vector2.Zero)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        float cone = dashDirection.ToRotation() + Main.rand.NextFloat(-0.5f, 0.5f);
                        Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, cone.ToRotationVector2() * Main.rand.NextFloat(3f, 7f), 100, default, 1.1f);
                        spark.noGravity = true;
                    }
                    Lighting.AddLight(Projectile.Center, 1.1f, 0.2f, 0.2f);
                }
                dashTimer--;
                trailAlpha = 1f;

                // Wake droplets shedding behind the dash (gravity-falling blood).
                if (Main.GameUpdateCount % 3 == 0 && dashDirection != Vector2.Zero)
                    MiniYoyoFx.SpawnBloodDrip(Projectile.Center - dashDirection * 10f, -dashDirection * 2f, 1f);

                // Slight home so it doesn't overshoot
                NPC target = FindNearestEnemy(500f);
                if (target != null)
                {
                    Vector2 targetDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    if (targetDir != Vector2.Zero && dashDirection != Vector2.Zero)
                        dashDirection = Vector2.Lerp(dashDirection, targetDir, 0.08f).SafeNormalize(dashDirection);
                }

                if (dashDirection != Vector2.Zero)
                    Projectile.velocity = dashDirection * 22f;

                if (Projectile.ai[0] < 0f)
                    Projectile.ai[0] = 0f;

                // Fire orbs every 40px traveled (dash-born: ai[0] == 1).
                orbDistanceAcc += Projectile.velocity.Length();
                if (orbDistanceAcc >= 40f)
                {
                    orbDistanceAcc = 0f;
                    if (owner)
                    {
                        Vector2 dir1 = dashDirection.RotatedBy(-MathHelper.PiOver2);
                        Vector2 dir2 = dir1.RotatedBy(MathHelper.Pi); // +180deg from first = +90 from dash
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            Projectile.Center,
                            dir1 * 3f,
                            ModContent.ProjectileType<DreadnautilusBloodOrbProj>(),
                            Projectile.damage / 3,
                            Projectile.knockBack,
                            Projectile.owner,
                            1f);
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            Projectile.Center,
                            dir2 * 3f,
                            ModContent.ProjectileType<DreadnautilusBloodOrbProj>(),
                            Projectile.damage / 3,
                            Projectile.knockBack,
                            Projectile.owner,
                            1f);
                    }
                }

                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 100, default, 1.2f);
                    dust.noGravity = true;
                    dust.velocity *= 0.3f;
                }

                // Dash streak: fast blood streak trailing behind the dash.
                // Blood Moon: the moon lengthens every streak.
                int streaks = Main.bloodMoon ? 3 : 1;
                for (int i = 0; i < streaks; i++)
                {
                    Dust streak = Dust.NewDustPerfect(Projectile.Center - dashDirection * Main.rand.NextFloat(6f, 20f) + Main.rand.NextVector2Circular(5f, 5f), DustID.Blood, -dashDirection * 2f + Main.rand.NextVector2Circular(0.8f, 0.8f), 100, default, 1.1f);
                    streak.noGravity = true;
                    streak.velocity *= 0.6f;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.15f, 0.15f);

                if (dashTimer == 0)
                {
                    dashCount++;
                    Projectile.velocity *= 0.1f;
                    dashGapTimer = 60;
                    // Dash-end thud: ground-shock ring + soft thump (visual only).
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 8f;
                        Dust thud = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, angle.ToRotationVector2() * 3.2f, 100, default, 1f);
                        thud.noGravity = true;
                    }
                    // M8: ink settles over the thud.
                    for (int i = 0; i < 2; i++)
                    {
                        Dust soot = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.Smoke, new Vector2(0f, -1f), 120, default, 1f);
                        soot.noGravity = true;
                    }
                    Lighting.AddLight(Projectile.Center, 0.9f, 0.15f, 0.15f);
                    if (owner)
                        Projectile.netUpdate = true;
                }
            }
            else
            {
                // Gap between dashes
                if (dashGapTimer > 0)
                {
                    dashGapTimer--;
                    // M5: the shell inhales blood between charges (runs on all clients; timer is synced).
                    if (Main.rand.NextBool(4))
                    {
                        Vector2 edge = Projectile.Center + Main.rand.NextVector2Circular(28f, 28f);
                        Dust inhale = Dust.NewDustPerfect(edge, DustID.Blood, (Projectile.Center - edge) * 0.09f, 100, default, 0.9f);
                        inhale.noGravity = true;
                    }
                    if (dashGapTimer == 0 && dashCount < 3 && owner)
                        StartDash();
                }
                else if (owner && dashDirection == Vector2.Zero)
                {
                    StartDash();
                }
            }
        }

        private void SpawnTears()
        {
            for (int i = 0; i < 3; i++)
            {
                float angle = MathHelper.TwoPi * i / 3f;
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<DreadnautilusTearsProj>(),
                    Projectile.damage / 2,
                    Projectile.knockBack,
                    Projectile.owner,
                    Projectile.whoAmI,
                    angle);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() * 0.5f;

            // Blood wake under the shell (dash drags it wider).
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type, dashTimer > 0 ? 10f : -1f);

            int effectiveCycle = cycleIndex == 3 ? 0 : cycleIndex;

            // Seat-glow for the shell, tinted by phase (spray pale / dash dark /
            // tears bright) + single tears orbit ring (tears draw none).
            // M7: the seat throbs with a slow heartbeat.
            // Blood Moon sync: redder seat, faster heart, longer dash streaks.
            Color seat = effectiveCycle == 1 ? new Color(140, 20, 30) : effectiveCycle == 2 ? new Color(230, 70, 80) : new Color(190, 40, 50);
            float heartSpeed = Main.bloodMoon ? 0.22f : 0.1f;
            MiniYoyoFx.ApplyMotionReadability(Projectile);
            float heartbeat = 0.3f + 0.08f * (float)Math.Sin(Main.GameUpdateCount * heartSpeed);
            if (Main.bloodMoon)
                seat = Color.Lerp(seat, new Color(255, 60, 70), 0.4f);
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f, seat, heartbeat);
            // Shared orbit circle at the trio's true radius: smooth ring + halo +
            // 6 orbiting spark glints. (Dash line strips removed — the afterimage
            // ghosts carry the charge without the stray-line clutter.)
            if (effectiveCycle == 2)
            {
                MiniYoyoFx.DrawRing(Projectile.Center, 64f, 2f, new Color(190, 40, 50, 110), 32);
                MiniYoyoFx.DrawGlow(Projectile.Center, 30f, new Color(180, 35, 45), 0.3f);
                for (int i = 0; i < 6; i++)
                {
                    float ang = Main.GameUpdateCount * 0.05f + MathHelper.TwoPi * i / 6f;
                    Vector2 sparkPos = Projectile.Center + ang.ToRotationVector2() * 64f;
                    MiniYoyoFx.DrawGlow(sparkPos, 7f, new Color(235, 110, 120), 0.5f);
                }
            }

            // Phase transition shockwave ring (major overhaul)
            if (phaseFxTimer > 0)
            {
                float elapsed = 30f - phaseFxTimer;
                MiniYoyoFx.DrawShockwaveRing(Projectile.Center, 10f, 80f, 30f, elapsed, phaseFxColor, 4f);
                // Secondary inner ring
                MiniYoyoFx.DrawShockwaveRing(Projectile.Center, 5f, 50f, 20f, elapsed * 1.5f, phaseFxColor, 2f);
                // Echo ring: delayed twin chasing the main burst at half pace.
                MiniYoyoFx.DrawShockwaveRing(Projectile.Center, 8f, 62f, 40f, elapsed * 0.55f, phaseFxColor, 3f);
            }

            // Dash afterimages with blood trail (major overhaul)
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                float distFromCurrent = Vector2.Distance(Projectile.Center, oldCenter);
                if (distFromCurrent < 4f)
                    continue;

                Vector2 drawPos = oldCenter - Main.screenPosition;
                float progress = (float)i / Projectile.oldPos.Length;
                float distanceFade = MathHelper.Clamp(distFromCurrent / 120f, 0.2f, 1f);
                float alpha = (1f - progress) * 0.6f * trailAlpha * distanceFade;
                float rotation = Projectile.oldRot[i];

                // Shadow layer
                Color shadowColor = Color.Black * alpha * 0.45f;
                Main.EntitySpriteDraw(texture, drawPos + new Vector2(2f, 2f), null, shadowColor, rotation, drawOrigin, Projectile.scale * 1.05f, SpriteEffects.None, 0);

                // Blood trail layer (new: thicker, more visible during dash)
                if (trailAlpha > 0.1f)
                {
                    Color bloodTrail = new Color(120, 15, 20) * alpha * 0.8f;
                    Main.EntitySpriteDraw(texture, drawPos, null, bloodTrail, rotation, drawOrigin, Projectile.scale * 1.15f, SpriteEffects.None, 0);
                }

                // Main trail layer
                Color trailColor = Color.Lerp(lightColor, new Color(180, 40, 40), 0.5f) * alpha;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            return true;
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Dash carnage: arterial fan + impact burst (major overhaul)
            MiniYoyoFx.ImpactFX(target.Center, new Color(220, 80, 90), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
            if (dashTimer > 0 && dashCount < 3)
            {
                // Directional carnage fan along the dash (never a circle).
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 jet = (dashDirection + new Vector2(-dashDirection.Y, dashDirection.X) * i * 0.35f).SafeNormalize(Vector2.UnitX);
                    Dust fan = Dust.NewDustPerfect(target.Center, DustID.Blood, jet * Main.rand.NextFloat(4f, 7f), 100, default, 1.1f);
                    fan.noGravity = true;
                }
                Lighting.AddLight(target.Center, 1f, 0.2f, 0.2f);
                // Synced vanilla slash sparkles on the dash hit (free MP sync).
                Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                    new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = target.Center }, Projectile.owner);
                YoyoParticles.SpawnBloom(target.Center, 3f, 12f, 16, new Color(220, 80, 90), 0.6f, 0.35f);

                // Screen shake on dash hit
                if (Main.myPlayer == Projectile.owner)
                    MiniYoyoFx.Shake(2, 10);
            }
            else
            {
                // Normal hit: small blood spurt
                for (int i = 0; i < 4; i++)
                {
                    Dust spurt = Dust.NewDustPerfect(target.Center, DustID.Blood, Main.rand.NextVector2Circular(2f, 2f), 100, default, 0.9f);
                    spurt.noGravity = true;
                }
            }
        }

        public override void PostDraw(Color lightColor)
        {
            // No blob glow: PreDraw seat + lighting + _Glow file already carry
            // identity. PostDraw keeps only motion/condition reads.
            // Aura budget (C1): fury ring cut - belongs to Ogre/Betsy/DarkMage now.
        }
    }
}
