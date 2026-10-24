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
    public class DarkMageYoyoProj : ModProjectile
    {
        private int ritualTimer;
        private int ritualInterval = 1200;
        private int ritualKindle = 60;
        private int hitCount;
        private float ritualSync = -1f;
        private Vector2 ritualCenter;
        private readonly List<Vector2> trail = new List<Vector2>();

        // Ritual cadence comes from the server config (default 2s, so the seal and
        // its bolt crowns are easy to watch; the old hardcoded value was 20s). The
        // kindle wind-up scales with the cycle so a short cadence never sits in a
        // permanent summon wind-up.
        private static int GetRitualIntervalTicks()
        {
            float seconds = 2f;
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoServerConfig>();
                if (cfg != null)
                    seconds = cfg.DarkMageRitualCooldownSeconds;
            }
            catch { }
            return (int)MathHelper.Clamp(seconds * 60f, 30f, 3600f);
        }

        public override void SetStaticDefaults()
        {
            // M8-support: record recent positions/rotations for vanilla afterimage ghosts.
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
            Projectile.scale = 1f;

            // 22s deployment: the ritual needs a full continuous 20s out to trigger
            // (at 12s the timer reset on every throw and the seal never came).
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 22f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 240f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 12f;
        }

        // M1: lift the sprite slightly out of darkness so the purple reads on dark backgrounds.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        public override void AI()
        {
            MiniYoyoFx.RibbonPush(trail, Projectile.Center, 10f, 12);
            MiniYoyoFx.ApplyMotionReadability(Projectile);

            // Refresh the configured cadence (read live, no reload needed). The
            // kindle wind-up is a fifth of the cycle, clamped to 12-60 ticks.
            ritualInterval = GetRitualIntervalTicks();
            ritualKindle = (int)MathHelper.Clamp(ritualInterval / 5, 12, 60);
            int kindleStart = ritualInterval - ritualKindle;

            // M2: deterministic visual spin only (hitbox is a square; rotation never affects collision).
            Projectile.rotation += 0.18f;

            // Spell sync: every 10 ticks, read the owned ritual's charge so the
            // yoyo and its seal visibly beat as one spell (-1 = no ritual out).
            if (ritualTimer % 10 == 0)
            {
                ritualSync = -1f;
                int ritualType = ModContent.ProjectileType<DarkMageYoyoMagicRitual>();
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.owner == Projectile.owner && p.type == ritualType)
                    {
                        ritualSync = ((DarkMageYoyoMagicRitual)p.ModProjectile).Charge;
                        ritualCenter = p.Center;
                        break;
                    }
                }
            }

            // Sparse directional streak, doubling while the summon kindles.
            int wispRate = ritualTimer >= kindleStart ? 3 : 6;
            if (Main.rand.NextBool(wispRate) && Projectile.velocity.LengthSquared() > 4f)
            {
                Vector2 drift = -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.4f, 0.4f);
                Dust wisp = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.PurpleTorch, drift, 120, default, 0.85f);
                wisp.noGravity = true;
            }

            // M5: fast-move micro-sparks snapped to the flight direction (speed-gated, throttled).
            if (Projectile.velocity.LengthSquared() > 25f && Main.rand.NextBool(8))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust spark = Dust.NewDustPerfect(Projectile.Center - back * 6f, DustID.MagicMirror,
                    -back * 1.2f + Main.rand.NextVector2Circular(0.5f, 0.5f), 100, default, 0.7f);
                spark.noGravity = true;
            }

            // M4: string shimmer — one mote drifting along the player->yoyo line (visual only).
            if (Main.rand.NextBool(12) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player owner = Main.player[Projectile.owner];
                if (owner != null && owner.active)
                {
                    Vector2 pos = Vector2.Lerp(owner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.PurpleTorch, new Vector2(0f, -0.4f), 140, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            // M3: pulsing purple light (flickers with the spell beat instead of a flat value).
            float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GameUpdateCount * 0.06f);
            Lighting.AddLight(Projectile.Center, 0.55f + 0.12f * pulse, 0.22f + 0.05f * pulse, 0.7f + 0.12f * pulse);

            // Every `ritualInterval` ticks (config; default 2s) spawn a stationary
            // ritual at the yoyo position.
            ritualTimer++;
            if (ritualTimer >= ritualInterval)
            {
                ritualTimer = 0;
                if (Main.myPlayer == Projectile.owner)
                {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<DarkMageYoyoMagicRitual>(), Projectile.damage / 2, 0f, Projectile.owner);
                    // Summon helix: 10 motes corkscrewing up + expanding rune ring + chime.
                    // (The ritual is the payoff of the cycle, so it arrives loud.)
                    for (int i = 0; i < 10; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 10f;
                        int helixType = i % 2 == 0 ? DustID.MagicMirror : DustID.PurpleTorch;
                        Dust helix = Dust.NewDustPerfect(Projectile.Center + angle.ToRotationVector2() * 12f, helixType, new Vector2(-angle.ToRotationVector2().Y, -2.4f), 100, default, 1.1f);
                        helix.noGravity = true;
                    }
                    for (int i = 0; i < 12; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 12f;
                        Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, angle.ToRotationVector2() * 4f, 100, default, 1.1f);
                        ring.noGravity = true;
                    }
                    Lighting.AddLight(Projectile.Center, 1.2f, 0.7f, 1.4f);
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item27, Projectile.Center);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Layer 1: motion ribbon following the yoyo's real path (tapered, fading tail).
            MiniYoyoFx.RibbonDrawSoft(trail, Projectile.type);

            // Layer 1b: vanilla afterimage ghosts (oldPos/oldRot, oldest->newest so overlaps read correctly).
            DrawAfterimages(lightColor);

            // Layer 2: rune glow beating with the owned ritual's charge when one is out.
            float beat = ritualSync >= 0f ? ritualSync : 0.5f + 0.5f * (float)Math.Sin(Main.GameUpdateCount * 0.06f);
            int kindleStart = ritualInterval - ritualKindle;
            Color seat = Color.Lerp(new Color(150, 80, 255), new Color(225, 200, 255), ritualTimer >= kindleStart ? (ritualTimer - kindleStart) / (float)ritualKindle * 0.7f : 0f);
            MiniYoyoFx.DrawGlow(Projectile.Center, 24f + 4f * beat, seat, 0.3f + 0.15f * beat);

            // Layer 3 (kindle window only): ritual-summon kindle.
            if (ritualTimer >= kindleStart)
            {
                float kindle = (ritualTimer - kindleStart) / (float)ritualKindle;
                MiniYoyoFx.DrawRing(Projectile.Center, 26f - 8f * kindle, 2f, new Color(190, 130, 255) * (0.3f + 0.5f * kindle), 24);
                MiniYoyoFx.DrawGlow(Projectile.Center, 30f + 24f * kindle, new Color(220, 190, 255), 0.25f + 0.3f * kindle);
            }
            // Etched circle: while the ritual lives, its seal stays drawn on the
            // ground where it was cast (static ring, no pulse - the yoyo keeps the beat).
            if (ritualSync >= 0f)
            {
                MiniYoyoFx.DrawRing(ritualCenter, 26f, 2f, new Color(150, 80, 255) * 0.5f, 28);
                MiniYoyoFx.DrawGlow(ritualCenter, 20f, new Color(150, 80, 255), 0.18f);
            }
            return true;
        }

        private void DrawAfterimages(Color lightColor)
        {
            // Technique: tModLoader ExampleMod afterimage trail (Basic Projectile wiki) —
            // TrailCacheLength + TrailingMode recorded oldPos/oldRot, drawn oldest->newest
            // with age-faded alpha. Calamity's DrawAfterimagesCentered does the same.
            if (Projectile.oldPos == null || Projectile.oldPos.Length < 2)
                return;
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 halfHitbox = new Vector2(Projectile.width, Projectile.height) * 0.5f;
            Vector2 origin = tex.Size() * 0.5f;
            int ghosts = Math.Min(4, Projectile.oldPos.Length - 1);
            for (int k = ghosts; k >= 1; k--)
            {
                Vector2 oldCenter = Projectile.oldPos[k] + halfHitbox;
                // Skip uninitialized slots (zero vectors from spawn).
                if (oldCenter.LengthSquared() < 1f)
                    continue;
                float age = 1f - k / (float)(ghosts + 1);
                Color color = (GetAlpha(lightColor) ?? lightColor) * (0.28f * age);
                float rot = (Projectile.oldRot != null && k < Projectile.oldRot.Length) ? Projectile.oldRot[k] : Projectile.rotation;
                float scale = Projectile.scale * (1f - 0.08f * k / ghosts);
                Main.EntitySpriteDraw(tex, oldCenter - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                    null, color, rot, origin, scale, SpriteEffects.None, 0);
            }
        }

        public override void PostDraw(Color lightColor)
        {
            // No blob glow: PreDraw seat + lighting + _Glow file already carry
            // identity. PostDraw keeps only motion/condition reads.
            MiniYoyoFx.DrawFuryAura(Projectile);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MiniYoyoFx.ImpactFX(target.Center, new Color(200, 150, 255), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
            // Alternating heal seal per hit (gold / teal) so repeated hits don't
            // strobe one identical ring. 15 dust, all directional.
            hitCount++;
            int ringType = hitCount % 2 == 0 ? DustID.GoldFlame : DustID.Frost;
            for (int i = 0; i < 10; i++)
            {
                float angle = MathHelper.TwoPi * i / 10f;
                Dust ring = Dust.NewDustPerfect(target.Center, ringType, angle.ToRotationVector2() * 2.4f, 0, default, 1f);
                ring.noGravity = true;
            }

            for (int i = 0; i < 5; i++)
            {
                Vector2 pos = target.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-4f, 6f));
                float riseSpeed = i == 0 ? -3.4f : -2.2f;
                Dust rise = Dust.NewDustPerfect(pos, DustID.Frost, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), riseSpeed), 0, default, 0.85f);
                rise.noGravity = true;
            }

            // M6: heal cross-glints — 2 bright ticks snapped to the hit frame for a clean contact pop.
            for (int i = 0; i < 2; i++)
            {
                float angle = Main.GameUpdateCount * 0.1f + MathHelper.PiOver2 * i;
                Dust glint = Dust.NewDustPerfect(target.Center, DustID.MagicMirror, angle.ToRotationVector2() * 3f, 100, default, 0.9f);
                glint.noGravity = true;
            }

            Lighting.AddLight(target.Center, 0.6f, 0.5f, 0.7f);

            // Heal nearby players (within 300px) for 3 HP each
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player.active && !player.dead && Vector2.Distance(player.Center, target.Center) <= 300f)
                {
                    if (player.statLife < player.statLifeMax2)
                    {
                        player.statLife = Math.Min(player.statLifeMax2, player.statLife + 3);
                        player.HealEffect(3, false);
                        // Gold cross pulse over the healed ally (one kiss per heal).
                        Vector2 pc = player.Center;
                        YoyoParticles.SpawnSpark(pc, new Vector2(0f, -3f), new Color(255, 220, 130), 0.8f, 18, 0.95f);
                        YoyoParticles.SpawnSpark(pc, new Vector2(0f, 3f), new Color(255, 220, 130), 0.8f, 18, 0.95f);
                        YoyoParticles.SpawnSpark(pc, new Vector2(-3f, 0f), new Color(255, 220, 130), 0.8f, 18, 0.95f);
                        YoyoParticles.SpawnSpark(pc, new Vector2(3f, 0f), new Color(255, 220, 130), 0.8f, 18, 0.95f);
                        // Heal thread: gold motes walk the yoyo->ally line.
                        Vector2 thread = pc - Projectile.Center;
                        float threadLen = thread.Length();
                        if (threadLen > 1f)
                        {
                            Vector2 threadDir = thread / threadLen;
                            for (int m = 0; m < 3; m++)
                            {
                                Vector2 motePos = Projectile.Center + threadDir * (threadLen * (0.25f + 0.25f * m));
                                YoyoParticles.SpawnSpark(motePos, -threadDir * 1.5f, new Color(255, 220, 130), 0.7f, 20, 0.96f);
                            }
                        }
                    }
                    else
                    {
                        // Over-heal feedback: full allies burst gold instead.
                        for (int g = 0; g < 4; g++)
                        {
                            float gangle = MathHelper.TwoPi * g / 4f + Main.GameUpdateCount * 0.05f;
                            YoyoParticles.SpawnSpark(player.Center, gangle.ToRotationVector2() * 3f, new Color(255, 230, 150), 0.9f, 22, 0.95f);
                        }
                    }
                }
            }
        }
    }
}
