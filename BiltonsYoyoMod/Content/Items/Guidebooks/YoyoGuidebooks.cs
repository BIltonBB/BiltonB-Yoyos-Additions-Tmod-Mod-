using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Guidebook;

namespace BiltonsYoyoMod.Content.Items.Guidebooks
{
	/// <summary>Opens the General guidebook. Crafted at a Bookcase.</summary>
	public class YoyoGuidebookGeneral : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 30;
			Item.maxStack = 1;
			Item.consumable = false;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.UseSound = SoundID.Item35;
			Item.rare = ItemRarityID.White;
			Item.value = Item.buyPrice(silver: 50);
		}

		public override bool CanUseItem(Player player) => !YoyoGuidebookUISystem.Open;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				YoyoGuidebookUISystem.OpenBook("General");
			return true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Book, 1)
				.AddIngredient(ItemID.Silk, 5)
				.AddIngredient(ItemID.Cobweb, 10)
				.AddTile(TileID.Bookcases)
				.Register();
		}
	}

	public class YoyoGuidebookYoyos : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28; Item.height = 30; Item.maxStack = 1; Item.consumable = false;
			Item.useStyle = ItemUseStyleID.HoldUp; Item.useTime = 20; Item.useAnimation = 20;
			Item.UseSound = SoundID.Item35; Item.rare = ItemRarityID.White; Item.value = Item.buyPrice(silver: 50);
		}
		public override bool CanUseItem(Player player) => !YoyoGuidebookUISystem.Open;
		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				YoyoGuidebookUISystem.OpenBook("Yoyos");
			return true;
		}
		public override void AddRecipes()
		{
			CreateRecipe().AddIngredient(ItemID.Book, 1).AddIngredient(ItemID.Silk, 5).AddIngredient(ItemID.Cobweb, 10).AddTile(TileID.Bookcases).Register();
		}
	}

	public class YoyoGuidebookAccessories : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28; Item.height = 30; Item.maxStack = 1; Item.consumable = false;
			Item.useStyle = ItemUseStyleID.HoldUp; Item.useTime = 20; Item.useAnimation = 20;
			Item.UseSound = SoundID.Item35; Item.rare = ItemRarityID.White; Item.value = Item.buyPrice(silver: 50);
		}
		public override bool CanUseItem(Player player) => !YoyoGuidebookUISystem.Open;
		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				YoyoGuidebookUISystem.OpenBook("Accessories");
			return true;
		}
		public override void AddRecipes()
		{
			CreateRecipe().AddIngredient(ItemID.Book, 1).AddIngredient(ItemID.Silk, 5).AddIngredient(ItemID.Cobweb, 10).AddTile(TileID.Bookcases).Register();
		}
	}

	public class YoyoGuidebookOthers : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28; Item.height = 30; Item.maxStack = 1; Item.consumable = false;
			Item.useStyle = ItemUseStyleID.HoldUp; Item.useTime = 20; Item.useAnimation = 20;
			Item.UseSound = SoundID.Item35; Item.rare = ItemRarityID.White; Item.value = Item.buyPrice(silver: 50);
		}
		public override bool CanUseItem(Player player) => !YoyoGuidebookUISystem.Open;
		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				YoyoGuidebookUISystem.OpenBook("Others");
			return true;
		}
		public override void AddRecipes()
		{
			CreateRecipe().AddIngredient(ItemID.Book, 1).AddIngredient(ItemID.Silk, 5).AddIngredient(ItemID.Cobweb, 10).AddTile(TileID.Bookcases).Register();
		}
	}

	public class YoyoGuidebookConsumables : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28; Item.height = 30; Item.maxStack = 1; Item.consumable = false;
			Item.useStyle = ItemUseStyleID.HoldUp; Item.useTime = 20; Item.useAnimation = 20;
			Item.UseSound = SoundID.Item35; Item.rare = ItemRarityID.White; Item.value = Item.buyPrice(silver: 50);
		}
		public override bool CanUseItem(Player player) => !YoyoGuidebookUISystem.Open;
		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				YoyoGuidebookUISystem.OpenBook("Consumables");
			return true;
		}
		public override void AddRecipes()
		{
			CreateRecipe().AddIngredient(ItemID.Book, 1).AddIngredient(ItemID.Silk, 5).AddIngredient(ItemID.Cobweb, 10).AddTile(TileID.Bookcases).Register();
		}
	}

	public class YoyoGuidebookBlocksBosses : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28; Item.height = 30; Item.maxStack = 1; Item.consumable = false;
			Item.useStyle = ItemUseStyleID.HoldUp; Item.useTime = 20; Item.useAnimation = 20;
			Item.UseSound = SoundID.Item35; Item.rare = ItemRarityID.White; Item.value = Item.buyPrice(silver: 50);
		}
		public override bool CanUseItem(Player player) => !YoyoGuidebookUISystem.Open;
		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				YoyoGuidebookUISystem.OpenBook("BlocksBosses");
			return true;
		}
		public override void AddRecipes()
		{
			CreateRecipe().AddIngredient(ItemID.Book, 1).AddIngredient(ItemID.Silk, 5).AddIngredient(ItemID.Cobweb, 10).AddTile(TileID.Bookcases).Register();
		}
	}
}
