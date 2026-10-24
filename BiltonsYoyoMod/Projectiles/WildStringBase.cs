using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public abstract class WildStringBase : ModProjectile
    {
        // SPIN SPEED CONFIG

        // Global multiplier applied to ALL intentional Wild String rotation.
        //
        // Safe to change at any time: every rotational term in this file
        // reads through EffectiveSpinMult(), so there is no fixed speed
        // baked into spawns, saves or netcode.
        //
        // 1.00f = 100%
        // 0.75f = 75%
        // 0.50f = 50%
        //
        // The in-game Config slider (Wild String Spin) multiplies on top.
        private static float spinSpeedMultiplier = 1.00f;

        public static float SpinSpeedMultiplier
        {
            get => spinSpeedMultiplier;
            set => spinSpeedMultiplier = MathHelper.Clamp(value, 0f, 3f);
        }

        // Combined code + config multiplier. All rotation below uses this.
        public static float EffectiveSpinMult()
        {
            float cfg = 1f;
            try { cfg = ModContent.GetInstance<Configuration.YoyoConfig>()?.WildStringSpeed ?? 1f; }
            catch { }
            return spinSpeedMultiplier * cfg;
        }

        // VARIANT SETTINGS

        protected abstract int MinimumDistance { get; }
        protected abstract int MaximumDistance { get; }

        protected abstract float MinimumSpeedDegrees { get; }
        protected abstract float MaximumSpeedDegrees { get; }

        // COLORS

        protected virtual Color StringColor
        {
            get
            {
                int type =
                    Projectile.type;

                if (
                    type ==
                    ModContent.ProjectileType<
                        WildStringsAPCProj>()
                )
                {
                    return new Color(
                        70,
                        220,
                        90
                    );
                }

                if (
                    type ==
                    ModContent.ProjectileType<
                        WildStringsAPProj>()
                )
                {
                    return new Color(
                        80,
                        150,
                        255
                    );
                }

                if (
                    type ==
                    ModContent.ProjectileType<
                        WildStringsANProj>()
                )
                {
                    return new Color(
                        255,
                        220,
                        70
                    );
                }

                if (
                    type ==
                    ModContent.ProjectileType<
                        WildStringsAKProj>()
                )
                {
                    return new Color(
                        139,
                        90,
                        43
                    );
                }

                // AC Cotton = white.
                return Color.White;
            }
        }

        // CENTER ROTATION

        protected float Angle;
        protected float Distance;

        /*
         * AngularSpeed stores the RAW randomly rolled speed.
         *
         * SpinSpeedMultiplier is applied only when the speed is used.
         */

        protected float AngularSpeed;

        private int OrbitDirection = 1;

        // LIFETIME

        private const int MinimumLifetime =
            15 * 60;

        private const int MaximumLifetime =
            35 * 60;

        // ROPE PHYSICS (tuned for cost: fewer substeps/sweeps/samples than
        // the original sim with no visible change in open air; tile bends
        // resolve marginally coarser at sub-pixel scale only).

        private const int RopePointCount = 36;

        private const int PhysicsSubsteps = 3;

        private const int ConstraintIterations = 5;

        private const float RopeGravity = 0.30f;

        private const float RopeDamping = 0.998f;

        private const float RopeBounce = 0.05f;

        private const float RopeFriction = 0.91f;

        private const float RopeRadius = 2f;

        private const float RopeWidth = 1.25f;

        private const int RopeSweepSteps = 8;

        private const int RopeEscapeDistance = 36;

        // TILE COLLISION

        /*
         * The rope keeps its segment length when wrapping around tiles.
         *
         * Instead of moving a node to a random safe position, the child
         * node is rotated around its parent until the entire segment is
         * outside the tile.
         *
         * This lets the rope bend around corners naturally.
         */

        private const float RopeSegmentCollisionSpacing =
            2f;

        private const int RopeWrapSearchSteps =
            120;

        private const float RopeWrapSearchStepDegrees =
            1.5f;

        private const float RopeCollisionSkin =
            0.05f;

        private const int RopeWrapPasses =
            2;

        // CENTER MOTOR

        private const int DrivenPointCount = 5;

        private const float MinimumRotationSpeed =
            0.0025f;

        private const float CenterTorque =
            0.095f;

        private const float CenterSpeedCorrection =
            0.42f;

        private const float MinimumCenterSpeed =
            0.65f;

        private const float MaximumCenterVelocity =
            18f;

        // ROTATIONAL MOMENTUM

        private float[] rotationalMomentum;

        private const float RotationTransfer =
            0.62f;

        private const float CollisionRotationTransfer =
            0.08f;

        private const float RotationRetention =
            0.995f;

        private const float MinimumRotationalMomentum =
            0.01f;

        private const float MaximumRotationalMomentum =
            1.5f;

        // STUCK DETECTION

        private int stuckTimer;

        private const int StuckLimit =
            60;

        private Vector2 lastEndpointPosition;

        // ROPE STATE

        private Vector2[] ropePositions;

        private Vector2[] previousRopePositions;

        private float ropeSegmentLength;

        private bool ropeInitialized;

        // PLAYER STATE

        private Vector2 currentPlayerPosition;

        private Vector2 previousPlayerPosition;

        // TEXTURE

        public override string Texture =>
            "Terraria/Images/MagicPixel";

        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;

            Projectile.friendly = true;
            Projectile.hostile = false;

            /*
             * Terraria does not control the endpoint.
             *
             * Rope physics handles collisions itself.
             */

            Projectile.tileCollide = false;

            Projectile.ignoreWater = false;

            Projectile.penetrate = -1;

            Projectile.timeLeft =
                MaximumLifetime;

            Projectile.DamageType =
                DamageClass.Melee;
        }

        // EFFECTIVE ROTATION VALUES

        private float GetEffectiveAngularSpeed()
        {
            return Math.Max(
                0f,
                AngularSpeed *
                EffectiveSpinMult()
            );
        }

        private float GetEffectiveMinimumRotationSpeed()
        {
            return
                MinimumRotationSpeed *
                EffectiveSpinMult();
        }

        private float GetEffectiveCenterTorque()
        {
            return
                CenterTorque *
                EffectiveSpinMult();
        }

        private float GetEffectiveMinimumCenterSpeed()
        {
            return
                MinimumCenterSpeed *
                EffectiveSpinMult();
        }

        // SPAWN

        public override void OnSpawn(
            Terraria.DataStructures.IEntitySource source)
        {
            if (
                Projectile.owner < 0 ||
                Projectile.owner >= Main.maxPlayers
            )
            {
                return;
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
                return;
            }

            Angle =
                Main.rand.NextFloat(
                    0f,
                    MathHelper.TwoPi
                );

            Distance =
                Main.rand.NextFloat(
                    MinimumDistance,
                    MaximumDistance
                );

            AngularSpeed =
                MathHelper.ToRadians(
                    Main.rand.NextFloat(
                        MinimumSpeedDegrees,
                        MaximumSpeedDegrees
                    )
                );

            OrbitDirection =
                Main.rand.NextBool()
                    ? 1
                    : -1;

            Projectile.localAI[0] =
                Distance;

            Projectile.localAI[1] =
                AngularSpeed;

            Projectile.timeLeft =
                Main.rand.Next(
                    MinimumLifetime,
                    MaximumLifetime + 1
                );

            currentPlayerPosition =
                player.Center;

            previousPlayerPosition =
                currentPlayerPosition;

            Vector2 direction =
                Angle.ToRotationVector2();

            Vector2 endpoint =
                player.Center +
                direction *
                Distance;

            endpoint =
                FindSafePosition(
                    endpoint
                );

            InitializeRope(
                player.Center,
                endpoint,
                direction
            );

            ropeInitialized =
                true;

            rotationalMomentum =
                new float[
                    RopePointCount
                ];

            int last =
                RopePointCount - 1;

            Projectile.Center =
                ropePositions[last];

            Projectile.velocity =
                Vector2.Zero;

            lastEndpointPosition =
                Projectile.Center;

            float initialMomentum =
                Math.Max(
                    0.25f *
                    EffectiveSpinMult(),
                    GetEffectiveAngularSpeed() *
                    12f
                );

            for (
                int i = 1;
                i < RopePointCount;
                i++
            )
            {
                float influence =
                    1f -
                    (
                        i /
                        (float)RopePointCount
                    );

                rotationalMomentum[i] =
                    initialMomentum *
                    influence;
            }

            Projectile.netUpdate =
                true;
        }

        // AI

        public override void AI()
        {
            if (
                Projectile.owner < 0 ||
                Projectile.owner >= Main.maxPlayers
            )
            {
                Projectile.Kill();
                return;
            }

            Player player =
                Main.player[
                    Projectile.owner
                ];

            if (
                player == null ||
                !player.active ||
                player.dead
            )
            {
                Projectile.Kill();
                return;
            }

            previousPlayerPosition =
                currentPlayerPosition;

            currentPlayerPosition =
                player.Center;

            Distance =
                Projectile.localAI[0];

            AngularSpeed =
                Projectile.localAI[1];

            float rotationSpeed =
                Math.Max(
                    GetEffectiveAngularSpeed(),
                    GetEffectiveMinimumRotationSpeed()
                );

            Angle +=
                rotationSpeed *
                OrbitDirection;

            while (
                Angle >=
                MathHelper.TwoPi
            )
            {
                Angle -=
                    MathHelper.TwoPi;
            }

            while (
                Angle < 0f
            )
            {
                Angle +=
                    MathHelper.TwoPi;
            }

            // ROPE SAFETY

            if (
                !ropeInitialized ||
                ropePositions == null ||
                previousRopePositions == null ||
                rotationalMomentum == null
            )
            {
                InitializeRope(
                    player.Center,
                    Projectile.Center,
                    Angle.ToRotationVector2()
                );

                rotationalMomentum =
                    new float[
                        RopePointCount
                    ];

                ropeInitialized =
                    true;
            }

            // PHYSICS

            SimulateRope(
                player.Center,
                rotationSpeed
            );

            // FREE ENDPOINT

            int last =
                RopePointCount - 1;

            Projectile.Center =
                ropePositions[last];

            Projectile.velocity =
                ropePositions[last] -
                previousRopePositions[last];

            // STUCK DETECTION

            float endpointMovement =
                Vector2.DistanceSquared(
                    Projectile.Center,
                    lastEndpointPosition
                );

            if (
                endpointMovement < 0.05f
            )
            {
                stuckTimer++;

                if (
                    stuckTimer >=
                    StuckLimit
                )
                {
                    /*
                     * Reverse the CENTER source only.
                     */

                    OrbitDirection *=
                        -1;

                    stuckTimer = 0;

                    ApplyCenterRelease(
                        player.Center
                    );

                    Projectile.netUpdate =
                        true;
                }
            }
            else
            {
                stuckTimer = 0;
            }

            lastEndpointPosition =
                Projectile.Center;
        }

        // ROPE INITIALIZATION

        private void InitializeRope(
            Vector2 playerPosition,
            Vector2 endpoint,
            Vector2 direction)
        {
            ropePositions =
                new Vector2[
                    RopePointCount
                ];

            previousRopePositions =
                new Vector2[
                    RopePointCount
                ];

            float length =
                Math.Max(
                    8f,
                    Vector2.Distance(
                        playerPosition,
                        endpoint
                    )
                );

            ropeSegmentLength =
                length /
                (
                    RopePointCount - 1
                );

            Vector2 normal =
                direction.SafeNormalize(
                    Vector2.UnitX
                );

            for (
                int i = 0;
                i < RopePointCount;
                i++
            )
            {
                Vector2 position =
                    playerPosition +
                    normal *
                    ropeSegmentLength *
                    i;

                ropePositions[i] =
                    position;

                previousRopePositions[i] =
                    position;
            }

            Vector2 tangent =
                normal.RotatedBy(
                    MathHelper.PiOver2 *
                    OrbitDirection
                );

            float initialVelocity =
                Math.Max(
                    GetEffectiveMinimumCenterSpeed(),
                    length *
                    Math.Max(
                        GetEffectiveAngularSpeed(),
                        GetEffectiveMinimumRotationSpeed()
                    )
                );

            for (
                int i = 1;
                i < RopePointCount;
                i++
            )
            {
                float influence =
                    i /
                    (float)(
                        RopePointCount - 1
                    );

                previousRopePositions[i] -=
                    tangent *
                    initialVelocity *
                    influence;
            }

            ropePositions[0] =
                playerPosition;

            previousRopePositions[0] =
                playerPosition;
        }

        // ROPE SIMULATION

        private void SimulateRope(
            Vector2 playerPosition,
            float rotationSpeed)
        {
            int last =
                RopePointCount - 1;

            float gravity =
                RopeGravity /
                PhysicsSubsteps;

            for (
                int substep = 0;
                substep < PhysicsSubsteps;
                substep++
            )
            {
                float progress =
                    (substep + 1f) /
                    PhysicsSubsteps;

                Vector2 simulatedPlayer =
                    Vector2.Lerp(
                        previousPlayerPosition,
                        playerPosition,
                        progress
                    );

                // FREE ROPE PHYSICS

                for (
                    int i = 1;
                    i <= last;
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

                    ropePositions[i] =
                        MoveRopeNode(
                            current,
                            desired,
                            ref velocity
                        );

                    previousRopePositions[i] =
                        ropePositions[i] -
                        velocity;
                }

                // PLAYER CENTER

                ropePositions[0] =
                    simulatedPlayer;

                previousRopePositions[0] =
                    simulatedPlayer;

                // CENTER ROTATION SOURCE

                ApplyCenterRotation(
                    simulatedPlayer,
                    rotationSpeed
                );

                // ROTATIONAL ENERGY PROPAGATION

                PropagateRotation(
                    simulatedPlayer
                );

                // CONSTRAINTS

                for (
                    int iteration = 0;
                    iteration <
                    ConstraintIterations;
                    iteration++
                )
                {
                    SolveConstraints(
                        simulatedPlayer
                    );
                }

                // TILE RESPONSE

                ResolveAllTiles(
                    last
                );
            }

            ropePositions[0] =
                playerPosition;
        }

        // RESOLVE ALL TILES

        private void ResolveAllTiles(
            int last)
        {
            /*
             * Direct node penetration still gets removed first.
             */

            for (
                int i = 1;
                i <= last;
                i++
            )
            {
                if (
                    RopeCollides(
                        ropePositions[i]
                    )
                )
                {
                    PushNodeOutOfTiles(
                        i
                    );
                }
            }

            /*
             * Wrap the rope around obstacles.
             *
             * Multiple passes allow the bend to travel down the rope
             * naturally when the rope encounters a large corner.
             */

            for (
                int pass = 0;
                pass < RopeWrapPasses;
                pass++
            )
            {
                for (
                    int i = 0;
                    i < last;
                    i++
                )
                {
                    ResolveRopeSegment(
                        i,
                        i + 1
                    );
                }
            }

            /*
             * Final node check.
             */

            for (
                int i = 1;
                i <= last;
                i++
            )
            {
                if (
                    RopeCollides(
                        ropePositions[i]
                    )
                )
                {
                    PushNodeOutOfTiles(
                        i
                    );
                }
            }
        }

        // RESOLVE ROPE SEGMENT

        private bool ResolveRopeSegment(
            int firstIndex,
            int secondIndex)
        {
            if (
                firstIndex < 0 ||
                secondIndex <= 0 ||
                secondIndex >= RopePointCount
            )
            {
                return false;
            }

            Vector2 parent =
                ropePositions[firstIndex];

            Vector2 child =
                ropePositions[secondIndex];

            Vector2 difference =
                child -
                parent;

            float segmentLength =
                difference.Length();

            if (
                segmentLength <
                0.001f
            )
            {
                return false;
            }

            /*
             * The segment is already safe.
             */

            if (
                !SegmentCollides(
                    parent,
                    child
                ) &&
                !RopeCollides(child)
            )
            {
                return false;
            }

            float currentAngle =
                difference.ToRotation();

            /*
             * Search around the parent for the nearest direction where
             * the SAME-LENGTH segment is completely outside the tile.
             *
             * Because the radius never changes, the rope does not
             * shorten when it bends.
             */

            Vector2 bestPosition =
                Vector2.Zero;

            float bestAngleDifference =
                float.MaxValue;

            bool found =
                false;

            for (
                int step = 1;
                step <= RopeWrapSearchSteps;
                step++
            )
            {
                float angleOffset =
                    MathHelper.ToRadians(
                        step *
                        RopeWrapSearchStepDegrees
                    );

                for (
                    int direction = -1;
                    direction <= 1;
                    direction += 2
                )
                {
                    float testAngle =
                        currentAngle +
                        angleOffset *
                        direction;

                    Vector2 candidate =
                        parent +
                        testAngle.ToRotationVector2() *
                        segmentLength;

                    if (
                        RopeCollides(
                            candidate
                        )
                    )
                    {
                        continue;
                    }

                    if (
                        SegmentCollides(
                            parent,
                            candidate
                        )
                    )
                    {
                        continue;
                    }

                    float angleDifference =
                        Math.Abs(
                            MathHelper.WrapAngle(
                                testAngle -
                                currentAngle
                            )
                        );

                    if (
                        angleDifference <
                        bestAngleDifference
                    )
                    {
                        bestAngleDifference =
                            angleDifference;

                        bestPosition =
                            candidate;

                        found =
                            true;
                    }
                }

                if (found)
                {
                    /*
                     * Since angles are searched from smallest to largest,
                     * this is the closest bend to the original rope path.
                     */

                    break;
                }
            }

            if (!found)
            {
                /*
                 * If the rope is completely trapped in a closed space,
                 * do not randomly shorten the segment.
                 *
                 * Let the normal node collision system handle it.
                 */

                return false;
            }

            /*
             * Move only the child.
             *
             * The distance from parent to child is EXACTLY the same as
             * before the collision.
             */

            ropePositions[secondIndex] =
                bestPosition;

            /*
             * Keep the child's tangential motion so the rope slides
             * around the wall instead of stopping dead.
             */

            Vector2 velocity =
                ropePositions[secondIndex] -
                previousRopePositions[secondIndex];

            Vector2 radial =
                (
                    ropePositions[secondIndex] -
                    parent
                ).SafeNormalize(
                    Vector2.UnitX
                );

            Vector2 tangent =
                radial.RotatedBy(
                    MathHelper.PiOver2
                );

            float tangentSpeed =
                Vector2.Dot(
                    velocity,
                    tangent
                );

            velocity =
                tangent *
                tangentSpeed *
                RopeFriction;

            /*
             * Keep previous position consistent with the new wrapped
             * position so the correction itself does not create a giant
             * artificial velocity.
             */

            previousRopePositions[secondIndex] =
                ropePositions[secondIndex] -
                velocity;

            return true;
        }

        // SEGMENT COLLISION

        private bool SegmentCollides(
            Vector2 start,
            Vector2 end)
        {
            Vector2 difference =
                end -
                start;

            float length =
                difference.Length();

            if (
                length <
                0.001f
            )
            {
                return RopeCollides(
                    start
                );
            }

            int samples =
                Math.Max(
                    2,
                    (int)Math.Ceiling(
                        length /
                        RopeSegmentCollisionSpacing
                    )
                );

            for (
                int sample = 0;
                sample <= samples;
                sample++
            )
            {
                float progress =
                    sample /
                    (float)samples;

                Vector2 position =
                    Vector2.Lerp(
                        start,
                        end,
                        progress
                    );

                if (
                    RopeCollides(
                        position
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }

        // CENTER ROTATION

        private void ApplyCenterRotation(
            Vector2 playerPosition,
            float rotationSpeed)
        {
            if (
                rotationalMomentum == null
            )
            {
                return;
            }

            int count =
                Math.Min(
                    DrivenPointCount,
                    RopePointCount - 1
                );

            Vector2 radial =
                Angle.ToRotationVector2();

            Vector2 tangent =
                radial.RotatedBy(
                    MathHelper.PiOver2 *
                    OrbitDirection
                );

            float desiredSpeed =
                Math.Max(
                    GetEffectiveMinimumCenterSpeed(),
                    rotationSpeed *
                    Distance *
                    0.50f
                );

            desiredSpeed =
                Math.Min(
                    desiredSpeed,
                    MaximumCenterVelocity
                );

            float effectiveCenterTorque =
                GetEffectiveCenterTorque();

            for (
                int i = 1;
                i <= count;
                i++
            )
            {
                float influence =
                    1f -
                    (
                        (i - 1f) /
                        Math.Max(
                            1f,
                            count
                        )
                    );

                influence =
                    MathHelper.Lerp(
                        0.55f,
                        1f,
                        influence
                    );

                rotationalMomentum[i] *=
                    RotationRetention;

                rotationalMomentum[i] +=
                    effectiveCenterTorque *
                    influence;

                Vector2 velocity =
                    ropePositions[i] -
                    previousRopePositions[i];

                float tangentialSpeed =
                    Vector2.Dot(
                        velocity,
                        tangent
                    );

                float desiredTangential =
                    desiredSpeed *
                    influence;

                float difference =
                    desiredTangential -
                    tangentialSpeed;

                velocity +=
                    tangent *
                    difference *
                    CenterSpeedCorrection;

                previousRopePositions[i] =
                    ropePositions[i] -
                    velocity;

                rotationalMomentum[i] =
                    MathHelper.Clamp(
                        rotationalMomentum[i],
                        -MaximumRotationalMomentum,
                        MaximumRotationalMomentum
                    );
            }
        }

        // ROTATION PROPAGATION

        private void PropagateRotation(
            Vector2 playerPosition)
        {
            if (
                rotationalMomentum == null ||
                rotationalMomentum.Length !=
                RopePointCount
            )
            {
                return;
            }

            int last =
                RopePointCount - 1;

            for (
                int i = 1;
                i <= last;
                i++
            )
            {
                Vector2 current =
                    ropePositions[i];

                Vector2 previous =
                    previousRopePositions[i];

                Vector2 velocity =
                    current -
                    previous;

                int parent =
                    Math.Max(
                        0,
                        i - 1
                    );

                Vector2 parentVelocity =
                    ropePositions[parent] -
                    previousRopePositions[parent];

                Vector2 radial =
                    (
                        current -
                        playerPosition
                    ).SafeNormalize(
                        Angle.ToRotationVector2()
                    );

                Vector2 tangent =
                    radial.RotatedBy(
                        MathHelper.PiOver2 *
                        OrbitDirection
                    );

                float parentTangential =
                    Vector2.Dot(
                        parentVelocity,
                        tangent
                    );

                float childTangential =
                    Vector2.Dot(
                        velocity,
                        tangent
                    );

                float tangentialDifference =
                    parentTangential -
                    childTangential;

                tangentialDifference =
                    MathHelper.Clamp(
                        tangentialDifference,
                        -5f,
                        5f
                    );

                bool touchingTile =
                    RopeCollides(
                        current
                    );

                float transfer =
                    touchingTile
                        ? CollisionRotationTransfer
                        : RotationTransfer;

                float parentMomentum =
                    rotationalMomentum[parent];

                float momentumDifference =
                    parentMomentum -
                    rotationalMomentum[i];

                rotationalMomentum[i] +=
                    momentumDifference *
                    transfer;

                rotationalMomentum[i] *=
                    RotationRetention;

                float momentumVelocity =
                    rotationalMomentum[i] *
                    2.5f;

                velocity +=
                    tangent *
                    momentumVelocity;

                velocity +=
                    tangent *
                    tangentialDifference *
                    transfer *
                    0.45f;

                if (
                    Math.Abs(
                        rotationalMomentum[i]
                    ) <
                    MinimumRotationalMomentum
                )
                {
                    rotationalMomentum[i] =
                        parentMomentum *
                        transfer;
                }

                rotationalMomentum[i] =
                    MathHelper.Clamp(
                        rotationalMomentum[i],
                        -MaximumRotationalMomentum,
                        MaximumRotationalMomentum
                    );

                float speed =
                    velocity.Length();

                if (
                    speed >
                    MaximumCenterVelocity
                )
                {
                    velocity =
                        velocity /
                        speed *
                        MaximumCenterVelocity;
                }

                previousRopePositions[i] =
                    current -
                    velocity;
            }
        }

        // CENTER RELEASE

        private void ApplyCenterRelease(
            Vector2 playerPosition)
        {
            if (
                ropePositions == null ||
                previousRopePositions == null
            )
            {
                return;
            }

            Vector2 radial =
                (
                    ropePositions[1] -
                    playerPosition
                ).SafeNormalize(
                    Angle.ToRotationVector2()
                );

            Vector2 tangent =
                radial.RotatedBy(
                    MathHelper.PiOver2 *
                    OrbitDirection
                );

            previousRopePositions[1] -=
                tangent *
                (
                    4f *
                    EffectiveSpinMult()
                );

            if (
                rotationalMomentum != null &&
                rotationalMomentum.Length > 1
            )
            {
                rotationalMomentum[1] +=
                    (
                        0.35f *
                        EffectiveSpinMult()
                    ) *
                    OrbitDirection;
            }
        }

        // ROPE NODE MOVEMENT

        private Vector2 MoveRopeNode(
            Vector2 start,
            Vector2 target,
            ref Vector2 velocity)
        {
            Vector2 movement =
                target -
                start;

            if (
                movement.LengthSquared() <
                0.001f
            )
            {
                if (
                    !RopeCollides(start)
                )
                {
                    return start;
                }

                return FindSafePosition(
                    start
                );
            }

            Vector2 safe =
                start;

            bool collided =
                false;

            for (
                int step = 1;
                step <= RopeSweepSteps;
                step++
            )
            {
                Vector2 test =
                    Vector2.Lerp(
                        start,
                        target,
                        step /
                        (float)RopeSweepSteps
                    );

                if (
                    RopeCollides(test)
                )
                {
                    collided =
                        true;

                    break;
                }

                safe =
                    test;
            }

            if (!collided)
            {
                return safe;
            }

            Vector2 actualMovement =
                safe -
                start;

            Vector2 blockedMovement =
                movement -
                actualMovement;

            Vector2 normal =
                blockedMovement.SafeNormalize(
                    Vector2.UnitY
                );

            float intoSurface =
                Vector2.Dot(
                    velocity,
                    normal
                );

            if (
                intoSurface > 0f
            )
            {
                velocity -=
                    normal *
                    intoSurface;
            }

            Vector2 tangent =
                normal.RotatedBy(
                    MathHelper.PiOver2
                );

            Vector2 tangentVelocity =
                tangent *
                Vector2.Dot(
                    velocity,
                    tangent
                );

            velocity =
                tangentVelocity *
                RopeFriction;

            velocity +=
                normal *
                RopeBounce;

            if (
                RopeCollides(safe)
            )
            {
                safe =
                    FindSafePosition(
                        safe
                    );
            }

            return safe;
        }

        // ROPE COLLISION

        private bool RopeCollides(
            Vector2 position)
        {
            int size =
                Math.Max(
                    2,
                    (int)(
                        RopeRadius * 2f
                    )
                );

            return Collision.SolidCollision(
                position -
                    new Vector2(
                        RopeRadius,
                        RopeRadius
                    ),
                size,
                size
            );
        }

        // PUSH NODE OUT OF TILES

        private void PushNodeOutOfTiles(
            int index)
        {
            if (
                !RopeCollides(
                    ropePositions[index]
                )
            )
            {
                return;
            }

            Vector2 safe =
                FindSafePosition(
                    ropePositions[index]
                );

            ropePositions[index] =
                safe;

            Vector2 velocity =
                safe -
                previousRopePositions[index];

            velocity *=
                0.35f;

            previousRopePositions[index] =
                safe -
                velocity;

            /*
             * ORIGINAL rotational momentum reduction.
             */

            if (
                rotationalMomentum != null
            )
            {
                rotationalMomentum[index] *=
                    0.15f;
            }
        }

        // FIND SAFE POSITION

        private Vector2 FindSafePosition(
            Vector2 position)
        {
            if (
                !RopeCollides(position)
            )
            {
                return position;
            }

            const int AngleSamples = 40;

            for (
                int distance = 1;
                distance <= RopeEscapeDistance;
                distance++
            )
            {
                for (
                    int sample = 0;
                    sample < AngleSamples;
                    sample++
                )
                {
                    float angle =
                        MathHelper.TwoPi *
                        sample /
                        AngleSamples;

                    Vector2 direction =
                        angle.ToRotationVector2();

                    Vector2 test =
                        position +
                        direction *
                        distance;

                    if (
                        !RopeCollides(
                            test
                        )
                    )
                    {
                        return test;
                    }
                }
            }

            return position;
        }

        // CONSTRAINTS

        private void SolveConstraints(
            Vector2 playerPosition)
        {
            int last =
                RopePointCount - 1;

            /*
             * ONLY THE PLAYER IS FIXED.
             *
             * Endpoint remains completely free.
             */

            ropePositions[0] =
                playerPosition;

            for (
                int i = 0;
                i < last;
                i++
            )
            {
                Vector2 first =
                    ropePositions[i];

                Vector2 second =
                    ropePositions[i + 1];

                Vector2 difference =
                    second -
                    first;

                float distance =
                    difference.Length();

                if (
                    distance <
                    0.001f
                )
                {
                    continue;
                }

                float error =
                    distance -
                    ropeSegmentLength;

                Vector2 correction =
                    difference /
                    distance *
                    error;

                if (
                    i == 0
                )
                {
                    /*
                     * Player is fixed.
                     */

                    ropePositions[i + 1] -=
                        correction;
                }
                else
                {
                    /*
                     * Both rope particles remain free.
                     */

                    ropePositions[i] +=
                        correction *
                        0.5f;

                    ropePositions[i + 1] -=
                        correction *
                        0.5f;
                }
            }

            ropePositions[0] =
                playerPosition;
        }

        // DAMAGE

        public override bool? CanDamage()
        {
            return true;
        }

        public override bool? Colliding(
            Rectangle projHitbox,
            Rectangle targetHitbox)
        {
            if (
                Projectile.owner < 0 ||
                Projectile.owner >=
                Main.maxPlayers
            )
            {
                return false;
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
                return false;
            }

            float point = 0f;

            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                player.Center,
                Projectile.Center,
                12f,
                ref point
            );
        }

        // NETWORKING

        public override void SendExtraAI(
            BinaryWriter writer)
        {
            writer.Write(Angle);
            writer.Write(Distance);
            writer.Write(AngularSpeed);
            writer.Write(OrbitDirection);
        }

        public override void ReceiveExtraAI(
            BinaryReader reader)
        {
            Angle =
                reader.ReadSingle();

            Distance =
                reader.ReadSingle();

            AngularSpeed =
                reader.ReadSingle();

            OrbitDirection =
                reader.ReadInt32();

            Projectile.localAI[0] =
                Distance;

            Projectile.localAI[1] =
                AngularSpeed;

            ropeInitialized =
                false;
        }

        // DRAW

        // Direct rope accessor for the draw system (avoids reflection).
        public Vector2[] GetRopePositions()
        {
            return ropePositions;
        }

        public override bool PreDraw(
            ref Color lightColor)
        {
            // Rope rendering lives in Systems/WildStringSystems.PostDraw
            // (nicer lighting blend). Drawing here too would render every
            // segment twice, doubling Lighting.GetColor cost per frame.
            return false;
        }

        // DRAW ROPE

        private void DrawRope()
        {
            int last =
                RopePointCount - 1;

            for (
                int i = 0;
                i < last;
                i++
            )
            {
                Vector2 start =
                    ropePositions[i];

                Vector2 end =
                    ropePositions[i + 1];

                if (
                    Vector2.DistanceSquared(
                        start,
                        end
                    ) <
                    0.25f
                )
                {
                    continue;
                }

                Color color =
                    GetRopeLighting(
                        (
                            start +
                            end
                        ) *
                        0.5f
                    );

                DrawStringSegment(
                    start,
                    end,
                    RopeWidth,
                    color
                );
            }
        }

        // LIGHTING

        private Color GetRopeLighting(
            Vector2 position)
        {
            int x =
                (int)Math.Floor(
                    position.X /
                    16f
                );

            int y =
                (int)Math.Floor(
                    position.Y /
                    16f
                );

            if (
                x < 0 ||
                y < 0 ||
                x >= Main.maxTilesX ||
                y >= Main.maxTilesY
            )
            {
                return StringColor;
            }

            Color light =
                Lighting.GetColor(
                    x,
                    y
                );

            return MultiplyColor(
                StringColor,
                light
            );
        }

        // COLOR MULTIPLICATION

        private static Color MultiplyColor(
            Color baseColor,
            Color lightColor)
        {
            return new Color(
                baseColor.R *
                    lightColor.R /
                    255,

                baseColor.G *
                    lightColor.G /
                    255,

                baseColor.B *
                    lightColor.B /
                    255,

                255
            );
        }

        // DRAW SEGMENT

        private static void DrawStringSegment(
            Vector2 start,
            Vector2 end,
            float width,
            Color color)
        {
            Vector2 difference =
                end -
                start;

            float length =
                difference.Length();

            if (
                length <=
                0.5f
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
                color,
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

        // TEMPORARY PHYSICS RESET

        protected void ResetWildStringPhysics(
            float newDistance,
            float newAngularSpeed,
            float newAngle)
        {
            if (
                Projectile.owner < 0 ||
                Projectile.owner >=
                Main.maxPlayers
            )
            {
                return;
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
                return;
            }

            Distance =
                newDistance;

            AngularSpeed =
                newAngularSpeed;

            Angle =
                newAngle;

            Projectile.localAI[0] =
                newDistance;

            Projectile.localAI[1] =
                AngularSpeed;

            Vector2 direction =
                newAngle.ToRotationVector2();

            Vector2 endpoint =
                player.Center +
                direction *
                newDistance;

            endpoint =
                FindSafePosition(
                    endpoint
                );

            InitializeRope(
                player.Center,
                endpoint,
                direction
            );

            ropeInitialized =
                true;

            int last =
                RopePointCount - 1;

            Projectile.Center =
                ropePositions[last];

            Projectile.velocity =
                ropePositions[last] -
                previousRopePositions[last];

            Projectile.netUpdate =
                true;
        }

        // CHANGE CURRENT SPIN

        protected void SetWildStringSpin(
            float newAngularSpeed)
        {
            AngularSpeed =
                newAngularSpeed;

            Projectile.localAI[1] =
                AngularSpeed;

            Projectile.netUpdate =
                true;
        }
    }
}