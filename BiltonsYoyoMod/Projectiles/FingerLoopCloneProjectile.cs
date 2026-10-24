using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class FingerLoopCloneProjectile
        : ModProjectile
    {
        // COPIED DATA

        /*
         * ai[0] = original yoyo projectile type
         * ai[1] = copied maximum yoyo distance
         */

        private int SourceYoyoType =>
            (int)Projectile.ai[0];

        private float MaximumStringLength =>
            Projectile.ai[1];

        // LIFETIME

        /*
         * Snap clones ALWAYS last 20 seconds.
         *
         * This is completely independent from the original yoyo's
         * lifetime.
         */

        private const int Lifetime =
            20 * 60;

        // ROPE PHYSICS

        /*
         * Dense enough to look like a real string without going
         * completely insane with particle count.
         */

        private const int RopePointCount =
            56;

        /*
         * Physics substeps.
         *
         * Four substeps keeps the 56-particle rope stable without
         * making it unnecessarily expensive.
         */

        private const int PhysicsSubsteps =
            4;

        /*
         * Rope distance constraints.
         */

        private const int ConstraintIterations =
            7;

        /*
         * Gravity applies to EVERY dynamic rope point.
         */

        private const float RopeGravity =
            0.30f;

        /*
         * Small amount of damping.
         *
         * The rope can swing, but eventually settles.
         */

        private const float RopeDamping =
            0.994f;

        /*
         * Rope particle collision bounce.
         */

        private const float RopeBounce =
            0.12f;

        /*
         * Rope particle surface friction.
         */

        private const float RopeFriction =
            0.84f;

        /*
         * Approximate radius of a physical rope node.
         */

        private const float RopeRadius =
            2.25f;

        /*
         * Visual width.
         */

        private const float RopeWidth =
            1.2f;

        // CLONE BOUNCE

        /*
         * The clone itself keeps most of its velocity.
         */

        private const float CloneHorizontalBounce =
            0.92f;

        private const float CloneVerticalBounce =
            0.88f;

        private const float CloneGroundFriction =
            0.97f;

        private const float CloneMinimumBounceSpeed =
            0.65f;

        // ROPE STATE

        private Vector2[] ropePositions;

        private Vector2[] previousRopePositions;

        private float ropeSegmentLength;

        private bool ropeInitialized;

        // FIRST FRAME

        /*
         * Exactly one initial frame can show the connection to the
         * player.
         *
         * Afterward the player has absolutely nothing to do with
         * the rope.
         */

        private bool firstFrame =
            true;

        // INITIAL PLAYER POSITION

        private Vector2 initialPlayerPosition;

        private bool initialPlayerPositionCaptured;

        // DEFAULTS

        public override string Texture =>
            "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;

            Projectile.friendly = true;
            Projectile.hostile = false;

            /*
             * Clone collides with terrain.
             */

            Projectile.tileCollide = true;

            Projectile.ignoreWater = false;

            /*
             * Never disappears from enemy penetration.
             */

            Projectile.penetrate = -1;

            /*
             * Fixed finite lifetime.
             */

            Projectile.timeLeft =
                Lifetime;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity =
                true;

            Projectile.localNPCHitCooldown =
                10;
        }

        // AI

        public override void AI()
        {
            // CAPTURE PLAYER POSITION ONCE

            if (!initialPlayerPositionCaptured)
            {
                initialPlayerPositionCaptured =
                    true;

                if (
                    Projectile.owner >= 0 &&
                    Projectile.owner <
                    Main.maxPlayers
                )
                {
                    Player player =
                        Main.player[
                            Projectile.owner
                        ];

                    if (
                        player != null &&
                        player.active
                    )
                    {
                        initialPlayerPosition =
                            player.Center;
                    }
                    else
                    {
                        initialPlayerPosition =
                            Projectile.Center;
                    }
                }
                else
                {
                    initialPlayerPosition =
                        Projectile.Center;
                }
            }

            // INITIALIZE ROPE

            if (!ropeInitialized)
            {
                InitializeRope();

                ropeInitialized =
                    true;
            }

            // CLONE GRAVITY

            /*
             * This projectile was spawned with the ORIGINAL YOYO'S
             * CURRENT velocity.
             *
             * We preserve that velocity and add gravity normally.
             */

            Projectile.velocity.Y +=
                0.35f;

            /*
             * Extremely mild horizontal air drag.
             */

            Projectile.velocity.X *=
                0.999f;

            // ROTATION

            Projectile.rotation +=
                Projectile.velocity.X *
                0.055f;

            // ROPE

            if (!firstFrame)
            {
                SimulateRope();
            }

            // END INITIAL FRAME

            firstFrame =
                false;
        }

        // INITIALIZE ROPE

        private void InitializeRope()
        {
            ropePositions =
                new Vector2[
                    RopePointCount
                ];

            previousRopePositions =
                new Vector2[
                    RopePointCount
                ];

            float totalLength =
                MaximumStringLength;

            /*
             * Safety fallback.
             */

            if (totalLength < 8f)
            {
                totalLength = 8f;
            }

            ropeSegmentLength =
                totalLength /
                (
                    RopePointCount - 1
                );

            // INITIAL ROPE DIRECTION

            Vector2 playerDifference =
                initialPlayerPosition -
                Projectile.Center;

            Vector2 direction;

            if (
                playerDifference.LengthSquared() >
                1f
            )
            {
                direction =
                    playerDifference.SafeNormalize(
                        Vector2.UnitY
                    );
            }
            else
            {
                direction =
                    Vector2.UnitY;
            }

            // INITIAL PARTICLES

            for (
                int i = 0;
                i < RopePointCount;
                i++
            )
            {
                Vector2 position =
                    Projectile.Center +
                    direction *
                    (
                        ropeSegmentLength *
                        i
                    );

                ropePositions[i] =
                    position;

                previousRopePositions[i] =
                    position;
            }

            // INHERIT CLONE MOTION

            /*
             * Every free rope particle receives some of the clone's
             * initial motion.
             *
             * This creates natural trailing instead of freezing the
             * rope in its starting shape.
             */

            Vector2 inheritedVelocity =
                Projectile.velocity *
                0.55f;

            for (
                int i = 1;
                i < RopePointCount;
                i++
            )
            {
                previousRopePositions[i] -=
                    inheritedVelocity;
            }

            // FIX ANCHOR

            ropePositions[0] =
                Projectile.Center;

            previousRopePositions[0] =
                Projectile.Center;
        }

        // ROPE SIMULATION

        private void SimulateRope()
        {
            if (
                ropePositions == null ||
                previousRopePositions == null
            )
            {
                return;
            }

            float gravityPerStep =
                RopeGravity /
                PhysicsSubsteps;

            // PHYSICS SUBSTEPS

            for (
                int substep = 0;
                substep < PhysicsSubsteps;
                substep++
            )
            {
                // VERLET INTEGRATION

                for (
                    int i = 1;
                    i < RopePointCount;
                    i++
                )
                {
                    Vector2 current =
                        ropePositions[i];

                    Vector2 previous =
                        previousRopePositions[i];

                    /*
                     * Verlet velocity.
                     */

                    Vector2 velocity =
                        (
                            current -
                            previous
                        ) *
                        RopeDamping;

                    /*
                     * GRAVITY ON EVERY ROPE NODE.
                     */

                    velocity.Y +=
                        gravityPerStep;

                    /*
                     * Save current position.
                     */

                    previousRopePositions[i] =
                        current;

                    /*
                     * Integrate.
                     */

                    Vector2 desired =
                        current +
                        velocity;

                    /*
                     * Move through collision-aware motion.
                     */

                    MoveRopeNode(
                        i,
                        current,
                        desired,
                        velocity
                    );
                }

                // FIX CLONE ANCHOR

                ropePositions[0] =
                    Projectile.Center;

                previousRopePositions[0] =
                    Projectile.Center;

                // LENGTH CONSTRAINTS

                for (
                    int iteration = 0;
                    iteration <
                    ConstraintIterations;
                    iteration++
                )
                {
                    SolveConstraints();
                }

                // FORCE NODES OUT OF SOLIDS

                for (
                    int i = 1;
                    i < RopePointCount;
                    i++
                )
                {
                    PushNodeCompletelyOutOfTiles(
                        i
                    );
                }

                // RE-SOLVE

                for (
                    int iteration = 0;
                    iteration < 3;
                    iteration++
                )
                {
                    SolveConstraints();
                }

                // FINAL TILE CLEANUP

                for (
                    int i = 1;
                    i < RopePointCount;
                    i++
                )
                {
                    PushNodeCompletelyOutOfTiles(
                        i
                    );
                }

                // RE-PIN CLONE

                ropePositions[0] =
                    Projectile.Center;

                previousRopePositions[0] =
                    Projectile.Center;
            }
        }

        // MOVE ROPE NODE

        private void MoveRopeNode(
            int index,
            Vector2 previousPosition,
            Vector2 desiredPosition,
            Vector2 velocity)
        {
            // NO COLLISION

            if (
                !RopeNodeCollides(
                    desiredPosition
                )
            )
            {
                ropePositions[index] =
                    desiredPosition;

                return;
            }

            // SWEPT COLLISION

            /*
             * Find the final safe point before entering the tile.
             *
             * More steps than before because the rope now has
             * smaller segments.
             */

            Vector2 safePosition =
                previousPosition;

            const int sweepSteps = 20;

            for (
                int step = 1;
                step <= sweepSteps;
                step++
            )
            {
                float t =
                    step /
                    (float)sweepSteps;

                Vector2 test =
                    Vector2.Lerp(
                        previousPosition,
                        desiredPosition,
                        t
                    );

                if (
                    RopeNodeCollides(
                        test
                    )
                )
                {
                    break;
                }

                safePosition =
                    test;
            }

            ropePositions[index] =
                safePosition;

            // DETERMINE COLLISION DIRECTION

            Vector2 xMovement =
                new Vector2(
                    desiredPosition.X,
                    safePosition.Y
                );

            Vector2 yMovement =
                new Vector2(
                    safePosition.X,
                    desiredPosition.Y
                );

            bool blockedX =
                RopeNodeCollides(
                    xMovement
                );

            bool blockedY =
                RopeNodeCollides(
                    yMovement
                );

            Vector2 newVelocity =
                velocity;

            // HORIZONTAL BOUNCE

            if (blockedX)
            {
                newVelocity.X =
                    -newVelocity.X *
                    RopeBounce;
            }

            // VERTICAL BOUNCE

            if (blockedY)
            {
                newVelocity.Y =
                    -newVelocity.Y *
                    RopeBounce;
            }

            // SURFACE FRICTION

            if (blockedY)
            {
                newVelocity.X *=
                    RopeFriction;
            }

            previousRopePositions[index] =
                safePosition -
                newVelocity;
        }

        // ROPE NODE COLLISION

        private bool RopeNodeCollides(
            Vector2 position)
        {
            int size =
                (int)(
                    RopeRadius * 2f
                );

            if (size < 2)
            {
                size = 2;
            }

            Vector2 topLeft =
                position -
                new Vector2(
                    RopeRadius,
                    RopeRadius
                );

            return Collision.SolidCollision(
                topLeft,
                size,
                size
            );
        }

        // PUSH NODE COMPLETELY OUT OF TILES

        private void PushNodeCompletelyOutOfTiles(
            int index)
        {
            if (
                !RopeNodeCollides(
                    ropePositions[index]
                )
            )
            {
                return;
            }

            Vector2 original =
                ropePositions[index];

            /*
             * Search in increasingly large rings.
             *
             * The goal is not merely to find a nearby coordinate;
             * the goal is to guarantee that the entire rope particle
             * bounding box is outside solid terrain.
             */

            Vector2[] directions =
            {
                Vector2.UnitY * -1f,
                Vector2.UnitY,
                Vector2.UnitX * -1f,
                Vector2.UnitX,

                new Vector2(
                    -1f,
                    -1f
                ).SafeNormalize(
                    Vector2.UnitY
                ),

                new Vector2(
                    1f,
                    -1f
                ).SafeNormalize(
                    Vector2.UnitY
                ),

                new Vector2(
                    -1f,
                    1f
                ).SafeNormalize(
                    Vector2.UnitY
                ),

                new Vector2(
                    1f,
                    1f
                ).SafeNormalize(
                    Vector2.UnitY
                )
            };

            /*
             * First search very close to the current position.
             */

            for (
                int radius = 2;
                radius <= 32;
                radius += 2
            )
            {
                foreach (
                    Vector2 direction
                    in directions
                )
                {
                    Vector2 test =
                        original +
                        direction *
                        radius;

                    if (
                        !RopeNodeCollides(
                            test
                        )
                    )
                    {
                        ropePositions[index] =
                            test;

                        /*
                         * Remove most collision energy while
                         * preserving some movement.
                         */

                        Vector2 velocity =
                            (
                                test -
                                previousRopePositions[index]
                            ) *
                            0.25f;

                        previousRopePositions[index] =
                            test -
                            velocity;

                        return;
                    }
                }
            }

            /*
             * If the node somehow became deeply embedded, search
             * upward first because most natural rope situations
             * involve a floor.
             */

            for (
                int y = 2;
                y <= 64;
                y += 2
            )
            {
                Vector2 test =
                    original -
                    Vector2.UnitY *
                    y;

                if (
                    !RopeNodeCollides(
                        test
                    )
                )
                {
                    ropePositions[index] =
                        test;

                    previousRopePositions[index] =
                        test;

                    return;
                }
            }
        }

        // ROPE CONSTRAINTS

        private void SolveConstraints()
        {
            /*
             * Point 0 = clone.
             *
             * It is the ONLY fixed point.
             *
             * Every other point is fully dynamic.
             *
             * The solver only preserves segment length.
             */

            ropePositions[0] =
                Projectile.Center;

            for (
                int i = 1;
                i < RopePointCount;
                i++
            )
            {
                Vector2 previous =
                    ropePositions[i - 1];

                Vector2 current =
                    ropePositions[i];

                Vector2 difference =
                    current -
                    previous;

                float distance =
                    difference.Length();

                if (
                    distance <
                    0.001f
                )
                {
                    difference =
                        Vector2.UnitY;

                    distance =
                        1f;
                }

                float error =
                    distance -
                    ropeSegmentLength;

                Vector2 correction =
                    difference /
                    distance *
                    error;

                if (
                    i == 1
                )
                {
                    /*
                     * Point 0 is fixed.
                     */

                    ropePositions[i] -=
                        correction;
                }
                else
                {
                    /*
                     * Both points are dynamic.
                     */

                    ropePositions[i - 1] +=
                        correction * 0.5f;

                    ropePositions[i] -=
                        correction * 0.5f;
                }

                /*
                 * Keep anchor fixed.
                 */

                ropePositions[0] =
                    Projectile.Center;
            }
        }

        // CLONE TILE COLLISION

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            /*
             * Preserve most of the clone's momentum.
             */

            if (
                Math.Abs(
                    oldVelocity.X
                ) > 0.1f
            )
            {
                Projectile.velocity.X =
                    -oldVelocity.X *
                    CloneHorizontalBounce;
            }

            if (
                Math.Abs(
                    oldVelocity.Y
                ) > 0.1f
            )
            {
                Projectile.velocity.Y =
                    -oldVelocity.Y *
                    CloneVerticalBounce;
            }

            /*
             * Stop only extremely tiny vertical jitter.
             */

            if (
                Math.Abs(
                    Projectile.velocity.Y
                ) <
                CloneMinimumBounceSpeed
            )
            {
                Projectile.velocity.Y =
                    0f;

                Projectile.velocity.X *=
                    CloneGroundFriction;
            }

            Projectile.netUpdate =
                true;

            /*
             * Keep clone alive.
             */

            return false;
        }

        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            if (firstFrame)
            {
                DrawInitialPlayerConnection();
            }
            else
            {
                DrawPhysicsRope();
            }

            DrawCopiedYoyo(
                lightColor
            );

            return false;
        }

        // INITIAL PLAYER CONNECTION

        private void DrawInitialPlayerConnection()
        {
            if (
                !initialPlayerPositionCaptured
            )
            {
                return;
            }

            /*
             * This is the ONLY frame where the player is used for
             * drawing the Finger Loop string.
             */

            DrawStringSegment(
                initialPlayerPosition,
                Projectile.Center,
                RopeWidth,
                GetLightingForPoint(
                    (
                        initialPlayerPosition +
                        Projectile.Center
                    ) *
                    0.5f
                )
            );
        }

        // PHYSICS ROPE DRAW

        private void DrawPhysicsRope()
        {
            if (
                ropePositions == null ||
                ropePositions.Length <
                2
            )
            {
                return;
            }

            /*
             * The rope is rendered from its actual physical particles.
             *
             * Every physical segment receives several visual samples
             * so the lighting changes smoothly.
             */

            for (
                int i = 0;
                i < RopePointCount - 1;
                i++
            )
            {
                Vector2 p0 =
                    i == 0
                        ? ropePositions[i]
                        : ropePositions[i - 1];

                Vector2 p1 =
                    ropePositions[i];

                Vector2 p2 =
                    ropePositions[i + 1];

                Vector2 p3 =
                    i + 2 <
                    RopePointCount
                        ? ropePositions[i + 2]
                        : p2;

                const int samples =
                    4;

                Vector2 previous =
                    p1;

                for (
                    int sample = 1;
                    sample <= samples;
                    sample++
                )
                {
                    float t =
                        sample /
                        (float)samples;

                    /*
                     * Centripetal-ish interpolation using a simple
                     * smooth cubic interpolation.
                     *
                     * Unlike the old Catmull-Rom implementation,
                     * this clamps the rendered point between the
                     * neighboring physics points so it cannot make
                     * giant visual loops.
                     */

                    Vector2 point =
                        SmoothPoint(
                            p0,
                            p1,
                            p2,
                            p3,
                            t
                        );

                    /*
                     * Keep rendering inside the physical segment's
                     * local endpoints.
                     */

                    Vector2 segment =
                        p2 - p1;

                    float segmentLength =
                        segment.Length();

                    if (
                        segmentLength >
                        0.001f
                    )
                    {
                        Vector2 fromStart =
                            point - p1;

                        float projected =
                            Vector2.Dot(
                                fromStart,
                                segment /
                                segmentLength
                            );

                        projected =
                            MathHelper.Clamp(
                                projected,
                                0f,
                                segmentLength
                            );

                        point =
                            p1 +
                            segment /
                            segmentLength *
                            projected;
                    }

                    Color lighting =
                        GetLightingForPoint(
                            (
                                previous +
                                point
                            ) *
                            0.5f
                        );

                    DrawStringSegment(
                        previous,
                        point,
                        RopeWidth,
                        lighting
                    );

                    previous =
                        point;
                }
            }
        }

        // SMOOTH POINT

        private static Vector2 SmoothPoint(
            Vector2 p0,
            Vector2 p1,
            Vector2 p2,
            Vector2 p3,
            float t)
        {
            /*
             * Hermite-style interpolation.
             *
             * Tangents are deliberately limited so visual
             * interpolation cannot overshoot the actual rope.
             */

            Vector2 tangent1 =
                (p2 - p0) *
                0.5f;

            Vector2 tangent2 =
                (p3 - p1) *
                0.5f;

            float t2 =
                t * t;

            float t3 =
                t2 * t;

            float h00 =
                2f * t3 -
                3f * t2 +
                1f;

            float h10 =
                t3 -
                2f * t2 +
                t;

            float h01 =
                -2f * t3 +
                3f * t2;

            float h11 =
                t3 -
                t2;

            Vector2 result =
                h00 * p1 +
                h10 * tangent1 +
                h01 * p2 +
                h11 * tangent2;

            /*
             * Hard clamp to the local bounding rectangle.
             *
             * This prevents interpolation from making the string
             * visually jump outside its simulated neighborhood.
             */

            float minX =
                Math.Min(
                    p1.X,
                    p2.X
                );

            float maxX =
                Math.Max(
                    p1.X,
                    p2.X
                );

            float minY =
                Math.Min(
                    p1.Y,
                    p2.Y
                );

            float maxY =
                Math.Max(
                    p1.Y,
                    p2.Y
                );

            result.X =
                MathHelper.Clamp(
                    result.X,
                    minX,
                    maxX
                );

            result.Y =
                MathHelper.Clamp(
                    result.Y,
                    minY,
                    maxY
                );

            return result;
        }

        // LIGHTING

        private static Color GetLightingForPoint(
            Vector2 position)
        {
            int tileX =
                (int)(
                    position.X /
                    16f
                );

            int tileY =
                (int)(
                    position.Y /
                    16f
                );

            /*
             * Outside the world.
             */

            if (
                tileX < 0 ||
                tileY < 0 ||
                tileX >= Main.maxTilesX ||
                tileY >= Main.maxTilesY
            )
            {
                return Color.White;
            }

            /*
             * Direct Terraria world lighting.
             */

            return Lighting.GetColor(
                tileX,
                tileY
            );
        }

        // STRING SEGMENT

        private static void DrawStringSegment(
            Vector2 start,
            Vector2 end,
            float width,
            Color lighting)
        {
            Vector2 difference =
                end -
                start;

            float length =
                difference.Length();

            if (
                length <= 0.5f
            )
            {
                return;
            }

            Texture2D pixel =
                TextureAssets
                    .MagicPixel
                    .Value;

            Main.EntitySpriteDraw(
                pixel,
                (
                    start +
                    end
                ) *
                0.5f -
                Main.screenPosition,
                new Rectangle(
                    0,
                    0,
                    1,
                    1
                ),
                lighting,
                difference.ToRotation(),
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

        // DRAW YOYO

        private void DrawCopiedYoyo(
            Color lightColor)
        {
            int type =
                SourceYoyoType;

            if (
                type < 0 ||
                type >=
                TextureAssets
                    .Projectile
                    .Length
            )
            {
                return;
            }

            Texture2D texture =
                TextureAssets
                    .Projectile[
                        type
                    ].Value;

            if (texture == null)
            {
                return;
            }

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                texture.Size() *
                0.5f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );
        }

        // COPIED MAXIMUM DISTANCE

        public float GetCopiedMaximumDistance()
        {
            return MaximumStringLength;
        }
    }
}