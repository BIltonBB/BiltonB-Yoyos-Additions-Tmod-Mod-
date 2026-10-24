using Terraria.ModLoader;

namespace BiltonsYoyoMod.Settings
{
	public class YoyoWarpKeybind : ModSystem
	{
		public static ModKeybind QuantumWarpKey { get; private set; }

		public override void Load()
		{
			QuantumWarpKey = KeybindLoader.RegisterKeybind(Mod, "Quantum Warp", "Q");
		}

		public override void Unload()
		{
			QuantumWarpKey = null;
		}
	}
}
