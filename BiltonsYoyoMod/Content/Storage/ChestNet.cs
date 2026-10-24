using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Storage
{
	// Yoyo Chest multiplayer packets (channel 251). The owning client
	// predicts instantly (swap/sort/expand/stack apply locally), then
	// pushes full chest state; the server validates (chest still there,
	// per-drawer category rules, hidden slots empty unless expanded,
	// expansion flag only in Hardmode) and rebroadcasts via WorldData.
	// Payments (expansion bars/coins) happen client-side like trap tracks;
	// the server validates gates, not funds.
	public static class ChestNet
	{
		public enum Msg : byte
		{
			Push = 0,
		}

		private static Mod Mod => ModContent.GetInstance<BiltonsYoyoMod>();

		public static void SendChest(Point tile)
		{
			if (!YoyoChestSystem.Chests.TryGetValue(tile, out var data))
				return;
			ModPacket p = Mod.GetPacket();
			p.Write((byte)251); // chest channel discriminator
			p.Write((byte)Msg.Push);
			p.Write(tile.X); p.Write(tile.Y);
			p.Write(data.Upgraded);
			for (int s = 0; s < YoyoChestSystem.MaxTotalSlots; s++)
				ItemIO.Send(data.Items[s] ?? new Item(), p);
			p.Send();
		}

		public static void Handle(BinaryReader reader, int whoAmI)
		{
			try {
				HandleInner(reader, whoAmI);
			}
			catch { }
		}

		private static void HandleInner(BinaryReader reader, int whoAmI)
		{
			Msg msg = (Msg)reader.ReadByte();
			if (msg != Msg.Push)
				return;
			Point tile = new Point(reader.ReadInt32(), reader.ReadInt32());
			bool upgraded = false;
			try {
				upgraded = reader.ReadBoolean();
			}
			catch {
				return;
			}
			var items = new Item[YoyoChestSystem.MaxTotalSlots];
			for (int s = 0; s < items.Length; s++) {
				try {
					items[s] = ItemIO.Receive(reader);
				}
				catch {
					items[s] = new Item();
				}
			}
			if (Main.netMode == NetmodeID.Server) {
				ApplyPushed(tile, upgraded, items);
				YoyoChestSystem.SyncWorld();
			}
			// Clients never receive Push (they converge via WorldData).
		}

		private static void ApplyPushed(Point tile, bool upgraded, Item[] items)
		{
			if (!YoyoChestSystem.IsTileStillChest(tile))
				return;
			if (upgraded && !Main.hardMode)
				upgraded = false;
			ChestData data = YoyoChestSystem.GetOrCreate(tile);
			data.Upgraded = upgraded;
			int per = data.SlotsPerCategory;
			for (int cat = 0; cat < YoyoChestSystem.CategoryCount; cat++) {
				for (int s = 0; s < YoyoChestSystem.UpSlotsPerCategory; s++) {
					int idx = cat * YoyoChestSystem.UpSlotsPerCategory + s;
					Item it = idx < items.Length ? items[idx] : null;
					if (it == null || it.IsAir) {
						data.Items[idx] = new Item();
						continue;
					}
					// Hidden slots (unexpanded 5-9) and wrong-category items
					// are dropped from the push, never stored.
					if (s >= per || !YoyoChestSystem.MatchesCategory(it, cat)) {
						data.Items[idx] = new Item();
						continue;
					}
					data.Items[idx] = it;
				}
			}
		}
	}
}
