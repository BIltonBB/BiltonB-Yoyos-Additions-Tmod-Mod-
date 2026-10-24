using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;

namespace BiltonsYoyoMod.Projectiles
{
    public class GolemYoyoProjL : ModProjectile
    {
        // SETTINGS

        // 5 seconds.
        private const int Lifetime = 300;

        // Fade during final second.
        private const int FadeOutTime = 60;

        // Visually half-sized.
        private const float VisualScale = 0.50f;

        // Fast laser projectile.
        private const float Speed = 16f;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = false;

            Projectile.ignoreWater = true;

            Projectile.penetrate = 1;

            Projectile.timeLeft =
                Lifetime;

            Projectile.DamageType =
                DamageClass.Melee;

            Projectile.usesLocalNPCImmunity = true;

            Projectile.localNPCHitCooldown =
                12;


            ProjectileID.Sets.TrailCacheLength[
                Projectile.type
            ] = 12;

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


            /*
             * Keep the projectile at constant speed.
             */
            if (
                Projectile.velocity.LengthSquared() >
                0.001f
            )
            {
                Projectile.velocity =
                    Projectile.velocity.SafeNormalize(
                        Vector2.UnitX
                    )
                    *
                    Speed;


                // FACE MOVEMENT DIRECTION

                /*
                 * No spinning.
                 *
                 * The sprite simply points where the projectile
                 * is moving.
                 */
                Projectile.rotation =
                    Projectile.velocity.ToRotation();
            }


            // YELLOW LIGHT

            Lighting.AddLight(
                Projectile.Center,
                1.0f * fade,
                0.75f * fade,
                0.08f * fade
            );


            // SMALL YELLOW PARTICLES

            if (
                Main.rand.NextBool(4)
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            2f,
                            2f
                        ),
                        DustID.GoldFlame,
                        Main.rand.NextVector2Circular(
                            0.5f,
                            0.5f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.25f,
                            0.5f
                        )
                    );


                dust.noGravity = true;
            }
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


                // WORLD LIGHTING

                Color tileLight =
                    Lighting.GetColor(
                        (int)(worldPosition.X / 16f),
                        (int)(worldPosition.Y / 16f)
                    );


                // SHADOW PASS

                byte shadowAlpha =
                    (byte)(
                        28f *
                        fade
                    );


                Main.EntitySpriteDraw(
                    texture,
                    position +
                    new Vector2(
                        1f,
                        1.5f
                    ),
                    null,
                    new Color(
                        10,
                        10,
                        10,
                        shadowAlpha
                    ),
                    Projectile.rotation,
                    texture.Size() *
                    0.5f,
                    VisualScale,
                    SpriteEffects.None,
                    0f
                );


                // LIGHTING-AWARE YELLOW AFTERIMAGE

                Color litColor =
                    new Color(
                        tileLight.R,
                        tileLight.G,
                        tileLight.B,
                        (byte)(
                            110f *
                            fade
                        )
                    );


                /*
                 * Blend the actual world light with yellow.
                 *
                 * This keeps the afterimage affected by the
                 * environment instead of being a flat yellow copy.
                 */
                litColor =
                    Color.Lerp(
                        litColor,
                        new Color(
                            255,
                            215,
                            60
                        ),
                        0.65f
                    );


                Main.EntitySpriteDraw(
                    texture,
                    position,
                    null,
                    litColor,
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
                    225,
                    70,
                    100
                ),
                0f,
                sun.Size() *
                0.5f,
                0.045f *
                VisualScale /
                0.5f,
                SpriteEffects.None,
                0f
            );


            // ACTUAL LASER

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                texture.Size() *
                0.5f,
                VisualScale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}