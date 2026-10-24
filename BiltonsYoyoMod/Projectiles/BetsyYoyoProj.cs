using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

using BiltonsYoyoMod.Content.Systems;

using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Betsy yoyo. Loops: DoubleDash (4s) -> FireDrop (5s) -> Dash (2s) ->
    /// FireballBarrage (6s) -> FireBreath (3s) -> loop. Roar (SoundID.Roar) on phase change.
    /// Dashes deliberately overshoot (aim past the target by 200px) at speed 24.
    /// FireDrop drops flames downward every 6 ticks; Barrage fires 6 fireballs per second at
    /// enemies' CURRENT positions; Breath spawns a cone of breath flames every 4 ticks.
    /// </summary>
    public class BetsyYoyoProj : ModProjectile
    {
        private const int PhaseDoubleDash = 0;
        private const int PhaseFireDrop = 1;
        private const int PhaseDash = 2;
        private const int PhaseBarrage = 3;
        private const int PhaseBreath = 4;

        private int phase;
        private int phaseTimer;
        private int dashTimer;
        private int critFlash;
        private Vector2 dashDirection;

        // Phase transition FX state
        private int phaseFxTimer;
        private Color phaseFxColor;

        // Wyrm wake ribbon (dead table profile at MiniYoyoFx:3435 -> wired).
        private readonly List<Vector2> wake = new List<Vector2>();

        // K1: announced-barrage lane (pre-fire aim preview).
        private Vector2 forecastTo;
        private int forecastTimer;

        // K7: overshoot skid chevrons at the dash turnaround.
        private int skidTimer;
        private Vector2 skidDirection;

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
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 14f;
            // 22s deployment: the 5-phase loop runs 20s, and Breath (last 3s) never
            // played inside the old 16s lifetime (same trap as the Mage ritual).
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 22f;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override bool PreAI()
        {
            // Dash leash: hold the range open BEFORE the vanilla yoyo code runs.
            // (Mod AI runs after vanilla each tick while YoyoBoostSystem resets the
            // set post-tick, so AI-time writes never reached vanilla in time — the
            // yoyo latched return mid-dash and sometimes flew home. PreAI fixes it
            // with no extra projectiles.) Covers both dash phases plus any spent
            // dash drift under 900px; a sprinting player can still force a return.
            float dist = 0f;
            if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player owner = Main.player[Projectile.owner];
            MiniYoyoFx.ApplyMotionReadability(Projectile);
                if (owner != null && owner.active)
                    dist = Vector2.Distance(owner.Center, Projectile.Center);
            }
            bool hold = dashTimer > 0 || phase == PhaseDoubleDash || phase == PhaseDash || (dist > 300f && dist < 900f);
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = hold ? 1200f : 300f;
            return true;
        }

        private static int PhaseDuration(int p)
        {
            return p switch
            {
                PhaseDoubleDash => 240,
                PhaseFireDrop => 300,
                PhaseDash => 120,
                PhaseBarrage => 360,
                PhaseBreath => 180,
                _ => 240,
            };
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(phaseFxTimer);
            writer.Write(phaseFxColor.R);
            writer.Write(phaseFxColor.G);
            writer.Write(phaseFxColor.B);
            // M5: phase + dash state ride along so remotes see dashes, not just rings.
            writer.Write(phase);
            writer.Write(phaseTimer);
            writer.Write(dashTimer);
            writer.WriteVector2(dashDirection);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            phaseFxTimer = reader.ReadInt32();
            phaseFxColor = new Color(reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
            phase = reader.ReadInt32();
            phaseTimer = reader.ReadInt32();
            dashTimer = reader.ReadInt32();
            dashDirection = reader.ReadVector2();
        }

        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        public override void AI()
        {
            // Phase transition FX timer
            if (phaseFxTimer > 0)
                phaseFxTimer--;

            // M3: deterministic dragon spin (square hitbox; never affects collision).
            Projectile.rotation += 0.13f;
            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 10f, 12);

            // Phase-specific lighting (major overhaul: dragon fire identity)
            if (critFlash > 0)
                critFlash--;
            if (skidTimer > 0)
                skidTimer--;
            if (forecastTimer > 0)
                forecastTimer--;
            if (phase == PhaseBreath)
                Lighting.AddLight(Projectile.Center, 1.4f, 0.5f, 0.1f);
            else if (dashTimer > 0)
            Lighting.AddLight(Projectile.Center, 1.2f, 0.5f, 0.15f);
            else if (phase == PhaseBarrage)
                Lighting.AddLight(Projectile.Center, 1.0f, 0.4f, 0.1f);
            else
                Lighting.AddLight(Projectile.Center, 0.8f, 0.3f, 0.08f);

            // Thin ember accent: rising mote every 4th tick (was 1/6 box cloud).
            // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
            if (Main.GameUpdateCount % 4 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Torch, new Vector2(0f, -1.2f), 100, default, 0.85f);
                dust.noGravity = true;
            }

            // M7: fast-move cinder spray kicked behind the wyrm at flank speed.
            if (Projectile.velocity.LengthSquared() > 49f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust spray = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.OrangeTorch, -back * 1.4f + Main.rand.NextVector2Circular(0.4f, 0.4f), 100, default, 0.85f);
                spray.noGravity = true;
            }

            // M6: ember string shimmer along the player->wyrm line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.GoldFlame, new Vector2(0f, -0.4f), 120, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            phaseTimer++;

            // NOTE: dash range hold lives in PreAI (before vanilla); see there.

            if (phaseTimer >= PhaseDuration(phase))
            {
                phase = (phase + 1) % 5;
                phaseTimer = 0;
                dashTimer = 0;
                SoundEngine.PlaySound(SoundID.Roar, Projectile.Center);

                // E1/K1: announced releases - the rope tauts and the barrage
                // previews its lane ~10 ticks before the first volley.
                if (phase == PhaseBarrage || phase == PhaseFireDrop)
                    YoyoStringHookSystem.AnnounceVolley(Projectile, 10);
                if (phase == PhaseBarrage)
                {
                    NPC forecast = FindNearestEnemy(1000f);
                    if (forecast != null)
                    {
                        forecastTo = forecast.Center;
                        forecastTimer = 10;
                    }
                }

                // Phase transition: fire ring + screen shake (major overhaul)
                phaseFxTimer = 35;
                phaseFxColor = phase switch
                {
                    PhaseDoubleDash => new Color(255, 200, 100),
                    PhaseFireDrop => new Color(255, 120, 40),
                    PhaseDash => new Color(255, 220, 150),
                    PhaseBarrage => new Color(255, 160, 60),
                    PhaseBreath => new Color(255, 100, 30),
                    _ => new Color(255, 150, 50)
                };

                Lighting.AddLight(Projectile.Center, 1.6f, 0.6f, 0.2f);
                if (phase == PhaseBreath)
                {
                    YoyoScreenShockwaveSystem.Trigger(Projectile.Center, 0.38f, new Color(255, 130, 50));
                }

                // Phase-change fire burst
                if (Main.myPlayer == Projectile.owner)
                {
                    MiniYoyoFx.Shake(2, 12);
                    // Roar ring: the phase change is visible, not just audible.
                    YoyoParticles.SpawnRing(Projectile.Center, 12f, 90f, 28, phaseFxColor, 3f);
                    for (int i = 0; i < 10; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 10f;
                        int dustType = i % 3 == 0 ? DustID.GoldFlame : DustID.OrangeTorch;
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, dustType, angle.ToRotationVector2() * 4.5f, 100, default, 1.1f);
                        dust.noGravity = true;
                    }
                    // Wing-gust: a fast flat shove-ring of cinders on every roar.
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 8f;
                        Vector2 gust = angle.ToRotationVector2() * 7f;
                        gust.Y *= 0.45f;
                        Dust gale = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, gust, 120, new Color(120, 70, 40), 1.1f);
                        gale.noGravity = true;
                    }
                }
                Projectile.netUpdate = true;
            }

            bool owner = Main.myPlayer == Projectile.owner;

            switch (phase)
            {
                case PhaseDoubleDash:
                    if (owner && (phaseTimer == 1 || phaseTimer == 121))
                        StartDash(900f, 90);
                    UpdateDash();
                    break;

                case PhaseFireDrop:
                    if (owner && phaseTimer % 6 == 0)
                    {
                        // Drips fall from the yoyo's underside (not its center),
                        // with a downward head so the trail streams behind.
                        Vector2 dripPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-4f, 4f), 10f);
                        Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), dripPos, new Vector2(Main.rand.NextFloat(-1f, 1f), 8f),
                            ModContent.ProjectileType<BetsyYoyoFireProj>(),
                            Math.Max(1, Projectile.damage / 2), Projectile.knockBack, Projectile.owner);
                        MiniYoyoFx.FlashFX(dripPos, new Color(255, 150, 80), 0.6f);
                    }
                    break;

                case PhaseDash:
                    if (owner && phaseTimer == 1)
                        StartDash(900f, 60);
                    UpdateDash();
                    break;

                case PhaseBarrage:
                    if (owner && phaseTimer % 60 == 0)
                        FireVolley();
                    break;

                case PhaseBreath:
                    if (owner && phaseTimer % 4 == 0)
                        FireBreath();
                    // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
                    if (Main.GameUpdateCount % 4 == 0)
                    {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, new Vector2(0f, -0.8f), 120, default, 0.9f);
                        dust.noGravity = true;
                    }
                    // MAJOR B (rain half): gold rises join the breath embers.
                    if (Main.GameUpdateCount % 8 == 0)
                    {
                        Dust rise = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.GoldFlame, new Vector2(0f, -1.4f), 120, default, 0.85f);
                        rise.noGravity = true;
                    }
                    break;
            }
        }

        private void StartDash(float range, int duration)
        {
            NPC target = FindNearestEnemy(range);
            if (target == null)
            {
                // B4: a dash that finds nothing still snaps its rope in search.
                MiniYoyoFx.StyledMiss(Projectile.Center, new Color(255, 170, 90));
                return;
            }

            Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            Vector2 aimPast = target.Center + toTarget * 200f; // deliberate overshoot
            dashDirection = (aimPast - Projectile.Center).SafeNormalize(Vector2.UnitX);
            dashTimer = duration;

            // Dash ignition: fire burst + screen shake (major overhaul)
            if (Main.myPlayer == Projectile.owner)
            {
                MiniYoyoFx.Shake(3, 15);
                for (int i = 0; i < 12; i++)
                {
                    float angle = MathHelper.TwoPi * i / 12f;
                    Dust ignition = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 5f, 100, default, 1.2f);
                    ignition.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1.5f, 0.6f, 0.15f);
            }

            Projectile.netUpdate = true;
        }

        private void UpdateDash()
        {
            if (dashTimer <= 0)
                return;

            dashTimer--;

            if (dashDirection != Vector2.Zero)
                Projectile.velocity = dashDirection * 24f;

            if (Projectile.ai[0] < 0f)
                Projectile.ai[0] = 0f;

            if (Projectile.ai[0] < 0f)
                Projectile.ai[0] = 0f;

            // Dash fire-wake: layered flame trail (major overhaul)
            if (dashTimer % 2 == 0)
            {
                // Primary ember streak
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, -dashDirection * 2f, 120, default, 1f);
                dust.noGravity = true;
                // Secondary gold flame
                Dust gold = Dust.NewDustPerfect(Projectile.Center - dashDirection * 6f, DustID.GoldFlame, -dashDirection * 1.5f, 100, default, 0.8f);
                gold.noGravity = true;
                // Tertiary ember-smoke (tinted: no gray balls on the trail).
                Dust smoke = Dust.NewDustPerfect(Projectile.Center - dashDirection * 12f, DustID.Smoke, -dashDirection * 1f, 120, new Color(255, 150, 80), 0.6f);
                smoke.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1.2f, 0.5f, 0.15f);

            if (dashTimer == 0)
            {
                Projectile.velocity *= 0.1f;
                // K7: the deliberate overshoot finally leaves a mark.
                skidTimer = 14;
                skidDirection = dashDirection;
                if (Main.myPlayer == Projectile.owner)
                    Projectile.netUpdate = true;
            }
        }

        private void FireVolley()
        {
            NPC target = FindNearestEnemy(1000f);
            if (target == null)
            {
                // B4: an empty sky gets a beat, not silence.
                MiniYoyoFx.StyledMiss(Projectile.Center, new Color(255, 170, 90));
                return;
            }

            for (int k = 0; k < 6; k++)
            {
                // Aim at the enemy's CURRENT position, with a small spread per fireball.
                Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                dir = dir.RotatedBy(Main.rand.NextFloat(-0.26f, 0.26f));

                Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, dir * 11f,
                    ModContent.ProjectileType<BetsyYoyoFireBallProj>(),
                    Math.Max(1, Projectile.damage / 2), Projectile.knockBack, Projectile.owner);
            }

            // M8: bombard flare over the volley launch.
            MiniYoyoFx.FlashFX(Projectile.Center, new Color(255, 170, 90));
            for (int i = 0; i < 3; i++)
            {
                Dust flare = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, Main.rand.NextVector2Circular(2.5f, 2.5f), 100, default, 1.1f);
                flare.noGravity = true;
            }

            // Signature ember wisps over the volley (custom ModDust).
            for (int i = 0; i < 3; i++)
            {
                Dust wisp = Dust.NewDustDirect(Projectile.Center, 8, 8, ModContent.DustType<global::BiltonsYoyoMod.Content.Dusts.EmberWispDust>(), 0f, -1f, 100, default, 1f);
                wisp.noGravity = true;
            }
            // H3 note: the barrage used to roar in the dragon's own voice here.
            // Removed - the barrage repeats, so the sound was noise pollution.
        }

        private void FireBreath()
        {
            NPC target = FindNearestEnemy(700f);
            Vector2 dir = target != null
                ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                : Main.rand.NextVector2Unit();
            dir = dir.RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)); // breath cone

            Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, dir * 13f,
                ModContent.ProjectileType<BetsyYoyoBreathProj>(),
                Math.Max(1, Projectile.damage / 3), Projectile.knockBack, Projectile.owner);
            // Cinder storm: breath gusts hurl ground embers sideways (owner-side).
            if (Main.GameUpdateCount % 2 == 0)
            {
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? 3f : -3f;
                    Dust cinder = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), 10f), DustID.OrangeTorch, new Vector2(side + dir.X * 2f, -1f), 100, default, 0.9f);
                    cinder.noGravity = true;
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (dashTimer > 0)
                modifiers.SourceDamage *= 2f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MiniYoyoFx.ImpactFX(target.Center, new Color(255, 170, 90), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
            // Burning ember chunks (textured, flicker out fast).
            for (int i = 0; i < 3; i++)
            {
                Dust ember = Dust.NewDustDirect(target.Center, 8, 8, ModContent.DustType<global::BiltonsYoyoMod.Content.Dusts.EmberChunkDust>(), Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-2.5f, 2.5f), 100, default, 1f);
                ember.noGravity = true;
            }
            if (hit.Crit)
            {
                critFlash = 10;
                MiniYoyoFx.ImpactFX(target.Center, Color.White, 1.2f, Projectile.owner, (target.Center - Projectile.Center).ToRotation());
                LuminanceBridge.ShakeAtPoint(target.Center, 5f);
            }
            if (dashTimer > 0)
            {
                dashTimer = 0;
                Projectile.velocity *= 0.1f;
                Projectile.ai[0] = 0f;

                // Dash impact: fire explosion + screen shake (major overhaul)
                if (Main.myPlayer == Projectile.owner)
                {
                    MiniYoyoFx.Shake(4, 15);
                    MiniYoyoFx.SlashFX(target.Center, (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX), new Color(255, 170, 90), MiniYoyoFx.HitScale(damageDone), Projectile.owner);
                    ImpactStaggerSystem.Stagger(target, 4);
                    // Synced vanilla slash sparkles on the dash hit (free MP sync).
                    Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                        new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = target.Center }, Projectile.owner);
                    YoyoParticles.SpawnBloom(target.Center, 3f, 14f, 16, new Color(255, 170, 90), 0.6f);
                    // Directional impact fans along the dash (never circles).
                    for (int i = -2; i <= 2; i++)
                    {
                        Vector2 jet = (dashDirection + new Vector2(-dashDirection.Y, dashDirection.X) * i * 0.25f).SafeNormalize(Vector2.UnitX);
                        Dust impact = Dust.NewDustPerfect(target.Center, DustID.OrangeTorch, jet * (4f + System.Math.Abs(i)), 100, default, 1.2f);
                        impact.noGravity = true;
                    }
                }
            }
        }

        public override void PostDraw(Color lightColor)
        {
            // No blob overlay: seat glow in PreDraw + _Glow file carry fire.
            // Crits already flash via seat tint + shake, no fullscreen wash.
            MiniYoyoFx.DrawFuryAura(Projectile);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            // Ember wake under the body (dashes drag it wider).
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type, dashTimer > 0 ? 10f : -1f);

            // K1: the announced barrage's lane (gone the moment it fires).
            if (forecastTimer > 0)
                MiniYoyoFx.DrawForecastLane(Projectile.Center, forecastTo, new Color(255, 170, 90), forecastTimer / 10f * 0.8f);

            // K7: three fading skid chevrons bite into the turnaround point,
            // apexes toward travel so the overshoot finally reads.
            if (skidTimer > 0 && skidDirection != Vector2.Zero)
            {
                float f = skidTimer / 14f;
                Vector2 fwd = skidDirection.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 3; i++)
                {
                    Vector2 tip = Projectile.Center - fwd * (6f + i * 10f);
                    float armLen = 9f + i * 1.5f;
                    Vector2 arm1 = -fwd.RotatedBy(0.75f);
                    Vector2 arm2 = -fwd.RotatedBy(-0.75f);
                    float alpha = 0.45f * f * (1f - i * 0.22f);
                    MiniYoyoFx.DrawSoftBar(tip + arm1 * armLen * 0.5f, arm1.ToRotation(), armLen, 2.5f, new Color(255, 200, 120), alpha);
                    MiniYoyoFx.DrawSoftBar(tip + arm2 * armLen * 0.5f, arm2.ToRotation(), armLen, 2.5f, new Color(255, 200, 120), alpha);
                }
            }

            // Seat glow tinted by phase (dash white-hot, fire phases ember).
            // M9: the jaw throbs while the breath pours.
            // Afterimages only mid-dash (was constant 12-ghost double-trail).
            Color seat = dashTimer > 0 ? new Color(255, 220, 180) : phase == PhaseBreath ? new Color(255, 150, 80) : new Color(255, 130, 60);
            float seatAlpha = dashTimer > 0 ? 0.5f : phase == PhaseBreath ? 0.35f + 0.1f * (float)Math.Sin(Main.GameUpdateCount * 0.2f) : 0.35f;
            // Shader pilot: pulsing seat, white-hot while the crit flash burns.
            int seatMode = critFlash > 0 ? 2 : 1;
            Color seatTint = critFlash > 0 ? Color.White : seat;
            float seatSize = dashTimer > 0 ? 26f : 18f;
            MiniYoyoFx.DrawWithShader(seatMode, seatTint, 0.85f,
                () => MiniYoyoFx.DrawGlow(Projectile.Center, seatSize, seat, seatAlpha));

            // Phase omen color for the anticipation ring (phase runs on all clients).
            Color omen = phase switch
            {
                PhaseDoubleDash => new Color(255, 200, 100),
                PhaseFireDrop => new Color(255, 120, 40),
                PhaseDash => new Color(255, 220, 150),
                PhaseBarrage => new Color(255, 160, 60),
                PhaseBreath => new Color(255, 100, 30),
                _ => new Color(255, 150, 50)
            };

            // MAJOR A (omen half): contracting anticipation ring in the last 15
            // ticks before the roar flips the phase.
            int phaseDur = PhaseDuration(phase);
            if (phaseTimer > 0 && phaseDur - phaseTimer < 15)
            {
                float wind = 1f - (phaseDur - phaseTimer) / 15f;
                MiniYoyoFx.DrawDashedRing(Projectile.Center, MathHelper.Lerp(46f, 22f, wind), 2f, omen * (0.3f + 0.5f * wind), 12, Main.GameUpdateCount * 0.12f);
            }

            // Phase transition shockwave ring (major overhaul)
            if (phaseFxTimer > 0)
            {
                float elapsed = 35f - phaseFxTimer;
                MiniYoyoFx.DrawShockwaveRing(Projectile.Center, 10f, 90f, 35f, elapsed, phaseFxColor, 5f);
                // Inner ring
                MiniYoyoFx.DrawShockwaveRing(Projectile.Center, 5f, 55f, 25f, elapsed * 1.5f, phaseFxColor, 3f);
                // Echo ring: delayed twin chasing the main burst at half pace.
                MiniYoyoFx.DrawShockwaveRing(Projectile.Center, 8f, 70f, 45f, elapsed * 0.55f, phaseFxColor, 3f);
                // Star flare crowning fresh phase flips.
                if (phaseFxTimer > 25)
                    MiniYoyoFx.DrawStarSparkle(Projectile.Center, new Color(255, 230, 180), 1.4f, 0f, phaseFxTimer / 35f);
            }

            // Fire breath beam preview (major overhaul: shows breath direction).
            // MAJOR B (jaw half): continuous draconic beam + growing throat glow
            // while the breath pours (was a 1-in-4 flicker).
            if (phase == PhaseBreath)
            {
                float throat = 16f + 24f * (phaseTimer / (float)PhaseDuration(PhaseBreath));
                // Swirl mode on the throat: vortex churn while the breath pours.
                MiniYoyoFx.DrawWithShader(3, new Color(255, 140, 60), 0.8f,
                    () => MiniYoyoFx.DrawGlow(Projectile.Center, throat, new Color(255, 140, 60), 0.4f));
                NPC target = FindNearestEnemy(700f);
                if (target != null)
                {
                    // Smooth tapered beam: overlapping segments that soften
                    // the old rectangle read without rigid edges, starting ahead of
                    // the hull so it never covers the yoyo.
                    Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Vector2 beamStart = Projectile.Center + dir * 14f;
                    Vector2 beamEnd = beamStart + dir * 186f;
                    // Beam v2 (P1.3): grow-in over 10 ticks, collapse in the last 8.
                    float breathGrow = MathHelper.Clamp(phaseTimer / 10f, 0f, 1f);
                    float breathLeft = PhaseDuration(PhaseBreath) - phaseTimer;
                    if (breathLeft < 8f)
                        breathGrow = MathHelper.Min(breathGrow, breathLeft / 8f);
                    MiniYoyoFx.DrawBeamV2(beamStart, beamEnd, new Color(255, 220, 160), new Color(255, 120, 40), 3.1f, breathGrow, 1.1f);
                    MiniYoyoFx.DrawBeamHeatShimmer(beamStart, beamEnd, new Color(255, 150, 60), 7f, breathGrow);
                    MetaballSystem.SpawnMetaball(Projectile.Center + dir * 100f, dir * 3f, 1.2f, 0.7f, 30, new Color(255, 170, 90));
                }
            }

            // MAJOR C (drum half): bombard beat ring swelling on each volley beat.
            if (phase == PhaseBarrage && phaseTimer % 60 < 6)
            {
                float beat = (phaseTimer % 60) / 6f;
                MiniYoyoFx.DrawRing(Projectile.Center, 18f + 22f * beat, 2.5f, new Color(255, 170, 70) * (0.6f * (1f - beat) + 0.15f), 24);
            }

            // Dash afterimages with fire trail (major overhaul: enhanced from vanilla)
            if (dashTimer > 0)
            {
                // MAJOR A (streak half): speed-lines streaming behind the charge.
                if (dashDirection != Vector2.Zero)
                {
                    Vector2 side = new Vector2(-dashDirection.Y, dashDirection.X);
                    for (int i = -1; i <= 1; i++)
                    {
                        Vector2 tail = Projectile.Center - dashDirection * (16f + 8f * (i + 1)) + side * (i * 7f);
                        Vector2 tip = tail - dashDirection * 20f;
                        MiniYoyoFx.DrawLine(tail - Main.screenPosition, tip - Main.screenPosition, 2f, new Color(255, 150, 50, 140) * 0.7f);
                    }
                }
                for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    float progress = (float)i / Projectile.oldPos.Length;
                    float alpha = (1f - progress) * 0.55f;

                    // Shadow
                    Main.EntitySpriteDraw(texture, drawPos + new Vector2(2f, 2f), null,
                        Color.Black * alpha * 0.45f, Projectile.oldRot[i], origin, Projectile.scale * 1.05f, SpriteEffects.None, 0);

                    // Fire trail layer (new)
                    Color fireTrail = new Color(255, 120, 30) * alpha * 0.7f;
                    Main.EntitySpriteDraw(texture, drawPos, null, fireTrail, Projectile.oldRot[i], origin, Projectile.scale * 1.15f, SpriteEffects.None, 0);

                    // Main afterimage
                    Color trailColor = Color.Lerp(lightColor, new Color(255, 120, 30), 0.5f) * alpha;
                    Main.EntitySpriteDraw(texture, drawPos, null,
                        trailColor, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }
            else
            {
                // M2: two faint cruise ghosts so the wyrm never flies naked.
                for (int i = 2; i >= 1; i--)
                {
                    if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero)
                        continue;
                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    float progress = (float)i / Projectile.oldPos.Length;
                    float alpha = (1f - progress) * 0.16f;
                    Main.EntitySpriteDraw(texture, drawPos, null,
                        (GetAlpha(lightColor) ?? lightColor) * alpha, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            return true;
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
