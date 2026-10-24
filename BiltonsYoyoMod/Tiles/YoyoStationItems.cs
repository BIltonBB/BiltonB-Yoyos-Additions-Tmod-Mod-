using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Tiles.YoyoStationItems
{
	public abstract class YoyoStationItemBase : ModItem
	{
		protected abstract int TileType { get; }
		protected abstract int ItemW { get; }
		protected abstract int ItemH { get; }

		public override void SetDefaults()
		{
			Item.width = ItemW;
			Item.height = ItemH;
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

	public class AdjustmentMachineItem : YoyoStationItemBase
	{
		protected override int TileType => ModContent.TileType<AdjustmentMachineTile>();
		protected override int ItemW => 30;
		protected override int ItemH => 28;
		public override string Texture => "BiltonsYoyoMod/Tiles/AdjustmentMachine";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 12)
				.AddIngredient(ItemID.Wood, 10)
				.AddIngredient(ItemID.Glass, 5)
				.AddIngredient(ItemID.FallenStar, 3)
				.AddIngredient(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 8)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}

	public class TinkeringMachineItem : YoyoStationItemBase
	{
		protected override int TileType => ModContent.TileType<TinkeringMachineTile>();
		protected override int ItemW => 30;
		protected override int ItemH => 28;
		public override string Texture => "BiltonsYoyoMod/Tiles/TinkeringMachine";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wood, 12)
				.AddRecipeGroup(RecipeGroupID.IronBar, 8)
				.AddIngredient(ItemID.Cobweb, 5)
				.AddIngredient(ItemID.FallenStar, 3)
				.AddIngredient(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 10)
				.AddTile(TileID.WorkBenches)
				.AddCondition(Condition.DownedEyeOfCthulhu)
				.Register();
		}
	}

	public class SpecializedAdjustmentMachineItem : YoyoStationItemBase
	{
		protected override int TileType => ModContent.TileType<SpecializedAdjustmentMachineTile>();
		protected override int ItemW => 30;
		protected override int ItemH => 24;
		public override string Texture => "BiltonsYoyoMod/Tiles/SpecializedAdjustmentMachine";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 12)
				.AddIngredient(ItemID.StoneBlock, 10)
				.AddIngredient(ItemID.SoulofNight, 5)
				.AddIngredient(ItemID.SoulofLight, 5)
				.AddIngredient(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>(), 3)
				.AddTile(TileID.Anvils)
				.AddCondition(Condition.Hardmode)
				.Register();
		}
	}

	public class CalibratingMachineItem : YoyoStationItemBase
	{
		protected override int TileType => ModContent.TileType<CalibratingMachineTile>();
		protected override int ItemW => 30;
		protected override int ItemH => 28;
		public override string Texture => "BiltonsYoyoMod/Tiles/CalibrationMachine";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 10)
				.AddIngredient(ItemID.Glass, 8)
				.AddIngredient(ItemID.FallenStar, 5)
				.AddIngredient(ItemID.Wire, 5)
				.AddIngredient(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 6)
				.AddTile(TileID.Anvils)
				.AddCondition(Condition.DownedSkeletron)
				.Register();
		}
	}

	public class SpecializedTinkeringMachineItem : YoyoStationItemBase
	{
		protected override int TileType => ModContent.TileType<SpecializedTinkeringMachineTile>();
		protected override int ItemW => 30;
		protected override int ItemH => 28;
		public override string Texture => "BiltonsYoyoMod/Tiles/SpecializedTinkeringMachine";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup("BiltonsYoyoMod:AnyMythrilBar", 12)
				.AddIngredient(ItemID.Wire, 8)
				.AddIngredient(ItemID.Glass, 8)
				.AddIngredient(ItemID.SoulofMight, 5)
				.AddIngredient(ItemID.SoulofFright, 5)
				.AddIngredient(ItemID.SoulofSight, 5)
				.AddIngredient(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>(), 4)
				.AddTile(TileID.MythrilAnvil)
				.AddCondition(Condition.Hardmode)
				.Register();
		}
	}

	public class MagicalAdjustmentMachineItem : YoyoStationItemBase
	{
		protected override int TileType => ModContent.TileType<MagicalAdjustmentMachineTile>();
		protected override int ItemW => 30;
		protected override int ItemH => 24;
		public override string Texture => "BiltonsYoyoMod/Tiles/MagicalAdjustmentMachine";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.ChlorophyteBar, 12)
				.AddIngredient(ItemID.Ectoplasm, 8)
				.AddIngredient(ItemID.LifeFruit, 5)
				.AddIngredient(ItemID.LihzahrdPowerCell, 1)
				.AddIngredient(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.GuardianCoreM>(), 3)
				.AddTile(TileID.AdamantiteForge)
				.AddCondition(Condition.DownedPlantera)
				.Register();
		}
	}
}
