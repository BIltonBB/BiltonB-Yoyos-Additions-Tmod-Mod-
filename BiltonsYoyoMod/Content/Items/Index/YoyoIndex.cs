using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Index;

namespace BiltonsYoyoMod.Content.Items.Index
{
	/// <summary>Guide Index: collection checklist of every yoyo ever carried. NOT a guidebook.</summary>
	public class YoyoIndex : ModItem
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
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.buyPrice(silver: 75);
		}

		public override bool CanUseItem(Player player) => !YoyoIndexUISystem.Open;

		public override bool? UseItem(Player player)
		{
			if (player.whoAmI == Main.myPlayer)
				YoyoIndexUISystem.OpenIndex();
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
}
