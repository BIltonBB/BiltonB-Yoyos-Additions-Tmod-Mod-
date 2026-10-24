using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Traps
{
	// Central trap world state + server-authoritative ticking.
	// Mirrors ResonatorSystem patterns: Dictionary<Point,TrapState>, IsTileStill*,
	// Save/Load parallel lists, NetSend/Receive, SyncWorld via WorldData.
	public class TrapSystem : ModSystem
	{
		public static readonly Dictionary<Point, TrapState> Traps = new();

		public static TrapState GetOrCreate(Point key, TrapKind kind)
		{
			if (!Traps.TryGetValue(key, out var s)) {
				s = TrapState.DefaultFor(kind);
				Traps[key] = s;
			}
			return s;
		}

		public static TrapState Get(Point key)
		{
			Traps.TryGetValue(key, out var s);
			return s;
		}

		public static void ForgetTile(Point key)
		{
			bool removed = Traps.Remove(key);
			// String Link: safely disconnect partner.
			if (!removed) {
				// still need to scan for partners pointing here
			}
			// Break any links pointing at this tile.
			var toClear = new List<Point>();
			foreach (var kv in Traps) {
				if (kv.Value.Kind == TrapKind.Link && kv.Value.Linked && kv.Value.LinkPoint == key)
					toClear.Add(kv.Key);
			}
			foreach (var p in toClear) {
				if (Traps.TryGetValue(p, out var ps)) {
					ps.LinkX = -9999; ps.LinkY = -9999; ps.WiredActive = false;
				}
			}
			try { LinkRope.ClearInvalid(); } catch { }
			if (removed || toClear.Count > 0)
				SyncWorld();
		}

		public static void SyncWorld()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public static bool IsTrapTile(Point key, TrapKind kind)
		{
			if (!WorldGen.InWorld(key.X, key.Y))
				return false;
			Tile t = Framing.GetTileSafely(key.X, key.Y);
			if (!t.HasTile)
				return false;
			return t.TileType == TrapTiles.TileTypeFor(kind);
		}

		public static bool IsAnyTrapTile(Point key)
		{
			if (!WorldGen.InWorld(key.X, key.Y))
				return false;
			Tile t = Framing.GetTileSafely(key.X, key.Y);
			if (!t.HasTile)
				return false;
			int tt = t.TileType;
			return tt == ModContent.TileType<WildstringSnareTile>()
				|| tt == ModContent.TileType<BearingTrapTile>()
				|| tt == ModContent.TileType<CounterweightSentryTile>()
				|| tt == ModContent.TileType<StringLinkTile>()
				|| tt == ModContent.TileType<YoyoTripwireTile>();
		}

		public static Vector2 TileCenter(Point key)
		{
			return new Vector2(key.X * 16f + 8f, key.Y * 16f + 8f);
		}

		// ---- Mesh / energy / synergy (creative systems) ----

		// Local-mesh bonus tier 0..3 via Research station network.
		// Traps are treated like stations: any covering engine buffs them.
		public static int MeshTier(Point tile)
		{
			try {
				return Content.Placeables.StationNetwork.BestTier(tile);
			}
			catch { return 0; }
		}

		public static bool MeshCovered(Point tile)
		{
			try {
				return Content.Placeables.StationNetwork.Covers(tile);
			}
			catch { return false; }
		}

		// Aegis Mesh (defensive-only): same shape, triple projection.
		public static int AegisTier(Point tile)
		{
			try {
				return Content.Placeables.StationNetwork.AegisBestTier(tile);
			}
			catch { return 0; }
		}

		public static bool AegisCovered(Point tile)
		{
			try {
				return Content.Placeables.StationNetwork.AegisCovers(tile);
			}
			catch { return false; }
		}

		public static bool AegisFar(Point tile)
		{
			try {
				return Content.Placeables.StationNetwork.AegisFarCovers(tile);
			}
			catch { return false; }
		}

		public static bool AegisFueled(Point tile)
		{
			try {
				return Content.Placeables.StationNetwork.AegisFueled(tile);
			}
			catch { return false; }
		}

		public static bool IsDefensive(TrapKind kind)
			=> kind == TrapKind.Sentry || kind == TrapKind.Link || kind == TrapKind.Tripwire;

		// Defensive traps use Aegis; offensive traps use Local Mesh.
		public static bool MeshedForKind(Point tile, TrapKind kind)
			=> IsDefensive(kind) ? AegisCovered(tile) : MeshCovered(tile);

		public static int TierForKind(Point tile, TrapKind kind)
			=> IsDefensive(kind) ? AegisTier(tile) : MeshTier(tile);

		// Link-held registry (server sim): which NPCs a barrier is holding and
		// when, so Aegis-meshed sentries can focus-fire snared targets.
		private static readonly Dictionary<int, int> linkHeldTick = new();

		public static void NoteLinkHeld(int whoAmI)
		{
			linkHeldTick[whoAmI] = (int)Main.GameUpdateCount;
			if (linkHeldTick.Count > 500) {
				int now = (int)Main.GameUpdateCount;
				var stale = new System.Collections.Generic.List<int>();
				foreach (var kv in linkHeldTick) {
					if (now - kv.Value > 1200)
						stale.Add(kv.Key);
				}
				foreach (var k in stale)
					linkHeldTick.Remove(k);
			}
		}

		// Ticks since this NPC was last held by any barrier (-1 = never).
		public static int LinkHeldAgo(int whoAmI)
		{
			if (linkHeldTick.TryGetValue(whoAmI, out int t))
				return (int)Main.GameUpdateCount - t;
			return -1;
		}

		// Human-readable list of Aegis-only unlocks for the 3 defensive blocks.
		// Kept to two short lines: this renders as a cursor tooltip over the
		// panel, so sprawl covers buttons.
		public static string AegisUnlocksFor(TrapKind kind) => kind switch {
			TrapKind.Sentry => "Aegis-only: Endurance aura, Ichor tracers, Lv2+ upgrades, 2x regen.",
			TrapKind.Link => "Aegis-only: Ward Lattice, Auto-Rewire, Weave tracks, Far range.",
			_ => "Aegis-only: Snare Tip, Twin Throw synergy, Lv2+ upgrades, 2x regen.",
		};

		// Experiment synergy: a different trap kind within 25 tiles that is
		// also meshed (per-kind mesh) shares calibration (+10% dmg, -10% cd).
		public static bool HasSynergy(Point tile, TrapKind kind)
		{
			if (!MeshedForKind(tile, kind))
				return false;
			foreach (var kv in Traps) {
				if (kv.Key == tile || kv.Value.Kind == kind)
					continue;
				float dx = kv.Key.X - tile.X;
				float dy = kv.Key.Y - tile.Y;
				if (dx * dx + dy * dy > 25 * 25)
					continue;
				if (!MeshedForKind(kv.Key, kv.Value.Kind))
					continue;
				return true;
			}
			return false;
		}

		public static int EffectiveDamageRaw(Point tile, TrapState s)
		{
			int dmg = s.Damage + s.Level * 10;
			int tier = TierForKind(tile, s.Kind);
			if (tier > 0)
				dmg = (int)(dmg * (1f + 0.10f * tier));
			if (IsDefensive(s.Kind)) {
				if (AegisFueled(tile))
					dmg = (int)(dmg * 1.25f); // Aegis overdrive burns charge for punch
				// Veteran bonus: lifetime kills harden the machine (25/100/250).
				dmg = (int)(dmg * (1f + VeteranBonus(s.Kills) / 100f));
			}
			if (HasSynergy(tile, s.Kind))
				dmg = (int)(dmg * 1.10f);
			if (s.Kind == TrapKind.Sentry && s.SentryBunker)
				dmg = (int)(dmg * 1.50f);
			if (s.Kind == TrapKind.Link && s.LongSpan)
				dmg = (int)(dmg * 0.75f);
			if (s.Kind == TrapKind.Tripwire && s.HairTrigger)
				dmg = (int)(dmg * 0.75f);
			// Maintenance: worn defensive machines hit softer (never below half).
			if (s.Kind != TrapKind.WildSnare && s.Kind != TrapKind.Bearing)
				dmg = (int)(dmg * (0.5f + 0.5f * System.Math.Clamp(s.Condition, 0, 100) / 100f));
			return Math.Max(1, dmg);
		}

		public static int EffectiveDamage(Point tile, TrapState s)
		{
			int dmg = EffectiveDamageRaw(tile, s);
			if (s.Overcharge)
				dmg = (int)(dmg * 1.50f);
			return Math.Max(1, dmg);
		}

		// Fueled automation: AutoOC traps fire overcharged while an Aegis
		// Overdrive mesh fuels them — at 2% backfire instead of 15%.
		public static bool OverchargeActive(Point tile, TrapState s)
			=> s.Overcharge || (s.AutoOC && AegisFueled(tile));

		public static float OverchargeRisk(Point tile, TrapState s)
			=> s.Overcharge ? 0.15f : 0.02f;

		// Veteran bonus % from lifetime kills: 25 -> +2, 100 -> +4, 250 -> +6.
		public static int VeteranBonus(int kills)
		{
			if (kills >= 250) return 6;
			if (kills >= 100) return 4;
			if (kills >= 25) return 2;
			return 0;
		}

		// Milestone announcement (server-side kills only; gated by mod config).
		public static void CheckVeteranMilestone(Point key, TrapState s, int prevKills)
		{
			foreach (int t in new[] { 25, 100, 250 }) {
				if (prevKills < t && s.Kills >= t) {
					bool announce = true;
					try {
						var cfg = ModContent.GetInstance<Configuration.YoyoServerConfig>();
						if (cfg != null)
							announce = cfg.AnnounceVeteranMilestones;
					}
					catch { }
					if (announce) {
						string who = string.IsNullOrEmpty(s.Name) ? $"{s.Kind} ({key.X},{key.Y})" : s.Name;
						Terraria.Chat.ChatHelper.BroadcastChatMessage(
							Terraria.Localization.NetworkText.FromLiteral($"{who} is now a veteran: {s.Kills} kills (+{VeteranBonus(s.Kills)}% damage)!"),
							new Color(255, 215, 120));
					}
					break;
				}
			}
		}

		public static int EffectiveRangeTiles(Point tile, TrapState s)
		{
			int r = s.Range + s.Level * 2 + TierForKind(tile, s.Kind) * 2;
			if (s.Kind == TrapKind.Sentry)
				r += System.Math.Clamp(s.SentryScope, 0, 3) * 2; // Spotter Scope
			if (s.Kind == TrapKind.Sentry && s.SentryBunker)
				r = (int)(r * 0.65f);
			if (s.Kind == TrapKind.Link && s.LongSpan)
				r = (int)(r * 1.5f);
			if (IsDefensive(s.Kind) && AegisCovered(tile) && AegisFar(tile)) {
				// Far Aegis: TRIPLE detection range for defensive blocks only.
				r = (s.Range + s.Level * 2) * 3 + TierForKind(tile, s.Kind) * 2;
				return Math.Min(r, 60);
			}
			return IsDefensive(s.Kind) ? Math.Min(r, 45) : Math.Min(r, 30);
		}

		public static int EffectiveCooldown(Point tile, TrapState s)
		{
			float mult = 1f - 0.12f * s.Level;
			int tier = TierForKind(tile, s.Kind);
			if (tier > 0)
				mult *= 1f - 0.05f * tier;
			if (HasSynergy(tile, s.Kind))
				mult *= 0.90f;
			if (s.Overcharge)
				mult *= 1.10f; // overcharge hits harder but cycles slightly slower
			if (s.Kind == TrapKind.Tripwire && s.HairTrigger)
				mult *= 0.50f;
			if (s.Kind == TrapKind.Tripwire && s.TripWind > 0)
				mult *= 1f - 0.10f * System.Math.Clamp(s.TripWind, 0, 3); // Second Wind
			mult = Math.Max(0.40f, mult);
			return Math.Max(20, (int)(s.CooldownMax * mult));
		}

		public static float EnergyRegenPerTick(Point tile)
		{
			// base 1 energy / 60 ticks; doubled in per-kind mesh
			return 1f / 60f;
		}

		public static float EnergyRegenFor(Point tile, TrapKind kind)
			=> MeshedForKind(tile, kind) ? 2f / 60f : 1f / 60f;

		// Wildstring progression pool for the SNARE (0..4, Moonlord maxes it).
		// Link strings use the extended pool below (Moonlord opens 5..7,
		// trap level gates each rung).
		public static int MaxStringVariant()
		{
			if (NPC.downedMoonlord) return 4;
			if (NPC.downedPlantBoss || NPC.downedGolemBoss) return 3;
			if (Main.hardMode) return 2; // polyester tier available in hardmode
			if (NPC.downedBoss3) return 2; // skeletron unlocks polyester pool
			if (NPC.downedBoss2) return 1;
			if (NPC.downedBoss1) return 1;
			return 0;
		}

		// Extended pool for LINK strings: same early ladder, then Moonlord
		// opens Silk (5), Shadow (6) and Solar (7) — trap level gates each.
		public static int MaxLinkProg()
		{
			if (NPC.downedMoonlord) return 7;
			return MaxStringVariant();
		}

		public static int PickSnareVariant()
		{
			int max = MaxStringVariant();
			if (max <= 0) return 0;
			return Main.rand.Next(0, max + 1);
		}

		public static Color StringColor(int variant)
		{
			return variant switch {
				1 => new Color(70, 220, 90),
				2 => new Color(80, 150, 255),
				3 => new Color(255, 220, 70),
				4 => new Color(139, 90, 43),
				5 => new Color(255, 170, 200), // silk pink
				6 => new Color(95, 65, 145), // shadow violet
				7 => new Color(255, 140, 50), // solar orange
				_ => Color.White,
			};
		}

		public static int StringDamage(int variant)
		{
			return variant switch {
				1 => 25,
				2 => 50,
				3 => 65,
				4 => 120,
				_ => 5,
			};
		}

		public static int BearingDamage(int tier)
		{
			return tier switch {
				1 => 45,
				2 => 90,
				_ => 18,
			};
		}

		// ---- Counterweight varieties (sentry slot, 11 total) ----
		// Art = verified in-repo textures plus one vanilla dice (wiki projectile IDs,
		// no magenta risk). Iron uses the vanilla Black Counterweight dice (proj 556).
		public struct CounterDef
		{
			public string Name;
			public string Tip;
			public string Tex;
			public int VanillaProj; // >=0: vanilla projectile art, else mod Tex
			public Color Tint;
			public float Scale;
			public int Dmg;
			public float Speed;
			public int Debuff;
			public int DebuffTicks;
		}

		public static readonly CounterDef[] Counters = {
			new CounterDef { Name = "Golden", Tip = "Gilded baseline. Sentry Lv1.", Tex = "BiltonsYoyoMod/Content/Projectiles/GoldenCounterweightProj", VanillaProj = -1, Tint = new Color(255, 255, 255), Scale = 1f, Dmg = 6, Speed = 12f, Debuff = -1 },
			new CounterDef { Name = "Ancient", Tip = "Old bones. Experiment: Bone x50 + anything (Lab tab).", Tex = "BiltonsYoyoMod/Content/Projectiles/AncientCounterweightProj", VanillaProj = -1, Tint = new Color(255, 255, 255), Scale = 1f, Dmg = 16, Speed = 13f, Debuff = -1 },
			new CounterDef { Name = "Tungsten", Tip = "Dense slug. Sentry Lv2.", Tex = "BiltonsYoyoMod/Content/Projectiles/CounterweightYG", VanillaProj = -1, Tint = new Color(200, 200, 210), Scale = 1f, Dmg = 9, Speed = 12f, Debuff = -1 },
			new CounterDef { Name = "Demon", Tip = "Smells faintly of evil. Sentry Lv3.", Tex = "BiltonsYoyoMod/Content/Projectiles/ADCounterweightProj", VanillaProj = -1, Tint = new Color(255, 255, 255), Scale = 1f, Dmg = 12, Speed = 12f, Debuff = -1 },
			new CounterDef { Name = "Hostile", Tip = "Hard temper, Poison 3s. Sentry Lv4.", Tex = "BiltonsYoyoMod/Content/Projectiles/HostileCounterweight", VanillaProj = -1, Tint = new Color(255, 255, 255), Scale = 1f, Dmg = 14, Speed = 12f, Debuff = BuffID.Poisoned, DebuffTicks = 180 },
			new CounterDef { Name = "Venom", Tip = "Queen glaze, Poison 5s. Experiment: Stingers.", Tex = "BiltonsYoyoMod/Projectiles/BeeYoyoProj", VanillaProj = -1, Tint = new Color(255, 255, 255), Scale = 1f, Dmg = 15, Speed = 13f, Debuff = BuffID.Poisoned, DebuffTicks = 300 },
			new CounterDef { Name = "Gilded", Tip = "Filed star, expensive. Experiment: Gold Bars.", Tex = "BiltonsYoyoMod/Content/Projectiles/StarYG", VanillaProj = -1, Tint = new Color(255, 215, 120), Scale = 1f, Dmg = 20, Speed = 13f, Debuff = -1 },
			new CounterDef { Name = "Warden", Tip = "Hums softly, Slow 2s. Experiment: Ectoplasm.", Tex = "BiltonsYoyoMod/Content/Projectiles/OrbiShieldProj", VanillaProj = -1, Tint = new Color(150, 230, 255), Scale = 0.8f, Dmg = 18, Speed = 13f, Debuff = BuffID.Slow, DebuffTicks = 120 },
			new CounterDef { Name = "Orbit", Tip = "Golem-grade ballast, Slow 3s. Experiment: Power Cell.", Tex = "BiltonsYoyoMod/Content/Projectiles/OrbitCounterweightProj", VanillaProj = -1, Tint = new Color(255, 255, 255), Scale = 0.55f, Dmg = 22, Speed = 12f, Debuff = BuffID.Slow, DebuffTicks = 180 },
			new CounterDef { Name = "Lunar", Tip = "Moon souvenir, Frostburn 4s. Experiment: Lunar Bars.", Tex = "BiltonsYoyoMod/Projectiles/EnchantedYoyoProj", VanillaProj = -1, Tint = new Color(180, 220, 255), Scale = 1f, Dmg = 28, Speed = 14f, Debuff = BuffID.Frostburn, DebuffTicks = 240 },
			new CounterDef { Name = "Iron", Tip = "Basic iron dice. Standard issue.", Tex = "", VanillaProj = 556, Tint = new Color(255, 255, 255), Scale = 1f, Dmg = 2, Speed = 11f, Debuff = -1 },
		};

		public static CounterDef CounterOf(int idx) => Counters[System.Math.Clamp(idx, 0, Counters.Length - 1)];

		// Full unlock check (UI time, has a player). Main heads: exactly one
		// per trap upgrade (Iron default, one new main per level L1..L4).
		public static bool CounterUnlocked(int idx, Point tile, TrapState s, Player player, out string why)
		{
			why = "";
			int lv = s?.Level ?? 0;
			switch (System.Math.Clamp(idx, 0, 10)) {
				case 0:
					if (lv < 1) { why = "Needs sentry Lv1 (one head per upgrade)."; return false; }
					return true;
				case 1:
					if (s == null || (s.ExpHeads & (1 << 25)) == 0) { why = "Experiment: Bone x50 + anything (Lab tab)."; return false; }
					return true;
				case 2:
					if (lv < 2) { why = "Needs sentry Lv2 (one head per upgrade)."; return false; }
					return true;
				case 3:
					if (lv < 3) { why = "Needs sentry Lv3 (one head per upgrade)."; return false; }
					return true;
				case 4:
					if (lv < 4) { why = "Needs sentry Lv4 (one head per upgrade)."; return false; }
					return true;
				case 5:
					if (s == null || (s.ExpHeads & 1) == 0) { why = "Experiment: Stinger x5 + anything (Lab tab)."; return false; }
					return true;
				case 6:
					if (s == null || (s.ExpHeads & 2) == 0) { why = "Experiment: Gold Bar x5 + anything (Lab tab)."; return false; }
					return true;
				case 7:
					if (s == null || (s.ExpHeads & 4) == 0) { why = "Experiment: Ectoplasm x3 + anything (Lab tab)."; return false; }
					return true;
				case 8:
					if (s == null || (s.ExpHeads & 8) == 0) { why = "Experiment: Power Cell x1 + anything (Lab tab)."; return false; }
					return true;
				case 9:
					if (s == null || (s.ExpHeads & 16) == 0) { why = "Experiment: Lunar Bar x5 + anything (Lab tab)."; return false; }
					return true;
				default: return true;
			}
		}

		// Server-side gate (world state + owned bits; license/mesh stay
		// client-gated at selection time). Keeps spoofed configs honest.
		public static bool CounterProgressionOk(int idx, TrapState s)
		{
			int lv = s?.Level ?? 0;
			return System.Math.Clamp(idx, 0, 10) switch {
				0 => lv >= 1,
				1 => s != null && (s.ExpHeads & (1 << 25)) != 0,
				2 => lv >= 2,
				3 => lv >= 3,
				4 => lv >= 4,
				5 => s != null && (s.ExpHeads & 1) != 0,
				6 => s != null && (s.ExpHeads & 2) != 0,
				7 => s != null && (s.ExpHeads & 4) != 0,
				8 => s != null && (s.ExpHeads & 8) != 0,
				9 => s != null && (s.ExpHeads & 16) != 0,
				_ => true,
			};
		}

		// ---- Tripwire yoyo head styles (8): real in-mod yoyo sprites ----
		// (verified paths, single-frame), head-matched tether color, damage.
		public struct TripStyleDef
		{
			public string Name;
			public string Tex;
			public Color String;
			public float DmgMult;
			public string Obtain;
		}

		public static readonly TripStyleDef[] TripStyles = {
			new TripStyleDef { Name = "Anchor", Tex = "BiltonsYoyoMod/Projectiles/HeavyYoyoProj", String = new Color(220, 160, 220), DmgMult = 1f, Obtain = "Standard issue." },
			new TripStyleDef { Name = "Moonpearl", Tex = "BiltonsYoyoMod/Projectiles/HallowYoyoProj", String = new Color(235, 235, 240), DmgMult = 1f, Obtain = "Tripwire Lv1." },
			new TripStyleDef { Name = "Riot", Tex = "BiltonsYoyoMod/Projectiles/CrimsonYoyoProj", String = new Color(255, 90, 90), DmgMult = 1.15f, Obtain = "Tripwire Lv2." },
			new TripStyleDef { Name = "Snare", Tex = "BiltonsYoyoMod/Projectiles/PlanteraYoyoProj", String = new Color(120, 220, 120), DmgMult = 0.9f, Obtain = "Tripwire Lv4." },
			new TripStyleDef { Name = "Bullion", Tex = "BiltonsYoyoMod/Projectiles/GoldYoyoProj", String = new Color(255, 210, 140), DmgMult = 1.1f, Obtain = "Tripwire Lv3." },
			new TripStyleDef { Name = "Dusk", Tex = "BiltonsYoyoMod/Projectiles/CorruptionYoyoProj", String = new Color(140, 140, 210), DmgMult = 1.05f, Obtain = "Experiment: Black Lens." },
			new TripStyleDef { Name = "Glacier", Tex = "BiltonsYoyoMod/Projectiles/DeerYoyoProj", String = new Color(160, 215, 255), DmgMult = 1f, Obtain = "Experiment: Ice. Frostburn on hit." },
			new TripStyleDef { Name = "Blood Tithe", Tex = "BiltonsYoyoMod/Projectiles/FleshYoyoProj", String = new Color(215, 70, 85), DmgMult = 1.2f, Obtain = "Experiment: Vertebrae." },
		};

		public static TripStyleDef TripStyleOf(int idx) => TripStyles[System.Math.Clamp(idx, 0, TripStyles.Length - 1)];

		// Server-side style gate (level rungs + owned bits, no player needed).
		public static bool TripStyleOk(int idx, TrapState s)
		{
			int lv = s?.Level ?? 0;
			return System.Math.Clamp(idx, 0, 7) switch {
				1 => lv >= 1,
				2 => lv >= 2,
				4 => lv >= 3,
				3 => lv >= 4,
				5 => s != null && (s.ExpHeads & 32) != 0,
				6 => s != null && (s.ExpHeads & 64) != 0,
				7 => s != null && (s.ExpHeads & 128) != 0,
				_ => true,
			};
		}

		// Tripwire STRING types (the tether itself, NOT the head): mirror the
		// string accessories exactly — Barbed string cuts for 5 + Bleeding 5s
		// like Barbed Strings, Tangled for 1 + Bleeding 3s like Tangled
		// Strings. Colors match the accessory shaders (black / silver).
		public static string TripStringName(int v) => System.Math.Clamp(v, 0, 24) switch {
			1 => "Barbed",
			2 => "Tangled",
			3 => "Cotton",
			4 => "Polyester",
			5 => "Silk",
			6 => "Shadow",
			7 => "Solar",
			8 => "Cyan",
			9 => "Ancient",
			10 => "Crimson",
			11 => "Red",
			12 => "Orange",
			13 => "Yellow",
			14 => "Lime",
			15 => "Green",
			16 => "Teal",
			17 => "Sky Blue",
			18 => "Blue",
			19 => "Purple",
			20 => "Violet",
			21 => "Pink",
			22 => "Black",
			23 => "Brown",
			24 => "Rainbow",
			_ => "Plain",
		};

		public static Color TripStringColor(int v, Color headString) => System.Math.Clamp(v, 0, 24) switch {
			1 => new Color(25, 25, 30), // black string, like Barbed Strings
			2 => new Color(225, 230, 235), // silver string, like Tangled Strings
			3 => new Color(240, 240, 240), // cotton white
			4 => new Color(80, 150, 255), // polyester blue
			5 => new Color(255, 170, 200), // silk pink
			6 => new Color(95, 65, 145), // shadow violet
			7 => new Color(255, 140, 50), // solar orange
			8 => new Color(100, 225, 255), // cyan
			9 => new Color(175, 125, 225), // ancient purple
			10 => new Color(220, 50, 70), // crimson red
			11 => new Color(255, 70, 70), // vanilla red string
			12 => new Color(255, 160, 50), // vanilla orange string
			13 => new Color(255, 225, 80), // vanilla yellow string
			14 => new Color(170, 255, 70), // vanilla lime string
			15 => new Color(70, 220, 100), // vanilla green string
			16 => new Color(70, 220, 180), // vanilla teal string
			17 => new Color(130, 200, 255), // vanilla sky blue string
			18 => new Color(90, 150, 255), // vanilla blue string
			19 => new Color(160, 120, 230), // vanilla purple string
			20 => new Color(185, 130, 235), // vanilla violet string
			21 => new Color(255, 175, 205), // vanilla pink string
			22 => new Color(30, 30, 35), // vanilla black string
			23 => new Color(150, 100, 60), // vanilla brown string
			24 => new Color(255, 255, 255), // rainbow handled as disco in PreDraw
			0 => headString.A == 0 ? new Color(255, 255, 255) : headString, // plain: head-matched, white fallback
			_ => new Color(255, 255, 255), // out-of-range safety
		};

		public static int TripStringDamage(int v) => v switch { 1 => 5, 2 => 1, _ => 0 };

		public static int TripStringBleed(int v) => v switch { 1 => 300, 2 => 180, _ => 0 };

			// Tether obtain text (also used as the lock reason in TripStringUnlocked).
			public static string TripStringObtain(int v) => System.Math.Clamp(v, 0, 24) switch {
			1 => "Tripwire Lv1 upgrade.",
			2 => "Tripwire Lv1 upgrade.",
			8 => "Tripwire Lv2 + Lab experiment: Cyan String x1 + anything.",
			9 => "Tripwire Lv3 + Lab experiment: Ancient Scraps x5 + anything.",
			10 => "Tripwire Lv3 + Lab experiment: Crimtane Bar x5 + anything.",
			>= 11 => "Tripwire Lv2 + Lab experiment: " + TripStringName(v) + " String x1 + anything.",
			>= 3 => "Tripwire Lv2 upgrade.",
			_ => "Standard issue.",
			};

		// Tripwire tether unlocks: Plain always, Barbed/Tangled at Lv1, the
		// five material colors at Lv2, Cyan/vanilla dyes at Lv2+Lab, Ancient/Crimson at Lv3+Lab.
		public static bool TripStringUnlocked(int idx, TrapState s, out string why)
		{
			why = "";
			int lv = s?.Level ?? 0;
			int i = System.Math.Clamp(idx, 0, 24);
			if (i == 0)
				return true;
			if ((i == 1 || i == 2) && lv < 1) { why = "Needs tripwire Lv1."; return false; }
			if (i >= 3 && i <= 7 && lv < 2) { why = "Needs tripwire Lv2."; return false; }
			if (i == 8 && (lv < 2 || s == null || (s.ExpHeads & (1 << 8)) == 0)) { why = TripStringObtain(i); return false; }
			if ((i == 9 || i == 10) && lv < 3) { why = TripStringObtain(i); return false; }
			if (i == 9 && (s == null || (s.ExpHeads & (1 << 9)) == 0)) { why = TripStringObtain(i); return false; }
			if (i == 10 && (s == null || (s.ExpHeads & (1 << 10)) == 0)) { why = TripStringObtain(i); return false; }
			if (i >= 11 && (lv < 2 || s == null || (s.ExpHeads & (1 << i)) == 0)) { why = TripStringObtain(i); return false; }
			return true;
		}

		// Sentry tether colors (visual only): White default + link palette.
		public static string SentryStringName(int v) => System.Math.Clamp(v, 0, 7) switch {
			1 => "Cotton",
			2 => "Poly-Cotton",
			3 => "Polyester",
			4 => "Nylon",
			5 => "Kevlar",
			6 => "Shadow",
			7 => "Solar",
			_ => "White",
		};

		public static Color SentryStringColor(int v) => System.Math.Clamp(v, 0, 7) switch {
			1 => new Color(235, 235, 235),
			2 => new Color(70, 220, 90),
			3 => new Color(80, 150, 255),
			4 => new Color(255, 220, 70),
			5 => new Color(190, 130, 80),
			6 => new Color(150, 120, 220),
			7 => new Color(255, 140, 50),
			_ => new Color(255, 255, 255),
		};

		// Yoyo head style unlocks: main heads purely by trap upgrade (one per
		// level L0..L4); exclusive heads (Dusk/Glacier/Blood Tithe) purely by
		// Lab experiments. Selection-time gates (UI); the server re-checks
		// level + bits, so a style picked on one trap stays valid.
		public static bool TripStyleUnlocked(int idx, Point tile, TrapState s, Player player, out string why)
		{
			why = "";
			int lv = s?.Level ?? 0;
			switch (System.Math.Clamp(idx, 0, 7)) {
				case 1:
					if (lv < 1) { why = "Needs tripwire Lv1 (one head per upgrade)."; return false; }
					return true;
				case 2:
					if (lv < 2) { why = "Needs tripwire Lv2 (one head per upgrade)."; return false; }
					return true;
				case 4:
					if (lv < 3) { why = "Needs tripwire Lv3 (one head per upgrade)."; return false; }
					return true;
				case 3:
					if (lv < 4) { why = "Needs tripwire Lv4 (one head per upgrade)."; return false; }
					return true;
				case 5:
					if (s == null || (s.ExpHeads & 32) == 0) { why = "Experiment: Black Lens x1 + anything (Lab tab)."; return false; }
					return true;
				case 6:
					if (s == null || (s.ExpHeads & 64) == 0) { why = "Experiment: Ice x50 + anything (Lab tab)."; return false; }
					return true;
				case 7:
					if (s == null || (s.ExpHeads & 128) == 0) { why = "Experiment: Vertebrae x10 + anything (Lab tab)."; return false; }
					return true;
				default: return true;
			}
		}

		// Aegis power sharing: donors (stance 1, above half cell) trickle 5
		// power per 5s into the nearest needy (stance 2, below half) sharing
		// one Aegis mesh. Slow on purpose — a grid, not a battery.
		private static void TickShare(Point key, TrapState s)
		{
			if (s.ShareMode != 1 || !s.Enabled)
				return;
			if (!AegisCovered(key))
				return;
			if (Main.GameUpdateCount % 300 != 0)
				return;
			if (s.Energy <= s.MaxEnergy / 2)
				return;
			Point best = default;
			int bestD = int.MaxValue;
			bool found = false;
			foreach (var kv in Traps) {
				if (kv.Key == key)
					continue;
				var o = kv.Value;
				if (!IsDefensive(o.Kind) || o.ShareMode != 2 || !o.Enabled)
					continue;
				if (!AegisCovered(kv.Key))
					continue;
				if (!Content.Placeables.StationNetwork.SharesAegis(key, kv.Key))
					continue;
				if (o.Energy >= o.MaxEnergy / 2)
					continue;
				int dx = kv.Key.X - key.X, dy = kv.Key.Y - key.Y;
				int d = dx * dx + dy * dy;
				if (d < bestD) { bestD = d; best = kv.Key; found = true; }
			}
			if (!found || !Traps.TryGetValue(best, out var needy))
				return;
			int amt = System.Math.Min(5, s.Energy - s.MaxEnergy / 2);
			amt = System.Math.Min(amt, needy.MaxEnergy - needy.Energy);
			if (amt <= 0)
				return;
			s.Energy -= amt;
			needy.Energy += amt;
			SyncWorld();
		}

		// ---- Entity classification for Tripwire (six categories) ----
		// Town: townNPC. Hostile: enemy (!friendly,!townNPC, damage>0, lifeMax>5).
		// Friendly: friendly && !townNPC. Ordinary NPC: active && !town && !hostile && !friendly.
		// All: master override allowing any otherwise-valid entity.
		public static bool ClassifyNPC(NPC npc, out string category)
		{
			category = "npc";
			if (npc == null || !npc.active || npc.life <= 0) {
				category = "invalid";
				return false;
			}
			if (npc.townNPC) { category = "town"; return true; }
			bool hostile = !npc.friendly && !npc.townNPC && npc.damage > 0 && npc.lifeMax > 5 && !Terraria.ID.NPCID.Sets.CountsAsCritter[npc.type];
			if (hostile) { category = "hostile"; return true; }
			if (npc.friendly && !npc.townNPC) { category = "friendly"; return true; }
			category = "npc";
			return true;
		}

		public static bool TripwireTriggerEligible(TrapState s, Player player)
		{
			if (player == null || !player.active || player.dead)
				return false;
			if (s.HitAll) return true;
			return s.HitPlayers;
		}

		public static bool TripwireTriggerEligible(TrapState s, NPC npc)
		{
			if (npc == null || !npc.active || npc.life <= 0 || npc.dontTakeDamage)
				return false;
			if (s.HitAll) return true;
			if (!ClassifyNPC(npc, out string cat))
				return false;
			return cat switch {
				"town" => s.HitTown,
				"hostile" => s.HitHostile,
				"friendly" => s.HitFriendly,
				_ => s.HitNPCs,
			};
		}

		// Sentry target eligibility (same six-toggle semantics as Tripwire).
		// OFF categories are never aimed at or fired upon.
		public static bool SentryEligible(TrapState s, NPC n)
		{
			if (n == null || !n.active || n.life <= 0 || n.dontTakeDamage)
				return false;
			if (s.HitAll) return true;
			if (!ClassifyNPC(n, out string cat))
				return false;
			return cat switch {
				"town" => s.HitTown,
				"hostile" => s.HitHostile,
				"friendly" => s.HitFriendly,
				_ => s.HitNPCs,
			};
		}

		// Sentry vs players (PvP mode): aimed and fired upon only when the
		// Players filter (or ALL override) is ON. Damage itself is gated in
		// CanHitPlayer (never the trap owner).
		public static bool SentryEligiblePlayer(TrapState s, Player p)
		{
			if (p == null || !p.active || p.dead)
				return false;
			if (s.HitAll) return true;
			return s.HitPlayers;
		}

		// String Link victim eligibility. OFF categories pass through the rope
		// untouched: no damage, no restraint hold, and no stress contribution.
		public static bool LinkEligiblePlayer(TrapState s, Player p)
		{
			if (p == null || !p.active || p.dead)
				return false;
			if (s.HitAll) return true;
			return s.HitPlayers;
		}

		public static bool LinkEligibleNPC(TrapState s, NPC n)
		{
			if (n == null || !n.active || n.life <= 0 || n.dontTakeDamage)
				return false;
			if (s.HitAll) return true;
			if (!ClassifyNPC(n, out string cat))
				return false;
			return cat switch {
				"town" => s.HitTown,
				"hostile" => s.HitHostile,
				"friendly" => s.HitFriendly,
				_ => s.HitNPCs,
			};
		}

		// ---- Server tick ----
		public override void PostUpdateEverything()
		{
			bool sim = Main.netMode != NetmodeID.MultiplayerClient;
			if (!sim)
				return;
			// Wildstring-style link ropes advance every tick (cheap: few pairs).
			try { LinkRope.StepAllSim(); } catch { }
			// Stagger scans: idle traps tick every 12 ticks by hash; live (wired,
			// unfrayed, enabled) Link barriers tick every 4 for responsive holds.
			// Balance rates below scale by step so wall-clock costs stay identical.
			int tick12 = (int)(Main.GameUpdateCount % 12);
			int tick4 = (int)(Main.GameUpdateCount % 4);
			var keys = new List<Point>(Traps.Keys);
			foreach (var key in keys) {
				if (!Traps.TryGetValue(key, out var s))
					continue;
				if (!IsAnyTrapTile(key))
					continue; // ghost cleanup lazily (kept until break forgets)
				int hash = Math.Abs(key.X * 7 + key.Y * 13 + (int)s.Kind);
				int step = 12;
				if (s.Kind == TrapKind.Link && s.Linked && s.WiredActive && s.Frayed <= 0 && s.Enabled)
					step = 4;
				if ((step == 4 ? hash % 4 : hash % 12) != (step == 4 ? tick4 : tick12))
					continue;
				// Parked (disabled) traps freeze upkeep: no wear, no regen, no
				// cooldown ticking. Service still works; recovery still cools
				// inside the pair tick below.
				if (s.Enabled) {
				// Maintenance wear (defensive machines only): ~1% per full ingame day.
				if (s.Kind != TrapKind.WildSnare && s.Kind != TrapKind.Bearing && s.Condition > 0) {
					s.CondTick += step;
					if (s.CondTick >= 86400) { s.CondTick = 0; s.Condition = Math.Max(0, s.Condition - 1); SyncWorld(); }
				}
				// Energy regen: ~2.5/s base, +5/s meshed (per-kind mesh).
				if (s.Energy < s.MaxEnergy) {
					int gain = (Main.GameUpdateCount % 24 == 0) ? 1 : 0;
					if (MeshedForKind(key, s.Kind))
						gain += 1;
					// Aegis overdrive burns engine charge for trap output; the
					// trap itself still regens normally here.
					s.Energy = Math.Min(s.MaxEnergy, s.Energy + gain);
				}
				if (s.Cooldown > 0)
					s.Cooldown -= step;
				if (s.ActiveTimer > 0) {
					s.ActiveTimer -= step;
					if (s.ActiveTimer <= 0 && s.Kind == TrapKind.Link) {
						// timed barrier expiry only if Duration-based mode; wiring toggle otherwise persists
					}
				}
				}
				try {
					switch (s.Kind) {
						case TrapKind.WildSnare: TickSnare(key, s); break;
						case TrapKind.Bearing: TickBearing(key, s); break;
						case TrapKind.Sentry: TickSentry(key, s); break;
						case TrapKind.Link: TickLink(key, s); break;
						case TrapKind.Tripwire: TickTripwire(key, s); break;
					}
					TickShare(key, s);
				}
				catch { /* never crash world tick */ }
			}
			try { NotePositions(); } catch { }
		}

		private static void TickSnare(Point key, TrapState s)
		{
			if (s.Cooldown > 0 || s.ActiveTimer > 0)
				return;
			if (s.Energy < s.EnergyCost)
				return;
			Vector2 center = TileCenter(key);
			int rangePx = EffectiveRangeTiles(key, s) * 16;
			Player target = null;
			foreach (var p in Main.player) {
				if (p == null || !p.active || p.dead)
					continue;
                if (Vector2.DistanceSquared(p.Center, center) <= rangePx * rangePx && Collision.CanHitLine(center, 0, 0, p.Center, 0, 0)) {
					target = p;
					break;
				}
			}
			if (target == null)
				return;
			int variant = Math.Min(PickSnareVariant() + (s.Level >= 1 ? 1 : 0), 4);
			variant = Math.Min(variant, 4);
			// Clamp to progression pool + level bonus (level adds range of choice, not beyond max)
			variant = Math.Min(variant, Math.Min(4, MaxStringVariant() + (s.Level >= 2 ? 1 : 0)));
			int dmg = (EffectiveDamage(key, s) + StringDamage(variant)) / 2;
			dmg = Math.Max(5, dmg);
			int dur = s.Duration + s.Level * 120;
			// Overcharge malfunction: 15% chance to backfire.
			if (s.Overcharge && !TrapActions.LicensedNoBackfire(key) && Main.rand.NextFloat() < 0.15f) {
				s.Cooldown = EffectiveCooldown(key, s) * 2;
				s.Energy = Math.Max(0, s.Energy - s.EnergyCost);
				if (!Main.dedServ) {
					for (int i = 0; i < 12; i++)
						Dust.NewDust(center - new Vector2(8, 8), 16, 16, DustID.Smoke, 0f, -1f, 100, default, 1.2f);
				}
				return;
			}
			var src = new EntitySource_Misc("WildSnare");
			int proj = Projectile.NewProjectile(src, center, Vector2.Zero,
				ModContent.ProjectileType<SnareStringProj>(), dmg, 2f, Main.myPlayer,
				// Per-activation direction spread (synced): each deployment spins
				// either way; speed comes from the real string table, untouched.
				// into ai1 (synced) so every machine simulates the same spin.
				ai0: variant, ai1: Main.rand.NextBool() ? 1f : -1f);
			if (proj >= 0 && proj < Main.maxProjectiles) {
				Main.projectile[proj].timeLeft = dur;
				// Snap to exact trap center (spawn position semantics differ across versions).
				Main.projectile[proj].Center = center;
				Main.projectile[proj].localAI[0] = center.X;
				Main.projectile[proj].localAI[1] = center.Y;
				Main.projectile[proj].netUpdate = true;
			}
			s.Energy -= s.EnergyCost;
			s.Cooldown = EffectiveCooldown(key, s);
			s.ActiveTimer = Math.Min(dur, 600);
			s.Uses++;
			TrapActions.CheckOffensiveAutoUpgrade(key);
			SyncWorld();
		}

		private static void TickBearing(Point key, TrapState s)
		{
			if (s.Cooldown > 0)
				return;
			if (s.Energy < s.EnergyCost)
				return;
			Vector2 center = TileCenter(key);
			int rangePx = EffectiveRangeTiles(key, s) * 16;
			Player target = null;
			float best = float.MaxValue;
			foreach (var p in Main.player) {
				if (p == null || !p.active || p.dead)
					continue;
				float d = Vector2.DistanceSquared(p.Center, center);
				if (d <= rangePx * rangePx && d < best && Collision.CanHitLine(center, 0, 0, p.Center, 0, 0)) {
					best = d; target = p;
				}
			}
			if (target == null)
				return;
			int burst = s.Level switch { 1 => 2, 2 => 3, _ => 1 };
			int interval = s.Level switch { 1 => 70, 2 => 50, _ => 90 };
			float speed = 9f + s.Level;
			int dmg = EffectiveDamage(key, s) + BearingDamage(s.BearingTier);
			dmg = Math.Max(8, dmg / 2);
			if (s.Overcharge && !TrapActions.LicensedNoBackfire(key) && Main.rand.NextFloat() < 0.15f) {
				s.Cooldown = EffectiveCooldown(key, s) * 2;
				s.Energy = Math.Max(0, s.Energy - s.EnergyCost);
				return;
			}
			var src = new EntitySource_Misc("BearingTrap");
			for (int i = 0; i < burst; i++) {
				Vector2 dir = (target.Center - center).SafeNormalize(Vector2.UnitX);
				// slight spread for burst
				dir = dir.RotatedBy((i - (burst - 1) / 2f) * 0.12f);
				int proj = Projectile.NewProjectile(src, center, dir * speed,
					ModContent.ProjectileType<BearingTrapShot>(), dmg, 2f, Main.myPlayer);
				if (proj >= 0 && proj < Main.maxProjectiles) {
					Main.projectile[proj].netUpdate = true;
					Main.projectile[proj].Center = center;
					// Lifetime matches effective range: no cross-map flyers.
					Main.projectile[proj].timeLeft = System.Math.Clamp((int)(rangePx / System.Math.Max(1f, speed)) + 40, 60, 180);
				}
			}
			s.Energy -= s.EnergyCost;
			s.Cooldown = Math.Max(interval, EffectiveCooldown(key, s) / 3);
			s.Uses++;
			TrapActions.CheckOffensiveAutoUpgrade(key);
			SyncWorld();
		}

		public static Vector2 SentryMountWorld(Point key, TrapState s)
		{
			// Single centered pivot (measured art: head opaque bbox 50x18,
			// visual center = texture center (25,11); base top at foot-24).
			// Socket mount buries the head bbox 2px: no float, no deep bury,
			// mount X = base top-center X so horizontal stays centered while
			// the head spins in place. SentryAnchor kept for save compat only.
			Vector2 foot = new Vector2(key.X * 16f + 8f, key.Y * 16f + 16f);
			return foot - new Vector2(0f, 30f);
		}

		// Muzzle tip in world coords (barrel end along aim). Shots spawn here
		// so they emerge from the barrel instead of the head center.
		public static Vector2 SentryMuzzleWorld(Point key, TrapState s, Vector2 aimDir)
		{
			Vector2 mount = SentryMountWorld(key, s);
			if (aimDir.LengthSquared() < 0.001f)
				aimDir = Vector2.UnitX;
			aimDir = aimDir.SafeNormalize(Vector2.UnitX);
			return mount + aimDir * 23f; // head bbox half-width 25, 2px inside tip
		}

		private static void UpdateSentryAim(Point key, TrapState s, Vector2 center)
		{
			float best = float.MaxValue;
			bool found = false;
			float bestAng = s.AimAngle;
			int rangePx = (EffectiveRangeTiles(key, s) + 6) * 16;
			Vector2 mount = SentryMountWorld(key, s);
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC n = Main.npc[i];
				if (!SentryEligible(s, n))
					continue;
				float d = Vector2.DistanceSquared(n.Center, mount);
				if (d > rangePx * rangePx)
					continue;
				if (d < best) { best = d; bestAng = (n.Center - mount).ToRotation(); found = true; }
			}
			// PvP preview: aim the nearest eligible player too.
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player pl = Main.player[i];
				if (!SentryEligiblePlayer(s, pl))
					continue;
				float d = Vector2.DistanceSquared(pl.Center, mount);
				if (d > rangePx * rangePx)
					continue;
				if (!Collision.CanHitLine(mount, 0, 0, pl.Center, 0, 0))
					continue;
				if (d < best) { best = d; bestAng = (pl.Center - mount).ToRotation(); found = true; }
			}
			if (found) {
				// Normalize to [-Pi, Pi] so >180 deg wraps instead of spinning.
				while (bestAng > MathHelper.Pi) bestAng -= MathHelper.TwoPi;
				while (bestAng < -MathHelper.Pi) bestAng += MathHelper.TwoPi;
				if (bestAng != s.AimAngle) { s.AimAngle = bestAng; }
			}
		}

		private static void TickSentry(Point key, TrapState s)
		{
			if (!s.Enabled)
				return;
			// Schedule: off-phase traps hold fire (upkeep still ticks).
			if (s.Sched != 0) {
				int per = s.PulsePeriod == 10 ? 10 : s.PulsePeriod == 60 ? 60 : 30;
				bool wantFire = s.Sched switch {
					1 => Main.dayTime,
					2 => !Main.dayTime,
					_ => (Main.GameUpdateCount / (per * 60)) % 2 == 0,
				};
				if (!wantFire)
					return;
			}
			Vector2 center0 = SentryMountWorld(key, s);
			UpdateSentryAim(key, s, center0);
			// Aegis Broadcast (unique option): powered Endurance aura for nearby
			// players. Aegis-gated, drains power while feeding anyone.
			if (s.SentryAegis && AegisCovered(key) && s.Energy >= 1) {
				int auraPx = EffectiveRangeTiles(key, s) * 16;
				bool fed = false;
				foreach (var pl in Main.player) {
					if (pl == null || !pl.active || pl.dead)
						continue;
					if (Vector2.DistanceSquared(pl.Center, center0) > auraPx * auraPx)
						continue;
					pl.AddBuff(BuffID.Endurance, 150, false);
					fed = true;
				}
				if (fed)
					s.Energy = Math.Max(0, s.Energy - 1);
			}
			if (s.Cooldown > 0)
				return;
			if (s.Energy < s.EnergyCost)
				return;
			Vector2 center = SentryMountWorld(key, s);
			int rangePx = EffectiveRangeTiles(key, s) * 16;
			NPC target = null;
			Player targetP = null;
			// Focus fire (Aegis combined arms): most-recently-snared eligible
			// NPC first. Falls through to normal priority when nothing is held.
			if (s.Priority == 2 && AegisCovered(key)) {
				int bestAgo = int.MaxValue;
				float bestD = float.MaxValue;
				foreach (var n in Main.npc) {
					if (!SentryEligible(s, n))
						continue;
					float d = Vector2.DistanceSquared(n.Center, center);
					if (d > rangePx * rangePx)
						continue;
					if (!Collision.CanHitLine(center, 0, 0, n.Center, 0, 0))
						continue;
					int ago = LinkHeldAgo(n.whoAmI);
					if (ago < 0 || ago > 300)
						continue;
					if (ago < bestAgo || (ago == bestAgo && d < bestD)) { bestAgo = ago; bestD = d; target = n; }
				}
			}
			if (target == null && targetP == null) {
			if (s.Priority == 1) {
				int bestLife = -1;
				foreach (var n in Main.npc) {
					if (!SentryEligible(s, n))
						continue;
					if (Vector2.DistanceSquared(n.Center, center) > rangePx * rangePx)
						continue;
					if (!Collision.CanHitLine(center, 0, 0, n.Center, 0, 0))
						continue;
					if (n.lifeMax > bestLife) { bestLife = n.lifeMax; target = n; }
				}
				// PvP: strongest may be a player.
				foreach (var pl in Main.player) {
					if (!SentryEligiblePlayer(s, pl))
						continue;
					if (Vector2.DistanceSquared(pl.Center, center) > rangePx * rangePx)
						continue;
					if (!Collision.CanHitLine(center, 0, 0, pl.Center, 0, 0))
						continue;
					if (pl.statLifeMax > bestLife) { bestLife = pl.statLifeMax; target = null; targetP = pl; }
					else if (pl.statLifeMax == bestLife && target == null) { targetP = pl; }
				}
			}
			else {
				float best = float.MaxValue;
				foreach (var n in Main.npc) {
					if (!SentryEligible(s, n))
						continue;
					float d = Vector2.DistanceSquared(n.Center, center);
					if (d > rangePx * rangePx)
						continue;
					if (!Collision.CanHitLine(center, 0, 0, n.Center, 0, 0))
						continue;
					if (d < best) { best = d; target = n; targetP = null; }
				}
				foreach (var pl in Main.player) {
					if (!SentryEligiblePlayer(s, pl))
						continue;
					float d = Vector2.DistanceSquared(pl.Center, center);
					if (d > rangePx * rangePx)
						continue;
					if (!Collision.CanHitLine(center, 0, 0, pl.Center, 0, 0))
						continue;
					if (d < best) { best = d; target = null; targetP = pl; }
				}
			}
			}
			if (target == null && targetP == null)
				return;
			// Limit active attacks: max 1 sentry projectile per trap.
			for (int i = 0; i < Main.maxProjectiles; i++) {
				var pr = Main.projectile[i];
				if (pr.active && pr.type == ModContent.ProjectileType<SentryCounterweightProj>()
					&& pr.ai[0] == key.X && pr.ai[1] == key.Y)
					return;
			}
			var cdef = CounterOf(s.CounterType);
			// Outward behavior: +25% damage on the way out (Both gets no bonus).
			// Fueled automation (AutoOC): overcharged while fueled at 2% risk.
			bool ocAuto = !s.Overcharge && s.AutoOC && AegisFueled(key);
			int dmg = (int)((EffectiveDamageRaw(key, s) + cdef.Dmg) * (OverchargeActive(key, s) ? 1.5f : 1f) * (s.Behavior == 0 ? 1.25f : 1f));
			if (OverchargeActive(key, s) && !TrapActions.LicensedNoBackfire(key) && Main.rand.NextFloat() < OverchargeRisk(key, s)) {
				s.Cooldown = EffectiveCooldown(key, s) * 2;
				s.Energy = Math.Max(0, s.Energy - s.EnergyCost - (ocAuto ? 5 : 0));
				return;
			}
			Vector2 mount = SentryMountWorld(key, s);
			Vector2 aimAt = target != null ? target.Center : targetP.Center;
			Vector2 dir = (aimAt - mount).SafeNormalize(Vector2.UnitX);
			var src = new EntitySource_Misc("Sentry");
			Vector2 muzzle = SentryMuzzleWorld(key, s, dir);
			int proj = Projectile.NewProjectile(src, muzzle, dir * cdef.Speed,
				ModContent.ProjectileType<SentryCounterweightProj>(), dmg, 2f, Main.myPlayer,
				ai0: key.X, ai1: key.Y, ai2: target != null ? target.whoAmI : (targetP != null ? 1000 + targetP.whoAmI : -1));
			if (proj >= 0 && proj < Main.maxProjectiles) {
				Main.projectile[proj].netUpdate = true;
				Main.projectile[proj].Center = muzzle;
			}
			s.Energy -= s.EnergyCost + (ocAuto ? 5 : 0);
			s.Cooldown = EffectiveCooldown(key, s);
			s.Uses++;
			SyncWorld();
		}

		private static void TickLink(Point key, TrapState s)
		{
			// Only process each pair once (lexicographically smaller endpoint).
			if (!s.Linked)
				return;
			Point other = s.LinkPoint;
			if (key.X > other.X || (key.X == other.X && key.Y > other.Y))
				return;
			if (!Traps.TryGetValue(other, out var os))
				return;
			if (!IsAnyTrapTile(other))
				return;
			// Master switch: a disabled endpoint parks the pair (no barrier,
			// no drain, no strain) while fray/stress still cool down below.
			if (!s.Enabled || !os.Enabled) {
				s.WiredActive = false; os.WiredActive = false;
			}
			// Schedule (pair-wide, overrides manual wiring): Day arms by day,
			// Night by night, Pulse toggles every 30s. Frayed/disabled refuse.
			if (s.Sched != 0 && s.Frayed <= 0 && os.Frayed <= 0 && s.Enabled && os.Enabled) {
				int per = s.PulsePeriod == 10 ? 10 : s.PulsePeriod == 60 ? 60 : 30;
				bool want = s.Sched switch {
					1 => Main.dayTime,
					2 => !Main.dayTime,
					_ => (Main.GameUpdateCount / (per * 60)) % 2 == 0,
				};
				if (s.WiredActive != want) {
					s.WiredActive = want; os.WiredActive = want;
					SyncWorld();
				}
			}
			int svar = System.Math.Clamp(System.Math.Max(s.StringVariant, os.StringVariant), 0, 7);
			int maxStress = System.Math.Max(s.MaxStress(), os.MaxStress());
			int regenWeave = System.Math.Max(s.RegenWeave, os.RegenWeave);
			int step = (s.WiredActive && s.Frayed <= 0) ? 4 : 12;
			// Fray + stress tick down even while the barrier is off.
			// Regen weave speeds recovery; tension weave raises the ceiling
			// (Stress stays proportional: clamp, never auto-release).
			if (s.Frayed > 0 || os.Frayed > 0) {
				s.Frayed = System.Math.Max(0, s.Frayed - 12);
				os.Frayed = s.Frayed;
				// Slow stress bleed while frayed (rope rests, 1 per stagger).
				if (s.Stress > 0) { s.Stress = System.Math.Max(0, s.Stress - 1); os.Stress = s.Stress; }
				// Keep MP clients fresh on recovery milestones.
				if (s.Frayed == 0 || Main.GameUpdateCount % 60 == 0)
					SyncWorld();
				// Auto-Rewire: Aegis-gated auto re-engage after full recovery.
				// Manual reswitch is required otherwise (stays OFF).
				if (s.Frayed == 0 && s.Stress <= 0) {
					bool auto = (s.AutoRewire || os.AutoRewire) && AegisCovered(key) && AegisCovered(other);
					if (auto) {
						s.WiredActive = true; os.WiredActive = true;
						SyncWorld();
					}
				}
			}
			// Dangerous collision follows the wildstring-style rope curve, fetched
			// up front so the alarm sensor sees crossings even while idle.
			Vector2[] ropePts;
			try { ropePts = LinkRope.GetPoints(key, other, s.Bend); }
			catch { ropePts = null; }
			// Alarm behavior (either endpoint): sensor-only trip ping on any
			// eligible crossing — no damage, no hold, no wiring needed.
			if (ropePts != null && (s.Behavior == 3 || os.Behavior == 3))
				TickLinkAlarm(key, other, ropePts);
			// Latch behavior (either endpoint): first eligible touch slams the
			// barrier armed and it STAYS shut until rewired. Latched rope acts
			// as Both (damage + hold). Frayed/empty pairs refuse to latch.
			if (ropePts != null && !s.WiredActive && (s.Behavior == 4 || os.Behavior == 4)
				&& s.Frayed <= 0 && os.Frayed <= 0 && s.Energy > 0 && os.Energy > 0) {
				float latchPt = 0f;
				bool tripped = false;
				string latchWho = "";
				foreach (var pl in Main.player) {
					if (pl == null || !pl.active || pl.dead)
						continue;
					if (!LinkEligiblePlayer(s, pl) && !LinkEligiblePlayer(os, pl))
						continue;
					if (RopeTouched(ropePts, true, pl.whoAmI, pl.position, pl.Size, pl.velocity, 12f + System.Math.Min(26f, pl.velocity.Length() * 1.2f), ref latchPt)) { tripped = true; latchWho = "Player:" + pl.name; break; }
				}
				if (!tripped) {
					foreach (var n in Main.npc) {
						if (n == null || !n.active || n.life <= 0 || n.dontTakeDamage)
							continue;
						if (!LinkEligibleNPC(s, n) && !LinkEligibleNPC(os, n))
							continue;
						if (RopeTouched(ropePts, false, n.whoAmI, n.position, new Vector2(n.width, n.height), n.velocity, 12f + System.Math.Min(26f, n.velocity.Length() * 1.2f), ref latchPt)) {
							tripped = true;
							ClassifyNPC(n, out string latchCat);
							latchWho = (n.boss ? "BOSS " : "") + latchCat;
							break;
						}
					}
				}
				if (tripped) {
					s.WiredActive = true; os.WiredActive = true;
					s.Uses++; os.Uses++;
					LogAlarmTrip(LinkRope.Canonical(key, other), latchWho + " (latched)");
					if (!Main.dedServ) {
						Vector2 mid = (ropePts[0] + ropePts[ropePts.Length - 1]) * 0.5f;
						for (int q = 0; q < 12; q++) {
							Dust dd = Dust.NewDustDirect(mid - new Vector2(8, 8), 16, 16, DustID.Electric, 0f, -1f, 100, default, 1.2f);
							dd.noGravity = true;
						}
					}
					Terraria.Audio.SoundEngine.PlaySound(SoundID.Mech, (ropePts[0] + ropePts[ropePts.Length - 1]) * 0.5f);
					SyncWorld();
				}
			}
			if (!s.WiredActive) {
				int decay = 4 + System.Math.Clamp(regenWeave, 0, 3) * 2;
				if (s.Stress > 0 || os.Stress > 0) { s.Stress = System.Math.Max(0, s.Stress - decay); os.Stress = s.Stress; }
				return;
			}
			// Frayed lockout: a snapped rope cannot re-engage until it recovers.
			// Manual reswitch required (wire again); Auto-Rewire bypasses above.
			if (s.Frayed > 0) {
				s.WiredActive = false; os.WiredActive = false;
				// Smouldering recovery: a frayed rope smokes faintly while
				// it heals (client storytelling, zero gameplay).
				if (AmbientDust() && !Main.dedServ && ropePts != null && ropePts.Length > 1 && Main.rand.NextBool(12)) {
					Vector2 rp = ropePts[Main.rand.Next(ropePts.Length)];
					Dust sm = Dust.NewDustDirect(rp - new Vector2(2, 2), 4, 4, DustID.Smoke, 0f, -0.8f, 150, default, 0.9f);
					sm.noGravity = true;
				}
				SyncWorld();
				return;
			}
			// Active barrier drains energy from both endpoints slowly.
			if (s.Energy <= 0 || os.Energy <= 0) {
				s.WiredActive = false; os.WiredActive = false;
				SyncWorld();
				return;
			}
			if (Main.GameUpdateCount % 60 == 0) {
				s.Energy = System.Math.Max(0, s.Energy - 2);
				os.Energy = System.Math.Max(0, os.Energy - 2);
			}
			// Dangerous collision follows the wildstring-style rope curve (segment checks),
			// not a straight line, so damage matches the drawn string.
			// (ropePts fetched above for the alarm sensor; reuse it here.)
			if (ropePts == null)
				return;
			int dmg = (EffectiveDamage(key, s) + EffectiveDamage(other, os)) / 2;
			dmg = System.Math.Max(2, dmg / 2); // nerfed: restraint tool first
			// Strain-stiffening (nonlinear elasticity): the deeper the rope
			// is already loaded, the harder it shoves back per pixel of
			// penetration. Capped everywhere as before: never launches.
			float stiffK = 1f + 0.7f * System.Math.Max(s.StressPct(), os.StressPct()) / 100f;
			// Behavior mix rules (pair union): Damage legs (0/2/4) deal damage;
			// Restraint legs (1/2/4) hold; Alarm legs (3) only ping (see above).
			// Latch legs (4) act as Both once something trips them shut.
			bool doDmg = s.Behavior == 0 || s.Behavior == 2 || s.Behavior == 4 || os.Behavior == 0 || os.Behavior == 2 || os.Behavior == 4;
			bool doHold = s.Behavior == 1 || s.Behavior == 2 || s.Behavior == 4 || os.Behavior == 1 || os.Behavior == 2 || os.Behavior == 4;
			float point = 0f;
			bool hitSomething = false;
			int held = 0;
			float bulkBonus = 0f;
			float wSum = 0f;
			float wMax = 0f;
			float spdMax = 0f;
			float wCen = 0f; // span-weighted load (mid-span pushes bend hardest)
			float jerkSum = 0f; // suddenness load (per-victim approach deltas)
			float ripChunk = 0f; // breakthrough gouges awaiting application
			float pressBonus = 0f; // Hertzian pressure (small/fast pierces deeper)
			float approachSum = 0f; // struggle meter (weight x motion, gates grinding)
			foreach (var pl in Main.player) {
				// Pair-wide filters (either endpoint allows = counts).
				if (!LinkEligiblePlayer(s, pl) && !LinkEligiblePlayer(os, pl))
					continue;
				// Swept check (anti phase-through): fast movers are tested at
				// current + backtracked boxes along their velocity.
				if (!RopeTouched(ropePts, true, pl.whoAmI, pl.position, pl.Size, pl.velocity, 14f + System.Math.Min(26f, pl.velocity.Length() * 1.2f), ref point))
					continue;
				// Phase-save proof: single-box test at the CURRENT box only.
				// If it misses while the path/swept test above hit, this victim
				// would have tunnelled without the anti-phase system.
				{
					float singlePt = 0f;
					if (!RopeHits(ropePts, pl.position, pl.Size, 14f + System.Math.Min(26f, pl.velocity.Length() * 1.2f), ref singlePt))
						PhaseIntercepts++;
				}
				// Immunity frames gate DAMAGE only — restraint never drops out.
				if (doDmg && !pl.immune)
					pl.Hurt(PlayerDeathReason.ByCustomReason(Terraria.Localization.NetworkText.FromLiteral("was cut by a String Link.")), dmg / 2, 0);
				hitSomething = true;
				// Push dynamics sampled BEFORE any resolve below moves the
				// body: approach speed, jerk vs last scan, breakthrough, and
				// span position. Resolve steering must not see these coming.
				Vector2 pushContactP = ClosestRopePoint(ropePts, pl.Center);
				float approachP = ApproachSpeed(pl.velocity, pl.Center, pushContactP);
				bool rippedP = false;
				approachSum += 1.5f * approachP;
				if (doHold && approachP > 0.01f) {
					rippedP = CheckRip(1.5f, approachP, svar, -1000 - pl.whoAmI, out float jerkP);
					jerkSum += jerkP * 1.5f;
					if (rippedP) {
						ripChunk += RipChunkFor(1.5f, approachP, svar);
						RipFeedback(pushContactP);
					}
					else {
						wCen += 1.5f * CenterFactor(ropePts[0], ropePts[ropePts.Length - 1], pushContactP);
					}
				}
				Vector2 prePosP = pl.position;
				Vector2 preVelP = pl.velocity;
				if (doHold) {
					int rest = System.Math.Max(s.Restraint, os.Restraint);
					pl.AddBuff(BuffID.Slow, rest * 60, false);
					if (!rippedP) {
						wSum += 1.5f; wMax = System.Math.Max(wMax, 1.5f);
					}
					spdMax = System.Math.Max(spdMax, pl.velocity.Length());
					// Finger-on-string resolve (position-based): the rope surface
					// is SOLID - project the body out to it first, then answer
					// velocity. Never drags across (no launches, no pop-through).
					Vector2 contactP = ClosestRopePoint(ropePts, pl.Center);
					Vector2 Pc = pl.Center;
					Vector2 outP = Pc - contactP;
					float dlP = outP.Length();
					float bodyRP = System.Math.Max(pl.width, pl.height) * 0.5f;
					float penP = bodyRP + 7f - dlP;
					if (penP > 0f) {
						Vector2 pushP = dlP > 0.001f ? outP / dlP : new Vector2(0f, -1f);
						if (dlP < bodyRP * 0.35f && pl.velocity.LengthSquared() > 0.01f)
						pushP = -pl.velocity.SafeNormalize(Vector2.Zero);
						pl.position += pushP * System.Math.Min((penP + 1f) * stiffK, 14f);
						Pc = pl.Center;
					}
					Vector2 tangentP = RopeTangentAt(ropePts, contactP);
					float vTanP = Vector2.Dot(pl.velocity, tangentP);
					Vector2 vNormP = pl.velocity - tangentP * vTanP;
					Vector2 awayP = Pc - contactP;
					float alP = awayP.Length();
					Vector2 nrmP = alP > 0.001f ? awayP / alP : new Vector2(0f, -1f);
					float vIntoP = -Vector2.Dot(vNormP, nrmP);
					if (vIntoP > 0f)
						vNormP += nrmP * vIntoP * 0.95f;
					float muP = 0.30f + 0.05f * System.Math.Clamp(svar, 0, 7);
					pl.velocity = tangentP * vTanP * System.Math.Max(0f, 1f - muP) + vNormP;
					if (penP > 0f)
						pl.velocity += nrmP * System.Math.Min(3f, penP * 0.12f);
					if (pl.velocity.Length() > 9f)
						pl.velocity = pl.velocity.SafeNormalize(Vector2.Zero) * 9f;
					if (!rippedP && !Main.dedServ) { for (int q = 0; q < 6; q++) { Dust dd = Dust.NewDustDirect(pl.position, pl.width, pl.height, DustID.MagicMirror, 0f, -1f, 100, default, 1.1f); dd.noGravity = true; } }
					if (rippedP) {
						// Tore straight through: restraint never took hold,
						// so unwind the resolve above (dust already spent).
						pl.position = prePosP;
						pl.velocity = preVelP;
					}
					// The shove above teleports: never leave a body embedded
					// in solid tiles to flail (walk back out, then up).
					UnstickFromTiles(pl);
				}
				// Barbed Wrap track: bites poison longer per level (3s +1s).
				int wrapLvlP = System.Math.Max(s.LinkBarb, os.LinkBarb);
				if (wrapLvlP > 0)
					pl.AddBuff(BuffID.Poisoned, 180 + 60 * (wrapLvlP - 1), false);
			}
			foreach (var n in Main.npc) {
				// OFF categories pass through untouched: no damage, no hold,
				// no stress (filtered before any contact response).
				// Pair-wide: either endpoint allowing counts (mirrored configs).
				if (!LinkEligibleNPC(s, n) && !LinkEligibleNPC(os, n))
					continue;
				// Damage and buffs still land on a dummy; the position solve
				// below does not. A hand-written position/velocity nudge is the
				// one motion source GlobalNPC cannot stop after the fact.
				bool pinnedDummy = DummySafety.Pin(n);
				if (!RopeTouched(ropePts, false, n.whoAmI, n.position, n.Size, n.velocity, 14f + System.Math.Min(26f, n.velocity.Length() * 1.2f), ref point))
					continue;
				{
					float singlePt = 0f;
					if (!RopeHits(ropePts, n.position, n.Size, 14f + System.Math.Min(26f, n.velocity.Length() * 1.2f), ref singlePt))
						PhaseIntercepts++;
				}
				if (doDmg) {
					int dir = n.Center.X - ropePts[0].X < 0f ? -1 : 1;
					n.StrikeNPC(new NPC.HitInfo { Damage = dmg, Knockback = 1f, HitDirection = dir }, false, false);
					if (n.life <= 0) {
						int prev = s.Kills;
						s.Kills++;
						CheckVeteranMilestone(key, s, prev);
						int prevO = os.Kills;
						os.Kills++;
						CheckVeteranMilestone(other, os, prevO);
					}
				}
				hitSomething = true;
				if (doHold && !pinnedDummy) {
					// Same push-dynamics sampling as players, before steering.
					Vector2 pushContactN = ClosestRopePoint(ropePts, n.Center);
					float approachN = ApproachSpeed(n.velocity, n.Center, pushContactN);
					float wN = StrainWeightFor(n);
					bool rippedN = false;
					approachSum += wN * approachN;
					if (approachN > 0.01f) {
						rippedN = CheckRip(wN, approachN, svar, n.whoAmI, out float jerkN);
						jerkSum += jerkN * wN;
						if (rippedN) {
							ripChunk += RipChunkFor(wN, approachN, svar);
							RipFeedback(pushContactN);
						}
						else {
							wCen += wN * CenterFactor(ropePts[0], ropePts[ropePts.Length - 1], pushContactN);
							// Hertzian pressure: small bodies concentrate
							// force (needle vs hammer) — fast small fry pierce
							// deeper than their weight alone suggests.
							pressBonus += wN * System.Math.Clamp((20f - System.Math.Max(n.width, n.height) * 0.5f) / 10f, 0f, 2f);
						}
					}
					Vector2 prePosN = n.position;
					Vector2 preVelN = n.velocity;
					int rest2 = System.Math.Max(s.Restraint, os.Restraint);
					Vector2 contact = ClosestRopePoint(ropePts, n.Center);
					float bulk = System.Math.Clamp(n.width * n.height / 2000f, 0.5f, 3f);
					if (bulk >= 2f)
						bulkBonus += 1f; // heavies amplify strain (IRL load)
					float w = StrainWeightFor(n);
					if (!rippedN) {
						wSum += w; wMax = System.Math.Max(wMax, w);
					}
					spdMax = System.Math.Max(spdMax, n.velocity.Length());
					bool flyer = IsFlyer(n);
					if (!rippedN && !n.boss) {
						// Finger-on-string resolve (position-based): project out
						// to the rope surface first (stiff strings fully, soft
						// strings yield), then kill into-rope velocity and apply
						// Coulomb-style tangent friction.
						float give = svar switch { 0 => 0.45f, 1 => 0.35f, 2 => 0.28f, 3 => 0.22f, 4 => 0.15f, 5 => 0.50f, 6 => 0.12f, 7 => 0.25f, _ => 0.30f };
						float stiffN = 1f - give * 0.5f;
						Vector2 Nc = n.Center;
						Vector2 outN = Nc - contact;
						float dl0 = outN.Length();
						float bodyRN = System.Math.Max(n.width, n.height) * 0.5f;
						float penN = bodyRN + 7f - dl0;
						if (penN > 0f) {
							Vector2 pushN = dl0 > 0.001f ? outN / dl0 : new Vector2(0f, -1f);
							if (dl0 < bodyRN * 0.35f && n.velocity.LengthSquared() > 0.01f)
							pushN = -n.velocity.SafeNormalize(Vector2.Zero);
							n.position += pushN * System.Math.Min((penN * stiffN + 1f) * stiffK, 14f / bulk);
							Nc = n.Center;
						}
						Vector2 tangent = RopeTangentAt(ropePts, contact);
						float vTan = Vector2.Dot(n.velocity, tangent);
						Vector2 vNorm = n.velocity - tangent * vTan;
						Vector2 awayN = Nc - contact;
						float alN = awayN.Length();
						Vector2 nrmN = alN > 0.001f ? awayN / alN : new Vector2(0f, -1f);
						float vIntoN = -Vector2.Dot(vNorm, nrmN);
						if (vIntoN > 0f)
							vNorm += nrmN * vIntoN * 0.95f;
						float muN = 0.30f + 0.05f * System.Math.Clamp(svar, 0, 7);
						n.velocity = tangent * vTan * System.Math.Max(0f, 1f - muN) + vNorm;
						if (flyer)
							n.velocity.Y += 0.2f;
						if (penN > 0f)
							n.velocity += nrmN * System.Math.Min(2f, penN * 0.06f / bulk);
						if (n.velocity.Length() > 8f)
							n.velocity = n.velocity.SafeNormalize(Vector2.Zero) * 8f;
					}
					else if (!rippedN) {
						// Bosses: no teleport, no yank — steer 20% toward the
						// rope, halve the rope-normal drift, cap at 12. They
						// still feel the restraint (plus Slow below) while
						// straining it at 6x weight.
						Vector2 tangentB = RopeTangentAt(ropePts, contact);
						float vTanB = Vector2.Dot(n.velocity, tangentB);
						Vector2 vNormB = n.velocity - tangentB * vTanB;
						Vector2 toCB = contact - n.Center;
						n.velocity = tangentB * vTanB * 0.95f + vNormB * 0.5f
							+ toCB.SafeNormalize(Vector2.Zero) * System.Math.Min(0.8f, (1f + svar * 0.25f) / bulk);
						if (n.velocity.Length() > 12f)
							n.velocity = n.velocity.SafeNormalize(Vector2.Zero) * 12f;
					}
					n.AddBuff(BuffID.Slow, (int)(rest2 * 60 * (1f + 0.25f * System.Math.Clamp(svar, 0, 7))), false);
					if (!rippedN && !Main.dedServ) { for (int q = 0; q < 6; q++) { Dust dd = Dust.NewDustDirect(contact - new Vector2(3, 3), 6, 6, DustID.MagicMirror, 0f, -1f, 100, default, 1.1f); dd.noGravity = true; } }
					if (rippedN) {
						// Tore straight through: unwind the resolve above so
						// momentum carries it past the rope (the clip).
						n.position = prePosN;
						n.velocity = preVelN;
					}
					else {
						NoteLinkHeld(n.whoAmI);
						held++;
					}
					UnstickFromTiles(n);
				}
				int wrapLvl = System.Math.Max(s.LinkBarb, os.LinkBarb);
				if (wrapLvl > 0)
					n.AddBuff(BuffID.Poisoned, 180 + 60 * (wrapLvl - 1), false);
				if (Main.netMode == NetmodeID.Server)
					NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, n.whoAmI);
			}
			// Proximity hold zone (anti walk-through): eligible bodies NEAR the rope
			// (not just touching it) stay slowed and steered back, so crossers get
			// re-caught every scan instead of strolling past between yanks. No damage,
			// no stress, no Uses: pure restraint.
			if (doHold && s.Frayed <= 0 && os.Frayed <= 0) {
				const float holdR = 48f;
				foreach (var pl in Main.player) {
					if (pl == null || !pl.active || pl.dead)
						continue;
					if (!LinkEligiblePlayer(s, pl) && !LinkEligiblePlayer(os, pl))
						continue;
					Vector2 hcp = ClosestRopePoint(ropePts, pl.Center);
					if (Vector2.DistanceSquared(pl.Center, hcp) > holdR * holdR)
						continue;
					int rest = System.Math.Max(s.Restraint, os.Restraint);
					pl.AddBuff(BuffID.Slow, rest * 60, false);
					Vector2 toC = hcp - pl.Center;
					if (toC.Length() > 4f) {
						pl.velocity += toC.SafeNormalize(Vector2.Zero) * 0.35f;
						if (pl.velocity.Length() > 9f)
						pl.velocity = pl.velocity.SafeNormalize(Vector2.Zero) * 9f;
					}
				}
				foreach (var n in Main.npc) {
					if (n == null || !n.active || n.life <= 0 || n.dontTakeDamage)
						continue;
					if (!LinkEligibleNPC(s, n) && !LinkEligibleNPC(os, n))
						continue;
					if (DummySafety.Pin(n))
						continue;
					Vector2 hcp2 = ClosestRopePoint(ropePts, n.Center);
					if (Vector2.DistanceSquared(n.Center, hcp2) > holdR * holdR)
						continue;
					int rest2 = System.Math.Max(s.Restraint, os.Restraint);
					n.AddBuff(BuffID.Slow, rest2 * 60, false);
					if (!n.boss) {
						Vector2 toC2 = hcp2 - n.Center;
						if (toC2.Length() > 4f) {
						n.velocity += toC2.SafeNormalize(Vector2.Zero) * (IsFlyer(n) ? 0.45f : 0.3f);
						float hcap = IsFlyer(n) ? 7f : 6f;
						if (n.velocity.Length() > hcap)
						n.velocity = n.velocity.SafeNormalize(Vector2.Zero) * hcap;
					}
					}
				}
			}
			// Stress (IRL finger-push): a taut string has no give, so the same
			// shove tensions it MORE than a slack one (lateral deflection
			// of a tensioned string: d ~= F*L/4T — low slack, high stress).
			// Mid-span pushes bend hardest (peak moment at center, like a
			// catenary sagging deepest mid-span). Sudden rushes add jerk
			// load on top of steady push; overwhelming impulse rips through
			// (see CheckRip) and gouges the rope below.
			// STATIC vs DYNAMIC (answers the stepping question): a still
			// load does NOT grind forever — it relaxes toward a static
			// equilibrium (deflection balance), capped only by true overload
			// creeping past ultimate. Only ACTION (struggle, rush, jerk,
			// misconfiguration) grinds past equilibrium toward a snap.
			int totalHeld = held;
			// NOTE: players strain through wSum (no separate counter needed).
			// Optimization cadence (config, default 1 = every scan): higher
			// values run this whole block — accumulation AND decay — less
			// often with proportionally bigger steps, so long-run behavior
			// matches while checks/sec drop. Grab, damage, hold, rip events
			// and timers above always run every scan (no phasing, ever).
			// Rip events force processing so breakthroughs never wait.
			int cadence = 1;
			try {
				cadence = System.Math.Clamp(ModContent.GetInstance<Configuration.YoyoServerConfig>().StrainCadence, 1, 4);
			}
			catch { }
			int estep = step * cadence;
			int phase = cadence > 1 ? System.Math.Abs(key.X * 7 + key.Y * 13) % cadence : 0;
			bool strainTick = ripChunk > 0f || cadence <= 1 || (int)(Main.GameUpdateCount % (uint)cadence) == phase;
			if ((totalHeld > 0 || wSum > 0f || ripChunk > 0f) && strainTick) {
				int baseRate = TrapState.StrainRateFor(svar);
				float tension = s.Bend == 0 ? 1.15f : s.Bend == 1 ? 1.0f : 0.8f;
				// Capstan (Euler–Eytelwein): rope grip around the endpoints
				// sheds load exponentially with wrap angle — slack, curvy
				// rope is gripped, taut straight rope takes it all.
				float capstan = CapstanFactor(svar, s.Bend);
				// Stick-slip: static grip alternates with kinetic slides, so
				// strain arrives in pulses that average out (phase 0 bites).
				float slip = ((Main.GameUpdateCount / 12u) % 4u == 0u) ? 1.6f : 0.8f;
				float load = wMax + 0.5f * (wCen - wMax);
				// Static equilibrium level: what this load would settle at if
				// nobody struggled. Stronger ropes sit cooler (same load is a
				// smaller fraction of ultimate). Uncapped on purpose: genuine
				// static overload (a crowd on cotton) creeps past max slowly.
				float equil = load * 70f * tension * capstan;
				// Activity gate (0..1): still bodies barely work the rope no
				// matter their weight; movers grind at full base rate.
				float activity = System.Math.Clamp(approachSum / 12f, 0f, 1f);
				// One rounding for the dynamic sum (fractions count).
				float dyn = (baseRate * load * tension * capstan * slip + bulkBonus) * activity
					+ System.Math.Min(6f, jerkSum / 10f) + spdMax / 6f + pressBonus;
				int bendMax = System.Math.Min(TrapState.MaxBendFor(s.StringVariant), TrapState.MaxBendFor(os.StringVariant));
				if (s.Bend > bendMax || os.Bend > bendMax)
					dyn += 3; // over-bent: fighting its own curvature
				// Static pull: FAST warm toward equilibrium, SLOW cool
				// (hysteresis — heating is work, cooling is dissipation).
				// Up step capped: true overload creeps with full warning.
				// All scalings below use estep (cadence-stretched scans stay
				// wall-clock equivalent); grab/damage/hold/rip above always
				// run every scan regardless of cadence.
				float kPull = (equil >= s.Stress ? 0.10f : 0.02f) * estep / 12f;
				float pull = (equil - s.Stress) * kPull;
				if (pull > 0f)
					pull = System.Math.Min(pull, (2f + maxStress / 60f) * estep / 12f);
				float next = s.Stress + pull + dyn * estep / 12f;
				s.Stress = System.Math.Min(maxStress, System.Math.Max(0, (int)System.Math.Round(next)));
				os.Stress = s.Stress;
				// Grind ledger + creep (material memory): sustained redline
				// sags the ceiling toward the stress (cyclic fatigue); cool
				// running heals it slowly. Transient per session.
				UpdateFatigue(key, other, s.Stress, maxStress, dyn, estep);
				int effMax = EffMaxFor(key, other, maxStress);
				if (ripChunk > 0f) {
					// Breakthrough gouge: capped so a rip hurts but never
					// snaps the rope outright (rupture-lite, not fray).
					s.Stress = System.Math.Min(maxStress, s.Stress + (int)System.Math.Min(maxStress / 4f, ripChunk));
					os.Stress = s.Stress;
				}
				// Strain sparks: a working rope sheds faint sparks along
				// its length, denser with load (visual echo of the bar).
				if (AmbientDust() && !Main.dedServ && ropePts != null && ropePts.Length > 1) {
					float heat = s.Stress / (float)System.Math.Max(1, maxStress);
					if (heat > 0.45f && Main.rand.NextBool((int)(34 - heat * 28))) {
						Vector2 rp = ropePts[Main.rand.Next(ropePts.Length)];
						Dust sk = Dust.NewDustDirect(rp - new Vector2(2, 2), 4, 4, DustID.Electric, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1.5f, -0.2f), 100, default, Main.rand.NextFloat(0.6f, 1f));
						sk.noGravity = true;
					}
				}
				if (s.Stress >= effMax) {
					s.WiredActive = false; os.WiredActive = false;
					int fray = TrapState.FrayTicksFor(svar, regenWeave);
					s.Frayed = fray; os.Frayed = fray;
					s.Uses++; os.Uses++;
					// IRL snapback: the break whips (visual shockwave follows
					// in the draw pass); victims are never launched by design.
					NoteSnapFx(key, other);
					// The rope splits in two: both halves recoil to their
					// anchors and dangle 5s (drawn in the unwired pass).
					// Bigger ropes whip harder (same scaling as debris/sound).
					LinkRope.SnapRope(key, other, ropePts, svar, System.Math.Min(1.6f, System.Math.Max(0.7f, maxStress / 200f)));
					// Whiplash crack (server-side): the snap itself stings
					// nearby at 30% pair damage, zero knockback — a crack,
					// never a launch. Same eligibility as the barrier, NPC
					// hits direct (barrier precedent), players gated by
					// immunity. Fray lockout is the natural cooldown.
					if (Main.netMode != NetmodeID.MultiplayerClient) {
						Vector2 mid = (ropePts[0] + ropePts[ropePts.Length - 1]) * 0.5f;
						int crackNpc = System.Math.Max(1, dmg * 30 / 100);
						int crackPl = System.Math.Max(1, dmg * 15 / 100);
						for (int ni = 0; ni < Main.maxNPCs; ni++) {
							NPC target = Main.npc[ni];
							if (target == null || !target.active || target.life <= 0 || target.dontTakeDamage)
								continue;
							if (!LinkEligibleNPC(s, target) && !LinkEligibleNPC(os, target))
								continue;
							if (Vector2.DistanceSquared(target.Center, mid) > 70f * 70f)
								continue;
							int dir = target.Center.X < mid.X ? -1 : 1;
							target.StrikeNPC(new NPC.HitInfo { Damage = crackNpc, Knockback = 0f, HitDirection = dir }, false, false);
						}
						for (int pi = 0; pi < Main.maxPlayers; pi++) {
							Player pl = Main.player[pi];
							if (pl == null || !pl.active || pl.dead || pl.immune)
								continue;
							if (!LinkEligiblePlayer(s, pl) && !LinkEligiblePlayer(os, pl))
								continue;
							if (Vector2.DistanceSquared(pl.Center, mid) > 70f * 70f)
								continue;
							pl.Hurt(PlayerDeathReason.ByCustomReason(Terraria.Localization.NetworkText.FromLiteral("was cracked by a snapping String Link.")), crackPl, 0);
						}
					}
					// No fling: restraint holds back, it never launches.
					// Snapback scales with the rope that let go: bigger max
					// stress throws more debris and cracks deeper.
					if (!Main.dedServ) {
						Vector2 mid = (ropePts[0] + ropePts[ropePts.Length - 1]) * 0.5f;
						int puff = 16 + maxStress / 20;
						for (int q = 0; q < puff; q++) { Dust dd = Dust.NewDustDirect(mid - new Vector2(8, 8), 16, 16, DustID.Smoke, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 0.5f), 100, default, 1.3f); dd.noGravity = true; }
					}
					Terraria.Audio.SoundEngine.PlaySound(SoundID.Dig, (ropePts[0] + ropePts[ropePts.Length - 1]) * 0.5f);
					Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, (ropePts[0] + ropePts[ropePts.Length - 1]) * 0.5f);
					if (maxStress >= 160)
						Terraria.Audio.SoundEngine.PlaySound(SoundID.Mech, (ropePts[0] + ropePts[ropePts.Length - 1]) * 0.5f);
					SyncWorld();
					return;
				}
			}
			else if ((s.Stress > 0 || os.Stress > 0) && strainTick) {
				// Viscoelastic relaxation: hot rope bleeds fast, the last
				// dregs linger (decay proportional to current stress, Wax
				// steepens the curve instead of adding flat ticks).
				int decay = System.Math.Max(1, (int)(System.Math.Max(s.Stress, os.Stress) * (0.02f + 0.01f * System.Math.Clamp(regenWeave, 0, 3)) * estep / 12f));
				s.Stress = System.Math.Max(0, s.Stress - decay); os.Stress = s.Stress;
			}
			// Ward Lattice (unique option): the live barrier also grounds minor
			// hostile shots crossing it (house rule: Orbi-Shield caps at 150 dmg).
			// Aegis-gated; Grounding Coil lowers the power cost (4/3/2/1).
			int zapCost = System.Math.Max(1, 4 - System.Math.Max(s.LinkCoil, os.LinkCoil));
			if (s.WiredActive && (s.WardLattice || os.WardLattice) && AegisCovered(key) && (s.Energy >= zapCost || os.Energy >= zapCost)) {
				for (int pi = 0; pi < Main.maxProjectiles; pi++) {
					Projectile shot = Main.projectile[pi];
					if (shot == null || !shot.active || !shot.hostile || shot.friendly || shot.damage <= 0 || shot.damage > 150)
						continue;
					float sp = 0f;
					if (!RopeHitsSwept(ropePts, shot.position, new Vector2(shot.width, shot.height), shot.velocity, 14f, ref sp))
						continue;
					if (s.Energy >= os.Energy) s.Energy = System.Math.Max(0, s.Energy - zapCost); else os.Energy = System.Math.Max(0, os.Energy - zapCost);
					if (!Main.dedServ) { for (int d = 0; d < 6; d++) { Dust dd = Dust.NewDustDirect(shot.position, shot.width, shot.height, DustID.Electric, 0f, 0f, 100, default, 0.8f); dd.noGravity = true; } }
					Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, shot.Center);
					if (Main.netMode != NetmodeID.MultiplayerClient)
						shot.Kill();
					hitSomething = true;
				}
			}
			if (hitSomething) { s.Uses++; os.Uses++; SyncWorld(); }
		}
		// Alarm throttle: one ping per pair per 90 ticks (sensor, not weapon).
		private static readonly Dictionary<Point, int> lastAlarmTick = new();

		// Grind ledger (material memory): canonical pair -> (ceiling sag
		// 0..0.35, lifetime work done). Sustained redline sags the snap
		// ceiling toward the stress (cyclic fatigue); cool running heals
		// it. Transient per session, computed identically on all sides.
		private static readonly Dictionary<Point, (float creep, float work)> fatigue = new();

		public static void UpdateFatigue(Point a, Point b, int stress, int maxStress, float dyn, int step)
		{
			Point canon = LinkRope.Canonical(a, b);
			fatigue.TryGetValue(canon, out var f);
			float frac = stress / (float)System.Math.Max(1, maxStress);
			if (frac > 0.7f)
				f.creep = System.Math.Min(0.35f, f.creep + (frac - 0.7f) * 0.01f * step / 12f * 3f);
			else
				f.creep = System.Math.Max(0f, f.creep - 0.002f * step / 12f);
			f.work = System.Math.Min(1000000f, f.work + System.Math.Max(0f, dyn) * step / 12f);
			fatigue[canon] = f;
			if (fatigue.Count > 256) {
				foreach (var k in new System.Collections.Generic.List<Point>(fatigue.Keys)) {
					if (k != canon) {
						fatigue.Remove(k);
						break;
					}
				}
			}
		}

		public static int EffMaxFor(Point a, Point b, int maxStress)
		{
			if (fatigue.TryGetValue(LinkRope.Canonical(a, b), out var f) && f.creep > 0f)
				return System.Math.Max(10, (int)(maxStress * (1f - f.creep)));
			return maxStress;
		}

		private static bool FatigueOf(Point tile, out float creep, out float work)
		{
			creep = 0f;
			work = 0f;
			if (!Traps.TryGetValue(tile, out var s) || !s.Linked)
				return false;
			if (!fatigue.TryGetValue(LinkRope.Canonical(tile, s.LinkPoint), out var f))
				return false;
			creep = f.creep;
			work = f.work;
			return true;
		}

		public static float PairCreep(Point tile) => FatigueOf(tile, out float creep, out _) ? creep : 0f;
		public static float PairWork(Point tile) => FatigueOf(tile, out _, out float work) ? work : 0f;

		// Client-side ambient dust master switch (strain sparks, fray
		// smoke). Event bursts (snap puff, shockwave) always play.
		public static bool AmbientDust()
		{
			try {
				return ModContent.GetInstance<Configuration.YoyoConfig>().TrapVisuals.AmbientDust;
			}
			catch {
				return true;
			}
		}

		public static int EffMaxStress(Point tile, TrapState s)
		{
			if (s == null)
				return 0;
			return System.Math.Max(10, (int)(s.MaxStress() * (1f - PairCreep(tile))));
		}

		// Snap shockwave FX (visual only): pair -> tick of the snap. The
		// rope draw reads it for an expanding ring + light flash. Capped
		// and pruned; server entries are harmless (never drawn there).
		private static readonly Dictionary<Point, long> snapFx = new();

		public static void NoteSnapFx(Point a, Point b)
		{
			Point canon = LinkRope.Canonical(a, b);
			snapFx[canon] = (long)Main.GameUpdateCount;
			if (snapFx.Count > 64) {
				long now = (long)Main.GameUpdateCount;
				var dead = new System.Collections.Generic.List<Point>();
				foreach (var kv in snapFx) {
					if (now - kv.Value > 120)
						dead.Add(kv.Key);
				}
				foreach (var k in dead)
					snapFx.Remove(k);
				if (snapFx.Count > 64)
					snapFx.Remove(canon);
			}
		}

		// Ticks since this pair's snap, or -1. Read by the rope draw pass.
		public static int SnapFxAge(Point a, Point b)
		{
			if (!snapFx.TryGetValue(LinkRope.Canonical(a, b), out long t))
				return -1;
			long age = (long)Main.GameUpdateCount - t;
			return age < 0 || age > 30 ? -1 : (int)age;
		}

		// Alarm trip log: last 5 crossings per pair (time + who), for review.
		private static readonly Dictionary<Point, Queue<string>> alarmLog = new();

		public static List<string> AlarmLogFor(Point a, Point b)
		{
			if (alarmLog.TryGetValue(LinkRope.Canonical(a, b), out var q))
				return new List<string>(q);
			return new List<string>();
		}

		private static void LogAlarmTrip(Point canonical, string who)
		{
			if (!alarmLog.TryGetValue(canonical, out var q)) {
				// Pair count is bounded by built traps, but cap anyway.
				if (alarmLog.Count >= 100) {
					foreach (var k in new System.Collections.Generic.List<Point>(alarmLog.Keys)) {
						alarmLog.Remove(k);
						break;
					}
				}
				q = new Queue<string>();
				alarmLog[canonical] = q;
			}
			string clock = (Main.dayTime ? "Day " : "Night ") + (Main.time / 3600.0).ToString("0.0") + "h";
			q.Enqueue($"{clock} — {who}");
			while (q.Count > 5)
				q.Dequeue();
		}

		// Trip alarm: any eligible crossing pings (sound + sparks + Uses).
		// No damage, no hold, no strain, no wiring — pure sensor.
		private static void TickLinkAlarm(Point key, Point other, Vector2[] ropePts)
		{
			if (!Traps.TryGetValue(key, out var s) || !Traps.TryGetValue(other, out var os))
				return;
			// Idle-only scope: stay silent while the barrier is armed.
			if ((s.AlarmIdleOnly || os.AlarmIdleOnly) && (s.WiredActive || os.WiredActive))
				return;
			Point canonical = LinkRope.Canonical(key, other);
			int now = (int)Main.GameUpdateCount;
			if (lastAlarmTick.TryGetValue(canonical, out int prev) && now - prev < 90)
				return;
			float point = 0f;
			bool crossed = false;
			string who = "";
			foreach (var pl in Main.player) {
				if (pl == null || !pl.active || pl.dead)
					continue;
				if (!LinkEligiblePlayer(s, pl) && !LinkEligiblePlayer(os, pl))
					continue;
				if (RopeTouched(ropePts, true, pl.whoAmI, pl.position, pl.Size, pl.velocity, 12f + System.Math.Min(26f, pl.velocity.Length() * 1.2f), ref point)) { crossed = true; who = "Player:" + pl.name; break; }
			}
			if (!crossed) {
				foreach (var n in Main.npc) {
					if (n == null || !n.active || n.life <= 0 || n.dontTakeDamage)
						continue;
					if (!LinkEligibleNPC(s, n) && !LinkEligibleNPC(os, n))
						continue;
					if (RopeTouched(ropePts, false, n.whoAmI, n.position, new Vector2(n.width, n.height), n.velocity, 12f + System.Math.Min(26f, n.velocity.Length() * 1.2f), ref point)) {
						crossed = true;
						ClassifyNPC(n, out string cat);
						who = (n.boss ? "BOSS " : "") + cat;
						break;
					}
				}
			}
			if (!crossed)
				return;
			lastAlarmTick[canonical] = now;
			LogAlarmTrip(canonical, who);
			if (lastAlarmTick.Count > 200) {
				var stale = new System.Collections.Generic.List<Point>();
				foreach (var kv in lastAlarmTick) {
					if (now - kv.Value > 3600)
						stale.Add(kv.Key);
				}
				foreach (var k in stale)
					lastAlarmTick.Remove(k);
			}
			s.Uses++; os.Uses++;
			if (!Main.dedServ) {
				Vector2 mid = (ropePts[0] + ropePts[ropePts.Length - 1]) * 0.5f;
				for (int q = 0; q < 10; q++) {
					Dust dd = Dust.NewDustDirect(mid - new Vector2(8, 8), 16, 16, DustID.Electric, 0f, -1f, 100, default, 1.1f);
					dd.noGravity = true;
				}
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Mech, (ropePts[0] + ropePts[ropePts.Length - 1]) * 0.5f);
			SyncWorld();
		}

		public static bool RopeHits(Vector2[] pts, Vector2 pos, Vector2 size, float width, ref float point)
		{
			if (pts == null || pts.Length < 2)
				return false;
			for (int i = 0; i < pts.Length - 1; i++) {
				if (Collision.CheckAABBvLineCollision(pos, size, pts[i], pts[i + 1], width, ref point))
					return true;
			}
			return false;
		}

		// Phase-save counter (diagnostic, sim-side only): how many times the
		// path-history/swept test caught a victim the single current-box
		// test would have missed (i.e., prevented tunnelings). Displayed
		// in the Weave tab; in MP only the server advances it.
		public static long PhaseIntercepts;

		// Per-enemy strain weight (IRL load): chaff barely strains the rope,
		// heavies and bosses strain it a lot. Zombies (~45 HP) sit at 0.5x.
		public static float StrainWeightFor(NPC n)
		{
			if (n == null)
				return 1f;
			if (n.boss)
				return 6f;
			return n.lifeMax switch {
				< 60 => 0.5f,
				< 200 => 1f,
				< 800 => 1.5f,
				< 2000 => 2.5f,
				_ => 4f,
			};
		}

		// Into-rope approach speed (px/tick): the finger-push input. Zero
		// when resting on the rope or moving along it; large on charges.
		public static float ApproachSpeed(Vector2 vel, Vector2 center, Vector2 contact)
		{
			Vector2 away = center - contact;
			float dl = away.Length();
			if (dl < 0.5f)
				return vel.Length();
			return System.Math.Max(0f, -Vector2.Dot(vel, away / dl));
		}

		// De-embed after a finger-push shove: the resolve teleports bodies,
		// which can plant them inside solid tiles where vanilla AI flails
		// stuck. Walk back along entry velocity, then straight up; give up
		// after that (vanilla collision finishes the job next tick anyway).
		public static void UnstickFromTiles(Entity ent)
		{
			if (ent == null || !Collision.SolidCollision(ent.position, ent.width, ent.height))
				return;
			Vector2 back = ent.velocity;
			if (back.LengthSquared() < 0.01f)
				back = new Vector2(0f, -1f);
			back.Normalize();
			for (int k = 0; k < 40; k++) {
				ent.position -= back * 1f;
				if (!Collision.SolidCollision(ent.position, ent.width, ent.height))
					return;
			}
			for (int k = 0; k < 24; k++) {
				ent.position.Y -= 1f;
				if (!Collision.SolidCollision(ent.position, ent.width, ent.height))
					return;
			}
		}

		// Span position factor: pushes land hardest mid-span (peak bending
		// moment at center, same reason a catenary sags deepest mid-span).
		// t = fractional contact position along the rope, 0..1.
		public static float CenterFactor(Vector2 a, Vector2 b, Vector2 contact)
		{
			Vector2 ab = b - a;
			float len2 = ab.LengthSquared();
			if (len2 < 1f)
				return 1f;
			float t = Vector2.Dot(contact - a, ab) / len2;
			t = System.Math.Clamp(t, 0f, 1f);
			return 1f + 0.4f * (float)System.Math.Sin(System.Math.PI * t);
		}

		// Breakthrough toughness (impulse units): overwhelming rushes tear
		// past the rope instead of being held (rupture past yield, not a
		// clean snap). Player movement tech (~40-90 impulse) holds on every
		// string — dash+jump combos are not a free pass — while boss-grade
		// charges (~150+) rip basic strings and only Kevlar-grade holds.
		public static float RipToughness(int variant)
		{
			return 60f + TrapState.MaxStressFor(System.Math.Clamp(variant, 0, 7), 0) * 0.55f;
		}

		// Capstan (Euler–Eytelwein) grip: rope friction around the endpoints
		// sheds load as e^(-mu*theta). Natural fibers grip (Cotton, Silk);
		// slick synthetics (Kevlar, Nylon) barely hold. Wrap angle comes
		// from the Bend stance: Straight ~0, Slight ~half rad, Curved ~1 rad.
		public static float CapstanMuFor(int variant) => System.Math.Clamp(variant, 0, 7) switch {
			0 => 0.28f, // Cotton: grippy
			1 => 0.22f, // Poly-Cotton
			2 => 0.15f, // Polyester
			3 => 0.12f, // Nylon: slick
			4 => 0.10f, // Kevlar: slickest
			5 => 0.30f, // Silk: grippiest
			6 => 0.18f, // Shadow
			_ => 0.20f, // Solar
		};

		public static float CapstanFactor(int variant, int bend)
		{
			float theta = bend <= 0 ? 0f : bend == 1 ? 0.5f : 1.0f;
			return (float)System.Math.Exp(-CapstanMuFor(variant) * theta);
		}

		public static float RipChunkFor(float w, float approach, int svar)
		{
			float overflow = w * approach * (1f + approach / 10f) - RipToughness(svar);
			if (overflow <= 0f)
				return 0f;
			return System.Math.Min(TrapState.MaxStressFor(System.Math.Clamp(svar, 0, 7), 0) * 0.25f, overflow * 0.5f);
		}

		// Per-victim approach memory (jerk = this scan minus last scan) and
		// rip cooldowns. Keys: NPC whoAmI, players as -1000-whoAmI.
		private static readonly Dictionary<int, (float approach, long tick)> prevAppr = new();
		private static readonly Dictionary<int, long> ripCd = new();

		// Returns true when this scan's push rips straight through the rope
		// (impulse-momentum: weight x sudden rush past toughness). Jerk is
		// always reported for the strain formula; cooldown stops machine-gun
		// rips while the victim is still inside.
		public static bool CheckRip(float w, float approach, int svar, int key, out float jerk)
		{
			jerk = 0f;
			long now = (long)Main.GameUpdateCount;
			if (ripCd.TryGetValue(key, out long until) && now < until) {
				prevAppr[key] = (approach, now);
				return false;
			}
			float prev = prevAppr.TryGetValue(key, out var pv) ? pv.approach : 0f;
			prevAppr[key] = (approach, now);
			// Braking impulse (seatbelt load): lunging in works the rope,
			// and so does slamming to a stop against it — at half rate.
			jerk = System.Math.Max(0f, System.Math.Max(approach - prev, (prev - approach) * 0.5f));
			float impulse = w * approach * (1f + approach / 10f);
			if (approach > 0.5f && impulse > RipToughness(svar)) {
				ripCd[key] = now + 45;
				PrunePushDicts();
				return true;
			}
			if (prevAppr.Count > 1200)
				PrunePushDicts();
			return false;
		}

		private static void PrunePushDicts()
		{
			var dead = new System.Collections.Generic.List<int>();
			foreach (int k in prevAppr.Keys) {
				bool alive = k >= 0
					? (k < Main.maxNPCs && Main.npc[k] != null && Main.npc[k].active)
					: (-1000 - k >= 0 && -1000 - k < Main.maxPlayers && Main.player[-1000 - k] != null && Main.player[-1000 - k].active);
				if (!alive)
					dead.Add(k);
			}
			foreach (int k in dead) {
				prevAppr.Remove(k);
				ripCd.Remove(k);
			}
			foreach (int k in new System.Collections.Generic.List<int>(ripCd.Keys)) {
				if (!prevAppr.ContainsKey(k))
					ripCd.Remove(k);
			}
		}

		public static void RipFeedback(Vector2 at)
		{
			if (Main.dedServ)
				return;
			for (int q = 0; q < 10; q++) {
				Dust d = Dust.NewDustDirect(at - new Vector2(4, 4), 8, 8, DustID.Smoke, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 1f), 100, default, 1.1f);
				d.noGravity = true;
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, at);
		}

		public static bool IsFlyer(NPC n)
			=> n != null && (n.noGravity || n.noTileCollide);

		// Swept anti-tunnel check: tests current + backtracked boxes along
		// velocity so fast enemies can't phase between 12-tick damage scans.
		public static bool RopeHitsSwept(Vector2[] pts, Vector2 pos, Vector2 size, Vector2 vel, float width, ref float point)
		{
			if (RopeHits(pts, pos, size, width, ref point))
				return true;
			float speed = vel.Length();
			if (speed < 4f)
				return false; // slow: current box is enough
			// Full-velocity coverage (k/steps reaches 1.0): nothing between
			// last tick and now goes untested. More steps when faster.
			int steps = System.Math.Clamp((int)(speed / 6f) + 2, 2, 5);
			for (int k = 1; k <= steps; k++) {
				Vector2 back = pos - vel * (k / (float)steps);
				if (RopeHits(pts, back, size, width, ref point))
					return true;
			}
			return false;
		}

