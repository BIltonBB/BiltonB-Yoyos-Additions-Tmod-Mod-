using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class EmpressYoyoProjEL : ModProjectile
    {
        private int age;
        private bool fired;
        private Vector2 firingDirection;
        private float firingSpeed;
        private float baseAngle;

        public override void SetDefaults()
        {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void AI()
        {
            age++;

            if (age == 1)
            {
                firingSpeed = Math.Max(1f, Projectile.ai[1]);
                firingDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                baseAngle = firingDirection.ToRotation();
            }

            if (!fired)
            {
                Projectile.velocity = Vector2.Zero;

                float progress = MathHelper.Clamp(age / 60f, 0f, 1f);
                const float totalTurns = 2f;

                // Starts exactly at its actual facing, makes two full turns,
                // and returns to that exact same facing while its angular speed
                // continuously decreases to zero.
                float easedSpin = 2f * progress - progress * progress;
                float spinAngle =
                    baseAngle + MathHelper.TwoPi * totalTurns * easedSpin;

                Projectile.rotation = spinAngle;

                if (age >= 60)
                {
                    fired = true;
                    Projectile.velocity = firingDirection * firingSpeed;
                    Projectile.rotation = baseAngle;
                    Projectile.netUpdate = true;
                }
            }
            else
            {
                Projectile.velocity = firingDirection * firingSpeed;
                Projectile.rotation = firingDirection.ToRotation();
            }

            Color glow = GetGlowColor();
            Lighting.AddLight(Projectile.Center, glow.ToVector3() * 1.5f);

            if (fired && Main.rand.NextBool(5))
            {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.PinkTorch,
                    -firingDirection * 0.55f,
                    120,
                    glow,
                    0.65f
                );
                dust.noGravity = true;
            }
        }

        private Color GetGlowColor()
        {
            int mode = (int)Projectile.ai[2];
            if (mode == 2)
                return new Color(255, 225, 90);

            return mode == 0
                ? new Color(255, 100, 210)
                : new Color(100, 180, 255);
        }

        private Color GetLitTrailColor(Vector2 worldPosition, Color glow)
        {
            int tileX = Math.Clamp((int)(worldPosition.X / 16f), 0, Main.maxTilesX - 1);
            int tileY = Math.Clamp((int)(worldPosition.Y / 16f), 0, Main.maxTilesY - 1);
            Color lighting = Lighting.GetColor(tileX, tileY);

            return new Color(
                glow.R * lighting.R / 255,
                glow.G * lighting.G / 255,
                glow.B * lighting.B / 255,
                255
            );
        }

        private void DrawLineSegment(Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            if (length <= 0.01f)
                return;

            Vector2 midpoint = (start + end) * 0.5f;

            Main.spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                midpoint - Main.screenPosition,
                new Rectangle(0, 0, 1, 1),
                color,
                delta.ToRotation(),
                new Vector2(0.5f, 0.5f),
                new Vector2(length, thickness),
                SpriteEffects.None,
                0f
            );
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Color glow = GetGlowColor();

            if (!fired)
            {
                const float warningLength = 1800f;
                float fade = 1f - MathHelper.Clamp((age - 1f) / 59f, 0f, 1f);

                Vector2 start = Projectile.Center + firingDirection * 10f;
                Vector2 end = start + firingDirection * warningLength;

                DrawLineSegment(start, end, glow * (Main.dayTime ? 0.62f : 0.46f) * fade, Main.dayTime ? 1.6f : 1.0f);

                // Thin white emissive core keeps the telegraph visible in daylight.
                DrawLineSegment(start, end, Color.White * (Main.dayTime ? 0.28f : 0.12f) * fade, 0.7f);
            }

            if (fired)
            {
                for (int i = 0; i < Projectile.oldPos.Length; i++)
                {
                    Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                    if (oldCenter == Vector2.Zero)
                        continue;

                    float distance = Vector2.Distance(Projectile.Center, oldCenter);
                    if (distance < 8f)
                        continue;

                    float alpha = MathHelper.Clamp(1f - distance / 220f, 0.02f, 0.48f) * 0.34f;
                    Color trailColor = GetLitTrailColor(oldCenter, glow) * alpha;

                    Main.spriteBatch.Draw(
                        texture,
                        oldCenter - Main.screenPosition,
                        null,
                        trailColor,
                        firingDirection.ToRotation(),
                        origin,
                        0.5f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            float neonStrength = Main.dayTime ? 1.25f : 1f;

            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                origin,
                0.5f,
                SpriteEffects.None,
                0f
            );

            // Outer neon halo.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                glow * (0.22f * neonStrength),
                Projectile.rotation,
                origin,
                0.66f,
                SpriteEffects.None,
                0f
            );

            // Tight colored band.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                glow * (0.42f * neonStrength),
                Projectile.rotation,
                origin,
                0.54f,
                SpriteEffects.None,
                0f
            );

            // Emissive core.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White * (0.38f * neonStrength),
                Projectile.rotation,
                origin,
                0.50f,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
