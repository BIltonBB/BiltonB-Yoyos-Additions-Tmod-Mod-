using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Tech;

namespace BiltonsYoyoMod.Items.Tech
{
	/// <summary>
	/// Swing Yoyo Tech: hold and use to toggle. While ON, yoyos swing
	/// Tinkerer-style arcs at 2x damage (full spin with Glove, wider with
	/// counterweights). Only one tech at a time. Never consumed.
	/// </summary>
	public class SwingYoyoTech : ModItem
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
			Item.rare = ItemRarityID.Green;
			Item.value = Item.buyPrice(gold: 8);
		}

		public override bool AltFunctionUse(Player player) => true;

		public override bool CanUseItem(Player player) => true;

		public override bool? UseItem(Player player)
		{
			// Either click while held toggles. Nothing is consumed or moved.
			player.GetModPlayer<YoyoTechPlayer>().SetTech(YoyoTechType.Swing);
			return true;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			bool on = false;
			if (Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers) {
				Player local = Main.player[Main.myPlayer];
				if (local != null && local.active)
					on = local.GetModPlayer<YoyoTechPlayer>().ActiveTech == YoyoTechType.Swing;
			}
			tooltips.Add(new TooltipLine(Mod, "TechState", on ? "Status: ON" : "Status: OFF") {
				OverrideColor = on ? Microsoft.Xna.Framework.Color.Lime : Microsoft.Xna.Framework.Color.Gray
			});
			tooltips.Add(new TooltipLine(Mod, "TechHow", "Hold and use to toggle. Cycle Tech hotkey works too."));
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Cobweb, 30)
				.AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 15)
				.AddIngredient(ItemID.Bone, 10)
				.AddIngredient(ModContent.ItemType<Materials.YoyoParts>(), 5)
				.AddIngredient(ModContent.ItemType<TierEmblems.T4Emblem>(), 1)
				.AddTile(TileID.Anvils)
				.AddCondition(Condition.DownedSkeletron)
				.Register();
		}
	}
}
