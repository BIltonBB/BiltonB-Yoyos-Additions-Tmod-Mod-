using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using YoyoPanel = BiltonsYoyoMod.Content.UI.YoyoNPC.YoyoNPCPanel;

namespace BiltonsYoyoMod.Content.Minigames
{
	// UI-embedded skill games, played entirely with the mouse inside the station
	// panel. Three flows share one canvas:
	//  (A) PRIZE games (one engine per station, each different): pick a SPECIFIC
	//      mod, beat its game AND pay its resource cost to earn it. Difficulty
	//      scales with the mod's star rating.
	//  (B) ATTUNEMENT trials (each different from its station's prize game):
	//      gear-stops, frequency pursuit, angular blade, charge-release, trace
	//      and the Worldfold Rite. Winning unlocks the family at THIS block only.
	//  (C) BLESSING resonance (ring-tap): win a unique 4-minute station buff.
	// All state lives here, so closing the panel (X, Escape, walking away) or
	// leaving the tab stops everything cold.
	public static class StationMinigames
	{
		// TargetKind: 0 tuning, 1 calibration, 2 mastery, 3 free reforge.
		public static bool Playing;
		public static bool TrialMode;
		public static string TrialFamily = "";
		public static int MiniMode; // 0 prize, 1 blessing (minigame tab only)
		public static int GameStation = -1;
		public static int TargetKind = -1;
		public static int TargetId = -1;
		public static bool TargetTrue;
		public static int NeedStars = 1;
		public static int Phase; // 0 picking, 1 playing, 2 won, 3 lost
		public static int Clock;
		public static int Round;
		public static int Stage; // gauntlet sub-stage (Worldfold Rite)
		public static int Score;
		public static int Misses;

		// Timing needles (0..100 float).
		private static float needle;
		private static float needleDir = 1f;
		private static float needle2;
		private static float needle2Dir = -1f;

		// Sequence (Simon) state. The full route AND every hint angle are picked
		// up-front in BuildSeq: the game always knows what is hinted, in order.
		private static readonly System.Collections.Generic.List<int> seq = new();
		private static readonly System.Collections.Generic.List<float> hintAngles = new();
		private static readonly float[] padAngle = new float[6];
		private static readonly float[] padHome = new float[6];
		private static readonly float[] padSnapT = new float[6];
		private static readonly float[] padSnapAng = new float[6];
		private static int seqShowIdx;
		private static int seqShowTimer;
		private static bool showing;
		private static int seqInputIdx;
		private static int seqLock;
		private static int roundSlack; // one free slip per round: warns, never scores

		// Reaction state.
		private static int flashQuad = -1;
		private static int flashTimer;
		private static int flashSpan = 60;
		private static int flashRounds;
		// Smooth hover physics per pad (grow on hover, ease back after).
		private static readonly float[] padScale = new float[6] { 1f, 1f, 1f, 1f, 1f, 1f };
		// Needle echo trail (timing-family games, visual only).
		private static readonly float[] needleEcho = new float[6];

		// Gear-stop state (3 gears).
		private static readonly float[] gearAngle = new float[3];
		private static readonly bool[] gearStopped = new bool[3];

		// Frequency pursuit state.
		private static float freqPhase;
		private static float FreqTarget => 50f + 30f * (float)System.Math.Sin(Clock * 0.02f + freqPhase);

		// Angular blade state.
		private static float bladeAngle;
		private static float arcCenter;

		// Charge-release state.
		private static float charge;
		private static bool wasDown;
		private static float chargeLo = 50f;
		private static float chargeHi = 66f;

		// Trace state.
		private static int traceIdx;
		private static int traceTimer;

		// Ring-tap (blessing) state.
		private static float ringR = 110f;
		private static int ringAttemptT;

		public static int Wins;

		private enum Engine
		{
			Roulette, Simon, Reaction, Timing, MovingBand, Dual,
			Gear, Freq, Angular, Charge, Trace, Ringtap,
		}

		private static Engine PrizeEngine(int station, int kind)
		{
			if (station == 0) return Engine.Roulette;
			if (station == 1) return Engine.Simon;
			if (station == 2) return Engine.Reaction;
			if (station == 3) return Engine.Timing;
			if (station == 4) return kind == 0 ? Engine.Simon : Engine.MovingBand;
			return Engine.Dual;
		}

		private static Engine TrialEngine(string family) => family switch {
			"tuning" => Engine.Gear,
			"calib" => Engine.Freq,
			"mastery" => Engine.Angular,
			"exttuning" => Engine.Charge,
			"extcalib" => Engine.Trace,
			_ => Engine.Dual, // true handled as gauntlet stages
		};

		public static bool IsRite => TrialMode && TrialFamily == "true";

		private static Engine ActiveEngine()
		{
			if (MiniMode == 1 && !TrialMode) return Engine.Ringtap;
			if (IsRite) return Stage == 0 ? Engine.Timing : Stage == 1 ? Engine.Simon : Engine.Dual;
			if (TrialMode) return TrialEngine(TrialFamily);
			return PrizeEngine(GameStation, TargetKind);
		}

		public static string GameName(int station) => station switch {
			0 => "Reforge Roulette",
			1 => "Trajectory Remap",
			2 => "Mirror Duel",
			3 => "Deadband Tune",
			4 => "Clockwork Cipher",
			5 => "Worldfold Convergence",
			_ => "Minigame",
		};

		public static string TrialGameName(string family) => family switch {
			"tuning" => "Gearbox Alignment",
			"calib" => "Frequency Pursuit",
			"mastery" => "Duelist's Blade",
			"exttuning" => "Overdrive Release",
			"extcalib" => "Circuit Trace",
			_ => "Worldfold Rite",
		};

		public static string GameHint(int station) => station switch {
			0 => "Stop the marker inside the gold band, 2 of 3. The reforge itself is free.",
			1 => "Watch each dial spin to its hinted angle, then repeat the route. Hands off while hinting, first slip free! Win + half resources = Tuning earned.",
			2 => "Strike the gold quadrant before it shifts. Win + half resources = Mastery earned.",
			3 => "Stop the needle inside the band. Win + half resources = Calibration earned.",
			4 => "Repeat the dial route (round 2+ spins the gears: follow COLORS) or ride the drifting band. Hands off while hinting!",
			5 => "One click when BOTH needles sit in their bands. Win + half resources = True Mastery earned.",
			_ => "",
		};

		public static string TrialBrief(string family) => family switch {
			"tuning" => "Stop all 3 gears with each marked tooth inside its gold arc.",
			"calib" => "Lock the dial onto the wandering frequency 4 times.",
			"mastery" => "Strike when the blade crosses the gold arc, 4 rounds.",
			"exttuning" => "Hold to charge, release inside the zone. No overcharge! 3 releases.",
			"extcalib" => "Tap each lit node along the circuit before its fuse burns. 8 nodes.",
			_ => "The Rite: timing, memory, convergence. 3 misses ends it.",
		};

		private static int TrialForStation(int station, int kind)
		{
			if (station == 0) return -1;
			if (station == 1) return 0;
			if (station == 2) return 2;
			if (station == 3) return 1;
			if (station == 4) return kind == 0 ? 3 : 4;
			return 5;
		}

		public static string GateText(int station, int kind)
		{
			int t = TrialForStation(station, kind);
			if (t < 0) return null;
			if (!Trials.YoyoTrialActions.TrialRequirementMet(Main.LocalPlayer, t))
				return "Requires: " + Trials.YoyoTrialActions.TrialRequirementText(t);
			return null;
		}

		private static string FamilyFor(int station, int kind, bool isTrue)
		{
			if (kind == 3) return "";
			if (kind == 0) return station == 4 ? "exttuning" : "tuning";
			if (kind == 1) return station == 4 ? "extcalib" : "calib";
			return isTrue ? "true" : "mastery";
		}

		public static int BlessingBuff(int station) => station switch {
			0 => ModContent.BuffType<Buffs.StoneskinPoiseBuff>(),
			1 => ModContent.BuffType<Buffs.LightRiggingBuff>(),
			2 => ModContent.BuffType<Buffs.DuelistEyeBuff>(),
			3 => ModContent.BuffType<Buffs.LongLensesBuff>(),
			4 => ModContent.BuffType<Buffs.OverdriveBuff>(),
			_ => ModContent.BuffType<Buffs.FoldMendingBuff>(),
		};

		public static string BlessingName(int station) => station switch {
			0 => "Stoneskin Poise (+8% yoyo damage, +4 defense, 4 min)",
			1 => "Light Rigging (+12% yoyo velocity, +8% move speed, 4 min)",
			2 => "Duelist's Eye (+8% yoyo crit, 4 min)",
			3 => "Long Lenses (+range, danger sense, 4 min)",
			4 => "Overdrive (+8% velocity and range, 4 min)",
			_ => "Fold Mending (+6% damage, regen, 4 min)",
		};

		// Primary family gating the blessing (station 0 is always open).
		public static string BlessingFamily(int station) => station switch {
			1 => "tuning",
			2 => "mastery",
			3 => "calib",
			4 => "exttuning",
			5 => "true",
			_ => "",
		};

		public static void Stop()
		{
			Playing = false;
			TrialMode = false;
			TrialFamily = "";
			MiniMode = 0;
			Phase = 0;
			Clock = 0;
			Round = 0;
			Stage = 0;
			Score = 0;
			Misses = 0;
			seq.Clear();
			wasDown = false;
			fxRings.Clear();
			fxPadSlot = -1;
		}

		public static void StopRun()
		{
			Playing = false;
			TrialMode = false;
			TrialFamily = "";
			Phase = 0;
			Clock = 0;
			Round = 0;
			Stage = 0;
			Score = 0;
			Misses = 0;
			seq.Clear();
			wasDown = false;
			fxRings.Clear();
			fxPadSlot = -1;
		}

		public static void Start(int station, int kind, int id, bool isTrue)
		{
			Player player = Main.LocalPlayer;
			if (Customization.YoyoStationActions.HeldYoyo(player) == null) {
				Customization.YoyoStationUISystem.SetMessage("Hold a yoyo in your hand first.");
				return;
			}
			string gate = GateText(station, kind);
			if (gate != null) {
				Customization.YoyoStationUISystem.SetMessage(gate);
				return;
			}
			if (kind < 3) {
				string fam = FamilyFor(station, kind, isTrue);
				string lockMsg = Customization.YoyoCustomizationSystem.TileFamilyLock(Customization.YoyoStationUISystem.OpenTile, fam);
				if (lockMsg != null) {
					Customization.YoyoStationUISystem.SetMessage(lockMsg);
					return;
				}
				if (id < 0) {
					Customization.YoyoStationUISystem.SetMessage("Pick a specific mod first, then play for it.");
					return;
				}
				// Both gates: you must hold the resources AND beat the game.
				if (!Customization.YoyoStationActions.CheckCosts(player, kind, station == 4 || isTrue, id))
					return;
			}
			Begin(station, kind, id, isTrue, trial: false, family: "");
		}

		public static void StartTrial(int station, string family)
		{
			Player player = Main.LocalPlayer;
			if (Customization.YoyoStationActions.HeldYoyo(player) == null) {
				Customization.YoyoStationUISystem.SetMessage("Hold a yoyo in your hand first.");
				return;
			}
			int kind = family == "tuning" || family == "exttuning" ? 0 : family == "calib" || family == "extcalib" ? 1 : 2;
			string gate = GateText(station, kind);
			if (gate != null) {
				Customization.YoyoStationUISystem.SetMessage(gate);
				return;
			}
			Begin(station, kind, -1, family == "true", trial: true, family: family);
			Customization.YoyoStationUISystem.SetMessage($"Attunement: {Customization.YoyoCustomizationSystem.FamilyTitle(family)} at THIS block. {TrialBrief(family)}");
		}

		public static void StartBlessing(int station)
		{
			string fam = BlessingFamily(station);
			if (fam != "") {
				string lockMsg = Customization.YoyoCustomizationSystem.TileFamilyLock(Customization.YoyoStationUISystem.OpenTile, fam);
				if (lockMsg != null) {
					Customization.YoyoStationUISystem.SetMessage("Blessing needs this block attuned first: " + Customization.YoyoCustomizationSystem.FamilyTitle(fam) + ".");
					return;
				}
			}
			MiniMode = 1;
			Begin(station, 0, -1, false, trial: false, family: "");
			Customization.YoyoStationUISystem.SetMessage("Resonance: tap when the ring kisses the circle. 3 kisses, 3 misses allowed... actually 3 misses ends it.");
		}

