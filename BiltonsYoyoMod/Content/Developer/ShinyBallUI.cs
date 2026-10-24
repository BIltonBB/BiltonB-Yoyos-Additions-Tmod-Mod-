using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using YoyoPanel = BiltonsYoyoMod.Content.UI.YoyoNPC.YoyoNPCPanel;

namespace BiltonsYoyoMod.Content.Developer
{
	// Shiny Ball dev panel: mod name/version/credits, Random Yoyo readout,
	// cosmetic Effects toggle, and a hidden ball-click easter egg.
	public class ShinyBallUISystem : ModSystem
	{
		public static bool Open;
		public static int RandomType = -1;
		public static string RandomName = "";
		public static string RandomStats = "";
		public static string RandomMeta = "";
		public static string RandomTooltip = "";
		public static int RandomIndex;
		public static int RandomTotal;
		public static string Message = "";
		public static int MessageTimer;

		private static int ballClicks;
		private static int ballClickTimer;
		private static bool eggFound;

		private static List<int> yoyoTypes;

		public static void OpenPanel()
		{
			Open = true;
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
			if (ballClickTimer > 0 && --ballClickTimer <= 0)
				ballClicks = 0;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead) {
				Close();
				return;
			}
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
				Close();
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Shiny Ball",
					() => {
						DrawPanel(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI));
		}

		private static List<int> YoyoTypes()
		{
			if (yoyoTypes != null)
				return yoyoTypes;
			yoyoTypes = new List<int>();
			try {
				// Every yoyo in the game: vanilla sets, this mod's shapes,
				// and any other mod's true yoyos. Same triple check the
				// chest and the sanitizer use (reversed there), so counts
				// agree everywhere.
				for (int t = 1; t < ItemLoader.ItemCount; t++) {
					Item sample = new();
					try {
						sample.SetDefaults(t);
					}
					catch {
						continue;
					}
					if (sample == null || sample.IsAir || sample.damage <= 0 || sample.accessory)
						continue;
					bool yoyo = false;
					try {
						yoyo = global::BiltonsYoyoMod.Content.NPCs.Town.YoyoTownShared.IsYoyo(sample)
							|| global::BiltonsYoyoMod.Content.NPCs.Town.YoyoTownShared.IsYoyoShape(sample);
					}
					catch { }
					if (!yoyo) {
						try {
							yoyo = global::BiltonsYoyoMod.Content.Storage.YoyoChestSystem.IsYoyo(sample);
						}
						catch { }
					}
					if (yoyo)
						yoyoTypes.Add(t);
				}
			}
			catch { }
			return yoyoTypes;
		}

