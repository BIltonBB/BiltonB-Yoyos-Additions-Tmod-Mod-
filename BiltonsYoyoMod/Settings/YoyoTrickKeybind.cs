using Terraria.ModLoader;

namespace BiltonsYoyoMod.Settings
{
    public class YoyoTrickKeybind : ModSystem
    {
        public static ModKeybind YoyoTrickKey { get; private set; }

        public override void Load()
        {
            YoyoTrickKey = KeybindLoader.RegisterKeybind(
                Mod,
                "Yoyo Trick",
                "V"
            );
        }

        public override void Unload()
        {
            YoyoTrickKey = null;
        }
    }
}