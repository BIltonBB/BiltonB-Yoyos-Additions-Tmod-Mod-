using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using Terraria.Audio;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class SolarYoyoProjF : ModProjectile
    {
        // SETTINGS

        private const int Lifetime = 300;

        private const float Gravity = 0.16f;

        private const int AfterimageCount = 6;

        private const float AfterimageSpacing = 8f;

        private const float SpriteScale = 0.55f;


        // STATE

        private bool initialized;

        private float distanceAccumulator;

        private bool explosionCreated;

        private Vector2[] afterimagePositions =
            new Vector2[AfterimageCount];


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;

            Projectile.friendly = true;
            Projectile.hostile = false;

            // F now collides with the ground.
            Projectile.tileCollide = true;

            Projectile.penetrate = 1;

            Projectile.ignoreWater = true;

            Projectile.timeLeft =
                Lifetime;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }


        // AI

        public override void AI()
        {
            // INITIALIZE AFTERIMAGES

            if (!initialized)
            {
                initialized = true;

                for (
                    int i = 0;
                    i < AfterimageCount;
                    i++
                )
                {
                    afterimagePositions[i] =
                        Projectile.Center;
                }
            }


            // GRAVITY

            Projectile.velocity.Y +=
                Gravity;


            // ROTATION

            Projectile.rotation +=
                0.12f;


            // DISTANCE-BASED AFTERIMAGES

            UpdateAfterimages();


            // SOLAR LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.75f,
                0.22f,
                0.03f
            );


            // FIRE PARTICLES

            if (Main.rand.NextBool(2))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            5f,
                            5f
                        ),
                        DustID.SolarFlare,
                        -Projectile.velocity *
                        0.08f,
                        50,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.15f
                        )
                    );


                dust.noGravity = true;
            }
        }


        // AFTERIMAGE UPDATE

        private void UpdateAfterimages()
        {
            float movement =
                Projectile.velocity.Length();


            distanceAccumulator +=
                movement;


            if (
                distanceAccumulator <
                AfterimageSpacing
            )
            {
                return;
            }


            distanceAccumulator = 0f;


            for (
                int i = AfterimageCount - 1;
                i > 0;
                i--
            )
            {
                afterimagePositions[i] =
                    afterimagePositions[i - 1];
            }


            afterimagePositions[0] =
                Projectile.Center;
        }


        // HIT NPC

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            target.AddBuff(
                BuffID.OnFire,
                300
            );


            CreateExplosion(
                Projectile.Center
            );
        }


        // TILE COLLISION

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            CreateExplosion(
                Projectile.Center
            );


            /*
             * Returning true allows the projectile to die normally.
             */
            return true;
        }


        // EXPLOSION

        private void CreateExplosion(
            Vector2 position)
        {
            if (explosionCreated)
            {
                return;
            }


            explosionCreated = true;


            // SOLAR BURST

            for (
                int i = 0;
                i < 22;
                i++
            )
            {
                Vector2 velocity =
                    Main.rand.NextVector2Unit() *
                    Main.rand.NextFloat(
                        2f,
                        5f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.SolarFlare,
                        velocity,
                        50,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.3f
                        )
                    );


                dust.noGravity = true;
            }


            // BRIGHT CORE

            for (
                int i = 0;
                i < 8;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Torch,
                        Main.rand.NextVector2Circular(
                            2f,
                            2f
                        ),
                        40,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.2f
                        )
                    );


                dust.noGravity = true;
            }


            Lighting.AddLight(
                position,
                1.0f,
                0.3f,
                0.02f
            );


            SoundEngine.PlaySound(
                SoundID.Item14,
                position
            );
        }


        // KILL

        public override void OnKill(
            int timeLeft)
        {
            /*
             * This catches normal expiration and any other
             * way the projectile is killed.
             *
             * explosionCreated prevents duplicate effects.
             */
            CreateExplosion(
                Projectile.Center
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


            // DISTANCE-BASED AFTERIMAGES

            for (
                int i = AfterimageCount - 1;
                i >= 1;
                i--
            )
            {
                Vector2 position =
                    afterimagePositions[i] -
                    Main.screenPosition;


                if (
                    Vector2.DistanceSquared(
                        afterimagePositions[i],
                        Projectile.Center
                    ) >
                    1000000f
                )
                {
                    continue;
                }


                Color lighting =
                    Lighting.GetColor(
                        (int)(
                            afterimagePositions[i].X /
                            16f
                        ),
                        (int)(
                            afterimagePositions[i].Y /
                            16f
                        )
                    );


                Color afterColor =
                    Color.Lerp(
                        lighting,
                        new Color(
                            255,
                            150,
                            40
                        ),
                        0.45f
                    );


                float opacity =
                    (
                        1f -
                        i /
                        (float)(AfterimageCount + 1)
                    ) *
                    0.32f;


                afterColor *=
                    opacity;


                Main.EntitySpriteDraw(
                    texture,
                    position,
                    null,
                    afterColor,
                    Projectile.rotation,
                    texture.Size() *
                    0.5f,
                    SpriteScale *
                    (
                        1f -
                        i *
                        0.03f
                    ),
                    SpriteEffects.None,
                    0f
                );
            }


            // SMALL FIRE GLOW

            Main.EntitySpriteDraw(
                TextureAssets.Sun2.Value,
                center,
                null,
                new Color(
                    255,
                    120,
                    30,
                    75
                ),
                0f,
                TextureAssets.Sun2.Value.Size() *
                0.5f,
                0.045f,
                SpriteEffects.None,
                0f
            );


            // BODY

            Color bodyColor =
                Color.Lerp(
                    Lighting.GetColor(
                        (int)(
                            Projectile.Center.X /
                            16f
                        ),
                        (int)(
                            Projectile.Center.Y /
                            16f
                        )
                    ),
                    new Color(
                        255,
                        190,
                        70
                    ),
                    0.55f
                );


            Main.EntitySpriteDraw(
                texture,
                center,
                null,
                bodyColor,
                Projectile.rotation,
                texture.Size() *
                0.5f,
                SpriteScale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}