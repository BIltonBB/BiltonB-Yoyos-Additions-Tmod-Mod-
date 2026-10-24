using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Station networking (#12, expanded): modded yoyo stations
	// (Resonator, Ward, Rift Anchor) each get their own buff while
	// within range of an engine running the station mesh. Range grows
	// with the branch: 5 tiles per owned mesh tech, up to 30.
	//
	// Expansion: buff strength tiers with mesh level (5-10:+1, 15-20:+2,
	// 25-30:+3); linked engine pairs merge into one connected radius
	// (the stronger hub); Overdrive Grid doubles output while the engine
	// holds charge; same-mesh station pairs unlock combos (each with its
	// own buff icon + description); full 30-range meshes unlock per-
	// station capstones (Superposition, Sanctuary, Deep Snap).
	// Coverage is computed live from tiles, so toggling the branch or
	// breaking blocks takes effect immediately with no stale state.
	public static class StationNetwork
	{
		public const int RangePerTech = 5;
		public const int MaxRange = 30;
		// Aegis Mesh (defensive-only): 15 tiles per tech, 90 max — triple the
		// Local Mesh reach, but only Counterweight Sentry / String Link /
		// Yoyo Tripwire can use it. Same upgrade shape, different ranges.
		public const int AegisPerTech = 15;
		public const int AegisMax = 90;

		public static int MeshRangeOf(ResearchData d)
		{
			if (d == null)
				return 0;
			int n = 0;
			if (ResearchTech.TechOn(d, ResearchTech.StNet1))
				n++;
			if (ResearchTech.TechOn(d, ResearchTech.StNet2))
				n++;
			if (ResearchTech.TechOn(d, ResearchTech.StNet3))
				n++;
			if (ResearchTech.TechOn(d, ResearchTech.StNet4))
				n++;
			if (ResearchTech.TechOn(d, ResearchTech.StNet5))
				n++;
			if (ResearchTech.TechOn(d, ResearchTech.StNet6))
				n++;
			return System.Math.Min(n * RangePerTech, MaxRange);
		}

		public static int AegisRangeOf(ResearchData d)
		{
			if (d == null)
				return 0;
			int n = 0;
			if (ResearchTech.TechOn(d, ResearchTech.Aegis1))
				n++;
			if (ResearchTech.TechOn(d, ResearchTech.Aegis2))
				n++;
			if (ResearchTech.TechOn(d, ResearchTech.Aegis3))
				n++;
			if (ResearchTech.TechOn(d, ResearchTech.Aegis4))
				n++;
			if (ResearchTech.TechOn(d, ResearchTech.Aegis5))
				n++;
			if (ResearchTech.TechOn(d, ResearchTech.Aegis6))
				n++;
			return System.Math.Min(n * AegisPerTech, AegisMax);
		}

		// Merged radius (one connected network): a linked pair projects
		// a single shared radius — the stronger of the two hubs.
		public static int CoverRange(Point tile)
		{
			if (!ResearchEngineSystem.Tiles.TryGetValue(tile, out var d))
				return 0;
			int range = MeshRangeOf(d);
			if (d.Linked) {
				Point p = new Point(d.LinkX, d.LinkY);
				if (!(p == tile) && ResearchEngineSystem.IsTileStillEngine(p)
					&& ResearchEngineSystem.Tiles.TryGetValue(p, out var pd))
					range = System.Math.Max(range, MeshRangeOf(pd));
			}
			return range;
		}

		// Aegis merged radius (defensive only, same linked-pair rule).
		public static int AegisCoverRange(Point tile)
		{
			if (!ResearchEngineSystem.Tiles.TryGetValue(tile, out var d))
				return 0;
			int range = AegisRangeOf(d);
			if (d.Linked) {
				Point p = new Point(d.LinkX, d.LinkY);
				if (!(p == tile) && ResearchEngineSystem.IsTileStillEngine(p)
					&& ResearchEngineSystem.Tiles.TryGetValue(p, out var pd))
					range = System.Math.Max(range, AegisRangeOf(pd));
			}
			return range;
		}

		private static bool InCoverRange(Point engine, Point station)
		{
			int range = CoverRange(engine);
			if (range <= 0)
				return false;
			float dx = station.X - engine.X;
			float dy = station.Y - engine.Y;
			return dx * dx + dy * dy <= range * range;
		}

		// True when any live engine's mesh reaches the station origin.
		public static bool Covers(Point station)
		{
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				if (InCoverRange(kv.Key, station))
					return true;
			}
			return false;
		}

		private static bool InAegisRange(Point engine, Point station)
		{
			int range = AegisCoverRange(engine);
			if (range <= 0)
				return false;
			float dx = station.X - engine.X;
			float dy = station.Y - engine.Y;
			return dx * dx + dy * dy <= range * range;
		}

		// True when any live engine's AEGIS mesh reaches the defensive trap.
		public static bool AegisCovers(Point station)
		{
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				if (InAegisRange(kv.Key, station))
					return true;
			}
			return false;
		}

		// Far Aegis: a covering Aegis engine owns the triple-range node.
		public static bool AegisFarCovers(Point station)
		{
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				if (!InAegisRange(kv.Key, station))
					continue;
				if (ResearchTech.TechOn(kv.Value, ResearchTech.AegisFar))
					return true;
				if (kv.Value.Linked) {
					Point p = new Point(kv.Value.LinkX, kv.Value.LinkY);
					if (ResearchEngineSystem.Tiles.TryGetValue(p, out var pd)
						&& ResearchEngineSystem.IsTileStillEngine(p)
						&& ResearchTech.TechOn(pd, ResearchTech.AegisFar)
						&& InAegisRange(p, station))
						return true;
				}
			}
			return false;
		}

		// Buff tier from a mesh range: 5-10:+1, 15-20:+2, 25-30:+3.
		public static int TierForRange(int range)
		{
			if (range >= 25)
				return 3;
			if (range >= 15)
				return 2;
			if (range >= 5)
				return 1;
			return 0;
		}

		// Best tier across covering engines.
		public static int BestTier(Point station)
		{
			int best = 0;
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				int range = CoverRange(kv.Key);
				if (range <= 0)
					continue;
				float dx = station.X - kv.Key.X;
				float dy = station.Y - kv.Key.Y;
				if (dx * dx + dy * dy > range * range)
					continue;
				int tier = TierForRange(range);
				if (tier > best)
					best = tier;
			}
			return best;
		}

		// Overdrive fuel: some covering engine toggled (with the node)
		// with charge held.
		public static bool Fueled(Point station)
		{
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				int range = CoverRange(kv.Key);
				if (range <= 0)
					continue;
				float dx = station.X - kv.Key.X;
				float dy = station.Y - kv.Key.Y;
				if (dx * dx + dy * dy > range * range)
					continue;
				if (kv.Value.OverdriveOn && kv.Value.Charge > 0 && ResearchTech.TechOn(kv.Value, ResearchTech.StNet7))
					return true;
			}
			return false;
		}

		// Aegis tier uses the same 5/15/25 bands so Local knowledge transfers,
		// but ranges run to 90 (triple). Bands: 15-30:+1, 45-60:+2, 75-90:+3.
		public static int AegisTierForRange(int range)
		{
			if (range >= 75)
				return 3;
			if (range >= 45)
				return 2;
			if (range >= 15)
				return 1;
			return 0;
		}

		public static int AegisBestTier(Point station)
		{
			int best = 0;
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				int range = AegisCoverRange(kv.Key);
				if (range <= 0)
					continue;
				float dx = station.X - kv.Key.X;
				float dy = station.Y - kv.Key.Y;
				if (dx * dx + dy * dy > range * range)
					continue;
				int tier = AegisTierForRange(range);
				if (tier > best)
					best = tier;
			}
			return best;
		}

		// Aegis overdrive fuel (Aegis7 node + toggled draw + held charge).
		public static bool AegisFueled(Point station)
		{
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				int range = AegisCoverRange(kv.Key);
				if (range <= 0)
					continue;
				float dx = station.X - kv.Key.X;
				float dy = station.Y - kv.Key.Y;
				if (dx * dx + dy * dy > range * range)
					continue;
				if (kv.Value.OverdriveOn && kv.Value.Charge > 0 && ResearchTech.TechOn(kv.Value, ResearchTech.Aegis7))
					return true;
			}
			return false;
		}

		public static int AegisEffTier(Point station)
		{
			int t = AegisBestTier(station);
			if (t <= 0)
				return 0;
			return AegisFueled(station) ? System.Math.Min(3, t * 2) : t;
		}

		// Effective tier: best, doubled (cap 3) while fueled.
		public static int EffTier(Point station)
		{
			int t = BestTier(station);
			if (t <= 0)
				return 0;
			return Fueled(station) ? System.Math.Min(3, t * 2) : t;
		}

		public static int ResonatorBonus(Point resoTile) => EffTier(resoTile);

		public static int WardBonus(Point wardTile) => EffTier(wardTile);

		public static float RiftCooldownMult(Point anchorTile)
		{
			int t = EffTier(anchorTile);
			if (t <= 0)
				return 1f;
			return System.Math.Max(0.5f, 1f - 0.05f * t - (Fueled(anchorTile) ? 0.1f : 0f));
		}

		// Full-mesh capstone check: covered by a 30-range mesh.
		public static bool IsR30(Point station)
		{
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				int range = CoverRange(kv.Key);
				if (range < MaxRange)
					continue;
				float dx = station.X - kv.Key.X;
				float dy = station.Y - kv.Key.Y;
				if (dx * dx + dy * dy <= range * range)
					return true;
			}
			return false;
		}

		// Same-mesh pair check (combos): one mesh covers both stations.
		public static bool SharesMesh(Point a, Point b)
		{
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				int range = CoverRange(kv.Key);
				if (range <= 0)
					continue;
				float dax = a.X - kv.Key.X;
				float day = a.Y - kv.Key.Y;
				if (dax * dax + day * day > range * range)
					continue;
				float dbx = b.X - kv.Key.X;
				float dby = b.Y - kv.Key.Y;
				if (dbx * dbx + dby * dby <= range * range)
					return true;
			}
			return false;
		}

		// Same-AEGIS check (defensive power sharing): one Aegis mesh covers
		// both trap tiles.
		public static bool SharesAegis(Point a, Point b)
		{
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				int range = AegisCoverRange(kv.Key);
				if (range <= 0)
					continue;
				float dax = a.X - kv.Key.X;
				float day = a.Y - kv.Key.Y;
				if (dax * dax + day * day > range * range)
					continue;
				float dbx = b.X - kv.Key.X;
				float dby = b.Y - kv.Key.Y;
				if (dbx * dbx + dby * dby <= range * range)
					return true;
			}
			return false;
		}

		// First live ward sharing a mesh with the station (combos).
		public static bool MeshHasWard(Point station)
		{
			int wardType = ModContent.TileType<Tiles.Placeables.YoyoWardTile>();
			foreach (var kv in YoyoWardSystem.Tiles) {
				if (!YoyoWardSystem.IsTileStillWard(kv.Key))
					continue;
				Tile t = Framing.GetTileSafely(kv.Key.X, kv.Key.Y);
				if (!t.HasTile || t.TileType != wardType)
					continue;
				if (SharesMesh(station, kv.Key))
					return true;
			}
			return false;
		}

		// First live resonator sharing a mesh with the station (combos).
		public static bool TryMeshResonator(Point station, out Point resoTile, out ResonatorData data)
		{
			resoTile = default;
			data = null;
			int resType = ModContent.TileType<Tiles.Placeables.YoyoResonatorTile>();
			foreach (var kv in ResonatorSystem.Tiles) {
				if (!ResonatorSystem.IsTileStillResonator(kv.Key))
					continue;
				Tile t = Framing.GetTileSafely(kv.Key.X, kv.Key.Y);
				if (!t.HasTile || t.TileType != resType)
					continue;
				if (SharesMesh(station, kv.Key)) {
					resoTile = kv.Key;
					data = kv.Value;
					return true;
				}
			}
			return false;
		}

		// Overdrive draw toggle (StNet7 Overdrive Grid / Aegis7 Aegis Overdrive):
		// while ON and the engine holds charge, covered stations burn it for
		// doubled mesh output. Free toggle; the drain is the cost.
		public static void TryMeshToggle(Player player, Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			bool hasLocal = ResearchTech.TechOn(data, ResearchTech.StNet7);
			bool hasAegis = ResearchTech.TechOn(data, ResearchTech.Aegis7);
			if (!hasLocal && !hasAegis) {
				ResearchEngineUISystem.SetMessage("Research Overdrive Grid or Aegis Overdrive first (Tech tab).");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			ApplyMeshToggle(tile);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchMeshToggle(tile);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, player.Center);
			ResearchEngineUISystem.SetMessage($"Overdrive draw: {(data.OverdriveOn ? "ON (drains charge while held)" : "OFF")}.");
		}

		public static void ApplyMeshToggle(Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (!ResearchTech.TechOn(data, ResearchTech.StNet7) && !ResearchTech.TechOn(data, ResearchTech.Aegis7))
				return;
			data.OverdriveOn = !data.OverdriveOn;
			ResearchEngineSystem.SyncWorld();
		}

		// Modifier gate (#17): EXTENDED tunings/calibrations and True
		// Masteries need a networked Research Engine (Station Networking
		// LV1+: Local Mesh) covering the customization station in use.
		public static bool ModifierNetworkOk(out string why)
		{
			why = "";
			Point station = global::BiltonsYoyoMod.Content.Customization.YoyoStationUISystem.OpenTile;
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				if (InCoverRange(kv.Key, station))
					return true;
			}
			why = "Needs a networked Research Engine in range (Station Networking LV1+: Local Mesh).";
			return false;
		}

		// Display counts (buffed stations by kind) + live combos + full
		// meshes. Refreshed in the world tick; read by the Network tab.
		public static int BuffedResonators;
		public static int BuffedWards;
		public static int BuffedRifts;
		public static bool ComboResWard; // Resonator + Ward share a mesh
		public static bool ComboWardRift; // Ward + Rift share a mesh
		public static bool ComboResRift; // Resonator + Rift share a mesh
		public static int FullMeshes; // engines projecting range 30

		public static void RefreshCoverage()
		{
			int r = 0;
			int resType = ModContent.TileType<Tiles.Placeables.YoyoResonatorTile>();
			foreach (var kv in ResonatorSystem.Tiles) {
				if (!ResonatorSystem.IsTileStillResonator(kv.Key))
					continue;
				Tile t = Framing.GetTileSafely(kv.Key.X, kv.Key.Y);
				if (!t.HasTile || t.TileType != resType)
					continue;
				if (Covers(kv.Key))
					r++;
			}
			int w = 0;
			int wardType = ModContent.TileType<Tiles.Placeables.YoyoWardTile>();
			foreach (var kv in YoyoWardSystem.Tiles) {
				if (!YoyoWardSystem.IsTileStillWard(kv.Key))
					continue;
				Tile t = Framing.GetTileSafely(kv.Key.X, kv.Key.Y);
				if (!t.HasTile || t.TileType != wardType)
					continue;
				if (Covers(kv.Key))
					w++;
			}
			// Rift anchors keep no world dict: scan around meshed
			// engines, dedupe by multi-tile origin (2 wide x 3 tall).
			int f = 0;
			int riftType = ModContent.TileType<Tiles.Placeables.RiftAnchorTile>();
			var seen = new HashSet<Point>();
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				int range = CoverRange(kv.Key);
				if (range <= 0)
					continue;
				for (int tx = kv.Key.X - range; tx <= kv.Key.X + range; tx++) {
					for (int ty = kv.Key.Y - range; ty <= kv.Key.Y + range; ty++) {
						if (!WorldGen.InWorld(tx, ty))
							continue;
						float dx = tx - kv.Key.X;
						float dy = ty - kv.Key.Y;
						if (dx * dx + dy * dy > range * range)
							continue;
						Tile t = Framing.GetTileSafely(tx, ty);
						if (!t.HasTile || t.TileType != riftType)
							continue;
						Point origin = new Point(tx - t.TileFrameX / 18, ty - t.TileFrameY / 18);
						if (!WorldGen.InWorld(origin.X, origin.Y))
							continue;
						if (seen.Add(origin))
							f++;
					}
				}
			}
			BuffedResonators = r;
			BuffedWards = w;
			BuffedRifts = f;
			// Combos + full meshes: per meshed engine, which station
			// kinds share its radius.
			ComboResWard = false;
			ComboWardRift = false;
			ComboResRift = false;
			FullMeshes = 0;
			int resType2 = ModContent.TileType<Tiles.Placeables.YoyoResonatorTile>();
			int wardType2 = ModContent.TileType<Tiles.Placeables.YoyoWardTile>();
			int riftType2 = ModContent.TileType<Tiles.Placeables.RiftAnchorTile>();
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				int range = CoverRange(kv.Key);
				if (range <= 0)
					continue;
				if (range >= MaxRange)
					FullMeshes++;
				bool hasRes = false;
				bool hasWard = false;
				bool hasRift = false;
				foreach (var skv in ResonatorSystem.Tiles) {
					if (!ResonatorSystem.IsTileStillResonator(skv.Key))
						continue;
					Tile st = Framing.GetTileSafely(skv.Key.X, skv.Key.Y);
					if (!st.HasTile || st.TileType != resType2)
						continue;
					float dx = skv.Key.X - kv.Key.X;
					float dy = skv.Key.Y - kv.Key.Y;
					if (dx * dx + dy * dy <= range * range) {
						hasRes = true;
						break;
					}
				}
				foreach (var skv in YoyoWardSystem.Tiles) {
					if (!YoyoWardSystem.IsTileStillWard(skv.Key))
						continue;
					Tile st = Framing.GetTileSafely(skv.Key.X, skv.Key.Y);
					if (!st.HasTile || st.TileType != wardType2)
						continue;
					float dx = skv.Key.X - kv.Key.X;
					float dy = skv.Key.Y - kv.Key.Y;
					if (dx * dx + dy * dy <= range * range) {
						hasWard = true;
						break;
					}
				}
				for (int tx = kv.Key.X - range; tx <= kv.Key.X + range && !hasRift; tx++) {
					for (int ty = kv.Key.Y - range; ty <= kv.Key.Y + range; ty++) {
						if (!WorldGen.InWorld(tx, ty))
							continue;
						float dx = tx - kv.Key.X;
						float dy = ty - kv.Key.Y;
						if (dx * dx + dy * dy > range * range)
							continue;
						Tile t = Framing.GetTileSafely(tx, ty);
						if (t.HasTile && t.TileType == riftType2) {
							hasRift = true;
							break;
						}
					}
				}
				if (hasRes && hasWard)
					ComboResWard = true;
				if (hasWard && hasRift)
					ComboWardRift = true;
				if (hasRes && hasRift)
					ComboResRift = true;
			}
		}
	}

	// Mesh pulse (#12 expansion pick 7): every 60s each meshed station
	// emits its kind's pulse. State changes run server/single-side;
	// visuals run everywhere. Combos/capstones ride along.
	public class StationPulseSystem : ModSystem
	{
		public const int PulseEveryTicks = 3600; // 60s

		public override void PostUpdateEverything()
		{
			if (Main.GameUpdateCount % PulseEveryTicks != 0)
				return;
			bool sim = Main.netMode != NetmodeID.MultiplayerClient;
			PulseResonators(sim);
			PulseWards(sim);
			PulseRifts(sim);
		}

		private static void PulseResonators(bool sim)
		{
			int resType = ModContent.TileType<Tiles.Placeables.YoyoResonatorTile>();
			foreach (var kv in ResonatorSystem.Tiles) {
				if (!ResonatorSystem.IsTileStillResonator(kv.Key))
					continue;
				Tile t = Framing.GetTileSafely(kv.Key.X, kv.Key.Y);
				if (!t.HasTile || t.TileType != resType)
					continue;
				if (StationNetwork.BestTier(kv.Key) <= 0)
					continue; // only meshed stations pulse
				Vector2 center = ResonatorSystem.ResonatorCenter(kv.Key);
				float radius = ResonatorSystem.EffRadiusPx(kv.Value);
				if (!Main.dedServ) {
					PlaceableUI.DustRing(center, radius, DustID.PinkTorch, PlaceableUI.RingCount(radius), 1.3f);
					Lighting.AddLight(center, 0.5f, 0.2f, 0.4f);
				}
				if (!sim)
					continue;
				float pct = ResonatorSystem.EffWeakenPct(kv.Value, StationNetwork.ResonatorBonus(kv.Key));
				int durBonus = StationNetwork.ResonatorBonus(kv.Key);
				int ticks = ResonatorSystem.WeakenTicks(System.Math.Min(kv.Value.Duration + durBonus, ResonatorSystem.MaxDuration));
				bool r30 = StationNetwork.IsR30(kv.Key);
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC npc = Main.npc[i];
					if (npc == null || !npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5 || npc.life <= 0)
						continue;
					if (Vector2.DistanceSquared(center, npc.Center) > radius * radius)
						continue;
					var g = npc.GetGlobalNPC<ResonatorGlobalNPC>();
					g.ApplyWeaken(pct, ticks);
					if (r30)
						npc.AddBuff(BuffID.Slow, ticks);
					npc.netUpdate = true;
				}
			}
		}

		private static void PulseWards(bool sim)
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc == null || !npc.active)
					continue;
				if (!YoyoWardSystem.IsEligible(npc))
					continue;
				if (!YoyoWardSystem.TryGetCoveringWard(npc.Center, out var wtile, out _))
					continue;
				if (StationNetwork.WardBonus(wtile) <= 0)
					continue; // only meshed wards pulse
				if (!Main.dedServ) {
					Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Grass, 0f, -1f, 120, default, 1f);
					d.noGravity = true;
				}
				if (!sim)
					continue;
				npc.life = System.Math.Clamp(npc.life + System.Math.Max(1, npc.lifeMax / 10), 1, npc.lifeMax);
				npc.netUpdate = true;
			}
		}

		private static void PulseRifts(bool sim)
		{
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (player == null || !player.active)
					continue;
				var rp = player.GetModPlayer<RiftAnchorPlayer>();
				if (rp == null || !rp.HasActive)
					continue;
				if (!RiftAnchorHelpers.IsValidAnchor(rp.ActiveTile))
					continue;
				if (StationNetwork.BestTier(rp.ActiveTile) <= 0)
					continue; // only meshed anchors pulse
				if (!Main.dedServ) {
					Vector2 c = RiftAnchorHelpers.AnchorCenter(rp.ActiveTile);
					for (int k = 0; k < 6; k++) {
						Dust d = Dust.NewDustDirect(c - new Vector2(4, 4), 8, 8, DustID.PurpleTorch, 0f, -1.5f, 120, default, 1f);
						d.noGravity = true;
					}
					Lighting.AddLight(c, 0.35f, 0.15f, 0.55f);
				}
				if (!sim)
					continue;
				if (rp.SnapCooldown > 0) {
					rp.SnapCooldown = System.Math.Max(0, rp.SnapCooldown - 300);
					if (Main.netMode == NetmodeID.Server)
						rp.SyncPlayer(-1, i, false);
				}
			}
		}
	}
}
