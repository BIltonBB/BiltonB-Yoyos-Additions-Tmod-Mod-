using System.Collections.Generic;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria;

namespace BiltonsYoyoMod.Traps
{
	// Wildstring-style rope for String Link barriers.
	//
	// Both endpoints are pinned at the two trap tiles (unlike the orbit-style
	// WildStringBase, which anchors to a moving player with a free endpoint).
	// Otherwise the solver mirrors the mod's wildstring feel: verlet
	// integration, gravity, distance constraints, damping, and solid-tile
	// push-out so the string drapes over blocks instead of clipping through.
	//
	// Deterministic: no randomness, fixed iteration counts, inputs are only
	// the two tile centers + bend. Server steps for damage, clients step in
	// PostDraw for visuals; both converge to the same sag.
	public static class LinkRope
	{
		public const int PointCount = 26;
		private const int Substeps = 2;
		private const int Iterations = 5;
		private const float Gravity = 0.35f;
		private const float Damping = 0.99f;

		private class RopeState
		{
			public Vector2[] Pos = new Vector2[PointCount];
			public Vector2[] Prev = new Vector2[PointCount];
			public int Bend;
			public int Variant;
			public bool HasSnag;
			public Vector2 SnagPos;
			public float SnagRadius;
			public float SnagEase;
			public bool Init;
			public int LastStepFrame = -1;
		}

		private static readonly Dictionary<Point, RopeState> Cache = new();

		public static Point Canonical(Point a, Point b)
		{
			if (a.X > b.X || (a.X == b.X && a.Y > b.Y))
				return b;
			return a;
		}

		private static float RestTotal(float dist, int bend, int variant)
		{
			// IRL finger-push: loose Cotton/Silk stretch deep, Shadow runs
			// tight. Rest length always >= span (never taut-pulls the ends).
			float stiff = System.Math.Clamp(variant, 0, 7) switch {
				5 => 1.30f,
				6 => 1.08f,
				7 => 1.18f,
				_ => 1.35f - 0.08f * System.Math.Clamp(variant, 0, 4),
			};
			return dist * (1f + 0.12f * bend) * stiff;
		}

		// Per-string feel: gravity sag + lateral compliance + ease speed.
		// Cotton yields soft and slow; Shadow barely yields and snaps back.
		private static float GravFor(int variant) => variant switch {
			0 => 0.32f, 1 => 0.34f, 2 => 0.35f, 3 => 0.36f, 4 => 0.38f,
			5 => 0.33f, 6 => 0.37f, 7 => 0.36f, _ => 0.35f,
		};
		private static float ComplyFor(int variant) => variant switch {
			0 => 0.65f, 1 => 0.55f, 2 => 0.45f, 3 => 0.35f, 4 => 0.25f,
			5 => 0.60f, 6 => 0.30f, 7 => 0.40f, _ => 0.45f,
		};
		private static float EaseFor(int variant) => variant switch {
			0 => 0.08f, 1 => 0.10f, 2 => 0.12f, 3 => 0.14f, 4 => 0.16f,
			5 => 0.09f, 6 => 0.15f, 7 => 0.13f, _ => 0.12f,
		};

		private static RopeState GetOrCreate(Point a, Point b, int bend, Vector2 pa, Vector2 pb)
		{
			return GetOrCreate(a, b, bend, 0, pa, pb);
		}

		private static RopeState GetOrCreate(Point a, Point b, int bend, int variant, Vector2 pa, Vector2 pb)
		{
			Point key = Canonical(a, b);
			if (!Cache.TryGetValue(key, out var st)) {
				st = new RopeState { Bend = bend, Variant = variant };
				InitRope(st, pa, pb);
				st.HasSnag = false;
				Cache[key] = st;
				return st;
			}
			if (st.Bend != bend || st.Variant != variant) {
				st.Bend = bend;
				st.Variant = variant;
				InitRope(st, pa, pb);
			}
			return st;
		}

		private static void InitRope(RopeState st, Vector2 pa, Vector2 pb)
		{
			for (int i = 0; i < PointCount; i++) {
				float t = i / (float)(PointCount - 1);
				Vector2 p = Vector2.Lerp(pa, pb, t);
				// De-bury at birth: a straight span through a hill starts
				// draped, never embedded-stuck waiting to crawl out.
				if (i > 0 && i < PointCount - 1)
					p = PushOut(p);
				st.Pos[i] = p;
				st.Prev[i] = p;
			}
			st.Init = true;
		}

		public static void Forget(Point a, Point b)
		{
			Cache.Remove(Canonical(a, b));
		}

