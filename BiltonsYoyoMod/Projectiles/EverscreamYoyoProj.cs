using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    public class EverscreamYoyoProj : ModProjectile
    {
        private int needleTimer;
        private int ballTimer;
        private int ballToss; // every 20th ornament is the gold jackpot
        private int puffCount; // every 5th needle breathes a launch puff (cheap rhythm)
        private readonly List<Vector2> wake = new List<Vector2>();

        public override void SetStaticDefaults()
        {
            // Afterimage ghosts for the grove (ExampleMod Basic Projectile pattern).
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

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 270f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 12f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 12f;
        }

        // M1: grove lift so the evergreen reads on Frost Moon nights.
        // Fullbright lift killed: sprite now respects cave lighting like surroundings.
        public override Color? GetAlpha(Color lightColor)
        {
            return null;
        }

        public override void AI()
        {
            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 10f, 12);
            MiniYoyoFx.ApplyMotionReadability(Projectile);

            // M3: deterministic pine spin (square hitbox; never affects collision).
            Projectile.rotation += 0.13f;

            Lighting.AddLight(Projectile.Center, 0.45f, 0.6f, 0.3f);

            // Thin grove aura: alternating pine/ember every 4th tick (was ~80/s).
            // NOTE: yoyo timeLeft is pinned at 2 by the yoyo AI, so gate on GameUpdateCount.
            // Settling snow sifts around the grove (heavier while raining).
            if (Main.rand.NextBool(Main.raining ? 12 : 24))
            {
                Dust snow = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 6f), DustID.WhiteTorch, new Vector2(0f, -0.5f), 140, default, 0.7f);
                snow.noGravity = true;
            }
            if (Main.GameUpdateCount % 4 == 0)
            {
                int t = Main.GameUpdateCount % 8 == 0 ? DustID.GreenTorch : DustID.Torch;
                Dust mote = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), t, new Vector2(0f, -1f), 0, default, 0.8f);
                mote.noGravity = true;
            }

            // M5: fast-move pine spray kicked behind the grove when she runs.
            if (Projectile.velocity.LengthSquared() > 36f && Main.rand.NextBool(10))
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Dust spray = Dust.NewDustPerfect(Projectile.Center - back * 7f, DustID.GreenTorch, -back * 1.4f + Main.rand.NextVector2Circular(0.4f, 0.4f), 0, default, 0.8f);
                spray.noGravity = true;
            }

            // M4: pine string shimmer along the player->grove line.
            if (Main.rand.NextBool(14) && Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
            {
                Player lineOwner = Main.player[Projectile.owner];
                if (lineOwner != null && lineOwner.active)
                {
                    Vector2 pos = Vector2.Lerp(lineOwner.Center, Projectile.Center, Main.rand.NextFloat(0.1f, 0.95f));
                    Dust shimmer = Dust.NewDustPerfect(pos, DustID.GreenTorch, new Vector2(0f, -0.4f), 0, default, 0.6f);
                    shimmer.noGravity = true;
                }
            }

            if (Main.myPlayer != Projectile.owner)
                return;

            needleTimer++;
            if (needleTimer >= 6)
            {
                needleTimer = 0;
                SpawnNeedle();
            }

            ballTimer++;
            if (ballTimer >= 30)
            {
                ballTimer = 0;
                SpawnBall();
            }
        }

        private void SpawnNeedle()
        {
            Vector2 direction = (-Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f));
            // Needles leave from the yoyo's crown, already moving up.
            Vector2 muzzle = Projectile.Center + new Vector2(0f, -10f);
            Projectile spawned = Projectile.NewProjectileDirect(
                Projectile.GetSource_FromThis(),
                muzzle,
                direction * 16f,
                ModContent.ProjectileType<EverscreamYoyoNeedleProj>(),
                Math.Max(1, Projectile.damage / 2),
                Projectile.knockBack,
                Projectile.owner);
            spawned.netUpdate = true;
            // M6: every 5th needle breathes a launch puff (10/s hose stays lean).
            if (++puffCount % 5 == 0)
            {
                Dust puff = Dust.NewDustPerfect(Projectile.Center + new Vector2(0f, -6f), DustID.GreenTorch, new Vector2(0f, -1.5f), 0, default, 0.9f);
                puff.noGravity = true;
            }
        }

        private void SpawnBall()
        {
            ballToss++;
            Vector2 velocity = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-8f, -4f));
            // Two ornament variants split from the original 2-ball sprite.
            int ballType = Main.rand.NextBool(2)
                ? ModContent.ProjectileType<EverscreamYoyoBallProj2>()
                : ModContent.ProjectileType<EverscreamYoyoBallProj>();
            // Toss breath puff (SpawnBall only runs owner-side).
            MiniYoyoFx.FlashFX(Projectile.Center, new Color(150, 230, 150), 0.7f);
            // Gold jackpot every 20th ornament: flash + chime over the toss.
            if (ballToss % 20 == 0)
            {
                MiniYoyoFx.FlashFX(Projectile.Center, new Color(255, 215, 130), 1.3f);
                Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item29 with { Pitch = 0.5f }, Projectile.Center);
            }
            Dust toss = Dust.NewDustPerfect(Projectile.Center, DustID.JungleSpore, new Vector2(0f, -1.2f), 120, default, 0.9f);
            toss.noGravity = true;
            Projectile spawned = Projectile.NewProjectileDirect(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                velocity,
                ballType,
                Math.Max(1, Projectile.damage / 2),
                Projectile.knockBack,
                Projectile.owner);
            spawned.netUpdate = true;
            // M7: snow puff marking the ornament toss.
            for (int i = 0; i < 2; i++)
            {
                Dust snow = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, Main.rand.NextVector2Circular(1.5f, 1.5f), 0, default, 0.9f);
                snow.noGravity = true;
            }
        }

        // M9: pine tick for the grove itself (dust + light only; damage untouched).
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            MiniYoyoFx.ImpactFX(target.Center, new Color(200, 255, 200), MiniYoyoFx.HitScale(damageDone), Projectile.owner, (target.Center - Projectile.Center).ToRotation());
            // Kills confirm centrally via YoyoKillFX (covers sub-projectiles too).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + Main.GameUpdateCount * 0.1f;
                Dust tick = Dust.NewDustPerfect(target.Center, DustID.GreenTorch, angle.ToRotationVector2() * 2.4f, 0, default, 1f);
                tick.noGravity = true;
            }
            Lighting.AddLight(target.Center, 0.5f, 0.9f, 0.4f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Pine-grove identity (not charred orange): green wake + grove glow + needle kindle.
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type);
            DrawAfterimages(lightColor);
            MiniYoyoFx.DrawGlow(Projectile.Center, 16f, new Color(110, 200, 120), 0.3f);

            float kindle = needleTimer / 6f;
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, -8f), 10f + 10f * kindle, new Color(150, 230, 150), 0.25f + 0.3f * kindle);

            // MAJOR A (fan half): needle-volley fan beams extending with the kindle.
            if (needleTimer >= 4)
            {
                float needleKindle = (needleTimer - 4f) / 2f;
                for (int i = 0; i < 3; i++)
                {
                    float angle = -MathHelper.PiOver2 + (i - 1) * 0.3f;
                    Vector2 dir = angle.ToRotationVector2();
                    Vector2 base_ = Projectile.Center - Main.screenPosition;
                    Vector2 tip = Projectile.Center + dir * (8f + 8f * needleKindle) - Main.screenPosition;
                    MiniYoyoFx.DrawLine(base_, tip, 1.5f, new Color(200, 255, 200, 140) * (0.25f + 0.4f * needleKindle));
                    Vector2 needlePos = Projectile.Center + dir * 12f;
                    MiniYoyoFx.DrawGlow(needlePos, 6f + 4f * needleKindle, new Color(200, 255, 200), 0.2f + 0.2f * needleKindle);
                }
            }

            // Minimal overhaul: ball launch telegraph
            if (ballTimer >= 25)
            {
                float ballKindle = (ballTimer - 25f) / 5f;
                MiniYoyoFx.DrawGlow(Projectile.Center, 12f + 8f * ballKindle, new Color(255, 200, 100), 0.2f + 0.25f * ballKindle);
            }
            // MAJOR A (snap half): ornament-toss snap ring on the launch tick.
            if (ballTimer == 0)
                MiniYoyoFx.DrawRing(Projectile.Center, 20f, 2f, new Color(255, 210, 140) * 0.6f, 20);

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
