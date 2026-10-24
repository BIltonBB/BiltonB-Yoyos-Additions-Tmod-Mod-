using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class WildStringLinkProj : ModProjectile
    {
        // NPC DATA

        private int FirstIndex =>
            (int)Projectile.ai[0];

        private int SecondIndex =>
            (int)Projectile.ai[1];

        private static NPC GetNPC(int index)
        {
            if (
                index < 0 ||
                index >= Main.maxNPCs
            )
            {
                return null;
            }

            NPC npc =
                Main.npc[index];

            if (
                npc == null ||
                !npc.active ||
                npc.friendly
            )
            {
                return null;
            }

            return npc;
        }

        // ROPE SETTINGS

        private const int RopePointCount = 40;

        private const int PhysicsSubsteps = 3;

        private const int ConstraintIterations = 6;

        private const float RopeGravity = 0.30f;

        private const float RopeDamping = 0.988f;

        private const float RopeFriction = 0.84f;

        private const float RopeBounce = 0.05f;

        private const float RopeRadius = 2f;

        private const float RopeWidth = 1.2f;

        private const int RopeSweepSteps = 10;

        private const int RopeEscapeDistance = 24;

        /*
         * Hard safety cap for the rope particles.
         */
        private const float MaximumRopeVelocity = 16f;

        /*
         * Prevents the constraint solver from making one huge
         * correction when an NPC suddenly teleports or is knocked
         * back.
         */
        private const float MaximumConstraintCorrection = 5f;

        // LINK LENGTH

        /*
         * The link's maximum distance is captured ONCE:
         *
         * original distance + 12 pixels
         */

        private const float ExtraLength = 12f;

        private float allowedDistance;

        // NPC PULL

        private const float PullStrength = 0.10f;

        private const float MaximumPull = 6f;

        /*
         * Extra safety cap for actual NPC velocity.
         */
        private const float MaximumNPCVelocity = 12f;

        // ROPE STATE

        private Vector2[] ropePositions;

        private Vector2[] previousRopePositions;

        private float ropeSegmentLength;

        private bool ropeInitialized;

        // DEAD END STATE

        private bool firstDead;

        private bool secondDead;

        private Vector2 firstLoosePosition;

        private Vector2 secondLoosePosition;

        private Vector2 firstLooseVelocity;

        private Vector2 secondLooseVelocity;

        // NPC HISTORY

        private Vector2 previousFirstPosition;

        private Vector2 previousSecondPosition;

        // DEFAULTS

        public override string Texture =>
            "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;

            /*
             * This projectile itself does not damage NPCs or interact
             * with projectiles.
             */
            Projectile.friendly = false;
            Projectile.hostile = false;

            /*
             * IMPORTANT:
             *
             * Tile collision belongs to the rope nodes.
             *
             * Terraria itself must NOT collide the projectile.
             */
            Projectile.tileCollide = false;

            Projectile.ignoreWater = true;

            Projectile.penetrate = -1;

            Projectile.timeLeft =
                10 * 60;

            /*
             * Needed for DrawBehind().
             */
            Projectile.hide = true;
        }

        // AI

        public override void AI()
        {
            NPC first =
                GetNPC(FirstIndex);

            NPC second =
                GetNPC(SecondIndex);

            bool firstAlive =
                first != null;

            bool secondAlive =
                second != null;

            // INITIALIZATION

            if (!ropeInitialized)
            {
                Vector2 firstStart =
                    firstAlive
                        ? first.Center
                        : Projectile.Center;

                Vector2 secondStart =
                    secondAlive
                        ? second.Center
                        : Projectile.Center;

                /*
                 * Capture the link length exactly once.
                 */
                float initialDistance =
                    Vector2.Distance(
                        firstStart,
                        secondStart);

                allowedDistance =
                    Math.Max(
                        8f,
                        initialDistance +
                        ExtraLength);

                InitializeRope(
                    firstStart,
                    secondStart);

                previousFirstPosition =
                    firstStart;

                previousSecondPosition =
                    secondStart;

                ropeInitialized =
                    true;
            }

            // BOSS CHECK

            /*
             * Wild String Link never affects bosses.
             *
             * The link simply disappears instead.
             */

            if (
                (firstAlive && first.boss) ||
                (secondAlive && second.boss)
            )
            {
                Projectile.Kill();
                return;
            }

            // DETECT FIRST DEATH

            if (
                !firstAlive &&
                !firstDead
            )
            {
                firstDead = true;

                firstLoosePosition =
                    ropePositions[0];

                firstLooseVelocity =
                    previousFirstPosition -
                    firstLoosePosition;

                firstLooseVelocity =
                    ClampVelocity(
                        firstLooseVelocity);
            }

            // DETECT SECOND DEATH

            if (
                !secondAlive &&
                !secondDead
            )
            {
                secondDead = true;

                int last =
                    RopePointCount - 1;

                secondLoosePosition =
                    ropePositions[last];

                secondLooseVelocity =
                    previousSecondPosition -
                    secondLoosePosition;

                secondLooseVelocity =
                    ClampVelocity(
                        secondLooseVelocity);
            }

            // BOTH TARGETS GONE

            if (
                firstDead &&
                secondDead
            )
            {
                Projectile.Kill();
                return;
            }

            // ENDPOINT POSITIONS

            Vector2 firstPosition;

            Vector2 secondPosition;

            // FIRST END

            if (firstAlive)
            {
                /*
                 * ONLY READ the NPC.
                 *
                 * Never modify its position or hitbox.
                 */

                firstPosition =
                    first.Center;

                firstLoosePosition =
                    firstPosition;

                firstLooseVelocity =
                    ClampVelocity(
                        first.velocity);
            }
            else
            {
                firstPosition =
                    UpdateLooseEnd(
                        ref firstLoosePosition,
                        ref firstLooseVelocity);
            }

            // SECOND END

            if (secondAlive)
            {
                /*
                 * ONLY READ the NPC.
                 */

                secondPosition =
                    second.Center;

                secondLoosePosition =
                    secondPosition;

                secondLooseVelocity =
                    ClampVelocity(
                        second.velocity);
            }
            else
            {
                secondPosition =
                    UpdateLooseEnd(
                        ref secondLoosePosition,
                        ref secondLooseVelocity);
            }

            // NPC PULL

            /*
             * Pulling is completely disabled if either target is dead.
             *
             * Therefore a dead endpoint can never pull a live NPC.
             */

            if (
                firstAlive &&
                secondAlive &&
                Main.netMode !=
                NetmodeID.MultiplayerClient
            )
            {
                ApplyLinkPull(
                    first,
                    second);
            }

            // ROPE PHYSICS

            SimulateRope(
                firstPosition,
                secondPosition);

            // PROJECTILE POSITION

            Vector2 center =
                (
                    firstPosition +
                    secondPosition
                ) * 0.5f;

            Projectile.velocity =
                ClampVelocity(
                    center -
                    (
                        previousFirstPosition +
                        previousSecondPosition
                    ) * 0.5f);

            Projectile.Center =
                center;

            // HISTORY

            previousFirstPosition =
                firstPosition;

            previousSecondPosition =
                secondPosition;
        }

        // NPC IMMOBILITY CHECK

        private static bool IsImmobileNPC(
            NPC npc)
        {
            if (
                npc == null ||
                !npc.active
            )
            {
                return true;
            }

            /*
             * Target Dummies must NEVER be moved.
             */
            if (
                npc.type ==
                NPCID.TargetDummy
            )
            {
                return true;
            }

            /*
             * Conservative protection for stationary,
             * knockback-immune NPCs.
             */
            if (
                npc.aiStyle == NPCAIStyleID.FaceClosestPlayer &&
                npc.knockBackResist <= 0f &&
                npc.velocity.LengthSquared() <
                0.0001f
            )
            {
                return true;
            }

            return false;
        }

        // NPC PULL

        private void ApplyLinkPull(
            NPC first,
            NPC second)
        {
            /*
             * Never touch boss NPCs.
             */
            if (
                first.boss ||
                second.boss
            )
            {
                return;
            }

            bool firstImmobile =
                IsImmobileNPC(first);

            bool secondImmobile =
                IsImmobileNPC(second);

            /*
             * Neither target can move.
             *
             * Most importantly, no Target Dummy velocity is changed.
             */
            if (
                firstImmobile &&
                secondImmobile
            )
            {
                return;
            }

            Vector2 difference =
                second.Center -
                first.Center;

            float distance =
                difference.Length();

            if (
                distance <=
                allowedDistance ||
                distance <=
                0.001f
            )
            {
                return;
            }

            Vector2 direction =
                difference /
                distance;

            float excess =
                distance -
                allowedDistance;

            float pull =
                MathHelper.Clamp(
                    excess *
                    PullStrength,
                    0f,
                    MaximumPull);

            // FIRST NPC

            if (!firstImmobile)
            {
                first.velocity +=
                    direction *
                    pull;

                first.velocity =
                    ClampNPCVelocity(
                        first.velocity);

                first.netUpdate =
                    true;
            }

            // SECOND NPC

            if (!secondImmobile)
            {
                second.velocity -=
                    direction *
                    pull;

                second.velocity =
                    ClampNPCVelocity(
                        second.velocity);

                second.netUpdate =
                    true;
            }
        }

        // INITIALIZE ROPE

        private void InitializeRope(
            Vector2 first,
            Vector2 second)
        {
            ropePositions =
                new Vector2[
                    RopePointCount];

            previousRopePositions =
                new Vector2[
                    RopePointCount];

            float distance =
                Math.Max(
                    8f,
                    Vector2.Distance(
                        first,
                        second));

            ropeSegmentLength =
                distance /
                (
                    RopePointCount - 1);

            for (
                int i = 0;
                i < RopePointCount;
                i++
            )
            {
                float progress =
                    i /
                    (float)(
                        RopePointCount - 1);

                Vector2 position =
                    Vector2.Lerp(
                        first,
                        second,
                        progress);

                ropePositions[i] =
                    position;

                previousRopePositions[i] =
                    position;
            }
        }

        // ROPE SIMULATION

        private void SimulateRope(
            Vector2 first,
            Vector2 second)
        {
            int last =
                RopePointCount - 1;

            /*
             * NPC positions are the authoritative endpoints.
             */
            ropePositions[0] =
                first;

            ropePositions[last] =
                second;

            for (
                int substep = 0;
                substep < PhysicsSubsteps;
                substep++
            )
            {
                float gravity =
                    RopeGravity /
                    PhysicsSubsteps;

                // DYNAMIC ROPE PARTICLES

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

                    velocity =
                        ClampVelocity(
                            velocity);

                    previousRopePositions[i] =
                        current;

                    Vector2 target =
                        current +
                        velocity;

                    ropePositions[i] =
                        MoveRopeNode(
                            current,
                            target,
                            ref velocity);

                    velocity =
                        ClampVelocity(
                            velocity);

                    previousRopePositions[i] =
                        ropePositions[i] -
                        velocity;
                }

                // LENGTH CONSTRAINTS

                for (
                    int iteration = 0;
                    iteration <
                    ConstraintIterations;
                    iteration++
                )
                {
                    SolveConstraints(
                        first,
                        second);
                }

                // TILE EXTRACTION

                for (
                    int i = 1;
                    i < last;
                    i++
                )
                {
                    if (
                        RopeNodeCollides(
                            ropePositions[i])
                    )
                    {
                        PushNodeOutOfTiles(i);
                    }
                }
            }

            /*
             * The NPCs remain the final anchors.
             */
            ropePositions[0] =
                first;

            ropePositions[last] =
                second;
        }

        // ROPE NODE MOVEMENT

        private Vector2 MoveRopeNode(
            Vector2 start,
            Vector2 target,
            ref Vector2 velocity)
        {
            velocity =
                ClampVelocity(
                    velocity);

            if (
                !RopeNodeCollides(target)
            )
            {
                return target;
            }

            Vector2 safe =
                start;

            // SWEPT MOVEMENT

            for (
                int step = 1;
                step <= RopeSweepSteps;
                step++
            )
            {
                float progress =
                    step /
                    (float)RopeSweepSteps;

                Vector2 test =
                    Vector2.Lerp(
                        start,
                        target,
                        progress);

                if (
                    RopeNodeCollides(test)
                )
                {
                    break;
                }

                safe =
                    test;
            }

            // REMOVE INTO-TILE VELOCITY

            Vector2 movement =
                target -
                start;

            Vector2 actual =
                safe -
                start;

            Vector2 blocked =
                movement -
                actual;

            if (
                blocked.LengthSquared() >
                0.001f
            )
            {
                Vector2 normal =
                    blocked.SafeNormalize(
                        Vector2.UnitY);

                float intoSurface =
                    Vector2.Dot(
                        velocity,
                        normal);

                if (
                    intoSurface > 0f
                )
                {
                    velocity -=
                        normal *
                        intoSurface;
                }

                Vector2 tangent =
                    velocity -
                    normal *
                    Vector2.Dot(
                        velocity,
                        normal);

                velocity =
                    tangent *
                    RopeFriction;

                velocity +=
                    normal *
                    RopeBounce;
            }

            return ClampPositionIfNeeded(
                safe);
        }

        // ROPE CONSTRAINTS

        private void SolveConstraints(
            Vector2 first,
            Vector2 second)
        {
            int last =
                RopePointCount - 1;

            /*
             * These are only rope anchors.
             *
             * They NEVER modify NPC positions.
             */

            ropePositions[0] =
                first;

            ropePositions[last] =
                second;

            for (
                int i = 0;
                i < last;
                i++
            )
            {
                Vector2 a =
                    ropePositions[i];

                Vector2 b =
                    ropePositions[i + 1];

                Vector2 difference =
                    b -
                    a;

                float distance =
                    difference.Length();

                if (
                    distance <=
                    0.001f
                )
                {
                    continue;
                }

                float error =
                    distance -
                    ropeSegmentLength;

                /*
                 * Only remove stretching.
                 *
                 * Compression/slack is allowed.
                 */
                if (
                    error <=
                    0f
                )
                {
                    continue;
                }

                Vector2 correction =
                    difference /
                    distance *
                    error;

                float correctionLength =
                    correction.Length();

                if (
                    correctionLength >
                    MaximumConstraintCorrection
                )
                {
                    correction =
                        correction /
                        correctionLength *
                        MaximumConstraintCorrection;
                }

                bool firstAnchor =
                    i == 0;

                bool secondAnchor =
                    i + 1 == last;

                if (
                    firstAnchor
                )
                {
                    ropePositions[i + 1] -=
                        correction;
                }
                else if (
                    secondAnchor
                )
                {
                    ropePositions[i] +=
                        correction;
                }
                else
                {
                    ropePositions[i] +=
                        correction *
                        0.5f;

                    ropePositions[i + 1] -=
                        correction *
                        0.5f;
                }
            }

            ropePositions[0] =
                first;

            ropePositions[last] =
                second;
        }

        // LOOSE DEAD END

        private Vector2 UpdateLooseEnd(
            ref Vector2 position,
            ref Vector2 velocity)
        {
            velocity.Y +=
                RopeGravity;

            velocity *=
                RopeDamping;

            velocity =
                ClampVelocity(
                    velocity);

            Vector2 target =
                position +
                velocity;

            position =
                MoveLooseEnd(
                    position,
                    target,
                    ref velocity);

            return position;
        }

        // LOOSE END TILE COLLISION

        private Vector2 MoveLooseEnd(
            Vector2 start,
            Vector2 target,
            ref Vector2 velocity)
        {
            if (
                !RopeNodeCollides(target)
            )
            {
                return target;
            }

            Vector2 safe =
                start;

            for (
                int step = 1;
                step <= RopeSweepSteps;
                step++
            )
            {
                float progress =
                    step /
                    (float)RopeSweepSteps;

                Vector2 test =
                    Vector2.Lerp(
                        start,
                        target,
                        progress);

                if (
                    RopeNodeCollides(test)
                )
                {
                    break;
                }

                safe =
                    test;
            }

            Vector2 movement =
                target -
                start;

            Vector2 actual =
                safe -
                start;

            Vector2 blocked =
                movement -
                actual;

            if (
                blocked.LengthSquared() >
                0.001f
            )
            {
                Vector2 normal =
                    blocked.SafeNormalize(
                        Vector2.UnitY);

                float intoSurface =
                    Vector2.Dot(
                        velocity,
                        normal);

                if (
                    intoSurface > 0f
                )
                {
                    velocity -=
                        normal *
                        intoSurface;
                }

                velocity *=
                    RopeFriction;
            }

            return ClampPositionIfNeeded(
                safe);
        }

        // TILE COLLISION

        private bool RopeNodeCollides(
            Vector2 position)
        {
            int size =
                Math.Max(
                    2,
                    (int)(
                        RopeRadius *
                        2f));

            return Collision.SolidCollision(
                position -
                    new Vector2(
                        RopeRadius,
                        RopeRadius),
                size,
                size);
        }

        // PUSH NODE OUT

        private void PushNodeOutOfTiles(
            int index)
        {
            if (
                !RopeNodeCollides(
                    ropePositions[index])
            )
            {
                return;
            }

            Vector2 safe =
                FindSafeRopePosition(
                    ropePositions[index]);

            ropePositions[index] =
                safe;

            Vector2 velocity =
                safe -
                previousRopePositions[index];

            velocity *=
                0.25f;

            velocity =
                ClampVelocity(
                    velocity);

            previousRopePositions[index] =
                safe -
                velocity;
        }

        // FIND SAFE ROPE POSITION

        private Vector2 FindSafeRopePosition(
            Vector2 position)
        {
            if (
                !RopeNodeCollides(position)
            )
            {
                return position;
            }

            const int AngleSamples = 16;

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
                        !RopeNodeCollides(test)
                    )
                    {
                        return test;
                    }
                }
            }

            return position;
        }

        // POSITION SAFETY

        private Vector2 ClampPositionIfNeeded(
            Vector2 position)
        {
            if (
                RopeNodeCollides(position)
            )
            {
                return FindSafeRopePosition(
                    position);
            }

            return position;
        }

        // VELOCITY CAP

        private static Vector2 ClampVelocity(
            Vector2 velocity)
        {
            float speed =
                velocity.Length();

            if (
                speed >
                MaximumRopeVelocity
            )
            {
                velocity =
                    velocity /
                    speed *
                    MaximumRopeVelocity;
            }

            return velocity;
        }

        private static Vector2 ClampNPCVelocity(
            Vector2 velocity)
        {
            float speed =
                velocity.Length();

            if (
                speed >
                MaximumNPCVelocity
            )
            {
                velocity =
                    velocity /
                    speed *
                    MaximumNPCVelocity;
            }

            return velocity;
        }

        // DRAW BEHIND NPCS

        public override void DrawBehind(
            int index,
            List<int> behindNPCsAndTiles,
            List<int> behindNPCs,
            List<int> behindProjectiles,
            List<int> overPlayers,
            List<int> overWiresUI)
        {
            /*
             * Draw the rope behind NPCs.
             *
             * This uses the current tModLoader DrawBehind signature.
             */

            behindNPCs.Add(index);
        }

        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            if (
                ropePositions == null ||
                ropePositions.Length < 2
            )
            {
                return false;
            }

            DrawRope();

            /*
             * The actual projectile sprite is never drawn.
             */
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

                Vector2 difference =
                    end -
                    start;

                float length =
                    difference.Length();

                /*
                 * Don't make a segment disappear just because the
                 * two nodes are very close together.
                 */
                if (
                    length <=
                    0.01f
                )
                {
                    continue;
                }

                DrawStringSegment(
                    start,
                    end,
                    RopeWidth,
                    GetLighting(
                        (
                            start +
                            end
                        ) *
                        0.5f
                    )
                );
            }
        }

        // LIGHTING

        private static Color GetLighting(
            Vector2 position)
        {
            int x =
                (int)(
                    position.X /
                    16f
                );

            int y =
                (int)(
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
                return Color.White;
            }

            return Lighting.GetColor(
                x,
                y
            );
        }

        // DRAW SEGMENT

        private static void DrawStringSegment(
            Vector2 start,
            Vector2 end,
            float width,
            Color color
        )
        {
            Vector2 difference =
                end -
                start;

            float length =
                difference.Length();

            if (
                length <=
                0.01f
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
    }
}