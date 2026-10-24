using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Tiles
{
	// Six crafting-station tiles. Textures live next to this file (moved out of BlockPngs):
	// Tiles/<Name>.png (item sprite, ~30px) and Tiles/<Name>Placed.png (tile sprite, ~48px).
	// Tile sizes follow the placed sprite: 3x2 for 38-40px tall, 3x3 for 46px tall.
	public abstract class YoyoStationTileBase : ModTile
	{
		protected abstract int StationIndex { get; }
		protected abstract Color MapColor { get; }
		protected abstract string StationDisplayName { get; }
		protected abstract bool Tall3x3 { get; }

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileTable[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;
			if (Tall3x3)
				TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
			else
				TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
			TileObjectData.newTile.Origin = new Point16(1, 1);
			TileObjectData.addTile(Type);
			AddMapEntry(MapColor);
			AdjTiles = new int[] { TileID.WorkBenches };
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			// Self-item drops via the automatic createTile system (single
			// placing item per station); manual spawn here would double it.
			// Breaking a block forgets its personal attunement.
			Content.Customization.YoyoCustomizationSystem.ForgetTile(TileOrigin(i, j, frameX, frameY));
		}

		protected abstract int StationItemType();

		// Multitile origin key so every placed block has a stable identity.
		public static Point TileOrigin(int i, int j, int frameX, int frameY)
		{
			return new Point(i - frameX / 18, j - frameY / 18);
		}

		public static Point TileOriginAt(int i, int j)
		{
			Tile t = Framing.GetTileSafely(i, j);
			return TileOrigin(i, j, t.TileFrameX, t.TileFrameY);
		}

		public override bool RightClick(int i, int j)
		{
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return false;
			Point key = TileOriginAt(i, j);
			Content.Customization.YoyoStationUISystem.OpenStation(StationIndex, key.X, key.Y);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = StationItemType();
			player.noThrow = 2;
		}
	}

	public class AdjustmentMachineTile : YoyoStationTileBase
	{
		protected override int StationIndex => 0;
		protected override Color MapColor => new(180, 160, 120);
		protected override string StationDisplayName => "Adjustment Machine (Yoyo)";
		protected override bool Tall3x3 => false;
		public override string Texture => "BiltonsYoyoMod/Tiles/AdjustmentMachinePlaced";
		protected override int StationItemType() => ModContent.ItemType<YoyoStationItems.AdjustmentMachineItem>();
	}

	public class TinkeringMachineTile : YoyoStationTileBase
	{
		protected override int StationIndex => 1;
		protected override Color MapColor => new(140, 180, 220);
		protected override string StationDisplayName => "Tinkering Machine (Yoyo)";
		protected override bool Tall3x3 => false;
		public override string Texture => "BiltonsYoyoMod/Tiles/TinkeringMachinePlaced";
		protected override int StationItemType() => ModContent.ItemType<YoyoStationItems.TinkeringMachineItem>();
	}

	public class SpecializedAdjustmentMachineTile : YoyoStationTileBase
	{
		protected override int StationIndex => 2;
		protected override Color MapColor => new(200, 140, 160);
		protected override string StationDisplayName => "Specialized Adjustment Machine (Yoyo)";
		protected override bool Tall3x3 => true;
		public override string Texture => "BiltonsYoyoMod/Tiles/SpecializedAdjustmentMachinePlaced";
		protected override int StationItemType() => ModContent.ItemType<YoyoStationItems.SpecializedAdjustmentMachineItem>();
	}

	public class CalibratingMachineTile : YoyoStationTileBase
	{
		protected override int StationIndex => 3;
		protected override Color MapColor => new(140, 220, 160);
		protected override string StationDisplayName => "Calibrating Machine (Yoyo)";
		protected override bool Tall3x3 => false;
		public override string Texture => "BiltonsYoyoMod/Tiles/CalibrationMachinePlaced";
		protected override int StationItemType() => ModContent.ItemType<YoyoStationItems.CalibratingMachineItem>();
	}

	public class SpecializedTinkeringMachineTile : YoyoStationTileBase
	{
		protected override int StationIndex => 4;
		protected override Color MapColor => new(180, 160, 240);
		protected override string StationDisplayName => "Specialized Tinkering Machine (Yoyo)";
		protected override bool Tall3x3 => false;
		public override string Texture => "BiltonsYoyoMod/Tiles/SpecializedTinkeringMachinePlaced";
		protected override int StationItemType() => ModContent.ItemType<YoyoStationItems.SpecializedTinkeringMachineItem>();
	}

	public class MagicalAdjustmentMachineTile : YoyoStationTileBase
	{
		protected override int StationIndex => 5;
		protected override Color MapColor => new(160, 220, 220);
		protected override string StationDisplayName => "Magical Adjustment Machine (Yoyo)";
		protected override bool Tall3x3 => true;
		public override string Texture => "BiltonsYoyoMod/Tiles/MagicalAdjustmentMachinePlaced";
		protected override int StationItemType() => ModContent.ItemType<YoyoStationItems.MagicalAdjustmentMachineItem>();
	}
}
