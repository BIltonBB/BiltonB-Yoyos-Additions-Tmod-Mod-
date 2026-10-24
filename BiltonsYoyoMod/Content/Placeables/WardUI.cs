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
	// Dedicated Yoyo Ward UI (new panel, reuses YoyoNPCPanel widget style).
	// Shows damage/defense/vitality/radius levels, costs, resulting effects,
	// affected allies and reversal options.
	public class WardUISystem : ModSystem
	{
		public static bool Open;
		public static Point OpenTile;
		public static string Message = "";
		public static int MessageTimer;
		public static int Tab; // 0 Upgrades, 1 Allies

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
			Vector2 tileWorld = YoyoWardSystem.WardCenter(OpenTile);
			if (Vector2.Distance(player.Center, tileWorld) > 700f)
				Close();
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
				Close();
			// In-world radius preview while configuring (client fx only).
			// Honors the player's ring toggle: OFF means OFF, even here.
			if (Open && !Main.dedServ && player.GetModPlayer<WardPlayer>().ShowRing && Main.GameUpdateCount % 6 == 0 && YoyoWardSystem.Tiles.TryGetValue(OpenTile, out var preview)) {
				float radius = YoyoWardSystem.EffRadiusPx(preview, OpenTile);
				PlaceableUI.DustRing(tileWorld, radius, DustID.Electric, PlaceableUI.RingCount(radius), 1.2f);
				Lighting.AddLight(tileWorld, 0.15f, 0.25f, 0.45f);
			}
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Ward Panel",
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
			PanelRect = new Rectangle(x, y, pw, ph);
			if (PanelRect.Contains(Main.MouseScreen.ToPoint()))
				player.mouseInterface = true;
			YoyoPanel.DrawPanelBackground(sb, PanelRect);
			PlaceableUI.HeaderStrip(sb, x, y, pw, new Color(140, 230, 170));
			YoyoPanel.DrawItemIcon(sb, ModContent.ItemType<global::BiltonsYoyoMod.Tiles.Placeables.YoyoWardItem>(), new Vector2(x + 28, y + 22), 24f, Color.White);
			Terraria.Utils.DrawBorderString(sb, "Yoyo Ward", new Vector2(x + 46, y + 12), Color.Gold, 1f);
			float subH = PlaceableUI.DrawWrapped(sb, "Supports yoyo allies in radius: townsfolk, night visitors and pets. Never buffs foes or bosses.", new Vector2(x + 46, y + 34), Color.LightGray, 0.75f, pw - 60);
			float cy = y + 34 + subH;
			PlaceableUI.Divider(sb, x + 14, (int)cy + 2, pw - 28, new Color(90, 98, 140, 180));
			cy += 8;
			Tab = PlaceableUI.TabRow(sb, x + 14, (int)cy, pw - 28, new[] { "Upgrades", "Allies" }, Tab);
			cy += 36;
			Rectangle xBtn = new(x + pw - 42, y + 8, 32, 30);
			if (YoyoPanel.Button(sb, xBtn, "X", "Close", false, false, 0.95f))
				Close();
			if (!YoyoWardSystem.Tiles.TryGetValue(OpenTile, out var data))
				data = new WardData();
			if (Tab == 0)
				cy = DrawUpgrades(sb, player, x, pw, cy, data);
			else
				cy = DrawAllies(sb, player, x, pw, cy, data);
			PlaceableUI.Divider(sb, x + 14, (int)cy, pw - 28, new Color(90, 98, 140, 180));
			cy += 6;
			if (!string.IsNullOrEmpty(Message))
				cy += PlaceableUI.DrawWrapped(sb, Message, new Vector2(x + 14, cy), Color.Yellow, 0.8f, pw - 28);
			PlaceableUI.DrawWrapped(sb, "Downgrade refunds 70% of that level's cost. Breaking the block loses upgrades (no refund). Bonuses never stack.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
		}

		private static float DrawUpgrades(SpriteBatch sb, Player player, int x, int pw, float cy, WardData data)
		{
			cy = DrawTrack(sb, player, new Rectangle(x + 14, (int)cy, pw - 28, 92), 0, "Damage", new Color(230, 120, 110), data.Damage, YoyoWardSystem.MaxDamage,
				$"Current: +{YoyoWardSystem.DamagePct(data.Damage):0}% ally damage",
				data.Damage >= YoyoWardSystem.MaxDamage ? "MAX" : NextText(YoyoWardSystem.DamageCost(data.Damage + 1), $"+{YoyoWardSystem.DamagePct(data.Damage + 1):0}%")) + 4;
			cy = DrawTrack(sb, player, new Rectangle(x + 14, (int)cy, pw - 28, 92), 1, "Defense", new Color(140, 180, 230), data.Defense, YoyoWardSystem.MaxDefense,
				$"Current: +{YoyoWardSystem.DefenseBonus(data.Defense)} ally defense",
				data.Defense >= YoyoWardSystem.MaxDefense ? "MAX" : NextText(YoyoWardSystem.DefenseCost(data.Defense + 1), $"+{YoyoWardSystem.DefenseBonus(data.Defense + 1)}")) + 4;
			cy = DrawTrack(sb, player, new Rectangle(x + 14, (int)cy, pw - 28, 92), 2, "Vitality", new Color(140, 230, 170), data.Vitality, YoyoWardSystem.MaxVitality,
				$"Current: +{YoyoWardSystem.LifePct(data.Vitality):0}% ally max life",
				data.Vitality >= YoyoWardSystem.MaxVitality ? "MAX" : NextText(YoyoWardSystem.VitalityCost(data.Vitality + 1), $"+{YoyoWardSystem.LifePct(data.Vitality + 1):0}%")) + 4;
			cy = DrawTrack(sb, player, new Rectangle(x + 14, (int)cy, pw - 28, 92), 3, "Radius", new Color(150, 200, 255), data.Radius, YoyoWardSystem.MaxRadius,
				$"Current: {YoyoWardSystem.RadiusPixels(data.Radius) / 16f:0} tiles",
				data.Radius >= YoyoWardSystem.MaxRadius ? "MAX" : NextText(YoyoWardSystem.RadiusCost(data.Radius + 1), $"{YoyoWardSystem.RadiusPixels(data.Radius + 1) / 16f:0} tiles")) + 4;
			return cy;
		}

		private static float DrawAllies(SpriteBatch sb, Player player, int x, int pw, float cy, WardData data)
		{
			// Affected allies with live HP readout.
			var allies = YoyoWardSystem.GetAffectedAllies(OpenTile, 8);
			Terraria.Utils.DrawBorderString(sb, allies.Count == 0 ? "No allies in radius" : $"Allies sheltered: {allies.Count}", new Vector2(x + 14, cy), allies.Count == 0 ? Color.Gray : Color.LightGreen, 0.9f);
			cy += 26;
			if (allies.Count == 0) {
				cy += PlaceableUI.DrawWrapped(sb, "House yoyo townsfolk, night visitors or pets near this block. Bonuses apply while they stay inside the blue ring.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
			}
			else {
				foreach (var a in allies) {
					Color hpColor = a.life * 2 >= a.lifeMax ? Color.LightGreen : a.life * 4 >= a.lifeMax ? Color.Yellow : Color.OrangeRed;
					cy += PlaceableUI.DrawWrapped(sb, $"{a.name}  {a.life}/{a.lifeMax}", new Vector2(x + 14, cy), hpColor, 0.8f, pw - 28);
				}
			}
			cy += 6;
			int wnet = StationNetwork.WardBonus(OpenTile);
			int wDmg = System.Math.Min(data.Damage + wnet, YoyoWardSystem.MaxDamage);
			int wDef = System.Math.Min(data.Defense + wnet, YoyoWardSystem.MaxDefense);
			int wVit = System.Math.Min(data.Vitality + wnet, YoyoWardSystem.MaxVitality);
			cy += PlaceableUI.DrawWrapped(sb, $"Shelter bonuses: +{YoyoWardSystem.DamagePct(wDmg):0}% dmg, +{YoyoWardSystem.DefenseBonus(wDef)} def, +{YoyoWardSystem.LifePct(wVit):0}% life over {YoyoWardSystem.EffRadiusPx(data, OpenTile) / 16f:0} tiles{(wnet > 0 ? " (meshed)" : "")}.", new Vector2(x + 14, cy), Color.LightGray, 0.8f, pw - 28);
			cy += 4;
			var wp = player.GetModPlayer<WardPlayer>();
			Rectangle ringBtn = new(x + 14, (int)cy, 280, 26);
			if (YoyoPanel.Button(sb, ringBtn, $"Radius ring: {(wp.ShowRing ? "ON" : "OFF")}", "Toggle the bright blue in-world radius ring", wp.ShowRing, false, 0.75f)) {
				wp.ShowRing = !wp.ShowRing;
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			cy += 32;
			return cy;
		}

		private static string NextText((int coins, int mat, int count) cost, string effect)
		{
			Item tmp = new Item();
			tmp.SetDefaults(cost.mat);
			return $"Next: {effect} — {YoyoPanel.FormatCoins(cost.coins)} + {cost.count}x {tmp.Name}";
		}

		private static float DrawTrack(SpriteBatch sb, Player player, Rectangle area, int track, string title, Color accent, int level, int max, string cur, string next)
		{
			sb.Draw(TextureAssets.MagicPixel.Value, area, new Color(34, 52, 44, 220));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(area.X, area.Y, 4, area.Height), accent);
			Terraria.Utils.DrawBorderString(sb, $"{title}: {level}/{max}", new Vector2(area.X + 8, area.Y + 4), Color.White, 0.85f);
			PlaceableUI.Pips(sb, new Vector2(area.X + 8, area.Y + 70), level, max);
			PlaceableUI.DrawTruncated(sb, cur, new Vector2(area.X + 8, area.Y + 24), Color.LightGreen, 0.75f, area.Width - 220);
			PlaceableUI.DrawTruncated(sb, next, new Vector2(area.X + 8, area.Y + 42), Color.LightGray, 0.75f, area.Width - 220);
			Rectangle buy = new(area.Right - 200, area.Y + 6, 94, 26);
			Rectangle down = new(area.Right - 100, area.Y + 6, 94, 26);
			bool maxed = level >= max;
			bool atBase = level <= 0;
			if (YoyoPanel.Button(sb, buy, "Upgrade", maxed ? "Maxed" : next, false, maxed, 0.75f))
				WardActions.BuyTrack(player, OpenTile, track);
			if (YoyoPanel.Button(sb, down, "Downgrade", atBase ? "At base" : "Refund 70% of this level", false, atBase, 0.75f))
				WardActions.DowngradeTrack(player, OpenTile, track);
			return area.Y + area.Height;
		}
	}
}
