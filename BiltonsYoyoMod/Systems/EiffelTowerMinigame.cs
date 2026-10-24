using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items.Accessories;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Systems
{
    public class EiffelTowerMinigame : YoyoTrickMinigame
    {
        // SETTINGS

        // The player has 10 seconds to trace the triangle.
        private const int Duration =
            10 * 60;

        // Yoyo distance required to trace a particle.
        private const float TraceRadius =
            18f;

        // Distance between individual dust particles.
        private const float DustSpacing =
            10f;

        // STATE

        private int timer;

        private int tracedCount;

        // FIXED TRIANGLE
        //
        // This triangle belongs to the MINIGAME.
        //
        // It is created once when the minigame begins.
        // It does NOT follow the yoyo.
        // It does NOT rotate.

        public Vector2 Top { get; private set; }

        public Vector2 Left { get; private set; }

        public Vector2 Right { get; private set; }

        // TRACE PARTICLES

        private Vector2[] TracePositions;

        private bool[] TraceCompleted;

        private int TraceCount;

        // START

        public override void Start(
            Player player,
            YoyoTrickItem trick
        )
        {
            Finished = false;
            Success = false;

            timer = 0;
            tracedCount = 0;

            // CREATE STATIONARY TRIANGLE

            Vector2 center =
                player.Center +
                new Vector2(
                    0f,
                    -20f
                );

            Top =
                center +
                new Vector2(
                    0f,
                    -65f
                );

            Left =
                center +
                new Vector2(
                    -65f,
                    40f
                );

            Right =
                center +
                new Vector2(
                    65f,
                    40f
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

            // GET ACTUAL MAIN YOYO

            Projectile yoyo =
                YoyoTrickSystem.GetHeldYoyo(
                    player
                );

            if (yoyo == null ||
                !yoyo.active)
            {
                Finished = true;
                Success = false;
                return;
            }

            // DRAW THE FIXED DUST TRIANGLE

            DrawIndicator();

            // TRACE WITH YOYO

            TraceWithYoyo(
                yoyo
            );

            // SUCCESS

            if (tracedCount >=
                TraceCount)
            {
                Finished = true;
                Success = true;
                return;
            }

            // TIMEOUT

            if (timer >= Duration)
            {
                Finished = true;
                Success = false;
            }
        }

        // BUILD TRACE PARTICLES

        private void BuildTraceParticles()
        {
            float sideLength =
                Vector2.Distance(
                    Top,
                    Left
                );

            int pointsPerSide =
                (int)(
                    sideLength /
                    DustSpacing
                ) + 2;

            int capacity =
                pointsPerSide * 3;

            TracePositions =
                new Vector2[
                    capacity
                ];

            TraceCompleted =
                new bool[
                    capacity
                ];

            TraceCount = 0;

            AddEdge(
                Top,
                Left
            );

            AddEdge(
                Left,
                Right
            );

            AddEdge(
                Right,
                Top
            );
        }

        // ADD EDGE PARTICLES

        private void AddEdge(
            Vector2 start,
            Vector2 end
        )
        {
            Vector2 difference =
                end - start;

            float length =
                difference.Length();

            if (length <= 0.01f)
                return;

            for (float distance = 0f;
                 distance <= length;
                 distance += DustSpacing)
            {
                if (TraceCount >=
                    TracePositions.Length)
                {
                    return;
                }

                float progress =
                    MathHelper.Clamp(
                        distance /
                        length,
                        0f,
                        1f
                    );

                TracePositions[
                    TraceCount
                ] =
                    Vector2.Lerp(
                        start,
                        end,
                        progress
                    );

                TraceCompleted[
                    TraceCount
                ] = false;

                TraceCount++;
            }
        }

        // TRACE

        private void TraceWithYoyo(
            Projectile yoyo
        )
        {
            for (int i = 0;
                 i < TraceCount;
                 i++)
            {
                if (TraceCompleted[i])
                    continue;

                float distance =
                    Vector2.Distance(
                        yoyo.Center,
                        TracePositions[i]
                    );

                if (distance >
                    TraceRadius)
                {
                    continue;
                }

                TraceCompleted[i] =
                    true;

                tracedCount++;

                // Small feedback at the traced point.
                Dust dust =
                    Dust.NewDustPerfect(
                        TracePositions[i],
                        DustID.YellowTorch,
                        Vector2.Zero
                    );

                dust.noGravity = true;
                dust.velocity = Vector2.Zero;
                dust.alpha = 0;
                dust.scale = 0.8f;
            }
        }

        // DRAW INDICATOR

        private void DrawIndicator()
        {
            for (int i = 0;
                 i < TraceCount;
                 i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        TracePositions[i],
                        DustID.WhiteTorch,
                        Vector2.Zero
                    );

                dust.noGravity =
                    true;

                dust.velocity =
                    Vector2.Zero;

                dust.scale =
                    0.7f;

                // TRACED PARTICLES
                //
                // 128 alpha = 50% transparent.
                //
                // This is applied every frame so the traced state
                // actually persists visually.

                dust.alpha =
                    TraceCompleted[i]
                        ? 128
                        : 0;
            }

            // CORNERS

            DrawCorner(Top);
            DrawCorner(Left);
            DrawCorner(Right);
        }

        // CORNER DUST

        private void DrawCorner(
            Vector2 position
        )
        {
            Dust dust =
                Dust.NewDustPerfect(
                    position,
                    DustID.YellowTorch,
                    Vector2.Zero
                );

            dust.noGravity =
                true;

            dust.velocity =
                Vector2.Zero;

            dust.scale =
                0.9f;
        }
    }
}