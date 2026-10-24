using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class EverscreamYoyoBallProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;

        // Ornament ribbon (a TrailProfile for this type was missing -> added).
        private readonly List<Vector2> wake = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 3;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 300;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M13: ornament lift, wired into the manual squash-and-stretch draw below.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(255, 210, 200), 0.28f) * Projectile.Opacity;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 5; i++)
                {
                    float angle = MathHelper.TwoPi * i / 5f;
                    int sparkType = i % 2 == 0 ? DustID.RedTorch : DustID.GreenTorch;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, sparkType, angle.ToRotationVector2() * 2.2f, 0, default, 0.9f);
                    dust.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.8f, 0.9f, 0.5f);
            }

            Projectile.velocity.Y += 0.35f;
            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;

            Projectile.rotation += Projectile.velocity.X * 0.02f;

            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 8f, 8);

            if (Projectile.timeLeft < 60)
            {
                Projectile.alpha += 4;
                if (Projectile.alpha > 255)
                    Projectile.alpha = 255;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.7f, 0.3f);

            // Lean ornament trail: one rising mote every 3rd tick (was ~80/s).
            if (Projectile.timeLeft % 3 == 0)
            {
                Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, new Vector2(0f, -0.8f), 0, default, 0.75f);
                trail.noGravity = true;
            }

            // M15: rare gold twinkle over the trail.
            if (Main.rand.NextBool(12))
            {
                Dust twinkle = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.GoldFlame, new Vector2(0f, -0.5f), 0, default, 0.8f);
                twinkle.noGravity = true;
            }

            // MAJOR C (spin half): rolling baubles wink gold off the rim at speed.
            if (Math.Abs(Projectile.velocity.X) + Math.Abs(Projectile.velocity.Y) > 2f && Main.rand.NextBool(8))
            {
                Vector2 rim = Projectile.Center + new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.UnitY) * 8f;
                Dust wink = Dust.NewDustPerfect(rim, DustID.GoldFlame, Vector2.Zero, 0, default, 0.85f);
                wink.noGravity = true;
            }

            // Bounce-squash recovery.
            if (Projectile.ai[1] > 0f)
                Projectile.ai[1]--;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Wired ornament ribbon under the ball (table profile).
            MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type);
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f, new Color(235, 120, 120), 0.4f);
            // Bounce squash-and-stretch: ai[1] counts down 10 ticks from each
            // tile hit, flattening across the impact then easing back to round.
            float squash = Projectile.ai[1] > 0f ? Projectile.ai[1] / 10f * 0.25f : 0f;
            Microsoft.Xna.Framework.Graphics.Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Microsoft.Xna.Framework.Vector2 origin = new Microsoft.Xna.Framework.Vector2(8.5f, 13.2f); // measured art centroid (19x24 canvas)
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, (GetAlpha(lightColor) ?? lightColor), Projectile.rotation, origin, new Microsoft.Xna.Framework.Vector2(1f + squash, 1f - squash), Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                int sparkType = i % 2 == 0 ? DustID.RedTorch : DustID.GreenTorch;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, sparkType, angle.ToRotationVector2() * 2.4f, 0, default, 0.9f);
                dust.noGravity = true;
            }
            // MAJOR C (pop half): snow over the ornament burst.
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                Dust snow = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, angle.ToRotationVector2() * 2f, 0, default, 0.9f);
                snow.noGravity = true;
            }
            // Synced vanilla slash sparkles over the pop (free MP sync).
            Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = Projectile.Center }, Projectile.owner);
            YoyoParticles.SpawnBloom(Projectile.Center, 5f, 22f, 20, new Color(255, 190, 170), 0.55f);
            Lighting.AddLight(Projectile.Center, 0.7f, 0.9f, 0.4f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.01f)
                Projectile.velocity.X = -oldVelocity.X * 0.65f;
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.01f)
                Projectile.velocity.Y = -oldVelocity.Y * 0.65f;
            Projectile.ai[1] = 10f;
            Projectile.netUpdate = true;
            for (int i = 0; i < 2; i++)
            {
                float angle = MathHelper.TwoPi * i / 2f;
                Dust bounce = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, angle.ToRotationVector2() * 1.6f, 0, default, 0.85f);
                bounce.noGravity = true;
            }
            // M14: bounce snow + MAJOR C (thud half): squashed landing ring + light pulse.
            for (int i = 0; i < 2; i++)
            {
                Dust snow = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 4f), DustID.WhiteTorch, new Vector2(0f, -1f), 0, default, 0.85f);
                snow.noGravity = true;
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 vel = angle.ToRotationVector2() * 2.4f;
                vel.Y *= 0.4f;
                Dust thud = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, vel, 0, default, 0.9f);
                thud.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.7f, 0.9f, 0.4f);
            return false;
        }
    }
}
