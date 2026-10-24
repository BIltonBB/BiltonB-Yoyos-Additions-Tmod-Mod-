using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Tech;

namespace BiltonsYoyoMod.Items.Tech
{
	/// <summary>
	/// Summon Yoyo Tech: hold and use to toggle. While ON, throws become
	/// minions at 0.6x damage. Minions linger 5 min after tech off/switch;
	/// the buff icon dismisses them all. Only one tech at a time.
	/// </summary>
	public class SummonYoyoTech : ModItem
	{
		public override void SetStaticDefaults()
		{
			ItemID.Sets.CanBePlacedOnWeaponRacks[Type] = true;
		}

		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 28;
			Item.maxStack = 1;
			Item.consumable = false;
			Item.accessory = false;
			Item.useStyle = ItemUseStyleID.HoldUp;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.UseSound = SoundID.MenuTick;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.buyPrice(gold: 15);
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool CanUseItem(Player player) => true;

		public override bool? UseItem(Player player)
		{
			// Either click while held toggles. Nothing is consumed or moved.
			player.GetModPlayer<YoyoTechPlayer>().SetTech(YoyoTechType.Summon);
			return true;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			bool on = false;
			if (Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers) {
				Player local = Main.player[Main.myPlayer];
				if (local != null && local.active)
					on = local.GetModPlayer<YoyoTechPlayer>().ActiveTech == YoyoTechType.Summon;
			}
			tooltips.Add(new TooltipLine(Mod, "TechState", on ? "Status: ON" : "Status: OFF") {
				OverrideColor = on ? Microsoft.Xna.Framework.Color.Lime : Microsoft.Xna.Framework.Color.Gray
			});
			tooltips.Add(new TooltipLine(Mod, "TechHow", "Hold and use to toggle. Cycle Tech hotkey works too."));
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Silk, 20)
				.AddRecipeGroup("BiltonsYoyoMod:AnyMythrilBar", 10)
				.AddIngredient(ItemID.SoulofFlight, 10)
				.AddIngredient(ModContent.ItemType<Materials.AdvancedYoyoParts>(), 5)
				.AddIngredient(ModContent.ItemType<TierEmblems.T6Emblem>(), 1)
				.AddTile(TileID.MythrilAnvil)
				.AddCondition(Condition.Hardmode)
				.Register();
		}
	}
}