		public static void ClearInvalid()
		{
			var dead = new List<Point>();
			foreach (var kv in Cache) {
				bool alive = false;
				foreach (var tkv in TrapSystem.Traps) {
					if (tkv.Value.Kind != TrapKind.Link || !tkv.Value.Linked)
						continue;
					Point other = tkv.Value.LinkPoint;
					if (Canonical(tkv.Key, other) == kv.Key) {
						alive = TrapSystem.IsAnyTrapTile(tkv.Key) && TrapSystem.IsAnyTrapTile(other);
						break;
					}
				}
				if (!alive)
					dead.Add(kv.Key);
			}
			foreach (var d in dead) {
				Cache.Remove(d);
				snapHalves.Remove(d);
			}
		}

		public static bool SolidAt(Vector2 p)
		{
			return Collision.SolidCollision(p - new Vector2(2f, 2f), 4, 4);
		}

		public static Vector2 PushOut(Vector2 p)
		{
			if (!SolidAt(p))
				return p;
			for (int r = 1; r <= 8; r++) {
				for (int k = 0; k < 8; k++) {
					float ang = k / 8f * MathHelper.TwoPi;
					Vector2 test = p + ang.ToRotationVector2() * r;
					if (!SolidAt(test))
						return test;
				}
			}
			return p;
		}

		// Fidelity dials (server-synced, so both sides agree exactly).
		// Defaults reproduce current behavior; turning them down only
		// costs drape fidelity, never hits (constraints still converge).
		private static int SimSubsteps()
		{
			try {
				return System.Math.Clamp(ModContent.GetInstance<Configuration.YoyoServerConfig>().RopeSubsteps, 1, 2);
			}
			catch {
				return 2;
			}
		}

		private static int SimIters()
		{
			try {
				return System.Math.Clamp(ModContent.GetInstance<Configuration.YoyoServerConfig>().RopeIterations, 2, 5);
			}
			catch {
				return 5;
			}
		}

		private static bool SimBuzz()
		{
			try {
				return ModContent.GetInstance<Configuration.YoyoServerConfig>().RopeBuzz;
			}
			catch {
				return true;
			}
		}

