using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Tiles.Placeables
{
	public abstract class PlaceableBlockItemBase : ModItem
	{
		protected abstract int TileType { get; }

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
			Item.createTile = TileType;
			Item.value = Item.buyPrice(gold: 1);
		}
	}

	public class YoyoResonatorItem : PlaceableBlockItemBase
	{
		protected override int TileType => ModContent.TileType<YoyoResonatorTile>();
		public override string Texture => "BiltonsYoyoMod/Tiles/Placeables/YoyoResonator";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 10)
				.AddIngredient(ItemID.Wood, 10)
				.AddIngredient(ItemID.FallenStar, 3)
				.AddIngredient(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 8)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}

	public class RiftAnchorItem : PlaceableBlockItemBase
	{
		protected override int TileType => ModContent.TileType<RiftAnchorTile>();
		public override string Texture => "BiltonsYoyoMod/Tiles/Placeables/RiftAnchor";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 8)
				.AddIngredient(ItemID.Wire, 6)
				.AddIngredient(ItemID.FallenStar, 5)
				.AddIngredient(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 4)
				.AddTile(TileID.Anvils)
				.AddCondition(Condition.DownedSkeletron)
				.Register();
		}
	}

	public class ResearchEngineItem : PlaceableBlockItemBase
	{
		protected override int TileType => ModContent.TileType<ResearchEngineTile>();
		public override string Texture => "BiltonsYoyoMod/Tiles/Placeables/ResearchEngine";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.StoneBlock, 20)
				.AddRecipeGroup(RecipeGroupID.IronBar, 10)
				.AddIngredient(ItemID.Wire, 8)
				.AddIngredient(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 6)
				.AddTile(TileID.Anvils)
				.AddCondition(Condition.DownedSkeletron)
				.Register();
		}
	}

	// Legacy Torque Engine item (pre-rename). Old inventories still hold
	// these: they remain usable and place the Research Engine block.
	public class TorqueEngineItem : PlaceableBlockItemBase
	{
		protected override int TileType => ModContent.TileType<ResearchEngineTile>();
		public override string Texture => "BiltonsYoyoMod/Tiles/Placeables/ResearchEngine";
	}

	public class YoyoWardItem : PlaceableBlockItemBase
	{
		protected override int TileType => ModContent.TileType<YoyoWardTile>();
		public override string Texture => "BiltonsYoyoMod/Tiles/Placeables/YoyoWard";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wood, 15)
				.AddRecipeGroup(RecipeGroupID.IronBar, 8)
				.AddIngredient(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 10)
				.AddTile(TileID.WorkBenches)
				.AddCondition(global::BiltonsYoyoMod.Systems.TierEmblemRecipes.DownedEvilBoss)
				.Register();
		}
	}
}
