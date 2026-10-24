using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using YoyoPanel = BiltonsYoyoMod.Content.UI.YoyoNPC.YoyoNPCPanel;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Dedicated Resonator upgrade UI (new panel, reuses YoyoNPCPanel
	// widget style). Shows levels, costs, resulting effects and reversal
	// options. Chosen over adapting the station UI because upgrade tracks
	// are per-tile weaken stats, not modifier browse lists.
	public class ResonatorUISystem : ModSystem
	{
		public static bool Open;
		public static Point OpenTile;
		public static string Message = "";
		public static int MessageTimer;
		public static int Tab; // 0 Upgrades, 1 Field

		private static Rectangle PanelRect;

		public static void OpenPanel(int x, int y)
		{
			Open = true;
			OpenTile = new Point(x, y);
			Message = "";
			MessageTimer = 0;
			Tab = 0;
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public static void Close()
		{
			Open = false;
		}

		public static void SetMessage(string text, int time = 360)
		{
			Message = text;
			MessageTimer = time;
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
			Vector2 tileWorld = ResonatorSystem.ResonatorCenter(OpenTile);
			if (Vector2.Distance(player.Center, tileWorld) > 800f)
				Close();
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
				Close();
			// In-world radius preview while configuring (client fx only).
			// Honors the player's ring toggle: OFF means OFF, even here.
			// Pink band pinned to the block, count scaled to circumference.
			if (Open && !Main.dedServ && player.GetModPlayer<ResonatorPlayer>().ShowRing && Main.GameUpdateCount % 6 == 0 && ResonatorSystem.Tiles.TryGetValue(OpenTile, out var preview)) {
				float radius = ResonatorSystem.EffRadiusPx(preview);
				PlaceableUI.DustRing(tileWorld, radius, DustID.PinkTorch, PlaceableUI.RingCount(radius), 1.2f);
				Lighting.AddLight(tileWorld, 0.3f, 0.12f, 0.3f);
			}
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Resonator Panel",
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
			int pw = System.Math.Min(560, Main.screenWidth - 16);
			int ph = System.Math.Min(620, Main.screenHeight - 16);
			int x = (Main.screenWidth - pw) / 2;
			int y = (Main.screenHeight - ph) / 2;
			PanelRect = new Rectangle(x, y, pw, ph);
			if (PanelRect.Contains(Main.MouseScreen.ToPoint()))
				player.mouseInterface = true;
			YoyoPanel.DrawPanelBackground(sb, PanelRect);
			PlaceableUI.HeaderStrip(sb, x, y, pw, new Color(150, 200, 255));
			YoyoPanel.DrawItemIcon(sb, ModContent.ItemType<global::BiltonsYoyoMod.Tiles.Placeables.YoyoResonatorItem>(), new Vector2(x + 28, y + 22), 24f, Color.White);
			Terraria.Utils.DrawBorderString(sb, "Yoyo Resonator", new Vector2(x + 46, y + 12), Color.Gold, 1f);
			float cy = y + 34 + PlaceableUI.DrawWrapped(sb, "Dissonant Hum: yoyo hits weaken enemies (less contact damage) while you are in radius. Yoyos only.", new Vector2(x + 46, y + 34), Color.LightGray, 0.75f, pw - 60);
			PlaceableUI.Divider(sb, x + 14, (int)cy + 2, pw - 28, new Color(90, 98, 140, 180));
			cy += 8;
			Tab = PlaceableUI.TabRow(sb, x + 14, (int)cy, pw - 28, new[] { "Upgrades", "Field" }, Tab);
			cy += 36;
			Rectangle xBtn = new(x + pw - 42, y + 8, 32, 30);
			if (YoyoPanel.Button(sb, xBtn, "X", "Close", false, false, 0.95f))
				Close();
			if (!ResonatorSystem.Tiles.TryGetValue(OpenTile, out var data))
				data = new ResonatorData();
			if (Tab == 0)
				cy = DrawUpgrades(sb, player, x, pw, cy, data);
			else
				cy = DrawField(sb, player, x, pw, cy, data);
			PlaceableUI.Divider(sb, x + 14, (int)cy, pw - 28, new Color(90, 98, 140, 180));
			cy += 6;
			if (!string.IsNullOrEmpty(Message))
				cy += PlaceableUI.DrawWrapped(sb, Message, new Vector2(x + 14, cy), Color.Yellow, 0.8f, pw - 28);
			PlaceableUI.DrawWrapped(sb, "Downgrade refunds 70% of that level's cost. Breaking the block loses upgrades (no refund).", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
		}

		private static float DrawUpgrades(SpriteBatch sb, Player player, int x, int pw, float cy, ResonatorData data)
		{
			cy = DrawTrack(sb, player, new Rectangle(x + 14, (int)cy, pw - 28, 96), 0, "Potency (weaken %)", new Color(230, 150, 90), data.Potency, ResonatorSystem.MaxPotency,
				$"Current: -{ResonatorSystem.WeakenPct(data.Potency) * 100f:0}% enemy contact damage",
				data.Potency >= ResonatorSystem.MaxPotency ? "MAX" : NextText(ResonatorSystem.PotencyCost(data.Potency + 1), $"-{ResonatorSystem.WeakenPct(data.Potency + 1) * 100f:0}%")) + 6;
			cy = DrawTrack(sb, player, new Rectangle(x + 14, (int)cy, pw - 28, 96), 1, "Duration", new Color(230, 200, 120), data.Duration, ResonatorSystem.MaxDuration,
				$"Current: {ResonatorSystem.WeakenTicks(data.Duration) / 60f:0.0}s weaken",
				data.Duration >= ResonatorSystem.MaxDuration ? "MAX" : NextText(ResonatorSystem.DurationCost(data.Duration + 1), $"{ResonatorSystem.WeakenTicks(data.Duration + 1) / 60f:0.0}s")) + 6;
			cy = DrawTrack(sb, player, new Rectangle(x + 14, (int)cy, pw - 28, 96), 2, "Radius", new Color(150, 200, 255), data.Radius, ResonatorSystem.MaxRadius,
				$"Current: {ResonatorSystem.RadiusPixels(data.Radius) / 16f:0} tiles",
				data.Radius >= ResonatorSystem.MaxRadius ? "MAX" : NextText(ResonatorSystem.RadiusCost(data.Radius + 1), $"{ResonatorSystem.RadiusPixels(data.Radius + 1) / 16f:0} tiles")) + 6;
			cy = DrawTrack(sb, player, new Rectangle(x + 14, (int)cy, pw - 28, 96), 3, "Feedback (yoyo dmg vs weakened)", new Color(230, 130, 200), data.Feedback, ResonatorSystem.MaxFeedback,
				data.Feedback <= 0 ? "Current: off" : $"Current: +{ResonatorSystem.FeedbackPct(data.Feedback) * 100f:0}% yoyo damage vs weakened foes",
				data.Feedback >= ResonatorSystem.MaxFeedback ? "MAX" : NextText(ResonatorSystem.FeedbackCost(data.Feedback + 1), $"+{ResonatorSystem.FeedbackPct(data.Feedback + 1) * 100f:0}%")) + 6;
			return cy;
		}

		private static float DrawField(SpriteBatch sb, Player player, int x, int pw, float cy, ResonatorData data)
		{
			// Hum Focus retune (free switch, same mechanic, new shape).
			Terraria.Utils.DrawBorderString(sb, "Hum Focus:", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 22;
			float bw = (pw - 28 - 12) / 3f;
			for (int f = 0; f < 3; f++) {
				Rectangle b = new(x + 14 + (int)(f * (bw + 6)), (int)cy, (int)bw, 28);
				if (YoyoPanel.Button(sb, b, ResonatorSystem.FocusName(f), ResonatorSystem.FocusTip(f), data.Focus == f, false, 0.75f)) {
					if (Main.netMode == NetmodeID.MultiplayerClient)
						PlaceableNet.SendResonatorMode(OpenTile, f);
					else
						ResonatorActions.ApplyFocus(OpenTile, f);
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				}
			}
			cy += 34;
			cy += PlaceableUI.DrawWrapped(sb, $"Focus: {ResonatorSystem.FocusTip(data.Focus)}", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			cy += 4;
			// Live field readout with big effective numbers.
			var rp = player.GetModPlayer<ResonatorPlayer>();
			float dist = Vector2.Distance(player.Center, ResonatorSystem.ResonatorCenter(OpenTile)) / 16f;
			bool covered = rp.InResonator;
			Terraria.Utils.DrawBorderString(sb, covered ? "FIELD: LIVE" : "FIELD: DARK", new Vector2(x + 14, cy),
				covered ? Color.Lerp(Color.Lime, Color.White, Main.GameUpdateCount % 40 < 20 ? 0f : 0.35f) : Color.Gray, 1f);
			cy += 30;
			cy += PlaceableUI.DrawWrapped(sb, covered
				? $"You are INSIDE ({dist:0} tiles). Field: -{ResonatorSystem.EffWeakenPct(data, StationNetwork.ResonatorBonus(OpenTile)) * 100f:0.0}% contact, {ResonatorSystem.WeakenTicks(System.Math.Min(data.Duration + StationNetwork.ResonatorBonus(OpenTile), ResonatorSystem.MaxDuration)) / 60f:0.0}s, {ResonatorSystem.EffRadiusPx(data) / 16f:0} tiles{(StationNetwork.ResonatorBonus(OpenTile) > 0 ? " (meshed)" : "")}."
				: $"You are OUTSIDE ({dist:0} tiles). Step into the pink ring to empower yoyos.",
				new Vector2(x + 14, cy), covered ? Color.LightGreen : Color.Orange, 0.8f, pw - 28);
			cy += 4;
			Rectangle ringBtn = new(x + 14, (int)cy, 260, 26);
			if (YoyoPanel.Button(sb, ringBtn, $"Radius ring: {(rp.ShowRing ? "ON" : "OFF")}", "Toggle the in-world hum ring while covered", rp.ShowRing, false, 0.75f)) {
				rp.ShowRing = !rp.ShowRing;
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			cy += 32;
			cy += PlaceableUI.DrawWrapped(sb, "Tip: weaken first, then let Feedback cash it in. Wide blankets arenas; Focused guards doors.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			return cy;
		}

		private static string NextText((int coins, int mat, int count) cost, string effect)
		{
			Item tmp = new Item(cost.mat);
			return $"Next: {effect} — {YoyoPanel.FormatCoins(cost.coins)} + {cost.count}x {tmp.Name}";
		}

		private static float DrawTrack(SpriteBatch sb, Player player, Rectangle area, int track, string title, Color accent, int level, int max, string cur, string next)
		{
			sb.Draw(TextureAssets.MagicPixel.Value, area, new Color(34, 42, 68, 220));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(area.X, area.Y, 4, area.Height), accent);
			Terraria.Utils.DrawBorderString(sb, $"{title}: {level}/{max}", new Vector2(area.X + 8, area.Y + 6), Color.White, 0.85f);
			PlaceableUI.Pips(sb, new Vector2(area.X + 8, area.Y + 70), level, max);
			PlaceableUI.DrawTruncated(sb, cur, new Vector2(area.X + 8, area.Y + 28), Color.LightGreen, 0.75f, area.Width - 220);
			PlaceableUI.DrawTruncated(sb, next, new Vector2(area.X + 8, area.Y + 48), Color.LightGray, 0.75f, area.Width - 220);
			Rectangle buy = new(area.Right - 200, area.Y + 8, 94, 26);
			Rectangle down = new(area.Right - 100, area.Y + 8, 94, 26);
			bool maxed = level >= max;
			bool atBase = level <= 0;
			if (YoyoPanel.Button(sb, buy, "Upgrade", maxed ? "Maxed" : next, false, maxed, 0.75f))
				ResonatorActions.BuyTrack(player, OpenTile, track);
			if (YoyoPanel.Button(sb, down, "Downgrade", atBase ? "At base" : "Refund 70% of this level", false, atBase, 0.75f))
				ResonatorActions.DowngradeTrack(player, OpenTile, track);
			return area.Y + area.Height;
		}
	}
}