		private static void Begin(int station, int kind, int id, bool isTrue, bool trial, string family)
		{
			Playing = true;
			TrialMode = trial;
			TrialFamily = family;
			GameStation = station;
			TargetKind = kind;
			TargetId = id;
			TargetTrue = isTrue;
			NeedStars = trial ? 3 : (kind < 3 && id >= 0 ? Customization.YoyoDifficulty.Stars(kind, id) : 1);
			Phase = 1;
			Clock = 0;
			Round = 1;
			Stage = 0;
			Score = 0;
			Misses = 0;
			needle = 0f;
			needleDir = 1f;
			needle2 = 100f;
			needle2Dir = -1f;
			seq.Clear();
			seqShowIdx = 0;
			seqShowTimer = 0;
			showing = true;
			seqInputIdx = 0;
			flashQuad = -1;
			flashTimer = 0;
			flashRounds = 0;
			for (int i = 0; i < 3; i++) { gearAngle[i] = Main.rand.NextFloat(MathHelper.TwoPi); gearStopped[i] = false; }
			freqPhase = Main.rand.NextFloat(MathHelper.TwoPi);
			bladeAngle = 0f;
			arcCenter = Main.rand.NextFloat(MathHelper.TwoPi);
			charge = 0f;
			wasDown = false;
			NewChargeZone();
			traceIdx = 0;
			traceTimer = 0;
			ringR = 110f;
			ringAttemptT = 0;
			fxRings.Clear();
			fxPadSlot = -1;
			Engine eng = ActiveEngine();
			if (eng == Engine.Simon) {
				int len0 = TrialMode ? (station == 4 ? 6 : 5) : 4 + (NeedStars - 1);
				BuildSeq(len0, 1);
			}
			if (eng == Engine.Reaction)
				NewFlash();
			SoundEngine.PlaySound(SoundID.MenuOpen);
		}

		private static int PadCount(int station) => station == 4 ? 4 : 6;

		private static void BuildSeq(int len, int showRound)
		{
			seq.Clear();
			hintAngles.Clear();
			int pads = PadCount(GameStation);
			for (int i = 0; i < len; i++) {
				seq.Add(Main.rand.Next(pads));
				hintAngles.Add(Main.rand.NextFloat(MathHelper.TwoPi));
			}
			for (int s = 0; s < 6; s++) {
				padHome[s] = Main.rand.NextFloat(MathHelper.TwoPi);
				padSnapT[s] = 0;
			}
			Round = showRound;
			seqShowIdx = 0;
			seqShowTimer = 0;
			showing = true;
			seqInputIdx = 0;
			seqLock = -1; // locked all through watching; released after
			roundSlack = 1;
		}

		private static void BuildPrizeSeq()
		{
			int st = GameStation;
			Round++;
			int len = (st == 4 ? 5 : 4) + (NeedStars - 1) + (Round - 1);
			BuildSeq(len, Round);
		}

		private static float EaseAngle(float from, float to, float k)
		{
			return from + MathHelper.WrapAngle(to - from) * k;
		}

		public static int PadColor(int station, int round, int slot)
		{
			int pads = PadCount(station);
			if (station == 4 && round >= 2)
				return (slot + 1) % pads;
			return slot % pads;
		}

		private static readonly Color[] PadColors = {
			new(255, 120, 120), new(120, 200, 255), new(140, 255, 150),
			new(255, 220, 120), new(200, 150, 255), new(255, 160, 220),
		};

		public static Color PadDrawColor(int colorId) => PadColors[colorId % PadColors.Length];

		private static void NewFlash()
		{
			flashQuad = Main.rand.Next(4);
			int span = System.Math.Max(TrialMode ? 32 : 34, 60 - flashRounds * 3 - (NeedStars - 1) * 3);
			flashTimer = span;
			flashSpan = span;
		}

		private static void NewChargeZone()
		{
			chargeLo = Main.rand.NextFloat(35f, 70f);
			chargeHi = chargeLo + System.Math.Max(11f, 18f - Round * 1.5f - (NeedStars - 1));
		}

		private static float BandHalf()
		{
			if (GameStation == 0) return System.Math.Max(7f, 17f - Round * 5f);
			return System.Math.Max(4f, 13f - Round * 2f - (NeedStars - 1) * 1.5f);
		}

		private static float BandCenter()
		{
			if (ActiveEngine() == Engine.MovingBand)
				return 50f + 22f * (float)System.Math.Sin(Clock * 0.025f);
			return 50f;
		}

		private static int SetsNeeded()
		{
			if (TrialMode) return 5;
			return 2 + NeedStars;
		}

		private static int DualNeeded() => 1 + NeedStars;

		private static int SeqRoundsNeeded() => NeedStars;

		private static int HitsNeeded() => TrialMode ? 8 : 4 + 2 * NeedStars;

		public static void Tick()
		{
			if (!Playing || Phase != 1)
				return;
			Clock++;
			Engine eng = ActiveEngine();
			if (eng == Engine.Roulette || eng == Engine.Timing || eng == Engine.MovingBand) {
				float speed = GameStation == 0 ? 1.1f + Round * 0.15f : 1.2f + Round * 0.15f + (NeedStars - 1) * 0.1f;
				needle += needleDir * speed;
				if (needle >= 100f) { needle = 100f; needleDir = -1f; }
				if (needle <= 0f) { needle = 0f; needleDir = 1f; }
				for (int i = needleEcho.Length - 1; i > 0; i--)
					needleEcho[i] = needleEcho[i - 1];
				needleEcho[0] = needle;
			}
			else if (eng == Engine.Dual) {
				float speed = 1.0f + Round * 0.15f;
				needle += needleDir * speed;
				if (needle >= 100f) { needle = 100f; needleDir = -1f; }
				if (needle <= 0f) { needle = 0f; needleDir = 1f; }
				needle2 += needle2Dir * speed;
				if (needle2 >= 100f) { needle2 = 100f; needle2Dir = -1f; }
				if (needle2 <= 0f) { needle2 = 0f; needle2Dir = 1f; }
			}
			else if (eng == Engine.Freq) {
				float speed = 2.2f + Score * 0.25f;
				needle += needleDir * speed;
				if (needle >= 100f) { needle = 100f; needleDir = -1f; }
				if (needle <= 0f) { needle = 0f; needleDir = 1f; }
				for (int i = freqEcho.Length - 1; i > 0; i--)
					freqEcho[i] = freqEcho[i - 1];
				freqEcho[0] = needle;
			}
			if (eng == Engine.Simon) {
				if (showing) {
					seqShowTimer++;
					// The active hint's pointer eases to its pre-picked angle;
					// every other dial drifts home.
					for (int s = 0; s < 6; s++) {
						float want = padHome[s];
						if (seqShowIdx < seq.Count && seqShowTimer < 44 && PadColor(GameStation, Round, s) == seq[seqShowIdx] && seqShowIdx < hintAngles.Count)
							want = hintAngles[seqShowIdx];
						padAngle[s] = EaseAngle(padAngle[s], want, 0.18f);
					}
					int hold = 44;
					int gap = 16;
					if (seqShowTimer >= hold + gap) {
						seqShowTimer = 0;
						seqShowIdx++;
						if (seqShowIdx >= seq.Count) {
							showing = false;
							seqInputIdx = 0;
							seqLock = 60; // +1s hands-off after watching ends
						}
					}
				}
				else {
					if (seqLock > 0) seqLock--;
					// Snapped dials glide to their confirmed angle.
					for (int s = 0; s < 6; s++) {
						if (padSnapT[s] > 0) {
							padSnapT[s]--;
							padAngle[s] = EaseAngle(padAngle[s], padSnapAng[s], 0.35f);
						}
					}
				}
			}
			else if (eng == Engine.Reaction) {
				if (flashQuad >= 0) {
					flashTimer--;
					if (flashTimer <= 0) {
						Misses++;
						fxMissTick = FxNow;
						FxPush(Main.MouseScreen, Color.Tomato * 0.8f);
						SoundEngine.PlaySound(SoundID.MenuTick);
						if (Misses >= 3) Lose("The mirror out-shifted you.");
						else NewFlash();
					}
				}
			}
			else if (eng == Engine.Gear) {
				float[] spd = { 0.03f, 0.04f, 0.05f };
				int[] dir = { 1, -1, 1 };
				for (int i = 0; i < 3; i++) {
					if (!gearStopped[i])
						gearAngle[i] += dir[i] * spd[i];
				}
			}
			else if (eng == Engine.Angular) {
				bladeAngle += 0.05f + Round * 0.012f;
				for (int i = bladeTrail.Length - 1; i > 0; i--)
					bladeTrail[i] = bladeTrail[i - 1];
				bladeTrail[0] = bladeAngle;
			}
			else if (eng == Engine.Charge) {
				bool down = Main.mouseLeft && ActiveCanvas().Contains(Main.MouseScreen.ToPoint());
				if (down && !wasDown) { charge = 0f; }
				if (down) {
					charge += 1.6f;
					if (charge >= 100f) {
						charge = 0f;
						wasDown = true;
						OnChargeMiss("overcharged! Vent it earlier.");
					}
				}
				else if (wasDown) {
					// Released: judge the vent.
					ClickChargeRelease();
				}
				wasDown = down;
			}
			else if (eng == Engine.Trace) {
				if (traceIdx < 8) {
					traceTimer++;
					int limit = System.Math.Max(50, 95 - traceIdx * 6);
					if (traceTimer > limit) {
						traceTimer = 0;
						OnMiss("the fuse burned out.");
					}
				}
			}
			else if (eng == Engine.Ringtap) {
				Rectangle ac = ActiveCanvas();
				if (ac == Rectangle.Empty || ac.Width < 40 || ac.Height < 40)
					return;
				float maxR = RingMax(ac);
				if (ringR > maxR) ringR = maxR;
				ringAttemptT++;
				ringR -= 1.1f + Score * 0.15f;
				if (ringR <= 4f) {
					ringR = maxR;
					ringAttemptT = 0;
					OnMiss("the ring collapsed unstruck.");
				}
				else if (ringAttemptT > 360) {
					ringR = maxR;
					ringAttemptT = 0;
					OnMiss("too slow.");
				}
			}
		}

		private static Rectangle activeCanvas;
		private static Rectangle ActiveCanvas() => activeCanvas;

		// Optional spriter skins: drop a PNG at the listed path and the game
		// uses it instead of the procedural shape (gameplay markers stay drawn
		// on top so readability never breaks). Missing file = procedural look.
		//   Content/Minigames/Skins/Pad.png     memory dials (square-ish)
		//   Content/Minigames/Skins/Gear.png    gear bodies (square, rotates)
		//   Content/Minigames/Skins/Needle.png  timing needle marker (tall)
		//   Content/Minigames/Skins/Node.png    trace nodes (square-ish)
		public static class MinigameSkins
		{
			public static Texture2D Pad;
			public static Texture2D Gear;
			public static Texture2D Needle;
			public static Texture2D Node;
			private static bool tried;

			public static void Ensure()
			{
				if (tried)
					return;
				tried = true;
				Pad = Load("Pad");
				Gear = Load("Gear");
				Needle = Load("Needle");
				Node = Load("Node");
			}

			private static Texture2D Load(string name)
			{
				try {
					if (ModContent.RequestIfExists<Texture2D>("BiltonsYoyoMod/Content/Minigames/Skins/" + name, out var asset) && asset?.Value != null)
						return asset.Value;
				}
				catch {
				}
				return null;
			}
		}

		// Draws a skin disc when present; returns false to fall back procedural.
		private static bool DrawSkin(SpriteBatch sb, Texture2D tex, Vector2 center, float radius, float rotation, Color color)
		{
			if (tex == null)
				return false;
			Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
			float scale = radius * 2f / System.Math.Max(1, System.Math.Max(tex.Width, tex.Height));
			sb.Draw(tex, center, null, color, rotation, origin, scale, SpriteEffects.None, 0f);
			return true;
		}

