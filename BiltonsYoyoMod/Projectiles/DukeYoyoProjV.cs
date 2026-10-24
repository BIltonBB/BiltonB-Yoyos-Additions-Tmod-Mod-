using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class DukeYoyoProjV : ModProjectile
    {
        // VARIANT

        // 0 = normal mini vortex.
        // 1 = tiny fast vortex.
        private int Variant
        {
            get
            {
                return (int)Projectile.ai[0];
            }
        }


        // SIZE

        /*
         * Permanent vortex = 1.0
         *
         * Normal mini = 0.50
         * Tiny fast   = 0.25
         */

        private const float NormalScale = 0.50f;
        private const float TinyScale = 0.25f;


        // SPEED

        /*
         * These are constant movement speeds.
         */

        private const float NormalSpeed = 3.5f;
        private const float TinySpeed = 7f;


        // LIFETIME

        // 25 seconds.
        private const int Lifetime = 1500;

        // 1 second fade in/out.
        private const int FadeTime = 60;


        // DIRECTION

        /*
         * Every 2 seconds, choose a new target heading.
         */
        private const int DirectionPickInterval = 60;


        /*
         * The new heading can never be more than 30 degrees away
         * from the current movement direction.
         */
        private const float MaxTurnAngle =
            MathHelper.Pi /
            6f;


        /*
         * Maximum steering amount per tick.
         *
         * 1.5 degrees/tick gives a smooth visible turn.
         *
         * Because the target is at most 30 degrees away, the full
         * turn takes roughly 20 ticks or less.
         */
        private const float TurnSpeed =
            MathHelper.Pi /
            120f;


        // VORTEX SHAPE

        private const float OuterRadius = 88f;
        private const float StreamEndRadius = 2f;

        private const int StreamCount = 3;
        private const int StreamPoints = 45;


        // VORTEX MOTION

        private const float RotationSpeed = 0.14f;
        private const float Twist = 5.7f;
        private const float FlowSpeed = 0.34f;


        // COLLISION

        /*
         * Collision radius of the full-size vortex.
         *
         * Normal:
         * 90 * 0.50 = 45
         *
         * Tiny:
         * 90 * 0.25 = 22.5
         */
        private const float BaseCollisionRadius = 90f;


        // 12 ticks = 0.2 seconds.
        private const int DamageCooldown = 12;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 180;
            Projectile.height = 180;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            /*
             * Tile collision is handled manually below.
             */
            Projectile.tileCollide = false;

            Projectile.timeLeft = Lifetime;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;

            Projectile.localNPCHitCooldown =
                DamageCooldown;
        }


        // AI

        public override void AI()
        {
            // TARGET SIZE

            float targetScale =
                Variant == 1
                    ? TinyScale
                    : NormalScale;


            // SPEED

            float targetSpeed =
                Variant == 1
                    ? TinySpeed
                    : NormalSpeed;


            // INITIALIZE DIRECTION

            /*
             * ai[1] stores the target direction angle.
             *
             * localAI[2] is used as a local initialization flag.
             */
            if (
                Projectile.localAI[2] == 0f
            )
            {
                if (
                    Projectile.velocity.LengthSquared() >
                    0.001f
                )
                {
                    Projectile.ai[1] =
                        Projectile.velocity.ToRotation();
                }
                else
                {
                    Projectile.ai[1] = 0f;

                    Projectile.velocity =
                        Vector2.UnitX *
                        targetSpeed;
                }


                Projectile.localAI[2] =
                    1f;
            }


            // DIRECTION TIMER

            Projectile.localAI[0]++;


            if (
                Projectile.localAI[0] >=
                DirectionPickInterval
            )
            {
                Projectile.localAI[0] = 0f;


                /*
                 * Only the owner picks a new direction.
                 */
                if (
                    Main.myPlayer ==
                    Projectile.owner
                )
                {
                    float currentAngle;


                    if (
                        Projectile.velocity.LengthSquared() >
                        0.001f
                    )
                    {
                        currentAngle =
                            Projectile.velocity.ToRotation();
                    }
                    else
                    {
                        currentAngle =
                            Projectile.ai[1];
                    }


                    /*
                     * Random offset between:
                     *
                     * -30 degrees
                     * +
                     * +30 degrees
                     */
                    float randomOffset =
                        Main.rand.NextFloat(
                            -MaxTurnAngle,
                            MaxTurnAngle
                        );


                    float newTarget =
                        currentAngle +
                        randomOffset;


                    Projectile.ai[1] =
                        MathHelper.WrapAngle(
                            newTarget
                        );


                    /*
                     * Sync the new target direction.
                     */
                    Projectile.netUpdate =
                        true;
                }
            }


            // SMOOTH STEERING

            /*
             * Current movement angle.
             */
            float currentDirection;


            if (
                Projectile.velocity.LengthSquared() >
                0.001f
            )
            {
                currentDirection =
                    Projectile.velocity.ToRotation();
            }
            else
            {
                currentDirection =
                    Projectile.ai[1];
            }


            /*
             * Smallest angular difference between current movement
             * and target movement.
             */
            float angleDifference =
                MathHelper.WrapAngle(
                    Projectile.ai[1] -
                    currentDirection
                );


            /*
             * Clamp the amount we turn this frame.
             */
            float turnAmount =
                MathHelper.Clamp(
                    angleDifference,
                    -TurnSpeed,
                    TurnSpeed
                );


            float newDirection =
                currentDirection +
                turnAmount;


            /*
             * Rebuild velocity from the new direction.
             *
             * This guarantees CONSTANT SPEED.
             */
            Projectile.velocity =
                newDirection.ToRotationVector2() *
                targetSpeed;


            // WALL REFLECTION

            HandleTileReflection(
                targetSpeed
            );


            // FADE

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


            float fade =
                Math.Min(
                    fadeIn,
                    fadeOut
                );


            /*
             * Smoothstep.
             */
            fade =
                fade *
                fade *
                (
                    3f -
                    2f *
                    fade
                );


            /*
             * Scale controls the COMPLETE vortex size.
             *
             * Therefore visual size and collision size match.
             */
            Projectile.scale =
                targetScale *
                fade;


            // ROTATION

            Projectile.rotation +=
                Variant == 1
                    ? 0.12f
                    : 0.075f;


            // SMALL LIGHT

            float lightStrength =
                fade *
                (
                    Variant == 1
                        ? 0.05f
                        : 0.025f
                );


            Lighting.AddLight(
                Projectile.Center,
                0.015f,
                lightStrength,
                lightStrength * 1.2f
            );


            // FAST VORTEX PARTICLES

            if (
                Variant == 1 &&
                fade > 0.4f &&
                Main.rand.NextBool(8)
            )
            {
                Vector2 offset =
                    Main.rand.NextVector2Circular(
                        8f,
                        8f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        offset,
                        Terraria.ID.DustID.Electric,
                        Projectile.velocity *
                        -0.04f,
                        140,
                        default,
                        Main.rand.NextFloat(
                            0.35f,
                            0.60f
                        )
                    );


                dust.noGravity = true;
            }
        }


        // TILE REFLECTION

        private void HandleTileReflection(
            float speed)
        {
            /*
             * During the fade-in/out the vortex is tiny, so use its
             * actual current visual radius for collision against tiles.
             */
            float halfSize =
                Math.Max(
                    4f,
                    BaseCollisionRadius *
                    Projectile.scale
                );


            int size =
                (int)Math.Ceiling(
                    halfSize *
                    2f
                );


            /*
             * Test the full future position.
             */
            Vector2 nextPosition =
                Projectile.Center +
                Projectile.velocity;


            bool hitsFull =
                IsSolidAt(
                    nextPosition,
                    size
                );


            if (!hitsFull)
            {
                return;
            }


            /*
             * Test X and Y independently.
             *
             * This lets us bounce correctly off:
             *
             * horizontal walls
             * vertical walls
             * corners
             */
            Vector2 xPosition =
                Projectile.Center +
                new Vector2(
                    Projectile.velocity.X,
                    0f
                );


            Vector2 yPosition =
                Projectile.Center +
                new Vector2(
                    0f,
                    Projectile.velocity.Y
                );


            bool hitX =
                IsSolidAt(
                    xPosition,
                    size
                );


            bool hitY =
                IsSolidAt(
                    yPosition,
                    size
                );


            if (hitX)
            {
                Projectile.velocity.X *=
                    -1f;
            }


            if (hitY)
            {
                Projectile.velocity.Y *=
                    -1f;
            }


            /*
             * If both tests failed but the full movement collided,
             * we are probably touching a corner.
             */
            if (
                !hitX &&
                !hitY
            )
            {
                Projectile.velocity *=
                    -1f;
            }


            /*
             * Keep speed EXACTLY constant after reflection.
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
                    speed;
            }


            /*
             * Change the steering target to the reflected direction.
             *
             * Without this, the steering system might immediately
             * try to turn back through the wall.
             */
            Projectile.ai[1] =
                Projectile.velocity.ToRotation();


            Projectile.localAI[0] =
                0f;


            Projectile.netUpdate =
                true;
        }


        // SOLID COLLISION TEST

        private bool IsSolidAt(
            Vector2 center,
            int size)
        {
            Vector2 topLeft =
                center -
                new Vector2(
                    size * 0.5f,
                    size * 0.5f
                );


            return Collision.SolidCollision(
                topLeft,
                size,
                size
            );
        }


        // PRE DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            DrawVortex();

            return false;
        }


        // DRAW VORTEX

        private void DrawVortex()
        {
            Texture2D pixel =
                TextureAssets.MagicPixel.Value;


            Vector2 center =
                Projectile.Center -
                Main.screenPosition;


            float time =
                Main.GameUpdateCount;


            float targetScale =
                Variant == 1
                    ? TinyScale
                    : NormalScale;


            float visibleScale =
                targetScale <= 0f
                    ? 0f
                    : Projectile.scale /
                      targetScale;


            if (
                visibleScale <=
                0.01f
            )
            {
                return;
            }


            // STREAMS

            for (
                int stream = 0;
                stream < StreamCount;
                stream++
            )
            {
                float streamOffset =
                    MathHelper.TwoPi *
                    stream /
                    StreamCount;


                Vector2 previous =
                    GetStreamPosition(
                        center,
                        time,
                        streamOffset,
                        0f
                    );


                for (
                    int i = 1;
                    i < StreamPoints;
                    i++
                )
                {
                    float progress =
                        i /
                        (float)(
                            StreamPoints - 1
                        );


                    Vector2 current =
                        GetStreamPosition(
                            center,
                            time,
                            streamOffset,
                            progress
                        );


                    // WIDTH

                    float width =
                        MathHelper.Lerp(
                            5.5f,
                            0.75f,
                            (float)Math.Pow(
                                progress,
                                0.92f
                            )
                        )
                        *
                        Projectile.scale;


                    // ALPHA

                    float alpha =
                        MathHelper.Lerp(
                            180f,
                            65f,
                            progress
                        )
                        *
                        visibleScale;


                    byte finalAlpha =
                        (byte)MathHelper.Clamp(
                            alpha,
                            0f,
                            255f
                        );


                    // COLOR

                    Color bodyColor;


                    if (
                        Variant == 1
                    )
                    {
                        bodyColor =
                            new Color(
                                75,
                                120,
                                150,
                                finalAlpha
                            );
                    }
                    else
                    {
                        bodyColor =
                            new Color(
                                65,
                                94,
                                103,
                                finalAlpha
                            );
                    }


                    DrawFiniteLine(
                        pixel,
                        previous,
                        current,
                        width,
                        bodyColor
                    );


                    // FAST VORTEX INNER EDGE

                    if (
                        Variant == 1
                    )
                    {
                        byte highlightAlpha =
                            (byte)MathHelper.Clamp(
                                35f *
                                visibleScale,
                                0f,
                                255f
                            );


                        DrawFiniteLine(
                            pixel,
                            previous,
                            current,
                            width * 0.16f,
                            new Color(
                                150,
                                195,
                                210,
                                highlightAlpha
                            )
                        );
                    }


                    previous =
                        current;
                }
            }
        }


        // STREAM POSITION

        private Vector2 GetStreamPosition(
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
                    FlowSpeed
                    +
                    streamOffset
                );


            radius -=
                Math.Max(
                    0f,
                    flowWave
                ) *
                MathHelper.Lerp(
                    0.5f,
                    5.5f,
                    progress
                );


            // ANGLE

            float angle =
                streamOffset
                +
                time *
                RotationSpeed
                +
                progress *
                Twist;


            angle +=
                (float)Math.Sin(
                    time * 0.16f
                    +
                    progress * 5f
                    +
                    streamOffset
                )
                *
                0.024f;


            // RADIAL / TANGENT

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
                        time * 0.20f
                        +
                        progress * 6.5f
                        +
                        streamOffset
                    )
                    *
                    (
                        0.15f +
                        progress * 0.7f
                    )
                );


            /*
             * Scale the ENTIRE vortex geometry.
             *
             * Normal = 0.50
             * Tiny   = 0.25
             */
            Vector2 localPosition =
                position -
                center;


            localPosition *=
                Projectile.scale;


            return
                center +
                localPosition;
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


            Vector2 difference =
                end -
                start;


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


        // COLLISION

        public override bool? Colliding(
            Rectangle projHitbox,
            Rectangle targetHitbox)
        {
            /*
             * Scale matches the actual rendered vortex.
             */
            if (
                Projectile.scale <=
                0.05f
            )
            {
                return false;
            }


            float radius =
                BaseCollisionRadius *
                Projectile.scale;


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
                radius *
                radius;
        }
    }
}