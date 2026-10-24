using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using BiltonsYoyoMod.Configuration;

namespace BiltonsYoyoMod.Items.Utility
{
	/// <summary>How the spool behaves. Off and Manual are quiet; the other
	/// three only change WHEN the existing near-expiry refresh runs.</summary>
	public enum TonicMode
	{
		Off = 0,
		Manual = 1,
		Combat = 2,
		Boss = 3,
		Always = 4,
	}

	/// <summary>One carried tonic. The catalog is built lazily because item
	/// and buff ids only exist after content loading.</summary>
	public readonly struct TonicInfo
	{
		public readonly int ItemType;
		public readonly int BuffType;
		public readonly int BuffTicks;
		public readonly string ShortName;
		public readonly string ItemName;
		public readonly Color Accent;

		public TonicInfo(int itemType, int buffType, int buffTicks,
			string shortName, string itemName, Color accent)
		{
			ItemType = itemType;
			BuffType = buffType;
			BuffTicks = buffTicks;
			ShortName = shortName;
			ItemName = itemName;
			Accent = accent;
		}

		public int Remaining(Player player)
			=> BuffType == BuffID.WellFed
				? TonicSpoolPlayer.BuffTimeOf(player, BuffID.WellFed)
				: TonicSpoolPlayer.BuffTimeOf(player, BuffType);
	}

	public static class TonicCatalog
	{
		public const int Count = 5;
		private static TonicInfo[] entries;

		public static TonicInfo[] All => entries ??= Build();

		private static TonicInfo[] Build() => new[] {
			new TonicInfo(ModContent.ItemType<Consumables.BearingOil>(),
				ModContent.BuffType<Content.Buffs.BearingGreaseBuff>(), 4 * 3600,
				"OIL", "Bearing Oil", new Color(230, 190, 120)),
			new TonicInfo(ModContent.ItemType<Consumables.StringWax>(),
				ModContent.BuffType<Content.Buffs.StringWaxBuff>(), 8 * 3600,
				"WAX", "String Wax", new Color(238, 226, 170)),
			new TonicInfo(ModContent.ItemType<Consumables.CultistsDraught>(),
				ModContent.BuffType<Content.Buffs.CultistFervorBuff>(), 3 * 3600,
				"DRAUGHT", "Cultist's Draught", new Color(190, 160, 240)),
			new TonicInfo(ModContent.ItemType<Consumables.MoonlightFlask>(),
				ModContent.BuffType<Content.Buffs.MoonlitFocusBuff>(), 5 * 3600,
				"FLASK", "Moonlight Flask", new Color(150, 190, 255)),
			new TonicInfo(ModContent.ItemType<Consumables.HoneySpool>(),
				BuffID.WellFed, 8 * 3600,
				"HONEY", "Honey Spool", new Color(245, 200, 90)),
		};

		/// <summary>Static caches hold content ids, so they must not survive a
		/// mod reload.</summary>
		public static void Reset() => entries = null;
	}

	/// <summary>
	/// Tonic Spool (utility, zero stats, NOT an accessory): while a switched-on
	/// spool is carried, yoyo tonics refresh shortly before they expire by
	/// spending carried spares. Right-click cycles OFF / MANUAL / COMBAT /
	/// BOSS / ALWAYS. The HUD drinks on click and toggles each tonic's auto
	/// upkeep on right-click. Never consumed, never free power.
	/// </summary>
	public class TonicSpool : ModItem
	{
		/// <summary>Per-copy behavior mode. Saved + synced. Old saves stored a
		/// boolean; ON migrates to ALWAYS, OFF to OFF.</summary>
		public TonicMode Mode = TonicMode.Always;

		public bool Enabled => Mode != TonicMode.Off;

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

		public static string ModeName(TonicMode mode)
		{
			switch (mode) {
				case TonicMode.Manual: return "MANUAL";
				case TonicMode.Combat: return "COMBAT";
				case TonicMode.Boss: return "BOSS";
				case TonicMode.Always: return "ALWAYS";
				default: return "OFF";
			}
		}

		public static string ModeDescription(TonicMode mode)
		{
			switch (mode) {
				case TonicMode.Manual:
					return "HUD only. Click a tonic there to drink it.";
				case TonicMode.Combat:
					return "Auto-refresh started tonics during nearby fights.";
				case TonicMode.Boss:
					return "Auto-refresh started tonics while a boss is alive.";
				case TonicMode.Always:
					return "Auto-refresh started tonics whenever they near expiry.";
				default:
					return "No HUD and no auto-refresh.";
			}
		}

		public override bool CanRightClick() => true;

		// Right-click only cycles the mode; the spool is never consumed.
		public override bool ConsumeItem(Player player) => false;

