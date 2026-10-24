using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Tiles.Placeables
{
	// Yoyo Ward: support block that buffs nearby friendly town NPCs
	// (mod yoyo NPCs only, never hostiles/bosses/pets). No dedicated UI;
	// right-click reports radius, bonuses and affected count.
	// Texture lives next to this file (moved out of StationImages).
	public class YoyoWardTile : ModTile
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Placeables/YoyoWardPlaced";

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
			TileObjectData.newTile.Origin = new Point16(1, 1);
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(140, 230, 170));
			DustType = DustID.Grass;
		}

		// Verdant hearth glow.
		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			float pulse = 0.8f + 0.2f * System.MathF.Sin(Main.GameUpdateCount * 0.03f + j * 0.4f);
			r = 0.12f * pulse;
			g = 0.38f * pulse;
			b = 0.18f * pulse;
		}

		// Leaf drift. Origin-only.
		public override void NearbyEffects(int i, int j, bool closer)
		{
			if (!closer || Main.dedServ)
				return;
			Tile t = Framing.GetTileSafely(i, j);
			if (t.TileFrameX != 0 || t.TileFrameY != 0)
				return;
			if (Main.rand.NextBool(50)) {
				Vector2 pos = new(i * 16f + Main.rand.NextFloat(0f, 32f), j * 16f + Main.rand.NextFloat(0f, 32f));
				Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, DustID.Grass, 0.4f, -0.6f, 120, default, 0.8f);
				d.noGravity = true;
			}
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			// Self-item drops via the automatic createTile system (single
			// placing item); manual spawn here would double it.
			// Breaking forgets upgrades. No refund on break (prevents
			// break/replace duplication); refunds only via UI downgrade.
			// NPC bonuses restore automatically once out of radius.
			Content.Placeables.YoyoWardSystem.ForgetTile(TileOrigin(i, j, frameX, frameY));
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
			Content.Placeables.WardUISystem.OpenPanel(key.X, key.Y);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<YoyoWardItem>();
			player.noThrow = 2;
		}
	}
}