// Per-tick path registry (anti phase-through): PostUpdateEverything
		// stores every NPC/player center + size at the END of each tick (12-deep
		// ring buffer), so staggered scans test the TRUE path: curves, charges,
		// dashes and teleports included.
		private static readonly Dictionary<int, Queue<Vector2>> prevNPCPath = new();
		private static readonly Dictionary<int, Queue<Vector2>> prevNPCSizePath = new();
		private static readonly Dictionary<int, Queue<Vector2>> prevPlayerPath = new();
		private static readonly Dictionary<int, Queue<Vector2>> prevPlayerSizePath = new();
		private const int PathKeep = 12; // one entry per tick: full inter-scan path

		public static void NotePositions()
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
			NPC n = Main.npc[i];
			if (n == null || !n.active) { prevNPCPath.Remove(i); prevNPCSizePath.Remove(i); continue; }
			if (!prevNPCPath.TryGetValue(i, out var nq)) { nq = new Queue<Vector2>(); prevNPCPath[i] = nq; }
			if (!prevNPCSizePath.TryGetValue(i, out var nsq)) { nsq = new Queue<Vector2>(); prevNPCSizePath[i] = nsq; }
			nq.Enqueue(n.Center);
			nsq.Enqueue(new Vector2(n.width, n.height));
			while (nq.Count > PathKeep) nq.Dequeue();
			while (nsq.Count > PathKeep) nsq.Dequeue();
			}
			for (int i = 0; i < Main.maxPlayers; i++) {
			Player pl = Main.player[i];
			if (pl == null || !pl.active || pl.dead) { prevPlayerPath.Remove(i); prevPlayerSizePath.Remove(i); continue; }
			if (!prevPlayerPath.TryGetValue(i, out var pq)) { pq = new Queue<Vector2>(); prevPlayerPath[i] = pq; }
			if (!prevPlayerSizePath.TryGetValue(i, out var psq)) { psq = new Queue<Vector2>(); prevPlayerSizePath[i] = psq; }
			pq.Enqueue(pl.Center);
			psq.Enqueue(pl.Size);
			while (pq.Count > PathKeep) pq.Dequeue();
			while (psq.Count > PathKeep) psq.Dequeue();
			}
		}

		// Swept path check: tests last tick boxes plus the interpolated path, so
		// nothing crosses the rope between staggered scans. Teleports (>500px)
		// test endpoints only (no cross-map false alarms).
		public static bool RopeHitsPath(Vector2[] pts, Vector2 prevCenter, Vector2 prevSize, Vector2 curPos, Vector2 curSize, float width, ref float point)
		{
			if (RopeHits(pts, curPos, curSize, width, ref point))
			return true;
			Vector2 prevPos = prevCenter - prevSize * 0.5f;
			if (RopeHits(pts, prevPos, prevSize, width, ref point))
			return true;
			float dist = Vector2.Distance(prevCenter, curPos + curSize * 0.5f);
			if (dist < 8f || dist > 500f)
			return false;
			int steps = System.Math.Clamp((int)(dist / 12f), 1, 10);
			for (int k = 1; k < steps; k++) {
			float f = k / (float)steps;
			if (RopeHits(pts, Vector2.Lerp(prevPos, curPos, f), Vector2.Lerp(prevSize, curSize, f), width, ref point))
			return true;
			}
			return false;
		}

		// Path-walking entry point (falls back to velocity backtrack with no history).
		public static bool RopeTouched(Vector2[] pts, bool isPlayer, int who, Vector2 curPos, Vector2 curSize, Vector2 vel, float width, ref float point)
		{
			Dictionary<int, Queue<Vector2>> paths = isPlayer ? prevPlayerPath : prevNPCPath;
			Dictionary<int, Queue<Vector2>> sizes = isPlayer ? prevPlayerSizePath : prevNPCSizePath;
			if (paths.TryGetValue(who, out var q) && sizes.TryGetValue(who, out var qs) && q.Count > 0 && qs.Count > 0) {
				Vector2[] pc = q.ToArray();
				Vector2[] ps = qs.ToArray();
				int n = System.Math.Min(pc.Length, ps.Length);
				for (int k = 1; k < n; k++) {
					if (RopeHitsPath(pts, pc[k - 1], ps[k - 1], pc[k] - ps[k] * 0.5f, ps[k], width, ref point))
					return true;
				}
				if (RopeHitsPath(pts, pc[n - 1], ps[n - 1], curPos, curSize, width, ref point))
					return true;
				return false;
			}
			return RopeHitsSwept(pts, curPos, curSize, vel, width, ref point);
		}

		// Rope tangent at a contact point (for slide-along-restraint).
		public static Vector2 RopeTangentAt(Vector2[] pts, Vector2 contact)
		{
			if (pts == null || pts.Length < 2)
				return Vector2.UnitX;
			float bestD = float.MaxValue;
			Vector2 bestTan = Vector2.UnitX;
			for (int i = 0; i < pts.Length - 1; i++) {
				Vector2 ab = pts[i + 1] - pts[i];
				float len = ab.Length();
				if (len < 0.001f)
					continue;
				Vector2 tan = ab / len;
				Vector2 mid = (pts[i] + pts[i + 1]) * 0.5f;
				float d = Vector2.DistanceSquared(contact, mid);
				if (d < bestD) { bestD = d; bestTan = tan; }
			}
			return bestTan.SafeNormalize(Vector2.UnitX);
		}

		private static void SlideAlongRope(Player pl, Vector2 contact, Vector2[] ropePts, float keep)
		{
			Vector2 tangent = RopeTangentAt(ropePts, contact);
			float vTan = Vector2.Dot(pl.velocity, tangent);
			Vector2 vNorm = pl.velocity - tangent * vTan;
			pl.velocity = tangent * vTan * keep + vNorm * 0.2f;
		}

		public static Vector2 ClosestRopePoint(Vector2[] pts, Vector2 pos)
		{
			Vector2 best = pts[0];
			float bestD = float.MaxValue;
			for (int i = 0; i < pts.Length - 1; i++) {
				Vector2 ab = pts[i + 1] - pts[i];
				float denom = ab.LengthSquared();
				float t = denom < 0.001f ? 0f : Vector2.Dot(pos - pts[i], ab) / denom;
				t = System.Math.Clamp(t, 0f, 1f);
				Vector2 c = pts[i] + ab * t;
				float d = Vector2.DistanceSquared(pos, c);
				if (d < bestD) { bestD = d; best = c; }
			}
			return best;
		}

		private static void TickTripwire(Point key, TrapState s)
		{
			if (!s.Enabled)
				return;
			// Schedule: off-phase traps hold fire (upkeep still ticks).
			if (s.Sched != 0) {
				int per = s.PulsePeriod == 10 ? 10 : s.PulsePeriod == 60 ? 60 : 30;
				bool wantFire = s.Sched switch {
					1 => Main.dayTime,
					2 => !Main.dayTime,
					_ => (Main.GameUpdateCount / (per * 60)) % 2 == 0,
				};
				if (!wantFire)
					return;
			}
			// Barbed/Tangled STRING touch (the tether itself is a weapon, like
			// the string accessories): runs while deployed, independent of the
			// trigger cooldown. Barbed = 5 + Bleeding 5s, Tangled = 1 +
			// Bleeding 3s — the exact accessory numbers. Hostile NPCs, plus
			// players when the Players filter is ON.
			int tstr = System.Math.Clamp(s.TripString, 0, 24);
			if (tstr != 0) {
				Vector2 anchorW = TileCenter(key);
				bool touched = false;
				for (int i = 0; i < Main.maxProjectiles; i++) {
					var pr = Main.projectile[i];
					if (!pr.active || pr.type != ModContent.ProjectileType<TripwireYoyoProj>())
						continue;
					if ((int)pr.ai[0] != key.X || (int)pr.ai[1] != key.Y)
						continue;
					Vector2 head = pr.Center;
					Vector2 hvel = pr.velocity;
					foreach (var n in Main.npc) {
						if (n == null || !n.active || n.life <= 0 || n.friendly || n.townNPC || n.dontTakeDamage)
							continue;
						if (n.damage <= 0 || n.lifeMax <= 5)
							continue;
						if (Terraria.ID.NPCID.Sets.CountsAsCritter[n.type])
							continue;
						float spill = 0f;
						bool htouch = false;
						for (int hb = 0; hb <= 1 && !htouch; hb++) {
							Vector2[] seg = { anchorW, head - hvel * (hb * 12f) };
							if (RopeTouched(seg, false, n.whoAmI, n.position, new Vector2(n.width, n.height), n.velocity, 14f, ref spill))
							htouch = true;
						}
						if (!htouch)
							continue;
						int dir = n.Center.X - anchorW.X < 0f ? -1 : 1;
						n.StrikeNPC(new NPC.HitInfo { Damage = TripStringDamage(tstr), Knockback = 0.5f, HitDirection = dir }, false, false);
						if (n.life <= 0) {
							int prev = s.Kills;
							s.Kills++;
							CheckVeteranMilestone(key, s, prev);
						}
						n.AddBuff(BuffID.Bleeding, TripStringBleed(tstr), false);
						n.netUpdate = true;
						if (Main.netMode == NetmodeID.Server)
							NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, n.whoAmI);
						if (!Main.dedServ) {
							for (int q = 0; q < (tstr == 1 ? 6 : 4); q++) {
								Dust dd = Dust.NewDustDirect(n.position, n.width, n.height,
									tstr == 1 ? DustID.Blood : DustID.WoodFurniture,
									Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 0f), 100, default, 0.9f);
								dd.noGravity = true;
							}
						}
						touched = true;
					}
					// Tether vs players (follows the Players filter).
					foreach (var pl in Main.player) {
						if (pl == null || !pl.active || pl.dead || pl.immune)
							continue;
						if (!s.HitPlayers && !s.HitAll)
							continue;
						float pspill = 0f;
						bool phtouch = false;
						for (int hb = 0; hb <= 1 && !phtouch; hb++) {
							Vector2[] pseg = { anchorW, head - hvel * (hb * 12f) };
							if (RopeTouched(pseg, true, pl.whoAmI, pl.position, pl.Size, pl.velocity, 14f, ref pspill))
							phtouch = true;
						}
						if (!phtouch)
							continue;
						pl.Hurt(PlayerDeathReason.ByCustomReason(Terraria.Localization.NetworkText.FromLiteral("was cut by a barbed tripwire.")),
							TripStringDamage(tstr), 0);
						pl.AddBuff(BuffID.Bleeding, TripStringBleed(tstr), false);
						touched = true;
					}
				}
				if (touched)
					SyncWorld();
			}
			if (s.Cooldown > 0)
				return;
			if (s.Energy < s.EnergyCost)
				return;
			// Don't stack overlapping attacks.
			for (int i = 0; i < Main.maxProjectiles; i++) {
				var pr = Main.projectile[i];
				if (pr.active && pr.type == ModContent.ProjectileType<TripwireYoyoProj>()
					&& pr.ai[0] == key.X && pr.ai[1] == key.Y)
					return;
			}
			Vector2 center = TileCenter(key);
			int rangePx = EffectiveRangeTiles(key, s) * 16;
			bool tripped = false;
			Vector2 aimAt = center + new Vector2(rangePx, 0);
			float bestD = float.MaxValue;
			foreach (var p in Main.player) {
				if (p == null || !p.active || p.dead)
					continue;
				float pd = Vector2.DistanceSquared(p.Center, center);
				if (pd > rangePx * rangePx || pd >= bestD)
					continue;
				if (!Collision.CanHitLine(center, 0, 0, p.Center, 0, 0))
					continue;
				if (TripwireTriggerEligible(s, p)) { tripped = true; bestD = pd; aimAt = p.Center; }
			}
			foreach (var n in Main.npc) {
				if (n == null || !n.active || n.life <= 0)
					continue;
				float nd = Vector2.DistanceSquared(n.Center, center);
				if (nd > rangePx * rangePx || nd >= bestD)
					continue;
				if (!Collision.CanHitLine(center, 0, 0, n.Center, 0, 0))
					continue;
				if (TripwireTriggerEligible(s, n)) { tripped = true; bestD = nd; aimAt = n.Center; }
			}
			if (!tripped)
				return;
			bool ocAuto = !s.Overcharge && s.AutoOC && AegisFueled(key);
			if (OverchargeActive(key, s) && !TrapActions.LicensedNoBackfire(key) && Main.rand.NextFloat() < OverchargeRisk(key, s)) {
				s.Cooldown = EffectiveCooldown(key, s) * 2;
				s.Energy = Math.Max(0, s.Energy - s.EnergyCost - (ocAuto ? 5 : 0));
				return;
			}
			// Aimed deployment (nearest trigger + slight spread) so the yoyo flies
			// at something instead of a random wall.
			float ang = (aimAt - center).ToRotation() + Main.rand.NextFloat(-0.15f, 0.15f);
			var tstyle = TripStyleOf(System.Math.Clamp(s.TripStyle, 0, 7));
			int dmg = System.Math.Max(1, (int)(EffectiveDamageRaw(key, s) * tstyle.DmgMult * (OverchargeActive(key, s) ? 1.5f : 1f)));
			var src = new EntitySource_Misc("Tripwire");
			int proj = Projectile.NewProjectile(src, center, ang.ToRotationVector2() * 2f,
				ModContent.ProjectileType<TripwireYoyoProj>(), dmg, 2f, Main.myPlayer,
				ai0: key.X, ai1: key.Y);
			if (proj >= 0 && proj < Main.maxProjectiles) {
				var pr = Main.projectile[proj];
				pr.ai[2] = ang;
				pr.Center = center;
				float needOut = s.OutSpeed > 0 ? s.MaxLen / (s.OutSpeed / 10f) : 999f;
				float needBack = s.ReturnSpeed > 0 ? s.MaxLen / (s.ReturnSpeed / 10f) : 999f;
				// Lubricated Axle: +20% flight time per level (longer hang).
				float axleMult = 1f + 0.2f * System.Math.Clamp(s.TripAxle, 0, 3);
				pr.timeLeft = System.Math.Clamp((int)(((needOut + needBack) + 90) * axleMult), 200, 1800);
				pr.netUpdate = true;
			// Twin Throw (unique option): a second yoyo deploys opposite, at 1.5x
			// total power cost (half extra). Both share the anchor and return independently.
			if (s.TwinThrow && s.Energy >= s.EnergyCost + s.EnergyCost / 2) {
				float ang2 = ang + MathHelper.Pi;
				int proj2 = Projectile.NewProjectile(src, center, ang2.ToRotationVector2() * 2f,
					ModContent.ProjectileType<TripwireYoyoProj>(), dmg, 2f, Main.myPlayer,
					ai0: key.X, ai1: key.Y);
				if (proj2 >= 0 && proj2 < Main.maxProjectiles) {
					var pr2 = Main.projectile[proj2];
					pr2.ai[2] = ang2;
					pr2.Center = center;
					pr2.timeLeft = pr.timeLeft;
					pr2.netUpdate = true;
					s.Energy -= s.EnergyCost / 2;
				}
			}
			}
			s.Energy -= s.EnergyCost + (ocAuto ? 5 : 0);
			s.Cooldown = EffectiveCooldown(key, s);
			s.Uses++;
			SyncWorld();
		}

		// ---- Save / Load / Net ----

		public override void OnWorldLoad() => Traps.Clear();
		public override void OnWorldUnload() => Traps.Clear();

		public override void SaveWorldData(TagCompound tag)
		{
			var xs = new List<int>(); var ys = new List<int>();
			var kinds = new List<int>(); var levels = new List<int>();
			var uses = new List<int>(); var energy = new List<int>();
			var over = new List<int>(); var cd = new List<int>();
			var rng = new List<int>(); var dmg = new List<int>();
			var dur = new List<int>(); var cdmax = new List<int>();
			var svar = new List<int>(); var btier = new List<int>();
			var ctype = new List<int>(); var pri = new List<int>();
			var beh = new List<int>(); var rest = new List<int>(); var bend = new List<int>();
			var maxlen = new List<int>(); var outs = new List<int>(); var rets = new List<int>();
			var burst = new List<int>(); var wired = new List<int>();
			var hp = new List<int>(); var hn = new List<int>(); var ht = new List<int>();
			var hh = new List<int>(); var hf = new List<int>(); var ha = new List<int>();
			var lx = new List<int>(); var ly = new List<int>(); var oAegis = new List<int>(); var oWard = new List<int>(); var oSnare = new List<int>(); var oBunk = new List<int>(); var oSpan = new List<int>(); var oHair = new List<int>(); var oBarb = new List<int>(); var oTrac = new List<int>(); var oTwin = new List<int>(); var oStress = new List<int>(); var oFray = new List<int>(); var oCond = new List<int>(); var oCTick = new List<int>(); var oPend = new List<int>(); var aim = new List<float>(); var oRegen = new List<int>(); var oTens = new List<int>(); var oLStress = new List<int>(); var oAuto = new List<int>(); var oAnchor = new List<int>(); var oCoil = new List<int>(); var oGyro = new List<int>(); var oAxle = new List<int>(); var oStyle = new List<int>(); var oTStr = new List<int>(); var oEn = new List<int>(); var oName = new List<string>(); var oAOC = new List<int>(); var oShare = new List<int>(); var oKills = new List<int>(); var oAlarm = new List<int>(); var oScope = new List<int>(); var oReserve = new List<int>(); var oWind = new List<int>(); var oSStr = new List<int>(); var oTreat = new List<int>(); var oWrap = new List<int>(); var oSched = new List<int>(); var oPulse = new List<int>(); var oExp = new List<int>();
			foreach (var kv in Traps) {
				if (!IsAnyTrapTile(kv.Key))
					continue;
				var st = kv.Value;
				xs.Add(kv.Key.X); ys.Add(kv.Key.Y);
				kinds.Add((int)st.Kind); levels.Add(st.Level);
				uses.Add(st.Uses); energy.Add(st.Energy);
				over.Add(st.Overcharge ? 1 : 0); cd.Add(Math.Max(0, st.Cooldown));
				rng.Add(st.Range); dmg.Add(st.Damage);
				dur.Add(st.Duration); cdmax.Add(st.CooldownMax);
				svar.Add(st.StringVariant); btier.Add(st.BearingTier);
				ctype.Add(st.CounterType); pri.Add(st.Priority);
				beh.Add(st.Behavior); rest.Add(st.Restraint); bend.Add(st.Bend);
				maxlen.Add(st.MaxLen); outs.Add(st.OutSpeed); rets.Add(st.ReturnSpeed);
				burst.Add(st.Burst); wired.Add(st.WiredActive ? 1 : 0);
				hp.Add(st.HitPlayers ? 1 : 0); hn.Add(st.HitNPCs ? 1 : 0); ht.Add(st.HitTown ? 1 : 0);
				hh.Add(st.HitHostile ? 1 : 0); hf.Add(st.HitFriendly ? 1 : 0); ha.Add(st.HitAll ? 1 : 0);
				lx.Add(st.LinkX); ly.Add(st.LinkY); oAegis.Add(st.SentryAegis ? 1 : 0); oWard.Add(st.WardLattice ? 1 : 0); oSnare.Add(st.SnareTip ? 1 : 0); aim.Add(st.AimAngle);
				oBunk.Add(st.SentryBunker ? 1 : 0); oSpan.Add(st.LongSpan ? 1 : 0); oHair.Add(st.HairTrigger ? 1 : 0);
				oBarb.Add(st.BarbedWire ? 1 : 0); oTrac.Add(st.TracerFire ? 1 : 0); oTwin.Add(st.TwinThrow ? 1 : 0); oStress.Add(st.Stress); oFray.Add(st.Frayed); oCond.Add(st.Condition); oCTick.Add(st.CondTick); oPend.Add(st.UpgradePending ? 1 : 0);
				oRegen.Add(System.Math.Clamp(st.RegenWeave, 0, 3)); oTens.Add(System.Math.Clamp(st.TensionWeave, 0, 3)); oLStress.Add(System.Math.Clamp(st.LinkStress, 0, TrapState.MaxLinkStress)); oAuto.Add(st.AutoRewire ? 1 : 0); oAnchor.Add(System.Math.Clamp(st.SentryAnchor, 0, 2));
				oCoil.Add(System.Math.Clamp(st.LinkCoil, 0, 3)); oGyro.Add(System.Math.Clamp(st.SentryGyro, 0, 3)); oAxle.Add(System.Math.Clamp(st.TripAxle, 0, 3)); oStyle.Add(System.Math.Clamp(st.TripStyle, 0, 7)); oTStr.Add(System.Math.Clamp(st.TripString, 0, 24)); oEn.Add(st.Enabled ? 1 : 0); oName.Add(st.Name ?? ""); oAOC.Add(st.AutoOC ? 1 : 0); oShare.Add(System.Math.Clamp(st.ShareMode, 0, 2)); oKills.Add(System.Math.Max(0, st.Kills)); oAlarm.Add(st.AlarmIdleOnly ? 1 : 0); oScope.Add(System.Math.Clamp(st.SentryScope, 0, 3)); oReserve.Add(System.Math.Clamp(st.LinkReserve, 0, 3)); oWind.Add(System.Math.Clamp(st.TripWind, 0, 3)); oSStr.Add(System.Math.Clamp(st.SentryString, 0, 7)); oTreat.Add(System.Math.Clamp(st.LinkTreat, 0, 2)); oWrap.Add(System.Math.Clamp(st.LinkBarb, 0, 3)); oSched.Add(System.Math.Clamp(st.Sched, 0, 3)); oPulse.Add(st.PulsePeriod == 10 ? 10 : st.PulsePeriod == 60 ? 60 : 30); oExp.Add(st.ExpHeads);
			}
			tag["trx"] = xs; tag["try"] = ys; tag["trk"] = kinds; tag["trl"] = levels;
			tag["tru"] = uses; tag["tre"] = energy; tag["tro"] = over; tag["trc"] = cd;
			tag["trr"] = rng; tag["trd"] = dmg; tag["trdu"] = dur; tag["trcm"] = cdmax;
			tag["trsv"] = svar; tag["trbt"] = btier; tag["trct"] = ctype; tag["trp"] = pri;
			tag["trb"] = beh; tag["trrs"] = rest; tag["trbe"] = bend;
			tag["trml"] = maxlen; tag["tros"] = outs; tag["trrt"] = rets;
			tag["trbu"] = burst; tag["trw"] = wired;
			tag["trhp"] = hp; tag["trhn"] = hn; tag["trht"] = ht;
			tag["trhh"] = hh; tag["trhf"] = hf; tag["trha"] = ha;
			tag["trlx"] = lx; tag["trly"] = ly; tag["trAegis"] = oAegis; tag["trWard"] = oWard; tag["trSnare"] = oSnare; tag["trBarb"] = oBarb; tag["trTrac"] = oTrac; tag["trTwin"] = oTwin; tag["trStress"] = oStress; tag["trFray"] = oFray; tag["trCond"] = oCond; tag["trCTick"] = oCTick; tag["trPend"] = oPend; tag["trBunk"] = oBunk; tag["trSpan"] = oSpan; tag["trHair"] = oHair; tag["traim"] = aim; tag["trRegen"] = oRegen; tag["trTens"] = oTens; tag["trLStress"] = oLStress; tag["trAuto"] = oAuto; tag["trAnchor"] = oAnchor; tag["trCoil"] = oCoil; tag["trGyro"] = oGyro; tag["trAxle"] = oAxle; tag["trStyle"] = oStyle; tag["trTStr"] = oTStr; tag["trEn"] = oEn; tag["trName"] = oName; tag["trAOC"] = oAOC; tag["trShare"] = oShare; tag["trKills"] = oKills; tag["trAlarm"] = oAlarm; tag["trScope"] = oScope; tag["trReserve"] = oReserve; tag["trWind"] = oWind; tag["trSStr"] = oSStr; tag["trTreat"] = oTreat; tag["trWrap"] = oWrap; tag["trSched"] = oSched; tag["trPulse"] = oPulse; tag["trExp"] = oExp;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Traps.Clear();
			if (!tag.ContainsKey("trx"))
				return;
			try {
				var xs = tag.Get<List<int>>("trx"); var ys = tag.Get<List<int>>("try");
				var kinds = tag.Get<List<int>>("trk"); var levels = tag.Get<List<int>>("trl");
				var uses = tag.Get<List<int>>("tru"); var energy = tag.Get<List<int>>("tre");
				var over = tag.Get<List<int>>("tro"); var cd = tag.Get<List<int>>("trc");
				var rng = tag.Get<List<int>>("trr"); var dmg = tag.Get<List<int>>("trd");
				var dur = tag.Get<List<int>>("trdu"); var cdmax = tag.Get<List<int>>("trcm");
				var svar = tag.Get<List<int>>("trsv"); var btier = tag.Get<List<int>>("trbt");
				var ctype = tag.Get<List<int>>("trct"); var pri = tag.Get<List<int>>("trp");
				var beh = tag.Get<List<int>>("trb"); var rest = tag.Get<List<int>>("trrs"); var bend = tag.Get<List<int>>("trbe");
				var maxlen = tag.Get<List<int>>("trml"); var outs = tag.Get<List<int>>("tros"); var rets = tag.Get<List<int>>("trrt");
				var burst = tag.Get<List<int>>("trbu"); var wired = tag.Get<List<int>>("trw");
				var hp = tag.Get<List<int>>("trhp"); var hn = tag.Get<List<int>>("trhn"); var ht = tag.Get<List<int>>("trht");
				var hh = tag.Get<List<int>>("trhh"); var hf = tag.Get<List<int>>("trhf"); var ha = tag.Get<List<int>>("trha");
				var lx = tag.Get<List<int>>("trlx"); var ly = tag.Get<List<int>>("trly");
				System.Collections.Generic.List<int> oAegis = tag.ContainsKey("trAegis") ? tag.Get<System.Collections.Generic.List<int>>("trAegis") : null;
				System.Collections.Generic.List<int> oWard = tag.ContainsKey("trWard") ? tag.Get<System.Collections.Generic.List<int>>("trWard") : null;
				System.Collections.Generic.List<int> oSnare = tag.ContainsKey("trSnare") ? tag.Get<System.Collections.Generic.List<int>>("trSnare") : null;
				System.Collections.Generic.List<int> oBarb = tag.ContainsKey("trBarb") ? tag.Get<System.Collections.Generic.List<int>>("trBarb") : null;
				System.Collections.Generic.List<int> oTrac = tag.ContainsKey("trTrac") ? tag.Get<System.Collections.Generic.List<int>>("trTrac") : null;
				System.Collections.Generic.List<int> oTwin = tag.ContainsKey("trTwin") ? tag.Get<System.Collections.Generic.List<int>>("trTwin") : null;
				System.Collections.Generic.List<int> oStress = tag.ContainsKey("trStress") ? tag.Get<System.Collections.Generic.List<int>>("trStress") : null;
				System.Collections.Generic.List<int> oFray = tag.ContainsKey("trFray") ? tag.Get<System.Collections.Generic.List<int>>("trFray") : null;
				System.Collections.Generic.List<int> oPend = tag.ContainsKey("trPend") ? tag.Get<System.Collections.Generic.List<int>>("trPend") : null;
				System.Collections.Generic.List<int> oCond = tag.ContainsKey("trCond") ? tag.Get<System.Collections.Generic.List<int>>("trCond") : null;
				System.Collections.Generic.List<int> oCTick = tag.ContainsKey("trCTick") ? tag.Get<System.Collections.Generic.List<int>>("trCTick") : null;
				System.Collections.Generic.List<int> oBunk = tag.ContainsKey("trBunk") ? tag.Get<System.Collections.Generic.List<int>>("trBunk") : null;
				System.Collections.Generic.List<int> oSpan = tag.ContainsKey("trSpan") ? tag.Get<System.Collections.Generic.List<int>>("trSpan") : null;
				System.Collections.Generic.List<int> oHair = tag.ContainsKey("trHair") ? tag.Get<System.Collections.Generic.List<int>>("trHair") : null;
				System.Collections.Generic.List<float> aim = tag.ContainsKey("traim") ? tag.Get<System.Collections.Generic.List<float>>("traim") : null;
				System.Collections.Generic.List<int> oRegen = tag.ContainsKey("trRegen") ? tag.Get<System.Collections.Generic.List<int>>("trRegen") : null;
				System.Collections.Generic.List<int> oTens = tag.ContainsKey("trTens") ? tag.Get<System.Collections.Generic.List<int>>("trTens") : null;
				System.Collections.Generic.List<int> oLStress = tag.ContainsKey("trLStress") ? tag.Get<System.Collections.Generic.List<int>>("trLStress") : null;
				System.Collections.Generic.List<int> oAuto = tag.ContainsKey("trAuto") ? tag.Get<System.Collections.Generic.List<int>>("trAuto") : null;
				System.Collections.Generic.List<int> oAnchor = tag.ContainsKey("trAnchor") ? tag.Get<System.Collections.Generic.List<int>>("trAnchor") : null;
				System.Collections.Generic.List<int> oCoil = tag.ContainsKey("trCoil") ? tag.Get<System.Collections.Generic.List<int>>("trCoil") : null;
				System.Collections.Generic.List<int> oGyro = tag.ContainsKey("trGyro") ? tag.Get<System.Collections.Generic.List<int>>("trGyro") : null;
				System.Collections.Generic.List<int> oAxle = tag.ContainsKey("trAxle") ? tag.Get<System.Collections.Generic.List<int>>("trAxle") : null;
				System.Collections.Generic.List<int> oStyle = tag.ContainsKey("trStyle") ? tag.Get<System.Collections.Generic.List<int>>("trStyle") : null;
				System.Collections.Generic.List<int> oTStr = tag.ContainsKey("trTStr") ? tag.Get<System.Collections.Generic.List<int>>("trTStr") : null;
				System.Collections.Generic.List<int> oEn = tag.ContainsKey("trEn") ? tag.Get<System.Collections.Generic.List<int>>("trEn") : null;
				System.Collections.Generic.List<string> oName = tag.ContainsKey("trName") ? tag.Get<System.Collections.Generic.List<string>>("trName") : null;
				System.Collections.Generic.List<int> oAOC = tag.ContainsKey("trAOC") ? tag.Get<System.Collections.Generic.List<int>>("trAOC") : null;
				System.Collections.Generic.List<int> oShare = tag.ContainsKey("trShare") ? tag.Get<System.Collections.Generic.List<int>>("trShare") : null;
				System.Collections.Generic.List<int> oKills = tag.ContainsKey("trKills") ? tag.Get<System.Collections.Generic.List<int>>("trKills") : null;
				System.Collections.Generic.List<int> oAlarm = tag.ContainsKey("trAlarm") ? tag.Get<System.Collections.Generic.List<int>>("trAlarm") : null;
				System.Collections.Generic.List<int> oScope = tag.ContainsKey("trScope") ? tag.Get<System.Collections.Generic.List<int>>("trScope") : null;
				System.Collections.Generic.List<int> oReserve = tag.ContainsKey("trReserve") ? tag.Get<System.Collections.Generic.List<int>>("trReserve") : null;
				System.Collections.Generic.List<int> oWind = tag.ContainsKey("trWind") ? tag.Get<System.Collections.Generic.List<int>>("trWind") : null;
				System.Collections.Generic.List<int> oSStr = tag.ContainsKey("trSStr") ? tag.Get<System.Collections.Generic.List<int>>("trSStr") : null;
				System.Collections.Generic.List<int> oTreat = tag.ContainsKey("trTreat") ? tag.Get<System.Collections.Generic.List<int>>("trTreat") : null;
				System.Collections.Generic.List<int> oWrap = tag.ContainsKey("trWrap") ? tag.Get<System.Collections.Generic.List<int>>("trWrap") : null;
				System.Collections.Generic.List<int> oSched = tag.ContainsKey("trSched") ? tag.Get<System.Collections.Generic.List<int>>("trSched") : null;
				System.Collections.Generic.List<int> oPulse = tag.ContainsKey("trPulse") ? tag.Get<System.Collections.Generic.List<int>>("trPulse") : null;
				System.Collections.Generic.List<int> oExp = tag.ContainsKey("trExp") ? tag.Get<System.Collections.Generic.List<int>>("trExp") : null;
				int n = xs.Count;
				n = Math.Min(n, ys.Count); n = Math.Min(n, kinds.Count);
				for (int i = 0; i < n; i++) {
					var st = new TrapState {
						Kind = (TrapKind)Math.Clamp(kinds[i], 0, 4),
						Level = Math.Clamp(levels[i], 0, 7),
						Uses = Math.Max(0, uses[i]),
						Energy = Math.Clamp(energy[i], 0, 400),
						Overcharge = over[i] != 0,
						Cooldown = Math.Max(0, cd[i]),
						Range = Math.Clamp(rng[i], 2, 30),
						Damage = Math.Clamp(dmg[i], 1, 500),
						Duration = Math.Clamp(dur[i], 60, 3600),
						CooldownMax = Math.Clamp(cdmax[i], 20, 3600),
						StringVariant = Math.Clamp(svar[i], 0, 7),
						BearingTier = Math.Clamp(btier[i], 0, 2),
						CounterType = Math.Clamp(ctype[i], 0, 9),
						Priority = Math.Clamp(pri[i], 0, 2),
						Behavior = Math.Clamp(beh[i], 0, 4),
						Restraint = Math.Clamp(rest[i], 0, 10),
						Bend = Math.Clamp(bend[i], 0, 2),
						MaxLen = Math.Clamp(maxlen[i], 60, 600),
						OutSpeed = Math.Clamp(outs[i], 5, 200),
						ReturnSpeed = Math.Clamp(rets[i], 5, 300),
						Burst = Math.Clamp(burst[i], 1, 5),
						WiredActive = wired[i] != 0,
						HitPlayers = hp[i] != 0, HitNPCs = hn[i] != 0, HitTown = ht[i] != 0,
						HitHostile = hh[i] != 0, HitFriendly = hf[i] != 0, HitAll = ha[i] != 0,
						LinkX = lx[i], LinkY = ly[i],
						AimAngle = (aim == null || i >= aim.Count) ? 0f : aim[i],
						SentryAegis = (oAegis == null || i >= oAegis.Count) ? false : oAegis[i] != 0,
						WardLattice = (oWard == null || i >= oWard.Count) ? false : oWard[i] != 0,
						SnareTip = (oSnare == null || i >= oSnare.Count) ? false : oSnare[i] != 0,
						BarbedWire = (oBarb == null || i >= oBarb.Count) ? false : oBarb[i] != 0,
						TracerFire = (oTrac == null || i >= oTrac.Count) ? false : oTrac[i] != 0,
						TwinThrow = (oTwin == null || i >= oTwin.Count) ? false : oTwin[i] != 0,
						Stress = (oStress == null || i >= oStress.Count) ? 0 : System.Math.Clamp(oStress[i], 0, 500),
						Frayed = (oFray == null || i >= oFray.Count) ? 0 : System.Math.Max(0, oFray[i]),
						UpgradePending = (oPend == null || i >= oPend.Count) ? false : oPend[i] != 0,
						Condition = (oCond == null || i >= oCond.Count) ? 100 : System.Math.Clamp(oCond[i], 0, 100),
						CondTick = (oCTick == null || i >= oCTick.Count) ? 0 : System.Math.Max(0, oCTick[i]),
						SentryBunker = (oBunk == null || i >= oBunk.Count) ? false : oBunk[i] != 0,
						LongSpan = (oSpan == null || i >= oSpan.Count) ? false : oSpan[i] != 0,
						HairTrigger = (oHair == null || i >= oHair.Count) ? false : oHair[i] != 0,
						RegenWeave = (oRegen == null || i >= oRegen.Count) ? 0 : System.Math.Clamp(oRegen[i], 0, 3),
						TensionWeave = (oTens == null || i >= oTens.Count) ? 0 : System.Math.Clamp(oTens[i], 0, 3),
						LinkStress = (oLStress == null || i >= oLStress.Count) ? 0 : System.Math.Clamp(oLStress[i], 0, TrapState.MaxLinkStress),
						AutoRewire = (oAuto == null || i >= oAuto.Count) ? false : oAuto[i] != 0,
						SentryAnchor = (oAnchor == null || i >= oAnchor.Count) ? 2 : System.Math.Clamp(oAnchor[i], 0, 2),
						LinkCoil = (oCoil == null || i >= oCoil.Count) ? 0 : System.Math.Clamp(oCoil[i], 0, 3),
						SentryGyro = (oGyro == null || i >= oGyro.Count) ? 0 : System.Math.Clamp(oGyro[i], 0, 3),
						TripAxle = (oAxle == null || i >= oAxle.Count) ? 0 : System.Math.Clamp(oAxle[i], 0, 3),
						TripStyle = (oStyle == null || i >= oStyle.Count) ? 0 : System.Math.Clamp(oStyle[i], 0, 7),
						TripString = (oTStr == null || i >= oTStr.Count) ? 0 : System.Math.Clamp(oTStr[i], 0, 24),
						Enabled = (oEn == null || i >= oEn.Count) ? true : oEn[i] != 0,
						Name = (oName == null || i >= oName.Count || string.IsNullOrEmpty(oName[i])) ? "" : oName[i].Substring(0, System.Math.Min(24, oName[i].Length)),
						AutoOC = (oAOC == null || i >= oAOC.Count) ? false : oAOC[i] != 0,
						ShareMode = (oShare == null || i >= oShare.Count) ? 0 : System.Math.Clamp(oShare[i], 0, 2),
						Kills = (oKills == null || i >= oKills.Count) ? 0 : System.Math.Max(0, oKills[i]),
						AlarmIdleOnly = (oAlarm == null || i >= oAlarm.Count) ? false : oAlarm[i] != 0,
						SentryScope = (oScope == null || i >= oScope.Count) ? 0 : System.Math.Clamp(oScope[i], 0, 3),
						LinkReserve = (oReserve == null || i >= oReserve.Count) ? 0 : System.Math.Clamp(oReserve[i], 0, 3),
						TripWind = (oWind == null || i >= oWind.Count) ? 0 : System.Math.Clamp(oWind[i], 0, 3),
						SentryString = (oSStr == null || i >= oSStr.Count) ? 0 : System.Math.Clamp(oSStr[i], 0, 7),
						LinkTreat = (oTreat == null || i >= oTreat.Count) ? 0 : System.Math.Clamp(oTreat[i], 0, 2),
						LinkBarb = (oWrap == null || i >= oWrap.Count) ? 0 : System.Math.Clamp(oWrap[i], 0, 3),
						Sched = (oSched == null || i >= oSched.Count) ? 0 : System.Math.Clamp(oSched[i], 0, 3),
						PulsePeriod = (oPulse == null || i >= oPulse.Count) ? 30 : (oPulse[i] == 10 ? 10 : oPulse[i] == 60 ? 60 : 30),
						ExpHeads = (oExp == null || i >= oExp.Count) ? 0 : oExp[i],
					};
					// Legacy migration: traps saved before target filters existed
					// stored all-false (which now means "hit nothing"). Restore
					// the behavior those traps always had (Sentry: hostile only;
					// Link: players + hostile + ordinary). A deliberately
					// all-off trap cannot survive a save/load (degenerate).
					// Old Barbed Filament stance converts to Barbed Wrap Lv1.
					if (!st.HitPlayers && !st.HitNPCs && !st.HitTown && !st.HitHostile && !st.HitFriendly && !st.HitAll) {
						if (st.Kind == TrapKind.Link) { st.HitPlayers = true; st.HitNPCs = true; st.HitHostile = true; }
						else if (st.Kind == TrapKind.Sentry) { st.HitHostile = true; }
					}
					if (st.Kind == TrapKind.Link && st.BarbedWire) {
						st.BarbedWire = false;
						if (st.LinkBarb <= 0)
							st.LinkBarb = 1;
					}
					Traps[new Point(xs[i], ys[i])] = st;
				}
			}
			catch { Traps.Clear(); }
		}

		public override void NetSend(BinaryWriter w)
		{
			w.Write(Traps.Count);
			foreach (var kv in Traps) {
				w.Write(kv.Key.X); w.Write(kv.Key.Y);
				var st = kv.Value;
				w.Write((byte)st.Kind); w.Write(st.Level); w.Write(st.Uses); w.Write(st.Energy);
				w.Write(st.Overcharge); w.Write(st.Cooldown); w.Write(st.ActiveTimer);
				w.Write(st.Range); w.Write(st.Damage); w.Write(st.Duration); w.Write(st.CooldownMax);
				w.Write(st.StringVariant); w.Write(st.BearingTier); w.Write(st.CounterType);
				w.Write(st.Priority); w.Write(st.Behavior); w.Write(st.Restraint); w.Write(st.Bend);
				w.Write(st.MaxLen); w.Write(st.OutSpeed); w.Write(st.ReturnSpeed); w.Write(st.Burst);
				w.Write(st.WiredActive);
				w.Write(st.HitPlayers); w.Write(st.HitNPCs); w.Write(st.HitTown);
				w.Write(st.HitHostile); w.Write(st.HitFriendly); w.Write(st.HitAll);
				w.Write(st.LinkX); w.Write(st.LinkY); w.Write(st.SentryAegis); w.Write(st.WardLattice); w.Write(st.SnareTip); w.Write(st.SentryBunker); w.Write(st.LongSpan); w.Write(st.HairTrigger); w.Write(st.BarbedWire); w.Write(st.TracerFire); w.Write(st.TwinThrow); w.Write(st.Stress); w.Write(st.Frayed); w.Write(st.Condition); w.Write(st.CondTick); w.Write(st.UpgradePending); w.Write(st.AimAngle);
				w.Write(System.Math.Clamp(st.RegenWeave, 0, 3)); w.Write(System.Math.Clamp(st.TensionWeave, 0, 3)); w.Write(st.AutoRewire); w.Write(System.Math.Clamp(st.SentryAnchor, 0, 2));
				w.Write(System.Math.Clamp(st.LinkCoil, 0, 3)); w.Write(System.Math.Clamp(st.SentryGyro, 0, 3)); w.Write(System.Math.Clamp(st.TripAxle, 0, 3)); w.Write(System.Math.Clamp(st.TripStyle, 0, 7)); w.Write(System.Math.Clamp(st.TripString, 0, 24)); w.Write(st.Enabled); w.Write(st.Name ?? ""); w.Write(st.AutoOC); w.Write(System.Math.Clamp(st.ShareMode, 0, 2)); w.Write(System.Math.Max(0, st.Kills)); w.Write(st.AlarmIdleOnly); w.Write(System.Math.Clamp(st.SentryScope, 0, 3)); w.Write(System.Math.Clamp(st.LinkReserve, 0, 3)); w.Write(System.Math.Clamp(st.TripWind, 0, 3)); w.Write(System.Math.Clamp(st.SentryString, 0, 7)); w.Write(System.Math.Clamp(st.LinkTreat, 0, 2)); w.Write(System.Math.Clamp(st.LinkBarb, 0, 3)); w.Write(System.Math.Clamp(st.Sched, 0, 3)); w.Write(st.PulsePeriod == 10 ? 10 : st.PulsePeriod == 60 ? 60 : 30); w.Write(st.ExpHeads); w.Write(System.Math.Clamp(st.LinkStress, 0, TrapState.MaxLinkStress));
			}
		}

		public override void NetReceive(BinaryReader r)
		{
			Traps.Clear();
			int n = r.ReadInt32();
			n = Math.Clamp(n, 0, 2000);
			for (int i = 0; i < n; i++) {
				var key = new Point(r.ReadInt32(), r.ReadInt32());
				var st = new TrapState {
					Kind = (TrapKind)Math.Clamp((int)r.ReadByte(), 0, 4),
					Level = Math.Clamp(r.ReadInt32(), 0, 7),
					Uses = Math.Max(0, r.ReadInt32()),
					Energy = Math.Clamp(r.ReadInt32(), 0, 400),
					Overcharge = r.ReadBoolean(),
					Cooldown = Math.Max(0, r.ReadInt32()),
					ActiveTimer = Math.Max(0, r.ReadInt32()),
					Range = Math.Clamp(r.ReadInt32(), 2, 30),
					Damage = Math.Clamp(r.ReadInt32(), 1, 500),
					Duration = Math.Clamp(r.ReadInt32(), 60, 3600),
					CooldownMax = Math.Clamp(r.ReadInt32(), 20, 3600),
					StringVariant = Math.Clamp(r.ReadInt32(), 0, 7),
					BearingTier = Math.Clamp(r.ReadInt32(), 0, 2),
					CounterType = Math.Clamp(r.ReadInt32(), 0, 9),
					Priority = Math.Clamp(r.ReadInt32(), 0, 2),
					Behavior = Math.Clamp(r.ReadInt32(), 0, 4),
					Restraint = Math.Clamp(r.ReadInt32(), 0, 10),
					Bend = Math.Clamp(r.ReadInt32(), 0, 2),
					MaxLen = Math.Clamp(r.ReadInt32(), 60, 600),
					OutSpeed = Math.Clamp(r.ReadInt32(), 5, 200),
					ReturnSpeed = Math.Clamp(r.ReadInt32(), 5, 300),
					Burst = Math.Clamp(r.ReadInt32(), 1, 5),
					WiredActive = r.ReadBoolean(),
					HitPlayers = r.ReadBoolean(), HitNPCs = r.ReadBoolean(), HitTown = r.ReadBoolean(),
					HitHostile = r.ReadBoolean(), HitFriendly = r.ReadBoolean(), HitAll = r.ReadBoolean(),
					LinkX = r.ReadInt32(), LinkY = r.ReadInt32(),
					SentryAegis = r.ReadBoolean(), WardLattice = r.ReadBoolean(), SnareTip = r.ReadBoolean(),
					SentryBunker = r.ReadBoolean(), LongSpan = r.ReadBoolean(), HairTrigger = r.ReadBoolean(), BarbedWire = r.ReadBoolean(), TracerFire = r.ReadBoolean(), TwinThrow = r.ReadBoolean(), Stress = r.ReadInt32(), Frayed = r.ReadInt32(), Condition = r.ReadInt32(), CondTick = r.ReadInt32(), UpgradePending = r.ReadBoolean(),
					AimAngle = r.ReadSingle(),
				};
				try {
					st.RegenWeave = Math.Clamp(r.ReadInt32(), 0, 3);
					st.TensionWeave = Math.Clamp(r.ReadInt32(), 0, 3);
					st.AutoRewire = r.ReadBoolean();
					st.SentryAnchor = Math.Clamp(r.ReadInt32(), 0, 2);
					st.Stress = Math.Clamp(st.Stress, 0, 500);
				}
				catch { }
				try {
					st.LinkCoil = Math.Clamp(r.ReadInt32(), 0, 3);
					st.SentryGyro = Math.Clamp(r.ReadInt32(), 0, 3);
					st.TripAxle = Math.Clamp(r.ReadInt32(), 0, 3);
					st.TripStyle = Math.Clamp(r.ReadInt32(), 0, 7);
					st.TripString = Math.Clamp(r.ReadInt32(), 0, 24);
					st.Enabled = r.ReadBoolean();
					st.Name = r.ReadString() ?? "";
					if (st.Name.Length > 24)
						st.Name = st.Name.Substring(0, 24);
					st.AutoOC = r.ReadBoolean();
					st.ShareMode = Math.Clamp(r.ReadInt32(), 0, 2);
					st.Kills = Math.Max(0, r.ReadInt32());
					st.AlarmIdleOnly = r.ReadBoolean();
					st.SentryScope = Math.Clamp(r.ReadInt32(), 0, 3);
					st.LinkReserve = Math.Clamp(r.ReadInt32(), 0, 3);
					st.TripWind = Math.Clamp(r.ReadInt32(), 0, 3);
					st.SentryString = Math.Clamp(r.ReadInt32(), 0, 7);
					st.LinkTreat = Math.Clamp(r.ReadInt32(), 0, 2);
					try { st.LinkBarb = Math.Clamp(r.ReadInt32(), 0, 3); } catch { }
					try { st.Sched = Math.Clamp(r.ReadInt32(), 0, 3); } catch { }
					try {
						int pp = r.ReadInt32();
						st.PulsePeriod = pp == 10 ? 10 : pp == 60 ? 60 : 30;
					try { st.ExpHeads = r.ReadInt32(); } catch { st.ExpHeads = 0; }
					try { st.LinkStress = Math.Clamp(r.ReadInt32(), 0, TrapState.MaxLinkStress); } catch { st.LinkStress = 0; }
					}
					catch { }
				}
				catch { }
				Traps[key] = st;
			}
		}
	}
}
