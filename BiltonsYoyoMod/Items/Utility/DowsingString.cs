using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using BiltonsYoyoMod.Content.Storage;
using BiltonsYoyoMod.UI;

namespace BiltonsYoyoMod.Items.Utility
{
	/// <summary>Finder modes, in wheel order. Off is a real mode so a spare
	/// string can be silenced without stashing it.</summary>
	public enum DowsingMode
	{
		Off = 0,
		Gear = 1,
		Critters = 2,
		Chests = 3,
		Town = 4,
		Shimmer = 5,
	}

	public enum DowsingKind
	{
		None = 0,
		Item,
		Critter,
		Chest,
		Town,
		Shimmer,
	}

	/// <summary>One scanned finder target. The struct is copied into the draw
	/// layer so drawing never touches live world arrays.</summary>
	public struct DowsingTarget
	{
		public DowsingKind Kind;
		public Vector2 World;
		public string Label;
		public float Distance;
		public int ItemType;
		public int Entity;
		public int TileX;
		public int TileY;
	}

	/// <summary>
	/// Dowsing String (utility, zero stats, NOT an accessory): a per-copy
	/// finder with five modes and four tiers. It marks loose yoyo gear,
	/// yoyo critters, chests holding yoyo gear, yoyo town NPCs or shimmer
	/// pools. Tier 1 adds Sonar Ping, Tier 2 adds the multi-target radar,
	/// Tier 3 adds shimmer and the longest range. Finder only: never pulls,
	/// never buffs, never consumes itself.
	/// </summary>
	public class DowsingString : ModItem
	{
		/// <summary>Per-copy mode. Saved + synced. Old saves migrate ON to
		/// Gear and OFF to Off.</summary>
		public DowsingMode Mode = DowsingMode.Gear;
		/// <summary>Per-copy upgrade tier, 0..3. Upgrades stay on this item;
		/// no second dowsing item is ever created.</summary>
		public int Tier;

		public bool Enabled => Mode != DowsingMode.Off;

		public override void SetDefaults()
		{
			Item.width = 26;
			Item.height = 26;
			Item.maxStack = 1;
			Item.consumable = false;
			Item.accessory = false;
			Item.rare = ItemRarityID.Green;
			Item.value = Item.buyPrice(gold: 3);
		}

		public static bool ModeUnlocked(DowsingMode mode, int tier)
		{
			switch (mode) {
				case DowsingMode.Off:
				case DowsingMode.Gear:
				case DowsingMode.Chests:
					return true;
				case DowsingMode.Critters:
					return tier >= 1;
				case DowsingMode.Town:
					return tier >= 2;
				case DowsingMode.Shimmer:
					return tier >= 3;
				default:
					return false;
			}
		}

		public static float RangeTiles(int tier) => tier >= 3 ? 240f : tier >= 1 ? 200f : 160f;

		public static float RangePixels(int tier) => RangeTiles(tier) * 16f;

		public static int RadarTargets(int tier) => tier >= 3 ? 5 : tier >= 2 ? 3 : 1;

		/// <summary>Sonar is a short reveal, not a permanent radar upgrade, so
		/// it opens the cap for three seconds and then falls back.</summary>
		public static int SonarTargets(int tier) => Math.Max(RadarTargets(tier), 6);

		public static string ModeName(DowsingMode mode)
		{
			switch (mode) {
				case DowsingMode.Gear: return "GEAR";
				case DowsingMode.Critters: return "CRITTERS";
				case DowsingMode.Chests: return "CHESTS";
				case DowsingMode.Town: return "TOWN";
				case DowsingMode.Shimmer: return "SHIMMER";
				default: return "OFF";
			}
		}

		public static string UpgradeText(int targetTier)
		{
			switch (targetTier) {
				case 1: return "12 Silk, 5 Fallen Stars";
				case 2: return "12 Silk, 5 Gold/Platinum Bars, 3 Amethysts";
				case 3: return "12 Silk, 5 Souls of Light/Night, 3 Crystal Shards";
				default: return "";
			}
		}

		private static bool ShiftHeld =>
			Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);

		public override bool CanRightClick() => true;

		// Right-click is a mode/upgrade control, never a use. Returning false
		// from the documented ConsumeItem hook keeps the string intact without
		// the old stack-refund trick (which briefly pushed a maxStack-1 item
		// to 2 before tModLoader decremented it again).
		public override bool ConsumeItem(Player player) => false;

		public override void RightClick(Player player)
		{
			if (player == null || player.whoAmI != Main.myPlayer || Main.dedServ)
				return;
			if (ShiftHeld)
				TryUpgrade(player);
			else
				CycleMode(player);
		}