		private static void StepState(RopeState st, Vector2 pa, Vector2 pb, float buzz)
		{
			int substeps = SimSubsteps();
			int iters = SimIters();
			if (!SimBuzz())
				buzz = 0f;
			float dist = Vector2.Distance(pa, pb);
			if (dist < 1f)
				dist = 1f;
			int variant = System.Math.Clamp(st.Variant, 0, 7);
			float segLen = RestTotal(dist, st.Bend, variant) / (PointCount - 1);
			float g = GravFor(variant) / substeps;
			float comply = ComplyFor(variant);
			float easeStep = EaseFor(variant);
			// Liquid medium (any scenario: water/lava/honey): buoyancy cuts
			// gravity, thick drag grabs motion. Probed at midspan from synced
			// tiles, so server and client agree exactly.
			float gravMult = 1f;
			float dampN = 1f;
			{
				Vector2 midProbe = st.Pos[PointCount / 2];
				int tx = (int)(midProbe.X / 16f);
				int ty = (int)(midProbe.Y / 16f);
				if (WorldGen.InWorld(tx, ty)) {
					Tile mt = Framing.GetTileSafely(tx, ty);
					if (mt.LiquidAmount > 0) {
						switch (mt.LiquidType) {
							case Terraria.ID.LiquidID.Lava: gravMult = 0.2f; dampN = 0.94f; break; // lava: buoyant syrup
							case Terraria.ID.LiquidID.Honey: gravMult = 0.3f; dampN = 0.96f; break; // honey: barely lets go
							default: gravMult = 0.45f; dampN = 0.965f; break; // water: floats + drags
						}
					}
				}
			}

			for (int s = 0; s < substeps; s++) {
				for (int i = 1; i < PointCount - 1; i++) {
					Vector2 cur = st.Pos[i];
					Vector2 tang = st.Pos[System.Math.Min(i + 1, PointCount - 1)] - st.Pos[System.Math.Max(i - 1, 0)];
					float tl = tang.Length();
					Vector2 vel = cur - st.Prev[i];
					// Cross-flow principle (Morison-style split): sideways
					// motion meets far more drag than sliding along the rope.
					Vector2 vT = Vector2.Zero;
					Vector2 vN = vel;
					if (tl > 0.001f) {
						Vector2 taxis = tang / tl;
						vT = taxis * Vector2.Dot(vel, taxis);
						vN = vel - vT;
					}
					vel = vT * 0.998f + vN * (0.985f * dampN);
					// Coulomb dry bite on top of viscous Rayleigh-style
					// damping: constant decel opposing motion, no flip.
					float vl = vel.Length();
					if (vl > 0.001f)
						vel -= vel / vl * System.Math.Min(0.004f, vl * 0.5f);
					vel.Y += g * gravMult;
					st.Prev[i] = cur;
					st.Pos[i] = PushOut(cur + vel);
				}
				st.Pos[0] = pa;
				st.Prev[0] = pa;
				st.Pos[PointCount - 1] = pb;
				// Snagged victim: the rope yields AWAY like a real string being
				// pushed with a finger (never threads through the body).
				// Compliance varies: Cotton gives deep, Kevlar barely moves.
				// Eased so nothing pops; lateral load tensions non-linearly
				// (the TickLink strain model charges extra per victim).
				float wantEase = st.HasSnag ? 1f : 0f;
				st.SnagEase += System.Math.Clamp(wantEase - st.SnagEase, -easeStep, easeStep);
				if (st.SnagEase > 0.001f) {
					for (int i = 1; i < PointCount - 1; i++) {
						Vector2 away = st.Pos[i] - st.SnagPos;
						float vd = away.Length();
						if (vd > 0.001f && vd < st.SnagRadius)
							st.Pos[i] += away / vd * (st.SnagRadius - vd) * comply * st.SnagEase;
					}
				}
				st.Prev[PointCount - 1] = pb;

				for (int it = 0; it < iters; it++) {
					st.Pos[0] = pa;
					st.Pos[PointCount - 1] = pb;
					for (int i = 0; i < PointCount - 1; i++) {
						Vector2 first = st.Pos[i];
						Vector2 second = st.Pos[i + 1];
						Vector2 diff = second - first;
						float d = diff.Length();
						if (d < 0.001f)
							continue;
						float err = d - segLen;
						Vector2 corr = diff / d * err;
						bool pinFirst = i == 0;
						bool pinLast = i + 1 == PointCount - 1;
						if (pinFirst)
							st.Pos[i + 1] -= corr;
						else if (pinLast)
							st.Pos[i] += corr;
						else {
							st.Pos[i] += corr * 0.5f;
							st.Pos[i + 1] -= corr * 0.5f;
						}
					}
				}
				for (int i = 1; i < PointCount - 1; i++)
					st.Pos[i] = PushOut(st.Pos[i]);
				st.Pos[0] = pa;
				st.Pos[PointCount - 1] = pb;
			}
			// Tension shimmer (transverse wave): a loaded string buzzes —
			// amplitude and phase speed grow with stress, like v ~= sqrt(T/mu).
			// Pure function of synced stress + tick, so server and client
			// converge. Written to Pos AND Prev so no phantom velocity
			// enters the verlet; endpoints stay pinned via the envelope.
			if (buzz > 0.01f) {
				float tick = (float)(Main.GameUpdateCount % 3600);
				float omega = 0.25f + 0.55f * buzz;
				float amp = 1.5f * buzz;
				// Wave impedance reflection: the hanging victim is a
				// discontinuity — part of the wave reflects there, so the
				// buzz runs hotter around the snag point (hotspot).
				float snagT = -1f;
				if (st.HasSnag) {
					Vector2 sab = pb - pa;
					float sl2 = sab.LengthSquared();
					if (sl2 > 1f)
						snagT = System.Math.Clamp(Vector2.Dot(st.SnagPos - pa, sab) / sl2, 0f, 1f);
				}
				for (int i = 1; i < PointCount - 1; i++) {
					float t = i / (float)(PointCount - 1);
					float env = (float)System.Math.Sin(System.Math.PI * t);
					float hot = 1f;
					if (snagT >= 0f) {
						float dd = (t - snagT) / 0.15f;
						hot = 1f + 0.6f * (float)System.Math.Exp(-dd * dd);
					}
					Vector2 tang = st.Pos[System.Math.Min(i + 1, PointCount - 1)] - st.Pos[System.Math.Max(i - 1, 0)];
					float tl = tang.Length();
					if (tl < 0.001f)
						continue;
					Vector2 nrm = new Vector2(-tang.Y / tl, tang.X / tl);
					Vector2 off = nrm * (amp * env * hot * (float)System.Math.Sin(i * 0.55f - tick * omega));
					st.Pos[i] += off;
					st.Prev[i] += off;
				}
			}
		}

