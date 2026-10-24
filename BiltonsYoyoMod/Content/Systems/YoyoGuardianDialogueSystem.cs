using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace BiltonsYoyoMod.Content.Systems
{
    // Fix #1: big centered dialogue text, rewritten so:
    //   - every individual LETTER scales in from big -> normal and fades in on its own timer
    //     (based on when the typewriter reveal actually uncovered it), then all currently-shown
    //     letters shrink from normal -> small and fade out together at the end of the line, and
    //   - every letter gets a small fixed random tilt (-5..5 degrees), generated once per line
    //     so it doesn't jitter frame to frame, and
    //   - a line that would run off the sides of the screen word-wraps onto additional lines
    //     stacked downward from the same vertical anchor the old single-line version used,
    //     instead of running offscreen, and
    //   - (this pass) a single WORD wider than the wrap width on its own gets hard-split
    //     mid-word instead of being left to overflow the screen edge, and the whole wrapped
    //     block is clamped to stay fully on-screen vertically (and each line horizontally),
    //     instead of trusting the centered-around-32%-height math to always fit.
    //
    // Fix #2: Enqueue/ShowSpeechLine now take an optional per-line scale, so specific lines
    // (e.g. "The outer shell is cracked!") can render smaller than the default.
    //
    // Still queues lines internally, same as before — enqueuing a second line while one is
    // showing doesn't cut it off, it waits its turn.
    public class YoyoGuardianDialogueSystem : ModSystem
    {
        private const float CharsPerTick = 0.5f;    // reveal speed
        private const int HoldTicks = 70;           // fully visible, held before fading
        private const int FadeTicks = 35;
        private const int LetterFadeInTicks = 8;    // fix #1: how long one letter takes to settle from big -> normal
        private const float MaxLineWidthFraction = 0.8f; // fix #1: wrap once a line would exceed this fraction of the screen
        private const float ScreenEdgeMargin = 12f;  // fix #1: minimum gap kept from every screen edge

        private static readonly Queue<(string text, Color color, float scale)> pending = new Queue<(string, Color, float)>();

        private static string currentText;   // original, unwrapped line — drives the reveal-count math
        private static Color currentColor;
        private static float currentScale = 1f;
        private static int elapsed;

        private static List<string> wrappedLines;
        private static float[] letterAngles; // one entry per character of currentText, in radians

        public static bool IsIdle => currentText == null && pending.Count == 0;

        public static void Enqueue(string text, Color color, float scale = 1f)
        {
            if (string.IsNullOrEmpty(text)) return;
            pending.Enqueue((text, color, scale));
        }

        public override void PostUpdateEverything()
        {
            if (currentText == null)
            {
                if (pending.Count == 0) return;
                (currentText, currentColor, currentScale) = pending.Dequeue();
                elapsed = 0;
                BuildLineLayout();
                return;
            }

            elapsed++;
            int revealTicks = (int)(currentText.Length / CharsPerTick) + 1;
            int totalTicks = revealTicks + HoldTicks + FadeTicks;
            if (elapsed >= totalTicks)
            {
                currentText = null;
                wrappedLines = null;
                letterAngles = null;
            }
        }

        // Word-wraps currentText against ~80% of the screen width, hard-splitting any single
        // word that's still too wide on its own (fix #1), and gives every character in the
        // (unwrapped) string a fixed random tilt.
        private static void BuildLineLayout()
        {
            DynamicSpriteFont font = FontAssets.DeathText.Value;
            float maxWidth = Main.screenWidth * MaxLineWidthFraction;

            wrappedLines = new List<string>();
            string[] words = currentText.Split(' ');
            string line = "";

            foreach (string word in words)
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (font.MeasureString(candidate).X > maxWidth && line.Length > 0)
                {
                    wrappedLines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }

                // Fix #1: a single word wider than the max line width on its own would
                // otherwise stay on its own overflowing line forever — hard-split it instead.
                while (font.MeasureString(line).X > maxWidth && line.Length > 1)
                {
                    int splitAt = FindHardSplitIndex(font, line, maxWidth);
                    wrappedLines.Add(line.Substring(0, splitAt));
                    line = line.Substring(splitAt);
                }
            }
            if (line.Length > 0) wrappedLines.Add(line);

            letterAngles = new float[currentText.Length];
            for (int i = 0; i < letterAngles.Length; i++)
                letterAngles[i] = MathHelper.ToRadians(Main.rand.NextFloat(-5f, 5f));
        }

        // Finds the largest prefix length of `text` that still measures under maxWidth.
        private static int FindHardSplitIndex(DynamicSpriteFont font, string text, float maxWidth)
        {
            int best = 1;
            for (int len = 1; len <= text.Length; len++)
            {
                if (font.MeasureString(text.Substring(0, len)).X > maxWidth)
                    break;
                best = len;
            }
            return Math.Max(1, best);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (currentText == null) return;

            int index = layers.FindIndex(l => l.Name.Equals("Vanilla: Resource Bars"));
            if (index == -1) return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "BiltonsYoyoMod: Guardian Dialogue",
                DrawDialogue,
                InterfaceScaleType.UI));
        }

        private static bool DrawDialogue()
        {
            if (currentText == null || wrappedLines == null) return true;

            int revealTicks = (int)(currentText.Length / CharsPerTick) + 1;
            int fadeStart = revealTicks + HoldTicks;
            int globalRevealCount = (int)MathHelper.Clamp((int)(elapsed * CharsPerTick), 0, currentText.Length);
            if (globalRevealCount <= 0) return true;

            // Overall envelope — only drives the shared fade-OUT now; fade-in is per letter below.
            float lineAlpha;
            float lineScale;
            if (elapsed < fadeStart)
            {
                lineAlpha = 1f;
                lineScale = 1f;
            }
            else
            {
                float t = MathHelper.Clamp((elapsed - fadeStart) / (float)FadeTicks, 0f, 1f);
                lineScale = MathHelper.Lerp(1f, 0.65f, t);
                lineAlpha = 1f - t;
            }

            // Fix #2: fold in the per-line scale requested via Enqueue.
            lineScale *= currentScale;

            DynamicSpriteFont font = FontAssets.DeathText.Value;

            float lineHeight = font.MeasureString("A").Y * lineScale;
            float blockHeight = lineHeight * wrappedLines.Count;

            // Fix #1: clamp the whole block so it can never be pushed off the top or bottom of
            // the screen, instead of trusting the 32%-height anchor to always leave enough room.
            float blockTop = Main.screenHeight * 0.32f - blockHeight * 0.5f;
            float maxBlockTop = Math.Max(ScreenEdgeMargin, Main.screenHeight - ScreenEdgeMargin - blockHeight);
            blockTop = MathHelper.Clamp(blockTop, ScreenEdgeMargin, maxBlockTop);

            int globalIndex = 0;

            for (int li = 0; li < wrappedLines.Count; li++)
            {
                string lineText = wrappedLines[li];
                if (globalIndex >= globalRevealCount) break;

                int charsToShowOnThisLine = Math.Min(lineText.Length, globalRevealCount - globalIndex);
                if (charsToShowOnThisLine <= 0) { globalIndex += lineText.Length + 1; continue; }

                string shown = lineText.Substring(0, charsToShowOnThisLine);
                float lineWidth = font.MeasureString(shown).X * lineScale;

                // Fix #1: clamp each line horizontally too, so an unusually wide shown-so-far
                // substring can't poke past either edge of the screen.
                float cursorX = Main.screenWidth / 2f - lineWidth * 0.5f;
                cursorX = MathHelper.Clamp(cursorX, ScreenEdgeMargin, Math.Max(ScreenEdgeMargin, Main.screenWidth - ScreenEdgeMargin - lineWidth));

                Vector2 cursor = new Vector2(cursorX, blockTop + li * lineHeight);

                for (int ci = 0; ci < shown.Length; ci++)
                {
                    char c = shown[ci];
                    int charGlobalIndex = globalIndex + ci;
                    Vector2 charSize = font.MeasureString(c.ToString());
                    float charWidth = charSize.X * lineScale;

                    // Fix #1: per-letter big -> normal fade-in, timed from when the typewriter
                    // actually revealed THIS letter, not the line as a whole.
                    float letterAge = elapsed - charGlobalIndex / CharsPerTick;
                    float inT = MathHelper.Clamp(letterAge / LetterFadeInTicks, 0f, 1f);
                    float letterScale = lineScale * MathHelper.Lerp(1.6f, 1f, inT);
                    float letterAlpha = lineAlpha * MathHelper.Clamp(inT * 2f, 0f, 1f);

                    if (letterAlpha > 0.01f)
                    {
                        float angle = charGlobalIndex < letterAngles.Length ? letterAngles[charGlobalIndex] : 0f;
                        Vector2 origin = charSize * 0.5f;
                        Vector2 pos = cursor + new Vector2(charWidth * 0.5f, lineHeight * 0.5f);

                        for (int ox = -2; ox <= 2; ox += 2)
                        for (int oy = -2; oy <= 2; oy += 2)
                        {
                            if (ox == 0 && oy == 0) continue;
                            Main.spriteBatch.DrawString(font, c.ToString(), pos + new Vector2(ox, oy), Color.Black * (letterAlpha * 0.6f),
                                angle, origin, letterScale, SpriteEffects.None, 0f);
                        }

                        Main.spriteBatch.DrawString(font, c.ToString(), pos, currentColor * letterAlpha,
                            angle, origin, letterScale, SpriteEffects.None, 0f);
                    }

                    cursor.X += charWidth;
                }

                globalIndex += lineText.Length + 1; // +1 accounts for the space the wrap consumed
            }

            return true;
        }
    }
}