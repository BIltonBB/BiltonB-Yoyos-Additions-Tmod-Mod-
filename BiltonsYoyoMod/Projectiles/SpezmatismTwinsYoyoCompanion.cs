using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class SpezmatismTwinsYoyoCompanion : ModProjectile
    {
        private const float OrbitRadius = 70f;
        private const float OrbitSpeed = 0.045f;

        private const int SprayCooldown = 600;
        private const int SprayProjectileCount = 72;

        private int sprayCooldown;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = false;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.tileCollide = false;

            Projectile.timeLeft = 10;
        }


        // AI

        public override void AI()
        {
            int parentIndex =
                (int)Projectile.ai[0];


            if (
                parentIndex < 0 ||
                parentIndex >= Main.maxProjectiles
            )
            {
                Projectile.Kill();
                return;
            }


            Projectile parent =
                Main.projectile[parentIndex];


            if (
                !parent.active ||
                parent.type !=
                ModContent.ProjectileType<
                    RetinazerTwinsYoyoProj>() ||
                parent.owner != Projectile.owner
            )
            {
                Projectile.Kill();
                return;
            }


            Projectile.timeLeft = 10;


            // ORBIT

            float angle =
                Main.GameUpdateCount *
                OrbitSpeed;

            angle += MathHelper.Pi;


            Vector2 desiredPosition =
                parent.Center +
                new Vector2(
                    OrbitRadius,
                    0f
                ).RotatedBy(angle);


            Vector2 difference =
                desiredPosition -
                Projectile.Center;


            if (difference.LengthSquared() > 0.01f)
            {
                Projectile.velocity =
                    Vector2.Lerp(
                        Projectile.velocity,
                        difference * 0.18f,
                        0.20f
                    );


                Projectile.Center +=
                    Projectile.velocity;


                if (
                    Projectile.velocity.LengthSquared() >
                    0.01f
                )
                {
                    Projectile.rotation =
                        Projectile.velocity.ToRotation();
                }
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.25f,
                0.8f,
                0.25f
            );


            // SPRAY

            sprayCooldown++;


            if (
                sprayCooldown >= SprayCooldown &&
                Main.myPlayer == Projectile.owner
            )
            {
                sprayCooldown = 0;

                FireSpray(parent);
            }
        }


        // SPRAY

        private void FireSpray(
            Projectile parent)
        {
            int spitDamage =
                Math.Max(
                    1,
                    parent.damage / 2
                );


            float startAngle =
                MathHelper.PiOver2;


            float angleStep =
                MathHelper.TwoPi /
                SprayProjectileCount;


            for (
                int i = 0;
                i < SprayProjectileCount;
                i++
            )
            {
                float angle =
                    startAngle +
                    angleStep * i;


                Vector2 direction =
                    Vector2.UnitX.RotatedBy(
                        angle
                    );


                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    direction * 8f,
                    ModContent.ProjectileType<
                        SpezmatismTwinsYoyoSpitProj>(),
                    spitDamage,
                    0f,
                    Projectile.owner
                );
            }


            // RADIOACTIVE DUST

            for (int i = 0; i < 25; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        4f,
                        4f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.GreenFairy,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1.2f
                        )
                    );


                dust.noGravity = true;
            }
        }


        // DRAW + STRING

        public override bool PreDraw(
            ref Color lightColor)
        {
            int parentIndex =
                (int)Projectile.ai[0];


            if (
                parentIndex < 0 ||
                parentIndex >= Main.maxProjectiles
            )
            {
                return false;
            }


            Projectile parent =
                Main.projectile[parentIndex];


            if (!parent.active)
                return false;


            // STRING

            Vector2 start =
                parent.Center -
                Main.screenPosition;


            Vector2 end =
                Projectile.Center -
                Main.screenPosition;


            Vector2 difference =
                end - start;


            float length =
                difference.Length();


            if (
                length > 2f &&
                length < 1000f
            )
            {
                Vector2 midpoint =
                    (start + end) *
                    0.5f;


                float rotation =
                    difference.ToRotation();


                Texture2D pixel =
                    TextureAssets.MagicPixel.Value;


                Main.spriteBatch.Draw(
                    pixel,
                    midpoint,
                    new Rectangle(
                        0,
                        0,
                        1,
                        1
                    ),
                    new Color(
                        70,
                        15,
                        15,
                        255
                    ),
                    rotation,
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    new Vector2(
                        length,
                        3f
                    ),
                    SpriteEffects.None,
                    0f
                );


                Main.spriteBatch.Draw(
                    pixel,
                    midpoint,
                    new Rectangle(
                        0,
                        0,
                        1,
                        1
                    ),
                    new Color(
                        180,
                        40,
                        40,
                        255
                    ),
                    rotation,
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    new Vector2(
                        length,
                        1f
                    ),
                    SpriteEffects.None,
                    0f
                );
            }


            // COMPANION SPRITE

            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/RetinazerTwinsYoyoProj"
                ).Value;


            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                texture.Size() * 0.5f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}