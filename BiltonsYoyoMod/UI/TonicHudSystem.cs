using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using BiltonsYoyoMod.Configuration;
using BiltonsYoyoMod.Content.Guidebook;
using BiltonsYoyoMod.Items.Utility;

namespace BiltonsYoyoMod.UI
{
	/// <summary>
	/// Compact right-edge tonic strip. Five rows, one per yoyo tonic, showing
	/// the running buff time and the carried spare count. LMB drinks (when the
	/// dose would not be wasted), RMB opts that tonic in or out of the spool's
	/// auto upkeep. Draw-only: no buffs are granted for free.
	/// </summary>
	public class TonicHudSystem : ModSystem
	{
		private const int RowWidth = 78;
		private const int RowHeight = 40;
		private const int RowGap = 4;
		private const int HeaderHeight = 16;
		private const int ManualWasteWindow = 30 * 60;
		private const int TabWidth = 14;
		private const int TabHeight = 52;

		private static readonly Color Plate = new(8, 14, 22);
		private static readonly Color Brass = new(219, 190, 120);
		private static readonly Color Muted = new(110, 124, 140);

		private static bool prevLeft;
		private static bool prevRight;
		private static float hudAnim;
		private static int peekHold;
		// Set when the tab click tucks the strip away. Without it the cursor
		// still sitting on the tab would instantly count as a hover-peek and
		// the strip could never be closed.
		private static bool suppressPeek;

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Tonic HUD",
					() => {
						DrawHud(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI));
		}

		public override void Unload()
		{
			TonicCatalog.Reset();
			prevLeft = false;
			prevRight = false;
			hudAnim = 0f;
			peekHold = 0;
			suppressPeek = false;
		}

		private static void DrawHud(SpriteBatch sb)
		{
			// Edge detection runs even on hidden frames so reopening the HUD
			// while the mouse is held never fires a phantom click.
			bool left = Main.mouseLeft;
			bool right = Main.mouseRight;
			bool leftPressed = left && !prevLeft;
			bool rightPressed = right && !prevRight;
			prevLeft = left;
			prevRight = right;

			if (Main.gameMenu || Main.dedServ)
				return;
			var config = ModContent.GetInstance<YoyoConfig>();
			if (config != null && !config.TonicHud)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead)
				return;
			if (Main.playerInventory || Main.mapFullscreen)
				return;
			if (YoyoGuidebookUISystem.Open)
				return;
			var spool = player.GetModPlayer<TonicSpoolPlayer>();
			if (spool == null || !spool.SpoolCarried || spool.Mode == TonicMode.Off)
				return;
			var splicer = player.GetModPlayer<StringSplicerPlayer>();
			if (splicer != null && splicer.WheelOpen)
				return;

			var tonics = TonicCatalog.All;
			if (tonics == null || tonics.Length == 0)
				return;

			int stripH = HeaderHeight + tonics.Length * (RowHeight + RowGap) - RowGap;
			int baseX = Math.Max(8, UiViewport.Width - RowWidth - 18);
			// Bottom-right keeps the strip clear of the Dowsing String's
			// screen-edge compass, which naturally parks at right-center.
			int y = Math.Clamp(UiViewport.Height - stripH - 150,
				64, Math.Max(64, UiViewport.Height - stripH - 64));

			// The peek tab is anchored to the screen edge and never moves, so
			// there is always a handle to pull the strip back out.
			Rectangle tab = new(
				Math.Max(8, UiViewport.Width - TabWidth - 2),
				Math.Clamp(y + stripH / 2 - TabHeight / 2, 8,
					Math.Max(8, UiViewport.Height - TabHeight - 8)),
				TabWidth, TabHeight);

			Point mousePoint = UiViewport.MousePoint;
			bool hoverTab = tab.Contains(mousePoint);
			if (!spool.HudCollapsed)
				suppressPeek = false;
			if (!hoverTab)
				suppressPeek = false;
			bool tabPeeks = hoverTab && !suppressPeek;
			Rectangle stripArea = new(baseX, y, RowWidth, stripH);
			// Hovering the tab starts the peek. Once it is out, staying inside
			// the strip keeps it open, so the cursor can travel to a row.
			if (tabPeeks || (peekHold > 0 && stripArea.Contains(mousePoint)))
				peekHold = 14;
			else if (peekHold > 0)
				peekHold--;

			// Hovering the tab peeks the strip out; moving into the strip keeps
			// it open. Clicking the tab pins it open or tucks it away again.
			bool peekOpen = !spool.HudCollapsed || tabPeeks || peekHold > 0;
			float target = peekOpen ? 1f : 0f;
			if (hudAnim < target)
				hudAnim = Math.Min(target, hudAnim + 0.12f);
			else if (hudAnim > target)
				hudAnim = Math.Max(target, hudAnim - 0.12f);
			if (Math.Abs(hudAnim - target) < 0.002f)
				hudAnim = target;
			float ease = hudAnim * hudAnim * (3f - 2f * hudAnim);
			float alpha = ease;
			int slide = (int)Math.Round((1f - ease) * (RowWidth + 24));
			int x = baseX + slide;

