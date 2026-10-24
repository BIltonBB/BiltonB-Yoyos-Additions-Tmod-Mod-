using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace BiltonsYoyoMod.Configuration
{
	public enum StorageUIStyle
	{
		Standard,
		Fancy
	}

	public enum YoyoVisualQuality
	{
		Full,
		Low
	}

	// Moved from Content.Systems (same type name, so enum localization still binds).
	public enum AfterMusicMode
	{
		Always,
		On,
		Off
	}

	// Arcane star crown for the DarkMage ritual's magic bolt. Off = the old
	// behavior (vanilla Excalibur sparkle on hit). Variants 1-5 are the original
	// smooth soft-flare stars (see MiniYoyoFx.DrawArcaneStar), restored to the
	// look they always had - this pass only added matched stamp particles to
	// them, and they still draw in the normal alpha-blended batch. Every one
	// snaps in and then pulses in its own way - none of them spin:
	//   1 = cool violet/white "lunar" 8-point whose points telescope
	//   2 = warm "solar" anamorphic cross flare (long slit + short one, no orbs)
	//   3 = magenta "rift" whose needles slice open over its life
	//   4 = "nova" multi-lobe bloom + expanding shockwave ring + spike cross
	//   5 = "prism" chromatic split flare (red/blue rim, diamond core)
	// Wildcard set (6-36): deliberately NOT stars, each a literal, immediately
	// readable archetype with its OWN colour palette - a crescent, a ground
	// slam, a lance thrust, a vortex whirlpool, a crystal shatter, a petal
	// bloom, a chain arc, a rune sigil, a piercing ray, an orbiting halo, a
	// directional slash, a gold crosscut, expanding ripples, a spike urchin, a
	// comet, a fire eruption, a ring of bolts, thorn tendrils, a nebula cloud,
	// a void tear and a converging salvo; then a second wave that deliberately
	// leaves the slash/lightning/orb/star families behind entirely - aurora
	// curtains, a spider web, a biting maw, an eye, a sawblade, a magnetic
	// field, a sonic burst, confetti, digital glitch blocks and a portal gate.
	// Placeholders in the sense that any attack site may borrow any of them;
	// every one is built from the same soft-lens/ring/stripe/line primitives,
	// and 6-41 draw through an additive window so they read as emitted light.
	// Read live, no reload needed. Kept as a single style enum so later attack
	// sites can borrow a look. New members are appended so existing config
	// values keep their meaning.
	public enum ArcaneStarStyle
	{
		Off,
		Variant1,
		Variant2,
		Variant3,
		Variant4,
		Variant5,
		Variant6,
		Variant7,
		Variant8,
		Variant9,
		Variant10,
		Variant11,
		Variant12,
		Variant13,
		Variant14,
		Variant15,
		Variant16,
		Variant17,
		Variant18,
		Variant19,
		Variant20,
		Variant21,
		Variant22,
		Variant23,
		Variant24,
		Variant25,
		Variant26,
		Variant27,
		Variant28,
		Variant29,
		Variant30,
		Variant31,
		Variant32,
		Variant33,
		Variant34,
		Variant35,
		Variant36,
		// 37-41: the PULSE set - five dedicated pulse-only crowns. Every one
		// of these holds a static orientation (the per-stamp `settle` angle)
		// and animates ONLY its lengths/alpha/dust, so none of them spins.
		// None of them reads as a shockwave: the crowns draw no ring at all,
		// AND the hit path skips the shared expanding ring + growing bloom for
		// these styles. Their stamps are dust motes, plus - only on 37 and 41 -
		// a couple of tiny gravity-free fluid blobs.
		//   37 = "heartbeat": bold 4-point star, double-beat thump, droplet puff
		//   38 = "breath": 6-point star inhaling/exhaling with lifting motes
		//   39 = "strobe": 8-point star snapping between two sizes, blinking studs
		//   40 = "drift": 5-point star firing staggered outward dust/droplet waves
		//   41 = "bloom": 8-point petal star breathing in counter-phase, rising pollen
		Variant37,
		Variant38,
		Variant39,
		Variant40,
		Variant41
	}

	// L1: where the effect shape bank gets its art from. Generated = the
	// shipped PNG assets under Assets/Shapes (the primary path). Procedural =
	// bake the same shapes at runtime; also the automatic fallback whenever a
	// PNG asset cannot be loaded.
	public enum ShapeArtSource
	{
		Generated,
		Procedural,
	}

	public class YoyoVisualOptions
	{
		[DefaultValue(true)]
		public bool Effects = true;

		[DefaultValue(true)]
		public bool UseShaders = true;

		// EXPERIMENTAL trail renderer (Terraria.Graphics.VertexStrip + the
		// YoyoStrip shader). It pinches to zero width wherever the path reverses
		// direction, and a yoyo's trail reverses constantly, so it rendered as
		// disconnected fragments unless the yoyo was dashing. All body trails
		// now use the Catmull-Rom ribbon (the same renderer the dev yoyo always
		// used) and this strip path is opt-in only. Read live.
		[DefaultValue(false)]
		public bool VertexStripTrailsExperimental = false;

		// F6: family trail renderer. ON = the soft streak ribbon (feathered
		// edges, smooth taper, no banding, soft additive hot core). OFF = the
		// old hard-edged rectangle ribbon. The dev yoyo always keeps the old
		// ribbon, so this only affects the 13 mini-boss families and their
		// ordnance. Read live.
		[DefaultValue(true)]
		public bool SoftTrails = true;

		// Revert toggles for the approved mini-boss signature extras (each is
		// read live on every spawn/draw, so unticking one removes that effect
		// immediately — no reload needed — and re-ticking brings it back).
		[DefaultValue(true)]
		public bool DutchmanSmokeRings = true;

		[DefaultValue(true)]
		public bool DarkMageRuneRing = true;

		[DefaultValue(true)]
		public bool OgreCrackDecal = true;

		// B1 impact taxonomy: SHEAR / CRUSH / PIERCE / DETONATE / CHANNEL accents
		// layered over each family's own hit FX (geometry-aware). Read live.
		[DefaultValue(true)]
		public bool ImpactClasses = true;

		// B2 fade shapes: four death reads (dissolve / shatter / collapse / pop)
		// chosen from how the projectile ended. Read live.
		[DefaultValue(true)]
		public bool FadeShapes = true;

		// G: victim marks. The last 3 families to hit an enemy leave a small
		// signature glyph on its body (4s), plus a 4-tick hit-direction
		// chevron; the killing family picks the death flourish. Draw-only:
		// per-client, no sync, no hitbox/damage change. Read live.
		[DefaultValue(true)]
		public bool VictimMarks = true;

		// E1: volley tension tells. Families announce a scheduled release and
		// the rope tightens + lifts for the last ~10 ticks before it fires.
		// Draw-time only; never delays a shot. Read live.
		[DefaultValue(true)]
		public bool TensionTells = true;

		// B4 styled misses: an aimed attack that finds no target kicks a dim
		// spark + slack coil beat instead of silently doing nothing. Read live.
		[DefaultValue(true)]
		public bool StyledMisses = true;

		// J1 reclaim: scheduled-death sub-projectiles bend home to the family's
		// deployed yoyo in their final ~12 ticks, so expiry reads as a recall
		// instead of a drop. Contact (instant-hit) attacks are excluded.
		[DefaultValue(true)]
		public bool Reclaim = true;

		// K3 ownership tether: a faint 0.2s line links a fresh sub-projectile
		// to the yoyo that owns it (swarm fights stay readable). Read live.
		[DefaultValue(true)]
		public bool OwnershipTether = true;

		// H1/H2/H6 family voices: spawn/death/hit sounds per family, pitch
		// ladders on repeat hits, all scaled down by ParticleAmount. Read live.
		[DefaultValue(true)]
		public bool FamilyVoices = true;

		// K1 forecast lanes: a faint dark lane previews the next aimed volley
		// (Martian lasers / Betsy barrage / Dutchman broadside). Read live.
		[DefaultValue(true)]
		public bool ForecastLanes = true;

		// C1 aura budget: the low-HP fury ring is a SIGNAL, so it now runs only
		// on the three families that own rage (Ogre / Betsy / DarkMage). This
		// toggle silences even those (read live in MiniYoyoFx.DrawFuryAura).
		[DefaultValue(true)]
		public bool SignatureAuras = true;

		// C2 contrast anchor: dark under-layer behind every main so additive
		// glows keep their value separation on bright backgrounds. Read live.
		[DefaultValue(true)]
		public bool ContrastAnchors = true;

		// D1/D2/D4 body language: impact squash, launch stretch, idle breath
		// on the 13 mains (draw-side scale only - hitboxes untouched).
		[DefaultValue(true)]
		public bool BodyLanguage = true;

		// D6 spin blur: two lagging rotation echoes on fast-spinning mains.
		[DefaultValue(true)]
		public bool SpinBlur = true;

		// J2/J3 charge bank: hits light up to three orbit rings; a full bank
		// spends inward on the next hit. Read live.
		[DefaultValue(true)]
		public bool ChargeBank = true;

		// Ground shadow pass: the 13 mini-boss mains drop a soft squashed
		// ellipse onto the terrain below them, sized/alpha'd by altitude, so
		// bodies stop floating unsupported. Read live every frame.
		[DefaultValue(true)]
		public bool GroundShadows = true;

		// Revert toggle for the DarkMage ritual's magic bolt hit crown. Off keeps
		// the vanilla Excalibur sparkle; Variants 1-5 stamp one of the smooth
		// soft-flare stars, 6-36 stamp one of the wildcard impact signatures
		// (slash / slam / lance / vortex / shatter / bloom / arc / sigil / ray /
		// halo / crescent / crosscut / ripple / quills / comet / eruption /
		// tempest / bramble / nebula / tear / salvo / aurora / web / fangs / eye /
		// sawblade / magnet / soundwave / confetti / glitch / portal), and 37-41
		// are the pulse-only set (heartbeat / breath / strobe / ripple /
		// bloom) - every one of those holds still and pulses, never spins.
		// This dropdown is the testing picker: new variants land here first and
		// are only reachable through it until an attack site adopts them.
		// Drawn by the bolt itself, read live in DarkMageYoyoMagicProj.
		[DefaultValue(ArcaneStarStyle.Variant1)]
		public ArcaneStarStyle DarkMageStar = ArcaneStarStyle.Variant1;

		[DefaultValue(true)]
		public bool Metaballs = true;

		// How long each spawned fluid droplet lives, as a multiplier on the life
		// its spawn site asks for (26-40 ticks). Read live in
		// MetaballSystem.SpawnMetaball, so changing it also affects fluid that is
		// already in flight. 1 = as designed.
		[Range(0.25f, 3f)]
		[DefaultValue(1f)]
		public float MetaballLifetime = 1f;

		// How much fluid the spawn sites actually emit, as a spawn-rate
		// multiplier. 1 = every site spawns exactly as designed; 0.1 = one
		// blob per ten calls; 5 = up to five blobs per call. Fractional
		// budgets accumulate across calls (and across families), so any
		// value in range scales the global fluid density without changing
		// lifetimes. The 256-particle pool recycles its oldest blob first,
		// so even 5x cannot freeze the cluster. Read live.
		[Range(0.1f, 5f)]
		[DefaultValue(1f)]
		public float MetaballAmount = 1f;

		[DefaultValue(false)]
		public bool PixelateGlow = false;

		[DefaultValue(false)]
		public bool Photosensitivity = false;

		[DefaultValue(YoyoVisualQuality.Full)]
		public YoyoVisualQuality Quality = YoyoVisualQuality.Full;

		[Range(0.25f, 2f)]
		[DefaultValue(1f)]
		public float ParticleAmount = 1f;

		[DefaultValue(false)]
		public bool TorchTribute = false;

		// E1/E2/E3/E4/E5 string canvas layers (material pips, whip, strain,
		// pluck, tension tell). Untick for the plain rope back. Read live.
		[DefaultValue(true)]
		public bool StringMaterial = true;

		// L2/B7: speed smears - one baked crescent dragged behind a fast main
		// (the 6 minimal-overhaul beats included). Uses the shape bank. Live.
		[DefaultValue(true)]
		public bool SwingSmears = true;

		// D3: verlet appendages - Pumpking cloth, Dutchman pennant, TorchGod
		// burning wick, Dreadnautilus tendrils. Drawn behind the body. Live.
		[DefaultValue(true)]
		public bool BodyAppendages = true;

		// L1: shape art source for the baked-shape effect bank. Generated uses
		// the shipped PNGs in Assets/Shapes; Procedural bakes at runtime (also
		// the automatic fallback if an asset is unavailable). Read live.
		[DefaultValue(ShapeArtSource.Generated)]
		public ShapeArtSource ShapeArt = ShapeArtSource.Generated;

		// F5: light occlusion for shape-bank draws - unlit pockets dim the
		// shapes instead of letting them glow through the dark. Read live.
		[DefaultValue(true)]
		public bool LightOcclusion = true;
	}

	public class YoyoFeedbackOptions
	{
		[DefaultValue(true)]
		public bool Screenshake = true;

		[DefaultValue(true)]
		public bool ImpactStagger = true;

		[DefaultValue(false)]
		public bool ParrotLies = false;
	}

	// Absorbed from Traps.TrapVisualConfig (same defaults, client-side).
	public class YoyoTrapVisualOptions
	{
		[DefaultValue(true)]
		public bool StressRedWarning = true;

		[DefaultValue(true)]
		public bool RopeShimmer = true;

		[DefaultValue(true)]
		public bool SnapHalves = true;

		[DefaultValue(true)]
		public bool StressGlow = true;

		[DefaultValue(true)]
		public bool AmbientDust = true;
	}

	public class YoyoConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		[Header("Storage")]
		[DefaultValue(StorageUIStyle.Standard)]
		public StorageUIStyle StorageUI;

		[Header("StringSplicer")]
		// Boss fights are the one place where the wheel should open on the
		// combat pins without an extra tab press. Off keeps the remembered
		// board untouched.
		[DefaultValue(true)]
		public bool SplicerAutoCombatBoard = true;

		[Header("TonicSpool")]
		// Compact right-edge tonic strip. Off hides it entirely; the spool
		// itself still works from its inventory right-click mode.
		[DefaultValue(true)]
		public bool TonicHud = true;

		[Header("Guidebook")]
		// The catalogue skin was retired. Better Book keeps the book skin but
		// adds the reading aids (chapter index, history, search snippets,
		// wheel paging, progress bar and a dimmed backdrop).
		[DefaultValue(true)]
		public bool BetterBook = true;

		[Range(0.25f, 2f)]
		[DefaultValue(1f)]
		public float WildStringSpeed;

		[Header("Paintings")]
		[Range(1, 3)]
		[DefaultValue(3)]
		public int NightPaintingVariants = 3;

		[Range(1, 3)]
		[DefaultValue(3)]
		public int DayPaintingVariants = 3;

		[Header("Visuals")]
		public YoyoVisualOptions Visuals = new YoyoVisualOptions();

		[Header("LunaticYoyo")]
		// OFF (default) keeps the Lunatic yoyo's lightning exactly as it has
		// always looked: two hard MagicPixel rectangles per recorded hop. ON
		// swaps in the soft Magical Tech discharge art. This is opt-in on
		// purpose - the original rectangles are the canonical look.
		[DefaultValue(false)]
		public bool LunaticSoftLightningBeam = false;

		[Header("Feedback")]
		public YoyoFeedbackOptions Feedback = new YoyoFeedbackOptions();

		[Header("TrapVisuals")]
		public YoyoTrapVisualOptions TrapVisuals = new YoyoTrapVisualOptions();

		[Header("Music")]
		[DefaultValue(AfterMusicMode.Always)]
		public AfterMusicMode PlayDefeatAfterMusic = AfterMusicMode.Always;
	}
}
