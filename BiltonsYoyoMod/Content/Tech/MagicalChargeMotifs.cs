using System;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// The charge signature of a release. Every ability used to channel with the
	/// exact same field, which made the whole library read as one ability; the
	/// motif picks the small accent pieces layered over the shared core so each
	/// yoyo's channel looks like ITS release. The metaball core, the poured
	/// pool and the containment seal are shared - only the accent changes.
	/// </summary>
	public enum MagicalChargeMotif
	{
		Arcane, // baseline: rune arcs + one small star
		Blades, // crescent blades orbiting in
		Frost,  // needles closing + a six-flake
		Flame,  // rising embers
		Storm,  // arcing static
		Void,   // dark lens + inward sweep
		Holy,   // pulsing soft rays
		Thorn,  // hook thorns tightening
		Chrono, // clock ticks
		Blood,  // drifting droplets
		Star    // twinkling sparkles
	}

	/// <summary>
	/// Class-name -> motif table. Kept in one place so the whole library's
	/// charge identities can be re-tuned (or a new ability added) without
	/// touching the ability classes themselves. Unknown abilities fall back to
	/// Arcane, which is also what the sustained beam uses.
	/// </summary>
	internal static class MagicalChargeMotifs
	{
		private static readonly Dictionary<string, MagicalChargeMotif> table = new(StringComparer.Ordinal) {
			["AegisBloomAbility"] = MagicalChargeMotif.Holy,
			["RiftTearAbility"] = MagicalChargeMotif.Void,
			["StarburstVolleyAbility"] = MagicalChargeMotif.Star,
			["ChainLatticeAbility"] = MagicalChargeMotif.Storm,
			["IonLanceAbility"] = MagicalChargeMotif.Storm,
			["ChainVerdictAbility"] = MagicalChargeMotif.Storm,
			["BladeRingAbility"] = MagicalChargeMotif.Blades,
			["NovaDetonationAbility"] = MagicalChargeMotif.Arcane,
			["WardingTotemAbility"] = MagicalChargeMotif.Holy,
			["MirrorGuardWallAbility"] = MagicalChargeMotif.Arcane,
			["ChronoFieldAbility"] = MagicalChargeMotif.Chrono,
			["PortalBarrageAbility"] = MagicalChargeMotif.Void,
			["LashWhipAbility"] = MagicalChargeMotif.Thorn,
			["OrbitalLanceAbility"] = MagicalChargeMotif.Star,
			["SanguineLeechAbility"] = MagicalChargeMotif.Blood,
			["CrystalGardenAbility"] = MagicalChargeMotif.Frost,
			["ThunderclapDashAbility"] = MagicalChargeMotif.Storm,
			["GravitonSnareAbility"] = MagicalChargeMotif.Void,
			["PhoenixFlareAbility"] = MagicalChargeMotif.Flame,
			["EchoTwinsAbility"] = MagicalChargeMotif.Arcane,
			["BladeCycloneAbility"] = MagicalChargeMotif.Blades,
			["SanctumWardAbility"] = MagicalChargeMotif.Holy,
			["StormRingAbility"] = MagicalChargeMotif.Storm,
			["FrostboundGraspAbility"] = MagicalChargeMotif.Frost,
			["BlinkSlashAbility"] = MagicalChargeMotif.Blades,
			["MoltenGeyserAbility"] = MagicalChargeMotif.Flame,
			["SeekingAnchorsAbility"] = MagicalChargeMotif.Thorn,
			["HaloSentriesAbility"] = MagicalChargeMotif.Holy,
			["PrismaticPrismAbility"] = MagicalChargeMotif.Star,
			["SpotlightJudgementAbility"] = MagicalChargeMotif.Holy,
			["LeechBrambleAbility"] = MagicalChargeMotif.Thorn,
			["BubblePrisonAbility"] = MagicalChargeMotif.Frost,
			["RunewardenSigilAbility"] = MagicalChargeMotif.Arcane,
			["EchoBarrageAbility"] = MagicalChargeMotif.Star,
			["VoidCollapseAbility"] = MagicalChargeMotif.Void,
			["StarfallVerdictAbility"] = MagicalChargeMotif.Star,
			["ThunderstormCrownAbility"] = MagicalChargeMotif.Storm,
			["MirrorMazeAbility"] = MagicalChargeMotif.Arcane,
			["ChronoAnchorAbility"] = MagicalChargeMotif.Chrono,
			["BulwarkChargeAbility"] = MagicalChargeMotif.Arcane,
			["SanctifiedCrownAbility"] = MagicalChargeMotif.Holy,
			["KaleidoscopeNovaAbility"] = MagicalChargeMotif.Star,
			["SoulReapAbility"] = MagicalChargeMotif.Blood,
			["PrismCageAbility"] = MagicalChargeMotif.Star,
			["PyreColumnAbility"] = MagicalChargeMotif.Flame,
			["VertexWellAbility"] = MagicalChargeMotif.Storm,
			["SteeringWellAbility"] = MagicalChargeMotif.Arcane,
			["SeekingWellAbility"] = MagicalChargeMotif.Star,
			["RapidWellAbility"] = MagicalChargeMotif.Storm,
			["WormWellAbility"] = MagicalChargeMotif.Thorn,
			["FractureWellAbility"] = MagicalChargeMotif.Blades,
			["OrbitFeederAbility"] = MagicalChargeMotif.Chrono,
			["LifedrainNovaAbility"] = MagicalChargeMotif.Blood,
			["WeaverSineAbility"] = MagicalChargeMotif.Star,
			["WeaverSpiralAbility"] = MagicalChargeMotif.Void,
			["WeaverInterferenceAbility"] = MagicalChargeMotif.Star,
			["WeaverAstroidAbility"] = MagicalChargeMotif.Star,
			["WeaverRanunculoidAbility"] = MagicalChargeMotif.Star,
			["WeaverKnotAbility"] = MagicalChargeMotif.Void,
			["GrowingStarAbility"] = MagicalChargeMotif.Star,
			["PulseCascadeAbility"] = MagicalChargeMotif.Storm,
			["BrandEmberAbility"] = MagicalChargeMotif.Flame,
			["BrandFrostAbility"] = MagicalChargeMotif.Frost,
			["BrandStormAbility"] = MagicalChargeMotif.Storm,
			["BrandVoidAbility"] = MagicalChargeMotif.Void,
			["LightningWellAbility"] = MagicalChargeMotif.Storm,
			["SnaptrapBloomAbility"] = MagicalChargeMotif.Thorn,
			["AnchorChainAbility"] = MagicalChargeMotif.Chrono,
			["PocketSingularityAbility"] = MagicalChargeMotif.Void,
			["GroundFaultAbility"] = MagicalChargeMotif.Storm,
		};

		public static MagicalChargeMotif Resolve(string abilityClass)
		{
			if (abilityClass != null && table.TryGetValue(abilityClass, out MagicalChargeMotif motif))
				return motif;
			return MagicalChargeMotif.Arcane;
		}
	}
}