			if (hoverTab)
				player.mouseInterface = true;

			if (hoverTab && leftPressed && !Main.blockMouse) {
				Main.mouseLeftRelease = false;
				spool.HudCollapsed = !spool.HudCollapsed;
				if (spool.HudCollapsed) {
					suppressPeek = true;
					peekHold = 0;
				}
				else {
					suppressPeek = false;
					peekHold = 14;
				}
				SoundEngine.PlaySound(SoundID.MenuTick with {
					Volume = 0.34f,
					Pitch = spool.HudCollapsed ? -0.16f : 0.14f,
					MaxInstances = 2,
				}, player.Center);
				// The strip is already sliding; block the same click from
				// reaching a row underneath.
				hoverTab = true;
			}

			int hover = -1;
			if (ease > 0.5f) {
				for (int i = 0; i < tonics.Length; i++) {
					if (RowRect(x, y, i).Contains(mousePoint)) {
						hover = i;
						break;
					}
				}
			}

			if (ease > 0.01f) {
				Terraria.Utils.DrawBorderString(sb,
					"TONICS  " + TonicSpool.ModeName(spool.Mode),
					new Vector2(x, y), Brass * (0.9f * alpha), 0.48f);
				FillRect(sb, new Rectangle(x, y + 12, RowWidth, 1),
					Brass * (0.35f * alpha));

				for (int i = 0; i < tonics.Length; i++) {
					TonicInfo tonic = tonics[i];
					Rectangle row = RowRect(x, y, i);
					bool autoOn = spool.IsTonicEnabled(i);
					int count = player.CountItem(tonic.ItemType);
					int remaining = tonic.Remaining(player);
					bool hovered = hover == i;

					FillRect(sb, row, Plate * ((hovered ? 0.98f : 0.86f) * alpha));
					FillRect(sb, new Rectangle(row.X, row.Y, 2, row.Height),
						tonic.Accent * ((autoOn ? 0.95f : 0.30f) * alpha));
					DrawBorder(sb, row,
						(hovered ? Brass : Muted) * ((autoOn ? 0.85f : 0.40f) * alpha));

					DrawItemIcon(sb, tonic.ItemType,
						new Vector2(row.X + 21, row.Center.Y), 26f,
						(count > 0 ? 1f : 0.32f) * alpha);

					Color nameColor = (autoOn ? tonic.Accent : tonic.Accent * 0.55f) * alpha;
					Terraria.Utils.DrawBorderString(sb, tonic.ShortName,
						new Vector2(row.X + 38, row.Y + 5), nameColor, 0.42f);
					int foodTime = tonic.BuffType == BuffID.WellFed
						? TonicSpoolPlayer.FoodTime(player) : 0;
					string bottom;
					Color bottomColor;
					if (remaining > 0) {
						bottom = TonicSpoolPlayer.TimeText(remaining);
						bottomColor = Color.White * 0.92f;
					}
					else if (foodTime > 0) {
						// A stronger meal is filling the slot Honey would use.
						bottom = "meal";
						bottomColor = Muted * 0.9f;
					}
					else if (count > 0) {
						bottom = $"x{count}";
						bottomColor = new Color(150, 210, 160);
					}
					else {
						bottom = "none";
						bottomColor = Muted * 0.9f;
					}
					Terraria.Utils.DrawBorderString(sb, bottom,
						new Vector2(row.X + 38, row.Y + 19),
						bottomColor * alpha, 0.46f);

					UiArt.DrawDisc(sb, new Vector2(row.Right - 8, row.Y + 8), 2.4f,
						(autoOn ? new Color(130, 220, 150) : new Color(80, 92, 104)) * alpha);
				}
			}

			DrawPeekTab(sb, tab, spool.HudCollapsed, hoverTab);

			if (hover < 0)
				return;

			player.mouseInterface = true;
			DrawHoverCard(sb, player, spool, tonics[hover], hover, x, y);

