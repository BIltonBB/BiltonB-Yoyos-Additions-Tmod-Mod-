using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using YoyoPanel = BiltonsYoyoMod.Content.UI.YoyoNPC.YoyoNPCPanel;

namespace BiltonsYoyoMod.Content.Storage
{
	// Yoyo Chest UI: 5 labeled rows x 5 slots. Left-click swaps with the
	// mouse item when the category accepts it; otherwise shows the reason.
	public class YoyoChestUISystem : ModSystem
	{
		public static bool Open;
		public static Point OpenTile;
		public static string Message = "";
		public static int MessageTimer;

		private const int SlotSize = 38;
		private const int Gap = 42;

		public static void OpenPanel(int x, int y)
		{
			Open = true;
			OpenTile = new Point(x, y);
			Message = "";
			MessageTimer = 0;
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public static void Close() => Open = false;

		public override void UpdateUI(GameTime gameTime)
		{
			if (!Open)
				return;
			if (MessageTimer > 0 && --MessageTimer <= 0)
				Message = "";
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead) {
				Close();
				return;
			}
			Vector2 tileWorld = new(OpenTile.X * 16f + 16f, OpenTile.Y * 16f + 16f);
			if (Vector2.Distance(player.Center, tileWorld) > 700f)
				Close();
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
				Close();
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Yoyo Chest",
					() => {
						DrawPanel(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI));
		}

