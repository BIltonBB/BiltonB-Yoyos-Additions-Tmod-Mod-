using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;
using BiltonsYoyoMod.Content.Systems;

namespace BiltonsYoyoMod.Content.Items
{
	// Summons the Six Stringer at night (Cowboys of Yoyos + Eye of Cthulhu
	// must be defeated, like his natural spawn).
	public class TravellersCaller : ModItem
	{
		public override string Texture => "BiltonsYoyoMod/Content/Items/TravellersCaller";

		public override void SetStaticDefaults()
		{
			Item.ResearchUnlockCount = 1;
		}

		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 24;
			Item.maxStack = 999;
			Item.consumable = true;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = 30;
			Item.useAnimation = 30;
			Item.rare = ItemRarityID.Orange;
			Item.value = Item.buyPrice(gold: 5);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Silk, 10)
				.AddIngredient(ItemID.GoldBar, 5)
				.AddIngredient(ItemID.FallenStar, 3)
				.AddTile(TileID.Loom)
				.Register();
		}

		public override bool CanUseItem(Player player)
		{
			if (Main.dayTime) {
				Main.NewText("The caller only works at night.", 200, 180, 100);
				return false;
			}
			if (!NPC.downedBoss1) {
				Main.NewText("The Eye of Cthulhu has not been defeated yet.", 200, 180, 100);
				return false;
			}
			if (!YoyoGuardianWorld.downedCowboyOfYoyos) {
				Main.NewText("The Cowboys of Yoyos have not been defeated yet.", 200, 180, 100);
				return false;
			}
			return true;
		}

		public override bool? UseItem(Player player)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return true; // Server spawns via... (kept simple: works in single player / host).
			if (YoyoTownSystem.SpawnTraveller<SixStringer>())
				return true;
			// Not consumed on failure: someone (even the vanilla merchant)
			// is already visiting tonight.
			Main.NewText("No traveler answers the call right now.", 200, 180, 100);
			return false;
		}
	}
}
