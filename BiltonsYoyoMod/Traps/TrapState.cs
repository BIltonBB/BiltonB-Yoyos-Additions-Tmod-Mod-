using System;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Traps
{
	public enum TrapKind : byte
	{
		WildSnare = 0,
		Bearing = 1,
		Sentry = 2,
		Link = 3,
		Tripwire = 4
	}

	// Per-tile persistent state. All fields are clamped on load/net receive.
	// Energy / Uses / Overcharge implement the creative systems:
	// - Energy: activations cost power; regen is faster inside a Research mesh.
	// - Uses (Field Experience): counts successful activations; gates upgrades.
	// - Overcharge (Risk/Reward): 2x energy for +50% damage with malfunction risk.
	// - Synergy (Experiment): different trap kinds near each other + meshed share bonus.
	public class TrapState
	{
		public TrapKind Kind;
		public int Level; // 0..MaxLevel (2 offensive, 4 defensive)
		public int Uses; // lifetime successful activations
		public int Energy = 100; // 0..MaxEnergy
		public bool Overcharge;
		public int Cooldown; // ticks remaining
		public int ActiveTimer; // ticks of current active deployment
		public bool WiredActive; // String Link wiring state (pair-wide)

		// Generic tuning (per-kind defaults applied on creation)
		public int Range; // detection radius in tiles
		public int Damage;
		public int Duration; // active duration ticks (snare/barrier)
		public int CooldownMax; // reload ticks

		// String / bearing / counterweight selection (indices into real mod content)
		public int StringVariant; // 0 AC, 1 APC, 2 AP, 3 AN, 4 AK
		public int BearingTier; // 0 wood .. 2 top (maps to LooseBearing damage)
		public int CounterType; // 0..10 (10 Iron default, 0 Golden Lv1)
		public int Priority; // 0 nearest, 1 strongest
		public int Behavior; // sentry: 0 outward,1 return,2 both | link: 0 dmg,1 restraint,2 both
		public int Restraint; // seconds of Slow applied by link barrier
		public int Bend; // 0 tight, 1 slack, 2 loose (rope rest-length slack)

		// Tripwire movement
		public int MaxLen; // pixels of string
		public int OutSpeed; // pixels per tick *10 (stored int for save safety)
		public int ReturnSpeed;
		public int Burst; // bearing shots per volley (1..3 by level)

		// Tripwire entity-category toggles (six independent + All master override)
		public bool HitPlayers;
		public bool HitNPCs; // ordinary non-town, non-hostile, non-friendly
		public bool HitTown;
		public bool HitHostile = true;
		public bool HitFriendly;
		public bool HitAll;

		// String Link pairing (origin of partner, -9999 = unlinked)
		public int LinkX = -9999;
		public int LinkY = -9999;

		// Unique options (all mesh-gated, see TrapActions):
		public bool SentryAegis; // sentry: Endurance aura while powered
		public bool WardLattice; // link: barrier zaps minor hostile shots
		public bool SnareTip; // tripwire: yoyo hits also Slow

		// Stance trade-offs (free toggles, no mesh needed):
		public bool SentryBunker; // sentry: +50% dmg, -35% range
		public bool LongSpan; // link: +50% pair range, -25% damage
		public bool HairTrigger; // tripwire: half cooldown, -25% damage
		public bool BarbedWire; // link: touches also Poison
		public bool TracerFire; // sentry: hits apply Ichor
		public bool TwinThrow; // tripwire: two opposite yoyos

		// Link stress/fray model: holding victims strains the rope.
		// Stress builds 0..MaxStress (varies per string, see MaxStressFor);
		// at max the rope snaps the barrier off and frays it. Frayed counts
		// down in ticks; RecoveryPct shows 0..100% healed.
		public int Stress; // builds while holding, snaps at MaxStress
		public int Frayed; // lockout ticks after a snap
		// Link weave tracks (own upgrade tab): regen + tension.
		public int RegenWeave; // 0..3 faster stress/fray recovery
		public int TensionWeave; // 0..3 more max stress capacity
		// Dedicated max-stress plating (Reinforced Rope, Barrier tab):
		// no mesh or uses gate, just string + coins.
		public int LinkStress; // 0..3 Reinforced Rope levels
		public const int MaxLinkStress = 3;
		public const int LinkStressPerLevel = 30;
		public bool AutoRewire; // aegis-gated auto re-engage after recovery
		// Unique upgrade tracks (0..3, Aegis-gated, see TrapActions.ApplyTrack):
		public int LinkCoil; // link: Grounding Coil, Ward zap costs less power
		public int SentryGyro; // sentry: Gyroscope, return-leg damage bonus
		public int TripAxle; // tripwire: Lubricated Axle, longer flight time
		public int SentryScope; // sentry: Spotter Scope, +2 range per level
		public int LinkReserve; // link: Silk Reserve, +15 max energy per level
		public int TripWind; // tripwire: Second Wind, -10% cooldown per level
		public int LinkBarb; // link: Barbed Wrap, poison bites 3s +1s per level
		// Tripwire yoyo head style 0..7 (real sprite + damage feel).
		public int TripStyle;
		// Tripwire STRING type 0..7 (the tether itself): 0 Plain, 1 Barbed
		// (5 touch dmg + Bleeding 5s), 2 Tangled (1 touch dmg + Bleeding 3s),
		// 3..7 material colors (Cotton/Polyester/Silk/Shadow/Solar, harmless).
		public int TripString;
		// Sentry tether color 0..7 (purely visual line color).
		public int SentryString;
		// Link rope coating 0..2 (None/Barbed/Tangled): accessory-number touch
		// damage on an active biting barrier (Damage/Both behaviors).
		public int LinkTreat;
		// Link schedule 0..3 (pair-wide): 0 Manual wiring, 1 Day only,
		// 2 Night only, 3 Pulse (auto-toggle on a period). Overrides wiring.
		public int Sched;
		// Pulse period in seconds (10/30/60) for schedule mode 3.
		public int PulsePeriod = 30;
		// Experiment head unlocks, bitmask (server-owned, like Uses/Kills).
		// Bits 0-4: Venom/Gilded/Warden/Orbit/Lunar weights; 5-7: Dusk/
		// Glacier/Blood Tithe yoyo heads.
		public int ExpHeads;
		// Pending auto-upgrade handshake (offensive): server flags readiness,
		// a nearby client pays from its own (authoritative) inventory.
		public bool UpgradePending;

		// Maintenance: machines wear ~1% per ingame day; service restores it.
		public int Condition = 100; // 0..100% efficiency
		public int CondTick; // wear accumulator (86400 = one full day)

		// Master switch: disabled traps never fire, drain, or strain.
		public bool Enabled = true;
		// Alarm scope: false = ping on every crossing, true = idle barriers only.
		public bool AlarmIdleOnly;

		// Custom nickname (preset list, shown in titles + monitor).
		public string Name = "";
		// Auto-overcharge: while Aegis-FUELED, fire overcharged at 2% risk.
		public bool AutoOC;
		// Mesh power sharing: 0 Off, 1 Donor, 2 Needy (Aegis mesh pool).
		public int ShareMode;
		// Lifetime kills (defensive blocks): milestones grant veteran bonus.
		public int Kills;

		// Sentry head aim (radians, transient visual; synced for MP). Base never rotates.
		public float AimAngle = 0f;
		// Sentry anchor method 0..2 (head pivot): 0 Mount (base top),
		// 1 TileCenter, 2 Calibrated (mount + sprite-center pivot).
		public int SentryAnchor;

		public bool Linked => LinkX != -9999;

		public Point LinkPoint => new Point(LinkX, LinkY);

		// ---- IRL string restraint table ----
		// Finger-push model: pushing a string sideways tensions it
		// non-linearly. Weak strings (Cotton) stretch deep and snap fast;
		// Kevlar barely yields and holds long. Bend = slack setting
		// (0 tight,1 slight,2 curved); MaxBend caps which settings each
		// string can safely hold. Multiple victims amplify strain.
		public static int MaxStressFor(int variant, int tensionWeave)
		{
			int baseMax = variant switch { 0 => 105, 1 => 128, 2 => 150, 3 => 180, 4 => 225, 5 => 90, 6 => 200, 7 => 165, _ => 100 };
			return baseMax + System.Math.Clamp(tensionWeave, 0, 3) * 38;
		}

		public int MaxStress() => MaxStressFor(StringVariant, TensionWeave) + System.Math.Clamp(LinkStress, 0, MaxLinkStress) * LinkStressPerLevel;

		public static int MaxBendFor(int variant) => variant switch {
			0 => 2, // Cotton: saggy but snappy
			1 => 2, // Poly-Cotton: all-round
			2 => 2, // Polyester: holds shape
			3 => 1, // Nylon: stiff, dislikes deep curves
			4 => 2, // Kevlar: anything
			5 => 2, // Silk: drapes deep
			6 => 1, // Shadow: stiff as slate
			_ => 2, // Solar: supple
		};

		// Base strain per tick-holding-victim (before multi-victim scaling).
		public static int StrainRateFor(int variant) => variant switch {
			0 => 8, 1 => 7, 2 => 6, 3 => 5, 4 => 4, 5 => 9, 6 => 3, 7 => 5, _ => 6,
		};

		// Regen lockout after a snap, in ticks. Varies per string; weave
		// speeds it (15% faster per level). Displayed as Recovery %.
		public static int FrayTicksFor(int variant, int regenWeave)
		{
			int baseTicks = variant switch { 0 => 1500, 1 => 1320, 2 => 1200, 3 => 1080, 4 => 900, 5 => 1600, 6 => 700, 7 => 950, _ => 1200 };
			float mult = 1f - 0.15f * System.Math.Clamp(regenWeave, 0, 3);
			return System.Math.Max(300, (int)(baseTicks * mult));
		}

		public int FrayMax() => FrayTicksFor(StringVariant, RegenWeave);

		public int RecoveryPct()
		{
			int max = FrayMax();
			if (Frayed <= 0)
				return 100;
			if (max <= 0)
				return 0;
			return System.Math.Clamp(100 - Frayed * 100 / max, 0, 100);
		}

		public int StressPct()
		{
			int max = MaxStress();
			if (max <= 0)
				return 0;
			return System.Math.Clamp(Stress * 100 / max, 0, 100);
		}

		public static TrapState DefaultFor(TrapKind kind)
		{
			var s = new TrapState { Kind = kind };
			switch (kind) {
				case TrapKind.WildSnare:
					s.Range = 8; s.Damage = 20; s.Duration = 300; s.CooldownMax = 360;
					s.StringVariant = 0;
					break;
				case TrapKind.Bearing:
					s.Range = 12; s.Damage = 15; s.CooldownMax = 90; s.Burst = 1;
					s.BearingTier = 0;
					break;
				case TrapKind.Sentry:
					s.Range = 10; s.Damage = 15; s.CooldownMax = 120;
					s.CounterType = 10; s.Priority = 0; s.Behavior = 2;
					s.SentryAnchor = 2; // Calibrated centered pivot (rotates in place)
					break;
				case TrapKind.Link:
					s.Range = 15; s.Damage = 20; s.Duration = 600; s.CooldownMax = 60;
					s.StringVariant = 0; s.Restraint = 3; s.Behavior = 2; s.Bend = 0;
					s.HitPlayers = true; s.HitNPCs = true; s.HitHostile = true;
					break;
				case TrapKind.Tripwire:
					s.Range = 6; s.Damage = 25; s.CooldownMax = 240;
					s.MaxLen = 170; s.OutSpeed = 28; s.ReturnSpeed = 55;
					s.Priority = 0;
					s.HitPlayers = false; s.HitNPCs = false; s.HitTown = false;
					s.HitHostile = true; s.HitFriendly = false; s.HitAll = false;
					break;
			}
			return s;
		}

		public int MaxEnergy => 100 + Level * 50 + (Kind == TrapKind.Link ? System.Math.Clamp(LinkReserve, 0, 3) * 15 : 0);

		public int EnergyCost => Overcharge ? 20 : 10;

		// Link gets 8 tiers (one per string type); sentry/tripwire cap at 4.
		// Offensive traps cap at 2.
		public int MaxLevel => (Kind == TrapKind.WildSnare || Kind == TrapKind.Bearing) ? 2 : (Kind == TrapKind.Link ? 7 : 4);

		// Link string tiers unlock in order: higher trap level admits higher
		// string types (intersected with world-progression pool).
		public int MaxLinkString()
		{
			int prog = TrapSystem.MaxLinkProg();
			// One level, one new string type: variant index == level.
			int gate = Level;
			return Math.Min(prog, gate);
		}
	}
}
