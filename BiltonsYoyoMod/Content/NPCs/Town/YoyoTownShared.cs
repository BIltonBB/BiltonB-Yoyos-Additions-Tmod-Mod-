using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.NPCs.Town
{
	// Shared helpers for the revamp Town NPCs (Yoyo Master, Yoyo Tinkerer,
	// Ancient Yo-yoist, Six Stringer, Yoyo Cultist).
	public static class YoyoTownShared
	{
		// String shader ids copied off the vanilla string items at runtime:
		// whatever vanilla uses to draw its own colored strings, guaranteed
		// valid (never a blind dye lookup).
		private static int yellowShader;
		private static int purpleShader;
		private static int blackShader;
		private static int silverShader;
		private static bool stringShadersCached;

		public static int YellowStringShader { get { EnsureStringShaders(); return yellowShader; } }
		public static int PurpleStringShader { get { EnsureStringShaders(); return purpleShader; } }
		public static int BlackStringShader { get { EnsureStringShaders(); return blackShader; } }
		public static int SilverStringShader { get { EnsureStringShaders(); return silverShader; } }

		private static void EnsureStringShaders()
		{
			if (stringShadersCached)
				return;
			stringShadersCached = true;
			yellowShader = StringShaderOf(ItemID.YellowString, ItemID.YellowDye);
			purpleShader = StringShaderOf(ItemID.PurpleString, ItemID.PurpleDye);
			blackShader = StringShaderOf(ItemID.BlackString, ItemID.BlackDye);
			// No vanilla gray string exists: silver dye mapping it is.
			silverShader = GameShaders.Armor.GetShaderIdFromItemId(ItemID.SilverDye);
			ModContent.GetInstance<global::BiltonsYoyoMod.BiltonsYoyoMod>().Logger.Info(
				$"String shaders cached: yellow={yellowShader} purple={purpleShader} black={blackShader} silver={silverShader}");
		}

		private static int StringShaderOf(int stringItemId, int dyeItemId)
		{
			try {
				Item tmp = new Item();
				tmp.SetDefaults(stringItemId);
				if (tmp.stringColor != 0)
					return tmp.stringColor;
			}
			catch {
			}
			return GameShaders.Armor.GetShaderIdFromItemId(dyeItemId);
		}

		// True if the item is a yoyo (vanilla or modded).
		// Lifetime 0 = never registered as a yoyo; -1 = infinite
		// flight time, which is still a yoyo.
		public static bool IsYoyo(Item item)
		{
			if (item == null || item.IsAir || item.shoot <= ProjectileID.None)
				return false;
			float[] lifetime = ProjectileID.Sets.YoyosLifeTimeMultiplier;
			return item.shoot < lifetime.Length && lifetime[item.shoot] != 0f;
		}

		// Item-shape check AND-ed with a real yoyo registration. The shape test
		// alone (MeleeNoSpeed + channel + noMelee + shoot) ALSO matches chainsaws,
		// drills, flails and jousting lances, so it must never be used on its own.
		// A weapon is only a yoyo here if it is also registered as one: either in
		// ItemID.Sets.Yoyo (vanilla yoyos + this mod's, via YoyoSetsGlobalItem) or
		// by its projectile's lifetime multiplier (the tML-standard registration
		// every real yoyo mod, including this one, writes).
		public static bool IsYoyoShape(Item item)
		{
			if (item == null || item.IsAir)
				return false;
			if (!(item.DamageType == DamageClass.MeleeNoSpeed
				&& item.channel
				&& item.noMelee
				&& item.shoot > ProjectileID.None
				&& !item.accessory))
				return false;
			// Tools that merely share the shape never qualify. A drill or a
			// chainsaw IS channelled MeleeNoSpeed with no melee hitbox, and its
			// projectile is registered in YoyosLifeTimeMultiplier exactly like a
			// yoyo's, so the lifetime registration on its own let AdamantiteDrill
			// and MythrilChainsaw through as "yoyos".
			if (item.pick > 0 || item.axe > 0 || item.hammer > 0)
				return false;
			// The authoritative registration: vanilla's own yoyo set, which this
			// mod's yoyos join via YoyoSetsGlobalItem. Flails, jousting lances
			// and spin-attack swords are not in it, so DaoofPow, The Meatball and
			// Terragrim stop counting as yoyos here.
			if (item.type > ItemID.None
				&& item.type < ItemID.Sets.Yoyo.Length
				&& ItemID.Sets.Yoyo[item.type])
				return true;
			// Other mods' yoyos: the tML-standard registration is the yoyo
			// lifetime multiplier, and it is trusted only for MODDED items - a
			// vanilla item that reaches this point is a flail or a sword.
			return item.ModItem != null && IsYoyo(item);
		}

		// Counts how many yoyos (total stack) a player carries (inventory + armor/accessories).
		public static int CountYoyos(Player player)
		{
			int count = 0;
			foreach (Item item in player.inventory) {
				if (IsYoyo(item))
					count += item.stack;
			}
			return count;
		}

		// Counts how many DIFFERENT yoyo types a player carries.
		public static int CountDistinctYoyos(Player player)
		{
			var seen = new HashSet<int>();
			foreach (Item item in player.inventory) {
				if (IsYoyo(item))
					seen.Add(item.type);
			}
			return seen.Count;
		}

		// True while the player's MAIN yoyo (lowest slot = first thrown)
		// is deployed. Duplicates alone don't count.
		public static bool HasMainYoyo(Player player)
		{
			Projectile best = null;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (proj.active && proj.owner == player.whoAmI && proj.aiStyle == ProjAIStyleID.Yoyo) {
					if (best == null || i < best.whoAmI)
						best = proj;
				}
			}
			return best != null;
		}

		// True while any of the player's yoyos is deployed.
		public static bool HasActiveYoyo(Player player)
		{
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (proj.active && proj.owner == player.whoAmI && proj.aiStyle == ProjAIStyleID.Yoyo)
					return true;
			}
			return false;
		}

		// The main yoyo is the owner's lowest-slot active yoyo: glove/bag
		// duplicates spawn after it, so checks against it stay single.
		public static bool IsMainYoyo(Projectile projectile)
		{
			for (int i = 0; i < Main.maxProjectiles; i++) {
				if (i == projectile.whoAmI)
					return true;
				Projectile other = Main.projectile[i];
				if (other.active && other.owner == projectile.owner && other.aiStyle == ProjAIStyleID.Yoyo)
					return false;
			}
			return true;
		}

		public static bool AnyPlayerHasYoyo()
		{
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (player != null && player.active && CountYoyos(player) > 0)
					return true;
			}
			return false;
		}

		public static bool AnyPlayerHasDistinctYoyos(int amount)
		{
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (player != null && player.active && CountDistinctYoyos(player) >= amount)
					return true;
			}
			return false;
		}

		// Daily-cycling shop helper: picks one entry based on the world day count.
		public static int DayCount => (int)(Main.GameUpdateCount / 86400L);

		public static T PickDaily<T>(IReadOnlyList<T> options)
		{
			if (options == null || options.Count == 0)
				return default;
			int index = ((DayCount % options.Count) + options.Count) % options.Count;
			return options[index];
		}

		// Tries to consume count of itemType from the player's inventory. Returns success.
		public static bool TryConsumeFromInventory(Player player, int itemType, int count)
		{
			int remaining = count;
			for (int i = 0; i < player.inventory.Length && remaining > 0; i++) {
				Item item = player.inventory[i];
				if (item != null && !item.IsAir && item.type == itemType) {
					int take = System.Math.Min(item.stack, remaining);
					item.stack -= take;
					remaining -= take;
					if (item.stack <= 0)
						item.TurnToAir();
				}
			}
			return remaining <= 0;
		}

		public static Item Priced(int itemType, int copperPrice)
		{
			var item = new Item(itemType);
			item.shopCustomPrice = copperPrice;
			return item;
		}

		public static Item Priced<T>(int copperPrice) where T : ModItem => Priced(ModContent.ItemType<T>(), copperPrice);

		public static int CountInInventory(Player player, int itemType)
		{
			int count = 0;
			foreach (Item item in player.inventory) {
				if (item != null && !item.IsAir && item.type == itemType)
					count += item.stack;
			}
			return count;
		}
	}

	// Shared ground movement for the non-town night visitors (Six Stringer,
	// Yoyo Cultist) so they walk smoothly, face travel direction, step up
	// single blocks and idle-stand instead of moonwalking.
	public static class VisitorMovement
	{
		public static void ApplyGravity(NPC npc)
		{
			npc.velocity.Y += 0.35f;
			if (npc.velocity.Y > 8f)
				npc.velocity.Y = 8f;
			if (npc.collideY && npc.velocity.Y > 0f)
				npc.velocity.Y = 0f;
		}

		public static bool Standing(NPC npc) => npc.collideY && npc.velocity.Y == 0f;

		// Uses npc.ai[0] as retarget timer and npc.ai[2] as walk direction.
		public static void Wander(NPC npc, Player player, float walkSpeed, float stickRange)
		{
			bool standing = Standing(npc);
			if (standing)
				npc.velocity.X *= 0.7f; // Firm ground friction: no ice-skating.
			else
				npc.velocity.X *= 0.98f; // Mild air drag.

			npc.ai[0]--;
			if (npc.ai[0] <= 0) {
				npc.ai[0] = Main.rand.Next(140, 320);
				float dir = 0f; // Idle stand half the time.
				if (player != null && player.active && !player.dead) {
					float dx = player.Center.X - npc.Center.X;
					if (System.Math.Abs(dx) > stickRange)
						dir = System.Math.Sign(dx);
					else if (Main.rand.NextBool())
						dir = Main.rand.NextBool() ? 1f : -1f;
				}
				else if (Main.rand.NextBool()) {
					dir = Main.rand.NextBool() ? 1f : -1f;
				}
				npc.ai[2] = dir;
				npc.netUpdate = true;
			}

			float want = npc.ai[2] * walkSpeed;
			npc.velocity.X = MathHelper.Lerp(npc.velocity.X, want, standing ? 0.15f : 0.06f);
			if (System.Math.Abs(npc.velocity.X) > walkSpeed)
				npc.velocity.X = System.Math.Sign(npc.velocity.X) * walkSpeed;

			// Step up single blocks; hop only if truly blocked.
			if (npc.collideX && standing) {
				bool stepped = false;
				try { Collision.StepUp(ref npc.position, ref npc.velocity, npc.width, npc.height, ref npc.stepSpeed, ref npc.gfxOffY); stepped = true; }
				catch { stepped = false; }
				if (!stepped || npc.collideX)
					npc.velocity.Y = -5.5f;
			}
		}

		public static void Face(NPC npc, Player player)
		{
			int dirSign = 0;
			if (System.Math.Abs(npc.velocity.X) > 0.4f)
				dirSign = System.Math.Sign(npc.velocity.X);
			else if (player != null && player.active && player.Center.X != npc.Center.X)
				dirSign = System.Math.Sign(player.Center.X - npc.Center.X);
			if (dirSign != 0) {
				npc.direction = dirSign;
				npc.spriteDirection = dirSign; // Art faces left at rest; flipped when heading right.
			}
		}
	}
}
