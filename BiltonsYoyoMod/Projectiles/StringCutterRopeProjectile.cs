using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items.Accessories;

namespace BiltonsYoyoMod.Projectiles
{
    public class StringCutterRopeProjectile : ModProjectile
    {
        // AI DATA

        /*
         * ai[0] = original yoyo projectile type
         * ai[1] = ORIGINAL maximum string distance copied from yoyo
         * ai[2] = StringCutterType
         */

        private int SourceYoyoType =>
            (int)Projectile.ai[0];

        private float OriginalMaximumStringLength =>
            Math.Max(
                8f,
                Projectile.ai[1]
            );

        /*
         * ACTUAL STRING CUTTER DISTANCE.
         *
         * Original maximum distance × 67.5%.
         */
        private float MaximumStringLength =>
            Math.Max(
                8f,
                OriginalMaximumStringLength * 0.675f
            );

        private StringCutterType CutterType =>
            (StringCutterType)(int)Projectile.ai[2];

        // LIFETIME

        private const int Lifetime =
            20 * 60;

        // ROPE PHYSICS

        /*
         * Keep the original 56-node storage.
         *
         * We dynamically choose how many nodes are ACTIVE instead
         * of squeezing all 56 into a short rope.
         */
        private const int RopePointCount =
            56;

        private const int PhysicsSubsteps =
            4;

        private const int ConstraintIterations =
            8;

        private const float RopeGravity =
            0.30f;

        private const float RopeDamping =
            0.994f;

        private const float RopeBounce =
            0.12f;

        private const float RopeFriction =
            0.84f;

        private const float RopeRadius =
            2.25f;

        private const float RopeWidth =
            1.2f;

        // YOYO PHYSICS

        private const float CloneHorizontalBounce =
            0.92f;

        private const float CloneVerticalBounce =
            0.88f;

        private const float CloneGroundFriction =
            0.97f;

        private const float CloneMinimumBounceSpeed =
            0.65f;

        // ROPE RESOLUTION

        /*
         * This represents the approximate physical spacing of the
         * original Finger Loop-style rope.
         *
         * The shortened String Cutter rope uses approximately the
         * same spacing, but therefore needs fewer active nodes.
         */
        private float BaseSegmentLength;

        private int ActiveRopePointCount;

        private float RopeMaximumPathLength;

        // STATE

        private Vector2[] ropePositions;

        private Vector2[] previousRopePositions;

        private bool ropeInitialized;

        private bool firstFrame = true;

        private int heatedTimer;

        // DEFAULTS

        public override string Texture =>
            "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;

            Projectile.penetrate = -1;

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
            Player player =
                GetOwner();

            if (player == null)
            {
                Projectile.Kill();
                return;
            }

            // INITIALIZE ROPE

            if (!ropeInitialized)
            {
                InitializeRope(
                    player
                );

                ropeInitialized =
                    true;
            }

            // DETACHED YOYO PHYSICS

            Projectile.velocity.Y +=
                0.35f;

            Projectile.velocity.X *=
                0.999f;

            Projectile.rotation +=
                Projectile.velocity.X *
                0.055f;

            // HARD 67.5% RANGE LIMIT

            EnforceMaximumDistance(
                player
            );

            // ROPE

            SimulateRope(
                player
            );

            // HEATED NIPPER

            if (
                CutterType ==
                StringCutterType.Heated
            )
            {
                heatedTimer++;

                if (
                    heatedTimer <= 30 &&
                    Main.rand.NextBool(2)
                )
                {
                    SpawnHeatedEmber();
                }

                if (
                    Projectile.velocity.Y > 0f
                )
                {
                    ApplyFireAura();
                }
            }

            firstFrame =
                false;
        }

        // OWNER

        private Player GetOwner()
        {
            if (
                Projectile.owner < 0 ||
                Projectile.owner >= Main.maxPlayers
            )
            {
                return null;
            }

            Player player =
                Main.player[
                    Projectile.owner
                ];

            if (
                player == null ||
                !player.active
            )
            {
                return null;
            }

            return player;
        }

