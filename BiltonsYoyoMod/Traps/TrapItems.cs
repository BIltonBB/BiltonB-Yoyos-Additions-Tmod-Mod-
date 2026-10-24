using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Traps
{
	public abstract class TrapItemBase : ModItem
	{
		protected abstract int TileType { get; }
		public override void SetDefaults()
		{
			Item.width = 16;
			Item.height = 16;
			Item.maxStack = 99;
			Item.useTurn = true;
			Item.autoReuse = false; // one click = one trap (no stack-eating spam)
			Item.useAnimation = 15;
			Item.useTime = 10;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.consumable = true;
			Item.createTile = TileType;
			Item.value = Item.buyPrice(gold: 1);
		}
	}

	public class WildstringSnareItem : TrapItemBase
	{
		protected override int TileType => ModContent.TileType<WildstringSnareTile>();
		public override string Texture => "BiltonsYoyoMod/Traps/WildstringSnare";
		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Cobweb, 30)
				.AddIngredient(ItemID.Silk, 5)
				.AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 10)
				.AddRecipeGroup(RecipeGroupID.IronBar, 8)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}

	public class BearingTrapItem : TrapItemBase
	{
		protected override int TileType => ModContent.TileType<BearingTrapTile>();
		public override string Texture => "BiltonsYoyoMod/Traps/BearingTrap";
		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.StoneBlock, 20)
				.AddRecipeGroup(RecipeGroupID.IronBar, 10)
				.AddIngredient(ItemID.Wire, 8)
				.AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 8)
				.AddTile(TileID.Anvils)
				.AddCondition(Condition.DownedSkeletron)
				.Register();
		}
	}

	public class CounterweightSentryItem : TrapItemBase
	{
		protected override int TileType => ModContent.TileType<CounterweightSentryTile>();
		public override string Texture => "BiltonsYoyoMod/Traps/CounterweightSentry";
		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 12)
				.AddIngredient(ItemID.Wire, 10)
				.AddIngredient(ModContent.ItemType<Items.Materials.YoyoParts>(), 6)
				.AddIngredient(ModContent.ItemType<Items.Accessories.LooseBearingsAW>(), 1)
				.AddTile(TileID.Anvils)
				.AddCondition(Condition.DownedSkeletron)
				.Register();
		}
	}

	public class StringLinkItem : TrapItemBase
	{
		protected override int TileType => ModContent.TileType<StringLinkTile>();
		public override string Texture => "BiltonsYoyoMod/Traps/StringLink";
		public override void AddRecipes()
		{
			CreateRecipe(2)
				.AddIngredient(ItemID.Cobweb, 20)
				.AddIngredient(ItemID.Silk, 10)
				.AddIngredient(ItemID.Wire, 5)
				.AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 6)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}

	public class YoyoTripwireItem : TrapItemBase
	{
		protected override int TileType => ModContent.TileType<YoyoTripwireTile>();
		public override string Texture => "BiltonsYoyoMod/Traps/YoyoTripwire";
		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wood, 15)
				.AddIngredient(ItemID.Cobweb, 20)
				.AddIngredient(ItemID.Wire, 6)
				.AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 10)
				.AddTile(TileID.WorkBenches)
				.AddCondition(Condition.DownedEyeOfCthulhu)
				.Register();
		}
	}

	// Dedicated linking item: right-click endpoints to pair (pairing != activation).
	public class StringLinkerItem : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Traps/StringLinker";
		public static Point FirstEndpoint = new Point(-9999, -9999);

		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 20;
			Item.maxStack = 1;
			Item.useTurn = true;
			Item.useAnimation = 20;
			Item.useTime = 20;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.value = Item.buyPrice(gold: 2);
			Item.rare = ItemRarityID.Green;
		}

		// Pairing happens on tile right-click (see TrapTileBase.RightClick):
		// hold this and right-click two endpoints. Left-click does nothing.
		public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "LinkerHint", "Right-click two String Link endpoints while holding this to pair them"));
		}

		// Live pairing readout while aiming with a pending first endpoint:
		// distance vs that endpoint's max range, right under the cursor.
		public override void HoldItem(Player player)
		{
			Point first = FirstEndpoint;
			if (first.X == -9999 || player == null || !player.active)
				return;
			if (player.whoAmI != Main.myPlayer)
				return;
			int tx = Player.tileTargetX;
			int ty = Player.tileTargetY;
			float dist = Vector2.Distance(new Vector2(first.X, first.Y), new Vector2(tx, ty));
			TrapState st = TrapSystem.Traps.TryGetValue(first, out var cur) ? cur : TrapState.DefaultFor(TrapKind.Link);
			int maxR = TrapActions.MaxLinkRange(st, first);
			player.cursorItemIconID = Type;
			player.cursorItemIconEnabled = true;
			player.cursorItemIconText = $"{dist:0}/{maxR} tiles";
		}
		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Silk, 10)
				.AddIngredient(ItemID.Wire, 10)
				.AddIngredient(ItemID.WoodYoyo, 1)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}
}