		private static void DrawPanel(SpriteBatch sb)
		{
			if (!Open)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;
			var mod = ModContent.GetInstance<BiltonsYoyoMod>();
			string version = mod.Version.ToString();
			int pw = System.Math.Min(500, Main.screenWidth - 16);
			int ph = System.Math.Min(530, Main.screenHeight - 16);
			int x = (Main.screenWidth - pw) / 2;
			int y = (Main.screenHeight - ph) / 2;
			Rectangle rect = new(x, y, pw, ph);
			if (rect.Contains(Main.MouseScreen.ToPoint()))
				player.mouseInterface = true;
			YoyoPanel.DrawPanelBackground(sb, rect);
			Placeables.PlaceableUI.HeaderStrip(sb, x, y, pw, new Color(255, 220, 130));
			// Clickable ball icon (easter egg lives here, no tooltip spoiling it).
			Rectangle ballBox = new(x + 14, y + 10, 28, 28);
			YoyoPanel.DrawItemIcon(sb, ModContent.ItemType<Tiles.Developer.ShinyBallDevItem>(), new Vector2(ballBox.X + 14, ballBox.Y + 14), 24f, Color.White);
			if (ballBox.Contains(Main.MouseScreen.ToPoint())) {
				player.mouseInterface = true;
				if (Main.mouseLeft && Main.mouseLeftRelease) {
					Main.mouseLeftRelease = false;
					ballClicks++;
					ballClickTimer = 180;
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
					if (ballClicks >= 7 && !eggFound) {
						eggFound = true;
						ballClicks = 0;
						if (!Main.dedServ) {
							for (int i = 0; i < 40; i++) {
								Dust d = Dust.NewDustDirect(player.Center - new Vector2(8, 8), 16, 16, DustID.RainbowMk2, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 100, default, 1.2f);
								d.noGravity = true;
							}
							Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup);
						}
						Message = "The ball approves. It hums in seven colors. (Easter egg found!)";
						MessageTimer = 600;
					}
				}
			}
			Terraria.Utils.DrawBorderString(sb, "BiltonB Yoyo Additions", new Vector2(x + 50, y + 10), Color.Gold, 1f);
			Terraria.Utils.DrawBorderString(sb, $"v{version} by BiltonB — thanks for spinning!", new Vector2(x + 50, y + 32), Color.LightGray, 0.75f);
			float cy = y + 56;
			Placeables.PlaceableUI.Divider(sb, x + 14, (int)cy, pw - 28, new Color(90, 98, 140, 180));
			cy += 8;
			cy += Placeables.PlaceableUI.DrawWrapped(sb, "Developer block. Random Yoyo picks from this mod and shows its stats. Effects toggles cosmetic yoyo dust. The ball seems to enjoy attention...", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			cy += 6;
			Rectangle randBtn = new(x + 14, (int)cy, 220, 28);
			if (YoyoPanel.Button(sb, randBtn, "Random Yoyo", "Picks a random mod yoyo and shows its full stats below.", false, false, 0.8f)) {
				var list = YoyoTypes();
				if (list.Count == 0) {
					RandomType = -1;
					RandomName = "";
					RandomStats = "";
					RandomMeta = "No yoyos found (reload needed).";
					RandomTooltip = "";
					RandomTotal = 0;
				}
				else {
					int idx = Main.rand.Next(list.Count);
					int pick = list[idx];
					Item s = new();
					try {
						s.SetDefaults(pick);
					}
					catch {
					}
					string nm;
					try {
						nm = Terraria.Lang.GetItemNameValue(pick);
					}
					catch {
						nm = "Unknown Yoyo";
					}
					string projName = "";
					try {
						if (s.shoot > ProjectileID.None)
							projName = Terraria.Lang.GetProjectileName(s.shoot).Value;
					}
					catch { }
					string src = "Terraria";
					try {
						ModItem srcMi = ItemLoader.GetItem(pick);
						if (srcMi != null)
							src = srcMi.Mod?.DisplayName ?? srcMi.Mod?.Name ?? "mods";
					}
					catch { }
					RandomType = pick;
					RandomName = string.IsNullOrEmpty(s.Name) ? nm : s.Name;
					RandomStats = $"{s.damage} dmg • {s.knockBack:0.0} knockback • {s.crit}% crit • use {s.useTime} ({UseLabel(s.useTime)})";
					RandomMeta = $"Shoots: {(string.IsNullOrEmpty(projName) ? "?" : projName)} • Worth: {YoyoPanel.FormatCoins(s.value)} • Max stack {s.maxStack} • From: {src}";
					RandomTooltip = YoyoTooltip(pick);
					RandomIndex = idx + 1;
					RandomTotal = list.Count;
				}
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
			}
			var mp = player.GetModPlayer<ShinyBallPlayer>();
			Rectangle fxBtn = new(x + 242, (int)cy, pw - 28 - 228, 28);
			if (YoyoPanel.Button(sb, fxBtn, $"Effects: {(mp.YoyoEffects ? ShinyBallPlayer.StyleName(mp.EffectStyle) : "OFF")}", "Click for the next dust style.", mp.YoyoEffects, false, 0.75f)) {
				if (!mp.YoyoEffects) {
					mp.YoyoEffects = true;
					mp.EffectStyle = 0;
				}
				else if (mp.EffectStyle < 2) {
					mp.EffectStyle++;
				}
				else {
					mp.YoyoEffects = false;
				}
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
			}
			cy += 34;
			if (RandomType > 0 && !string.IsNullOrEmpty(RandomName)) {
				// Result card: icon + name, wrapped stat lines, ability
				// text (passive/ability/effect from its tooltip), pool
				// position. Everything wrapped and line-capped so the card
				// always fits the panel.
				YoyoPanel.DrawItemIcon(sb, RandomType, new Vector2(x + 28, cy + 12), 24f, Color.White);
				Terraria.Utils.DrawBorderString(sb, $"{RandomName} ({RandomIndex}/{RandomTotal})", new Vector2(x + 46, cy), Color.Gold, 0.85f);
				cy += 24;
				cy += Placeables.PlaceableUI.DrawWrapped(sb, RandomStats, new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
				cy += Placeables.PlaceableUI.DrawWrapped(sb, RandomMeta, new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
				if (!string.IsNullOrEmpty(RandomTooltip))
					cy += Placeables.PlaceableUI.DrawWrappedCapped(sb, RandomTooltip, new Vector2(x + 14, cy), Color.White, 0.7f, pw - 28, 6);
			}
			else if (!string.IsNullOrEmpty(RandomMeta)) {
				cy += Placeables.PlaceableUI.DrawWrapped(sb, RandomMeta, new Vector2(x + 14, cy), Color.Gold, 0.8f, pw - 28);
			}
			Placeables.PlaceableUI.Divider(sb, x + 14, (int)cy, pw - 28, new Color(90, 98, 140, 180));
			cy += 6;
			if (!string.IsNullOrEmpty(Message))
				Placeables.PlaceableUI.DrawWrapped(sb, Message, new Vector2(x + 14, cy), Color.Yellow, 0.8f, pw - 28);
			Rectangle xBtn = new(x + pw - 42, y + 8, 32, 30);
			if (YoyoPanel.Button(sb, xBtn, "X", "Close", false, false, 0.95f))
				Close();
		}

		private static string UseLabel(int useTime) => useTime switch {
			<= 8 => "Insanely Fast",
			<= 12 => "Very Fast",
			<= 16 => "Fast",
			<= 20 => "Average",
			<= 25 => "Slow",
			<= 30 => "Very Slow",
			_ => "Extremely Slow",
		};

		// Ability text for the card: the item's own Tooltip localization
		// (holds the Passive Ability / Ability / Effect lines). Empty when
		// the item defines none — the card then just shows stats.
		private static string YoyoTooltip(int itemType)
		{
			try {
				ModItem mi = ItemLoader.GetItem(itemType);
				return mi?.GetLocalization("Tooltip").Value ?? "";
			}
			catch {
				return "";
			}
		}
	}
}
