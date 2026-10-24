using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using BiltonsYoyoMod.UI;

namespace BiltonsYoyoMod.Systems
{
    public class YoyoTrickUISystem : ModSystem
    {
        public override void ModifyInterfaceLayers(
            List<GameInterfaceLayer> layers
        )
        {
            int index =
                layers.FindIndex(
                    layer =>
                        layer.Name ==
                        "Vanilla: Mouse Text"
                );

            if (index < 0)
                index = layers.Count;

            layers.Insert(
                index,
                new LegacyGameInterfaceLayer(
                    "BiltonsYoyoMod: Yoyo Trick UI",
                    delegate
                    {
                        YoyoTrickUI.Draw(
                            Main.spriteBatch
                        );

                        return true;
                    },
                    InterfaceScaleType.UI
                )
            );
        }
    }
}