using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    public class SantaYoyoProj : ModProjectile
    {
        private int cycleTimer;
        private int spikyTimer;
        private int spikyNext;
        private int presentTimer;
        private int presentNext;
        private int presentQueue;
        private int presentQueueTimer;
        private int bulletPip; // every 10th tracer gets a gold pip (owner-side cadence)
        private readonly List<Vector2> wake = new List<Vector2>();

        public override void SetStaticDefaults()
        {
            // Afterimage ghosts for the sleigh (ExampleMod Basic Projectile pattern).
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
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 13f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 14f;
        }

        // M1: festive lift so the sleigh reads on Frost Moon nights.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(cycleTimer);
            writer.Write(spikyTimer);
            writer.Write(spikyNext);
            writer.Write(presentTimer);
            writer.Write(presentNext);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            cycleTimer = reader.ReadInt32();
            spikyTimer = reader.ReadInt32();
            spikyNext = reader.ReadInt32();
            presentTimer = reader.ReadInt32();
            presentNext = reader.ReadInt32();
        }

        public override void AI()
        {
            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 10f, 12);
            MiniYoyoFx.ApplyMotionReadability(Projectile);

            // M3: deterministic toy-shop spin (square hitbox; never affects collision).
            Projectile.rotation += 0.14f;

            bool inPhaseB = cycleTimer >= 300;

            if (inPhaseB)
                Lighting.AddLight(Projectile.Center, 0.2f, 0.7f, 0.25f);
            else
                Lighting.AddLight(Projectile.Center, 0.75f, 0.2f, 0.2f);

            // Thin festive aura: one cycling ember every 3rd tick (was every tick).
            // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
            if (Main.GameUpdateCount % 3 == 0)
            {
                int cyclePick = ((cycleTimer / 20) % 3 + 3) % 3;
                int auraType = cyclePick == 0 ? DustID.RedTorch : cyclePick == 1 ? DustID.GreenTorch : DustID.GoldFlame;
                Dust aura = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), auraType, new Vector2(0f, -0.6f), 0, default, 0.8f);
                aura.noGravity = true;
            }

            // M5: fast-move snow spray kicked behind the sleigh when she runs.
            if (Projectile.velocity.LengthSquared() > 36f && Main.rand.NextBool(12))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust snow = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.WhiteTorch, -back * 1.2f + Main.rand.NextVector2Circular(0.4f, 0.4f), 0, default, 0.8f);
                snow.noGravity = true;
            }

            // M4: confetti string shimmer along the player->sleigh line.
            if (Main.rand.NextBool(16) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.Confetti, new Vector2(0f, -0.5f), 0, default, 0.8f);
                    shimmer.noGravity = true;
                }
            }

            bool owner = Main.myPlayer == Projectile.owner;

            // Counters run on all clients (visual cadence stays agreed); random
            // rolls and spawns stay owner-side, synced via ExtraAI on each event.
            if (spikyNext <= 0 && owner)
            {
                spikyNext = Main.rand.Next(300, 601);
                Projectile.netUpdate = true;
            }
            if (presentNext <= 0 && owner)
            {
                presentNext = Main.rand.Next(360, 721);
                Projectile.netUpdate = true;
            } // 6-12s: always reachable inside the 14s deployment

            cycleTimer++;
            if (cycleTimer >= 600)
                cycleTimer = 0;

            if (cycleTimer == 0 || cycleTimer == 300)
            {
                // Jingle pitch ladder: bullets low, missiles bright.
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = cycleTimer == 0 ? -0.2f : 0.2f }, Projectile.Center);
                for (int i = 0; i < 8; i++)
                {
                    float angle = MathHelper.TwoPi * i / 8f;
                    int dustChoice = i % 3;
                    int flashType = dustChoice == 0 ? DustID.RedTorch : dustChoice == 1 ? DustID.GreenTorch : DustID.GoldFlame;
                    Dust flash = Dust.NewDustPerfect(Projectile.Center, flashType, angle.ToRotationVector2() * 3f, 0, default, 1f);
                    flash.noGravity = true;
                }
                // M6: confetti pops over the phase-change flash.
                for (int i = 0; i < 2; i++)
                {
                    Dust pop = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1f);
                    pop.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.8f, 0.5f);
                MiniYoyoFx.DissolveFX(Projectile.Center, new Color(255, 150, 110));

                // Phase-change shockwave ring in the incoming phase's color.
                if (Main.myPlayer == Projectile.owner)
                {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<SantaPhaseRingProj>(), 0, 0f, Projectile.owner, cycleTimer == 0 ? 0f : 1f);
                }
            }

            if (cycleTimer < 300)
            {
                if (owner && cycleTimer % 3 == 0)
                    SpawnBullet();
            }
            else
            {
                if (owner && (cycleTimer - 300) % 30 == 0)
                    SpawnMissile();
            }

            spikyTimer++;
            if (spikyNext > 0 && spikyTimer >= spikyNext)
            {
                spikyTimer = 0;
                if (owner)
                {
                    spikyNext = Main.rand.Next(300, 601);
                    SpawnSpikyBall();
                    Projectile.netUpdate = true;
                }
            }

            presentTimer++;
            if (presentNext > 0 && presentTimer >= presentNext)
            {
                presentTimer = 0;
                if (owner)
                {
                    presentNext = Main.rand.Next(360, 721);
                    if (Main.rand.Next(4) != 0) // 75%: at 50% + 20s rolls, droughts were the norm, not bad luck
                    {
                        // Christmas morning: a bigger shower, once a year.
                        bool xmas = System.DateTime.Now.Month == 12 && System.DateTime.Now.Day == 25;
                        presentQueue = xmas ? 30 : 25;
                        presentQueueTimer = 5;
                        // M8: gift-chute confetti ring as the shower starts.
                        for (int i = 0; i < 6; i++)
                        {
                            float angle = MathHelper.TwoPi * i / 6f;
                            Dust chute = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, angle.ToRotationVector2() * 2.4f, 0, default, 1f);
                            chute.noGravity = true;
                        }
                    }
                    Projectile.netUpdate = true;
                }
            }

            // Presents straggle out over ~2.5s (1 per 6 ticks, first at once).
            if (presentQueue > 0)
            {
                presentQueueTimer++;
                if (presentQueueTimer >= 6)
                {
                    presentQueueTimer = 0;
                    SpawnPresent();
                    presentQueue--;
                }
            }
        }

        private void SpawnBullet()
        {
            MiniYoyoFx.FlashFX(Projectile.Center, new Color(255, 110, 110), 0.8f);
            Vector2 direction = Main.rand.NextVector2Unit();
            if (direction.LengthSquared() < 0.001f)
                direction = Vector2.UnitX;
            float speed = Main.rand.NextFloat(6f, 12f);
            // Every 10th tracer gets a gold pip at the muzzle (SpawnBullet: owner-side).
            if (++bulletPip % 10 == 0)
            {
                Dust pip = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, direction * 2f, 0, default, 0.9f);
                pip.noGravity = true;
            }
            Projectile spawned = Projectile.NewProjectileDirect(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction * speed,
                ModContent.ProjectileType<SantaYoyoBulletProj>(),
                Math.Max(1, Projectile.damage / 2),
                Projectile.knockBack,
                Projectile.owner);
            spawned.netUpdate = true;
        }

        private void SpawnMissile()
        {
            MiniYoyoFx.FlashFX(Projectile.Center, new Color(110, 220, 130), 0.8f);
            Vector2 direction = Main.rand.NextVector2Unit();
            if (direction.LengthSquared() < 0.001f)
                direction = Vector2.UnitX;
            Projectile spawned = Projectile.NewProjectileDirect(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction * 8f,
                ModContent.ProjectileType<SantaYoyoMissilesProj>(),
                Math.Max(1, Projectile.damage / 2),
                Projectile.knockBack,
                Projectile.owner);
            spawned.netUpdate = true;
        }

        private void SpawnSpikyBall()
        {
            Vector2 spawnPos = Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), -30f);
            Vector2 velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), 0f);
            Projectile spawned = Projectile.NewProjectileDirect(
                Projectile.GetSource_FromThis(),
                spawnPos,
                velocity,
                ModContent.ProjectileType<SantaYoyoSpikyBallProj>(),
                Math.Max(1, Projectile.damage / 2),
                Projectile.knockBack,
                Projectile.owner);
            spawned.netUpdate = true;
            // M7: drop-chute smoke as the ornament tips out.
            for (int i = 0; i < 3; i++)
            {
                Dust chute = Dust.NewDustPerfect(spawnPos, DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -1f), 0, default, 1f);
                chute.noGravity = true;
            }
        }

        private void SpawnPresent()
        {
            // Presents rain above the battlefield (owner's position), not the yoyo.
            // Dud gifts (5%): damage-free, fall straight onto the owner's head.
            Vector2 field = Main.player[Projectile.owner].Center;
            bool dud = Main.rand.NextBool(20);
            Vector2 spawnPos = dud
                ? field + new Vector2(Main.rand.NextFloat(-30f, 30f), -400f)
                : field + new Vector2(Main.rand.NextFloat(-400f, 400f), -400f);
            Projectile spawned = Projectile.NewProjectileDirect(
                Projectile.GetSource_FromThis(),
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<SantaYoyoPresentsProj>(),
                dud ? 0 : Math.Max(1, Projectile.damage / 2),
                Projectile.knockBack,
                Projectile.owner);
            spawned.netUpdate = true;
            if (dud)
            {
                spawned.ai[0] = 1f;
                spawned.netUpdate = true;
            }
            // Landing reticle: simulated along the gift's own sway so the ring
            // sits exactly where this present will arrive (duds fall straight).
            int lane = spawned.whoAmI % 3;
            float fallSpeed = 1.2f + lane * 0.3f;
            float swayAmp = dud ? 0f : 1.7f + lane * 0.3f;
            MiniYoyoFx.MarkLandingSway(spawnPos, fallSpeed, swayAmp, (spawned.whoAmI % 5) * 12, new Color(255, 210, 170));
        }

        // M9: jingle tick for the sleigh itself (dust + light only; damage untouched).
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MiniYoyoFx.ImpactFX(target.Center, new Color(255, 150, 110), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + Main.GameUpdateCount * 0.1f;
                int jingleType = i % 2 == 0 ? DustID.RedTorch : DustID.GreenTorch;
                Dust jingle = Dust.NewDustPerfect(target.Center, jingleType, angle.ToRotationVector2() * 2.4f, 0, default, 1f);
                jingle.noGravity = true;
            }
            Lighting.AddLight(target.Center, 0.9f, 0.5f, 0.5f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Festive wake + phase-tinted seat glow (barrage red / missile green).
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type);
            DrawAfterimages(lightColor);
            bool inPhaseB = cycleTimer >= 300;
            Color seat = inPhaseB ? new Color(90, 220, 120) : new Color(235, 90, 90);
            float seatPulse = 0.32f + 0.08f * (float)Math.Sin(Main.GameUpdateCount * 0.12f);
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f, seat, seatPulse);

            // Minimal overhaul: phase transition flash ring
            if (cycleTimer < 5 || (cycleTimer >= 300 && cycleTimer < 305))
            {
                float flashT = cycleTimer < 5 ? cycleTimer / 5f : (cycleTimer - 300) / 5f;
                Color flashColor = cycleTimer < 5 ? new Color(255, 100, 100) : new Color(100, 255, 150);
                MiniYoyoFx.DrawRing(Projectile.Center, 15f + 25f * flashT, 3f, flashColor * (0.4f + 0.4f * flashT), 24);
                // Star flare crowning the phase flip.
                MiniYoyoFx.DrawStarSparkle(Projectile.Center, flashColor, 1.3f, 0f, 0.5f + 0.5f * flashT);
            }

            // MAJOR A (chute half): spiky-drop chute tick falling from bay to hull.
            if (spikyTimer >= spikyNext - 30)
            {
                float kindle = (spikyTimer - (spikyNext - 30f)) / 30f;
                MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, -30f), 10f + 8f * kindle, new Color(255, 200, 100), 0.25f + 0.3f * kindle);
                Vector2 bay = Projectile.Center + new Vector2(0f, -30f) - Main.screenPosition;
                Vector2 hull = Projectile.Center - Main.screenPosition;
                Vector2 fallTip = Vector2.Lerp(bay, hull, kindle);
                MiniYoyoFx.DrawLine(bay, fallTip, 2f, new Color(255, 210, 130, 150) * (0.3f + 0.5f * kindle));
            }

            // MAJOR A (shower half): single kindle while presents stream out
            // (stacked column removed: it read as random circles above the yoyo).
            if (presentQueue > 0)
            {
                MiniYoyoFx.DrawGlow(Projectile.Center, 16f, new Color(255, 170, 200), 0.25f);
            }

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
