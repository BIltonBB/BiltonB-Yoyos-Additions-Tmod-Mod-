using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using BiltonsYoyoMod.Items.Accessories;

namespace BiltonsYoyoMod.Systems
{
    public class BoingyBoingMinigame : YoyoTrickMinigame
    {
        // SETTINGS

        private const int RequiredHits = 3;

        // 3 seconds to complete all 3 movements.
        private const int Duration = 180;

        // The yoyo only needs to get reasonably close to the target.
        // This is intentionally forgiving so the minigame registers
        // the yoyo reliably.
        private const float TargetRadius = 45f;

        // Visual size of the target ring.
        private const float TargetVisualRadius = 24f;

        // STATE

        private int hitCount;
        private int timer;

        public Vector2 Target { get; private set; }

        // START

        public override void Start(
            Player player,
            YoyoTrickItem trick)
        {
            Finished = false;
            Success = false;

            hitCount = 0;
            timer = 0;

            // Verify that a yoyo is actually out.
            Projectile yoyo =
                YoyoTrickSystem.GetHeldYoyo(player);

            if (yoyo == null)
            {
                Main.NewText(
                    "BOINGY BOING FAILED: NO HELD YOYO",
                    Color.Red
                );

                Finished = true;
                Success = false;
                return;
            }

            CreateTarget(player);

            Main.NewText(
                "BOINGY BOING STARTED",
                Color.Cyan
            );

            Main.NewText(
                "MOVE THE YOYO THROUGH 3 TARGETS",
                Color.Yellow
            );
        }

        // CREATE TARGET

        private void CreateTarget(
            Player player)
        {
            // Required movement sequence:
            // 1 = up
            // 2 = down
            // 3 = up
            bool up =
                hitCount % 2 == 0;

            Target =
                player.Center +
                new Vector2(
                    Main.rand.Next(-65, 66),
                    up ? -70f : 45f
                );

            Main.NewText(
                "BOINGY TARGET " +
                (hitCount + 1) +
                "/3 CREATED",
                Color.Yellow
            );
        }

        // UPDATE

        public override void Update(
            Player player)
        {
            if (Finished)
                return;

            timer++;

            // TIMEOUT

            if (timer >= Duration)
            {
                Main.NewText(
                    "BOINGY BOING TIMED OUT - TRICK FAILED",
                    Color.Red
                );

                Finished = true;
                Success = false;
                return;
            }

            // GET ACTUAL HELD YOYO

            Projectile yoyo =
                YoyoTrickSystem.GetHeldYoyo(player);

            if (yoyo == null ||
                !yoyo.active)
            {
                // Keep the target alive instead of instantly failing
                // because the yoyo lookup was unavailable this frame.
                DrawTarget();
                return;
            }

            // TARGET VISUAL

            DrawTarget();

            // YOYO POSITION CHECK
            //
            // IMPORTANT:
            //
            // This uses the actual yoyo projectile's CENTER.
            //
            // The TARGET stays fixed.
            // The target does NOT follow the yoyo.
            //
            // 45 pixels is intentionally forgiving.

            float distance =
                Vector2.Distance(
                    yoyo.Center,
                    Target
                );

            if (distance <= TargetRadius)
            {
                hitCount++;

                Main.NewText(
                    "BOINGY BOUNCE " +
                    hitCount +
                    "/3!",
                    Color.Lime
                );

                // HIT EFFECT

                for (int i = 0;
                     i < 12;
                     i++)
                {
                    Dust dust =
                        Dust.NewDustPerfect(
                            yoyo.Center,
                            DustID.GoldFlame,
                            Main.rand.NextVector2Circular(
                                2.5f,
                                2.5f
                            )
                        );

                    dust.noGravity = true;
                    dust.scale =
                        Main.rand.NextFloat(
                            0.8f,
                            1.1f
                        );
                }

                // SUCCESS

                if (hitCount >= RequiredHits)
                {
                    Main.NewText(
                        "BOINGY BOING SUCCESS",
                        Color.Lime
                    );

                    Finished = true;
                    Success = true;
                    return;
                }

                // NEXT TARGET

                CreateTarget(player);
            }
        }

        // TARGET DRAWING

        private void DrawTarget()
        {
            float pulse =
                1f +
                0.08f *
                (float)System.Math.Sin(
                    timer * 0.18f
                );

            float radius =
                TargetVisualRadius *
                pulse;

            // YELLOW RING

            for (int i = 0;
                 i < 18;
                 i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    18f;

                Vector2 direction =
                    new Vector2(
                        (float)System.Math.Cos(angle),
                        (float)System.Math.Sin(angle)
                    );

                Vector2 position =
                    Target +
                    direction * radius;

                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.GoldFlame,
                        Vector2.Zero
                    );

                dust.noGravity = true;
                dust.velocity = Vector2.Zero;
                dust.scale = 0.9f;
            }

            // CENTER MARKER

            if (timer % 3 == 0)
            {
                Dust centerDust =
                    Dust.NewDustPerfect(
                        Target,
                        DustID.GoldFlame,
                        Vector2.Zero
                    );

                centerDust.noGravity = true;
                centerDust.velocity = Vector2.Zero;
                centerDust.scale = 0.7f;
            }
        }
    }
}