using Terraria;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using System;

namespace BiltonsYoyoMod.Players
{
    public class YoyoAdrenalineAFB :
        PlayerDrawLayer
    {
        // FB SIZE

        // Original sprite: 90 x 10.
        // 2x smaller = 45 x 5.
        private const float DisplayScale = 0.5f;

        private const float DisplayWidth = 45f;
        private const float DisplayHeight = 5f;


        // LAYER

        public override Position GetDefaultPosition()
        {
            return new AfterParent(
                PlayerDrawLayers.Head
            );
        }


        // DRAW

        protected override void Draw(
            ref PlayerDrawSet drawInfo)
        {
            Player player =
                drawInfo.drawPlayer;


            // ONLY THE WEARER SEES IT

            if (
                player.whoAmI !=
                Main.myPlayer
            )
            {
                return;
            }


            YoyoAdrenalineAPlayer mp =
                player.GetModPlayer<
                    YoyoAdrenalineAPlayer
                >();


            if (!mp.enabled)
            {
                return;
            }


            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Players/YoyoAdrenalineAFB"
                ).Value;


            // FILL

            float percentage =
                MathHelper.Clamp(
                    mp.FillPercentage,
                    0f,
                    1f
                );


            if (percentage <= 0f)
            {
                return;
            }


            // STABLE WORLD POSITION

            /*
             * Use the player's actual world position instead of
             * MountedCenter so sleeping/animation does not change
             * the bar's apparent size or anchor.
             */
            Vector2 worldPosition =
                new Vector2(
                    player.position.X +
                    player.width * 0.5f,

                    player.position.Y -
                    30f
                );


            Vector2 screenPosition =
                worldPosition -
                Main.screenPosition;


            // EMPOWERED SHAKE

            if (mp.Empowered)
            {
                float time =
                    Main.GameUpdateCount;


                screenPosition +=
                    new Vector2(
                        (float)Math.Sin(
                            time * 0.85f
                        ) * 1.0f,

                        (float)Math.Cos(
                            time * 1.05f
                        ) * 0.7f
                    );


                // SUBTLE RED RAGE PARTICLES

                if (Main.rand.NextBool(8))
                {
                    Vector2 dustPosition =
                        worldPosition +
                        new Vector2(
                            Main.rand.NextFloat(
                                -DisplayWidth * 0.45f,
                                DisplayWidth * 0.45f
                            ),
                            Main.rand.NextFloat(
                                -2f,
                                2f
                            )
                        );


                    Vector2 dustVelocity =
                        new Vector2(
                            Main.rand.NextFloat(
                                -0.15f,
                                0.15f
                            ),
                            Main.rand.NextFloat(
                                -0.35f,
                                -0.10f
                            )
                        );


                    Dust dust =
                        Dust.NewDustPerfect(
                            dustPosition,
                            DustID.Torch,
                            dustVelocity,
                            90,
                            new Color(
                                255,
                                55,
                                45
                            ),
                            Main.rand.NextFloat(
                                0.18f,
                                0.35f
                            )
                        );


                    dust.noGravity = true;
                    dust.fadeIn = 0.35f;
                }
            }


            // COLOR

            Color barColor;


            if (mp.Empowered)
            {
                barColor =
                    new Color(
                        255,
                        45,
                        45
                    );
            }
            else
            {
                barColor =
                    new Color(
                        120,
                        255,
                        150
                    );
            }


            // CENTER

            /*
             * Keep the 0.5 pixel correction.
             *
             * The FB is intentionally 0.5px lower than the BB.
             */
            Vector2 drawPosition =
                new Vector2(
                    screenPosition.X -
                    DisplayWidth * 0.5f,

                    screenPosition.Y -
                    DisplayHeight * 0.5f +
                    0.5f
                );


            // SOURCE CROP

            int sourceWidth =
                Math.Max(
                    1,
                    (int)(
                        90f *
                        percentage
                    )
                );


            Rectangle sourceRectangle =
                new Rectangle(
                    0,
                    0,
                    sourceWidth,
                    10
                );


            // DISPLAY WIDTH

            int displayFillWidth =
                Math.Max(
                    1,
                    (int)(
                        DisplayWidth *
                        percentage
                    )
                );


            Rectangle destinationRectangle =
                new Rectangle(
                    (int)drawPosition.X,
                    (int)drawPosition.Y,
                    displayFillWidth,
                    (int)DisplayHeight
                );


            // DRAW

            DrawData data =
                new DrawData(
                    texture,
                    destinationRectangle,
                    sourceRectangle,
                    barColor
                );


            drawInfo.DrawDataCache.Add(
                data
            );
        }
    }
}