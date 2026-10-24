using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using BiltonsYoyoMod.Content.Trials;
using YoyoPanel = BiltonsYoyoMod.Content.UI.YoyoNPC.YoyoNPCPanel;

namespace BiltonsYoyoMod.Content.Customization
{
	// Tile-station panel. Reuses the NPC panel widget style (buttons, icons, coins)
	// but with its own layout per station so it is not a 1:1 copy.
	public class YoyoStationUISystem : ModSystem
	{
		public static bool Open;
		public static int Station = -1; // 0..5
		public static int Section; // for dual stations: 0/1
		public static int Tab; // 0 browse, 1 minigame
		public static int ListScroll;
		public static string Message = "";
		public static int MessageTimer;
		public static Microsoft.Xna.Framework.Point OpenTile;

		public const int PanelW = 680;
		public const int PanelH = 480;
		public static Rectangle PanelRect;

		private static Color StationTint(int s) => s switch {
			0 => new(180, 160, 120),
			1 => new(140, 180, 220),
			2 => new(200, 140, 160),
			3 => new(140, 220, 160),
			4 => new(180, 160, 240),
			_ => new(160, 220, 220),
		};

		public static string StationTitle(int s) => s switch {
			0 => "Adjustment Machine (Yoyo)",
			1 => "Tinkering Machine (Yoyo)",
			2 => "Specialized Adjustment Machine (Yoyo)",
			3 => "Calibrating Machine (Yoyo)",
			4 => "Specialized Tinkering Machine (Yoyo)",
			5 => "Magical Adjustment Machine (Yoyo)",
			_ => "Yoyo Station",
		};

		public static void OpenStation(int station, int tileX, int tileY)
		{
			Open = true;
			Station = station;
			Section = 0;
			Tab = 0;
			ListScroll = 0;
			Message = "";
			MessageTimer = 0;
			Minigames.StationMinigames.Stop();
			OpenTile = new Microsoft.Xna.Framework.Point(tileX, tileY);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public static void Close()
		{
			// The UI disappearing stops any running minigame cold.
			Minigames.StationMinigames.Stop();
			Open = false;
			Station = -1;
		}

		public static void SetMessage(string text, int time = 360)
		{
			Message = text;
			MessageTimer = time;
		}

		public static bool MouseOverPanel()
		{
			if (PanelRect == Rectangle.Empty)
				return false;
			return PanelRect.Contains(Main.MouseScreen.ToPoint());
		}

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
			// Auto-close if walked far from the station tile.
			Vector2 tileWorld = OpenTile.ToWorldCoordinates();
			if (Vector2.Distance(player.Center, tileWorld) > 700f)
				Close();
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
				Close();
			if (Open)
				Minigames.StationMinigames.Tick();
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Yoyo Station Panel",
					() => {
						DrawPanel(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI));
		}

