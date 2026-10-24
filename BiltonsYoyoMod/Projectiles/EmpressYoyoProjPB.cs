using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.ID;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class EmpressYoyoProjPB : ModProjectile
    {
        private int age;
        private float turnPerFrame;
        private float turnSign;

        public override void SetDefaults()
        {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 180;
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
                float totalTurn =
                    MathHelper.ToRadians(
                        Main.rand.NextFloat(5f, 15f)
                    );

                turnSign = Main.rand.NextBool() ? 1f : -1f;

                turnPerFrame = Math.Min(
                    MathHelper.ToRadians(1f),
                    totalTurn / 60f
                );
            }

            if (age <= 60)
            {
                Projectile.velocity =
                    Projectile.velocity.RotatedBy(
                        turnPerFrame * turnSign
                    );

                Projectile.velocity *= 0.94f;
            }
            else
            {
                NPC target = GetTarget();
                float speed = Math.Max(1f, Projectile.ai[1]);

                if (target != null)
                {
                    Vector2 toTarget =
                        target.Center - Projectile.Center;

                    if (toTarget.LengthSquared() > 0.01f)
                    {
                        toTarget.Normalize();

                        Vector2 currentDirection =
                            Projectile.velocity.SafeNormalize(
                                Vector2.UnitX
                            );

                        Vector2 homingDirection =
                            Vector2.Lerp(
                                currentDirection,
                                toTarget,
                                0.18f
                            ).SafeNormalize(Vector2.UnitX);

                        Projectile.velocity =
                            homingDirection * speed;
                    }
                }
                else
                {
                    // No target = keep going straight.
                    Projectile.velocity =
                        Projectile.velocity.SafeNormalize(
                            Vector2.UnitX
                        ) * speed;
                }
            }

            // DO NOT CHANGE THIS ROTATION.
            Projectile.rotation =
                Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(
                Projectile.Center,
                GetGlowColor().ToVector3() * 1.65f
            );
        }

        private NPC GetTarget()
        {
            int index = (int)Projectile.ai[0];

            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC target = Main.npc[index];

            if (!target.active ||
                target.friendly ||
                target.dontTakeDamage ||
                target.life <= 0)
                return null;

            return target;
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

        private Color GetLitTrailColor(
            Vector2 worldPosition,
            Color glow)
        {
            int tileX = Math.Clamp(
                (int)(worldPosition.X / 16f),
                0,
                Main.maxTilesX - 1
            );

            int tileY = Math.Clamp(
                (int)(worldPosition.Y / 16f),
                0,
                Main.maxTilesY - 1
            );

            Color lighting =
                Lighting.GetColor(tileX, tileY);

            // Keep a small amount of emissive color even in dark tiles.
            int r = Math.Max(36, glow.R * lighting.R / 255);
            int g = Math.Max(36, glow.G * lighting.G / 255);
            int b = Math.Max(36, glow.B * lighting.B / 255);

            return new Color(r, g, b, 255);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture =
                TextureAssets.Projectile[Projectile.type].Value;

            Vector2 origin =
                texture.Size() * 0.5f;

            Color glow = GetGlowColor();

            // Distance-based afterimages.
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 oldCenter =
                    Projectile.oldPos[i] +
                    Projectile.Size * 0.5f;

                if (oldCenter == Vector2.Zero)
                    continue;

                float distance =
                    Vector2.Distance(
                        Projectile.Center,
                        oldCenter
                    );

                if (distance < 8f)
                    continue;

                float alpha =
                    MathHelper.Clamp(
                        1f - distance / 180f,
                        0.02f,
                        0.50f
                    ) * 0.40f;

                Color trailColor =
                    GetLitTrailColor(
                        oldCenter,
                        glow
                    ) * alpha;

                Main.spriteBatch.Draw(
                    texture,
                    oldCenter - Main.screenPosition,
                    null,
                    trailColor,
                    Projectile.rotation,
                    origin,
                    0.5f,
                    SpriteEffects.None,
                    0f
                );
            }

            float neonStrength =
                Main.dayTime ? 1.45f : 1.20f;

            // Existing base sprite.
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

            // Wide neon halo.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                glow * (0.24f * neonStrength),
                Projectile.rotation,
                origin,
                0.70f,
                SpriteEffects.None,
                0f
            );

            // Saturated neon band.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                glow * (0.48f * neonStrength),
                Projectile.rotation,
                origin,
                0.56f,
                SpriteEffects.None,
                0f
            );

            // Emissive white-hot core.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White * (0.46f * neonStrength),
                Projectile.rotation,
                origin,
                0.49f,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
