using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Tiles.Placeables
{
	// Yoyo Resonator: placeable device that grants yoyos Dissonant Hum
	// (yoyo hits weaken enemies, reducing contact damage) while the
	// player is within its radius. Yoyo-only, radius-gated.
	// Texture lives next to this file (moved out of StationImages).
	public class YoyoResonatorTile : ModTile
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Placeables/YoyoResonatorPlaced";

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
			TileObjectData.newTile.Origin = new Point16(1, 1);
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(255, 150, 220));
			DustType = DustID.PinkTorch;
		}

		// Bright pink glow so the device reads in the dark. Pulses firmly.
		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			float pulse = 0.8f + 0.2f * System.MathF.Sin(Main.GameUpdateCount * 0.08f);
			r = 0.45f * pulse;
			g = 0.16f * pulse;
			b = 0.42f * pulse;
		}

		// Ambient hum motes plus a slow heartbeat ring. Origin-only so a
		// 2x2 emits once, not 4x.
		public override void NearbyEffects(int i, int j, bool closer)
		{
			if (!closer || Main.dedServ)
				return;
			Tile t = Framing.GetTileSafely(i, j);
			if (t.TileFrameX != 0 || t.TileFrameY != 0)
				return;
			if (Main.rand.NextBool(16)) {
				Vector2 pos = new(i * 16f + Main.rand.NextFloat(0f, 32f), j * 16f + Main.rand.NextFloat(0f, 32f));
				Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, DustID.PinkTorch, 0f, -0.8f, 120, default, 1f);
				d.noGravity = true;
				d.velocity *= 0.3f;
			}
			if (Main.GameUpdateCount % 120 == 0) {
				Vector2 center = new(i * 16f + 16f, j * 16f + 16f);
				for (int k = 0; k < 20; k++) {
					float ang = k / 20f * Microsoft.Xna.Framework.MathHelper.TwoPi;
					Vector2 pos = center + ang.ToRotationVector2() * 36f;
					Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, DustID.PinkTorch, 0f, 0f, 120, default, 1f);
					d.noGravity = true;
					d.velocity = Vector2.Zero;
				}
			}
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			// Self-item drops via the automatic createTile system (single
			// placing item); manual spawn here would double it.
			// Breaking forgets upgrades. No refund on break (prevents
			// break/replace duplication); refunds only via UI downgrade.
			Content.Placeables.ResonatorSystem.ForgetTile(TileOrigin(i, j, frameX, frameY));
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
			Content.Placeables.ResonatorUISystem.OpenPanel(key.X, key.Y);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<YoyoResonatorItem>();
			player.noThrow = 2;
		}
	}
}
