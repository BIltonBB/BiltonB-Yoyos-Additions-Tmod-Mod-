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
    public class LunaticYoyoProjIP : ModProjectile
    {
        // SETTINGS

        private const int Lifetime = 360;
        private const int FadeTime = 60;

        private const int ShotInterval = 60;

        private const float PointSpeed = 1.5f;

        private const int ShotsPerWave = 6;

        private const float SpriteScale = 0.50f;


        // VISUAL SETTINGS

        // How far apart the real position-history afterimages are.
        private const int AfterimageSpacing = 2;

        private const int AfterimageCount = 6;

        private const int AuraDustFrequency = 2;

        private const int OrbitDustFrequency = 3;


        // STATE

        private int shotTimer;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;

            Projectile.friendly = false;
            Projectile.hostile = false;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.penetrate = -1;

            Projectile.timeLeft = Lifetime;
        }


        // AI

        public override void AI()
        {
            // MOVEMENT

            if (Projectile.velocity.LengthSquared() > 0.001f)
            {
                Projectile.velocity =
                    Projectile.velocity.SafeNormalize(Vector2.UnitX) *
                    PointSpeed;
            }


            // FADE

            float fade = GetFade();

            Projectile.alpha =
                (int)MathHelper.Clamp(
                    255f * (1f - fade),
                    0f,
                    255f
                );


            // SPIN

            Projectile.rotation += 0.070f;


            // ICE AURA

            if (Main.rand.NextBool(AuraDustFrequency))
            {
                Vector2 direction =
                    Main.rand.NextVector2Unit();

                float distance =
                    Main.rand.NextFloat(20f, 37f);

                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        direction * distance,
                        DustID.IceTorch,
                        direction *
                        Main.rand.NextFloat(0.35f, 1.25f),
                        80,
                        default,
                        Main.rand.NextFloat(0.55f, 1.10f) *
                        fade
                    );

                dust.noGravity = true;
            }


            // ORBITING FROST

            if (Main.rand.NextBool(OrbitDustFrequency))
            {
                float orbitAngle =
                    Main.GameUpdateCount * -0.075f +
                    Projectile.whoAmI * 1.37f;

                Vector2 orbit =
                    orbitAngle.ToRotationVector2();

                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        orbit * 30f,
                        DustID.Frost,
                        -orbit *
                        Main.rand.NextFloat(0.15f, 0.55f),
                        90,
                        default,
                        Main.rand.NextFloat(0.55f, 1.0f) *
                        fade
                    );

                dust.noGravity = true;
            }


            // INNER FROST PARTICLES

            if (Main.rand.NextBool(4))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            13f,
                            13f
                        ),
                        DustID.IceTorch,
                        Main.rand.NextVector2Circular(
                            0.25f,
                            0.25f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(0.4f, 0.75f) *
                        fade
                    );

                dust.noGravity = true;
            }


            // ICE WAVES

            shotTimer++;

            if (
                shotTimer >= ShotInterval &&
                Projectile.timeLeft > FadeTime
            )
            {
                shotTimer = 0;

                CreateWaveFlash();
                FireIceWave();
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.18f * fade,
                0.65f * fade,
                1.05f * fade
            );
        }


        // FADE

        private float GetFade()
        {
            int age =
                Lifetime -
                Projectile.timeLeft;

            float fadeIn =
                MathHelper.Clamp(
                    age / (float)FadeTime,
                    0f,
                    1f
                );

            float fadeOut =
                MathHelper.Clamp(
                    Projectile.timeLeft / (float)FadeTime,
                    0f,
                    1f
                );

            return Math.Min(
                fadeIn,
                fadeOut
            );
        }


        // WAVE FLASH

        private void CreateWaveFlash()
        {
            // Strong radial burst.
            for (int i = 0; i < 26; i++)
            {
                Vector2 direction =
                    Main.rand.NextVector2Unit();

                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        direction *
                        Main.rand.NextFloat(5f, 13f),
                        DustID.IceTorch,
                        direction *
                        Main.rand.NextFloat(2.5f, 5.5f),
                        45,
                        default,
                        Main.rand.NextFloat(0.7f, 1.25f)
                    );

                dust.noGravity = true;
            }


            // Small bright center.
            for (int i = 0; i < 12; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Frost,
                        Main.rand.NextVector2Circular(
                            2f,
                            2f
                        ),
                        70,
                        default,
                        Main.rand.NextFloat(0.5f, 0.95f)
                    );

                dust.noGravity = true;
            }


            Lighting.AddLight(
                Projectile.Center,
                0.45f,
                0.95f,
                1.45f
            );
        }


        // ICE WAVE

        private void FireIceWave()
        {
            for (int i = 0; i < ShotsPerWave; i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    ShotsPerWave;

                Vector2 direction =
                    angle.ToRotationVector2();

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    direction * 8f,
                    ModContent.ProjectileType<LunaticYoyoProjI>(),
                    Math.Max(
                        1,
                        Projectile.damage
                    ),
                    Projectile.knockBack,
                    Projectile.owner
                );
            }
        }


        // END EFFECT

        public override void OnKill(int timeLeft)
        {
            Vector2 center =
                Projectile.Center;


            // Large radial ice burst.
            for (int i = 0; i < 48; i++)
            {
                Vector2 direction =
                    Main.rand.NextVector2Unit();

                Dust dust =
                    Dust.NewDustPerfect(
                        center +
                        direction *
                        Main.rand.NextFloat(2f, 12f),
                        DustID.IceTorch,
                        direction *
                        Main.rand.NextFloat(2f, 6.5f),
                        70,
                        default,
                        Main.rand.NextFloat(0.65f, 1.3f)
                    );

                dust.noGravity = true;
            }


            // Frost fragments.
            for (int i = 0; i < 26; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Frost,
                        Main.rand.NextVector2Circular(
                            5f,
                            5f
                        ),
                        90,
                        default,
                        Main.rand.NextFloat(0.5f, 1.05f)
                    );

                dust.noGravity = true;
            }


            Lighting.AddLight(
                center,
                0.35f,
                0.9f,
                1.45f
            );


            SoundEngine.PlaySound(
                SoundID.Item27,
                center
            );
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor
        )
        {
            Texture2D texture =
                TextureAssets.Projectile[
                    Projectile.type
                ].Value;

            Vector2 center =
                Projectile.Center -
                Main.screenPosition;

            float fade =
                GetFade();


            // REAL POSITION-HISTORY AFTERIMAGES
            //
            // Unlike the previous version, these are NOT copies
            // simply moved behind the projectile.
            //
            // They use Projectile.oldPos, meaning each image is
            // actually where the projectile was in a previous frame.
            //
            // Lighting is sampled from EACH old position.

            for (
                int i = AfterimageCount;
                i >= 1;
                i--
            )
            {
                int oldIndex =
                    i * AfterimageSpacing;

                if (oldIndex >= Projectile.oldPos.Length)
                    continue;

                Vector2 oldWorldPosition =
                    Projectile.oldPos[oldIndex] +
                    Projectile.Size * 0.5f;

                Vector2 oldScreenPosition =
                    oldWorldPosition -
                    Main.screenPosition;

                float strength =
                    (1f -
                    i / (float)(AfterimageCount + 1)) *
                    0.42f *
                    fade;


                // Lighting from the actual old position.
                Color oldLight =
                    Lighting.GetColor(
                        (int)(oldWorldPosition.X / 16f),
                        (int)(oldWorldPosition.Y / 16f)
                    );


                Color afterColor =
                    Color.Lerp(
                        oldLight,
                        new Color(
                            90,
                            200,
                            255
                        ),
                        0.55f
                    );


                afterColor.A =
                    (byte)MathHelper.Clamp(
                        255f * strength,
                        0f,
                        255f
                    );


                Main.EntitySpriteDraw(
                    texture,
                    oldScreenPosition,
                    null,
                    afterColor,
                    Projectile.rotation -
                    i * 0.025f,
                    texture.Size() * 0.5f,
                    SpriteScale *
                    (1f - i * 0.025f),
                    SpriteEffects.None,
                    0f
                );
            }


            // OUTER FROST GLOW
            //
            // MiniYoyoGlow instead of TextureAssets.Sun2: the sun plate is a
            // SQUARE, and at this scale its corners read as a transparent box
            // behind the snowflake. The shared radial lens has alpha-0 corners.

            Main.EntitySpriteDraw(
                MiniYoyoFx.GlowTexture,
                center,
                null,
                new Color(
                    40,
                    180,
                    255,
                    (byte)(95f * fade)
                ),
                Main.GameUpdateCount * -0.009f,
                MiniYoyoFx.GlowTexture.Size() * 0.5f,
                0.88f,
                SpriteEffects.None,
                0f
            );


            // INNER WHITE-BLUE GLOW

            Main.EntitySpriteDraw(
                MiniYoyoFx.GlowTexture,
                center,
                null,
                new Color(
                    185,
                    245,
                    255,
                    (byte)(85f * fade)
                ),
                0f,
                MiniYoyoFx.GlowTexture.Size() * 0.5f,
                0.46f,
                SpriteEffects.None,
                0f
            );


            // SHADOW

            Main.EntitySpriteDraw(
                texture,
                center +
                new Vector2(2f, 2f),
                null,
                new Color(
                    0,
                    0,
                    0,
                    (byte)(100f * fade)
                ),
                Projectile.rotation,
                texture.Size() * 0.5f,
                SpriteScale,
                SpriteEffects.None,
                0f
            );


            // LIGHTING-AWARE BODY

            Color bodyColor =
                Color.Lerp(
                    Lighting.GetColor(
                        (int)(Projectile.Center.X / 16f),
                        (int)(Projectile.Center.Y / 16f)
                    ),
                    new Color(
                        145,
                        225,
                        255
                    ),
                    0.62f
                );


            bodyColor.A =
                (byte)(255f * fade);


            Main.EntitySpriteDraw(
                texture,
                center,
                null,
                bodyColor,
                Projectile.rotation,
                texture.Size() * 0.5f,
                SpriteScale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}