using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Research networking (#6): link up to 2 engines so they share all
	// research, plus nearby yoyo stations (Resonator/Ward/Rift Anchor)
	// slightly improve this engine in their own ways.
	//
	// Contract (kept simple so it cannot diverge):
	// - Linked engines share TECH only: linking unions both tech lists,
	//   and every later unlock mirrors to the live partner. Jobs, queue,
	//   points, charge and config stay per-engine.
	// - Destroying one engine never takes shared research away: the
	//   survivor already holds everything. With Failover (either side),
	//   the survivor additionally inherits the lost points + queue.
	// - Nearby stations (25 tiles, live-tile verified): Resonator +5%
	//   research speed each, Ward +1 charge per hit each, Rift +5%
	//   release power each (all capped at 2 stations, doubled by
	//   Station Synergy). Counts refresh in the world tick; effects
	//   read the cached counts so yoyo hits never scan tiles.
	// - Linking is free (no payment, no dupe surface). Linking needs
	//   Uplink on the initiating engine.
	public static class ResearchNetwork
	{
		public const int StationRadius = 25; // tiles (#12 makes ranges upgradeable later)
		public const int BaseLinkRange = 150; // tiles
		public const int FarLinkRange = 300; // tiles with Long Range Uplink

		public static int LinkRangeOf(ResearchData d)
		{
			return ResearchTech.TechOn(d, ResearchTech.NetFar) ? FarLinkRange : BaseLinkRange;
		}

		public static bool IsLinked(ResearchData d) => d != null && d.Linked;

		public static Point PartnerOf(ResearchData d) => d == null ? Point.Zero : new Point(d.LinkX, d.LinkY);

		public static bool PartnerLive(Point tile, ResearchData d)
		{
			if (!IsLinked(d))
				return false;
			Point p = PartnerOf(d);
			if (p == tile)
				return false;
			return ResearchEngineSystem.IsTileStillEngine(p);
		}

		// +10% research speed while linked with Resonant Study.
		public static bool LinkBonusActive(Point tile, ResearchData d)
		{
			return PartnerLive(tile, d) && ResearchTech.TechOn(d, ResearchTech.NetRes);
		}

		private static bool SynergyOn(ResearchData d) => ResearchTech.TechOn(d, ResearchTech.NetSyn);

		// Station effects from cached counts (capped at 2 stations).
		public static float StationRateBonus(ResearchData d)
		{
			int n = System.Math.Min(d?.NbRes ?? 0, 2);
			if (n <= 0)
				return 0f;
			return 0.05f * n * (SynergyOn(d) ? 2 : 1);
		}

		public static int ChargeBonus(ResearchData d)
		{
			int n = System.Math.Min(d?.NbWard ?? 0, 2);
			if (n <= 0)
				return 0;
			return n * (SynergyOn(d) ? 2 : 1);
		}

		public static float ReleasePowerMult(ResearchData d)
		{
			int n = System.Math.Min(d?.NbRift ?? 0, 2);
			if (n <= 0)
				return 1f;
			return 1f + 0.05f * n * (SynergyOn(d) ? 2 : 1);
		}

		private static bool IsStationTile(int tx, int ty, int type)
		{
			if (!WorldGen.InWorld(tx, ty))
				return false;
			Tile t = Framing.GetTileSafely(tx, ty);
			return t.HasTile && t.TileType == type;
		}

		// Recounts nearby stations into the cache. Server/single side
		// only; clients converge through the WorldData rebroadcast.
		public static void RefreshStations(Point tile, ResearchData d)
		{
			if (d == null || !ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			int resType = ModContent.TileType<Tiles.Placeables.YoyoResonatorTile>();
			int wardType = ModContent.TileType<Tiles.Placeables.YoyoWardTile>();
			int riftType = ModContent.TileType<Tiles.Placeables.RiftAnchorTile>();
			int r2 = StationRadius * StationRadius;
			int r = 0;
			foreach (var kv in ResonatorSystem.Tiles) {
				float dx = kv.Key.X - tile.X;
				float dy = kv.Key.Y - tile.Y;
				if (dx * dx + dy * dy > r2)
					continue;
				if (!IsStationTile(kv.Key.X, kv.Key.Y, resType))
					continue;
				r++;
			}
			int w = 0;
			foreach (var kv in YoyoWardSystem.Tiles) {
				float dx = kv.Key.X - tile.X;
				float dy = kv.Key.Y - tile.Y;
				if (dx * dx + dy * dy > r2)
					continue;
				if (!IsStationTile(kv.Key.X, kv.Key.Y, wardType))
					continue;
				w++;
			}
			// Rift anchors keep no world dict: scan tiles, dedupe by
			// multi-tile origin (2 wide x 3 tall).
			int f = 0;
			var seen = new HashSet<Point>();
			for (int tx = tile.X - StationRadius; tx <= tile.X + StationRadius; tx++) {
				for (int ty = tile.Y - StationRadius; ty <= tile.Y + StationRadius; ty++) {
					if (!WorldGen.InWorld(tx, ty))
						continue;
					Tile t = Framing.GetTileSafely(tx, ty);
					if (!t.HasTile || t.TileType != riftType)
						continue;
					Point origin = new Point(tx - t.TileFrameX / 18, ty - t.TileFrameY / 18);
					if (!WorldGen.InWorld(origin.X, origin.Y))
						continue;
					float dx = origin.X - tile.X;
					float dy = origin.Y - tile.Y;
					if (dx * dx + dy * dy > r2)
						continue;
					if (seen.Add(origin))
						f++;
				}
			}
			if (d.NbRes != r || d.NbWard != w || d.NbRift != f) {
				d.NbRes = r;
				d.NbWard = w;
				d.NbRift = f;
				ResearchEngineSystem.SyncWorld();
			}
		}

		// Union share: both tiles end up holding every tech either had.
		// New entries default ON (toggles stay per-block by design).
		private static void UnionTech(ResearchData da, ResearchData db)
		{
			var union = new List<string>();
			foreach (string id in da.Tech ?? new List<string>()) {
				if (ResearchTech.IsNode(id) && !union.Contains(id))
					union.Add(id);
			}
			foreach (string id in db.Tech ?? new List<string>()) {
				if (ResearchTech.IsNode(id) && !union.Contains(id))
					union.Add(id);
			}
			da.Tech = new List<string>(union);
			db.Tech = new List<string>(union);
		}

		// Mirrors one unlock to the live partner (called after Unlock).
		public static void MirrorUnlock(Point tile, ResearchData d, string id)
		{
			if (!PartnerLive(tile, d) || string.IsNullOrEmpty(id) || !ResearchTech.IsNode(id))
				return;
			var partner = ResearchEngineSystem.GetOrCreate(PartnerOf(d));
			if (!partner.HasTech(id)) {
				partner.Tech ??= new List<string>();
				partner.Tech.Add(id);
				ResearchEngineSystem.SyncWorld();
			}
		}

		public static void TryLink(Player player, Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (!ResearchTech.TechOn(data, ResearchTech.NetUp)) {
				ResearchEngineUISystem.SetMessage("Research Uplink first (Tech tab).");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			if (IsLinked(data)) {
				ResearchEngineUISystem.SetMessage("Already linked. Unlink first.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			int range = LinkRangeOf(data);
			Point best = default;
			float bestD2 = float.MaxValue;
			bool found = false;
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (kv.Key == tile)
					continue;
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				if (IsLinked(kv.Value))
					continue; // pairs only: up to 2 engines per network
				float dx = kv.Key.X - tile.X;
				float dy = kv.Key.Y - tile.Y;
				float d2 = dx * dx + dy * dy;
				if (d2 > range * range)
					continue;
				if (!found || d2 < bestD2) {
					best = kv.Key;
					bestD2 = d2;
					found = true;
				}
			}
			if (!found) {
				ResearchEngineUISystem.SetMessage($"No free engine within {range} tiles.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			ApplyLink(tile, best);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchLink(tile, best);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, player.Center);
			ResearchEngineUISystem.SetMessage($"Linked with engine ({best.X}, {best.Y}). Tech shared.");
		}

		// Authoritative link (free op: no payment, no dupe surface).
		public static void ApplyLink(Point a, Point b)
		{
			if (a == b)
				return;
			if (!ResearchEngineSystem.IsTileStillEngine(a) || !ResearchEngineSystem.IsTileStillEngine(b))
				return;
			var da = ResearchEngineSystem.GetOrCreate(a);
			var db = ResearchEngineSystem.GetOrCreate(b);
			if (!ResearchTech.TechOn(da, ResearchTech.NetUp))
				return;
			if (da.Linked || db.Linked) {
				// Re-union of the existing pair is idempotent; anything
				// else refuses (pairs only).
				if (!(da.Linked && db.Linked && PartnerOf(da) == b && PartnerOf(db) == a))
					return;
			}
			float dx = b.X - a.X;
			float dy = b.Y - a.Y;
			if (dx * dx + dy * dy > LinkRangeOf(da) * LinkRangeOf(da))
				return;
			UnionTech(da, db);
			da.Linked = true;
			da.LinkX = b.X;
			da.LinkY = b.Y;
			db.Linked = true;
			db.LinkX = a.X;
			db.LinkY = a.Y;
			ResearchEngineSystem.SyncWorld();
		}

		public static void Unlink(Player player, Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (!IsLinked(data)) {
				ResearchEngineUISystem.SetMessage("Not linked.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			ApplyUnlink(tile);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchUnlink(tile);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, player.Center);
			ResearchEngineUISystem.SetMessage("Unlinked. Each engine keeps its research.");
		}

		// Authoritative unlink: clears both sides of a live pair.
		public static void ApplyUnlink(Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			Point p = PartnerOf(data);
			data.Linked = false;
			if (ResearchEngineSystem.IsTileStillEngine(p)) {
				var dp = ResearchEngineSystem.GetOrCreate(p);
				if (dp.Linked && dp.LinkX == tile.X && dp.LinkY == tile.Y)
					dp.Linked = false;
			}
			ResearchEngineSystem.SyncWorld();
		}

		// Destroy hook: call BEFORE the tile entry is forgotten. Shared
		// tech already lives on the survivor; with Failover (either side)
		// the survivor additionally inherits points + queue.
		public static void OnDestroyed(Point origin)
		{
			if (!ResearchEngineSystem.Tiles.TryGetValue(origin, out var d))
				return;
			if (!IsLinked(d))
				return;
			Point p = PartnerOf(d);
			if (!ResearchEngineSystem.IsTileStillEngine(p))
				return;
			var sv = ResearchEngineSystem.GetOrCreate(p);
			if (ResearchTech.TechOn(d, ResearchTech.NetFail) || ResearchTech.TechOn(sv, ResearchTech.NetFail)) {
				int room = System.Math.Max(0, ResearchPoints.CapOf(sv) - sv.Points);
				int moved = System.Math.Min(room, System.Math.Max(0, d.Points));
				if (moved > 0) {
					sv.Points += moved;
					sv.LifetimePoints = System.Math.Max(0, sv.LifetimePoints + moved);
				}
				sv.Queue ??= new List<string>();
				foreach (string qid in d.Queue ?? new List<string>()) {
					if (sv.Queue.Count >= 6)
						break;
					if (ResearchTech.ById(qid) == null || sv.HasTech(qid) || ResearchTech.FindJob(sv, qid) != null || sv.Queue.Contains(qid))
						continue;
					sv.Queue.Add(qid);
				}
			}
			sv.Linked = false;
			ResearchEngineSystem.SyncWorld();
		}
	}
}
