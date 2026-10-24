using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Tech;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Orbital Convergence controller: the living core of the Magical Tech
	/// ability. While the owner holds right-click, every orbiting yoyo pours
	/// energy into this point; the pool grows with the SURVIVING orbit count,
	/// then releases a sustained beam toward the cursor (max 5s).
	///
	/// - Spawned/killed by the owning player; release and input loss cancel it.
	/// - Loses power in real time as orbit yoyos expire (no popping: eased).
	/// - Runs with zero orbiters too: the held yoyo is then the ability source,
	///   so right-click never silently does nothing after the guards expire.
	/// - Dies immediately when it has neither an orbit nor a held yoyo to cast.
	/// - Every side runs the same local clock; the kill packet is authoritative.
	/// </summary>
	public class MagicalConvergenceProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		// ai[0]: state (0 = charging, 1 = releasing)
		// ai[1]: source projectile type (ability identity)

		public const float StateCharging = 0f;
		public const float StateFiring = 1f;

		private int ageTicks;
		private int firingStartTick;
		private int manaClock;
		private float tier01;
		private float abilityTier01 = 0.5f;
		private bool chargeStarted;
		private bool firedBeam;
		private bool beamSeen;
		private float displayPower;  // eased capability from surviving orbits
		private float charge01;      // charge progress (also read by PreDraw)
		private int srcType;
		private int lastOrbitCount;
		private int lastCap = 3;
		private Color lastAccent = new(150, 180, 255);
		private readonly List<Vector2> orbitCenters = new();

		public override void SetDefaults()
		{
			Projectile.width = 8;
			Projectile.height = 8;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.netImportant = true;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.timeLeft = 60 * 10; // failsafe only; real ends are condition-driven
		}

		private static Player FindOwner(Projectile p)
		{
			if (p.owner < 0 || p.owner >= Main.maxPlayers)
				return null;
			Player owner = Main.player[p.owner];
			return owner != null && owner.active && !owner.dead ? owner : null;
		}

		private Projectile FindBeam()
		{
			int beamType = ModContent.ProjectileType<MagicalBeamProj>();
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == Projectile.owner && p.type == beamType && (int)p.ai[0] == Projectile.identity)
					return p;
			}
			return null;
		}

		public override void AI()
		{
			Player owner = FindOwner(Projectile);
			if (owner == null) {
				Projectile.Kill();
				return;
			}
			// Ability only exists while the Magical tech is attuned and carried.
			if (!YoyoTechCommon.TechActiveFor(owner, out YoyoTechType tech) || tech != YoyoTechType.Magical) {
				Projectile.Kill();
				return;
			}
			// Opt-in config: turning it off mid-channel collapses the field.
			if (!MagicalOrbit.AbilitiesEnabled) {
				Projectile.Kill();
				return;
			}
			// The channel is anchored to a yoyo in hand on every side. This
			// closes the other half of the "cast with orbiters out" exploit:
			// keeping a ring alive while switching to a non-yoyo cannot keep
			// the ability running either.
			if (MagicalOrbit.HeldYoyoSourceType(owner) <= 0) {
				Projectile.Kill();
				return;
			}
			// A controller may run with ZERO orbiters: the yoyo actually in hand
			// is then the source of the ability. Only a controller with neither
			// an orbit nor a held-yoyo identity has nothing to cast and may die.
			if ((int)Projectile.ai[1] <= 0)
				Projectile.ai[1] = MagicalOrbit.HeldYoyoSourceType(owner);
			int orbitCount = MagicalOrbit.CountOrbitYoyos(owner);
			if (orbitCount <= 0 && (int)Projectile.ai[1] <= 0) {
				Projectile.Kill();
				return;
			}
			// Owner let go (or lost the mouse context): stop cleanly.
			if (Projectile.owner == Main.myPlayer && !MagicalOrbit.RightHeldByLocal(owner)) {
				Projectile.Kill();
				return;
			}

			ageTicks++;
			Projectile.Center = owner.Center;
			Projectile.velocity = Vector2.Zero;
			Projectile.timeLeft = 60 * 10;

			MagicalOrbit.CapStats(owner, out _, out int cap);
			float powerTarget = orbitCount > 0
				? MathHelper.Clamp(orbitCount / (float)Math.Max(1, cap), 0f, 1f)
				: 0.35f; // hand-cast: one yoyo's worth of presence, no orbit ring
			displayPower = ageTicks <= 2 ? powerTarget : MathHelper.Lerp(displayPower, powerTarget, 0.08f);

			srcType = MagicalOrbit.PrimaryOrbitSourceType(owner, srcType > 0 ? srcType : (int)Projectile.ai[1]);
			if ((int)Projectile.ai[1] != srcType)
				Projectile.ai[1] = srcType;
			lastOrbitCount = orbitCount;
			lastCap = cap;

			MagicalAbilityProfile profile = MagicalAbilityRegistry.Get(srcType);
			lastAccent = profile.AccentColor(srcType);
			tier01 = MagicalOrbit.ManaTier01(MagicalOrbit.AverageOrbitDamage(owner));
			// Progression tier of the yoyo itself: every release (damage, sizes,
			// counts, impact weight) scales with it.
			abilityTier01 = MagicalTier.AbilityTier01(srcType);

			// Sustained mana upkeep, paid by the owning client only. Running dry
			// collapses the ability (it can re-form later if mana returns).
			if (Projectile.owner == Main.myPlayer) {
				// While the ability lives the pool never passively refills: only
				// real potions (Mana Flower) restore it, so the spend is honest.
				owner.manaRegenDelay = Math.Max(owner.manaRegenDelay, 60);
				manaClock++;
				if (manaClock >= 60) {
					manaClock = 0;
					// Upkeep climbs the longer the channel is held (10%/s, max 2.5x).
					float upkeep = Math.Max(1f, profile.ManaUpkeepPerSecond(MagicalOrbit.ManaMaxOf(owner), tier01)
						* MagicalOrbit.UpkeepRamp(ageTicks));
					// Worn Mana Flower drinks before the upkeep starves the ability.
					MagicalOrbit.ManaFlowerAssist(owner, upkeep);
					if (owner.statMana >= upkeep) {
						owner.statMana -= (int)upkeep;
						owner.manaRegenDelay = Math.Max(owner.manaRegenDelay, 75);
					}
					else {
						MagicalOrbit.CueManaStarvation(owner);
						owner.GetModPlayer<YoyoTechPlayer>().MagicalRestartCd = 30;
						Projectile.Kill();
						return;
					}
				}
			}

			if (Projectile.ai[0] == StateCharging) {
				charge01 = MathHelper.Clamp(ageTicks / (float)Math.Max(1, profile.ChargeTicks), 0f, 1f);
				MagicalAbilityContext ctx = BuildContext(owner, orbitCount, cap, charge01);
				if (!chargeStarted) {
					chargeStarted = true;
					profile.OnChargeStart(ctx);
				}
				profile.OnChargeTick(ctx);
				if (charge01 >= 1f) {
					Projectile.ai[0] = StateFiring;
					firingStartTick = ageTicks;
					Projectile.netUpdate = true;
					profile.OnChargeComplete(BuildContext(owner, orbitCount, cap, 1f));
				}
			}
			else {
				MagicalAbilityContext ctx = BuildContext(owner, orbitCount, cap, 1f);
				profile.OnFiringTick(ctx);
				if (profile.SpawnsBeam) {
					if (Projectile.owner == Main.myPlayer && !firedBeam)
						SpawnBeam(owner, profile, ctx);
					// The sustained release ends with its beam (5s hard cap inside
					// the beam itself). Follow it down on every side.
					if (FindBeam() != null)
						beamSeen = true;
					else if (beamSeen) {
						Projectile.Kill();
						return;
					}
				}
				else if (profile.FiringDurationTicks >= 0
					&& ageTicks - firingStartTick > profile.FiringDurationTicks) {
					// Self-driven release (no beam): it owns its own window.
					Projectile.Kill();
					return;
				}
				if (ageTicks > MagicalOrbit.BeamMaxLifeTicks + 300 + profile.ChargeTicks) {
					Projectile.Kill();
					return;
				}
			}

			Lighting.AddLight(Projectile.Center,
				lastAccent.ToVector3() * (0.35f + 0.55f * charge01) * (0.55f + 0.45f * displayPower));
		}

		private MagicalAbilityContext BuildContext(Player owner, int orbitCount, int cap, float charge)
		{
			Vector2 aim = MagicalOrbit.AimDirection(owner, Projectile.Center, Projectile.velocity.SafeNormalize(new Vector2(owner.direction == 0 ? 1f : owner.direction, 0f)));
			return new MagicalAbilityContext(owner, Projectile, Projectile.Center, aim,
				orbitCount, cap, srcType, charge, displayPower, abilityTier01, lastAccent);
		}

		/// <summary>Owner-side: fire the sustained release. Damage is a reduced
		/// slice of the orbit shells' damage, scaled by live orbit count.</summary>
		private void SpawnBeam(Player owner, MagicalAbilityProfile profile, in MagicalAbilityContext ctx)
		{
			int beamType = ModContent.ProjectileType<MagicalBeamProj>();
			Vector2 fallback = owner.direction == 0 ? Vector2.UnitX : new Vector2(owner.direction, 0f);
			Vector2 aim = MagicalOrbit.AimDirection(owner, Projectile.Center, fallback);
			float damageFactor = MathHelper.Lerp(0.6f, 1f, ctx.Power01);
			int damage = Math.Max(1, (int)(MagicalOrbit.AverageOrbitDamage(owner) * profile.BeamDamageFraction * damageFactor * ctx.TierScale));
			int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, aim,
				beamType, damage, 0f, Projectile.owner, ai0: Projectile.identity);
			if (idx < 0 || idx >= Main.maxProjectiles)
				return;
			Projectile beam = Main.projectile[idx];
			beam.netUpdate = true;
			firedBeam = true;
			beamSeen = true;
			profile.OnBeamSpawned(ctx, beam);
		}

		public override void OnKill(int timeLeft)
		{
			// Cascade: the release dies with its core. This runs on every side
			// after the synced kill, so no orphaned beams, hitboxes or FX remain.
			Projectile beam = FindBeam();
			if (beam != null)
				beam.Kill();

			Player owner = FindOwner(Projectile);
			if (owner != null) {
				int source = srcType > 0 ? srcType : (int)Projectile.ai[1];
				MagicalAbilityProfile profile = MagicalAbilityRegistry.Get(source);
				MagicalAbilityContext ctx = new(owner, Projectile, Projectile.Center, Vector2.Zero,
					lastOrbitCount, lastCap, source, charge01, displayPower, abilityTier01, lastAccent);
				profile.OnAbilityEnd(ctx, Projectile.ai[0] == StateCharging);

				// Per-ability lockout. This runs wherever the synced kill is
				// processed, but the timer matters only on the owning client
				// (the only side allowed to start a channel). It applies after
				// a complete release, a manual cancel and a mana collapse alike.
				if (Projectile.owner == Main.myPlayer) {
					int cooldown = Math.Max(MagicalOrbit.AbilityCooldownMinimum, profile.CooldownTicks);
					YoyoTechPlayer techPlayer = owner.GetModPlayer<YoyoTechPlayer>();
					techPlayer.MagicalAbilityCooldown =
						Math.Max(techPlayer.MagicalAbilityCooldown, cooldown);
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Player owner = FindOwner(Projectile);
			if (owner == null)
				return false;
			MagicalAbilityProfile profile = MagicalAbilityRegistry.Get(srcType);
			Vector2 fallback = Projectile.velocity.SafeNormalize(new Vector2(owner.direction == 0 ? 1f : owner.direction, 0f));
			Vector2 aim = MagicalOrbit.AimDirection(owner, Projectile.Center, fallback);
			MagicalAbilityContext ctx = new(owner, Projectile, Projectile.Center, aim,
				lastOrbitCount, lastCap, srcType, charge01, displayPower, abilityTier01, lastAccent);

			profile.DrawChargeField(ctx);
			orbitCenters.Clear();
			MagicalOrbit.CollectOrbitCenters(owner, orbitCenters);
			profile.DrawConnections(ctx, orbitCenters);
			return false;
		}
	}
}
