using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Tiles.Placeables
{
	// Legacy Torque Engine block (pre-rename). Old worlds still contain
	// these tiles: they load through this class and are converted into
	// ResearchEngineTile in place on world load (same footprint, so all
	// per-tile research data survives by coordinates). Anything left over
	// converts on right-click; breaking one drops the new block item.
	// DO NOT RENAME THIS CLASS: worlds resolve tiles by this exact name.
	public class TorqueEngineTile : ModTile
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Placeables/ResearchEnginePlaced";

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
			TileObjectData.newTile.Origin = new Point16(1, 1);
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(230, 160, 90));
			DustType = DustID.Torch;
		}

		public static Point TileOrigin(int i, int j, int frameX, int frameY)
		{
			return new Point(i - frameX / 18, j - frameY / 18);
		}

		public static Point TileOriginAt(int i, int j)
		{
			Tile t = Framing.GetTileSafely(i, j);
			return TileOrigin(i, j, t.TileFrameX, t.TileFrameY);
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			Item.NewItem(WorldGen.GetItemSource_FromTileBreak(i, j), i * 16, j * 16, 48, 32, ModContent.ItemType<ResearchEngineItem>());
			Point origin = TileOrigin(i, j, frameX, frameY);
			Content.Placeables.TechSaving.SaveOnBreak(origin);
			Content.Placeables.ResearchNetwork.OnDestroyed(origin);
			Content.Placeables.ResearchEngineSystem.ForgetTile(origin);
		}

		public override bool RightClick(int i, int j)
		{
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return false;
			Point key = TileOriginAt(i, j);
			Content.Placeables.ResearchEngineSystem.MigrateLegacyTile(key);
			Content.Placeables.ResearchEngineUISystem.OpenPanel(key.X, key.Y);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<ResearchEngineItem>();
			player.noThrow = 2;
		}
	}
}
