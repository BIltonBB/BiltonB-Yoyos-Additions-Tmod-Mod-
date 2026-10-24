using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles.Tech;
using BiltonsYoyoMod.Content.Systems;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// B-P beam: Prism Gate. The beam is a sustained aim ray like Orbital
	/// Convergence, but it terminates in a cut-gem puddle. The puddle fires
	/// three short refracted rays (centre, +/-30 degrees) on a cadence.
	/// Lord's Deathray is the B-N counterpart and deliberately has no puddle.
	/// </summary>
	public sealed class PrismGateAbility : OrbitalConvergenceProfile
	{
		public override float BeamDamageFraction => 0.14f;
		public override float BeamRange => 820f;
		public override float BeamThickness => 22f;
		public override bool BeamHasEndPuddle => true;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			if (Main.dedServ)
				return;
			// Prismatic release beat: one cool-white snap plus three coloured
			// shards, so the gate reads before the first refracted volley.
			Color hot = Color.Lerp(ctx.Accent, Color.White, 0.65f);
			YoyoParticles.SpawnRing(ctx.Center, 8f, 96f * ctx.TierSize, 22, hot, 2.6f);
			for (int i = 0; i < 3; i++) {
				float ang = ctx.Aim.ToRotation() + MathHelper.TwoPi * i / 3f;
				MagicalFx.Fan(ShapeId.WispTaper, ctx.Center, hot, 3, 3.2f, 16, 0.45f, ang, 0.5f);
			}
		}

		public override void OnBeamEndTick(Projectile beam, in MagicalAbilityContext ctx,
			Vector2 end, float liveLength)
		{
			// localAI is only three slots on a vanilla-sized projectile; guard
			// it so a projectile whose arrays were rebuilt by another mod (or
			// an unloaded clone) can never take the whole beam down.
			if (beam.localAI == null || beam.localAI.Length == 0 || beam.localAI[0] != 0f
				|| ctx.Owner == null || ctx.Owner.whoAmI != Main.myPlayer)
				return;
			beam.localAI[0] = 1f;
			int type = ModContent.ProjectileType<MagicalBeamAnchorProj>();
			// The anchor finds its live beam by owner, not by an unsynced
			// fourth ai slot. ai[0..2] already carry mode, power and source.
			MagicalReleaseCommon.Spawn(ctx.Owner, end, Vector2.Zero, type,
				beam.damage, MagicalBeamAnchorProj.ModePrism, ctx.Power01, ctx.SourceProjType);
		}

		public override void DrawBeam(Projectile beam, Vector2 origin, Vector2 end, Color accent,
			float power01, float grow)
		{
			Vector2 delta = end - origin;
			float len = delta.Length();
			if (len < 8f)
				return;
			float width = (5f + 9f * power01) * (1f + 0.04f * (float)Math.Sin(Main.GameUpdateCount * 0.55f + beam.whoAmI));
			Color core = CoreColor(accent);
			MiniYoyoFx.DrawBeamV2(origin, end, core, accent, width, grow, 1.5f);
			MiniYoyoFx.DrawBeamHeatShimmer(origin, end, accent, width, grow);
			MiniYoyoFx.DrawChromaticSplit((origin + end) * 0.5f, delta.ToRotation(), len * grow,
				width * 0.85f, core, 0.20f * grow, 3f);
		}

		public override void DrawBeamEnd(Projectile beam, Vector2 end, Color accent,
			float power01, float grow)
		{
			// The anchor draws the full gem; this keeps the join bright for the
			// one tick before it spawns.
			MiniYoyoFx.DrawGlowAdditive(end, 34f + 12f * power01,
				Color.Lerp(accent, Color.White, 0.5f), 0.28f * grow);
		}
	}

	/// <summary>
	/// B-P beam: Circuit Rune. The beam paints a closed gold/white circuit at
	/// the cursor puddle (beam - side node - side node - beam). Enemies crossing
	/// a segment are zapped once; after 0.8 s the closed circuit discharges.
	/// </summary>
	public sealed class CircuitRuneAbility : OrbitalConvergenceProfile
	{
		public override float BeamDamageFraction => 0.12f;
		public override float BeamRange => 780f;
		public override float BeamThickness => 18f;
		public override bool BeamHasEndPuddle => true;

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			base.OnChargeComplete(ctx);
			if (Main.dedServ)
				return;
			Color gold = new Color(255, 226, 140);
			YoyoParticles.SpawnRing(ctx.Center, 6f, 88f * ctx.TierSize, 20, gold, 2.2f);
			YoyoParticles.SpawnBloom(ctx.Center, 8f, 46f * ctx.TierSize, 18, gold, 0.4f);
		}

		public override void OnBeamEndTick(Projectile beam, in MagicalAbilityContext ctx,
			Vector2 end, float liveLength)
		{
			if (beam.localAI == null || beam.localAI.Length == 0 || beam.localAI[0] != 0f
				|| ctx.Owner == null || ctx.Owner.whoAmI != Main.myPlayer)
				return;
			beam.localAI[0] = 1f;
			int type = ModContent.ProjectileType<MagicalBeamAnchorProj>();
			MagicalReleaseCommon.Spawn(ctx.Owner, end, Vector2.Zero, type,
				beam.damage, MagicalBeamAnchorProj.ModeCircuit, ctx.Power01, ctx.SourceProjType);
		}

		public override void DrawBeam(Projectile beam, Vector2 origin, Vector2 end, Color accent,
			float power01, float grow)
		{
			Vector2 delta = end - origin;
			float len = delta.Length();
			if (len < 8f)
				return;
			float width = (4f + 7f * power01) * (1f + 0.03f * (float)Math.Sin(Main.GameUpdateCount * 0.4f + beam.whoAmI));
			Color gold = new Color(255, 226, 140);
			Color core = Color.Lerp(gold, Color.White, 0.55f);
			MiniYoyoFx.DrawBeamV2(origin, end, core, gold, width, grow, 1.2f);
			MiniYoyoFx.DrawBeamHeatShimmer(origin, end, gold, width, grow);

			// Travelling circuit pulses: small rectangular nodes are avoided;
			// these are soft round beads with a thin rune ring around each.
			Vector2 dir = delta / len;
			for (int i = 0; i < 5; i++) {
				float t = (i / 5f + Main.GameUpdateCount * 0.018f) % 1f;
				Vector2 p = origin + dir * (len * t);
				MiniYoyoFx.DrawGlowAdditive(p, 10f + 6f * power01, core, 0.22f * grow);
				MiniYoyoFx.DrawDashedRing(p, 7f + 2f * power01, 1.4f, gold, 5,
					Main.GameUpdateCount * 0.05f + i);
			}
		}

		public override void DrawBeamEnd(Projectile beam, Vector2 end, Color accent,
			float power01, float grow)
		{
			MiniYoyoFx.DrawGlowAdditive(end, 40f + 14f * power01,
				new Color(255, 226, 140), 0.24f * grow);
		}
	}
}
