using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using BiltonsYoyoMod.Configuration;
using BiltonsYoyoMod.Settings;
using BiltonsYoyoMod.UI;

namespace BiltonsYoyoMod.Items.Utility
{
	/// <summary>
	/// String Splicer (utility, no damage): keeps every carried yoyo one press
	/// away. Swap Yoyo opens the wheel; a yoyo click, Enter, or a second Swap
	/// equips the highlighted yoyo. Esc or right-click cancels. The wheel is also a small loadout
	/// instrument: three pin boards, a pin editor, stat comparison, sorting,
	/// filtering and a recently-used list all live on the Splicer itself. Pins
	/// never touch the yoyo items.
	/// </summary>
	public class StringSplicer : ModItem
	{
		private const int InventorySlots = 50; // hotbar + main inventory only

		public override void SetDefaults()
		{
			Item.width = 26;
			Item.height = 26;
			Item.maxStack = 1;
			Item.consumable = false;
			// The Splicer is a passive instrument. Left/right click deliberately
			// do nothing: the only action is the Swap Yoyo wheel toggle.
			Item.useStyle = ItemUseStyleID.None;
			Item.useTime = 0;
			Item.useAnimation = 0;
			Item.autoReuse = false;
			Item.UseSound = null;
			Item.rare = ItemRarityID.Green;
			Item.value = Item.buyPrice(gold: 2);
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			string key = YoyoTechKeybind.SwapKeyLabel;
			tooltips.Add(new TooltipLine(Mod, "SplicerToggle",
				$"Press [{key}] to open; press again to equip the highlighted yoyo.") {
				OverrideColor = new Color(150, 196, 235),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerEquip",
				"Click a yoyo or press Enter to equip it.") {
				OverrideColor = new Color(226, 195, 126),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerBoards",
				"Wheel: Tab boards (Ctrl+1-4 jumps)  |  Ctrl+S sort  |  Ctrl+P pins") {
				OverrideColor = new Color(196, 186, 158),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerQuick",
				"Search: Ctrl+F  |  Shift+Click pins  |  Delete / X clears") {
				OverrideColor = new Color(174, 166, 148),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerSplice",
				"Alt+Click splices up to 4 yoyos; assign Advance Splice Sequence in Controls.") {
				OverrideColor = new Color(150, 196, 235),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerLead",
				"Ctrl+Shift+Click sets the lead yoyo; switching boards selects it.") {
				OverrideColor = new Color(226, 195, 126),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerLocks",
				"LOCKS protects hotbar slots; Equip, Panic and Quick Cycle skip them.") {
				OverrideColor = new Color(174, 166, 148),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerRoles",
				"Search role:fast, role:long, role:heavy, role:dmg or role:pierce.") {
				OverrideColor = new Color(174, 166, 148),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerPanic",
				"Ctrl+Click a yoyo to set or clear the panic slot.") {
				OverrideColor = new Color(174, 166, 148),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerCombat",
				"Panic Equip / Quick Cycle: assign keys in Controls; Shift reverses the cycle.") {
				OverrideColor = new Color(174, 166, 148),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerPins",
				"Pins are stored on the Splicer, not on the yoyo items.") {
				OverrideColor = new Color(174, 166, 148),
			});
			tooltips.Add(new TooltipLine(Mod, "SplicerAnywhere",
				"Works while the Splicer is anywhere in the inventory.") {
				OverrideColor = new Color(174, 166, 148),
			});
		}

		// ------------------------------------------------------------ inventory

		/// <summary>Unique yoyo item types in hotbar + main inventory, in
		/// inventory order. Stable order matters: it is the wheel's default
		/// muscle memory.</summary>
		public static List<int> CollectTypes(Player player)
		{
			var types = new List<int>();
			var seen = new HashSet<int>();
			for (int i = 0; i < InventorySlots; i++) {
				Item item = player?.inventory != null && i < player.inventory.Length ? player.inventory[i] : null;
				if (item != null && !item.IsAir && IsYoyo(item) && seen.Add(item.type))
					types.Add(item.type);
			}
			return types;
		}

		public static int SlotOf(Player player, int type)
		{
			for (int i = 0; i < InventorySlots; i++) {
				Item item = player?.inventory != null && i < player.inventory.Length ? player.inventory[i] : null;
				if (item != null && !item.IsAir && item.type == type)
					return i;
			}
			return -1;
		}

		/// <summary>The yoyo the player is looking at: held yoyo first, then any hotbar yoyo.</summary>
		public static int DisplayedType(Player player)
		{
			if (player?.inventory == null)
				return -1;
			int selected = Math.Clamp(player.selectedItem, 0, 9);
			if (IsYoyo(player.inventory[selected]))
				return player.inventory[selected].type;
			for (int i = 0; i < 10 && i < player.inventory.Length; i++) {
				if (IsYoyo(player.inventory[i]))
					return player.inventory[i].type;
			}
			return -1;
		}

		private static int FindAnchor(Player player, bool unlockedOnly = false)
		{
			if (player?.inventory == null)
				return -1;
			int selected = Math.Clamp(player.selectedItem, 0, 9);
			if (IsYoyo(player.inventory[selected])
				&& (!unlockedOnly || !IsHotbarLocked(player, selected)))
				return selected;
			for (int i = 0; i < 10 && i < player.inventory.Length; i++) {
				if (IsYoyo(player.inventory[i])
					&& (!unlockedOnly || !IsHotbarLocked(player, i)))
					return i;
			}
			return -1;
		}

		/// <summary>A locked hotbar slot is never used as a swap destination
		/// and its yoyo is skipped by Panic/Quick Cycle. Selecting the yoyo in
		/// place is still safe because nothing is displaced.</summary>
		public static bool IsHotbarLocked(Player player, int slot)
		{
			if (player == null || slot < 0 || slot >= 10)
				return false;
			try {
				var state = player.GetModPlayer<StringSplicerPlayer>();
				return state != null && state.LockedHotbar[slot];
			}
			catch {
				return false;
			}
		}

		public static bool IsTypeLocked(Player player, int type)
		{
			int slot = SlotOf(player, type);
			return slot >= 0 && slot < 10 && IsHotbarLocked(player, slot);
		}

		/// <summary>Carried yoyos eligible for Panic and Quick Cycle. Locked
		/// hotbar yoyos stay in the wheel but are deliberately left out of
		/// emergency rotation.</summary>
		public static List<int> CollectCycleTypes(Player player)
		{
			var types = CollectTypes(player);
			for (int i = types.Count - 1; i >= 0; i--) {
				if (IsTypeLocked(player, types[i]))
					types.RemoveAt(i);
			}
			return types;
		}

		private static bool IsYoyo(Item item)
			=> item != null && !item.IsAir && Content.NPCs.Town.YoyoTownShared.IsYoyoShape(item);

		private static void Swap(Player player, int a, int b)
		{
			if (a == b)
				return;
			(player.inventory[a], player.inventory[b]) = (player.inventory[b], player.inventory[a]);
		}

		/// <summary>
		/// Puts the requested yoyo in a hotbar slot and selects it. If a hotbar
		/// yoyo already exists it is swapped with the target so no tool is ever
		/// displaced; otherwise the target is pulled into the selected slot (when
		/// the Splicer is held) or the first empty hotbar slot.
		/// </summary>
		public static bool Equip(Player player, int type)
		{
			if (player == null || !player.active || player.dead)
				return false;
			if (player.whoAmI != Main.myPlayer)
				return false;

			var types = CollectTypes(player);
			if (!types.Contains(type)) {
				Tell(player, "Splicer: that yoyo is no longer in the inventory.");
				return false;
			}

			int from = SlotOf(player, type);
			if (from < 0)
				return false;

			// A locked hotbar yoyo is never moved or swapped out. Selecting it
			// in place is the only safe "equip", and it is also what the player
			// asked for.
			if (from < 10 && IsHotbarLocked(player, from)) {
				player.selectedItem = from;
				Announce(player, from, types.IndexOf(type) + 1, types.Count);
				return true;
			}

			int anchor = FindAnchor(player, unlockedOnly: true);
			if (anchor < 0) {
				int selected = Math.Clamp(player.selectedItem, 0, 9);
				int destination = -1;
				bool heldSplicer = player.HeldItem?.ModItem is StringSplicer;
				bool selectedEmpty = player.inventory[selected] == null || player.inventory[selected].IsAir;
				bool selectedLocked = IsHotbarLocked(player, selected);
				if (!selectedLocked && (heldSplicer || selectedEmpty))
					destination = selected;
				else {
					for (int i = 0; i < 10 && i < player.inventory.Length; i++) {
						if (IsHotbarLocked(player, i))
							continue;
						Item hotbar = player.inventory[i];
						if (hotbar == null || hotbar.IsAir) {
							destination = i;
							break;
						}
					}
				}
				if (destination < 0) {
					Tell(player, "Splicer: no unlocked hotbar slot is free.");
					return false;
				}

				Swap(player, destination, from);
				player.selectedItem = destination;
				Announce(player, destination, types.IndexOf(type) + 1, types.Count);
				return true;
			}

			if (from != anchor)
				Swap(player, anchor, from);
			player.selectedItem = anchor;
			Announce(player, anchor, types.IndexOf(type) + 1, types.Count);
			return true;
		}

		private static void Announce(Player player, int slot, int index, int count)
		{
			Item item = player.inventory[slot];
			if (item == null || item.IsAir)
				return;
			string name;
			try { name = Lang.GetItemNameValue(item.type); }
			catch { name = "Yoyo"; }
			var state = player.GetModPlayer<StringSplicerPlayer>();
			state.NoteRecent(item.type);
			state.ShowToast(item.type, name, index, count, "EQUIPPED");
			SoundEngine.PlaySound(SoundID.Item35 with {
				Volume = 0.55f, Pitch = 0.12f, MaxInstances = 2,
			}, player.Center);
		}

		public static void Tell(Player player, string message)
		{
			if (player.whoAmI == Main.myPlayer && !Main.dedServ)
				Main.NewText(message, 200, 200, 200);
		}

		public static bool HasSplicer(Player player)
		{
			int want = ModContent.ItemType<StringSplicer>();
			for (int i = 0; i < InventorySlots; i++) {
				Item item = player?.inventory != null && i < player.inventory.Length ? player.inventory[i] : null;
				if (item != null && !item.IsAir && item.type == want)
					return true;
			}
			return false;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Cobweb, 20)
				.AddIngredient(ItemID.Silk, 4)
				.AddRecipeGroup("BiltonsYoyoMod:AnyIronBar", 5)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}

	/// <summary>Local-only state, pin boards and feedback for the Splicer. The
	/// world never needs to know which yoyo the player is admiring.</summary>
	public class StringSplicerPlayer : ModPlayer
	{
		public const int PinBoardCount = 3;
		public const int MaxPins = 8;
		public const int WheelPageSize = 10;
		public const int RecentMax = 8;
		public const int MaxSequence = 4;

		public static readonly string[] BoardNames = { "MAIN", "COMBAT", "EXPLORE", "FARM" };
		public static readonly string[] SortNames = { "INVENTORY", "DAMAGE", "RARITY", "NAME", "RECENT" };
		public static readonly string[] SortShortNames = { "INV", "DMG", "RARE", "NAME", "RECENT" };

		public int ToastItemType;
		public string ToastName = "";
		public string ToastCaption = "EQUIPPED";
		public int ToastIndex;
		public int ToastCount;
		public int ToastTimer;

		public bool WheelOpen;
		public int WheelOpenTicks;
		public int WheelCloseTicks;
		public readonly List<int> WheelTypes = new();
		public readonly List<int> WheelPool = new();
		public int WheelSelected;
		// Fractional selection angle used by the drawing layer. It trails
		// WheelSelected with a spring so the lit arc and medallion focus glide
		// between slices instead of snapping.
		public float SelectionGlide;
		public int SelectionGlideCount;
		public int WheelPage;
		public int WheelPageCount;
		public int WheelFilteredCount;
		public string Filter = "";
		// The page/slot to return to when a search is cleared. Searching
		// should not strand the wheel on the last page with only a few
		// matches visible after the query is erased.
		private int filterReturnPage = -1;
		private int filterReturnSlot;
		/// <summary>Mouse-wheel delta captured in PostUpdateInput before the
		/// vanilla hotbar consumes it. The wheel pages with this value.</summary>
		public int PendingScroll;
		/// <summary>True after clicking the filter plate. Escape first clears
		/// the query, then leaves the field, then closes the wheel.</summary>
		public bool FilterFocus;
		public bool PinEditor;
		/// <summary>Hotbar-lock editor. It replaces the splice rail in the
		/// footer while it is open, so the two never collide.</summary>
		public bool LockEditor;
		public int Board; // 0 = main/all; 1..3 = pin boards
		public int SortMode;
		public int LastPinBoard = 1;
		/// <summary>The emergency yoyo assigned with Ctrl+click. 0 means the
		/// panic key falls back to the previous/other carried yoyo.</summary>
		public int PanicType;
		/// <summary>The yoyo a board switch should select first. 0 means no
		/// lead is assigned.</summary>
		public int LeadType;
		/// <summary>Hotbar slots 0-9 protected from Splicer swaps and left out
		/// of Panic/Quick Cycle rotation.</summary>
		public readonly bool[] LockedHotbar = new bool[10];
		/// <summary>Per-board curated splice chains. MAIN is board 0, followed
		/// by the three pin boards. Each chain loops and holds at most four
		/// yoyos.</summary>
		public readonly List<int>[] Sequences =
		{
			new List<int>(), new List<int>(), new List<int>(), new List<int>(),
		};
		private readonly List<int> quickCycleOrder = new();
		public int PinNoticeTimer;
		public string PinNotice = "";
		public readonly List<int>[] PinBoards =
		{
			new List<int>(), new List<int>(), new List<int>(),
		};
		public readonly List<int> RecentTypes = new();

		private bool wheelMouseMoved;
		private Vector2 wheelOpenMouse;
		private bool prevLeft;
		private bool prevRight;
		private Keys[] prevKeys = Array.Empty<Keys>();
		// Click edges are latched in PostUpdateInput, before the wheel eats
		// the vanilla mouse triggers. If anything clears Main.mouseLeft
		// later in the tick, the click still reaches the wheel.
		private bool inputPrevLeft;
		private bool inputPrevRight;
		private bool wheelLeftClick;
		private bool wheelRightClick;

		// One virtual viewport shared by every draw and hit test. Terraria's
		// screen size and mouse position change meaning between the update
		// and interface-draw phases, so never read Main.screenWidth or
		// Main.MouseScreen directly here. UiViewport reconstructs the same
		// InterfaceScaleType.UI space in both phases.
		public static int UiWidth => UiViewport.Width;
		public static int UiHeight => UiViewport.Height;
		public static Vector2 UiMouse => UiViewport.Mouse;
		public static Point UiMousePoint => UiViewport.MousePoint;

		/// <summary>The wheel is the only circular element; its size reserves a
		/// measured band above for the title/tabs/filter and another below for
		/// the sort/pin chips and hints. That reservation is what keeps the
		/// compact layout from colliding on short or narrow screens.</summary>
		public static float WheelOuter {
			get {
				float widthLimit = (UiWidth - 48f) * 0.5f;
				float heightLimit = UiHeight * 0.5f - 106f;
				return MathHelper.Clamp(Math.Min(Math.Min(widthLimit, heightLimit), 230f), 96f, 230f);
			}
		}

		public static Vector2 WheelCenter => new(UiWidth * 0.5f, UiHeight * 0.5f + 2f);
		public static float WheelInner => WheelOuter * 0.455f;

		public static string BoardName(int board)
			=> board >= 0 && board < BoardNames.Length ? BoardNames[board] : "MAIN";

		public static string SortName(int sort)
			=> sort >= 0 && sort < SortNames.Length ? SortNames[sort] : "INVENTORY";

		/// <summary>Maps a UI-space point to the medallion under it. Returns -1
		/// over the hub or outside the wheel so a hub click can be handled as a
		/// deliberate equip rather than an accidental slice pick.</summary>
		public int WheelSliceAt(Vector2 point)
		{
			int n = WheelTypes.Count;
			if (n <= 0)
				return -1;
			Vector2 delta = point - WheelCenter;
			float dist = delta.Length();
			if (dist < WheelInner * 0.88f || dist > WheelOuter + 12f)
				return -1;
			float step = MathHelper.TwoPi / n;
			float angle = MathHelper.WrapAngle(delta.ToRotation() + MathHelper.PiOver2);
			int index = (int)Math.Round(angle / step);
			index %= n;
			if (index < 0)
				index += n;
			return index;
		}

		public void ShowToast(int itemType, string name, int index, int count, string caption = "EQUIPPED")
		{
			ToastItemType = itemType;
			ToastName = name ?? "Yoyo";
			ToastCaption = string.IsNullOrEmpty(caption) ? "EQUIPPED" : caption;
			ToastIndex = index;
			ToastCount = Math.Max(1, count);
			ToastTimer = 150;
		}

		public void NoteRecent(int type)
		{
			RecentTypes.Remove(type);
			RecentTypes.Insert(0, type);
			while (RecentTypes.Count > RecentMax)
				RecentTypes.RemoveAt(RecentTypes.Count - 1);
		}

