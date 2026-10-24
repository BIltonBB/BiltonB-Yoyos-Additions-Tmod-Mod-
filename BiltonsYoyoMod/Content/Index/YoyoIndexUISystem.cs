using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace BiltonsYoyoMod.Content.Index
{
	/// <summary>
	/// Bestiary-style collection UI for the Yoyo Index (NOT a guidebook):
	/// grid of yoyo icons, obtained in colour vs missing as silhouette,
	/// All/Obtained/Missing tabs, completion bar, hover source hints.
	/// Client-side only.
	/// </summary>
	public class YoyoIndexUISystem : ModSystem
	{
		public static bool Open;
		private static int page;
		private static int tab; // 0 All, 1 Obtained, 2 Missing.
		private static int openTimer;
		private static bool prevLeft;
		private static Rectangle prevBtn;
		private static Rectangle nextBtn;
		private static Rectangle closeBtn;
		private static readonly Rectangle[] tabBtns = new Rectangle[3];

		private const int Cols = 7;
		private const int Rows = 4;

		public static void OpenIndex()
		{
			page = 0;
			tab = 0;
			openTimer = 12;
			Open = true;
			// Modal like the books: inventory folds away while browsing.
			Main.playerInventory = false;
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public static void CloseIndex()
		{
			if (!Open)
				return;
			Open = false;
			SoundEngine.PlaySound(SoundID.MenuClose);
		}

		private static List<int> Filtered(Player player, HashSet<int> obtained)
		{
			var all = YoyoIndexRegistry.AllYoyoTypes();
			if (tab == 0)
				return all;
			var out_ = new List<int>();
			foreach (int t in all) {
				bool has = obtained.Contains(t);
				if (tab == 1 && has)
					out_.Add(t);
				else if (tab == 2 && !has)
					out_.Add(t);
			}
			return out_;
		}

		public override void PostUpdateInput()
		{
			if (Main.gameMenu || Main.dedServ || !Open)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active) {
				prevLeft = Main.mouseLeft;
				return;
			}
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) {
				CloseIndex();
				return;
			}
			bool left = Main.mouseLeft;
			if (left && !prevLeft && !Main.blockMouse) {
				Point mp = Main.MouseScreen.ToPoint();
				if (closeBtn.Contains(mp))
					CloseIndex();
				else if (prevBtn.Contains(mp)) {
					if (page > 0) { page--; SoundEngine.PlaySound(SoundID.MenuTick); }
				}
				else if (nextBtn.Contains(mp)) {
					page++; SoundEngine.PlaySound(SoundID.MenuTick);
				}
				else {
					for (int i = 0; i < 3; i++) {
						if (tabBtns[i].Contains(mp) && tab != i) {
							tab = i; page = 0; SoundEngine.PlaySound(SoundID.MenuTick);
							break;
						}
					}
				}
			}
			prevLeft = left;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!Open)
				return;
			if (openTimer > 0)
				openTimer--;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
				CloseIndex();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Yoyo Index",
					() => {
						DrawIndex(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI));
		}

		private static void DrawIndex(SpriteBatch sb)
		{
			if (!Open)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;
			var ip = player.GetModPlayer<YoyoIndexPlayer>();
			float pct = ip.Completion(out int obtained, out int total);

			int pw = System.Math.Min(640, Main.screenWidth - 24);
			int ph = System.Math.Min(480, Main.screenHeight - 24);
			int x = (Main.screenWidth - pw) / 2;
			int y = (Main.screenHeight - ph) / 2;
			Rectangle rect = new(x, y, pw, ph);
			if (rect.Contains(Main.MouseScreen.ToPoint()))
				player.mouseInterface = true;

			float openT = openTimer > 0 ? 1f - openTimer / 12f : 1f;
			sb.Draw(TextureAssets.MagicPixel.Value, rect, new Color(12, 12, 18, (byte)(225 * openT)));
			Color gold = new Color(200, 160, 90, (byte)(220 * openT));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, pw, 1), gold);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y + ph - 1, pw, 1), gold);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, 1, ph), gold);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x + pw - 1, y, 1, ph), gold);

			Terraria.Utils.DrawBorderString(sb, "Yoyo Index - every yoyo ever carried", new Vector2(x + 16, y + 10), Color.Gold * openT, 0.95f);
			Terraria.Utils.DrawBorderString(sb, $"{obtained}/{total} ({pct * 100f:0}%)", new Vector2(x + pw - 130, y + 10), Color.LightGray * openT, 0.85f);

			// Completion bar.
			Rectangle bar = new(x + 16, y + 36, pw - 32, 12);
			sb.Draw(TextureAssets.MagicPixel.Value, bar, new Color(40, 40, 50, 220));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bar.X, bar.Y, (int)(bar.Width * pct), bar.Height), new Color(255, 210, 120, 230));

			// Tabs.
			string[] tabs = { "All", "Obtained", "Missing" };
			for (int i = 0; i < 3; i++) {
				Rectangle b = new(x + 16 + i * 110, y + 54, 100, 26);
				tabBtns[i] = b;
				bool sel = tab == i;
				bool hover = b.Contains(Main.MouseScreen.ToPoint());
				sb.Draw(TextureAssets.MagicPixel.Value, b, sel ? new Color(110, 85, 50, 235) : hover ? new Color(70, 60, 45, 225) : new Color(45, 42, 40, 215));
				Vector2 sz = FontAssets.MouseText.Value.MeasureString(tabs[i]) * 0.8f;
				Terraria.Utils.DrawBorderString(sb, tabs[i], new Vector2(b.X + (b.Width - sz.X) / 2f, b.Y + (b.Height - sz.Y) / 2f), sel ? Color.White : Color.Gray, 0.8f);
			}

			var list = Filtered(player, ip.Obtained);
			int perPage = Cols * Rows;
			int maxPage = System.Math.Max(0, (list.Count - 1) / perPage);
			page = System.Math.Clamp(page, 0, maxPage);

			int gx = x + 24;
			int gy = y + 92;
			int cell = 72;
			Rectangle hoverBox = default;
			int hoverType = -1;
			bool hoverHas = false;

			for (int i = 0; i < perPage; i++) {
				int li = page * perPage + i;
				if (li >= list.Count)
					break;
				int t = list[li];
				int cx = gx + (i % Cols) * cell;
				int cy = gy + (i / Cols) * cell;
				Rectangle cellBox = new(cx, cy, 64, 64);
				bool has = ip.Obtained.Contains(t);
				sb.Draw(TextureAssets.MagicPixel.Value, cellBox, has ? new Color(50, 45, 38, 220) : new Color(25, 24, 28, 220));
				try {
					Texture2D tex = TextureAssets.Item[t].Value;
					float s = System.Math.Min(1f, 44f / System.Math.Max(tex.Width, tex.Height));
					Color c = has ? Color.White : new Color(0, 0, 0, 180);
					sb.Draw(tex, new Vector2(cx + 32, cy + 32), null, c * openT, 0f, tex.Size() * 0.5f, s, SpriteEffects.None, 0f);
					if (!has)
						Terraria.Utils.DrawBorderString(sb, "?", new Vector2(cx + 26, cy + 20), Color.Gray * 0.8f, 0.9f);
				}
				catch { }
				if (cellBox.Contains(Main.MouseScreen.ToPoint())) {
					hoverBox = cellBox;
					hoverType = t;
					hoverHas = has;
				}
			}

			// Hover tooltip.
			if (hoverType >= 0) {
				sb.Draw(TextureAssets.MagicPixel.Value, hoverBox, Color.White * 0.25f);
				string name;
				try { name = Lang.GetItemNameValue(hoverType); }
				catch { name = "???"; }
				string status = hoverHas ? "Obtained" : YoyoIndexRegistry.SourceHint(hoverType);
				Vector2 mp = Main.MouseScreen + new Vector2(16, 16);
				string tip = hoverHas ? name : "???";
				Terraria.Utils.DrawBorderString(sb, tip, mp, Color.White, 0.85f);
				Terraria.Utils.DrawBorderString(sb, status, mp + new Vector2(0, 20), Color.Gray, 0.7f);
				player.mouseInterface = true;
			}

			prevBtn = new Rectangle(x + 16, y + ph - 40, 110, 28);
			nextBtn = new Rectangle(x + pw - 126, y + ph - 40, 110, 28);
			closeBtn = new Rectangle(x + pw - 44, y + 6, 32, 26);
			Button(sb, prevBtn, "< Prev", page <= 0, openT);
			Button(sb, nextBtn, $"Next {page + 1}/{maxPage + 1}", page >= maxPage, openT);
			Button(sb, closeBtn, "X", false, openT);
		}

		private static void Button(SpriteBatch sb, Rectangle b, string label, bool disabled, float alpha)
		{
			bool hover = b.Contains(Main.MouseScreen.ToPoint()) && !disabled;
			sb.Draw(TextureAssets.MagicPixel.Value, b, disabled ? new Color(40, 38, 34, 200) : hover ? new Color(110, 85, 50, 235) : new Color(70, 55, 36, 225));
			Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * 0.8f;
			Terraria.Utils.DrawBorderString(sb, label, new Vector2(b.X + (b.Width - size.X) / 2f, b.Y + (b.Height - size.Y) / 2f), disabled ? Color.Gray : Color.White, 0.8f);
		}
	}
}
