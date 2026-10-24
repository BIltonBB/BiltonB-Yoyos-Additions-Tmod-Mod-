using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class LunaticYoyoProjI : ModProjectile
    {
        // SETTINGS

        private const int Lifetime = 150;

        // Fade during the final 20 ticks.
        private const int FadeOutTime = 20;

        private const float Speed = 8f;

        // Sprite only.
        private const float SpriteScale = 0.50f;


        // DEFAULTS

        public override void SetDefaults()
        {
            // Actual collision box.
            Projectile.width = 8;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.penetrate = 1;

            Projectile.timeLeft = Lifetime;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;

            Projectile.localNPCHitCooldown = 12;

            ProjectileID.Sets.TrailCacheLength[
                Projectile.type
            ] = 8;

            ProjectileID.Sets.TrailingMode[
                Projectile.type
            ] = 2;
        }


        // AI

        public override void AI()
        {
            // MOVEMENT

            if (
                Projectile.velocity.LengthSquared() >
                0.001f
            )
            {
                Projectile.velocity =
                    Projectile.velocity.SafeNormalize(
                        Vector2.UnitX
                    ) *
                    Speed;

                /*
                 * The sprite itself is apparently drawn 90 degrees
                 * off from the movement direction, so the correction
                 * is applied during drawing instead of changing the
                 * actual projectile velocity.
                 */
                Projectile.rotation =
                    Projectile.velocity.ToRotation();
            }


            // FADE OUT

            if (
                Projectile.timeLeft <
                FadeOutTime
            )
            {
                float fade =
                    Projectile.timeLeft /
                    (float)FadeOutTime;

                Projectile.alpha =
                    (int)MathHelper.Clamp(
                        255f *
                        (1f - fade),
                        0f,
                        255f
                    );
            }
            else
            {
                Projectile.alpha = 0;
            }


            // LIGHTING

            float lightFade =
                1f -
                Projectile.alpha /
                255f;

            Lighting.AddLight(
                Projectile.Center,
                0.15f * lightFade,
                0.45f * lightFade,
                0.75f * lightFade
            );
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                TextureAssets.Projectile[
                    Projectile.type
                ].Value;

            Vector2 center =
                Projectile.Center -
                Main.screenPosition;

            Color drawColor =
                lightColor;

            drawColor.A =
                (byte)MathHelper.Clamp(
                    255f -
                    Projectile.alpha,
                    0f,
                    255f
                );


            /*
             * +90 degrees fixes the sprite's native orientation
             * so that it points in the direction of movement.
             */
            float drawRotation =
                Projectile.rotation +
                MathHelper.PiOver2;


            /*
             * Only the sprite is 50% size.
             */
            Main.EntitySpriteDraw(
                texture,
                center,
                null,
                drawColor,
                drawRotation,
                texture.Size() * 0.5f,
                SpriteScale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}