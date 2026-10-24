using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using BiltonsYoyoMod.Players;
using BiltonsYoyoMod.Settings;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.UI
{
    public static class YoyoTrickUI
    {
        public static void Draw(
            SpriteBatch spriteBatch
        )
        {
            Player player =
                Main.LocalPlayer;

            YoyoTrickPlayer trickPlayer =
                player.GetModPlayer<YoyoTrickPlayer>();

            if (!trickPlayer.MinigameRunning)
                return;

            string key =
                GetKeyDisplayName();

            switch (trickPlayer.CurrentMinigame)
            {
                case TimingBoxMinigame timing:
                    DrawTimingBox(
                        spriteBatch,
                        timing,
                        key
                    );
                    break;

                case BoingyBoingMinigame boing:
                    DrawBoingy(
                        spriteBatch,
                        boing
                    );
                    break;

                case SleeperMinigame sleeper:
                    DrawSleeper(
                        spriteBatch,
                        sleeper
                    );
                    break;
            }
        }

        // KEY DISPLAY

        private static string GetKeyDisplayName()
        {
            if (YoyoTrickKeybind.YoyoTrickKey == null)
                return "?";

            var keys =
                YoyoTrickKeybind.YoyoTrickKey
                    .GetAssignedKeys();

            if (keys.Count <= 0)
                return "?";

            string key =
                keys[0];

            // Mouse buttons.
            if (key == "Mouse1" ||
                key == "Left Mouse" ||
                key == "Mouse Left")
            {
                return "M1";
            }

            if (key == "Mouse2" ||
                key == "Right Mouse" ||
                key == "Mouse Right")
            {
                return "M2";
            }

            if (key == "Mouse3" ||
                key == "Middle Mouse" ||
                key == "Mouse Middle")
            {
                return "M3";
            }

            // Automatically shorten long names.
            if (key.Length > 3)
            {
                return key.Substring(
                    0,
                    3
                );
            }

            return key;
        }

        // TIMING BOX

        private static void DrawTimingBox(
            SpriteBatch spriteBatch,
            TimingBoxMinigame timing,
            string key
        )
        {
            Texture2D pixel =
                TextureAssets.MagicPixel.Value;

            Vector2 boxCenter =
                new Vector2(
                    Main.screenWidth / 2f,
                    100f
                );

            float size =
                64f *
                timing.BoxScale;

            if (size < 8f)
                size = 8f;

            Rectangle rectangle =
                new Rectangle(
                    (int)(
                        boxCenter.X -
                        size / 2f
                    ),
                    (int)(
                        boxCenter.Y -
                        size / 2f
                    ),
                    (int)size,
                    (int)size
                );

            // DARK BOX

            spriteBatch.Draw(
                pixel,
                rectangle,
                new Rectangle(0, 0, 1, 1),
                Color.Black * 0.8f
            );

            // OUTLINE

            Color outlineColor =
                Color.Lerp(
                    Color.White,
                    Color.Red,
                    timing.RedProgress
                );

            DrawRectangleBorder(
                spriteBatch,
                rectangle,
                2,
                outlineColor *
                timing.BoxOpacity
            );

            // KEY

            Utils.DrawBorderString(
                spriteBatch,
                key,
                boxCenter,
                Color.White,
                1f,
                0.5f,
                0.5f
            );

            // TIMER

            string timerText =
                timing.WaitingForInput
                    ? $"{timing.Progress:0.00}s"
                    : "...";

            Color timerColor =
                timing.RedProgress > 0f
                    ? Color.Red
                    : Color.White;

            Vector2 timerPosition =
                new Vector2(
                    rectangle.Right + 12f,
                    boxCenter.Y
                );

            Utils.DrawBorderString(
                spriteBatch,
                timerText,
                timerPosition,
                timerColor,
                1f,
                0f,
                0.5f
            );
        }

        // RECTANGLE BORDER

        private static void DrawRectangleBorder(
            SpriteBatch spriteBatch,
            Rectangle rectangle,
            int thickness,
            Color color
        )
        {
            Texture2D pixel =
                TextureAssets.MagicPixel.Value;

            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    rectangle.X,
                    rectangle.Y,
                    rectangle.Width,
                    thickness
                ),
                new Rectangle(0, 0, 1, 1),
                color
            );

            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    rectangle.X,
                    rectangle.Bottom -
                    thickness,
                    rectangle.Width,
                    thickness
                ),
                new Rectangle(0, 0, 1, 1),
                color
            );

            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    rectangle.X,
                    rectangle.Y,
                    thickness,
                    rectangle.Height
                ),
                new Rectangle(0, 0, 1, 1),
                color
            );

            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    rectangle.Right -
                    thickness,
                    rectangle.Y,
                    thickness,
                    rectangle.Height
                ),
                new Rectangle(0, 0, 1, 1),
                color
            );
        }

        // BOINGY BOING

        private static void DrawBoingy(
            SpriteBatch spriteBatch,
            BoingyBoingMinigame boing
        )
        {
            Texture2D pixel =
                TextureAssets.MagicPixel.Value;

            float size =
                14f;

            Rectangle target =
                new Rectangle(
                    (int)(
                        boing.Target.X -
                        size / 2f -
                        Main.screenPosition.X
                    ),
                    (int)(
                        boing.Target.Y -
                        size / 2f -
                        Main.screenPosition.Y
                    ),
                    (int)size,
                    (int)size
                );

            spriteBatch.Draw(
                pixel,
                target,
                new Rectangle(0, 0, 1, 1),
                Color.Cyan
            );
        }

        // SLEEPER

        private static void DrawSleeper(
            SpriteBatch spriteBatch,
            SleeperMinigame sleeper
        )
        {
            Texture2D pixel =
                TextureAssets.MagicPixel.Value;

            float size =
                18f;

            Rectangle target =
                new Rectangle(
                    (int)(
                        sleeper.Target.X -
                        size / 2f -
                        Main.screenPosition.X
                    ),
                    (int)(
                        sleeper.Target.Y -
                        size / 2f -
                        Main.screenPosition.Y
                    ),
                    (int)size,
                    (int)size
                );

            spriteBatch.Draw(
                pixel,
                target,
                new Rectangle(0, 0, 1, 1),
                Color.White
            );
        }
    }
}