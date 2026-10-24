using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Developer yoyo. Goofy visuals (cycling rainbow dust, confetti light, wobble rotation).
    /// Every 6-10s (random) throws a DevYoyoRBProj at an enemy (speed 12, zero damage,
    /// huge knockback 20).
    /// </summary>
    public class DeveloperYoyoProj : ModProjectile
    {
        private int rbTimer;
        private int rbNext = 480;
        private bool firedOnce;
        private readonly List<Vector2> wake = new List<Vector2>();

        public override void SetStaticDefaults()
        {
            // Afterimage ghosts for the disco ball (ExampleMod Basic Projectile pattern).
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

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 260f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 16f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 12f;
        }

        // M1: disco lift so the mirror ball reads on dark nights.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        // M5: RB scheduling rides ExtraAI so remotes see the telegraph + snap,
        // not a stuck kindle (rbNext used to live owner-side only).
        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(rbTimer);
            writer.Write(rbNext);
            writer.Write(firedOnce);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            rbTimer = reader.ReadInt32();
            rbNext = reader.ReadInt32();
            firedOnce = reader.ReadBoolean();
        }

        public override void AI()
        {
            MiniYoyoFx.RibbonPushLegacy(wake, Projectile.Center, 10f, 12); // dev yoyo: wide 10 px history points
            MiniYoyoFx.ApplyMotionReadability(Projectile);

            Color disco = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            Lighting.AddLight(Projectile.Center, disco.R / 255f, disco.G / 255f, disco.B / 255f);
            // Disco floor: the tiles below the mirror ball cycle hue with it.
            if (Main.GameUpdateCount % 8 == 0)
            {
                Lighting.AddLight(Projectile.Center + new Vector2(0f, 64f), disco.R / 255f * 0.6f, disco.G / 255f * 0.6f, disco.B / 255f * 0.6f);
                Dust drop = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), 6f), DustID.Confetti, new Vector2(0f, 1.5f), 100, disco, 0.8f);
                drop.noGravity = false;
            }

            // Goofy wobble.
            Projectile.rotation = (float)Math.Sin(Main.GameUpdateCount * 0.2f + Projectile.whoAmI) * 0.5f;

            // Thin disco trail: rainbow wisp every 3rd tick + confetti pop every 8th
            // (was 1/3 + 1/6 + 1/8 + 1/4 wobble = ~70/s cloud).
            // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
            if (Main.GameUpdateCount % 3 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(7f, 7f), DustID.RainbowMk2, -Projectile.velocity * 0.05f, 100, default, 0.9f);
                dust.noGravity = true;
            }
            if (Main.rand.NextBool(8))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(9f, 9f), DustID.Confetti, Main.rand.NextVector2Circular(1.2f, 1.2f), 100, disco, 1f);
                dust.noGravity = true;
            }

            // M4: fast-move glitter spray kicked behind the mirror ball at pace.
            if (Projectile.velocity.LengthSquared() > 64f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust glitter = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.RainbowMk2, -back * 1.6f + Main.rand.NextVector2Circular(0.5f, 0.5f), 100, default, 0.9f);
                glitter.noGravity = true;
            }

            // M3: confetti string shimmer along the player->mirror-ball line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.Confetti, new Vector2(0f, -0.5f), 100, disco, 0.7f);
                    shimmer.noGravity = true;
                }
            }

            rbTimer++;

            if (rbTimer >= rbNext && Main.myPlayer == Projectile.owner)
            {
                NPC target = FindNearestEnemy(900f);
                if (target != null)
                {
                    Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);

                    Projectile rb = Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Projectile.Center,
                        dir * 12f, ModContent.ProjectileType<DevYoyoRBProj>(), 0, 20f, Projectile.owner);
                    rb.damage = 0;
                    rb.netUpdate = true;

                    // M6: premiere launch ring bursting off the mirror ball.
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 6f;
                        Dust launch = Dust.NewDustPerfect(Projectile.Center, DustID.RainbowMk2, angle.ToRotationVector2() * 3f, 100, default, 1.1f);
                        launch.noGravity = true;
                    }
                    Lighting.AddLight(Projectile.Center, 1.2f, 1.2f, 1.2f);
                    MiniYoyoFx.FlashFX(Projectile.Center, new Color(220, 170, 255), 1.2f);

                    rbTimer = 0;
                    rbNext = Main.rand.Next(360, 601); // 6-10s
                    // Disco rings: rainbow + white pooled rings on every premiere.
                    Color premiere = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
                    YoyoParticles.SpawnRing(Projectile.Center, 10f, 64f, 24, premiere, 3f);
                    YoyoParticles.SpawnRing(Projectile.Center, 6f, 40f, 20, Color.White, 2f);
                    firedOnce = true;
                    Projectile.netUpdate = true;
                }
                else
                {
                    rbTimer = rbNext - 60; // retry soon when no target
                    Projectile.netUpdate = true;
                }
            }
        }

        // M8: silly bonk tick for the mirror ball (dust + light only; damage untouched).
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MiniYoyoFx.ImpactFX(target.Center, new Color(190, 120, 245), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            MiniYoyoFx.HitShake(damageDone);
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
            Color disco = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + Main.GameUpdateCount * 0.15f;
                Dust bonk = Dust.NewDustPerfect(target.Center, DustID.Confetti, angle.ToRotationVector2() * 2.6f, 100, disco, 1f);
                bonk.noGravity = true;
            }
            Dust core = Dust.NewDustPerfect(target.Center, DustID.GoldFlame, new Vector2(0f, -1f), 100, default, 1f);
            core.noGravity = true;
            Lighting.AddLight(target.Center, disco.R / 255f, disco.G / 255f, disco.B / 255f);
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

        public override bool PreDraw(ref Color lightColor)
        {
            // Disco wake + cycling seat glow, breathing with the wobble
            // (same phase as the AI rotation so light and motion agree).
            // Beat quickens as the Rude Buster readies (synced schedule now).
            Color disco = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            MiniYoyoFx.RibbonDrawSoft(wake, new Color(120, 80, 160), disco, 9f);
            DrawAfterimages(lightColor);
            float readiness = MathHelper.Clamp(rbTimer / (float)Math.Max(1, rbNext), 0f, 1f);
            float breathe = 0.35f + 0.1f * (float)Math.Sin(Main.GameUpdateCount * (0.2f + 0.35f * readiness) + Projectile.whoAmI);
            // Hue-shift mode on the disco seat: colors cycle continuously.
            MiniYoyoFx.DrawWithShader(5, disco, 0.9f,
                () => MiniYoyoFx.DrawGlow(Projectile.Center, 12f, disco, breathe * 0.3f));

            // MAJOR B (entourage half): mirror-ball orbiters with short wakes
            // on wall-clock time.
            for (int i = 0; i < 6; i++)
            {
                float ang = Main.GameUpdateCount * 0.09f + MathHelper.TwoPi * i / 6f;
                Vector2 dir = ang.ToRotationVector2();
                Vector2 ballPos = Projectile.Center + dir * 26f;
                MiniYoyoFx.DrawGlow(ballPos, 4f, new Color(255, 255, 255), 0.2f);
                Vector2 side = new Vector2(-dir.Y, dir.X);
                MiniYoyoFx.DrawLine(ballPos - Main.screenPosition, ballPos - side * 10f - Main.screenPosition, 1.5f, new Color(255, 255, 255, 140) * 0.5f);
            }

            // M9: RB-charged halo — early warning a full second before the telegraph.
            if (rbTimer >= rbNext - 60)
            {
                float warn = (rbTimer - (rbNext - 60f)) / 60f;
                MiniYoyoFx.DrawRing(Projectile.Center, 30f, 2f, disco * (0.25f + 0.4f * warn), 24);
            }

            // RB launch telegraph (now remote-accurate via the synced schedule).
            if (rbTimer >= rbNext - 30)
            {
                float kindle = (rbTimer - (rbNext - 30f)) / 30f;
                NPC target = FindNearestEnemy(900f);
                if (target != null)
                {
                    Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    MiniYoyoFx.DrawGlow(Projectile.Center + dir * 15f, 10f + 8f * kindle, disco, 0.3f + 0.3f * kindle);
                }
            }

            // MAJOR B (snap half): premiere snap ring on the launch tick.
            if (firedOnce && rbTimer < 6)
            {
                float t = rbTimer / 6f;
                MiniYoyoFx.DrawRing(Projectile.Center, MathHelper.Lerp(14f, 26f, t), 2f, disco * (0.7f * (1f - t) + 0.1f), 24);
            }

            // Minimal overhaul: disco sparkle ring
            if (Main.GameUpdateCount % 10 == 0)
            {
                float ringT = (Main.GameUpdateCount % 30) / 30f;
                MiniYoyoFx.DrawRing(Projectile.Center, 20f + 10f * ringT, 1.5f, disco * (0.2f + 0.3f * (1f - ringT)), 20);
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
            int ghosts = Math.Min(3, Projectile.oldPos.Length - 1);
            for (int k = ghosts; k >= 1; k--)
            {
                Vector2 oldCenter = Projectile.oldPos[k] + halfHitbox;
                if (oldCenter.LengthSquared() < 1f)
                    continue;
                float age = 1f - k / (float)(ghosts + 1);
                Color color = (GetAlpha(lightColor) ?? lightColor) * (0.22f * age);
                float rot = (Projectile.oldRot != null && k < Projectile.oldRot.Length) ? Projectile.oldRot[k] : Projectile.rotation;
                Main.EntitySpriteDraw(tex, oldCenter - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                    null, color, rot, origin, Projectile.scale, SpriteEffects.None, 0);
            }
        }
    }
}
