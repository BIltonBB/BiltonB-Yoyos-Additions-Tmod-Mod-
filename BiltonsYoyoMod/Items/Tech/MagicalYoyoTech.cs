using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using BiltonsYoyoMod.Content.Tech;

namespace BiltonsYoyoMod.Items.Tech
{
	/// <summary>
	/// Magical Yoyo Tech: hold and use to toggle. While ON, each click deploys
	/// one orbiting mirror guard (two with a Yoyo Glove; cap 3/6) that draws the
	/// selected yoyo's sprite. Hold right-click: Orbital Convergence - the
	/// guards charge a core and release a sustained beam (max 5s).
	/// Only one tech at a time. Never consumed.
	/// </summary>
	public class MagicalYoyoTech : ModItem
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
			player.GetModPlayer<YoyoTechPlayer>().SetTech(YoyoTechType.Magical);
			return true;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			bool on = false;
			if (Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers) {
				Player local = Main.player[Main.myPlayer];
				if (local != null && local.active)
					on = local.GetModPlayer<YoyoTechPlayer>().ActiveTech == YoyoTechType.Magical;
			}
			tooltips.Add(new TooltipLine(Mod, "TechState", on ? "Status: ON" : "Status: OFF") {
				OverrideColor = on ? Microsoft.Xna.Framework.Color.Lime : Microsoft.Xna.Framework.Color.Gray
			});
			tooltips.Add(new TooltipLine(Mod, "TechHow", "Hold and use to toggle. Cycle Tech hotkey works too."));
			bool abilities = MagicalOrbit.AbilitiesEnabled;
			tooltips.Add(new TooltipLine(Mod, "TechAbility", abilities
				? "Right-click: charge this yoyo's ability (drains mana while held)."
				: "Right-click ability: OFF (enable it in Mod Configs -> MagicalTech).") {
				OverrideColor = abilities ? new Microsoft.Xna.Framework.Color(170, 215, 255) : Microsoft.Xna.Framework.Color.Gray
			});
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Silk, 20)
				.AddIngredient(ItemID.SoulofLight, 10)
				.AddIngredient(ItemID.SoulofNight, 10)
				.AddIngredient(ModContent.ItemType<Materials.MagicalEssence>(), 2)
				.AddIngredient(ModContent.ItemType<TierEmblems.T6Emblem>(), 1)
				.AddTile(TileID.MythrilAnvil)
				.AddCondition(Condition.Hardmode)
				.Register();
		}
	}
}
