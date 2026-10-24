using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

using BiltonsYoyoMod.Content.Systems;

namespace BiltonsYoyoMod.Projectiles
{
    public class DarkMageYoyoMagicRitual : ModProjectile, IYoyoInstantHit
    {
        private Vector2 lastFireDir = Vector2.UnitY;
        private bool spawnFxDone;
        private int fireTimer;
        private float spin;
        private bool implodeDone;

        // Charge of the 30-tick firing cycle for cross-projectile sync
        // (the yoyo pulses its seat glow with this). Visual-only read.
        public float Charge => fireTimer / 30f;

        public override void SetDefaults()
        {
            Projectile.width = 82;
            Projectile.height = 82;

            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;

            Projectile.timeLeft = 600;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.scale = 1f;
        }

        // M12: lighting-aware tint that respects the summon-in / fade-out opacity.
        public override Color? GetAlpha(Color lightColor)
        {
            Color lifted = Color.Lerp(lightColor, new Color(190, 130, 255), 0.3f);
            return lifted * Projectile.Opacity;
        }

        public override void AI()
        {
            // Immobile
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = 0f;

            // Staged summon: 4 radial snaps + 8 rim motes rushing inward
            // (implosion read: the seal drinking power in, not bursting out).
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4f + spin;
                    Dust flash = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, angle.ToRotationVector2() * 2.4f, 100, default, 1.1f);
                    flash.noGravity = true;
                }
                for (int i = 0; i < 8; i++)
                {
                    float angle = MathHelper.TwoPi * i / 8f;
                    Vector2 rim = Projectile.Center + angle.ToRotationVector2() * 64f;
                    Dust drink = Dust.NewDustPerfect(rim, DustID.PurpleTorch, -angle.ToRotationVector2() * 3.2f, 100, default, 1f);
                    drink.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.8f, 0.4f, 1f);
            }

            // Charge-synced spin: the seal visibly hurries as the bolt loads.
            float chargeNow = Charge;
            spin += 0.018f + 0.02f * chargeNow;

            // Gentle breathing scale + summon-in growth over the first 20 ticks.
            float summonAge = 600f - Projectile.timeLeft;
            float summonIn = summonAge < 20f ? summonAge / 20f : 1f;
            Projectile.scale = summonIn * (1f + (float)Math.Sin(Main.GameUpdateCount * 0.06f) * 0.04f);
            if (summonAge < 20f)
                Lighting.AddLight(Projectile.Center, 1f, 0.6f, 1.2f);

            // One sparse inhale mote, rushing faster as charge builds.
            if (Main.rand.NextBool(7))
            {
                float angle = spin + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 rim = Projectile.Center + angle.ToRotationVector2() * 58f;
                Dust mote = Dust.NewDustPerfect(rim, DustID.PurpleTorch, -angle.ToRotationVector2() * (0.9f + 0.8f * chargeNow), 120, default, 0.8f);
                mote.noGravity = true;
            }

            // M14: high-charge rim glint — one bright tick when the next bolt is almost loaded.
            if (fireTimer > 25 && Main.rand.NextBool(4))
            {
                float angle = spin + Main.rand.NextFloat(MathHelper.TwoPi);
                Dust glint = Dust.NewDustPerfect(Projectile.Center + angle.ToRotationVector2() * 44f, DustID.MagicMirror, Vector2.Zero, 100, default, 0.8f);
                glint.noGravity = true;
                glint.velocity *= 0.1f;
            }

            // M15: taper the seal light as it dies so the collapse reads as power draining.
            float fade = Projectile.timeLeft < 60 ? Projectile.timeLeft / 60f : 1f;
            Lighting.AddLight(Projectile.Center, 0.5f * fade, 0.2f * fade, 0.65f * fade);

            // Fade out in last 60 ticks
            if (Projectile.timeLeft < 60)
            {
                Projectile.alpha = (int)(255f * (1f - Projectile.timeLeft / 60f));
            }
            else
            {
                Projectile.alpha = 0;
            }

            // Every 30 ticks fire 1 bolt at a RANDOM valid enemy within 700px
            fireTimer++;
            if (fireTimer >= 30)
            {
                fireTimer = 0;
                if (Main.myPlayer == Projectile.owner)
                {
                    List<NPC> valid = new List<NPC>();
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC npc = Main.npc[i];
                        if (MiniYoyoFx.IsAttackable(npc) && Vector2.Distance(Projectile.Center, npc.Center) <= 700f)
                        {
                            valid.Add(npc);
                        }
                    }

                    if (valid.Count > 0)
                    {
                        NPC chosen = valid[Main.rand.Next(valid.Count)];
                        Vector2 dir = (chosen.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        lastFireDir = dir;
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 10f,
                            ModContent.ProjectileType<DarkMageYoyoMagicProj>(), Projectile.damage, 0f, Projectile.owner);
                    }
                }
            }

            // Cast flash: tight radial pop + launch streak along the bolt dir.
            if (fireTimer == 0)
            {
                // Fluid magic surge (metaball, config-gated) along the cast direction.
                MetaballSystem.SpawnMetaball(Projectile.Center, lastFireDir * 2.4f, 1.1f, 0.7f, 28, new Color(186, 132, 255));
                MetaballSystem.SpawnMetaball(Projectile.Center + new Vector2(0f, -4f), lastFireDir * 1.2f - Vector2.UnitY * 0.8f, 0.9f, 0.6f, 30, new Color(186, 132, 255));
                for (int i = 0; i < 3; i++)
                {
                    float angle = MathHelper.TwoPi * i / 3f + spin;
                    Dust cast = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, angle.ToRotationVector2() * 2.2f, 100, default, 1.1f);
                    cast.noGravity = true;
                }
                for (int i = 0; i < 3; i++)
                {
                    Dust streak = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, lastFireDir * (7f + i * 1.5f) + Main.rand.NextVector2Circular(0.5f, 0.5f), 100, default, 1f);
                    streak.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.8f, 0.4f, 1f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // MAJOR B: layered summoning circle — staged reveal + collapse.
            // Layers (each has one job, back->front):
            //  1. seat glow + ground shadow (grounds the seal, soft depth).
            //  2. outer summoning circle: smooth + 12 orbiting rune motes.
            //  3. charge inhale ring (contracts as the bolt loads — the telegraph).
            //  4. middle seal: smooth ring + 8 rotating rune ticks.
            //  5. inner pentacle star (counter-rotating, the arcane signature).
            //  6. inner seal ticks (4 fast ticks, half-speed counter-rotation).
            //  7. vertical light pillar (3 stacked glows rising — summon power).
            // All alphas multiply by Opacity so summon-in/fade-out stay clean.
            float opacity = Projectile.Opacity;
            float summonAge = 600f - Projectile.timeLeft;
            float summonIn = summonAge < 20f ? summonAge / 20f : 1f;
            float charge = fireTimer / 30f;

            float pulse = 1f + (float)Math.Sin(Main.GameUpdateCount * 0.06f) * 0.04f;
            MiniYoyoFx.DrawGlow(Projectile.Center, 90f * pulse * Projectile.scale, new Color(150, 80, 255), 0.22f * opacity * summonIn);

            // M13: soft ground shadow so the floating seal sits in the world.
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, 34f), 70f * Projectile.scale, new Color(60, 20, 110), 0.15f * opacity);

            // 2. Outer summoning circle: smooth and continuous, with 12 rune motes
            // orbiting it (slow craft) + charge-brightened ticks riding along.
            float outerR = 58f * Projectile.scale;
            MiniYoyoFx.DrawRing(Projectile.Center, outerR, 2.5f, new Color(170, 110, 240) * (0.35f + 0.45f * charge) * opacity, 48);
            for (int i = 0; i < 12; i++)
            {
                float a = spin * 0.7f + MathHelper.TwoPi * i / 12f;
                Vector2 motePos = Projectile.Center + a.ToRotationVector2() * outerR;
                MiniYoyoFx.DrawGlow(motePos, 7f, new Color(200, 150, 255), (0.35f + 0.4f * charge) * opacity);
            }

            // Converging rune ring (config: Visuals.DarkMageRuneRing, read live):
            // 8 glyph dashes drawn in from a distance ring, easing inward, then a
            // one-shot implosion flash as they set into the seal. The ritual's
            // signature gesture (vault §3).
            bool runeRing = true;
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                runeRing = cfg == null || cfg.Visuals.DarkMageRuneRing;
            }
            catch { }
            if (!implodeDone && runeRing)
            {
                float conv = MathHelper.Clamp(summonAge / 46f, 0f, 1f);
                float ease = conv * conv * (3f - 2f * conv);
                float glyphR = MathHelper.Lerp(94f, 30f, ease) * Projectile.scale;
                float dashLen = MathHelper.Lerp(16f, 9f, ease);
                float glyphA = Math.Min(1f, conv * 1.8f) * opacity;
                Color glyphCol = new Color(200, 150, 255) * glyphA;
                Color glyphCore = new Color(240, 220, 255) * (glyphA * 0.9f);
                for (int i = 0; i < 8; i++)
                {
                    float a = spin * 0.8f + MathHelper.TwoPi * i / 8f;
                    Vector2 dir = a.ToRotationVector2();
                    Vector2 gp = Projectile.Center + dir * glyphR - Main.screenPosition;
                    Vector2 gIn = gp - dir * (dashLen * 0.5f);
                    Vector2 gOut = gp + dir * (dashLen * 0.5f);
                    MiniYoyoFx.DrawLine(gIn, gOut, 2.2f, glyphCol);
                    MiniYoyoFx.DrawLine((gIn + gp) * 0.5f, (gp + gOut) * 0.5f, 1.1f, glyphCore);
                    Vector2 gSide = new Vector2(-dir.Y, dir.X);
                    MiniYoyoFx.DrawLine(gOut - gSide * 3.2f, gOut + gSide * 3.2f, 1.4f, glyphCol * 0.8f);
                    MiniYoyoFx.DrawGlow(Projectile.Center + dir * glyphR, 9f, new Color(210, 170, 255), 0.22f * glyphA);
                }
                if (conv >= 1f)
                {
                    implodeDone = true;
                    // Implosion flash: bright bloom + collapsing ring + inward motes.
                    YoyoParticles.SpawnBloom(Projectile.Center, 3f, 30f, 18, new Color(225, 200, 255), 0.7f);
                    YoyoParticles.SpawnRing(Projectile.Center, 46f, 10f, 14, new Color(200, 150, 255), 3.5f);
                    MiniYoyoFx.FlashFX(Projectile.Center, new Color(220, 190, 255), 0.8f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = spin + MathHelper.TwoPi * i / 8f;
                        Vector2 rim = Projectile.Center + a.ToRotationVector2() * 52f;
                        YoyoParticles.SpawnSpark(rim, -a.ToRotationVector2() * 5f, new Color(210, 170, 255), 1f, 18, 0.9f);
                    }
                }
            }

            // 3. Inhale ring synced to the 30-tick firing cycle: contracts onto the
            // rune as the next bolt charges (anticipation), snapping out on fire.
            float inhaleR = MathHelper.Lerp(68f, 44f, charge) * Projectile.scale;
            MiniYoyoFx.DrawRing(Projectile.Center, inhaleR, 2f + 0.75f * charge, new Color(190, 130, 255) * (0.2f + 0.55f * charge) * opacity, 24);

            // 4. Middle seal: smooth ring with 8 rotating rune ticks.
            float midR = 44f * Projectile.scale;
            MiniYoyoFx.DrawRing(Projectile.Center, midR, 2f, new Color(190, 130, 255) * (0.5f + 0.4f * charge) * opacity, 40);
            for (int i = 0; i < 8; i++)
            {
                float a = spin + MathHelper.TwoPi * i / 8f;
                Vector2 p0 = Projectile.Center + a.ToRotationVector2() * (midR - 4f) - Main.screenPosition;
                Vector2 p1 = Projectile.Center + a.ToRotationVector2() * (midR + 4f) - Main.screenPosition;
                MiniYoyoFx.DrawLine(p0, p1, 2f, new Color(225, 200, 255, 140) * (0.5f + 0.4f * charge) * opacity);
            }

            // 5. Inner pentacle: 5-point star polygon at r=26, counter-rotating.
            for (int i = 0; i < 5; i++)
            {
                int j = (i + 2) % 5;
                float a0 = -spin * 0.8f + MathHelper.TwoPi * i / 5f;
                float a1 = -spin * 0.8f + MathHelper.TwoPi * j / 5f;
                Vector2 p0 = Projectile.Center + a0.ToRotationVector2() * 26f * Projectile.scale - Main.screenPosition;
                Vector2 p1 = Projectile.Center + a1.ToRotationVector2() * 26f * Projectile.scale - Main.screenPosition;
                MiniYoyoFx.DrawLine(p0, p1, 1.5f, new Color(225, 195, 255, 120) * (0.35f + 0.4f * charge) * opacity);
            }

            // 6. Inner seal: 4 ticks counter-rotating at half speed (outer seal craft).
            for (int i = 0; i < 4; i++)
            {
                float a = -spin * 0.5f + MathHelper.TwoPi * i / 4f;
                Vector2 p0 = Projectile.Center + a.ToRotationVector2() * 18f * Projectile.scale - Main.screenPosition;
                Vector2 p1 = Projectile.Center + a.ToRotationVector2() * 26f * Projectile.scale - Main.screenPosition;
                MiniYoyoFx.DrawLine(p0, p1, 2f, new Color(230, 200, 255, 130) * (0.4f + 0.4f * charge) * opacity);
            }

            // 7. Vertical light pillar: 3 stacked glows rising from the seal.
            for (int k = 0; k < 3; k++)
            {
                Vector2 pillarPos = Projectile.Center + new Vector2(0f, -18f - k * 16f) * Projectile.scale;
                MiniYoyoFx.DrawGlow(pillarPos, (34f - k * 8f) * Projectile.scale, new Color(170, 110, 255), (0.10f + 0.06f * charge) * opacity * summonIn);
            }
            return true;
        }

        public override void OnKill(int timeLeft)
        {
            // Collapse in two beats: 6 rim motes implode, 6 sparks pop out.
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Vector2 rim = Projectile.Center + angle.ToRotationVector2() * 40f;
                Dust inDust = Dust.NewDustPerfect(rim, DustID.PurpleTorch, -angle.ToRotationVector2() * 3f, 100, default, 1f);
                inDust.noGravity = true;
            }
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f + MathHelper.PiOver4;
                Dust outDust = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, angle.ToRotationVector2() * 2.6f, 100, default, 1.1f);
                outDust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.7f, 0.35f, 0.9f);
            Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item27, Projectile.Center);
        }
    }
}
