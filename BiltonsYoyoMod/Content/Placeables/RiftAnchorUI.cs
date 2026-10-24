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
	// Rift Anchor UI: active/inactive, coords, usability, selection and cooldown.
	public class RiftAnchorUISystem : ModSystem
	{
		public static bool Open;
		public static Point OpenTile;

		public static void OpenPanel(int x, int y)
		{
			Open = true;
			OpenTile = new Point(x, y);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public static void Close()
		{
			Open = false;
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!Open)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead) {
				Close();
				return;
			}
			Vector2 tileWorld = new(OpenTile.X * 16f + 16f, OpenTile.Y * 16f + 24f);
			if (Vector2.Distance(player.Center, tileWorld) > 700f)
				Close();
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
				Close();
			// Anchor outline while configuring (client fx only).
			if (Open && !Main.dedServ && Main.GameUpdateCount % 8 == 0) {
				for (int i = 0; i < 24; i++) {
					float ang = i / 24f * Microsoft.Xna.Framework.MathHelper.TwoPi;
					Vector2 pos = tileWorld + ang.ToRotationVector2() * 44f;
					Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, DustID.PurpleTorch, 0f, 0f, 120, default, 1f);
					d.noGravity = true;
					d.velocity = Vector2.Zero;
				}
			}
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Rift Anchor Panel",
					() => {
						DrawPanel(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI));
		}

		private static string SnapKeyName()
		{
			try {
				var keys = global::BiltonsYoyoMod.Settings.RiftSnapKeybind.RiftSnapKey.GetAssignedKeys(Terraria.GameInput.InputMode.Keyboard);
				if (keys != null && keys.Count > 0)
					return keys[0];
			}
			catch { }
			return "G";
		}

		private static void DrawPanel(SpriteBatch sb)
		{
			if (!Open)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;
			var anchor = player.GetModPlayer<RiftAnchorPlayer>();
			int pw = System.Math.Min(500, Main.screenWidth - 16);
			int ph = System.Math.Min(530, Main.screenHeight - 16);
			int x = (Main.screenWidth - pw) / 2;
			int y = (Main.screenHeight - ph) / 2;
			Rectangle rect = new(x, y, pw, ph);
			if (rect.Contains(Main.MouseScreen.ToPoint()))
				player.mouseInterface = true;
			YoyoPanel.DrawPanelBackground(sb, rect);
			PlaceableUI.HeaderStrip(sb, x, y, pw, new Color(170, 140, 255));
			YoyoPanel.DrawItemIcon(sb, ModContent.ItemType<global::BiltonsYoyoMod.Tiles.Placeables.RiftAnchorItem>(), new Vector2(x + 28, y + 22), 24f, Color.White);
			Terraria.Utils.DrawBorderString(sb, "Rift Anchor", new Vector2(x + 46, y + 12), Color.Gold, 1f);
			float cy = y + 34 + PlaceableUI.DrawWrapped(sb, "Combat positioning device: pick one active anchor, then snap yoyos to it mid-fight.", new Vector2(x + 46, y + 34), Color.LightGray, 0.75f, pw - 60);
			PlaceableUI.Divider(sb, x + 14, (int)cy + 2, pw - 28, new Color(90, 98, 140, 180));
			cy += 8;
			float thisDist = Vector2.Distance(player.Center, RiftAnchorHelpers.AnchorCenter(OpenTile)) / 16f;
			cy += PlaceableUI.DrawWrapped(sb, $"This anchor: ({OpenTile.X}, {OpenTile.Y}) — {thisDist:0} tiles away", new Vector2(x + 14, cy), Color.LightGray, 0.8f, pw - 28);
			bool isActive = anchor.HasActive && anchor.ActiveTile == OpenTile;
			string status = isActive ? "ACTIVE" : "INACTIVE";
			Color statusColor = isActive
				? Color.Lerp(Color.Lime, Color.White, Main.GameUpdateCount % 40 < 20 ? 0f : 0.35f)
				: Color.Gray;
			Terraria.Utils.DrawBorderString(sb, $"Status: {status}", new Vector2(x + 14, cy), statusColor, 0.9f);
			cy += 22;
			string why = "";
			bool usable = RiftAnchorHelpers.IsUsable(player, OpenTile, out why);
			cy += PlaceableUI.DrawWrapped(sb, usable ? "Usable: yes" : $"Usable: no — {why}", new Vector2(x + 14, cy), usable ? Color.LightGreen : Color.Orange, 0.8f, pw - 28);
			if (anchor.HasActive) {
				bool activeValid = RiftAnchorHelpers.IsValidAnchor(anchor.ActiveTile);
				float activeDist = Vector2.Distance(player.Center, RiftAnchorHelpers.AnchorCenter(anchor.ActiveTile)) / 16f;
				string activeLine = activeValid
					? $"Active anchor: ({anchor.ActiveTile.X}, {anchor.ActiveTile.Y}) — {activeDist:0} tiles"
					: $"Active anchor: ({anchor.ActiveTile.X}, {anchor.ActiveTile.Y}) — DESTROYED";
				cy += PlaceableUI.DrawWrapped(sb, activeLine, new Vector2(x + 14, cy), activeValid ? Color.Cyan : Color.Red, 0.8f, pw - 28);
			}
			else {
				cy += PlaceableUI.DrawWrapped(sb, "Active anchor: none", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
			}
			string keyName = SnapKeyName();
			string cd = anchor.SnapCooldown > 0 ? $"Rift Snap cooldown: {anchor.SnapCooldown / 60}s" : $"Rift Snap: READY (press {keyName})";
			Terraria.Utils.DrawBorderString(sb, cd, new Vector2(x + 14, cy), Color.Yellow, 0.8f);
			cy += 22;
			cy += PlaceableUI.DrawWrapped(sb, "Snap: yoyos outrun themselves to the anchor, hold, burst, then resume. 20s cooldown.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			// Burst on arrival (per player, persists).
			Terraria.Utils.DrawBorderString(sb, "Burst on arrival:", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 22;
			float bw2 = (pw - 28 - 8) / 2f;
			Rectangle b0 = new(x + 14, (int)cy, (int)bw2, 28);
			Rectangle b1 = new(x + 14 + (int)bw2 + 8, (int)cy, (int)bw2, 28);
			if (YoyoPanel.Button(sb, b0, "Disrupt", "75% damage + Slow (2s). Holds enemies still.", anchor.BurstMode == 0, false, 0.75f)) {
				anchor.BurstMode = 0;
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			if (YoyoPanel.Button(sb, b1, "Concuss", "60% damage + heavy knockback. No slow.", anchor.BurstMode == 1, false, 0.75f)) {
				anchor.BurstMode = 1;
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			cy += 34;
			// Snap pace (per player, persists).
			Terraria.Utils.DrawBorderString(sb, "Snap pace:", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 22;
			Rectangle p0 = new(x + 14, (int)cy, (int)bw2, 28);
			Rectangle p1 = new(x + 14 + (int)bw2 + 8, (int)cy, (int)bw2, 28);
			if (YoyoPanel.Button(sb, p0, "Steady", "Snap at tuned speed.", anchor.Pace == 0, false, 0.75f)) {
				anchor.Pace = 0;
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			if (YoyoPanel.Button(sb, p1, "Swift", "Snap 25% faster. Same cooldown.", anchor.Pace == 1, false, 0.75f)) {
				anchor.Pace = 1;
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			cy += 34;
			// Anchor hold: how long the yoyo pins itself after the burst.
			Terraria.Utils.DrawBorderString(sb, "Anchor hold:", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 22;
			string[] holds = { "None: burst and release", "Brief: pin ~0.75s", "Long: pin ~1.5s" };
			float hw = (pw - 28 - 12) / 3f;
			for (int h = 0; h < 3; h++) {
				Rectangle b = new(x + 14 + (int)(h * (hw + 6)), (int)cy, (int)hw, 28);
				if (YoyoPanel.Button(sb, b, RiftAnchorPlayer.HoldName(h), holds[h], anchor.HoldMode == h, false, 0.7f)) {
					anchor.HoldMode = h;
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				}
			}
			cy += 34;
			PlaceableUI.Divider(sb, x + 14, (int)cy, pw - 28, new Color(90, 98, 140, 180));
			cy += 8;
			Rectangle select = new(x + 14, (int)cy, 200, 30);
			Rectangle clear = new(x + 222, (int)cy, 120, 30);
			Rectangle xBtn = new(x + pw - 42, y + 8, 32, 30);
			if (YoyoPanel.Button(sb, xBtn, "X", "Close", false, false, 0.95f))
				Close();
			if (YoyoPanel.Button(sb, select, isActive ? "Selected" : "Set Active", isActive ? "Already active" : "Only one anchor active per player; replaces previous", false, isActive || !usable, 0.85f)) {
				anchor.SetActive(OpenTile);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			if (YoyoPanel.Button(sb, clear, "Deactivate", "Clear active anchor", false, !anchor.HasActive, 0.85f)) {
				anchor.ClearActive();
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
		}
	}
}
