using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace BiltonsYoyoMod.Configuration
{
	// Moved from Content (same type name, so enum localization still binds).
	public enum YoyoGuardianDifficulty
	{
		Standard,
		Hell
	}

	/// <summary>
	/// Two readings of Ground Fault, selected at cast time.
	/// A: GroundHugger - the bolt follows the surface below its path, turns at
	///    walls, drops off ledges, and strikes upward at enemies it passes.
	/// B: GravityBounce - the bolt has real gravity, bounces off tiles and
	///    loses height/speed per bounce.
	/// </summary>
	public enum GroundFaultMode
	{
		GroundHugger,
		GravityBounce
	}

	// Merged server-side configs: YoyoGuardianConfig (difficulty) +
	// TrapPhysicsConfig + TrapGenConfig + TrapNotifyConfig + PaintingGenConfig.
	// All defaults reproduce the old behavior exactly. Old per-class config
	// files reset to these defaults once (same values, no action needed).
	public class YoyoServerConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		[Header("Guardian")]
		[DefaultValue(YoyoGuardianDifficulty.Standard)]
		public YoyoGuardianDifficulty Difficulty;

		public float HPMultiplier => Difficulty == YoyoGuardianDifficulty.Hell ? 3f : 1f;
		public float DamageMultiplier => Difficulty == YoyoGuardianDifficulty.Hell ? 1.5f : 1f;

		[Header("TrapPhysics")]
		[Range(2, 5)]
		[DefaultValue(5)]
		public int RopeIterations = 5;

		[Range(1, 2)]
		[DefaultValue(2)]
		public int RopeSubsteps = 2;

		[Range(1, 4)]
		[DefaultValue(1)]
		public int StrainCadence = 1;

		[DefaultValue(true)]
		public bool RopeBuzz = true;

		[Header("TrapSpawns")]
		[DefaultValue(true)]
		public bool SpawnWildstringSnareInDungeon = true;

		[DefaultValue(false)]
		public bool SpawnWildstringSnareInCaverns = false;

		[DefaultValue(true)]
		public bool SpawnBearingTrapInDungeon = true;

		[DefaultValue(false)]
		public bool SpawnBearingTrapInCaverns = false;

		[Header("TrapAlerts")]
		[DefaultValue(true)]
		public bool AnnounceVeteranMilestones = true;

		[Header("Decor")]
		[DefaultValue(true)]
		public bool ReplaceVanillaPaintings = true;

		[Header("MiniBossYoyos")]
		// DarkMage yoyo ritual cadence. The yoyo drops a stationary seal that
		// fires homing magic bolts; this is how many seconds of continuous
		// deployment pass between drops. 2s is the mod default (bolt-star
		// testing/feel); the old hardcoded value was 20s. The yoyo's ~22s
		// lifetime still caps how many can exist per throw, and the seal keeps
		// its own 10s life, so short values naturally overlap.
		[Range(1f, 20f)]
		[DefaultValue(2f)]
		public float DarkMageRitualCooldownSeconds = 2f;

		[Header("MagicalTech")]
		// Right-click abilities are OPT-IN. Left-click orbit guards are not
		// affected: only the charge-up field and the per-yoyo releases are
		// gated. Default OFF so a fresh install never changes how a yoyo feels.
		[DefaultValue(false)]
		public bool EnableMagicalAbilities = false;

		// Applied to both the one-off start cost and the per-second upkeep of
		// every magical release. 1 = the tuned default; raise it if mana still
		// feels free on a high-regen build.
		[Range(0.5f, 5f)]
		[DefaultValue(1f)]
		public float MagicalAbilityManaMultiplier = 1f;

		// Whether yoyos from OTHER mods (e.g. MooMoo's Ultimate Yoyo Revamp) are
		// auto-dealt one of this mod's Magical Tech abilities. Default OFF: our
		// tech only changes our own yoyos and vanilla yoyos unless the player
		// opts in. Takes effect after the next mod reload (assignments are
		// resolved once at load and the resolved roster is written to
		// BiltonsYoyoMod_AbilityRoster.txt).
		[DefaultValue(false)]
		public bool OtherModYoyosGetAbilities = false;

		// Ground Fault concept switch. Default is the bounce reading; the
		// surface-hugging reading is available without another config pass.
		[Header("GroundFault")]
		[DefaultValue(GroundFaultMode.GravityBounce)]
		public GroundFaultMode GroundFault = GroundFaultMode.GravityBounce;
	}
}
