using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using YoyoPanel = BiltonsYoyoMod.Content.UI.YoyoNPC.YoyoNPCPanel;

namespace BiltonsYoyoMod.Traps
{
	public static class TrapUI
	{
		public static bool Open;
		public static Point OpenTile;
		public static TrapKind OpenKind;
		public static int Tab;
		public static string Message = "";
		public static int MessageTimer;

		// Panel scroll (overflow guard): wheel over the panel pans content.
		public static float ScrollY;
		public static float ScrollMax;
		public static int prevWheel;
		// Content clip band (culling): rows outside [TrapUI.ClipTopY, TrapUI.ClipBotY] are
		// skipped visually but still advance layout, so scrolled content can
		// never paint outside the panel ("overflow when outside").
		public static float ClipTopY = -99999f;
		public static float ClipBotY = 99999f;

		// Clipped word-wrap: identical layout to PlaceableUI.DrawWrapped, but
		// lines outside the clip band are skipped (layout still advances).
		public static float CWrap(SpriteBatch sb, string text, Vector2 pos, Color color, float scale, float maxWidth)
		{
			var font = FontAssets.MouseText.Value;
			float lineH = font.LineSpacing * scale;
			float y = pos.Y;
			string line = "";
			foreach (string word in text.Split(' ')) {
				string test = line.Length == 0 ? word : line + " " + word;
				if (line.Length > 0 && font.MeasureString(test).X * scale > maxWidth) {
					CChars(sb, line, pos.X, y, scale, color);
					y += lineH;
					line = word;
				}
				else {
					line = test;
				}
			}
			if (line.Length > 0) {
				CChars(sb, line, pos.X, y, scale, color);
				y += lineH;
			}
			return y - pos.Y;
		}

		// Fixed-header wrap: identical layout to CWrap, always draws.
		public static float HWrap(SpriteBatch sb, string text, Vector2 pos, Color color, float scale, float maxWidth)
		{
			var font = FontAssets.MouseText.Value;
			float lineH = font.LineSpacing * scale;
			float y = pos.Y;
			string line = "";
			foreach (string word in text.Split(' ')) {
				string test = line.Length == 0 ? word : line + ' ' + word;
				if (line.Length > 0 && font.MeasureString(test).X * scale > maxWidth) {
					Terraria.Utils.DrawBorderString(sb, line, new Vector2(pos.X, y), color, scale);
					y += lineH;
					line = word;
				}
				else {
					line = test;
				}
			}
			if (line.Length > 0) {
				Terraria.Utils.DrawBorderString(sb, line, new Vector2(pos.X, y), color, scale);
				y += lineH;
			}
			return y - pos.Y;
		}

				// Clipped UI helpers (tech-tree discipline): every scrolled row draws
		// through these, so text and buttons can never paint or click outside
		// the content viewport (no overflow, overlap, or phantom clicks).
		public static bool Visible(float cy, float h)
		{
			return cy + h > ClipTopY && cy < ClipBotY;
		}

		public static Rectangle ClipView;

		public static void CText(SpriteBatch sb, string text, Vector2 pos, Color color, float scale)
		{
			CChars(sb, text, pos.X, pos.Y, scale, color);
		}

		// Tech-tree ClipFill: rects outside the viewport simply do not draw.
		public static void CFill(SpriteBatch sb, Rectangle r, Color color)
		{
			Rectangle c = Rectangle.Intersect(r, ClipView);
			if (c.Width > 0 && c.Height > 0)
				sb.Draw(TextureAssets.MagicPixel.Value, c, new Rectangle(0, 0, 1, 1), color);
		}

		// Fixed-header fill: always draws (header never scrolls).
		public static void HFill(SpriteBatch sb, Rectangle r, Color color)
		{
			if (r.Width > 0 && r.Height > 0)
				sb.Draw(TextureAssets.MagicPixel.Value, r, new Rectangle(0, 0, 1, 1), color);
		}

		// Tech-tree clipped text, left-aligned: partial rows keep readable
		// fragments instead of bleeding past the viewport edge.
		public static void CChars(SpriteBatch sb, string text, float x, float y, float scale, Color color)
		{
			ClipRun(sb, text, x, y, scale, color, -1f);
		}

		public static void CCharsC(SpriteBatch sb, string text, float centerX, float y, float scale, Color color)
		{
			ClipRun(sb, text, centerX, y, scale, color, centerX);
		}

		private static void ClipRun(SpriteBatch sb, string text, float x, float y, float scale, Color color, float centerX)
		{
			// Pixel clipping (GPU scissor): partial glyphs draw partially.
			// centerX < 0 means left-aligned at x (CChars); else centered.
			Content.Placeables.PlaceableUI.DrawClippedRun(sb, text, centerX < 0f ? x : centerX, y, scale, color, ClipView, centered: centerX >= 0f);
		}