			if (Main.blockMouse)
				return;
			if (leftPressed) {
				Main.mouseLeftRelease = false;
				spool.DrinkManually(hover);
			}
			if (rightPressed) {
				Main.mouseRightRelease = false;
				spool.ToggleTonic(hover);
			}
		}

		/// <summary>Slim right-edge handle. Three brass beads plus a chevron
		/// hint that the strip can be pulled out; clicking pins it.</summary>
		private static void DrawPeekTab(SpriteBatch sb, Rectangle tab, bool collapsed, bool hovered)
		{
			float a = hovered ? 1f : 0.82f;
			FillRect(sb, tab, Plate * (0.92f * a));
			DrawBorder(sb, tab, (collapsed ? Brass : Muted) * (0.78f * a));
			if (hovered)
				UiArt.DrawGlow(sb, new Vector2(tab.X - 2, tab.Center.Y), 30f,
					Brass * 0.10f);
			// Three beads: vertical when tucked away, pulled toward the edge
			// when pinned open, so the handle reads as a slide direction.
			for (int i = 0; i < 3; i++) {
				float bx = tab.Center.X + (collapsed ? 0f : 2f);
				float by = tab.Center.Y + (i - 1) * 9f;
				UiArt.DrawDisc(sb, new Vector2(bx, by), 2.1f,
					(collapsed ? Brass : Muted) * (0.85f * a));
			}
		}

		private static Rectangle RowRect(int x, int y, int index)
			=> new(x, y + HeaderHeight + index * (RowHeight + RowGap),
				RowWidth, RowHeight);

		private static void DrawHoverCard(SpriteBatch sb, Player player,
			TonicSpoolPlayer spool, TonicInfo tonic, int index, int stripX, int stripY)
		{
			int remaining = tonic.Remaining(player);
			int count = player.CountItem(tonic.ItemType);
			string line1 = tonic.ItemName;
			string line2 = remaining > 0
				? $"Buff {TonicSpoolPlayer.TimeText(remaining)}   Spares {count}"
				: $"No buff   Spares {count}";
			string line3 = spool.IsTonicEnabled(index)
				? "Auto ON  |  LMB drink  |  RMB toggle"
				: "Auto OFF  |  LMB drink  |  RMB toggle";
			string line4;
			if (count <= 0)
				line4 = "No spare carried.";
			else if (tonic.BuffType == BuffID.WellFed)
				line4 = TonicSpoolPlayer.FoodTime(player) > 0
					? "A meal buff is already active."
					: "Fills only an empty food slot.";
			else if (remaining > ManualWasteWindow)
				line4 = "Drink blocked until under 0:30.";
			else
				line4 = "Ready to drink.";

			float textW = Math.Max(Measure(line1, 0.62f),
				Math.Max(Measure(line2, 0.50f),
					Math.Max(Measure(line3, 0.48f), Measure(line4, 0.46f))));
			int cardW = (int)Math.Max(206f, textW + 18f);
			const int cardH = 72;

			Rectangle row = RowRect(stripX, stripY, index);
			int cardX = stripX - cardW - 8;
			if (cardX < 8)
				cardX = stripX + RowWidth + 8;
			int cardY = Math.Clamp(row.Center.Y - cardH / 2, 8,
				Math.Max(8, UiViewport.Height - cardH - 8));
			Rectangle card = new(cardX, cardY, cardW, cardH);

			FillRect(sb, card, Plate * 0.97f);
			DrawBorder(sb, card, Brass * 0.75f);
			FillRect(sb, new Rectangle(card.X, card.Y, card.Width, 2),
				tonic.Accent * 0.9f);

			Terraria.Utils.DrawBorderString(sb, line1,
				new Vector2(card.X + 9, card.Y + 6), tonic.Accent, 0.62f);
			Terraria.Utils.DrawBorderString(sb, line2,
				new Vector2(card.X + 9, card.Y + 24), Color.White * 0.9f, 0.50f);
			Terraria.Utils.DrawBorderString(sb, line3,
				new Vector2(card.X + 9, card.Y + 40), new Color(174, 190, 210), 0.48f);
			Terraria.Utils.DrawBorderString(sb, line4,
				new Vector2(card.X + 9, card.Y + 55), Muted, 0.46f);
		}

		private static float Measure(string text, float scale)
			=> FontAssets.MouseText.Value.MeasureString(text).X * scale;

		private static void DrawItemIcon(SpriteBatch sb, int type, Vector2 center,
			float maxSize, float alpha)
		{
			if (type <= 0 || type >= TextureAssets.Item.Length)
				return;
			try {
				// Vanilla item textures are lazy-loaded. Request the icon so a
				// tonic carried only in the main inventory never draws blank.
				Main.instance.LoadItem(type);
				Texture2D tex = TextureAssets.Item[type].Value;
				if (tex == null)
					return;
				float scale = Math.Min(1f, maxSize / Math.Max(tex.Width, tex.Height));
				sb.Draw(tex, center, null, Color.White * alpha, 0f,
					tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
			}
			catch { }
		}

		private static void DrawBorder(SpriteBatch sb, Rectangle rect, Color color)
		{
			FillRect(sb, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
			FillRect(sb, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
			FillRect(sb, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
			FillRect(sb, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
		}

		private static void FillRect(SpriteBatch sb, Rectangle rect, Color color)
		{
			if (rect.Width <= 0 || rect.Height <= 0)
				return;
			sb.Draw(TextureAssets.MagicPixel.Value, rect,
				new Rectangle(0, 0, 1, 1), color);
		}
	}
}
