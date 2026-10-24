using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Tiles.Placeables
{
	// Rift Anchor: combat positioning device. Multiple can be placed;
	// one per player is active. Rift Snap (keybind) redirects the
	// player's yoyos toward the active anchor for a burst.
	// Texture lives next to this file (moved out of StationImages).
	public class RiftAnchorTile : ModTile
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Placeables/RiftAnchorPlaced";

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;
			// Tall narrow sprite (24x46): 2 wide x 3 tall.
			TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
			TileObjectData.newTile.Width = 2;
			TileObjectData.newTile.Height = 3;
			TileObjectData.newTile.Origin = new Point16(1, 2);
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(170, 140, 255));
			DustType = DustID.PurpleTorch;
		}

		// Violet rift glow.
		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			float pulse = 0.75f + 0.25f * System.MathF.Sin(Main.GameUpdateCount * 0.04f + i * 0.3f);
			r = 0.35f * pulse;
			g = 0.15f * pulse;
			b = 0.55f * pulse;
		}

		// Rising violet wisps. Origin-only (frame 0,0) to avoid 6x spam.
		public override void NearbyEffects(int i, int j, bool closer)
		{
			if (!closer || Main.dedServ)
				return;
			Tile t = Framing.GetTileSafely(i, j);
			if (t.TileFrameX != 0 || t.TileFrameY != 0)
				return;
			if (Main.rand.NextBool(36)) {
				Vector2 pos = new(i * 16f + Main.rand.NextFloat(0f, 32f), j * 16f + Main.rand.NextFloat(0f, 48f));
				Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, DustID.PurpleTorch, 0f, -1.2f, 120, default, 0.9f);
				d.noGravity = true;
			}
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			// Self-item drops via the automatic createTile system (single
			// placing item); manual spawn here would double it.
			// If any player had this anchor active, it becomes invalid;
			// validation happens on use (no refund, no dup).
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

		public override bool RightClick(int i, int j)
		{
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return false;
			Point key = TileOriginAt(i, j);
			Content.Placeables.RiftAnchorUISystem.OpenPanel(key.X, key.Y);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<RiftAnchorItem>();
			player.noThrow = 2;
		}
	}
}