		private static void DrawPanel(SpriteBatch sb)
		{
			if (!Open || Station < 0)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;
			// Adaptive: never larger than the screen, so nothing can spill offscreen.
			int pw = System.Math.Min(PanelW, Main.screenWidth - 16);
			int ph = System.Math.Min(PanelH, Main.screenHeight - 16);
			int x = (Main.screenWidth - pw) / 2;
			int y = (Main.screenHeight - ph) / 2;
			PanelRect = new Rectangle(x, y, pw, ph);
			if (PanelRect.Contains(Main.MouseScreen.ToPoint()))
				player.mouseInterface = true;
			YoyoPanel.DrawPanelBackground(sb, PanelRect);
			Color tint = StationTint(Station);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x + 6, y + 5, pw - 12, 3), new Color(tint.R, tint.G, tint.B, 200));
			// Header.
			Terraria.Utils.DrawBorderString(sb, StationTitle(Station), new Vector2(x + 14, y + 12), Color.Gold, 1f);
			Terraria.Utils.DrawBorderString(sb, StationSubtitle(Station), new Vector2(x + 14, y + 34), Color.LightGray, 0.75f);
			// Attunement pips for this block's families.
			string[] fams = YoyoCustomizationSystem.StationFamilies(Station);
			if (fams.Length > 0) {
				string pips = "";
				foreach (string fam in fams)
					pips += YoyoCustomizationSystem.IsTileUnlocked(OpenTile, fam) ? "[*]" : "[ ]";
				Terraria.Utils.DrawBorderString(sb, pips + " attuned", new Vector2(x + 14, y + 50), Color.Cyan, 0.7f);
			}
			Rectangle xBtn = new(x + pw - 42, y + 8, 32, 30);
			if (YoyoPanel.Button(sb, xBtn, "X", "Close (minigame stops)", false, false, 0.95f))
				Close();
			Rectangle content = new(x + 14, y + 66, pw - 28, ph - 66 - 36);
			DrawContent(sb, player, content);
			if (!string.IsNullOrEmpty(Message))
				Terraria.Utils.DrawBorderString(sb, Message, new Vector2(x + 14, y + ph - 28), Color.Yellow, 0.8f);
		}

		private static string StationSubtitle(int s) => s switch {
			0 => "Ordinary reforges. Daily rate. Keeps every custom modifier.",
			1 => "Normal Tuning. Trial: Broken Trajectory Engine.",
			2 => "Mastery. Trial: Mirror Duel.",
			3 => "Normal Calibration. Trial: Pendulum Paradox.",
			4 => "Extended Tuning + Extended Calibration. Two trials.",
			5 => "Mastery + True Mastery. Trial: Worldfold Engine.",
			_ => "",
		};

		private static void DrawContent(SpriteBatch sb, Player player, Rectangle content)
		{
			float y = content.Y + 2;
			// Held yoyo card with every modifier shown.
			y += DrawHeldCard(sb, content, y);
			// Browse vs Minigame tabs. Leaving the tab stops a running game.
			Rectangle tabB = new(content.X, (int)y, 200, 26);
			Rectangle tabM = new(content.X + 208, (int)y, 200, 26);
			if (YoyoPanel.Button(sb, tabB, "Browse", "Coin shop: buy any unlocked mod", Tab == 0, false, 0.8f)) {
				if (Tab == 1 && Minigames.StationMinigames.Playing) {
					Minigames.StationMinigames.Stop();
					SetMessage("Minigame stopped (tab changed). Nothing spent.");
				}
				Tab = 0;
			}
			if (YoyoPanel.Button(sb, tabM, "Minigame", "Play for a SPECIFIC mod, free on win", Tab == 1, false, 0.8f))
				Tab = 1;
			y += 30;
			if (Tab == 1) {
				Minigames.StationMinigames.DrawTab(sb, player, content, (int)y, Station, Section);
				return;
			}
			switch (Station) {
				case 0: DrawReforge(sb, player, content, (int)y); break;
				case 1: DrawTuningList(sb, player, content, (int)y, extended: false); break;
				case 2: DrawMasteryList(sb, player, content, (int)y, trueMastery: false); break;
				case 3: DrawCalibrationList(sb, player, content, (int)y, extended: false); break;
				case 4:
					// Two sections.
					Rectangle b0 = new(content.X, (int)y, 220, 26);
					Rectangle b1 = new(content.X + 228, (int)y, 220, 26);
					if (YoyoPanel.Button(sb, b0, "Extended Tuning", "Clockwork Catastrophe", Section == 0, false, 0.8f)) { Section = 0; ListScroll = 0; }
					if (YoyoPanel.Button(sb, b1, "Extended Calibration", "The Impossible Circuit", Section == 1, false, 0.8f)) { Section = 1; ListScroll = 0; }
					y += 30;
					if (Section == 0) DrawTuningList(sb, player, content, (int)y, extended: true);
					else DrawCalibrationList(sb, player, content, (int)y, extended: true);
					break;
				case 5:
					Rectangle m0 = new(content.X, (int)y, 220, 26);
					Rectangle m1 = new(content.X + 228, (int)y, 220, 26);
					if (YoyoPanel.Button(sb, m0, "Mastery", "Attune Mirror Duel at THIS block", Section == 0, false, 0.8f)) { Section = 0; ListScroll = 0; }
					if (YoyoPanel.Button(sb, m1, "True Mastery", "Worldfold Engine", Section == 1, false, 0.8f)) { Section = 1; ListScroll = 0; }
					y += 30;
					if (Section == 0) DrawMasteryList(sb, player, content, (int)y, trueMastery: false);
					else DrawMasteryList(sb, player, content, (int)y, trueMastery: true);
					break;
			}
		}

		private static float DrawHeldCard(SpriteBatch sb, Rectangle content, float y)
		{
			Player player = Main.LocalPlayer;
			Item held = YoyoStationActions.HeldYoyo(player);
			Rectangle slot = new(content.X + 4, (int)y + 2, 44, 44);
			float bgScale = 44f / TextureAssets.InventoryBack.Value.Width;
			sb.Draw(TextureAssets.InventoryBack.Value, new Vector2(slot.X, slot.Y), null, Color.White * 0.9f, 0f, Vector2.Zero, bgScale, SpriteEffects.None, 0f);
			if (held != null) {
				YoyoPanel.DrawItemIcon(sb, held.type, new Vector2(slot.X + 22, slot.Y + 22), 36f, Color.White);
				Terraria.Utils.DrawBorderString(sb, held.Name, new Vector2(slot.X + 50, y + 2), Color.White, 0.8f);
				held.TryGetGlobalItem<YoyoCustomizationGlobalItem>(out var cust);
				held.TryGetGlobalItem<Common.YoyoEnhancementGlobalItem>(out var enh);
				string line = $"Mod: {(enh != null ? enh.GetModificationName() : "N/A")}  Spell: {(enh != null ? enh.GetSpellName() : "N/A")}";
				Terraria.Utils.DrawBorderString(sb, line, new Vector2(slot.X + 50, y + 22), Color.Gray, 0.7f);
				string line2 = $"Tuning: {(cust != null ? cust.TuningName() : "N/A")}  Calib: {(cust != null ? cust.CalibrationName() : "N/A")}";
				Terraria.Utils.DrawBorderString(sb, line2, new Vector2(slot.X + 50, y + 36), Color.Gray, 0.7f);
				string line3 = $"Mastery: {(cust != null ? cust.MasteryName() : "N/A")}  Curse: {(cust != null ? cust.CurseName() : "N/A")}";
				Terraria.Utils.DrawBorderString(sb, line3, new Vector2(slot.X + 50, y + 50), Color.Gray, 0.7f);
				return 72f;
			}
			Terraria.Utils.DrawBorderString(sb, "Hold a yoyo in your hand.", new Vector2(slot.X + 50, y + 12), Color.Yellow, 0.85f);
			return 52f;
		}

		private static void DrawReforge(SpriteBatch sb, Player player, Rectangle content, int top)
		{
			Item held = YoyoStationActions.HeldYoyo(player);
			float y = top + 4;
			y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 4, y),
				"Behaves like the Goblin reforge but yoyos only, with ordinary prefixes. Today's multiplier rerolls daily and is shown before you pay. Custom Tuning/Calibration/Mastery/Curse, Spells and Modifications are preserved.",
				Color.LightGray, 0.8f, content.Width - 8) + 6;
			if (held != null) {
				int price = YoyoCustomizationSystem.GetReforgePrice(held);
				Terraria.Utils.DrawBorderString(sb, $"Today's price: {YoyoPanel.FormatCoins(price)} (rate x{YoyoCustomizationSystem.GetReforgeMult():0.00})", new Vector2(content.X + 4, y), Color.Gold, 0.85f);
				y += 26;
				YoyoPanel.DrawCoins(sb, new Vector2(content.X + 4, y), price, 0.75f);
				y += 24;
				Rectangle go = new(content.Right - 190, (int)y, 180, 30);
				if (YoyoPanel.Button(sb, go, "Reforge", "Roll a new ordinary prefix. Keeps all custom modifiers.", false, false, 0.85f))
					YoyoStationActions.DoReforge(player);
			}
			else {
				Terraria.Utils.DrawBorderString(sb, "Hold an eligible yoyo to see today's price.", new Vector2(content.X + 4, y), Color.Yellow, 0.8f);
			}
		}

		private static void DrawLockedBlock(SpriteBatch sb, Player player, Rectangle content, float y, string fam)
		{
			int trial = YoyoCustomizationSystem.FamilyTrial(fam);
			y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 4, y),
				$"This block is not attuned ({YoyoCustomizationSystem.FamilyTitle(fam)}). Attunement is per-block: every placed station earns its own, permanently.", Color.Orange, 0.85f, content.Width - 210) + 4;
			y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 4, y),
				$"Trial: {Trials.YoyoTrialActions.TrialName(trial)} — {Minigames.StationMinigames.TrialBrief(fam)} Played right here, in this UI.", Color.LightGray, 0.8f, content.Width - 210) + 4;
			Rectangle go = new(content.Right - 200, (int)y, 190, 30);
			bool canTrial = Trials.YoyoTrialActions.TrialRequirementMet(player, trial);
			if (YoyoPanel.Button(sb, go, "Begin Attunement", canTrial ? "Play this block's unlock trial in the Minigame tab" : "Requires: " + Trials.YoyoTrialActions.TrialRequirementText(trial), false, !canTrial, 0.8f)) {
				Tab = 1;
				Minigames.StationMinigames.StartTrial(Station, fam);
			}
		}

		// Coins + material cost cluster drawn inside a browse row.
		private static void DrawCost(SpriteBatch sb, Rectangle row, int coins, int mat, int matCount)
		{
			YoyoPanel.DrawCoins(sb, new Vector2(row.X + 230, row.Y + 5), coins, 0.6f);
			YoyoPanel.DrawItemIcon(sb, mat, new Vector2(row.Right - 188, row.Y + 15), 20f, Color.White);
			Terraria.Utils.DrawBorderString(sb, "x" + matCount, new Vector2(row.Right - 174, row.Y + 6), Color.LightGray, 0.7f);
			if (new Rectangle(row.Right - 198, row.Y + 2, 104, 26).Contains(Main.MouseScreen.ToPoint())) {
				Main.LocalPlayer.mouseInterface = true;
				Main.hoverItemName = $"{Terraria.Lang.GetItemNameValue(mat)} x{matCount}";
			}
		}

		private static void DrawStars(SpriteBatch sb, Vector2 pos, string title, float scale, int stars)
		{
			Vector2 tsz = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(title) * scale;
			Terraria.Utils.DrawBorderString(sb, Customization.YoyoDifficulty.StarString(stars), new Vector2(pos.X + tsz.X + 6f, pos.Y), Color.Gold, 0.7f);
		}

		private static void DrawTuningList(SpriteBatch sb, Player player, Rectangle content, int top, bool extended)
		{
			string fam = extended ? "exttuning" : "tuning";
			string locked = YoyoCustomizationSystem.TileFamilyLock(OpenTile, fam);
			float y = top + 2;
			if (locked != null) {
				DrawLockedBlock(sb, player, content, y, fam);
				return;
			}
			var all = new System.Collections.Generic.List<YoyoTuningDef>();
			foreach (var d in YoyoTuningDefs.All) {
				if (d.Extended == extended) all.Add(d);
			}
			const int rowH = 34;
			int listTop = (int)y + 4;
			int footerH = 28;
			int listH = content.Bottom - listTop - footerH;
			int visible = System.Math.Max(1, listH / rowH);
			int maxScroll = System.Math.Max(0, all.Count - visible);
			ListScroll = System.Math.Clamp(ListScroll, 0, maxScroll);
			Item held = YoyoStationActions.HeldYoyo(player);
			YoyoCustomizationGlobalItem cust = null;
			if (held != null) held.TryGetGlobalItem<YoyoCustomizationGlobalItem>(out cust);
			int current = cust != null ? cust.TuningId : -1;
			for (int r = 0; r < visible; r++) {
				int idx = ListScroll + r;
				if (idx >= all.Count) break;
				var def = all[idx];
				Rectangle row = new(content.X + 4, listTop + r * rowH, content.Width - 8, rowH - 2);
				bool hrow = row.Contains(Main.MouseScreen.ToPoint());
				sb.Draw(TextureAssets.MagicPixel.Value, row, hrow ? new Color(48, 58, 92, 230) : new Color(34, 42, 68, 220));
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(row.X, row.Y, 4, row.Height), new Color(140, 180, 220));
				bool isCurrent = def.Id == current;
				string title = (isCurrent ? "[*] " : "") + def.Name;
				Terraria.Utils.DrawBorderString(sb, title, new Vector2(row.X + 8, row.Y + 5), isCurrent ? Color.Gold : Color.White, 0.8f);
				int stars = Customization.YoyoDifficulty.Stars(0, def.Id);
				DrawStars(sb, new Vector2(row.X + 8, row.Y + 5), title, 0.8f, stars);
				var (mat, matCount) = Customization.YoyoDifficulty.CostMat(0, extended, stars);
				if (row.Contains(Main.MouseScreen.ToPoint())) {
					Main.LocalPlayer.mouseInterface = true;
					Main.hoverItemName = $"{def.Name} ({def.Tier}) [{Customization.YoyoDifficulty.StarString(stars)}]\n{def.Description}\nCosts {YoyoPanel.FormatCoins(YoyoStationActions.TuningPrice(extended, stars))} + {matCount}x {Terraria.Lang.GetItemNameValue(mat)}\nReplaces Tuning: {(current >= 0 ? YoyoTuningDefs.ById(current)?.Name : "empty")}{(extended ? "\nNeeds networked Research Engine (Station Networking LV1+)." : "")}";
				}
				int price = YoyoStationActions.TuningPrice(extended, stars);
				DrawCost(sb, row, price, mat, matCount);
				Rectangle apply = new(row.Right - 90, row.Y + 3, 84, 24);
				if (YoyoPanel.Button(sb, apply, isCurrent ? "Active" : "Apply", isCurrent ? "Already applied" : $"Apply {def.Name}. Replaces current Tuning only.", false, held == null || isCurrent, 0.7f))
					YoyoStationActions.ApplyTuning(player, def);
			}
			DrawFooter(sb, content, all.Count, visible);
			// Strip.
			Rectangle strip = new(content.Right - 190, content.Bottom - 56, 180, 24);
			if (YoyoPanel.Button(sb, strip, "Strip Tuning (1g)", "Removes Tuning only", false, current < 0, 0.7f))
				YoyoStationActions.StripSlot(player, "tuning");
		}

		private static void DrawCalibrationList(SpriteBatch sb, Player player, Rectangle content, int top, bool extended)
		{
			string fam = extended ? "extcalib" : "calib";
			string locked = YoyoCustomizationSystem.TileFamilyLock(OpenTile, fam);
			float y = top + 2;
			if (locked != null) {
				DrawLockedBlock(sb, player, content, y, fam);
				return;
			}
			var all = new System.Collections.Generic.List<YoyoCalibrationDef>();
			foreach (var d in YoyoCalibrationDefs.All) {
				if (d.Extended == extended) all.Add(d);
			}
			const int rowH = 34;
			int listTop = (int)y + 4;
			int footerH = 28;
			int listH = content.Bottom - listTop - footerH;
			int visible = System.Math.Max(1, listH / rowH);
			int maxScroll = System.Math.Max(0, all.Count - visible);
			ListScroll = System.Math.Clamp(ListScroll, 0, maxScroll);
			Item held = YoyoStationActions.HeldYoyo(player);
			YoyoCustomizationGlobalItem cust = null;
			if (held != null) held.TryGetGlobalItem<YoyoCustomizationGlobalItem>(out cust);
			int current = cust != null ? cust.CalibrationId : -1;
			for (int r = 0; r < visible; r++) {
				int idx = ListScroll + r;
				if (idx >= all.Count) break;
				var def = all[idx];
				Rectangle row = new(content.X + 4, listTop + r * rowH, content.Width - 8, rowH - 2);
				bool hrow = row.Contains(Main.MouseScreen.ToPoint());
				sb.Draw(TextureAssets.MagicPixel.Value, row, hrow ? new Color(46, 68, 58, 230) : new Color(34, 52, 44, 220));
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(row.X, row.Y, 4, row.Height), new Color(140, 220, 160));
				bool isCurrent = def.Id == current;
				string title = (isCurrent ? "[*] " : "") + def.Name;
				Terraria.Utils.DrawBorderString(sb, title, new Vector2(row.X + 8, row.Y + 5), isCurrent ? Color.Gold : Color.White, 0.8f);
				int stars = Customization.YoyoDifficulty.Stars(1, def.Id);
				DrawStars(sb, new Vector2(row.X + 8, row.Y + 5), title, 0.8f, stars);
				var (mat, matCount) = Customization.YoyoDifficulty.CostMat(1, extended, stars);
				if (row.Contains(Main.MouseScreen.ToPoint())) {
					Main.LocalPlayer.mouseInterface = true;
					Main.hoverItemName = $"{def.Name} ({def.Tier}) [{Customization.YoyoDifficulty.StarString(stars)}]\n{def.Description}\nCosts {YoyoPanel.FormatCoins(YoyoStationActions.CalibrationPrice(extended, stars))} + {matCount}x {Terraria.Lang.GetItemNameValue(mat)}\nReplaces Calibration: {(current >= 0 ? YoyoCalibrationDefs.ById(current)?.Name : "empty")}{(extended ? "\nNeeds networked Research Engine (Station Networking LV1+)." : "")}";
				}
				int price = YoyoStationActions.CalibrationPrice(extended, stars);
				DrawCost(sb, row, price, mat, matCount);
				Rectangle apply = new(row.Right - 90, row.Y + 3, 84, 24);
				if (YoyoPanel.Button(sb, apply, isCurrent ? "Active" : "Apply", isCurrent ? "Already applied" : $"Apply {def.Name}. Replaces current Calibration only.", false, held == null || isCurrent, 0.7f))
					YoyoStationActions.ApplyCalibration(player, def);
			}
			DrawFooter(sb, content, all.Count, visible);
			Rectangle strip = new(content.Right - 190, content.Bottom - 56, 180, 24);
			if (YoyoPanel.Button(sb, strip, "Strip Calib (1g)", "Removes Calibration only", false, current < 0, 0.7f))
				YoyoStationActions.StripSlot(player, "calib");
		}

		private static void DrawMasteryList(SpriteBatch sb, Player player, Rectangle content, int top, bool trueMastery)
		{
			string fam = trueMastery ? "true" : "mastery";
			string locked = YoyoCustomizationSystem.TileFamilyLock(OpenTile, fam);
			float y = top + 2;
			if (locked != null) {
				DrawLockedBlock(sb, player, content, y, fam);
				return;
			}
			var all = new System.Collections.Generic.List<YoyoMasteryDef>();
			foreach (var d in YoyoMasteryDefs.All) {
				if (d.TrueMastery == trueMastery) all.Add(d);
			}
			const int rowH = 34;
			int listTop = (int)y + 4;
			int footerH = 28;
			int listH = content.Bottom - listTop - footerH;
			int visible = System.Math.Max(1, listH / rowH);
			int maxScroll = System.Math.Max(0, all.Count - visible);
			ListScroll = System.Math.Clamp(ListScroll, 0, maxScroll);
			Item held = YoyoStationActions.HeldYoyo(player);
			YoyoCustomizationGlobalItem cust = null;
			if (held != null) held.TryGetGlobalItem<YoyoCustomizationGlobalItem>(out cust);
			int current = cust != null ? cust.MasteryId : -1;
			for (int r = 0; r < visible; r++) {
				int idx = ListScroll + r;
				if (idx >= all.Count) break;
				var def = all[idx];
				Rectangle row = new(content.X + 4, listTop + r * rowH, content.Width - 8, rowH - 2);
				bool hrow = row.Contains(Main.MouseScreen.ToPoint());
				sb.Draw(TextureAssets.MagicPixel.Value, row, hrow ? new Color(68, 58, 44, 230) : new Color(52, 44, 34, 220));
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(row.X, row.Y, 4, row.Height), new Color(220, 180, 120));
				bool isCurrent = def.Id == current;
				string title = (isCurrent ? "[*] " : "") + def.Name;
				Terraria.Utils.DrawBorderString(sb, title, new Vector2(row.X + 8, row.Y + 5), isCurrent ? Color.Gold : Color.White, 0.8f);
				int stars = Customization.YoyoDifficulty.Stars(2, def.Id);
				DrawStars(sb, new Vector2(row.X + 8, row.Y + 5), title, 0.8f, stars);
				var (mat, matCount) = Customization.YoyoDifficulty.CostMat(2, trueMastery, stars);
				if (row.Contains(Main.MouseScreen.ToPoint())) {
					Main.LocalPlayer.mouseInterface = true;
					Main.hoverItemName = $"{def.Name} ({def.Tier}) [{Customization.YoyoDifficulty.StarString(stars)}]\n{def.Description}\nCosts {YoyoPanel.FormatCoins(YoyoStationActions.MasteryPrice(trueMastery, stars))} + {matCount}x {Terraria.Lang.GetItemNameValue(mat)}\nReplaces Mastery slot: {(current >= 0 ? YoyoMasteryDefs.ById(current)?.Name : "empty")}{(trueMastery ? "\nNeeds networked Research Engine (Station Networking LV1+)." : "")}";
				}
				int price = YoyoStationActions.MasteryPrice(trueMastery, stars);
				DrawCost(sb, row, price, mat, matCount);
				Rectangle apply = new(row.Right - 90, row.Y + 3, 84, 24);
				if (YoyoPanel.Button(sb, apply, isCurrent ? "Active" : "Apply", isCurrent ? "Already applied" : $"Apply {def.Name}. Replaces Mastery/True Mastery slot.", false, held == null || isCurrent, 0.7f))
					YoyoStationActions.ApplyMastery(player, def);
			}
			DrawFooter(sb, content, all.Count, visible);
			Rectangle strip = new(content.Right - 190, content.Bottom - 56, 180, 24);
			if (YoyoPanel.Button(sb, strip, "Strip Mastery (1g)", "Removes Mastery/True slot", false, current < 0, 0.7f))
				YoyoStationActions.StripSlot(player, "mastery");
		}

		private static void DrawFooter(SpriteBatch sb, Rectangle content, int total, int visible)
		{
			int maxScroll = System.Math.Max(0, total - visible);
			Rectangle up = new(content.Right - 190, content.Bottom - 26, 40, 24);
			Rectangle down = new(content.Right - 144, content.Bottom - 26, 40, 24);
			if (YoyoPanel.Button(sb, up, "^", "Scroll up", false, false, 0.8f))
				ListScroll = System.Math.Max(0, ListScroll - 1);
			if (YoyoPanel.Button(sb, down, "v", "Scroll down", false, false, 0.8f))
				ListScroll = System.Math.Min(maxScroll, ListScroll + 1);
			string text = $"{System.Math.Min(ListScroll + 1, total)}-{System.Math.Min(ListScroll + visible, total)} of {total}";
			Terraria.Utils.DrawBorderString(sb, text, new Vector2(content.X + 8, content.Bottom - 22), Color.Gray, 0.75f);
		}
	}
}