		private void CycleMode(Player player)
		{
			int next = (int)Mode;
			for (int i = 0; i < 6; i++) {
				next = (next + 1) % 6;
				if (ModeUnlocked((DowsingMode)next, Tier))
					break;
			}
			Mode = (DowsingMode)next;
			Main.NewText($"Dowsing String: {ModeName(Mode)}  (Tier {Tier})", 150, 230, 255);
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.45f, Pitch = Mode == DowsingMode.Off ? -0.20f : 0.12f,
				MaxInstances = 2,
			}, player.Center);
		}

		private void TryUpgrade(Player player)
		{
			if (Tier >= 3) {
				Main.NewText("Dowsing String: already fully upgraded (Tier 3).", 180, 200, 220);
				return;
			}
			int target = Tier + 1;
			if (!HasUpgradeMaterials(player, target)) {
				Main.NewText($"Dowsing String: Tier {target} needs {UpgradeText(target)}.",
					236, 196, 140);
				SoundEngine.PlaySound(SoundID.MenuTick with {
					Volume = 0.30f, Pitch = -0.30f, MaxInstances = 2,
				}, player.Center);
				return;
			}
			ConsumeUpgradeMaterials(player, target);
			Tier = target;
			string unlock = Tier == 1 ? ", critters + sonar ping"
				: Tier == 2 ? ", town + radar x3"
				: ", shimmer + radar x5";
			Main.NewText($"Dowsing String upgraded to Tier {Tier}: "
				+ $"{RangeTiles(Tier):0} tiles{unlock}.", 150, 230, 255);
			SoundEngine.PlaySound(SoundID.Item4 with {
				Volume = 0.55f, Pitch = 0.18f, MaxInstances = 2,
			}, player.Center);
		}

		private static bool HasUpgradeMaterials(Player player, int target)
		{
			switch (target) {
				case 1:
					return player.CountItem(ItemID.Silk) >= 12
						&& player.CountItem(ItemID.FallenStar) >= 5;
				case 2:
					return player.CountItem(ItemID.Silk) >= 12
						&& player.CountItem(ItemID.GoldBar) + player.CountItem(ItemID.PlatinumBar) >= 5
						&& player.CountItem(ItemID.Amethyst) >= 3;
				case 3:
					return player.CountItem(ItemID.Silk) >= 12
						&& player.CountItem(ItemID.SoulofLight) + player.CountItem(ItemID.SoulofNight) >= 5
						&& player.CountItem(ItemID.CrystalShard) >= 3;
				default:
					return false;
			}
		}

		private static void ConsumeUpgradeMaterials(Player player, int target)
		{
			Consume(player, ItemID.Silk, 12);
			switch (target) {
				case 1:
					Consume(player, ItemID.FallenStar, 5);
					break;
				case 2:
					ConsumeAny(player, ItemID.GoldBar, ItemID.PlatinumBar, 5);
					Consume(player, ItemID.Amethyst, 3);
					break;
				case 3:
					ConsumeAny(player, ItemID.SoulofLight, ItemID.SoulofNight, 5);
					Consume(player, ItemID.CrystalShard, 3);
					break;
			}
		}

		private static void Consume(Player player, int type, int count)
		{
			for (int i = 0; i < count; i++)
				player.ConsumeItem(type);
		}

		private static void ConsumeAny(Player player, int a, int b, int count)
		{
			int fromA = Math.Min(count, player.CountItem(a));
			for (int i = 0; i < fromA; i++)
				player.ConsumeItem(a);
			for (int i = 0; i < count - fromA; i++)
				player.ConsumeItem(b);
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			string status = Mode == DowsingMode.Off
				? "OFF"
				: $"{ModeName(Mode)}  |  Tier {Tier}/3";
			tooltips.Add(new TooltipLine(Mod, "DowsingState", $"Mode: {status}") {
				OverrideColor = Mode == DowsingMode.Off ? Color.Gray : Color.Lime
			});
			tooltips.Add(new TooltipLine(Mod, "DowsingHow",
				"Right-click cycles mode. Shift+right-click upgrades. Never consumed."));
			tooltips.Add(new TooltipLine(Mod, "DowsingScan",
				$"Range {RangeTiles(Tier):0} tiles  |  Radar x{RadarTargets(Tier)}"
					+ (Tier >= 1 ? "  |  Sonar: assign a key in Controls" : "")) {
				OverrideColor = new Color(174, 190, 210)
			});
			if (Tier < 3) {
				tooltips.Add(new TooltipLine(Mod, "DowsingUpgrade",
					$"Tier {Tier + 1}: {UpgradeText(Tier + 1)}") {
					OverrideColor = new Color(226, 195, 126)
				});
			}
			if (Mode != DowsingMode.Off && DowsingSystem.HasTarget) {
				tooltips.Add(new TooltipLine(Mod, "DowsingTarget",
					$"Tracking: {DowsingSystem.TargetLabel}  {DowsingSystem.TargetDistance / 16f:0} tiles") {
					OverrideColor = new Color(126, 224, 255)
				});
			}
		}

		public override ModItem Clone(Item newEntity)
		{
			var clone = (DowsingString)base.Clone(newEntity);
			clone.Mode = Mode;
			clone.Tier = Tier;
			return clone;
		}

		public override void SaveData(TagCompound tag)
		{
			tag["dowsingMode"] = (int)Mode;
			tag["dowsingTier"] = Tier;
		}

		public override void LoadData(TagCompound tag)
		{
			if (tag.ContainsKey("dowsingMode")) {
				Mode = (DowsingMode)Math.Clamp(tag.GetInt("dowsingMode"), 0, 5);
			}
			else {
				// Legacy saves only stored a boolean. ON was the old finder;
				// keep it on the closest equivalent mode instead of resetting.
				Mode = !tag.ContainsKey("enabled") || tag.GetBool("enabled")
					? DowsingMode.Gear
					: DowsingMode.Off;
			}
			Tier = Math.Clamp(tag.GetInt("dowsingTier"), 0, 3);
			if (!ModeUnlocked(Mode, Tier))
				Mode = DowsingMode.Gear;
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write((byte)Mode);
			writer.Write((byte)Tier);
		}

		public override void NetReceive(BinaryReader reader)
		{
			Mode = (DowsingMode)Math.Clamp((int)reader.ReadByte(), 0, 5);
			Tier = Math.Clamp((int)reader.ReadByte(), 0, 3);
			// Never trust a synced mode the tier has not unlocked.
			if (!ModeUnlocked(Mode, Tier))
				Mode = DowsingMode.Gear;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Cobweb, 20)
				.AddIngredient(ItemID.Silk, 6)
				.AddIngredient(ItemID.FallenStar, 3)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}

	public class DowsingPlayer : ModPlayer
	{
		private const int InventorySlots = 50;

		public bool Active;
		public int Tier;
		public DowsingMode Mode = DowsingMode.Off;
		/// <summary>Sonar reveal: 3 seconds of wider radar.</summary>
		public int SonarTicks;
		/// <summary>Trailing fade after the reveal, so the wider radar eases
		/// out instead of snapping off. 60 ticks (1 second).</summary>
		public int SonarFade;
		/// <summary>Total ticks since the ping, reveal + fade. Drives the
		/// expanding ring animation without restarting mid-effect.</summary>
		public int SonarAge;
		/// <summary>Five-second gate between pulses.</summary>
		public int SonarCooldown;

		public const int SonarRevealTicks = 180;
		public const int SonarFadeTicks = 60;

		public bool SonarActive => SonarTicks > 0 || SonarFade > 0;

		/// <summary>0..1 envelope: short fade-in as the ping lands, then a
		/// smooth one-second fade once the reveal expires.</summary>
		public float SonarIntensity {
			get {
				if (SonarTicks > 0) {
					int elapsed = SonarRevealTicks - SonarTicks;
					float rise = MathHelper.Clamp(elapsed / 12f, 0f, 1f);
					return rise * rise * (3f - 2f * rise);
				}
				if (SonarFade > 0)
					return MathHelper.Clamp(SonarFade / (float)SonarFadeTicks, 0f, 1f);
				return 0f;
			}
		}

		public override void ResetEffects()
		{
			Active = false;
			Mode = DowsingMode.Off;
			Tier = 0;
		}

		public override void PostUpdate()
		{
			// Finder state is local-only; remote inventories should not drive it.
			if (Player.whoAmI != Main.myPlayer || Main.dedServ)
				return;

			if (SonarTicks > 0) {
				SonarTicks--;
				SonarAge++;
				if (SonarTicks == 0)
					SonarFade = SonarFadeTicks;
			}
			else if (SonarFade > 0) {
				SonarFade--;
				SonarAge++;
			}
			if (SonarCooldown > 0)
				SonarCooldown--;

			if (Player.dead || !Player.active) {
				SonarTicks = 0;
				SonarFade = 0;
				SonarAge = 0;
				return;
			}

			// A player can carry an old Tier 0 string and a newer upgraded
			// one. Use the best tier among the switched-on strings instead of
			// whichever happens to sit first in the inventory.
			DowsingString chosen = null;
			for (int i = 0; i < InventorySlots; i++) {
				Item item = Player.inventory[i];
				if (item == null || item.IsAir || item.ModItem is not DowsingString ds)
					continue;
				if (ds.Mode == DowsingMode.Off)
					continue;
				if (chosen == null || ds.Tier > chosen.Tier)
					chosen = ds;
			}
			if (chosen != null) {
				Active = true;
				Tier = chosen.Tier;
				Mode = chosen.Mode;
			}

			if (!Active) {
				SonarTicks = 0;
				SonarFade = 0;
				SonarAge = 0;
			}
		}

		/// <summary>Soft 3-second reveal. Purely informational: it never
		/// pulls items, marks NPCs or changes spawns. The string is read
		/// straight from the inventory because key triggers can run before
		/// this tick's cached finder state has been rebuilt.</summary>
		public void PingSonar(Player player)
		{
			if (player == null || player.whoAmI != Main.myPlayer || Main.dedServ)
				return;
			DowsingMode mode = DowsingMode.Off;
			int tier = 0;
			// Match the tick cache: the highest-tier switched-on string wins.
			for (int i = 0; i < 50 && i < player.inventory.Length; i++) {
				Item item = player.inventory[i];
				if (item == null || item.IsAir || item.ModItem is not DowsingString ds)
					continue;
				if (ds.Mode == DowsingMode.Off || ds.Tier < tier)
					continue;
				mode = ds.Mode;
				tier = ds.Tier;
			}
			if (mode == DowsingMode.Off)
				return;
			if (tier < 1) {
				Main.NewText("Dowsing String: Sonar Ping unlocks at Tier 1 "
					+ "(Shift+right-click to upgrade).", 200, 210, 225);
				return;
			}
			if (SonarTicks > 0 || SonarCooldown > 0)
				return;
			SonarTicks = SonarRevealTicks;
			SonarFade = 0;
			SonarAge = 0;
			SonarCooldown = 300;
			SoundEngine.PlaySound(SoundID.Item4 with {
				Volume = 0.45f, Pitch = 0.30f, MaxInstances = 2,
			}, player.Center);
		}
	}

	/// <summary>One scan per tick, reused by the draw layer. Chests and
	/// shimmer rescan on a short cadence because they are expensive; loose
	/// items and NPCs are cheap enough to scan every tick.</summary>
	public class DowsingSystem : ModSystem
	{
		private static readonly List<DowsingTarget> found = new();
		public static readonly List<DowsingTarget> Targets = new();

		private static readonly Dictionary<int, bool> gearCache = new();
		private static readonly Dictionary<int, int> rankCache = new();
		private static readonly List<DowsingTarget> chestCache = new();
		private static int chestTimer;
		private static DowsingTarget shimmerTarget;
		private static bool shimmerFound;
		private static int shimmerTimer;

		private static DowsingKind stickyKind;
		private static int stickyEntity;
		private static int stickyType;
		private static int stickyTileX;
		private static int stickyTileY;
		private static DowsingMode lastMode = DowsingMode.Off;

		private static readonly string[] GearWords = {
			"Yoyo", "String", "Bearing", "Axle", "Emblem", "Counterweight",
			"Trick", "Case", "Backpack", "Ruck", "Vault", "Parts", "Scraps",
			"Tonic", "Spool",
		};

		public static bool HasTarget => Targets.Count > 0;
		public static DowsingTarget Primary => Targets.Count > 0 ? Targets[0] : default;
		public static Vector2 TargetWorld => Primary.World;
		public static string TargetLabel => Primary.Label ?? "";
		public static float TargetDistance => Primary.Distance;
		public static int TargetItemType => Primary.ItemType;
		public static DowsingKind TargetKind => Primary.Kind;

		/// <summary>Screen size in UI coordinates. Main.screenWidth changes
		/// meaning between update and draw, so use the same reconstructed
		/// viewport the Splicer and Guidebook hit-test against.</summary>
		public static Vector2 UiScreenSize
			=> new(UiViewport.Width, UiViewport.Height);

		/// <summary>World point to UI-layer screen coordinates, including zoom and
		/// UI scale. This mirrors the transform vanilla uses for world markers
		/// drawn inside the UI pass.</summary>
		public static Vector2 WorldToUi(Vector2 world)
		{
			float scale = Main.UIScale <= 0.01f ? 1f : Main.UIScale;
			return Vector2.Transform(world - Main.screenPosition,
				Main.GameViewMatrix.ZoomMatrix) / scale;
		}

		public override void PostUpdatePlayers()
		{
			Targets.Clear();
			if (Main.gameMenu || Main.dedServ) {
				ResetTracking();
				return;
			}

			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead) {
				ResetTracking();
				return;
			}
			var dowsing = player.GetModPlayer<DowsingPlayer>();
			if (dowsing == null || !dowsing.Active || dowsing.Mode == DowsingMode.Off) {
				ResetTracking();
				return;
			}

			if (dowsing.Mode != lastMode) {
				lastMode = dowsing.Mode;
				stickyKind = DowsingKind.None;
				// Chest and shimmer scans are cached. A mode switch can last
				// long enough for both to go stale, so force a fresh scan.
				chestTimer = 0;
				shimmerTimer = 0;
			}

			float range = DowsingString.RangePixels(dowsing.Tier);
			int cap = dowsing.SonarActive
				? DowsingString.SonarTargets(dowsing.Tier)
				: DowsingString.RadarTargets(dowsing.Tier);

			found.Clear();
			switch (dowsing.Mode) {
				case DowsingMode.Gear:
					ScanGear(player, range);
					break;
				case DowsingMode.Critters:
					ScanCritters(player, range);
					break;
				case DowsingMode.Chests:
					ScanChests(player, range);
					break;
				case DowsingMode.Town:
					ScanTown(player, range);
					break;
				case DowsingMode.Shimmer:
					ScanShimmer(player, range);
					break;
			}

			if (found.Count == 0) {
				stickyKind = DowsingKind.None;
				return;
			}

			found.Sort((a, b) => a.Distance.CompareTo(b.Distance));
			// Tag & Track: the marked target stays primary while it exists,
			// even if another target drifts closer. The tag only moves when
			// the target despawns, leaves range or the mode changes.
			int stickyIndex = found.FindIndex(IsSticky);
			if (stickyIndex > 0) {
				DowsingTarget sticky = found[stickyIndex];
				found.RemoveAt(stickyIndex);
				found.Insert(0, sticky);
			}

			int count = Math.Min(cap, found.Count);
			for (int i = 0; i < count; i++)
				Targets.Add(found[i]);

			DowsingTarget primary = Targets[0];
			stickyKind = primary.Kind;
			stickyEntity = primary.Entity;
			stickyType = primary.ItemType;
			stickyTileX = primary.TileX;
			stickyTileY = primary.TileY;
		}

		private static void ResetTracking()
		{
			stickyKind = DowsingKind.None;
			lastMode = DowsingMode.Off;
			found.Clear();
		}

		private static bool IsSticky(DowsingTarget target)
		{
			if (stickyKind == DowsingKind.None || target.Kind != stickyKind)
				return false;
			// Chests and shimmer pools have fixed tile coordinates, which is
			// stabler than an array slot (Main.chest order can shift and the
			// mod's own chests are keyed by tile).
			if (stickyKind == DowsingKind.Shimmer || stickyKind == DowsingKind.Chest)
				return target.TileX == stickyTileX && target.TileY == stickyTileY;
			// Entity slots are recycled. Matching the type as well keeps a
			// newly spawned item or NPC from inheriting the old tag.
			return target.Entity == stickyEntity && target.ItemType == stickyType;
		}

		private static void ScanGear(Player player, float range)
		{
			float rangeSq = range * range;
			for (int i = 0; i < Main.maxItems; i++) {
				Item item = Main.item[i];
				if (item == null || !item.active || item.IsAir)
					continue;
				if (!IsYoyoGear(item.type))
					continue;
				float d2 = Vector2.DistanceSquared(player.Center, item.Center);
				if (d2 > rangeSq)
					continue;
				found.Add(new DowsingTarget {
					Kind = DowsingKind.Item,
					World = item.Center,
					Label = ItemName(item.type),
					Distance = (float)Math.Sqrt(d2),
					ItemType = item.type,
					Entity = i,
				});
			}
		}

		private static void ScanCritters(Player player, float range)
		{
			float rangeSq = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active
					|| npc.ModNPC is not Content.NPCs.Critters.YoyoCritterBase)
					continue;
				float d2 = Vector2.DistanceSquared(player.Center, npc.Center);
				if (d2 > rangeSq)
					continue;
				found.Add(new DowsingTarget {
					Kind = DowsingKind.Critter,
					World = npc.Center,
					Label = NpcName(npc),
					Distance = (float)Math.Sqrt(d2),
					ItemType = npc.type,
					Entity = i,
				});
			}
		}

		private static void ScanChests(Player player, float range)
		{
			if (chestTimer <= 0) {
				chestTimer = 12;
				chestCache.Clear();
				float rangeSq = range * range;
				for (int i = 0; i < Main.chest.Length; i++) {
					Chest chest = Main.chest[i];
					if (chest == null)
						continue;
					Vector2 world = new(chest.x * 16f + 16f, chest.y * 16f + 16f);
					float d2 = Vector2.DistanceSquared(player.Center, world);
					if (d2 > rangeSq)
						continue;
					Item[] slots = chest.item;
					if (slots == null)
						continue;
					int bestType = BestChestItem(slots, slots.Length);
					if (bestType <= 0)
						continue;
					chestCache.Add(new DowsingTarget {
						Kind = DowsingKind.Chest,
						World = world,
						Label = "Chest  |  " + ItemName(bestType),
						Distance = (float)Math.Sqrt(d2),
						ItemType = bestType,
						Entity = i,
						TileX = chest.x,
						TileY = chest.y,
					});
				}

				// This mod's yoyo chests are world data, not Main.chest
				// entries. They are containers too, so CHESTS mode must see
				// them. Stale dictionary keys are skipped.
				foreach (var pair in Content.Storage.YoyoChestSystem.Chests) {
					Point key = pair.Key;
					if (!Content.Storage.YoyoChestSystem.IsTileStillChest(key))
						continue;
					Vector2 world = new(key.X * 16f + 16f, key.Y * 16f + 16f);
					float d2 = Vector2.DistanceSquared(player.Center, world);
					if (d2 > rangeSq)
						continue;
					ChestData data = pair.Value;
					if (data?.Items == null)
						continue;
					int bestType = BestChestItem(data.Items, data.TotalSlots);
					if (bestType <= 0)
						continue;
					chestCache.Add(new DowsingTarget {
						Kind = DowsingKind.Chest,
						World = world,
						Label = "Yoyo Chest  |  " + ItemName(bestType),
						Distance = (float)Math.Sqrt(d2),
						ItemType = bestType,
						Entity = -1,
						TileX = key.X,
						TileY = key.Y,
					});
				}
			}
			else {
				chestTimer--;
			}

			float limitSq = range * range;
			foreach (DowsingTarget target in chestCache) {
				float d2 = Vector2.DistanceSquared(player.Center, target.World);
				if (d2 > limitSq)
					continue;
				DowsingTarget copy = target;
				copy.Distance = (float)Math.Sqrt(d2);
				found.Add(copy);
			}
		}

		/// <summary>Chest priority: 0 real yoyo, 1 other yoyo gear (cases,
		/// accessories, emblems), 2 tonics/spares, 99 not gear. Returns 0
		/// when the container holds nothing the finder cares about.</summary>
		private static int BestChestItem(Item[] slots, int count)
		{
			if (slots == null)
				return 0;
			// 99 is "not gear". Starting at 99 means a chest of plain blocks
			// and potions can never claim a slot.
			int bestRank = 99;
			int bestType = 0;
			int limit = Math.Min(Math.Max(0, count), slots.Length);
			for (int s = 0; s < limit; s++) {
				Item item = slots[s];
				if (item == null || item.IsAir)
					continue;
				int rank = GearRank(item.type);
				if (rank >= 99)
					continue;
				if (rank < bestRank) {
					bestRank = rank;
					bestType = item.type;
					// A real yoyo outranks everything; no need to keep
					// walking a full chest once one is found.
					if (rank == 0)
						break;
				}
			}
			return bestType;
		}

		private static void ScanTown(Player player, float range)
		{
			float rangeSq = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active || !npc.townNPC)
					continue;
				string ns = npc.ModNPC?.GetType().Namespace ?? string.Empty;
				if (!ns.Contains("BiltonsYoyoMod"))
					continue;
				float d2 = Vector2.DistanceSquared(player.Center, npc.Center);
				if (d2 > rangeSq)
					continue;
				found.Add(new DowsingTarget {
					Kind = DowsingKind.Town,
					World = npc.Center,
					Label = NpcName(npc),
					Distance = (float)Math.Sqrt(d2),
					ItemType = npc.type,
					Entity = i,
				});
			}
		}

		private static void ScanShimmer(Player player, float range)
		{
			if (shimmerTimer <= 0) {
				shimmerTimer = 20;
				shimmerFound = FindShimmer(player, range, out shimmerTarget);
			}
			else {
				shimmerTimer--;
			}
			if (!shimmerFound)
				return;
			float d2 = Vector2.DistanceSquared(player.Center, shimmerTarget.World);
			if (d2 > range * range)
				return;
			DowsingTarget copy = shimmerTarget;
			copy.Distance = (float)Math.Sqrt(d2);
			found.Add(copy);
		}

		/// <summary>Ring scan: walk outward in coarse steps and stop at the
		/// first ring that contains shimmer, then refine to one tile. This is
		/// far cheaper than sweeping the whole 480x480 square every tick and
		/// always returns the nearest pool edge.</summary>
		private static bool FindShimmer(Player player, float range, out DowsingTarget target)
		{
			target = default;
			const int step = 4;
			int max = Math.Max(step, (int)(range / 16f));
			int cx = (int)(player.Center.X / 16f);
			int cy = (int)(player.Center.Y / 16f);

			for (int r = step; r <= max; r += step) {
				int bx = 0;
				int by = 0;
				float best = float.MaxValue;
				bool hit = false;
				for (int dx = -r; dx <= r; dx += step) {
					CheckShimmer(cx + dx, cy - r, player, ref hit, ref best, ref bx, ref by);
					CheckShimmer(cx + dx, cy + r, player, ref hit, ref best, ref bx, ref by);
				}
				for (int dy = -r + step; dy <= r - step; dy += step) {
					CheckShimmer(cx - r, cy + dy, player, ref hit, ref best, ref bx, ref by);
					CheckShimmer(cx + r, cy + dy, player, ref hit, ref best, ref bx, ref by);
				}
				if (!hit)
					continue;

				int rx = bx;
				int ry = by;
				for (int dx = -step; dx <= step; dx++) {
					for (int dy = -step; dy <= step; dy++)
						CheckShimmer(rx + dx, ry + dy, player, ref hit, ref best, ref bx, ref by);
				}

				target = new DowsingTarget {
					Kind = DowsingKind.Shimmer,
					World = new Vector2(bx * 16f + 8f, by * 16f + 8f),
					Label = "Shimmer pool",
					TileX = bx,
					TileY = by,
				};
				return true;
			}
			return false;
		}

		private static void CheckShimmer(int x, int y, Player player,
			ref bool hit, ref float best, ref int bx, ref int by)
		{
			if (!WorldGen.InWorld(x, y, 1))
				return;
			Tile tile = Main.tile[x, y];
			if (tile == null || tile.LiquidAmount <= 0 || tile.LiquidType != LiquidID.Shimmer)
				return;
			float d2 = Vector2.DistanceSquared(player.Center,
				new Vector2(x * 16f + 8f, y * 16f + 8f));
			if (d2 >= best)
				return;
			best = d2;
			bx = x;
			by = y;
			hit = true;
		}

		private static string ItemName(int itemType)
		{
			try {
				string name = Lang.GetItemNameValue(itemType);
				if (!string.IsNullOrWhiteSpace(name))
					return name;
			}
			catch { }
			return "Yoyo gear";
		}

		private static string NpcName(NPC npc)
		{
			try {
				if (!string.IsNullOrWhiteSpace(npc.FullName))
					return npc.FullName;
			}
			catch { }
			try {
				string name = Lang.GetNPCNameValue(npc.netID);
				if (!string.IsNullOrWhiteSpace(name))
					return name;
			}
			catch { }
			return "Critter";
		}

		public static bool IsYoyoGear(int itemType)
		{
			if (itemType <= 0)
				return false;
			if (gearCache.TryGetValue(itemType, out bool cached))
				return cached;

			bool result = false;
			try {
				var probe = new Item();
				probe.SetDefaults(itemType);
				if (!probe.IsAir) {
					if (Content.NPCs.Town.YoyoTownShared.IsYoyoShape(probe)) {
						result = true;
					}
					else {
						string name = Lang.GetItemNameValue(itemType) ?? string.Empty;
						foreach (string word in GearWords) {
							if (name.Contains(word, StringComparison.OrdinalIgnoreCase)) {
								result = true;
								break;
							}
						}
					}
				}
			}
			catch {
				result = false;
			}
			gearCache[itemType] = result;
			return result;
		}

		/// <summary>Chest priority: 0 real yoyo, 1 other yoyo gear (cases,
		/// accessories, emblems), 2 tonics/spares, 99 not gear.</summary>
		public static int GearRank(int itemType)
		{
			if (itemType <= 0)
				return 99;
			if (rankCache.TryGetValue(itemType, out int cached))
				return cached;

			int rank = 99;
			try {
				var probe = new Item();
				probe.SetDefaults(itemType);
				if (!probe.IsAir) {
					if (Content.NPCs.Town.YoyoTownShared.IsYoyoShape(probe)) {
						rank = 0;
					}
					else if (IsTonicItem(itemType)) {
						// Match the five real tonic items by type. Name-based
						// matching missed "Cultist's Draught" and "Moonlight
						// Flask", and would also flag vanilla flasks.
						rank = 2;
					}
					else if (IsYoyoGear(itemType)) {
						rank = 1;
					}
				}
			}
			catch {
				rank = 99;
			}
			rankCache[itemType] = rank;
			return rank;
		}

		private static bool IsTonicItem(int itemType)
		{
			var tonics = TonicCatalog.All;
			for (int i = 0; i < tonics.Length; i++) {
				if (tonics[i].ItemType == itemType)
					return true;
			}
			return false;
		}

		public override void OnWorldUnload()
		{
			ResetCaches();
		}

		public override void Unload()
		{
			ResetCaches();
		}

		private static void ResetCaches()
		{
			Targets.Clear();
			found.Clear();
			chestCache.Clear();
			gearCache.Clear();
			rankCache.Clear();
			chestTimer = 0;
			shimmerTimer = 0;
			shimmerFound = false;
			stickyKind = DowsingKind.None;
			stickyEntity = 0;
			stickyType = 0;
			lastMode = DowsingMode.Off;
		}
	}

	public class DowsingDrawSystem : ModSystem
	{
		private static readonly Color Brass = new(219, 190, 120);
		private static readonly Color FinderCyan = new(126, 224, 255);
		private static readonly Color CritterGreen = new(150, 226, 150);
		private static readonly Color TownWarm = new(238, 198, 142);
		private static readonly Color ShimmerViolet = new(198, 154, 255);

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Dowsing String",
					() => {
						DrawFinders(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI));
		}

		private static void DrawFinders(SpriteBatch sb)
		{
			if (Main.gameMenu || Main.dedServ || Main.playerInventory || Main.mapFullscreen)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
				return;
			var dowsing = player.GetModPlayer<DowsingPlayer>();
			if (dowsing == null || !dowsing.Active)
				return;

			float pulse = 0.72f + 0.28f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4.2f);
			var targets = DowsingSystem.Targets;
			// Targets beyond the normal radar cap only exist during a sonar
			// pulse. Fade them with the envelope so the extra markers ease out
			// instead of vanishing when the reveal ends.
			int normalCap = DowsingString.RadarTargets(dowsing.Tier);
			float sonar = dowsing.SonarIntensity;
			if (sonar < 0.001f)
				sonar = 0f;
			// Draw secondaries first so the tagged target sits on top.
			for (int i = targets.Count - 1; i >= 0; i--) {
				float alpha = i < normalCap ? 1f : sonar;
				if (alpha <= 0.001f)
					continue;
				DrawTarget(sb, player, targets[i], i == 0, pulse, alpha);
			}

			if (dowsing.SonarActive)
				DrawSonarPulse(sb, player, dowsing);
		}

		private static void DrawTarget(SpriteBatch sb, Player player, DowsingTarget target,
			bool primary, float pulse, float alpha = 1f)
		{
			Vector2 view = DowsingSystem.UiScreenSize;
			Vector2 screen = DowsingSystem.WorldToUi(target.World);
			bool onScreen = screen.X > 24 && screen.Y > 24
				&& screen.X < view.X - 24
				&& screen.Y < view.Y - 24;
			Color color = KindColor(target.Kind) * alpha;
			float distTiles = MathHelper.Clamp(target.Distance / 16f, 1f, 9999f);

			if (onScreen) {
				DrawItemMarker(sb, screen, pulse, color, primary ? 1f : 0.72f);
				if (primary) {
					DrawLabel(sb, BuildLabel(target, player, distTiles),
						screen + new Vector2(0, -34f), color * pulse);
				}
				else {
					DrawSmallLabel(sb, $"{distTiles:0}",
						screen + new Vector2(0, 26f), color * 0.80f);
				}
				return;
			}

			// Offscreen: clamp a compass needle to the screen edge. Unlike the
			// old fixed 120px orbit, this works at every resolution and always
			// points from the screen center toward the target.
			Vector2 dir = (target.World - player.Center).SafeNormalize(Vector2.UnitX);
			Vector2 center = view * 0.5f;
			float margin = Math.Min(64f, Math.Min(view.X, view.Y) / 4f);
			float tx = Math.Abs(dir.X) > 0.0001f
				? (view.X / 2f - margin) / Math.Abs(dir.X)
				: float.MaxValue;
			float ty = Math.Abs(dir.Y) > 0.0001f
				? (view.Y / 2f - margin) / Math.Abs(dir.Y)
				: float.MaxValue;
			Vector2 anchor = center + dir * Math.Min(tx, ty);
			anchor.X = Math.Clamp(anchor.X, margin, view.X - margin);
			anchor.Y = Math.Clamp(anchor.Y, margin, view.Y - margin);

			DrawCompass(sb, anchor, dir, pulse, primary ? 1f : 0.62f, color);
			if (primary) {
				Vector2 labelPos = anchor + new Vector2(0, 32);
				if (labelPos.Y > view.Y - 38)
					labelPos.Y = anchor.Y - 40;
				DrawLabel(sb, BuildLabel(target, player, distTiles), labelPos, color * pulse);
			}
			else {
				Vector2 pos = anchor + new Vector2(0, 22);
				if (pos.Y > view.Y - 20)
					pos.Y = anchor.Y - 26;
				DrawSmallLabel(sb, $"{distTiles:0}", pos, color * 0.80f);
			}
		}

		/// <summary>Tag text for the tracked target: name, stack, distance and
		/// elevation. Loose gear also reports how many are in the pile, which
		/// matters for bearings, strings and tonic spares.</summary>
		private static string BuildLabel(DowsingTarget target, Player player, float distTiles)
		{
			string name = target.Label;
			if (target.Kind == DowsingKind.Item
				&& target.Entity >= 0 && target.Entity < Main.maxItems) {
				Item item = Main.item[target.Entity];
				if (item != null && item.active && item.stack > 1)
					name += $" x{item.stack}";
			}
			string label = $"{name}  {distTiles:0} tiles";
			float dy = target.World.Y - player.Center.Y;
			if (Math.Abs(dy) >= 48f)
				label += $"  {(dy < 0 ? "UP" : "DOWN")} {Math.Abs(dy) / 16f:0}";
			return label;
		}

		private static Color KindColor(DowsingKind kind)
		{
			switch (kind) {
				case DowsingKind.Critter: return CritterGreen;
				case DowsingKind.Chest: return Brass;
				case DowsingKind.Town: return TownWarm;
				case DowsingKind.Shimmer: return ShimmerViolet;
				default: return FinderCyan;
			}
		}

		private static void DrawItemMarker(SpriteBatch sb, Vector2 center, float pulse,
			Color color, float scale = 1f)
		{
			float half = 18f * scale;
			float arm = 8f * scale;
			Color c = color * (0.75f + 0.25f * pulse);

			DrawLine(sb, center + new Vector2(-half, -half), center + new Vector2(-half + arm, -half), c, 2f);
			DrawLine(sb, center + new Vector2(-half, -half), center + new Vector2(-half, -half + arm), c, 2f);
			DrawLine(sb, center + new Vector2(half, -half), center + new Vector2(half - arm, -half), c, 2f);
			DrawLine(sb, center + new Vector2(half, -half), center + new Vector2(half, -half + arm), c, 2f);
			DrawLine(sb, center + new Vector2(-half, half), center + new Vector2(-half + arm, half), c, 2f);
			DrawLine(sb, center + new Vector2(-half, half), center + new Vector2(-half, half - arm), c, 2f);
			DrawLine(sb, center + new Vector2(half, half), center + new Vector2(half - arm, half), c, 2f);
			DrawLine(sb, center + new Vector2(half, half), center + new Vector2(half, half - arm), c, 2f);
		}

		private static void DrawCompass(SpriteBatch sb, Vector2 center, Vector2 dir, float pulse,
			float scale, Color accent)
		{
			float radius = 20f * scale;
			// A dark diamond plate with a brass rim: no new texture required.
			DrawDiamond(sb, center, radius + 3f, new Color(6, 11, 19) * 0.9f);
			DrawDiamond(sb, center, radius, new Color(24, 38, 54) * 0.96f);
			DrawDiamondOutline(sb, center, radius, Brass * 0.8f);

			Vector2 tip = center + dir * (radius * 0.66f);
			Vector2 tail = center - dir * (radius * 0.48f);
			Color needle = Color.Lerp(accent, Color.White, 0.32f) * (0.8f + 0.2f * pulse);
			DrawLine(sb, tail, tip, needle, 2.4f);

			Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
			Vector2 back = tip - dir * (radius * 0.38f);
			DrawLine(sb, tip, back + perp * (radius * 0.25f), needle, 2f);
			DrawLine(sb, tip, back - perp * (radius * 0.25f), needle, 2f);
		}

		private static void DrawSonarPulse(SpriteBatch sb, Player player, DowsingPlayer dowsing)
		{
			float intensity = dowsing.SonarIntensity;
			if (intensity <= 0.001f)
				return;
			Vector2 center = DowsingSystem.WorldToUi(player.Center);
			// Ring expansion is driven by total age, so the pulse keeps moving
			// through the fade tail rather than resetting when the timer ends.
			float life = dowsing.SonarAge / 90f;
			for (int i = 0; i < 2; i++) {
				float phase = (life * 1.6f + i * 0.5f) % 1f;
				float radius = 36f + phase * 520f;
				UiArt.DrawRing(sb, center, radius,
					FinderCyan * (0.30f * (1f - phase) * intensity));
			}
			UiArt.DrawGlow(sb, center, 90f, FinderCyan * (0.08f * intensity));
		}

		private static void DrawDiamond(SpriteBatch sb, Vector2 center, float radius, Color color)
		{
			// MagicPixel is a padded texture; crop a single texel or the scale
			// overload multiplies by the whole sheet and the compass fills the screen.
			sb.Draw(TextureAssets.MagicPixel.Value, center, new Rectangle(0, 0, 1, 1), color,
				MathHelper.PiOver4, new Vector2(0.5f), new Vector2(radius * 1.42f),
				SpriteEffects.None, 0f);
		}

		private static void DrawDiamondOutline(SpriteBatch sb, Vector2 center, float radius, Color color)
		{
			Vector2 top = center + new Vector2(0, -radius);
			Vector2 right = center + new Vector2(radius, 0);
			Vector2 bottom = center + new Vector2(0, radius);
			Vector2 left = center + new Vector2(-radius, 0);
			DrawLine(sb, top, right, color, 1.2f);
			DrawLine(sb, right, bottom, color, 1.2f);
			DrawLine(sb, bottom, left, color, 1.2f);
			DrawLine(sb, left, top, color, 1.2f);
		}

		private static void DrawLabel(SpriteBatch sb, string text, Vector2 center, Color color)
		{
			if (string.IsNullOrEmpty(text))
				return;
			const float scale = 0.68f;
			Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
			Vector2 pos = center - size * 0.5f;
			Rectangle box = new((int)pos.X - 7, (int)pos.Y - 4,
				(int)size.X + 14, (int)size.Y + 8);
			Vector2 view = DowsingSystem.UiScreenSize;
			box.X = Math.Clamp(box.X, 4, Math.Max(4, (int)view.X - box.Width - 4));
			box.Y = Math.Clamp(box.Y, 4, Math.Max(4, (int)view.Y - box.Height - 4));
			FillRect(sb, box, new Color(6, 11, 19) * 0.88f);
			FillRect(sb, new Rectangle(box.X, box.Y, box.Width, 1), Brass * 0.75f);
			FillRect(sb, new Rectangle(box.X, box.Bottom - 1, box.Width, 1), Brass * 0.4f);
			Terraria.Utils.DrawBorderString(sb, text,
				new Vector2(box.X + 7, box.Y + 4), color, scale);
		}

		/// <summary>Radar secondaries get distance only: five name plates would
		/// bury the screen, but five numbers stay readable.</summary>
		private static void DrawSmallLabel(SpriteBatch sb, string text, Vector2 center, Color color)
		{
			if (string.IsNullOrEmpty(text))
				return;
			const float scale = 0.52f;
			Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
			Vector2 pos = center - size * 0.5f;
			Terraria.Utils.DrawBorderString(sb, text, pos + new Vector2(0f, 1f),
				Color.Black * 0.65f, scale);
			Terraria.Utils.DrawBorderString(sb, text, pos, color, scale);
		}

		private static void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thickness)
		{
			Vector2 delta = b - a;
			float length = delta.Length();
			if (length <= 0.01f)
				return;
			sb.Draw(TextureAssets.MagicPixel.Value, a, new Rectangle(0, 0, 1, 1), color,
				delta.ToRotation(), new Vector2(0f, 0.5f), new Vector2(length, thickness),
				SpriteEffects.None, 0f);
		}

		private static void FillRect(SpriteBatch sb, Rectangle rect, Color color)
		{
			if (rect.Width <= 0 || rect.Height <= 0)
				return;
			sb.Draw(TextureAssets.MagicPixel.Value, rect, new Rectangle(0, 0, 1, 1), color);
		}
	}
}
