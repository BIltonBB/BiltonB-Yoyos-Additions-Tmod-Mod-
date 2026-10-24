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
    public class LunaticYoyoProjLP : ModProjectile
    {
        // SETTINGS

        private const int Lifetime = 360;
        private const int FadeTime = 60;

        private const int LightningDelay = 60;

        private const float SearchRange = 800f;

        private const float SpriteScale = 0.50f;


        // VISUAL SETTINGS

        private const int AuraDustFrequency = 2;
        private const int OrbitDustFrequency = 3;

        private const int ChargeSparkFrequency = 3;


        // STATE

        private int lightningTimer;

        private bool hasFired;


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
            Projectile.velocity =
                Vector2.Zero;


            float fade =
                GetFade();


            Projectile.alpha =
                (int)(
                    255f *
                    (1f - fade)
                );


            // ROTATION

            Projectile.rotation +=
                0.025f;


            // BLUE AURA

            if (
                Main.rand.NextBool(
                    AuraDustFrequency
                )
            )
            {
                Vector2 direction =
                    Main.rand.NextVector2Unit();

                float distance =
                    Main.rand.NextFloat(
                        22f,
                        40f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        direction * distance,
                        DustID.Electric,
                        direction *
                        Main.rand.NextFloat(
                            0.35f,
                            1.35f
                        ),
                        70,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1.2f
                        ) *
                        fade
                    );


                dust.noGravity = true;
            }


            // ORBITING ELECTRIC SPARK

            if (
                Main.rand.NextBool(
                    OrbitDustFrequency
                )
            )
            {
                float orbitAngle =
                    Main.GameUpdateCount *
                    0.075f +
                    Projectile.whoAmI *
                    1.7f;


                Vector2 orbit =
                    orbitAngle.ToRotationVector2();


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        orbit * 31f,
                        DustID.Electric,
                        -orbit *
                        Main.rand.NextFloat(
                            0.25f,
                            0.75f
                        ),
                        60,
                        default,
                        Main.rand.NextFloat(
                            0.65f,
                            1.1f
                        ) *
                        fade
                    );


                dust.noGravity = true;
            }


            // PURPLE ACCENT PARTICLES
            //
            // Purple is now an accent rather than the main colour.

            if (
                Main.rand.NextBool(5)
            )
            {
                Vector2 direction =
                    Main.rand.NextVector2Unit();


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        direction *
                        Main.rand.NextFloat(
                            13f,
                            25f
                        ),
                        DustID.PurpleTorch,
                        direction *
                        Main.rand.NextFloat(
                            0.25f,
                            0.8f
                        ),
                        80,
                        default,
                        Main.rand.NextFloat(
                            0.45f,
                            0.85f
                        ) *
                        fade
                    );


                dust.noGravity = true;
            }


            // CHARGING SPARKS

            if (
                !hasFired &&
                Main.rand.NextBool(
                    ChargeSparkFrequency
                )
            )
            {
                Vector2 direction =
                    Main.rand.NextVector2Unit();


                float distance =
                    Main.rand.NextFloat(
                        7f,
                        20f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        direction * distance,
                        DustID.Electric,
                        -direction *
                        Main.rand.NextFloat(
                            0.5f,
                            1.5f
                        ),
                        50,
                        default,
                        Main.rand.NextFloat(
                            0.65f,
                            1.15f
                        )
                    );


                dust.noGravity = true;
            }


            // LIGHTNING CHARGE

            if (
                !hasFired &&
                Main.myPlayer ==
                Projectile.owner
            )
            {
                lightningTimer++;


                if (
                    lightningTimer >=
                    LightningDelay
                )
                {
                    hasFired = true;


                    FireLightning();

                    CreateFireFlash();
                }
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.18f * fade,
                0.48f * fade,
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
                    age /
                    (float)FadeTime,
                    0f,
                    1f
                );


            float fadeOut =
                MathHelper.Clamp(
                    Projectile.timeLeft /
                    (float)FadeTime,
                    0f,
                    1f
                );


            return Math.Min(
                fadeIn,
                fadeOut
            );
        }


        // FIRE FLASH

        private void CreateFireFlash()
        {
            // ELECTRIC FLASH

            for (
                int i = 0;
                i < 42;
                i++
            )
            {
                Vector2 direction =
                    Main.rand.NextVector2Unit();


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        direction *
                        Main.rand.NextFloat(
                            3f,
                            17f
                        ),
                        DustID.Electric,
                        direction *
                        Main.rand.NextFloat(
                            2f,
                            7f
                        ),
                        35,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.5f
                        )
                    );


                dust.noGravity = true;
            }


            // PURPLE CORE ACCENT

            for (
                int i = 0;
                i < 16;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PurpleTorch,
                        Main.rand.NextVector2Circular(
                            3f,
                            3f
                        ),
                        40,
                        default,
                        Main.rand.NextFloat(
                            0.55f,
                            1.05f
                        )
                    );


                dust.noGravity = true;
            }


            Lighting.AddLight(
                Projectile.Center,
                0.65f,
                0.9f,
                1.6f
            );


            SoundEngine.PlaySound(
                SoundID.Item94,
                Projectile.Center
            );
        }


        // FIRE LIGHTNING

        private void FireLightning()
        {
            NPC target =
                FindNearestTarget();


            Vector2 direction;


            if (
                target != null
            )
            {
                direction =
                    target.Center -
                    Projectile.Center;


                if (
                    direction.LengthSquared() <
                    0.001f
                )
                {
                    direction =
                        Vector2.UnitX;
                }
                else
                {
                    direction.Normalize();
                }
            }
            else
            {
                direction =
                    Main.rand.NextVector2Unit();


                if (
                    direction.LengthSquared() <
                    0.001f
                )
                {
                    direction =
                        Vector2.UnitX;
                }


                direction.Normalize();
            }


            float directionAngle =
                direction.ToRotation();


            float targetID =
                target != null
                    ? target.whoAmI + 1
                    : 0;


            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<LunaticYoyoProjL>(),
                Math.Max(
                    1,
                    Projectile.damage
                ),
                0f,
                Projectile.owner,
                targetID,
                directionAngle,
                Projectile.whoAmI
            );
        }


        // FIND NEAREST TARGET

        private NPC FindNearestTarget()
        {
            NPC best =
                null;


            float bestDistance =
                SearchRange *
                SearchRange;


            for (
                int i = 0;
                i < Main.maxNPCs;
                i++
            )
            {
                NPC npc =
                    Main.npc[i];


                if (
                    !npc.active ||
                    !npc.CanBeChasedBy()
                )
                {
                    continue;
                }


                float distance =
                    Vector2.DistanceSquared(
                        Projectile.Center,
                        npc.Center
                    );


                if (
                    distance <
                    bestDistance
                )
                {
                    bestDistance =
                        distance;

                    best =
                        npc;
                }
            }


            return best;
        }


        // LP DISAPPEARS

        public override void OnKill(
            int timeLeft
        )
        {
            int lightningType =
                ModContent.ProjectileType<
                    LunaticYoyoProjL
                >();


            // KILL THIS LP'S LIGHTNING

            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile other =
                    Main.projectile[i];


                if (
                    !other.active ||
                    other.owner !=
                    Projectile.owner ||
                    other.type !=
                    lightningType
                )
                {
                    continue;
                }


                if (
                    (int)other.ai[2] ==
                    Projectile.whoAmI
                )
                {
                    other.Kill();
                }
            }


            Vector2 center =
                Projectile.Center;


            // ELECTRIC BURST

            for (
                int i = 0;
                i < 52;
                i++
            )
            {
                Vector2 direction =
                    Main.rand.NextVector2Unit();


                Dust dust =
                    Dust.NewDustPerfect(
                        center +
                        direction *
                        Main.rand.NextFloat(
                            2f,
                            14f
                        ),
                        DustID.Electric,
                        direction *
                        Main.rand.NextFloat(
                            2f,
                            7f
                        ),
                        55,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.4f
                        )
                    );


                dust.noGravity = true;
            }


            // PURPLE ACCENTS

            for (
                int i = 0;
                i < 18;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.PurpleTorch,
                        Main.rand.NextVector2Circular(
                            5f,
                            5f
                        ),
                        60,
                        default,
                        Main.rand.NextFloat(
                            0.5f,
                            1f
                        )
                    );


                dust.noGravity = true;
            }


            Lighting.AddLight(
                center,
                0.45f,
                0.8f,
                1.5f
            );


            SoundEngine.PlaySound(
                SoundID.Item94,
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


            Color tileLight =
                Lighting.GetColor(
                    (int)(
                        Projectile.Center.X /
                        16f
                    ),
                    (int)(
                        Projectile.Center.Y /
                        16f
                    )
                );


            // OUTER ENERGY
            //
            // Was TextureAssets.Sun2 - a square sun-glow plate. At this small
            // scale its corners stayed visible, so every lightning point had a
            // faint transparent SQUARE behind its sprite. MiniYoyoGlow is the
            // shared radial lens (corners alpha 0), so the halo is round at
            // every size.

            Main.EntitySpriteDraw(
                MiniYoyoFx.GlowTexture,
                center,
                null,
                new Color(
                    40,
                    145,
                    255,
                    (byte)(
                        105f *
                        fade
                    )
                ),
                Main.GameUpdateCount *
                0.008f,
                MiniYoyoFx.GlowTexture.Size() *
                0.5f,
                0.90f,
                SpriteEffects.None,
                0f
            );


            // SECONDARY BLUE GLOW

            Main.EntitySpriteDraw(
                MiniYoyoFx.GlowTexture,
                center,
                null,
                new Color(
                    100,
                    205,
                    255,
                    (byte)(
                        65f *
                        fade
                    )
                ),
                Main.GameUpdateCount *
                -0.012f,
                MiniYoyoFx.GlowTexture.Size() *
                0.5f,
                0.64f,
                SpriteEffects.None,
                0f
            );


            // WHITE-BLUE CORE

            Main.EntitySpriteDraw(
                MiniYoyoFx.GlowTexture,
                center,
                null,
                new Color(
                    205,
                    245,
                    255,
                    (byte)(
                        75f *
                        fade
                    )
                ),
                0f,
                MiniYoyoFx.GlowTexture.Size() *
                0.5f,
                0.42f,
                SpriteEffects.None,
                0f
            );


            // SHADOW

            Main.EntitySpriteDraw(
                texture,
                center +
                new Vector2(
                    2f,
                    2f
                ),
                null,
                new Color(
                    0,
                    0,
                    0,
                    (byte)(
                        105f *
                        fade
                    )
                ),
                Projectile.rotation,
                texture.Size() *
                0.5f,
                SpriteScale,
                SpriteEffects.None,
                0f
            );


            // LIGHTING-AWARE BODY

            Color bodyColor =
                Color.Lerp(
                    tileLight,
                    new Color(
                        120,
                        190,
                        255
                    ),
                    0.68f
                );


            bodyColor.A =
                (byte)(
                    255f *
                    fade
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