using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

using BiltonsYoyoMod.Content.Systems;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Ice Queen yoyo. Every 10s (600 ticks) picks Attack1/Attack2 50/50 for 7.5s (450 ticks).
    /// Attack1: Ice Shard Rain (shard every 15 ticks from above, downward speed 9).
    /// Attack2: Ice Shard Burst (shard every 12 ticks in a random direction, speed 10).
    /// While idle, dashes at nearby enemies (4s cooldown, 30 ticks, speed 20, slight homing,
    /// drops shards downward every 5 ticks). Shard hits inflict Frostburn via IceQueenYoyoShardProj.
    /// </summary>
    public class IceQueenYoyoProj : ModProjectile
    {
        private const int CycleTime = 600;
        private const int AttackDuration = 450;
        private const int DashCooldown = 240;
        private const int DashDuration = 30;

        private int cycleTimer;
        private int attackType = -1; // -1 = none, 0 = shard rain, 1 = shard burst
        private int attackTimer;
        private int fireTimer;
        private int dashCooldownTimer;
        private int dashTimer;
        private Vector2 dashDirection;
        // Frost wake ribbon (dead table profile at MiniYoyoFx:3433 -> wired).
        private readonly List<Vector2> wake = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 290f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 14f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 14f;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override bool PreAI()
        {
            // Dash leash: hold the range open BEFORE the vanilla yoyo code runs.
            // (Mod AI runs after vanilla each tick while YoyoBoostSystem resets the
            // set post-tick, so AI-time writes never reached vanilla in time — the
            // yoyo latched return mid-dash and sometimes flew home. PreAI fixes it
            // with no extra projectiles.) Capped at 900px so a sprinting player can
            // still outrun a spent dash and trigger a normal return.
            float dist = 0f;
            if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player owner = Main.player[Projectile.owner];
                if (owner != null && owner.active)
                    dist = Vector2.Distance(owner.Center, Projectile.Center);
            }
            bool hold = dashTimer > 0 || (dist > 290f && dist < 900f);
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = hold ? 1200f : 290f;
            return true;
        }

        // M1: frost lift so the queen reads on blizzard nights.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(attackType);
            // M5: timers + dash bearing ride along so remotes see dashes/phases,
            // not just the attack pick (Dreadnautilus precedent).
            writer.Write(cycleTimer);
            writer.Write(attackTimer);
            writer.Write(dashCooldownTimer);
            writer.Write(dashTimer);
            writer.WriteVector2(dashDirection);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            attackType = reader.ReadInt32();
            cycleTimer = reader.ReadInt32();
            attackTimer = reader.ReadInt32();
            dashCooldownTimer = reader.ReadInt32();
            dashTimer = reader.ReadInt32();
            dashDirection = reader.ReadVector2();
        }

        public override void AI()
        {
            MiniYoyoFx.ApplyMotionReadability(Projectile);
            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 10f, 12);
            Lighting.AddLight(Projectile.Center, 0.3f, 0.6f, 0.9f);

            // M3: deterministic frost spin (square hitbox; never affects collision).
            Projectile.rotation += 0.12f;

            // Thin frost aura: alternating mist/sparkle every 4th tick (was ~50/s).
            // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
            if (Main.GameUpdateCount % 4 == 0)
            {
                int t = Main.GameUpdateCount % 8 == 0 ? DustID.Frost : DustID.IceTorch;
                Dust breath = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), t, new Vector2(0f, -0.5f), 120, default, 0.8f);
                breath.noGravity = true;
            }
            // Frost creep: while attacking, cold crawls in from the screen edges.
            if (attackType != -1 && Main.GameUpdateCount % 10 == 0)
            {
                Vector2 edge = Main.GameUpdateCount % 20 == 0
                    ? new Vector2(Main.screenPosition.X - 10f, Main.screenPosition.Y + Main.rand.NextFloat(0f, Main.screenHeight))
                    : new Vector2(Main.screenPosition.X + Main.screenWidth + 10f, Main.screenPosition.Y + Main.rand.NextFloat(0f, Main.screenHeight));
                Dust creep = Dust.NewDustPerfect(edge, DustID.Frost, new Vector2(edge.X < Main.screenPosition.X ? 0.8f : -0.8f, 0.3f), 140, default, 0.9f);
                creep.noGravity = true;
            }

            // M4: fast-move snow spray kicked behind the queen when she runs.
            if (Projectile.velocity.LengthSquared() > 36f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust snow = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.IceTorch, -back * 1.2f + Main.rand.NextVector2Circular(0.4f, 0.4f), 120, default, 0.8f);
                snow.noGravity = true;
            }

            // M2: frost string shimmer along the player->queen line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.Frost, new Vector2(0f, -0.4f), 140, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            // NOTE: dash range hold lives in PreAI (before vanilla); see there.

            if (attackType == -1)
            {
                cycleTimer++;
                UpdateDash();

                // E1: the rope goes taut 10 ticks before the attack cycle flips.
                if (cycleTimer == CycleTime - 10)
                    YoyoStringHookSystem.AnnounceVolley(Projectile, 10);

                if (cycleTimer >= CycleTime)
                {
                    cycleTimer = 0;
                    attackTimer = 0;
                    fireTimer = 0;

                    if (Main.myPlayer == Projectile.owner)
                    {
                        attackType = Main.rand.Next(2);
                        Projectile.netUpdate = true;
                    }
                    MiniYoyoFx.FlashFX(Projectile.Center, new Color(170, 225, 255));
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item28, Projectile.Center);
                }
            }
            else
            {
                attackTimer++;
                fireTimer++;

                if (Main.myPlayer == Projectile.owner)
                {
                    if (attackType == 0 && fireTimer >= 15)
                    {
                        fireTimer = 0;
                        // Shard rain falls above the battlefield (owner's position).
                        Vector2 field = Main.player[Projectile.owner].Center;
                        Vector2 spawnPos = field + new Vector2(Main.rand.NextFloat(-500f, 500f), -400f);
                        Vector2 vel = (MathHelper.PiOver2 + Main.rand.NextFloat(-0.4f, 0.4f)).ToRotationVector2() * 9f;

                        Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), spawnPos, vel,
                            ModContent.ProjectileType<IceQueenYoyoShardProj>(),
                            Math.Max(1, Projectile.damage / 2), Projectile.knockBack, Projectile.owner);
                        // Landing reticle on every 4th volley (pool-friendly),
                        // simulated along the shard's own tilt so the ring sits
                        // exactly where this shard will touch down.
                        if (attackTimer % 60 == 0)
                            MiniYoyoFx.MarkLandingFalling(spawnPos, vel, 0.15f, new Color(170, 225, 255));
                    }
                    else if (attackType == 1 && fireTimer >= 12)
                    {
                        fireTimer = 0;
                        Vector2 dir = Main.rand.NextVector2Unit();
                        if (dir.LengthSquared() < 0.001f)
                            dir = Vector2.UnitY;

                        Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, dir * 10f,
                            ModContent.ProjectileType<IceQueenYoyoShardProj>(),
                            Math.Max(1, Projectile.damage / 2), Projectile.knockBack, Projectile.owner);
                        // M7: burst pip streaking along the launch bearing.
                        Dust pip = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, dir * 4f, 120, default, 0.9f);
                        pip.noGravity = true;
                    }
                }

                if (attackTimer >= AttackDuration)
                {
                    attackType = -1;
                    attackTimer = 0;
                    fireTimer = 0;
                    cycleTimer = 0;
                }
            }
        }

        private void UpdateDash()
        {
            if (dashTimer > 0)
            {
                dashTimer--;

                NPC target = FindNearestEnemy(600f);
                if (target != null)
                {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    if (want != Vector2.Zero)
                        dashDirection = Vector2.Lerp(dashDirection, want, 0.15f).SafeNormalize(dashDirection);
                }

                if (dashDirection != Vector2.Zero)
                    Projectile.velocity = dashDirection * 20f;

                // Frost mist shearing off the dash (metaball, config-gated).
                if (dashTimer % 4 == 0)
                    MetaballSystem.SpawnMetaball(Projectile.Center - dashDirection * 8f, -dashDirection * 1.6f, 1f, 0.6f, 26, new Color(150, 210, 255));

                if (Projectile.ai[0] < 0f)
                    Projectile.ai[0] = 0f;

                if (Main.myPlayer == Projectile.owner && dashTimer % 5 == 0)
                {
                    Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center, new Vector2(0f, 6f),
                        ModContent.ProjectileType<IceQueenYoyoShardProj>(),
                        Math.Max(1, Projectile.damage / 2), Projectile.knockBack, Projectile.owner);
                    // M8: icicle streak under each dropped shard.
                    Dust icicle = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, new Vector2(0f, 2f), 120, default, 0.85f);
                    icicle.noGravity = true;
                }

                if (Main.rand.NextBool(4))
                {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, Vector2.Zero, 120, default, 0.9f);
                    dust.noGravity = true;
                }
                // Exhale fog: cold breath hangs behind the dash and settles.
                if (dashTimer % 3 == 0)
                {
                    Dust fog = Dust.NewDustPerfect(Projectile.Center - dashDirection * 12f, DustID.Smoke, -dashDirection * 0.8f + new Vector2(0f, -0.4f), 140, default, 1.1f);
                    fog.noGravity = true;
                }

                // Lean dash ice-trail: one frosty streak every other tick (was 1.5/tick).
                if (dashTimer % 2 == 0)
                {
                    Dust trail = Dust.NewDustPerfect(Projectile.Center - dashDirection * Main.rand.NextFloat(6f, 18f), DustID.Frost, -dashDirection * 1.5f, 120, default, 0.9f);
                    trail.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1f);

                if (dashTimer == 0)
                {
                    Projectile.velocity *= 0.1f;
                    // M6: dash-end snow poof (runs on all clients post-sync).
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 4f;
                        Dust poof = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, angle.ToRotationVector2() * 2.6f, 120, default, 1f);
                        poof.noGravity = true;
                    }
                    Lighting.AddLight(Projectile.Center, 0.5f, 0.8f, 1f);
                    if (Main.myPlayer == Projectile.owner)
                        Projectile.netUpdate = true;
                }

                return;
            }

            dashCooldownTimer++;
            if (dashCooldownTimer >= DashCooldown)
            {
                dashCooldownTimer = 0;

                if (Main.myPlayer == Projectile.owner)
                {
                    NPC target = FindNearestEnemy(600f);
                    if (target != null)
                    {
                        dashTimer = DashDuration;
                        dashDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                        Projectile.netUpdate = true;
                        // Launch crack: frost snaps toward the dash line (dust+light only).
                        for (int i = 0; i < 6; i++)
                        {
                            float angle = MathHelper.TwoPi * i / 6f;
                            Dust crack = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, angle.ToRotationVector2() * 3f + dashDirection * 3f, 100, default, 1f);
                            crack.noGravity = true;
                        }
                        Lighting.AddLight(Projectile.Center, 0.5f, 0.8f, 1.1f);
                    }
                }
            }
        }

        // M9: frost tick for the queen herself (dust + light only; damage untouched).
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + Main.GameUpdateCount * 0.1f;
                Dust tick = Dust.NewDustPerfect(target.Center, DustID.Frost, angle.ToRotationVector2() * 2.4f, 120, default, 1f);
                tick.noGravity = true;
            }
            // Shattered ice crystals (textured, bounce off tiles).
            for (int i = 0; i < 3; i++)
            {
                Dust crystal = Dust.NewDustDirect(target.Center, 8, 8, ModContent.DustType<global::BiltonsYoyoMod.Content.Dusts.IceCrystalDust>(), Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1f);
                crystal.noGravity = false;
            }
            Lighting.AddLight(target.Center, 0.4f, 0.7f, 1f);
            // Synced vanilla slash sparkles on the hit (free MP sync).
            Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = target.Center }, Projectile.owner);
            YoyoParticles.SpawnBloom(target.Center, 5f, 22f, 20, new Color(170, 225, 255), 0.55f);
            MiniYoyoFx.ImpactFX(target.Center, new Color(170, 225, 255), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
        }

        public override void PostDraw(Color lightColor)
        {
            // No blob glow. Seat + lights + _Glow carry identity.
            // Aura budget (C1): fury ring cut - belongs to Ogre/Betsy/DarkMage now.
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            // Wake first, under the body (dash drags it wider).
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type, dashTimer > 0 ? 9.5f : -1f);

            // Seat sheen (kept tight: big discs live only on specials).
            MiniYoyoFx.DrawGlow(Projectile.Center, 16f, new Color(140, 210, 255), 0.3f);

            // Rain spins a dashed ring that hurries as the storm spends
            // (synced attackType/attackTimer, so every client agrees).
            if (attackType == 0)
            {
                // Storm ring: smooth circle with 8 orbiting ice teeth, spinning
                // up as the storm spends (no more gappy dashes).
                // Storm cloud: dark bruise blooming above the yoyo while it rains.
                MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, -30f), 36f, new Color(40, 60, 110), 0.22f);
                MiniYoyoFx.DrawRing(Projectile.Center, 26f, 2f, new Color(150, 220, 255, 120) * 0.8f, 32);
                float spin = 0.05f + 0.1f * (attackTimer / (float)AttackDuration);
                for (int i = 0; i < 8; i++)
                {
                    float a = Main.GameUpdateCount * spin + MathHelper.TwoPi * i / 8f;
                    Vector2 toothPos = Projectile.Center + a.ToRotationVector2() * 26f;
                    MiniYoyoFx.DrawGlow(toothPos, 7f, new Color(185, 230, 255), 0.5f);
                }
            }
            else if (attackType == 1)
            {
                // MAJOR B (muzzle half): burst star flashing on the spawn cadence.
                if (attackTimer % 12 < 2)
                {
                    Vector2 c = Projectile.Center - Main.screenPosition;
                    Color star = new Color(220, 245, 255, 150) * 0.7f;
                    MiniYoyoFx.DrawLine(c + new Vector2(-8f, 0f), c + new Vector2(8f, 0f), 1.5f, star);
                    MiniYoyoFx.DrawLine(c + new Vector2(0f, -8f), c + new Vector2(0f, 8f), 1.5f, star);
                }
            }

            if (dashTimer > 0)
            {
                // Head glow rides the charge (stray line strips removed).
                MiniYoyoFx.DrawGlow(Projectile.Center, 24f, new Color(170, 225, 255), 0.4f);
                for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    float progress = (float)i / Projectile.oldPos.Length;
                    float alpha = (1f - progress) * 0.5f;

                    Main.EntitySpriteDraw(texture, drawPos, null, new Color(150, 220, 255) * alpha,
                        Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            // Minimal overhaul: attack phase transition double ring.
            if (attackTimer < 10 && attackType != -1)
            {
                float ringT = attackTimer / 10f;
                Color ringColor = attackType == 0 ? new Color(100, 180, 255) : new Color(150, 220, 255);
                MiniYoyoFx.DrawRing(Projectile.Center, 20f + 15f * ringT, 2f, ringColor * (0.3f + 0.5f * ringT), 24);
                MiniYoyoFx.DrawRing(Projectile.Center, (20f + 15f * ringT) * 0.6f, 1.5f, new Color(230, 245, 255) * (0.3f + 0.4f * ringT), 20);
            }

            // Dash telegraph glow while the cooldown ripes (aim tick removed —
            // the lone line read as random; the dash ghosts show direction).
            if (attackType == -1 && dashTimer == 0 && dashCooldownTimer >= DashCooldown - 20)
            {
                float telegraphT = (dashCooldownTimer - (DashCooldown - 20f)) / 20f;
                MiniYoyoFx.DrawGlow(Projectile.Center, 20f + 15f * telegraphT, new Color(150, 220, 255), 0.2f + 0.3f * telegraphT);
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