		// Server/single-player stepping: advance every linked pair once per tick.
		public static void StepAllSim()
		{
			ClearInvalid();
			foreach (var kv in TrapSystem.Traps) {
				if (kv.Value.Kind != TrapKind.Link || !kv.Value.Linked)
					continue;
				Point other = kv.Value.LinkPoint;
				if (kv.Key.X > other.X || (kv.Key.X == other.X && kv.Key.Y > other.Y))
					continue; // canonical side only
				if (!TrapSystem.IsAnyTrapTile(other))
					continue;
				if (!TrapSystem.Traps.TryGetValue(other, out var os))
					continue;
				Vector2 pa = TrapSystem.TileCenter(kv.Key);
				Vector2 pb = TrapSystem.TileCenter(other);
				int variant = System.Math.Clamp(System.Math.Max(kv.Value.StringVariant, os.StringVariant), 0, 7);
				RopeState st = GetOrCreate(kv.Key, other, kv.Value.Bend, variant, pa, pb);
				RefreshSnag(kv.Key, other);
				StepState(st, pa, pb, PairBuzz(kv.Value, os));
			}
		}

		// Shimmer drive (0..1): the hotter of the two endpoints' stress
		// fractions. Null-safe (missing state reads calm). Both sim and
		// visual paths derive it from synced state, so they agree.
		// Shimmer drive (0..1): the hotter of the two endpoints' stress
		// fractions. Null-safe (missing state reads calm). Both sim and
		// visual paths derive it from synced state, so they agree.
		private static float PairBuzz(TrapState sa, TrapState sb)
		{
			float f = 0f;
			if (sa != null && sa.MaxStress() > 0)
				f = System.Math.Max(f, sa.Stress / (float)sa.MaxStress());
			if (sb != null && sb.MaxStress() > 0)
				f = System.Math.Max(f, sb.Stress / (float)sb.MaxStress());
			return System.Math.Clamp(f, 0f, 1f);
		}

		// Client visual stepping: once per render frame per pair.
		public static Vector2[] StepClient(Point a, Point b, int bend)
		{
			int variant = 0;
			if (TrapSystem.Traps.TryGetValue(a, out var sa) && TrapSystem.Traps.TryGetValue(b, out var sb))
				variant = System.Math.Clamp(System.Math.Max(sa.StringVariant, sb.StringVariant), 0, 7);
			Vector2 pa = TrapSystem.TileCenter(a);
			Vector2 pb = TrapSystem.TileCenter(b);
			RopeState st = GetOrCreate(a, b, bend, variant, pa, pb);
			RefreshSnag(a, b);
			int frame = unchecked((int)Main.GameUpdateCount);
			if (st.LastStepFrame != frame) {
				TrapSystem.Traps.TryGetValue(a, out var sa2);
				TrapSystem.Traps.TryGetValue(b, out var sb2);
				StepState(st, pa, pb, PairBuzz(sa2, sb2));
				st.LastStepFrame = frame;
			}
			return st.Pos;
		}