        // CALCULATE ACTIVE NODE COUNT

        private void CalculateRopeResolution()
        {
            /*
             * The original Finger Loop rope has:
             *
             * 56 nodes
             * 55 segments
             *
             * Preserve approximately that physical node spacing.
             */
            BaseSegmentLength =
                OriginalMaximumStringLength /
                (RopePointCount - 1);

            if (
                BaseSegmentLength < 1f
            )
            {
                BaseSegmentLength = 1f;
            }

            /*
             * Determine how many segments are required for the
             * shortened 67.5% rope.
             *
             * This prevents the rope from becoming a microscopic
             * 56-node compressed mess.
             */
            int requiredSegments =
                (int)Math.Ceiling(
                    MaximumStringLength /
                    BaseSegmentLength
                );

            /*
             * At least a few segments are necessary for the rope to
             * remain visually and physically meaningful.
             */
            requiredSegments =
                Math.Max(
                    4,
                    requiredSegments
                );

            /*
             * Never exceed the available 55 segments.
             */
            requiredSegments =
                Math.Min(
                    RopePointCount - 1,
                    requiredSegments
                );

            ActiveRopePointCount =
                requiredSegments + 1;

            /*
             * Actual maximum path represented by the active nodes.
             *
             * The final endpoint itself is still hard-capped to the
             * exact 67.5% maximum.
             */
            RopeMaximumPathLength =
                MaximumStringLength;
        }

        // HARD MAXIMUM DISTANCE

        private void EnforceMaximumDistance(
            Player player)
        {
            Vector2 anchor =
                player.Center;

            Vector2 difference =
                Projectile.Center -
                anchor;

            float distance =
                difference.Length();

            float maximumDistance =
                MaximumStringLength;

            if (
                distance <= maximumDistance
            )
            {
                return;
            }

            Vector2 direction;

            if (
                distance > 0.001f
            )
            {
                direction =
                    difference /
                    distance;
            }
            else
            {
                direction =
                    Vector2.UnitY;
            }

            /*
             * Hard position cap.
             */
            Projectile.Center =
                anchor +
                direction *
                maximumDistance;

            /*
             * Preserve tangential movement while removing the
             * velocity trying to leave the allowed range.
             */
            Vector2 velocity =
                Projectile.velocity;

            float outwardSpeed =
                Vector2.Dot(
                    velocity,
                    direction
                );

            if (
                outwardSpeed > 0f
            )
            {
                Projectile.velocity -=
                    direction *
                    outwardSpeed;
            }

            Projectile.netUpdate =
                true;

            if (
                ropePositions != null &&
                previousRopePositions != null
            )
            {
                int last =
                    ActiveRopePointCount - 1;

                if (
                    last >= 0 &&
                    last < RopePointCount
                )
                {
                    ropePositions[last] =
                        Projectile.Center;

                    previousRopePositions[last] =
                        Projectile.Center;
                }
            }
        }

        // INITIALIZE ROPE