		private static void DrawPanel(SpriteBatch sb)
		{
			if (!Open)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;
			int pw = System.Math.Min(580, Main.screenWidth - 16);
			int ph = System.Math.Min(600, Main.screenHeight - 16);
			int x = (Main.screenWidth - pw) / 2;
			int y = (Main.screenHeight - ph) / 2;
			Rectangle rect = new(x, y, pw, ph);
			if (rect.Contains(Main.MouseScreen.ToPoint()))
				player.mouseInterface = true;
			YoyoPanel.DrawPanelBackground(sb, rect);
			Placeables.PlaceableUI.HeaderStrip(sb, x, y, pw, new Color(200, 160, 60));
			YoyoPanel.DrawItemIcon(sb, ModContent.ItemType<global::BiltonsYoyoMod.Tiles.Storage.YoyoChestItem>(), new Vector2(x + 28, y + 22), 24f, Color.White);
			Terraria.Utils.DrawBorderString(sb, "Yoyo Chest", new Vector2(x + 46, y + 12), Color.Gold, 1f);
			Placeables.PlaceableUI.DrawWrapped(sb, "Five drawers, five slots each. Only the matching yoyo gear fits.", new Vector2(x + 46, y + 34), Color.LightGray, 0.75f, pw - 60);
			float cy = y + 62;
			Placeables.PlaceableUI.Divider(sb, x + 14, (int)cy, pw - 28, new Color(90, 98, 140, 180));
			cy += 8;
			Item[] chest = YoyoChestSystem.GetOrCreate(OpenTile).Items;
			int perDrawer = YoyoChestSystem.SlotsPerCategory(OpenTile);
			bool expanded = YoyoChestSystem.IsUpgraded(OpenTile);
			int total = YoyoChestSystem.TotalSlots(OpenTile);
			int stored = 0;
			int[] filled = new int[YoyoChestSystem.CategoryCount];
			for (int cat = 0; cat < YoyoChestSystem.CategoryCount; cat++) {
				for (int s = 0; s < perDrawer; s++) {
					Item it = chest[cat * YoyoChestSystem.UpSlotsPerCategory + s];
					if (it != null && !it.IsAir) {
						stored++;
						filled[cat]++;
					}
				}
			}
			Terraria.Utils.DrawBorderString(sb, $"Stored: {stored}/{total}", new Vector2(x + 14, cy), Color.Gold, 0.8f);
			cy += 22;
			// Sort + one-time Hardmode expansion, side by side.
			// Sort + Quick Stack + one-time Hardmode expansion.
			Rectangle sortBtn = new(x + 14, (int)cy, 110, 24);
			if (YoyoPanel.Button(sb, sortBtn, "Sort", "Stacks and groups every drawer by item type.", false, false, 0.7f)) {
				YoyoChestSystem.SortChest(OpenTile);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.Grab);
			}
			Rectangle stackBtn = new(x + 130, (int)cy, 130, 24);
			if (YoyoPanel.Button(sb, stackBtn, "Quick Stack", "Pulls matching gear from your inventory into the right drawers.", false, false, 0.7f)) {
				int moved = YoyoChestSystem.TryQuickStack(player, OpenTile);
				Message = moved > 0 ? $"Stacked {moved} item{(moved == 1 ? "" : "s")}." : "Nothing to stack.";
				MessageTimer = 240;
				Terraria.Audio.SoundEngine.PlaySound(moved > 0 ? SoundID.Grab : SoundID.MenuTick);
			}
			string expLabel = expanded ? "Expanded" : $"Expand 5→10 ({YoyoChestSystem.ExpandCostText()})";
			string expTip = expanded ? "This chest is fully expanded."
				: !Main.hardMode ? "Needs Hardmode."
				: $"One-time upgrade: every drawer holds {YoyoChestSystem.UpSlotsPerCategory}. Costs {YoyoChestSystem.ExpandCostText()}.";
			Rectangle expBtn = new(x + 266, (int)cy, pw - 28 - 252, 24);
			bool expCan = !expanded && Main.hardMode;
			if (YoyoPanel.Button(sb, expBtn, expLabel, expTip, expanded, !expCan, 0.75f)) {
				if (YoyoChestSystem.TryUpgrade(player, OpenTile)) {
					Message = "Chest expanded: every drawer now holds 10.";
					MessageTimer = 300;
					Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup);
				}
				else {
					Message = !Main.hardMode ? "Expansion needs Hardmode." : $"Expansion needs {YoyoChestSystem.ExpandCostText()}.";
					MessageTimer = 300;
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
				}
			}
			cy += 30;
			for (int cat = 0; cat < YoyoChestSystem.CategoryCount; cat++) {
				// Soft drawer plate so rows read as furniture, not bare slots.
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x + 8, (int)cy - 2, pw - 16, 22 + SlotSize + 4), new Color(20, 24, 40, 120));
				Terraria.Utils.DrawBorderString(sb, $"{YoyoChestSystem.CategoryName(cat)} ({filled[cat]}/{perDrawer}):", new Vector2(x + 14, cy), CatColor(cat), 0.8f);
				cy += 22;
				for (int s = 0; s < perDrawer; s++) {
					int slot = cat * YoyoChestSystem.UpSlotsPerCategory + s;
					Vector2 pos = new(x + 14 + s * Gap, cy);
					DrawSlot(sb, chest, slot, cat, pos);
				}
				cy += SlotSize + 8;
			}
			Rectangle xBtn = new(x + pw - 42, y + 8, 32, 30);
			if (YoyoPanel.Button(sb, xBtn, "X", "Close", false, false, 0.95f))
				Close();
			Placeables.PlaceableUI.Divider(sb, x + 14, (int)cy, pw - 28, new Color(90, 98, 140, 180));
			cy += 6;
			cy += Placeables.PlaceableUI.DrawWrapped(sb, "Drawers take yoyos, yoyo accessories, yoyo tonics, yoyo materials, then other yoyo gear — in that order, nothing else.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += Placeables.PlaceableUI.DrawWrapped(sb, "Breaking the chest drops everything inside.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			if (!string.IsNullOrEmpty(Message))
				Placeables.PlaceableUI.DrawWrapped(sb, Message, new Vector2(x + 14, cy), Color.Yellow, 0.8f, pw - 28);
		}

		private static Color CatColor(int cat) => cat switch {
			0 => new Color(255, 220, 130),
			1 => new Color(150, 200, 255),
			2 => new Color(255, 170, 200),
			3 => new Color(170, 230, 170),
			_ => Color.LightGray,
		};

		private static void DrawSlot(SpriteBatch sb, Item[] chest, int slot, int category, Vector2 pos)
		{
			Rectangle hitbox = new((int)pos.X, (int)pos.Y, SlotSize, SlotSize);
			bool hover = hitbox.Contains(Main.MouseScreen.ToPoint());
			// Slot plate.
			sb.Draw(TextureAssets.MagicPixel.Value, hitbox, new Color(24, 20, 36, 235));
			Color trim = hover ? new Color(255, 225, 150) : new Color(176, 128, 52);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(hitbox.X, hitbox.Y, hitbox.Width, 1), trim);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(hitbox.X, hitbox.Bottom - 1, hitbox.Width, 1), trim);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(hitbox.X, hitbox.Y, 1, hitbox.Height), trim);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(hitbox.Right - 1, hitbox.Y, 1, hitbox.Height), trim);

			Item item = chest[slot];
			if (item != null && !item.IsAir)
				YoyoPanel.DrawItemIcon(sb, item.type, pos + new Vector2(SlotSize / 2f, SlotSize / 2f), 28f, Color.White);

			if (!hover)
				return;
			Main.LocalPlayer.mouseInterface = true;
			if (item != null && !item.IsAir)
				ItemSlot.MouseHover(ref chest[slot], ItemSlot.Context.ChestItem);
			else
				Main.hoverItemName = YoyoChestSystem.CategoryName(category);
			if (Main.mouseLeft && Main.mouseLeftRelease) {
				Main.mouseLeftRelease = false;
				bool canTake = true;
				if (!Main.mouseItem.IsAir && !YoyoChestSystem.MatchesCategory(Main.mouseItem, category)) {
					Message = YoyoChestSystem.RejectReason(category);
					MessageTimer = 240;
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
					canTake = false;
				}
				if (canTake && !chest[slot].IsAir && !Main.mouseItem.IsAir && !YoyoChestSystem.MatchesCategory(chest[slot], category)) {
					// Safety: never let a wrong item get stuck via swap.
					canTake = false;
				}
				if (canTake) {
					Item held = Main.mouseItem;
					Main.mouseItem = chest[slot];
					chest[slot] = held;
					YoyoChestSystem.PushTile(OpenTile);
					Terraria.Audio.SoundEngine.PlaySound(SoundID.Grab);
				}
			}
		}
	}
}
