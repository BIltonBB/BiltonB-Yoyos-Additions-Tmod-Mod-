using Terraria.ModLoader;

namespace BiltonsYoyoMod.Common.Systems
{
    public class StringCutterKeybindSystem : ModSystem
    {
        public static ModKeybind CutStringKeybind;

        public override void Load()
        {
            CutStringKeybind =
                KeybindLoader.RegisterKeybind(
                    Mod,
                    "Cut Yoyo String",
                    "V"
                );
        }

        public override void Unload()
        {
            CutStringKeybind = null;
        }
    }
}