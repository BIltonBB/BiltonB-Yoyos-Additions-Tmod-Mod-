using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Tiles.Placeables
{
	// Research Engine: placeable block charged by direct yoyo impacts.
	// Custom AABB overlap (not tile collision) registers distinct hits
	// so the yoyo never lodges; each hit adds 50s lifetime + rebound.
	// Texture lives next to this file (moved out of StationImages).
	public class ResearchEngineTile : ModTile
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

		// Heat glow scales with stored charge: dim when empty, bright orange at full.
		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			Tile t = Framing.GetTileSafely(i, j);
			Point origin = TileOrigin(i, j, t.TileFrameX, t.TileFrameY);
			float fill = 0f;
			if (Content.Placeables.ResearchEngineSystem.Tiles.TryGetValue(origin, out var data))
				fill = data.Charge / (float)Content.Placeables.ResearchEngineSystem.MaxChargeOf(data);
			float flicker = 0.9f + 0.1f * System.MathF.Sin(Main.GameUpdateCount * 0.12f + i);
			r = (0.15f + 0.55f * fill) * flicker;
			g = (0.08f + 0.28f * fill) * flicker;
			b = 0.05f * flicker;
		}

		// Cinder drift, heavier when charged. Origin-only.
		public override void NearbyEffects(int i, int j, bool closer)
		{
			if (!closer || Main.dedServ)
				return;
			Tile t = Framing.GetTileSafely(i, j);
			if (t.TileFrameX != 0 || t.TileFrameY != 0)
				return;
			Point origin = TileOrigin(i, j, t.TileFrameX, t.TileFrameY);
			float fill = 0f;
			if (Content.Placeables.ResearchEngineSystem.Tiles.TryGetValue(origin, out var data))
				fill = data.Charge / (float)Content.Placeables.ResearchEngineSystem.MaxChargeOf(data);
			int chance = fill >= 1f ? 8 : fill > 0.5f ? 20 : 45;
			if (Main.rand.NextBool(chance)) {
				Vector2 pos = new(i * 16f + Main.rand.NextFloat(0f, 48f), j * 16f + Main.rand.NextFloat(0f, 32f));
				Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, fill >= 1f ? DustID.Torch : DustID.Smoke, 0f, -0.8f, 100, default, fill >= 1f ? 1f : 0.7f);
				d.noGravity = true;
			}
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			Item.NewItem(WorldGen.GetItemSource_FromTileBreak(i, j), i * 16, j * 16, 48, 32, ModContent.ItemType<ResearchEngineItem>());
			// Breaking loses stored charge and config (no refund of charge;
			// prevents break/replace charge duplication). Tech Saving files
			// a player backup first; the network hook runs next so a linked
			// survivor inherits (Failover, #6). Without the branch, the
			// entry is simply forgotten: everything is wiped (#15).
			Point origin = TileOrigin(i, j, frameX, frameY);
			Content.Placeables.TechSaving.SaveOnBreak(origin);
			Content.Placeables.ResearchNetwork.OnDestroyed(origin);
			Content.Placeables.ResearchEngineSystem.ForgetTile(origin);
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
