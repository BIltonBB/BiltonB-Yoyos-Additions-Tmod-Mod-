using Terraria.ModLoader;

namespace BiltonsYoyoMod.Settings
{
	public class RiftSnapKeybind : ModSystem
	{
		public static ModKeybind RiftSnapKey { get; private set; }

		public override void Load()
		{
			RiftSnapKey = KeybindLoader.RegisterKeybind(Mod, "Rift Snap", "G");
		}

		public override void Unload()
		{
			RiftSnapKey = null;
		}
	}
}