		// Clipped button (tech-tree node discipline): chrome hard-clips to the
		// viewport, hover/click only fire with the mouse inside the viewport.
		public static bool CButton(SpriteBatch sb, Rectangle r, string text, string tooltip, bool selected, bool disabled, float textScale = 0.85f)
		{
			if (Rectangle.Intersect(r, ClipView).IsEmpty)
				return false;
			Point m = Main.MouseScreen.ToPoint();
			bool inView = ClipView.Contains(m);
			bool hover = !disabled && inView && r.Contains(m);
			Color fill = selected ? new Color(70, 84, 130) : hover ? new Color(48, 58, 92) : new Color(30, 36, 58);
			Color edge = selected ? Color.Gold : hover ? new Color(160, 170, 220) : new Color(90, 98, 140);
			CFill(sb, r, fill);
			CFill(sb, new Rectangle(r.X, r.Y, r.Width, 1), edge);
			CFill(sb, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), edge);
			CFill(sb, new Rectangle(r.X, r.Y, 1, r.Height), edge);
			CFill(sb, new Rectangle(r.Right - 1, r.Y, 1, r.Height), edge);
			if (hover) {
				float glow = 0.5f + 0.3f * (float)System.Math.Sin((Main.GameUpdateCount % 2147483647) * 0.12f);
				CFill(sb, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, 1), Color.White * (0.35f * glow));
			}
			if (selected)
				CFill(sb, new Rectangle(r.X + 4, r.Bottom - 3, r.Width - 8, 2), Color.Gold * 0.9f);
			var font = FontAssets.MouseText.Value;
			Vector2 tsize = font.MeasureString(text) * textScale;
			Color tc = disabled ? Color.Gray : selected ? Color.White : Color.LightGray;
			CCharsC(sb, text, r.X + r.Width / 2f, r.Y + (r.Height - tsize.Y) / 2f, textScale, tc);
			if (hover) {
				Main.LocalPlayer.mouseInterface = true;
				if (!string.IsNullOrEmpty(tooltip))
					Main.hoverItemName = YoyoPanel.WrapHover(tooltip);
				if (Main.mouseLeft && Main.mouseLeftRelease) {
					Main.mouseLeftRelease = false;
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
					return true;
				}
			}
			return false;
		}

		public static void CDivider(SpriteBatch sb, int x, int y, int w, Color color)
		{
			CFill(sb, new Rectangle(x, y, w, 1), color);
		}

		public static void CPips(SpriteBatch sb, Vector2 pos, int level, int max)
		{
			for (int i = 0; i < max; i++) {
				Color pc = i < level ? Color.Gold : new Color(50, 56, 84, 220);
				CFill(sb, new Rectangle((int)pos.X + i * 14, (int)pos.Y, 10, 10), pc);
			}
		}

		public static void OpenFor(TrapKind kind, int x, int y)
		{
			// Offensive traps never open a panel.
			if (kind == TrapKind.WildSnare || kind == TrapKind.Bearing)
				return;
			// Idempotent: the tall-art click hook and the vanilla tile click
			// can fire on the same tick — never double-open (double sound).
			if (Open && OpenTile.X == x && OpenTile.Y == y)
				return;
			Open = true;
			OpenKind = kind;
			OpenTile = new Point(x, y);
			Tab = 0;
			ScrollY = 0;
			ScrollMax = 0;
			ClipView = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
			Message = "";
			MessageTimer = 0;
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public static void Close()
		{
			// Closing commits any open naming draft (never silently drop text).
			if (NamingTile.X != -9999 && TrapSystem.Traps.TryGetValue(NamingTile, out var ns)) {
				ns.Name = NamingBuf ?? "";
				if (ns.Name.Length > MaxNameLen)
					ns.Name = ns.Name.Substring(0, MaxNameLen);
				TrapUISystem.PushConfig(NamingTile, ns);
			}
			NamingTile = new Point(-9999, -9999);
			NamingBuf = "";
			Open = false;
		}

		// Free-text trap naming (max 16 chars, whitelist for font safety).
		// Staged locally per keystroke, pushed to server on confirm.
		public const int MaxNameLen = 16;
		public static Point NamingTile = new Point(-9999, -9999);
		public static string NamingBuf = "";
		private static Microsoft.Xna.Framework.Input.KeyboardState prevKeys;

		public static bool NamingThis(Point tile) => Open && NamingTile == tile;

		private static bool NameChar(Microsoft.Xna.Framework.Input.Keys k, bool shift, out char c)
		{
			c = '\0';
			if (k >= Microsoft.Xna.Framework.Input.Keys.A && k <= Microsoft.Xna.Framework.Input.Keys.Z) {
				c = (char)((shift ? 'A' : 'a') + (k - Microsoft.Xna.Framework.Input.Keys.A));
				return true;
			}
			if (k >= Microsoft.Xna.Framework.Input.Keys.D0 && k <= Microsoft.Xna.Framework.Input.Keys.D9) {
				c = (char)('0' + (k - Microsoft.Xna.Framework.Input.Keys.D0));
				return true;
			}
			if (k >= Microsoft.Xna.Framework.Input.Keys.NumPad0 && k <= Microsoft.Xna.Framework.Input.Keys.NumPad9) {
				c = (char)('0' + (k - Microsoft.Xna.Framework.Input.Keys.NumPad0));
				return true;
			}
			if (k == Microsoft.Xna.Framework.Input.Keys.Space) { c = ' '; return true; }
			if (k == Microsoft.Xna.Framework.Input.Keys.OemMinus) { c = shift ? '_' : '-'; return true; }
			return false;
		}

		public static void UpdateNaming(Player player, Point tile)
		{
			var ks = Main.keyState;
			bool shift = ks.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || ks.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
			foreach (var k in ks.GetPressedKeys()) {
				if (System.Array.IndexOf(prevKeys.GetPressedKeys(), k) >= 0)
					continue; // held key, not a new press
				if (k == Microsoft.Xna.Framework.Input.Keys.Escape || k == Microsoft.Xna.Framework.Input.Keys.Enter) {
					CommitName(player, tile);
					prevKeys = ks;
					return;
				}
				if (k == Microsoft.Xna.Framework.Input.Keys.Back) {
					if (NamingBuf.Length > 0) {
						NamingBuf = NamingBuf.Substring(0, NamingBuf.Length - 1);
						if (TrapSystem.Traps.TryGetValue(tile, out var bs)) bs.Name = NamingBuf;
					}
					continue;
				}
				if (NamingBuf.Length >= MaxNameLen)
					continue;
				if (NameChar(k, shift, out char c)) {
					NamingBuf += c;
					if (TrapSystem.Traps.TryGetValue(tile, out var bs)) bs.Name = NamingBuf;
				}
			}
			prevKeys = ks;
		}

		public static void CommitName(Player player, Point tile)
		{
			if (TrapSystem.Traps.TryGetValue(tile, out var s)) {
				s.Name = NamingBuf ?? "";
				if (s.Name.Length > MaxNameLen)
					s.Name = s.Name.Substring(0, MaxNameLen);
				TrapUISystem.PushConfig(tile, s);
			}
			NamingTile = new Point(-9999, -9999);
			NamingBuf = "";
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
		}

		public static void SetMessage(string t, int time = 360)
		{
			Message = t; MessageTimer = time;
		}
	}

	public class TrapUISystem : ModSystem
	{
		private static Rectangle PanelRect;

		public override void UpdateUI(GameTime gameTime)
		{
			if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 30 == 0)
				TrapActions.TryClaimOffensiveUpgrades(Main.LocalPlayer);
			// Panel wheel-scroll (overflow guard): only while hovering the panel.
			int wheel = Microsoft.Xna.Framework.Input.Mouse.GetState().ScrollWheelValue;
			int wheelDelta = wheel - TrapUI.prevWheel;
			TrapUI.prevWheel = wheel;
			if (wheelDelta != 0 && TrapUI.Open && PanelRect.Contains(Main.MouseScreen.ToPoint()))
				TrapUI.ScrollY = System.Math.Clamp(TrapUI.ScrollY - wheelDelta * 0.25f, 0f, TrapUI.ScrollMax);
			SentryTallInteract();
			if (!TrapUI.Open)
				return;
			if (TrapUI.MessageTimer > 0 && --TrapUI.MessageTimer <= 0)
				TrapUI.Message = "";
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead) {
				TrapUI.Close();
				return;
			}
			// Free-text naming captures keys; Esc/Enter inside commits.
			if (TrapUI.NamingTile.X != -9999) {
				if (TrapUI.NamingTile != TrapUI.OpenTile) {
					// Panel moved on: commit the stale draft, follow the panel.
					if (TrapSystem.Traps.TryGetValue(TrapUI.NamingTile, out var olds)) {
						olds.Name = TrapUI.NamingBuf ?? "";
						if (olds.Name.Length > TrapUI.MaxNameLen)
							olds.Name = olds.Name.Substring(0, TrapUI.MaxNameLen);
						if (Main.netMode == NetmodeID.MultiplayerClient)
							TrapNet.SendConfig(TrapUI.NamingTile, olds);
						else
							TrapActions.ApplyConfig(TrapUI.NamingTile, olds);
					}
					TrapUI.NamingTile = TrapUI.OpenTile;
					TrapUI.NamingBuf = TrapSystem.Traps.TryGetValue(TrapUI.OpenTile, out var news) ? news.Name ?? "" : "";
				}
				else {
					TrapUI.UpdateNaming(player, TrapUI.OpenTile);
				}
			}
			Vector2 center = TrapSystem.TileCenter(TrapUI.OpenTile);
			if (Vector2.Distance(player.Center, center) > 800f)
				TrapUI.Close();
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
				TrapUI.Close();
		}

		private static bool prevMouseRight;

		// Tall-art interaction for the sentry: its art (~50px wide, ~60px tall)
		// overhangs the 1x1 logic tile, so hovering/clicking the visible
		// machine (not just the base tile) shows the cursor icon and opens the
		// panel on right-click. Mining still uses the 1x1 tile.
		private static void SentryTallInteract()
		{
			if (Main.dedServ || Main.gameMenu)
				return;
			bool clicked = Main.mouseRight && !prevMouseRight;
			prevMouseRight = Main.mouseRight;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
				return;
			if (player.mouseInterface)
				return; // some UI already owns the mouse
			if (TrapUI.Open)
				return; // panel open: vanilla tile click still works
			Vector2 mw = Main.MouseWorld;
			Point mp = mw.ToTileCoordinates();
			for (int dx = -2; dx <= 2; dx++) {
				for (int dy = -4; dy <= 1; dy++) {
					Point t = new Point(mp.X + dx, mp.Y + dy);
					if (!TrapSystem.Traps.TryGetValue(t, out var st) || st.Kind != TrapKind.Sentry)
						continue;
					if (!TrapSystem.IsAnyTrapTile(t))
						continue;
					Vector2 foot = new Vector2(t.X * 16f + 8f, t.Y * 16f + 16f);
					if (System.Math.Abs(mw.X - foot.X) > 26f || mw.Y < foot.Y - 58f || mw.Y > foot.Y + 8f)
						continue;
					// Within extended tile reach (vanilla caps interact range;
					// 20 tiles stays legitimate for a machine panel).
					if (Vector2.DistanceSquared(player.Center, foot) > 320f * 320f)
						continue;
					player.cursorItemIconEnabled = true;
					player.cursorItemIconID = ModContent.ItemType<CounterweightSentryItem>();
					player.noThrow = 2;
					if (clicked)
						TrapUI.OpenFor(TrapKind.Sentry, t.X, t.Y);
					return; // nearest art claims the hover
				}
			}
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			layers.Insert(idx < 0 ? layers.Count : idx,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Trap Panel",
					() => { DrawPanel(Main.spriteBatch); return true; },
					InterfaceScaleType.UI));
		}

		private static void DrawPanel(SpriteBatch sb)
		{
			if (!TrapUI.Open)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;
			if (!TrapSystem.Traps.TryGetValue(TrapUI.OpenTile, out var s)) {
				TrapUI.Close();
				return;
			}
			int pw = System.Math.Min(600, Main.screenWidth - 16);
			int ph = System.Math.Min(700, Main.screenHeight - 8);
			int x = (Main.screenWidth - pw) / 2;
			int y = (Main.screenHeight - ph) / 2;
			PanelRect = new Rectangle(x, y, pw, ph);
			TrapUI.ClipTopY = -99999f;
			TrapUI.ClipBotY = 99999f;
			if (PanelRect.Contains(Main.MouseScreen.ToPoint()))
				player.mouseInterface = true;
			YoyoPanel.DrawPanelBackground(sb, PanelRect);
			// Per-block accent strip (visual organization at a glance).
			Color accent = s.Kind switch {
				TrapKind.Sentry => new Color(220, 190, 90),
				TrapKind.Link => new Color(140, 180, 255),
				TrapKind.Tripwire => new Color(200, 120, 200),
				_ => new Color(180, 160, 220),
			};
			Content.Placeables.PlaceableUI.HeaderStrip(sb, x, y, pw, accent);
			string title = s.Kind switch {
				TrapKind.WildSnare => "Wildstring Snare (Offensive)",
				TrapKind.Bearing => "Bearing Trap (Offensive)",
				TrapKind.Sentry => "Counterweight Sentry (Defensive)",
				TrapKind.Link => "String Link (Wired Barrier)",
				_ => "Yoyo Tripwire (Defensive)",
			};
			if (!string.IsNullOrEmpty(s.Name))
				title += " — " + s.Name;
			Terraria.Utils.DrawBorderString(sb, title, new Vector2(x + 14, y + 10), Color.Gold, 1f);
			Rectangle xBtn = new Rectangle(x + pw - 42, y + 8, 32, 28);
			if (YoyoPanel.Button(sb, xBtn, "X", "Close", false, false, 0.9f))
				TrapUI.Close();
			float cy = y + 34;
			cy += TrapUI.HWrap(sb, Describe(s, TrapUI.OpenTile), new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			// Short screens: drop the guide paragraph (tips stay on buttons).
			if (ph >= 640)
				cy += TrapUI.HWrap(sb, FieldGuide(s.Kind), new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += 6;
			TrapUI.HFill(sb, new Rectangle(x + 14, (int)cy, pw - 28, 1), new Color(90, 98, 140, 180));
			cy += 6;
			// Common status line (defensive blocks use Aegis Mesh, not Local).
			string mesh = TrapSystem.AegisCovered(TrapUI.OpenTile) ? $"Aegis T{TrapSystem.AegisTier(TrapUI.OpenTile)}{(TrapSystem.AegisFar(TrapUI.OpenTile) ? " Farx3" : "")}{(TrapSystem.AegisFueled(TrapUI.OpenTile) ? " Fuelx2" : "")}" : "No Aegis";
			string syn = TrapSystem.HasSynergy(TrapUI.OpenTile, s.Kind) ? " +Synergy" : "";
			string state;
			Color stCol;
			if (!s.Enabled) { state = "DISABLED"; stCol = Color.Gray; }
			else if (s.Frayed > 0) { state = $"FRAYED {s.RecoveryPct()}% (rewire at 100%)"; stCol = new Color(255, 90, 90); }
			else if (s.Energy <= 0) { state = "NO POWER"; stCol = new Color(255, 90, 90); }
			else if (s.Cooldown > 0) { state = $"Reloading {s.Cooldown / 60f:0.0}s"; stCol = new Color(255, 180, 80); }
			else if (s.Kind == TrapKind.Link && !s.Linked) { state = "UNLINKED (use String Linker)"; stCol = Color.Gray; }
			else if (s.Kind == TrapKind.Link && !s.WiredActive) { state = "IDLE - needs wire signal"; stCol = new Color(255, 220, 130); }
			else if (s.WiredActive) { state = "ACTIVE (wired)"; stCol = Color.Gold; }
			else { state = "Ready"; stCol = new Color(130, 255, 130); }
			cy += TrapUI.HWrap(sb, $"Lv{s.Level}/{s.MaxLevel}  Uses:{s.Uses}  Power:{s.Energy}/{s.MaxEnergy}  {mesh}{syn}", new Vector2(x + 14, cy), Color.White, 0.8f, pw - 28);
			string holdNote = "";
			if (s.Kind == TrapKind.Link && s.Linked) {
				bool meHold = s.Behavior == 1 || s.Behavior == 2 || s.Behavior == 4;
				bool partnerHold = false;
				if (TrapSystem.Traps.TryGetValue(s.LinkPoint, out var phs))
				partnerHold = phs.Behavior == 1 || phs.Behavior == 2 || phs.Behavior == 4;
				if (!meHold && !partnerHold)
				holdNote = " - Damage-only: nothing is held (Both/Restraint holds)";
			}
			TrapUI.HFill(sb, new Rectangle(x + 14, (int)cy + 3, 8, 12), stCol);
			cy += TrapUI.HWrap(sb, "State: " + state + holdNote, new Vector2(x + 28, cy), stCol, 0.8f, pw - 42);
			Rectangle pbar = new Rectangle(x + 14, (int)cy, pw - 28, 8);
			TrapUI.HFill(sb, pbar, new Color(40, 40, 50, 220));
			float pfrac = s.MaxEnergy > 0 ? s.Energy / (float)s.MaxEnergy : 0f;
			Color pcol = pfrac > 0.5f ? new Color(90, 200, 120) : pfrac > 0.25f ? new Color(230, 200, 90) : new Color(230, 90, 90);
			TrapUI.HFill(sb, new Rectangle(pbar.X, pbar.Y, (int)(pbar.Width * pfrac), pbar.Height), pcol);
			cy += 12;
			cy += 4;
			string[] tabs = s.Kind switch { TrapKind.Link => new[] { "Barrier", "Weave", "Tech", "Lab" }, TrapKind.Tripwire => new[] { "Trigger", "Attack", "Tech", "Lab" }, _ => new[] { "Tune", "Tech", "Lab" } };
			TrapUI.Tab = System.Math.Clamp(TrapUI.Tab, 0, tabs.Length - 1);
			// Tab row geometry is recorded here but PAINTED AFTER the scrolled
			// content (see below): fixed chrome always wins, so scrolled rows
			// can never visibly slide over the tabs no matter what.
			int tabW = pw - 28 - 40;
			float tabY = cy;
			Rectangle qb = new Rectangle(x + 14 + tabW + 8, (int)cy, 32, 28);
			cy += 34;
			float contentTop = cy;
			float bottomEdge = y + ph - 10;
			float footerH = 30f;
			TrapUI.ClipTopY = contentTop;
			TrapUI.ClipBotY = bottomEdge - footerH;
			TrapUI.ClipView = new Rectangle(x + 2, (int)contentTop, pw - 4, (int)System.Math.Max(0f, bottomEdge - footerH - contentTop));
			cy -= TrapUI.ScrollY;
			if (s.Kind == TrapKind.Sentry) { if (TrapUI.Tab == 2) cy = DrawExperimentTab(sb, player, x, pw, cy, s); else if (TrapUI.Tab == 1) cy = DrawSentryTech(sb, player, x, pw, cy, s); else cy = DrawSentry(sb, player, x, pw, cy, s); }
			else if (s.Kind == TrapKind.Link) { if (TrapUI.Tab == 3) cy = DrawExperimentTab(sb, player, x, pw, cy, s); else if (TrapUI.Tab == 2) cy = DrawLinkTech(sb, player, x, pw, cy, s); else if (TrapUI.Tab == 1) cy = DrawLinkWeave(sb, player, x, pw, cy, s); else cy = DrawLinkBarrier(sb, player, x, pw, cy, s); }
			else { if (TrapUI.Tab == 3) cy = DrawExperimentTab(sb, player, x, pw, cy, s); else if (TrapUI.Tab == 2) cy = DrawTripwireTech(sb, player, x, pw, cy, s); else if (TrapUI.Tab == 1) cy = DrawTripwireAttack(sb, player, x, pw, cy, s); else cy = DrawTripwireTrigger(sb, player, x, pw, cy, s); }
			bool isExpTab = (s.Kind == TrapKind.Sentry && TrapUI.Tab == 2) || (s.Kind == TrapKind.Link && TrapUI.Tab == 3) || (s.Kind == TrapKind.Tripwire && TrapUI.Tab == 3);
			if (!isExpTab) {
			TrapUI.CDivider(sb, x + 14, (int)cy, pw - 28, new Color(90, 98, 140, 180));
			cy += 6;
			cy = DrawUpgradeRow(sb, player, x, pw, cy, s, ph);
			cy += 4;
			}
			if (!string.IsNullOrEmpty(TrapUI.Message) && cy < y + ph - 30)
				cy += TrapUI.CWrap(sb, TrapUI.Message, new Vector2(x + 14, cy), Color.Yellow, 0.8f, pw - 28);
			// Fixed tab row paints last (over any scrolled bleed-through).
			// One-frame tab-switch lag is imperceptible; clicks register now.
			int newTab = Content.Placeables.PlaceableUI.TabRow(sb, x + 14, (int)tabY, tabW, tabs, TrapUI.Tab);
			if (newTab != TrapUI.Tab) { TrapUI.Tab = newTab; TrapUI.ScrollY = 0; TrapUI.ScrollMax = 0; }
			else TrapUI.Tab = newTab;
			if (YoyoPanel.Button(sb, qb, "?", TrapSystem.AegisUnlocksFor(s.Kind), false, false, 0.8f))
				TrapUI.SetMessage(TrapSystem.AegisUnlocksFor(s.Kind));
			// Scroll bookkeeping: unscrolled content height vs visible window.
			// bottomEdge declared above (footer reserved before content).
			TrapUI.ScrollMax = System.Math.Max(0f, (cy + TrapUI.ScrollY) - TrapUI.ClipBotY);
			TrapUI.ScrollY = System.Math.Clamp(TrapUI.ScrollY, 0f, TrapUI.ScrollMax);
			{
				// Fixed footer bar (opaque): step buttons live here so scrolled
				// content can never slide under them. Frame line drawn on top.
				Rectangle foot = new Rectangle(x, (int)TrapUI.ClipBotY + 2, pw, (int)(bottomEdge - TrapUI.ClipBotY - 2));
				TrapUI.HFill(sb, foot, new Color(20, 24, 40, 235));
				TrapUI.HFill(sb, new Rectangle(x + 6, (int)TrapUI.ClipBotY + 2, pw - 12, 1), new Color(150, 128, 84));
				if (TrapUI.ScrollMax > 0f) {
				Rectangle upB = new Rectangle(x + 14, (int)TrapUI.ClipBotY + 6, 90, 20);
				Rectangle dnB = new Rectangle(x + 110, (int)TrapUI.ClipBotY + 6, 90, 20);
				if (YoyoPanel.Button(sb, upB, "^", "Scroll up", false, TrapUI.ScrollY <= 0f, 0.8f))
					TrapUI.ScrollY = System.Math.Max(0f, TrapUI.ScrollY - 90f);
				if (YoyoPanel.Button(sb, dnB, "v", "Scroll down", false, TrapUI.ScrollY >= TrapUI.ScrollMax, 0.8f))
					TrapUI.ScrollY = System.Math.Min(TrapUI.ScrollMax, TrapUI.ScrollY + 90f);
				int pct = TrapUI.ScrollMax <= 0f ? 100 : (int)(TrapUI.ScrollY / TrapUI.ScrollMax * 100f);
				Terraria.Utils.DrawBorderString(sb, pct + "%", new Vector2(x + 208, TrapUI.ClipBotY + 9), Color.Gray, 0.7f);
				// Tech-tree-style rail: bordered track with a gold-capped thumb.
				float trackTop = contentTop - 4f;
				float trackH = System.Math.Max(40f, TrapUI.ClipBotY - trackTop);
				Rectangle rail = new Rectangle(x + pw - 12, (int)trackTop, 6, (int)trackH);
				TrapUI.HFill(sb, rail, new Color(30, 36, 58, 220));
				TrapUI.HFill(sb, new Rectangle(rail.X, rail.Y, rail.Width, 1), new Color(90, 98, 140));
				TrapUI.HFill(sb, new Rectangle(rail.X, rail.Bottom - 1, rail.Width, 1), new Color(90, 98, 140));
				TrapUI.HFill(sb, new Rectangle(rail.X, rail.Y, 1, rail.Height), new Color(90, 98, 140));
				TrapUI.HFill(sb, new Rectangle(rail.Right - 1, rail.Y, 1, rail.Height), new Color(90, 98, 140));
				float thumbH = System.Math.Max(22f, trackH * trackH / (trackH + TrapUI.ScrollMax));
				float thumbY = trackTop + (TrapUI.ScrollMax <= 0f ? 0f : TrapUI.ScrollY / TrapUI.ScrollMax * (trackH - thumbH));
				Rectangle thumb = new Rectangle(x + pw - 11, (int)thumbY, 4, (int)thumbH);
				TrapUI.HFill(sb, thumb, new Color(70, 84, 130));
				TrapUI.HFill(sb, new Rectangle(thumb.X, thumb.Y, thumb.Width, 1), Color.Gold);
				TrapUI.HFill(sb, new Rectangle(thumb.X, thumb.Bottom - 1, thumb.Width, 1), Color.Gold);
				}
			}
		}

		private static string Describe(TrapState s, Point tile)
		{
			return s.Kind switch {
				TrapKind.WildSnare => "Deploys dangerous Wild Strings at players. String pool grows with progression. Needs power; meshed traps hit harder and reload faster.",
				TrapKind.Bearing => "Fires Loose Bearings at players. Bearings are consumed on impact and never return. Burst capacity grows with level.",
				TrapKind.Sentry => "Launches a counterweight at hostile NPCs; it strikes outward and/or on return, then flies home. Server-authoritative, 1 active attack.",
				TrapKind.Link => "Pair two endpoints with the String Linker, then toggle with vanilla wiring. Inactive shows nodes only; active deploys the dangerous rope.",
				_ => "Tripwire detects chosen entity types, then throws an attached yoyo at the nearest trigger. It swings out to Max Length and returns. Triggers != damage rules (see tabs).",
		};
		}

		private static string FieldGuide(TrapKind kind)
		{
			return kind switch {
				TrapKind.WildSnare => "Guide: place at chokepoints with line-of-sight. Higher levels widen the string pool; mesh it to cut reloads.",
				TrapKind.Bearing => "Guide: needs line-of-sight and power. Bursts grow with level; keep 4+ power banked per salvo.",
				TrapKind.Sentry => "Guide: needs hostile NPCs in range with line-of-sight. Bunker trades reach for punch; Aegis wants Aegis Mesh + power.",
				TrapKind.Link => "Guide: restraint holds without damage; watch stress %. Snapped ropes regen (15-25s by string/weave) then need rewiring. Kevlar holds longest.",
				_ => "Guide: triggers start attacks, the yoyo only hurts hostiles. Twin Throw costs 1.5x power; Snare Tip slows what it tags.",
			};
		}

		public static void PushConfig(Point tile, TrapState s)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TrapNet.SendConfig(tile, s);
			else
				TrapActions.ApplyConfig(tile, s);
		}

		private static float Row(SpriteBatch sb, int x, int pw, float cy, string label, string value)
		{
			if (pw >= 460) {
				TrapUI.CText(sb, label, new Vector2(x + 14, cy), Color.White, 0.8f);
				TrapUI.CText(sb, value, new Vector2(x + 220, cy), Color.LightGreen, 0.8f);
				return cy + 24;
			}
			// Narrow panels: stack the value under the label (never overlap).
			TrapUI.CText(sb, label, new Vector2(x + 14, cy), Color.White, 0.8f);
			return cy + 20 + TrapUI.CWrap(sb, value, new Vector2(x + 14, cy + 20), Color.LightGreen, 0.8f, pw - 28);
		}

		private static float Stepper(SpriteBatch sb, Player player, Point tile, TrapState s, int x, int pw, float cy, string label, int current, int min, int max, int step, System.Action<int> set)
		{

			TrapUI.CText(sb, $"{label}: {current}", new Vector2(x + 14, cy), Color.White, 0.8f);
			Rectangle minus = new Rectangle(x + pw - 120, (int)cy - 2, 50, 22);
			Rectangle plus = new Rectangle(x + pw - 64, (int)cy - 2, 50, 22);
			if (TrapUI.CButton(sb, minus, "-", "Decrease", false, current <= min, 0.85f)) {
				set(System.Math.Max(min, current - step));
				PushConfig(tile, s);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			if (TrapUI.CButton(sb, plus, "+", "Increase", false, current >= max, 0.85f)) {
				set(System.Math.Min(max, current + step));
				PushConfig(tile, s);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			return cy + 26;
		}

		private static float Toggle(SpriteBatch sb, Player player, Point tile, TrapState s, int x, float cy, string label, bool value, System.Action<bool> set, string tip = "")
		{
			return ToggleAt(sb, player, tile, s, x + 14, cy, 220, label, value, set, tip);
		}

		// Two-column capable toggle: draws one checkbox at (bx, cy) with given width.
		// Returns the y AFTER the row (cy + 28) so callers can place pairs side by side.
		// lockedAt = required trap level (0 = free). Locked toggles refuse with why.
		private static float ToggleAt(SpriteBatch sb, Player player, Point tile, TrapState s, int bx, float cy, int w, string label, bool value, System.Action<bool> set, string tip = "", int lockedAt = 0)
		{
			bool locked = s.Level < lockedAt;

			Rectangle b = new Rectangle(bx, (int)cy, w, 24);
			if (TrapUI.CButton(sb, b, $"{label}: {(value ? "ON" : "OFF")}{(locked ? $" (Lv{lockedAt})" : "")}", locked ? $"Needs trap Lv{lockedAt} ({TrapActions.LevelPerkText(s.Kind, lockedAt)})" : tip, value, locked, 0.7f)) {
				if (locked) {
					TrapUI.SetMessage($"{label} needs trap Lv{lockedAt}.");
				}
				else {
					set(!value);
					PushConfig(tile, s);
				}
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			return cy + 28;
		}

		// Schedule cycler (Manual/Day/Night/Pulse). Link pairs override manual
		// wiring; sentries/tripwires hold fire off-phase (upkeep still ticks).
		private static float DrawSchedRow(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s, Point tile)
		{
			string[] scheds = { "Manual", "Day", "Night", "Pulse" };
			string[] schedTips = {
				"Manual control (wiring for links).",
				"Active by day, idle at night.",
				"Active by night, idle at day.",
				"Auto-toggles every 30s.",
			};
			int sm = System.Math.Clamp(s.Sched, 0, 3);
			TrapUI.CText(sb, $"Schedule: {scheds[sm]}", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			float scw = (pw - 28 - 18) / 4f;
			for (int i = 0; i < 4; i++) {
				Rectangle b = new Rectangle(x + 14 + (int)(i * (scw + 6)), (int)cy, (int)scw, 24);
				if (TrapUI.CButton(sb, b, scheds[i], schedTips[i], sm == i, false, 0.7f)) { s.Sched = i; PushConfig(tile, s); }
			}
			cy += 28;
			if (sm == 3) {
				int pp = s.PulsePeriod == 10 ? 10 : s.PulsePeriod == 60 ? 60 : 30;
				Rectangle pb = new Rectangle(x + 14, (int)cy, System.Math.Min(240, pw - 28), 24);
				if (TrapUI.CButton(sb, pb, $"Pulse every: {pp}s", "Cycle 10s / 30s / 60s.", false, false, 0.75f)) {
					s.PulsePeriod = pp == 10 ? 30 : pp == 30 ? 60 : 10;
					PushConfig(tile, s);
				}
				cy += 28;
			}
			return cy;
		}
		// OFF categories are fully ignored (no damage/hold/stress/targeting).
		private static float DrawEntityToggles(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s, bool includePlayers)
		{
			Point tile = TrapUI.OpenTile;
			TrapUI.CText(sb, "Who counts:", new Vector2(x + 14, cy), Color.Gold, 0.8f);
			cy += 22;
			int colW = (pw - 28 - 8) / 2;
			if (includePlayers) {
				float row = cy;
				cy = ToggleAt(sb, player, tile, s, x + 14, row, colW, "Players", s.HitPlayers, v => s.HitPlayers = v, "Human players");
				ToggleAt(sb, player, tile, s, x + 14 + colW + 8, row, colW, "Hostile NPCs", s.HitHostile, v => s.HitHostile = v, "Enemies");
			}
			else {
				float row = cy;
				cy = ToggleAt(sb, player, tile, s, x + 14, row, colW, "Hostile NPCs", s.HitHostile, v => s.HitHostile = v, "Enemies");
				ToggleAt(sb, player, tile, s, x + 14 + colW + 8, row, colW, "NPCs (ordinary)", s.HitNPCs, v => s.HitNPCs = v, "Critters and neutrals");
			}
			{
				float row = cy;
				if (includePlayers) {
					cy = ToggleAt(sb, player, tile, s, x + 14, row, colW, "NPCs (ordinary)", s.HitNPCs, v => s.HitNPCs = v, "Critters and neutrals");
					ToggleAt(sb, player, tile, s, x + 14 + colW + 8, row, colW, "Town NPCs", s.HitTown, v => s.HitTown = v, "townNPC=true");
				}
				else {
					cy = ToggleAt(sb, player, tile, s, x + 14, row, colW, "Town NPCs", s.HitTown, v => s.HitTown = v, "townNPC=true");
					ToggleAt(sb, player, tile, s, x + 14 + colW + 8, row, colW, "Friendly NPCs", s.HitFriendly, v => s.HitFriendly = v, "friendly && !townNPC");
				}
			}
			{
				float row = cy;
				if (includePlayers) {
					cy = ToggleAt(sb, player, tile, s, x + 14, row, colW, "Friendly NPCs", s.HitFriendly, v => s.HitFriendly = v, "friendly && !townNPC");
					ToggleAt(sb, player, tile, s, x + 14 + colW + 8, row, colW, "ALL (override)", s.HitAll, v => s.HitAll = v, "Anything trips/counts");
				}
				else {
					cy = ToggleAt(sb, player, tile, s, x + 14, row, colW, "ALL (override)", s.HitAll, v => s.HitAll = v, "Anything counts");
					cy += TrapUI.CWrap(sb, "Counterweights can't harm players.", new Vector2(x + 14 + colW + 8, row), Color.Gray, 0.7f, colW);
				}
			}
			return cy;
		}

		// ---- Per-trap panels ----

		// Aegis-gated unique-option toggle. Defensive blocks need a live
		// Aegis Mesh over the trap (Defensive-Mesh requirement, not Local).
		// All mesh options also need trap Lv2 (each level unlocks something).
		private static float MeshOption(SpriteBatch sb, Player player, Point tile, TrapState s, int x, int pw, float cy, string label, bool value, System.Action<bool> set, string onTip)
		{
			bool meshed = TrapSystem.AegisCovered(tile);
			bool leveled = s.Level >= 2;
			int w = System.Math.Min(360, pw - 28);

			Rectangle b = new Rectangle(x + 14, (int)cy, w, 24);
			string why = !leveled ? "Needs trap Lv2" : "Needs Aegis Mesh over this trap (AEGIS branch)";
			if (TrapUI.CButton(sb, b, label + ": " + (value ? "ON" : "OFF"), leveled && meshed ? onTip : why, value, !leveled || !meshed, 0.75f)) {
				if (!leveled || !meshed)
					TrapUI.SetMessage(label + " " + why + ".");
				else {
					set(!value);
					PushConfig(tile, s);
				}
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			return cy + 28;
		}

		private static float DrawSentry(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s)
		{
			Point tile = TrapUI.OpenTile;
			// Counterweight slot: 11 varieties (Prev/Next cycler). Iron is the
			// Ancient match their real item-family art; the rest reuse
			// verified in-mod counterweight sprites with unique tints.
			var cdef = TrapSystem.CounterOf(s.CounterType);
			int unlocked = 0;
			for (int ci = 0; ci < TrapSystem.Counters.Length; ci++) {
				if (TrapSystem.CounterUnlocked(ci, tile, s, player, out _))
					unlocked++;
			}
			string debuffTxt = cdef.Debuff >= 0 ? $" • {Lang.GetBuffName(cdef.Debuff)}" : "";
			TrapUI.CText(sb, $"Weight: {cdef.Name} (+{cdef.Dmg} dmg, {cdef.Speed:0.#} spd{debuffTxt})", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			Rectangle prev = new Rectangle(x + 14, (int)cy, 120, 24);
			Rectangle next = new Rectangle(x + 140, (int)cy, 120, 24);
			if (x + 268 + 130 <= x + pw - 14)
			TrapUI.CText(sb, $"{unlocked}/{TrapSystem.Counters.Length} unlocked", new Vector2(x + 268, cy + 4), Color.Gray, 0.75f);
			else
			TrapUI.CText(sb, $"{unlocked}/{TrapSystem.Counters.Length}", new Vector2(x + 268, cy + 4), Color.Gray, 0.75f);
			if (TrapUI.CButton(sb, prev, "< Prev", "Previous unlocked counterweight", false, false, 0.75f)) {
				// Skip locked varieties (they show their reason when landed on
				// via Next; Prev jumps straight to the last unlocked one).
				int ni = s.CounterType;
				for (int k = 0; k < TrapSystem.Counters.Length; k++) {
					ni = (ni + TrapSystem.Counters.Length - 1) % TrapSystem.Counters.Length;
					if (TrapSystem.CounterUnlocked(ni, tile, s, player, out _))
						break;
				}
				if (ni != s.CounterType) { s.CounterType = ni; PushConfig(tile, s); }
			}
			if (TrapUI.CButton(sb, next, "Next >", cdef.Tip, false, false, 0.75f)) {
				int ni = (s.CounterType + 1) % TrapSystem.Counters.Length;
				if (TrapSystem.CounterUnlocked(ni, tile, s, player, out string why)) { s.CounterType = ni; PushConfig(tile, s); }
				else TrapUI.SetMessage(TrapSystem.CounterOf(ni).Name + ": " + why);
			}
			cy += 28;
			cy += TrapUI.CWrap(sb, "Get: " + cdef.Tip, new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy = Stepper(sb, player, tile, s, x, pw, cy, "Detect range (tiles)", s.Range, 4, 24, 1, v => s.Range = v);
			// Tether color (visual only): White default + link palette.
			TrapUI.CText(sb, $"Tether: {TrapSystem.SentryStringName(System.Math.Clamp(s.SentryString, 0, 7))}", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			Rectangle tp = new Rectangle(x + 14, (int)cy, 120, 24);
			Rectangle tn = new Rectangle(x + 140, (int)cy, 120, 24);
			if (TrapUI.CButton(sb, tp, "< Prev", "Previous tether color", false, false, 0.75f)) {
				s.SentryString = (s.SentryString + 7) % 8;
				PushConfig(tile, s);
			}
			if (TrapUI.CButton(sb, tn, "Next >", "Next tether color", false, false, 0.75f)) {
				s.SentryString = (s.SentryString + 1) % 8;
				PushConfig(tile, s);
			}
			cy += 28;
			// Priority (responsive thirds, incl. Aegis focus fire)
			float prw = (pw - 28 - 12) / 3f;
			Rectangle p0 = new Rectangle(x + 14, (int)cy, (int)prw, 24);
			Rectangle p1 = new Rectangle(x + 14 + (int)(prw + 6), (int)cy, (int)prw, 24);
			Rectangle p2 = new Rectangle(x + 14 + (int)((prw + 6) * 2), (int)cy, (int)prw, 24);
			if (TrapUI.CButton(sb, p0, "Nearest", "Target closest enemy", s.Priority == 0, false, 0.7f)) { s.Priority = 0; PushConfig(tile, s); }
			if (TrapUI.CButton(sb, p1, "Strongest", "Target highest max HP", s.Priority == 1, false, 0.7f)) { s.Priority = 1; PushConfig(tile, s); }
			if (TrapUI.CButton(sb, p2, "Snared", "Focus NPCs held by a String Link (needs Aegis cover, else Nearest)", s.Priority == 2, false, 0.7f)) { s.Priority = 2; PushConfig(tile, s); }
			cy += 28;
			// Behavior: outward / return / both (responsive thirds).
			// Outward = damages only while flying OUT to the target (safe,
			// harmless return). Return = damages only on the way BACK home
			// (throw flies past, kills coming back). Both = whole flight.
			TrapUI.CText(sb, "Attack behavior:", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			string[] beh = { "Outward", "Return", "Both" };
			string[] behTips = {
				"Damages only flying OUT (+25% damage). Safe return, no hits coming home.",
				"Damages only flying BACK (+25% damage). Throw passes through, kills on return.",
				"Damages the whole flight, out and back (no bonus).",
			};
			float bw = (pw - 28 - 12) / 3f;
			for (int i = 0; i < 3; i++) {
				Rectangle b = new Rectangle(x + 14 + (int)(i * (bw + 6)), (int)cy, (int)bw, 24);
				if (TrapUI.CButton(sb, b, beh[i], behTips[i], s.Behavior == i, false, 0.7f)) { s.Behavior = i; PushConfig(tile, s); }
			}
			cy += 28;
			Rectangle oc = new Rectangle(x + 14, (int)cy, System.Math.Min(300, pw - 28), 24);
			bool ocLocked = s.Level < 1;
			if (TrapUI.CButton(sb, oc, $"Overcharge {(s.Overcharge ? "ON" : "OFF")}{(ocLocked ? " (Lv1)" : "")}", ocLocked ? "Needs trap Lv1" : "Risk/reward: +50% dmg, 15% backfire unless License III nearby", s.Overcharge, ocLocked, 0.75f)) {
				if (!ocLocked) { s.Overcharge = !s.Overcharge; PushConfig(tile, s); }
				else TrapUI.SetMessage("Overcharge needs trap Lv1.");
			}
			cy += 28;
			cy = MeshOption(sb, player, tile, s, x, pw, cy, "Aegis Broadcast", s.SentryAegis, v => s.SentryAegis = v, "Endurance aura for nearby players while powered (drains power, Aegis only)");
			cy = ToggleAt(sb, player, tile, s, x + 14, cy, pw - 28, "Bunker Stance", s.SentryBunker, v => s.SentryBunker = v, "+50% damage, -35% range", 1);
			cy = ToggleAt(sb, player, tile, s, x + 14, cy, pw - 28, "Ichor Tracers", s.TracerFire, v => s.TracerFire = v, "Counterweight hits corrode armor (Ichor 5s)", 2);
			return cy;
		}

		private static float DrawSentryTech(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s)
		{
			Point tile = TrapUI.OpenTile;
			cy = DrawEntityToggles(sb, player, x, pw, cy, s, true);
			cy += TrapUI.CWrap(sb, "Players: hits everyone except the trap owner (PvP).", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy = TrackRow(sb, player, x, pw, cy, s, tile, TrapSystem.AegisCovered(tile), 2, $"Gyroscope (+{s.SentryGyro * 12}% return dmg)", s.SentryGyro, "Return leg hits harder per level");
			cy = TrackRow(sb, player, x, pw, cy, s, tile, TrapSystem.AegisCovered(tile), 5, $"Spotter Scope (+{s.SentryScope * 2} range)", s.SentryScope, "See farther per level");
			cy = MeshOption(sb, player, tile, s, x, pw, cy, "Auto-Overcharge", s.AutoOC, v => s.AutoOC = v, "While Aegis-fueled: fires overcharged at 2% backfire (+5 power/shot)");
			cy = DrawShareRow(sb, player, x, pw, cy, s, tile);
			cy = DrawSchedRow(sb, player, x, pw, cy, s, tile);
			return cy;
		}

		private static float DrawLinkBarrier(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s)
		{
			Point tile = TrapUI.OpenTile;
			string conn = s.Linked ? $"Linked to ({s.LinkX},{s.LinkY})" : "Unlinked (use String Linker item)";
			string wire = s.WiredActive ? "ACTIVE (dangerous)" : (s.Frayed > 0 ? $"Frayed {s.RecoveryPct()}%" : "Idle");
			cy = Row(sb, x, pw, cy, "Link", conn + " • " + wire);
			int maxStress = TrapSystem.EffMaxStress(tile, s);
			int pct = maxStress > 0 ? System.Math.Clamp(s.Stress * 100 / maxStress, 0, 100) : 0;
			cy = Row(sb, x, pw, cy, "Rope stress", $"{s.Stress}/{maxStress} ({pct}%)");
			TrapUI.CPips(sb, new Vector2(x + 14, cy), System.Math.Clamp(s.StressPct() / 10, 0, 10), 10);
			cy += 16;
			cy = DrawReinforceRow(sb, player, x, pw, cy, s, tile);
			if (s.Frayed > 0)
				cy = Row(sb, x, pw, cy, "Recovery", $"{s.RecoveryPct()}% ({s.Frayed / 60f:0.0}s, reswitch at 100%)");
			if (s.Linked) {
				Rectangle u = new Rectangle(x + 14, (int)cy, 160, 24);
				if (TrapUI.CButton(sb, u, "Unlink", "Safely disconnect the pair", false, false, 0.75f)) {
					if (Main.netMode == NetmodeID.MultiplayerClient)
						TrapNet.SendUnlink(tile);
					else
						TrapActions.ApplyUnlink(tile);
					TrapUI.SetMessage("Unlinked.");
				}
				cy += 28;
			}
			string[] names = { "Cotton (AC)", "Poly-Cotton (APC)", "Polyester (AP)", "Nylon (AN)", "Kevlar (AK)", "Silk (SL)", "Shadow (SH)", "Solar (SO)" };
			int maxVar = s.MaxLinkString();
			int shownVar = System.Math.Min(s.StringVariant, maxVar);
			int bendMax = TrapState.MaxBendFor(shownVar);
			TrapUI.CText(sb, $"String: {names[shownVar]}", new Vector2(x + 14, cy), Color.White, 0.8f);
			Rectangle prev = new Rectangle(x + 220, (int)cy - 2, 60, 22);
			Rectangle next = new Rectangle(x + 286, (int)cy - 2, 60, 22);
			if (TrapUI.CButton(sb, prev, "< Prev", "Select string material", false, false, 0.75f)) {
				s.StringVariant = s.StringVariant - 1 < 0 ? maxVar : s.StringVariant - 1;
				PushConfig(tile, s);
			}
			if (TrapUI.CButton(sb, next, "Next >", "Select string material", false, false, 0.75f)) {
				s.StringVariant = s.StringVariant + 1 > maxVar ? 0 : s.StringVariant + 1;
				PushConfig(tile, s);
			}
			cy += 28;
			cy = Stepper(sb, player, tile, s, x, pw, cy, "Contact damage", s.Damage, 5, 150, 5, v => s.Damage = v);
			cy = Stepper(sb, player, tile, s, x, pw, cy, "Restraint (Slow s)", s.Restraint, 0, 8, 1, v => s.Restraint = v);
			string[] beh = { "Damage", "Restraint", "Both", "Alarm", "Latch" };
			string[] behTips = {
				"Hurts what touches (pair union).",
				"Restrains without damage.",
				"Hurts and restrains.",
				"Sensor only: pings on crossing, no damage/hold/wiring.",
				"Ambush: first touch slams it shut (Both) until rewired.",
			};
			TrapUI.CText(sb, "Barrier behavior:", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			float bbw = (pw - 28 - 24) / 5f;
			for (int i = 0; i < 5; i++) {
				Rectangle b = new Rectangle(x + 14 + (int)(i * (bbw + 6)), (int)cy, (int)bbw, 24);
				if (TrapUI.CButton(sb, b, beh[i], behTips[i], s.Behavior == i, false, 0.6f)) { s.Behavior = i; PushConfig(tile, s); }
			}
			cy += 28;
			cy += TrapUI.CWrap(sb, $"Bend cap for {names[shownVar]}: {(bendMax == 2 ? "Curved" : "Slight")}. Over-bent +3/tk.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy = DrawStringStats(sb, player, x, pw, cy, s, tile);
			return cy;
		}

		// Dedicated max-stress upgrade (Reinforced Rope): lives on the
		// Barrier tab so it is always visible, needs no Aegis cover and
		// no field uses — just string + coins. Stacks with Tension Braid.
		private static float DrawReinforceRow(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s, Point tile)
		{
			int cur = System.Math.Clamp(s.LinkStress, 0, TrapState.MaxLinkStress);
			TrapUI.CText(sb, $"Reinforced Rope (+{cur * TrapState.LinkStressPerLevel} max) Lv{cur}/{TrapState.MaxLinkStress}", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			string cost = TrapActions.ReinforceCostText(cur + 1);
			Rectangle up = new Rectangle(x + 14, (int)cy, 200, 24);
			bool can = cur < TrapState.MaxLinkStress;
			if (TrapUI.CButton(sb, up, cur >= TrapState.MaxLinkStress ? "MAX" : $"+1 ({cost})", can ? "Thicker rope, higher snap point. Pair shares it. Stacks with Tension Braid." : "Fully reinforced.", false, !can, 0.75f)) {
				if (Main.netMode == NetmodeID.MultiplayerClient) {
					if (TrapActions.TryReinforce(player, tile))
						TrapNet.SendReinforce(tile);
					else TrapUI.SetMessage("Cannot reinforce: check silk/cobweb/coins.");
				}
				else if (!TrapActions.TryReinforce(player, tile))
					TrapUI.SetMessage("Cannot reinforce: check silk/cobweb/coins.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			cy += 28;
			return cy;
		}

		// Visible stat card for the current string: every string has different
		// numbers (max stress, bend, strain, regen, yield) plus live weave math.
		private static float DrawStringStats(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s, Point tile)
		{
			string[] sn = { "Cotton", "Poly-Cotton", "Polyester", "Nylon", "Kevlar", "Silk", "Shadow", "Solar" };
			int[] sMax = { 105, 128, 150, 180, 225, 90, 200, 165 };
			int[] sRate = { 8, 7, 6, 5, 4, 9, 3, 5 };
			int[] sGive = { 45, 35, 28, 22, 15, 50, 12, 25 };
			float[] sRegen = { 25f, 22f, 20f, 18f, 15f, 26.7f, 11.7f, 15.8f };
			int sv = System.Math.Clamp(s.StringVariant, 0, 7);
			int effDmg = System.Math.Max(2, TrapSystem.EffectiveDamage(tile, s) / 2);
			cy += TrapUI.CWrap(sb, $"{sn[sv]} stats — Max {sMax[sv]}+{s.TensionWeave * 38}+{System.Math.Clamp(s.LinkStress, 0, TrapState.MaxLinkStress) * TrapState.LinkStressPerLevel} stress • Bends to {(TrapState.MaxBendFor(sv) == 2 ? "Curved" : "Slight")} • Strain {sRate[sv]}/tk • Regen {sRegen[sv]:0.#}s (-15%/wax) • Yield {sGive[sv]}% • Hits ~{effDmg}", new Vector2(x + 14, cy), Color.LightGreen, 0.75f, pw - 28);
			cy += TrapUI.CWrap(sb, "Multi-victim amp: 1st victim counts full weight (zombie 0.5x … boss 6x), extras half. Heavies +bulk.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += TrapUI.CWrap(sb, "Medium matters: submerged rope floats and drags (lava is buoyant syrup, honey barely lets go); snag points buzz hotter where waves reflect.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += TrapUI.CWrap(sb, $"Taut rope strains +15%, slack −20%. Mid-span pushes +40%. Sudden rushes jerk it; rushes past {TrapSystem.RipToughness(sv):0} impulse rip straight through (gouges, never snaps). Endpoint grip, stick-slip, stiff pushback shape the grind; hot rope relaxes fast, dregs linger. Small fast bodies needle in on pressure; snaps crack a shockwave ring. Standing loads settle at equilibrium instead of grinding forever - only crowds past ultimate, action, and overload creep to a snap; halves dangle 5s; the crack itself stings at 30%.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += TrapUI.CWrap(sb, "Water floats and drags the rope (lava barely lets go); snag points buzz hotter where waves reflect; braking against the rope loads it like a seatbelt; cut ends keep their string color with white frayed tips.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			// Alarm trip log (last 5, this pair only): shown on alarm pairs.
			if (s.Behavior == 3 && s.Linked) {
				cy = ToggleAt(sb, player, tile, s, x + 14, cy, pw - 28, "Alarm idle-only", s.AlarmIdleOnly, v => s.AlarmIdleOnly = v, "ON: silent while armed. OFF: ping every crossing.");
				var log = TrapSystem.AlarmLogFor(tile, s.LinkPoint);
				if (log.Count > 0) {
					TrapUI.CText(sb, "Trip log:", new Vector2(x + 14, cy), Color.Gold, 0.8f);
					cy += 22;
					foreach (var entry in log)
						cy += TrapUI.CWrap(sb, "> " + entry, new Vector2(x + 14, cy), Color.LightGray, 0.7f, pw - 28);
				}
				else {
					cy += TrapUI.CWrap(sb, "Trip log empty — crossings will list here.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
				}
			}
			return cy;
		}

		private static float DrawLinkTech(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s)
		{
			Point tile = TrapUI.OpenTile;
			string[] bends = { "Straight", "Slight", "Curved" };
			int bendMax = TrapState.MaxBendFor(System.Math.Clamp(s.StringVariant, 0, 7));
			TrapUI.CText(sb, $"Rope slack (max {(bendMax == 2 ? "Curved" : "Slight")} for this string):", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			float bbw = (pw - 28 - 12) / 3f;
			for (int i = 0; i < 3; i++) {
				bool locked = i > bendMax;
				Rectangle b = new Rectangle(x + 14 + (int)(i * (bbw + 6)), (int)cy, (int)bbw, 24);
				if (TrapUI.CButton(sb, b, bends[i], locked ? "Too curvy for this string (would over-strain)" : "Sag: tight/slight/curved. The barrier rope drapes with gravity and rests on tiles.", s.Bend == i, locked, 0.7f)) {
					if (!locked) { s.Bend = i; PushConfig(tile, s); }
					else TrapUI.SetMessage("That bend is too deep for this string.");
				}
			}
			cy += 28;
			cy = Stepper(sb, player, tile, s, x, pw, cy, "Link range (tiles)", s.Range, 6, 30, 1, v => s.Range = v);
			cy = MeshOption(sb, player, tile, s, x, pw, cy, "Ward Lattice", s.WardLattice, v => s.WardLattice = v, "Live barrier zaps minor hostile shots crossing it (4 power each, Aegis only)");
			cy = MeshOption(sb, player, tile, s, x, pw, cy, "Auto-Rewire", s.AutoRewire, v => s.AutoRewire = v, "Auto re-engage after full recovery (Aegis only). OFF = manual reswitch required.");
			cy = ToggleAt(sb, player, tile, s, x + 14, cy, pw - 28, "Long Span", s.LongSpan, v => s.LongSpan = v, "+50% pair range, -25% damage", 1);
			cy = DrawEntityToggles(sb, player, x, pw, cy, s, true);
			cy += TrapUI.CWrap(sb, "Filters gate hits, hold and strain together.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy = DrawSchedRow(sb, player, x, pw, cy, s, tile);
			cy = TrackRow(sb, player, x, pw, cy, s, tile, TrapSystem.AegisCovered(tile), 6, $"Silk Reserve (+{s.LinkReserve * 15} max power)", s.LinkReserve, "Bigger power cell per level");
			cy = DrawShareRow(sb, player, x, pw, cy, s, tile);
			return cy;
		}

		private static float TrackRow(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s, Point tile, bool aegis, int track, string title, int cur, string blurb)
		{

			TrapUI.CText(sb, $"{title} Lv{cur}/3", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			string cost = cur >= 3 ? "MAX" : cur == 0 ? "8 scraps + 2g + 5 uses" : cur == 1 ? "6 parts + 6g + 15 uses" : "4 adv.parts + 12g + 30 uses";
			Rectangle up = new Rectangle(x + 14, (int)cy, 200, 24);
			bool leveled = s.Level >= 1;
			bool can = cur < 3 && s.Uses >= (cur == 0 ? 5 : cur == 1 ? 15 : 30) && aegis && leveled;
			if (TrapUI.CButton(sb, up, cur >= 3 ? "MAX" : $"+1 ({cost})", can ? blurb : (!leveled ? "Needs trap Lv1" : !aegis ? "Needs Aegis cover" : "Needs uses/mats/coins"), false, !can, 0.75f)) {
				if (Main.netMode == NetmodeID.MultiplayerClient) {
					if (TrapActions.TryUpgradeTrack(player, tile, track))
						TrapNet.SendTrack(tile, track);
					else TrapUI.SetMessage($"Cannot upgrade {title}: check uses/mats/Aegis.");
				}
				else if (!TrapActions.TryUpgradeTrack(player, tile, track))
					TrapUI.SetMessage($"Cannot upgrade {title}: check uses/mats/Aegis.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			cy += 28;
			return cy;
		}

		// Mesh power sharing stance: Off / Donor / Needy (cycles on click).
		private static float DrawShareRow(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s, Point tile)
		{

			string[] modes = { "Off", "Donor", "Needy" };
			string[] tips = {
				"No power sharing.",
				"Gives energy to drained neighbors in this Aegis mesh (above half cell).",
				"Takes energy from donors in this Aegis mesh (below half cell).",
			};
			int m = System.Math.Clamp(s.ShareMode, 0, 2);
			Rectangle b = new Rectangle(x + 14, (int)cy, System.Math.Min(300, pw - 28), 24);
			if (TrapUI.CButton(sb, b, $"Mesh share: {modes[m]}", tips[m], m != 0, false, 0.75f)) {
				s.ShareMode = (m + 1) % 3;
				PushConfig(tile, s);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			cy += 28;
			return cy;
		}

		private static float DrawLinkWeave(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s)
		{
			Point tile = TrapUI.OpenTile;
			bool aegis = TrapSystem.AegisCovered(tile);
			cy += TrapUI.CWrap(sb, "Weave upgrades: Tension max stress, Wax faster recovery, Coil cheaper zaps, Wrap longer poison. Needs Aegis cover. Pair shares tracks.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			cy += 4;
			int effMax = TrapSystem.EffMaxStress(tile, s);
			int frayMax = s.FrayMax();
			int zap = System.Math.Max(1, 4 - System.Math.Clamp(s.LinkCoil, 0, 3));
			string phaseNote = TrapSystem.PhaseIntercepts > 0 ? $" Intercepts: {TrapSystem.PhaseIntercepts}" : "";
			cy += TrapUI.CWrap(sb, $"Now: max {effMax} stress, {frayMax / 60f:0.0}s regen, zap {zap} power, Recovery {s.RecoveryPct()}%, Stress {s.StressPct()}%.{phaseNote}", new Vector2(x + 14, cy), Color.White, 0.8f, pw - 28);
			cy += TrapUI.CWrap(sb, $"Grind ledger: {TrapSystem.PairWork(tile):0} work done, ceiling sagged {TrapSystem.PairCreep(tile) * 100f:0}% (heals when cool).", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += 4;
			cy = TrackRow(sb, player, x, pw, cy, s, tile, aegis, 0, $"Tension Braid (+{s.TensionWeave * 38} max)", s.TensionWeave, "Tighter weave, higher snap point");
			cy = TrackRow(sb, player, x, pw, cy, s, tile, aegis, 1, $"Wax Treatment (-{s.RegenWeave * 15}% regen)", s.RegenWeave, "Faster stress bleed + shorter fray");
			cy = TrackRow(sb, player, x, pw, cy, s, tile, aegis, 4, $"Grounding Coil (zap {zap} power)", s.LinkCoil, "Ward Lattice zaps cost less power");
			int barbSecs = 3 + System.Math.Max(0, s.LinkBarb - 1);
			cy = TrackRow(sb, player, x, pw, cy, s, tile, aegis, 8, $"Barbed Wrap (Poison {barbSecs}s)", s.LinkBarb, "Bites poison longer per level");
			cy += TrapUI.CWrap(sb, "Creative: Cotton wants Wax (snaps fast), Kevlar wants Tension (holds forever). Nylon hates deep curves — run it Straight.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			return cy;
		}
		private static float DrawTripwireTrigger(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s)
		{
			Point tile = TrapUI.OpenTile;
			TrapUI.CText(sb, "Trigger categories (which entities trip it):", new Vector2(x + 14, cy), Color.Gold, 0.8f);
			cy += 22;
			int colW = (pw - 28 - 8) / 2;
			float row = cy;
			cy = ToggleAt(sb, player, tile, s, x + 14, row, colW, "Players", s.HitPlayers, v => s.HitPlayers = v, "Human players");
			ToggleAt(sb, player, tile, s, x + 14 + colW + 8, row, colW, "NPCs (ordinary)", s.HitNPCs, v => s.HitNPCs = v, "Non-town, non-hostile, non-friendly (e.g. critters)");
			row = cy;
			cy = ToggleAt(sb, player, tile, s, x + 14, row, colW, "Town NPCs", s.HitTown, v => s.HitTown = v, "townNPC=true");
			ToggleAt(sb, player, tile, s, x + 14 + colW + 8, row, colW, "Hostile NPCs", s.HitHostile, v => s.HitHostile = v, "Enemies (!friendly, damage>0)");
			row = cy;
			cy = ToggleAt(sb, player, tile, s, x + 14, row, colW, "Friendly NPCs", s.HitFriendly, v => s.HitFriendly = v, "friendly && !townNPC");
			ToggleAt(sb, player, tile, s, x + 14 + colW + 8, row, colW, "ALL (override)", s.HitAll, v => s.HitAll = v, "Master override: any valid entity trips");
			cy += 2;
			cy += TrapUI.CWrap(sb, "Damage rules are separate: the yoyo damages hostile NPCs in reach; triggers only decide what starts the attack.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += 4;
			return cy;
		}

		private static float DrawTripwireAttack(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s)
		{
			Point tile = TrapUI.OpenTile;
			cy = Stepper(sb, player, tile, s, x, pw, cy, "Detect range (tiles)", s.Range, 2, 20, 1, v => s.Range = v);
			cy = Stepper(sb, player, tile, s, x, pw, cy, "Max string (px)", s.MaxLen, 80, 480, 20, v => s.MaxLen = v);
			cy = Stepper(sb, player, tile, s, x, pw, cy, "Outward speed", s.OutSpeed, 10, 100, 5, v => s.OutSpeed = v);
			cy = Stepper(sb, player, tile, s, x, pw, cy, "Return speed", s.ReturnSpeed, 20, 200, 5, v => s.ReturnSpeed = v);
			cy = Stepper(sb, player, tile, s, x, pw, cy, "Reload (ticks)", s.CooldownMax, 60, 900, 30, v => s.CooldownMax = v);
			// Yoyo head style: 8 looks (tint + string color) with damage feel.
			var tstyle = TrapSystem.TripStyleOf(s.TripStyle);
			TrapUI.CText(sb, $"Head: {tstyle.Name} (x{tstyle.DmgMult:0.##} dmg)", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			Rectangle yp = new Rectangle(x + 14, (int)cy, 120, 24);
			Rectangle yn = new Rectangle(x + 140, (int)cy, 120, 24);
			if (TrapUI.CButton(sb, yp, "< Prev", "Previous unlocked yoyo head", false, false, 0.75f)) {
				int ni = s.TripStyle;
				for (int k = 0; k < TrapSystem.TripStyles.Length; k++) {
					ni = (ni + TrapSystem.TripStyles.Length - 1) % TrapSystem.TripStyles.Length;
					if (TrapSystem.TripStyleUnlocked(ni, tile, s, player, out _))
						break;
				}
				if (ni != s.TripStyle) { s.TripStyle = ni; PushConfig(tile, s); }
			}
			if (TrapUI.CButton(sb, yn, "Next >", "Next yoyo head (locked shows why)", false, false, 0.75f)) {
				int ni = (s.TripStyle + 1) % TrapSystem.TripStyles.Length;
				if (TrapSystem.TripStyleUnlocked(ni, tile, s, player, out string why)) { s.TripStyle = ni; PushConfig(tile, s); }
				else TrapUI.SetMessage(TrapSystem.TripStyleOf(ni).Name + ": " + why);
			}
			cy += 28;
			cy += TrapUI.CWrap(sb, "Get: " + tstyle.Obtain, new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			// TETHER string (not the head): Plain + Barbed/Tangled (accessory
			// numbers) + 8 material colors. Only Barbed/Tangled deal damage.
			int tstr = System.Math.Clamp(s.TripString, 0, 24);
			string tstrTip = tstr == 1 ? "Barbed tether: 5 touch dmg + Bleeding 5s (hostiles)" : tstr == 2 ? "Tangled tether: 1 touch dmg + Bleeding 3s (hostiles)" : $"{TrapSystem.TripStringName(tstr)} tether: colored line, harmless";
			TrapUI.CText(sb, $"String: {TrapSystem.TripStringName(tstr)}", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			Rectangle sp = new Rectangle(x + 14, (int)cy, 120, 24);
			Rectangle sn = new Rectangle(x + 140, (int)cy, 120, 24);
			if (TrapUI.CButton(sb, sp, "< Prev", "Previous tether string", false, false, 0.75f)) {
				int ni = s.TripString;
				for (int k = 0; k < 25; k++) {
					ni = (ni + 24) % 25;
					if (TrapSystem.TripStringUnlocked(ni, s, out _))
						break;
				}
				if (ni != s.TripString) { s.TripString = ni; PushConfig(tile, s); }
			}
			if (TrapUI.CButton(sb, sn, "Next >", tstrTip, false, false, 0.75f)) {
				int ni = (s.TripString + 1) % 25;
				if (TrapSystem.TripStringUnlocked(ni, s, out string why)) { s.TripString = ni; PushConfig(tile, s); }
				else TrapUI.SetMessage(TrapSystem.TripStringName(ni) + ": " + why);
			}
			cy += 28;
			cy += TrapUI.CWrap(sb, tstrTip, new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += TrapUI.CWrap(sb, "Get: " + TrapSystem.TripStringObtain(tstr), new Vector2(x + 14, cy), Color.LightGreen, 0.75f, pw - 28);
			int halfW = (pw - 28 - 8) / 2;
			Rectangle p0 = new Rectangle(x + 14, (int)cy, halfW, 24);
			Rectangle p1 = new Rectangle(x + 14 + halfW + 8, (int)cy, halfW, 24);
			if (TrapUI.CButton(sb, p0, "Nearest", "Target priority (damage)", s.Priority == 0, false, 0.75f)) { s.Priority = 0; PushConfig(tile, s); }
			if (TrapUI.CButton(sb, p1, "Strongest", "Target priority (damage)", s.Priority == 1, false, 0.75f)) { s.Priority = 1; PushConfig(tile, s); }
			cy += 28;
			Rectangle oc = new Rectangle(x + 14, (int)cy, System.Math.Min(300, pw - 28), 24);
			bool ocLocked = s.Level < 1;
			if (TrapUI.CButton(sb, oc, $"Overcharge {(s.Overcharge ? "ON" : "OFF")}{(ocLocked ? " (Lv1)" : "")}", ocLocked ? "Needs trap Lv1" : "Risk/reward: +50% dmg, 15% backfire unless License III nearby", s.Overcharge, ocLocked, 0.75f)) {
				if (!ocLocked) { s.Overcharge = !s.Overcharge; PushConfig(tile, s); }
				else TrapUI.SetMessage("Overcharge needs trap Lv1.");
			}
			cy += 28;
			cy = MeshOption(sb, player, tile, s, x, pw, cy, "Snare Tip", s.SnareTip, v => s.SnareTip = v, "Yoyo hits also Slow enemies for 2s (Aegis only)");
			cy = ToggleAt(sb, player, tile, s, x + 14, cy, pw - 28, "Hair Trigger", s.HairTrigger, v => s.HairTrigger = v, "Half reload, -25% damage", 1);
			cy = ToggleAt(sb, player, tile, s, x + 14, cy, pw - 28, "Twin Throw", s.TwinThrow, v => s.TwinThrow = v, "Second yoyo opposite at 1.5x total power (half extra)", 2);
			string st = TrapUIHelper.TripwireState(tile, s);
			cy = Row(sb, x, pw, cy, "Trap state", st);
			return cy;
		}

		private static float DrawTripwireTech(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s)
		{
			Point tile = TrapUI.OpenTile;
			cy = TrackRow(sb, player, x, pw, cy, s, tile, TrapSystem.AegisCovered(tile), 3, $"Lubricated Axle (+{s.TripAxle * 20}% flight)", s.TripAxle, "Yoyo hangs longer per level");
			cy = TrackRow(sb, player, x, pw, cy, s, tile, TrapSystem.AegisCovered(tile), 7, $"Second Wind (-{s.TripWind * 10}% reload)", s.TripWind, "Reloads faster per level");
			cy = MeshOption(sb, player, tile, s, x, pw, cy, "Auto-Overcharge", s.AutoOC, v => s.AutoOC = v, "While Aegis-fueled: fires overcharged at 2% backfire (+5 power/shot)");
			cy = DrawShareRow(sb, player, x, pw, cy, s, tile);
			cy = DrawSchedRow(sb, player, x, pw, cy, s, tile);
			return cy;
		}
		private static float DrawExperimentTab(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s)
		{
			Point tile = TrapUI.OpenTile;
			cy += TrapUI.CWrap(sb, "Field experiments: load two inventory stacks, preview the reaction, Combine. Nothing is consumed unless the mix is known.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			cy += 4;
			cy = TrapExperiment.DrawPicker(sb, player, x, pw, cy, "Reagent A", ref TrapExperiment.SlotA, ref TrapExperiment.AmtA, ref TrapExperiment.PageA, TrapExperiment.FillA, () => TrapExperiment.FillA = true);
			cy = TrapExperiment.DrawPicker(sb, player, x, pw, cy, "Reagent B", ref TrapExperiment.SlotB, ref TrapExperiment.AmtB, ref TrapExperiment.PageB, !TrapExperiment.FillA, () => TrapExperiment.FillA = false);
			int fx = TrapExperiment.PreviewEffectFor(player, tile, out string label);
			cy += TrapUI.CWrap(sb, "Expects: " + label, new Vector2(x + 14, cy), Color.Yellow, 0.8f, pw - 28);
			cy += 4;
			Rectangle go = new Rectangle(x + 14, (int)cy, 200, 26);
			Rectangle clr = new Rectangle(x + 220, (int)cy, 140, 26);
			if (TrapUI.CButton(sb, go, "Combine", fx < 0 ? "No known reaction" : label, false, fx < 0, 0.8f)) {
				if (TrapExperiment.TryCombine(player, tile))
					TrapUI.SetMessage("Experiment accepted: " + label);
				else
					TrapUI.SetMessage("Experiment refused.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			if (TrapUI.CButton(sb, clr, "Clear", "Empty both reagent slots", false, false, 0.8f))
				TrapExperiment.Clear();
			cy += 32;
			cy += TrapUI.CWrap(sb, "Codex — Oil & Wax Service: Bearing Oil/String Wax x1 + any = +25% condition.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += TrapUI.CWrap(sb, "Codex — Rewire Kit: Wire x5 + any = clears stress+fray, +40 power.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += TrapUI.CWrap(sb, "Codex — Field Recharge: 2 yoyo mats (4+ total) = full power, no cooldown.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += TrapUI.CWrap(sb, "Codex — Attunement: Cobweb/Silk x10 + any = +10 field uses.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			if (s.Kind == TrapKind.Sentry)
			cy += TrapUI.CWrap(sb, "Codex — Weights: Stinger x5 = Venom, Gold Bar x5 = Gilded, Ectoplasm x3 = Warden, Power Cell x1 = Orbit, Lunar Bar x5 = Lunar, Bone x50 = Ancient (each + anything, this trap only).", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			if (s.Kind == TrapKind.Tripwire) {
			cy += TrapUI.CWrap(sb, "Codex — Heads: Black Lens x1 = Dusk, Ice x50 = Glacier, Vertebrae x10 = Blood Tithe (each + anything, this trap only).", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			cy += TrapUI.CWrap(sb, "Codex — Tethers: Red/Orange/Yellow/Lime/Green/Teal/Cyan/Sky Blue/Blue/Purple/Violet/Pink/Black/Brown/Rainbow String x1 = matching tether (Lv2), Ancient Scraps x5 = Ancient, Crimtane Bar x5 = Crimson (Lv3, each + anything).", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			}
			return cy;
		}

		private static float DrawUpgradeRow(SpriteBatch sb, Player player, int x, int pw, float cy, TrapState s, int ph)
		{
			Point tile = TrapUI.OpenTile;
			bool roomy = ph >= 640;
			// Master switch first: disabled traps never fire, drain, or strain.
			// Copy/Paste share the row: stamp one block's settings onto
			// same-kind blocks (settings only — levels/tracks/power stay).
			Rectangle en = new Rectangle(x + 14, (int)cy, 150, 24);
			if (TrapUI.CButton(sb, en, $"Enabled: {(s.Enabled ? "ON" : "OFF")}", "Master switch. OFF parks the trap (no fire, no drain, no strain).", s.Enabled, false, 0.8f)) {
				s.Enabled = !s.Enabled; PushConfig(tile, s);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			Rectangle cp = new Rectangle(x + 170, (int)cy, 90, 24);
			if (TrapUI.CButton(sb, cp, "Copy", "Copy this block's settings (same-kind paste).", false, false, 0.75f)) {
				TrapActions.CopyPreset(tile);
				TrapUI.SetMessage($"Copied {s.Kind} settings. Open a {s.Kind} and Paste.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			bool canPaste = TrapActions.CanPastePreset(tile);
			Rectangle ps = new Rectangle(x + 266, (int)cy, 90, 24);
			if (TrapUI.CButton(sb, ps, "Paste", canPaste ? "Stamp the copied settings here." : (TrapActions.CopyBuf == null ? "Copy a block first." : "Copied kind differs."), false, !canPaste, 0.75f)) {
				if (canPaste && TrapSystem.Traps.TryGetValue(tile, out var dst)) {
					TrapActions.CopyConfigFields(dst, TrapActions.CopyBuf);
					PushConfig(tile, dst);
					TrapUI.SetMessage("Settings pasted (levels/tracks/power kept).");
				}
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			cy += 28;
			// Nickname: free text, max 16 chars (Set → type → Enter/Esc).
			// Hidden on short screens (title still shows an existing name).
			if (roomy) {
				bool naming = TrapUI.NamingThis(tile);
				string shown = naming ? TrapUI.NamingBuf + ((Main.GameUpdateCount % 30 < 15) ? "_" : "") : (string.IsNullOrEmpty(s.Name) ? "(unnamed)" : s.Name);
				TrapUI.CText(sb, $"Name: {shown}", new Vector2(x + 14, cy), Color.White, 0.8f);
				Rectangle setB = new Rectangle(x + 220, (int)cy - 2, 80, 22);
				if (TrapUI.CButton(sb, setB, naming ? "Done" : "Set", naming ? "Confirm the typed name" : "Type a custom name (max 16)", naming, false, 0.8f)) {
					if (naming) {
						TrapUI.CommitName(player, tile);
					}
					else {
						TrapUI.NamingTile = tile;
						TrapUI.NamingBuf = s.Name ?? "";
					}
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				}
				cy += 26;
			}
			var cost = TrapActions.CostFor(s.Kind, s.Level + 1);
			string req = "";
			bool can = s.Level < s.MaxLevel && TrapActions.UpgradeRequirementMet(tile, s, out req, player);
			string costText = s.Level >= s.MaxLevel ? "MAX LEVEL" : $"{cost.Count}x {new Item(cost.Mat).Name} + {YoyoPanel.FormatCoins(cost.Coins)}";
			TrapUI.CText(sb, $"Upgrade {s.Level}/{s.MaxLevel}: {costText}", new Vector2(x + 14, cy), Color.White, 0.8f);
			cy += 22;
			if (s.Level < s.MaxLevel) {
				string perk = TrapActions.LevelPerkText(s.Kind, s.Level + 1);
				if (!string.IsNullOrEmpty(perk))
					cy += TrapUI.CWrap(sb, "Next: " + perk, new Vector2(x + 14, cy), Color.LightGreen, 0.75f, pw - 28);
			}
			int vet = TrapSystem.VeteranBonus(s.Kills);
			if (roomy) {
				TrapUI.CText(sb, $"Kills: {s.Kills}{(vet > 0 ? $" (+{vet}% veteran)" : " (25/100/250 for veteran bonus)")}", new Vector2(x + 14, cy), Color.White, 0.8f);
				cy += 22;
			}
			if (!can && s.Level < s.MaxLevel) {
				cy += TrapUI.CWrap(sb, req, new Vector2(x + 14, cy), Color.Orange, 0.75f, pw - 28);
			}
			Rectangle up = new Rectangle(x + 14, (int)cy, 180, 26);
			bool afford = can && player.CanAfford(cost.Coins) && Content.NPCs.Town.YoyoTownShared.CountInInventory(player, cost.Mat) >= cost.Count;
			if (TrapUI.CButton(sb, up, "Upgrade", can ? costText : req, false, !afford && s.Level < s.MaxLevel || s.Level >= s.MaxLevel, 0.8f)) {
				if (Main.netMode == NetmodeID.MultiplayerClient) {
					// Pay client-side (mirrors placeable pattern), server validates gates.
					if (TrapActions.TryUpgrade(player, tile))
						TrapNet.SendUpgrade(tile);
					else
						TrapUI.SetMessage("Cannot upgrade: check costs/uses/Aegis.");
				}
				else {
					if (!TrapActions.TryUpgrade(player, tile))
						TrapUI.SetMessage("Cannot upgrade: check costs/uses/Aegis.");
				}
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			// Power cell note (compact, wraps on narrow panels)
			cy += TrapUI.CWrap(sb, "Power drains on fire, regens 2x in Aegis.", new Vector2(x + 200, cy + 4), Color.Gray, 0.7f, pw - 214);
			cy += 16;
			// Maintenance: condition + Bearing-Oil service (compact).
			string condLabel = s.Condition >= 100 ? "Pristine" : s.Condition >= 95 ? "Good" : s.Condition >= 75 ? "Worn" : s.Condition >= 50 ? "Degraded" : "Failing";
			TrapUI.CText(sb, $"Condition: {s.Condition}% ({condLabel})", new Vector2(x + 14, cy), Color.White, 0.8f);
			if (s.Condition < 100) {
				int svcW = System.Math.Min(220, pw - 28 - 206);
				if (svcW > 120) {
					Rectangle svc = new Rectangle(x + 14 + 206, (int)cy - 2, svcW, 22);
					bool canSvc = player.CanAfford(Item.buyPrice(gold: 1)) && Content.NPCs.Town.YoyoTownShared.CountInInventory(player, ModContent.ItemType<Items.Consumables.BearingOil>()) >= 1;
					if (TrapUI.CButton(sb, svc, "Service (oil+1g)", "Bearing Oil + 1g restores 25%", false, !canSvc, 0.75f)) {
						if (TrapActions.TryService(player, tile))
							TrapUI.SetMessage("Serviced: +25% condition.");
						else
							TrapUI.SetMessage("Need 1 Bearing Oil + 1g.");
						Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
					}
				}
			}
			cy += 24;
			return cy;
		}
	}
}


namespace BiltonsYoyoMod.Traps
{
	public static class TrapUIHelper
	{
		public static string TripwireState(Microsoft.Xna.Framework.Point tile, TrapState s)
		{
			if (s.Cooldown > 0) return $"Reloading {s.Cooldown / 60f:0.0}s";
			for (int i = 0; i < Terraria.Main.maxProjectiles; i++) {
				var pr = Terraria.Main.projectile[i];
				if (pr.active && pr.type == ModContent.ProjectileType<TripwireYoyoProj>() && (int)pr.ai[0] == tile.X && (int)pr.ai[1] == tile.Y)
					return pr.localAI[0] == 0 ? "Deployed (outward)" : "Returning";
			}
			return "Idle (ready)";
		}
	}
}
