using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Storage
{
	// Yoyo Chest world data: 5 categories x 5 slots normally, x10 after
	// the one-time Hardmode expansion (25 -> 50 total). Storage is always
	// a 50-slot array; unexpanded drawers only use the first 5 per row.
	public class ChestData
	{
		public Item[] Items = new Item[YoyoChestSystem.MaxTotalSlots];
		public bool Upgraded;

		public ChestData()
		{
			for (int i = 0; i < Items.Length; i++)
				Items[i] = new Item();
		}

		public int SlotsPerCategory => Upgraded ? YoyoChestSystem.UpSlotsPerCategory : YoyoChestSystem.BaseSlotsPerCategory;
		public int TotalSlots => SlotsPerCategory * YoyoChestSystem.CategoryCount;
	}

	public class YoyoChestSystem : ModSystem
	{
		public const int BaseSlotsPerCategory = 5;
		public const int UpSlotsPerCategory = 10;
		public const int CategoryCount = 5;
		public const int BaseTotalSlots = BaseSlotsPerCategory * CategoryCount;
		public const int MaxTotalSlots = UpSlotsPerCategory * CategoryCount;

	// One-time Hardmode expansion price.
		public const int ExpandBars = 12;
		public static readonly int ExpandCoins = Item.buyPrice(gold: 15);

		public static readonly Dictionary<Point, ChestData> Chests = new();

		public static string CategoryName(int cat) => cat switch {
			0 => "Yoyos",
			1 => "Yoyo Accessories",
			2 => "Yoyo Potions",
			3 => "Yoyo Materials",
			_ => "Other Yoyo Items",
		};

		public static ChestData GetOrCreate(Point key)
		{
			if (!Chests.TryGetValue(key, out var data)) {
				data = new ChestData();
				Chests[key] = data;
			}
			return data;
		}

		public static int SlotsPerCategory(Point key) => GetOrCreate(key).SlotsPerCategory;
		public static int TotalSlots(Point key) => GetOrCreate(key).TotalSlots;
		public static bool IsUpgraded(Point key) => GetOrCreate(key).Upgraded;

		public static void ForgetTile(Point key)
		{
			if (Chests.Remove(key) && Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public static void SyncWorld()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		// Push local chest state outward after any mutation: clients send
		// to the server (validated + rebroadcast), the server/host just
		// broadcasts, single-player needs nothing.
		public static void PushTile(Point key)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				ChestNet.SendChest(key);
			else
				SyncWorld();
		}

		public static string ExpandCostText()
		{
			string barName;
			try {
				barName = Lang.GetItemNameValue(ItemID.HallowedBar).ToLower();
			}
			catch {
				barName = "hallowed bars";
			}
			return $"{ExpandBars}x {barName} + {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(ExpandCoins)}";
		}

		// One-time expansion: 5 -> 10 slots per drawer. Hardmode + bars +
		// coins, no mesh or uses gates. Existing items stay put (first 5 of
		// each drawer keep their slots).
		public static bool TryUpgrade(Player player, Point key)
		{
			ChestData data = GetOrCreate(key);
			if (data.Upgraded)
				return false;
			if (!Main.hardMode)
				return false;
			if (!player.CanAfford(ExpandCoins))
				return false;
			if (Content.NPCs.Town.YoyoTownShared.CountInInventory(player, ItemID.HallowedBar) < ExpandBars)
				return false;
			player.PayCurrency(ExpandCoins);
			Content.NPCs.Town.YoyoTownShared.TryConsumeFromInventory(player, ItemID.HallowedBar, ExpandBars);
			data.Upgraded = true;
			PushTile(key);
			return true;
		}

		// Sort + stack-merge each drawer by item type. Only touches the
		// drawer's live slots; category rules already hold by construction.
		public static void SortChest(Point key)
		{
			ChestData data = GetOrCreate(key);
			bool changed = false;
			for (int cat = 0; cat < CategoryCount; cat++) {
				int per = data.SlotsPerCategory;
				var items = new System.Collections.Generic.List<Item>();
				for (int s = 0; s < per; s++) {
					Item it = data.Items[cat * UpSlotsPerCategory + s];
					if (it != null && !it.IsAir)
						items.Add(it);
				}
				items.Sort((a, b) => {
					int c = a.type.CompareTo(b.type);
					return c != 0 ? c : b.stack.CompareTo(a.stack);
				});
				// Merge stacks of the same type (prefixes stay on survivors).
				for (int i = 0; i < items.Count; i++) {
					for (int j = i + 1; j < items.Count; j++) {
						if (items[i].type != items[j].type || items[i].prefix != items[j].prefix)
							continue;
						int space = items[i].maxStack - items[i].stack;
						if (space <= 0)
							break;
						int move = System.Math.Min(space, items[j].stack);
						items[i].stack += move;
						items[j].stack -= move;
						if (items[j].stack <= 0) {
							items[j].TurnToAir();
							break;
						}
					}
				}
				int w = 0;
				for (int s = 0; s < per; s++) {
					Item cur = data.Items[cat * UpSlotsPerCategory + s];
					Item next = null;
					while (w < items.Count) {
						if (!items[w].IsAir) {
							next = items[w++];
							break;
						}
						w++;
					}
					Item want = next ?? new Item();
					if (cur.type != want.type || cur.stack != want.stack || cur.prefix != want.prefix)
						changed = true;
					data.Items[cat * UpSlotsPerCategory + s] = want;
				}
			}
			if (changed)
				PushTile(key);
		}

		// Quick Stack: pulls matching gear from the player's main inventory
		// (hotbar + main, never favorited) into the right drawers — merges
		// into partial stacks first, then empty slots. Returns items moved.
		public static int TryQuickStack(Player player, Point key)
		{
			ChestData data = GetOrCreate(key);
			int per = data.SlotsPerCategory;
			int moved = 0;
			Item[] inv = player.inventory;
			int scan = System.Math.Min(50, inv.Length);
			for (int cat = 0; cat < CategoryCount; cat++) {
				for (int invIdx = 0; invIdx < scan; invIdx++) {
					Item invIt = inv[invIdx];
					if (invIt == null || invIt.IsAir || invIt.favorited)
						continue;
					if (!MatchesCategory(invIt, cat))
						continue;
					for (int s = 0; s < per && !invIt.IsAir; s++) {
						int idx = cat * UpSlotsPerCategory + s;
						Item slot = data.Items[idx];
						if (slot.IsAir || slot.type != invIt.type || slot.prefix != invIt.prefix)
							continue;
						int space = slot.maxStack - slot.stack;
						if (space <= 0)
							continue;
						int move = System.Math.Min(space, invIt.stack);
						slot.stack += move;
						invIt.stack -= move;
						moved += move;
						if (invIt.stack <= 0)
							invIt.TurnToAir();
					}
					if (!invIt.IsAir) {
						for (int s = 0; s < per; s++) {
							int idx = cat * UpSlotsPerCategory + s;
							if (!data.Items[idx].IsAir)
								continue;
							moved += invIt.stack;
							data.Items[idx] = invIt.Clone();
							invIt.TurnToAir();
							break;
						}
					}
				}
			}
			if (moved > 0)
				PushTile(key);
			return moved;
		}

		public static bool IsTileStillChest(Point key)
		{
			if (!WorldGen.InWorld(key.X, key.Y))
				return false;
			Tile t = Framing.GetTileSafely(key.X, key.Y);
			return t.HasTile && t.TileType == ModContent.TileType<Tiles.Storage.YoyoChestTile>();
		}

		public static bool IsYoyo(Item item)
		{
			if (item == null || item.IsAir || item.damage <= 0)
				return false;
			if (item.type >= ItemID.None && item.type < ItemID.Sets.Yoyo.Length && ItemID.Sets.Yoyo[item.type])
				return true;
			// Modded yoyos: melee weapon from this mod with Yoyo in the name.
			if (item.ModItem != null && item.ModItem.Mod == ModContent.GetInstance<BiltonsYoyoMod>())
				return item.Name.Contains("Yoyo", System.StringComparison.OrdinalIgnoreCase) && !item.accessory;
			return false;
		}

		public static bool IsYoyoAccessory(Item item)
		{
			if (item == null || item.IsAir || !item.accessory)
				return false;
			if (item.Name.Contains("Yoyo", System.StringComparison.OrdinalIgnoreCase))
				return true;
			if (item.Name.Contains("String", System.StringComparison.OrdinalIgnoreCase)
				|| item.Name.Contains("Counterweight", System.StringComparison.OrdinalIgnoreCase)
				|| item.Name.Contains("Bearing", System.StringComparison.OrdinalIgnoreCase)
				|| item.Name.Contains("Axle", System.StringComparison.OrdinalIgnoreCase)
				|| item.Name.Contains("Emblem", System.StringComparison.OrdinalIgnoreCase))
				return item.ModItem != null && item.ModItem.Mod == ModContent.GetInstance<BiltonsYoyoMod>();
			return false;
		}

		private static bool IsYoyoTonicBuff(int buffType)
		{
			return buffType == ModContent.BuffType<Content.Buffs.BearingGreaseBuff>()
				|| buffType == ModContent.BuffType<Content.Buffs.CultistFervorBuff>()
				|| buffType == ModContent.BuffType<Content.Buffs.StringWaxBuff>()
				|| buffType == ModContent.BuffType<Content.Buffs.MoonlitFocusBuff>();
		}

		public static bool IsYoyoPotion(Item item)
		{
			if (item == null || item.IsAir || item.accessory || item.buffType <= 0)
				return false;
			if (IsYoyoTonicBuff(item.buffType))
				return true;
			return item.ModItem is global::BiltonsYoyoMod.Items.Consumables.HoneySpool;
		}

		public static bool IsYoyoMaterial(Item item)
		{
			if (item == null || item.IsAir)
				return false;
			var mod = ModContent.GetInstance<BiltonsYoyoMod>();
			if (item.ModItem == null || item.ModItem.Mod != mod)
				return false;
			string ns = item.ModItem.GetType().Namespace ?? "";
			if (ns.Contains("Materials") || ns.Contains("TierEmblems"))
				return true;
			string n = item.ModItem.GetType().Name;
			return n is "IllegalYoyoParts" or "TangledStrings" or "BarbedStrings"
				or "TierEmblemMold" or "AdvancedTierEmblemMold";
		}

		public static bool IsOtherYoyoItem(Item item)
		{
			if (item == null || item.IsAir)
				return true; // empty always fits (lets players clear slots)
			var mod = ModContent.GetInstance<BiltonsYoyoMod>();
			return item.ModItem != null && item.ModItem.Mod == mod
				&& !IsYoyo(item) && !IsYoyoAccessory(item) && !IsYoyoPotion(item) && !IsYoyoMaterial(item);
		}

		public static bool MatchesCategory(Item item, int category)
		{
			if (item == null || item.IsAir)
				return true;
			return category switch {
				0 => IsYoyo(item),
				1 => IsYoyoAccessory(item),
				2 => IsYoyoPotion(item),
				3 => IsYoyoMaterial(item),
				_ => IsOtherYoyoItem(item),
			};
		}

		public static string RejectReason(int category) => category switch {
			0 => "Only yoyo weapons fit here.",
			1 => "Only yoyo accessories fit here.",
			2 => "Only yoyo tonics fit here.",
			3 => "Only yoyo materials fit here.",
			_ => "Only other Bilton yoyo items fit here.",
		};

		public override void OnWorldLoad() => Chests.Clear();
		public override void OnWorldUnload() => Chests.Clear();

		public override void SaveWorldData(TagCompound tag)
		{
			var xs = new List<int>();
			var ys = new List<int>();
			var ups = new List<int>();
			var items = new List<TagCompound>();
			foreach (var kv in Chests) {
				if (!IsTileStillChest(kv.Key))
					continue;
				xs.Add(kv.Key.X);
				ys.Add(kv.Key.Y);
				ups.Add(kv.Value.Upgraded ? 1 : 0);
				for (int s = 0; s < MaxTotalSlots; s++)
					items.Add(ItemIO.Save(kv.Value.Items[s] ?? new Item()));
			}
			tag["ychX"] = xs;
			tag["ychY"] = ys;
			tag["ychU"] = ups;
			tag["ychI"] = items;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Chests.Clear();
			if (!tag.ContainsKey("ychX"))
				return;
			var xs = tag.Get<List<int>>("ychX");
			var ys = tag.Get<List<int>>("ychY");
			var items = tag.Get<List<TagCompound>>("ychI");
			var ups = tag.ContainsKey("ychU") ? tag.Get<List<int>>("ychU") : null;
			int n = System.Math.Min(xs.Count, ys.Count);
			int idx = 0;
			for (int i = 0; i < n; i++) {
				var data = new ChestData();
				// Old saves (pre-expansion): 25 flat slots in 5-per-drawer
				// order, no flag list. Remap into the 50-slot layout.
				bool oldFormat = ups == null && items.Count == n * BaseTotalSlots;
				if (oldFormat) {
					for (int cat = 0; cat < CategoryCount; cat++) {
						for (int s = 0; s < BaseSlotsPerCategory; s++) {
							if (idx < items.Count)
								data.Items[cat * UpSlotsPerCategory + s] = ItemIO.Load(items[idx++]);
						}
					}
				}
				else {
					data.Upgraded = ups != null && i < ups.Count && ups[i] != 0;
					for (int s = 0; s < MaxTotalSlots; s++) {
						if (idx < items.Count)
							data.Items[s] = ItemIO.Load(items[idx++]);
					}
				}
				Chests[new Point(xs[i], ys[i])] = data;
			}
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(Chests.Count);
			foreach (var kv in Chests) {
				writer.Write(kv.Key.X);
				writer.Write(kv.Key.Y);
				writer.Write(kv.Value.Upgraded);
				for (int s = 0; s < MaxTotalSlots; s++)
					ItemIO.Send(kv.Value.Items[s] ?? new Item(), writer);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			Chests.Clear();
			int n = reader.ReadInt32();
			for (int i = 0; i < n; i++) {
				var key = new Point(reader.ReadInt32(), reader.ReadInt32());
				var data = new ChestData();
				try {
					data.Upgraded = reader.ReadBoolean();
				}
				catch {
					data.Upgraded = false;
				}
				for (int s = 0; s < MaxTotalSlots; s++) {
					try {
						data.Items[s] = ItemIO.Receive(reader);
					}
					catch {
						data.Items[s] = new Item();
					}
				}
				Chests[key] = data;
			}
		}
	}
}