		// Visual-only FX state (never affects outcomes). Timed on the wall clock
		// so effects always fade even after the game stops (the game Clock
		// freezes when not playing, which used to leave stale rings behind).
		private static int FxNow => (int)(Main.GameUpdateCount % 2147483647);
		private static readonly System.Collections.Generic.List<(Vector2 p, Color c, int t)> fxRings = new();
		private static int fxMissTick = -999;
		private static readonly float[] bladeTrail = new float[14];
		private static readonly float[] freqEcho = new float[8];
		private static int fxBladeHit = -999;
		private static int fxPadSlot = -1;
		private static int fxPadTick = -999;

		private static void FxPush(Vector2 p, Color c)
		{
			fxRings.Add((p, c, FxNow));
			if (fxRings.Count > 12) fxRings.RemoveAt(0);
		}

		private static void DrawFx(SpriteBatch sb)
		{
			int now = FxNow;
			for (int i = fxRings.Count - 1; i >= 0; i--) {
				var f = fxRings[i];
				int age = now - f.t;
				if (age > 24) { fxRings.RemoveAt(i); continue; }
				DrawSmoothRing(sb, f.p, 6f + age * 2.4f, f.c * (1f - age / 24f), 28);
			}
			if (now - fxMissTick < 18) {
				float a = 1f - (now - fxMissTick) / 18f;
				Rectangle cv = activeCanvas;
				if (cv != Rectangle.Empty) {
					Color edge = new Color(220, 70, 60) * (a * 0.8f);
					sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cv.X, cv.Y, cv.Width, 2), edge);
					sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(cv.X, cv.Bottom - 2, cv.Width, 2), edge);
				}
			}
		}

		private static void OnMiss(string why)
		{
			Misses++;
			fxMissTick = FxNow;
			FxPush(Main.MouseScreen, Color.Tomato * 0.8f);
			SoundEngine.PlaySound(SoundID.MenuTick);
			if (Misses >= 3) Lose(why + " (3 misses)");
		}

		private static void Win()
		{
			Phase = 2;
			Wins++;
			SoundEngine.PlaySound(SoundID.Item29);
			FxPush(Main.MouseScreen, Color.Gold);
			FxPush(Main.MouseScreen + new Vector2(10, 0), Color.White);
			if (TrialMode) {
				Customization.YoyoCustomizationSystem.UnlockTile(Customization.YoyoStationUISystem.OpenTile, TrialFamily);
				Customization.YoyoStationUISystem.SetMessage($"Attuned! {Customization.YoyoCustomizationSystem.FamilyTitle(TrialFamily)} unlocked at THIS block, permanently.");
				Playing = false;
				return;
			}
			if (MiniMode == 1) {
				Player player = Main.LocalPlayer;
				player.AddBuff(BlessingBuff(GameStation), 60 * 60 * 4);
				Customization.YoyoStationUISystem.SetMessage("Resonant! Blessing gained for 4 minutes.");
				Playing = false;
				return;
			}
			Award();
		}

		private static void Lose(string why)
		{
			Phase = 3;
			Playing = false;
			SoundEngine.PlaySound(SoundID.Item94);
			FxPush(Main.MouseScreen, Color.Tomato);
			fxMissTick = FxNow;
			Customization.YoyoStationUISystem.SetMessage("Lost: " + why + " Retry anytime; nothing was spent.");
		}

		private static void Award()
		{
			Player player = Main.LocalPlayer;
			Item held = Customization.YoyoStationActions.HeldYoyo(player);
			if (held == null) {
				Customization.YoyoStationUISystem.SetMessage("Win voided: no yoyo held anymore.");
				Playing = false;
				return;
			}
			if (TargetKind == 3) {
				bool ok = held.Prefix(-2);
				if (held.TryGetGlobalItem<Common.YoyoEnhancementGlobalItem>(out _))
					Common.YoyoEnhancementGlobalItem.RefreshNameOverride(held);
				Customization.YoyoCustomizationGlobalItem.RefreshNameOverride(held);
				Customization.YoyoStationUISystem.SetMessage(ok ? $"Roulette triumph! Free reforge: {held.Name}." : "Roulette triumph! The machine found no better prefix; no charge either way.");
				Playing = false;
				return;
			}
			string fam = FamilyFor(GameStation, TargetKind, TargetTrue);
			string lockMsg = Customization.YoyoCustomizationSystem.TileFamilyLock(Customization.YoyoStationUISystem.OpenTile, fam);
			if (lockMsg != null) {
				Customization.YoyoStationUISystem.SetMessage("Win voided: " + lockMsg);
				Playing = false;
				return;
			}
			// Both gates: the win means nothing without the resources.
			bool ext = GameStation == 4 || TargetTrue;
			if (!Customization.YoyoStationActions.PayCosts(player, TargetKind, ext, TargetId)) {
				Customization.YoyoStationUISystem.SetMessage("Won the game but lack the resources. Nothing applied; farm up and retry.");
				Playing = false;
				Phase = 3;
				return;
			}
			var cust = held.GetGlobalItem<Customization.YoyoCustomizationGlobalItem>();
			string what;
			if (TargetKind == 0) {
				var def = Customization.YoyoTuningDefs.ById(TargetId);
				if (def == null) return;
				cust.TuningId = def.Id;
				what = $"{def.Tier}: {def.Name}";
			}
			else if (TargetKind == 1) {
				var def = Customization.YoyoCalibrationDefs.ById(TargetId);
				if (def == null) return;
				cust.CalibrationId = def.Id;
				what = $"{def.Tier}: {def.Name}";
			}
			else {
				var def = Customization.YoyoMasteryDefs.ById(TargetId);
				if (def == null) return;
				cust.MasteryId = def.Id;
				what = $"{def.Tier}: {def.Name}";
			}
			Customization.YoyoCustomizationGlobalItem.RefreshNameOverride(held);
			Customization.YoyoStationUISystem.SetMessage($"Earned {what} (half resources paid, NO coins). Other modifiers kept.");
			Playing = false;
		}

		// ---- Click entries ----
		public static void ClickTiming(float at)
		{
			float half = BandHalf();
			float center = BandCenter();
			// Forgiving: the strike counts slightly outside the painted band.
			if (System.Math.Abs(at - needle) <= half + 6f && System.Math.Abs(needle - center) <= half + 2.5f) {
				Score++;
				SoundEngine.PlaySound(SoundID.Item4);
				if (IsRite && Stage == 0) {
					if (Score >= 2) { Stage = 1; Score = 0; BuildSeq(5, 1); Customization.YoyoStationUISystem.SetMessage("Rite I complete. Memorize the fold..."); }
					return;
				}
				if (GameStation == 0 ? Score >= 2 : Score >= SetsNeeded()) { Win(); return; }
				Round++;
				needle = Round % 2 == 0 ? 0f : 100f;
				needleDir = Round % 2 == 0 ? 1f : -1f;
			}
			else {
				Misses++;
				fxMissTick = FxNow;
				FxPush(Main.MouseScreen, Color.Tomato * 0.8f);
				SoundEngine.PlaySound(SoundID.MenuTick);
				if (Misses >= 2) { Lose("outside the band."); return; }
			}
		}

		public static void ClickDual()
		{
			float half = System.Math.Max(9f, 13f - Round * 2f - (NeedStars - 1)) + 3f;
			bool a = System.Math.Abs(needle - 30f) <= half;
			bool b = System.Math.Abs(needle2 - 70f) <= half;
			if (a && b) {
				Score++;
				SoundEngine.PlaySound(SoundID.Item4);
				if (IsRite && Stage == 2) {
					if (Score >= 2) { Win(); return; }
					return;
				}
				if (Score >= DualNeeded()) { Win(); return; }
				Round++;
			}
			else {
				Misses++;
				fxMissTick = FxNow;
				FxPush(Main.MouseScreen, Color.Tomato * 0.8f);
				SoundEngine.PlaySound(SoundID.MenuTick);
				if (Misses >= (IsRite ? 3 : 2)) { Lose("the folds misaligned."); return; }
			}
		}

		public static void ClickSeq(int slotClicked)
		{
			int st = GameStation;
			if (showing || seq.Count == 0 || seqLock > 0)
				return;
			int expectedColor = seq[seqInputIdx];
			int clickedColor = PadColor(st, Round, slotClicked);
			if (clickedColor == expectedColor) {
				seqInputIdx++;
				// Snap this dial to its confirmed angle with a green flash.
				if (seqInputIdx - 1 < hintAngles.Count) {
					padSnapAng[slotClicked] = hintAngles[seqInputIdx - 1];
					padSnapT[slotClicked] = 40;
				}
				fxPadSlot = slotClicked;
				fxPadTick = FxNow;
				SoundEngine.PlaySound(SoundID.Item4);
				if (seqInputIdx >= seq.Count) {
					if (IsRite && Stage == 1) {
						Stage = 2; Score = 0; Round = 1;
						Customization.YoyoStationUISystem.SetMessage("Rite II complete. Converge the fold...");
						return;
					}
					if (TrialMode) {
						int need = GameStation == 4 ? 2 : 3;
						if (Round >= need) { Win(); return; }
						Round++;
						BuildSeq(GameStation == 4 ? 5 + Round : 4 + Round, Round);
						return;
					}
					if (Round >= SeqRoundsNeeded()) { Win(); return; }
					BuildPrizeSeq();
				}
			}
			else {
				// One free slip per round: buzzes a warning but never misses.
				if (roundSlack > 0) {
					roundSlack = 0;
					SoundEngine.PlaySound(SoundID.MenuTick);
					FxPush(Main.MouseScreen, Color.Yellow * 0.8f);
					Customization.YoyoStationUISystem.SetMessage("Careful! Next slip counts.");
					return;
				}
				Misses++;
				fxMissTick = FxNow;
				FxPush(Main.MouseScreen, Color.Tomato * 0.8f);
				SoundEngine.PlaySound(SoundID.MenuTick);
				Lose("wrong pad. The route collapsed.");
			}
		}

		public static void ClickQuad(int quad)
		{
			if (flashQuad < 0)
				return;
			if (quad == flashQuad) {
				Score++;
				flashRounds++;
				SoundEngine.PlaySound(SoundID.Item4);
				if (Score >= HitsNeeded()) { Win(); return; }
				NewFlash();
			}
			else {
				Misses++;
				fxMissTick = FxNow;
				FxPush(Main.MouseScreen, Color.Tomato * 0.8f);
				SoundEngine.PlaySound(SoundID.MenuTick);
				if (Misses >= 3) { Lose("struck plated armor thrice."); return; }
				NewFlash();
			}
		}

		public static void ClickGear(int gear)
		{
			float half = 0.65f;
			float a = MathHelper.WrapAngle(gearAngle[gear] - (-MathHelper.PiOver2));
			if (gearStopped[gear]) {
				gearStopped[gear] = false; // re-spin a parked gear
				SoundEngine.PlaySound(SoundID.MenuTick);
				return;
			}
			if (System.Math.Abs(a) <= half) {
				gearStopped[gear] = true;
				SoundEngine.PlaySound(SoundID.Item4);
				if (gearStopped[0] && gearStopped[1] && gearStopped[2]) { Win(); return; }
			}
			else {
				OnMiss("tooth missed the arc.");
			}
		}

		public static void ClickFreq()
		{
			float tol = System.Math.Max(6f, 12f - Score * 2f);
			if (System.Math.Abs(needle - FreqTarget) <= tol) {
				Score++;
				freqPhase += 2.1f;
				SoundEngine.PlaySound(SoundID.Item4);
				if (Score >= 4) { Win(); return; }
			}
			else {
				OnMiss("off-frequency.");
			}
		}

		public static void ClickAngular()
		{
			float half = System.Math.Max(0.32f, 0.6f - Round * 0.07f);
			float a = System.Math.Abs(MathHelper.WrapAngle(bladeAngle - arcCenter));
			if (a <= half + 0.08f) {
				Score++;
				fxBladeHit = FxNow;
				SoundEngine.PlaySound(SoundID.Item4);
				if (Round >= 4) { Win(); return; }
				Round++;
				arcCenter = Main.rand.NextFloat(MathHelper.TwoPi);
			}
			else {
				OnMiss("the blade missed the arc.");
			}
		}

		public static void ClickChargeRelease()
		{
			if (charge >= chargeLo && charge <= chargeHi) {
				Score++;
				SoundEngine.PlaySound(SoundID.Item4);
				if (Score >= 3) { Win(); return; }
				charge = 0f;
				NewChargeZone();
			}
			else {
				charge = 0f;
				NewChargeZone();
				OnMiss("released outside the zone.");
			}
		}

		public static void ClickTraceNode(int node)
		{
			if (node == traceIdx) {
				traceIdx++;
				traceTimer = 0;
				SoundEngine.PlaySound(SoundID.Item4);
				if (traceIdx >= 8) { Win(); return; }
			}
			else {
				OnMiss("wrong node.");
			}
		}

		public static void ClickRing()
		{
			float tr = RingTarget(ActiveCanvas());
			float tol = System.Math.Max(7f, 11f - Score * 2f);
			if (System.Math.Abs(ringR - tr) <= tol) {
				Score++;
				SoundEngine.PlaySound(SoundID.Item4);
				if (Score >= 3) { Win(); return; }
				ringR = RingMax(ActiveCanvas());
				ringAttemptT = 0;
			}
			else {
				OnMiss("the ring slipped past.");
			}
		}

		private static void OnChargeMiss(string why)
		{
			NewChargeZone();
			OnMiss(why);
		}

		// ---- Drawing ----
		public static void DrawTab(SpriteBatch sb, Player player, Rectangle content, int top, int station, int section)
		{
			float y = top + 2;
			int kind = station switch { 0 => 3, 1 => 0, 2 => 2, 3 => 1, 4 => section == 0 ? 0 : 1, _ => 2 };
			bool isTrue = station == 5;
			if (!Playing && GameStation != station)
				TargetId = -1;
			if (TrialMode) {
				DrawTrialRun(sb, player, content, ref y, station);
				return;
			}
			// Prize / Blessing mode toggle.
			Rectangle pb = new(content.X, (int)y, 150, 24);
			Rectangle bb = new(content.X + 156, (int)y, 150, 24);
			if (YoyoPanel.Button(sb, pb, "Prize", "Win a SPECIFIC mod (costs resources too)", MiniMode == 0, false, 0.75f)) { MiniMode = 0; StopRun(); }
			if (YoyoPanel.Button(sb, bb, "Blessing", "Win a 4-minute station buff", MiniMode == 1, false, 0.75f)) { MiniMode = 1; StopRun(); }
			y += 28;
			if (MiniMode == 1) {
				DrawBlessing(sb, player, content, ref y, station);
				return;
			}
			if (station == 4)
				y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 4, y), section == 0 ? "Prize pool: Extended Tuning (Clockwork Cipher)." : "Prize pool: Extended Calibration (drifting band).", Color.LightGray, 0.8f, content.Width - 8) + 2;
			// Compact prize selector: cycles, never overflows.
			if (kind == 3) {
				y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 4, y), "Prize: FREE ordinary reforge (keeps every custom modifier).", Color.Gold, 0.85f, content.Width - 8) + 2;
			}
			else {
				y += DrawPrizeSelector(sb, content, y, station, kind, isTrue) + 2;
			}
			string gate = GateText(station, kind);
			if (gate != null) {
				y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y), "Locked: " + gate, Color.Orange, 0.85f, content.Width - 200) + 2;
				return;
			}
			string fam = FamilyFor(station, kind, isTrue);
			string tileLock = kind == 3 ? null : Customization.YoyoCustomizationSystem.TileFamilyLock(Customization.YoyoStationUISystem.OpenTile, fam);
			bool attuneLocked = tileLock != null && gate == null;
			if (attuneLocked) {
				y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y),
					$"This block is not attuned ({Customization.YoyoCustomizationSystem.FamilyTitle(fam)}). Prizes need attunement first.", Color.Orange, 0.85f, content.Width - 200) + 2;
				Rectangle tr = new(content.Right - 210, (int)y, 200, 30);
				if (YoyoPanel.Button(sb, tr, "Begin Attunement", "Play this block's unlock trial, in this UI", false, false, 0.8f))
					StartTrial(station, fam);
				return;
			}
			y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 4, y), GameHint(station), Color.LightGray, 0.75f, content.Width - 200) + 2;
			Rectangle startBtn = new(content.Right - 190, (int)y - 2, 180, 28);
			bool hasYoyo = Customization.YoyoStationActions.HeldYoyo(player) != null;
			if (!Playing) {
				if (YoyoPanel.Button(sb, startBtn, Phase == 3 ? "Retry" : "Play", "Win, then pay HALF resources (no coins)", false, !hasYoyo || (kind < 3 && TargetId < 0 && station != 0), 0.8f))
					Start(station, kind, TargetId, isTrue);
			}
			else {
				if (YoyoPanel.Button(sb, startBtn, "Give up", "Stop the minigame (nothing spent)", false, false, 0.8f)) {
					StopRun();
					Customization.YoyoStationUISystem.SetMessage("Minigame stopped. Nothing spent.");
					return;
				}
			}
			string status = !Playing
				? (Phase == 2 ? $"Won! Total wins: {Wins}." : Phase == 3 ? "Lost. Retry when ready." : $"Wins: {Wins}. Pick a prize, then Play.")
				: $"Round {Round}  Score {Score}  Misses {Misses}";
			Terraria.Utils.DrawBorderString(sb, status, new Vector2(content.X + 4, y + 4), Color.Cyan, 0.8f);
			y += 32;
			DrawCanvas(sb, content, ref y, station, kind, idleText: station == 0 ? "Roulette idle." : station == 2 ? "Mirror idle." : "Awaiting challenger...");
		}

		private static void DrawBlessing(SpriteBatch sb, Player player, Rectangle content, ref float y, int station)
		{
			int buff = BlessingBuff(station);
			// Buff icon, pulled straight from the buff's own texture.
			var buffInst = ModContent.GetModBuff(buff);
			if (buffInst != null && ModContent.RequestIfExists<Texture2D>(buffInst.Texture, out var texAsset) && texAsset?.Value != null) {
				Texture2D tex = texAsset.Value;
				float scale = 30f / System.Math.Max(tex.Width, tex.Height);
				sb.Draw(tex, new Vector2(content.Right - 42, y), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			}
			int idx = player.FindBuffIndex(buff);
			string fam = BlessingFamily(station);
			if (idx >= 0) {
				int secs = player.buffTime[idx] / 60;
				y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 4, y), $"Blessed: {BlessingName(station)} — {secs / 60}m {secs % 60}s left. Replay to refresh.", Color.LightGreen, 0.8f, content.Width - 200) + 2;
			}
			else {
				y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 4, y), $"Prize: {BlessingName(station)}. Tap when the ring kisses the gold circle, 3 kisses.", Color.Gold, 0.8f, content.Width - 200) + 2;
			}
			if (fam != "") {
				string lockMsg = Customization.YoyoCustomizationSystem.TileFamilyLock(Customization.YoyoStationUISystem.OpenTile, fam);
				if (lockMsg != null) {
					y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 4, y), "Blessing needs this block attuned first (" + Customization.YoyoCustomizationSystem.FamilyTitle(fam) + ").", Color.Orange, 0.85f, content.Width - 200) + 2;
					return;
				}
			}
			Rectangle go = new(content.Right - 190, (int)y - 2, 180, 28);
			if (!Playing) {
				if (YoyoPanel.Button(sb, go, "Begin Resonance", "Start the ring-tap blessing game", false, false, 0.8f))
					StartBlessing(station);
			}
			else {
				if (YoyoPanel.Button(sb, go, "Give up", "Stop (nothing spent)", false, false, 0.8f)) {
					StopRun();
					Customization.YoyoStationUISystem.SetMessage("Resonance faded. Nothing spent.");
					return;
				}
			}
			string status = Playing ? $"Kisses {Score}/3  Misses {Misses}/3" : "Awaiting resonance...";
			Terraria.Utils.DrawBorderString(sb, status, new Vector2(content.X + 4, y + 4), Color.Cyan, 0.8f);
			y += 32;
			DrawCanvas(sb, content, ref y, station, 0, idleText: "The ring sleeps.");
		}

		private static void DrawTrialRun(SpriteBatch sb, Player player, Rectangle content, ref float y, int station)
		{
			string fam = TrialFamily;
			int trial = Customization.YoyoCustomizationSystem.FamilyTrial(fam);
			y += YoyoPanel.DrawTextBlock(sb, new Vector2(content.X + 4, y),
				$"ATTUNEMENT: {Trials.YoyoTrialActions.TrialName(trial)} ({TrialGameName(fam)}) — {TrialBrief(fam)} Win unlocks {Customization.YoyoCustomizationSystem.FamilyTitle(fam)} at THIS block.", Color.Gold, 0.8f, content.Width - 200) + 2;
			Rectangle give = new(content.Right - 190, (int)y - 2, 180, 28);
			if (YoyoPanel.Button(sb, give, "Abandon", "Stop the trial (block stays locked)", false, false, 0.8f)) {
				StopRun();
				Customization.YoyoStationUISystem.SetMessage("Trial abandoned. The block stays locked.");
				return;
			}
			string status = IsRite ? $"Rite stage {Stage + 1}/3  Score {Score}  Misses {Misses}/3" : $"Round {Round}  Score {Score}  Misses {Misses}/3";
			Terraria.Utils.DrawBorderString(sb, status, new Vector2(content.X + 4, y + 4), Color.Cyan, 0.8f);
			y += 32;
			DrawCanvas(sb, content, ref y, station, TargetKind, idleText: "Trial over.");
		}

		private static void DrawCanvas(SpriteBatch sb, Rectangle content, ref float y, int station, int kind, string idleText)
		{
			int canvasH = System.Math.Max(110, content.Bottom - (int)y - 6);
			Rectangle canvas = new(content.X + 4, (int)y, content.Width - 8, canvasH);
			activeCanvas = canvas;
			MinigameSkins.Ensure();
			Color tint = station switch {
				0 => new(180, 160, 120), 1 => new(140, 180, 220), 2 => new(200, 140, 160),
				3 => new(140, 220, 160), 4 => new(180, 160, 240), _ => new(160, 220, 220),
			};
			sb.Draw(TextureAssets.MagicPixel.Value, canvas, new Color(6, 8, 16, 232));
			sb.Draw(TextureAssets.MagicPixel.Value, canvas, new Color(tint.R, tint.G, tint.B, 10));
			DrawStarfield(sb, canvas);
			// Faint blueprint grid.
			for (int gx = canvas.X + 24; gx < canvas.Right - 4; gx += 24)
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(gx, canvas.Y + 4, 1, canvas.Height - 8), new Color(120, 140, 200, 18));
			for (int gy = canvas.Y + 24; gy < canvas.Bottom - 4; gy += 24)
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.X + 4, gy, canvas.Width - 8, 1), new Color(120, 140, 200, 18));
			float pulse = 0.6f + 0.4f * (float)System.Math.Sin(Clock * 0.08f);
			Color edge = new((int)(tint.R * pulse), (int)(tint.G * pulse), (int)(tint.B * pulse));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.X, canvas.Y, canvas.Width, 1), edge);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.X, canvas.Bottom - 1, canvas.Width, 1), edge);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.X, canvas.Y, 1, canvas.Height), edge);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.Right - 1, canvas.Y, 1, canvas.Height), edge);
			// Gold corner brackets.
			int bl = 12;
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.X - 1, canvas.Y - 1, bl, 2), Color.Gold * 0.8f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.X - 1, canvas.Y - 1, 2, bl), Color.Gold * 0.8f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.Right - bl + 1, canvas.Y - 1, bl, 2), Color.Gold * 0.8f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.Right - 1, canvas.Y - 1, 2, bl), Color.Gold * 0.8f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.X - 1, canvas.Bottom - 1, bl, 2), Color.Gold * 0.8f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.X - 1, canvas.Bottom - bl + 1, 2, bl), Color.Gold * 0.8f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.Right - bl + 1, canvas.Bottom - 1, bl, 2), Color.Gold * 0.8f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(canvas.Right - 1, canvas.Bottom - bl + 1, 2, bl), Color.Gold * 0.8f);
			if (Playing && Phase == 1)
				DrawGame(sb, canvas, station, kind);
			else {
				// Mirror idles on smooth dim discs, not just text.
				if (station == 2 && !TrialMode) {
					var (gx, gy, size) = QuadGeom(canvas);
					for (int q = 0; q < 4; q++) {
						Vector2 qc = new(gx + (q % 2) * (size + 8) + size / 2f, gy + (q / 2) * (size + 8) + size / 2f);
						float rad = size / 2f - 2f;
						FillDisc(sb, qc, rad, new Color(38, 42, 70, 255));
						DrawSmoothRing(sb, qc, rad, new Color(90, 100, 150), 36);
					}
				}
				Color c = Phase == 2 ? Color.Gold : Phase == 3 ? Color.Tomato : Color.Gray;
				float pop = Phase == 0 ? 0.9f : 1.1f + 0.1f * (float)System.Math.Sin(FxNow * 0.15f);
				float bob = Phase == 0 ? (float)System.Math.Sin(FxNow * 0.06f) * 3f : 0f;
				Vector2 sz = Terraria.GameContent.FontAssets.MouseText.Value.MeasureString(idleText) * pop;
				Terraria.Utils.DrawBorderString(sb, idleText, new Vector2(canvas.X + (canvas.Width - sz.X) / 2f, canvas.Y + (canvas.Height - sz.Y) / 2f + bob), c, pop);
			}
			DrawFx(sb);
			HandleCanvasClick(canvas, station, kind);
		}

		private static void DrawStarfield(SpriteBatch sb, Rectangle cv)
		{
			for (int i = 0; i < 36; i++) {
				int hx = (i * 7919) % 997;
				int hy = (i * 104729) % 991;
				float fx = cv.X + 6 + (hx / 997f) * (cv.Width - 12);
				float fy = cv.Y + 6 + (hy / 991f) * (cv.Height - 12);
				float tw = 0.25f + 0.35f * (float)System.Math.Abs(System.Math.Sin(Clock * 0.05f + i));
				Color c = i % 5 == 0 ? new Color(150, 180, 255) * tw : Color.White * (tw * 0.5f);
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)fx, (int)fy, 2, 2), c);
			}
		}

		// Compact prize selector: < name [***] >. One row, never overflows.
		private static float DrawPrizeSelector(SpriteBatch sb, Rectangle content, float y, int station, int kind, bool isTrue)
		{
			var names = new System.Collections.Generic.List<(int id, string name)>();
			if (kind == 0) {
				bool ext = station == 4;
				foreach (var d in Customization.YoyoTuningDefs.All) {
					if (d.Extended == ext) names.Add((d.Id, d.Name));
				}
			}
			else if (kind == 1) {
				bool ext = station == 4;
				foreach (var d in Customization.YoyoCalibrationDefs.All) {
					if (d.Extended == ext) names.Add((d.Id, d.Name));
				}
			}
			else {
				foreach (var d in Customization.YoyoMasteryDefs.All) {
					if (d.TrueMastery == isTrue) names.Add((d.Id, d.Name));
				}
			}
			if (names.Count == 0)
				return 0f;
			int sel = names.FindIndex(e => e.id == TargetId);
			if (sel < 0) sel = 0;
			Rectangle prev = new(content.X + 4, (int)y, 44, 26);
			Rectangle next = new(content.X + 52, (int)y, 44, 26);
			if (YoyoPanel.Button(sb, prev, "<", "Previous mod", false, false, 0.85f)) {
				sel = (sel + names.Count - 1) % names.Count;
				PickPrize(station, kind, isTrue, names[sel].id);
			}
			if (YoyoPanel.Button(sb, next, ">", "Next mod", false, false, 0.85f)) {
				sel = (sel + 1) % names.Count;
				PickPrize(station, kind, isTrue, names[sel].id);
			}
			int stars = Customization.YoyoDifficulty.Stars(kind, names[sel].id);
			string label = $"{names[sel].name} [{Customization.YoyoDifficulty.StarString(stars)}]";
			Rectangle nameBox = new(content.X + 104, (int)y, content.Width - 112, 26);
			sb.Draw(TextureAssets.MagicPixel.Value, nameBox, new Color(26, 32, 54, 220));
			Terraria.Utils.DrawBorderString(sb, sel + 1 + "/" + names.Count + "  " + label, new Vector2(nameBox.X + 8, nameBox.Y + 4), Color.White, 0.8f);
			if (nameBox.Contains(Main.MouseScreen.ToPoint())) {
				Main.LocalPlayer.mouseInterface = true;
				Main.hoverItemName = PrizeTip(kind, names[sel].id);
			}
			// Auto-highlight first entry so Play is one click away.
			if (TargetId < 0)
				PickPrize(station, kind, isTrue, names[sel].id, quiet: true);
			string prize = $"Prize: {PrizeShort(kind, TargetId)} — win + half resources (no coins).";
			Terraria.Utils.DrawBorderString(sb, prize, new Vector2(content.X + 4, y + 30), Color.Gold, 0.75f);
			return 52f;
		}

		private static void PickPrize(int station, int kind, bool isTrue, int id, bool quiet = false)
		{
			TargetId = id;
			GameStation = station;
			TargetKind = kind;
			TargetTrue = isTrue;
			if (Playing) StopRun();
			Phase = 0;
			if (!quiet)
				SoundEngine.PlaySound(SoundID.MenuTick);
		}

		private static string PrizeShort(int kind, int id)
		{
			if (kind == 0) return Customization.YoyoTuningDefs.ById(id)?.Name ?? "?";
			if (kind == 1) return Customization.YoyoCalibrationDefs.ById(id)?.Name ?? "?";
			return Customization.YoyoMasteryDefs.ById(id)?.Name ?? "?";
		}

		private static string PrizeTip(int kind, int id)
		{
			int stars = Customization.YoyoDifficulty.Stars(kind, id);
			string diff = $"Difficulty {Customization.YoyoDifficulty.StarString(stars)} (harder games, hungrier costs).";
			var (_, mat, n) = Customization.YoyoStationActions.CostFor(kind, id);
			int half = (n + 1) / 2;
			string cost = $"Win the game + {half}x {Terraria.Lang.GetItemNameValue(mat)} (NO coins on a win; browse pays full).";
			if (kind == 0) { var d = Customization.YoyoTuningDefs.ById(id); return d == null ? "" : $"{d.Name} ({d.Tier})\n{d.Description}\n{diff}\n{cost}"; }
			if (kind == 1) { var d = Customization.YoyoCalibrationDefs.ById(id); return d == null ? "" : $"{d.Name} ({d.Tier})\n{d.Description}\n{diff}\n{cost}"; }
			var m = Customization.YoyoMasteryDefs.ById(id); return m == null ? "" : $"{m.Name} ({m.Tier})\n{m.Description}\n{diff}\n{cost}";
		}

		private static void DrawGame(SpriteBatch sb, Rectangle cv, int station, int kind)
		{
			if (MiniMode == 1 && !TrialMode) { DrawRingGame(sb, cv); return; }
			if (IsRite) {
				if (Stage == 0) DrawTimingGame(sb, cv);
				else if (Stage == 1) DrawSeqGame(sb, cv, 5);
				else DrawDualGame(sb, cv);
				return;
			}
			if (TrialMode) {
				if (TrialFamily == "tuning") DrawGearGame(sb, cv);
				else if (TrialFamily == "calib") DrawFreqGame(sb, cv);
				else if (TrialFamily == "mastery") DrawAngularGame(sb, cv);
				else if (TrialFamily == "exttuning") DrawChargeGame(sb, cv);
				else DrawTraceGame(sb, cv);
				return;
			}
			Engine eng = PrizeEngine(station, kind);
			if (eng == Engine.Roulette || eng == Engine.Timing || eng == Engine.MovingBand) DrawTimingGame(sb, cv);
			else if (eng == Engine.Dual) DrawDualGame(sb, cv);
			else if (eng == Engine.Simon) DrawSeqGame(sb, cv, station);
			else DrawReactionGame(sb, cv);
		}

		private static void Bar(SpriteBatch sb, Rectangle cv, float pos, float half, float bandCenter, Color bandColor)
		{
			int bx = cv.X + 12, bw = cv.Width - 24;
			int by = cv.Y + cv.Height / 2 - 10;
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bx, by, bw, 20), new Color(30, 38, 60, 255));
			// Ruler ticks every 10 units.
			for (int u = 0; u <= 100; u += 10) {
				int tx = bx + (int)(u / 100f * bw);
				bool major = u % 50 == 0;
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(tx, by + (major ? 0 : 6), 1, major ? 20 : 8), new Color(120, 140, 200, major ? 90 : 50));
			}
			// Needle motion streak.
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bx, by + 8, bw, 4), new Color(60, 70, 110, 255));
			int zx = bx + (int)((bandCenter - half) / 100f * bw);
			int zw = (int)(half * 2f / 100f * bw);
			float glow = 0.7f + 0.3f * (float)System.Math.Sin(Clock * 0.12f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(zx, by, zw, 20), bandColor * glow);
			// Crisp zone edges: white ticks exactly where the hit window starts/ends.
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(zx - 1, by - 4, 2, 28), Color.White * 0.9f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(zx + zw - 1, by - 4, 2, 28), Color.White * 0.9f);
			int mx = bx + (int)(pos / 100f * bw);
			// Echo ghosts trailing the needle.
			for (int k = 1; k < needleEcho.Length; k++) {
				int ex = bx + (int)(needleEcho[k] / 100f * bw);
				FillDisc(sb, new Vector2(ex, by + 10), 2.5f, Color.White * (0.28f - k * 0.04f));
			}
			// Dark halo behind the needle so it pops on any background.
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(mx - 3, by - 9, 6, 38), new Color(0, 0, 0, 160));
			if (!DrawSkin(sb, MinigameSkins.Needle, new Vector2(mx, by + 10), 20f, 0f, Color.White)) {
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(mx - 2, by - 8, 4, 36), Color.White);
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(mx - 5, by - 4, 10, 3), Color.White * 0.6f);
				FillDisc(sb, new Vector2(mx, by - 10), 4f, Color.White);
			}
			// Rounded end caps.
			FillDisc(sb, new Vector2(bx, by + 10), 10f, new Color(50, 60, 96, 255));
			FillDisc(sb, new Vector2(bx + bw, by + 10), 10f, new Color(50, 60, 96, 255));
		}

		private static void DrawTimingGame(SpriteBatch sb, Rectangle cv)
		{
			float half = BandHalf();
			float center = BandCenter();
			Bar(sb, cv, needle, half, center, ActiveEngine() == Engine.MovingBand ? new Color(150, 200, 255) : new Color(200, 160, 60));
			string t = GameStation == 0 ? $"Stop in gold ({Score}/2)" : TrialMode ? $"Sets {Score}/5" : $"Sets landed {Score}/{SetsNeeded()}";
			Terraria.Utils.DrawBorderString(sb, t, new Vector2(cv.X + 8, cv.Y + 6), Color.LightGray, 0.75f);
			Terraria.Utils.DrawBorderString(sb, ActiveEngine() == Engine.MovingBand ? "The band DRIFTS. CLICK in it!" : "CLICK the bar!", new Vector2(cv.X + 8, cv.Bottom - 22), Color.Yellow, 0.8f);
		}

		private static void DrawDualGame(SpriteBatch sb, Rectangle cv)
		{
			float half = System.Math.Max(9f, 13f - Round * 2f - (NeedStars - 1));
			int bx = cv.X + 12, bw = cv.Width - 24;
			int by1 = cv.Y + cv.Height / 3 - 9, by2 = cv.Y + 2 * cv.Height / 3 - 9;
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bx, by1, bw, 18), new Color(30, 38, 60, 255));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bx, by2, bw, 18), new Color(30, 60, 38, 255));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bx + (int)((30f - half) / 100f * bw), by1, (int)(half * 2f / 100f * bw), 18), new Color(120, 170, 255));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bx + (int)((70f - half) / 100f * bw), by2, (int)(half * 2f / 100f * bw), 18), new Color(150, 255, 170));
			FillDisc(sb, new Vector2(bx, by1 + 9), 9f, new Color(50, 60, 96, 255));
			FillDisc(sb, new Vector2(bx + bw, by1 + 9), 9f, new Color(50, 60, 96, 255));
			FillDisc(sb, new Vector2(bx, by2 + 9), 9f, new Color(50, 60, 96, 255));
			FillDisc(sb, new Vector2(bx + bw, by2 + 9), 9f, new Color(50, 60, 96, 255));
			Vector2 m1 = new(bx + needle / 100f * bw, by1 + 9);
			Vector2 m2 = new(bx + needle2 / 100f * bw, by2 + 9);
			bool a = System.Math.Abs(needle - 30f) <= half + 3f;
			bool b = System.Math.Abs(needle2 - 70f) <= half + 3f;
			// Convergence beam: the closer both needles, the hotter the link.
			if (a || b)
				DrawLine(sb, m1, m2, new Color(150, 220, 255, 90));
			if (a && b) {
				DrawLine(sb, m1, m2, Color.Gold);
				DrawLine(sb, m1 + new Vector2(0, 1), m2 + new Vector2(0, 1), Color.White * 0.8f);
				FillDisc(sb, (m1 + m2) / 2f, 5f + (float)System.Math.Sin(Clock * 0.2f) * 1.5f, Color.White);
				// Spark shuttling down the live link.
				float tt = (FxNow * 0.02f) % 1f;
				FillDisc(sb, Vector2.Lerp(m1, m2, tt), 3.5f, Color.White);
			}
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)m1.X - 3, (int)m1.Y - 16, 6, 32), new Color(0, 0, 0, 160));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)m1.X - 2, (int)m1.Y - 15, 4, 30), Color.White);
			FillDisc(sb, m1 + new Vector2(0, -17), 4f, Color.White);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)m2.X - 3, (int)m2.Y - 16, 6, 32), new Color(0, 0, 0, 160));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)m2.X - 2, (int)m2.Y - 15, 4, 30), Color.White);
			FillDisc(sb, m2 + new Vector2(0, -17), 4f, Color.White);
			Terraria.Utils.DrawBorderString(sb, IsRite ? $"Converge! ({Score}/2)" : $"Converge both! ({Score}/{DualNeeded()})", new Vector2(cv.X + 8, cv.Y + 6), Color.LightGray, 0.75f);
			Terraria.Utils.DrawBorderString(sb, "CLICK when BOTH needles are in bands!", new Vector2(cv.X + 8, cv.Bottom - 22), Color.Yellow, 0.8f);
			DrawPips(sb, new Vector2(cv.Right - 8 - (IsRite ? 2 : DualNeeded()) * 14f, cv.Y + 10), Score, IsRite ? 2 : DualNeeded());
		}

		private static (int sx, int sy, int pw, int ph) SeqGeom(Rectangle cv, int pads)
		{
			int pw = System.Math.Min(72, (cv.Width - 24 - (pads - 1) * 10) / pads);
			int totalW = pads * pw + (pads - 1) * 10;
			int sx = cv.X + (cv.Width - totalW) / 2;
			int sy = cv.Y + (cv.Height - 64) / 2;
			return (sx, sy, pw, 64);
		}

		private static void DrawSeqGame(SpriteBatch sb, Rectangle cv, int station)
		{
			int pads = PadCount(station);
			var (sx, sy, pw, ph) = SeqGeom(cv, pads);
			bool locked = showing || seqLock > 0;
			for (int slot = 0; slot < pads; slot++) {
				Vector2 center = new(sx + slot * (pw + 10) + pw / 2f, sy + ph / 2f);
				float rad = System.Math.Min(pw, ph) / 2f - 3f;
				int color = PadColor(station, Round, slot);
				bool lit = showing && seqShowIdx < seq.Count && seq[seqShowIdx] == color && seqShowTimer < 44;
				bool hinted = seq.Contains(color);
				// Smooth hover physics: swell toward the cursor, ease back after.
				// No hover ring at all; locked boards ignore the mouse entirely.
				bool hover = !locked && Vector2.Distance(Main.MouseScreen, center) <= rad + 8f;
				float want = hover ? 1.18f : 1f;
				padScale[slot] += (want - padScale[slot]) * 0.25f;
				float r2 = rad * padScale[slot];
				// Flat smooth disc + ring: no bubble bevel anywhere.
				// Spriter skin replaces the body (markers stay drawn on top).
				bool skinned = DrawSkin(sb, MinigameSkins.Pad, center, r2, 0f, lit ? Color.White : Color.LightGray);
				if (!skinned) {
					FillDisc(sb, center, r2, new Color(30, 36, 62, 255));
					DrawSmoothRing(sb, center, r2, PadDrawColor(color), 36);
				}
				if (lit) {
					FillDisc(sb, center, r2 * 0.55f, Color.Gold);
					DrawSmoothRing(sb, center, r2 + 3f, Color.Gold, 36);
				}
				// Dials that were never hinted wear green edges from the start.
				if (!hinted)
					DrawSmoothRing(sb, center, r2, new Color(140, 230, 150), 32);
				// Pointer line, like a compass blade.
				Vector2 tip = center + new Vector2((float)System.Math.Cos(padAngle[slot]), (float)System.Math.Sin(padAngle[slot])) * (r2 - 5f);
				DrawLine(sb, center, tip, new Color(0, 0, 0, 160));
				DrawLine(sb, center, tip, Color.White * 0.9f);
				FillDisc(sb, tip, 3.5f, lit ? Color.White : PadDrawColor(color));
				FillDisc(sb, center, 3.5f, Color.Gold * 0.85f);
				// Click shockwave: expanding double ring + collapsing flash core.
				if (slot == fxPadSlot && FxNow - fxPadTick < 22) {
					float age = FxNow - fxPadTick;
					DrawSmoothRing(sb, center, r2 + 3f + age * 1.8f, Color.Gold * (1f - age / 22f), 36);
					DrawSmoothRing(sb, center, r2 + 3f + age * 1.1f, Color.White * (1f - age / 22f), 36);
					FillDisc(sb, center, r2 * 0.6f * (1f - age / 30f), Color.White * (1f - age / 22f));
				}
				// Snapped dials hold a green seal while gliding to confirmation.
				if (padSnapT[slot] > 0)
					DrawSmoothRing(sb, center, r2 + 2f, new Color(140, 255, 150), 32);
				if (station == 4 && Round >= 2)
					Terraria.Utils.DrawBorderString(sb, "G" + (color + 1), new Vector2(center.X - 8, center.Y + rad + 2), Color.White, 0.7f);
			}
			// Living sequence indicator: pulsing mode banner + per-beat pips.
			string mode = showing ? "WATCH" : "REPEAT";
			Color modeC = showing ? new Color(150, 200, 255) : Color.Gold;
			float pulse = 1f + (showing ? 0.08f * (float)System.Math.Sin(FxNow * 0.15f) : 0f);
			Terraria.Utils.DrawBorderString(sb, mode, new Vector2(cv.X + 8, cv.Y + 4), modeC, 0.85f * pulse);
			int total = System.Math.Max(1, seq.Count);
			int done = showing ? seqShowIdx : seqInputIdx;
			float pipW = total * 14f;
			Vector2 pip0 = new(cv.X + cv.Width - 8 - pipW, cv.Y + 8);
			for (int i = 0; i < total; i++) {
				Vector2 p = pip0 + new Vector2(i * 14f + 4f, 4f);
				bool isDone = i < done;
				bool isCur = i == done;
				if (isDone)
					FillDisc(sb, p, 4f, Color.Gold);
				else if (isCur)
					DrawSmoothRing(sb, p, 4f + (float)System.Math.Sin(FxNow * 0.2f) * 1.5f, Color.Gold, 14);
				else
					FillDisc(sb, p, 2.5f, new Color(120, 130, 170, 160));
			}
			Terraria.Utils.DrawBorderString(sb, $"Round {Round}", new Vector2(cv.X + 8, cv.Y + 24), Color.Gray, 0.7f);
			if (station == 4 && Round >= 2)
				Terraria.Utils.DrawBorderString(sb, "Gears spun: follow COLORS!", new Vector2(cv.X + 8, cv.Bottom - 22), Color.Gold, 0.8f);
		}

		private static (int gx, int gy, int size) QuadGeom(Rectangle cv)
		{
			int size = System.Math.Min(74, System.Math.Min((cv.Height - 44) / 2, (cv.Width - 28) / 2));
			size = System.Math.Max(34, size);
			int gx = cv.X + (cv.Width - size * 2 - 8) / 2;
			int gy = cv.Y + 32;
			return (gx, gy, size);
		}

		private static void DrawReactionGame(SpriteBatch sb, Rectangle cv)
		{
			var (gx, gy, size) = QuadGeom(cv);
			Vector2 mid = new(gx + size + 4f, gy + size + 4f);
			DrawLine(sb, mid + new Vector2(-size - 14f, 0f), mid + new Vector2(size + 14f, 0f), new Color(90, 100, 150, 70));
			DrawLine(sb, mid + new Vector2(0f, -size - 14f), mid + new Vector2(0f, size + 14f), new Color(90, 100, 150, 70));
			for (int q = 0; q < 4; q++) {
				Vector2 c = new(gx + (q % 2) * (size + 8) + size / 2f, gy + (q / 2) * (size + 8) + size / 2f);
				float rad = size / 2f - 2f;
				bool exposed = q == flashQuad;
				bool hover = !exposed && Vector2.Distance(Main.MouseScreen, c) <= rad + 6f;
				// Flat smooth disc + ring like memory: no bubble bevel.
				FillDisc(sb, c, rad, exposed ? new Color(200, 160, 60, 255) : new Color(38, 42, 70, 255));
				DrawSmoothRing(sb, c, rad, exposed ? Color.Gold : hover ? Color.White : new Color(90, 100, 150), 36);
				if (exposed)
					FillDisc(sb, c, rad * 0.5f, Color.White);
				if (exposed) {
					// Radar ring shrinks with the remaining flash time: the telegraph.
					float frac = flashSpan > 0 ? (float)flashTimer / flashSpan : 0f;
					DrawSmoothRing(sb, c, rad + 4f + (1f - frac) * 10f, Color.Gold * (0.4f + 0.6f * frac), 36);
				}
				if (exposed) {
					float frac = flashSpan > 0 ? (float)flashTimer / flashSpan : 0f;
					Terraria.Utils.DrawBorderString(sb, "HIT", new Vector2(c.X - 12, c.Y + rad * 0.45f), Color.White, 0.7f + 0.25f * frac);
				}
			}
			Terraria.Utils.DrawBorderString(sb, $"Hits {Score}/{HitsNeeded()}  Miss {Misses}/3", new Vector2(cv.X + 8, cv.Y + 6), Color.LightGray, 0.75f);
			DrawPips(sb, new Vector2(cv.Right - 8 - HitsNeeded() * 14f, cv.Y + 10), Score, HitsNeeded());
		}

		private static Vector2 GearCenter(Rectangle cv, int i)
		{
			float cx = cv.X + cv.Width * (0.25f + 0.25f * i);
			float cy = cv.Y + cv.Height / 2f;
			return new Vector2(cx, cy);
		}

		private static float GearRadius(Rectangle cv) => System.Math.Min(46f, cv.Height * 0.28f);

		private static void DrawCircleOutline(SpriteBatch sb, Vector2 c, float rad, Color color, int dots = 44, int dot = 3)
		{
			for (int i = 0; i < dots; i++) {
				float ang = i / (float)dots * MathHelper.TwoPi;
				Vector2 p = c + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * rad;
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)p.X - dot / 2, (int)p.Y - dot / 2, dot, dot), color);
			}
		}

		// Smooth continuous ring (no dusty gaps): polyline around the circle.
		private static void DrawSmoothRing(SpriteBatch sb, Vector2 c, float rad, Color color, int segs = 64)
		{
			Vector2 prev = c + new Vector2(rad, 0f);
			for (int i = 1; i <= segs; i++) {
				float ang = i / (float)segs * MathHelper.TwoPi;
				Vector2 p = c + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * rad;
				DrawLine(sb, prev, p, color);
				prev = p;
			}
		}

		// Solid polygon fill (scanline). Powers real gear silhouettes.
		private static void FillPolygon(SpriteBatch sb, System.Collections.Generic.List<Vector2> pts, Color color)
		{
			if (pts == null || pts.Count < 3)
				return;
			float minY = pts[0].Y, maxY = pts[0].Y;
			foreach (Vector2 p in pts) {
				if (p.Y < minY) minY = p.Y;
				if (p.Y > maxY) maxY = p.Y;
			}
			var xs = new System.Collections.Generic.List<float>(8);
			for (int y = (int)minY; y <= (int)maxY; y++) {
				xs.Clear();
				for (int i = 0; i < pts.Count; i++) {
					Vector2 a = pts[i];
					Vector2 b = pts[(i + 1) % pts.Count];
					if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y)) {
						float t = (y - a.Y) / (b.Y - a.Y);
						xs.Add(a.X + t * (b.X - a.X));
					}
				}
				xs.Sort();
				for (int k = 0; k + 1 < xs.Count; k += 2) {
					int x0 = (int)xs[k];
					int x1 = (int)xs[k + 1];
					if (x1 >= x0)
						sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x0, y, x1 - x0 + 1, 1), color);
				}
			}
		}

		// Gear silhouette points: trapezoid teeth around a body circle.
		private static System.Collections.Generic.List<Vector2> GearOutline(Vector2 c, float rBody, float rTip, int teeth, float phase)
		{
			var pts = new System.Collections.Generic.List<Vector2>(teeth * 4);
			float step = MathHelper.TwoPi / teeth;
			for (int k = 0; k < teeth; k++) {
				float a = phase + k * step;
				float halfTooth = step * 0.22f;
				float halfGap = step * 0.5f;
				pts.Add(c + new Vector2((float)System.Math.Cos(a - halfGap), (float)System.Math.Sin(a - halfGap)) * rBody);
				pts.Add(c + new Vector2((float)System.Math.Cos(a - halfTooth), (float)System.Math.Sin(a - halfTooth)) * rTip);
				pts.Add(c + new Vector2((float)System.Math.Cos(a + halfTooth), (float)System.Math.Sin(a + halfTooth)) * rTip);
				pts.Add(c + new Vector2((float)System.Math.Cos(a + halfGap), (float)System.Math.Sin(a + halfGap)) * rBody);
			}
			return pts;
		}

		// Composite glossy disc: halo + body + top-left bevel highlight + core.
		private static void DrawGlossDisc(SpriteBatch sb, Vector2 c, float rad, Color body, Color ring, bool lit)
		{
			if (lit)
				DrawSmoothRing(sb, c, rad + 4f, ring, 40);
			FillDisc(sb, c, rad, body);
			DrawSmoothRing(sb, c, rad, ring, 40);
			FillDisc(sb, c + new Vector2(-rad * 0.25f, -rad * 0.3f), rad * 0.35f, Color.White * 0.25f);
			if (lit)
				FillDisc(sb, c, rad * 0.55f, Color.White);
		}

		// Filled disc via scanlines. Use for pads, nodes, quadrants, markers.
		private static void FillDisc(SpriteBatch sb, Vector2 c, float rad, Color color)
		{
			int r = System.Math.Max(1, (int)rad);
			for (int dy = -r; dy <= r; dy++) {
				int hw = (int)System.Math.Sqrt(r * r - dy * dy);
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)c.X - hw, (int)c.Y + dy, hw * 2 + 1, 1), color);
			}
		}

		// Small progress pips: filled gold vs smooth hollow rings.
		private static void DrawPips(SpriteBatch sb, Vector2 origin, int score, int need)
		{
			for (int i = 0; i < need; i++) {
				Vector2 p = origin + new Vector2(i * 14f, 0f);
				if (i < score) {
					FillDisc(sb, p, 4f, Color.Gold);
				}
				else {
					DrawSmoothRing(sb, p, 4f, Color.Gray, 12);
				}
			}
		}

		private static void DrawGearGame(SpriteBatch sb, Rectangle cv)
		{
			float r = GearRadius(cv);
			// Slow watermark gear behind everything, tucked aside so the middle
			// gear's tooth never blends into it.
			Vector2 wm = new(cv.X + cv.Width * 0.5f, cv.Y + cv.Height * 0.5f);
			FillPolygon(sb, GearOutline(wm, System.Math.Min(cv.Width, cv.Height) * 0.3f, System.Math.Min(cv.Width, cv.Height) * 0.3f + 6f, 12, Clock * 0.004f), new Color(90, 105, 150, 8));
			for (int i = 0; i < 3; i++) {
				Vector2 c = GearCenter(cv, i);
				// Hover halo: the gear under the cursor answers back.
				if (!gearStopped[i] && Vector2.Distance(Main.MouseScreen, c) <= r + 14f)
					DrawSmoothRing(sb, c, r + 11f, Color.White * (0.5f + 0.2f * (float)System.Math.Sin(FxNow * 0.15f)), 44);
				// One solid toothed silhouette: no scattered dots.
				Color steel = new(122, 142, 182);
				Color steelDark = gearStopped[i] ? new Color(52, 112, 90, 255) : new Color(44, 52, 84, 255);
				Color gold = new(212, 172, 82);
				FillPolygon(sb, GearOutline(c, r, r + 7f, 12, gearAngle[i]), steelDark);
				if (DrawSkin(sb, MinigameSkins.Gear, c, r + 7f, gearAngle[i], gearStopped[i] ? new Color(150, 220, 180) : Color.White)) {
					// Skinned body still gets hub + markers below for readability.
				}
				DrawSmoothRing(sb, c, r - 4f, steel, 48);
				for (int s = 0; s < 4; s++) {
					float ang = s / 4f * MathHelper.Pi + gearAngle[i];
					Vector2 tip = c + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * (r - 8f);
					DrawLine(sb, c, tip, steel);
				}
				FillDisc(sb, c, 7f, gold * 0.9f);
				FillDisc(sb, c, 3f, new Color(30, 34, 58));
				// Gold arc gate at the top.
				for (int g = -2; g <= 2; g++) {
					float ang = -MathHelper.PiOver2 + g * 0.13f;
					Vector2 ap = c + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * (r + 7f);
					FillDisc(sb, ap, 3.5f, gold);
				}
				// Marked tooth: rides the rim, glows live inside the arc.
				float a = System.Math.Abs(MathHelper.WrapAngle(gearAngle[i] + MathHelper.PiOver2));
				bool inArc = a <= 0.65f;
				Vector2 tooth = c + new Vector2((float)System.Math.Cos(gearAngle[i]), (float)System.Math.Sin(gearAngle[i])) * (r + 7f);
				// Dark collar first so the bright tooth pops on ANY background.
				FillDisc(sb, tooth, 8f, new Color(8, 10, 20, 220));
				FillDisc(sb, tooth, 6f, inArc ? new Color(150, 255, 160) : Color.White);
				if (inArc)
					DrawSmoothRing(sb, tooth, 9f, new Color(150, 255, 160), 24);
				string tag = gearStopped[i] ? "HELD" : inArc ? "NOW!" : "G" + (i + 1);
				Terraria.Utils.DrawBorderString(sb, tag, new Vector2(c.X - 14, c.Y + r + 8), gearStopped[i] ? Color.LightGreen : inArc ? Color.Gold : Color.LightGray, 0.7f);
			}
			Terraria.Utils.DrawBorderString(sb, "Click a gear to STOP its bright tooth in the gold arc. Click a held gear to re-spin.", new Vector2(cv.X + 8, cv.Bottom - 22), Color.Yellow, 0.7f);
		}

		private static void DrawFreqGame(SpriteBatch sb, Rectangle cv)
		{
			float tol = System.Math.Max(6f, 12f - Score * 2f);
			int bx = cv.X + 12, bw = cv.Width - 24;
			int by = cv.Y + cv.Height / 2 - 10;
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bx, by, bw, 20), new Color(30, 38, 60, 255));
			// Faint future path of the wandering ghost.
			for (int dt = 0; dt <= 120; dt += 10) {
				float future = 50f + 30f * (float)System.Math.Sin((Clock + dt) * 0.02f + freqPhase);
				int fx = bx + (int)(future / 100f * bw);
				FillDisc(sb, new Vector2(fx, by + 10), 2f, new Color(150, 200, 255, 90));
			}
			// Wandering target ghost with white core.
			int tx = bx + (int)(FreqTarget / 100f * bw);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(tx - 3, by - 10, 6, 40), new Color(150, 200, 255, 160));
			FillDisc(sb, new Vector2(tx, by + 10), 3.5f, Color.White);
			// Dial echo trail + knob disc.
			for (int k = 1; k < freqEcho.Length; k++) {
				int ex = bx + (int)(freqEcho[k] / 100f * bw);
				FillDisc(sb, new Vector2(ex, by + 10), 2.5f, Color.White * (0.3f - k * 0.03f));
			}
			int mx = bx + (int)(needle / 100f * bw);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(mx - 3, by - 9, 6, 38), new Color(0, 0, 0, 160));
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(mx - 2, by - 8, 4, 36), Color.White);
			FillDisc(sb, new Vector2(mx, by - 10), 4f, Color.White);
			Terraria.Utils.DrawBorderString(sb, $"Lock the dial onto the ghost ({Score}/4)", new Vector2(cv.X + 8, cv.Y + 6), Color.LightGray, 0.75f);
			Terraria.Utils.DrawBorderString(sb, "CLICK to lock!", new Vector2(cv.X + 8, cv.Bottom - 22), Color.Yellow, 0.8f);
			DrawPips(sb, new Vector2(cv.Right - 8 - 4 * 14f, cv.Y + 10), Score, 4);
		}

		private static void DrawAngularGame(SpriteBatch sb, Rectangle cv)
		{
			Vector2 c = new(cv.X + cv.Width / 2f, cv.Y + cv.Height / 2f + 6f);
			float r = System.Math.Min(cv.Width, cv.Height) * 0.32f;
			// Dial furniture: tick ring, cardinals, faint crosshair.
			for (int k = 0; k < 48; k++) {
				float ang = k / 48f * MathHelper.TwoPi;
				Vector2 p = c + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * r;
				bool card = k % 12 == 0;
				FillDisc(sb, p, card ? 3f : 1.5f, card ? new Color(160, 170, 210) : new Color(90, 100, 140));
			}
			DrawLine(sb, c + new Vector2(-r - 8f, 0f), c + new Vector2(r + 8f, 0f), new Color(70, 80, 120, 90));
			DrawLine(sb, c + new Vector2(0f, -r - 8f), c + new Vector2(0f, r + 8f), new Color(70, 80, 120, 90));
			FillDisc(sb, c, r, new Color(24, 30, 52, 255));
			DrawSmoothRing(sb, c, r, new Color(120, 130, 170), 56);
			float half = System.Math.Max(0.32f, 0.6f - Round * 0.07f);
			// Gold arc wedge (polyline, not a lonely square).
			Vector2 prev = c + new Vector2((float)System.Math.Cos(arcCenter - half), (float)System.Math.Sin(arcCenter - half)) * r;
			for (int i = 1; i <= 20; i++) {
				float ang = arcCenter - half + (2f * half) * i / 20f;
				Vector2 p = c + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * r;
				DrawLine(sb, prev, p, Color.Gold);
				prev = p;
			}
			// Motion-smear wedge trailing the blade.
			for (int k = 1; k <= 8; k++) {
				float ang = bladeAngle - k * 0.03f;
				Vector2 p = c + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * (r - 4f);
				FillDisc(sb, p, 2.5f, Color.White * (0.35f - k * 0.04f));
			}
			// Recorded trail ghosts.
			for (int k = 1; k < bladeTrail.Length; k++) {
				Vector2 p = c + new Vector2((float)System.Math.Cos(bladeTrail[k]), (float)System.Math.Sin(bladeTrail[k])) * (r - 4f);
				FillDisc(sb, p, 2f, new Color(150, 200, 255) * (0.3f - k * 0.02f));
			}
			Vector2 tip = c + new Vector2((float)System.Math.Cos(bladeAngle), (float)System.Math.Sin(bladeAngle)) * (r - 4f);
			DrawLine(sb, c, tip, new Color(0, 0, 0, 160));
			DrawLine(sb, c, tip - Vector2.UnitX, Color.White);
			FillDisc(sb, tip, 4.5f, Color.White);
			FillDisc(sb, c, 4f, Color.Gold);
			if (FxNow - fxBladeHit < 20)
				DrawSmoothRing(sb, tip, 6f + (FxNow - fxBladeHit) * 2f, Color.Gold * (1f - (FxNow - fxBladeHit) / 20f), 28);
			Terraria.Utils.DrawBorderString(sb, $"Strike when the blade enters gold ({Score}/4)", new Vector2(cv.X + 8, cv.Y + 6), Color.LightGray, 0.75f);
		}

		private static void DrawChargeGame(SpriteBatch sb, Rectangle cv)
		{
			int bx = cv.X + 12, bw = cv.Width - 24;
			int by = cv.Y + cv.Height / 2 - 12;
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bx, by, bw, 24), new Color(30, 38, 60, 255));
			FillDisc(sb, new Vector2(bx, by + 12), 12f, new Color(50, 60, 96, 255));
			FillDisc(sb, new Vector2(bx + bw, by + 12), 12f, new Color(50, 60, 96, 255));
			// Living energy fill: segmented bands surging right + rising sparks.
			if (charge > 1f) {
				int cx = bx + (int)(charge / 100f * bw);
				int segs = 14;
				for (int s = 0; s < segs; s++) {
					int sx0 = bx + (cx - bx) * s / segs;
					int sx1 = bx + (cx - bx) * (s + 1) / segs;
					float wave = 0.55f + 0.45f * (float)System.Math.Sin(FxNow * 0.2f - s * 0.9f);
					sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(sx0, by + 3, System.Math.Max(1, sx1 - sx0), 18), new Color(120, 200, 255) * (0.35f + 0.45f * wave));
				}
				for (int k = 0; k < 5; k++) {
					float ph = (FxNow * 0.05f + k * 0.35f) % 1f;
					float sparkX = bx + 8 + ph * System.Math.Max(1f, cx - bx - 16);
					float sparkY = by + 20 - ph * 26f;
					FillDisc(sb, new Vector2(sparkX, sparkY), 2.5f * (1f - ph * 0.5f), Color.White * (1f - ph));
				}
				// Sparking fill tip.
				FillDisc(sb, new Vector2(cx, by + 12), 5f, Color.White);
				DrawSmoothRing(sb, new Vector2(cx, by + 12), 8f + (float)System.Math.Sin(FxNow * 0.3f) * 2f, new Color(150, 220, 255), 20);
			}
			int zx = bx + (int)(chargeLo / 100f * bw);
			int zw = (int)((chargeHi - chargeLo) / 100f * bw);
			float zpulse = 0.75f + 0.25f * (float)System.Math.Sin(FxNow * 0.15f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(zx, by, zw, 24), new Color(150, 220, 150) * zpulse);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(zx - 1, by - 3, 2, 30), Color.White * 0.85f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(zx + zw - 1, by - 3, 2, 30), Color.White * 0.85f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bx + (int)(charge / 100f * bw) - 2, by - 6, 4, 36), Color.White);
			FillDisc(sb, new Vector2(bx + charge / 100f * bw, by - 8), 4f, Color.White);
			// Overcharge edge burns brighter the fuller the meter gets.
			float danger = charge / 100f;
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bx + bw - 4, by - 6, 4, 36), Color.Red * (0.5f + danger * 0.5f));
			DrawSmoothRing(sb, new Vector2(bx + bw - 2, by + 12), 6f + danger * 6f, Color.Red * danger, 20);
			Terraria.Utils.DrawBorderString(sb, $"HOLD to charge, RELEASE in green ({Score}/3). Red edge = overcharge!", new Vector2(cv.X + 8, cv.Y + 6), Color.LightGray, 0.7f);
		}

		private static Vector2 TraceNode(Rectangle cv, int i)
		{
			float fx = cv.X + cv.Width * (0.12f + 0.76f * i / 7f);
			float fy = cv.Y + cv.Height / 2f + (float)System.Math.Sin(i * 1.4f) * cv.Height * 0.28f;
			return new Vector2(fx, fy);
		}

		private static void DrawTraceGame(SpriteBatch sb, Rectangle cv)
		{
			for (int i = 1; i < 8; i++)
				DrawLine(sb, TraceNode(cv, i - 1), TraceNode(cv, i), new Color(60, 70, 110));
			// Molten trail through everything already traced.
			for (int i = 1; i < traceIdx && i < 8; i++) {
				DrawLine(sb, TraceNode(cv, i - 1), TraceNode(cv, i), new Color(150, 220, 160));
				DrawLine(sb, TraceNode(cv, i - 1) + new Vector2(0, 1), TraceNode(cv, i) + new Vector2(0, 1), new Color(150, 220, 160, 120));
			}
			// Energy packet traveling the finished trail on loop.
			if (traceIdx >= 2) {
				float total = 0f;
				for (int i = 1; i < traceIdx && i < 8; i++)
					total += Vector2.Distance(TraceNode(cv, i - 1), TraceNode(cv, i));
				if (total > 1f) {
					float d = (FxNow * 2.2f) % total;
					Vector2 pp = TraceNode(cv, 0);
					for (int i = 1; i < traceIdx && i < 8; i++) {
						Vector2 a = TraceNode(cv, i - 1), b = TraceNode(cv, i);
						float seg = Vector2.Distance(a, b);
						if (d <= seg) { pp = Vector2.Lerp(a, b, d / seg); break; }
						d -= seg;
						pp = b;
					}
					FillDisc(sb, pp, 4.5f, Color.White);
					DrawSmoothRing(sb, pp, 7f + (float)System.Math.Sin(FxNow * 0.25f) * 2f, new Color(150, 255, 170), 20);
				}
			}
			for (int i = 0; i < 8; i++) {
				Vector2 p = TraceNode(cv, i);
				Color c = i < traceIdx ? new Color(120, 200, 140) : i == traceIdx ? new Color(200, 160, 60) : new Color(50, 56, 86);
				float breathe = i == traceIdx ? 1f : 1f + 0.08f * (float)System.Math.Sin(FxNow * 0.08f + i * 0.9f);
				float r = (i == traceIdx ? 9f : 7f) * breathe;
				if (!DrawSkin(sb, MinigameSkins.Node, p, r, 0f, Color.White))
					FillDisc(sb, p, r, c);
				if (i == traceIdx) {
					DrawSmoothRing(sb, p, r + 4f, Color.Gold, 24);
					// Orbiting sparks around the live node.
					for (int k = 0; k < 3; k++) {
						float ang = FxNow * 0.1f + k * 2.094f;
						Vector2 op = p + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * (r + 8f);
						FillDisc(sb, op, 2.5f, Color.Gold);
					}
				}
				else {
					DrawSmoothRing(sb, p, r + 2f, c, 20);
				}
				Terraria.Utils.DrawBorderString(sb, "" + (i + 1), new Vector2(p.X - 4, p.Y - 26), Color.Gray, 0.65f);
			}
			Terraria.Utils.DrawBorderString(sb, $"Trace the circuit in order ({traceIdx}/8)", new Vector2(cv.X + 8, cv.Y + 6), Color.LightGray, 0.75f);
		}

		private static float RingTarget(Rectangle cv) => System.Math.Min(26f, System.Math.Min(cv.Width, cv.Height) / 2f - 16f);

		private static float RingMax(Rectangle cv) => System.Math.Min(110f, System.Math.Min(cv.Width, cv.Height) / 2f - 6f);

		private static void DrawRingGame(SpriteBatch sb, Rectangle cv)
		{
			Vector2 c = new(cv.X + cv.Width / 2f, cv.Y + cv.Height / 2f);
			float tr = RingTarget(cv) * (1f + 0.05f * (float)System.Math.Sin(Clock * 0.1f));
			float rr = System.Math.Min(ringR, RingMax(cv));
			// Faint guide orbit, gold target ring, bright white pulse ring.
			DrawSmoothRing(sb, c, tr * 1.6f, new Color(70, 80, 120, 120), 48);
			DrawSmoothRing(sb, c, tr, Color.Gold, 56);
			FillDisc(sb, c, 3f, Color.Gold * 0.9f);
			DrawSmoothRing(sb, c, System.Math.Max(2f, rr), Color.White, 56);
			Terraria.Utils.DrawBorderString(sb, $"Tap when the ring kisses gold ({Score}/3)", new Vector2(cv.X + 8, cv.Y + 6), Color.LightGray, 0.75f);
			DrawPips(sb, new Vector2(cv.Right - 8 - 3 * 14f, cv.Y + 10), Score, 3);
		}

		private static void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, Color color)
		{
			Vector2 diff = b - a;
			float len = diff.Length();
			if (len < 1f)
				return;
			sb.Draw(TextureAssets.MagicPixel.Value, (a + b) / 2f, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
				new Vector2(0.5f, 0.5f), new Vector2(len, 2f), SpriteEffects.None, 0f);
		}

		private static void HandleCanvasClick(Rectangle canvas, int station, int kind)
		{
			if (!Playing || Phase != 1)
				return;
			if (!canvas.Contains(Main.MouseScreen.ToPoint()))
				return;
			Main.LocalPlayer.mouseInterface = true;
			Engine eng = ActiveEngine();
			// Charge uses press+release; everything else fires on press.
			if (eng == Engine.Charge)
				return;
			if (!(Main.mouseLeft && Main.mouseLeftRelease))
				return;
			Main.mouseLeftRelease = false;
			// Universal press ripple: every canvas click answers back.
			FxPush(Main.MouseScreen, Color.White * 0.55f);
			FireClick(canvas, station, kind, eng);
		}

		private static void FireClick(Rectangle canvas, int station, int kind, Engine eng)
		{
			if (IsRite) {
				if (Stage == 0) {
					int bx = canvas.X + 12, bw = canvas.Width - 24;
					float at = System.Math.Clamp((Main.MouseScreen.X - bx) / bw * 100f, 0f, 100f);
					ClickTiming(at);
				}
				else if (Stage == 1) {
					ClickSeqSlot(canvas, 6);
				}
				else {
					ClickDual();
				}
				return;
			}
			switch (eng) {
				case Engine.Roulette:
				case Engine.Timing:
				case Engine.MovingBand: {
					int bx = canvas.X + 12, bw = canvas.Width - 24;
					float at = System.Math.Clamp((Main.MouseScreen.X - bx) / bw * 100f, 0f, 100f);
					ClickTiming(at);
					break;
				}
				case Engine.Dual:
					ClickDual();
					break;
				case Engine.Simon:
					ClickSeqSlot(canvas, PadCount(station));
					break;
				case Engine.Reaction: {
					var (gx, gy, size) = QuadGeom(canvas);
					for (int q = 0; q < 4; q++) {
						Vector2 c = new(gx + (q % 2) * (size + 8) + size / 2f, gy + (q / 2) * (size + 8) + size / 2f);
						if (Vector2.Distance(Main.MouseScreen, c) <= size / 2f + 4f) {
							ClickQuad(q);
							break;
						}
					}
					break;
				}
				case Engine.Gear: {
					for (int i = 0; i < 3; i++) {
						Vector2 c = GearCenter(canvas, i);
						float r = GearRadius(canvas);
						if (Vector2.Distance(Main.MouseScreen, c) <= r + 14f) {
							ClickGear(i);
							break;
						}
					}
					break;
				}
				case Engine.Freq:
					ClickFreq();
					break;
				case Engine.Angular:
					ClickAngular();
					break;
				case Engine.Trace: {
					for (int i = 0; i < 8; i++) {
						if (Vector2.Distance(Main.MouseScreen, TraceNode(canvas, i)) <= 16f) {
							ClickTraceNode(i);
							break;
						}
					}
					break;
				}
				case Engine.Ringtap:
					ClickRing();
					break;
			}
		}

		private static void ClickSeqSlot(Rectangle canvas, int pads)
		{
			// Hands off while hinting, plus one second after.
			if (showing || seqLock > 0)
				return;
			var (sx, sy, pw, ph) = SeqGeom(canvas, pads);
			for (int slot = 0; slot < pads; slot++) {
				Vector2 center = new(sx + slot * (pw + 10) + pw / 2f, sy + ph / 2f);
				float rad = System.Math.Min(pw, ph) / 2f + 4f;
				if (Vector2.Distance(Main.MouseScreen, center) <= rad) {
					ClickSeq(slot);
					break;
				}
			}
		}
	}
}
