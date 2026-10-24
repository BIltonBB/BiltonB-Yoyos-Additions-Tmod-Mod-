using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Traps.Licenses
{
	// Yoyo Licenses: tiered permits that unlock high trap progression.
	// They are HELD in inventory (never consumed): Lv3 upgrades need Tier I
	// held, Lv4 needs Tier II held, and Tier III held makes overcharged shots
	// never backfire. Textures live next to this file (moved from LicenseImages).
	public abstract class YoyoLicenseBase : ModItem
	{
		protected abstract int Tier { get; }

		public override void SetDefaults()
		{
			Item.width = 30;
			Item.height = 20;
			Item.maxStack = 1;
			Item.value = Item.buyPrice(gold: 5);
			Item.rare = Tier switch { 3 => ItemRarityID.Pink, 2 => ItemRarityID.Orange, _ => ItemRarityID.Green };
		}

		public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips)
		{
			string text = Tier switch {
				3 => "Held: overcharged traps never backfire",
				2 => "Held: allows defensive trap upgrades to Lv4",
				_ => "Held: allows defensive trap upgrades to Lv3",
			};
			tooltips.Add(new TooltipLine(Mod, "LicenseHint", text));
		}
	}

	public class YoyoLicenseT1 : YoyoLicenseBase
	{
		protected override int Tier => 1;
		public override string Texture => "BiltonsYoyoMod/Traps/Licenses/YoyoLicenseT1";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Wood, 10)
				.AddIngredient(ModContent.ItemType<Items.Materials.YoyoScraps>(), 10)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}

	public class YoyoLicenseT2 : YoyoLicenseBase
	{
		protected override int Tier => 2;
		public override string Texture => "BiltonsYoyoMod/Traps/Licenses/YoyoLicenseT2";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup(RecipeGroupID.IronBar, 10)
				.AddIngredient(ModContent.ItemType<Items.Materials.YoyoParts>(), 8)
				.AddIngredient(ItemID.HellstoneBar, 5)
				.AddTile(TileID.Anvils)
				.AddCondition(Condition.DownedSkeletron)
				.Register();
		}
	}

	public class YoyoLicenseT3 : YoyoLicenseBase
	{
		protected override int Tier => 3;
		public override string Texture => "BiltonsYoyoMod/Traps/Licenses/YoyoLicenseT3";

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ModContent.ItemType<Items.Materials.AdvancedYoyoParts>(), 5)
				.AddIngredient(ItemID.SoulofSight, 5)
				.AddTile(TileID.MythrilAnvil)
				.AddCondition(Condition.Hardmode)
				.Register();
		}
	}
}
