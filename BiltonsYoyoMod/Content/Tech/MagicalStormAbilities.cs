using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Configuration;
using BiltonsYoyoMod.Content.Projectiles.Tech;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// Relay Storm. A Straight bolt whose bites leave storm cells. Each cell
	/// fires one return bolt along the bearing captured at cast time, so the
	/// network is readable and deterministic in multiplayer.
	/// </summary>
	public sealed class RelayStormAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 44;
		protected override float ReleaseDamageFactor => 1.05f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			// The bolt captures its own initial velocity as its return bearing.
			// No unsynced fourth ai slot is needed.
			Voltaic(ctx, MagicalLightningProj.ModeRelay, 1f,
				ctx.Center + ctx.Aim * 16f, ctx.Aim);
		}
	}

	/// <summary>
	/// Faraday Lattice. A Chain bolt whose bites anchor a lattice. The field
	/// controller connects the anchors with lightning, hits enemies crossing
	/// an edge once, then discharges the whole chain.
	/// </summary>
	public sealed class FaradayLatticeAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 82;
		protected override float ReleaseDamageFactor => 0.95f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			// The field is spawned first and owns the cast. Anchors are
			// matched to it by owner + mode, so no extra synced slot is
			// needed and old casts are purged on the field's first tick.
			MagicalReleaseCommon.Spawn(ctx.Owner, ctx.Center, Vector2.Zero,
				ModContent.ProjectileType<MagicalChainFieldProj>(), Damage(ctx, 1f),
				MagicalChainFieldProj.ModeFaraday, 1f, ctx.SourceProjType);
			Voltaic(ctx, MagicalLightningProj.ModeFaraday, 1f,
				ctx.Center + ctx.Aim * 16f, ctx.Aim);
		}
	}

	/// <summary>
	/// Polarity Reversal. A Chain bolt whose bites become alternating poles.
	/// The field pulls and pushes enemies between the poles, then snaps them
	/// toward the final bite once. No per-tick damage loop.
	/// </summary>
	public sealed class PolarityReversalAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 82;
		protected override float ReleaseDamageFactor => 0.95f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			MagicalReleaseCommon.Spawn(ctx.Owner, ctx.Center, Vector2.Zero,
				ModContent.ProjectileType<MagicalChainFieldProj>(), Damage(ctx, 1f),
				MagicalChainFieldProj.ModePolarity, 1f, ctx.SourceProjType);
			Voltaic(ctx, MagicalLightningProj.ModePolarity, 1f,
				ctx.Center + ctx.Aim * 16f, ctx.Aim);
		}
	}

	/// <summary>
	/// Ball Lightning. A real rolling, bouncing orb with one contact hit per
	/// enemy. It is not a floating well and does not spray extra bolts.
	/// </summary>
	public sealed class BallLightningAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 34;
		protected override float ReleaseDamageFactor => 1.3f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Special(ctx, ModContent.ProjectileType<MagicalBallLightningProj>(), 1f,
				ctx.Center + ctx.Aim * 22f, ctx.Aim * 8.5f);
		}
	}

	/// <summary>
	/// Thunderhead. A stationary storm cloud that strikes different nearby
	/// enemies one at a time. It is intentionally a cloud with an eye, rain
	/// and internal arcs, not a row of identical lightning bolts.
	/// </summary>
	public sealed class ThunderheadAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 40;
		protected override float ReleaseDamageFactor => 1.15f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Vector2 at = CursorPoint(ctx, 300f, 90f, 760f);
			Special(ctx, ModContent.ProjectileType<MagicalThunderheadProj>(), 1f, at);
		}
	}

	/// <summary>
	/// Static Trail. A charged line that marks each enemy it crosses with
	/// Static Charge. The next lightning hit on a marked enemy is amplified
	/// and discharges one short arc to another enemy.
	/// </summary>
	public sealed class StaticTrailAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 34;
		protected override float ReleaseDamageFactor => 1.05f;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			Special(ctx, ModContent.ProjectileType<MagicalStaticTrailProj>(), 1f,
				ctx.Center + ctx.Aim * 18f, ctx.Aim);
		}
	}

	/// <summary>
	/// Ground Fault. The config picks one of two readings at cast time:
	/// Ground-Hugger lands on the first floor and runs along it, throwing
	/// short arcs upward; Gravity Bounce is a real gravity projectile that
	/// reflects off tiles and loses speed per bounce. Both are one bolt, one
	/// hit per enemy, and neither chains.
	/// </summary>
	public sealed class GroundFaultAbility : MagicalReleaseProfile
	{
		protected override int ReleaseWindowTicks => 42;
		protected override float ReleaseDamageFactor => 1.2f;

		public override Color AccentColor(int sourceProjType) => new(255, 196, 92);

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);

			YoyoServerConfig cfg = ModContent.GetInstance<YoyoServerConfig>();
			bool hugger = cfg != null && cfg.GroundFault == GroundFaultMode.GroundHugger;
			float mode = hugger
				? MagicalGroundFaultProj.ModeGroundHugger
				: MagicalGroundFaultProj.ModeGravityBounce;
			// Ground-Hugger wants the floor, not the cursor's Y: fire it flat
			// toward the cursor with a downward bias, then let gravity find
			// the first surface. Gravity Bounce is a true aimed lob.
			Vector2 flat = new(Math.Sign(ctx.Aim.X) == 0 ? ctx.Owner.direction : Math.Sign(ctx.Aim.X), 0.55f);
			Vector2 velocity = hugger ? flat.SafeNormalize(Vector2.UnitX) * 8.5f : ctx.Aim * 11f;

			// Deliberately not the Special() helper: that reserves ai[0] for
			// power, while Ground Fault needs ai[0] to carry the config-selected
			// mode to every client and the server.
			MagicalReleaseCommon.Spawn(ctx.Owner, ctx.Center + ctx.Aim * 18f, velocity,
				ModContent.ProjectileType<MagicalGroundFaultProj>(), Damage(ctx, 1f),
				mode, ctx.Power01, ctx.SourceProjType);
		}
	}

}
