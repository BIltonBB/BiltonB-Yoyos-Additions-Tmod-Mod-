using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Tiles.Storage
{
	// Yoyo Chest: 2x2 categorized storage (5 yoyos, 5 accessories,
	// 5 potions, 5 materials, 5 other). Custom UI enforces categories.
	// Texture lives next to this file (moved out of BImages).
	public class YoyoChestTile : ModTile
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Storage/YoyoChestPlaced";

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
			AddMapEntry(new Color(200, 160, 60));
			DustType = DustID.WoodFurniture;
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			Point origin = TileOrigin(i, j, frameX, frameY);
			// Drop everything inside first — breaking never eats storage.
			// (The chest block itself drops via the automatic createTile
			// system; manual spawn here would double it.)
			if (Content.Storage.YoyoChestSystem.Chests.TryGetValue(origin, out var stored)) {
				for (int s = 0; s < Content.Storage.YoyoChestSystem.MaxTotalSlots; s++) {
					Item it = stored.Items[s];
					if (it != null && !it.IsAir)
						Item.NewItem(WorldGen.GetItemSource_FromTileBreak(i, j), i * 16, j * 16, 32, 32, it.type, it.stack, false, it.prefix);
				}
			}
			Content.Storage.YoyoChestSystem.ForgetTile(origin);
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
			Content.Storage.YoyoChestUISystem.OpenPanel(key.X, key.Y);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<YoyoChestItem>();
			player.noThrow = 2;
		}
	}

	public class YoyoChestItem : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Storage/YoyoChest";

		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 28;
			Item.maxStack = 99;
			Item.useTurn = true;
			Item.autoReuse = true;
			Item.useAnimation = 15;
			Item.useTime = 10;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.consumable = true;
			Item.createTile = ModContent.TileType<YoyoChestTile>();
			Item.value = Item.buyPrice(gold: 2);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wood, 15)
				.AddRecipeGroup(RecipeGroupID.IronBar, 8)
				.AddIngredient(ItemID.Silk, 5)
				.AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 8)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
