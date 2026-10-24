using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Traps
{
	// Server-authoritative trap mutations (upgrades, config, pairing, wiring).
	// UI calls Try* locally then sends net packet; server applies via Apply*.
	public static class TrapActions
	{
		public static int MaxLinkRange(TrapState s, Point tile)
		{
			int r = s.Range + s.Level * 5 + TrapSystem.AegisTier(tile) * 2;
			if (s.LongSpan)
				r = (int)(r * 1.5f);
			// Far Aegis triple: pair range triples for defensive Link only.
			if (TrapSystem.AegisCovered(tile) && TrapSystem.AegisFar(tile))
				r = System.Math.Min(90, r * 3);
			return System.Math.Min(r, 90);
		}

		// Pure validation (no mutation): safe for client-side pre-checks.
		public static bool CanPair(Point a, Point b, out string error)
		{
			error = "";
			if (a == b) { error = "Cannot self-link."; return false; }
			if (!WorldGen.InWorld(a.X, a.Y) || !WorldGen.InWorld(b.X, b.Y)) { error = "Out of world."; return false; }
			Tile ta = Framing.GetTileSafely(a.X, a.Y);
			Tile tb = Framing.GetTileSafely(b.X, b.Y);
			int linkType = ModContent.TileType<StringLinkTile>();
			if (!ta.HasTile || ta.TileType != linkType || !tb.HasTile || tb.TileType != linkType) {
				error = "Both ends must be String Link endpoints."; return false;
			}
			TrapState sa = TrapSystem.Traps.TryGetValue(a, out var xa) ? xa : TrapState.DefaultFor(TrapKind.Link);
			TrapState sb = TrapSystem.Traps.TryGetValue(b, out var xb) ? xb : TrapState.DefaultFor(TrapKind.Link);
			if (sa.Linked) { error = "First endpoint already linked. Break it first (right-click Unlink)."; return false; }
			if (sb.Linked) { error = "Second endpoint already linked."; return false; }
			float dist = Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));
			int maxR = System.Math.Max(MaxLinkRange(sa, a), MaxLinkRange(sb, b));
			if (dist > maxR) { error = $"Too far ({dist:0} > {maxR} tiles). Upgrade Link Range."; return false; }
			return true;
		}

		public static bool TryPair(Point a, Point b, out string error)
		{
			if (!CanPair(a, b, out error))
				return false;
			ApplyPair(a, b);
			return true;
		}

		public static void ApplyPair(Point a, Point b)
		{
			if (!TrapSystem.Traps.TryGetValue(a, out var sa))
				sa = TrapSystem.GetOrCreate(a, TrapKind.Link);
			if (!TrapSystem.Traps.TryGetValue(b, out var sb))
				sb = TrapSystem.GetOrCreate(b, TrapKind.Link);
			sa.LinkX = b.X; sa.LinkY = b.Y; sa.WiredActive = false;
			sb.LinkX = a.X; sb.LinkY = a.Y; sb.WiredActive = false;
			TrapSystem.SyncWorld();
		}

		public static void ApplyUnlink(Point key)
		{
			if (!TrapSystem.Traps.TryGetValue(key, out var s))
				return;
			if (!s.Linked)
				return;
			Point other = s.LinkPoint;
			s.LinkX = -9999; s.LinkY = -9999; s.WiredActive = false;
			s.Stress = 0; s.Frayed = 0;
			if (TrapSystem.Traps.TryGetValue(other, out var os)) {
				os.LinkX = -9999; os.LinkY = -9999; os.WiredActive = false;
				os.Stress = 0; os.Frayed = 0;
			}
			TrapSystem.SyncWorld();
		}

		// Mesh breaker: arm/disarm every linked pair under one engine's Aegis
		// mesh in a single throw. Snapped (frayed) ropes refuse — rewire those
		// by hand. Server-authoritative; UI sends via TrapNet.
		public static void ApplyBreaker(Point engine, bool arm)
		{
			if (!Content.Placeables.ResearchEngineSystem.IsTileStillEngine(engine))
				return;
			int range = Content.Placeables.StationNetwork.AegisCoverRange(engine);
			if (range <= 0)
				return;
			bool changed = false;
			var done = new System.Collections.Generic.HashSet<Point>();
			foreach (var kv in TrapSystem.Traps) {
				if (kv.Value.Kind != TrapKind.Link || !kv.Value.Linked)
					continue;
				Point a = kv.Key, b = kv.Value.LinkPoint;
				if (!TrapSystem.Traps.TryGetValue(b, out var os))
					continue;
				if (!done.Add(LinkRope.Canonical(a, b)))
					continue;
				float adx = a.X - engine.X, ady = a.Y - engine.Y;
				float bdx = b.X - engine.X, bdy = b.Y - engine.Y;
				if (adx * adx + ady * ady > range * range || bdx * bdx + bdy * bdy > range * range)
					continue;
				if (arm) {
					if (kv.Value.Frayed > 0 || os.Frayed > 0)
						continue;
					if (!kv.Value.WiredActive) { kv.Value.WiredActive = true; os.WiredActive = true; changed = true; }
				}
				else if (kv.Value.WiredActive) {
					kv.Value.WiredActive = false; os.WiredActive = false; changed = true;
				}
			}
			if (changed)
				TrapSystem.SyncWorld();
		}

		// Mesh surge: a FULL engine dumps its entire charge to fire every
		// covered defensive trap at once — sentries/tripwires reload to full
		// and fire next tick, link pairs arm (frayed refuse). One volley.
		public static void ApplySurge(Point engine)
		{
			if (!Content.Placeables.ResearchEngineSystem.IsTileStillEngine(engine))
				return;
			if (!Content.Placeables.ResearchEngineSystem.Tiles.TryGetValue(engine, out var edata))
				return;
			int full = Content.Placeables.ResearchEngineSystem.MaxChargeOf(edata);
			if (edata.Charge < full)
				return;
			int range = Content.Placeables.StationNetwork.AegisCoverRange(engine);
			if (range <= 0)
				return;
			edata.Charge = 0;
			bool InRange(Point t)
			{
				float dx = t.X - engine.X, dy = t.Y - engine.Y;
				return dx * dx + dy * dy <= range * range;
			}
			var armed = new System.Collections.Generic.HashSet<Point>();
			foreach (var kv in TrapSystem.Traps) {
				if (!TrapSystem.IsDefensive(kv.Value.Kind) || !kv.Value.Enabled)
					continue;
				if (!InRange(kv.Key))
					continue;
				if (kv.Value.Kind == TrapKind.Link) {
					if (!kv.Value.Linked)
						continue;
					Point other = kv.Value.LinkPoint;
					if (!TrapSystem.Traps.TryGetValue(other, out var os))
						continue;
					Point canon = LinkRope.Canonical(kv.Key, other);
					if (!armed.Add(canon))
						continue;
					float odx = other.X - engine.X, ody = other.Y - engine.Y;
					if (odx * odx + ody * ody > range * range)
						continue;
					if (kv.Value.Frayed > 0 || os.Frayed > 0)
						continue;
					kv.Value.WiredActive = true; os.WiredActive = true;
					kv.Value.Energy = kv.Value.MaxEnergy; os.Energy = os.MaxEnergy;
				}
				else {
					kv.Value.Energy = kv.Value.MaxEnergy;
					kv.Value.Cooldown = 0;
				}
			}
			Content.Placeables.ResearchEngineSystem.SyncWorld();
			TrapSystem.SyncWorld();
		}

		private static readonly System.Collections.Generic.Dictionary<Point, int> LastWireTick = new System.Collections.Generic.Dictionary<Point, int>();

		public static void ApplyLinkWire(Point hitTile)
		{
			// Wiring either endpoint toggles the pair. No auto-activation on pairing.
			if (!TrapSystem.Traps.TryGetValue(hitTile, out var s))
				return;
			if (s.Kind != TrapKind.Link)
				return;
			if (!s.Linked) {
				// Unlinked endpoint wired: brief spark, no barrier.
				return;
			}
			Point other = s.LinkPoint;
			if (!TrapSystem.Traps.TryGetValue(other, out var os))
				return;
			bool next = !s.WiredActive;
			// Frayed lockout: a snapped rope refuses to re-engage until it recovers.
			if (s.Frayed > 0 || os.Frayed > 0) {
				if (!Main.dedServ) { Vector2 fc = TrapSystem.TileCenter(hitTile); for (int f = 0; f < 4; f++) { Dust dd = Dust.NewDustDirect(fc - new Vector2(8, 8), 16, 16, DustID.Smoke, 0f, -0.5f, 100, default, 1f); dd.noGravity = true; } }
				return;
			}
			// One wire pulse often hits BOTH endpoints in the same tick (wire runs
			// under the pair): without a debounce the second HitWire would instantly
			// toggle back off, so switches appear to do nothing.
			Point canonical = LinkRope.Canonical(hitTile, other);
			int now = (int)Main.GameUpdateCount;
			if (LastWireTick.TryGetValue(canonical, out int prev) && now - prev < 30)
				return;
			LastWireTick[canonical] = now;
			if (LastWireTick.Count > 200) {
				var stale = new System.Collections.Generic.List<Point>();
				foreach (var kv in LastWireTick) {
					if (now - kv.Value > 3600)
						stale.Add(kv.Key);
				}
				foreach (var k in stale)
					LastWireTick.Remove(k);
			}

			// Prevent duplicate hitboxes: single toggle for the pair.
			s.WiredActive = next; os.WiredActive = next;
			s.ActiveTimer = next ? s.Duration : 0;
			os.ActiveTimer = next ? os.Duration : 0;
			// Wire signal consumes a little energy to start.
			if (next) {
				s.Energy = System.Math.Max(0, s.Energy - 5);
				os.Energy = System.Math.Max(0, os.Energy - 5);
			}
			if (!Main.dedServ) {
				Vector2 c = TrapSystem.TileCenter(hitTile);
				for (int i = 0; i < 8; i++)
					Dust.NewDustDirect(c - new Vector2(8, 8), 16, 16, next ? DustID.Electric : DustID.Smoke, 0f, -1f, 100, default, 1f);
			}
			Terraria.Audio.SoundEngine.PlaySound(next ? SoundID.Mech : SoundID.MenuTick, TrapSystem.TileCenter(hitTile));
			TrapSystem.SyncWorld();
		}

		// ---- Upgrades ----

		public struct UpgradeCost
		{
			public int Coins;
			public int Mat;
			public int Count;
		}

		public static UpgradeCost CostFor(TrapKind kind, int nextLevel)
		{
			int scraps = ModContent.ItemType<Items.Materials.YoyoScraps>();
			int parts = ModContent.ItemType<Items.Materials.YoyoParts>();
			int adv = ModContent.ItemType<Items.Materials.AdvancedYoyoParts>();
			int core = ModContent.ItemType<Items.Materials.GuardianCoreM>();
			return (kind, nextLevel) switch {
				(TrapKind.WildSnare, 1) => new UpgradeCost { Coins = Item.buyPrice(gold: 3), Mat = scraps, Count = 12 },
				(TrapKind.WildSnare, _) => new UpgradeCost { Coins = Item.buyPrice(gold: 10), Mat = parts, Count = 8 },
				(TrapKind.Link, 1) => new UpgradeCost { Coins = Item.buyPrice(gold: 3), Mat = scraps, Count = 10 },
				(TrapKind.Link, 2) => new UpgradeCost { Coins = Item.buyPrice(gold: 6), Mat = parts, Count = 6 },
				(TrapKind.Link, 3) => new UpgradeCost { Coins = Item.buyPrice(gold: 12), Mat = adv, Count = 4 },
				(TrapKind.Link, 4) => new UpgradeCost { Coins = Item.buyPrice(gold: 20), Mat = core, Count = 3 },
				(TrapKind.Link, 5) => new UpgradeCost { Coins = Item.buyPrice(gold: 30), Mat = adv, Count = 6 },
				(TrapKind.Link, 6) => new UpgradeCost { Coins = Item.buyPrice(gold: 50), Mat = core, Count = 4 },
				(TrapKind.Link, 7) => new UpgradeCost { Coins = Item.buyPrice(gold: 80), Mat = core, Count = 6 },
				(TrapKind.Sentry, 1) => new UpgradeCost { Coins = Item.buyPrice(gold: 5), Mat = parts, Count = 8 },
				(TrapKind.Sentry, 3) => new UpgradeCost { Coins = Item.buyPrice(gold: 25), Mat = adv, Count = 8 },
				(TrapKind.Sentry, 4) => new UpgradeCost { Coins = Item.buyPrice(gold: 40), Mat = core, Count = 4 },
				(TrapKind.Sentry, _) => new UpgradeCost { Coins = Item.buyPrice(gold: 15), Mat = adv, Count = 5 },
				(TrapKind.Tripwire, 1) => new UpgradeCost { Coins = Item.buyPrice(gold: 3), Mat = scraps, Count = 12 },
				(TrapKind.Tripwire, 2) => new UpgradeCost { Coins = Item.buyPrice(gold: 8), Mat = parts, Count = 8 },
				(TrapKind.Tripwire, 3) => new UpgradeCost { Coins = Item.buyPrice(gold: 15), Mat = adv, Count = 5 },
				(TrapKind.Tripwire, 4) => new UpgradeCost { Coins = Item.buyPrice(gold: 30), Mat = core, Count = 3 },
				(_, _) => new UpgradeCost { Coins = Item.buyPrice(gold: 12), Mat = parts, Count = 8 },
			};
		}

		public static bool UpgradeRequirementMet(Point tile, TrapState s, out string why, Player player = null)
		{
			why = "";
			if (s.Level >= s.MaxLevel) { why = "Max level."; return false; }
			int needUses = s.Level switch { 0 => 10, 1 => 25, 2 => 40, 3 => 60, 4 => 80, 5 => 110, _ => 150 };
			if (s.Uses < needUses) { why = $"Needs {needUses} field uses (has {s.Uses}). Use the trap more."; return false; }
			if (s.Level >= 1) {
				if (TrapSystem.IsDefensive(s.Kind)) {
					if (TrapSystem.AegisTier(tile) <= 0 && !TrapSystem.AegisCovered(tile)) {
						why = $"Lv{s.Level + 1} needs Aegis Mesh cover (Research Engine AEGIS branch LV1+).";
						return false;
					}
				}
				else if (TrapSystem.MeshTier(tile) <= 0 && !TrapSystem.MeshCovered(tile)) {
					why = $"Lv{s.Level + 1} needs a Research Engine mesh covering the trap (Station Networking LV1+).";
					return false;
				}
			}
			// License permits (held, never consumed): Lv3 needs Tier I, Lv4 needs Tier II.
			// License permits are checked against the acting player when known.
			if (s.Level + 1 >= 3 && player != null && !HasLicense(player, 1)) { why = "Lv3 needs a Yoyo License I held in inventory."; return false; }
			if (s.Level + 1 >= 4 && player != null && !HasLicense(player, 2)) { why = "Lv4 needs a Yoyo License II held in inventory."; return false; }
			if (s.Level + 1 >= 7 && player != null && !HasLicense(player, 3)) { why = "Lv7 needs a Yoyo License III held in inventory."; return false; }
			return true;
		}

		public static bool HasLicense(Player player, int tier)
		{
			int type = tier >= 3 ? ModContent.ItemType<Licenses.YoyoLicenseT3>() : tier == 2 ? ModContent.ItemType<Licenses.YoyoLicenseT2>() : ModContent.ItemType<Licenses.YoyoLicenseT1>();
			if (player != null)
				return Content.NPCs.Town.YoyoTownShared.CountInInventory(player, type) > 0;
			foreach (var pl in Main.player) {
				if (pl != null && pl.active && !pl.dead && Content.NPCs.Town.YoyoTownShared.CountInInventory(pl, type) > 0)
					return true;
			}
			return false;
		}

		// Licensed overcharge: a Tier III permit anywhere nearby removes backfire risk.
		public static bool LicensedNoBackfire(Point tile)
		{
			int type = ModContent.ItemType<Licenses.YoyoLicenseT3>();
			Vector2 center = TrapSystem.TileCenter(tile);
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player pl = Main.player[i];
				if (pl == null || !pl.active || pl.dead)
					continue;
				if (Vector2.DistanceSquared(pl.Center, center) > 20f * 16f * 20f * 16f)
					continue;
				if (Content.NPCs.Town.YoyoTownShared.CountInInventory(pl, type) > 0)
					return true;
			}
			return false;
		}
		public static bool TryUpgrade(Player player, Point tile)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return false;
			if (s.Level >= s.MaxLevel)
				return false;
			if (!UpgradeRequirementMet(tile, s, out _, player))
				return false;
			var cost = CostFor(s.Kind, s.Level + 1);
			if (!player.CanAfford(cost.Coins))
				return false;
			int have = Content.NPCs.Town.YoyoTownShared.CountInInventory(player, cost.Mat);
			if (have < cost.Count)
				return false;
			player.PayCurrency(cost.Coins);
			Content.NPCs.Town.YoyoTownShared.TryConsumeFromInventory(player, cost.Mat, cost.Count);
			ApplyUpgrade(tile);
			return true;
		}

		public static void ApplyUpgrade(Point tile)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return;
			if (s.Level >= s.MaxLevel)
				return;
			s.Level++;
			s.UpgradePending = false; // consume any readiness flag on upgrade
			// Level bumps core stats.
			s.Range += 1;
			s.Damage += 5;
			if (s.Kind == TrapKind.Bearing && s.Level == 2)
				s.BearingTier = System.Math.Min(2, s.BearingTier + 1);
			if (s.Kind == TrapKind.Bearing)
				s.Burst = System.Math.Min(1 + s.Level, 3);
			if (s.Kind == TrapKind.Sentry)
				s.CooldownMax = System.Math.Max(40, s.CooldownMax - 10);
			if (s.Kind == TrapKind.Tripwire) {
				s.MaxLen = System.Math.Min(600, s.MaxLen + 25);
				s.CooldownMax = System.Math.Max(60, s.CooldownMax - 15);
			}
			s.Energy = s.MaxEnergy; // upgrade refills power cell
			TrapSystem.SyncWorld();
		}


		public static void ApplyConfig(Point tile, TrapState fresh)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return;
			// Offensive traps have no UI: reject remote config for them.
			// They level automatically via field experience (see CheckOffensiveAutoUpgrade).
			if (s.Kind == TrapKind.WildSnare || s.Kind == TrapKind.Bearing)
				return;
			// Validate + clamp every field (never trust client values).
			s.Range = System.Math.Clamp(fresh.Range, 2, 30);
			s.Damage = System.Math.Clamp(fresh.Damage, 1, 200);
			s.Duration = System.Math.Clamp(fresh.Duration, 60, 3600);
			s.CooldownMax = System.Math.Clamp(fresh.CooldownMax, 20, 3600);
			s.StringVariant = s.Kind == TrapKind.Link ? System.Math.Clamp(fresh.StringVariant, 0, s.MaxLinkString()) : System.Math.Clamp(fresh.StringVariant, 0, TrapSystem.MaxStringVariant());
			int prevCounter = s.CounterType;
			s.CounterType = System.Math.Clamp(fresh.CounterType, 0, 10);
			// Server-side gate: level rungs + owned experiment bits.
			if (s.Kind == TrapKind.Sentry && !TrapSystem.CounterProgressionOk(s.CounterType, s))
				s.CounterType = TrapSystem.CounterProgressionOk(prevCounter, s) ? System.Math.Clamp(prevCounter, 0, 10) : 10;
			int prevStyle = s.TripStyle;
			s.TripStyle = System.Math.Clamp(fresh.TripStyle, 0, 7);
			if (s.Kind == TrapKind.Tripwire && !TrapSystem.TripStyleOk(s.TripStyle, s))
				s.TripStyle = TrapSystem.TripStyleOk(prevStyle, s) ? System.Math.Clamp(prevStyle, 0, 7) : 0;
			int prevTStr = s.TripString;
			s.TripString = System.Math.Clamp(fresh.TripString, 0, 24);
			if (s.Kind == TrapKind.Tripwire && !TrapSystem.TripStringUnlocked(s.TripString, s, out _))
				s.TripString = TrapSystem.TripStringUnlocked(prevTStr, s, out _) ? System.Math.Clamp(prevTStr, 0, 24) : 0;
			if (s.Kind != TrapKind.Tripwire)
				s.TripString = 0;
			s.SentryString = s.Kind == TrapKind.Sentry ? System.Math.Clamp(fresh.SentryString, 0, 7) : s.SentryString;
			s.LinkTreat = s.Kind == TrapKind.Link ? System.Math.Clamp(fresh.LinkTreat, 0, 2) : 0;
			s.Sched = TrapSystem.IsDefensive(s.Kind) ? System.Math.Clamp(fresh.Sched, 0, 3) : 0;
			{
				int pp = fresh.PulsePeriod;
				s.PulsePeriod = TrapSystem.IsDefensive(s.Kind) ? (pp == 10 ? 10 : pp == 60 ? 60 : 30) : 30;
			}
			s.Priority = System.Math.Clamp(fresh.Priority, 0, 2);
			s.Behavior = System.Math.Clamp(fresh.Behavior, 0, 4);
			s.Restraint = System.Math.Clamp(fresh.Restraint, 0, 10);
			// Bend is capped by the string's IRL max bend (server authoritative).
			int bendMax = TrapState.MaxBendFor(s.StringVariant);
			s.Bend = System.Math.Clamp(fresh.Bend, 0, bendMax);
			s.MaxLen = System.Math.Clamp(fresh.MaxLen, 60, 600);
			s.OutSpeed = System.Math.Clamp(fresh.OutSpeed, 5, 200);
			s.ReturnSpeed = System.Math.Clamp(fresh.ReturnSpeed, 5, 300);
			s.HitPlayers = fresh.HitPlayers; s.HitNPCs = fresh.HitNPCs; s.HitTown = fresh.HitTown;
			s.HitHostile = fresh.HitHostile; s.HitFriendly = fresh.HitFriendly; s.HitAll = fresh.HitAll;
			s.Overcharge = fresh.Overcharge;
			s.Enabled = fresh.Enabled;
			s.ShareMode = System.Math.Clamp(fresh.ShareMode, 0, 2);
			s.Name = string.IsNullOrEmpty(fresh.Name) ? "" : fresh.Name.Substring(0, System.Math.Min(24, fresh.Name.Length));
			s.AutoOC = fresh.AutoOC && (s.Kind == TrapKind.Sentry || s.Kind == TrapKind.Tripwire) && TrapSystem.AegisCovered(tile) && s.Level >= 2;
			s.SentryAnchor = System.Math.Clamp(fresh.SentryAnchor, 0, 2);
			// Unique options: kind-locked, AEGIS-gated, AND level-gated — every
			// trap level unlocks something tangible (see LevelPerkText).
			// Defensive blocks require Defensive-Mesh (Aegis), not Local Mesh.
			bool aegisCfg = TrapSystem.AegisCovered(tile);
			int lvl = s.Level;
			s.SentryAegis = fresh.SentryAegis && s.Kind == TrapKind.Sentry && aegisCfg && lvl >= 2;
			s.WardLattice = fresh.WardLattice && s.Kind == TrapKind.Link && aegisCfg && lvl >= 2;
			s.SnareTip = fresh.SnareTip && s.Kind == TrapKind.Tripwire && aegisCfg && lvl >= 2;
			s.AutoRewire = fresh.AutoRewire && s.Kind == TrapKind.Link && aegisCfg && lvl >= 2;
			// Weave tracks are validated here but upgraded via TryUpgradeTrack
			// (separate tab, costs uses+mats). Clamp only, never trust client.
			s.RegenWeave = System.Math.Clamp(s.RegenWeave, 0, 3);
			s.TensionWeave = System.Math.Clamp(s.TensionWeave, 0, 3);
			s.LinkStress = System.Math.Clamp(s.LinkStress, 0, TrapState.MaxLinkStress);
			// Stances are free trade-offs: kind-locked + level-gated, no mesh needed.
			s.SentryBunker = fresh.SentryBunker && s.Kind == TrapKind.Sentry && lvl >= 1;
			s.LongSpan = fresh.LongSpan && s.Kind == TrapKind.Link && lvl >= 1;
			s.HairTrigger = fresh.HairTrigger && s.Kind == TrapKind.Tripwire && lvl >= 1;
			s.BarbedWire = fresh.BarbedWire && s.Kind == TrapKind.Link && lvl >= 1;
			s.TracerFire = fresh.TracerFire && s.Kind == TrapKind.Sentry && lvl >= 2;
			s.TwinThrow = fresh.TwinThrow && s.Kind == TrapKind.Tripwire && lvl >= 2;
			if (s.Kind == TrapKind.Sentry && lvl < 1)
				s.Overcharge = false;
			if (s.Kind == TrapKind.Tripwire && lvl < 1)
				s.Overcharge = false;
			// A linked pair shares one string: mirror its visual identity to the partner.
			if (s.Kind == TrapKind.Link && s.Linked && TrapSystem.Traps.TryGetValue(s.LinkPoint, out var partner)) {
				partner.StringVariant = s.StringVariant;
				partner.Bend = s.Bend;
				partner.RegenWeave = s.RegenWeave;
				partner.TensionWeave = s.TensionWeave;
				partner.LinkStress = s.LinkStress;
				partner.AutoRewire = s.AutoRewire;
				partner.LinkTreat = s.LinkTreat;
				partner.Sched = s.Sched;
				// Target filters are pair-wide too (the barrier is one rope):
				// configuring either endpoint sets both, so it never matters
				// which endpoint the server ticks (canonical side).
				partner.HitPlayers = s.HitPlayers; partner.HitNPCs = s.HitNPCs; partner.HitTown = s.HitTown;
				partner.HitHostile = s.HitHostile; partner.HitFriendly = s.HitFriendly; partner.HitAll = s.HitAll;
				partner.AlarmIdleOnly = s.AlarmIdleOnly;
			}
			TrapSystem.SyncWorld();
		}

		// What the NEXT level unlocks (one new thing per level, not just
		// numbers). Shown in the upgrade row so levels feel like loot.
		// Every claim here is enforced (option gates + track Lv1 + string tiers).
		// Every claim here is enforced (option gates + track Lv1 + string tiers).
		public static string LevelPerkText(TrapKind kind, int nextLevel)
		{
			return (kind, nextLevel) switch {
				(TrapKind.Sentry, 1) => "Lv1 unlocks: Golden weight, Bunker Stance, Overcharge",
				(TrapKind.Sentry, 2) => "Lv2 unlocks: Tungsten weight, Ichor Tracers, Aegis Broadcast",
				(TrapKind.Sentry, 3) => "Lv3 unlocks: Demon weight, Gyroscope track",
				(TrapKind.Sentry, 4) => "Lv4 unlocks: Hostile weight, max power cell",
				(TrapKind.Link, 1) => "Lv1 unlocks: Long Span, Barbed Wrap track, Weave tracks",
				(TrapKind.Link, 2) => "Lv2 unlocks: Ward Lattice, Auto-Rewire, Polyester string",
				(TrapKind.Link, 3) => "Lv3 unlocks: Nylon string",
				(TrapKind.Link, 4) => "Lv4 unlocks: Kevlar string",
				(TrapKind.Link, 5) => "Lv5 unlocks: Silk string",
				(TrapKind.Link, 6) => "Lv6 unlocks: Shadow string",
				(TrapKind.Link, 7) => "Lv7 unlocks: Solar string, max power cell",
				(TrapKind.Tripwire, 1) => "Lv1 unlocks: Moonpearl head, Barbed/Tangled tethers, Overcharge, Hair Trigger, Axle track",
				(TrapKind.Tripwire, 2) => "Lv2 unlocks: Riot head, material + vanilla tethers (Lab), Twin Throw, Snare Tip",
				(TrapKind.Tripwire, 3) => "Lv3 unlocks: Bullion head, exotic tethers (Lab), longer string, faster reload",
				(TrapKind.Tripwire, 4) => "Lv4 unlocks: Snare head, max power cell",
				_ => "",
			};
		}

		// Unique upgrade tracks (0..3 each, Aegis-gated, defensive identity).
		// id: 0 Tension Braid (Link, +38 max stress), 1 Wax Treatment (Link,
		// -15% regen time), 2 Gyroscope (Sentry, +12% return-leg damage),
		// 3 Lubricated Axle (Tripwire, +20% flight time), 4 Grounding Coil
		// (Link, Ward zap costs less power), 5 Spotter Scope (Sentry, +2
		// range), 6 Silk Reserve (Link, +15 max energy), 7 Second Wind
		// (Tripwire, -10% cooldown), 8 Barbed Wrap (Link, poison bites
		// longer). Costs uses + mats.
		public static bool TryUpgradeTrack(Player player, Point tile, int track)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return false;
			bool isLink = track == 0 || track == 1 || track == 4 || track == 6 || track == 8;
			bool isSentry = track == 2 || track == 5;
			bool isTrip = track == 3 || track == 7;
			if (isLink && s.Kind != TrapKind.Link)
				return false;
			if (isSentry && s.Kind != TrapKind.Sentry)
				return false;
			if (isTrip && s.Kind != TrapKind.Tripwire)
				return false;
			if (!isLink && !isSentry && !isTrip)
				return false;
			int cur = track switch {
				0 => s.TensionWeave,
				1 => s.RegenWeave,
				2 => s.SentryGyro,
				3 => s.TripAxle,
				4 => s.LinkCoil,
				5 => s.SentryScope,
				6 => s.LinkReserve,
				7 => s.TripWind,
				_ => s.LinkBarb,
			};
			if (cur >= 3)
				return false;
			// Tracks open at trap Lv1 (each level-up feeds a new track).
			if (s.Level < 1)
				return false;
			int needUses = cur switch { 0 => 5, 1 => 15, 2 => 30, _ => 50 };
			if (s.Uses < needUses)
				return false;
			if (!TrapSystem.AegisCovered(tile))
				return false;
			int scraps = ModContent.ItemType<Items.Materials.YoyoScraps>();
			int parts = ModContent.ItemType<Items.Materials.YoyoParts>();
			int adv = ModContent.ItemType<Items.Materials.AdvancedYoyoParts>();
			int mat = cur == 0 ? scraps : cur == 1 ? parts : adv;
			int count = cur == 0 ? 8 : cur == 1 ? 6 : 4;
			int coins = cur == 0 ? Item.buyPrice(gold: 2) : cur == 1 ? Item.buyPrice(gold: 6) : Item.buyPrice(gold: 12);
			if (!player.CanAfford(coins))
				return false;
			if (Content.NPCs.Town.YoyoTownShared.CountInInventory(player, mat) < count)
				return false;
			player.PayCurrency(coins);
			Content.NPCs.Town.YoyoTownShared.TryConsumeFromInventory(player, mat, count);
			ApplyTrack(tile, track);
			return true;
		}

		public static void ApplyTrack(Point tile, int track)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return;
			// Server-authoritative gate (payment happened client-side).
			if (!TrapSystem.AegisCovered(tile))
				return;
			if (s.Level < 1)
				return;
			switch (track) {
				case 0:
					if (s.Kind != TrapKind.Link || s.TensionWeave >= 3)
						return;
					s.TensionWeave++;
					break;
				case 1:
					if (s.Kind != TrapKind.Link || s.RegenWeave >= 3)
						return;
					s.RegenWeave++;
					break;
				case 2:
					if (s.Kind != TrapKind.Sentry || s.SentryGyro >= 3)
						return;
					s.SentryGyro++;
					break;
				case 3:
					if (s.Kind != TrapKind.Tripwire || s.TripAxle >= 3)
						return;
					s.TripAxle++;
					break;
				case 4:
					if (s.Kind != TrapKind.Link || s.LinkCoil >= 3)
						return;
					s.LinkCoil++;
					break;
				case 5:
					if (s.Kind != TrapKind.Sentry || s.SentryScope >= 3)
						return;
					s.SentryScope++;
					break;
				case 6:
					if (s.Kind != TrapKind.Link || s.LinkReserve >= 3)
						return;
					s.LinkReserve++;
					break;
				case 7:
					if (s.Kind != TrapKind.Tripwire || s.TripWind >= 3)
						return;
					s.TripWind++;
					break;
				case 8:
					if (s.Kind != TrapKind.Link || s.LinkBarb >= 3)
						return;
					s.LinkBarb++;
					break;
				default: return;
			}
			if (s.Kind == TrapKind.Link && s.Linked && TrapSystem.Traps.TryGetValue(s.LinkPoint, out var partner)) {
				partner.RegenWeave = s.RegenWeave;
				partner.TensionWeave = s.TensionWeave;
				partner.LinkStress = s.LinkStress;
				partner.LinkCoil = s.LinkCoil;
				partner.LinkReserve = s.LinkReserve;
				partner.LinkBarb = s.LinkBarb;
			}
			TrapSystem.SyncWorld();
		}

		// Reinforced Rope: the dedicated max-stress upgrade for String Link
		// pairs. +30 max stress per level (0..3). Deliberately ungated:
		// no Aegis cover, no field uses, no trap level needed — just
		// string + coins, right here on the Barrier tab. Stacks with
		// Tension Braid (weave tab). Pair shares it like the weaves.
		public static (int coins, int mat, int count) ReinforceCost(int next) => next switch {
			1 => (Terraria.Item.buyPrice(gold: 2), Terraria.ID.ItemID.Silk, 10),
			2 => (Terraria.Item.buyPrice(gold: 8), Terraria.ID.ItemID.Cobweb, 20),
			_ => (Terraria.Item.buyPrice(gold: 20), Terraria.ID.ItemID.Silk, 20),
		};

		public static string ReinforceCostText(int next)
		{
			if (next > TrapState.MaxLinkStress)
				return "MAX";
			var (coins, mat, count) = ReinforceCost(next);
			string matName;
			try {
				matName = Terraria.Lang.GetItemNameValue(mat).ToLower();
			}
			catch {
				matName = "silk";
			}
			return $"{count}x {matName} + {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(coins)}";
		}

		public static bool TryReinforce(Player player, Point tile)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return false;
			if (s.Kind != TrapKind.Link)
				return false;
			if (s.LinkStress >= TrapState.MaxLinkStress)
				return false;
			var (coins, mat, count) = ReinforceCost(s.LinkStress + 1);
			if (!player.CanAfford(coins))
				return false;
			if (Content.NPCs.Town.YoyoTownShared.CountInInventory(player, mat) < count)
				return false;
			player.PayCurrency(coins);
			Content.NPCs.Town.YoyoTownShared.TryConsumeFromInventory(player, mat, count);
			ApplyReinforce(tile);
			return true;
		}

		public static void ApplyReinforce(Point tile)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return;
			// Server-authoritative gate (payment happened client-side).
			if (s.Kind != TrapKind.Link || s.LinkStress >= TrapState.MaxLinkStress)
				return;
			s.LinkStress++;
			if (s.Linked && TrapSystem.Traps.TryGetValue(s.LinkPoint, out var partner))
				partner.LinkStress = s.LinkStress;
			TrapSystem.SyncWorld();
		}

		// Config presets: copy one block's SETTINGS (never levels, tracks,
		// power, wear or pairing) and stamp them onto same-kind blocks.
		public static TrapState CopyBuf;

		public static void CopyConfigFields(TrapState dst, TrapState src)
		{
			dst.Range = src.Range; dst.Damage = src.Damage; dst.Duration = src.Duration; dst.CooldownMax = src.CooldownMax;
			dst.StringVariant = src.StringVariant; dst.BearingTier = src.BearingTier; dst.CounterType = src.CounterType;
			dst.Priority = src.Priority; dst.Behavior = src.Behavior; dst.Restraint = src.Restraint; dst.Bend = src.Bend;
			dst.MaxLen = src.MaxLen; dst.OutSpeed = src.OutSpeed; dst.ReturnSpeed = src.ReturnSpeed; dst.Burst = src.Burst;
			dst.HitPlayers = src.HitPlayers; dst.HitNPCs = src.HitNPCs; dst.HitTown = src.HitTown;
			dst.HitHostile = src.HitHostile; dst.HitFriendly = src.HitFriendly; dst.HitAll = src.HitAll;
			dst.Overcharge = src.Overcharge; dst.SentryAnchor = src.SentryAnchor;
			dst.SentryAegis = src.SentryAegis; dst.WardLattice = src.WardLattice; dst.SnareTip = src.SnareTip; dst.AutoRewire = src.AutoRewire;
			dst.SentryBunker = src.SentryBunker; dst.LongSpan = src.LongSpan; dst.HairTrigger = src.HairTrigger;
			dst.BarbedWire = src.BarbedWire; dst.TracerFire = src.TracerFire; dst.TwinThrow = src.TwinThrow;
			dst.TripStyle = src.TripStyle; dst.TripString = src.TripString; dst.Enabled = src.Enabled;
			dst.Name = src.Name ?? ""; dst.AutoOC = src.AutoOC; dst.ShareMode = System.Math.Clamp(src.ShareMode, 0, 2);
			dst.Sched = src.Sched;
			dst.PulsePeriod = src.PulsePeriod == 10 ? 10 : src.PulsePeriod == 60 ? 60 : 30;
		}

		public static void CopyPreset(Point tile)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return;
			var buf = new TrapState { Kind = s.Kind };
			CopyConfigFields(buf, s);
			CopyBuf = buf;
		}

		public static bool CanPastePreset(Point tile)
		{
			if (CopyBuf == null || !TrapSystem.Traps.TryGetValue(tile, out var s))
				return false;
			return s.Kind == CopyBuf.Kind;
		}

		// Maintenance service: Bearing Oil + coins restores 25% condition.
		public static bool TryService(Player player, Point tile)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return false;
			if (s.Kind != TrapKind.Sentry && s.Kind != TrapKind.Link && s.Kind != TrapKind.Tripwire)
				return false;
			if (s.Condition >= 100)
				return false;
			int oil = ModContent.ItemType<Items.Consumables.BearingOil>();
			int coins = Item.buyPrice(gold: 1);
			if (!player.CanAfford(coins))
				return false;
			if (Content.NPCs.Town.YoyoTownShared.CountInInventory(player, oil) < 1)
				return false;
			player.PayCurrency(coins);
			Content.NPCs.Town.YoyoTownShared.TryConsumeFromInventory(player, oil, 1);
			s.Condition = System.Math.Min(100, s.Condition + 25);
			s.CondTick = 0;
			TrapSystem.SyncWorld();
			return true;
		}

		// Experiment outcomes (server-authoritative application; payment
		// happened client-side through the picker). Effect ids mirror
		// TrapExperiment: 0 recharge, 1 rewire, 2 service, 3 attune.
		public static void ApplyExperiment(Point tile, int effect)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return;
			if (s.Kind != TrapKind.Sentry && s.Kind != TrapKind.Link && s.Kind != TrapKind.Tripwire)
				return;
			switch (effect) {
				case 0:
					s.Energy = s.MaxEnergy; s.Cooldown = 0;
					break;
				case 1:
					s.Stress = 0; s.Frayed = 0; s.Energy = System.Math.Min(s.MaxEnergy, s.Energy + 40);
					break;
				case 2:
					s.Condition = System.Math.Min(100, s.Condition + 25); s.CondTick = 0;
					break;
				default:
					if (effect >= 10 && effect <= 35) {
						// Head/tether research: unlock one exclusive head or tether on this trap.
						s.ExpHeads |= 1 << (effect - 10);
						break;
					}
					s.Uses += 10;
					break;
			}
			TrapSystem.SyncWorld();
		}

		// Offensive traps have no UI, so they level automatically:
		// field experience (Uses) + mesh gate for Lv2, materials auto-consumed
		// from the nearest player in range. Closest reliable alternative to
		// manual UI upgrades without breaking the no-UI rule.
		public static void CheckOffensiveAutoUpgrade(Point tile)
		{
			if (!TrapSystem.Traps.TryGetValue(tile, out var s))
				return;
			if (s.Kind != TrapKind.WildSnare && s.Kind != TrapKind.Bearing)
				return;
			if (s.Level >= 2)
				return;
			// Server (or single-player sim) only flags readiness here. Payment
			// always happens on the machine that owns the inventory (see
			// TryClaimOffensiveUpgrades): server-side inventory edits would
			// never sync back to multiplayer clients.
			if (!UpgradeRequirementMet(tile, s, out _))
				return;
			if (!s.UpgradePending) {
				s.UpgradePending = true;
				TrapSystem.SyncWorld();
			}
		}

		// Client-side counterpart (single-player + MP clients only): if a
		// nearby offensive trap is flagged ready and the LOCAL player can pay,
		// pay locally and forward to the server like a UI upgrade.
		public static void TryClaimOffensiveUpgrades(Player player)
		{
			if (player == null || !player.active || player.dead)
				return;
			foreach (var kv in TrapSystem.Traps) {
				if (kv.Value.Kind != TrapKind.WildSnare && kv.Value.Kind != TrapKind.Bearing)
					continue;
				if (!kv.Value.UpgradePending)
					continue;
				if (Vector2.DistanceSquared(player.Center, TrapSystem.TileCenter(kv.Key)) > 12f * 16f * 12f * 16f)
					continue;
				if (TrapActions.TryUpgrade(player, kv.Key)) {
					if (Main.netMode == NetmodeID.MultiplayerClient)
						TrapNet.SendUpgrade(kv.Key);
					if (Main.netMode != NetmodeID.Server)
						Main.NewText($"Trap at ({kv.Key.X},{kv.Key.Y}) auto-upgraded to Lv{kv.Value.Level} (field experience).", 150, 255, 150);
				}
			}
		}
}
}
