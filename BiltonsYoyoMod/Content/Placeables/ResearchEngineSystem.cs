using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Research Engine world data: charge + configuration per tile.
	//
	// Charge modes: Rapid (+3/hit, 0.6x release), Balanced (+2/hit, 1.0x),
	// Overload (+1/hit, 1.5x). Max 100. Release effects (chosen on the
	// block): Impact (next yoyo hit +dmg), Burst (short AoE on next hit),
	// Momentum (temporary yoyo speed). Only one engine releases per yoyo
	// hit (nearest full), so multiple engines do not stack full effects.
	// Charge and config persist via world save; breaking loses both.
	public class ResearchData
	{
		public int Charge;
		public int Mode; // 0 Rapid, 1 Balanced, 2 Overload
		public int Effect; // 0 Impact, 1 Burst, 2 Momentum, 3 Launch
		public int Style; // 0 One Hit (all charge, full power), 1 Over Time (drips, reduced), 2 Surge (big drips, strong)
		public bool AutoRelease = true;
		public int Rebound = 1; // 0 Soft, 1 Normal, 2 Crisp
		public List<string> Tech = new(); // owned tech-tree node ids
		public List<string> OffTech = new(); // owned-but-disabled node ids (Loadout tab)
		public List<ResearchJob> Research = new(); // active timed research (ticks with UI closed)
		public int Points; // spendable research points stored in this block (#2)
		public int PointsUp; // bought storage upgrades, each +250 cap (#2)
		public int LifetimePoints; // total points ever stored here (future scaling)
		public List<string> Queue = new(); // paid automation queue, auto-starts (#4)
		public bool Linked; // networked with one other engine (#6)
		public int LinkX; // linked partner tile origin (#6)
		public int LinkY;
		public int NbRes; // cached nearby stations, refreshed by tick (#6)
		public int NbWard;
		public int NbRift;
		public bool OverdriveOn; // mesh overdrive draw toggle (#12 expansion)
		public bool AutoService; // auto-service stockless repair (weaker than manual)
		public long LastHit; // last charge-hit tick for idle decay (#7, transient)
		public string ActiveEventId = ""; // timed event condition (#8)
		public int EventLeft; // ticks remaining on the timed condition (#8)
		public int EventIn; // ticks until the next event roll (#8)
		public List<string> ExpOffers = new(); // 3 trial board offers, packed (#11)
		public bool LastDayTime; // dawn-rollover tracking for trials (#11)

		public bool HasTech(string id) => Tech != null && Tech.Contains(id);
		public int TechCount => Tech?.Count ?? 0;
	}

	// One timed research job. The BAYS branch raises how many of these a
	// block can run at once.
	public class ResearchJob
	{
		public string Id = "";
		public int Left; // ticks remaining
		public float Acc; // fractional rate accumulator (transient)
	}

	public class ResearchEngineSystem : ModSystem
	{
		public static readonly Dictionary<Point, ResearchData> Tiles = new();

		public const int MaxCharge = 100;
		public const int MaxRebound = 2;

		// Engine charge ceiling (#23): 100, or 150 with Deep Cells.
		public static int MaxChargeOf(ResearchData d)
		{
			return MaxCharge + (ResearchTech.TechOn(d, ResearchTech.ChargeCells) ? 50 : 0);
		}

		public static int ChargePerHit(int mode) => mode switch {
			0 => 3,
			2 => 1,
			_ => 2,
		};

		public static float ReleaseMult(int mode) => mode switch {
			0 => 0.6f,
			2 => 1.5f,
			_ => 1f,
		};

		public static string ModeName(int mode) => mode switch {
			0 => "Rapid",
			2 => "Overload",
			_ => "Balanced",
		};

		public static string EffectName(int effect) => effect switch {
			1 => "Burst",
			2 => "Momentum",
			3 => "Launch",
			_ => "Impact",
		};

		public static string EffectTip(int effect) => effect switch {
			1 => "Burst: 250% AoE shockwave + insane knockback.",
			2 => "Momentum: temporary yoyo speed after release.",
			3 => "Launch: -90% damage, stupid knockback. For fun.",
			_ => "Impact: 500% empowered strike + insane knockback.",
		};

		// Over-time effectiveness: 75% less effective (25%), 40% with tech.
		public static float OverEff(ResearchData d)
		{
			return d != null && ResearchTech.TechOn(d, ResearchTech.OverEff) ? 0.4f : 0.25f;
		}

		public static int OverDrain(ResearchData d)
		{
			return d != null && ResearchTech.TechOn(d, ResearchTech.OverFlow) ? 8 : 10;
		}

		// Surge effectiveness: strong gulps, 60% (75% tuned).
		public static float SurgeEff(ResearchData d)
		{
			return d != null && ResearchTech.TechOn(d, ResearchTech.SurgeEff) ? 0.75f : 0.6f;
		}

		public const int SurgeDrain = 25;

		public static bool StyleDrips(ResearchData d) => d != null && d.Style != 0;
		public static int StyleDrain(ResearchData d) => d != null && d.Style == 2 ? SurgeDrain : OverDrain(d);
		public static float StylePower(ResearchData d) => d != null && d.Style == 2 ? SurgeEff(d) : OverEff(d);

		public static string StyleName(int style) => style switch {
			2 => "Surge",
			1 => "Over Time",
			_ => "One Hit",
		};

		public static string StyleTip(int style) => style switch {
			2 => "Surge: big 25-charge gulps at strong power. Research in Tech.",
			1 => "Over Time: drip ~10 charge per hit at reduced power. Research in Tech.",
			_ => "One Hit: spend it all at once, full power.",
		};

		public static string ReboundName(int rebound) => rebound switch {
			0 => "Soft",
			2 => "Crisp",
			_ => "Normal",
		};

		public static string ReboundTip(int rebound) => rebound switch {
			0 => "Soft: gentle 4-10 bounce. Easiest farming.",
			2 => "Crisp: sharp 8-18 kick. Lively, harder to aim.",
			_ => "Normal: even 6-14 bounce.",
		};

		public static (float lo, float hi) ReboundRange(int rebound) => rebound switch {
			0 => (4f, 10f),
			2 => (8f, 18f),
			_ => (6f, 14f),
		};

		public static ResearchData GetOrCreate(Point key)
		{
			if (!Tiles.TryGetValue(key, out var data)) {
				data = new ResearchData { Mode = 1, Effect = 0, AutoRelease = true, Rebound = 1 };
				Tiles[key] = data;
			}
			return data;
		}

		public static void ForgetTile(Point key)
		{
			if (Tiles.Remove(key))
				SyncWorld();
		}

		public static void SyncWorld()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public static bool IsTileStillEngine(Point key)
		{
			if (!WorldGen.InWorld(key.X, key.Y))
				return false;
			Tile t = Framing.GetTileSafely(key.X, key.Y);
			return t.HasTile && t.TileType == ModContent.TileType<Tiles.Placeables.ResearchEngineTile>();
		}

		// Any live boss (vanilla or modded, true bosses and sub-bosses).
		// Drives the boss-fight charge lockout (#7).
		public static bool AnyBossActive()
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc != null && npc.active && npc.boss && npc.life > 0)
					return true;
			}
			return false;
		}

		// Engine tile AABB in world pixels (3x2).
		public static Rectangle EngineRect(Point key)
		{
			return new Rectangle(key.X * 16, key.Y * 16, 48, 32);
		}

		public static void SetMode(Point key, int mode)
		{
			if (!IsTileStillEngine(key))
				return;
			var d = GetOrCreate(key);
			d.Mode = System.Math.Clamp(mode, 0, 2);
			SyncWorld();
		}

		public static void SetEffect(Point key, int effect)
		{
			if (!IsTileStillEngine(key))
				return;
			var d = GetOrCreate(key);
			d.Effect = System.Math.Clamp(effect, 0, 3);
			SyncWorld();
		}

		public static void SetStyle(Point key, int style)
		{
			if (!IsTileStillEngine(key))
				return;
			var d = GetOrCreate(key);
			style = System.Math.Clamp(style, 0, 2);
			// Over Time / Surge must be researched first (Tech tab).
			if (style == 1 && !ResearchTech.TechOn(d, ResearchTech.StyleOver))
				return;
			if (style == 2 && !ResearchTech.TechOn(d, ResearchTech.StyleSurge))
				return;
			d.Style = style;
			SyncWorld();
		}

		public static void AddCharge(Point key, int amount)
		{
			if (!IsTileStillEngine(key))
				return;
			var d = GetOrCreate(key);
			// Every charge add is a hit: restarts the idle-decay clock (#7).
			d.LastHit = (long)Main.GameUpdateCount;
			// Absolute ceiling covers Deep Cells (#23): never over 150.
			d.Charge = System.Math.Clamp(d.Charge + amount, 0, MaxCharge + 50);
			SyncWorld();
		}

		public static bool TryConsumeFull(Point key)
		{
			if (!Tiles.TryGetValue(key, out var d) || d.Charge < MaxChargeOf(d))
				return false;
			if (!d.AutoRelease)
				return false; // Held: charge stays until released or vented.
			d.Charge = 0;
			SyncWorld();
			return true;
		}

		// Over-time drip: burns a chunk per yoyo hit. Returns the amount
		// actually burned (0 when held, empty, or gone).
		public static int TryConsumeTick(Point key)
		{
			if (!Tiles.TryGetValue(key, out var d) || d.Charge <= 0)
				return 0;
			if (!d.AutoRelease)
				return 0;
			int burn = System.Math.Min(OverDrain(d), d.Charge);
			d.Charge -= burn;
			SyncWorld();
			return burn;
		}

		public static void VentCharge(Point key)
		{
			if (!Tiles.TryGetValue(key, out var d) || d.Charge <= 0)
				return;
			d.Charge = 0;
			SyncWorld();
		}

		public static void SetAuto(Point key, bool auto)
		{
			if (!IsTileStillEngine(key))
				return;
			GetOrCreate(key).AutoRelease = auto;
			SyncWorld();
		}

		public static void SetRebound(Point key, int rebound)
		{
			if (!IsTileStillEngine(key))
				return;
			GetOrCreate(key).Rebound = System.Math.Clamp(rebound, 0, MaxRebound);
			SyncWorld();
		}

		public override void OnWorldLoad()
		{
			Tiles.Clear();
			migratedLegacy = false;
		}

		public override void OnWorldUnload()
		{
			Tiles.Clear();
			migratedLegacy = false;
		}

		// One-shot legacy migration flag (first world tick, tiles ready).
		private static bool migratedLegacy;

		// Converts one pre-rename Torque Engine block into a Research
		// Engine in place. Same 3x2 footprint and origin convention, so
		// all per-tile research data survives by coordinates. Frames are
		// written directly (top-left cell = origin, 18px steps) rather
		// than PlaceTile, so the converted block lands exactly.
		public static bool MigrateLegacyTile(Point origin)
		{
			if (!WorldGen.InWorld(origin.X, origin.Y))
				return false;
			Tile t = Framing.GetTileSafely(origin.X, origin.Y);
			if (!t.HasTile || t.TileType != ModContent.TileType<Tiles.Placeables.TorqueEngineTile>())
				return false;
			for (int dx = 0; dx < 3; dx++) {
				for (int dy = 0; dy < 2; dy++) {
					if (!WorldGen.InWorld(origin.X + dx, origin.Y + dy))
						return false;
				}
			}
			int nt = ModContent.TileType<Tiles.Placeables.ResearchEngineTile>();
			for (int dx = 0; dx < 3; dx++) {
				for (int dy = 0; dy < 2; dy++) {
					Tile c = Main.tile[origin.X + dx, origin.Y + dy];
					c.ClearTile();
					c.HasTile = true;
					c.TileType = (ushort)nt;
					c.TileFrameX = (short)(dx * 18);
					c.TileFrameY = (short)(dy * 18);
					c.Slope = 0;
					c.IsHalfBlock = false;
				}
			}
			Tile check = Framing.GetTileSafely(origin.X, origin.Y);
			bool ok = check.HasTile && check.TileType == nt && check.TileFrameX == 0 && check.TileFrameY == 0;
			if (!ok) {
				// Rollback: re-place the legacy block rather than leaving
				// a hole (retries on next load/click).
				int lt = ModContent.TileType<Tiles.Placeables.TorqueEngineTile>();
				for (int dx = 0; dx < 3; dx++) {
					for (int dy = 0; dy < 2; dy++) {
						Tile c = Main.tile[origin.X + dx, origin.Y + dy];
						c.ClearTile();
						c.HasTile = true;
						c.TileType = (ushort)lt;
						c.TileFrameX = (short)(dx * 18);
						c.TileFrameY = (short)(dy * 18);
						c.Slope = 0;
						c.IsHalfBlock = false;
					}
				}
				return false;
			}
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendTileSquare(-1, origin.X, origin.Y, 3, 2);
			return true;
		}

		private static void ConvertLegacyTiles()
		{
			int legacy = ModContent.TileType<Tiles.Placeables.TorqueEngineTile>();
			for (int x = 2; x < Main.maxTilesX - 3; x++) {
				for (int y = 2; y < Main.maxTilesY - 3; y++) {
					Tile t = Main.tile[x, y];
					if (t == null || !t.HasTile || t.TileType != legacy)
						continue;
					if (t.TileFrameX != 0 || t.TileFrameY != 0)
						continue; // origin only: one conversion per block
					MigrateLegacyTile(new Point(x, y));
				}
			}
		}

		// Research timers tick here so countdowns run with the UI closed.
		// Iterated backwards: completions mutate the job list.
		public override void PostUpdateEverything()
		{
			// Legacy migration backstop (first tick, tiles guaranteed
			// loaded). Idempotent: converted blocks never match again.
			if (!migratedLegacy) {
				migratedLegacy = true;
				ConvertLegacyTiles();
			}
			// Station-network coverage is read-only tile/dict state, so all
			// sides refresh it identically (#12 display counts).
			if (Main.GameUpdateCount % 600 == 0)
				StationNetwork.RefreshCoverage();
			// Passive study trickle runs server/single-player side only;
			// clients converge through the WorldData rebroadcast.
			bool doTrickle = Main.netMode != NetmodeID.MultiplayerClient && Main.GameUpdateCount % ResearchPoints.TrickleEveryTicks == 0;
			foreach (var kv in Tiles) {
				var d = kv.Value;
				if (!IsTileStillEngine(kv.Key)) {
					d.Research?.Clear();
					continue;
				}
				// Station scan shares the trickle cadence (server/single
				// only; clients converge through WorldData).
				if (doTrickle)
					ResearchNetwork.RefreshStations(kv.Key, d);
				if (doTrickle)
					ResearchPoints.Trickle(d);
				// Trickle charge (#23): sips to 25 with no hits needed.
				if (doTrickle && ResearchTech.TechOn(d, ResearchTech.ChargeTrick) && d.Charge < 25)
					AddCharge(kv.Key, 1);
				// Idle decay (#7): -1 charge per 0.1s after 5s without a
				// hit (twice as slow with Insulation, #23). Server/single
				// only; clients converge (syncs are throttled to every 5
				// charge, plus the empty tick).
				int decayEvery = ResearchTech.TechOn(d, ResearchTech.ChargeInsul) ? 12 : 6;
				if (Main.netMode != NetmodeID.MultiplayerClient && d.Charge > 0
					&& (long)Main.GameUpdateCount - d.LastHit >= 300 && Main.GameUpdateCount % (uint)decayEvery == 0) {
					d.Charge = System.Math.Max(0, d.Charge - 1);
					if (d.Charge == 0 || d.Charge % 5 == 0)
						SyncWorld();
				}
				// Overdrive draw (#12 expansion): toggled engines burn held
				// charge to double their mesh output. Server/single only.
				// Aegis7 counts too (it was silently free before — bugfix).
				if (Main.netMode != NetmodeID.MultiplayerClient && d.OverdriveOn && (ResearchTech.TechOn(d, ResearchTech.StNet7) || ResearchTech.TechOn(d, ResearchTech.Aegis7)) && d.Charge > 0 && Main.GameUpdateCount % 120 == 0) {
					d.Charge = System.Math.Max(0, d.Charge - 1);
					if (d.Charge == 0 || d.Charge % 5 == 0)
						SyncWorld();
				}
				// Auto-service: +1% condition per 30s to the most-worn covered
				// defensive trap below 80%, for 5 charge. Deliberately weaker
				// than manual service (+25% for oil+1g). Server/single only.
				if (Main.netMode != NetmodeID.MultiplayerClient && d.AutoService && d.Charge >= 5 && Main.GameUpdateCount % 1800 == 0) {
					Point worst = default;
					int worstCond = 80;
					bool foundWorn = false;
					int arange = StationNetwork.AegisCoverRange(kv.Key);
					if (arange > 0) {
						foreach (var tkv in global::BiltonsYoyoMod.Traps.TrapSystem.Traps) {
							var ts = tkv.Value;
							if (!global::BiltonsYoyoMod.Traps.TrapSystem.IsDefensive(ts.Kind) || !ts.Enabled)
								continue;
							if (ts.Condition >= worstCond)
								continue;
							float dx = tkv.Key.X - kv.Key.X, dy = tkv.Key.Y - kv.Key.Y;
							if (dx * dx + dy * dy > arange * arange)
								continue;
							worstCond = ts.Condition;
							worst = tkv.Key;
							foundWorn = true;
						}
					}
					if (foundWorn && global::BiltonsYoyoMod.Traps.TrapSystem.Traps.TryGetValue(worst, out var worn)) {
						worn.Condition = System.Math.Min(100, worn.Condition + 1);
						worn.CondTick = 0;
						d.Charge = System.Math.Max(0, d.Charge - 5);
						global::BiltonsYoyoMod.Traps.TrapSystem.SyncWorld();
						SyncWorld();
					}
				}
				// Random events (#8): countdown plus timed conditions run
				// server/single only; clients converge via WorldData.
				if (Main.netMode != NetmodeID.MultiplayerClient && ResearchTech.TechOn(d, ResearchTech.EvSig)) {
					if (ResearchEvents.TickCondition(d))
						SyncWorld();
					d.EventIn--;
					if (d.EventIn <= 0) {
						ResearchEvents.Roll(kv.Key, d);
						d.EventIn = ResearchEvents.CadenceOf(d);
						SyncWorld();
					}
				}
				// Trial board (#11): fresh offers every in-game dawn,
				// server/single only; clients converge via WorldData.
				if (Main.netMode != NetmodeID.MultiplayerClient && ResearchTech.TechOn(d, ResearchTech.ExpBoard)) {
					if (Main.dayTime != d.LastDayTime) {
						d.LastDayTime = Main.dayTime;
						if (Main.dayTime) {
							ResearchTrials.RollOffers(kv.Key, d);
							SyncWorld();
						}
					}
				}
				// Automation queue (#4): paid entries auto-start while bays
				// are free, even when nothing is currently researching.
				if (ResearchTech.PromoteQueue(kv.Key, d) && !Main.dedServ) {
					if (ResearchEngineUISystem.Open && ResearchEngineUISystem.OpenTile == kv.Key)
						ResearchEngineUISystem.SetMessage("Queued research started.");
				}
				if (d.Research == null || d.Research.Count == 0)
					continue;
				float rate = ResearchTech.ResearchRate(d);
				// Network + station research bonuses (#6).
				rate *= 1f + ResearchNetwork.StationRateBonus(d);
				if (ResearchNetwork.LinkBonusActive(kv.Key, d))
					rate *= 1.1f;
				// Timed event condition (#8: Frenzy/Slump).
				rate *= ResearchEvents.ActiveRateMult(d);
				for (int j = d.Research.Count - 1; j >= 0; j--) {
					var job = d.Research[j];
					var node = ResearchTech.ById(job.Id);
					if (node == null || d.HasTech(job.Id)) {
						d.Research.RemoveAt(j);
						continue;
					}
					job.Acc += rate;
					int whole = (int)job.Acc;
					if (whole > 0) {
						job.Acc -= whole;
						job.Left -= whole;
					}
					if (job.Left > 0)
						continue;
					d.Research.RemoveAt(j);
					ResearchTech.Unlock(kv.Key, node.Id);
					ResearchNetwork.MirrorUnlock(kv.Key, d, node.Id);
					var (stored, lost) = ResearchPoints.OnComplete(d, node);
					if (!Main.dedServ) {
						Vector2 center = new(kv.Key.X * 16f + 24f, kv.Key.Y * 16f + 16f);
						for (int i = 0; i < 14; i++) {
							Dust dust = Dust.NewDustDirect(center - new Vector2(4, 4), 8, 8, DustID.GoldCoin, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1f);
							dust.noGravity = true;
						}
						if (ResearchEngineUISystem.Open && ResearchEngineUISystem.OpenTile == kv.Key)
							ResearchEngineUISystem.SetMessage($"Researched {node.Name}! +{stored} pts{(lost > 0 ? " (storage full)" : "")}!");
					}
				}
			}
		}

		public override void SaveWorldData(TagCompound tag)
		{
			var xs = new List<int>();
			var ys = new List<int>();
			var ch = new List<int>();
			var mo = new List<int>();
			var ef = new List<int>();
			var au = new List<int>();
			var rb = new List<int>();
			var st = new List<int>();
			var te = new List<string>();
			var tc = new List<int>();
			var oe = new List<string>();
			var oc = new List<int>();
			var rqC = new List<int>();
			var rqI = new List<string>();
			var rqL = new List<int>();
			var pp = new List<int>();
			var pu = new List<int>();
			var pl = new List<int>();
			var qq = new List<string>();
			var qc = new List<int>();
			var ln = new List<int>();
			var lx = new List<int>();
			var ly = new List<int>();
			var sr = new List<int>();
			var sw = new List<int>();
			var sf = new List<int>();
			var od = new List<int>();
			var asv = new List<int>();
			var ev = new List<string>();
			var evL = new List<int>();
			var evN = new List<int>();
			var xo = new List<string>();
			var xd = new List<int>();
			foreach (var kv in Tiles) {
				if (!IsTileStillEngine(kv.Key))
					continue; // Never persist ghosts for destroyed blocks.
				xs.Add(kv.Key.X);
				ys.Add(kv.Key.Y);
				ch.Add(kv.Value.Charge);
				mo.Add(kv.Value.Mode);
				ef.Add(kv.Value.Effect);
				au.Add(kv.Value.AutoRelease ? 1 : 0);
				rb.Add(kv.Value.Rebound);
				st.Add(kv.Value.Style);
				var tech = kv.Value.Tech ?? new List<string>();
				tc.Add(tech.Count);
				te.AddRange(tech);
				var off = kv.Value.OffTech ?? new List<string>();
				oc.Add(off.Count);
				oe.AddRange(off);
				var jobs = kv.Value.Research ?? new List<ResearchJob>();
				rqC.Add(jobs.Count);
				foreach (var job in jobs) {
					rqI.Add(job.Id ?? "");
					rqL.Add(job.Left);
				}
				pp.Add(kv.Value.Points);
				pu.Add(System.Math.Clamp(kv.Value.PointsUp, 0, ResearchPoints.MaxUpgrades));
				pl.Add(System.Math.Max(0, kv.Value.LifetimePoints));
				var queue = kv.Value.Queue ?? new List<string>();
				qc.Add(System.Math.Min(queue.Count, 8));
				for (int qi = 0; qi < queue.Count && qi < 8; qi++)
					qq.Add(queue[qi] ?? "");
				ln.Add(kv.Value.Linked ? 1 : 0);
				lx.Add(kv.Value.LinkX);
				ly.Add(kv.Value.LinkY);
				sr.Add(System.Math.Clamp(kv.Value.NbRes, 0, 99));
				sw.Add(System.Math.Clamp(kv.Value.NbWard, 0, 99));
				sf.Add(System.Math.Clamp(kv.Value.NbRift, 0, 99));
				od.Add(kv.Value.OverdriveOn ? 1 : 0);
				asv.Add(kv.Value.AutoService ? 1 : 0);
				ev.Add(ResearchEvents.IsKnown(kv.Value.ActiveEventId) ? kv.Value.ActiveEventId : "");
				evL.Add(System.Math.Max(0, kv.Value.EventLeft));
				evN.Add(System.Math.Max(0, kv.Value.EventIn));
				var offers = kv.Value.ExpOffers ?? new List<string>();
				for (int ei = 0; ei < ResearchTrials.OfferSlots; ei++)
					xo.Add(ei < offers.Count ? offers[ei] ?? "" : "");
				xd.Add(kv.Value.LastDayTime ? 1 : 0);
			}
			tag["tqX"] = xs;
			tag["tqY"] = ys;
			tag["tqC"] = ch;
			tag["tqM"] = mo;
			tag["tqE"] = ef;
			tag["tqA"] = au;
			tag["tqR"] = rb;
			tag["tqS"] = st;
			tag["tqT"] = te;
			tag["tqTC"] = tc;
			tag["tqO"] = oe;
			tag["tqOC"] = oc;
			tag["tqAR"] = rqI;
			tag["tqRL"] = rqL;
			tag["tqRC"] = rqC;
			tag["tqP"] = pp;
			tag["tqPU"] = pu;
			tag["tqPL"] = pl;
			tag["tqQ"] = qq;
			tag["tqQC"] = qc;
			tag["tqLN"] = ln;
			tag["tqLX"] = lx;
			tag["tqLY"] = ly;
			tag["tqSR"] = sr;
			tag["tqSW"] = sw;
			tag["tqSF"] = sf;
			tag["tqOD"] = od;
			tag["tqAS"] = asv;
			tag["tqEV"] = ev;
			tag["tqEVL"] = evL;
			tag["tqEVN"] = evN;
			tag["tqXO"] = xo;
			tag["tqXD"] = xd;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Tiles.Clear();
			if (!tag.ContainsKey("tqX"))
				return;
			var xs = tag.Get<List<int>>("tqX");
			var ys = tag.Get<List<int>>("tqY");
			var ch = tag.Get<List<int>>("tqC");
			var mo = tag.Get<List<int>>("tqM");
			var ef = tag.Get<List<int>>("tqE");
			List<int> au = tag.ContainsKey("tqA") ? tag.Get<List<int>>("tqA") : null;
			List<int> rb = tag.ContainsKey("tqR") ? tag.Get<List<int>>("tqR") : null;
			List<int> st = tag.ContainsKey("tqS") ? tag.Get<List<int>>("tqS") : null;
			List<string> te = tag.ContainsKey("tqT") ? tag.Get<List<string>>("tqT") : null;
			List<int> tc = tag.ContainsKey("tqTC") ? tag.Get<List<int>>("tqTC") : null;
			List<string> oe = tag.ContainsKey("tqO") ? tag.Get<List<string>>("tqO") : null;
			List<int> oc = tag.ContainsKey("tqOC") ? tag.Get<List<int>>("tqOC") : null;
			List<string> rqi = tag.ContainsKey("tqAR") ? tag.Get<List<string>>("tqAR") : null;
			List<int> rql = tag.ContainsKey("tqRL") ? tag.Get<List<int>>("tqRL") : null;
			List<int> rqc = tag.ContainsKey("tqRC") ? tag.Get<List<int>>("tqRC") : null;
			List<int> pp = tag.ContainsKey("tqP") ? tag.Get<List<int>>("tqP") : null;
			List<int> pu = tag.ContainsKey("tqPU") ? tag.Get<List<int>>("tqPU") : null;
			List<int> pl = tag.ContainsKey("tqPL") ? tag.Get<List<int>>("tqPL") : null;
			List<string> qq = tag.ContainsKey("tqQ") ? tag.Get<List<string>>("tqQ") : null;
			List<int> qc = tag.ContainsKey("tqQC") ? tag.Get<List<int>>("tqQC") : null;
			List<int> ln = tag.ContainsKey("tqLN") ? tag.Get<List<int>>("tqLN") : null;
			List<int> lx = tag.ContainsKey("tqLX") ? tag.Get<List<int>>("tqLX") : null;
			List<int> ly = tag.ContainsKey("tqLY") ? tag.Get<List<int>>("tqLY") : null;
			List<int> sr = tag.ContainsKey("tqSR") ? tag.Get<List<int>>("tqSR") : null;
			List<int> sw = tag.ContainsKey("tqSW") ? tag.Get<List<int>>("tqSW") : null;
			List<int> sf = tag.ContainsKey("tqSF") ? tag.Get<List<int>>("tqSF") : null;
			List<int> od = tag.ContainsKey("tqOD") ? tag.Get<List<int>>("tqOD") : null;
			List<int> asv = tag.ContainsKey("tqAS") ? tag.Get<List<int>>("tqAS") : null;
			List<string> ev = tag.ContainsKey("tqEV") ? tag.Get<List<string>>("tqEV") : null;
			List<int> evL = tag.ContainsKey("tqEVL") ? tag.Get<List<int>>("tqEVL") : null;
			List<int> evN = tag.ContainsKey("tqEVN") ? tag.Get<List<int>>("tqEVN") : null;
			List<string> xo = tag.ContainsKey("tqXO") ? tag.Get<List<string>>("tqXO") : null;
			List<int> xd = tag.ContainsKey("tqXD") ? tag.Get<List<int>>("tqXD") : null;
			int xoi = 0;
			int qi = 0;
			int n = xs.Count;
			n = System.Math.Min(n, ys.Count);
			n = System.Math.Min(n, ch.Count);
			n = System.Math.Min(n, mo.Count);
			n = System.Math.Min(n, ef.Count);
			int ti = 0;
			int oi = 0;
			int ri = 0;
			int rli = 0;
			for (int i = 0; i < n; i++) {
				var tech = new List<string>();
				if (te != null && tc != null && i < tc.Count) {
					int count = System.Math.Clamp(tc[i], 0, 64);
					for (int k = 0; k < count && ti < te.Count; k++, ti++)
						tech.Add(te[ti]);
				}
				var off = new List<string>();
				if (oe != null && oc != null && i < oc.Count) {
					int count = System.Math.Clamp(oc[i], 0, 64);
					for (int k = 0; k < count && oi < oe.Count; k++, oi++) {
						if (ResearchTech.IsNode(oe[oi]) && tech.Contains(oe[oi]))
							off.Add(oe[oi]);
					}
				}
				Tiles[new Point(xs[i], ys[i])] = new ResearchData {
					Charge = System.Math.Clamp(ch[i], 0, MaxCharge + 50),
					Mode = System.Math.Clamp(mo[i], 0, 2),
					Effect = System.Math.Clamp(ef[i], 0, 3),
					AutoRelease = au == null || i >= au.Count || au[i] != 0,
					Rebound = rb == null || i >= rb.Count ? 1 : System.Math.Clamp(rb[i], 0, MaxRebound),
					Style = st == null || i >= st.Count ? 0 : System.Math.Clamp(st[i], 0, 2),
					Tech = tech,
					OffTech = off,
				};
				var loaded = Tiles[new Point(xs[i], ys[i])];
				if (rqc != null) {
					// Current format: flat job id/left lists with per-tile counts.
					if (i < rqc.Count) {
						int count = System.Math.Clamp(rqc[i], 0, 3);
						for (int k = 0; k < count && rqi != null && rql != null && ri < rqi.Count && rli < rql.Count; k++, ri++, rli++) {
							var rn = ResearchTech.ById(rqi[ri]);
							if (rn != null && !loaded.HasTech(rqi[ri])) {
								int left = System.Math.Clamp(rql[rli], 0, ResearchTech.ResearchSecs(rn) * 60);
								if (left > 0)
									loaded.Research.Add(new ResearchJob { Id = rqi[ri], Left = left, Acc = 0f });
							}
						}
					}
				}
				else if (rqi != null && rql != null && i < rqi.Count && i < rql.Count) {
					// Legacy single-job format: one id + one timer per tile.
					var rn = ResearchTech.ById(rqi[i]);
					if (rn != null && !loaded.HasTech(rqi[i])) {
						int left = System.Math.Clamp(rql[i], 0, ResearchTech.ResearchSecs(rn) * 60);
						if (left > 0)
							loaded.Research.Add(new ResearchJob { Id = rqi[i], Left = left, Acc = 0f });
					}
				}
				// Research points (#2): missing tags (older worlds) load as fresh storage.
				if (pu != null && i < pu.Count)
					loaded.PointsUp = System.Math.Clamp(pu[i], 0, ResearchPoints.MaxUpgrades);
				if (pp != null && i < pp.Count)
					loaded.Points = System.Math.Clamp(pp[i], 0, ResearchPoints.CapOf(loaded));
				if (pl != null && i < pl.Count)
					loaded.LifetimePoints = System.Math.Max(0, pl[i]);
				// Automation queue (#4): unknown/owned ids drop on load.
				if (qc != null && i < qc.Count) {
					int qcount = System.Math.Clamp(qc[i], 0, 6);
					for (int k = 0; k < qcount && qq != null && qi < qq.Count; k++, qi++) {
						if (ResearchTech.ById(qq[qi]) != null && !loaded.HasTech(qq[qi]) && !loaded.Queue.Contains(qq[qi]))
							loaded.Queue.Add(qq[qi]);
					}
				}
				// Network link + station cache (#6): stale links resolve
				// live (partner-lost shows in the Network tab).
				if (ln != null && i < ln.Count)
					loaded.Linked = ln[i] != 0;
				if (lx != null && i < lx.Count)
					loaded.LinkX = lx[i];
				if (ly != null && i < ly.Count)
					loaded.LinkY = ly[i];
				if (sr != null && i < sr.Count)
					loaded.NbRes = System.Math.Clamp(sr[i], 0, 99);
				if (sw != null && i < sw.Count)
					loaded.NbWard = System.Math.Clamp(sw[i], 0, 99);
				if (sf != null && i < sf.Count)
					loaded.NbRift = System.Math.Clamp(sf[i], 0, 99);
				if (od != null && i < od.Count)
					loaded.OverdriveOn = od[i] != 0;
				if (asv != null && i < asv.Count)
					loaded.AutoService = asv[i] != 0;
				// Random events (#8): unknown ids drop, timers clamp.
				if (ev != null && i < ev.Count && ResearchEvents.IsKnown(ev[i]))
					loaded.ActiveEventId = ev[i];
				if (evL != null && i < evL.Count)
					loaded.EventLeft = System.Math.Max(0, evL[i]);
				if (evN != null && i < evN.Count)
					loaded.EventIn = System.Math.Max(0, evN[i]);
				// Trial board (#11): fixed 3 slots, validated offers.
				loaded.ExpOffers = new List<string> { "", "", "" };
				if (xo != null) {
					for (int k = 0; k < ResearchTrials.OfferSlots && xoi < xo.Count; k++, xoi++) {
						if (ResearchTrials.TryParseOffer(xo[xoi], out _, out _, out _, out _))
							loaded.ExpOffers[k] = xo[xoi];
					}
				}
				if (xd != null && i < xd.Count)
					loaded.LastDayTime = xd[i] != 0;
				else
					loaded.LastDayTime = Main.dayTime; // no instant reroll on first load
			}
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(Tiles.Count);
			foreach (var kv in Tiles) {
				writer.Write(kv.Key.X);
				writer.Write(kv.Key.Y);
				writer.Write(kv.Value.Charge);
				writer.Write(kv.Value.Mode);
				writer.Write(kv.Value.Effect);
				writer.Write(kv.Value.AutoRelease ? 1 : 0);
				writer.Write(kv.Value.Rebound);
				writer.Write(kv.Value.Style);
				var tech = kv.Value.Tech ?? new List<string>();
				writer.Write(System.Math.Min(tech.Count, 64));
				for (int i = 0; i < tech.Count && i < 64; i++)
					writer.Write(tech[i] ?? "");
				var off = kv.Value.OffTech ?? new List<string>();
				writer.Write(System.Math.Min(off.Count, 64));
				for (int i = 0; i < off.Count && i < 64; i++)
					writer.Write(off[i] ?? "");
				var jobs = kv.Value.Research ?? new List<ResearchJob>();
				writer.Write(System.Math.Min(jobs.Count, 8));
				for (int i = 0; i < jobs.Count && i < 8; i++) {
					writer.Write(jobs[i]?.Id ?? "");
					writer.Write(jobs[i]?.Left ?? 0);
				}
				writer.Write(kv.Value.Points);
				writer.Write(System.Math.Clamp(kv.Value.PointsUp, 0, ResearchPoints.MaxUpgrades));
				writer.Write(System.Math.Max(0, kv.Value.LifetimePoints));
				var queue = kv.Value.Queue ?? new List<string>();
				writer.Write(System.Math.Min(queue.Count, 8));
				for (int qi = 0; qi < queue.Count && qi < 8; qi++)
					writer.Write(queue[qi] ?? "");
				writer.Write(kv.Value.Linked ? 1 : 0);
				writer.Write(kv.Value.LinkX);
				writer.Write(kv.Value.LinkY);
				writer.Write(System.Math.Clamp(kv.Value.NbRes, 0, 99));
				writer.Write(System.Math.Clamp(kv.Value.NbWard, 0, 99));
				writer.Write(System.Math.Clamp(kv.Value.NbRift, 0, 99));
				writer.Write(kv.Value.OverdriveOn ? 1 : 0);
				writer.Write(kv.Value.AutoService ? 1 : 0);
				writer.Write(ResearchEvents.IsKnown(kv.Value.ActiveEventId) ? kv.Value.ActiveEventId : "");
				writer.Write(System.Math.Max(0, kv.Value.EventLeft));
				writer.Write(System.Math.Max(0, kv.Value.EventIn));
				var offers = kv.Value.ExpOffers ?? new List<string>();
				for (int ei = 0; ei < ResearchTrials.OfferSlots; ei++)
					writer.Write(ei < offers.Count ? offers[ei] ?? "" : "");
				writer.Write(kv.Value.LastDayTime ? 1 : 0);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			Tiles.Clear();
			int n = reader.ReadInt32();
			for (int i = 0; i < n; i++) {
				var key = new Point(reader.ReadInt32(), reader.ReadInt32());
				var data = new ResearchData {
					Charge = System.Math.Clamp(reader.ReadInt32(), 0, MaxCharge + 50),
					Mode = System.Math.Clamp(reader.ReadInt32(), 0, 2),
					Effect = System.Math.Clamp(reader.ReadInt32(), 0, 3),
					AutoRelease = reader.ReadInt32() != 0,
					Rebound = System.Math.Clamp(reader.ReadInt32(), 0, MaxRebound),
					Style = System.Math.Clamp(reader.ReadInt32(), 0, 2),
				};
				int techCount = System.Math.Clamp(reader.ReadInt32(), 0, 64);
				for (int k = 0; k < techCount; k++) {
					string id = reader.ReadString();
					if (!string.IsNullOrEmpty(id) && ResearchTech.IsNode(id))
						data.Tech.Add(id);
				}
				int offCount = System.Math.Clamp(reader.ReadInt32(), 0, 64);
				for (int k = 0; k < offCount; k++) {
					string id = reader.ReadString();
					if (!string.IsNullOrEmpty(id) && ResearchTech.IsNode(id) && data.Tech.Contains(id))
						data.OffTech.Add(id);
				}
				int jobCount = System.Math.Clamp(reader.ReadInt32(), 0, 3);
				for (int k = 0; k < jobCount; k++) {
					string jid = reader.ReadString();
					int jleft = reader.ReadInt32();
					var rn = ResearchTech.ById(jid);
					if (rn != null && !data.HasTech(jid)) {
						int left = System.Math.Clamp(jleft, 0, ResearchTech.ResearchSecs(rn) * 60);
						if (left > 0)
							data.Research.Add(new ResearchJob { Id = jid, Left = left, Acc = 0f });
					}
				}
				int pointsRaw = reader.ReadInt32();
				data.PointsUp = System.Math.Clamp(reader.ReadInt32(), 0, ResearchPoints.MaxUpgrades);
				data.Points = System.Math.Clamp(pointsRaw, 0, ResearchPoints.CapOf(data));
				data.LifetimePoints = System.Math.Max(0, reader.ReadInt32());
				int queueCount = System.Math.Clamp(reader.ReadInt32(), 0, 6);
				for (int k = 0; k < queueCount; k++) {
					string qid = reader.ReadString();
					if (!string.IsNullOrEmpty(qid) && ResearchTech.ById(qid) != null && !data.HasTech(qid) && !data.Queue.Contains(qid))
						data.Queue.Add(qid);
				}
				data.Linked = reader.ReadInt32() != 0;
				data.LinkX = reader.ReadInt32();
				data.LinkY = reader.ReadInt32();
				data.NbRes = System.Math.Clamp(reader.ReadInt32(), 0, 99);
				data.NbWard = System.Math.Clamp(reader.ReadInt32(), 0, 99);
				data.NbRift = System.Math.Clamp(reader.ReadInt32(), 0, 99);
				data.OverdriveOn = reader.ReadInt32() != 0;
				try { data.AutoService = reader.ReadInt32() != 0; } catch { data.AutoService = false; }
				string evid = reader.ReadString();
				data.ActiveEventId = ResearchEvents.IsKnown(evid) ? evid : "";
				data.EventLeft = System.Math.Max(0, reader.ReadInt32());
				data.EventIn = System.Math.Max(0, reader.ReadInt32());
				data.ExpOffers = new List<string> { "", "", "" };
				for (int ei = 0; ei < ResearchTrials.OfferSlots; ei++) {
					string packed = reader.ReadString();
					if (ResearchTrials.TryParseOffer(packed, out _, out _, out _, out _))
						data.ExpOffers[ei] = packed;
				}
				data.LastDayTime = reader.ReadInt32() != 0;
				Tiles[key] = data;
			}
		}
	}

	// Per-player Research state (Momentum timer, boss lockout).
	public class ResearchEnginePlayer : ModPlayer
	{
		public int MomentumTimer;
		public float MomentumMult = 1f;
		public bool BossCharged; // full-charged an engine during this boss fight (#7)

		// Tech-saving backup (#15): one engine blueprint per player.
		public bool HasBackup;
		public List<string> BpTech = new();
		public List<string> BpOff = new();
		public List<string> BpQueue = new();
		public List<string> BpJobIds = new();
		public List<int> BpJobLefts = new();
		public int BpUp;
		public int BpPoints;
		public int BpLife;
		public int BpCharge;
		public int BpMode = 1;
		public int BpEffect;
		public int BpStyle;
		public int BpAuto = 1;
		public int BpRebound = 1;

		public void ClearBackup()
		{
			HasBackup = false;
			BpTech = new List<string>();
			BpOff = new List<string>();
			BpQueue = new List<string>();
			BpJobIds = new List<string>();
			BpJobLefts = new List<int>();
			BpUp = 0;
			BpPoints = 0;
			BpLife = 0;
			BpCharge = 0;
			BpMode = 1;
			BpEffect = 0;
			BpStyle = 0;
			BpAuto = 1;
			BpRebound = 1;
		}

		public override void SaveData(TagCompound tag)
		{
			tag["rsHas"] = HasBackup;
			if (!HasBackup)
				return;
			tag["rsT"] = BpTech ?? new List<string>();
			tag["rsO"] = BpOff ?? new List<string>();
			tag["rsQ"] = BpQueue ?? new List<string>();
			tag["rsJI"] = BpJobIds ?? new List<string>();
			tag["rsJL"] = BpJobLefts ?? new List<int>();
			tag["rsU"] = BpUp;
			tag["rsP"] = BpPoints;
			tag["rsL"] = BpLife;
			tag["rsC"] = BpCharge;
			tag["rsM"] = BpMode;
			tag["rsE"] = BpEffect;
			tag["rsS"] = BpStyle;
			tag["rsA"] = BpAuto;
			tag["rsR"] = BpRebound;
		}

		public override void LoadData(TagCompound tag)
		{
			ClearBackup();
			if (!tag.ContainsKey("rsHas") || !tag.GetBool("rsHas"))
				return;
			HasBackup = true;
			if (tag.ContainsKey("rsT")) {
				foreach (string id in tag.Get<List<string>>("rsT")) {
					if (ResearchTech.IsNode(id) && !BpTech.Contains(id))
						BpTech.Add(id);
				}
			}
			if (tag.ContainsKey("rsO")) {
				foreach (string id in tag.Get<List<string>>("rsO")) {
					if (ResearchTech.IsNode(id) && BpTech.Contains(id) && !BpOff.Contains(id))
						BpOff.Add(id);
				}
			}
			if (tag.ContainsKey("rsQ")) {
				foreach (string id in tag.Get<List<string>>("rsQ")) {
					if (ResearchTech.IsNode(id) && !BpTech.Contains(id) && !BpQueue.Contains(id) && BpQueue.Count < 8)
						BpQueue.Add(id);
				}
			}
			if (tag.ContainsKey("rsJI") && tag.ContainsKey("rsJL")) {
				var ids = tag.Get<List<string>>("rsJI");
				var lefts = tag.Get<List<int>>("rsJL");
				for (int i = 0; i < ids.Count && i < lefts.Count && BpJobIds.Count < 3; i++) {
					var rn = ResearchTech.ById(ids[i]);
					if (rn != null && !BpTech.Contains(ids[i]) && !BpJobIds.Contains(ids[i])) {
						BpJobIds.Add(ids[i]);
						BpJobLefts.Add(System.Math.Clamp(lefts[i], 1, ResearchTech.ResearchSecs(rn) * 60));
					}
				}
			}
			if (tag.ContainsKey("rsU"))
				BpUp = System.Math.Clamp(tag.GetInt("rsU"), 0, ResearchPoints.MaxUpgrades);
			if (tag.ContainsKey("rsP"))
				BpPoints = System.Math.Max(0, tag.GetInt("rsP"));
			if (tag.ContainsKey("rsL"))
				BpLife = System.Math.Max(0, tag.GetInt("rsL"));
			if (tag.ContainsKey("rsC"))
				BpCharge = System.Math.Clamp(tag.GetInt("rsC"), 0, ResearchEngineSystem.MaxCharge + 50);
			if (tag.ContainsKey("rsM"))
				BpMode = System.Math.Clamp(tag.GetInt("rsM"), 0, 2);
			if (tag.ContainsKey("rsE"))
				BpEffect = System.Math.Clamp(tag.GetInt("rsE"), 0, 3);
			if (tag.ContainsKey("rsS"))
				BpStyle = System.Math.Clamp(tag.GetInt("rsS"), 0, 2);
			if (tag.ContainsKey("rsA"))
				BpAuto = tag.GetInt("rsA") != 0 ? 1 : 0;
			if (tag.ContainsKey("rsR"))
				BpRebound = System.Math.Clamp(tag.GetInt("rsR"), 0, ResearchEngineSystem.MaxRebound);
		}

		public override void ResetEffects()
		{
		}

		public override void PostUpdate()
		{
			if (MomentumTimer > 0 && --MomentumTimer <= 0)
				MomentumMult = 1f;
			// The fight is over: the 100-charge lockout fixes itself (#7).
			if (BossCharged && !ResearchEngineSystem.AnyBossActive())
				BossCharged = false;
		}
	}
}
