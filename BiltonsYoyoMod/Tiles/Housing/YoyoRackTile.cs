using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace BiltonsYoyoMod.Tiles.Housing
{
	// Yoyo Rack: 3x4 bookshelf-style yoyo furniture. Counts as a bookshelf
	// (table) for housing. Buffs only the Yoyo NPC whose assigned housing
	// contains the rack (see Content.Housing.YoyoRackSystem).
	// Texture lives next to this file (moved out of BImages).
	public class YoyoRackTile : ModTile
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Housing/YoyoRackPlaced";

		public override void SetStaticDefaults()
		{
			Main.tileFrameImportant[Type] = true;
			Main.tileNoAttach[Type] = true;
			Main.tileTable[Type] = true;
			Main.tileLavaDeath[Type] = true;
			TileID.Sets.DisableSmartCursor[Type] = true;
			TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
			TileObjectData.newTile.Origin = new Point16(1, 3);
			TileObjectData.newTile.CoordinatePadding = 0;
			TileObjectData.newTile.CoordinateWidth = 16;
			TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16, 16 };
			TileObjectData.addTile(Type);
			AddMapEntry(new Color(180, 140, 90));
			AddToArray(ref TileID.Sets.RoomNeeds.CountsAsTable);
			AdjTiles = new int[] { TileID.Bookcases };
			DustType = DustID.WoodFurniture;
		}

		public override void KillMultiTile(int i, int j, int frameX, int frameY)
		{
			// Self-item drops via the automatic createTile system (single
			// placing item); manual spawn here would double it.
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

		// Right-click reports who this rack boosts and with what, so the
		// buff is never a mystery. Buffs themselves tick in RackSystem.
		public override bool RightClick(int i, int j)
		{
			if (Main.dedServ)
				return true;
			Point key = TileOriginAt(i, j);
			var names = Content.Housing.YoyoRackSystem.ServedNPCNames(key);
			if (names.Count == 0) {
				Main.NewText("Yoyo Rack: no housed Yoyo NPC claims this spot.", Color.Gray);
			}
			else {
				Main.NewText($"Yoyo Rack boosts {string.Join(", ", names)}: {Content.Housing.YoyoRackSystem.BuffLine}.", Color.LightGreen);
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
			return true;
		}

		public override void MouseOver(int i, int j)
		{
			Player player = Main.LocalPlayer;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconID = ModContent.ItemType<YoyoRackItem>();
			var names = Content.Housing.YoyoRackSystem.ServedNPCNames(TileOriginAt(i, j));
			player.cursorItemIconText = names.Count > 0 ? "Boosts: " + string.Join(", ", names) : "No Yoyo resident";
			player.noThrow = 2;
		}
	}

	public class YoyoRackItem : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Tiles/Housing/YoyoRack";

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
			Item.createTile = ModContent.TileType<YoyoRackTile>();
			Item.value = Item.buyPrice(gold: 1);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wood, 20)
				.AddRecipeGroup(RecipeGroupID.IronBar, 5)
				.AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