        private void InitializeRope(
            Player player)
        {
            ropePositions =
                new Vector2[
                    RopePointCount
                ];

            previousRopePositions =
                new Vector2[
                    RopePointCount
                ];

            CalculateRopeResolution();

            Vector2 playerPosition =
                player.Center;

            Vector2 yoyoPosition =
                Projectile.Center;

            Vector2 difference =
                yoyoPosition -
                playerPosition;

            float currentDistance =
                difference.Length();

            Vector2 direction;

            if (
                currentDistance > 0.001f
            )
            {
                direction =
                    difference /
                    currentDistance;
            }
            else
            {
                direction =
                    Vector2.UnitY;
            }

            /*
             * IMPORTANT:
             *
             * Never initialize the rope beyond the String Cutter's
             * actual 67.5% range.
             */
            float initialDistance =
                Math.Min(
                    currentDistance,
                    MaximumStringLength
                );

            Vector2 initialEnd =
                playerPosition +
                direction *
                initialDistance;

            /*
             * If the original yoyo was beyond the allowed range,
             * immediately clamp the copied projectile too.
             */
            if (
                currentDistance >
                MaximumStringLength
            )
            {
                Projectile.Center =
                    initialEnd;

                Vector2 velocity =
                    Projectile.velocity;

                float outwardSpeed =
                    Vector2.Dot(
                        velocity,
                        direction
                    );

                if (
                    outwardSpeed > 0f
                )
                {
                    Projectile.velocity -=
                        direction *
                        outwardSpeed;
                }
            }

            // INITIAL NODE POSITIONS

            int last =
                ActiveRopePointCount - 1;

            for (
                int i = 0;
                i < ActiveRopePointCount;
                i++
            )
            {
                float progress =
                    i /
                    (float)last;

                Vector2 position =
                    Vector2.Lerp(
                        playerPosition,
                        initialEnd,
                        progress
                    );

                ropePositions[i] =
                    position;

                previousRopePositions[i] =
                    position;
            }

            /*
             * Unused nodes are kept safely at the final point.
             */
            for (
                int i = ActiveRopePointCount;
                i < RopePointCount;
                i++
            )
            {
                ropePositions[i] =
                    initialEnd;

                previousRopePositions[i] =
                    initialEnd;
            }

            // INHERITED YOYO MOTION

            Vector2 inheritedVelocity =
                Projectile.velocity *
                0.55f;

            for (
                int i = 1;
                i < last;
                i++
            )
            {
                previousRopePositions[i] -=
                    inheritedVelocity;
            }

            LockEndpoints(
                player
            );
        }

        // ROPE SIMULATION

        private void SimulateRope(
            Player player)
        {
            if (
                ropePositions == null ||
                previousRopePositions == null
            )
            {
                return;
            }

            int last =
                ActiveRopePointCount - 1;

            if (last < 1)
            {
                return;
            }

            float gravity =
                RopeGravity /
                PhysicsSubsteps;

            for (
                int substep = 0;
                substep <
                PhysicsSubsteps;
                substep++
            )
            {
                // VERLET

                for (
                    int i = 1;
                    i < last;
                    i++
                )
                {
                    Vector2 current =
                        ropePositions[i];

                    Vector2 previous =
                        previousRopePositions[i];

                    Vector2 velocity =
                        (
                            current -
                            previous
                        ) *
                        RopeDamping;

                    velocity.Y +=
                        gravity;

                    previousRopePositions[i] =
                        current;

                    Vector2 desired =
                        current +
                        velocity;

                    MoveRopeNode(
                        i,
                        current,
                        desired,
                        velocity
                    );
                }

                // LOCK ENDPOINTS

                LockEndpoints(
                    player
                );

                // SEGMENT CONSTRAINTS

                for (
                    int iteration = 0;
                    iteration <
                    ConstraintIterations;
                    iteration++
                )
                {
                    SolveConstraints(
                        player
                    );
                }

                // TILE COLLISION

                for (
                    int i = 1;
                    i < last;
                    i++
                )
                {
                    PushNodeOutOfTiles(
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
                    SolveConstraints(
                        player
                    );
                }

                // FINAL ENDPOINT LOCK

                LockEndpoints(
                    player
                );
            }
        }

        // LOCK ENDPOINTS

        private void LockEndpoints(
            Player player)
        {
            int last =
                ActiveRopePointCount - 1;

            /*
             * FIRST END:
             *
             * Player.
             */
            ropePositions[0] =
                player.Center;

            previousRopePositions[0] =
                player.Center;

            /*
             * LAST END:
             *
             * Detached yoyo.
             */
            ropePositions[last] =
                Projectile.Center;

            previousRopePositions[last] =
                Projectile.Center;
        }

        // MOVE ROPE NODE

        private void MoveRopeNode(
            int index,
            Vector2 previousPosition,
            Vector2 desiredPosition,
            Vector2 velocity)
        {
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

            Vector2 safePosition =
                previousPosition;

            const int sweepSteps =
                20;

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
                    RopeNodeCollides(test)
                )
                {
                    break;
                }

                safePosition =
                    test;
            }

