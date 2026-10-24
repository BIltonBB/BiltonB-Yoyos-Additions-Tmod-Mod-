using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

using BiltonsYoyoMod.Content.Systems;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    public class OgreYoyoProj : ModProjectile
    {
        private int clubTimer;
        private int spitTimer;
        private int shockTimer;
        private int shockInterval = 600;
        private int swingCount; // every 3rd consecutive swing lands heavy
        private int shockWindup; // 12-tick ground-crack anticipation before eruption
        private readonly List<Vector2> wake = new List<Vector2>();

        public override void SetStaticDefaults()
        {
            // Afterimage ghosts for the brute (ExampleMod Basic Projectile pattern).
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
            Projectile.tileCollide = false;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 250f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 12f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 12f;
        }

        // M1: jungle-rot lift so the brute reads in dark caves without fullbrighting.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        public override void AI()
        {
            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 10f, 12);
            MiniYoyoFx.ApplyMotionReadability(Projectile);

            // M3: deterministic visual spin (square hitbox; never affects collision).
            Projectile.rotation += 0.14f;

            // War-drum pulse: light swells as the club swing loads (anticipation).
            float drumPulse = 0.75f + 0.5f * (clubTimer / 60f);
            Lighting.AddLight(Projectile.Center, 0.25f * drumPulse, 0.9f * drumPulse, 0.25f * drumPulse);

            // Thin jungle-rot presence: one alternating spore/wisp every 3rd tick.
            // Was 2 dust every tick (~120/s cloud). NOTE: yoyo timeLeft is pinned
            // at 2 by the yoyo AI, so gate on GameUpdateCount.
            if (Main.GameUpdateCount % 3 == 0)
            {
                int t = (int)(Main.GameUpdateCount % 2);
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(7f, 7f), t == 0 ? DustID.Poisoned : DustID.Smoke, -Projectile.velocity * 0.05f + new Vector2(0f, -0.4f), 120, default, 0.75f);
                d.noGravity = true;
            }

            // M5: fast-move leaf spray snapped behind the hull when she charges.
            if (Projectile.velocity.LengthSquared() > 36f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust spray = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.Poisoned, -back * 1.4f + Main.rand.NextVector2Circular(0.4f, 0.4f), 120, default, 0.85f);
                spray.noGravity = true;
            }

            // M4: mossy string shimmer along the player->brute line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.JungleSpore, new Vector2(0f, -0.4f), 140, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            if (Main.myPlayer != Projectile.owner)
                return;

            clubTimer++;
            spitTimer++;
            shockTimer++;

            // Charge inhale: spores converge as the club swing readies (1 per 2 ticks).
            // War-drum beat: a click + muzzle kiss lands exactly as the charge starts.
            if (clubTimer == 40)
            {
                Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item37 with { Pitch = -0.3f }, Projectile.Center);
                MiniYoyoFx.FlashFX(Projectile.Center, new Color(150, 230, 150), 0.6f);
            }
            if (clubTimer >= 40 && clubTimer % 2 == 0)
            {
                Vector2 edge = Projectile.Center + Main.rand.NextVector2Circular(24f, 24f);
                Vector2 inward = (Projectile.Center - edge) * 0.08f;
                Dust charge = Dust.NewDustPerfect(edge, DustID.Poisoned, inward, 120, default, 0.85f);
                charge.noGravity = true;
                Lighting.AddLight(Projectile.Center, 0.35f, 1f, 0.35f);
            }

            // Every 1s: club swing at nearby enemy (every 3rd is a heavy).
            if (clubTimer >= 60)
            {
                clubTimer = 0;
                NPC target = FindNearestEnemy(200f);
                if (target != null)
                {
                    swingCount++;
                    bool heavy = swingCount % 3 == 0;
                    // Parent-anchored swing: spawn at the yoyo, swing around it.
                    // ai[1] carries the heavy flag (synced at spawn).
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<OgreYoyoClubProj>(),
                        Projectile.damage,
                        Projectile.knockBack,
                        Projectile.owner,
                        Projectile.whoAmI,
                        heavy ? 1f : 0f);
                    MiniYoyoFx.FlashFX(Projectile.Center, new Color(150, 230, 150), heavy ? 1.2f : 0.8f);
                }
                else
                {
                    swingCount = 0;
                }
            }

            // Every 2s: 1-2 spit toward nearby enemies (25%: one sour seed).
            if (spitTimer >= 120)
            {
                spitTimer = 0;
                NPC target = FindNearestEnemy(600f);
                if (target != null)
                {
                    int count = Main.rand.Next(1, 3);
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                        dir = dir.RotatedBy(Main.rand.NextFloat(-0.15f, 0.15f));
                        bool seed = Main.rand.NextBool(4);
                        // Spit leaves from the brute's front, already on its way.
                        Vector2 spitMuzzle = Projectile.Center + dir * 10f;
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            spitMuzzle,
                            dir * (seed ? 7f : 9f),
                            ModContent.ProjectileType<OgreYoyoSpitProj>(),
                            Projectile.damage / 2,
                            Projectile.knockBack,
                            Projectile.owner,
                            seed ? 1f : 0f);
                    }
                }
            }

            // M7: spit-windup hawking lick toward the mark (owner-side; PreDraw glow covers remotes).
            if (spitTimer >= 100)
            {
                NPC mark = FindNearestEnemy(600f);
                if (mark != null && Main.GameUpdateCount % 2 == 0)
                {
                    Vector2 dir = (mark.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    Dust lick = Dust.NewDustPerfect(Projectile.Center + dir * 8f, DustID.Poisoned, dir * 2.2f, 120, default, 0.9f);
                    lick.noGravity = true;
                }
            }

            // Random 2-20s shockwave, staged: 12-tick windup, then eruption.
            if (shockWindup > 0)
            {
                shockWindup--;
                if (Main.GameUpdateCount % 2 == 0)
                {
                    Vector2 crack = Projectile.Center + new Vector2(Main.rand.NextFloat(-34f, 34f), 10f);
                    Dust inhale = Dust.NewDustPerfect(crack, DustID.Poisoned, (Projectile.Center - crack) * 0.1f, 120, default, 0.9f);
                    inhale.noGravity = true;
                }
                // M8: windup sparks bursting at the brute's base as the ground cracks.
                if (Main.rand.NextBool(4))
                {
                    Dust crack2 = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 8f), DustID.Smoke, new Vector2(0f, -2f), 120, default, 1f);
                    crack2.noGravity = true;
                }
                if (shockWindup == 0)
                    DoShockwave();
            }
            else if (shockTimer >= shockInterval)
            {
                shockTimer = 0;
                shockInterval = Main.rand.Next(120, 1201);
                shockWindup = 12;
                Projectile.netUpdate = true;
                // Club-ready click + converging dust as the ground crack loads.
                Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item37, Projectile.Center);
                MiniYoyoFx.ImplodeFX(Projectile.Center, new Color(150, 235, 150), 1.2f);
            }
        }

        private void DoShockwave()
        {
            // Damage rides the expanding ring projectile through the standard combat
            // pipeline (defense, crits, player bonuses, MP sync) with the same
            // damage/2 and 6f knockback numbers — no more hand-built StrikeNPC hit,
            // which skipped defense/crits/scaling/sync and felt like a shove.

            // 8 spit projs radially
            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 vel = angle.ToRotationVector2() * 9f;
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    vel,
                    ModContent.ProjectileType<OgreYoyoSpitProj>(),
                    Projectile.damage / 2,
                    Projectile.knockBack,
                    Projectile.owner);
            }

            // Upward debris eruption: ground-pound chunks (directional, thinned 8->5).
            for (int i = 0; i < 5; i++)
            {
                Vector2 launch = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-7f, -3f));
                Dust chunk = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 8f), DustID.Smoke, launch, 100, default, Main.rand.NextFloat(1f, 1.3f));
                chunk.noGravity = false;
            }
            for (int i = 0; i < 4; i++)
            {
                Vector2 clod = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -2f));
                Dust rot = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f), DustID.Poisoned, clod, 120, default, 1f);
                rot.noGravity = true;
            }

            // Single crisp venom ring (was 24) + ground-hugging ring (was 16).
            // Ground-pound only: the venom hugs the floor like the shock ring.
            for (int i = 0; i < 14; i++)
            {
                float angle = MathHelper.TwoPi * i / 14f;
                Vector2 venom = angle.ToRotationVector2() * 5f;
                venom.Y *= 0.35f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Poisoned, venom, 100, default, 1.3f);
                dust.noGravity = true;
            }

            // Fluid metaball burst on shockwave eruption.
            for (int i = 0; i < 6; i++)
            {
                float a = MathHelper.TwoPi * i / 6f;
                MetaballSystem.SpawnMetaball(Projectile.Center + a.ToRotationVector2() * 10f, a.ToRotationVector2() * 3f, 1.2f, 0.7f, 40, new Color(170, 235, 170));
            }

            // Outer ground-hugging shockwave ring.
            for (int i = 0; i < 10; i++)
            {
                float angle = MathHelper.TwoPi * i / 10f;
                Vector2 vel = angle.ToRotationVector2() * 7f;
                vel.Y *= 0.35f;
                Dust shock = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel, 120, default, 1.2f);
                shock.noGravity = true;
            }
            // Mic-drop: the club flings chips that stick where they land.
            for (int i = 0; i < 6; i++)
            {
                Dust splinter = Dust.NewDustDirect(Projectile.Center, 8, 8, ModContent.DustType<global::BiltonsYoyoMod.Content.Dusts.WoodChipDust>(), Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-7f, -3f), 100, default, 1.2f);
                splinter.noGravity = false;
            }

            // Ground crack decal on club land (4 s) + dirt-chip arcs. The decal
            // rides the generalized ground-decal family; both gated by
            // Config Visuals.OgreCrackDecal (read live, untick to remove).
            bool crackFx = true;
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                crackFx = cfg == null || cfg.Visuals.OgreCrackDecal;
            }
            catch { }
            if (crackFx)
            {
                MiniYoyoFx.GroundCrack(Projectile.Center + new Vector2(0f, 14f), new Color(120, 170, 100), 1.15f, 240);
                for (int i = 0; i < 7; i++)
                {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 chipVel = new Vector2(side * Main.rand.NextFloat(1.5f, 3.8f), Main.rand.NextFloat(-6.5f, -3.4f));
                    YoyoParticles.SpawnSpark(Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), 10f),
                        chipVel, Main.rand.NextBool(3) ? new Color(132, 106, 78) : new Color(92, 66, 44),
                        1.1f, Main.rand.Next(34, 50), 0.985f, 0.22f);
                }
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 1f, 0.4f);

            // Screen shockwave (L5): the ground-pound ripples the whole frame.
            YoyoScreenShockwaveSystem.Trigger(Projectile.Center, 0.42f, new Color(170, 235, 170));

            // Visual-only expanding ground ring.
            if (Main.myPlayer == Projectile.owner)
            {
                // The ring carries the shockwave damage (standard knockback).
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<OgreYoyoShockRingProj>(), Projectile.damage / 2, 6f, Projectile.owner);
            }
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

        public override bool PreDraw(ref Color lightColor)
        {
            // Rot-wake ribbon + seat glow + club-ready kindle (anticipation read).
            // War paint: seat gilds with the owner's combo (25+ full gold).
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type);
            DrawAfterimages(lightColor);
            float warpaint = MathHelper.Clamp(YoyoKillFX.GetCombo(Projectile.owner) / 25f, 0f, 1f);
            MiniYoyoFx.DrawGlow(Projectile.Center, 30f, Color.Lerp(new Color(80, 200, 100), new Color(255, 210, 130), warpaint), 0.3f);

            // MAJOR A (club half): war-drum charge ring contracting onto the brute as
            // the swing loads. M6: kindle yellows when the next swing lands heavy.
            if (clubTimer >= 40)
            {
                float kindle = (clubTimer - 40f) / 20f;
                bool heavyNext = swingCount % 3 == 2;
                Color kindleColor = heavyNext
                    ? Color.Lerp(new Color(150, 255, 150), new Color(255, 230, 130), kindle)
                    : new Color(150, 255, 150);
                MiniYoyoFx.DrawDashedRing(Projectile.Center, MathHelper.Lerp(34f, 16f, kindle), 2f, new Color(150, 235, 150) * (0.25f + 0.55f * kindle), 12, Main.GameUpdateCount * 0.1f);
                MiniYoyoFx.DrawGlow(Projectile.Center, 20f + 18f * kindle, kindleColor, 0.25f + 0.35f * kindle);
            }

            // Shockwave windup: contracting ground ring over the 12-tick crack.
            if (shockWindup > 0)
            {
                float wind = 1f - shockWindup / 12f;
                MiniYoyoFx.DrawRing(Projectile.Center, MathHelper.Lerp(60f, 26f, wind), 2.5f, new Color(150, 235, 150) * (0.3f + 0.5f * wind), 24);
                MiniYoyoFx.DrawGlow(Projectile.Center, 24f + 20f * wind, new Color(120, 220, 120), 0.25f + 0.3f * wind);
            }

            // MAJOR A (spit half): the marked target wears a tightening venom ring.
            if (spitTimer >= 100)
            {
                NPC target = FindNearestEnemy(600f);
                if (target != null)
                {
                    float kindle = (spitTimer - 100f) / 20f;
                    MiniYoyoFx.DrawRing(target.Center, MathHelper.Lerp(20f, 12f, kindle), 2f, new Color(180, 255, 120) * (0.3f + 0.5f * kindle), 20);
                    MiniYoyoFx.DrawGlow(target.Center, 12f + 6f * kindle, new Color(180, 255, 100), 0.2f + 0.2f * kindle);
                }
            }

            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MiniYoyoFx.ImpactFX(target.Center, new Color(150, 230, 150), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
        }

        public override void PostDraw(Color lightColor)
        {
            // No blob glow. Seat + lights + _Glow carry identity.
            MiniYoyoFx.DrawFuryAura(Projectile);
        }

        private void DrawAfterimages(Color lightColor)
        {
            // Same verified pattern as DarkMage/TorchGod/Dutchman: TrailCacheLength ghosts.
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
