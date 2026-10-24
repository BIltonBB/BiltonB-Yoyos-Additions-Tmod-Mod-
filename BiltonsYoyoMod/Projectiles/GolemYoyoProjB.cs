using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;

namespace BiltonsYoyoMod.Projectiles
{
    public class GolemYoyoProjB : ModProjectile
    {
        // SETTINGS

        // 25 seconds.
        private const int Lifetime = 1500;

        // Fade during final 1 second.
        private const int FadeOutTime = 60;

        // Visual size = 50% of the normal projectile sprite.
        private const float VisualScale = 0.50f;

        // Gravity.
        private const float Gravity = 0.11f;

        private const float MaxFallSpeed = 10f;

        // Bounce strength.
        private const float BounceStrength = 0.72f;

        // Horizontal energy lost when bouncing.
        private const float BounceFriction = 0.86f;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            /*
             * IMPORTANT:
             *
             * B now collides with tiles so it can bounce.
             */
            Projectile.tileCollide = true;

            Projectile.ignoreWater = true;

            Projectile.penetrate = -1;

            Projectile.timeLeft = Lifetime;

            Projectile.DamageType =
                DamageClass.Melee;

            Projectile.usesLocalNPCImmunity = true;

            Projectile.localNPCHitCooldown =
                12;


            ProjectileID.Sets.TrailCacheLength[
                Projectile.type
            ] = 10;

            ProjectileID.Sets.TrailingMode[
                Projectile.type
            ] = 2;
        }


        // AI

        public override void AI()
        {
            float fade =
                Projectile.timeLeft <
                FadeOutTime
                    ? Projectile.timeLeft /
                      (float)FadeOutTime
                    : 1f;


            // GRAVITY

            Projectile.velocity.Y +=
                Gravity;


            if (
                Projectile.velocity.Y >
                MaxFallSpeed
            )
            {
                Projectile.velocity.Y =
                    MaxFallSpeed;
            }


            // ROTATION

            /*
             * Fireball rotates according to its movement.
             */
            if (
                Projectile.velocity.LengthSquared() >
                0.01f
            )
            {
                Projectile.rotation =
                    Projectile.velocity.ToRotation();
            }


            // RED FIRE TRAIL

            if (
                Main.rand.NextBool(2)
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            3f,
                            3f
                        ),
                        DustID.RedTorch,
                        -Projectile.velocity *
                        0.08f +
                        Main.rand.NextVector2Circular(
                            0.55f,
                            0.55f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.45f,
                            0.85f
                        )
                    );


                dust.noGravity = true;
            }


            // SMALL HOT PARTICLES

            if (
                Main.rand.NextBool(5)
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Torch,
                        Main.rand.NextVector2Circular(
                            0.8f,
                            0.8f
                        ),
                        120,
                        default,
                        Main.rand.NextFloat(
                            0.3f,
                            0.6f
                        )
                    );


                dust.noGravity = true;
            }


            // YELLOW LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.9f * fade,
                0.7f * fade,
                0.08f * fade
            );
        }


        // TILE BOUNCE

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            /*
             * Bounce vertically when hitting the ground/ceiling.
             */
            if (
                oldVelocity.Y !=
                Projectile.velocity.Y
            )
            {
                Projectile.velocity.Y =
                    -oldVelocity.Y *
                    BounceStrength;
            }


            /*
             * Bounce horizontally too, but with a little energy loss.
             */
            if (
                oldVelocity.X !=
                Projectile.velocity.X
            )
            {
                Projectile.velocity.X =
                    -oldVelocity.X *
                    BounceFriction;
            }


            /*
             * Prevent Terraria from deleting the projectile.
             */
            return false;
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                TextureAssets.Projectile[
                    Projectile.type
                ].Value;


            // AFTERIMAGES

            for (
                int i = 0;
                i < Projectile.oldPos.Length;
                i++
            )
            {
                float fade =
                    1f -
                    i /
                    (float)Projectile.oldPos.Length;


                Vector2 worldPosition =
                    Projectile.oldPos[i] +
                    Projectile.Size *
                    0.5f;


                Vector2 position =
                    worldPosition -
                    Main.screenPosition;


                /*
                 * Get the actual lighting around each
                 * old position.
                 */
                Color tileLight =
                    Lighting.GetColor(
                        (int)(worldPosition.X / 16f),
                        (int)(worldPosition.Y / 16f)
                    );


                // SHADOW PASS

                byte shadowAlpha =
                    (byte)(
                        35f *
                        fade
                    );


                Color shadowColor =
                    new Color(
                        15,
                        10,
                        8,
                        shadowAlpha
                    );


                Main.EntitySpriteDraw(
                    texture,
                    position +
                    new Vector2(
                        1.5f,
                        2f
                    ),
                    null,
                    shadowColor,
                    Projectile.rotation,
                    texture.Size() *
                    0.5f,
                    VisualScale,
                    SpriteEffects.None,
                    0f
                );


                // LIGHTING-AWARE AFTERIMAGE

                Color afterimageColor =
                    new Color(
                        tileLight.R,
                        tileLight.G,
                        tileLight.B,
                        (byte)(
                            95f *
                            fade
                        )
                    );


                /*
                 * Add a slight warm fire tint without
                 * overriding the world lighting.
                 */
                afterimageColor =
                    Color.Lerp(
                        afterimageColor,
                        new Color(
                            210,
                            55,
                            35
                        ),
                        0.35f
                    );


                Main.EntitySpriteDraw(
                    texture,
                    position,
                    null,
                    afterimageColor,
                    Projectile.rotation,
                    texture.Size() *
                    0.5f,
                    VisualScale,
                    SpriteEffects.None,
                    0f
                );
            }


            // YELLOW GLOW

            Texture2D sun =
                TextureAssets.Sun2.Value;


            Main.EntitySpriteDraw(
                sun,
                Projectile.Center -
                Main.screenPosition,
                null,
                new Color(
                    255,
                    220,
                    70,
                    85
                ),
                0f,
                sun.Size() *
                0.5f,
                0.035f *
                VisualScale /
                0.5f,
                SpriteEffects.None,
                0f
            );


            // ACTUAL FIREBALL

            Color bodyColor =
                lightColor;


            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                bodyColor,
                Projectile.rotation,
                texture.Size() *
                0.5f,
                VisualScale,
                SpriteEffects.None,
                0f
            );


            return false;
        }


        // END EFFECT

        public override void OnKill(
            int timeLeft)
        {
            for (
                int i = 0;
                i < 14;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.RedTorch,
                        Main.rand.NextVector2Circular(
                            2.5f,
                            2.5f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.5f,
                            1f
                        )
                    );


                dust.noGravity = true;
            }
        }
    }
}