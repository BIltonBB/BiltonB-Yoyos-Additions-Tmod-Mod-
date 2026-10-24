using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public class DukeYoyoProjW : ModProjectile
    {
        // WHIRLPOOL

        private const float WhirlpoolRadius = 90f;

        // 12 ticks = 0.2 seconds.
        private const int DamageCooldown = 12;


        // STREAM STRUCTURE

        private const int MainStreamCount = 3;

        // More points = shorter individual ribbon segments.
        private const int MainStreamPoints = 120;

        private const int SecondaryStreamCount = 3;
        private const int SecondaryStreamPoints = 80;


        // SIZE

        private const float OuterRadius = 88f;
        private const float StreamEndRadius = 2f;

        private const float CoreRadius = 11f;


        // MOTION

        private const float MainRotationSpeed = 0.14f;
        private const float SecondaryRotationSpeed = 0.17f;

        private const float MainTwist = 5.7f;
        private const float SecondaryTwist = 5.15f;

        private const float MainFlowSpeed = 0.34f;
        private const float SecondaryFlowSpeed = 0.30f;


        // MAIN STREAM WIDTH

        private const float MainOuterWidth = 5.5f;
        private const float MainInnerWidth = 0.75f;

        private const float SecondaryOuterWidth = 2.4f;
        private const float SecondaryInnerWidth = 0.45f;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 180;
            Projectile.height = 180;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.tileCollide = false;

            Projectile.timeLeft = 9999;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;

            Projectile.localNPCHitCooldown =
                DamageCooldown;
        }


        // DRAW ORDER

        public override void DrawBehind(
            int index,
            List<int> behindNPCsAndTiles,
            List<int> behindNPCs,
            List<int> behindProjectiles,
            List<int> overPlayers,
            List<int> overWiresUI)
        {
            behindProjectiles.Add(index);
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
                ModContent.ProjectileType<DukeYoyoProj>() ||
                parent.owner != Projectile.owner
            )
            {
                Projectile.Kill();
                return;
            }


            // FOLLOW YOYO

            Projectile.Center =
                parent.Center;

            Projectile.timeLeft =
                10;


            // DAMAGE

            Projectile.damage =
                Math.Max(
                    1,
                    parent.damage / 20
                );


            // VORTEX LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.055f,
                0.11f,
                0.14f
            );
        }


        // PRE DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            DrawVortex();

            return false;
        }


        // MAIN VORTEX

        private void DrawVortex()
        {
            Texture2D pixel =
                TextureAssets.MagicPixel.Value;

            Texture2D coreTexture =
                TextureAssets.Sun2.Value;


            Vector2 center =
                Projectile.Center -
                Main.screenPosition;


            float time =
                Main.GameUpdateCount;


            // MAIN STREAMS

            DrawMainStreams(
                pixel,
                center,
                time
            );


            // SECONDARY STREAMS

            DrawSecondaryStreams(
                pixel,
                center,
                time
            );


            // MOVING HIGHLIGHTS

            DrawMovingHighlights(
                pixel,
                center,
                time
            );


            // CORE

            DrawCore(
                coreTexture,
                center,
                time
            );
        }


        // MAIN STREAMS

        private void DrawMainStreams(
            Texture2D pixel,
            Vector2 center,
            float time)
        {
            for (
                int stream = 0;
                stream < MainStreamCount;
                stream++
            )
            {
                float streamOffset =
                    MathHelper.TwoPi *
                    stream /
                    MainStreamCount;


                Vector2 previous =
                    GetMainStreamPosition(
                        center,
                        time,
                        streamOffset,
                        0f
                    );


                for (
                    int i = 1;
                    i < MainStreamPoints;
                    i++
                )
                {
                    float progress =
                        i /
                        (float)(MainStreamPoints - 1);


                    Vector2 current =
                        GetMainStreamPosition(
                            center,
                            time,
                            streamOffset,
                            progress
                        );


                    float width =
                        GetMainStreamWidth(
                            progress,
                            time,
                            stream
                        );


                    /*
                     * Main body.
                     *
                     * This uses the same finite-rectangle concept
                     * as the user's red string renderer.
                     */

                    DrawFiniteLine(
                        pixel,
                        previous,
                        current,
                        width,
                        new Color(
                            72,
                            101,
                            108,
                            205
                        )
                    );


                    /*
                     * Thin center highlight.
                     *
                     * Kept much less opaque than the body.
                     */
                    DrawFiniteLine(
                        pixel,
                        previous,
                        current,
                        width * 0.24f,
                        new Color(
                            115,
                            143,
                            149,
                            85
                        )
                    );


                    previous =
                        current;
                }
            }
        }


        // SECONDARY STREAMS

        private void DrawSecondaryStreams(
            Texture2D pixel,
            Vector2 center,
            float time)
        {
            for (
                int stream = 0;
                stream < SecondaryStreamCount;
                stream++
            )
            {
                float streamOffset =
                    MathHelper.TwoPi *
                    stream /
                    SecondaryStreamCount
                    +
                    MathHelper.Pi /
                    SecondaryStreamCount;


                Vector2 previous =
                    GetSecondaryStreamPosition(
                        center,
                        time,
                        streamOffset,
                        0f
                    );


                for (
                    int i = 1;
                    i < SecondaryStreamPoints;
                    i++
                )
                {
                    float progress =
                        i /
                        (float)(SecondaryStreamPoints - 1);


                    Vector2 current =
                        GetSecondaryStreamPosition(
                            center,
                            time,
                            streamOffset,
                            progress
                        );


                    float width =
                        MathHelper.Lerp(
                            SecondaryOuterWidth,
                            SecondaryInnerWidth,
                            (float)Math.Pow(
                                progress,
                                0.9f
                            )
                        );


                    float fade =
                        1f -
                        SmoothStep(
                            0.68f,
                            1f,
                            progress
                        );


                    byte alpha =
                        (byte)(
                            85f *
                            fade
                        );


                    DrawFiniteLine(
                        pixel,
                        previous,
                        current,
                        width,
                        new Color(
                            58,
                            84,
                            91,
                            alpha
                        )
                    );


                    previous =
                        current;
                }
            }
        }


        // MOVING HIGHLIGHTS

        private void DrawMovingHighlights(
            Texture2D pixel,
            Vector2 center,
            float time)
        {
            /*
             * These are actual finite little rectangles traveling
             * along the streams.
             *
             * This is intentionally sparse.
             */

            for (
                int stream = 0;
                stream < MainStreamCount;
                stream++
            )
            {
                float streamOffset =
                    MathHelper.TwoPi *
                    stream /
                    MainStreamCount;


                for (
                    int streak = 0;
                    streak < 4;
                    streak++
                )
                {
                    float head =
                        (
                            streak * 0.22f
                            +
                            time * 0.0024f
                            +
                            stream * 0.17f
                        ) % 0.82f;


                    float tail =
                        head +
                        0.035f;


                    if (
                        head < 0.02f ||
                        tail > 0.94f
                    )
                    {
                        continue;
                    }


                    Vector2 start =
                        GetMainStreamPosition(
                            center,
                            time,
                            streamOffset,
                            head
                        );


                    Vector2 end =
                        GetMainStreamPosition(
                            center,
                            time,
                            streamOffset,
                            tail
                        );


                    float fade =
                        1f -
                        SmoothStep(
                            0.62f,
                            0.92f,
                            head
                        );


                    DrawFiniteLine(
                        pixel,
                        start,
                        end,
                        MathHelper.Lerp(
                            1.6f,
                            0.55f,
                            head
                        ),
                        new Color(
                            145,
                            183,
                            190,
                            (byte)(
                                125f *
                                fade
                            )
                        )
                    );
                }
            }
        }


        // MAIN STREAM POSITION

        private Vector2 GetMainStreamPosition(
            Vector2 center,
            float time,
            float streamOffset,
            float progress)
        {
            // RADIUS

            float radiusCurve =
                (float)Math.Pow(
                    progress,
                    1.38f
                );


            float radius =
                MathHelper.Lerp(
                    OuterRadius,
                    StreamEndRadius,
                    radiusCurve
                );


            // FLOW

            float flowWave =
                (float)Math.Sin(
                    progress *
                    MathHelper.TwoPi *
                    1.85f
                    -
                    time *
                    MainFlowSpeed
                    +
                    streamOffset
                );


            float inwardPull =
                MathHelper.Lerp(
                    0.5f,
                    6f,
                    progress
                );


            radius -=
                Math.Max(
                    0f,
                    flowWave
                ) *
                inwardPull;


            // SPIRAL

            float angle =
                streamOffset
                +
                time *
                MainRotationSpeed
                +
                progress *
                MainTwist;


            /*
             * Low-amplitude organic movement.
             */
            angle +=
                (float)Math.Sin(
                    time * 0.17f
                    +
                    progress * 4.7f
                    +
                    streamOffset
                ) *
                0.025f;


            angle +=
                (float)Math.Sin(
                    time * 0.09f
                    +
                    progress * 10f
                ) *
                0.012f;


            Vector2 radial =
                angle.ToRotationVector2();


            Vector2 tangent =
                new Vector2(
                    -radial.Y,
                    radial.X
                );


            // POSITION

            Vector2 position =
                center +
                radial *
                radius;


            float sidewaysWave =
                (float)Math.Sin(
                    time * 0.19f
                    +
                    progress * 6.5f
                    +
                    streamOffset
                );


            position +=
                tangent *
                sidewaysWave *
                (
                    0.15f +
                    progress * 0.75f
                );


            return position;
        }


        // SECONDARY STREAM POSITION

        private Vector2 GetSecondaryStreamPosition(
            Vector2 center,
            float time,
            float streamOffset,
            float progress)
        {
            // RADIUS

            float radius =
                MathHelper.Lerp(
                    OuterRadius - 11f,
                    StreamEndRadius + 2f,
                    (float)Math.Pow(
                        progress,
                        1.18f
                    )
                );


            float wave =
                (float)Math.Sin(
                    progress *
                    MathHelper.TwoPi *
                    1.55f
                    +
                    time *
                    SecondaryFlowSpeed
                    +
                    streamOffset
                );


            radius -=
                Math.Max(
                    0f,
                    wave
                ) *
                MathHelper.Lerp(
                    0.5f,
                    4f,
                    progress
                );


            // ANGLE

            float angle =
                streamOffset
                -
                time *
                SecondaryRotationSpeed
                +
                progress *
                SecondaryTwist;


            angle +=
                (float)Math.Sin(
                    time * 0.15f
                    +
                    progress * 5.8f
                    +
                    streamOffset
                ) *
                0.018f;


            Vector2 radial =
                angle.ToRotationVector2();


            Vector2 tangent =
                new Vector2(
                    -radial.Y,
                    radial.X
                );


            // POSITION

            Vector2 position =
                center +
                radial *
                radius;


            position +=
                tangent *
                (
                    (float)Math.Sin(
                        time * 0.23f
                        +
                        progress * 7f
                        +
                        streamOffset
                    )
                    *
                    0.28f
                );


            return position;
        }


        // MAIN STREAM WIDTH

        private float GetMainStreamWidth(
            float progress,
            float time,
            int stream)
        {
            float width =
                MathHelper.Lerp(
                    MainOuterWidth,
                    MainInnerWidth,
                    (float)Math.Pow(
                        progress,
                        0.92f
                    )
                );


            /*
             * Moving thickness variation.
             */
            float bulge =
                (float)Math.Sin(
                    time * 0.11f
                    +
                    progress * 13f
                    +
                    stream * 2.7f
                );


            float bulgeAmount =
                MathHelper.Lerp(
                    0.78f,
                    1.16f,
                    (
                        bulge +
                        1f
                    ) * 0.5f
                );


            width *=
                bulgeAmount;


            /*
             * Accelerates into the center.
             */
            width *=
                MathHelper.Lerp(
                    1f,
                    0.72f,
                    SmoothStep(
                        0.72f,
                        1f,
                        progress
                    )
                );


            return width;
        }


        // FINITE LINE

        private void DrawFiniteLine(
            Texture2D pixel,
            Vector2 start,
            Vector2 end,
            float width,
            Color color)
        {
            if (
                pixel == null ||
                width <= 0f ||
                color.A == 0
            )
            {
                return;
            }


            /*
             * This deliberately follows the exact approach used in
             * the user's working red-string code:
             *
             * start -> end
             * finite length
             * exact midpoint
             * exact rotation
             * finite rectangle
             *
             * No giant scaling of a circular texture.
             */

            Vector2 difference =
                end - start;


            float length =
                difference.Length();


            if (
                length <= 0.5f ||
                length > 1000f
            )
            {
                return;
            }


            Vector2 midpoint =
                (start + end) *
                0.5f;


            float rotation =
                difference.ToRotation();


            Main.spriteBatch.Draw(
                pixel,
                midpoint,
                new Rectangle(
                    0,
                    0,
                    1,
                    1
                ),
                color,
                rotation,
                new Vector2(
                    0.5f,
                    0.5f
                ),
                new Vector2(
                    length,
                    width
                ),
                SpriteEffects.None,
                0f
            );
        }


        // CORE

        private void DrawCore(
            Texture2D texture,
            Vector2 center,
            float time)
        {
            /*
             * Small dark center.
             */

            float pulse =
                1f +
                (float)Math.Sin(
                    time * 0.11f
                ) *
                0.04f;


            DrawCoreCircle(
                texture,
                center,
                CoreRadius *
                1.10f *
                pulse,
                new Color(
                    0,
                    8,
                    10,
                    30
                )
            );


            DrawCoreCircle(
                texture,
                center,
                CoreRadius *
                0.64f *
                pulse,
                new Color(
                    0,
                    0,
                    0,
                    70
                )
            );


            // INNER ROTATION

            for (
                int i = 0;
                i < 4;
                i++
            )
            {
                float angle =
                    time * 0.065f
                    +
                    MathHelper.TwoPi *
                    i /
                    4f;


                Vector2 radial =
                    angle.ToRotationVector2();


                Vector2 tangent =
                    new Vector2(
                        -radial.Y,
                        radial.X
                    );


                Vector2 position =
                    center +
                    radial *
                    (
                        CoreRadius *
                        0.72f
                    );


                DrawFiniteLine(
                    Terraria.GameContent.TextureAssets.MagicPixel.Value,
                    position -
                    tangent * 1.3f,
                    position +
                    tangent * 1.3f,
                    0.9f,
                    new Color(
                        100,
                        134,
                        141,
                        40
                    )
                );
            }
        }


        // CORE CIRCLE

        private void DrawCoreCircle(
            Texture2D texture,
            Vector2 center,
            float size,
            Color color)
        {
            if (
                texture == null ||
                size <= 0f ||
                color.A == 0
            )
            {
                return;
            }


            float scale =
                size /
                Math.Max(
                    1f,
                    texture.Width
                );


            Main.EntitySpriteDraw(
                texture,
                center,
                null,
                color,
                0f,
                texture.Size() * 0.5f,
                scale,
                SpriteEffects.None,
                0f
            );
        }


        // SMOOTH STEP

        private float SmoothStep(
            float edge0,
            float edge1,
            float value)
        {
            if (
                Math.Abs(
                    edge1 -
                    edge0
                ) < 0.0001f
            )
            {
                return value < edge0
                    ? 0f
                    : 1f;
            }


            value =
                MathHelper.Clamp(
                    (
                        value -
                        edge0
                    ) /
                    (
                        edge1 -
                        edge0
                    ),
                    0f,
                    1f
                );


            return
                value *
                value *
                (
                    3f -
                    2f *
                    value
                );
        }


        // COLLISION

        public override bool? Colliding(
            Rectangle projHitbox,
            Rectangle targetHitbox)
        {
            float closestX =
                MathHelper.Clamp(
                    Projectile.Center.X,
                    targetHitbox.Left,
                    targetHitbox.Right
                );


            float closestY =
                MathHelper.Clamp(
                    Projectile.Center.Y,
                    targetHitbox.Top,
                    targetHitbox.Bottom
                );


            Vector2 closestPoint =
                new Vector2(
                    closestX,
                    closestY
                );


            float distanceSquared =
                Vector2.DistanceSquared(
                    Projectile.Center,
                    closestPoint
                );


            return
                distanceSquared <=
                WhirlpoolRadius *
                WhirlpoolRadius;
        }
    }
}