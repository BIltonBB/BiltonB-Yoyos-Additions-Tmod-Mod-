using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items.Accessories;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Systems
{
    public class WalkTheDogMinigame :
        YoyoTrickMinigame
    {
        // SETTINGS

        // The player has exactly 2 seconds.
        private const int Duration =
            2 * 60;

        // Individual dust particles along the guide.
        private const float DustSpacing =
            8f;

        // Distance required to trace an individual particle.
        private const float TraceRadius =
            18f;

        // 85% of the dust must be traced.
        private const float RequiredProgress =
            0.85f;

        // STATE

        private int timer;

        private Vector2 lineCenter;

        private Vector2[] TracePositions;

        private bool[] TraceCompleted;

        private int TraceCount;

        private int TracedCount;

        // LINE

        // Fixed endpoints.
        //
        // These do NOT follow the yoyo.
        public Vector2 LineStart { get; private set; }

        public Vector2 LineEnd { get; private set; }

        // ENDPOINT RINGS

        private const float RingMinimum =
            4f;

        private const float RingMaximum =
            9f;

        private const float RingSpeed =
            0.12f;

        // START

        public override void Start(
            Player player,
            YoyoTrickItem trick
        )
        {
            Finished =
                false;

            Success =
                false;

            timer =
                0;

            TracedCount =
                0;

            // GET YOYO

            Projectile yoyo =
                YoyoTrickSystem.GetHeldYoyo(
                    player
                );

            if (yoyo == null)
            {
                Finished =
                    true;

                Success =
                    false;

                return;
            }

            // CREATE FIXED LINE

            lineCenter =
                yoyo.Center;

            LineStart =
                lineCenter +
                new Vector2(
                    -100f,
                    0f
                );

            LineEnd =
                lineCenter +
                new Vector2(
                    100f,
                    0f
                );

            BuildTraceParticles();
        }

        // UPDATE

        public override void Update(
            Player player
        )
        {
            if (Finished)
                return;

            timer++;

            // GET MAIN YOYO

            Projectile yoyo =
                YoyoTrickSystem.GetHeldYoyo(
                    player
                );

            if (
                yoyo == null ||
                !yoyo.active
            )
            {
                Finished =
                    true;

                Success =
                    false;

                return;
            }

            // DRAW DUST GUIDE

            DrawGuide();

            // TRACE

            TraceWithYoyo(
                yoyo
            );

            // SUCCESS

            if (
                TracedCount >=
                TraceCount *
                RequiredProgress
            )
            {
                Finished =
                    true;

                Success =
                    true;

                return;
            }

            // TIME LIMIT

            if (timer >= Duration)
            {
                Finished =
                    true;

                Success =
                    TracedCount >=
                    TraceCount *
                    RequiredProgress;

                return;
            }
        }

        // BUILD TRACE PARTICLES

        private void BuildTraceParticles()
        {
            float length =
                Vector2.Distance(
                    LineStart,
                    LineEnd
                );

            int capacity =
                (int)(
                    length /
                    DustSpacing
                ) + 2;

            TracePositions =
                new Vector2[
                    capacity
                ];

            TraceCompleted =
                new bool[
                    capacity
                ];

            TraceCount =
                0;

            for (
                float distance = 0f;
                distance <= length;
                distance += DustSpacing
            )
            {
                if (
                    TraceCount >=
                    TracePositions.Length
                )
                {
                    break;
                }

                float progress =
                    MathHelper.Clamp(
                        distance / length,
                        0f,
                        1f
                    );

                TracePositions[
                    TraceCount
                ] =
                    Vector2.Lerp(
                        LineStart,
                        LineEnd,
                        progress
                    );

                TraceCompleted[
                    TraceCount
                ] =
                    false;

                TraceCount++;
            }
        }

        // TRACE WITH YOYO

        private void TraceWithYoyo(
            Projectile yoyo
        )
        {
            for (
                int i = 0;
                i < TraceCount;
                i++
            )
            {
                if (TraceCompleted[i])
                    continue;

                float distance =
                    Vector2.Distance(
                        yoyo.Center,
                        TracePositions[i]
                    );

                if (
                    distance >
                    TraceRadius
                )
                {
                    continue;
                }

                TraceCompleted[i] =
                    true;

                TracedCount++;
            }
        }

        // DRAW GUIDE

        private void DrawGuide()
        {
            if (
                TracePositions == null ||
                TraceCompleted == null
            )
            {
                return;
            }

            // DUST LINE

            for (
                int i = 0;
                i < TraceCount;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        TracePositions[i],
                        DustID.YellowTorch,
                        Vector2.Zero
                    );

                dust.noGravity =
                    true;

                dust.velocity =
                    Vector2.Zero;

                dust.scale =
                    0.72f;

                // Traced individual dust =
                // 50% transparent.
                dust.alpha =
                    TraceCompleted[i]
                        ? 128
                        : 0;
            }

            // GOLDEN ENDPOINT RINGS

            DrawCornerRing(
                LineStart,
                0
            );

            DrawCornerRing(
                LineEnd,
                1
            );
        }

        // ENDPOINT RING

        private void DrawCornerRing(
            Vector2 center,
            int index
        )
        {
            float pulse =
                (
                    (float)System.Math.Sin(
                        timer *
                        RingSpeed +
                        index *
                        1.7f
                    ) +
                    1f
                ) *
                0.5f;

            float radius =
                MathHelper.Lerp(
                    RingMinimum,
                    RingMaximum,
                    pulse
                );

            const int RingParticles =
                8;

            for (
                int i = 0;
                i < RingParticles;
                i++
            )
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    RingParticles;

                Vector2 position =
                    center +
                    angle.ToRotationVector2() *
                    radius;

                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.GoldFlame,
                        Vector2.Zero
                    );

                dust.noGravity =
                    true;

                dust.velocity =
                    Vector2.Zero;

                dust.scale =
                    0.65f;
            }
        }
    }
}