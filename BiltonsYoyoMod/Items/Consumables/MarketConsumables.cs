using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items.BlackMarket;
using BiltonsYoyoMod.Items.Materials;

namespace BiltonsYoyoMod.Items.Consumables
{
	// Greased bearings spin faster: yoyo potion sold by the Yoyo Tinkerer.
	public class BearingOil : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 28;
			Item.maxStack = 30;
			Item.consumable = true;
			Item.useStyle = ItemUseStyleID.DrinkLiquid;
			Item.useAnimation = 17;
			Item.useTime = 17;
			Item.UseSound = SoundID.Item3;
			Item.rare = ItemRarityID.Green;
			Item.value = Item.buyPrice(silver: 75);
			Item.buffType = ModContent.BuffType<Content.Buffs.BearingGreaseBuff>();
			Item.buffTime = 4 * 3600; // 4 minutes.
		}

		public override void AddRecipes()
		{
			CreateRecipe(2)
				.AddIngredient(ItemID.BottledWater)
				.AddIngredient<IllegalYoyoParts>(3)
				.AddTile(TileID.Bottles)
				.Register();
		}
	}

	// Forbidden vintage: big yoyo damage at the cost of protection.
	public class CultistsDraught : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 28;
			Item.maxStack = 30;
			Item.consumable = true;
			Item.useStyle = ItemUseStyleID.DrinkLiquid;
			Item.useAnimation = 17;
			Item.useTime = 17;
			Item.UseSound = SoundID.Item3;
			Item.rare = ItemRarityID.LightRed;
			Item.value = Item.buyPrice(silver: 90);
			Item.buffType = ModContent.BuffType<Content.Buffs.CultistFervorBuff>();
			Item.buffTime = 3 * 3600; // 3 minutes.
		}

		public override void AddRecipes()
		{
			CreateRecipe(2)
				.AddIngredient(ItemID.BottledWater)
				.AddIngredient<AncientScraps>(4)
				.AddTile(TileID.Bottles)
				.Register();
		}
	}

	// Trail food wound on a spool: a hearty, honeyed meal for long sessions.
	public class HoneySpool : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 24;
			Item.maxStack = 30;
			Item.consumable = true;
			Item.useStyle = ItemUseStyleID.EatFood;
			Item.useAnimation = 17;
			Item.useTime = 17;
			Item.UseSound = SoundID.Item2;
			Item.rare = ItemRarityID.Green;
			Item.value = Item.buyPrice(silver: 40);
			Item.buffType = BuffID.WellFed;
			Item.buffTime = 8 * 3600; // 8 minutes.
		}

		public override void AddRecipes()
		{
			CreateRecipe(3)
				.AddIngredient(ItemID.BottledHoney)
				.AddIngredient(ItemID.Cobweb, 5)
				.AddTile(TileID.CookingPots)
				.Register();
		}
	}

	// Slicked string slides faster: yoyo potion sold by the Yoyo Tinkerer.
	public class StringWax : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 28;
			Item.maxStack = 30;
			Item.consumable = true;
			Item.useStyle = ItemUseStyleID.DrinkLiquid;
			Item.useAnimation = 17;
			Item.useTime = 17;
			Item.UseSound = SoundID.Item3;
			Item.rare = ItemRarityID.Green;
			Item.value = Item.buyPrice(silver: 80);
			Item.buffType = ModContent.BuffType<Content.Buffs.StringWaxBuff>();
			Item.buffTime = 8 * 3600; // 8 minutes.
		}

		public override void AddRecipes()
		{
			CreateRecipe(2)
				.AddIngredient(ItemID.Cobweb, 10)
				.AddIngredient(ItemID.Gel, 5)
				.AddTile(TileID.WorkBenches)
				.Register();
		}
	}

	// Bottled moon-gaze: steady hands and a deeper mana well. Cultist stock.
	public class MoonlightFlask : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 28;
			Item.maxStack = 30;
			Item.consumable = true;
			Item.useStyle = ItemUseStyleID.DrinkLiquid;
			Item.useAnimation = 17;
			Item.useTime = 17;
			Item.UseSound = SoundID.Item3;
			Item.rare = ItemRarityID.LightPurple;
			Item.value = Item.buyPrice(silver: 95);
			Item.buffType = ModContent.BuffType<Content.Buffs.MoonlitFocusBuff>();
			Item.buffTime = 5 * 3600; // 5 minutes.
		}

		public override void AddRecipes()
		{
			CreateRecipe(2)
				.AddIngredient(ItemID.BottledWater)
				.AddIngredient(ItemID.Moonglow)
				.AddIngredient(ItemID.FallenStar)
				.AddTile(TileID.Bottles)
				.Register();
		}
	}
}
