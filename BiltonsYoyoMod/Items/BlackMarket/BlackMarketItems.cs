using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.BlackMarket
{
	// Six Stringer's off-the-books stock. Bought, never crafted (except the
	// Smuggler's Gyro-Harness, which the parts can also build).
	public class IllegalYoyoParts : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 20;
			Item.maxStack = 9999;
			Item.rare = ItemRarityID.White;
			Item.value = Item.buyPrice(silver: 12);
		}
	}

	public class GoldYoyo : ModItem
	{
		public override void SetDefaults()
		{
			Item.damage = 26;
			Item.DamageType = DamageClass.MeleeNoSpeed;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 25;
			Item.useAnimation = 25;
			Item.knockBack = 3f;
			Item.width = 24;
			Item.height = 24;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.channel = true;
			Item.shoot = ModContent.ProjectileType<Projectiles.GoldYoyoProj>();
			Item.shootSpeed = 11f;
			Item.UseSound = SoundID.Item1;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.buyPrice(gold: 2);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.WoodYoyo)
				.AddIngredient(ItemID.GoldBar, 8)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}

	public class GoldenString : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.rare = ItemRarityID.Orange;
			Item.value = Item.buyPrice(gold: 3);
			Item.stringColor = global::BiltonsYoyoMod.Content.NPCs.Town.YoyoTownShared.YellowStringShader;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Cobweb, 20)
				.AddIngredient(ItemID.GoldBar, 5)
				.AddTile(TileID.Loom)
				.Register();
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.06f;
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 4f;
			player.yoyoString = true;
			player.stringColor = Item.stringColor;
			player.GetModPlayer<MarketPlayer>().GoldenString = true;
		}
	}

	public class GoldenCounterweight : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.rare = ItemRarityID.Orange;
			Item.value = Item.buyPrice(gold: 4);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.GoldBar, 6)
				.AddIngredient<Materials.YoyoParts>(3)
				.AddTile(TileID.Anvils)
				.Register();
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.04f;
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 10f;
			player.GetModPlayer<MarketPlayer>().GoldenCounterweight = true;
		}
	}

	public class LuckyYoyo : ModItem
	{
		public override void SetDefaults()
		{
			Item.damage = 62;
			Item.DamageType = DamageClass.MeleeNoSpeed;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 25;
			Item.useAnimation = 25;
			Item.knockBack = 2f;
			Item.width = 24;
			Item.height = 24;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.channel = true;
			Item.shoot = ModContent.ProjectileType<Projectiles.LuckyYoyoProj>();
			Item.shootSpeed = 13f;
			Item.UseSound = SoundID.Item1;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.buyPrice(gold: 40);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<GoldYoyo>()
				.AddIngredient(ItemID.SoulofLight, 5)
				.AddTile(TileID.Anvils)
				.Register();
		}

		public override void HoldItem(Player player)
		{
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 10f;
		}
	}

	public class IllegalYoyo : ModItem
	{
		public override void SetDefaults()
		{
			Item.damage = 72;
			Item.DamageType = DamageClass.MeleeNoSpeed;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 25;
			Item.useAnimation = 25;
			Item.knockBack = 4f;
			Item.width = 24;
			Item.height = 24;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.channel = true;
			Item.shoot = ModContent.ProjectileType<Projectiles.IllegalYoyoProj>();
			Item.shootSpeed = 12f;
			Item.UseSound = SoundID.Item1;
			Item.rare = ItemRarityID.LightRed;
			Item.value = Item.buyPrice(gold: 80);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<IllegalYoyoParts>(12)
				.AddIngredient(ItemID.DemoniteBar, 10)
				.AddTile(TileID.Anvils)
				.Register();
			CreateRecipe()
				.AddIngredient<IllegalYoyoParts>(12)
				.AddIngredient(ItemID.CrimtaneBar, 10)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}

	public class StringersSpecial : ModItem
	{
		public override void SetDefaults()
		{
			Item.damage = 78;
			Item.DamageType = DamageClass.MeleeNoSpeed;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 25;
			Item.useAnimation = 25;
			Item.knockBack = 3f;
			Item.width = 24;
			Item.height = 24;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.channel = true;
			Item.shoot = ModContent.ProjectileType<Projectiles.StringersSpecialProj>();
			Item.shootSpeed = 13f;
			Item.UseSound = SoundID.Item1;
			Item.rare = ItemRarityID.Purple;
			Item.value = Item.buyPrice(gold: 90);
		}
	}

	public class SmugglersGyroHarness : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.buyPrice(gold: 60);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.08f;
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 8f;
			player.statDefense += 4;
			player.GetModPlayer<global::BiltonsYoyoMod.Players.YoyoStoragePlayer>().AddYoyoDefense(4);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<IllegalYoyoParts>(10)
				.AddIngredient(ItemID.GoldBar, 5)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}

	public class SuperGoldYoyo : ModItem
	{
		public override void SetDefaults()
		{
			Item.damage = 95;
			Item.DamageType = DamageClass.MeleeNoSpeed;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 28;
			Item.useAnimation = 28;
			Item.knockBack = 8f;
			Item.width = 26;
			Item.height = 26;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.channel = true;
			Item.shoot = ModContent.ProjectileType<Projectiles.SuperGoldYoyoProj>();
			Item.shootSpeed = 8.5f;
			Item.UseSound = SoundID.Item1;
			Item.rare = ItemRarityID.Yellow;
			Item.value = Item.buyPrice(gold: 150);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<GoldYoyo>()
				.AddIngredient(ItemID.GoldBar, 8)
				.AddIngredient<IllegalYoyoParts>(5)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
