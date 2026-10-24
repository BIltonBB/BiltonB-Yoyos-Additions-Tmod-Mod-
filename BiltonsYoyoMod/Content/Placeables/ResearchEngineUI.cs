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
	// Research Engine configuration UI: charge/mode/effect, persists on close.
	public class ResearchEngineUISystem : ModSystem
	{
		public static bool Open;
		public static Point OpenTile;
		public static string Message = "";
		public static int MessageTimer;
		public static int Tab; // 0 Config, 1 Tech, 2 Research, 3 Network, 4 Trials, 5 Loadout

		// Paged tab-row scroll offset (#2). Reset on open.
		private static int tabOffset;
		// Tech mini-tabs (#3): 0 = ALL branches, 1+ = one lane each.
		// techMiniApplied tracks the last camera refit so switching minis
		// deterministically reframes (never a random jump). Reset on open.
		private static int techMini;
		private static int techMiniApplied = -1;
		private static int techMiniOffset;

		// Salvage converter selection (#10). Reset on open.
		private static int salvageMat;
		private static int salvageAmt = 1;

		// Tech canvas camera (drag to pan). Reset on open.
		private static float camX;
		private static float camY;
		private static bool dragging;
		private static Vector2 downMouse;
		private static float downCamX;
		private static float downCamY;
		private static bool dragMoved;

		public static void OpenPanel(int x, int y)
		{
			Open = true;
			OpenTile = new Point(x, y);
			Message = "";
			MessageTimer = 0;
			Tab = 0;
			tabOffset = 0;
			techMini = 0;
			techMiniApplied = -1;
			techMiniOffset = 0;
			salvageMat = 0;
			salvageAmt = 1;
			camX = 30f;
			camY = 130f;
			dragging = false;
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		public static void Close()
		{
			Open = false;
			dragging = false;
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
			Vector2 tileWorld = new(OpenTile.X * 16f + 24f, OpenTile.Y * 16f + 16f);
			if (Vector2.Distance(player.Center, tileWorld) > 700f)
				Close();
			if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
				Close();
			// Heat shimmer outline while configuring (client fx only).
			if (Open && !Main.dedServ && Main.GameUpdateCount % 8 == 0) {
				for (int i = 0; i < 28; i++) {
					float ang = i / 28f * Microsoft.Xna.Framework.MathHelper.TwoPi;
					Vector2 pos = tileWorld + new Vector2(ang.ToRotationVector2().X * 34f, ang.ToRotationVector2().Y * 24f);
					Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, DustID.Torch, 0f, -0.4f, 120, default, 1f);
					d.noGravity = true;
				}
			}
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Research Panel",
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
			// Never create storage from a draw call: a destroyed block must
			// not leave ghost charge entries behind.
			bool exists = ResearchEngineSystem.Tiles.TryGetValue(OpenTile, out var data);
			if (!exists || !ResearchEngineSystem.IsTileStillEngine(OpenTile))
				data = new ResearchData { Mode = 1, Effect = 0 };
			int pw = System.Math.Min(560, Main.screenWidth - 16);
			int ph = System.Math.Min(660, Main.screenHeight - 16);
			int x = (Main.screenWidth - pw) / 2;
			int y = (Main.screenHeight - ph) / 2;
			Rectangle rect = new(x, y, pw, ph);
			if (rect.Contains(Main.MouseScreen.ToPoint()))
				player.mouseInterface = true;
			YoyoPanel.DrawPanelBackground(sb, rect);
			PlaceableUI.HeaderStrip(sb, x, y, pw, new Color(230, 160, 90));
			YoyoPanel.DrawItemIcon(sb, ModContent.ItemType<global::BiltonsYoyoMod.Tiles.Placeables.ResearchEngineItem>(), new Vector2(x + 28, y + 22), 24f, Color.White);
			Terraria.Utils.DrawBorderString(sb, "Research Engine", new Vector2(x + 46, y + 12), Color.Gold, 1f);
			float cyTop = y + 34 + PlaceableUI.DrawWrapped(sb, "Farm charge off this block, then cash it in. Research points fuel the lab; the Tech tree holds the wild stuff.", new Vector2(x + 46, y + 34), Color.LightGray, 0.75f, pw - 60);
			PlaceableUI.Divider(sb, x + 14, (int)cyTop + 2, pw - 28, new Color(90, 98, 140, 180));
			cyTop += 8;
			Tab = PlaceableUI.TabRowPaged(sb, x + 14, (int)cyTop, pw - 28, new[] { "Config", "Tech", "Research", "Network", "Trials", "Loadout" }, Tab, ref tabOffset);
			float cy = cyTop + 36;
			if (Tab == 1) {
				cy = DrawTech(sb, player, x, y, pw, ph, cy, data);
			}
			else if (Tab == 2) {
				cy = DrawResearch(sb, player, x, pw, cy, data);
			}
			else if (Tab == 3) {
				cy = DrawNetwork(sb, player, x, pw, cy, data);
			}
			else if (Tab == 4) {
				cy = DrawTrials(sb, player, x, pw, cy, data);
			}
			else if (Tab == 5) {
				cy = DrawLoadout(sb, player, x, pw, cy, data);
			}
			else {
			int chargeMax = ResearchEngineSystem.MaxChargeOf(data);
			bool coils = ResearchTech.TechOn(data, ResearchTech.ChargeCoils);
			bool full = coils && data.Charge >= chargeMax;
			string chargeLine = !coils
				? $"Charge: LOCKED — research Kinetic Coils in the Tech tab to charge by hitting this block."
				: full
				? $"Charge: FULL {data.Charge}/{chargeMax} — next yoyo hit releases {ResearchEngineSystem.EffectName(data.Effect)}!"
				: $"Charge: {data.Charge}/{chargeMax} — strike this block with a yoyo (+50s flight per hit).";
			Color chargeColor = !coils
				? Color.Gray
				: full
				? (Main.GameUpdateCount % 30 < 15 ? Color.Gold : Color.Orange)
				: Color.LightGray;
			cy += PlaceableUI.DrawWrapped(sb, chargeLine, new Vector2(x + 14, cy), chargeColor, 0.8f, pw - 28);
			cy += 4;
			// Charge bar.
			Rectangle bar = new(x + 14, (int)cy, pw - 28, 16);
			sb.Draw(TextureAssets.MagicPixel.Value, bar, new Color(40, 40, 50, 220));
			int fill = (int)(bar.Width * (data.Charge / (float)System.Math.Max(1, chargeMax)));
			Color fillColor = full
				? Color.Lerp(Color.Gold, Color.White, Main.GameUpdateCount % 30 < 15 ? 0f : 0.45f)
				: new Color(230, 160, 90, 230);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bar.X, bar.Y, fill, bar.Height), fillColor);
			// Segment ticks every 25 charge.
			for (int s = 1; s < 4; s++) {
				int sx = bar.X + bar.Width * s / 4;
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(sx, bar.Y, 1, bar.Height), new Color(10, 10, 14, 200));
			}
			// Barber-pole shimmer while charge is held: the bar feels alive.
			if (fill > 12) {
				int stripe = bar.X + (int)(Main.GameUpdateCount * 2 % (bar.Width + 24)) - 12;
				int sx0 = System.Math.Max(stripe, bar.X);
				int sx1 = System.Math.Min(stripe + 6, bar.X + fill);
				if (sx1 > sx0)
					sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(sx0, bar.Y, sx1 - sx0, bar.Height), new Color(255, 255, 255, 70));
			}
			cy = bar.Y + bar.Height + 8;
			// Points readout (discoverability): gains start at 1 tech.
			cy += PlaceableUI.DrawWrapped(sb, $"Points: {data.Points}/{ResearchPoints.CapOf(data)} (+{ResearchPoints.PassivePerTrickle(data)}/10s) — earn & spend in the Research tab.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			Terraria.Utils.DrawBorderString(sb, "Charge mode:", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 24;
			string[] modes = { "Rapid (+3/hit, 0.6x release)", "Balanced (+2/hit, 1.0x)", "Overload (+1/hit, 1.5x)" };
			float mw = (pw - 28 - 12) / 3f;
			for (int m = 0; m < 3; m++) {
				Rectangle b = new(x + 14 + (int)(m * (mw + 6)), (int)cy, (int)mw, 28);
				if (YoyoPanel.Button(sb, b, ResearchEngineSystem.ModeName(m), coils ? modes[m] : "Locked: research Kinetic Coils in the Tech tab.", data.Mode == m, !coils, 0.75f)) {
					if (Main.netMode == NetmodeID.MultiplayerClient)
						PlaceableNet.SendResearchConfig(OpenTile, m, data.Effect, data.AutoRelease, data.Rebound);
					else
						ResearchEngineSystem.SetMode(OpenTile, m);
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				}
			}
			cy += 34;
			cy += 6;
			Terraria.Utils.DrawBorderString(sb, "Release effect (nearest ready engine only):", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 24;
			float ew = (pw - 28 - 18) / 4f;
			for (int e = 0; e < 4; e++) {
				Rectangle b = new(x + 14 + (int)(e * (ew + 6)), (int)cy, (int)ew, 28);
				if (YoyoPanel.Button(sb, b, ResearchEngineSystem.EffectName(e), ResearchEngineSystem.EffectTip(e), data.Effect == e, false, 0.7f)) {
					if (Main.netMode == NetmodeID.MultiplayerClient)
						PlaceableNet.SendResearchConfig(OpenTile, data.Mode, e, data.AutoRelease, data.Rebound);
					else
						ResearchEngineSystem.SetEffect(OpenTile, e);
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				}
			}
			cy += 34;
			cy += PlaceableUI.DrawWrapped(sb, ResearchEngineSystem.EffectTip(data.Effect), new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			cy += 6;
			// Release style: one big hit, a metered drip, or heavy gulps.
			Terraria.Utils.DrawBorderString(sb, "Release style:", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 22;
			float sw = (pw - 28 - 12) / 3f;
			string[] styles = { "One Hit", "Over Time", "Surge" };
			string[] styleTips = {
				"Spend it all at once, full power.",
				"Drip ~10 charge per hit at reduced power. Research in Tech.",
				"Big 25-charge gulps at strong power. Research in Tech.",
			};
			for (int s = 0; s < 3; s++) {
				Rectangle b = new(x + 14 + (int)(s * (sw + 6)), (int)cy, (int)sw, 28);
				bool locked = (s == 1 && !data.HasTech(ResearchTech.StyleOver)) || (s == 2 && !data.HasTech(ResearchTech.StyleSurge));
				if (YoyoPanel.Button(sb, b, styles[s], locked ? $"Locked: research {styles[s]} in the Tech tab." : styleTips[s], data.Style == s, locked, 0.7f)) {
					if (Main.netMode == NetmodeID.MultiplayerClient)
						PlaceableNet.SendResearchStyle(OpenTile, s);
					else
						ResearchEngineSystem.SetStyle(OpenTile, s);
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				}
			}
			cy += 34;
			// Release control: hold a full charge or vent it with no effect.
			Rectangle autoBtn = new(x + 14, (int)cy, 240, 28);
			Rectangle ventBtn = new(x + 262, (int)cy, 150, 28);
			if (YoyoPanel.Button(sb, autoBtn, $"Auto-release: {(data.AutoRelease ? "ON" : "OFF")}", data.AutoRelease ? "Full charge fires on the next yoyo hit" : "Full charge holds until released or vented", data.AutoRelease, false, 0.75f)) {
				bool next = !data.AutoRelease;
				if (Main.netMode == NetmodeID.MultiplayerClient)
					PlaceableNet.SendResearchConfig(OpenTile, data.Mode, data.Effect, next, data.Rebound);
				else
					ResearchEngineSystem.SetAuto(OpenTile, next);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			}
			if (YoyoPanel.Button(sb, ventBtn, "Vent Charge", "Dump stored charge with no effect", false, data.Charge <= 0, 0.75f)) {
				if (Main.netMode == NetmodeID.MultiplayerClient)
					PlaceableNet.SendResearchVent(OpenTile);
				else
					ResearchEngineSystem.VentCharge(OpenTile);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, player.Center);
			}
			cy += 34;
			// Rebound feel: how hard the block kicks the yoyo back.
			Terraria.Utils.DrawBorderString(sb, "Rebound:", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 22;
			for (int rb = 0; rb < 3; rb++) {
				Rectangle b = new(x + 14 + (int)(rb * (mw + 6)), (int)cy, (int)mw, 28);
				if (YoyoPanel.Button(sb, b, ResearchEngineSystem.ReboundName(rb), ResearchEngineSystem.ReboundTip(rb), data.Rebound == rb, false, 0.75f)) {
					if (Main.netMode == NetmodeID.MultiplayerClient)
						PlaceableNet.SendResearchConfig(OpenTile, data.Mode, data.Effect, data.AutoRelease, rb);
					else
						ResearchEngineSystem.SetRebound(OpenTile, rb);
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				}
			}
			cy += 34;
			} // end Config tab
			PlaceableUI.Divider(sb, x + 14, (int)cy, pw - 28, new Color(90, 98, 140, 180));
			cy += 6;
			if (!string.IsNullOrEmpty(Message))
				cy += PlaceableUI.DrawWrapped(sb, Message, new Vector2(x + 14, cy), Color.Yellow, 0.8f, pw - 28);
			PlaceableUI.DrawWrapped(sb, "Breaking the block loses charge, config, tech and points. Config persists when the UI closes.", new Vector2(x + 14, cy), Color.Gray, 0.7f, pw - 28);
			Rectangle xBtn = new(x + pw - 42, y + 8, 32, 30);
			if (YoyoPanel.Button(sb, xBtn, "X", "Close", false, false, 0.95f))
				Close();
		}

		// Research (#2): the block's own points pool. Completing research
		// grants points scaled by research length, passive study trickles
		// points scaled by owned techs, and points rush active jobs.
		// Storage is capped; the cap upgrades here. Later systems (field
		// research, salvaging, experimental) feed this same pool.
		private static float DrawResearch(SpriteBatch sb, Player player, int x, int pw, float cy, ResearchData data)
		{
			int cap = ResearchPoints.CapOf(data);
			cy += PlaceableUI.DrawWrapped(sb, "Research points live in this block. Earn them by researching, spend them to rush jobs.", new Vector2(x + 14, cy), Color.LightGray, 0.8f, pw - 28);
			cy += 6;
			Terraria.Utils.DrawBorderString(sb, $"Research Points: {data.Points}/{cap}", new Vector2(x + 14, cy), Color.Gold, 0.85f);
			cy += 24;
			// Points bar (teal, distinct from the orange charge bar).
			Rectangle bar = new(x + 14, (int)cy, pw - 28, 16);
			sb.Draw(TextureAssets.MagicPixel.Value, bar, new Color(40, 40, 50, 220));
			int fill = cap > 0 ? (int)(bar.Width * System.Math.Clamp(data.Points / (float)cap, 0f, 1f)) : 0;
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bar.X, bar.Y, fill, bar.Height), new Color(90, 200, 190, 230));
			cy = bar.Y + bar.Height + 8;
			// Storage upgrade (repeatable to Lv 8, escalating cost).
			if (data.PointsUp >= ResearchPoints.MaxUpgrades) {
				cy += PlaceableUI.DrawWrapped(sb, $"Storage maxed (Lv {ResearchPoints.MaxUpgrades}): {cap} cap.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
				cy += 6;
			}
			else {
				Rectangle upBtn = new(x + 14, (int)cy, 300, 28);
				string upLabel = $"Storage Lv {data.PointsUp} -> {data.PointsUp + 1} (+{ResearchPoints.CapPerUpgrade})";
				if (YoyoPanel.Button(sb, upBtn, upLabel, $"Raises this block's points cap to {cap + ResearchPoints.CapPerUpgrade}.\nCost: {ResearchPoints.CapCostText(data)}", false, false, 0.75f))
					ResearchPoints.BuyCapUpgrade(player, OpenTile);
				cy += 34;
			}
			// Energize (#16 sink): spend points straight into charge.
			Rectangle nrgBtn = new(x + 14, (int)cy, 300, 28);
			if (YoyoPanel.Button(sb, nrgBtn, $"Energize +{ResearchPoints.EnergizeCharge} ({ResearchPoints.EnergizeCost} pts)", $"Burns {ResearchPoints.EnergizeCost} points into +{ResearchPoints.EnergizeCharge} charge.", false, data.Points < ResearchPoints.EnergizeCost, 0.75f))
				ResearchPoints.Energize(player, OpenTile);
			cy += 34;
			Terraria.Utils.DrawBorderString(sb, "Active gains:", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 24;
			float mult = ResearchPoints.AwardMult(data) * ResearchTech.ScalingMult(data);
			int lo = (int)(30 * mult);
			int hi = (int)(120 * mult);
			cy += PlaceableUI.DrawWrapped(sb, $"> Completed research: +{lo}-{hi} pts each — bigger research pays more.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			int trickle = ResearchPoints.PassivePerTrickle(data);
			if (trickle > 0)
				cy += PlaceableUI.DrawWrapped(sb, $"> Passive study ({data.TechCount} techs): +{trickle} pts every 10s.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			else
				cy += PlaceableUI.DrawWrapped(sb, "> Passive study: research 1+ techs to start a 10s trickle.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			cy += PlaceableUI.DrawWrapped(sb, $"> Rush: {ResearchPoints.RushCostOf(data)} pts -> -30s on a job.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			cy += PlaceableUI.DrawWrapped(sb, "Spend points on: rush jobs, trial Rush Orders, Fresh Leads, energize.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
		cy += PlaceableUI.DrawWrapped(sb, $"Earned total: {data.LifetimePoints} pts.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			cy += PlaceableUI.DrawWrapped(sb, $"Research scaling: +{(ResearchTech.ScalingMult(data) - 1f) * 100f:0.0}% from {data.TechCount} techs.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			if (string.IsNullOrEmpty(data.ActiveEventId) || !ResearchTech.TechOn(data, ResearchTech.EvSig))
				cy += PlaceableUI.DrawWrapped(sb, "Current event: calm. Nothing stirs... for now.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			else
				cy += PlaceableUI.DrawWrapped(sb, $"Current event: {ResearchEvents.NameOf(data.ActiveEventId)} ({ResearchEvents.DescOf(data.ActiveEventId)}, {System.Math.Max(0, data.EventLeft / 60)}s left).", new Vector2(x + 14, cy), Color.Gold, 0.75f, pw - 28);
			string field = FieldResearch.ActiveSources(data);
			if (!string.IsNullOrEmpty(field))
				cy += PlaceableUI.DrawWrapped(sb, $"> Field research: {field}.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			else
				cy += PlaceableUI.DrawWrapped(sb, "Locked: research Combat Logs (Tech tab) for field reports.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			// Salvage converter (#10): resource cycle + up/down/double stepper.
			if (ResearchTech.TechOn(data, ResearchTech.Salvage)) {
				var acc = TechSalvage.Accepted(data);
				if (acc.Count > 0) {
					salvageMat = System.Math.Clamp(salvageMat, 0, acc.Count - 1);
					int maxU = TechSalvage.MaxUnits(data);
					salvageAmt = System.Math.Clamp(salvageAmt, 1, maxU);
					int mtype = acc[salvageMat].type;
					int mval = acc[salvageMat].value;
					int owned = Content.NPCs.Town.YoyoTownShared.CountInInventory(player, mtype);
					Terraria.Utils.DrawBorderString(sb, "Salvage:", new Vector2(x + 14, cy), Color.White, 0.85f);
					cy += 24;
					Rectangle matPrev = new(x + 14, (int)cy, 30, 28);
					if (YoyoPanel.Button(sb, matPrev, "<", "Previous resource", false, false, 0.9f))
						salvageMat = (salvageMat - 1 + acc.Count) % acc.Count;
					Rectangle matNext = new(x + pw - 44, (int)cy, 30, 28);
					if (YoyoPanel.Button(sb, matNext, ">", "Next resource", false, false, 0.9f))
						salvageMat = (salvageMat + 1) % acc.Count;
					Terraria.Utils.DrawBorderString(sb, $"{TechSalvage.MatName(mtype)} x{owned} ({mval} pt/u)", new Vector2(x + 52, cy + 5), Color.LightGray, 0.8f);
					cy += 34;
					Rectangle minus = new(x + 14, (int)cy, 44, 28);
					if (YoyoPanel.Button(sb, minus, "-1", "One less unit", false, salvageAmt <= 1, 0.8f))
						salvageAmt = System.Math.Max(1, salvageAmt - 1);
					Rectangle plus = new(x + 64, (int)cy, 44, 28);
					if (YoyoPanel.Button(sb, plus, "+1", "One more unit", false, salvageAmt >= maxU, 0.8f))
						salvageAmt = System.Math.Min(maxU, salvageAmt + 1);
					Rectangle dblUp = new(x + 114, (int)cy, 50, 28);
					if (YoyoPanel.Button(sb, dblUp, "x2", "Double the units", false, salvageAmt >= maxU, 0.8f))
						salvageAmt = System.Math.Min(maxU, salvageAmt * 2);
					Rectangle dblDn = new(x + 170, (int)cy, 50, 28);
					if (YoyoPanel.Button(sb, dblDn, "/2", "Halve the units", false, salvageAmt <= 1, 0.8f))
						salvageAmt = System.Math.Max(1, salvageAmt / 2);
					Terraria.Utils.DrawBorderString(sb, $"x{salvageAmt}", new Vector2(x + 228, cy + 5), Color.White, 0.85f);
					int preview = TechSalvage.Preview(data, mtype, salvageAmt);
					Rectangle goBtn = new(x + pw - 200, (int)cy, 186, 28);
					if (YoyoPanel.Button(sb, goBtn, $"Salvage -> {preview} pts", $"Burns {salvageAmt}x {TechSalvage.MatName(mtype)} into research points.", false, false, 0.75f))
						TechSalvage.Salvage(player, OpenTile, mtype, salvageAmt);
					cy += 34;
				}
			}
			else {
				// #13: locked converters say so instead of hiding.
				cy += PlaceableUI.DrawWrapped(sb, "Locked: research Scrap Salvage (Tech tab) to salvage.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
				cy += 6;
			}
			cy += 6;
			// Active jobs with rush buttons (Loadout keeps the cancel buttons).
			if (data.Research != null) {
				foreach (var job in new System.Collections.Generic.List<ResearchJob>(data.Research)) {
					var cur = ResearchTech.ById(job.Id);
					if (cur == null)
						continue;
					PlaceableUI.DrawTruncated(sb, $"{cur.Name} — {System.Math.Max(0, job.Left / 60)}s left", new Vector2(x + 14, cy), Color.Gold, 0.8f, pw - 200);
					Rectangle rush = new(x + pw - 170, (int)cy - 3, 156, 24);
					int rushCost = ResearchPoints.RushCostOf(data);
					if (YoyoPanel.Button(sb, rush, $"Rush ({rushCost})", $"Spends {rushCost} points to shave 30s off {cur.Name}.", false, data.Points < rushCost, 0.75f))
						ResearchPoints.RushJob(player, OpenTile, job.Id);
					cy += 28;
				}
			}
			if (data.Research == null || data.Research.Count == 0)
				cy += PlaceableUI.DrawWrapped(sb, "No active research — start one in the Tech tab.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
			// Automation queue (#4): paid entries waiting for a free bay.
			int qslots = ResearchTech.QueueSlots(data);
			int qcount = data.Queue?.Count ?? 0;
			cy += 4;
			if (qslots <= 0 && qcount <= 0) {
				cy += PlaceableUI.DrawWrapped(sb, "No queue — research automation in the Tech tab to queue.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
			}
			else {
				Terraria.Utils.DrawBorderString(sb, $"Queue ({qcount}/{qslots}):", new Vector2(x + 14, cy), Color.White, 0.85f);
				cy += 24;
				if (data.Queue != null) {
					for (int qi = 0; qi < data.Queue.Count; qi++) {
						var qn = ResearchTech.ById(data.Queue[qi]);
						if (qn == null)
							continue;
						PlaceableUI.DrawTruncated(sb, $"#{qi + 1} {qn.Name} — paid, waits for a bay", new Vector2(x + 14, cy), Color.LightGray, 0.8f, pw - 200);
						Rectangle unq = new(x + pw - 170, (int)cy - 3, 156, 24);
						string qid = data.Queue[qi];
						if (YoyoPanel.Button(sb, unq, "Cancel (refund)", "Dequeues this research, refunds everything paid.", false, false, 0.75f))
							ResearchTech.CancelResearch(player, OpenTile, qid);
						cy += 28;
					}
				}
				if (qcount == 0)
					cy += PlaceableUI.DrawWrapped(sb, "Queue empty — fill all bays, then research more.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
			}
			// Backup (#15): a saved engine blueprint restores onto a fresh
			// engine. Breaking without Tech Saving wipes everything.
			var bp = player.GetModPlayer<ResearchEnginePlayer>();
			cy += 4;
			if (bp.HasBackup && bp.BpTech.Count > 0) {
				cy += PlaceableUI.DrawWrapped(sb, $"Backup: {bp.BpTech.Count} techs, {bp.BpPoints} pts. Restore needs a fresh engine.", new Vector2(x + 14, cy), Color.LightGray, 0.8f, pw - 28);
				Rectangle restBtn = new(x + 14, (int)cy, 240, 28);
				if (YoyoPanel.Button(sb, restBtn, "Restore backup", "Applies the saved backup to this engine (must be fresh). Consumes the backup.", false, false, 0.8f))
					TechSaving.Restore(player, OpenTile);
				cy += 34;
			}
			else {
				cy += PlaceableUI.DrawWrapped(sb, "No backup. Breaking a Tech-Saving engine saves one.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
			}
			return cy;
		}

		// Network (#6): engine pairing (up to 2 share all tech) plus the
		// nearby yoyo stations improving this engine in their own ways.
		// Linked engines share TECH only; jobs, queue, points, charge and
		// config stay per-engine. A destroyed engine never takes shared
		// research away; Failover additionally passes points + queue on.
		private static float DrawNetwork(SpriteBatch sb, Player player, int x, int pw, float cy, ResearchData data)
		{
			bool hasUplink = ResearchTech.TechOn(data, ResearchTech.NetUp);
			bool linked = ResearchNetwork.IsLinked(data);
			bool partnerLive = ResearchNetwork.PartnerLive(OpenTile, data);
			cy += PlaceableUI.DrawWrapped(sb, "Link up to 2 engines to share all research. Nearby stations boost this engine.", new Vector2(x + 14, cy), Color.LightGray, 0.8f, pw - 28);
			cy += 6;
			if (linked) {
				Point p = ResearchNetwork.PartnerOf(data);
				Color sigColor = partnerLive ? Color.Lime : Color.Orange;
				cy += PlaceableUI.DrawWrapped(sb, $"Linked to engine ({p.X}, {p.Y}) — signal: {(partnerLive ? "strong" : "LOST (partner gone)")}.", new Vector2(x + 14, cy), sigColor, 0.8f, pw - 28);
				cy += 4;
				cy += PlaceableUI.DrawWrapped(sb, $"Shared tech: {data.TechCount} (both engines hold everything).", new Vector2(x + 14, cy), Color.LightGray, 0.8f, pw - 28);
				if (partnerLive && ResearchTech.TechOn(data, ResearchTech.NetRes))
					cy += PlaceableUI.DrawWrapped(sb, "Link bonus: +10% research speed (Resonant Study).", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
				Rectangle unBtn = new(x + 14, (int)cy, 200, 28);
				if (YoyoPanel.Button(sb, unBtn, "Unlink", "Breaks the pair. Each engine keeps its research.", false, false, 0.8f))
					ResearchNetwork.Unlink(player, OpenTile);
				cy += 34;
			}
			else if (hasUplink) {
				cy += PlaceableUI.DrawWrapped(sb, "Not linked. Linking is free and shares all tech both ways.", new Vector2(x + 14, cy), Color.LightGray, 0.8f, pw - 28);
				Rectangle linkBtn = new(x + 14, (int)cy, 300, 28);
				if (YoyoPanel.Button(sb, linkBtn, $"Link nearest (<={ResearchNetwork.LinkRangeOf(data)} tiles)", "Pairs with the nearest free engine in range. Shares all tech.", false, false, 0.75f))
					ResearchNetwork.TryLink(player, OpenTile);
				cy += 34;
			}
			else {
				cy += PlaceableUI.DrawWrapped(sb, "Not linked. Research Uplink (Tech tab) to link engines.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
				cy += 6;
			}
			Terraria.Utils.DrawBorderString(sb, $"Nearby stations ({ResearchNetwork.StationRadius} tiles):", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 24;
			bool syn = ResearchTech.TechOn(data, ResearchTech.NetSyn);
			if (data.NbRes <= 0 && data.NbWard <= 0 && data.NbRift <= 0) {
				cy += PlaceableUI.DrawWrapped(sb, "No stations in range.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
			}
			else {
				if (data.NbRes > 0) {
					int pct = (int)(ResearchNetwork.StationRateBonus(data) * 100f);
					cy += PlaceableUI.DrawWrapped(sb, $"> Resonator x{data.NbRes}: +{pct}% research speed.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
				}
				if (data.NbWard > 0)
					cy += PlaceableUI.DrawWrapped(sb, $"> Ward x{data.NbWard}: +{ResearchNetwork.ChargeBonus(data)} charge per hit.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
				if (data.NbRift > 0) {
					int pct = (int)((ResearchNetwork.ReleasePowerMult(data) - 1f) * 100f);
					cy += PlaceableUI.DrawWrapped(sb, $"> Rift Anchor x{data.NbRift}: +{pct}% release power.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
				}
				if (syn)
					cy += PlaceableUI.DrawWrapped(sb, "Station Synergy x2 active.", new Vector2(x + 14, cy), Color.Gold, 0.75f, pw - 28);
			}
			if (ResearchTech.TechOn(data, ResearchTech.NetFail))
				cy += PlaceableUI.DrawWrapped(sb, "Failover: the survivor inherits points + queue if its partner is destroyed.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			// Station networking (#12, expanded): this engine's mesh range
			// (merged across a linked pair) plus the buffed stations
			// across the world (each kind has its own buff).
			int mesh = StationNetwork.CoverRange(OpenTile);
			int own = StationNetwork.MeshRangeOf(data);
			cy += 4;
			if (mesh > 0)
				cy += PlaceableUI.DrawWrapped(sb, $"Station mesh range: {mesh} tiles{(mesh > own ? " (linked)" : "")}.", new Vector2(x + 14, cy), Color.White, 0.85f, pw - 28);
			else
				cy += PlaceableUI.DrawWrapped(sb, "No station mesh. Research Local Mesh (Tech tab) to buff nearby stations.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
			int aegis = StationNetwork.AegisCoverRange(OpenTile);
			if (aegis > 0)
				cy += PlaceableUI.DrawWrapped(sb, $"Aegis Mesh range: {aegis} tiles (defensive traps only).", new Vector2(x + 14, cy), Color.White, 0.85f, pw - 28);
			else
				cy += PlaceableUI.DrawWrapped(sb, "No Aegis Mesh. Research Aegis Mesh (Tech AEGIS lane) for Sentry/Link/Tripwire.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
			// Trap monitor (read-only kill board): every Aegis-covered defensive
			// trap ranked by lifetime kills. Max 5 lines so the tab never
			// overflows; the rest are counted.
			cy += 4;
			Terraria.Utils.DrawBorderString(sb, "Aegis kill board:", new Vector2(x + 14, cy), Color.White, 0.85f);
			cy += 24;
			{
				var board = new System.Collections.Generic.List<(Point p, string line, int kills)>();
				foreach (var kv in global::BiltonsYoyoMod.Traps.TrapSystem.Traps) {
					if (!global::BiltonsYoyoMod.Traps.TrapSystem.IsAnyTrapTile(kv.Key))
						continue;
					var ts = kv.Value;
					if (ts.Kind != global::BiltonsYoyoMod.Traps.TrapKind.Sentry && ts.Kind != global::BiltonsYoyoMod.Traps.TrapKind.Link && ts.Kind != global::BiltonsYoyoMod.Traps.TrapKind.Tripwire)
						continue;
					if (!StationNetwork.AegisCovers(kv.Key))
						continue;
					string tname = ts.Kind == global::BiltonsYoyoMod.Traps.TrapKind.Sentry ? "Sentry" : ts.Kind == global::BiltonsYoyoMod.Traps.TrapKind.Link ? "Link" : "Trip";
					string who = string.IsNullOrEmpty(ts.Name) ? $"({kv.Key.X},{kv.Key.Y})" : ts.Name;
					string tstate = !ts.Enabled ? "OFF" : ts.Cooldown > 0 ? "Reload" : ts.WiredActive ? "ACTIVE" : ts.Frayed > 0 ? $"Fray{ts.RecoveryPct()}%" : "Ready";
					string extra = ts.Kind == global::BiltonsYoyoMod.Traps.TrapKind.Link ? $" S{ts.StressPct()}%" : "";
					board.Add((kv.Key, $"> {tname} {who}: {tstate} Pwr{ts.Energy}/{ts.MaxEnergy} K{ts.Kills}{extra}", ts.Kills));
				}
				board.Sort((a, b) => b.kills.CompareTo(a.kills));
				int shown = 0;
				foreach (var row in board) {
					if (shown >= 5)
						break;
					cy += PlaceableUI.DrawWrapped(sb, row.line, new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
					shown++;
				}
				if (board.Count == 0)
					cy += PlaceableUI.DrawWrapped(sb, "No Aegis-covered traps. Build one inside this mesh.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
				else if (board.Count > shown)
					cy += PlaceableUI.DrawWrapped(sb, $"+{board.Count - shown} more covered.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			}
			// Mesh breaker: one switch arms/disarms every linked pair under
			// THIS engine's Aegis mesh. Snapped ropes refuse (rewire by hand).
			{
				int pairs = 0, armed = 0;
				var seen = new System.Collections.Generic.HashSet<Point>();
				int brange = StationNetwork.AegisCoverRange(OpenTile);
				if (brange > 0 && ResearchEngineSystem.IsTileStillEngine(OpenTile)) {
					foreach (var kv in global::BiltonsYoyoMod.Traps.TrapSystem.Traps) {
						if (kv.Value.Kind != global::BiltonsYoyoMod.Traps.TrapKind.Link || !kv.Value.Linked)
							continue;
						Point other = kv.Value.LinkPoint;
						if (!global::BiltonsYoyoMod.Traps.TrapSystem.Traps.TryGetValue(other, out _))
							continue;
						Point a = kv.Key;
						Point canon = (a.X < other.X || (a.X == other.X && a.Y <= other.Y)) ? a : other;
						if (!seen.Add(canon))
							continue;
						float adx = a.X - OpenTile.X, ady = a.Y - OpenTile.Y;
						float bdx = other.X - OpenTile.X, bdy = other.Y - OpenTile.Y;
						if (adx * adx + ady * ady > brange * brange || bdx * bdx + bdy * bdy > brange * brange)
							continue;
						pairs++;
						if (kv.Value.WiredActive)
							armed++;
					}
				}
				if (pairs > 0) {
					bool disarm = armed >= pairs;
					Rectangle brBtn = new(x + 14, (int)cy, 300, 28);
					if (YoyoPanel.Button(sb, brBtn, disarm ? $"Breaker: disarm all ({pairs})" : $"Breaker: arm all ({armed}/{pairs})", "One switch for every linked pair in this Aegis mesh. Snapped ropes refuse.", false, false, 0.75f)) {
						if (Main.netMode == NetmodeID.MultiplayerClient)
							global::BiltonsYoyoMod.Traps.TrapNet.SendBreaker(OpenTile, !disarm);
						else
							global::BiltonsYoyoMod.Traps.TrapActions.ApplyBreaker(OpenTile, !disarm);
						Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
					}
					cy += 34;
				}
			}
			// Mesh surge: dump a FULL charge to fire every covered defensive
			// trap at once (reloads + arms). Disabled when not full.
			{
				int full = ResearchEngineSystem.MaxChargeOf(data);
				bool ready = data.Charge >= full && StationNetwork.AegisCoverRange(OpenTile) > 0;
				Rectangle sgBtn = new(x + 14, (int)cy, 300, 28);
				if (YoyoPanel.Button(sb, sgBtn, $"Surge volley ({data.Charge}/{full})", ready ? "Dump the full charge: every covered trap fires/arms at once." : $"Needs a FULL charge ({full}) + Aegis mesh.", false, !ready, 0.75f)) {
					if (ready) {
						if (Main.netMode == NetmodeID.MultiplayerClient)
							global::BiltonsYoyoMod.Traps.TrapNet.SendSurge(OpenTile);
						else
							global::BiltonsYoyoMod.Traps.TrapActions.ApplySurge(OpenTile);
						Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, player.Center);
					}
				}
				cy += 34;
			}
			// Auto-service: +1% per 30s to the most-worn covered trap below
			// 80%, for 5 charge. Weaker than manual (+25% oil+1g) on purpose.
			{
				Rectangle svBtn = new(x + 14, (int)cy, 300, 28);
				string svState = data.AutoService ? "ON (+1%/30s, 5 charge)" : "OFF";
				if (YoyoPanel.Button(sb, svBtn, $"Auto-service: {svState}", "Covered traps below 80% slowly self-repair off this engine's charge. Needs an Aegis mesh to reach anything.", data.AutoService, false, 0.75f)) {
					if (Main.netMode == NetmodeID.MultiplayerClient)
						PlaceableNet.SendResearchAutoService(OpenTile);
					else
						PlaceableNet.ApplyResearchAutoService(OpenTile);
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				}
				cy += 34;
			}
			if (ResearchTech.TechOn(data, ResearchTech.StNet7) || ResearchTech.TechOn(data, ResearchTech.Aegis7)) {
				Rectangle odBtn = new(x + 14, (int)cy, 300, 28);
				string odState = data.OverdriveOn ? (data.Charge > 0 ? "DRAWING x2" : "ON (no charge)") : "OFF";
				if (YoyoPanel.Button(sb, odBtn, $"Overdrive draw: {odState}", "While ON and holding charge, this engine's mesh output doubles. Drains 1 charge per 2s.", data.OverdriveOn, false, 0.75f))
					StationNetwork.TryMeshToggle(player, OpenTile);
				cy += 34;
			}
			if (StationNetwork.BuffedResonators > 0)
				cy += PlaceableUI.DrawWrapped(sb, $"> Resonator x{StationNetwork.BuffedResonators}: Potency/Duration up.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			if (StationNetwork.BuffedWards > 0)
				cy += PlaceableUI.DrawWrapped(sb, $"> Ward x{StationNetwork.BuffedWards}: Damage/Defense/Vitality up.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			if (StationNetwork.BuffedRifts > 0)
				cy += PlaceableUI.DrawWrapped(sb, $"> Rift Anchor x{StationNetwork.BuffedRifts}: faster snap, harder burst.", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
			if (StationNetwork.ComboResWard)
				cy += PlaceableUI.DrawWrapped(sb, "Combo live: Harmonic Hum (Resonator + Ward).", new Vector2(x + 14, cy), Color.Gold, 0.75f, pw - 28);
			if (StationNetwork.ComboWardRift)
				cy += PlaceableUI.DrawWrapped(sb, "Combo live: Anchor Blessing (Ward + Rift).", new Vector2(x + 14, cy), Color.Gold, 0.75f, pw - 28);
			if (StationNetwork.ComboResRift)
				cy += PlaceableUI.DrawWrapped(sb, "Combo live: Snap Surge (Resonator + Rift).", new Vector2(x + 14, cy), Color.Gold, 0.75f, pw - 28);
			if (StationNetwork.FullMeshes > 0)
				cy += PlaceableUI.DrawWrapped(sb, "Full mesh (30) online: Superposition, Sanctuary, Deep Snap live.", new Vector2(x + 14, cy), Color.Gold, 0.75f, pw - 28);
			if (mesh > 0 && StationNetwork.BuffedResonators <= 0 && StationNetwork.BuffedWards <= 0 && StationNetwork.BuffedRifts <= 0)
				cy += PlaceableUI.DrawWrapped(sb, "No stations in any mesh yet.", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
			return cy;
		}

		// Trials (#11): 3 quest-like offers per engine, refreshed every
		// in-game dawn. Accept one or ignore it; accepted slots stay open
		// until dawn. Amounts randomize per board; mystery contracts hide
		// a randomized benefit-or-negative until accepted.
		private static float DrawTrials(SpriteBatch sb, Player player, int x, int pw, float cy, ResearchData data)
		{
			if (!ResearchTech.TechOn(data, ResearchTech.ExpBoard)) {
				cy += PlaceableUI.DrawWrapped(sb, "No trial board. Research Trial Board (Tech tab) to receive daily offers.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
				return cy;
			}
			cy += PlaceableUI.DrawWrapped(sb, "Today's experiments. Offers refresh at dawn.", new Vector2(x + 14, cy), Color.LightGray, 0.8f, pw - 28);
			cy += 6;
			for (int slot = 0; slot < ResearchTrials.OfferSlots; slot++) {
				string packed = (data.ExpOffers != null && slot < data.ExpOffers.Count) ? data.ExpOffers[slot] : "";
				if (!ResearchTrials.TryParseOffer(packed, out string template, out int cost, out int mats, out int reward)) {
					cy += PlaceableUI.DrawWrapped(sb, "— open (refreshes at dawn) —", new Vector2(x + 14, cy), Color.Gray, 0.75f, pw - 28);
					cy += 6;
					continue;
				}
				int s = slot;
				PlaceableUI.DrawTruncated(sb, ResearchTrials.OfferName(template), new Vector2(x + 14, cy), Color.Gold, 0.85f, pw - 200);
				Rectangle acc = new(x + pw - 170, (int)cy - 3, 156, 24);
				if (YoyoPanel.Button(sb, acc, "Accept", $"Take the {ResearchTrials.OfferName(template)}. Costs are paid now.", false, false, 0.8f))
					ResearchTrials.Accept(player, OpenTile, s);
				cy += 26;
				cy += PlaceableUI.DrawWrapped(sb, $"Cost: {ResearchTrials.CostText(data, template, cost, mats)}", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
				cy += PlaceableUI.DrawWrapped(sb, $"Reward: {ResearchTrials.RewardText(data, template, reward)}", new Vector2(x + 14, cy), Color.LightGray, 0.75f, pw - 28);
				cy += 8;
			}
			if (ResearchTech.TechOn(data, ResearchTech.ExpFresh)) {
				Rectangle reBtn = new(x + 14, (int)cy, 300, 28);
				if (YoyoPanel.Button(sb, reBtn, $"Fresh leads ({ResearchTrials.RerollCost} pts)", "Re-rolls today's offers right now.", false, false, 0.75f))
					ResearchTrials.TryReroll(player, OpenTile);
				cy += 34;
			}
			return cy;
		}

		// Loadout: switch owned tech on/off per block without losing it.
		// Disabled tech simply stops applying (styles gated by switched-off
		// tech lock too). Respec refunds everything at 70% and clears all.
		private static float DrawLoadout(SpriteBatch sb, Player player, int x, int pw, float cy, ResearchData data)
		{
			cy += PlaceableUI.DrawWrapped(sb, $"Owned: {data.TechCount}/{ResearchTech.All.Count}. Toggles are per block and instant.", new Vector2(x + 14, cy), Color.LightGray, 0.8f, pw - 28);
			cy += 6;
			// Live research progress with countdown bars + seconds. Ticks
			// whether this panel is open or not.
			if (data.Research != null) {
				foreach (var job in new System.Collections.Generic.List<ResearchJob>(data.Research)) {
					var cur = ResearchTech.ById(job.Id);
					if (cur == null)
						continue;
					int total = System.Math.Max(1, ResearchTech.ResearchSecs(cur) * 60);
					float done = 1f - System.Math.Clamp(job.Left / (float)total, 0f, 1f);
					Terraria.Utils.DrawBorderString(sb, $"Researching {cur.Name} — {System.Math.Max(0, job.Left / 60)}s left", new Vector2(x + 14, cy), Color.Gold, 0.8f);
					cy += 22;
					Rectangle bar = new(x + 14, (int)cy, pw - 320, 12);
					sb.Draw(TextureAssets.MagicPixel.Value, bar, new Color(40, 40, 50, 220));
					sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bar.X, bar.Y, (int)(bar.Width * done), bar.Height), new Color(255, 210, 120, 230));
					Rectangle cancel = new(x + pw - 180, (int)cy - 5, 166, 22);
					if (YoyoPanel.Button(sb, cancel, "Cancel (refund)", "Stops this research, refunds everything paid.", false, false, 0.7f))
						ResearchTech.CancelResearch(player, OpenTile, job.Id);
					cy += 20;
				}
			}
			if (data.TechCount == 0 && (data.Research == null || data.Research.Count == 0)) {
				cy += PlaceableUI.DrawWrapped(sb, "Nothing researched yet — the Tech tab is where the wild stuff lives.", new Vector2(x + 14, cy), Color.Gray, 0.8f, pw - 28);
				return cy;
			}
			float bw = (pw - 28 - 8) / 2f;
			int col = 0;
			float rowY = cy;
			foreach (var node in ResearchTech.All) {
				if (!data.HasTech(node.Id))
					continue;
				bool on = ResearchTech.TechOn(data, node.Id);
				Rectangle b = new(x + 14 + (int)(col * (bw + 8)), (int)rowY, (int)bw, 26);
				if (YoyoPanel.Button(sb, b, $"{node.Name}: {(on ? "ON" : "OFF")}", $"{node.Desc}\nClick to switch {(on ? "OFF" : "ON")}.", on, false, 0.7f)) {
					if (Main.netMode == NetmodeID.MultiplayerClient)
						PlaceableNet.SendResearchToggle(OpenTile, node.Id);
					else
						ResearchTech.ToggleOff(OpenTile, node.Id);
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				}
				col++;
				if (col >= 2) {
					col = 0;
					rowY += 30;
				}
			}
			if (col != 0)
				rowY += 30;
			cy = rowY + 4;
			Rectangle respec = new(x + 14, (int)cy, 280, 28);
			if (YoyoPanel.Button(sb, respec, "Respec (70% refund each)", "Refunds every researched tech at 70% and clears the tree. The sacrificed yoyo stays sacrificed.", false, false, 0.75f)) {
				ResearchTech.Respec(player, OpenTile);
			}
			cy += 34;
			return cy;
		}

		// Tech tree canvas: hold LEFT CLICK and drag to pan, click a node
		// (no drag) to research it. Node centers live in canvas space.
		// Mini-tabs (#3) filter by branch lane; ALL shows everything.
		private static float DrawTech(SpriteBatch sb, Player player, int x, int y, int pw, int ph, float cy, ResearchData data)
		{
			// Mini-tab counts use the pre-click selection; the canvas below
			// re-derives after the row so camera and content never disagree.
			string[] minis = MiniNames();
			techMini = System.Math.Clamp(techMini, 0, minis.Length - 1);
			int[] headSet = MiniSet(techMini);
			int mTot = 0;
			int mOwn = 0;
			foreach (var n in ResearchTech.All) {
				if (!InMiniSet(headSet, LaneOf(n)))
					continue;
				mTot++;
				if (data.HasTech(n.Id))
					mOwn++;
			}
			string headName = headSet == null ? "Researched" : minis[techMini];
			Terraria.Utils.DrawBorderString(sb, $"{headName}: {mOwn}/{mTot} — drag to pan, click a node to research.", new Vector2(x + 14, cy), Color.LightGray, 0.75f);
			cy += 22;
			techMini = PlaceableUI.TabRowPaged(sb, x + 14, (int)cy, pw - 28, minis, techMini, ref techMiniOffset, 4);
			cy += 34;
			int viewH = (int)(y + ph - 76 - cy);
			if (viewH < 160)
				viewH = 160;
			Rectangle view = new(x + 14, (int)cy, pw - 28, viewH);
			sb.Draw(TextureAssets.MagicPixel.Value, view, new Color(12, 14, 24, 230));
			// NOTE (#1): the viewport frame is drawn LAST at the end of
			// DrawTech, so the backdrop grid, lane bands, links and node
			// boxes can never overlap it.
			Vector2 mouse = Main.MouseScreen;
			bool overView = view.Contains(mouse.ToPoint());
			if (overView)
				player.mouseInterface = true;
			// Post-row selection drives canvas + camera (always in sync).
			techMini = System.Math.Clamp(techMini, 0, minis.Length - 1);
			int[] miniSet = MiniSet(techMini);
			bool showAllMini = miniSet == null;
			// Refit the camera whenever the mini-tab changes: ALL restores
			// the overview, a lane left-aligns its first node and centers
			// the branch vertically. Deterministic, no random jumps.
			if (techMiniApplied != techMini) {
				FrameMini(view);
				techMiniApplied = techMini;
			}
			// Backdrop grid (layer 1, behind everything): canvas-locked
			// like everything else (scrolls with the tree), thinner and
			// fainter than the branch grids so depth reads instantly.
			// Inset inside the frame and clipped hard to the viewport,
			// so it never touches the frame, buttons or branch rects.
			// Wide enough that dragging never exposes ungridded void.
			Rectangle inner = new(view.X + 1, view.Y + 1, view.Width - 2, view.Height - 2);
			Color backC = new Color(28, 34, 68, 55);
			for (int gx = -400; gx <= 1400; gx += 80) {
				float sx = view.X + camX + gx;
				if (sx < inner.X || sx > inner.Right)
					continue;
				ClipFill(sb, view, new Rectangle((int)sx, inner.Y, 1, inner.Height), backC);
			}
			for (int gy = -520; gy <= 1680; gy += 80) {
				float sy = view.Y + camY + gy;
				if (sy < inner.Y || sy > inner.Bottom)
					continue;
				ClipFill(sb, view, new Rectangle(inner.X, (int)sy, inner.Width, 1), backC);
			}
			// Lane bands, finite: each spans only its own branch content
			// plus a small margin, with a thin grid pinned inside the band.
			Color gridC = new Color(140, 170, 255, 55);
			var allLanes = ResearchTech.Lanes;
			for (int li = 0; li < allLanes.Length; li++) {
				var lane = allLanes[li];
				if (!InMiniSet(miniSet, li))
					continue;
				float minX = float.MaxValue;
				float maxX = float.MinValue;
				foreach (var node in ResearchTech.All) {
					if (System.Math.Abs(node.Y - lane.rowY) > 45)
						continue;
					if (node.X - 65 < minX)
						minX = node.X - 65;
					if (node.X + 65 > maxX)
						maxX = node.X + 65;
				}
				if (minX > maxX)
					continue;
				minX -= 60f;
				maxX += 60f;
				float sy0 = view.Y + camY + lane.rowY - 40f;
				float sy1 = view.Y + camY + lane.rowY + 40f;
				Rectangle band = new((int)(view.X + camX + minX), (int)sy0, (int)(maxX - minX), (int)(sy1 - sy0));
				if (!band.Intersects(view))
					continue;
				Color tint = lane.tint;
				Rectangle cb = Rectangle.Intersect(band, view);
				sb.Draw(TextureAssets.MagicPixel.Value, cb, new Color(tint.R, tint.G, tint.B, 18));
				int gx0 = (int)System.Math.Floor(minX / 40f) * 40;
				for (int gx = gx0; gx <= maxX; gx += 40) {
					float sx = view.X + camX + gx;
					if (sx < view.X || sx > view.Right)
						continue;
					DrawAxisBar(sb, view, new Vector2(sx, System.Math.Max(sy0, view.Y)), new Vector2(sx, System.Math.Min(sy1, view.Bottom)), gridC);
				}
				Terraria.Utils.DrawBorderString(sb, lane.label, new Vector2(view.X + 6, System.Math.Max(band.Y, view.Y) + 4), new Color(tint.R, tint.G, tint.B, 220), 0.65f);
			}
			// Contained board in brighter blue: the whole tree for ALL,
			// just this branch for a lane mini-tab.
			Color limC = new Color(140, 170, 255, 170);
			if (showAllMini) {
				Vector2 lim0 = new Vector2(view.X + camX - 260, view.Y + camY - 300);
				Vector2 lim1 = new Vector2(view.X + camX + 1400, view.Y + camY + 1660);
				DrawAxisBar(sb, view, new Vector2(lim0.X, lim0.Y), new Vector2(lim1.X, lim0.Y), limC);
				DrawAxisBar(sb, view, new Vector2(lim0.X, lim1.Y), new Vector2(lim1.X, lim1.Y), limC);
				DrawAxisBar(sb, view, new Vector2(lim0.X, lim0.Y), new Vector2(lim0.X, lim1.Y), limC);
				DrawAxisBar(sb, view, new Vector2(lim1.X, lim0.Y), new Vector2(lim1.X, lim1.Y), limC);
			}
			else {
				// Lane board: just this branch plus margin.
				MiniSpan(miniSet, out float lminX, out float lmaxX, out int ltopY, out int lbotY);
				Vector2 p0 = new Vector2(view.X + camX + lminX - 140f, view.Y + camY + ltopY);
				Vector2 p1 = new Vector2(view.X + camX + lmaxX + 140f, view.Y + camY + lbotY);
				DrawAxisBar(sb, view, new Vector2(p0.X, p0.Y), new Vector2(p1.X, p0.Y), limC);
				DrawAxisBar(sb, view, new Vector2(p0.X, p1.Y), new Vector2(p1.X, p1.Y), limC);
				DrawAxisBar(sb, view, new Vector2(p0.X, p0.Y), new Vector2(p0.X, p1.Y), limC);
				DrawAxisBar(sb, view, new Vector2(p1.X, p0.Y), new Vector2(p1.X, p1.Y), limC);
			}
			// Drag vs click: moving >8px while held pans, otherwise release buys.
			if (overView && Main.mouseLeft) {
				if (!dragging) {
					dragging = true;
					downMouse = mouse;
					downCamX = camX;
					downCamY = camY;
					dragMoved = false;
				}
				else {
					if (Vector2.Distance(mouse, downMouse) > 8f)
						dragMoved = true;
					if (dragMoved) {
						camX = downCamX + (mouse.X - downMouse.X);
						camY = downCamY + (mouse.Y - downMouse.Y);
						if (showAllMini) {
							// Boundary clamp with room to spare: the limit box
							// can never be dragged fully out of the viewport.
							camX = System.Math.Clamp(camX, view.Width - 1300f, 220f);
							camY = System.Math.Clamp(camY, view.Height - 1660f, 420f);
						}
						else {
							// Lane clamp: the branch board stays in view, so a
							// mini-tab can never be dragged into the void.
							MiniSpan(miniSet, out float cminX, out float cmaxX, out int ctopY, out int cbotY);
							camX = ClampAxis(view.Width, cminX - 140f, cmaxX + 140f, camX);
							camY = ClampAxis(view.Height, ctopY, cbotY, camY);
						}
					}
				}
			}
			else if (dragging && !Main.mouseLeft) {
				bool wasDrag = dragMoved;
				dragging = false;
				if (!wasDrag && overView) {
					foreach (var node in ResearchTech.All) {
						if (!InMiniSet(miniSet, LaneOf(node)))
							continue;
						if (NodeRect(view, node).Contains(mouse.ToPoint())) {
							ResearchTech.BuyNode(player, OpenTile, node.Id);
							break;
						}
					}
				}
			}
			// Prereq links under the nodes.
			foreach (var node in ResearchTech.All) {
				if (string.IsNullOrEmpty(node.Prereq))
					continue;
				var pre = ResearchTech.ById(node.Prereq);
				if (pre == null)
					continue;
				if (!InMiniSet(miniSet, LaneOf(pre)) || !InMiniSet(miniSet, LaneOf(node)))
					continue;
				Vector2 a = NodeCenter(view, pre);
				Vector2 b = NodeCenter(view, node);
				if (!view.Contains(a.ToPoint()) && !view.Contains(b.ToPoint()))
					continue;
				Color lc = data.HasTech(pre.Id) && data.HasTech(node.Id) ? Color.Gold : new Color(90, 98, 140, 160);
				DrawTechLine(sb, view, a, b, lc);
			}
			// Core root as a real card, parked left of every lane so it
			// never touches a branch.
			Rectangle coreBox = new((int)(view.X + camX - 200), (int)(view.Y + camY - 18), 100, 36);
			if (coreBox.Intersects(view)) {
				ClipFill(sb, view, coreBox, new Color(50, 44, 20, 235));
				ClipFill(sb, view, new Rectangle(coreBox.X, coreBox.Y, coreBox.Width, 1), Color.Gold);
				ClipFill(sb, view, new Rectangle(coreBox.X, coreBox.Bottom - 1, coreBox.Width, 1), Color.Gold);
				ClipFill(sb, view, new Rectangle(coreBox.X, coreBox.Y, 1, coreBox.Height), Color.Gold);
				ClipFill(sb, view, new Rectangle(coreBox.Right - 1, coreBox.Y, 1, coreBox.Height), Color.Gold);
				PlaceableUI.DrawClippedText(sb, "RESEARCH CORE", coreBox.X + coreBox.Width / 2f, coreBox.Y + 4, 0.65f, Color.Gold, view);
				PlaceableUI.DrawClippedText(sb, "root", coreBox.X + coreBox.Width / 2f, coreBox.Y + 20, 0.6f, Color.Gray, view);
			}
			// Nodes over the links. Boxes clip hard to the viewport so a
			// half-visible node stays a clean half-box; labels clip
			// per-pixel via scissor in DrawClippedText so a half-visible
			// glyph shows as a half-letter (never whole-letter popping).
			// Hover/tooltip still needs the node center inside (no stray
			// hover off a sliver).
			foreach (var node in ResearchTech.All) {
				Rectangle r = NodeRect(view, node);
				if (!r.Intersects(view))
					continue;
				if (!InMiniSet(miniSet, LaneOf(node)))
					continue;
				bool owned = data.HasTech(node.Id);
				bool on = ResearchTech.TechOn(data, node.Id);
				// #13: locked cards spell out WHY (prereq or tech count).
				string lockWhy = "";
				bool open = owned || ResearchTech.PrereqMet(data, node, out lockWhy);
				Vector2 center = new Vector2(r.X + r.Width / 2f, r.Y + r.Height / 2f);
				bool centerIn = view.Contains(center.ToPoint());
				// Box may show as a clean half-button at the edge; the
				// scissored label on top shows the matching half-letters.
				Color fill = owned ? (on ? new Color(70, 84, 46, 235) : new Color(60, 52, 40, 235)) : open ? new Color(34, 42, 68, 235) : new Color(22, 24, 38, 235);
				Color edge = !owned ? (open ? node.Color : new Color(70, 76, 110)) : on ? Color.Gold : new Color(200, 150, 90);
				bool researching = ResearchTech.FindJob(data, node.Id) != null;
				if (researching)
					edge = Color.Lerp(Color.Gold, Color.White, Main.GameUpdateCount % 30 < 15 ? 0f : 0.6f);
				bool hov = centerIn && !dragging && r.Contains(mouse.ToPoint());
				if (hov)
					edge = Color.White;
				ClipFill(sb, view, r, fill);
				ClipFill(sb, view, new Rectangle(r.X, r.Y, r.Width, 1), edge);
				ClipFill(sb, view, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), edge);
				ClipFill(sb, view, new Rectangle(r.X, r.Y, 1, r.Height), edge);
				ClipFill(sb, view, new Rectangle(r.Right - 1, r.Y, 1, r.Height), edge);
				ClipFill(sb, view, new Rectangle(r.X, r.Y, 4, r.Height), node.Color);
				// Labels clip per-pixel (GPU scissor) like the boxes:
				// half-visible cards keep partial glyphs instead of popping.
				PlaceableUI.DrawClippedText(sb, node.Name, r.X + r.Width / 2f, r.Y + 6, 0.7f, !owned ? (open ? Color.White : Color.Gray) : on ? Color.Gold : Color.Orange, view);
				string sub;
				var rjob = ResearchTech.FindJob(data, node.Id);
				int qpos = ResearchTech.QueuePosition(data, node.Id);
				if (rjob != null) {
					var rn2 = ResearchTech.ById(node.Id);
					int total2 = System.Math.Max(1, ResearchTech.ResearchSecs(rn2) * 60);
					int pct2 = (int)((1f - System.Math.Clamp(rjob.Left / (float)total2, 0f, 1f)) * 100f);
					sub = $"{pct2}% - {System.Math.Max(0, rjob.Left / 60)}s";
				}
				else if (qpos >= 0) {
					sub = $"QUEUED #{qpos + 1}";
				}
				else {
					sub = !owned ? (open ? ShortCost(node) : "LOCKED") : on ? "OWNED" : "OFF (see Loadout)";
				}
				PlaceableUI.DrawClippedText(sb, sub, r.X + r.Width / 2f, r.Y + 26, 0.6f, !owned ? (open ? Color.LightGray : Color.DarkGray) : on ? Color.Lime : Color.Orange, view);
				if (hov) {
					player.mouseInterface = true;
					Main.hoverItemName = $"{node.Name}\n{node.Desc}\nCost: {ResearchTech.CostText(data, node)}{(!owned && !open ? $"\nLocked: {lockWhy}" : "")}";
				}
			}
			// Viewport frame draws LAST so it always sits above the
			// backdrop grid, lane bands, links and node boxes (#1: the
			// dark grid can never overlap buttons/branch rects again).
			Color frameC = new Color(90, 98, 140, 220);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(view.X, view.Y, view.Width, 1), frameC);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(view.X, view.Bottom - 1, view.Width, 1), frameC);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(view.X, view.Y, 1, view.Height), frameC);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(view.Right - 1, view.Y, 1, view.Height), frameC);
			return view.Bottom + 6;
		}

		// Fill clipped to the viewport: outside-grid stays invisible, so a
		// half-visible button reads as a clean half-button.
		private static void ClipFill(SpriteBatch sb, Rectangle view, Rectangle r, Color color)
		{
			Rectangle c = Rectangle.Intersect(r, view);
			if (c.Width > 0 && c.Height > 0)
				sb.Draw(TextureAssets.MagicPixel.Value, c, color);
		}

		private static string ShortCost(ResearchTechNode node)
		{
			if (node.CostsYoyo)
				return "20g + yoyo";
			if (node.Coins >= 1000000)
				return $"{node.Coins / 1000000f:0.#}p + mats";
			if (node.Coins >= 10000)
				return $"{node.Coins / 10000f:0.#}g + mats";
			return $"{node.Coins / 100f:0.#}s + mats";
		}

		private static Vector2 NodeCenter(Rectangle view, ResearchTechNode node)
		{
			return new Vector2(view.X + camX + node.X, view.Y + camY + node.Y);
		}

		private static Rectangle NodeRect(Rectangle view, ResearchTechNode node)
		{
			Vector2 c = NodeCenter(view, node);
			return new Rectangle((int)c.X - 65, (int)c.Y - 22, 130, 44);
		}

		private static void DrawTechLine(SpriteBatch sb, Rectangle view, Vector2 a, Vector2 b, Color color)
		{
			// Elbow routing (horizontal-vertical-horizontal) between node
			// edges: reads as a proper tree instead of stray diagonals.
			// a/b arrive as node centers; edges inset by half a node.
			Vector2 start = new Vector2(a.X + 65, a.Y);
			Vector2 end = new Vector2(b.X - 65, b.Y);
			float midX = (start.X + end.X) / 2f;
			DrawAxisBar(sb, view, start, new Vector2(midX, start.Y), color);
			DrawAxisBar(sb, view, new Vector2(midX, start.Y), new Vector2(midX, end.Y), color);
			DrawAxisBar(sb, view, new Vector2(midX, end.Y), end, color);
			// Joint dots so corners read as connected, not overlapping.
			// Clipped hard to the viewport: dots never leave the box.
			foreach (Vector2 j in new[] { new Vector2(midX, start.Y), new Vector2(midX, end.Y) }) {
				ClipFill(sb, view, new Rectangle((int)j.X - 2, (int)j.Y - 2, 4, 4), color);
			}
		}

		private static void DrawAxisBar(SpriteBatch sb, Rectangle view, Vector2 a, Vector2 b, Color color)
		{
			Vector2 lo = new(System.Math.Min(a.X, b.X), System.Math.Min(a.Y, b.Y));
			Vector2 hi = new(System.Math.Max(a.X, b.X), System.Math.Max(a.Y, b.Y));
			// Clip to strictly INSIDE the viewport (1px frame margin), so
			// links, lane grids and the limit box can never poke a line
			// out of the box. The 2px bars expand inward only.
			Rectangle inner = new(view.X + 1, view.Y + 1, view.Width - 2, view.Height - 2);
			lo.X = System.Math.Max(lo.X, inner.X);
			lo.Y = System.Math.Max(lo.Y, inner.Y);
			hi.X = System.Math.Min(hi.X, inner.Right);
			hi.Y = System.Math.Min(hi.Y, inner.Bottom);
			if (hi.X - lo.X < 1f && hi.Y - lo.Y < 1f)
				return;
			if (hi.X - lo.X >= hi.Y - lo.Y)
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)lo.X, (int)lo.Y - 1, (int)(hi.X - lo.X), 2), color);
			else
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)lo.X - 1, (int)lo.Y, 2, (int)(hi.Y - lo.Y)), color);
		}

		// Mini-tabs (#3, grouped in #16): ALL, the multi-branch groups
		// beside it (CHARGE = charge/release lanes, LAB = everything
		// research/points), then a divider gap and the individual branch
		// lanes to the right. Cached; lanes are static data so this never
		// goes stale mid-session.
		private static string[] miniNames;
		private static int[][] miniLaneSets; // null = ALL lanes
		private static int miniLaneCount; // cache guard: rebuild when lanes change (hot reload safe)

		private static void EnsureMinis()
		{
			var lanes = ResearchTech.Lanes;
			if (miniNames != null && miniLaneCount == lanes.Length)
				return;
			var names = new System.Collections.Generic.List<string> { "ALL", "CHARGE", "LAB", "|" };
			var sets = new System.Collections.Generic.List<int[]>();
			sets.Add(null);
			sets.Add(ChargeLaneSet(lanes));
			sets.Add(LabLaneSet(lanes));
			sets.Add(new int[0]); // divider gap (never selectable)
			for (int i = 0; i < lanes.Length; i++) {
				names.Add(lanes[i].label);
				sets.Add(new[] { i });
			}
			miniNames = names.ToArray();
			miniLaneSets = sets.ToArray();
			miniLaneCount = lanes.Length;
		}

		private static string[] MiniNames()
		{
			EnsureMinis();
			return miniNames;
		}

		private static int[] MiniSet(int techMini)
		{
			EnsureMinis();
			if (techMini < 0 || techMini >= miniLaneSets.Length)
				return null;
			var set = miniLaneSets[techMini];
			return set != null && set.Length == 0 ? null : set;
		}

		private static int[] ChargeLaneSet((string label, int rowY, Color tint)[] lanes)
		{
			var idx = new System.Collections.Generic.List<int>();
			foreach (string want in new[] { "IMPACT", "FLOW", "BURST", "TUNE", "LAUNCH", "CHARGE", "STATION", "AEGIS" }) {
				for (int i = 0; i < lanes.Length; i++) {
					if (lanes[i].label == want && !idx.Contains(i))
						idx.Add(i);
				}
			}
			return idx.ToArray();
		}

		private static int[] LabLaneSet((string label, int rowY, Color tint)[] lanes)
		{
			// LAB = research/points + meshes (explicit so AEGIS/STATION show
			// here AND in CHARGE instead of being excluded as "everything else").
			var idx = new System.Collections.Generic.List<int>();
			foreach (string want in new[] { "SPEED", "TRADE", "BAYS", "AUTO", "YIELD", "NET", "EVENT", "FIELD", "SALVAGE", "TRIALS", "STATION", "AEGIS", "SAVE" }) {
				for (int i = 0; i < lanes.Length; i++) {
					if (lanes[i].label == want && !idx.Contains(i))
						idx.Add(i);
				}
			}
			// Any future lane not listed above still shows in LAB (never hidden).
			for (int i = 0; i < lanes.Length; i++) {
				if (!idx.Contains(i)) {
					bool inCharge = false;
					foreach (string want in new[] { "IMPACT", "FLOW", "BURST", "TUNE", "LAUNCH", "CHARGE" }) {
						if (lanes[i].label == want) { inCharge = true; break; }
					}
					if (!inCharge)
						idx.Add(i);
				}
			}
			return idx.ToArray();
		}

		// Which lane a node belongs to (same 45px predicate as bands).
		private static int LaneOf(ResearchTechNode node)
		{
			var lanes = ResearchTech.Lanes;
			for (int i = 0; i < lanes.Length; i++) {
				if (System.Math.Abs(node.Y - lanes[i].rowY) <= 45)
					return i;
			}
			return -1;
		}

		private static bool InMiniSet(int[] set, int laneIdx)
		{
			return set == null || System.Array.IndexOf(set, laneIdx) >= 0;
		}

		// Canvas span of a mini-tab's node cards (same 45px predicate and
		// 65px half-card as the band layout, so framing matches content).
		private static void MiniSpan(int[] set, out float minX, out float maxX, out int topY, out int botY)
		{
			var lanes = ResearchTech.Lanes;
			topY = int.MaxValue;
			botY = int.MinValue;
			if (set == null) {
				foreach (var lane in lanes) {
					if (lane.rowY - 100 < topY)
						topY = lane.rowY - 100;
					if (lane.rowY + 100 > botY)
						botY = lane.rowY + 100;
				}
			}
			else {
				foreach (int li in set) {
					if (li < 0 || li >= lanes.Length)
						continue;
					if (lanes[li].rowY - 100 < topY)
						topY = lanes[li].rowY - 100;
					if (lanes[li].rowY + 100 > botY)
						botY = lanes[li].rowY + 100;
				}
			}
			if (topY > botY) {
				topY = -100;
				botY = 100;
			}
			minX = float.MaxValue;
			maxX = float.MinValue;
			foreach (var node in ResearchTech.All) {
				if (!InMiniSet(set, LaneOf(node)))
					continue;
				if (node.X - 65 < minX)
					minX = node.X - 65;
				if (node.X + 65 > maxX)
					maxX = node.X + 65;
			}
			if (minX > maxX) {
				minX = 0f;
				maxX = 0f;
			}
		}

		// One axis of the lane clamp: keeps the board in view while wide
		// enough to pan; dead-centers when the content is narrower.
		private static float ClampAxis(int size, float b0, float b1, float v)
		{
			float lo = size - 8f - b1;
			float hi = 8f - b0;
			if (lo > hi)
				return (lo + hi) / 2f;
			return System.Math.Clamp(v, lo, hi);
		}

		// Deterministic camera refit for a mini-tab switch: ALL restores
		// the overview, anything else left-aligns its first node and
		// centers the branch vertically.
		private static void FrameMini(Rectangle view)
		{
			int[] set = MiniSet(techMini);
			if (set == null) {
				camX = 30f;
				camY = 130f;
				return;
			}
			MiniSpan(set, out float minX, out float maxX, out int topY, out int botY);
			camX = 8f - (minX - 140f);
			camY = (view.Height - 8f - botY + 8f - topY) / 2f;
		}
	}
}