		// -------------------------------------------------------------- wheel

		/// <summary>Swap Yoyo opens the wheel, then confirms the highlighted
		/// yoyo on the next press. Right-click remains the pure cancel. Pin
		/// editing is the exception: Swap closes it without equipping.</summary>
		public void ToggleWheel(Player player)
		{
			if (WheelOpen) {
				if (PinEditor || LockEditor)
					CancelWheel();
				else
					ConfirmWheel();
				return;
			}
			if (player == null || player.dead || !player.active)
				return;
			if (!StringSplicer.HasSplicer(player))
				return;
			OpenWheel(player);
		}

		/// <summary>Assigns or clears the emergency yoyo. Ctrl+click a medallion
		/// to call this; the same yoyo toggles the assignment off.</summary>
		public void SetPanicType(Player player, int type)
		{
			if (type <= 0)
				return;
			bool clearing = PanicType == type;
			PanicType = clearing ? 0 : type;
			PinNotice = clearing ? "PANIC CLEARED" : "PANIC SET";
			PinNoticeTimer = 80;
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.28f, Pitch = clearing ? -0.18f : 0.22f, MaxInstances = 2,
			}, player?.Center ?? Main.LocalPlayer.Center);
		}

		/// <summary>Emergency swap that never opens the wheel. A designated
		/// panic yoyo always wins; otherwise it falls back to the most recently
		/// used carried yoyo other than the one in hand, then any other carried
		/// yoyo. Locked hotbar yoyos are deliberately left out. Unbound by
		/// default.</summary>
		public void EquipPanicYoyo(Player player)
		{
			if (player == null || !player.active || player.dead)
				return;
			if (!StringSplicer.HasSplicer(player))
				return;
			var carried = StringSplicer.CollectTypes(player);
			var cycle = StringSplicer.CollectCycleTypes(player);
			if (cycle.Count == 0) {
				if (carried.Count > 0)
					StringSplicer.Tell(player, "Splicer: every carried yoyo is locked.");
				return;
			}
			int current = StringSplicer.DisplayedType(player);
			if (PanicType > 0 && cycle.Contains(PanicType)) {
				// If the panic yoyo is already in hand, do not cycle away from
				// it. A panic key should be a safe destination, not a toggle.
				if (PanicType != current)
					StringSplicer.Equip(player, PanicType);
				return;
			}
			int type = 0;
			foreach (int recent in RecentTypes) {
				if (recent > 0 && recent != current && cycle.Contains(recent)) {
					type = recent;
					break;
				}
			}
			if (type <= 0) {
				foreach (int other in cycle) {
					if (other > 0 && other != current) {
						type = other;
						break;
					}
				}
			}
			if (type <= 0)
				return;
			if (WheelOpen)
				CloseWheel();
			StringSplicer.Equip(player, type);
		}

		/// <summary>Sets or clears the lead yoyo. A board switch selects the
		/// lead when it is carried and present on that board; auto-COMBAT still
		/// snaps to the yoyo already in hand.</summary>
		public void SetLeadType(Player player, int type)
		{
			if (type <= 0)
				return;
			bool clearing = LeadType == type;
			LeadType = clearing ? 0 : type;
			PinNotice = clearing ? "LEAD CLEARED" : "LEAD SET";
			PinNoticeTimer = 80;
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.28f, Pitch = clearing ? -0.16f : 0.26f, MaxInstances = 2,
			}, player?.Center ?? Main.LocalPlayer.Center);
		}

		/// <summary>Adds or removes a yoyo from the active board's splice
		/// chain. Alt+click is the only editor gesture; the chain itself is
		/// advanced by the optional unbound Splice Sequence key.</summary>
		public void ToggleSequence(Player player, int type)
		{
			if (type <= 0)
				return;
			int board = Math.Clamp(Board, 0, PinBoardCount);
			var sequence = Sequences[board];
			if (sequence.Remove(type)) {
				PinNotice = "SPLICE REMOVED";
				PinNoticeTimer = 80;
			}
			else if (sequence.Count >= MaxSequence) {
				PinNotice = $"SPLICE FULL ({MaxSequence})";
				PinNoticeTimer = 90;
				SoundEngine.PlaySound(SoundID.MenuTick with {
					Volume = 0.2f, Pitch = -0.3f, MaxInstances = 2,
				}, player.Center);
				return;
			}
			else {
				sequence.Add(type);
				PinNotice = "SPLICE ADDED";
				PinNoticeTimer = 80;
			}
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.3f, Pitch = sequence.Contains(type) ? 0.22f : -0.12f, MaxInstances = 2,
			}, player.Center);
		}

		/// <summary>Walks the active board's splice chain in a loop. Missing
		/// or no-longer-carried entries are skipped without rewriting the
		/// saved chain, so a stashed yoyo returns to the rotation when it is
		/// picked up again.</summary>
		public void AdvanceSequence(Player player)
		{
			if (WheelOpen)
				return;
			if (player == null || !player.active || player.dead)
				return;
			if (!StringSplicer.HasSplicer(player))
				return;
			var sequence = Sequences[Math.Clamp(Board, 0, PinBoardCount)];
			var carried = StringSplicer.CollectTypes(player);
			var valid = new List<int>();
			foreach (int type in sequence) {
				if (type > 0 && carried.Contains(type) && !valid.Contains(type))
					valid.Add(type);
			}
			if (valid.Count == 0) {
				StringSplicer.Tell(player,
					$"Splicer: no splice sequence on {StringSplicerPlayer.BoardName(Board)}.");
				return;
			}
			int current = StringSplicer.DisplayedType(player);
			int index = valid.IndexOf(current);
			int next = index < 0 ? 0 : (index + 1) % valid.Count;
			int target = valid[next];
			if (!StringSplicer.Equip(player, target))
				return;
			// Equip announces "EQUIPPED"; this is the one place the caption
			// should say which chain the player is walking.
			ShowToast(target, ItemName(target), next + 1, valid.Count, "SPLICE");
		}

		public void ToggleLockEditor(Player player)
		{
			LockEditor = !LockEditor;
			if (LockEditor) {
				PinEditor = false;
				Filter = "";
				FilterFocus = false;
				ForgetFilterView();
			}
			int keep = WheelTypes.Count > 0
				? WheelTypes[Math.Clamp(WheelSelected, 0, WheelTypes.Count - 1)]
				: StringSplicer.DisplayedType(player);
			RebuildWheel(player, keep);
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.28f, Pitch = LockEditor ? 0.18f : -0.10f, MaxInstances = 2,
			}, player.Center);
		}

		public void ToggleLockedSlot(Player player, int slot)
		{
			if (slot < 0 || slot >= LockedHotbar.Length)
				return;
			LockedHotbar[slot] = !LockedHotbar[slot];
			PinNotice = LockedHotbar[slot] ? "HOTBAR SLOT LOCKED" : "HOTBAR SLOT UNLOCKED";
			PinNoticeTimer = 80;
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.28f, Pitch = LockedHotbar[slot] ? 0.24f : -0.14f, MaxInstances = 2,
			}, player.Center);
		}

		/// <summary>Instant next/previous carried yoyo without opening the wheel.
		/// The order is cached from the inventory at the first press and kept
		/// stable while Equip swaps items between hotbar slots.</summary>
		public void CycleCarriedYoyo(Player player, int direction)
		{
			if (player == null || !player.active || player.dead)
				return;
			if (!StringSplicer.HasSplicer(player))
				return;
			var carried = StringSplicer.CollectCycleTypes(player);
			if (carried.Count == 0) {
				if (StringSplicer.CollectTypes(player).Count > 0)
					StringSplicer.Tell(player, "Splicer: every carried yoyo is locked.");
				return;
			}
			if (!SameTypes(quickCycleOrder, carried)) {
				quickCycleOrder.Clear();
				quickCycleOrder.AddRange(carried);
			}
			int current = StringSplicer.DisplayedType(player);
			if (carried.Count == 1) {
				if (current != carried[0])
					StringSplicer.Equip(player, carried[0]);
				return;
			}
			int step = direction >= 0 ? 1 : -1;
			int index = quickCycleOrder.IndexOf(current);
			int next = index < 0
				? (step > 0 ? 0 : quickCycleOrder.Count - 1)
				: (index + step) % quickCycleOrder.Count;
			if (next < 0)
				next += quickCycleOrder.Count;
			int type = quickCycleOrder[next];
			if (type > 0 && type != current)
				StringSplicer.Equip(player, type);
		}

		private static bool SameTypes(List<int> a, List<int> b)
		{
			if (a.Count != b.Count)
				return false;
			foreach (int type in a) {
				if (!b.Contains(type))
					return false;
			}
			return true;
		}

		private static bool AnyBossAlive()
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc.active && npc.boss && !npc.friendly && npc.life > 0)
					return true;
			}
			return false;
		}

		public void OpenWheel(Player player)
		{
			if (player == null || !player.active || player.dead)
				return;
			if (Main.playerInventory || Main.mapFullscreen || Main.gameMenu)
				return;

			var types = StringSplicer.CollectTypes(player);
			if (types.Count == 0)
				return;

			if (Board < 0 || Board > PinBoardCount)
				Board = 0;
			if (SortMode < 0 || SortMode >= SortNames.Length)
				SortMode = 0;

			// During a boss fight, open straight onto COMBAT when it has a
			// carried yoyo. The setting keeps this predictable and reversible.
			bool autoCombat = false;
			var config = ModContent.GetInstance<YoyoConfig>();
			if (config != null && config.SplicerAutoCombatBoard
				&& Board != 1 && AnyBossAlive() && PinBoards[0].Count > 0) {
				foreach (int type in types) {
					if (PinBoards[0].Contains(type)) {
						autoCombat = true;
						break;
					}
				}
				if (autoCombat) {
					Board = 1;
					LastPinBoard = 1;
					WheelPage = 0;
					WheelSelected = 0;
					// A stale query would hide the combat pins exactly when
					// they are needed most.
					Filter = "";
					FilterFocus = false;
					ForgetFilterView();
				}
			}

			PinEditor = false;
			LockEditor = false;
			FilterFocus = false;
			ToastTimer = 0;
			PinNoticeTimer = 0;
			WheelOpenTicks = 0;
			WheelCloseTicks = 0;
			WheelOpen = true;
			wheelMouseMoved = false;
			wheelOpenMouse = UiMouse;
			prevLeft = Main.mouseLeft;
			prevRight = Main.mouseRight;
			prevKeys = Main.keyState.GetPressedKeys();
			inputPrevLeft = Main.mouseLeft;
			inputPrevRight = Main.mouseRight;
			wheelLeftClick = false;
			wheelRightClick = false;

			// Reopen exactly where the player left the instrument: same board,
			// page and filter. Board and sort already persist; the page/filter
			// are carried in-session here and collapsed only by HardReset.
			// The automatic combat board instead snaps to the held yoyo, or
			// the first combat pin when that yoyo is not on the board.
			int preferred = autoCombat ? StringSplicer.DisplayedType(player) : -1;
			RebuildWheel(player, preferred, preservePage: !autoCombat);
			SelectionGlide = WheelSelected;
			SelectionGlideCount = WheelTypes.Count;

			SoundEngine.PlaySound(SoundID.MenuOpen with {
				Volume = 0.34f, Pitch = 0.22f, MaxInstances = 2,
			}, player.Center);
		}

		public void ConfirmWheel()
		{
			if (!WheelOpen)
				return;
			// Enter in pin- or lock-editing mode finishes that editor and
			// leaves the wheel open so the player can inspect or equip another
			// yoyo.
			if (PinEditor) {
				TogglePinEditor(Player);
				return;
			}
			if (LockEditor) {
				ToggleLockEditor(Player);
				return;
			}
			int count = WheelTypes.Count;
			int index = Math.Clamp(WheelSelected, 0, Math.Max(0, count - 1));
			int type = count > 0 ? WheelTypes[index] : 0;
			CloseWheel();
			if (type > 0)
				StringSplicer.Equip(Player, type);
		}

		public void CancelWheel()
		{
			if (!WheelOpen) {
				HardReset();
				return;
			}
			CloseWheel();
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.2f, Pitch = -0.2f, MaxInstances = 2,
			}, Player.Center);
		}

		private void CloseWheel()
		{
			WheelOpen = false;
			WheelCloseTicks = 8;
			FilterFocus = false;
			LockEditor = false;
			// Keep the wheel data during the close animation; it is cleared once
			// the instrument has faded out.
		}

		/// <summary>Drops any open wheel immediately.</summary>
		public void HardReset()
		{
			WheelOpen = false;
			WheelOpenTicks = 0;
			WheelCloseTicks = 0;
			WheelTypes.Clear();
			WheelPool.Clear();
			WheelSelected = 0;
			SelectionGlide = 0f;
			SelectionGlideCount = 0;
			WheelPage = 0;
			WheelPageCount = 0;
			WheelFilteredCount = 0;
			Filter = "";
			FilterFocus = false;
			ForgetFilterView();
			PinEditor = false;
			LockEditor = false;
			PinNoticeTimer = 0;
			wheelMouseMoved = false;
			wheelLeftClick = false;
			wheelRightClick = false;
			inputPrevLeft = Main.mouseLeft;
			inputPrevRight = Main.mouseRight;
		}

		/// <summary>Called from PostUpdateInput while the vanilla triggers are
		/// still intact. Latches a press edge so the wheel never loses a click
		/// when the trigger set is consumed later in the same tick.</summary>
		public void CaptureWheelClickEdges()
		{
			bool left = Main.mouseLeft;
			bool right = Main.mouseRight;
			if (WheelOpen) {
				if (left && !inputPrevLeft)
					wheelLeftClick = true;
				if (right && !inputPrevRight)
					wheelRightClick = true;
			}
			else {
				wheelLeftClick = false;
				wheelRightClick = false;
			}
			inputPrevLeft = left;
			inputPrevRight = right;
		}

		// ------------------------------------------------------------- boards

		public void CycleBoard(Player player, int direction)
		{
			int next = Board + direction;
			if (next < 0)
				next = PinBoardCount;
			if (next > PinBoardCount)
				next = 0;
			SetBoard(player, next);
		}

		public void SetBoard(Player player, int board)
		{
			board = Math.Clamp(board, 0, PinBoardCount);
			FilterFocus = false;
			if (board == Board)
				return;
			Board = board;
			if (board > 0)
				LastPinBoard = board;
			int keep = WheelTypes.Count > 0
				? WheelTypes[Math.Clamp(WheelSelected, 0, WheelTypes.Count - 1)]
				: StringSplicer.DisplayedType(player);
			// A board switch is a deliberate change of working set, so the
			// lead yoyo takes the selection when it belongs to the new board.
			if (LeadType > 0) {
				bool onBoard = board == 0 || PinBoards[board - 1].Contains(LeadType);
				if (onBoard && StringSplicer.CollectTypes(player).Contains(LeadType))
					keep = LeadType;
			}
			RebuildWheel(player, keep);
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.24f, Pitch = 0.08f, MaxInstances = 2,
			}, player.Center);
		}

		public void TogglePinEditor(Player player)
		{
			if (Board == 0)
				Board = Math.Clamp(LastPinBoard, 1, PinBoardCount);
			PinEditor = !PinEditor;
			if (PinEditor)
				LockEditor = false;
			Filter = "";
			FilterFocus = false;
			ForgetFilterView();
			int keep = WheelTypes.Count > 0
				? WheelTypes[Math.Clamp(WheelSelected, 0, WheelTypes.Count - 1)]
				: StringSplicer.DisplayedType(player);
			RebuildWheel(player, keep);
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.28f, Pitch = PinEditor ? 0.22f : -0.08f, MaxInstances = 2,
			}, player.Center);
		}

		public void TogglePin(Player player, int type)
		{
			if (type <= 0)
				return;
			// Quick-pin from MAIN needs a destination. Use the last pin board
			// the player worked on instead of forcing a board switch first.
			int target = Board > 0 ? Board : Math.Clamp(LastPinBoard, 1, PinBoardCount);
			var board = PinBoards[target - 1];
			if (board.Remove(type)) {
				PinNotice = Board > 0 ? "YOYO UNPINNED" : $"UNPINNED FROM {BoardName(target)}";
				PinNoticeTimer = 80;
			}
			else {
				if (board.Count >= MaxPins) {
					PinNotice = $"BOARD FULL ({MaxPins})";
					PinNoticeTimer = 90;
					SoundEngine.PlaySound(SoundID.MenuTick with {
						Volume = 0.2f, Pitch = -0.3f, MaxInstances = 2,
					}, player.Center);
					return;
				}
				board.Add(type);
				PinNotice = Board > 0 ? "YOYO PINNED" : $"PINNED TO {BoardName(target)}";
				PinNoticeTimer = 80;
			}

			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.3f, Pitch = board.Contains(type) ? 0.25f : -0.15f, MaxInstances = 2,
			}, player.Center);
			RebuildWheel(player, type);
		}

		public void CycleSort(Player player)
		{
			// Pin boards are manually curated. Sorting them would visually
			// reorder favorites, so the sort control is MAIN-only.
			if (Board > 0 && !PinEditor) {
				PinNotice = "PIN ORDER IS MANUAL — SORT APPLIES TO MAIN";
				PinNoticeTimer = 80;
				SoundEngine.PlaySound(SoundID.MenuTick with {
					Volume = 0.2f, Pitch = -0.18f, MaxInstances = 2,
				}, player.Center);
				return;
			}
			SortMode = (SortMode + 1) % SortNames.Length;
			int keep = WheelTypes.Count > 0
				? WheelTypes[Math.Clamp(WheelSelected, 0, WheelTypes.Count - 1)]
				: StringSplicer.DisplayedType(player);
			RebuildWheel(player, keep);
			SoundEngine.PlaySound(SoundID.MenuTick with {
				Volume = 0.22f, Pitch = 0.12f, MaxInstances = 2,
			}, player.Center);
		}

		// ------------------------------------------------------------- pool

		public void RebuildWheel(Player player, int preferredType = -1, bool preservePage = false)
		{
			int previousPage = Math.Max(0, WheelPage);
			int previousSlot = Math.Clamp(WheelSelected, 0, WheelPageSize - 1);
			var all = StringSplicer.CollectTypes(player);
			var source = new List<int>();

			if (PinEditor || Board == 0) {
				source.AddRange(all);
			}
			else {
				// Board views are curated lists: keep the order the player
				// pinned, not inventory order. SORT only changes the MAIN
				// wheel; it must never reshuffle a favorite board.
				var carried = new HashSet<int>(all);
				foreach (int type in PinBoards[Board - 1]) {
					if (carried.Contains(type))
						source.Add(type);
				}
			}

			if (!string.IsNullOrWhiteSpace(Filter)) {
				string needle = Filter.Trim();
				for (int i = source.Count - 1; i >= 0; i--) {
					if (!YoyoSpliceRoles.MatchesQuery(source[i], needle))
						source.RemoveAt(i);
				}
			}

			// Manual pin order wins on boards. The pin editor itself is a
			// full-inventory picker, so it still follows the active sort.
			if (PinEditor || Board == 0)
				SortTypes(source, all);

			if (!preservePage && preferredType <= 0) {
				if (WheelTypes.Count > 0 && WheelSelected >= 0 && WheelSelected < WheelTypes.Count)
					preferredType = WheelTypes[WheelSelected];
				else
					preferredType = StringSplicer.DisplayedType(player);
			}

			WheelPool.Clear();
			WheelPool.AddRange(source);
			WheelFilteredCount = WheelPool.Count;
			WheelPageCount = Math.Max(1, (WheelFilteredCount + WheelPageSize - 1) / WheelPageSize);

			if (preservePage) {
				// Remembered view: keep the player's page and slot, clamped to
				// whatever the current inventory/filter still contains. This is
				// what makes reopening feel like returning to the same drawer.
				WheelPage = Math.Clamp(previousPage, 0, WheelPageCount - 1);
				int pageStart = WheelPage * WheelPageSize;
				if (WheelFilteredCount == 0) {
					WheelSelected = 0;
				}
				else {
					int last = Math.Min(WheelFilteredCount, pageStart + WheelPageSize) - 1;
					WheelSelected = Math.Clamp(pageStart + previousSlot, pageStart,
						Math.Max(pageStart, last)) - pageStart;
				}
			}
			else {
				int found = preferredType > 0 ? WheelPool.IndexOf(preferredType) : -1;
				if (found >= 0) {
					WheelPage = found / WheelPageSize;
					WheelSelected = found % WheelPageSize;
				}
				else {
					WheelPage = Math.Clamp(WheelPage, 0, WheelPageCount - 1);
					int absolute = WheelPage * WheelPageSize + Math.Clamp(WheelSelected, 0, WheelPageSize - 1);
					if (WheelFilteredCount == 0)
						WheelSelected = 0;
					else if (absolute >= WheelFilteredCount)
						WheelSelected = Math.Clamp(WheelFilteredCount - 1 - WheelPage * WheelPageSize, 0, WheelPageSize - 1);
				}

				if (WheelPage >= WheelPageCount)
					WheelPage = WheelPageCount - 1;
			}

			WheelTypes.Clear();
			int start = WheelPage * WheelPageSize;
			int end = Math.Min(WheelFilteredCount, start + WheelPageSize);
			for (int i = start; i < end; i++)
				WheelTypes.Add(WheelPool[i]);
			if (WheelTypes.Count == 0) {
				WheelSelected = 0;
				SelectionGlide = 0f;
				SelectionGlideCount = 0;
			}
			else {
				WheelSelected = Math.Clamp(WheelSelected, 0, WheelTypes.Count - 1);
				// A different number of slices means the layout itself changed
				// (filter, board or page). Snap the focus there; ordinary
				// selection changes keep gliding.
				if (SelectionGlideCount != WheelTypes.Count) {
					SelectionGlide = WheelSelected;
					SelectionGlideCount = WheelTypes.Count;
				}
			}
		}

		// Remember where the player was before the first typed character. When
		// the query is cleared, restore that page/slot instead of following the
		// last search hit (which can be on a short final page).
		private void RememberFilterView()
		{
			if (Filter.Length == 0 && filterReturnPage < 0) {
				filterReturnPage = WheelPage;
				filterReturnSlot = WheelSelected;
			}
		}

		private bool RestoreFilterView(Player player)
		{
			if (filterReturnPage < 0)
				return false;
			WheelPage = filterReturnPage;
			WheelSelected = filterReturnSlot;
			filterReturnPage = -1;
			RebuildWheel(player, preservePage: true);
			return true;
		}

		private void ForgetFilterView()
		{
			filterReturnPage = -1;
		}

		private void UpdateSelectionGlide()
		{
			int n = WheelTypes.Count;
			if (n <= 0) {
				SelectionGlide = 0f;
				SelectionGlideCount = 0;
				return;
			}
			if (SelectionGlideCount != n) {
				SelectionGlide = WheelSelected;
				SelectionGlideCount = n;
			}

			float target = WheelSelected;
			float delta = target - SelectionGlide;
			// Take the shortest way around the wheel, so moving from the last
			// slice to the first sweeps one notch instead of the long way.
			if (delta > n * 0.5f)
				delta -= n;
			else if (delta < -n * 0.5f)
				delta += n;

			SelectionGlide += delta * 0.30f;
			if (Math.Abs(delta) < 0.0025f)
				SelectionGlide = target;

			if (SelectionGlide < -0.5f)
				SelectionGlide += n;
			else if (SelectionGlide > n - 0.5f)
				SelectionGlide -= n;
		}

		private void SortTypes(List<int> types, List<int> inventoryOrder)
		{
			if (SortMode == 0 || SortMode == 4 && RecentTypes.Count == 0)
				return;

			var order = new Dictionary<int, int>();
			for (int i = 0; i < inventoryOrder.Count; i++)
				order[inventoryOrder[i]] = i;

			if (SortMode == 4) {
				var recent = new Dictionary<int, int>();
				for (int i = 0; i < RecentTypes.Count; i++)
					recent[RecentTypes[i]] = i;
				types.Sort((a, b) => {
					bool ra = recent.TryGetValue(a, out int ia);
					bool rb = recent.TryGetValue(b, out int ib);
					if (ra != rb)
						return ra ? -1 : 1;
					if (ra && ia != ib)
						return ia.CompareTo(ib);
					return order.TryGetValue(a, out int oa) && order.TryGetValue(b, out int ob)
						? oa.CompareTo(ob) : a.CompareTo(b);
				});
				return;
			}

			if (SortMode == 3) {
				types.Sort((a, b) => {
					int c = string.Compare(ItemName(a), ItemName(b), StringComparison.OrdinalIgnoreCase);
					return c != 0 ? c : a.CompareTo(b);
				});
				return;
			}

			types.Sort((a, b) => {
				Item sa = Sample(a);
				Item sb = Sample(b);
				int c = SortMode == 2
					? (sb?.rare ?? 0).CompareTo(sa?.rare ?? 0)
					: (sb?.damage ?? 0).CompareTo(sa?.damage ?? 0);
				if (c != 0)
					return c;
				c = (sb?.damage ?? 0).CompareTo(sa?.damage ?? 0);
				if (c != 0)
					return c;
				int oa = order.TryGetValue(a, out int ia) ? ia : int.MaxValue;
				int ob = order.TryGetValue(b, out int ib) ? ib : int.MaxValue;
				return oa.CompareTo(ob);
			});
		}

		private static Item Sample(int type)
		{
			try {
				return ContentSamples.ItemsByType.TryGetValue(type, out Item item) ? item : null;
			}
			catch {
				return null;
			}
		}

		public static string ItemName(int type)
		{
			try { return Lang.GetItemNameValue(type) ?? "Yoyo"; }
			catch { return "Yoyo"; }
		}

		// -------------------------------------------------------------- input

		public override void PostUpdate()
		{
			if (ToastTimer > 0)
				ToastTimer--;
			if (PinNoticeTimer > 0)
				PinNoticeTimer--;

			if (WheelCloseTicks > 0) {
				WheelCloseTicks--;
				if (WheelCloseTicks == 0) {
					// Keep WheelPage, WheelSelected and Filter as the remembered
					// view for the next opening. Only transient slice data and
					// the pin-editing mode are discarded.
					WheelTypes.Clear();
					WheelPool.Clear();
					WheelPageCount = 0;
					WheelFilteredCount = 0;
					PinEditor = false;
				}
			}

			if (!WheelOpen) {
				prevLeft = Main.mouseLeft;
				prevRight = Main.mouseRight;
				prevKeys = Main.keyState.GetPressedKeys();
				return;
			}

			if (Player.whoAmI != Main.myPlayer || Player.dead || !Player.active
				|| Main.playerInventory || Main.mapFullscreen || Main.gameMenu || Main.dedServ
				|| !StringSplicer.HasSplicer(Player)) {
				HardReset();
				return;
			}

			bool leftClick = wheelLeftClick || (Main.mouseLeft && !prevLeft);
			bool rightClick = wheelRightClick || (Main.mouseRight && !prevRight);
			wheelLeftClick = false;
			wheelRightClick = false;

			if (WheelOpenTicks < 60)
				WheelOpenTicks++;
			UpdateSelectionGlide();

			// Refresh the carried list if the inventory changed underneath us
			// (quick Trash, pickup, etc.). Preserve the selected type.
			var carried = StringSplicer.CollectTypes(Player);
			if (carried.Count != WheelPool.Count && !PinEditor && Board == 0 && Filter.Length == 0) {
				int keep = WheelTypes.Count > 0
					? WheelTypes[Math.Clamp(WheelSelected, 0, WheelTypes.Count - 1)] : -1;
				RebuildWheel(Player, keep);
			}

			Keys[] keys = Main.keyState.GetPressedKeys();
			bool shift = ShiftDown(keys);
			bool alt = AltDown(keys);
			Point mp = (UiMouse).ToPoint();

			if (JustPressed(keys, Keys.Escape)) {
				if (Filter.Length > 0) {
					Filter = "";
					FilterFocus = false;
					int keep = WheelTypes.Count > 0
						? WheelTypes[Math.Clamp(WheelSelected, 0, WheelTypes.Count - 1)] : -1;
					if (!RestoreFilterView(Player))
						RebuildWheel(Player, keep);
				}
				else if (LockEditor) {
					LockEditor = false;
					RebuildWheel(Player);
				}
				else if (PinEditor) {
					PinEditor = false;
					RebuildWheel(Player);
				}
				else {
					CancelWheel();
				}
				FinishInput(keys);
				return;
			}
			if (JustPressed(keys, Keys.Tab)) {
				CycleBoard(Player, shift ? -1 : 1);
				FinishInput(keys);
				return;
			}
			// Sorting and the pin editor take Ctrl so ordinary letters can be
			// typed into the filter. A bare S/P must remain search input.
			bool ctrl = ControlDown(keys);
			// Ctrl+F focuses the search plate from anywhere in the wheel, just
			// like a desktop command palette. Clicking SEARCH still works.
			if (ctrl && JustPressed(keys, Keys.F)) {
				FilterFocus = true;
				FinishInput(keys);
				return;
			}
			if (ctrl && JustPressed(keys, Keys.S)) {
				CycleSort(Player);
				FinishInput(keys);
				return;
			}
			if (ctrl && JustPressed(keys, Keys.P)) {
				TogglePinEditor(Player);
				FinishInput(keys);
				return;
			}
			if (ctrl && JustPressed(keys, Keys.L)) {
				ToggleLockEditor(Player);
				FinishInput(keys);
				return;
			}
			// Direct board jumps. Ctrl keeps these separate from the 1-9
			// medallion shortcuts and from search text entry.
			if (ctrl) {
				for (int n = 1; n <= 4; n++) {
					if (JustPressed(keys, Keys.D0 + n)
						|| JustPressed(keys, Keys.NumPad0 + n)) {
						SetBoard(Player, n - 1);
						FinishInput(keys);
						return;
					}
				}
			}
			if (JustPressed(keys, Keys.Enter)) {
				ConfirmWheel();
				FinishInput(keys);
				return;
			}

			// Mouse wheel and number keys are no-aim selection. The mouse angle
			// remains available, but a boss fight should not require aiming at a
			// tiny sector.
			int scroll = PendingScroll;
			PendingScroll = 0;
			if (scroll != 0 && WheelFilteredCount > 0)
				MoveSelection(-Math.Sign(scroll));
			// Number shortcuts are only active outside the search field; once
			// the player is typing, digits belong to the query.
			if (!FilterFocus) {
				for (int n = 1; n <= 9 && n <= WheelTypes.Count; n++) {
					if (JustPressed(keys, Keys.D0 + n)) {
						WheelSelected = n - 1;
						break;
					}
				}
				if (JustPressed(keys, Keys.D0) && WheelTypes.Count >= 10)
					WheelSelected = 9;
			}

			// Text entry is explicit: click the SEARCH plate first. This keeps
			// ordinary letters from fighting the Swap/tech keybinds and makes
			// the field's focus/focus-loss behavior predictable.
			bool typed = false;
			if (FilterFocus && !ctrl) {
				foreach (Keys key in keys) {
					if (!JustPressed(keys, key))
						continue;
					char c = KeyToChar(key, shift);
					// Strict Swap toggle: the assigned Swap key must never be
					// appended to the query on its way to closing the wheel.
					if (c != '\0' && !YoyoTechKeybind.IsSwapCharacter(c)
						&& !YoyoTechKeybind.IsPanicCharacter(c)
						&& Filter.Length < 28) {
						RememberFilterView();
						Filter += c;
						typed = true;
					}
				}
			}
			// Delete wipes the query in one press. Ctrl+Backspace is the
			// familiar desktop alias for the same action.
			if (FilterFocus && Filter.Length > 0
				&& (JustPressed(keys, Keys.Delete)
					|| (ctrl && JustPressed(keys, Keys.Back)))) {
				Filter = "";
				typed = true;
			}
			if (FilterFocus && JustPressed(keys, Keys.Back) && Filter.Length > 0) {
				Filter = Filter.Substring(0, Filter.Length - 1);
				typed = true;
			}
			if (typed) {
				int keep = WheelTypes.Count > 0
					? WheelTypes[Math.Clamp(WheelSelected, 0, WheelTypes.Count - 1)] : -1;
				if (Filter.Length == 0) {
					// Back to the pre-search page/slot, not the last match.
					if (!RestoreFilterView(Player))
						RebuildWheel(Player, keep);
				}
				else {
					RebuildWheel(Player, keep);
				}
			}

			// Right-click is a cancel gesture only. It is never a swap action.
			if (rightClick) {
				CancelWheel();
				FinishInput(keys);
				return;
			}
			if (leftClick) {
				// Clicking any control other than the search plate leaves text
				// entry, so a later Swap press closes the wheel normally.
				if (!StringSplicerUiSystem.FilterRect.Contains(mp))
					FilterFocus = false;
				if (LockEditor) {
					StringSplicerUiSystem.RefreshLockSlotHitboxes(this);
					int lockHit = StringSplicerUiSystem.LockSlotAt(mp);
					if (lockHit >= 0) {
						ToggleLockedSlot(Player, lockHit);
						FinishInput(keys);
						return;
					}
				}
				StringSplicerUiSystem.RefreshBoardTabHitboxes(this);
				int boardHit = StringSplicerUiSystem.BoardTabAt(mp);
				if (boardHit >= 0) {
					SetBoard(Player, boardHit);
					FinishInput(keys);
					return;
				}
				int pageHit = StringSplicerUiSystem.PageDotAt(mp);
				if (pageHit >= 0) {
					int target = pageHit * WheelPageSize;
					if (target < WheelPool.Count) {
						WheelPage = pageHit;
						WheelSelected = 0;
						RebuildWheel(Player, WheelPool[target]);
						SelectionGlide = WheelSelected;
						SelectionGlideCount = WheelTypes.Count;
					}
					FinishInput(keys);
					return;
				}
				if (StringSplicerUiSystem.SortChipRect.Contains(mp)) {
					CycleSort(Player);
					FinishInput(keys);
					return;
				}
				if (StringSplicerUiSystem.PinChipRect.Contains(mp)) {
					TogglePinEditor(Player);
					FinishInput(keys);
					return;
				}
				if (StringSplicerUiSystem.LockChipRect.Contains(mp)) {
					ToggleLockEditor(Player);
					FinishInput(keys);
					return;
				}
				if (!StringSplicerUiSystem.FilterClearRect.IsEmpty
					&& StringSplicerUiSystem.FilterClearRect.Contains(mp)) {
					Filter = "";
					FilterFocus = true;
					int keep = WheelTypes.Count > 0
						? WheelTypes[Math.Clamp(WheelSelected, 0, WheelTypes.Count - 1)]
						: StringSplicer.DisplayedType(Player);
					if (!RestoreFilterView(Player))
						RebuildWheel(Player, keep);
					SelectionGlide = WheelSelected;
					SelectionGlideCount = WheelTypes.Count;
					FinishInput(keys);
					return;
				}
				if (StringSplicerUiSystem.FilterRect.Contains(mp)) {
					FilterFocus = true;
					FinishInput(keys);
					return;
				}

				FilterFocus = false;
				Vector2 click = UiMouse;
				float clickDist = (click - WheelCenter).Length();
				if (PinEditor) {
					int slice = WheelSliceAt(click);
					if (slice >= 0) {
						WheelSelected = slice;
						if (alt) {
							ToggleSequence(Player, WheelTypes[slice]);
							FinishInput(keys);
							return;
						}
						if (ctrl && shift) {
							SetLeadType(Player, WheelTypes[slice]);
							FinishInput(keys);
							return;
						}
						if (ctrl) {
							SetPanicType(Player, WheelTypes[slice]);
							FinishInput(keys);
							return;
						}
						TogglePin(Player, WheelTypes[slice]);
					}
					else if (clickDist > WheelOuter + 18f) {
						CancelWheel();
					}
				}
				else if (clickDist <= WheelOuter + 14f) {
					// Clicking a medallion equips that yoyo. Shift+click is a
					// quick pin/unpin gesture that leaves the wheel open, so a
					// combat board can be prepared without entering the editor.
					// Ctrl+click assigns the emergency yoyo instead of equipping.
					// Clicking the hub equips the highlighted one; clicking the
					// dimmed world outside the circle only closes the wheel.
					int slice = WheelSliceAt(click);
					if (slice >= 0) {
						WheelSelected = slice;
						if (alt) {
							ToggleSequence(Player, WheelTypes[slice]);
							FinishInput(keys);
							return;
						}
						if (ctrl && shift) {
							SetLeadType(Player, WheelTypes[slice]);
							FinishInput(keys);
							return;
						}
						if (ctrl) {
							SetPanicType(Player, WheelTypes[slice]);
							FinishInput(keys);
							return;
						}
						if (shift) {
							TogglePin(Player, WheelTypes[slice]);
							FinishInput(keys);
							return;
						}
					}
					ConfirmWheel();
				}
				else {
					CancelWheel();
				}
				FinishInput(keys);
				return;
			}

			// Ignore the first small mouse movement so opening the wheel while
			// aiming does not instantly jump to a random slice.
			Vector2 mouse = UiMouse;
			if (!wheelMouseMoved
				&& (mouse - wheelOpenMouse).LengthSquared() >= 18f * 18f)
				wheelMouseMoved = true;

			// Page dots live in the hub. Skip slice-hover while the pointer is
			// on a dot so a page click does not immediately reselect by angle.
			bool overPageDots = StringSplicerUiSystem.PageDotAt(mp) >= 0;
			if (wheelMouseMoved && !overPageDots) {
				int hoverSlice = WheelSliceAt(mouse);
				if (hoverSlice >= 0)
					WheelSelected = hoverSlice;
			}

			FinishInput(keys);
		}

		private void MoveSelection(int delta)
		{
			if (WheelFilteredCount <= 0)
				return;
			int current = WheelPage * WheelPageSize + WheelSelected;
			int absolute = (current + delta) % WheelFilteredCount;
			if (absolute < 0)
				absolute += WheelFilteredCount;
			int type = WheelPool[absolute];
			// Rebuild with the target type first; RebuildWheel then derives the
			// correct page and slot from it.
			RebuildWheel(Player, type);
		}

		private void FinishInput(Keys[] keys)
		{
			prevLeft = Main.mouseLeft;
			prevRight = Main.mouseRight;
			prevKeys = keys;
		}

		private bool JustPressed(Keys[] now, Keys key)
		{
			if (!Main.keyState.IsKeyDown(key))
				return false;
			foreach (Keys old in prevKeys) {
				if (old == key)
					return false;
			}
			return true;
		}

		private static bool ShiftDown(Keys[] keys)
		{
			foreach (Keys key in keys) {
				if (key == Keys.LeftShift || key == Keys.RightShift)
					return true;
			}
			return false;
		}

		private static bool ControlDown(Keys[] keys)
		{
			foreach (Keys key in keys) {
				if (key == Keys.LeftControl || key == Keys.RightControl)
					return true;
			}
			return false;
		}

		private static bool AltDown(Keys[] keys)
		{
			foreach (Keys key in keys) {
				if (key == Keys.LeftAlt || key == Keys.RightAlt)
					return true;
			}
			return false;
		}

		private static char KeyToChar(Keys key, bool shift)
		{
			if (key >= Keys.A && key <= Keys.Z) {
				char c = (char)('a' + (key - Keys.A));
				return shift ? char.ToUpperInvariant(c) : c;
			}
			if (key >= Keys.D0 && key <= Keys.D9)
				return (char)('0' + (key - Keys.D0));
			// The role search needs a colon; US layouts produce it with
			// Shift+semicolon. Keeping the unshifted semicolon is harmless
			// because it simply matches no item name.
			if (key == Keys.OemSemicolon)
				return shift ? ':' : ';';
			if (key == Keys.Space)
				return ' ';
			return '\0';
		}

		// ----------------------------------------------------------- persistence

		public override void SaveData(TagCompound tag)
		{
			for (int i = 0; i < PinBoards.Length; i++)
				tag["splicerPins" + i] = new List<int>(PinBoards[i]);
			for (int i = 0; i < Sequences.Length; i++)
				tag["splicerSequence" + i] = new List<int>(Sequences[i]);
			tag["splicerRecent"] = new List<int>(RecentTypes);
			tag["splicerBoard"] = Board;
			tag["splicerLastPinBoard"] = LastPinBoard;
			tag["splicerSort"] = SortMode;
			tag["splicerPanic"] = PanicType;
			tag["splicerLead"] = LeadType;
			var locked = new List<int>();
			for (int i = 0; i < LockedHotbar.Length; i++) {
				if (LockedHotbar[i])
					locked.Add(i);
			}
			tag["splicerLocks"] = locked;
		}

		public override void LoadData(TagCompound tag)
		{
			for (int i = 0; i < PinBoards.Length; i++) {
				PinBoards[i].Clear();
				if (tag.ContainsKey("splicerPins" + i)) {
					foreach (int type in tag.GetList<int>("splicerPins" + i)) {
						if (type > 0 && !PinBoards[i].Contains(type))
							PinBoards[i].Add(type);
					}
				}
				while (PinBoards[i].Count > MaxPins)
					PinBoards[i].RemoveAt(PinBoards[i].Count - 1);
			}
			for (int i = 0; i < Sequences.Length; i++) {
				Sequences[i].Clear();
				if (tag.ContainsKey("splicerSequence" + i)) {
					foreach (int type in tag.GetList<int>("splicerSequence" + i)) {
						if (type > 0 && !Sequences[i].Contains(type))
							Sequences[i].Add(type);
					}
				}
				while (Sequences[i].Count > MaxSequence)
					Sequences[i].RemoveAt(Sequences[i].Count - 1);
			}
			RecentTypes.Clear();
			if (tag.ContainsKey("splicerRecent")) {
				foreach (int type in tag.GetList<int>("splicerRecent")) {
					if (type > 0 && !RecentTypes.Contains(type))
						RecentTypes.Add(type);
				}
			}
			while (RecentTypes.Count > RecentMax)
				RecentTypes.RemoveAt(RecentTypes.Count - 1);
			Board = Math.Clamp(tag.GetInt("splicerBoard"), 0, PinBoardCount);
			LastPinBoard = Math.Clamp(tag.GetInt("splicerLastPinBoard"), 1, PinBoardCount);
			SortMode = Math.Clamp(tag.GetInt("splicerSort"), 0, SortNames.Length - 1);
			PanicType = Math.Max(0, tag.GetInt("splicerPanic"));
			LeadType = Math.Max(0, tag.GetInt("splicerLead"));
			Array.Clear(LockedHotbar, 0, LockedHotbar.Length);
			if (tag.ContainsKey("splicerLocks")) {
				foreach (int slot in tag.GetList<int>("splicerLocks")) {
					if (slot >= 0 && slot < LockedHotbar.Length)
						LockedHotbar[slot] = true;
				}
			}
		}

		public override void OnRespawn()
		{
			ToastTimer = 0;
			PinNoticeTimer = 0;
			HardReset();
		}
	}

	/// <summary>The wheel owns the mouse while it is open: clicks pick a yoyo
	/// instead of swinging a weapon or placing a tile.</summary>
	public class StringSplicerInputSystem : ModSystem
	{
		public override void PostUpdateInput()
		{
			if (Main.gameMenu || Main.dedServ)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;
			var state = player.GetModPlayer<StringSplicerPlayer>();
			state.CaptureWheelClickEdges();
			if (!state.WheelOpen) {
				state.PendingScroll = 0;
				return;
			}
			// Capture the wheel delta for the Splicer's page selection and then
			// consume it, so scrolling the radial menu cannot also scroll the
			// vanilla hotbar behind it.
			state.PendingScroll = PlayerInput.ScrollWheelDelta;
			PlayerInput.ScrollWheelDelta = 0;
			player.mouseInterface = true;
			player.controlInv = false;
			player.controlMap = false;
			player.controlUseItem = false;
			player.controlUseTile = false;
			// While the search plate is focused, letters and Space are text,
			// not movement/jump input. The rest of the wheel stays combat-safe:
			// the player can still move while merely choosing a yoyo.
			if (state.FilterFocus) {
				player.controlLeft = false;
				player.controlRight = false;
				player.controlUp = false;
				player.controlDown = false;
				player.controlJump = false;
				player.controlHook = false;
				player.controlMount = false;
				player.controlTorch = false;
				player.controlSmart = false;
				player.controlQuickHeal = false;
				player.controlQuickMana = false;
				player.controlThrow = false;
			}
			// Player.Update copies the trigger set into the control fields after
			// PostUpdateInput, so clearing the Player fields alone is not enough.
			// Always eat inventory/map/hotbar selection; the wheel owns digits
			// and Escape while it is open. Text entry also owns movement, jump,
			// grapple, quick-heal/mana/buff and loadout keys.
			try {
				var t = PlayerInput.Triggers.Current;
				t.MouseLeft = false;
				t.MouseRight = false;
				t.Inventory = false;
				t.MapFull = false;
				t.MapStyle = false;
				t.OpenCreativePowersMenu = false;
				t.LockOn = false;
				t.HotbarMinus = false;
				t.HotbarPlus = false;
				t.Hotbar1 = false;
				t.Hotbar2 = false;
				t.Hotbar3 = false;
				t.Hotbar4 = false;
				t.Hotbar5 = false;
				t.Hotbar6 = false;
				t.Hotbar7 = false;
				t.Hotbar8 = false;
				t.Hotbar9 = false;
				t.Hotbar10 = false;
				if (state.FilterFocus) {
					t.Up = false;
					t.Down = false;
					t.Left = false;
					t.Right = false;
					t.Jump = false;
					t.Throw = false;
					t.Grapple = false;
					t.SmartSelect = false;
					t.SmartCursor = false;
					t.QuickMount = false;
					t.QuickHeal = false;
					t.QuickMana = false;
					t.QuickBuff = false;
					t.Loadout1 = false;
					t.Loadout2 = false;
					t.Loadout3 = false;
				}
			}
			catch { }
		}
	}

	/// <summary>Draws the Splicer wheel and its toast. The wheel is a quiet
	/// circular instrument in the same dark-navy and muted-brass language as
	/// the NPC panels: one bezel, restrained engraved ticks, medallion icons,
	/// a comparison hub and a pin editor. It reserves a measured band above
	/// and below the circle so the tabs, filter, chips and hints never collide.
	/// All hitboxes are published here so the ModPlayer can handle clicks
	/// without duplicating layout math.</summary>
	public class StringSplicerUiSystem : ModSystem
	{
		public static readonly Rectangle[] BoardTabRects = new Rectangle[4];
		public static readonly List<Rectangle> PageDotRects = new();
		public static readonly Rectangle[] LockSlotRects = new Rectangle[10];
		public static Rectangle SortChipRect;
		public static Rectangle PinChipRect;
		public static Rectangle LockChipRect;
		public static Rectangle FilterRect;
		public static Rectangle FilterClearRect;

		private const float BoardTabTextScale = 0.48f;
		private const float BoardTabTracking = 1.6f;
		private const float BoardTabGap = 5f;
		private const int BoardTabHeight = 23;

		/// <summary>Shared tab measurement used by both drawing and hit tests.
		/// Keeping one layout routine means a tab can never be painted in one
		/// place and clicked in another.</summary>
		public static void LayoutBoardTabs(Vector2 center, float y, float[] widths,
			out float startX)
		{
			float total = 0f;
			for (int i = 0; i < 4; i++) {
				string label = StringSplicerPlayer.BoardNames[i];
				widths[i] = Math.Max(48f, TrackWidth(label, BoardTabTextScale, BoardTabTracking) + 22f);
				total += widths[i] + BoardTabGap;
			}
			total -= BoardTabGap;
			startX = center.X - total * 0.5f;
		}

		/// <summary>Recomputes the tab hitboxes from the current animation state
		/// before a click is tested. This avoids relying on a rectangle from a
		/// previous draw while the opening animation is still moving the row.</summary>
		public static void RefreshBoardTabHitboxes(StringSplicerPlayer state)
		{
			float scale = 0.92f + 0.08f * EaseOutBack(state.WheelOpenTicks / 10f);
			float ringTop = StringSplicerPlayer.WheelCenter.Y
				- StringSplicerPlayer.WheelOuter * scale;
			float y = ringTop - 69f;
			var widths = new float[4];
			LayoutBoardTabs(StringSplicerPlayer.WheelCenter, y, widths, out float x);
			for (int i = 0; i < 4; i++) {
				BoardTabRects[i] = new Rectangle((int)x, (int)y, (int)widths[i],
					BoardTabHeight);
				x += widths[i] + BoardTabGap;
			}
		}

		public static int BoardTabAt(Point p)
		{
			for (int i = 0; i < BoardTabRects.Length; i++) {
				Rectangle hit = BoardTabRects[i];
				if (hit.Width <= 0)
					continue;
				// Tabs are only 23 px tall. The shared viewport already makes
				// the hitbox exact, so only a small vertical cushion is needed;
				// a larger one would steal the top edge of the search plate.
				hit.Inflate(2, 3);
				if (hit.Contains(p))
					return i;
			}
			return -1;
		}

		public static int PageDotAt(Point p)
		{
			for (int i = 0; i < PageDotRects.Count; i++) {
				Rectangle hit = PageDotRects[i];
				hit.Inflate(1, 2);
				if (hit.Contains(p))
					return i;
			}
			return -1;
		}

		/// <summary>Publishes the ten hotbar-lock hitboxes from the same layout
		/// the footer draws. The row sits below the chips, where the splice
		/// rail lives when the editor is closed.</summary>
		public static void RefreshLockSlotHitboxes(StringSplicerPlayer state)
		{
			float scale = 0.92f + 0.08f * EaseOutBack(state.WheelOpenTicks / 10f);
			float y = StringSplicerPlayer.WheelCenter.Y
				+ StringSplicerPlayer.WheelOuter * scale + 50f;
			int available = Math.Max(160, StringSplicerPlayer.UiWidth - 28);
			const int gap = 3;
			const int labelWidth = 34;
			int slot = Math.Clamp((available - labelWidth - 8 - 9 * gap) / 10, 14, 22);
			int total = 10 * slot + 9 * gap;
			int start = (int)(StringSplicerPlayer.WheelCenter.X
				- (total + labelWidth + 8) * 0.5f) + labelWidth + 8;
			for (int i = 0; i < LockSlotRects.Length; i++) {
				LockSlotRects[i] = new Rectangle(start + i * (slot + gap), (int)y, slot, slot);
			}
		}

		public static int LockSlotAt(Point p)
		{
			for (int i = 0; i < LockSlotRects.Length; i++) {
				Rectangle hit = LockSlotRects[i];
				if (hit.Width <= 0)
					continue;
				hit.Inflate(1, 1);
				if (hit.Contains(p))
					return i;
			}
			return -1;
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: String Splicer UI",
					() => {
						DrawAll(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI));
		}

		private static void DrawAll(SpriteBatch sb)
		{
			if (Main.gameMenu || Main.dedServ)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
				return;
			var state = player.GetModPlayer<StringSplicerPlayer>();
			DrawWheel(sb, state, player);
			DrawToast(sb, state);
		}

		// --------------------------------------------------------------- helpers

		// Shared with the mod's NPC/panel widgets: dark navy shells, brass trim,
		// muted blue selection. Keeping these in one place prevents the Splicer
		// from drifting back into a bright, sci-fi palette.
		private static readonly Color PanelShell = new(12, 15, 26);
		private static readonly Color PanelEdge = new(150, 128, 84);
		private static readonly Color PanelInner = new(52, 62, 96);
		private static readonly Color ButtonFill = new(30, 36, 58);
		private static readonly Color ButtonHover = new(48, 58, 92);
		private static readonly Color ButtonSelected = new(70, 84, 130);
		private static readonly Color ButtonEdge = new(90, 98, 140);
		private static readonly Color ButtonEdgeHover = new(160, 170, 220);
		private static readonly Color TextMain = new(232, 236, 244);
		private static readonly Color TextMuted = new(150, 164, 184);
		private static readonly Color Gold = new(226, 195, 126);
		private static readonly Color GoldDim = new(150, 128, 84);
		private static readonly Color Brass = new(196, 164, 104);
		private static readonly Color BrassBright = new(238, 210, 146);
		private static readonly Color Steel = new(78, 90, 122);
		private static readonly Color BlueText = new(150, 196, 235);

		private static float EaseOutCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return 1f - (1f - t) * (1f - t) * (1f - t);
		}

		private static float EaseOutBack(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			const float c = 1.70158f;
			float u = t - 1f;
			return 1f + (c + 1f) * u * u * u + c * u * u;
		}

		private static void FillRect(SpriteBatch sb, Rectangle rect, Color color)
		{
			if (rect.Width <= 0 || rect.Height <= 0)
				return;
			// MagicPixel is a padded 2x2 sheet. Sampling the whole texture
			// stretches its transparent padding into bright blocks, so every
			// solid fill must sample the single white texel.
			sb.Draw(TextureAssets.MagicPixel.Value, rect, new Rectangle(0, 0, 1, 1), color);
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

		private static void DrawArc(SpriteBatch sb, Vector2 center, float radius, float start,
			float end, float thickness, Color color, int segments)
		{
			if (segments < 2)
				return;
			Vector2 prev = center + start.ToRotationVector2() * radius;
			for (int i = 1; i <= segments; i++) {
				float t = start + (end - start) * (i / (float)segments);
				Vector2 next = center + t.ToRotationVector2() * radius;
				DrawLine(sb, prev, next, color, thickness);
				prev = next;
			}
		}

		private static void DrawCentered(SpriteBatch sb, string text, Vector2 center, Color color, float scale)
		{
			if (string.IsNullOrEmpty(text))
				return;
			Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * scale;
			sb.DrawString(FontAssets.MouseText.Value, text,
				center - size * 0.5f, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private static void DrawCenteredShadow(SpriteBatch sb, string text, Vector2 center, Color color, float scale)
		{
			DrawCentered(sb, text, center + new Vector2(1f, 1f), new Color(0, 0, 0, 190) * (color.A / 255f), scale);
			DrawCentered(sb, text, center, color, scale);
		}

		private static void DrawFitCentered(SpriteBatch sb, string text, Vector2 center, Color color,
			float scale, float minScale, float maxWidth)
		{
			if (string.IsNullOrEmpty(text))
				return;
			while (scale > minScale
				&& FontAssets.MouseText.Value.MeasureString(text).X * scale > maxWidth)
				scale -= 0.02f;
			DrawCentered(sb, text, center, color, scale);
		}

		private static float TrackWidth(string text, float scale, float tracking)
		{
			if (string.IsNullOrEmpty(text))
				return 0f;
			float w = 0f;
			for (int i = 0; i < text.Length; i++) {
				w += FontAssets.MouseText.Value.MeasureString(text[i].ToString()).X * scale;
				if (i < text.Length - 1)
					w += tracking;
			}
			return w;
		}

		private static void DrawTracked(SpriteBatch sb, string text, Vector2 pos, Color color,
			float scale, float tracking)
		{
			if (string.IsNullOrEmpty(text))
				return;
			float x = pos.X;
			for (int i = 0; i < text.Length; i++) {
				string glyph = text[i].ToString();
				sb.DrawString(FontAssets.MouseText.Value, glyph, new Vector2(x, pos.Y), color,
					0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				x += FontAssets.MouseText.Value.MeasureString(glyph).X * scale + tracking;
			}
		}

		/// <summary>A dark instrument plaque rather than a flat UI rectangle.
		/// The corners are chamfered by two pixels, the brass rim sits on an
		/// opaque ink bed, and two small recessed screws catch the light. The
		/// same routine draws tabs, search, chips, the hover card and the toast,
		/// so every control belongs to one physical object.</summary>
		private static void DrawPlate(SpriteBatch sb, Rectangle r, Color fill, Color edge, float alpha)
		{
			if (r.Width < 3 || r.Height < 3)
				return;

			// Soft cast shadow, then an opaque ink bed. No world colour is
			// allowed to bleed through the instrument face.
			FillRect(sb, new Rectangle(r.X + 2, r.Y + 3, r.Width, r.Height),
				new Color(0, 0, 0, 138) * alpha);
			FillRect(sb, new Rectangle(r.X, r.Y, r.Width, r.Height),
				new Color(3, 5, 9) * alpha);

			// Chamfered body: rows narrow by one pixel at each end.
			FillRect(sb, new Rectangle(r.X + 2, r.Y, r.Width - 4, r.Height), fill * alpha);
			FillRect(sb, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), fill * alpha);
			FillRect(sb, new Rectangle(r.X, r.Y + 2, r.Width, r.Height - 4), fill * alpha);

			// Brass frame follows the cut corners instead of outlining a box.
			Color topEdge = edge * alpha;
			Color sideEdge = edge * (0.62f * alpha);
			Color lowEdge = edge * (0.40f * alpha);
			FillRect(sb, new Rectangle(r.X + 2, r.Y, r.Width - 4, 1), topEdge);
			FillRect(sb, new Rectangle(r.X + 1, r.Y + 1, 1, 1), edge * (0.72f * alpha));
			FillRect(sb, new Rectangle(r.Right - 2, r.Y + 1, 1, 1), edge * (0.72f * alpha));
			FillRect(sb, new Rectangle(r.X, r.Y + 2, 1, r.Height - 4), sideEdge);
			FillRect(sb, new Rectangle(r.Right - 1, r.Y + 2, 1, r.Height - 4), sideEdge);
			FillRect(sb, new Rectangle(r.X + 1, r.Bottom - 2, 1, 1), lowEdge);
			FillRect(sb, new Rectangle(r.Right - 2, r.Bottom - 2, 1, 1), lowEdge);
			FillRect(sb, new Rectangle(r.X + 2, r.Bottom - 1, r.Width - 4, 1), lowEdge);

			// Recessed bevel: a thin lit lip above, shadow below.
			FillRect(sb, new Rectangle(r.X + 3, r.Y + 1, r.Width - 6, 1),
				new Color(255, 246, 220, 22) * alpha);
			FillRect(sb, new Rectangle(r.X + 2, r.Bottom - 2, r.Width - 4, 1),
				new Color(0, 0, 0, 88) * alpha);
			FillRect(sb, new Rectangle(r.X + 2, r.Y + 2, 1, Math.Max(0, r.Height - 4)),
				new Color(255, 246, 220, 10) * alpha);

			// Two shallow brass screw heads. They are deliberately tiny: detail,
			// not decoration that competes with the label.
			int cy = r.Y + r.Height / 2;
			if (r.Width >= 76 && r.Height >= 16) {
				int lx = r.X + 5;
				int rx = r.Right - 6;
				FillRect(sb, new Rectangle(lx, cy, 2, 2), edge * (0.62f * alpha));
				FillRect(sb, new Rectangle(rx, cy, 2, 2), edge * (0.62f * alpha));
				FillRect(sb, new Rectangle(lx, cy, 1, 1), new Color(255, 244, 210, 96) * alpha);
				FillRect(sb, new Rectangle(rx, cy, 1, 1), new Color(255, 244, 210, 96) * alpha);
			}
		}

		private static Color RarityColor(int rare)
		{
			return rare switch {
				-1 => new Color(140, 140, 140),
				0 => new Color(220, 220, 220),
				1 => new Color(110, 160, 255),
				2 => new Color(100, 220, 120),
				3 => new Color(255, 160, 80),
				4 => new Color(255, 110, 110),
				5 => new Color(255, 130, 210),
				6 => new Color(190, 140, 255),
				7 => new Color(180, 240, 100),
				8 => new Color(255, 230, 110),
				9 => new Color(100, 240, 255),
				10 => new Color(255, 100, 140),
				11 => new Color(210, 130, 255),
				_ => new Color(226, 195, 126),
			};
		}

		private static Item Sample(int type)
		{
			try { return ContentSamples.ItemsByType.TryGetValue(type, out Item item) ? item : null; }
			catch { return null; }
		}

		private static string ItemName(int type)
			=> StringSplicerPlayer.ItemName(type);

		private static void DrawItem(SpriteBatch sb, int type, Vector2 center, float maxSize,
			float alpha, float extraScale)
		{
			if (type <= 0 || type >= TextureAssets.Item.Length)
				return;
			try {
				// Vanilla item textures are lazy-loaded: a yoyo carried in the
				// main inventory may not have had its texture requested yet.
				// Drawing TextureAssets.Item directly then yields a blank
				// medallion until the item is equipped/held. Loading it here
				// makes the wheel independent of hotbar/inventory draw order.
				Main.instance.LoadItem(type);
				Texture2D tex = TextureAssets.Item[type].Value;
				if (tex == null)
					return;
				float s = Math.Min(1f, maxSize / Math.Max(tex.Width, tex.Height)) * extraScale;
				sb.Draw(tex, center, null, Color.White * alpha, 0f, tex.Size() * 0.5f,
					s, SpriteEffects.None, 0f);
			}
			catch { }
		}

		private static void DrawPinGlyph(SpriteBatch sb, Vector2 pos, float size, Color color, bool filled)
		{
			FillRect(sb, new Rectangle((int)(pos.X - size * 0.18f), (int)(pos.Y - size * 0.55f),
				Math.Max(2, (int)(size * 0.36f)), Math.Max(2, (int)(size * 0.7f))), color);
			UiArt.DrawDisc(sb, pos + new Vector2(0, -size * 0.34f), size * 0.28f, color);
			if (filled)
				UiArt.DrawDisc(sb, pos + new Vector2(0, -size * 0.34f), size * 0.12f,
					new Color(20, 15, 10) * (color.A / 255f));
		}

		private static string KeyLabel => YoyoTechKeybind.SwapKeyLabel;

		// --------------------------------------------------------------- wheel

		private static void DrawWheel(SpriteBatch sb, StringSplicerPlayer state, Player player)
		{
			if (!state.WheelOpen && state.WheelCloseTicks <= 0)
				return;

			int w = StringSplicerPlayer.UiWidth;
			int h = StringSplicerPlayer.UiHeight;
			Vector2 center = StringSplicerPlayer.WheelCenter;
			float outer = StringSplicerPlayer.WheelOuter;
			float inner = StringSplicerPlayer.WheelInner;

			float openT = EaseOutCubic(state.WheelOpenTicks / 10f);
			float closeT = state.WheelCloseTicks / 8f;
			float anim = state.WheelOpen ? openT : closeT;
			if (anim <= 0.001f)
				return;

			float scale = 0.92f + 0.08f * EaseOutBack(state.WheelOpenTicks / 10f);
			if (!state.WheelOpen)
				scale = 0.92f + 0.08f * closeT;
			float alpha = anim;
			int count = state.WheelTypes.Count;
			int selected = count > 0 ? Math.Clamp(state.WheelSelected, 0, count - 1) : 0;
			int displayed = StringSplicer.DisplayedType(player);
			Vector2 mouse = StringSplicerPlayer.UiMouse;
			int hoverSlice = state.WheelSliceAt(mouse);
			// The hub publishes the page-dot hitboxes every frame; clear them
			// first so an empty filter result cannot leave stale click targets.
			PageDotRects.Clear();

			// The world dims behind the instrument, but the wheel itself is a
			// solid dark-navy mechanism with a warm brass rim. There are no
			// white or cyan fills anywhere in the frame.
			FillRect(sb, new Rectangle(0, 0, w, h), new Color(2, 4, 8) * (0.56f * alpha));
			sb.Draw(UiArt.Vignette, new Rectangle(0, 0, w, h), Color.Black * (0.46f * alpha));
			UiArt.DrawGlow(sb, center, (outer + 38f) * scale, Gold * (0.035f * alpha));

			float ringTop = center.Y - outer * scale;
			float capY = ringTop - 92f;
			float tabY = ringTop - 69f;
			float filterY = ringTop - 43f;

			// Layered circular case: shadow, dark bezel, inset brass band,
			// second dark shoulder, then the navy instrument face. Each ring
			// is deliberately opaque so the world never shows through.
			UiArt.DrawDisc(sb, center, (outer + 26f) * scale, Color.Black * (0.46f * alpha));
			UiArt.DrawDisc(sb, center, (outer + 22f) * scale, new Color(2, 4, 8) * (0.99f * alpha));
			UiArt.DrawRing(sb, center, (outer + 21f) * scale, GoldDim * (0.42f * alpha));
			UiArt.DrawDisc(sb, center, (outer + 18f) * scale, new Color(24, 28, 42) * (0.99f * alpha));
			UiArt.DrawRing(sb, center, (outer + 18f) * scale, Brass * (0.68f * alpha));
			UiArt.DrawRing(sb, center, (outer + 15f) * scale, new Color(106, 91, 62) * (0.68f * alpha));
			UiArt.DrawDisc(sb, center, (outer + 13f) * scale, PanelShell * (0.995f * alpha));
			UiArt.DrawRing(sb, center, (outer + 13f) * scale, PanelInner * (0.62f * alpha));

			UiArt.DrawDisc(sb, center, outer * scale, new Color(15, 19, 31) * (0.995f * alpha));
			UiArt.DrawRing(sb, center, outer * scale, Brass * (0.92f * alpha));
			UiArt.DrawRing(sb, center, (outer - 4f) * scale, new Color(70, 78, 104) * (0.58f * alpha));
			UiArt.DrawRing(sb, center, (outer - 12f) * scale, new Color(54, 63, 88) * (0.30f * alpha));
			UiArt.DrawRing(sb, center, (inner + 7f) * scale, GoldDim * (0.30f * alpha));

			// Engraved graduations around the brass band. They are shorter and
			// softer than the old spinning arcs, so the wheel reads as a calm
			// measuring instrument rather than a glowing sci-fi dial.
			for (int i = 0; i < 36; i++) {
				float a = i * MathHelper.TwoPi / 36f - MathHelper.PiOver2;
				bool major = i % 6 == 0;
				Vector2 v = a.ToRotationVector2();
				DrawLine(sb, center + v * (outer + (major ? 13.5f : 14.5f)) * scale,
					center + v * (outer + 17.5f) * scale,
					GoldDim * ((major ? 0.52f : 0.20f) * alpha), major ? 1.3f : 1f);
			}

			// Twelve recessed case screws on the brass band.
			for (int i = 0; i < 12; i++) {
				float a = i * MathHelper.TwoPi / 12f - MathHelper.PiOver2;
				Vector2 p = center + a.ToRotationVector2() * (outer + 18f) * scale;
				UiArt.DrawDisc(sb, p, 2.5f * scale, new Color(8, 10, 16) * (0.82f * alpha));
				UiArt.DrawDisc(sb, p, 1.6f * scale, Brass * (0.52f * alpha));
				UiArt.DrawDisc(sb, p - new Vector2(0.55f, 0.55f), 0.55f * scale,
					BrassBright * (0.82f * alpha));
			}

			float step = count > 0 ? MathHelper.TwoPi / count : MathHelper.TwoPi;
			float selectedAngle = -MathHelper.PiOver2 + state.SelectionGlide * step;

			// Warm amber wash on the chosen slice. It is felt more than seen;
			// the brass arc and lifted medallion carry the actual selection.
			if (count > 0) {
				float halfSector = Math.Min(step * 0.43f, 0.42f);
				for (int i = 0; i < 22; i++) {
					float a = selectedAngle - halfSector + halfSector * 2f * (i / 21f);
					Vector2 v = a.ToRotationVector2();
					DrawLine(sb, center + v * (inner + 5f) * scale,
						center + v * (outer - 7f) * scale,
						new Color(178, 148, 92) * (0.10f * alpha),
						(outer - inner) * 0.16f * scale);
				}
				DrawArc(sb, center, (outer - 7f) * scale,
					selectedAngle - halfSector, selectedAngle + halfSector,
					2f, Gold * (0.92f * alpha), 24);
				DrawArc(sb, center, (inner + 5f) * scale,
					selectedAngle - halfSector * 0.80f, selectedAngle + halfSector * 0.80f,
					1.4f, Brass * (0.80f * alpha), 18);
			}

			// Slice dividers are thin engraved seams, never bright outlines.
			for (int i = 0; i < count; i++) {
				float angle = -MathHelper.PiOver2 + i * step;
				Vector2 v = angle.ToRotationVector2();
				DrawLine(sb, center + v * (inner + 5f) * scale,
					center + v * (outer - 7f) * scale,
					PanelInner * (0.62f * alpha), 1f);
			}

			float iconRadius = inner + (outer - inner) * 0.56f;
			float slotR = MathHelper.Clamp(outer * 0.135f, 15f, 28f);
			for (int i = 0; i < count; i++) {
				float angle = -MathHelper.PiOver2 + i * step;
				// The lit focus follows SelectionGlide, not the integer index,
				// so the selected rim and medallion lift travel with the arc.
				float focus = MathHelper.Clamp(1f - Math.Abs(
					MathHelper.WrapAngle((i - state.SelectionGlide) * step)) / step, 0f, 1f);
				float stagger = MathHelper.Clamp((state.WheelOpenTicks - i * 1.1f) / 11f, 0f, 1f);
				float reveal = EaseOutCubic(stagger);
				float pop = EaseOutBack(stagger);
				float slotAlpha = alpha * (state.WheelOpen
					? MathHelper.Clamp(stagger * 1.65f, 0f, 1f) : 1f);
				float push = 4f * focus * alpha;
				float slotScale = 0.88f + 0.12f * pop;
				float radial = MathHelper.Lerp(inner + 5f, iconRadius, reveal);
				Vector2 pos = center + angle.ToRotationVector2() * (radial + push) * scale;
				int type = state.WheelTypes[i];
				bool pinned = state.Board > 0 && state.PinBoards[state.Board - 1].Contains(type);
				bool hovered = hoverSlice == i;
				float lit = Math.Max(focus, hovered ? 0.48f : 0f);

				// Recessed brass medallion: warm halo only on the chosen slot,
				// then a dark outer well, face, engraved inner ring and a thin
				// upper highlight that reads as a real bevel.
				if (lit > 0.55f) {
					UiArt.DrawGlow(sb, pos, slotR * 2.35f * slotScale,
						Gold * (0.075f * lit * slotAlpha));
				}
				UiArt.DrawDisc(sb, pos + new Vector2(0f, 2.5f * scale), (slotR + 3.5f) * slotScale,
					Color.Black * (0.58f * slotAlpha));
				UiArt.DrawDisc(sb, pos, (slotR + 2f) * slotScale,
					new Color(8, 10, 16) * (0.995f * slotAlpha));
				UiArt.DrawRing(sb, pos, (slotR + 2f) * slotScale,
					Color.Lerp(GoldDim, Gold, lit) * ((0.54f + 0.40f * lit) * slotAlpha));

				Color face = Color.Lerp(new Color(24, 30, 47), new Color(48, 58, 86), lit);
				UiArt.DrawDisc(sb, pos, slotR * slotScale, face * (0.995f * slotAlpha));
				UiArt.DrawRing(sb, pos, (slotR - 1.4f) * slotScale,
					Color.Lerp(Steel, Brass, lit) * ((0.50f + 0.36f * lit) * slotAlpha));
				UiArt.DrawRing(sb, pos, (slotR - 4.2f) * slotScale,
					new Color(64, 74, 104) * ((0.26f + 0.22f * lit) * slotAlpha));
				DrawArc(sb, pos, (slotR - 2.2f) * slotScale,
					MathHelper.Pi * 1.08f, MathHelper.Pi * 1.92f, 1f,
					new Color(255, 244, 214) * ((0.10f + 0.08f * lit) * slotAlpha), 10);

				int rarity = Sample(type)?.rare ?? 0;
				DrawArc(sb, pos, (slotR + 0.2f) * slotScale,
					MathHelper.Pi * 0.28f, MathHelper.Pi * 0.72f, 1.4f,
					RarityColor(rarity) * (0.56f * slotAlpha), 8);
				DrawItem(sb, type, pos, slotR * 1.06f * slotScale,
					(0.84f + 0.16f * lit) * slotAlpha, 1f);

				if (state.PinEditor) {
					if (pinned) {
						DrawPinGlyph(sb, pos + new Vector2(slotR * 0.72f, -slotR * 0.70f) * slotScale,
							slotR * 0.58f * slotScale, Gold * slotAlpha, true);
					}
					else {
						UiArt.DrawDisc(sb, pos, slotR * slotScale, new Color(2, 4, 8) * (0.30f * slotAlpha));
					}
				}
				else if (state.Board > 0 && pinned) {
					DrawPinGlyph(sb, pos + new Vector2(slotR * 0.72f, -slotR * 0.70f) * slotScale,
						slotR * 0.50f * slotScale, Gold * (0.88f * slotAlpha), true);
				}

				// Panic assignment: a small ember medallion opposite the pin.
				if (state.PanicType == type) {
					float badgeR = MathHelper.Clamp(slotR * 0.23f, 4.4f, 6.6f);
					Vector2 badge = pos + new Vector2(-slotR * 0.72f, -slotR * 0.70f) * slotScale;
					float numeralScale = MathHelper.Clamp(slotR / 54f, 0.28f, 0.40f);
					UiArt.DrawDisc(sb, badge, badgeR + 1.2f,
						new Color(20, 8, 8) * (0.90f * slotAlpha));
					UiArt.DrawRing(sb, badge, badgeR,
						new Color(236, 136, 88) * (0.86f * slotAlpha));
					DrawCenteredShadow(sb, "P", badge,
						new Color(255, 198, 148) * (0.94f * slotAlpha), numeralScale);
				}

				if (type == displayed) {
					UiArt.DrawDisc(sb, pos + new Vector2(0f, slotR * 0.94f * slotScale),
						1.7f * scale, Gold * (0.92f * slotAlpha));
				}

				// Lead yoyo: a small four-point star opposite the number
				// badge. It marks the board-switch favourite without taking
				// over the pin or panic corners.
				if (state.LeadType == type) {
					Vector2 star = pos + new Vector2(slotR * 0.66f, slotR * 0.58f) * slotScale;
					DrawStarGlyph(sb, star, MathHelper.Clamp(slotR * 0.34f, 5f, 8f),
						Gold * (0.94f * slotAlpha));
				}

				// Wheel-slot number. This is not a hotbar bind: it only selects
				// this page slot while the wheel is open, so it also reaches
				// yoyos carried outside the ten hotbar slots.
				if (i < 10) {
					string numeral = i == 9 ? "0" : (i + 1).ToString();
					float badgeR = MathHelper.Clamp(slotR * 0.24f, 4.6f, 7f);
					Vector2 badge = pos + new Vector2(-slotR * 0.66f, slotR * 0.58f) * slotScale;
					float numeralScale = MathHelper.Clamp(slotR / 54f, 0.30f, 0.42f);
					UiArt.DrawDisc(sb, badge, badgeR + 1.2f,
						new Color(4, 6, 11) * (0.86f * slotAlpha));
					UiArt.DrawRing(sb, badge, badgeR,
						Brass * (0.62f * slotAlpha));
					DrawCenteredShadow(sb, numeral, badge,
						TextMain * (0.88f * slotAlpha), numeralScale);
				}
			}

			if (count == 0)
				DrawEmptyWheel(sb, state, center, inner, scale, alpha);
			else
				DrawHub(sb, state, center, inner, scale, alpha, selected, displayed);

			DrawChips(sb, state, center, outer, scale, alpha);
			if (state.LockEditor)
				DrawLockRow(sb, state, player, center, outer, scale, alpha);
			else
				DrawSequenceRail(sb, state, center, outer, scale, alpha, displayed);
			DrawHints(sb, state, center, outer, scale, alpha);

			// Header last: it lives in its reserved band above the circle and
			// cannot be covered by the wheel or the toast.
			if (capY >= 4f)
				DrawWheelCaption(sb, state, center, capY, alpha);
			DrawBoardTabs(sb, state, center, tabY, alpha);
			DrawFilterPlate(sb, state, center, filterY, alpha);

			// Hover card is the last layer so it can sit above the bezel,
			// header and footer without being clipped by the circular plate.
			if (state.WheelOpen && count > 0 && hoverSlice >= 0 && hoverSlice < count
				&& StringSplicerUiSystem.PageDotAt(mouse.ToPoint()) < 0) {
				DrawHoverCard(sb, state, state.WheelTypes[hoverSlice], mouse, alpha);
			}
		}

		private static void DrawWheelCaption(SpriteBatch sb, StringSplicerPlayer state,
			Vector2 center, float y, float alpha)
		{
			string boardName = StringSplicerPlayer.BoardName(state.Board);
			string caption = state.LockEditor
				? $"HOTBAR LOCKS  |  {boardName}"
				: state.PinEditor
					? $"PIN EDITOR  |  {boardName}"
					: $"STRING SPLICER  |  {boardName}";
			const float textScale = 0.54f;
			const float tracking = 2.6f;
			float width = TrackWidth(caption, textScale, tracking);
			float left = center.X - width * 0.5f;
			DrawTracked(sb, caption, new Vector2(left, y),
				Gold * (0.92f * alpha), textScale, tracking);

			// Engraved double rule with tiny brass finials. It frames the
			// title like the maker's plate on a physical instrument.
			int ruleY = (int)y + 16;
			float ruleW = Math.Min(width + 34f, Math.Min(250f, StringSplicerPlayer.UiWidth - 20f));
			float ruleL = center.X - ruleW * 0.5f;
			float ruleR = center.X + ruleW * 0.5f;
			FillRect(sb, new Rectangle((int)ruleL, ruleY, (int)ruleW, 1),
				GoldDim * (0.46f * alpha));
			FillRect(sb, new Rectangle((int)(ruleL + 9f), ruleY + 2,
				Math.Max(0, (int)ruleW - 18), 1), GoldDim * (0.18f * alpha));
			UiArt.DrawDisc(sb, new Vector2(ruleL, ruleY + 0.5f), 1.7f,
				Brass * (0.62f * alpha));
			UiArt.DrawDisc(sb, new Vector2(ruleR, ruleY + 0.5f), 1.7f,
				Brass * (0.62f * alpha));
		}

		private static void DrawBoardTabs(SpriteBatch sb, StringSplicerPlayer state, Vector2 center,
			float y, float alpha)
		{
			var widths = new float[4];
			LayoutBoardTabs(center, y, widths, out float x);
			Point mp = (StringSplicerPlayer.UiMouse).ToPoint();
			for (int i = 0; i < 4; i++) {
				var r = new Rectangle((int)x, (int)y, (int)widths[i], BoardTabHeight);
				BoardTabRects[i] = r;
				bool active = state.Board == i;
				Rectangle hoverRect = r;
				hoverRect.Inflate(2, 6);
				bool hover = hoverRect.Contains(mp);
				Color fill = active ? new Color(43, 53, 80)
					: hover ? new Color(35, 43, 67) : new Color(22, 27, 43);
				Color edge = active ? Gold : hover ? Brass : GoldDim * 0.82f;
				DrawPlate(sb, r, fill, edge, (active ? 0.98f : hover ? 0.94f : 0.88f) * alpha);

				if (active) {
					// Lit top lip, deep bottom lip and a small downward index
					// notch that points at the board it controls.
					FillRect(sb, new Rectangle(r.X + 4, r.Y + 1, r.Width - 8, 1),
						BrassBright * (0.42f * alpha));
					FillRect(sb, new Rectangle(r.X + 4, r.Bottom - 3, r.Width - 8, 2),
						Gold * (0.88f * alpha));
					int notchX = r.Center.X;
					FillRect(sb, new Rectangle(notchX - 6, r.Bottom, 12, 1),
						Gold * (0.78f * alpha));
					FillRect(sb, new Rectangle(notchX - 3, r.Bottom + 1, 6, 1),
						Gold * (0.46f * alpha));
				}
				else if (hover) {
					FillRect(sb, new Rectangle(r.X + 4, r.Y + 1, r.Width - 8, 1),
						Gold * (0.28f * alpha));
				}

				float tw = TrackWidth(StringSplicerPlayer.BoardNames[i],
					BoardTabTextScale, BoardTabTracking);
				float textH = FontAssets.MouseText.Value.MeasureString(
					StringSplicerPlayer.BoardNames[i]).Y * BoardTabTextScale;
				DrawTracked(sb, StringSplicerPlayer.BoardNames[i],
					new Vector2(r.X + (r.Width - tw) * 0.5f, r.Y + (r.Height - textH) * 0.5f),
					(active ? TextMain : hover ? TextMain : TextMuted) * alpha,
					BoardTabTextScale, BoardTabTracking);
				if (i > 0 && state.PinBoards[i - 1].Count > 0) {
					Vector2 stud = new(r.Right - 6f, r.Y + 5f);
					UiArt.DrawDisc(sb, stud, 2.2f, new Color(8, 10, 16) * (0.85f * alpha));
					UiArt.DrawDisc(sb, stud, 1.5f, Gold * (0.86f * alpha));
					UiArt.DrawDisc(sb, stud - new Vector2(0.4f, 0.4f), 0.5f,
						BrassBright * (0.75f * alpha));
				}
				x += widths[i] + BoardTabGap;
			}
		}

		private static void DrawFilterPlate(SpriteBatch sb, StringSplicerPlayer state, Vector2 center,
			float y, float alpha)
		{
			int available = Math.Max(120, StringSplicerPlayer.UiWidth - 16);
			int width = (int)Math.Min(StringSplicerPlayer.WheelOuter * 1.70f, available);
			width = Math.Clamp(width, Math.Min(180, available), available);
			FilterRect = new Rectangle((int)(center.X - width * 0.5f), (int)y, width, 23);
			bool hasClear = state.Filter.Length > 0;
			FilterClearRect = hasClear
				? new Rectangle(FilterRect.Right - 30, FilterRect.Y + 2, 20, FilterRect.Height - 4)
				: Rectangle.Empty;
			bool clearHover = hasClear && FilterClearRect.Contains(StringSplicerPlayer.UiMousePoint);
			bool hover = FilterRect.Contains(StringSplicerPlayer.UiMousePoint);
			Color fill = state.FilterFocus ? new Color(18, 25, 42)
				: hover ? new Color(35, 43, 67) : new Color(22, 27, 43);
			Color edge = state.FilterFocus ? Gold : hover ? Brass : GoldDim * 0.82f;
			DrawPlate(sb, FilterRect, fill, edge,
				(state.FilterFocus ? 0.98f : hover ? 0.94f : 0.88f) * alpha);
			if (state.FilterFocus) {
				FillRect(sb, new Rectangle(FilterRect.X + 2, FilterRect.Y + 2, 2,
					FilterRect.Height - 4), Gold * (0.84f * alpha));
				FillRect(sb, new Rectangle(FilterRect.X + 5, FilterRect.Y + 1,
					FilterRect.Width - 10, 1), BrassBright * (0.26f * alpha));
			}

			// Recessed magnifier, then a text field inset behind it.
			Vector2 icon = new(FilterRect.X + 12f, FilterRect.Y + FilterRect.Height * 0.5f);
			Color iconColor = state.FilterFocus ? Gold : hover ? Brass : TextMuted;
			UiArt.DrawRing(sb, icon, 4.2f, iconColor * (0.82f * alpha));
			DrawLine(sb, icon + new Vector2(2.8f, 2.8f), icon + new Vector2(6.2f, 6.2f),
				iconColor * (0.82f * alpha), 1.4f);

			string text = state.Filter.Length > 0
				? state.Filter + (state.FilterFocus && (Main.GameUpdateCount / 20) % 2 == 0 ? "|" : "")
				: "SEARCH YOYOS";
			Color color = state.Filter.Length > 0 ? TextMain : TextMuted;
			if (state.FilterFocus && state.Filter.Length == 0
				&& (Main.GameUpdateCount / 20) % 2 == 0) {
				FillRect(sb, new Rectangle(FilterRect.X + 23, FilterRect.Y + 5, 1, 13),
					Gold * (0.82f * alpha));
			}
			Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * 0.52f;
			sb.DrawString(FontAssets.MouseText.Value, text,
				new Vector2(FilterRect.X + 24, FilterRect.Y + (FilterRect.Height - size.Y) * 0.5f),
				color * alpha, 0f, Vector2.Zero, 0.52f, SpriteEffects.None, 0f);

			string right = state.Filter.Length > 0 || state.FilterFocus
				? state.WheelFilteredCount.ToString() : "CTRL+F";
			Color rightColor = state.Filter.Length > 0 || state.FilterFocus
				? BlueText * (0.76f * alpha) : GoldDim * (0.50f * alpha);
			float rightScale = state.Filter.Length > 0 || state.FilterFocus ? 0.48f : 0.40f;
			Vector2 rs = FontAssets.MouseText.Value.MeasureString(right) * rightScale;
			float rightPad = hasClear ? 35f : 9f;
			if (FilterRect.Width > (int)rs.X + (int)rightPad + 40) {
				sb.DrawString(FontAssets.MouseText.Value, right,
					new Vector2(FilterRect.Right - rs.X - rightPad,
						FilterRect.Y + (FilterRect.Height - rs.Y) * 0.5f),
					rightColor, 0f, Vector2.Zero, rightScale, SpriteEffects.None, 0f);
			}

			// Mouse-only clear affordance. Delete still works as the fast
			// keyboard path; this is for players already holding the mouse.
			if (hasClear) {
				Vector2 xc = new(FilterClearRect.Center.X, FilterClearRect.Center.Y);
				Color xcColor = clearHover ? BrassBright : GoldDim;
				UiArt.DrawDisc(sb, xc, 7.2f, new Color(7, 10, 17) * (0.72f * alpha));
				UiArt.DrawRing(sb, xc, 6.1f, xcColor * ((clearHover ? 0.86f : 0.54f) * alpha));
				DrawLine(sb, xc + new Vector2(-3.1f, -3.1f), xc + new Vector2(3.1f, 3.1f),
					xcColor * (0.92f * alpha), 1.3f);
				DrawLine(sb, xc + new Vector2(3.1f, -3.1f), xc + new Vector2(-3.1f, 3.1f),
					xcColor * (0.92f * alpha), 1.3f);
			}
		}

		private static void DrawPageDots(SpriteBatch sb, StringSplicerPlayer state, Vector2 center,
			float y, float alpha)
		{
			PageDotRects.Clear();
			if (state.WheelPageCount <= 1)
				return;
			int count = state.WheelPageCount;
			int gap = 12;
			float total = (count - 1) * gap;
			for (int i = 0; i < count; i++) {
				float x = center.X - total * 0.5f + i * gap;
				var r = new Rectangle((int)x - 5, (int)y - 5, 10, 10);
				PageDotRects.Add(r);
				bool active = i == state.WheelPage;
				Vector2 p = new(x, y);
				UiArt.DrawDisc(sb, p + new Vector2(0f, 1f), active ? 4.8f : 3.6f,
					new Color(6, 8, 13) * (0.88f * alpha));
				UiArt.DrawRing(sb, p, active ? 4.0f : 2.8f,
					(active ? Gold : PanelInner) * (0.92f * alpha));
				if (active) {
					UiArt.DrawDisc(sb, p, 1.9f, Gold * (0.92f * alpha));
					UiArt.DrawDisc(sb, p - new Vector2(0.4f, 0.4f), 0.55f,
						BrassBright * (0.72f * alpha));
				}
			}
		}

		/// <summary>The raised central instrument boss shared by the populated
		/// and empty states. It has the same brass/dark-navy construction as the
		/// outer case so the hub reads as a machine part, not a flat circle.</summary>
		private static void DrawHubCase(SpriteBatch sb, Vector2 center, float panelR,
			float alpha, bool compact)
		{
			UiArt.DrawGlow(sb, center, panelR * 1.48f, Gold * (0.045f * alpha));
			UiArt.DrawDisc(sb, center, panelR + 5f, Color.Black * (0.52f * alpha));
			UiArt.DrawDisc(sb, center, panelR + 3.5f, new Color(4, 6, 10) * (0.995f * alpha));
			UiArt.DrawRing(sb, center, panelR + 3.5f, GoldDim * (0.70f * alpha));
			UiArt.DrawDisc(sb, center, panelR, new Color(10, 13, 22) * (0.995f * alpha));
			UiArt.DrawRing(sb, center, panelR - 1f, Brass * (0.72f * alpha));
			UiArt.DrawRing(sb, center, panelR - 4f, PanelInner * (0.72f * alpha));
			UiArt.DrawRing(sb, center, panelR - 8f, new Color(54, 63, 88) * (0.26f * alpha));

			// Sparse graduations and four recessed case screws around the boss.
			// At tiny sizes the type needs the room more than the decoration does.
			if (panelR >= 54f) {
				int ticks = compact ? 8 : 12;
				for (int i = 0; i < ticks; i++) {
					float a = i * MathHelper.TwoPi / ticks - MathHelper.PiOver2;
					Vector2 v = a.ToRotationVector2();
					bool major = i % (ticks / 4) == 0;
					DrawLine(sb, center + v * (panelR - (major ? 7f : 5f)),
						center + v * (panelR - 2.5f),
						GoldDim * ((major ? 0.40f : 0.18f) * alpha), major ? 1.2f : 1f);
				}
				for (int i = 0; i < 4; i++) {
					float a = MathHelper.PiOver4 + i * MathHelper.PiOver2;
					Vector2 p = center + a.ToRotationVector2() * (panelR - 5.5f);
					UiArt.DrawDisc(sb, p, 2.1f, new Color(7, 9, 14) * (0.72f * alpha));
					UiArt.DrawDisc(sb, p, 1.2f, Brass * (0.48f * alpha));
				}
			}
		}

		private static void DrawEmptyWheel(SpriteBatch sb, StringSplicerPlayer state, Vector2 center,
			float inner, float scale, float alpha)
		{
			float panelR = inner * 0.92f * scale;
			bool compact = panelR < 70f;
			DrawHubCase(sb, center, panelR, alpha, compact);
			string title = state.Filter.Length > 0
				? "NO MATCHES"
				: state.Board > 0 ? "BOARD EMPTY" : "NO YOYOS";
			if (compact && state.Filter.Length > 0)
				title = "NO MATCH";
			DrawCenteredShadow(sb, title, center + new Vector2(0, -panelR * 0.22f),
				TextMain * alpha, compact ? 0.54f : 0.68f);

			string hint;
			if (state.Filter.Length > 0)
				hint = state.FilterFocus
					? (compact ? "BACKSPACE" : "BACKSPACE TO WIDEN")
					: (compact ? "CLICK SEARCH" : "CLICK SEARCH TO EDIT");
			else if (state.Board > 0)
				hint = compact ? "CTRL+P TO PIN" : $"CTRL+P TO PIN YOYOS TO {StringSplicerPlayer.BoardName(state.Board)}";
			else
				hint = compact ? "CARRY A YOYO" : "CARRY A YOYO TO BEGIN";
			DrawFitCentered(sb, hint, center + new Vector2(0, panelR * 0.10f),
				TextMuted * (0.86f * alpha), 0.50f, 0.38f, panelR * 1.62f);
			if (state.Filter.Length == 0 && state.Board > 0)
				DrawCentered(sb, $"PINNED 0 / {StringSplicerPlayer.MaxPins}",
					center + new Vector2(0, panelR * 0.42f),
					Gold * (0.82f * alpha), 0.44f);
		}

		private static void DrawHub(SpriteBatch sb, StringSplicerPlayer state,
			Vector2 center, float inner, float scale, float alpha, int selected, int displayed)
		{
			float panelR = inner * 0.92f * scale;
			bool compact = panelR < 82f;
			DrawHubCase(sb, center, panelR, alpha, compact);

			int type = state.WheelTypes[selected];
			Item sample = Sample(type);
			Item heldSample = Sample(displayed);
			string name = ItemName(type);

			float iconY = center.Y - panelR * (compact ? 0.50f : 0.47f);
			float iconR = MathHelper.Clamp(panelR * (compact ? 0.22f : 0.24f),
				compact ? 10f : 16f, compact ? 20f : 25f);
			Vector2 iconPos = new(center.X, iconY);
			UiArt.DrawDisc(sb, iconPos + new Vector2(0f, 2f), iconR * 1.36f,
				Color.Black * (0.44f * alpha));
			UiArt.DrawDisc(sb, iconPos, iconR * 1.32f,
				new Color(6, 8, 13) * (0.995f * alpha));
			UiArt.DrawRing(sb, iconPos, iconR * 1.32f,
				GoldDim * (0.78f * alpha));
			UiArt.DrawDisc(sb, iconPos, iconR * 1.18f,
				new Color(24, 30, 47) * (0.995f * alpha));
			UiArt.DrawRing(sb, iconPos, iconR * 1.16f,
				RarityColor(sample?.rare ?? 0) * (0.62f * alpha));
			DrawArc(sb, iconPos, iconR * 1.06f, MathHelper.Pi * 1.08f,
				MathHelper.Pi * 1.92f, 1f, new Color(255, 244, 214) * (0.10f * alpha), 10);
			DrawItem(sb, type, iconPos, iconR * 1.46f, alpha, 1f);

			float nameScale = compact ? 0.60f : 0.72f;
			float maxNameW = panelR * 1.78f;
			while (nameScale > 0.44f
				&& FontAssets.MouseText.Value.MeasureString(name).X * nameScale > maxNameW)
				nameScale -= 0.03f;
			DrawCenteredShadow(sb, name,
				new Vector2(center.X, center.Y - panelR * (compact ? 0.06f : 0.10f)),
				TextMain * alpha, nameScale);

			string status;
			Color statusColor;
			if (state.PinNoticeTimer > 0) {
				float fade = MathHelper.Clamp(state.PinNoticeTimer / 20f, 0f, 1f);
				status = state.PinNotice;
				statusColor = Gold * (0.94f * fade);
			}
			else if (state.PinEditor) {
				status = "CLICK TO PIN";
				statusColor = Gold * (0.92f);
			}
			else if (type == displayed) {
				status = "EQUIPPED";
				statusColor = Gold * (0.92f);
			}
			else {
				status = "CLICK TO EQUIP";
				statusColor = BlueText * (0.90f);
			}
			DrawFitCentered(sb, status,
				new Vector2(center.X, center.Y + panelR * (compact ? 0.21f : 0.10f)),
				statusColor * alpha, compact ? 0.40f : 0.44f, 0.34f, panelR * 1.72f);

			if (!compact) {
				// Base stats and deltas against the currently displayed yoyo.
				float statY = center.Y + panelR * 0.28f;
				float rowGap = panelR * 0.18f;
				float leftX = center.X - panelR * 0.55f;
				float rightX = center.X + panelR * 0.06f;
				DrawStat(sb, "DMG", sample?.damage ?? 0, sample?.damage - heldSample?.damage ?? 0,
					new Vector2(leftX, statY), alpha, true, 0.43f);
				DrawStat(sb, "USE", sample?.useTime ?? 0, sample?.useTime - heldSample?.useTime ?? 0,
					new Vector2(rightX, statY), alpha, true, 0.43f, lowerIsBetter: true);
				DrawStat(sb, "CRIT", sample?.crit ?? 0, sample?.crit - heldSample?.crit ?? 0,
					new Vector2(leftX, statY + rowGap), alpha, true, 0.43f);
				DrawStat(sb, "KB", (int)Math.Round((sample?.knockBack ?? 0f) * 10f),
					(int)Math.Round(((sample?.knockBack ?? 0f) - (heldSample?.knockBack ?? 0f)) * 10f),
					new Vector2(rightX, statY + rowGap), alpha, true, 0.43f);
			}

			int absolute = state.WheelPage * StringSplicerPlayer.WheelPageSize + selected + 1;
			string page = $"{absolute} / {state.WheelFilteredCount}";
			if (state.PinEditor) {
				int pinned = state.Board > 0 ? state.PinBoards[state.Board - 1].Count : 0;
				page += $"  |  PINS {pinned} / {StringSplicerPlayer.MaxPins}";
			}
			DrawFitCentered(sb, page,
				new Vector2(center.X, center.Y + panelR * (compact ? 0.54f : 0.72f)),
				TextMuted * (0.82f * alpha), compact ? 0.36f : 0.42f, 0.30f, panelR * 1.72f);
			// Page dots live inside the hub, below the page count. They are
			// clear of the lower medallions and remain clickable.
			DrawPageDots(sb, state, center, center.Y + panelR * (compact ? 0.82f : 0.88f), alpha);
		}

		/// <summary>Compact hover card for the medallion under the pointer. It
		/// mirrors the hub's DMG/USE/CRIT/KB comparison but stays beside the
		/// cursor so inspecting a yoyo never yanks the eye back to the center.</summary>
		private static void DrawHoverCard(SpriteBatch sb, StringSplicerPlayer state,
			int type, Vector2 mouse, float alpha)
		{
			Item sample = Sample(type);
			string name = ItemName(type);
			int damage = sample?.damage ?? 0;
			int use = sample?.useTime ?? 0;
			int crit = sample?.crit ?? 0;
			float kb = sample?.knockBack ?? 0f;
			string stats = $"DMG {damage}   USE {use}   CRIT {crit}%   KB {kb:0.0}";
			string roleTags = YoyoSpliceRoles.TagText(type);
			string roles = "ROLE  " + (roleTags.Length > 0 ? roleTags : "NONE");

			bool pinned = state.Board > 0
				&& state.PinBoards[state.Board - 1].Contains(type);
			bool equipped = type == StringSplicer.DisplayedType(Main.LocalPlayer);
			string status = state.PinEditor
				? (pinned ? "PINNED  |  CLICK TO UNPIN" : "CLICK TO PIN")
				: equipped ? "EQUIPPED" : "CLICK TO EQUIP";
			if (state.PanicType == type)
				status = "PANIC  |  " + status;
			if (state.LeadType == type)
				status = "LEAD  |  " + status;
			Color statusColor = state.PanicType == type
				? new Color(236, 136, 88)
				: state.LeadType == type ? Gold
				: state.PinEditor ? Gold : equipped ? Gold : BlueText;

			const float nameScale = 0.58f;
			const float statsScale = 0.42f;
			const float roleScale = 0.40f;
			const float statusScale = 0.38f;
			float nameW = FontAssets.MouseText.Value.MeasureString(name).X * nameScale;
			float statsW = FontAssets.MouseText.Value.MeasureString(stats).X * statsScale;
			float roleW = FontAssets.MouseText.Value.MeasureString(roles).X * roleScale;
			float statusW = FontAssets.MouseText.Value.MeasureString(status).X * statusScale;
			int cardW = (int)MathHelper.Clamp(Math.Max(nameW, Math.Max(statsW,
				Math.Max(roleW, statusW))) + 22f,
				164f, Math.Min(284f, StringSplicerPlayer.UiWidth - 16f));
			const int cardH = 84;

			Vector2 pos = mouse + new Vector2(17f, 17f);
			if (pos.X + cardW > StringSplicerPlayer.UiWidth - 8f)
				pos.X = mouse.X - cardW - 17f;
			if (pos.Y + cardH > StringSplicerPlayer.UiHeight - 8f)
				pos.Y = mouse.Y - cardH - 17f;
			pos.X = MathHelper.Clamp(pos.X, 8f,
				Math.Max(8f, StringSplicerPlayer.UiWidth - cardW - 8f));
			pos.Y = MathHelper.Clamp(pos.Y, 8f,
				Math.Max(8f, StringSplicerPlayer.UiHeight - cardH - 8f));

			var rect = new Rectangle((int)pos.X, (int)pos.Y, cardW, cardH);
			Color rarity = RarityColor(sample?.rare ?? 0);
			DrawPlate(sb, rect, new Color(18, 22, 36), rarity, 0.97f * alpha);
			FillRect(sb, new Rectangle(rect.X + 1, rect.Y + 1, 2, rect.Height - 2),
				rarity * (0.88f * alpha));
			sb.DrawString(FontAssets.MouseText.Value, name,
				new Vector2(rect.X + 9, rect.Y + 5), rarity * alpha, 0f, Vector2.Zero,
				nameScale, SpriteEffects.None, 0f);
			sb.DrawString(FontAssets.MouseText.Value, stats,
				new Vector2(rect.X + 9, rect.Y + 27), TextMuted * (0.94f * alpha),
				0f, Vector2.Zero, statsScale, SpriteEffects.None, 0f);
			sb.DrawString(FontAssets.MouseText.Value, roles,
				new Vector2(rect.X + 9, rect.Y + 46),
				(roleTags.Length > 0 ? BlueText : TextMuted) * (0.88f * alpha),
				0f, Vector2.Zero, roleScale, SpriteEffects.None, 0f);
			sb.DrawString(FontAssets.MouseText.Value, status,
				new Vector2(rect.X + 9, rect.Y + 64), statusColor * (0.92f * alpha),
				0f, Vector2.Zero, statusScale, SpriteEffects.None, 0f);
		}

		private static void DrawStat(SpriteBatch sb, string label, int value, int delta,
			Vector2 pos, float alpha, bool alignLeft, float textScale, bool lowerIsBetter = false)
		{
			string valueText = value.ToString();
			string deltaText = delta == 0 ? "" : delta > 0 ? $"+{delta}" : delta.ToString();
			float labelW = FontAssets.MouseText.Value.MeasureString(label).X * textScale;
			float valueW = FontAssets.MouseText.Value.MeasureString(valueText).X * textScale;
			float deltaW = deltaText.Length > 0
				? FontAssets.MouseText.Value.MeasureString(deltaText).X * textScale : 0f;
			float totalW = labelW + 4f + valueW + (deltaW > 0f ? 4f + deltaW : 0f);
			float x = alignLeft ? pos.X : pos.X - totalW;
			sb.DrawString(FontAssets.MouseText.Value, label, new Vector2(x, pos.Y),
				TextMuted * (0.86f * alpha), 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
			x += labelW + 4f;
			sb.DrawString(FontAssets.MouseText.Value, valueText, new Vector2(x, pos.Y),
				TextMain * (0.95f * alpha), 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
			if (deltaText.Length > 0) {
				x += valueW + 4f;
				bool better = lowerIsBetter ? delta < 0 : delta > 0;
				Color color = better ? new Color(128, 212, 142) : new Color(224, 132, 132);
				sb.DrawString(FontAssets.MouseText.Value, deltaText, new Vector2(x, pos.Y),
					color * (0.92f * alpha), 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
			}
		}

		private static void DrawChips(SpriteBatch sb, StringSplicerPlayer state, Vector2 center,
			float outer, float scale, float alpha)
		{
			int chipW = (int)MathHelper.Clamp(outer * 0.56f, 74f, 132f);
			// Three chips share one row. The row stays inside very narrow
			// windows because the hitboxes are rebuilt from these same values
			// every draw.
			int chipRoom = Math.Max(150, StringSplicerPlayer.UiWidth - 24);
			chipW = Math.Min(chipW, Math.Max(56, (chipRoom - 16) / 3));
			const int chipH = 23;
			const int gap = 8;
			int total = chipW * 3 + gap * 2;
			int x = (int)(center.X - total * 0.5f);
			int y = (int)(center.Y + outer * scale + 22f);
			SortChipRect = new Rectangle(x, y, chipW, chipH);
			PinChipRect = new Rectangle(x + chipW + gap, y, chipW, chipH);
			LockChipRect = new Rectangle(x + (chipW + gap) * 2, y, chipW, chipH);
			bool sortHover = SortChipRect.Contains(StringSplicerPlayer.UiMousePoint);
			bool pinHover = PinChipRect.Contains(StringSplicerPlayer.UiMousePoint);
			bool lockHover = LockChipRect.Contains(StringSplicerPlayer.UiMousePoint);
			Color sortFill = sortHover ? new Color(35, 43, 67) : new Color(22, 27, 43);
			Color pinFill = state.PinEditor ? new Color(43, 53, 80)
				: pinHover ? new Color(35, 43, 67) : new Color(22, 27, 43);
			Color lockFill = state.LockEditor ? new Color(43, 53, 80)
				: lockHover ? new Color(35, 43, 67) : new Color(22, 27, 43);
			DrawPlate(sb, SortChipRect, sortFill,
				sortHover ? Brass : GoldDim * 0.82f,
				(sortHover ? 0.96f : 0.90f) * alpha);
			DrawPlate(sb, PinChipRect, pinFill,
				state.PinEditor ? Gold : pinHover ? Brass : GoldDim * 0.82f,
				(state.PinEditor ? 0.98f : pinHover ? 0.96f : 0.90f) * alpha);
			DrawPlate(sb, LockChipRect, lockFill,
				state.LockEditor ? Gold : lockHover ? Brass : GoldDim * 0.82f,
				(state.LockEditor ? 0.98f : lockHover ? 0.96f : 0.90f) * alpha);

			// Sort glyph: three descending rules with a small direction mark.
			Color sortGlyph = sortHover ? BrassBright : GoldDim;
			int gx = SortChipRect.X + 8;
			int gy = SortChipRect.Y + 7;
			DrawLine(sb, new Vector2(gx, gy), new Vector2(gx + 11, gy),
				sortGlyph * (0.82f * alpha), 1.2f);
			DrawLine(sb, new Vector2(gx, gy + 4f), new Vector2(gx + 7, gy + 4f),
				sortGlyph * (0.66f * alpha), 1.2f);
			DrawLine(sb, new Vector2(gx, gy + 8f), new Vector2(gx + 3, gy + 8f),
				sortGlyph * (0.48f * alpha), 1.2f);

			// Pin glyph for the board editor.
			DrawPinGlyph(sb, new Vector2(PinChipRect.X + 12f, PinChipRect.Center.Y + 4f),
				8f, (state.PinEditor ? Gold : pinHover ? Brass : GoldDim) * alpha, true);
			// Padlock glyph for the hotbar-lock editor.
			DrawLockGlyph(sb, new Vector2(LockChipRect.X + 11f, LockChipRect.Center.Y + 1f),
				8f, (state.LockEditor ? Gold : lockHover ? Brass : GoldDim) * alpha);

			string sortLabel = state.SortMode >= 0 && state.SortMode < StringSplicerPlayer.SortShortNames.Length
				? StringSplicerPlayer.SortShortNames[state.SortMode] : "INV";
			string sortText = state.Board > 0 && !state.PinEditor
				? "PIN ORDER" : chipW >= 100 ? "SORT  " + sortLabel : "SORT";
			string pinText = state.PinEditor ? "DONE" : chipW >= 100 ? "EDIT PINS" : "PINS";
			string lockText = state.LockEditor ? "DONE" : "LOCKS";
			DrawFitCentered(sb, sortText,
				new Vector2(SortChipRect.Center.X + 7f, SortChipRect.Center.Y - 1f),
				(sortHover ? TextMain : TextMuted) * (0.94f * alpha),
				0.45f, 0.36f, chipW - 28f);
			DrawFitCentered(sb, pinText,
				new Vector2(PinChipRect.Center.X + 7f, PinChipRect.Center.Y - 1f),
				(state.PinEditor ? Gold : pinHover ? TextMain : TextMuted) * (0.94f * alpha),
				0.45f, 0.36f, chipW - 28f);
			DrawFitCentered(sb, lockText,
				new Vector2(LockChipRect.Center.X + 7f, LockChipRect.Center.Y - 1f),
				(state.LockEditor ? Gold : lockHover ? TextMain : TextMuted) * (0.94f * alpha),
				0.45f, 0.36f, chipW - 28f);
			if (state.PinEditor) {
				FillRect(sb, new Rectangle(PinChipRect.X + 4, PinChipRect.Bottom - 3,
					PinChipRect.Width - 8, 2), Gold * (0.84f * alpha));
			}
			if (state.LockEditor) {
				FillRect(sb, new Rectangle(LockChipRect.X + 4, LockChipRect.Bottom - 3,
					LockChipRect.Width - 8, 2), Gold * (0.84f * alpha));
			}
		}

		/// <summary>Compact four-slot preview of the active board's splice
		/// chain. It is deliberately non-interactive: editing happens on the
		/// medallions with Alt+click, and the optional key only advances the
		/// chain.</summary>
		private static void DrawSequenceRail(SpriteBatch sb, StringSplicerPlayer state,
			Vector2 center, float outer, float scale, float alpha, int displayed)
		{
			var sequence = state.Sequences[Math.Clamp(state.Board, 0, StringSplicerPlayer.PinBoardCount)];
			const int slot = 18;
			const int gap = 4;
			const int labelW = 34;
			int total = labelW + 8 + slot * 4 + gap * 3;
			float x = center.X - total * 0.5f;
			float y = center.Y + outer * scale + 50f;

			DrawTracked(sb, "SPLICE", new Vector2(x, y + 4f),
				GoldDim * (0.86f * alpha), 0.40f, 1.1f);
			float sx = x + labelW + 8f;
			DrawLine(sb, new Vector2(sx, y + slot * 0.5f),
				new Vector2(sx + slot * 4 + gap * 3, y + slot * 0.5f),
				GoldDim * (0.20f * alpha), 1f);
			for (int i = 0; i < 4; i++) {
				var r = new Rectangle((int)sx, (int)y, slot, slot);
				bool filled = i < sequence.Count;
				int type = filled ? sequence[i] : 0;
				bool current = filled && type == displayed;
				Color fill = filled ? new Color(26, 32, 50) : new Color(15, 19, 31);
				Color edge = current ? Gold : filled ? GoldDim * 0.82f : GoldDim * 0.34f;
				DrawPlate(sb, r, fill, edge, (filled ? 0.94f : 0.74f) * alpha);
				if (filled)
					DrawItem(sb, type, new Vector2(r.Center.X, r.Center.Y), slot * 0.72f,
						0.92f * alpha, 1f);
				else
					UiArt.DrawDisc(sb, new Vector2(r.Center.X, r.Center.Y), 1.5f,
						GoldDim * (0.38f * alpha));
				if (current) {
					FillRect(sb, new Rectangle(r.X + 2, r.Bottom - 2, r.Width - 4, 1),
						Gold * (0.86f * alpha));
				}
				sx += slot + gap;
			}
		}

		/// <summary>The hotbar-lock editor row. Locked slots are visibly
		/// brass-rimmed and keep their item icon; clicking toggles protection
		/// without moving anything.</summary>
		private static void DrawLockRow(SpriteBatch sb, StringSplicerPlayer state, Player player,
			Vector2 center, float outer, float scale, float alpha)
		{
			RefreshLockSlotHitboxes(state);
			Rectangle first = LockSlotRects[0];
			DrawTracked(sb, "LOCKS", new Vector2(first.X - 8f - 34f, first.Y + first.Height * 0.5f - 4f),
				Gold * (0.90f * alpha), 0.38f, 1.0f);
			for (int i = 0; i < LockSlotRects.Length; i++) {
				Rectangle r = LockSlotRects[i];
				bool locked = state.LockedHotbar[i];
				bool hover = r.Contains(StringSplicerPlayer.UiMousePoint);
				Item item = player?.inventory != null && i < player.inventory.Length
					? player.inventory[i] : null;
				Color fill = locked ? new Color(44, 37, 26)
					: hover ? new Color(35, 43, 67) : new Color(20, 25, 40);
				Color edge = locked ? Gold : hover ? Brass : GoldDim * 0.52f;
				DrawPlate(sb, r, fill, edge, (locked ? 0.98f : 0.90f) * alpha);
				if (item != null && !item.IsAir)
					DrawItem(sb, item.type, new Vector2(r.Center.X, r.Center.Y),
						r.Width * 0.64f, (locked ? 0.82f : 0.92f) * alpha, 1f);
				else
					UiArt.DrawDisc(sb, new Vector2(r.Center.X, r.Center.Y),
						Math.Max(1.2f, r.Width * 0.10f), GoldDim * (0.30f * alpha));
				if (locked) {
					DrawLockGlyph(sb, new Vector2(r.Right - 4.5f, r.Y + 4.5f), 6f,
						Gold * (0.92f * alpha));
				}
				if (r.Width >= 18) {
					string numeral = i == 9 ? "0" : (i + 1).ToString();
					DrawCenteredShadow(sb, numeral,
						new Vector2(r.X + 4f, r.Bottom - 4f),
						(locked ? Gold : TextMuted) * (0.72f * alpha), 0.30f);
				}
			}
		}

		private static void DrawStarGlyph(SpriteBatch sb, Vector2 pos, float size, Color color)
		{
			UiArt.DrawGlow(sb, pos, size * 2.1f, color * 0.10f);
			DrawLine(sb, pos + new Vector2(0f, -size), pos + new Vector2(0f, size),
				color, 1.15f);
			DrawLine(sb, pos + new Vector2(-size, 0f), pos + new Vector2(size, 0f),
				color, 1.15f);
			DrawLine(sb, pos + new Vector2(-size * 0.55f, -size * 0.55f),
				pos + new Vector2(size * 0.55f, size * 0.55f), color * 0.72f, 0.9f);
			DrawLine(sb, pos + new Vector2(size * 0.55f, -size * 0.55f),
				pos + new Vector2(-size * 0.55f, size * 0.55f), color * 0.72f, 0.9f);
			UiArt.DrawDisc(sb, pos, size * 0.30f, color);
		}

		private static void DrawLockGlyph(SpriteBatch sb, Vector2 pos, float size, Color color)
		{
			float bodyW = Math.Max(4f, size * 0.66f);
			float bodyH = Math.Max(3f, size * 0.46f);
			var body = new Rectangle((int)(pos.X - bodyW * 0.5f),
				(int)(pos.Y - bodyH * 0.10f), (int)bodyW, (int)bodyH);
			FillRect(sb, body, color);
			DrawArc(sb, new Vector2(pos.X, body.Y), size * 0.27f,
				MathHelper.Pi, MathHelper.TwoPi, 1.1f, color, 8);
		}

		private static void DrawHints(SpriteBatch sb, StringSplicerPlayer state, Vector2 center,
			float outer, float scale, float alpha)
		{
			string hints = state.LockEditor
				? "CLICK A HOTBAR SLOT TO LOCK / UNLOCK   |   CTRL+L DONE"
				: state.PinEditor
					? "CLICK TO PIN / UNPIN   |   ALT+CLICK SPLICE   |   CTRL+CLICK PANIC   |   CTRL+P DONE"
					: "CLICK EQUIP   |   SHIFT+CLICK PIN   |   ALT+CLICK SPLICE   |   CTRL+CLICK PANIC   |   CTRL+SHIFT+CLICK LEAD   |   TAB BOARD   |   CTRL+S SORT   |   CTRL+F SEARCH";
			string confirm = state.PinEditor || state.LockEditor ? "CLOSE" : "EQUIP";
			string close = state.LockEditor
				? $"LOCKS PROTECT HOTBAR SLOTS FROM SWAPS AND CYCLES   |   [{KeyLabel}] CLOSE   |   RMB CANCEL"
				: state.FilterFocus
					? $"TYPE TO FILTER   |   DEL / X CLEAR   |   ESC BACK   |   [{KeyLabel}] {confirm}   |   RMB CANCEL"
					: $"CLICK SEARCH OR CTRL+F   |   SCROLL / 1-9 SELECT   |   [{KeyLabel}] {confirm}   |   RMB CANCEL";
			float y = center.Y + outer * scale + (state.LockEditor ? 80f : 76f);
			float maxW = StringSplicerPlayer.UiWidth - 28f;
			float ruleW = Math.Min(232f, maxW);
			FillRect(sb, new Rectangle((int)(center.X - ruleW * 0.5f), (int)y - 7,
				(int)ruleW, 1), GoldDim * (0.24f * alpha));
			UiArt.DrawDisc(sb, new Vector2(center.X - ruleW * 0.5f, y - 6.5f), 1.3f,
				GoldDim * (0.42f * alpha));
			UiArt.DrawDisc(sb, new Vector2(center.X + ruleW * 0.5f, y - 6.5f), 1.3f,
				GoldDim * (0.42f * alpha));
			DrawFitCentered(sb, hints, new Vector2(center.X, y),
				TextMuted * (0.90f * alpha), 0.48f, 0.34f, maxW);
			DrawFitCentered(sb, close, new Vector2(center.X, y + 14f),
				new Color(132, 146, 168) * (0.72f * alpha), 0.44f, 0.32f, maxW);
		}

		// --------------------------------------------------------------- toast

		private static void DrawToast(SpriteBatch sb, StringSplicerPlayer state)
		{
			// The wheel and its toast never share the screen. Pin feedback is
			// shown inside the hub instead; this leaves the footer and the
			// header permanently collision-free.
			if (state.WheelOpen || state.WheelCloseTicks > 0)
				return;
			if (Main.playerInventory || Main.mapFullscreen || Main.gameMenu
				|| Content.Guidebook.YoyoGuidebookUISystem.Open)
				return;
			if (state.ToastTimer <= 0 || state.ToastItemType <= 0)
				return;

			float fade = MathHelper.Clamp(state.ToastTimer / 18f, 0f, 1f);
			float appear = MathHelper.Clamp((150 - state.ToastTimer) / 12f, 0f, 1f);
			float alpha = Math.Min(fade, appear);
			string caption = state.ToastCaption;
			string name = state.ToastName;
			string line = $"{state.ToastIndex} / {state.ToastCount}";
			int width = Math.Min(356, StringSplicerPlayer.UiWidth - 16);
			const int height = 62;
			int viewW = StringSplicerPlayer.UiWidth;
			int viewH = StringSplicerPlayer.UiHeight;
			float nameScale = 0.74f;
			float maxNameW = width - 82f;
			while (nameScale > 0.50f
				&& FontAssets.MouseText.Value.MeasureString(name).X * nameScale > maxNameW)
				nameScale -= 0.03f;
			float slide = EaseOutCubic(appear);
			int x = Math.Clamp((viewW - width) / 2, 4, Math.Max(4, viewW - width - 4));
			int y = Math.Clamp(viewH - 174 + (int)((1f - slide) * 16f), 4,
				Math.Max(4, viewH - height - 4));

			FillRect(sb, new Rectangle(x + 5, y + 6, width, height), new Color(0, 0, 0, 120) * alpha);
			DrawPlate(sb, new Rectangle(x, y, width, height),
				PanelShell, PanelEdge, 0.98f * alpha);
			FillRect(sb, new Rectangle(x, y, 3, height), Gold * (0.86f * alpha));
			DrawPlate(sb, new Rectangle(x + 13, y + 12, 38, 38),
				ButtonFill, RarityColor(Sample(state.ToastItemType)?.rare ?? 0), 0.90f * alpha);
			DrawItem(sb, state.ToastItemType, new Vector2(x + 32, y + 31), 29f, alpha, 1f);
			DrawTracked(sb, caption.ToUpperInvariant(), new Vector2(x + 62, y + 8),
				Gold * (0.94f * alpha), 0.46f, 1.8f);
			DrawCenteredShadow(sb, name, new Vector2(x + 62 + (width - 74) * 0.5f, y + 30),
				TextMain * alpha, nameScale);
			DrawCentered(sb, line, new Vector2(x + 62 + (width - 74) * 0.5f, y + 47),
				TextMuted * (0.80f * alpha), 0.46f);
			float bar = MathHelper.Clamp(state.ToastTimer / 150f, 0f, 1f);
			FillRect(sb, new Rectangle(x + 1, y + height - 2, (int)((width - 2) * bar), 1),
				GoldDim * (0.72f * alpha));
		}
	}
}
