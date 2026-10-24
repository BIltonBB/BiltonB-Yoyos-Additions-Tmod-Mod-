using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Tiles.Developer
{
	// Shiny Ball Developer block: dev-only interactive furniture.
	// Right-click opens the dev panel (mod name/version/credits,
	// Random Yoyo, cosmetic Effects toggle, hidden easter egg).
	// Obtainable ONLY via the /shinyball chat command (no recipe,
	// no shop, no drops). Texture lives next to this file (BImages).
	public class ShinyBallDevTile : ModTile
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Developer/ShinyBallDevPlaced";

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
			TileObjectData.newTile.Origin = new Point16(0, 1);
			TileObjectData.newTile.CoordinatePadding = 0;
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(255, 220, 130));
			DustType = DustID.GoldCoin;
		}

		public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
		{
			float pulse = 0.8f + 0.2f * System.MathF.Sin(Main.GameUpdateCount * 0.05f + i * 0.5f);
			r = 0.45f * pulse;
			g = 0.38f * pulse;
			b = 0.15f * pulse;
		}

		public override void NearbyEffects(int i, int j, bool closer)
		{
			if (!closer || Main.dedServ)
				return;
			Tile t = Framing.GetTileSafely(i, j);
			if (t.TileFrameX != 0 || t.TileFrameY != 0)
				return;
			Vector2 center = new(i * 16f + 16f, j * 16f + 16f);
			// Scattered sparkle field (deliberately NOT a ring): random
			// positions breathing in a loose box around the block, denser
			// near the ball, plus a rare rising mote.
			if (Main.GameUpdateCount % 6 == 0) {
				for (int q = 0; q < 3; q++) {
					float dx = Main.rand.NextFloat(-30f, 30f);
					float dy = Main.rand.NextFloat(-24f, 22f);
					float falloff = 1f - System.Math.Min(1f, new Vector2(dx, dy).Length() / 40f);
					if (!Main.rand.NextBool((int)(1 + falloff * 3)))
						continue;
					Dust d = Dust.NewDustDirect(center + new Vector2(dx, dy) - new Vector2(2, 2), 4, 4, Main.rand.NextBool(7) ? DustID.RainbowMk2 : DustID.GoldCoin, Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.9f, -0.2f), 120, default, Main.rand.NextFloat(0.7f, 1.1f));
					d.noGravity = true;
				}
			}
			if (Main.rand.NextBool(50)) {
				Vector2 pos = center + new Vector2(Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(0f, 14f));
				Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, DustID.GoldCoin, 0f, -1.4f, 120, default, 1f);
				d.noGravity = true;
			}
			Lighting.AddLight(center, 0.35f, 0.28f, 0.12f);
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			// Self-item drops via the automatic createTile system (single
			// placing item); manual spawn here would double it.
		}

		public override bool RightClick(int i, int j)
		{
			Content.Developer.ShinyBallUISystem.OpenPanel();
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<ShinyBallDevItem>();
			player.noThrow = 2;
		}
	}

	public class ShinyBallDevItem : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Developer/ShinyBallDev";

		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 30;
			Item.maxStack = 99;
			Item.useTurn = true;
			Item.autoReuse = true;
			Item.useAnimation = 15;
			Item.useTime = 10;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.consumable = true;
			Item.createTile = ModContent.TileType<ShinyBallDevTile>();
			Item.value = 0;
			Item.rare = ItemRarityID.Purple;
		}

		// No recipe, no shop, no drops: /shinyball command only.
	}
}
