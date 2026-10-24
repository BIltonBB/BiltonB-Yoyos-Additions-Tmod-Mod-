using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Random events (#8): once Strange Signals is researched, the engine
	// picks up strange happenings, good or bad — who knows why. Rolls run
	// server/single-player side only; clients converge via WorldData.
	//
	// Two kinds: instant one-shots (points/charge/job-time shifts) and
	// timed conditions that last until the next roll. Good Omens doubles
	// good odds, Warding Charms halves bad hits, Storm Chasing doubles
	// roll frequency, Eye of the Storm halves bad timed durations.
	public static class ResearchEvents
	{
		public const int BaseCadence = 7200; // 2 in-game hours between rolls
		public const int ChasedCadence = 3600; // 1 in-game hour with Storm Chasing

		// Timed conditions.
		public const string Tide = "tide";
		public const string Drought = "drought";
		public const string Frenzy = "frenzy";
		public const string Slump = "slump";

		// Instant one-shots.
		public const string Windfall = "windfall";
		public const string Leak = "leak";
		public const string Breakthrough = "breakthrough";
		public const string Setback = "setback";
		public const string Surge = "surge";
		public const string Brownout = "brownout";

		public static int CadenceOf(ResearchData d)
		{
			return ResearchTech.TechOn(d, ResearchTech.EvChase) ? ChasedCadence : BaseCadence;
		}

		public static bool IsKnown(string id)
		{
			return id == Tide || id == Drought || id == Frenzy || id == Slump
				|| id == Windfall || id == Leak || id == Breakthrough || id == Setback
				|| id == Surge || id == Brownout;
		}

		public static bool IsTimed(string id) => id == Tide || id == Drought || id == Frenzy || id == Slump;

		public static bool IsGood(string id) => id == Tide || id == Frenzy || id == Windfall || id == Breakthrough || id == Surge;

		public static string NameOf(string id) => id switch {
			Tide => "High Tide",
			Drought => "Drought",
			Frenzy => "Frenzy",
			Slump => "Slump",
			Windfall => "Windfall",
			Leak => "Leak",
			Breakthrough => "Breakthrough",
			Setback => "Setback",
			Surge => "Surge",
			Brownout => "Brownout",
			_ => "Calm",
		};

		public static string DescOf(string id) => id switch {
			Tide => "trickle x2",
			Drought => "trickle choked",
			Frenzy => "+25% research speed",
			Slump => "research slowed",
			Windfall => "+50 pts",
			Leak => "points leaked",
			Breakthrough => "a job leaps forward",
			Setback => "a job slips back",
			Surge => "+20 charge",
			Brownout => "charge drained",
			_ => "nothing stirs",
		};

		// Timed-condition multipliers (Charms softens the bad ones).
		// Dormant without Strange Signals (respec/toggle pauses effects).
		public static float ActiveTrickleMult(ResearchData d)
		{
			if (!ResearchTech.TechOn(d, ResearchTech.EvSig))
				return 1f;
			string id = d?.ActiveEventId ?? "";
			if (id == Tide)
				return 2f;
			if (id == Drought)
				return ResearchTech.TechOn(d, ResearchTech.EvCharm) ? 0.75f : 0.5f;
			return 1f;
		}

		public static float ActiveRateMult(ResearchData d)
		{
			if (!ResearchTech.TechOn(d, ResearchTech.EvSig))
				return 1f;
			string id = d?.ActiveEventId ?? "";
			if (id == Frenzy)
				return 1.25f;
			if (id == Slump)
				return ResearchTech.TechOn(d, ResearchTech.EvCharm) ? 0.875f : 0.75f;
			return 1f;
		}

		// One roll: weighted table (Omens doubles good weights), then
		// apply. Called with the world tick, engine known live.
		public static void Roll(Point tile, ResearchData d)
		{
			bool omens = ResearchTech.TechOn(d, ResearchTech.EvOmen);
			int good = omens ? 2 : 1;
			(string id, int w)[] table = {
				(Windfall, 10 * good), (Leak, 8),
				(Breakthrough, 8 * good), (Setback, 6),
				(Surge, 8 * good), (Brownout, 6),
				(Tide, 8 * good), (Drought, 6),
				(Frenzy, 8 * good), (Slump, 6),
			};
			int total = 0;
			foreach (var e in table)
				total += e.w;
			int roll = Main.rand.Next(total);
			string picked = table[0].id;
			foreach (var e in table) {
				if (roll < e.w) {
					picked = e.id;
					break;
				}
				roll -= e.w;
			}
			// Job swings need a running job: fold them into points swings.
			if (d.Research == null || d.Research.Count == 0) {
				if (picked == Breakthrough)
					picked = Windfall;
				else if (picked == Setback)
					picked = Leak;
			}
			Apply(tile, d, picked);
		}

		private static void Announce(Point tile, string id)
		{
			if (Main.dedServ)
				return;
			if (ResearchEngineUISystem.Open && ResearchEngineUISystem.OpenTile == tile)
				ResearchEngineUISystem.SetMessage($"Strange event: {NameOf(id)} — {DescOf(id)}. Who knows why.");
		}

		private static void Fx(Point tile)
		{
			if (Main.dedServ)
				return;
			Vector2 center = new(tile.X * 16f + 24f, tile.Y * 16f + 16f);
			for (int i = 0; i < 10; i++) {
				Dust dust = Dust.NewDustDirect(center - new Vector2(4, 4), 8, 8, DustID.MagicMirror, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 100, default, 1f);
				dust.noGravity = true;
			}
		}

		public static void Apply(Point tile, ResearchData d, string id)
		{
			if (d == null || !IsKnown(id))
				return;
			int soft = ResearchTech.TechOn(d, ResearchTech.EvCharm) ? 2 : 1; // bad hits halved
			switch (id) {
				case Windfall:
					ResearchPoints.Add(d, 50);
					break;
				case Leak: {
					int loss = 30 / soft;
					d.Points = System.Math.Max(0, d.Points - loss);
					ResearchEngineSystem.SyncWorld();
					break;
				}
				case Breakthrough: {
					var job = RandomJob(d);
					if (job != null)
						job.Left = System.Math.Max(0, job.Left - 7200);
					ResearchEngineSystem.SyncWorld();
					break;
				}
				case Setback: {
					var job = RandomJob(d);
					if (job != null) {
						var node = ResearchTech.ById(job.Id);
						int total = node == null ? job.Left + 7200 / soft : ResearchTech.ResearchSecs(node) * 60;
						job.Left = System.Math.Min(total, job.Left + 7200 / soft);
					}
					ResearchEngineSystem.SyncWorld();
					break;
				}
				case Surge:
					ResearchEngineSystem.AddCharge(tile, 20);
					break;
				case Brownout: {
					d.Charge = System.Math.Max(0, d.Charge - 20 / soft);
					ResearchEngineSystem.SyncWorld();
					break;
				}
				default: {
					// Timed condition until the next roll (bad ones fade
					// twice as fast with Eye of the Storm).
					int dur = CadenceOf(d);
					if (!IsGood(id) && ResearchTech.TechOn(d, ResearchTech.EvEye))
						dur /= 2;
					d.ActiveEventId = id;
					d.EventLeft = dur;
					ResearchEngineSystem.SyncWorld();
					break;
				}
			}
			Announce(tile, id);
			Fx(tile);
		}

		private static ResearchJob RandomJob(ResearchData d)
		{
			if (d?.Research == null || d.Research.Count == 0)
				return null;
			return d.Research[Main.rand.Next(d.Research.Count)];
		}

		// Ticks the active timed condition; true when it just expired.
		public static bool TickCondition(ResearchData d)
		{
			if (d == null || string.IsNullOrEmpty(d.ActiveEventId))
				return false;
			if (!IsTimed(d.ActiveEventId)) {
				d.ActiveEventId = "";
				d.EventLeft = 0;
				return true;
			}
			d.EventLeft--;
			if (d.EventLeft > 0)
				return false;
			d.ActiveEventId = "";
			d.EventLeft = 0;
			return true;
		}
	}
}