            ropePositions[index] =
                safePosition;

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

            if (blockedX)
            {
                newVelocity.X =
                    -newVelocity.X *
                    RopeBounce;
            }

            if (blockedY)
            {
                newVelocity.Y =
                    -newVelocity.Y *
                    RopeBounce;

                newVelocity.X *=
                    RopeFriction;
            }

            previousRopePositions[index] =
                safePosition -
                newVelocity;
        }

        // ROPE COLLISION

        private bool RopeNodeCollides(
            Vector2 position)
        {
            int size =
                (int)(
                    RopeRadius *
                    2f
                );

            if (size < 2)
            {
                size = 2;
            }

            Vector2 topLeft =
                position -
                new Vector2(
                    RopeRadius
                );

            return Collision.SolidCollision(
                topLeft,
                size,
                size
            );
        }

        // PUSH NODE OUT

        private void PushNodeOutOfTiles(
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

            Vector2[] directions =
            {
                -Vector2.UnitY,
                Vector2.UnitY,
                -Vector2.UnitX,
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
                        !RopeNodeCollides(test)
                    )
                    {
                        ropePositions[index] =
                            test;

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

            // DEEP EMBED FALLBACK

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
                    !RopeNodeCollides(test)
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

        // CONSTRAINTS

        private void SolveConstraints(
            Player player)
        {
            int last =
                ActiveRopePointCount - 1;

            LockEndpoints(
                player
            );

            for (
                int i = 1;
                i < ActiveRopePointCount;
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
                    distance < 0.001f
                )
                {
                    continue;
                }

                /*
                 * Each active segment has a maximum physical length.
                 *
                 * It can be shorter, which allows sagging.
                 */
                if (
                    distance <=
                    BaseSegmentLength
                )
                {
                    continue;
                }

                float excess =
                    distance -
                    BaseSegmentLength;

                Vector2 direction =
                    difference /
                    distance;

                Vector2 correction =
                    direction *
                    excess;

                bool playerEndpoint =
                    i == 1;

                bool yoyoEndpoint =
                    i == last;

                if (
                    playerEndpoint &&
                    yoyoEndpoint
                )
                {
                    /*
                     * Extremely short rope.
                     *
                     * Both ends remain fixed.
                     */
                }
                else if (
                    playerEndpoint
                )
                {
                    /*
                     * Player cannot move.
                     */
                    ropePositions[i] -=
                        correction;
                }
                else if (
                    yoyoEndpoint
                )
                {
                    /*
                     * Yoyo endpoint cannot move during the rope
                     * constraint solve.
                     */
                    ropePositions[i - 1] +=
                        correction;
                }
                else
                {
                    /*
                     * Normal internal rope nodes.
                     */
                    ropePositions[i - 1] +=
                        correction *
                        0.5f;

                    ropePositions[i] -=
                        correction *
                        0.5f;
                }

                LockEndpoints(
                    player
                );
            }
        }

        // YOYO TILE COLLISION

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            if (
                Math.Abs(oldVelocity.X) >
                0.1f
            )
            {
                Projectile.velocity.X =
                    -oldVelocity.X *
                    CloneHorizontalBounce;
            }

            if (
                Math.Abs(oldVelocity.Y) >
                0.1f
            )
            {
                Projectile.velocity.Y =
                    -oldVelocity.Y *
                    CloneVerticalBounce;
            }

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

            return false;
        }

        // HEATED EMBER

        private void SpawnHeatedEmber()
        {
            int emberType =
                ModContent.ProjectileType<
                    StringCutterEmberProjectile>();

            Vector2 velocity =
                new Vector2(
                    Main.rand.NextFloat(
                        -1.2f,
                        1.2f
                    ),
                    Main.rand.NextFloat(
                        -2.5f,
                        -0.5f
                    )
                );

            int ember =
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center +
                    Main.rand.NextVector2Circular(
                        8f,
                        8f
                    ),
                    velocity,
                    emberType,
                    Math.Max(
                        1,
                        Projectile.damage / 4
                    ),
                    0f,
                    Projectile.owner
                );

            if (
                ember >= 0 &&
                ember < Main.maxProjectiles
            )
            {
                Main.projectile[
                    ember
                ].netUpdate = true;
            }
        }

        // HEATED AURA

        private void ApplyFireAura()
        {
            const float radius =
                28f;

            float radiusSquared =
                radius * radius;

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
                    npc.friendly ||
                    npc.dontTakeDamage
                )
                {
                    continue;
                }

                if (
                    Vector2.DistanceSquared(
                        npc.Center,
                        Projectile.Center
                    ) >
                    radiusSquared
                )
                {
                    continue;
                }

                npc.AddBuff(
                    BuffID.OnFire,
                    30
                );
            }
        }

        // HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            if (
                CutterType ==
                StringCutterType.Heated
            )
            {
                target.AddBuff(
                    BuffID.OnFire,
                    180
                );
            }
        }

        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            if (firstFrame)
            {
                DrawInitialConnection();
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

        // INITIAL CONNECTION

        private void DrawInitialConnection()
        {
            Player player =
                GetOwner();

            if (player == null)
                return;

            DrawStringSegment(
                player.Center,
                Projectile.Center,
                RopeWidth,
                GetLightingForPoint(
                    (
                        player.Center +
                        Projectile.Center
                    ) * 0.5f
                )
            );
        }

        // DRAW ROPE

        private void DrawPhysicsRope()
        {
            if (
                ropePositions == null ||
                ActiveRopePointCount < 2
            )
            {
                return;
            }

            /*
             * Only draw active nodes.
             *
             * Unused storage nodes never participate in rendering.
             */
            for (
                int i = 0;
                i < ActiveRopePointCount - 1;
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
                    ActiveRopePointCount
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

                    Vector2 point =
                        SmoothPoint(
                            p0,
                            p1,
                            p2,
                            p3,
                            t
                        );

                    Vector2 segment =
                        p2 -
                        p1;

                    float segmentLength =
                        segment.Length();

                    if (
                        segmentLength >
                        0.001f
                    )
                    {
                        Vector2 direction =
                            segment /
                            segmentLength;

                        float projected =
                            Vector2.Dot(
                                point -
                                p1,
                                direction
                            );

                        projected =
                            MathHelper.Clamp(
                                projected,
                                0f,
                                segmentLength
                            );

                        point =
                            p1 +
                            direction *
                            projected;
                    }

                    Color lighting =
                        GetLightingForPoint(
                            (
                                previous +
                                point
                            ) * 0.5f
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
            Vector2 tangent1 =
                (p2 - p0) * 0.5f;

            Vector2 tangent2 =
                (p3 - p1) * 0.5f;

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

            result.X =
                MathHelper.Clamp(
                    result.X,
                    Math.Min(
                        p1.X,
                        p2.X
                    ),
                    Math.Max(
                        p1.X,
                        p2.X
                    )
                );

            result.Y =
                MathHelper.Clamp(
                    result.Y,
                    Math.Min(
                        p1.Y,
                        p2.Y
                    ),
                    Math.Max(
                        p1.Y,
                        p2.Y
                    )
                );

            return result;
        }

        // LIGHTING

        private static Color GetLightingForPoint(
            Vector2 position)
        {
            int tileX =
                (int)(
                    position.X / 16f
                );

            int tileY =
                (int)(
                    position.Y / 16f
                );

            if (
                tileX < 0 ||
                tileY < 0 ||
                tileX >= Main.maxTilesX ||
                tileY >= Main.maxTilesY
            )
            {
                return Color.White;
            }

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

            if (length <= 0.5f)
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
                ) * 0.5f -
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
                    .Projectile[type]
                    .Value;

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
                texture.Size() * 0.5f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );
        }

        // EFFECTIVE MAXIMUM DISTANCE

        public float GetCopiedMaximumDistance()
        {
            return MaximumStringLength;
        }
    }
}