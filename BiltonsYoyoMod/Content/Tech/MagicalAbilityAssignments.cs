using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// Named catalogue of every non-beam ability in the library. The assignment
	/// system deals from this list in order, and both the built-in keyword table
	/// and the user-editable assignment file refer to abilities by these names.
	/// The sustained beam is assignable too, under the special name
	/// "OrbitalConvergence" (or "Beam"), but it is never dealt at random.
	/// </summary>
	public static class MagicalAbilityCatalog
	{
		public static readonly (string Name, MagicalAbilityProfile Profile)[] Entries = {
			// ARCHIVED (cursor-focus rework, classes kept in MagicalAbilityLibrary.cs):
			// AegisBloom, WardingTotem, MirrorGuardWall, ChronoField, PortalBarrage,
			// LashWhip, OrbitalLance.
			("RiftTear", new RiftTearAbility()),
			("StarburstVolley", new StarburstVolleyAbility()),
			("PrismGate", new PrismGateAbility()),
			("CircuitRune", new CircuitRuneAbility()),
			("ChainLattice", new ChainLatticeAbility()),
			("IonLance", new IonLanceAbility()),
			("ChainVerdict", new ChainVerdictAbility()),
			("BladeRing", new BladeRingAbility()),
			("NovaDetonation", new NovaDetonationAbility()),
			// ARCHIVED (cursor-focus rework, classes kept in MagicalAbilityLibrary.cs):
			// WardingTotem, MirrorGuardWall, ChronoField, PortalBarrage, LashWhip, OrbitalLance.
			("SanguineLeech", new SanguineLeechAbility()),
			("CrystalGarden", new CrystalGardenAbility()),
			("ThunderclapDash", new ThunderclapDashAbility()),
			("GravitonSnare", new GravitonSnareAbility()),
			("PhoenixFlare", new PhoenixFlareAbility()),
			("EchoTwins", new EchoTwinsAbility()),
			("BladeCyclone", new BladeCycloneAbility()),
			("SanctumWard", new SanctumWardAbility()),
			("StormRing", new StormRingAbility()),
			("FrostboundGrasp", new FrostboundGraspAbility()),
			("BlinkSlash", new BlinkSlashAbility()),
			("MoltenGeyser", new MoltenGeyserAbility()),
			("SeekingAnchors", new SeekingAnchorsAbility()),
			("HaloSentries", new HaloSentriesAbility()),
			("PrismaticPrism", new PrismaticPrismAbility()),
			("SpotlightJudgement", new SpotlightJudgementAbility()),
			("LeechBramble", new LeechBrambleAbility()),
			("BubblePrison", new BubblePrisonAbility()),
			("RunewardenSigil", new RunewardenSigilAbility()),
			("EchoBarrage", new EchoBarrageAbility()),
			("VoidCollapse", new VoidCollapseAbility()),
			("StarfallVerdict", new StarfallVerdictAbility()),
			("ThunderstormCrown", new ThunderstormCrownAbility()),
			("MirrorMaze", new MirrorMazeAbility()),
			("ChronoAnchor", new ChronoAnchorAbility()),
			("BulwarkCharge", new BulwarkChargeAbility()),
			("SanctifiedCrown", new SanctifiedCrownAbility()),
			("KaleidoscopeNova", new KaleidoscopeNovaAbility()),
			("SoulReap", new SoulReapAbility()),
			("PrismCage", new PrismCageAbility()),
			("PyreColumn", new PyreColumnAbility()),

			// ---- Cursor rework wave: wells, feeder, brands ----
			("VertexWell", new VertexWellAbility()),
			("SteeringWell", new SteeringWellAbility()),
			("SeekingWell", new SeekingWellAbility()),
			("RapidWell", new RapidWellAbility()),
			("WormWell", new WormWellAbility()),
			("FractureWell", new FractureWellAbility()),
			("OrbitFeeder", new OrbitFeederAbility()),
			("LifedrainNova", new LifedrainNovaAbility()),
			("WeaverSine", new WeaverSineAbility()),
			("WeaverSpiral", new WeaverSpiralAbility()),
			("WeaverSaw", new WeaverSawAbility()),
			("WeaverTriangle", new WeaverTriangleAbility()),
			("WeaverDamped", new WeaverDampedAbility()),
			("WeaverFigure", new WeaverFigureAbility()),
			("GrowingStar", new GrowingStarAbility()),
			("PulseCascade", new PulseCascadeAbility()),
			("BrandEmber", new BrandEmberAbility()),
			("BrandFrost", new BrandFrostAbility()),
			("BrandStorm", new BrandStormAbility()),
			("BrandVoid", new BrandVoidAbility()),

			// ---- Cursor wave 2: the approved board + the lightning puddle ----
			("LightningWell", new LightningWellAbility()),
			("SnaptrapBloom", new SnaptrapBloomAbility()),
			("AnchorChain", new AnchorChainAbility()),
			("PocketSingularity", new PocketSingularityAbility()),

			// ---- Storm second pass (2026-10-20) ----
			// Appended, not inserted: existing auto-dealt yoyos keep their
			// positions in the free list. The six replaced abilities below are
			// marked Retired so they stay assignable by file but never enter
			// the random deal.
			("RelayStorm", new RelayStormAbility()),
			("FaradayLattice", new FaradayLatticeAbility()),
			("PolarityReversal", new PolarityReversalAbility()),
			("BallLightning", new BallLightningAbility()),
			("Thunderhead", new ThunderheadAbility()),
			("StaticTrail", new StaticTrailAbility()),
			("GroundFault", new GroundFaultAbility()),

			// ---- Math-curve wave pass (2026-10-21) ----
			// Appended so the existing free-list positions do not move. The
			// Core Four are assigned by the curated table / post-deal override.
			("WeaverInterference", new WeaverInterferenceAbility()),
			("WeaverAstroid", new WeaverAstroidAbility()),
			("WeaverRanunculoid", new WeaverRanunculoidAbility()),
			("WeaverKnot", new WeaverKnotAbility()),
		};

		/// <summary>
		/// Abilities that were superseded by the storm pass. They remain in the
		/// catalogue so an assignment file can still name them, but they must
		/// not be dealt at random - otherwise replacing one yoyo would silently
		/// push the old ability onto an unrelated yoyo.
		/// </summary>
		public static readonly HashSet<string> Retired = new(StringComparer.OrdinalIgnoreCase) {
			"ChainLattice",
			"LightningWell",
			"StormRing",
			"ThunderstormCrown",
			"ThunderclapDash",
			"BrandStorm",
		};

		private static Dictionary<Type, string> displayNames;

		/// <summary>
		/// Display name for a profile: its catalogue name (or the special beam
		/// name) humanised for the charge label. Resolved once per type.
		/// </summary>
		public static string NameOf(MagicalAbilityProfile profile)
		{
			if (profile == null)
				return string.Empty;
			if (displayNames == null) {
				displayNames = new Dictionary<Type, string>();
				foreach ((string name, MagicalAbilityProfile entryProfile) in Entries)
					displayNames[entryProfile.GetType()] = name;
				if (MagicalAbilityRegistry.Default != null)
					displayNames[MagicalAbilityRegistry.Default.GetType()] = "OrbitalConvergence";
			}
			if (displayNames.TryGetValue(profile.GetType(), out string catalogueName))
				return Humanize(catalogueName);
			// Not in the catalogue (a custom profile): strip the class suffix.
			string fallback = profile.GetType().Name;
			if (fallback.EndsWith("Profile", StringComparison.Ordinal))
				fallback = fallback.Substring(0, fallback.Length - "Profile".Length);
			if (fallback.EndsWith("Ability", StringComparison.Ordinal))
				fallback = fallback.Substring(0, fallback.Length - "Ability".Length);
			return Humanize(fallback);
		}

		/// <summary>"BladeCyclone" -> "Blade Cyclone" (catalogue names are PascalCase).</summary>
		private static string Humanize(string name)
		{
			if (string.IsNullOrEmpty(name))
				return name;
			System.Text.StringBuilder sb = new System.Text.StringBuilder(name.Length + 8);
			for (int i = 0; i < name.Length; i++) {
				char c = name[i];
				if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
					sb.Append(' ');
				sb.Append(c);
			}
			return sb.ToString();
		}

		public static bool TryGet(string name, out MagicalAbilityProfile profile)
		{
			// The sustained beam is assignable by name but never dealt at random.
			if (name != null
				&& (name.Equals("OrbitalConvergence", StringComparison.OrdinalIgnoreCase)
					|| name.Equals("Beam", StringComparison.OrdinalIgnoreCase))) {
				profile = MagicalAbilityRegistry.Default;
				return true;
			}
			foreach ((string entryName, MagicalAbilityProfile entryProfile) in Entries) {
				if (string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase)) {
					profile = entryProfile;
					return true;
				}
			}
			profile = null;
			return false;
		}
	}

	/// <summary>
	/// Gives every yoyo in the game (vanilla or modded) a Magical Tech ability.
	/// Marquee yoyos are matched against a keyword table (hand-picked to suit the
	/// boss each yoyo comes from); everything else is dealt the remaining library
	/// abilities in order, so no yoyo is ever left with a bare default. Edit
	/// MagicalAbilityAssignments.txt in the mod folder to override any line.
	///
	/// The deal is fully deterministic: this mod's yoyos first, then vanilla,
	/// then other mods, each group sorted by name. The keyword table only
	/// applies to this mod's yoyos and vanilla, so another mod's content can
	/// never claim a curated ability or shift what our yoyos carry.
	/// </summary>
	public sealed class MagicalAbilityAssignments : ModSystem
	{
		/// <summary>
		/// Default keyword table. Longest keywords are matched first, so
		/// "CGuardian" is never swallowed by "Guardian".
		/// Deserves-a-beam yoyos are pointed at "OrbitalConvergence"; the beam is
		/// also guaranteed a minimum of three carriers even if the table misses.
		/// </summary>
		private static readonly (string Keyword, string Ability)[] Curated = {
			// MirrorGuardWall / WardingTotem / PortalBarrage / ChronoField / LashWhip
			// were archived in the cursor-focus rework; their yoyos auto-deal until
			// the new cursor abilities are mapped onto them.
			("CGuardian", "PrismGate"),
			("Guardian", "CircuitRune"),
			("Skeletron", "BladeRing"),
			("Destroyer", "FaradayLattice"),
			("Prime", "HaloSentries"),
			("Twins", "EchoTwins"),
			("TheEyeOfCthulhu", "WeaverKnot"), // specific, before "Eye"
			("Eye", "NovaDetonation"),
			("Plantera", "LeechBramble"),
			("Golem", "SpotlightJudgement"),
			// ---- Thematic swap chain. Santa's bubbles belonged to Duke, and
			// four separate bosses were sharing OrbitalConvergence. Every entry
			// below is a net-zero move: no ability loses its only home, and each
			// one lands on the yoyo it actually reads as.
			("Duke", "BubblePrison"),        // was RiftTear
			("Empress", "PrismaticPrism"),
			("Lunatic", "RiftTear"),         // was OrbitalConvergence (4-way dup)
			("Lord", "OrbitalConvergence"),  // reverted from IonLance: the Deathray keeps its beam, no lightning
			("Solar", "PhoenixFlare"),
			("Martian", "RelayStorm"),
			("Betsy", "MoltenGeyser"),
			("Ogre", "BulwarkCharge"),
			("Deer", "FrostboundGrasp"),
			("Dreadnautilus", "SanguineLeech"),
			("Everscream", "CrystalGarden"),
			("Pumpking", "Thunderhead"),
			("MourningWood", "PyreColumn"),  // was EchoBarrage; a burning dead tree
			("Santa", "StarburstVolley"),    // was BubblePrison; he fires ordnance
			("Royal", "GravitonSnare"),
			("Corruption", "BladeCyclone"),
			("Flesh", "SeekingAnchors"),
			("Hallow", "SanctifiedCrown"),
			("Ancient", "RunewardenSigil"),
			("Specialized", "PolarityReversal"),
			("Developer", "BlinkSlash"),
			("Random", "SanctumWard"),
			("Bee", "EchoBarrage"),          // was StarburstVolley; it is a swarm
			("TorchGod", "BrandEmber"),

			// ---- Cursor wave 2: abilities fitted to the yoyo that earns them.
			// "SuperGold" MUST stay immediately above "Gold": longest keyword
			// wins in the built-in table, and the Super Gold yoyo must never be
			// swallowed by the Gold entry.
			("SuperGold", "RapidWell"),
			("Gold", "PulseCascade"),
			("IceQueen", "BrandFrost"),
			("DarkMage", "PocketSingularity"),
			("Dutchman", "AnchorChain"),
			("Forbidden", "SnaptrapBloom"),
			("Crimson", "LifedrainNova"),
			("Cultist", "WeaverSine"),       // restored: the cult's sine rite is its signature read (ChainVerdict displaced it)
			("Enchanted", "WeaverSpiral"),
			("Heavy", "VertexWell"),
			("Illegal", "FractureWell"),
			("Lucky", "SeekingWell"),
			("Selene", "OrbitFeeder"),
			("Stringers", "SteeringWell"),

			// ---- Vanilla marquee yoyos ----
			("Terrarian", "StarfallVerdict"),
			("Kraken", "WormWell"),
			("Amarok", "WeaverAstroid"),    // ice star; Brand Frost stays on IceQueen
			("HiveFive", "WeaverRanunculoid"), // bee flower: five-petal coil
			("HelFire", "StaticTrail"),     // hellfire becomes a charged static wake
			("Valor", "GroundFault"),       // cave yoyo; Blade Ring stays on Skeletron
			("Cascade", "WeaverInterference"), // waterfall beat; Sine stays on Cultist
			("Code2", "BallLightning"),     // post-Moonlord electric orb

			// ---- Wave family, spread to the plainest yoyos ----
			// Sine and spiral were only on the Cultist/Enchanted/Cascade trio;
			// the plainest yoyos now carry the other wave shapes. Chik is left on
			// Ion Lance so at least one built-in Straight lightning yoyo remains.
			("WoodYoyo", "WeaverSaw"),      // crude, toothed, wooden
			("Gradient", "WeaverDamped"),   // one hard swing that settles
			("Rally", "WeaverTriangle"),    // the starter yoyo's straight zigzag
			("FormatC", "WeaverFigure"),    // machine-perfect figure-eight
		};

		public override void PostSetupContent()
		{
			// 1) Every distinct yoyo projectile, split into stable groups: this
			// mod's, vanilla, then other mods. Each group is sorted by name so
			// adding new content never reshuffles an existing assignment.
			List<(int Proj, string Name)> ours = new();
			List<(int Proj, string Name)> vanilla = new();
			List<(int Proj, string Name)> others = new();
			for (int type = 1; type < ItemLoader.ItemCount; type++) {
				Item item = new();
				item.SetDefaults(type);
				if (!YoyoTownShared.IsYoyoShape(item) || item.shoot <= ProjectileID.None)
					continue;
				string name = item.ModItem?.Name ?? ItemID.Search.GetName(type);
				if (item.ModItem == null)
					vanilla.Add((item.shoot, name));
				else if (item.ModItem.Mod == Mod)
					ours.Add((item.shoot, name));
				else
					others.Add((item.shoot, name));
			}
			SortByName(ours);
			SortByName(vanilla);
			SortByName(others);

			// Other mods' yoyos are opt-in (YoyoServerConfig.OtherModYoyosGetAbilities,
			// default OFF). Their load order must never gamble with our roster, and
			// a player who installs MooMoo's mod did not ask for a second ability
			// layer on every one of its 24 yoyos.
			bool includeOtherMods = false;
			try {
				Configuration.YoyoServerConfig cfg =
					ModContent.GetInstance<Configuration.YoyoServerConfig>();
				includeOtherMods = cfg != null && cfg.OtherModYoyosGetAbilities;
			}
			catch { }

			List<(int Proj, string Name, bool Curated, string Group)> yoyos = new();
			HashSet<int> seenProjectiles = new();
			foreach (List<(int Proj, string Name)> group in new[] { ours, vanilla, others }) {
				string tag = group == ours ? "mod" : (group == vanilla ? "vanilla" : "other");
				bool curated = group != others;
				if (group == others && !includeOtherMods)
					continue;
				foreach ((int proj, string name) in group) {
					if (seenProjectiles.Add(proj))
						yoyos.Add((proj, name, curated, tag));
				}
			}

			// 2) Keyword pass: file overrides first, then the built-in table.
			// This mod's yoyos and vanilla only - the keyword table is written
			// for THIS mod's content, and keeping other mods out of the pass
			// means their load order can never change what our yoyos carry.
			List<(string Keyword, string Ability)> keywords = LoadKeywordTable();
			HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
			List<(int Proj, string Name)> pending = new();
			int curatedCount = 0;
			int beamCount = 0;
			foreach ((int proj, string name, bool curated, _) in yoyos) {
				string picked = curated ? MatchKeyword(keywords, name) : null;
				if (picked == "-") {
					pending.Add((proj, name)); // explicitly auto-dealt
					continue;
				}
				if (picked != null && MagicalAbilityCatalog.TryGet(picked, out MagicalAbilityProfile profile)) {
					MagicalAbilityRegistry.Register(proj, profile);
					used.Add(picked);
					curatedCount++;
					if (profile.SpawnsBeam)
						beamCount++;
					continue;
				}
				pending.Add((proj, name));
			}

			// 3) Deal the rest from the abilities nobody claimed yet. The beam is
			// never dealt at random: if the keyword table did not put at least
			// three beams into the world, the first few dealt yoyos carry it
			// instead, so the sustained release is always playable somewhere.
			List<MagicalAbilityProfile> free = new();
			foreach ((string entryName, MagicalAbilityProfile entryProfile) in MagicalAbilityCatalog.Entries) {
				if (!used.Contains(entryName) && !MagicalAbilityCatalog.Retired.Contains(entryName))
					free.Add(entryProfile);
			}
			int beamsMissing = Math.Max(0, 3 - beamCount);
			int dealtIndex = 0;
			foreach ((int proj, string _) in pending) {
				if (beamsMissing > 0) {
					MagicalAbilityRegistry.Register(proj, MagicalAbilityRegistry.Default);
					beamsMissing--;
					beamCount++;
					continue;
				}
				if (free.Count == 0)
					break;
				MagicalAbilityRegistry.Register(proj, free[dealtIndex % free.Count]);
				dealtIndex++;
			}

			// No post-deal overrides: both CrimsonYoyos inherit LifedrainNova
			// from the shared "Crimson" keyword, and HiveFive keeps its dealt
			// ability. Nothing is shifted after the deal.
			int overrideCount = 0;

			Mod.Logger.Info($"Magical Tech abilities: {yoyos.Count} yoyos found "
				+ $"({ours.Count} this mod, {vanilla.Count} vanilla, "
				+ $"{others.Count} other mods {(includeOtherMods ? "included" : "skipped by config")}), "
				+ $"{curatedCount} hand-picked, {pending.Count} dealt from the library, "
				+ $"{beamCount} beams, {overrideCount} post-deal overrides.");

			// The full resolved roster, written next to the save. This is the
			// ground truth for "which yoyo has which ability", including every
			// auto-dealt one, and it needs no game session to read.
			try {
				System.Text.StringBuilder roster = new();
				roster.AppendLine("Magical Tech - resolved yoyo ability roster");
				roster.AppendLine("Group | Yoyo [projectile] = Ability");
				roster.AppendLine(new string('-', 64));
				foreach ((int proj, string name, _, string group) in yoyos) {
					MagicalAbilityProfile profile = MagicalAbilityRegistry.Get(proj);
					roster.AppendLine($"[{group,-7}] {name} [{proj}] = {MagicalAbilityCatalog.NameOf(profile)}");
				}
				File.WriteAllText(Path.Combine(Main.SavePath, "BiltonsYoyoMod_AbilityRoster.txt"),
					roster.ToString());
			}
			catch (Exception e) {
				Mod.Logger.Warn("Could not write the resolved ability roster: " + e.Message);
			}
		}

		/// <summary>Deterministic order: name (case-insensitive), projectile id as tiebreak.</summary>
		private static void SortByName(List<(int Proj, string Name)> list)
		{
			list.Sort((a, b) => {
				int c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
				return c != 0 ? c : a.Proj.CompareTo(b.Proj);
			});
		}

		/// <summary>File lines first (they win), then built-ins, longest keyword first.</summary>
		private List<(string Keyword, string Ability)> LoadKeywordTable()
		{
			List<(string Keyword, string Ability)> table = new();
			try {
				using Stream stream = Mod.GetFileStream("MagicalAbilityAssignments.txt");
				using StreamReader reader = new(stream);
				string line;
				while ((line = reader.ReadLine()) != null) {
					line = line.Trim();
					if (line.Length == 0 || line.StartsWith("#"))
						continue;
					int split = line.IndexOf('=');
					if (split <= 0)
						continue;
					string key = line[..split].Trim();
					string ability = line[(split + 1)..].Trim();
					if (key.Length > 0)
						table.Add((key, ability));
				}
			}
			catch {
				// No file shipped: the built-in table carries the defaults.
			}
			// Append built-ins that the file did not mention, longest first so the
			// more specific keyword always gets the first look.
			List<(string Keyword, string Ability)> builtins = new(Curated);
			builtins.Sort((a, b) => b.Keyword.Length.CompareTo(a.Keyword.Length));
			foreach ((string keyword, string ability) in builtins) {
				bool overridden = false;
				foreach ((string fileKey, string _) in table) {
					if (string.Equals(fileKey, keyword, StringComparison.OrdinalIgnoreCase)) {
						overridden = true;
						break;
					}
				}
				if (!overridden)
					table.Add((keyword, ability));
			}
			return table;
		}

		/// <summary>Returns the ability name for a yoyo, "-" for explicit
		/// auto-deal, or null when no keyword matches.</summary>
		private static string MatchKeyword(List<(string Keyword, string Ability)> table, string name)
		{
			if (string.IsNullOrEmpty(name))
				return null;
			foreach ((string keyword, string ability) in table) {
				if (keyword.Length == 0)
					continue;
				if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
					return ability;
			}
			return null;
		}
	}
}
