using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using BiltonsYoyoMod.Items.Accessories;

namespace BiltonsYoyoMod.Systems
{
    public class SleeperMinigame : YoyoTrickMinigame
    {
        // SETTINGS

        // The Sleeper minigame lasts exactly 5 seconds.
        private const int MaxDuration = 300;

        // Yoyo must stay within this distance of the fixed target.
        private const float TargetRadius = 45f;

        // Yoyo must remain inside the target for 0.75 seconds.
        private const int RequiredTime = 45;

        // Visual radius of the target ring.
        private const float TargetVisualRadius = 24f;

        // STATE

        private int timer;
        private int targetTimer;

        // Captured ONCE when the minigame starts.
        // This NEVER follows the yoyo.
        public Vector2 Target { get; private set; }

        // START

        public override void Start(
            Player player,
            YoyoTrickItem trick)
        {
            Finished = false;
            Success = false;

            timer = 0;
            targetTimer = 0;

            // CAPTURE YOYO POSITION

            Projectile yoyo =
                YoyoTrickSystem.GetMainYoyo(player);

            if (yoyo == null)
            {
                Main.NewText(
                    "SLEEPER FAILED: NO YOYO AT START",
                    Color.Red
                );

                Finished = true;
                Success = false;
                return;
            }

            // Fixed target position.
            Target = yoyo.Center;

            Main.NewText(
                "SLEEPER TARGET CREATED AT YOYO POSITION",
                Color.Cyan
            );

            Main.NewText(
                "SLEEPER: KEEP YOYO AT TARGET FOR 0.75 SECONDS",
                Color.Yellow
            );

            Main.NewText(
                "SLEEPER DURATION: 5 SECONDS",
                Color.Yellow
            );
        }

        // UPDATE

        public override void Update(
            Player player)
        {
            if (Finished)
                return;

            targetTimer++;

            // 5 SECOND TIMEOUT

            if (targetTimer >= MaxDuration)
            {
                Main.NewText(
                    "SLEEPER TIMED OUT - TRICK FAILED",
                    Color.Red
                );

                Finished = true;
                Success = false;
                return;
            }

            // GET CURRENT YOYO

            Projectile yoyo =
                YoyoTrickSystem.GetMainYoyo(player);

            if (yoyo == null)
            {
                Main.NewText(
                    "SLEEPER FAILED: YOYO DISAPPEARED",
                    Color.Red
                );

                Finished = true;
                Success = false;
                return;
            }

            // TARGET FADE
            //
            // The target remains for all 5 seconds.
            // It only starts fading during the final second.

            float alpha = 1f;

            if (targetTimer > MaxDuration - 60)
            {
                float fadeProgress =
                    (targetTimer - (MaxDuration - 60)) / 60f;

                alpha =
                    1f - MathHelper.Clamp(
                        fadeProgress,
                        0f,
                        1f
                    );
            }

            // TARGET VISUAL

            int particleCount = 18;

            float pulse =
                1f +
                0.08f *
                (float)System.Math.Sin(
                    targetTimer * 0.15f
                );

            float radius =
                TargetVisualRadius * pulse;

            for (int i = 0;
                 i < particleCount;
                 i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    particleCount;

                Vector2 direction =
                    new Vector2(
                        (float)System.Math.Cos(angle),
                        (float)System.Math.Sin(angle)
                    );

                Vector2 spawnPosition =
                    Target +
                    direction * radius;

                Dust dust =
                    Dust.NewDustPerfect(
                        spawnPosition,
                        DustID.GoldFlame,
                        Vector2.Zero
                    );

                dust.noGravity = true;
                dust.velocity = Vector2.Zero;

                dust.scale =
                    0.9f * alpha;
            }

            // CENTER MARKER

            if (targetTimer % 3 == 0)
            {
                Dust centerDust =
                    Dust.NewDustPerfect(
                        Target,
                        DustID.GoldFlame,
                        Vector2.Zero
                    );

                centerDust.noGravity = true;
                centerDust.velocity = Vector2.Zero;

                centerDust.scale =
                    0.65f * alpha;
            }

            // CHECK YOYO DISTANCE

            float distance =
                Vector2.Distance(
                    yoyo.Center,
                    Target
                );

            if (distance <= TargetRadius)
            {
                timer++;

                if (timer >= RequiredTime)
                {
                    Main.NewText(
                        "SLEEPER SUCCESS",
                        Color.Lime
                    );

                    Finished = true;
                    Success = true;
                    return;
                }
            }
            else
            {
                // Leaving the target resets the required hold time.
                timer = 0;
            }
        }
    }
}