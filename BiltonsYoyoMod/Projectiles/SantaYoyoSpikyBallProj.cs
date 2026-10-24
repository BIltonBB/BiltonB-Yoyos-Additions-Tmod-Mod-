using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class SantaYoyoSpikyBallProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 420;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.TwoPi * i / 6f;
                    int launchType = i % 2 == 0 ? DustID.RedTorch : DustID.GreenTorch;
                    Dust fx = Dust.NewDustPerfect(Projectile.Center, launchType, angle.ToRotationVector2() * 2.4f, 0, default, 1f);
                    fx.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.4f, 0.4f);
            }

            Projectile.velocity.Y += 0.5f;
            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;

            if (Projectile.ai[0] == 1f)
                Projectile.velocity.X *= 0.98f;

            Projectile.rotation += Projectile.velocity.X * 0.05f;

            Lighting.AddLight(Projectile.Center, 0.6f, 0.25f, 0.25f);

            // Lean ornament trail, shed only while rolling fast (was every tick).
            // M13 (glint half): rolling gold winks off the paint.
            if (Projectile.timeLeft % 3 == 0 && Math.Abs(Projectile.velocity.X) + Math.Abs(Projectile.velocity.Y) > 2f)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), Projectile.timeLeft % 6 == 0 ? DustID.RedTorch : DustID.GreenTorch, -Projectile.velocity * 0.05f, 0, default, 0.8f);
                dust.noGravity = true;
                if (Main.rand.NextBool(12))
                {
                    Dust glint = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), DustID.GoldFlame, Vector2.Zero, 0, default, 0.9f);
                    glint.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Single orb pinned to the sprite draw point (dome removed: its
            // +10px offset is what read as off-center while rolling).
            Vector2 drawPoint = Projectile.Center + new Vector2(0f, Projectile.gfxOffY);
            MiniYoyoFx.DrawGlow(drawPoint, 24f, new Color(235, 100, 100), 0.3f);
            Microsoft.Xna.Framework.Graphics.Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Main.EntitySpriteDraw(texture, drawPoint - Main.screenPosition, null,
                lightColor * Projectile.Opacity, Projectile.rotation, texture.Size() * 0.5f, Projectile.scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Projectile.ai[0] == 0f)
            {
                Projectile.ai[0] = 1f;

                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.TwoPi * i / 6f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, angle.ToRotationVector2() * 3f, 0, default, 1f);
                    dust.noGravity = true;
                }
                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.TwoPi * i / 6f + MathHelper.PiOver4;
                    int craterType = i % 2 == 0 ? DustID.RedTorch : DustID.GreenTorch;
                    // Crater spray kicks back along the incoming roll direction.
                    Vector2 kick = -oldVelocity.SafeNormalize(Vector2.UnitY) * 2f + new Vector2(0f, -1f);
                    Dust crater = Dust.NewDustPerfect(Projectile.Center, craterType, angle.ToRotationVector2() * 2.2f + kick, 0, default, 1f);
                    crater.noGravity = true;
                }
                for (int i = 0; i < 2; i++)
                {
                    Dust puff = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1f), 0, default, 1.2f);
                    puff.noGravity = true;
                }
                // M13 (landing half): wrapping-paper scraps over the crater dust.
                for (int i = 0; i < 2; i++)
                {
                    Dust wrap = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, Main.rand.NextVector2Circular(2.5f, 2.5f), 0, default, 1f);
                    wrap.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.4f, 0.35f);
                // Synced vanilla slash sparkles over the landing (free MP sync).
                Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                    new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = Projectile.Center }, Projectile.owner);
                YoyoParticles.SpawnBloom(Projectile.Center, 6f, 22f, 20, new Color(255, 200, 150), 0.5f);
                SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);

                float dir = oldVelocity.X != 0f ? Math.Sign(oldVelocity.X) : (Main.rand.NextBool() ? 1f : -1f);
                Projectile.velocity.X = dir * 4f;
                Projectile.velocity.Y = -oldVelocity.Y * 0.4f;
                if (Math.Abs(Projectile.velocity.Y) < 1f)
                    Projectile.velocity.Y = -2f;
            }
            else
            {
                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.01f)
                    Projectile.velocity.X = -oldVelocity.X * 0.7f;
                if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.01f)
                    Projectile.velocity.Y = -oldVelocity.Y * 0.4f;
            }

            return false;
        }
    }
}
