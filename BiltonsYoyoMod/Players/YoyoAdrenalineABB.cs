using Terraria;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Players
{
    public class YoyoAdrenalineABB :
        PlayerDrawLayer
    {
        // BB SIZE

        // Original sprite: 96 x 16.
        // 2x smaller = 48 x 8.
        private const float DisplayScale = 0.5f;


        // LAYER

        public override Position GetDefaultPosition()
        {
            return new BeforeParent(
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
                    "BiltonsYoyoMod/Players/YoyoAdrenalineABB"
                ).Value;


            // STABLE WORLD POSITION

            /*
             * Do NOT use MountedCenter here.
             *
             * MountedCenter can move with the player's animation
             * state, including sleeping.
             *
             * player.position is the actual world-space body position,
             * so the bar keeps a fixed visual size and stable anchor.
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
                        ) * 1.5f,

                        (float)Math.Cos(
                            time * 1.05f
                        ) * 1.0f
                    );
            }


            // DRAW WHOLE BACK BAR

            /*
             * Draw the ENTIRE texture as one sprite.
             *
             * No source cropping.
             * No splitting.
             * No player-scale multiplication.
             *
             * Therefore it remains exactly 48x8 visually.
             */
            DrawData data =
                new DrawData(
                    texture,
                    screenPosition,
                    null,
                    Color.White,
                    0f,
                    texture.Size() * 0.5f,
                    DisplayScale,
                    SpriteEffects.None,
                    0
                );


            drawInfo.DrawDataCache.Add(
                data
            );
        }
    }
}