		public override void RightClick(Player player)
		{
			if (player == null || player.whoAmI != Main.myPlayer || Main.dedServ)
				return;
			Mode = (TonicMode)(((int)Mode + 1) % 5);
			Main.NewText($"Tonic Spool: {ModeName(Mode)}  -  {ModeDescription(Mode)}",
				200, 230, 255);
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.45f,
				Pitch = Mode == TonicMode.Off ? -0.20f : 0.12f,
				MaxInstances = 2,
			}, player.Center);
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "SpoolState",
				$"Mode: {ModeName(Mode)}") {
				OverrideColor = Enabled ? Color.Lime : Color.Gray
			});
			tooltips.Add(new TooltipLine(Mod, "SpoolHow",
				"Right-click cycles OFF / MANUAL / COMBAT / BOSS / ALWAYS."));
			tooltips.Add(new TooltipLine(Mod, "SpoolMode",
				ModeDescription(Mode)) {
				OverrideColor = new Color(174, 190, 210)
			});
			tooltips.Add(new TooltipLine(Mod, "SpoolLimit",
				"Auto only maintains tonics you have already started.") {
				OverrideColor = new Color(174, 190, 210)
			});

			Player player = Main.LocalPlayer;
			if (player != null) {
				var tonics = TonicCatalog.All;
				var parts = new List<string>();
				for (int i = 0; i < tonics.Length; i++) {
					int count = player.CountItem(tonics[i].ItemType);
					if (count > 0)
						parts.Add($"{tonics[i].ShortName} {count}");
				}
				string spares = parts.Count > 0
					? "Spares: " + string.Join("  ", parts)
					: "No spare tonics carried.";
				tooltips.Add(new TooltipLine(Mod, "SpoolSpares", spares) {
					OverrideColor = parts.Count > 0
						? Color.LightSkyBlue
						: Color.Gray
				});
			}

			var config = ModContent.GetInstance<YoyoConfig>();
			string hudLine;
			if (Mode == TonicMode.Off)
				hudLine = "Right-click to leave OFF and show the HUD.";
			else if (config != null && !config.TonicHud)
				hudLine = "HUD is hidden in Settings > TonicSpool.";
			else
				hudLine = "HUD: LMB drinks (under 0:30), RMB toggles that tonic.";
			tooltips.Add(new TooltipLine(Mod, "SpoolHud", hudLine) {
				OverrideColor = new Color(150, 200, 170)
			});
			tooltips.Add(new TooltipLine(Mod, "SpoolPeek",
				"Hover the right-edge tab to peek; click it to pin or tuck away.") {
				OverrideColor = new Color(174, 190, 210)
			});
		}

		public override ModItem Clone(Item newEntity)
		{
			var clone = (TonicSpool)base.Clone(newEntity);
			clone.Mode = Mode;
			return clone;
		}

		public override void SaveData(TagCompound tag)
		{
			tag["tonicMode"] = (int)Mode;
			// Written for downgrade safety: an older build only knows the bool.
			tag["enabled"] = Enabled;
		}

		public override void LoadData(TagCompound tag)
		{
			if (tag.ContainsKey("tonicMode")) {
				Mode = (TonicMode)Math.Clamp(tag.GetInt("tonicMode"), 0, 4);
			}
			else if (tag.ContainsKey("enabled")) {
				// Legacy ON was the old always-on refresher.
				Mode = tag.GetBool("enabled") ? TonicMode.Always : TonicMode.Off;
			}
			else {
				Mode = TonicMode.Always;
			}
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write((byte)Mode);
		}

		public override void NetReceive(BinaryReader reader)
		{
			Mode = (TonicMode)Math.Clamp((int)reader.ReadByte(), 0, 4);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Bottle, 5)
				.AddIngredient(ItemID.Silk, 6)
				.AddIngredient(ItemID.Cobweb, 15)
				.AddTile(TileID.Bottles)
				.Register();
		}
	}

	public class TonicSpoolPlayer : ModPlayer
	{
		private const int InventorySlots = 50; // hotbar + main inventory, no trash
		private const int RefreshWindow = 5 * 60; // 5 seconds before expiry
		private const int ManualWasteWindow = 30 * 60; // block wasteful HUD clicks

		/// <summary>Any spool is carried (even one switched OFF).</summary>
		public bool SpoolCarried;
		/// <summary>The carried spool is not OFF.</summary>
		public bool Active;
		public TonicMode Mode = TonicMode.Off;
		/// <summary>Player preference: the right-edge strip is tucked away
		/// until the peek tab is hovered or clicked.</summary>
		public bool HudCollapsed;

		/// <summary>"Wanted" flags: a tonic that expired while a spare was
		/// missing resumes as soon as one is picked up.</summary>
		private readonly bool[] had = new bool[TonicCatalog.Count];
		private int cooldown;
		private int autoMask = (1 << TonicCatalog.Count) - 1;
		private int combatTimer;
		private bool combatNear;

		public override void ResetEffects()
		{
			Active = false;
			SpoolCarried = false;
			Mode = TonicMode.Off;
		}

		public override void PostUpdate()
		{
			Active = false;
			SpoolCarried = false;
			Mode = TonicMode.Off;

			// The spool is local inventory logic. Running it on every client for
			// every player would let one client consume another player's tonics.
			if (Player.whoAmI != Main.myPlayer || Main.dedServ)
				return;

			if (Player.active && !Player.dead) {
				TonicSpool chosen = null;
				for (int i = 0; i < InventorySlots && i < Player.inventory.Length; i++) {
					Item item = Player.inventory[i];
					if (item == null || item.IsAir || item.ModItem is not TonicSpool spool)
						continue;
					// Prefer an enabled spool; a spare switched OFF should not
					// mask the one the player is actually using.
					if (chosen == null)
						chosen = spool;
					if (spool.Mode != TonicMode.Off) {
						chosen = spool;
						break;
					}
				}
				if (chosen != null) {
					SpoolCarried = true;
					Mode = chosen.Mode;
					Active = chosen.Mode != TonicMode.Off;
				}
			}

			if (cooldown > 0)
				cooldown--;

			if (!Active) {
				CacheStates();
				return;
			}

			bool auto = Mode switch {
				TonicMode.Always => true,
				TonicMode.Combat => CombatNear(),
				TonicMode.Boss => AnyBossAlive(),
				_ => false,
			};
			if (!auto) {
				// OFF and MANUAL are user intent: forget expired tonics.
				// COMBAT/BOSS are temporary gates, so keep the wanted flags;
				// a tonic that lapsed between fights resumes at the next one.
				if (Mode == TonicMode.Manual)
					CacheStates();
				return;
			}
			// Do NOT CacheStates here: that would forget a tonic whose spare
			// is still missing while another tonic's drink cooldown runs.
			if (cooldown > 0)
				return;

			var tonics = TonicCatalog.All;
			for (int i = 0; i < tonics.Length; i++) {
				if (!IsTonicEnabled(i))
					continue;
				TonicInfo tonic = tonics[i];
				int remaining = tonic.Remaining(Player);
				bool running = remaining > 0;

				// Honey Spool only fills an empty Well Fed slot. Any meal tier
				// occupies that slot, so the spool waits instead of replacing
				// a stronger food buff early.
				if (tonic.BuffType == BuffID.WellFed) {
					if (TonicSpoolPlayer.FoodTime(Player) > 0) {
						// Only Honey's own buff counts as "started". Eating a
						// steak must not make the spool start Honey later.
						had[i] = running;
					}
					else if (had[i]) {
						TryDrink(tonic);
					}
					continue;
				}

				bool shouldDrink = running ? remaining <= RefreshWindow : had[i];
				if (shouldDrink) {
					// Stay "wanted" even when the spare is missing so a later
					// pickup finishes the refresher instead of forgetting it.
					had[i] = true;
					TryDrink(tonic);
				}
				else {
					had[i] = running;
				}
			}
		}

		public bool IsTonicEnabled(int index)
			=> index >= 0 && index < TonicCatalog.Count
				&& (autoMask & (1 << index)) != 0;

		/// <summary>HUD right-click: opt this tonic in or out of auto upkeep.
		/// Manual drinking still works while it is opted out.</summary>
		public void ToggleTonic(int index)
		{
			if (Player.whoAmI != Main.myPlayer || Main.dedServ)
				return;
			if (index < 0 || index >= TonicCatalog.Count)
				return;
			autoMask ^= 1 << index;
			bool on = IsTonicEnabled(index);
			Main.NewText($"Tonic Spool: {TonicCatalog.All[index].ShortName} auto "
				+ (on ? "ON" : "OFF") + ".",
				on ? new Color(150, 226, 150) : new Color(200, 200, 200));
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.32f, Pitch = on ? 0.12f : -0.20f, MaxInstances = 2,
			}, Player.Center);
		}

		/// <summary>HUD left-click. Refuses to waste a dose that still has more
		/// than 30 seconds left, and never downgrades a stronger meal buff.</summary>
		public void DrinkManually(int index)
		{
			if (Player.whoAmI != Main.myPlayer || Main.dedServ
				|| !Player.active || Player.dead)
				return;
			var tonics = TonicCatalog.All;
			if (index < 0 || index >= tonics.Length)
				return;
			TonicInfo tonic = tonics[index];
			int remaining = tonic.Remaining(Player);

			if (tonic.BuffType == BuffID.WellFed) {
				if (TonicSpoolPlayer.FoodTime(Player) > 0) {
					Main.NewText("Tonic Spool: a meal buff is already active.",
						210, 210, 200);
					Tick(-0.20f);
					return;
				}
			}
			else if (remaining > ManualWasteWindow) {
				Main.NewText($"Tonic Spool: {tonic.ItemName} still has "
					+ $"{TimeText(remaining)} left.", 210, 210, 200);
				Tick(-0.20f);
				return;
			}

			if (Player.CountItem(tonic.ItemType) <= 0
				|| !Player.ConsumeItem(tonic.ItemType)) {
				Main.NewText($"Tonic Spool: no {tonic.ItemName} carried.",
					210, 210, 200);
				Tick(-0.20f);
				return;
			}
			Player.AddBuff(tonic.BuffType, tonic.BuffTicks, false);
			had[index] = true;
			Main.NewText($"Tonic Spool: drank {tonic.ItemName}.", 200, 230, 255);
			SoundEngine.PlaySound(SoundID.Item3 with {
				Volume = 0.55f, Pitch = 0.05f, MaxInstances = 2,
			}, Player.Center);
		}

		private void Tick(float pitch)
		{
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.32f, Pitch = pitch, MaxInstances = 2,
			}, Player.Center);
		}

		private bool TryDrink(TonicInfo tonic)
		{
			if (tonic.ItemType <= 0)
				return false;
			// ConsumeItem handles stack changes and is multiplayer-safe when
			// called from client code; manual stack-- was the old desync bug.
			if (!Player.ConsumeItem(tonic.ItemType))
				return false;
			// Non-deterministic (inventory-dependent) buffs must not be quiet:
			// other clients need to see the refreshed tonic.
			Player.AddBuff(tonic.BuffType, tonic.BuffTicks, false);
			cooldown = 8; // stagger multiple expiries without gaps
			if (Player.whoAmI == Main.myPlayer)
				Main.NewText($"Tonic Spool: refreshed {tonic.ItemName}.",
					200, 230, 255);
			SoundEngine.PlaySound(SoundID.Item3 with {
				Volume = 0.55f, Pitch = 0.05f, MaxInstances = 2,
			}, Player.Center);
			return true;
		}

		private void CacheStates()
		{
			var tonics = TonicCatalog.All;
			for (int i = 0; i < tonics.Length; i++)
				had[i] = tonics[i].Remaining(Player) > 0;
		}

		private bool CombatNear()
		{
			if (combatTimer > 0) {
				combatTimer--;
				return combatNear;
			}
			combatTimer = 15;
			combatNear = AnyBossAlive();
			if (combatNear)
				return true;

			float range = 50f * 16f;
			float rangeSq = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active || npc.friendly || npc.townNPC
					|| npc.life <= 0 || npc.damage <= 0)
					continue;
				if (npc.type > NPCID.None && npc.type < NPCID.Sets.CountsAsCritter.Length
					&& NPCID.Sets.CountsAsCritter[npc.type])
					continue;
				if (Vector2.DistanceSquared(Player.Center, npc.Center) <= rangeSq) {
					combatNear = true;
					return true;
				}
			}
			return false;
		}

		public static bool AnyBossAlive()
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc != null && npc.active && npc.boss
					&& !npc.friendly && npc.life > 0)
					return true;
			}
			return false;
		}

		public static int BuffTimeOf(Player player, int buffType)
		{
			if (player == null)
				return 0;
			int index = player.FindBuffIndex(buffType);
			return index < 0 ? 0 : player.buffTime[index];
		}

		public static int FoodTime(Player player)
			=> Math.Max(BuffTimeOf(player, BuffID.WellFed),
				Math.Max(BuffTimeOf(player, BuffID.WellFed2),
					BuffTimeOf(player, BuffID.WellFed3)));

		public static string TimeText(int ticks)
		{
			if (ticks <= 0)
				return "--";
			int seconds = ticks / 60;
			return $"{seconds / 60}:{seconds % 60:00}";
		}

		public override void SaveData(TagCompound tag)
		{
			tag["tonicAutoMask"] = autoMask;
			tag["tonicHudCollapsed"] = HudCollapsed;
		}

		public override void LoadData(TagCompound tag)
		{
			autoMask = tag.ContainsKey("tonicAutoMask")
				? tag.GetInt("tonicAutoMask") & ((1 << TonicCatalog.Count) - 1)
				: (1 << TonicCatalog.Count) - 1;
			HudCollapsed = tag.ContainsKey("tonicHudCollapsed")
				&& tag.GetBool("tonicHudCollapsed");
		}
	}
}
