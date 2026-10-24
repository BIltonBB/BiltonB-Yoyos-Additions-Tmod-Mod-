using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Tiles.Decorative
{
	// Stringy Day Painting: 2x3 wall painting, day-themed yoyo design.
	// Same 3-variant system as Night (YoyoConfig.DayPaintingVariants).
	public class StringyDayPaintingTile : ModTile
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Decorative/StringyDayPaintingPlaced";

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
			TileObjectData.newTile.Origin = new Point16(0, 1);
			TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
			TileObjectData.newTile.AnchorTop = AnchorData.Empty;
			TileObjectData.newTile.AnchorWall = true;
			TileObjectData.newTile.CoordinatePadding = 0;
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(200, 170, 90), CreateMapEntryName());
			DustType = DustID.WoodFurniture;
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			// Self-item drops via the automatic createTile system (single
			// placing item); manual spawn here would double it.
			Content.Decorative.PaintingSystem.ForgetTile(TileOrigin(i, j, frameX, frameY));
		}

		public static Point TileOrigin(int i, int j, int frameX, int frameY)
		{
			return new Point(i - frameX / 16, j - frameY / 16);
		}

		public static Point TileOriginAt(int i, int j)
		{
			Tile t = Framing.GetTileSafely(i, j);
			return TileOrigin(i, j, t.TileFrameX, t.TileFrameY);
		}

		public override bool RightClick(int i, int j)
		{
			Point key = TileOriginAt(i, j);
			int count = Content.Decorative.PaintingSystem.EnabledCount(night: false);
			int cur = Content.Decorative.PaintingSystem.GetVariant(key);
			int next = (cur + 1) % System.Math.Max(1, count);
			Content.Decorative.PaintingSystem.SetVariant(key, next);
			if (!Main.dedServ) {
				Main.NewText(Content.Decorative.PaintingSystem.VariantName(false, next), Color.LightYellow);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
			}
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<StringyDayPaintingItem>();
			player.noThrow = 2;
		}
	}

	public class StringyDayPaintingItem : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Decorative/StringyDayPainting";

		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 32;
			Item.maxStack = 99;
			Item.useTurn = true;
			Item.autoReuse = true;
			Item.useAnimation = 15;
			Item.useTime = 10;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.consumable = true;
			Item.createTile = ModContent.TileType<StringyDayPaintingTile>();
			Item.value = Item.buyPrice(silver: 50);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wood, 8)
				.AddIngredient(ItemID.Sunflower, 1)
				.AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 4)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
