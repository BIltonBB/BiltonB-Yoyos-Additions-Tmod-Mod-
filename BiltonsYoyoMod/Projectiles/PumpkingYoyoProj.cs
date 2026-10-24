using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    public class PumpkingYoyoProj : ModProjectile
    {
        private int bladeTimer;
        private int chargeTimer = -1;
        private int scytheTimer;
        private int fireCycleTimer;
        private int bladeCount; // every 3rd blade swing is a harvest moon
        private int scytheCount; // alternates the launch flash omens
        private readonly List<Vector2> wake = new List<Vector2>();

        public override void SetStaticDefaults()
        {
            // Afterimage ghosts for the harvest (ExampleMod Basic Projectile pattern).
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

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 280f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 12f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 14f;
        }

        // M1: harvest lift so the jack-o'-furnace reads on dark autumn nights.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        public override void AI()
        {
            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 10f, 12);
            MiniYoyoFx.ApplyMotionReadability(Projectile);

            // M3: deterministic reaper spin (square hitbox; never affects collision).
            Projectile.rotation += 0.13f;

            Lighting.AddLight(Projectile.Center, 0.7f, 0.35f, 0.1f);

            // Thin candle-cinder aura: every 3rd tick (was every tick).
            // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
            if (Main.GameUpdateCount % 3 == 0)
            {
                int cinderType = (fireCycleTimer % 40 < 20) ? DustID.OrangeTorch : DustID.GreenTorch;
                Dust cinder = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), cinderType, new Vector2(0f, -0.6f), 0, default, 0.8f);
                cinder.noGravity = true;
            }

            // M5: fast-move leaf-ember spray kicked behind the harvest when she runs.
            if (Projectile.velocity.LengthSquared() > 36f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust spray = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.OrangeTorch, -back * 1.4f + Main.rand.NextVector2Circular(0.4f, 0.4f), 0, default, 0.8f);
                spray.noGravity = true;
            }

            // M4: ember string shimmer along the player->harvest line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.OrangeTorch, new Vector2(0f, -0.4f), 0, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            if (Main.myPlayer != Projectile.owner)
                return;

            if (chargeTimer >= 0)
            {
                chargeTimer++;
                // Lean charge inhale: one converging pair every other tick (was 3/tick).
                if (chargeTimer % 2 == 0)
                {
                    Vector2 edge = Projectile.Center + Main.rand.NextVector2Circular(14f, 14f);
                    Dust dust = Dust.NewDustPerfect(edge, DustID.OrangeTorch, (Projectile.Center - edge) * 0.12f, 0, default, 0.9f);
                    dust.noGravity = true;
                    Vector2 edge2 = Projectile.Center + Main.rand.NextVector2Circular(14f, 14f);
                    Dust spark = Dust.NewDustPerfect(edge2, DustID.PurpleTorch, (Projectile.Center - edge2) * 0.12f, 0, default, 0.8f);
                    spark.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.7f + chargeTimer * 0.01f, 0.35f + chargeTimer * 0.005f, 0.15f);
                // Harvest whistle: two rising ticks as the moon ripens (10 low, 20 high).
                if (chargeTimer == 10)
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item37 with { Pitch = -0.5f }, Projectile.Center);
                if (chargeTimer == 20)
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item37 with { Pitch = 0.1f }, Projectile.Center);

                if (chargeTimer >= 30)
                {
                    chargeTimer = -1;
                    bladeTimer = 0;
                    SpawnBlade();
                }
            }
            else
            {
                bladeTimer++;
                if (bladeTimer >= 120)
                {
                    NPC target = FindNearestEnemy(250f);
                    if (target != null)
                    {
                        chargeTimer = 0;
                        // Club-ready click + converging embers as the swing loads.
                        Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item37, Projectile.Center);
                        MiniYoyoFx.ImplodeFX(Projectile.Center, new Color(255, 190, 120));
                    }
                    else
                    {
                        bladeTimer = 0;
                        bladeCount = 0; // whiffed the set: combo restarts
                    }
                }
            }

            scytheTimer++;
            if (scytheTimer >= 300)
            {
                scytheTimer = 0;
                SpawnScythe();
            }

            // Reaping windup: cursed sparks gather for 15 ticks before launch.
            if (scytheTimer >= 285 && scytheTimer < 300)
            {
                Vector2 edge = Projectile.Center + Main.rand.NextVector2Circular(20f, 20f);
                Dust gather = Dust.NewDustPerfect(edge, DustID.CursedTorch, (Projectile.Center - edge) * 0.1f, 0, default, 0.9f);
                gather.noGravity = true;
            }

            fireCycleTimer++;
            if (fireCycleTimer >= 420)
                fireCycleTimer = 0;

            // Exhale surge: the furnace roars brighter through the high-lob opening.
            if (fireCycleTimer < 60)
                Lighting.AddLight(Projectile.Center, 0.35f, 0.15f, 0.04f);

            if (fireCycleTimer < 300 && fireCycleTimer % 12 == 0)
                SpawnFire();
        }

        private void SpawnBlade()
        {
            bladeCount++;
            // Harvest moons every 3rd swing, golden moons every 100th,
            // pale moons on full-moon nights every other swing.
            bool moon = bladeCount % 3 == 0;
            bool golden = bladeCount % 100 == 0;
            if (!moon && Main.moonPhase == 0 && bladeCount % 2 == 0)
                moon = true;
            Projectile spawned = Projectile.NewProjectileDirect(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<PumpkingYoyoBladeProj>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner,
                Projectile.whoAmI,
                moon ? 1f : 0f);
            spawned.netUpdate = true;
            // M6: harvest sparks over the swing whoosh (moons burst pale).
            for (int i = 0; i < 2; i++)
            {
                Dust reaping = Dust.NewDustPerfect(Projectile.Center, moon ? DustID.MagicMirror : DustID.GoldFlame, Main.rand.NextVector2Circular(2f, 2f), 0, default, 1f);
                reaping.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = moon ? -0.3f : 0f }, Projectile.Center); // swing whoosh (moons groan lower)
            MiniYoyoFx.FlashFX(Projectile.Center, golden ? new Color(255, 215, 120) : moon ? new Color(255, 240, 210) : new Color(255, 200, 130), golden ? 1.6f : moon ? 1.2f : 0.8f);
            if (golden)
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f }, Projectile.Center); // golden moon chime
        }

        private void SpawnScythe()
        {
            scytheCount++;
            Vector2 direction = Main.rand.NextVector2Unit();
            if (direction.LengthSquared() < 0.001f)
                direction = Vector2.UnitX;
            // Alternating launch omens: cursed-led, then ember-led.
            int lead = scytheCount % 2;
            // Fountain launch blast straight up (SpawnScythe: owner-side).
            for (int i = 0; i < 3; i++)
            {
                Dust fountain = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-4f, 4f), 0f), DustID.OrangeTorch, new Vector2(0f, -4f - i * 1.5f), 0, default, 1f);
                fountain.noGravity = true;
            }
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                int pick = (i + lead) % 3;
                int flashType = pick == 0 ? DustID.OrangeTorch : pick == 1 ? DustID.PurpleTorch : DustID.CursedTorch;
                Dust flash = Dust.NewDustPerfect(Projectile.Center, flashType, angle.ToRotationVector2() * 2.6f, 0, default, 1f);
                flash.noGravity = true;
            }
            // M7: grave-smoke risers over the launch flash.
            for (int i = 0; i < 2; i++)
            {
                Dust riser = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), 0f), DustID.Smoke, new Vector2(0f, -1.8f), 0, default, 1f);
                riser.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.6f, 1f, 0.5f);
            Projectile spawned = Projectile.NewProjectileDirect(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction * 13f,
                ModContent.ProjectileType<PumpkingYoyoScytheProj>(),
                Math.Max(1, Projectile.damage / 2),
                Projectile.knockBack,
                Projectile.owner);
            spawned.netUpdate = true;
        }

        private void SpawnFire()
        {
            // Exhale phrasing: the first second of each fire phase lobs high,
            // the rest breathes low. Same rate and damage throughout.
            bool exhale = fireCycleTimer < 60;
            // Breath ember curling off each lob (SpawnFire: owner-side).
            Dust lob = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, new Vector2(0f, -2f), 0, default, 0.9f);
            lob.noGravity = true;
            Vector2 velocity = exhale
                ? new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-8f, -5f))
                : new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-5f, -2f));
            Projectile spawned = Projectile.NewProjectileDirect(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                velocity,
                ModContent.ProjectileType<PumpkingYoyoFireProj>(),
                Math.Max(1, Projectile.damage / 3),
                Projectile.knockBack,
                Projectile.owner);
            spawned.netUpdate = true;
            // M8: breath-puff curling off the lob.
            Dust puff = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1.5f), 0, default, 0.8f);
            puff.noGravity = true;
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

        // M9: harvest tick for the reaper itself (dust + light only; damage untouched).
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MiniYoyoFx.ImpactFX(target.Center, new Color(255, 190, 120), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + Main.GameUpdateCount * 0.1f;
                Dust spark = Dust.NewDustPerfect(target.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 2.4f, 0, default, 1f);
                spark.noGravity = true;
            }
            Lighting.AddLight(target.Center, 1f, 0.5f, 0.2f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Harvest wake + furnace glow + charge/windup kindles (readable telegraphs).
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type);
            DrawAfterimages(lightColor);
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f, new Color(255, 140, 60), 0.3f);

            if (chargeTimer >= 0)
            {
                float kindle = chargeTimer / 30f;
                MiniYoyoFx.DrawTelegraphRing(Projectile.Center, kindle, 40f, new Color(255, 170, 80));
                MiniYoyoFx.DrawGlow(Projectile.Center, 24f + 4f * kindle, new Color(255, 170, 80), 0.3f + 0.35f * kindle);
            }
            if (scytheTimer >= 285)
            {
                float wind = (scytheTimer - 285f) / 15f;
                MiniYoyoFx.DrawGlow(Projectile.Center, 20f + 16f * wind, new Color(120, 230, 130), 0.25f + 0.3f * wind);
            }

            // Minimal overhaul: blade swing arc telegraph
            if (chargeTimer >= 20)
            {
                float swingT = (chargeTimer - 20f) / 10f;
                Color arcColor = bladeCount % 3 == 2 ? new Color(255, 220, 100) : new Color(255, 180, 80);
                MiniYoyoFx.DrawRing(Projectile.Center, 30f + 10f * swingT, 2.5f, arcColor * (0.3f + 0.4f * swingT), 24);
            }

            // Minimal overhaul: scythe launch direction indicator
            if (scytheTimer >= 290)
            {
                float launchT = (scytheTimer - 290f) / 10f;
                MiniYoyoFx.DrawGlow(Projectile.Center, 15f + 10f * launchT, new Color(180, 255, 150), 0.3f + 0.3f * launchT);
            }

            // MAJOR A (surge half): exhale restart snap ring as the furnace roars back.
            if (fireCycleTimer == 0)
                MiniYoyoFx.DrawRing(Projectile.Center, 26f, 2.5f, new Color(255, 190, 110) * 0.7f, 24);

            return true;
        }

        public override void PostDraw(Color lightColor)
        {
            // No blob glow: PreDraw seat + lighting + _Glow file already carry
            // identity. PostDraw keeps only motion/condition reads.
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
