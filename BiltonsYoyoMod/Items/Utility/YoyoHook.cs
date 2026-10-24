using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Utility
{
	/// <summary>
	/// Yoyo Hook: a momentum swing grapple. It fires a head that latches to solid
	/// tiles; unlike a normal hook it never reels the player in, so you swing on
	/// gravity and rope tension instead. While attached, W/S shorten/lengthen the
	/// rope and A/D steer the swing horizontally. Press again (or jump) to detach
	/// and keep your momentum.
	/// </summary>
	public class YoyoHook : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 32;
			Item.height = 32;
			Item.maxStack = 1;
			Item.consumable = false;
			Item.noUseGraphic = true;
			Item.noMelee = true;
			Item.damage = 0;
			Item.knockBack = 0f;
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.useTime = 16;
			Item.useAnimation = 16;
			Item.autoReuse = false;
			Item.UseSound = SoundID.Item1;
			Item.shoot = ModContent.ProjectileType<Projectiles.Utility.YoyoHookProj>();
			Item.shootSpeed = 16f;
			Item.rare = ItemRarityID.Blue;
			Item.value = Item.buyPrice(gold: 1);
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
			Vector2 velocity, int type, int damage, float knockback)
		{
			// Pressing use while a hook is out releases it (momentum is preserved
			// because nothing touches the player's velocity).
			if (Projectiles.Utility.YoyoHookProj.ReleaseHook(player.whoAmI))
				return false;

			Vector2 dir = velocity.SafeNormalize(Vector2.UnitX * player.direction);
			int idx = Projectile.NewProjectile(source, player.MountedCenter, dir * Item.shootSpeed,
				type, 0, 0f, player.whoAmI);
			if (idx >= 0 && idx < Main.maxProjectiles)
			{
				Main.projectile[idx].netUpdate = true;
				player.GetModPlayer<global::BiltonsYoyoMod.Players.YoyoHookPlayer>().HookIndex = idx;
			}
			return false;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 12)
				.AddIngredient(ItemID.Chain, 3)
				.AddIngredient(ItemID.Silk, 6)
				.AddIngredient(ItemID.FallenStar, 2)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}
