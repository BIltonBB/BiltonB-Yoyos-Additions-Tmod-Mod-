using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace BiltonsYoyoMod
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class BiltonsYoyoMod : Mod
	{
		public override void Load()
		{
			// Arena backdrop sky: draws the black fill in the background pass
			// (guaranteed behind tiles/NPCs, in front of the background).
			Filters.Scene[Content.Sky.GuardianArenaSky.Key] = new Filter(
				new ScreenShaderData("FilterMiniTower").UseColor(1f, 1f, 1f).UseOpacity(0.05f),
				EffectPriority.VeryHigh);
			SkyManager.Instance[Content.Sky.GuardianArenaSky.Key] = new Content.Sky.GuardianArenaSky();

			// YoyoStringLib does not self-install its DrawProj_DrawYoyoString
			// hook from a dllReference (it is a Mod class, never loaded), so
			// strings are handled by our own detour in MiniBossYoyoStrings.
		}

		public override void Unload()
		{
			// The arcane-star shape masks are the only runtime-generated textures in
			// the mod and they sit in static fields. Without this they survive a hot
			// reload and pin the old assembly, which is what tML reports as
			// "mod class still using memory".
			Projectiles.MiniYoyoFx.DisposeShapeTextures();
		}

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			long pos = -1;
			try { pos = reader.BaseStream.Position; } catch { }
			byte first = 255;
			try { first = reader.ReadByte(); } catch { return; }
			if (first == 100) {
				Content.Tech.YoyoTechPlayer.HandlePacket(reader, whoAmI);
				return;
			}
			if (first == 250) {
				Traps.TrapNet.Handle(reader, whoAmI);
				return;
			}
			if (first == 251) {
				Content.Storage.ChestNet.Handle(reader, whoAmI);
				return;
			}
			try { if (pos >= 0) reader.BaseStream.Position = pos; } catch { }
			Content.Placeables.PlaceableNet.HandlePacket(reader, whoAmI);
		}
	}
}
