using Microsoft.Xna.Framework;
using Terraria;
using BiltonsYoyoMod.Items.Accessories;

namespace BiltonsYoyoMod.Systems
{
    public class TimingBoxMinigame : YoyoTrickMinigame
    {
        private readonly int requiredHits;

        private int currentHit;
        private int timer;
        private int randomGap;

        private bool waitingForInput;

        // BOX TIMING

        // 1 second of expansion.
        private const int ExpansionDuration = 60;

        // Additional 0.5 seconds after expansion.
        private const int OverflowDuration = 30;

        // Total box lifetime = 1.5 seconds.
        private const int BoxLifetime =
            ExpansionDuration +
            OverflowDuration;

        // SUCCESS WINDOW

        // 0.75s = 45 frames. 0.5s = 30 frames. 0.25s = 15 frames
        private const int SuccessStartFrame = 15;

        // 1.25s = 75 frames. 1.5s = 90 frames
        private const int SuccessEndFrame = 75;

        // VISUAL STATE

        public float BoxScale { get; private set; }

        public float BoxOpacity { get; private set; }

        // 0 = white
        // 1 = completely red
        public float RedProgress { get; private set; }

        public bool WaitingForInput =>
            waitingForInput;

        // Current box timer in seconds.
        public float Progress
        {
            get
            {
                if (!waitingForInput)
                    return 0f;

                return MathHelper.Clamp(
                    timer / 60f,
                    0f,
                    1.5f
                );
            }
        }

        // Useful for diagnostics.
        public int CurrentFrame =>
            timer;

        // CONSTRUCTOR

        public TimingBoxMinigame(
            int requiredHits)
        {
            this.requiredHits =
                requiredHits < 1
                    ? 1
                    : requiredHits;
        }

        // START

        public override void Start(
            Player player,
            YoyoTrickItem trick)
        {
            Finished = false;
            Success = false;

            currentHit = 0;
            timer = 0;

            waitingForInput = false;

            randomGap =
                Main.rand.Next(
                    20,
                    50
                );

            BoxScale = 0.35f;
            BoxOpacity = 0.5f;
            RedProgress = 0f;
        }

        // UPDATE

        public override void Update(
            Player player)
        {
            if (Finished)
                return;

            // YOYO MUST STILL EXIST

            if (YoyoTrickSystem.GetHeldYoyo(player) == null)
            {
                Finished = true;
                Success = false;
                return;
            }

            // WAIT FOR THE NEXT BOX

            if (!waitingForInput)
            {
                timer++;

                if (timer >= randomGap)
                {
                    timer = 0;

                    waitingForInput = true;

                    BoxScale = 0.35f;
                    BoxOpacity = 0.5f;
                    RedProgress = 0f;

                    Main.NewText(
                        "TIMING BOX APPEARED",
                        Color.Cyan
                    );
                }

                return;
            }

            // BOX TIMER

            timer++;

            // 0.00 - 1.00 SECONDS

            if (timer <= ExpansionDuration)
            {
                float expansionProgress =
                    timer /
                    (float)ExpansionDuration;

                BoxScale =
                    MathHelper.Lerp(
                        0.35f,
                        1f,
                        expansionProgress
                    );

                BoxOpacity =
                    MathHelper.Lerp(
                        0.5f,
                        1f,
                        expansionProgress
                    );

                RedProgress = 0f;

                return;
            }

            // 1.00 - 1.50 SECONDS
            //
            // The box is now full-sized.
            // It no longer expands.
            // It gradually turns red.

            BoxScale = 1f;
            BoxOpacity = 1f;

            int overflowFrame =
                timer -
                ExpansionDuration;

            RedProgress =
                MathHelper.Clamp(
                    overflowFrame /
                    (float)OverflowDuration,
                    0f,
                    1f
                );

            // 1.50 SECOND FAILURE

            if (timer >= BoxLifetime)
            {
                Main.NewText(
                    "TIMING BOX EXPIRED - TRICK FAILED",
                    Color.Red
                );

                Finished = true;
                Success = false;

                return;
            }
        }

        // KEY PRESS

        public override void ReceiveKeyPress(
            Player player)
        {
            if (Finished ||
                !waitingForInput)
            {
                return;
            }

            // DIRECT FRAME CHECK
            //
            // 45 frames = 0.75 sec
            // 75 frames = 1.25 sec
            //
            // No floating-point timing conversion is used here.

            bool valid =
                timer >= SuccessStartFrame &&
                timer <= SuccessEndFrame;

            // DIAGNOSTIC

            Main.NewText(
                "TIMING INPUT: " +
                timer +
                " FRAMES (" +
                (timer / 60f).ToString("0.00") +
                "s)",
                valid
                    ? Color.Lime
                    : Color.Red
            );

            // SUCCESS

            if (valid)
            {
                currentHit++;

                Main.NewText(
                    "TIMING HIT " +
                    currentHit +
                    "/" +
                    requiredHits,
                    Color.Lime
                );

                // FINISHED

                if (currentHit >= requiredHits)
                {
                    Main.NewText(
                        "TIMING BOX SEQUENCE COMPLETE",
                        Color.Lime
                    );

                    Finished = true;
                    Success = true;

                    return;
                }

                // NEXT BOX

                waitingForInput = false;

                timer = 0;

                randomGap =
                    Main.rand.Next(
                        20,
                        55
                    );

                BoxScale = 0.35f;
                BoxOpacity = 0.5f;
                RedProgress = 0f;

                return;
            }

            // FAILED INPUT

            Main.NewText(
                "TIMING INPUT FAILED: OUTSIDE 0.75-1.25s WINDOW",
                Color.Red
            );

            Finished = true;
            Success = false;
        }
    }
}