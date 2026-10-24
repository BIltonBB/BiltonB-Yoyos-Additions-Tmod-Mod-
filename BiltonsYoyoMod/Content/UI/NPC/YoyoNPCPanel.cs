using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;

using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace BiltonsYoyoMod.Content.UI.YoyoNPC
{
	// Frame, tab row, shared widgets and draw dispatch for the 5-button NPC panel.
	// Right-click an NPC to open it; vanilla 2-button chat is fully bypassed.
	public static class YoyoNPCPanel
	{
		public const int PanelW = 660;
		public const int PanelH = 470;

		public static Rectangle PanelRect;
		public static Rectangle ContentRect;

		private static DynamicSpriteFont Font => FontAssets.MouseText.Value;

		public static bool MouseOverPanel()
		{
			if (PanelRect == Rectangle.Empty)
				return false;
			return PanelRect.Contains(Main.MouseScreen.ToPoint());
		}

		public static void Draw(SpriteBatch sb)
		{
			if (!YoyoNPCUISystem.Open)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;
			NPC npc = YoyoNPCUISystem.TargetNPC();
			if (npc == null)
				return;

			int x = (Main.screenWidth - PanelW) / 2;
			int y = (Main.screenHeight - PanelH) / 2;
			PanelRect = new Rectangle(x, y, PanelW, PanelH);
			ContentRect = new Rectangle(x + 14, y + 92, PanelW - 28, PanelH - 92 - 40);

			bool hoverPanel = MouseOverPanel();
			if (hoverPanel)
				player.mouseInterface = true;

			DrawPanelBackground(sb, PanelRect);
			DrawHeader(sb, npc, new Rectangle(x + 12, y + 8, PanelW - 24, 40));
			DrawTabs(sb, npc, new Rectangle(x + 12, y + 52, PanelW - 24, 32));

			YoyoNPCPanelTabs.DrawContent(sb, npc, ContentRect);

			DrawMessageBar(sb, new Rectangle(x + 14, y + PanelH - 34, PanelW - 28, 24));
		}

		// Frame

		public static void DrawPanelBackground(SpriteBatch sb, Rectangle r)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			sb.Draw(px, r, new Color(12, 15, 26, 235));
			// Outer gold-ish border.
			Color edge = new(150, 128, 84);
			sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 2), edge);
			sb.Draw(px, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), edge);
			sb.Draw(px, new Rectangle(r.X, r.Y, 2, r.Height), edge);
			sb.Draw(px, new Rectangle(r.Right - 2, r.Y, 2, r.Height), edge);
			// Inner thin line.
			Color inner = new(52, 62, 96);
			sb.Draw(px, new Rectangle(r.X + 4, r.Y + 4, r.Width - 8, 1), inner);
			sb.Draw(px, new Rectangle(r.X + 4, r.Bottom - 5, r.Width - 8, 1), inner);
			sb.Draw(px, new Rectangle(r.X + 4, r.Y + 4, 1, r.Height - 8), inner);
			sb.Draw(px, new Rectangle(r.Right - 5, r.Y + 4, 1, r.Height - 8), inner);
		}

		private static void DrawHeader(SpriteBatch sb, NPC npc, Rectangle r)
		{
			// Portrait (mod head icon when available).
			Rectangle portrait = new(r.X + 2, r.Y + 2, 36, 36);
			sb.Draw(TextureAssets.InventoryBack.Value, new Vector2(portrait.X, portrait.Y), null, Color.White * 0.85f, 0f, Vector2.Zero, 36f / TextureAssets.InventoryBack.Value.Width, SpriteEffects.None, 0f);
			DrawHeadIcon(sb, npc, new Vector2(portrait.X + 18, portrait.Y + 18), 30f);
			string title = YoyoNPCDialogue.NpcDisplayName(npc.type);
			Terraria.Utils.DrawBorderString(sb, title, new Vector2(r.X + 44, r.Y), Color.Gold, 1f);
			string sub = YoyoNPCDialogue.NpcSubtitle(npc.type);
			Terraria.Utils.DrawBorderString(sb, sub, new Vector2(r.X + 44, r.Y + 22), Color.LightGray, 0.75f);
			// Role badge: merchant / visitor / town pet.
			string role = RoleBadge(npc.type);
			Color roleColor = role == "TOWN PET" ? Color.LightGreen : role == "VISITOR" ? new Color(200, 170, 255) : Color.Gold;
			Vector2 roleSize = Font.MeasureString(role) * 0.7f;
			Terraria.Utils.DrawBorderString(sb, role, new Vector2(r.Right - 34 - roleSize.X, r.Y + 4), roleColor, 0.7f);
			Rectangle xBtn = new(r.Right - 28, r.Y + 2, 26, 26);
			if (Button(sb, xBtn, "X", "Close", false, false, 0.9f))
				YoyoNPCUISystem.ClosePanel();
		}

		public static string RoleBadge(int npcType)
		{
			if (npcType == ModContent.NPCType<Content.NPCs.Town.Pets.BillyTheBearing>()
				|| npcType == ModContent.NPCType<Content.NPCs.Town.Pets.MagicalYoyoPet>())
				return "TOWN PET";
			if (npcType == ModContent.NPCType<Content.NPCs.Town.SixStringer>()
				|| npcType == ModContent.NPCType<Content.NPCs.Town.YoyoCultist>())
				return "VISITOR";
			return "MERCHANT";
		}

		public static void DrawHeadIcon(SpriteBatch sb, NPC npc, Vector2 center, float maxSize)
		{
			if (npc.ModNPC == null)
				return;
			string path = npc.ModNPC.Texture + "_Head";
			if (!ModContent.RequestIfExists<Texture2D>(path, out var asset) || asset?.Value == null)
				return;
			Texture2D tex = asset.Value;
			float scale = Math.Min(maxSize / tex.Width, maxSize / tex.Height);
			sb.Draw(tex, center, null, Color.White, 0f, new Vector2(tex.Width, tex.Height) / 2f, scale, SpriteEffects.None, 0f);
		}

		private static void DrawTabs(SpriteBatch sb, NPC npc, Rectangle r)
		{
			string[] tabs = YoyoNPCTabs.GetTabs(npc.type);
			int n = tabs.Length;
			int gap = 6;
			int w = (r.Width - gap * (n - 1)) / n;
			for (int i = 0; i < n; i++) {
				Rectangle br = new(r.X + i * (w + gap), r.Y, w, r.Height);
				bool selected = YoyoNPCUISystem.TabId == tabs[i];
				if (Button(sb, br, YoyoNPCTabs.TabTitle(tabs[i]), null, selected, false, 0.85f)) {
					YoyoNPCUISystem.TabId = tabs[i];
					YoyoNPCUISystem.Page = 0;
					YoyoNPCUISystem.Sub = "";
					YoyoNPCUISystem.Message = "";
					YoyoNPCUISystem.ListScroll = 0;
					YoyoNPCUISystem.TradeScroll = 0;
					YoyoNPCUISystem.MoodScroll = 0;
					YoyoNPCUISystem.UpgradeScroll = 0;
				}
			}
		}

		private static void DrawMessageBar(SpriteBatch sb, Rectangle r)
		{
			string msg = YoyoNPCUISystem.Message;
			if (string.IsNullOrEmpty(msg))
				return;
			Terraria.Utils.DrawBorderString(sb, msg, new Vector2(r.X + 4, r.Y + 4), Color.Yellow, 0.8f);
		}

		// Widgets

		private static Rectangle lastClicked;
		private static int lastClickTick = -999999;
		private static int UiNow => (int)(Main.GameUpdateCount % 2147483647);

		// Word-wrap for hover tooltips: Main.hoverItemName renders one
		// unwrapped line, so a long single-line tip would stretch across
		// the whole screen. Wrap at word boundaries (~48 cols); existing
		// newlines are kept. Short tips pass through untouched.
		public static string WrapHover(string text, int maxCols = 48)
		{
			if (string.IsNullOrEmpty(text) || maxCols < 10)
				return text ?? "";
			var lines = new System.Collections.Generic.List<string>();
			foreach (string paragraph in text.Split('\n')) {
				string p = paragraph.Trim();
				if (p.Length == 0) {
					lines.Add("");
					continue;
				}
				string current = "";
				foreach (string word in p.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)) {
					if (current.Length == 0) {
						current = word;
					}
					else if (current.Length + 1 + word.Length > maxCols) {
						lines.Add(current);
						current = word;
					}
					else {
						current += " " + word;
					}
				}
				if (current.Length > 0)
					lines.Add(current);
			}
			return string.Join("\n", lines);
		}

		// Returns true on click. Consumes the click.
		// Visuals only: hover edge-glow pulse, gold underline while selected,
		// and a quick pressed dip on click. Return semantics unchanged.
		public static bool Button(SpriteBatch sb, Rectangle r, string text, string tooltip, bool selected, bool disabled, float textScale = 0.85f)
		{
			Texture2D px = TextureAssets.MagicPixel.Value;
			Point mouse = Main.MouseScreen.ToPoint();
			bool hover = !disabled && r.Contains(mouse);
			Color fill = selected ? new Color(70, 84, 130) : hover ? new Color(48, 58, 92) : new Color(30, 36, 58);
			Color edge = selected ? Color.Gold : hover ? new Color(160, 170, 220) : new Color(90, 98, 140);
			sb.Draw(px, r, fill);
			sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 1), edge);
			sb.Draw(px, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), edge);
			sb.Draw(px, new Rectangle(r.X, r.Y, 1, r.Height), edge);
			sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, r.Height), edge);
			if (hover && !disabled) {
				// Breathing inner highlight while hovered.
				float glow = 0.5f + 0.3f * (float)System.Math.Sin(UiNow * 0.12f);
				sb.Draw(px, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, 1), Color.White * (0.35f * glow));
			}
			if (selected) {
				// Gold underline marks the active tab/choice.
				sb.Draw(px, new Rectangle(r.X + 4, r.Bottom - 3, r.Width - 8, 2), Color.Gold * 0.9f);
			}
			if (r == lastClicked && UiNow - lastClickTick < 10) {
				// Pressed dip: brief dark overlay collapsing inward.
				float k = 1f - (UiNow - lastClickTick) / 10f;
				sb.Draw(px, r, new Color(0, 0, 0, (int)(90 * k)));
				sb.Draw(px, new Rectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2), Color.Gold * (0.5f * k));
			}
			Vector2 size = Font.MeasureString(text) * textScale;
			Color textColor = disabled ? Color.Gray : selected ? Color.White : Color.LightGray;
			Terraria.Utils.DrawBorderString(sb, text, new Vector2(r.X + (r.Width - size.X) / 2f, r.Y + (r.Height - size.Y) / 2f), textColor, textScale);
			if (hover) {
				Main.LocalPlayer.mouseInterface = true;
				if (!string.IsNullOrEmpty(tooltip))
					Main.hoverItemName = WrapHover(tooltip);
				if (Main.mouseLeft && Main.mouseLeftRelease) {
					Main.mouseLeftRelease = false;
					lastClicked = r;
					lastClickTick = UiNow;
					SoundEngine.PlaySound(SoundID.MenuTick);
					return true;
				}
			}
			return false;
		}

		private static readonly HashSet<int> loggedIconFailures = new();
		private static readonly Dictionary<int, ReLogic.Content.Asset<Texture2D>> vanillaIconCache = new();

		public static void DrawItemIcon(SpriteBatch sb, int itemType, Vector2 center, float maxSize, Color color)
		{
			Texture2D tex = null;
			try {
				if (itemType > Terraria.ID.ItemID.None && itemType < Terraria.ID.ItemID.Count) {
					// Own request, cached per type: immune to other mods
					// overwriting the shared TextureAssets.Item slots.
					if (!vanillaIconCache.TryGetValue(itemType, out var asset)) {
						asset = Main.Assets.Request<Texture2D>(
							"Images/Item_" + itemType,
							ReLogic.Content.AssetRequestMode.ImmediateLoad);
						vanillaIconCache[itemType] = asset;
					}
					tex = asset.Value;
				}
				else if (itemType >= Terraria.ID.ItemID.Count && itemType < TextureAssets.Item.Length) {
					tex = TextureAssets.Item[itemType].Value;
				}
			}
			catch {
				tex = null;
			}
			if (tex == null || tex.Width <= 0 || tex.Height <= 0) {
				// Unmissable fallback: dark plate + red "?" + one log line per type.
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)center.X - 11, (int)center.Y - 11, 22, 22), new Color(10, 10, 14, 230));
				Terraria.Utils.DrawBorderString(sb, "?", center - new Vector2(7f, 10f), Color.Red, 1f);
				if (loggedIconFailures.Add(itemType)) {
					string name;
					try { name = Lang.GetItemNameValue(itemType); }
					catch { name = "unknown"; }
					ModContent.GetInstance<BiltonsYoyoMod>().Logger.Warn($"Shop icon texture missing/blank for item type {itemType} ({name}).");
				}
				return;
			}
			// Sheet guard: some textures resolve taller than they should (multi-frame
			// sheets). Icons always show the top Width x Width square so a sheet can
			// never render as an invisible sliver.
			Rectangle? src = null;
			Vector2 origin = new Vector2(tex.Width, tex.Height) / 2f;
			if (tex.Height > tex.Width * 3 && tex.Width > 0) {
				src = new Rectangle(0, 0, tex.Width, tex.Width);
				origin = new Vector2(tex.Width, tex.Width) / 2f;
			}
			float drawW = src?.Width ?? tex.Width;
			float drawH = src?.Height ?? tex.Height;
			float scale = Math.Min(maxSize / drawW, maxSize / drawH);
			sb.Draw(tex, center, src, color, 0f, origin, scale, SpriteEffects.None, 0f);
		}

		public static void DrawCoins(SpriteBatch sb, Vector2 pos, int copper, float scale = 0.8f)
		{
			int plat = copper / 1000000; copper %= 1000000;
			int gold = copper / 10000; copper %= 10000;
			int silver = copper / 100; copper %= 100;
			float x = pos.X;
			x = DrawOneCoin(sb, x, pos.Y, ItemID.PlatinumCoin, plat, scale);
			x = DrawOneCoin(sb, x, pos.Y, ItemID.GoldCoin, gold, scale);
			x = DrawOneCoin(sb, x, pos.Y, ItemID.SilverCoin, silver, scale);
			x = DrawOneCoin(sb, x, pos.Y, ItemID.CopperCoin, copper, scale);
		}

		private static float DrawOneCoin(SpriteBatch sb, float x, float y, int coinType, int amount, float scale)
		{
			if (amount <= 0)
				return x;
			DrawItemIcon(sb, coinType, new Vector2(x + 8f, y + 8f), 16f, Color.White);
			string text = amount.ToString();
			Terraria.Utils.DrawBorderString(sb, text, new Vector2(x + 18f, y + 2f), Color.White, scale);
			return x + 18f + Font.MeasureString(text).X * scale + 8f;
		}

		public static string FormatCoins(int copper)
		{
			int plat = copper / 1000000; copper %= 1000000;
			int gold = copper / 10000; copper %= 10000;
			int silver = copper / 100; copper %= 100;
			var parts = new List<string>();
			if (plat > 0) parts.Add($"{plat} platinum");
			if (gold > 0) parts.Add($"{gold} gold");
			if (silver > 0) parts.Add($"{silver} silver");
			if (copper > 0 || parts.Count == 0) parts.Add($"{copper} copper");
			return string.Join(" ", parts);
		}

		public static List<string> WrapText(string text, float maxWidth, float scale = 0.85f)
		{
			var lines = new List<string>();
			foreach (string paragraph in text.Split('\n')) {
				string[] words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				string current = "";
				foreach (string word in words) {
					string test = string.IsNullOrEmpty(current) ? word : current + " " + word;
					if (Font.MeasureString(test).X * scale <= maxWidth || string.IsNullOrEmpty(current))
						current = test;
					else {
						lines.Add(current);
						current = word;
					}
				}
				lines.Add(current);
			}
			return lines;
		}

		public static float DrawTextBlock(SpriteBatch sb, Vector2 pos, string text, Color color, float scale, float maxWidth)
		{
			float y = pos.Y;
			foreach (string line in WrapText(text, maxWidth, scale)) {
				Terraria.Utils.DrawBorderString(sb, line, new Vector2(pos.X, y), color, scale);
				y += 20f * scale + 2f;
			}
			return y - pos.Y;
		}
	}
}
