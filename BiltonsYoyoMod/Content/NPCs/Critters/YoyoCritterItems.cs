using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.NPCs.Critters
{
	// Caught-critter items. They reuse existing mod textures (Texture override)
	// so no new sprites are required for them to work in-game.
	public class GoldBearingItem : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Content/NPCs/Mobs/WildBearing";

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 5;
		}

		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 20;
			Item.maxStack = 999;
			Item.value = Item.buyPrice(gold: 1);
			Item.rare = ItemRarityID.Orange;
			Item.makeNPC = ModContent.NPCType<GoldBearingCritter>();
			Item.bait = 0;
		}
	}

	public class GoldenStringsItem : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/WildStringProjectile";

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 5;
		}

		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 20;
			Item.maxStack = 999;
			Item.value = Item.buyPrice(gold: 1);
			Item.rare = ItemRarityID.Orange;
			Item.makeNPC = ModContent.NPCType<GoldenStringsCritter>();
		}
	}

	public class GemBearingItem : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Content/NPCs/Mobs/StarGuardianNPC";

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 5;
		}

		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 20;
			Item.maxStack = 999;
			Item.value = Item.buyPrice(gold: 2);
			Item.rare = ItemRarityID.LightRed;
			Item.makeNPC = ModContent.NPCType<GemBearingCritter>();
		}
	}

	public class AllKnowingWormItem : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/WildStringLinkProj";

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 1;
		}

		public override void SetDefaults()
		{
			Item.width = 20;
			Item.height = 20;
			Item.maxStack = 999;
			Item.value = Item.buyPrice(platinum: 5);
			Item.rare = ItemRarityID.Purple;
			Item.makeNPC = ModContent.NPCType<AllKnowingWormCritter>();
			// Left-click releases the worm; also a 500% bait.
			Item.useStyle = ItemUseStyleID.Swing;
			Item.useTime = 15;
			Item.useAnimation = 15;
			Item.consumable = true;
			Item.bait = 500;
		}
	}
}
