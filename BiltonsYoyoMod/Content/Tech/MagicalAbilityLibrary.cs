using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles.Tech;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// Base for every Magical Tech release that is NOT a sustained beam.
	/// It inherits the whole Orbital Convergence charge look (so all of the
	/// library abilities share the same polish), then spends its release window
	/// spawning one or more executor projectiles from MagicalReleases.cs.
	/// Subclasses only describe WHAT the release is; the controller, mana
	/// economy, orbit plumbing and cleanup stay shared with the beam path.
	///
	/// Design rule: every release HURTS. Supports (aegis, totem, walls, chrono,
	/// sanctum) trade raw numbers for zone control or protection, but their
	/// projectiles still deal contact/tick damage - nothing in this library is
	/// allowed to be a no-damage decoration.
	/// </summary>
	public abstract class MagicalReleaseProfile : OrbitalConvergenceProfile
	{
		public override bool SpawnsBeam => false;

		/// <summary>How long the release window stays open after the charge peaks.</summary>
		protected virtual int ReleaseWindowTicks => 20;
		public override int FiringDurationTicks => ReleaseWindowTicks;

		/// <summary>Damage of one release hit as a slice of the orbit shells' damage.</summary>
		protected virtual float ReleaseDamageFactor => 1f;

		protected int Damage(in MagicalAbilityContext ctx, float factor)
		{
			float power = MathHelper.Lerp(0.7f, 1.15f, ctx.Power01);
			return Math.Max(1, (int)(MagicalOrbit.AverageOrbitDamage(ctx.Owner)
				* ReleaseDamageFactor * factor * power * ctx.TierScale));
		}

		/// <summary>The point the ability is cast at: along the aim, `distance` away.</summary>
		protected static Vector2 AimPoint(in MagicalAbilityContext ctx, float distance)
		{
			return ctx.Center + ctx.Aim * distance;
		}

		// ---- Executor spawn helpers (all owner-side, all net-synced) ----

		protected int Burst(in MagicalAbilityContext ctx, float mode, float factor,
			Vector2? at = null, Vector2? aim = null)
		{
			return MagicalReleaseCommon.Spawn(ctx.Owner, at ?? ctx.Center, aim ?? ctx.Aim,
				ModContent.ProjectileType<MagicalBurstProj>(), Damage(ctx, factor),
				mode, ctx.Power01, ctx.SourceProjType);
		}

		protected int Star(in MagicalAbilityContext ctx, float mode, float factor, Vector2 at, Vector2 vel)
		{
			return MagicalReleaseCommon.Spawn(ctx.Owner, at, vel,
				ModContent.ProjectileType<MagicalStarProj>(), Damage(ctx, factor),
				mode, ctx.Power01, ctx.SourceProjType);
		}

		protected int Zone(in MagicalAbilityContext ctx, float mode, float factor, Vector2 at)
		{
			return MagicalReleaseCommon.Spawn(ctx.Owner, at, Vector2.Zero,
				ModContent.ProjectileType<MagicalZoneProj>(), Damage(ctx, factor),
				mode, ctx.Power01, ctx.SourceProjType);
		}

		protected int Deploy(in MagicalAbilityContext ctx, float mode, float factor, Vector2 at, Vector2 vel = default)
		{
			return MagicalReleaseCommon.Spawn(ctx.Owner, at, vel,
				ModContent.ProjectileType<MagicalDeployProj>(), Damage(ctx, factor),
				mode, ctx.Power01, ctx.SourceProjType);
		}

		protected int Tether(in MagicalAbilityContext ctx, float mode, float factor)
		{
			return MagicalReleaseCommon.Spawn(ctx.Owner, ctx.Center, Vector2.Zero,
				ModContent.ProjectileType<MagicalTetherProj>(), Damage(ctx, factor),
				mode, ctx.Power01, ctx.SourceProjType);
		}

		/// <summary>Lightning archetype: a bolt born aimed at the cursor. It
		/// travels, damages everything along its path, and - depending on mode -
		/// throws forks that leap to further enemies (see the mode constants on
		/// <see cref="MagicalLightningProj"/>).</summary>
		protected int Voltaic(in MagicalAbilityContext ctx, float mode, float factor, Vector2 at, Vector2 dir, int generation = 0)
		{
			int idx = MagicalReleaseCommon.Spawn(ctx.Owner, at, dir.SafeNormalize(Vector2.UnitX) * MagicalLightningProj.BoltSpeed,
				ModContent.ProjectileType<MagicalLightningProj>(), Damage(ctx, factor),
				mode, ctx.Power01, ctx.SourceProjType);
			if (idx >= 0 && idx < Main.maxProjectiles)
				Main.projectile[idx].localAI[1] = generation;
			return idx;
		}

		// ---- Movement helpers (owner client only) ----

		/// <summary>Short teleport along the aim (Blink Slash, Chrono Anchor).</summary>
		protected static bool Blink(Player player, Vector2 dir, float distance)
		{
			if (player == null || player.whoAmI != Main.myPlayer || dir.LengthSquared() < 0.001f)
				return false;
			dir = dir.SafeNormalize(Vector2.UnitX);
			Vector2 start = player.Center;
			Vector2 target = start + dir * distance;
			if (!Collision.CanHitLine(start, 1, 1, target, 1, 1))
				target = start + dir * (distance * 0.45f);
			player.position += target - player.Center;
			player.velocity = dir * (player.velocity.Length() * 0.25f + 4f);
			player.immune = true;
			player.immuneTime = Math.Max(player.immuneTime, 16);
			player.fallStart = (int)(player.position.Y / 16f);
			return true;
		}

		/// <summary>Momentum shove along the aim (Thunderclap, Bulwark).</summary>
		protected static void Charge(Player player, Vector2 dir, float speed)
		{
			if (player == null || player.whoAmI != Main.myPlayer || dir.LengthSquared() < 0.001f)
				return;
			player.velocity += dir.SafeNormalize(Vector2.UnitX) * speed;
			player.immune = true;
			player.immuneTime = Math.Max(player.immuneTime, 20);
		}

		// ---- Cursor release helpers (owner-side, net-synced) ----

		/// <summary>The exact cursor point (owner side), clamped to a sane cast
		/// range so a well never spawns inside the caster or half a screen away.</summary>
		protected static Vector2 CursorPoint(in MagicalAbilityContext ctx, float fallbackDistance,
			float minDistance, float maxDistance)
		{
			if (ctx.Owner != null && ctx.Owner.whoAmI == Main.myPlayer) {
				Vector2 off = Main.MouseWorld - ctx.Center;
				float len = MathHelper.Clamp(off.Length(), minDistance, maxDistance);
				return ctx.Center + off.SafeNormalize(ctx.Aim) * len;
			}
			return ctx.Center + ctx.Aim * MathHelper.Clamp(fallbackDistance, minDistance, maxDistance);
		}

		/// <summary>Cast a cursor PUDDLE. It is born exactly on the cursor point
		/// with no velocity and does not move; the arrival beam is drawn from the
		/// caster to the pool (see MagicalPuddleProj).</summary>
		protected int Well(in MagicalAbilityContext ctx, float mode, float factor)
		{
			Vector2 at = CursorPoint(ctx, 320f, 110f, 780f);
			return MagicalReleaseCommon.Spawn(ctx.Owner, at, at - ctx.Center,
				ModContent.ProjectileType<MagicalPuddleProj>(), Damage(ctx, factor),
				mode, ctx.Power01, ctx.SourceProjType);
		}

		/// <summary>Stamp a cursor BRAND (flavour picks the look and particle set).</summary>
		protected int Brand(in MagicalAbilityContext ctx, float flavor, float factor)
		{
			Vector2 at = CursorPoint(ctx, 280f, 0f, 1400f);
			return MagicalReleaseCommon.Spawn(ctx.Owner, at, Vector2.Zero,
				ModContent.ProjectileType<MagicalBrandProj>(), Damage(ctx, factor),
				flavor, ctx.Power01, ctx.SourceProjType);
		}

		/// <summary>One of the cursor-wave executors (snap trap, anchor chain,
		/// pocket singularity). Their ai layout is [0] power01, [1] tier01,
		/// [2] source, so they scale with the yoyo like every other release.</summary>
		protected int Special(in MagicalAbilityContext ctx, int projType, float factor,
			Vector2 at, Vector2 vel = default)
		{
			return MagicalReleaseCommon.Spawn(ctx.Owner, at, vel, projType, Damage(ctx, factor),
				ctx.Power01, ctx.Tier01, ctx.SourceProjType);
		}

		/// <summary>Lob the growing bomb at the cursor; it swells while it homes.</summary>
		protected int GrowOrb(in MagicalAbilityContext ctx, float factor)
		{
			Vector2 at = CursorPoint(ctx, 300f, 60f, 900f);
			return MagicalReleaseCommon.Spawn(ctx.Owner, ctx.Center,
				(at - ctx.Center).SafeNormalize(ctx.Aim) * 7.4f,
				ModContent.ProjectileType<MagicalGrowOrbProj>(), Damage(ctx, factor),
				0f, ctx.Power01, ctx.SourceProjType);
		}

		/// <summary>Two mirrored weavers: sine waves or crossing spirals.</summary>
		protected void WeaverPair(in MagicalAbilityContext ctx, bool spiral, float factor)
		{
			WeaverWave(ctx,
				spiral ? MagicalWeaverProj.ModeSpiralA : MagicalWeaverProj.ModeSineA,
				spiral ? MagicalWeaverProj.ModeSpiralB : MagicalWeaverProj.ModeSineB,
				factor);
		}

		/// <summary>Two mirrored weavers with an explicit shape pair.</summary>
		protected void WeaverWave(in MagicalAbilityContext ctx, float modeA, float modeB, float factor)
		{
			Vector2 at = CursorPoint(ctx, 320f, 80f, 900f);
			Vector2 dir = (at - ctx.Center).SafeNormalize(ctx.Aim);
			int type = ModContent.ProjectileType<MagicalWeaverProj>();
			int damage = Damage(ctx, factor);
			MagicalReleaseCommon.Spawn(ctx.Owner, ctx.Center, dir * 7.2f, type, damage,
				modeA, ctx.Power01, ctx.SourceProjType);
			MagicalReleaseCommon.Spawn(ctx.Owner, ctx.Center, dir * 7.2f, type, damage,
				modeB, ctx.Power01, ctx.SourceProjType);
		}

		/// <summary>The strobing 0.1s beam focus.</summary>
		protected int PulseCascade(in MagicalAbilityContext ctx, float factor)
		{
			return MagicalReleaseCommon.Spawn(ctx.Owner, ctx.Center, Vector2.Zero,
				ModContent.ProjectileType<MagicalPulseCascadeProj>(), Damage(ctx, factor),
				0f, ctx.Power01, ctx.SourceProjType);
		}

		/// <summary>A lifesteal detonation at the cursor.</summary>
		protected int LifedrainNova(in MagicalAbilityContext ctx, float factor)
		{
			Vector2 at = CursorPoint(ctx, 280f, 40f, 900f);
			return MagicalReleaseCommon.Spawn(ctx.Owner, at, Vector2.Zero,
				ModContent.ProjectileType<MagicalLifedrainProj>(), Damage(ctx, factor),
				0f, ctx.Power01, ctx.SourceProjType);
		}

		/// <summary>Spend up to four ring yoyos as ricochet rounds. The real
		/// orbiters keep orbiting and hitting; they only stop drawing, so the
		/// ring visibly thins and clicks back as each feeder returns.</summary>
		protected void FeedOrbiters(in MagicalAbilityContext ctx, float factor)
		{
			Player owner = ctx.Owner;
			if (owner == null || owner.whoAmI != Main.myPlayer)
				return;
			Vector2 at = CursorPoint(ctx, 320f, 120f, 820f);
			int limit = Math.Clamp(ctx.OrbitCount, 2, 4);
			int damage = Damage(ctx, factor);
			int type = ModContent.ProjectileType<MagicalYoyoProj>();
			int spawned = 0;
			for (int i = 0; i < Main.maxProjectiles && spawned < limit; i++) {
				Projectile orb = Main.projectile[i];
				if (!orb.active || orb.owner != owner.whoAmI || orb.type != type)
					continue;
				if (orb.ModProjectile is not MagicalYoyoProj yoyo || yoyo.FeederAway)
					continue;
				yoyo.FeederAway = true;
				orb.netUpdate = true;
				Vector2 dir = (at - orb.Center).SafeNormalize((at - ctx.Center).SafeNormalize(ctx.Aim));
				MagicalReleaseCommon.Spawn(owner, orb.Center, dir,
					ModContent.ProjectileType<MagicalFeederProj>(), damage,
					spawned * 15f, i, yoyo.SourceProjType);
				spawned++;
			}
			if (spawned > 0)
				return;
			// No ring to spend: fire a phantom pair so the release is never a dud.
			for (int i = 0; i < 2; i++) {
				Vector2 dir = (at - ctx.Center).SafeNormalize(ctx.Aim)
					.RotatedBy(i == 0 ? -0.16f : 0.16f);
				MagicalReleaseCommon.Spawn(owner, ctx.Center + dir * 30f, dir,
					ModContent.ProjectileType<MagicalFeederProj>(), damage,
					i * 12f, -1, ctx.SourceProjType);
			}
		}
	}

	// =========================================================================
	//  Core twenty: offence first, with a small support wing that still hits.
	// =========================================================================

	/// <summary>#1 Aegis Bloom - the shell bursts out and leaves a barrier that
	/// orbits you and eats incoming fire. Support; the burst still cuts.
	/// ARCHIVED (cursor-focus rework): the class is kept so a future fire/cursor
	/// rework of Torch God can reuse or learn from it, but it is no longer in the
	/// catalogue.</summary>
	public sealed class AegisBloomAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 200;
		protected override float ReleaseDamageFactor => 0.85f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Burst(ctx, MagicalBurstProj.ModeBloom, 1.1f);
			Deploy(ctx, MagicalDeployProj.ModeShield, 1f, ctx.Center);
		}
	}

	/// <summary>#2 Rift Tear - a gravity slit drags enemies in, grinds them
	/// against its throat, then implodes when it closes.</summary>
	public sealed class RiftTearAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 60;
		protected override float ReleaseDamageFactor => 1.1f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 at = AimPoint(ctx, 170f);
			Zone(ctx, MagicalZoneProj.ModeVoidWell, 1f, at);
			Burst(ctx, MagicalBurstProj.ModeBloom, 0.6f, at);
			if (!Main.dedServ) {
				// The tear opens hungry: inward-falling wisps + a cracked rim so
				// the pull is legible from the frame it appears.
				MagicalReleaseCommon.InfallWisps(at, 200f, ctx.Accent, 12, 1.2f);
				MagicalFx.Fan(ShapeId.RingCracked, at, ctx.Accent, 5, 3.6f, 24, 0.6f);
			}
		}
	}

	/// <summary>#3 Starburst Volley - one seeking star per surviving guard.</summary>
	public sealed class StarburstVolleyAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 26;
		protected override float ReleaseDamageFactor => 0.85f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			int count = Math.Clamp(2 + ctx.OrbitCount, 2, 8);
			float baseAngle = ctx.Aim.ToRotation();
			if (!Main.dedServ) {
				// Volley flare: one bright launch beat + a fan bloom, so the stars
				// read as a volley leaving the core instead of popping into being.
				MagicalFx.CastFlash(ctx.Center, ctx.Accent, 0.85f);
				MagicalFx.Fan(ShapeId.SmearCrescent, ctx.Center, ctx.Accent, 8, 3.6f, 20, 0.45f,
					baseAngle, 1.2f);
			}
			for (int i = 0; i < count; i++) {
				float t = count == 1 ? 0f : i / (float)(count - 1) - 0.5f;
				Vector2 dir = (baseAngle + t * 0.9f).ToRotationVector2();
				Star(ctx, MagicalStarProj.ModeHoming, 1f / MathF.Sqrt(count), ctx.Center + dir * 20f, dir * 11f);
			}
		}
	}

	/// <summary>#4 Chain Lightning Lattice - a bolt races out at the cursor; every
	/// enemy it strikes forks a fresh bolt at the next enemy while the original
	/// keeps flying, so the lattice walks a crowd without any aiming.</summary>
	public sealed class ChainLatticeAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 55;
		protected override float ReleaseDamageFactor => 0.9f;

		public override float ManaStartCost(float maxMana, float tier01)
			=> base.ManaStartCost(maxMana, tier01) * 1.25f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Voltaic(ctx, MagicalLightningProj.ModeChain, 1f, ctx.Center + ctx.Aim * 14f, ctx.Aim);
		}
	}

	/// <summary>#4b Ion Lance - a single heavy lightning lance that keeps going
	/// through everything in its lane, damaging every enemy along the path.</summary>
	public sealed class IonLanceAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 40;
		protected override float ReleaseDamageFactor => 1.6f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			// ONE lance. This used to fire three bolts in a +/-0.085 rad fan, which
			// is precisely the "spread out and multiple" lightning read: three
			// near-parallel squiggles that never matched each other, and all three
			// could land on the same enemy. A single lance carries its own width
			// and barbs now, so it reads as one heavy discharge - and the hit is
			// once per enemy, per bolt.
			Vector2 dir = ctx.Aim.SafeNormalize(Vector2.UnitX);
			Voltaic(ctx, MagicalLightningProj.ModeStraight, 1f, ctx.Center + dir * 14f, dir);
		}
	}

	/// <summary>#4c Chain Verdict - the strike walks: an enemy hit STOPS the bolt
	/// and the fork carries on from there, so the discharge steps target to
	/// target and ends buried in the crowd.</summary>
	public sealed class ChainVerdictAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 55;
		protected override float ReleaseDamageFactor => 1.05f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Voltaic(ctx, MagicalLightningProj.ModeCompleteChain, 1f, ctx.Center + ctx.Aim * 14f, ctx.Aim);
		}
	}

	/// <summary>#5 Blade Ring - twin returning scythes sweep out and home back.</summary>
	public sealed class BladeRingAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 40;
		protected override float ReleaseDamageFactor => 1.1f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 side = new(-ctx.Aim.Y, ctx.Aim.X);
			Star(ctx, MagicalStarProj.ModeBoomerang, 1f, ctx.Center + side * 18f, ctx.Aim * 13f);
			Star(ctx, MagicalStarProj.ModeBoomerang, 1f, ctx.Center - side * 18f, ctx.Aim * 13f);
			Burst(ctx, MagicalBurstProj.ModeBloom, 0.6f);
		}
	}

	/// <summary>#6 Nova Detonation - the core compresses darkly, then a single
	/// huge blast lands on you. Heavy mana, heavy hit.</summary>
	public sealed class NovaDetonationAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 120;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 2.3f;

		public override float ManaStartCost(float maxMana, float tier01)
			=> base.ManaStartCost(maxMana, tier01) * 1.4f;
		public override float ManaUpkeepPerSecond(float maxMana, float tier01)
			=> base.ManaUpkeepPerSecond(maxMana, tier01) * 1.3f;

		/// <summary>Compression beat: for the last third of the charge the field
		/// visibly squeezes - a warning ring tightens around the core and stray
		/// sparks are pulled INTO it - so the detonation has a wind-up instead
		/// of arriving unannounced.</summary>
		public override void OnChargeTick(in MagicalAbilityContext ctx)
		{
			float t = MathHelper.Clamp((ctx.Charge01 - 0.62f) / 0.38f, 0f, 1f);
			if (t <= 0f || Main.dedServ)
				return;
			float ringR = MathHelper.Lerp(150f, 54f, t);
			// Queued ring, NOT MagicalFx.Ring: this runs from AI (the charge
			// tick), and stamping shape art there throws "Draw was called, but
			// Begin has not yet been called" and silently kills the rest of the
			// tick. Glows are queued, so they are safe from update code.
			MagicalFx.QueueRing(ctx.Center, ringR, ctx.Accent, 0.22f + 0.40f * t, 30, 0.85f,
				Main.GameUpdateCount * 0.025f);
			MagicalFx.Bloom(ctx.Center, 40f + 30f * t, ctx.Accent, 0.10f + 0.18f * t);
			if (Main.rand.NextBool(2)) {
				float a = Main.rand.NextFloat(MathHelper.TwoPi);
				Vector2 from = ctx.Center + a.ToRotationVector2() * (ringR + 34f);
				YoyoParticles.SpawnSpark(from, (ctx.Center - from) * 0.12f, ctx.Accent,
					0.85f + 0.35f * t, 15, 0.93f);
			}
		}

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Burst(ctx, MagicalBurstProj.ModeNova, 1f);
			if (!Main.dedServ) {
				// Aftermath: a slow shock ring and a scorched floor mark, so the
				// blast leaves a readable footprint instead of a blink.
				YoyoParticles.SpawnRing(ctx.Center, 20f, 260f + 140f * ctx.Power01, 34, ctx.Accent, 4.2f);
				MagicalFx.Scorch(ctx.Center, ctx.Accent, 1.6f, crack: true);
				MiniYoyoFx.FlashFX(ctx.Center, Color.White, 1.35f);
			}
		}
	}

	// =========================================================================
	//  ARCHIVED RELEASES (cursor-focus rework): pulled from the catalogue so
	//  nothing deals them. Kept for reference; re-enable by returning the entry
	//  to MagicalAbilityCatalog.
	// =========================================================================

	/// <summary>#7 Warding Totem - a crystal totem deploys and fires by itself.
	/// Support firepower; the bolts still track and hit.</summary>
	public sealed class WardingTotemAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 300;
		protected override float ReleaseDamageFactor => 0.6f;

		public override float ManaStartCost(float maxMana, float tier01)
			=> base.ManaStartCost(maxMana, tier01) * 0.8f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Deploy(ctx, MagicalDeployProj.ModeTotem, 1f, ctx.Center);
		}
	}

	/// <summary>#8 Mirror Guard Wall - a travelling mirror wall slices what it
	/// touches, turns enemy shots around and sends them home. Support.</summary>
	public sealed class MirrorGuardWallAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 80;
		protected override float ReleaseDamageFactor => 0.85f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Deploy(ctx, MagicalDeployProj.ModeWall, 1f, ctx.Center, ctx.Aim * 5f);
		}
	}

	/// <summary>#9 Chrono Field - a bubble slows everything hostile inside,
	/// drains it, and leaves your own movement untouched. Support.</summary>
	public sealed class ChronoFieldAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 75;
		protected override int ReleaseWindowTicks => 260;
		protected override float ReleaseDamageFactor => 0.9f;

		public override Color AccentColor(int sourceProjType) => new(150, 230, 220);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Zone(ctx, MagicalZoneProj.ModeChrono, 1f, ctx.Center);
		}
	}

	/// <summary>#10 Portal Barrage - rifts open at your sides and stream bolts
	/// toward the cursor.</summary>
	public sealed class PortalBarrageAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 300;
		protected override float ReleaseDamageFactor => 0.55f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 side = new(-ctx.Aim.Y, ctx.Aim.X);
			Deploy(ctx, MagicalDeployProj.ModePortal, 1f, ctx.Center + side * 62f);
			Deploy(ctx, MagicalDeployProj.ModePortal, 1f, ctx.Center - side * 62f);
		}
	}

	/// <summary>#11 Lash Whip - a cord lashes onto the nearest enemy, drains it
	/// and reels it toward you.</summary>
	public sealed class LashWhipAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Tether(ctx, MagicalTetherProj.ModeDrag, 1f);
		}
	}

	/// <summary>#12 Orbital Lance - a marker grows at the cursor, then a pillar
	/// of light drops on it.</summary>
	public sealed class OrbitalLanceAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 105;
		protected override int ReleaseWindowTicks => 70;
		protected override float ReleaseDamageFactor => 1.6f;

		public override float ManaUpkeepPerSecond(float maxMana, float tier01)
			=> base.ManaUpkeepPerSecond(maxMana, tier01) * 1.15f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Burst(ctx, MagicalBurstProj.ModeLance, 1f, AimPoint(ctx, 290f));
		}
	}

	/// <summary>#13 Sanguine Leech - a blood tether drains the nearest enemy and
	/// mends you while it holds. Leech support that still cuts.</summary>
	public sealed class SanguineLeechAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 210;
		protected override float ReleaseDamageFactor => 0.85f;

		public override Color AccentColor(int sourceProjType) => new(220, 40, 60);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Tether(ctx, MagicalTetherProj.ModeLeech, 1f);
		}
	}

	/// <summary>#14 Crystal Garden - crystals grow around you, then shatter into
	/// an all-direction shard volley over a slowing field.</summary>
	public sealed class CrystalGardenAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 80;
		protected override float ReleaseDamageFactor => 0.85f;

		public override Color AccentColor(int sourceProjType) => new(190, 235, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Zone(ctx, MagicalZoneProj.ModeBramble, 0.75f, ctx.Center);
			for (int i = 0; i < 8; i++) {
				Vector2 dir = (MathHelper.TwoPi * i / 8f + Main.rand.NextFloat(0.2f)).ToRotationVector2();
				Star(ctx, MagicalStarProj.ModeShard, 0.8f, ctx.Center + dir * 16f,
					dir * (11f + Main.rand.NextFloat(3f)));
			}
		}
	}

	/// <summary>#15 Thunderclap Dash - a short dash toward the cursor, thunder
	/// bursting at every step and a slash where you stop.</summary>
	public sealed class ThunderclapDashAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 60;
		protected override int ReleaseWindowTicks => 34;
		protected override float ReleaseDamageFactor => 0.9f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 dir = ctx.Aim.SafeNormalize(Vector2.UnitX);
			Player owner = ctx.Owner;
			Burst(ctx, MagicalBurstProj.ModeSlash, 1f, ctx.Center + dir * 46f, dir);
			for (int i = 1; i <= 2; i++)
				Burst(ctx, MagicalBurstProj.ModeNova, 0.5f, ctx.Center - dir * (i * 52f));
			Charge(owner, dir, 9.5f);
			SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.3f }, ctx.Center);
		}
	}

	/// <summary>#16 Graviton Snare - anchors drag enemies into a shared point
	/// and hold them there while the well grinds.</summary>
	public sealed class GravitonSnareAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 160;
		protected override float ReleaseDamageFactor => 0.9f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Zone(ctx, MagicalZoneProj.ModeVoidWell, 1f, AimPoint(ctx, 150f));
			Burst(ctx, MagicalBurstProj.ModeBloom, 0.7f, AimPoint(ctx, 150f));
		}
	}

	/// <summary>#17 Phoenix Flare - burning wings orbit you and scorch anything
	/// that touches the ring.</summary>
	public sealed class PhoenixFlareAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 240;
		protected override float ReleaseDamageFactor => 0.9f;

		public override Color AccentColor(int sourceProjType) => new(255, 150, 60);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Burst(ctx, MagicalBurstProj.ModeNova, 0.8f);
			Deploy(ctx, MagicalDeployProj.ModeShield, 1f, ctx.Center);
		}
	}

	/// <summary>#18 Echo Twins - two spectral copies of the formation orbit you,
	/// cutting what they brush and mirroring the guards.</summary>
	public sealed class EchoTwinsAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 240;
		protected override float ReleaseDamageFactor => 0.75f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			// Spawn velocity carries each copy's orbit phase (half a turn apart).
			Deploy(ctx, MagicalDeployProj.ModeShield, 0.9f, ctx.Center, new Vector2(0f, 0f));
			Deploy(ctx, MagicalDeployProj.ModeShield, 0.9f, ctx.Center, new Vector2(157f, 0f));
		}
	}

	/// <summary>#19 Blade Cyclone - a ring of spinning blades tears outward
	/// through everything close, then reels back through the survivors.</summary>
	public sealed class BladeCycloneAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 120;
		protected override float ReleaseDamageFactor => 0.95f;

		public override Color AccentColor(int sourceProjType) => new(205, 220, 240);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			int saws = 3 + Math.Min(2, ctx.OrbitCount / 2);
			float baseAngle = ctx.Aim.ToRotation();
			for (int i = 0; i < saws; i++) {
				Vector2 dir = (baseAngle + MathHelper.TwoPi * i / saws).ToRotationVector2();
				Star(ctx, MagicalStarProj.ModeSaw, 0.8f, ctx.Center + dir * 22f, dir * 13f);
			}
			Burst(ctx, MagicalBurstProj.ModeSlash, 0.7f);
		}
	}

	/// <summary>#20 Sanctum Ward - runes consecrate the ground: you mend, and
	/// everything hostile standing inside is seared. Support.</summary>
	public sealed class SanctumWardAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 290;
		protected override float ReleaseDamageFactor => 0.8f;

		public override Color AccentColor(int sourceProjType) => new(255, 230, 150);
		public override float ManaStartCost(float maxMana, float tier01)
			=> base.ManaStartCost(maxMana, tier01) * 0.85f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Zone(ctx, MagicalZoneProj.ModeSanctum, 1f, ctx.Center);
		}
	}

	// =========================================================================
	//  Variation twenty-three: same framework, different shape and rhythm.
	// =========================================================================

	/// <summary>#21 Storm Ring - a crackling ring bursts out and static seeks
	/// the nearest bodies.</summary>
	public sealed class StormRingAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 80;
		protected override float ReleaseDamageFactor => 0.9f;

		public override Color AccentColor(int sourceProjType) => new(170, 205, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Burst(ctx, MagicalBurstProj.ModeNova, 1f);
			for (int i = 0; i < 4; i++) {
				float a = Main.rand.NextFloat(MathHelper.TwoPi);
				Star(ctx, MagicalStarProj.ModeHoming, 0.55f, ctx.Center + a.ToRotationVector2() * 20f,
					a.ToRotationVector2() * 9f);
			}
		}
	}

	/// <summary>#22 Frostbound Grasp - a freezing cone locks everything ahead,
	/// a shard volley peppers it, and an ice patch keeps it pinned.</summary>
	public sealed class FrostboundGraspAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 90;
		protected override float ReleaseDamageFactor => 1f;

		public override Color AccentColor(int sourceProjType) => new(140, 210, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 aim = ctx.Aim.SafeNormalize(Vector2.UnitX);
			Burst(ctx, MagicalBurstProj.ModeCone, 1f);
			for (int i = -1; i <= 1; i++) {
				Vector2 dir = aim.RotatedBy(i * 0.24f);
				Star(ctx, MagicalStarProj.ModeShard, 0.55f, ctx.Center + dir * 18f, dir * 13f);
			}
			Zone(ctx, MagicalZoneProj.ModeBramble, 0.8f, AimPoint(ctx, 150f));
		}
	}

	/// <summary>#23 Blink Slash - three short blinks toward the cursor, a wide
	/// crescent sweep carved at every landing.</summary>
	public sealed class BlinkSlashAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 60;
		protected override int ReleaseWindowTicks => 40;
		protected override float ReleaseDamageFactor => 0.85f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 dir = ctx.Aim.SafeNormalize(Vector2.UnitX);
			Player owner = ctx.Owner;
			for (int i = 0; i < 3; i++) {
				Burst(ctx, MagicalBurstProj.ModeSlash, 0.85f, ctx.Center + dir * (i * 96f), dir);
				Blink(owner, dir, 96f);
			}
			SoundEngine.PlaySound(SoundID.Item24 with { Volume = 0.6f, Pitch = 0.4f }, ctx.Center);
		}
	}

	/// <summary>#24 Molten Geyser - the ground erupts in a line toward the
	/// cursor.</summary>
	public sealed class MoltenGeyserAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 100;
		protected override int ReleaseWindowTicks => 110;
		protected override float ReleaseDamageFactor => 1.1f;

		public override Color AccentColor(int sourceProjType) => new(255, 120, 40);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Burst(ctx, MagicalBurstProj.ModeGeyser, 1f);
		}
	}

	/// <summary>#25 Seeking Anchors - spinning anchors fly out, then haul their
	/// catch back to you.</summary>
	public sealed class SeekingAnchorsAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 0.95f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Tether(ctx, MagicalTetherProj.ModeDrag, 1f);
			Vector2 side = new(-ctx.Aim.Y, ctx.Aim.X);
			Star(ctx, MagicalStarProj.ModeBoomerang, 0.7f, ctx.Center + side * 14f, ctx.Aim * 12f);
			Star(ctx, MagicalStarProj.ModeBoomerang, 0.7f, ctx.Center - side * 14f, ctx.Aim * 12f);
		}
	}

	/// <summary>#26 Halo Sentries - three totems take up a ring around you.</summary>
	public sealed class HaloSentriesAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 300;
		protected override float ReleaseDamageFactor => 0.6f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			for (int i = 0; i < 3; i++) {
				float a = MathHelper.TwoPi * i / 3f;
				Deploy(ctx, MagicalDeployProj.ModeTotem, 0.8f, ctx.Center + a.ToRotationVector2() * 96f);
			}
		}
	}

	/// <summary>#27 Prismatic Prism - the release splits into three rays of
	/// different colour.</summary>
	public sealed class PrismaticPrismAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 55;
		protected override float ReleaseDamageFactor => 0.9f;

		public override Color AccentColor(int sourceProjType) => new(235, 200, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 aim = ctx.Aim.SafeNormalize(Vector2.UnitX);
			for (int i = -1; i <= 1; i++)
				Burst(ctx, MagicalBurstProj.ModeCone, 1f, ctx.Center, aim.RotatedBy(i * 0.34f));
		}
	}

	/// <summary>#28 Spotlight Judgement - three pillars of judgement walk away
	/// from you along the cursor line.</summary>
	public sealed class SpotlightJudgementAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 110;
		protected override int ReleaseWindowTicks => 90;
		protected override float ReleaseDamageFactor => 1.2f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			for (int i = 0; i < 3; i++)
				Burst(ctx, MagicalBurstProj.ModeLance, 0.85f, AimPoint(ctx, 170f + i * 150f));
		}
	}

	/// <summary>#29 Leech Bramble - thorn vines root a patch of ground and drink
	/// from whatever they catch.</summary>
	public sealed class LeechBrambleAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 200;
		protected override float ReleaseDamageFactor => 0.8f;

		public override Color AccentColor(int sourceProjType) => new(120, 200, 110);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Zone(ctx, MagicalZoneProj.ModeBramble, 1f, AimPoint(ctx, 130f));
			Tether(ctx, MagicalTetherProj.ModeLeech, 0.9f);
		}
	}

	/// <summary>#30 Bubble Prison - a drifting bubble field traps everything
	/// that walks into it and bursts in their face.</summary>
	public sealed class BubblePrisonAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 190;
		protected override float ReleaseDamageFactor => 0.75f;

		public override Color AccentColor(int sourceProjType) => new(170, 225, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Zone(ctx, MagicalZoneProj.ModeBubble, 1f, AimPoint(ctx, 180f));
			Burst(ctx, MagicalBurstProj.ModeBloom, 0.7f, AimPoint(ctx, 180f));
		}
	}

	/// <summary>#31 Runewarden Sigil - a rune waits on the ground and detonates
	/// when anything steps onto it.</summary>
	public sealed class RunewardenSigilAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 70;
		protected override int ReleaseWindowTicks => 26;
		protected override float ReleaseDamageFactor => 1.25f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Zone(ctx, MagicalZoneProj.ModeSigil, 1f, AimPoint(ctx, 160f));
		}
	}

	/// <summary>#32 Echo Barrage - a rapid spray of spectral seeking shots.</summary>
	public sealed class EchoBarrageAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 44;
		protected override float ReleaseDamageFactor => 0.6f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 aim = ctx.Aim.SafeNormalize(Vector2.UnitX);
			for (int i = 0; i < 5; i++) {
				float t = i / 4f - 0.5f;
				Vector2 dir = aim.RotatedBy(t * 0.5f);
				Star(ctx, MagicalStarProj.ModeHoming, 0.75f, ctx.Center + dir * 18f, dir * (13f + 2f * i));
			}
		}
	}

	/// <summary>#33 Void Collapse - everything in reach is dragged in, then the
	/// point crushes shut and keeps swallowing for the whole release window, so
	/// the cast reads as a sustained maw instead of a single pop.</summary>
	public sealed class VoidCollapseAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 110;
		protected override int ReleaseWindowTicks => 90;
		protected override float ReleaseDamageFactor => 1.6f;

		public override Color AccentColor(int sourceProjType) => new(150, 90, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 point = AimPoint(ctx, 120f);
			Burst(ctx, MagicalBurstProj.ModeImplosion, 1f, point);
			if (Main.dedServ)
				return;
			// The swallow, not just the pop: an infalling ring of wisps, a dark
			// lens and a fan of shards announce the collapse in one beat.
			MagicalReleaseCommon.InfallWisps(point, 190f, new Color(150, 90, 255), 16, 1.9f);
			MagicalFx.Fan(ShapeId.WispTaper, point, new Color(120, 60, 200), 8, 4f, 22, 0.5f);
			MiniBossShapeBank.Spawn(ShapeId.RingCracked, point, 0f, new Vector2(130f / 64f),
				new Color(40, 16, 60) * 0.9f, 26, Vector2.Zero, 0.01f, 0f);
			MagicalFx.Fan(ShapeId.SplatStar, point, new Color(150, 90, 255), 6, 3.4f, 22, 0.75f);
			MiniYoyoFx.FlashFX(point, new Color(120, 60, 200), 0.9f);
			MiniYoyoFx.Shake(3, 8, point);
		}

		public override void OnFiringTick(in MagicalAbilityContext ctx)
		{
			base.OnFiringTick(ctx);
			if (Main.dedServ)
				return;
			// The point keeps swallowing AT the cursor for the whole window:
			// gentle draw + wisps on a fixed cadence (no per-instance state, so
			// two casters can never desync each other's collapse).
			Vector2 point = AimPoint(ctx, 120f);
			if (Main.GameUpdateCount % 8 == 0)
				MagicalReleaseCommon.InfallWisps(point, 150f, new Color(150, 90, 255), 5, 1.3f);
			if (Main.GameUpdateCount % 14 == 0)
				YoyoParticles.SpawnBurst(point + Main.rand.NextVector2Circular(20f, 20f), 3, 1.4f,
					new Color(120, 60, 200), 0.4f, 26);
			if (MagicalReleaseCommon.CanMoveNpcs && Main.GameUpdateCount % 20 == 0)
				MagicalReleaseCommon.RadialGrab(point, 240f, 0.16f, towardCentre: true);
		}
	}

	/// <summary>#34 Starfall Verdict - five meteors answer the call, landing in
	/// a spread around the cursor.</summary>
	public sealed class StarfallVerdictAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 100;
		protected override int ReleaseWindowTicks => 90;
		protected override float ReleaseDamageFactor => 0.9f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 point = AimPoint(ctx, 260f);
			for (int i = 0; i < 5; i++) {
				float t = i / 4f - 0.5f;
				Vector2 at = point + new Vector2(t * 210f, -520f - i * 55f);
				Star(ctx, MagicalStarProj.ModeFall, 1f, at, new Vector2(t * 1.4f, 12f));
			}
		}
	}

	/// <summary>#35 Thunderstorm Crown - a storm cloud follows you and calls
	/// strikes down on whatever is nearest.</summary>
	public sealed class ThunderstormCrownAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 290;
		protected override float ReleaseDamageFactor => 0.75f;

		public override Color AccentColor(int sourceProjType) => new(180, 190, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Deploy(ctx, MagicalDeployProj.ModeCloud, 1f, ctx.Center - new Vector2(0f, 120f));
		}
	}

	/// <summary>#36 Mirror Maze - a prism cage slams shut on a point while
	/// mirror walls give the pocket edges and turn return fire around.</summary>
	public sealed class MirrorMazeAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 190;
		protected override float ReleaseDamageFactor => 0.8f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 point = AimPoint(ctx, 190f);
			Deploy(ctx, MagicalDeployProj.ModeCage, 1f, point);
			for (int i = 0; i < 2; i++) {
				float a = MathHelper.PiOver2 + MathHelper.Pi * i;
				// A whisper of velocity only sets the wall's orientation.
				Deploy(ctx, MagicalDeployProj.ModeWall, 1f, point + a.ToRotationVector2() * 96f,
					a.ToRotationVector2() * 0.1f);
			}
		}
	}

	/// <summary>#37 Chrono Anchor - you step through time to the cursor and leave
	/// a slow bubble behind where you stood.</summary>
	public sealed class ChronoAnchorAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 70;
		protected override int ReleaseWindowTicks => 60;
		protected override float ReleaseDamageFactor => 0.7f;

		public override Color AccentColor(int sourceProjType) => new(150, 230, 220);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 old = ctx.Center;
			Zone(ctx, MagicalZoneProj.ModeChrono, 1f, old);
			Blink(ctx.Owner, ctx.Aim, 230f);
			Burst(ctx, MagicalBurstProj.ModeBloom, 0.7f, old);
		}
	}

	/// <summary>#38 Bulwark Charge - you ram forward behind a mirror shield.</summary>
	public sealed class BulwarkChargeAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 40;
		protected override float ReleaseDamageFactor => 1f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 dir = ctx.Aim.SafeNormalize(Vector2.UnitX);
			Deploy(ctx, MagicalDeployProj.ModeWall, 1f, ctx.Center + dir * 40f, dir * 4.5f);
			Charge(ctx.Owner, dir, 7.5f);
			Burst(ctx, MagicalBurstProj.ModeSlash, 0.9f, ctx.Center + dir * 70f, dir);
		}
	}

	/// <summary>#39 Sanctified Crown - holy ground under you plus a warding
	/// orbit: mend and faith in one cast. Support.</summary>
	public sealed class SanctifiedCrownAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 260;
		protected override float ReleaseDamageFactor => 0.75f;

		public override Color AccentColor(int sourceProjType) => new(255, 235, 170);
		public override float ManaStartCost(float maxMana, float tier01)
			=> base.ManaStartCost(maxMana, tier01) * 0.9f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Zone(ctx, MagicalZoneProj.ModeSanctum, 1f, ctx.Center);
			Deploy(ctx, MagicalDeployProj.ModeShield, 0.9f, ctx.Center);
		}
	}

	/// <summary>#40 Kaleidoscope Nova - six rays of colour burst out in every
	/// direction, capped by a ring.</summary>
	public sealed class KaleidoscopeNovaAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 115;
		protected override int ReleaseWindowTicks => 90;
		protected override float ReleaseDamageFactor => 0.9f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			for (int i = 0; i < 6; i++) {
				Vector2 dir = (MathHelper.TwoPi * i / 6f).ToRotationVector2();
				Burst(ctx, MagicalBurstProj.ModeCone, 0.75f, ctx.Center, dir);
			}
			Burst(ctx, MagicalBurstProj.ModeNova, 1f);
		}
	}

	/// <summary>#41 Soul Reap - three wide soul-taking crescents sweep out in a
	/// fan. Every kill the sweep lands mends the wielder: pure offence that
	/// pays you back for committing to the front line.</summary>
	public sealed class SoulReapAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 80;
		protected override int ReleaseWindowTicks => 60;
		protected override float ReleaseDamageFactor => 1.1f;

		public override Color AccentColor(int sourceProjType) => new(180, 235, 210);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 aim = ctx.Aim.SafeNormalize(Vector2.UnitX);
			Burst(ctx, MagicalBurstProj.ModeSlash, 1f, ctx.Center, aim);
			Burst(ctx, MagicalBurstProj.ModeSlash, 0.75f, ctx.Center, aim.RotatedBy(0.55f));
			Burst(ctx, MagicalBurstProj.ModeSlash, 0.75f, ctx.Center, aim.RotatedBy(-0.55f));
			Tether(ctx, MagicalTetherProj.ModeLeech, 0.6f);
		}
	}

	/// <summary>#42 Prism Cage - six light walls drop around a point and grind
	/// whatever is inside, then the cage detonates inward.</summary>
	public sealed class PrismCageAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 95;
		protected override int ReleaseWindowTicks => 190;
		protected override float ReleaseDamageFactor => 1f;

		public override Color AccentColor(int sourceProjType) => new(210, 190, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 aim = ctx.Aim.SafeNormalize(Vector2.UnitX);
			Deploy(ctx, MagicalDeployProj.ModeCage, 1f, AimPoint(ctx, 190f));
			for (int i = -1; i <= 1; i += 2)
				Burst(ctx, MagicalBurstProj.ModeCone, 0.6f, ctx.Center, aim.RotatedBy(i * 0.5f));
		}
	}

	/// <summary>#43 Pyre Column - the ground erupts into a standing wall of
	/// flame that scorches everything inside it and lights the sky.</summary>
	public sealed class PyreColumnAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 90;
		protected override int ReleaseWindowTicks => 140;
		protected override float ReleaseDamageFactor => 0.85f;

		public override Color AccentColor(int sourceProjType) => new(255, 140, 60);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Zone(ctx, MagicalZoneProj.ModePyre, 1f, AimPoint(ctx, 160f));
			Burst(ctx, MagicalBurstProj.ModeGeyser, 0.85f);
		}
	}

	// =========================================================================
	//  CURSOR WELLS - the beam lands on the cursor and the pool does the rest.
	// =========================================================================

	/// <summary>#44 Vertex Well - the snap beam stops on the cursor and opens a
	/// whirlpool whose eye auto-fires instant micro-beams at the pack.</summary>
	public sealed class VertexWellAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 90;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.1f;

		public override Color AccentColor(int sourceProjType) => new(160, 205, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Well(ctx, MagicalPuddleProj.ModeVertex, 1f);
		}
	}

	/// <summary>#45 Steering Well - the pool lobs orbs you curve with the cursor.</summary>
	public sealed class SteeringWellAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 90;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1f;

		public override Color AccentColor(int sourceProjType) => new(178, 160, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Well(ctx, MagicalPuddleProj.ModeSteer, 1f);
		}
	}

	/// <summary>#46 Seeking Well - the pool lobs homing orbs.</summary>
	public sealed class SeekingWellAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 90;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1f;

		public override Color AccentColor(int sourceProjType) => new(196, 205, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Well(ctx, MagicalPuddleProj.ModeSeek, 1f);
		}
	}

	/// <summary>#47 Rapid Well - a fast straight stream out of the pool.</summary>
	public sealed class RapidWellAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 90;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 0.95f;

		public override Color AccentColor(int sourceProjType) => new(205, 225, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Well(ctx, MagicalPuddleProj.ModeRapid, 1f);
		}
	}

	/// <summary>#48 Brood Well - the pool births a growing, piercing worm.</summary>
	public sealed class WormWellAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 100;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.25f;

		public override Color AccentColor(int sourceProjType) => new(176, 140, 226);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Well(ctx, MagicalPuddleProj.ModeWorm, 1f);
		}
	}

	/// <summary>#49 Fracture Well - the pool spits seeds that split into shards.</summary>
	public sealed class FractureWellAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 90;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.15f;

		public override Color AccentColor(int sourceProjType) => new(160, 230, 178);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Well(ctx, MagicalPuddleProj.ModeSplit, 1f);
		}
	}

	/// <summary>#50 Orbit Feeder - spends ring yoyos as ricochet rounds.</summary>
	public sealed class OrbitFeederAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 80;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.15f;

		public override Color AccentColor(int sourceProjType) => new(200, 215, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			FeedOrbiters(ctx, 1f);
		}
	}

	/// <summary>#51 Lifedrain Nova - a cursor detonation that pays you health.</summary>
	public sealed class LifedrainNovaAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 95;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.35f;

		public override Color AccentColor(int sourceProjType) => new(255, 96, 116);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			LifedrainNova(ctx, 1f);
		}
	}

	/// <summary>#52 Weaver (Sine) - a mirrored sine braid across the aim line.</summary>
	public sealed class WeaverSineAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.05f;

		public override Color AccentColor(int sourceProjType) => new(190, 190, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			WeaverPair(ctx, spiral: false, 1f);
		}
	}

	/// <summary>#53 Weaver (Spiral) - two counter-rotating spirals that cross.</summary>
	public sealed class WeaverSpiralAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.05f;

		public override Color AccentColor(int sourceProjType) => new(196, 150, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			WeaverPair(ctx, spiral: true, 1f);
		}
	}

	/// <summary>#59 Weaver (Saw) - a hard-edged sawtooth pair: linear ramps
	/// that snap back at every tooth, so the braid reads as machinery.</summary>
	public sealed class WeaverSawAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.05f;

		public override Color AccentColor(int sourceProjType) => new(214, 206, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			WeaverWave(ctx, MagicalWeaverProj.ModeSawA, MagicalWeaverProj.ModeSawB, 1f);
		}
	}

	/// <summary>#60 Weaver (Triangle) - a triangular wave pair: straight rises
	/// and falls with no dwell at the peaks.</summary>
	public sealed class WeaverTriangleAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.05f;

		public override Color AccentColor(int sourceProjType) => new(182, 236, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			WeaverWave(ctx, MagicalWeaverProj.ModeTriangleA, MagicalWeaverProj.ModeTriangleB, 1f);
		}
	}

	/// <summary>#61 Weaver (Damped) - a hard first swing that bleeds out: the
	/// wave starts wide and settles into the aim line.</summary>
	public sealed class WeaverDampedAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.05f;

		public override Color AccentColor(int sourceProjType) => new(255, 198, 236);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			WeaverWave(ctx, MagicalWeaverProj.ModeDampedA, MagicalWeaverProj.ModeDampedB, 1f);
		}
	}

	/// <summary>#62 Weaver (Figure) - a 1:2 Lissajous pair: the two ribbons
	/// braid a figure-eight knot instead of crossing on a flat axis.</summary>
	public sealed class WeaverFigureAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.05f;

		public override Color AccentColor(int sourceProjType) => new(255, 232, 170);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			WeaverWave(ctx, MagicalWeaverProj.ModeFigureA, MagicalWeaverProj.ModeFigureB, 1f);
		}
	}

	/// <summary>#63 Weaver (Interference) - two source ribbons whose sum is the
	/// hit path; the bright beat seam is the payoff frame.</summary>
	public sealed class WeaverInterferenceAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.05f;

		public override Color AccentColor(int sourceProjType) => new(150, 225, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			WeaverWave(ctx, MagicalWeaverProj.ModeInterferenceA, MagicalWeaverProj.ModeInterferenceB, 1f);
		}
	}

	/// <summary>#64 Weaver (Astroid) - a four-cusped star loop rolled along the
	/// aim line. Sharp geometry, not another smooth braid.</summary>
	public sealed class WeaverAstroidAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.05f;

		public override Color AccentColor(int sourceProjType) => new(255, 220, 160);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			WeaverWave(ctx, MagicalWeaverProj.ModeAstroidA, MagicalWeaverProj.ModeAstroidB, 1f);
		}
	}

	/// <summary>#65 Weaver (Ranunculoid) - a five-cusped epicycloid; the literal
	/// "n=5 circle" flower loop, with a petal flare at every cusp.</summary>
	public sealed class WeaverRanunculoidAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.05f;

		public override Color AccentColor(int sourceProjType) => new(255, 150, 220);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			WeaverWave(ctx, MagicalWeaverProj.ModeRanunculoidA, MagicalWeaverProj.ModeRanunculoidB, 1f);
		}
	}

	/// <summary>#66 Weaver (Lissajous Knot) - a 3:2 projection whose depth axis
	/// (cos 5t) drives draw scale and alpha. Distinct from the 1:2 Figure-Eight.</summary>
	public sealed class WeaverKnotAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.05f;

		public override Color AccentColor(int sourceProjType) => new(200, 170, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			WeaverWave(ctx, MagicalWeaverProj.ModeKnotA, MagicalWeaverProj.ModeKnotB, 1f);
		}
	}

	/// <summary>#54 Growing Star - lobbed at the cursor, swells, then detonates
	/// with a blast sized by how much it actually grew.</summary>
	public sealed class GrowingStarAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 95;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.3f;

		public override Color AccentColor(int sourceProjType) => new(196, 170, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			GrowOrb(ctx, 1f);
		}
	}

	/// <summary>#55 Pulse Cascade - a 0.1s instant beam at the cursor, over and
	/// over, for two seconds. The focus is a strobe, not a sustained beam.</summary>
	public sealed class PulseCascadeAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 85;
		protected override int ReleaseWindowTicks => 130;
		protected override float ReleaseDamageFactor => 1f;

		public override Color AccentColor(int sourceProjType) => new(186, 214, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			PulseCascade(ctx, 1f);
		}
	}

	/// <summary>#56 Brand of Ruin (Ember) - the cursor stamps a burning mark that
	/// bites every 0.1s and jumps between victims as you sweep it.</summary>
	public sealed class BrandEmberAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 70;
		protected override int ReleaseWindowTicks => 26;
		protected override float ReleaseDamageFactor => 1.3f;

		public override Color AccentColor(int sourceProjType) => new(255, 148, 64);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Brand(ctx, MagicalBrandProj.FlavorEmber, 1f);
		}
	}

	/// <summary>#57 Brand of Ruin (Frost) - a frozen sigil that bites and shards.</summary>
	public sealed class BrandFrostAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 70;
		protected override int ReleaseWindowTicks => 26;
		protected override float ReleaseDamageFactor => 1.25f;

		public override Color AccentColor(int sourceProjType) => new(150, 215, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Brand(ctx, MagicalBrandProj.FlavorFrost, 1f);
		}
	}

	/// <summary>#58 Brand of Ruin (Storm) - a voltaic sigil ringed with arcs.</summary>
	public sealed class BrandStormAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 70;
		protected override int ReleaseWindowTicks => 26;
		protected override float ReleaseDamageFactor => 1.3f;

		public override Color AccentColor(int sourceProjType) => new(178, 205, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Brand(ctx, MagicalBrandProj.FlavorStorm, 1f);
		}
	}

	/// <summary>#59 Brand of Ruin (Void) - a collapsing mark that drags motes in.</summary>
	public sealed class BrandVoidAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 70;
		protected override int ReleaseWindowTicks => 26;
		protected override float ReleaseDamageFactor => 1.35f;

		public override Color AccentColor(int sourceProjType) => new(178, 120, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Brand(ctx, MagicalBrandProj.FlavorVoid, 1f);
		}
	}

	// =========================================================================
	//  CURSOR WAVE 2 - the approved board: snap trap, anchor chain and pocket
	//  singularity, plus the lightning well that completes the puddle family
	//  (Vertex micro-beams + Rapid stream + Storm chain lightning).
	// =========================================================================

	/// <summary>#60 Lightning Well - the seventh puddle. The bowl crackles and
	/// spits live bolts that leap from victim to victim; with Vertex (beams)
	/// and Rapid (stream), the pool now covers all three fire flavours.</summary>
	public sealed class LightningWellAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 90;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.1f;

		public override Color AccentColor(int sourceProjType) => new(185, 200, 255);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Well(ctx, MagicalPuddleProj.ModeStorm, 1f);
		}
	}

	/// <summary>#61 Snaptrap Bloom - a spring-loaded flower planted AT the
	/// cursor. It blooms, lunges at whatever comes near with tooth-ringed jaws,
	/// and finally bursts into seeds that crack into shards. Bites scale with
	/// the yoyo's tier (3 early, up to 5 at the top).</summary>
	public sealed class SnaptrapBloomAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 95;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.25f;

		public override Color AccentColor(int sourceProjType) => new(150, 235, 120);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 at = CursorPoint(ctx, 200f, 60f, 420f);
			Special(ctx, ModContent.ProjectileType<MagicalSnapTrapProj>(), 1f, at);
		}
	}

	/// <summary>#62 Anchor Chain - the cursor throws a hook; wherever it lands
	/// becomes a mooring point, and the chain hauls everything around it in.
	/// Heavy armour, on a leash.</summary>
	public sealed class AnchorChainAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 80;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.2f;

		public override Color AccentColor(int sourceProjType) => new(205, 185, 140);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 at = CursorPoint(ctx, 320f, 90f, 560f);
			Vector2 dir = (at - ctx.Center).SafeNormalize(ctx.Aim);
			Special(ctx, ModContent.ProjectileType<MagicalAnchorProj>(), 1f,
				ctx.Center, dir * MagicalAnchorProj.LaunchSpeed);
		}
	}

	/// <summary>#63 Pocket Singularity - a black pocket at the cursor. It pulls
	/// the pack in, chews on them, then collapses into a shockwave pointed the
	/// other way. The late-yoyo showpiece.</summary>
	public sealed class PocketSingularityAbility : MagicalReleaseProfile
	{
		public override int ChargeTicks => 110;
		protected override int ReleaseWindowTicks => 30;
		protected override float ReleaseDamageFactor => 1.5f;

		public override Color AccentColor(int sourceProjType) => new(120, 60, 200);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 at = CursorPoint(ctx, 240f, 80f, 480f);
			Special(ctx, ModContent.ProjectileType<MagicalSingularityProj>(), 1f, at);
		}
	}
}