		// Snag refresh: while a restraining barrier holds a victim, both the
		// server sim (damage) and the client sim (visuals) bend the rope toward
		// the same victim, so shape and hits agree.
		public static void RefreshSnag(Point a, Point b)
		{
			Point key = Canonical(a, b);
			if (!Cache.TryGetValue(key, out var st))
				return;
			st.HasSnag = false;
			if (!TrapSystem.Traps.TryGetValue(a, out var sa) || !TrapSystem.Traps.TryGetValue(b, out var sb))
				return;
			if (!sa.Linked || !sb.Linked || !sa.WiredActive)
				return;
			if (sa.Behavior == 0 && sb.Behavior == 0)
				return;
			float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
			foreach (var p in st.Pos) {
				if (p.X < minX) minX = p.X;
				if (p.Y < minY) minY = p.Y;
				if (p.X > maxX) maxX = p.X;
				if (p.Y > maxY) maxY = p.Y;
			}
			float best = float.MaxValue;
			Vector2 bestPos = Vector2.Zero;
			float bestRadius = 20f;
			bool found = false;
			float spill = 0f;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC n = Main.npc[i];
				if (n == null || !n.active || n.life <= 0 || n.friendly || n.townNPC || n.dontTakeDamage)
					continue;
				if (n.position.X + n.width < minX - 70f || n.position.X > maxX + 70f || n.position.Y + n.height < minY - 70f || n.position.Y > maxY + 70f)
					continue;
				if (!TrapSystem.RopeHits(st.Pos, n.position, new Vector2(n.width, n.height), 110f, ref spill))
					continue;
				float d = Vector2.DistanceSquared(n.Center, (st.Pos[0] + st.Pos[PointCount - 1]) * 0.5f);
				if (d < best) { best = d; bestPos = n.Center; bestRadius = System.Math.Max(n.width, n.height) * 0.5f + 8f; found = true; }
			}
			// Players push the rope too (two-way coupling): an eligible player
			// leaning on the barrier bends it, exactly like held NPCs.
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player pl = Main.player[i];
				if (pl == null || !pl.active || pl.dead)
					continue;
				if (!TrapSystem.LinkEligiblePlayer(sa, pl) && !TrapSystem.LinkEligiblePlayer(sb, pl))
					continue;
				if (pl.position.X + pl.width < minX - 70f || pl.position.X > maxX + 70f || pl.position.Y + pl.height < minY - 70f || pl.position.Y > maxY + 70f)
					continue;
				if (!TrapSystem.RopeHits(st.Pos, pl.position, pl.Size, 110f, ref spill))
					continue;
				float pd = Vector2.DistanceSquared(pl.Center, (st.Pos[0] + st.Pos[PointCount - 1]) * 0.5f);
				if (pd < best) { best = pd; bestPos = pl.Center; bestRadius = System.Math.Max(pl.width, pl.height) * 0.5f + 8f; found = true; }
			}
			if (!found)
				return;
			st.HasSnag = true;
			st.SnagPos = bestPos;
			st.SnagRadius = bestRadius;
		}

		// Snapped halves (visual only, 5s fade): on a snap the rope does NOT
		// vanish — IRL both cut ends violently recoil toward their anchors
		// (snapback from released elastic energy), oscillate, and dangle.
		// Each half is pinned at its anchor tile with a free whipping end.
		// Stepped in StepClient only (never the server sim): unwired ropes
		// deal no hits, so visuals here cannot affect damage or fairness.
		public const int SnapHalfLife = 300; // 5s at 60 ticks
		private const int SnapHalfPoints = 13;

		private class SnapHalf
		{
			public Vector2[] Pos = new Vector2[SnapHalfPoints];
			public Vector2[] Prev = new Vector2[SnapHalfPoints];
			public float[] Rest = new float[SnapHalfPoints - 1];
			public int Variant;
		}

		private static readonly Dictionary<Point, (SnapHalf, SnapHalf, long, long)> snapHalves = new();

		// Capture the live shape at the snap instant; free ends get a kick
		// toward their anchors (faster at the free tip, like a real whip),
		// scaled by rope size (bigger ropes snap harder — same reason the
		// debris and crack scale with max stress).
		public static void SnapRope(Point a, Point b, Vector2[] pts, int variant, float power)
		{
			if (Main.dedServ || pts == null || pts.Length < 4)
				return;
			try {
				if (!ModContent.GetInstance<Configuration.YoyoConfig>().TrapVisuals.SnapHalves)
					return;
			}
			catch { }
			Point key = Canonical(a, b);
			int mid = pts.Length / 2;
			var A = new SnapHalf { Variant = System.Math.Clamp(variant, 0, 7) };
			var B = new SnapHalf { Variant = System.Math.Clamp(variant, 0, 7) };
			// Arc-length resampling (even spacing along the shape): plain
			// index fractions collapse into dot-blobs whenever captured
			// points cluster (constraint stacking), while arc length skips
			// clusters naturally and always spans the full half.
			Vector2[] ra = Resample(pts, 0, mid, SnapHalfPoints);
			Vector2[] rb = Resample(pts, mid, pts.Length - 1, SnapHalfPoints);
			for (int i = 0; i < SnapHalfPoints; i++) {
				A.Pos[i] = ra[i];
				B.Pos[i] = rb[i];
			}
			KickHalf(A, pts[0], power, true);
			KickHalf(B, pts[pts.Length - 1], power, false);
			// Rest lengths from the capture shape: the halves stay ropes
			// (distance constraints), not drifting particles.
			for (int i = 0; i < SnapHalfPoints - 1; i++) {
				A.Rest[i] = System.Math.Max(1f, Vector2.Distance(A.Pos[i], A.Pos[i + 1]));
				B.Rest[i] = System.Math.Max(1f, Vector2.Distance(B.Pos[i], B.Pos[i + 1]));
			}
			snapHalves[key] = (A, B, (long)Main.GameUpdateCount, (long)Main.GameUpdateCount);
			if (snapHalves.Count > 64) {
				long now = (long)Main.GameUpdateCount;
				var dead = new System.Collections.Generic.List<Point>();
				foreach (var kv in snapHalves) {
					if (now - kv.Value.Item3 > SnapHalfLife)
						dead.Add(kv.Key);
				}
				foreach (var k in dead)
					snapHalves.Remove(k);
				if (snapHalves.Count > 64)
					snapHalves.Remove(key);
			}
		}

		// Even arc-length resample of pts[ia..ib] into n points. Immune to
		// clustered inputs (degenerate runs contribute ~zero length).
		private static Vector2[] Resample(Vector2[] pts, int ia, int ib, int n)
		{
			var output = new Vector2[n];
			if (pts == null || pts.Length == 0 || n <= 0)
				return output;
			ia = System.Math.Clamp(ia, 0, pts.Length - 1);
			ib = System.Math.Clamp(ib, 0, pts.Length - 1);
			if (ib < ia) {
				int tmp = ia;
				ia = ib;
				ib = tmp;
			}
			float total = 0f;
			for (int i = ia; i < ib; i++)
				total += Vector2.Distance(pts[i], pts[i + 1]);
			if (total < 0.001f) {
				for (int i = 0; i < n; i++)
					output[i] = pts[ia];
				return output;
			}
			float step = total / (n - 1);
			output[0] = pts[ia];
			output[n - 1] = pts[ib];
			int seg = ia;
			float segStart = 0f;
			float segLen = Vector2.Distance(pts[ia], pts[System.Math.Min(ia + 1, ib)]);
			for (int i = 1; i < n - 1; i++) {
				float want = step * i;
				while (seg < ib - 1 && segStart + segLen < want) {
					segStart += segLen;
					seg++;
					segLen = Vector2.Distance(pts[seg], pts[System.Math.Min(seg + 1, ib)]);
				}
				float f = segLen > 0.001f ? System.Math.Clamp((want - segStart) / segLen, 0f, 1f) : 0f;
				output[i] = Vector2.Lerp(pts[seg], pts[System.Math.Min(seg + 1, ib)], f);
			}
			return output;
		}

		private static void KickHalf(SnapHalf h, Vector2 anchor, float power, bool freeIsLast)
		{
			for (int i = 0; i < SnapHalfPoints; i++) {
				float t = i / (float)(SnapHalfPoints - 1);
				// Kick grows toward the FREE end (cut whips, anchor holds).
				float f = freeIsLast ? t : 1f - t;
				Vector2 toAnchor = anchor - h.Pos[i];
				float dl = toAnchor.Length();
				Vector2 dir = dl > 0.001f ? toAnchor / dl : new Vector2(0f, -1f);
				h.Prev[i] = h.Pos[i] - dir * ((2f + 9f * f) * power);
			}
		}

		// Step both halves with the same physics family as the main rope:
		// verlet + gravity, distance constraints (rest from capture),
		// pinned ANCHOR end (first index for half A, last for half B —
		// pinning the cut end instead freezes the whip, the classic
		// halves bug), solid push-out. Contact then slides (velocity
		// bled on touch) so halves drape and rest on tiles instead of
		// jittering stuck inside them.
		private static bool StepHalves(SnapHalf h, Vector2 anchor, bool pinFirst)
		{
			int pin = pinFirst ? 0 : SnapHalfPoints - 1;
			int iters = SimIters();
			// Same integrator family as the live rope (variant gravity,
			// shared damping, full constraint iterations): halves move
			// like rope, not slow-mo particles.
			float g = GravFor(h.Variant);
			for (int i = 0; i < SnapHalfPoints; i++) {
				if (i == pin)
					continue;
				Vector2 cur = h.Pos[i];
				Vector2 vel = (cur - h.Prev[i]) * Damping;
				vel.Y += g;
				h.Prev[i] = cur;
				h.Pos[i] = cur + vel;
			}
			h.Pos[pin] = anchor;
			h.Prev[pin] = anchor;
			for (int it = 0; it < iters; it++) {
				for (int i = 0; i < SnapHalfPoints - 1; i++) {
					Vector2 diff = h.Pos[i + 1] - h.Pos[i];
					float d = diff.Length();
					if (d < 0.001f)
						continue;
					float err = d - h.Rest[i];
					Vector2 corr = diff / d * err;
					bool pinA = (i == pin);
					bool pinB = (i + 1 == pin);
					if (pinA)
						h.Pos[i + 1] -= corr;
					else if (pinB)
						h.Pos[i] += corr;
					else {
						h.Pos[i] += corr * 0.5f;
						h.Pos[i + 1] -= corr * 0.5f;
					}
				}
				h.Pos[pin] = anchor;
			}
			for (int i = 0; i < SnapHalfPoints; i++) {
				if (i == pin)
					continue;
				Vector2 freed = PushOut(h.Pos[i]);
				if ((freed - h.Pos[i]).LengthSquared() > 0.01f)
					h.Prev[i] = Vector2.Lerp(h.Prev[i], freed, 0.8f);
				h.Pos[i] = freed;
			}
			h.Pos[pin] = anchor;
			h.Prev[pin] = anchor;
			return true;
		}

		// Read halves for drawing. Steps them by ELAPSED TICKS (catch-up,
		// capped), never once-per-draw-call: below 60fps they must run in
		// real time, not slow motion; above it they must not fast-forward.
		// Fade 1->0 over the 5s lifetime. Heat reports snap freshness
		// (1 at the snap, gone ~1.6s later) for the red-hot tint.
		public static bool TryGetSnapHalves(Point a, Point b, Vector2 anchorA, Vector2 anchorB, out Vector2[] posA, out Vector2[] posB, out float fade, out float heat)
		{
			posA = null;
			posB = null;
			fade = 0f;
			heat = 0f;
			Point key = Canonical(a, b);
			if (!snapHalves.TryGetValue(key, out var sh))
				return false;
			long now = (long)Main.GameUpdateCount;
			long age = now - sh.Item3;
			if (age < 0 || age > SnapHalfLife) {
				snapHalves.Remove(key);
				return false;
			}
			heat = System.Math.Max(0f, 1f - age / 100f);
			int steps = System.Math.Clamp((int)(now - sh.Item4), 0, 5);
			for (int q = 0; q < steps; q++) {
				StepHalves(sh.Item1, anchorA, true);
				StepHalves(sh.Item2, anchorB, false);
			}
			sh.Item4 = now;
			snapHalves[key] = sh;
			posA = sh.Item1.Pos;
			posB = sh.Item2.Pos;
			fade = 1f - age / (float)SnapHalfLife;
			return true;
		}

		public static Vector2[] GetPoints(Point a, Point b, int bend)
		{
			int variant = 0;
			if (TrapSystem.Traps.TryGetValue(a, out var sa) && TrapSystem.Traps.TryGetValue(b, out var sb))
				variant = System.Math.Clamp(System.Math.Max(sa.StringVariant, sb.StringVariant), 0, 7);
			Vector2 pa = TrapSystem.TileCenter(a);
			Vector2 pb = TrapSystem.TileCenter(b);
			RopeState st = GetOrCreate(a, b, bend, variant, pa, pb);
			return st.Pos;
		}
	}
	public static class SnareRope
	{
		// Snare deployment physics, mirroring the real WildStringBase rotation model:
		// verlet + gravity, a driven center (first points steered toward the orbit
		// speed), and parent-to-child ROTATIONAL MOMENTUM PROPAGATION, so spin
		// travels down the rope with lag instead of being forced everywhere at
		// once. Same constants as WildStringBase; deterministic (no randomness).
		public const int Points = 20;
		private const int Substeps = 2;
		private const int Iters = 5;
		private const float Gravity = 0.30f;
		private const float Damping = 0.998f;
		private const int DrivenPointCount = 5;
		private const float RotationTransfer = 0.62f;
		private const float CollisionRotationTransfer = 0.08f;
		private const float RotationRetention = 0.995f;
		private const float MinimumRotationalMomentum = 0.01f;
		private const float MaximumRotationalMomentum = 1.5f;
		private const float CenterTorque = 0.095f;
		private const float CenterSpeedCorrection = 0.42f;
		private const float MinimumCenterSpeed = 0.65f;
		private const float MaximumCenterVelocity = 18f;

		public class State
		{
			public Vector2[] Pos = new Vector2[Points];
			public Vector2[] Prev = new Vector2[Points];
			public float[] Mom = new float[Points];
			public int Variant;
			public float Dir;
		}

		private static readonly Dictionary<int, State> Cache = new Dictionary<int, State>();
		private static int CleanupTick;

		public static float RestLength(int variant)
		{
			return variant switch { 1 => 190f, 2 => 120f, 3 => 160f, 4 => 212f, _ => 107f };
		}

		public static float SpinRadians(int variant)
		{
			return MathHelper.ToRadians(variant switch { 1 => 2.5f, 2 => 4f, 3 => 4.5f, 4 => 5.5f, _ => 1.5f });
		}

		public static State GetOrCreate(int key, Vector2 anchor, int variant, float dir)
		{
			if (!Cache.TryGetValue(key, out var st) || st.Variant != variant
				|| Vector2.DistanceSquared(st.Pos[0], anchor) > 16f) {
				st = new State { Variant = variant, Dir = dir };
				float a0 = (anchor.X * 0.37f + anchor.Y * 0.73f) % MathHelper.TwoPi;
				for (int i = 0; i < Points; i++) {
					Vector2 p = anchor + a0.ToRotationVector2() * (RestLength(variant) * i / (Points - 1));
					st.Pos[i] = p;
					st.Prev[i] = p;
				}
				float initial = System.Math.Max(0.25f, SpinRadians(variant) * 12f);
				for (int i = 1; i < Points; i++)
					st.Mom[i] = initial * (1f - i / (float)Points);
				Cache[key] = st;
			}
			return st;
		}

		public static void Cleanup()
		{
			if (++CleanupTick % 60 != 0)
				return;
			var dead = new List<int>();
			foreach (var kv in Cache) {
				if (kv.Key < 0 || kv.Key >= Main.maxProjectiles)
					continue;
				Projectile pr = Main.projectile[kv.Key];
				if (pr == null || !pr.active || pr.type != ModContent.ProjectileType<SnareStringProj>())
					dead.Add(kv.Key);
			}
			foreach (int d in dead)
				Cache.Remove(d);
		}

		public static void Step(State st, Vector2 anchor)
		{
			float segLen = RestLength(st.Variant) / (Points - 1);
			float spin = SpinRadians(st.Variant) * st.Dir;
			float g = Gravity / Substeps;
			for (int s = 0; s < Substeps; s++) {
				for (int i = 1; i < Points; i++) {
					Vector2 cur = st.Pos[i];
					Vector2 vel = (cur - st.Prev[i]) * Damping;
					vel.Y += g;
					st.Prev[i] = cur;
					st.Pos[i] = LinkRope.PushOut(cur + vel);
				}
				st.Pos[0] = anchor;
				st.Prev[0] = anchor;
				ApplyCenterDrive(st, anchor, spin);
				Propagate(st, anchor);
				for (int it = 0; it < Iters; it++) {
					st.Pos[0] = anchor;
					for (int i = 0; i < Points - 1; i++) {
						Vector2 first = st.Pos[i];
						Vector2 second = st.Pos[i + 1];
						Vector2 diff = second - first;
						float d = diff.Length();
						if (d < 0.001f)
							continue;
						float err = d - segLen;
						Vector2 corr = diff / d * err;
						if (i == 0)
							st.Pos[i + 1] -= corr;
						else {
							st.Pos[i] += corr * 0.5f;
							st.Pos[i + 1] -= corr * 0.5f;
						}
					}
				}
				for (int i = 1; i < Points; i++)
					st.Pos[i] = LinkRope.PushOut(st.Pos[i]);
				st.Pos[0] = anchor;
			}
		}

		private static void ApplyCenterDrive(State st, Vector2 anchor, float spin)
		{
			int count = System.Math.Min(5, Points - 1);
			float desired = System.Math.Max(MinimumCenterSpeed, spin * RestLength(st.Variant) * 0.50f);
			desired = System.Math.Min(desired, MaximumCenterVelocity);
			for (int i = 1; i <= count; i++) {
				float influence = 1f - ((i - 1f) / System.Math.Max(1f, count));
				influence = MathHelper.Lerp(0.55f, 1f, influence);
				st.Mom[i] *= RotationRetention;
				st.Mom[i] += CenterTorque * influence;
				Vector2 radial = (st.Pos[i] - anchor);
				float rl = radial.Length();
				Vector2 tangent = rl < 0.001f ? new Vector2(0f, st.Dir) : new Vector2(-radial.Y / rl, radial.X / rl) * st.Dir;
				Vector2 vel = st.Pos[i] - st.Prev[i];
				float tangSpeed = Vector2.Dot(vel, tangent);
				vel += tangent * (desired * influence - tangSpeed) * CenterSpeedCorrection;
				st.Prev[i] = st.Pos[i] - vel;
				st.Mom[i] = MathHelper.Clamp(st.Mom[i], -MaximumRotationalMomentum, MaximumRotationalMomentum);
			}
		}

		private static void Propagate(State st, Vector2 anchor)
		{
			for (int i = 1; i < Points; i++) {
				Vector2 cur = st.Pos[i];
				Vector2 vel = cur - st.Prev[i];
				Vector2 pvel = st.Pos[i - 1] - st.Prev[i - 1];
				Vector2 radial = cur - anchor;
				float rl = radial.Length();
				Vector2 tangent = rl < 0.001f ? new Vector2(0f, st.Dir) : new Vector2(-radial.Y / rl, radial.X / rl) * st.Dir;
				float pTang = Vector2.Dot(pvel, tangent);
				float cTang = Vector2.Dot(vel, tangent);
				float tangDiff = MathHelper.Clamp(pTang - cTang, -5f, 5f);
				float transfer = LinkRope.SolidAt(cur) ? CollisionRotationTransfer : RotationTransfer;
				float parentMom = st.Mom[i - 1];
				st.Mom[i] += (parentMom - st.Mom[i]) * transfer;
				st.Mom[i] *= RotationRetention;
				vel += tangent * (st.Mom[i] * 2.5f);
				vel += tangent * tangDiff * transfer * 0.45f;
				if (System.Math.Abs(st.Mom[i]) < MinimumRotationalMomentum)
					st.Mom[i] = parentMom * transfer;
				st.Mom[i] = MathHelper.Clamp(st.Mom[i], -MaximumRotationalMomentum, MaximumRotationalMomentum);
				float speed = vel.Length();
				if (speed > MaximumCenterVelocity)
					vel = vel / speed * MaximumCenterVelocity;
				st.Prev[i] = cur - vel;
			}
		}
	}
}
