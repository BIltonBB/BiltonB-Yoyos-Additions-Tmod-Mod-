using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items;
using BiltonsYoyoMod.Items.Materials;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items.Ancient
{
	// The Yoyo Cultist's relic stock. Bought, never crafted (except the
	// Ancient Yoyo Relic, which ancient scraps can also assemble).
	public class AncientCounterweight : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.rare = ItemRarityID.Green;
			Item.value = Item.buyPrice(gold: 2);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.12f;
			player.GetArmorPenetration(DamageClass.MeleeNoSpeed) += 12;
			player.GetModPlayer<MarketPlayer>().AncientCounterweight = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 8)
				.AddIngredient<Materials.AncientScraps>(4)
				.AddTile(TileID.Anvils)
				.AddCondition(Condition.DownedSkeletron)
				.Register();
		}
	}

	public class AncientYoyoCase : YoyoStorageCase
	{
		public override string Texture => "BiltonsYoyoMod/Items/Ancient/AncientYoyoCase";
		public override int StorageTier => 5;
		// Backpack-sized relic case (TrickBook, WildString, LooseBearings,
		// ResponsePads, YoyoAxle, TensionBearing, Strings, Counterweight)
		// plus the relic extras: emblem, accessory and potion slots.
		public override int[] StorageSlots => new[] { 0, 3, 6, 9, 10, 11, 15, 16, 20, 21, 22 };
		public override float YoyoDamageBonus => 0.05f;

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<YoyoCollectionCaseA>()
				.AddIngredient(ItemID.Silk, 20)
				.AddIngredient<AncientScraps>(8)
				.AddTile(TileID.Anvils)
				.AddCondition(Condition.DownedSkeletron)
				.Register();
		}
	}

	public class EnchantedYoyo : ModItem
	{
		public override void SetDefaults()
		{
			Item.damage = 32;
			Item.DamageType = DamageClass.MeleeNoSpeed;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 25;
			Item.useAnimation = 25;
			Item.knockBack = 2.5f;
			Item.width = 24;
			Item.height = 24;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.channel = true;
			Item.shoot = ModContent.ProjectileType<Projectiles.EnchantedYoyoProj>();
			Item.shootSpeed = 12f;
			Item.UseSound = SoundID.Item1;
			Item.rare = ItemRarityID.Green;
			Item.value = Item.buyPrice(gold: 6);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.WoodYoyo)
				.AddIngredient(ItemID.FallenStar, 5)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}

	public class AncientString : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.rare = ItemRarityID.Green;
			Item.value = Item.buyPrice(gold: 2);
			Item.stringColor = global::BiltonsYoyoMod.Content.NPCs.Town.YoyoTownShared.PurpleStringShader;
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.06f;
			// No lifeMagnet/manaMagnet here: pickups are routed through the
			// deployed main yoyo by AncientMagnet instead, so nothing pulls
			// while no yoyo is out.
			player.yoyoString = true;
			player.stringColor = Item.stringColor;
			player.GetModPlayer<MarketPlayer>().AncientString = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Cobweb, 25)
				.AddIngredient(ItemID.FallenStar, 3)
				.AddIngredient<AncientScraps>(2)
				.AddTile(TileID.Loom)
				.AddCondition(Condition.DownedSkeletron)
				.Register();
		}
	}

	public class CultistYoyo : ModItem
	{
		public override void SetDefaults()
		{
			Item.damage = 58;
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
			Item.shoot = ModContent.ProjectileType<Projectiles.CultistYoyoProj>();
			Item.shootSpeed = 12.5f;
			Item.UseSound = SoundID.Item1;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.buyPrice(gold: 20);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.IronBar, 10)
				.AddIngredient<Materials.AncientScraps>(10)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}

	public class ForbiddenYoyo : ModItem
	{
		public override void SetDefaults()
		{
			Item.damage = 90;
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
			Item.shoot = ModContent.ProjectileType<Projectiles.ForbiddenYoyoProj>();
			Item.shootSpeed = 13f;
			Item.UseSound = SoundID.Item1;
			Item.rare = ItemRarityID.Red;
			Item.value = Item.buyPrice(gold: 35);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<CultistYoyo>()
				.AddIngredient(ItemID.SoulofNight, 8)
				.AddIngredient<Materials.MagicalEssence>(2)
				.AddTile(TileID.MythrilAnvil)
				.Register();
		}
	}

	public class AncientYoyoAccessory : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.rare = ItemRarityID.LightRed;
			Item.value = Item.buyPrice(gold: 15);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 10f;
			player.GetModPlayer<MarketPlayer>().AncientRange = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<AncientCounterweight>()
				.AddIngredient<AncientScraps>(8)
				.AddTile(TileID.MythrilAnvil)
				.AddCondition(Condition.Hardmode)
				.Register();
		}
	}

	public class AncientYoyoGlove : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.rare = ItemRarityID.LightPurple;
			Item.value = Item.buyPrice(gold: 25);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 8f;
			// Counts as a Yoyo Glove: vanilla throws a duplicate yoyo, and
			// the relic case's YoyoGlove slot accepts it by name.
			player.yoyoGlove = true;
			player.GetModPlayer<MarketPlayer>().AncientGlove = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.YoYoGlove)
				.AddIngredient<AncientScraps>(10)
				.AddTile(TileID.MythrilAnvil)
				.AddCondition(Systems.TierEmblemRecipes.DownedTwoMechBosses)
				.Register();
		}
	}

	public class CelestialYoyoAccessory : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.rare = ItemRarityID.Cyan;
			Item.value = Item.buyPrice(gold: 60);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<AncientYoyoAccessory>()
				.AddIngredient(ItemID.HallowedBar, 8)
				.AddIngredient(ItemID.SoulofLight, 5)
				.AddIngredient(ItemID.SoulofNight, 5)
				.AddIngredient<Materials.MagicalEssence>(2)
				.AddTile(TileID.TinkerersWorkbench)
				.Register();
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.18f;
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 10f;
			// Mends only while the main yoyo is out working.
			if (Content.NPCs.Town.YoyoTownShared.HasMainYoyo(player))
				player.lifeRegen += 2;
			player.GetModPlayer<MarketPlayer>().CelestialBlessing = true;
		}
	}

	public class AncientYoyoRelic : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.accessory = true;
			Item.rare = ItemRarityID.Purple;
			Item.value = Item.buyPrice(gold: 120);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.statDefense += 8;
			player.GetModPlayer<global::BiltonsYoyoMod.Players.YoyoStoragePlayer>().AddYoyoDefense(8);
			// Mends only while the main yoyo is out working.
			if (Content.NPCs.Town.YoyoTownShared.HasMainYoyo(player))
				player.lifeRegen += 1;
			player.GetModPlayer<MarketPlayer>().AncientRelic = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<AncientScraps>(12)
				.AddIngredient(ItemID.SoulofNight, 5)
				.AddTile(TileID.MythrilAnvil)
				.AddCondition(Condition.Hardmode)
				.Register();
		}
	}

	public class SeleneCoilYoyo : ModItem
	{
		public override void SetDefaults()
		{
			Item.damage = 92;
			Item.DamageType = DamageClass.MeleeNoSpeed;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 25;
			Item.useAnimation = 25;
			Item.knockBack = 3.5f;
			Item.width = 24;
			Item.height = 24;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.channel = true;
			Item.shoot = ModContent.ProjectileType<Projectiles.SeleneCoilProj>();
			Item.shootSpeed = 14f;
			Item.UseSound = SoundID.Item1;
			Item.rare = ItemRarityID.Cyan;
			Item.value = Item.buyPrice(gold: 90);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<EnchantedYoyo>()
				.AddIngredient(ItemID.HallowedBar, 12)
				.AddIngredient(ItemID.SoulofLight, 5)
				.AddIngredient<Materials.MagicalEssence>(2)
				.AddTile(TileID.MythrilAnvil)
				.Register();
		}
	}
}
