using System.Collections.Generic;
using BiltonsYoyoMod.UI;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace BiltonsYoyoMod.Systems
{
    public class YoyoStorageUISystem : ModSystem
    {
        public override void ModifyInterfaceLayers(
            List<GameInterfaceLayer> layers)
        {
            int index =
                layers.FindIndex(
                    layer =>
                        layer.Name ==
                        "Vanilla: Mouse Text"
                );

            layers.Insert(
                index < 0
                    ? layers.Count
                    : index,

                new LegacyGameInterfaceLayer(
                    "BiltonsYoyoMod: Yoyo Storage",
                    () =>
                    {
                        YoyoStorageUI.Draw(
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