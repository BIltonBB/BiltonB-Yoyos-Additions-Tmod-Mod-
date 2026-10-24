using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles.Tech;
using BiltonsYoyoMod.Content.Systems;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// Immutable per-frame snapshot handed to ability hooks. Value type: built on
	/// the stack by the controller/beam, so hooks never allocate.
	/// </summary>
	public readonly struct MagicalAbilityContext
	{
		public readonly Player Owner;
		public readonly Projectile Controller;
		public readonly Vector2 Center;
		public readonly Vector2 Aim;
		public readonly int OrbitCount;
		public readonly int OrbitCap;
		public readonly int SourceProjType;
		public readonly float Charge01;
		public readonly float Power01;
		/// <summary>Progression tier of the source yoyo, 0 (starter) .. 1 (endgame).</summary>
		public readonly float Tier01;
		public readonly Color Accent;

		public MagicalAbilityContext(Player owner, Projectile controller, Vector2 center, Vector2 aim,
			int orbitCount, int orbitCap, int sourceProjType, float charge01, float power01, Color accent)
			: this(owner, controller, center, aim, orbitCount, orbitCap, sourceProjType, charge01, power01, 0.5f, accent)
		{
		}

		public MagicalAbilityContext(Player owner, Projectile controller, Vector2 center, Vector2 aim,
			int orbitCount, int orbitCap, int sourceProjType, float charge01, float power01, float tier01,
			Color accent)
		{
			Owner = owner;
			Controller = controller;
			Center = center;
			Aim = aim;
			OrbitCount = orbitCount;
			OrbitCap = orbitCap;
			SourceProjType = sourceProjType;
			Charge01 = charge01;
			Power01 = power01;
			Tier01 = MathHelper.Clamp(tier01, 0f, 1f);
			Accent = accent;
		}

		/// <summary>Damage multiplier: an endgame yoyo's ability hits about 70%
		/// harder than a starter's before the yoyo's own damage is even counted.</summary>
		public float TierScale => 0.78f + 0.54f * Tier01;

		/// <summary>Visual/area multiplier: a touch of size growth with tier.</summary>
		public float TierSize => 0.85f + 0.35f * Tier01;
	}

	/// <summary>
	/// Base class for per-yoyo Magical Tech abilities. A yoyo's ability identity is
	/// keyed by its projectile type and registered in <see cref="MagicalAbilityRegistry"/>.
	/// Every member has a sensible default so a unique yoyo only overrides what it
	/// needs (charge time, damage cut, colours, VFX, sounds, beam behaviour).
	/// The shared Orbital Convergence look lives in <see cref="OrbitalConvergenceProfile"/>:
	/// it is the default, NOT a mandate - individual yoyos may replace it entirely.
	/// </summary>
	public class MagicalAbilityProfile
	{
		/// <summary>Channel duration before the release (per-yoyo).</summary>
		public virtual int ChargeTicks => MagicalOrbit.ChargeTicksDefault;

		/// <summary>
		/// Lockout after this ability's release ends. The controller enforces a
		/// hard floor of one second; individual profiles may be longer. Put the
		/// cooldown here, not on the input edge, so it applies equally to beams,
		/// self-driven releases, a manual cancel and a mana-starved collapse.
		/// </summary>
		public virtual int CooldownTicks => MagicalOrbit.AbilityCooldownDefault;

		/// <summary>Fraction of the orbit shells' damage the sustained attack deals.</summary>
		public virtual float BeamDamageFraction => MagicalOrbit.BeamDamageFraction;

		/// <summary>Ticks between repeated hits on the same target (local immunity).</summary>
		public virtual int BeamHitCooldown => MagicalOrbit.BeamHitCooldownDefault;

		/// <summary>Max reach of the release attack in pixels.</summary>
		public virtual float BeamRange => 900f;

		/// <summary>Collision thickness of the release attack.</summary>
		public virtual float BeamThickness => 18f;

		/// <summary>Accent colour; defaults to a sample of the source yoyo's sprite.</summary>
		public virtual Color AccentColor(int sourceProjType) => MagicalOrbit.AccentOf(sourceProjType);

		public virtual Color CoreColor(Color accent) => Color.Lerp(accent, Color.White, 0.6f);

		/// <summary>
		/// Charge signature: the small accent pieces that build around the shared
		/// core while the channel runs. Resolved from the table in
		/// <see cref="MagicalChargeMotifs"/> by class name, so a new ability only
		/// needs one table line.
		/// </summary>
		public virtual MagicalChargeMotif Motif => MagicalChargeMotifs.Resolve(GetType().Name);

		/// <summary>
		/// Human-readable ability name, floats over the charge core while the
		/// channel runs (world-space text, no chat spam) so every yoyo's ability
		/// is identifiable at a glance. Resolved from the assignment catalogue.
		/// </summary>
		public virtual string DisplayName => MagicalAbilityCatalog.NameOf(this);

		// ---- Release shape ----
		// Not every ability has to be a beam: a profile can set SpawnsBeam = false
		// and run its own release (burst, shield, portal, volley...) from
		// OnChargeComplete + the lifecycle hooks, using FiringDurationTicks as its
		// window. The controller, mana economy and cleanup stay shared.

		/// <summary>False when this ability's release is not a sustained beam.</summary>
		public virtual bool SpawnsBeam => true;

		/// <summary>Lifetime of a self-driven (non-beam) release. &lt;0 = until release/cancel.</summary>
		public virtual int FiringDurationTicks => -1;

		// ---- Mana economy (charged on the owning client) ----
		// Start cost is paid once when the charge forms (scales with the yoyo's
		// damage tier AND the player's max mana); upkeep is drained every second
		// the ability is held. Both are per-yoyo overridable.

		/// <summary>One-off mana paid when the charge starts.</summary>
		public virtual float ManaStartCost(float maxMana, float tier01) => MagicalOrbit.ManaStartCost(maxMana, tier01);

		/// <summary>Mana drained every second the ability stays active.</summary>
		public virtual float ManaUpkeepPerSecond(float maxMana, float tier01) => MagicalOrbit.ManaUpkeepPerSecond(maxMana, tier01);

		// ---- Lifecycle events (all sides; audio/particles should self-guard dedServ) ----

		public virtual void OnChargeStart(in MagicalAbilityContext ctx) { }
		public virtual void OnChargeTick(in MagicalAbilityContext ctx) { }
		public virtual void OnChargeComplete(in MagicalAbilityContext ctx) { }
		public virtual void OnFiringTick(in MagicalAbilityContext ctx) { }
		public virtual void OnAbilityEnd(in MagicalAbilityContext ctx, bool cancelled) { }
		public virtual void OnBeamSpawned(in MagicalAbilityContext ctx, Projectile beam) { }
		public virtual void OnBeamHit(NPC target, Projectile beam, in MagicalAbilityContext ctx) { }

		// ---- Beam end-puddle family ----
		// A profile with BeamHasEndPuddle = true is a B-P beam: the beam stops
		// where the cursor touches it and leaves a live puddle anchor there.
		// A profile that leaves this false is a B-N continuous beam (Lord's
		// Deathray): it ends at the cursor/range and has no puddle.

		/// <summary>True for a beam-with-end-puddle (B-P), false for a pure beam (B-N).</summary>
		public virtual bool BeamHasEndPuddle => false;

		/// <summary>Called every beam tick after the tip has been resolved. Spawn
		/// the anchor/effects here; use beam.localAI for per-beam state.</summary>
		public virtual void OnBeamEndTick(Projectile beam, in MagicalAbilityContext ctx,
			Vector2 end, float liveLength) { }

		/// <summary>Draw the end puddle itself (B-P only). Runs inside PreDraw.</summary>
		public virtual void DrawBeamEnd(Projectile beam, Vector2 end, Color accent,
			float power01, float grow) { }

		/// <summary>Called once when a B-P beam dies, so a lingering puddle can
		/// discharge or fade instead of vanishing with the beam.</summary>
		public virtual void OnBeamEndKill(Projectile beam, in MagicalAbilityContext ctx,
			Vector2 end) { }

		// ---- Drawing (called inside a live PreDraw batch) ----

		/// <summary>Central charge puddle/field. Charge01 grows it; Power01 is the
		/// live capability of the surviving orbits and must shrink it back down.</summary>
		public virtual void DrawChargeField(in MagicalAbilityContext ctx) { }

		/// <summary>Orbit-to-centre energy connections.</summary>
		public virtual void DrawConnections(in MagicalAbilityContext ctx, List<Vector2> orbitCenters) { }

		/// <summary>The release (beam or equivalent). Default: layered beam with heat shimmer.</summary>
		public virtual void DrawBeam(Projectile beam, Vector2 origin, Vector2 end, Color accent, float power01, float grow) { }
	}

	/// <summary>
	/// Default ability: Orbital Convergence. Orbiting yoyos pour energy into a
	/// growing core; at full charge a sustained beam fires from the core toward
	/// the cursor for up to 5 seconds. Everything scales with the surviving
	/// orbit count and the source yoyo's accent colour.
	/// </summary>
	public class OrbitalConvergenceProfile : MagicalAbilityProfile
	{
		public override void OnChargeStart(in MagicalAbilityContext ctx)
		{
			if (Main.dedServ)
				return;
			SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.55f, Pitch = 0.2f }, ctx.Center);
			MagicalFx.CastFlash(ctx.Center, ctx.Accent, 1f);
			MagicalFx.Scorch(ctx.Center, ctx.Accent, 0.9f);
		}

		public override void OnChargeTick(in MagicalAbilityContext ctx)
		{
			if (Main.dedServ)
				return;
			float charge = ctx.Charge01;

			// Pooled fluid: the core puddle thickens as the charge builds.
			int blobs = charge < 0.3f ? 1 : 2;
			for (int i = 0; i < blobs; i++) {
				Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
				Vector2 pos = ctx.Center + dir * Main.rand.NextFloat(4f, 30f);
				MetaballSystem.SpawnMetaball(pos, -dir * Main.rand.NextFloat(0.2f, 1.1f),
					0.85f + 1.5f * charge, 0.85f, 22 + (int)(34f * charge), ctx.Accent, noGravity: true);
			}

			// Real motes feed the funnel: pooled sparks (round and smooth) spiral
			// inward, so the motion is continuous instead of popping stamped rods.
			if (charge > 0.08f && Main.rand.NextBool(2)) {
				float ang = Main.rand.NextFloat(MathHelper.TwoPi);
				float radius = MagicalOrbit.OrbitRadiusConverged * Main.rand.NextFloat(0.78f, 1.28f);
				Vector2 pos = ctx.Center + ang.ToRotationVector2() * radius;
				Vector2 swirl = new Vector2(-MathF.Sin(ang), MathF.Cos(ang)) * (0.7f + 1.1f * charge);
				Vector2 inward = (ctx.Center - pos) * 0.045f;
				YoyoParticles.SpawnSpark(pos, inward + swirl, ctx.Accent, 0.85f, 22, 0.96f);
			}

			// Progress read: a soft bloom EMANATION every quarter charge, so the
			// field breathes out without stamping rings around the player.
			float step = 1f / Math.Max(1, ChargeTicks);
			for (int k = 1; k <= 3; k++) {
				float mark = k / 4f;
				if (charge >= mark && charge < mark + step)
					YoyoParticles.SpawnBloom(ctx.Center, 12f, (54f + 26f * k) * (0.7f + 0.3f * ctx.Power01),
						20, ctx.Accent, 0.38f);
			}

			// High charge: the core spits extra round sparks. No bolts, no
			// zigzags - stray lines are exactly what reads as noise.
			if (charge > 0.62f && Main.rand.NextBool(3)) {
				Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
				YoyoParticles.SpawnSpark(ctx.Center + dir * (6f + 10f * charge), dir * (1.6f + 1.4f * charge),
					ctx.Accent, 0.9f, 18, 0.95f);
			}
		}

		public override void OnChargeComplete(in MagicalAbilityContext ctx)
		{
			if (Main.dedServ)
				return;
			float scale = 0.55f + 0.45f * ctx.Power01;
			SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = 0.05f }, ctx.Center);
			SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = -0.35f }, ctx.Center);
			// Double shockwave: a fast snap ring plus a slower, wider bloom, so the
			// transition has weight even at a single surviving orbit. Sizes grew
			// so the release BEAT lands as an event, not a blink.
			YoyoParticles.SpawnRing(ctx.Center, 10f * scale, 118f * scale, 26, ctx.Accent, 3.6f);
			YoyoParticles.SpawnRing(ctx.Center, 4f, 182f * scale, 30, CoreColor(ctx.Accent), 2.4f);
			YoyoParticles.SpawnBloom(ctx.Center, 12f, 102f * scale, 26, ctx.Accent, 0.55f);
			YoyoParticles.SpawnBurst(ctx.Center, 5 + (int)(5f * ctx.Power01), 3.4f * scale, ctx.Accent, 0.9f, 20);
			MagicalFx.Fan(ShapeId.SplatSoft, ctx.Center, ctx.Accent, 5, 4.6f, 22, 0.6f * scale);
			MagicalFx.Fan(ShapeId.SmearCrescent, ctx.Center, CoreColor(ctx.Accent), 10, 6.5f * scale, 24, 0.34f);
			MagicalFx.Scorch(ctx.Center, ctx.Accent, 1.2f, crack: true);
			MiniYoyoFx.FlashFX(ctx.Center, Color.White, 1.25f);
			MagicalFx.ScreenPulse(ctx.Center, 0.2f + 0.15f * scale, ctx.Accent);
			MiniYoyoFx.Shake(4, 9, ctx.Center);
			// Shared release language: the cast arc + cursor glyph stamp, so the
			// sustained beam lands on the cursor with the same weight as every
			// release ability.
			MagicalCast.Kick(this, ctx);
		}

		public override void OnFiringTick(in MagicalAbilityContext ctx)
		{
			if (Main.dedServ)
				return;
			// The core keeps feeding while the release is active - fewer blobs,
			// brighter, so the puddle reads as "energised", not rebuilt.
			if (Main.rand.NextBool(2)) {
				Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
				Vector2 pos = ctx.Center + dir * Main.rand.NextFloat(2f, 18f);
				MetaballSystem.SpawnMetaball(pos, -dir * 0.6f,
					1.4f + 1.2f * ctx.Power01, 0.9f, 26, ctx.Accent, noGravity: true);
			}
			if (Main.rand.NextBool(4)) {
				Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
				YoyoParticles.SpawnSpark(ctx.Center + dir * 10f, dir * 2.2f, ctx.Accent, 0.8f, 18, 0.95f);
			}
			if (Main.rand.NextBool(6)) {
				MiniBossShapeBank.Spawn(ShapeId.SmearCrescent,
					ctx.Center + Main.rand.NextVector2Circular(26f, 26f),
					Main.rand.NextFloat(MathHelper.TwoPi), 0.22f, ctx.Accent * 0.5f, 18,
					Main.rand.NextVector2Circular(1.4f, 1.4f), 0.02f);
			}
		}

		public override void OnAbilityEnd(in MagicalAbilityContext ctx, bool cancelled)
		{
			if (Main.dedServ)
				return;
			float scale = 0.5f + 0.5f * ctx.Power01;
			YoyoParticles.SpawnRing(ctx.Center, 52f * scale, 6f, 18, ctx.Accent, 3f);
			YoyoParticles.SpawnBloom(ctx.Center, 6f, 34f * scale, 18, ctx.Accent, 0.4f);
			MagicalFx.Fan(ShapeId.SmearCrescent, ctx.Center, ctx.Accent, 6, 2.2f, 22, 0.3f);
			SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = -0.5f }, ctx.Center);
		}

		public override void OnBeamHit(NPC target, Projectile beam, in MagicalAbilityContext ctx)
		{
			if (Main.dedServ)
				return;
			YoyoParticles.SpawnBurst(target.Center, 3, 3f, ctx.Accent, 0.85f, 16);
			MiniBossShapeBank.Spawn(ShapeId.SplatSoft, target.Center, Main.rand.NextFloat(MathHelper.TwoPi),
				0.35f, ctx.Accent * 0.7f, 12);
			Lighting.AddLight(target.Center, ctx.Accent.ToVector3() * 0.6f);
		}

		// ---- Drawing ----

		public override void DrawChargeField(in MagicalAbilityContext ctx)
		{
			float charge = ctx.Charge01;
			float power = ctx.Power01;
			Color accent = ctx.Accent;
			Color core = CoreColor(accent);
			float size = 0.45f + 0.55f * power; // orbit count influence
			float coreR = (13f + 55f * MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, charge)) * size;
			float pulse = 1f + 0.07f * (float)Math.Sin(Main.GameUpdateCount * 0.24f);

			// One small warm heart under the metaball cluster. No wide rims, no
			// offset pools, no plume, no seal decks, no spiral arms - the core
			// IS the effect; everything else was noise around it.
			Lighting.AddLight(ctx.Center, accent.ToVector3() * (0.4f + 0.5f * charge) * (0.6f + 0.4f * power));
			MiniYoyoFx.DrawGlowAdditive(ctx.Center, coreR * 2.1f * pulse, accent, 0.08f + 0.12f * charge);
			MiniYoyoFx.DrawGlowAdditive(ctx.Center, coreR * 1.0f, Color.White, 0.05f + 0.10f * charge);

			// The ability's signature: a few soft motes hugging the core.
			DrawMotif(ctx, core, coreR, charge);

			// Fully loaded: a soft static flare - "ready", no crown.
			if (charge >= 0.999f)
				MagicalFx.StarFlare(ctx.Center, Color.Lerp(core, Color.White, 0.35f), 22f * pulse, 0f, 0.4f);

			// Name tag: fades in over the core so the ability is identifiable at
			// a glance (world-space text; nothing is printed to chat).
			if (charge > 0.3f) {
				string title = DisplayName;
				if (!string.IsNullOrEmpty(title)) {
					float textAlpha = MathHelper.Clamp((charge - 0.3f) / 0.35f, 0f, 1f) * 0.9f;
					Vector2 pos = ctx.Center - Main.screenPosition - new Vector2(0f, coreR * 1.9f + 52f);
					Terraria.Utils.DrawBorderString(Main.spriteBatch, title, pos,
						Color.Lerp(accent, Color.White, 0.4f) * textAlpha, 0.82f, 0.5f, 0.5f);
				}
			}
		}

		/// <summary>
		/// Per-ability charge signature: a handful of small SOFT accents (round
		/// motes, blobs, curls) hugging the core - never lines, arcs, bolts or
		/// spinning stamps. Identity at a glance is the name tag's job; this is
		/// just flavour, so it stays quiet.
		/// </summary>
		private void DrawMotif(in MagicalAbilityContext ctx, Color core, float coreR, float charge)
		{
			Color accent = ctx.Accent;
			float heat = charge;
			float tick = Main.GameUpdateCount;
			float spin = tick * 0.015f;
			float ringR = coreR * 1.55f;

			switch (Motif) {
				case MagicalChargeMotif.Blades:
					for (int i = 0; i < 4; i++) {
						float a = spin * 1.6f + i * MathHelper.PiOver2;
						Vector2 p = ctx.Center + a.ToRotationVector2() * (ringR + 10f * heat);
						MagicalFx.Shape(ShapeId.SmearCrescent, p, a + MathHelper.PiOver2,
							22f + 10f * heat, 15f + 6f * heat,
							Color.Lerp(accent, Color.White, 0.3f) * (0.16f + 0.16f * heat));
					}
					break;
				case MagicalChargeMotif.Frost:
					for (int i = 0; i < 6; i++) {
						float a = spin * 0.8f + i * MathHelper.Pi / 3f;
						Vector2 p = ctx.Center + a.ToRotationVector2() * ringR;
						MiniYoyoFx.DrawGlowAdditive(p, 14f + 8f * heat, Color.Lerp(accent, Color.White, 0.55f),
							0.14f + 0.12f * heat);
						MagicalFx.Shape(ShapeId.SmearCrescent, p, a, 16f + 8f * heat, 9f,
							Color.Lerp(accent, Color.White, 0.4f) * (0.12f + 0.10f * heat));
					}
					break;
				case MagicalChargeMotif.Flame:
					for (int i = 0; i < 4; i++) {
						float f = (tick * 0.013f + i * 0.25f) % 1f;
						Vector2 p = ctx.Center + new Vector2((i - 1.5f) * 12f, -coreR * 0.6f - f * 56f);
						float fade = (float)Math.Sin(f * Math.PI);
						MagicalFx.Shape(ShapeId.EmberCurl, p, i * 1.3f + tick * 0.02f,
							18f + 10f * fade, 13f + 7f * fade,
							accent * (0.20f * fade * (0.5f + 0.5f * heat)));
					}
					break;
				case MagicalChargeMotif.Storm:
					for (int i = 0; i < 5; i++) {
						float a = spin * 1.9f + i * MathHelper.TwoPi / 5f;
						float flick = 0.5f + 0.5f * (float)Math.Sin(tick * 0.5f + i * 2.3f);
						Vector2 p = ctx.Center + a.ToRotationVector2() * (ringR + 6f);
						MiniYoyoFx.DrawGlowAdditive(p, (10f + 10f * flick) * (1f + 0.5f * heat),
							Color.Lerp(accent, Color.White, 0.45f), (0.14f + 0.12f * heat) * flick);
					}
					break;
				case MagicalChargeMotif.Void:
					MiniYoyoFx.DrawSolidEllipse(ctx.Center, coreR * 1.25f, coreR * 1.25f, 0f,
						new Color(10, 6, 18), 0.30f);
					for (int i = 0; i < 3; i++) {
						float a = -spin * 1.2f + i * MathHelper.TwoPi / 3f;
						Vector2 p = ctx.Center + a.ToRotationVector2() * (ringR + 12f);
						MiniYoyoFx.DrawGlowAdditive(p, 20f + 10f * heat, accent, 0.10f + 0.10f * heat);
					}
					break;
				case MagicalChargeMotif.Holy:
					for (int i = 0; i < 8; i++) {
						float a = spin * 0.6f + i * MathHelper.Pi / 4f;
						float tw = 0.5f + 0.5f * (float)Math.Sin(tick * 0.07f + i * 1.1f);
						Vector2 p = ctx.Center + a.ToRotationVector2() * (ringR + 4f * tw);
						MiniYoyoFx.DrawGlowAdditive(p, 12f + 8f * tw, Color.Lerp(accent, Color.White, 0.55f),
							0.14f + 0.12f * heat);
					}
					break;
				case MagicalChargeMotif.Thorn:
					for (int i = 0; i < 5; i++) {
						float a = spin * 0.7f + i * MathHelper.TwoPi / 5f;
						Vector2 p = ctx.Center + a.ToRotationVector2() * (ringR + 6f);
						MagicalFx.Shape(ShapeId.SmearHook, p, a + 1.6f, 20f, 14f,
							Color.Lerp(accent, Color.White, 0.2f) * (0.16f + 0.14f * heat));
					}
					break;
				case MagicalChargeMotif.Chrono:
					for (int i = 0; i < 12; i++) {
						float a = spin * 0.6f + i * MathHelper.TwoPi / 12f;
						bool major = i % 3 == 0;
						Vector2 p = ctx.Center + a.ToRotationVector2() * (ringR + (major ? 7f : 4f));
						MiniYoyoFx.DrawGlowAdditive(p, major ? 11f : 7f,
							Color.Lerp(accent, Color.White, major ? 0.4f : 0.15f), 0.12f + 0.10f * heat);
					}
					break;
				case MagicalChargeMotif.Blood:
					for (int i = 0; i < 4; i++) {
						float f = (tick * 0.017f + i * 0.25f) % 1f;
						float a = spin * 0.5f + i * 1.9f;
						Vector2 p = ctx.Center + a.ToRotationVector2() * (coreR * MathHelper.Lerp(1.9f, 0.9f, f));
						float fade = (float)Math.Sin(f * Math.PI);
						MagicalFx.Shape(ShapeId.SplatSoft, p, a + 0.7f, 13f + 7f * fade, 13f + 7f * fade,
							accent * (0.24f * fade * (0.5f + 0.5f * heat)));
					}
					break;
				case MagicalChargeMotif.Star:
					for (int i = 0; i < 5; i++) {
						float a = spin * 0.8f + i * MathHelper.TwoPi / 5f + 0.4f;
						float tw = 0.5f + 0.5f * (float)Math.Sin(tick * 0.05f + i * 1.7f);
						Vector2 p = ctx.Center + a.ToRotationVector2() * (ringR * (1.25f - 0.25f * tw));
						MiniYoyoFx.DrawGlowAdditive(p, (10f + 12f * tw) * (1f + 0.4f * heat),
							Color.Lerp(accent, Color.White, 0.5f), (0.14f + 0.12f * heat) * (0.4f + 0.6f * tw));
					}
					break;
				default: // Arcane
					for (int i = 0; i < 3; i++) {
						float a = spin * 1.1f + i * MathHelper.TwoPi / 3f;
						Vector2 p = ctx.Center + a.ToRotationVector2() * (ringR + 8f * heat);
						MiniYoyoFx.DrawGlowAdditive(p, 18f + 10f * heat, Color.Lerp(accent, Color.White, 0.3f),
							0.14f + 0.12f * heat);
					}
					break;
			}
		}

		// Scratch buffer for connection ribbons (paths are drawn one at a time,
		// so a single static array serves every link with zero allocation).
		private static readonly Vector2[] linkPts = new Vector2[32];

		public override void DrawConnections(in MagicalAbilityContext ctx, List<Vector2> orbitCenters)
		{
			if (orbitCenters == null || orbitCenters.Count == 0)
				return;
			float charge = ctx.Charge01;
			float intensity = (0.3f + 0.7f * charge) * (0.55f + 0.45f * ctx.Power01);
			if (intensity <= 0.02f)
				return;
			Color accent = ctx.Accent;
			Color core = CoreColor(accent);
			float flow = Main.GameUpdateCount * 0.02f;
			// One path resolution for every link: 22 samples along a straight run
			// is visually exact, and DrawPathBand welds the joints seamlessly.
			const int segs = 22;

			for (int i = 0; i < orbitCenters.Count; i++) {
				Vector2 from = orbitCenters[i];
				Vector2 toCenter = ctx.Center - from;
				float len = toCenter.Length();
				if (len < 30f)
					continue;
				Vector2 dir = toCenter / len;

				// Endpoints sit just outside both bodies so the link grows out of
				// the guard and pours INTO the core without overdrawing either.
				Vector2 a = from + dir * 13f;
				Vector2 b = ctx.Center - dir * (13f + 8f * charge);
				float w = (4.6f + 4.4f * charge) * (0.6f + 0.4f * ctx.Power01);

				// ONE seamless ribbon per link: a densely sampled straight run
				// through DrawPathBand (shared joint widths, constant alpha) - no
				// segment steps, no seams, no dashes, no bowing.
				for (int k = 0; k <= segs; k++)
					linkPts[k] = Vector2.Lerp(a, b, k / (float)segs);
				MiniYoyoFx.DrawPathBand(linkPts, segs + 1, w, w * 0.32f, accent, 0.30f * intensity);
				// Hot inner filament, sample-identical to the outer band so the two
				// can never misalign.
				MiniYoyoFx.DrawPathBand(linkPts, segs + 1, w * 0.44f, w * 0.16f, core, 0.20f * intensity);

				// Flow: one soft head with a smooth tail sliding along the SAME
				// line, so the link reads as energy in motion (no stamped art).
				float t2 = (flow + i * 0.31f) % 1f;
				float t1 = MathHelper.Clamp(t2 - 0.30f, 0f, 1f);
				Vector2 p0 = Vector2.Lerp(a, b, t1);
				Vector2 p1 = Vector2.Lerp(a, b, t2);
				float move = (p1 - p0).Length();
				if (move > 0.01f) {
					MiniYoyoFx.DrawSoftBar((p0 + p1) * 0.5f, (p1 - p0).ToRotation(), move + 14f, w * 1.15f,
						Color.Lerp(accent, core, 0.35f), 0.5f * intensity);
					MiniYoyoFx.DrawGlowAdditive(p1, 10f + 12f * charge, core, 0.3f * intensity);
				}
				// Soft muzzle where the link leaves the guard.
				MiniYoyoFx.DrawGlowAdditive(a, 18f + 14f * charge, accent, 0.22f * intensity);
			}
		}

		public override void DrawBeam(Projectile beam, Vector2 origin, Vector2 end, Color accent, float power01, float grow)
		{
			float width = (4f + 9f * power01) * (1f + 0.05f * (float)Math.Sin(Main.GameUpdateCount * 0.6f + beam.whoAmI));
			Color core = CoreColor(accent);
			Vector2 dir = end - origin;
			float len = dir.Length();
			if (len < 8f)
				return;
			dir /= len;
			Vector2 side = new(-dir.Y, dir.X);

			MiniYoyoFx.DrawBeamV2(origin, end, core, accent, width, grow, 1.5f);
			MiniYoyoFx.DrawBeamHeatShimmer(origin, end, accent, width, grow);

			// Helix: two strands twisting around the ray so the beam has volume
			// and a visible direction of travel rather than being a flat bar.
			float live = len * grow;
			for (int s = 0; s < 2; s++) {
				for (int k = 0; k < 7; k++) {
					float t = (k / 7f + Main.GameUpdateCount * 0.035f) % 1f;
					if (t * live > live)
						continue;
					Vector2 axis = origin + dir * (t * live);
					float phase = t * 9f + (s == 0 ? 0f : MathHelper.Pi) + Main.GameUpdateCount * 0.09f;
					Vector2 p = axis + side * ((float)Math.Sin(phase) * width * 1.15f);
					MiniYoyoFx.DrawGlowAdditive(p, width * (2.1f + 0.5f * (float)Math.Cos(phase)),
						core, (0.16f + 0.16f * power01) * grow);
				}
			}

			// Origin: lens flare + a rune crown where the core spits the ray.
			float flarePulse = 1f + 0.09f * (float)Math.Sin(Main.GameUpdateCount * 0.45f);
			MiniYoyoFx.DrawGlowAdditive(origin, (44f + 58f * power01) * flarePulse, accent, 0.3f + 0.2f * grow);
			MagicalFx.Rune(origin, (20f + 26f * power01) * (0.8f + 0.2f * grow),
				0.6f, (0.26f + 0.16f * power01) * grow, core);
			MagicalFx.StarFlare(origin, Color.Lerp(core, Color.White, 0.4f), 26f + 20f * flarePulse,
				0.4f, 0.35f * grow);

			// Tip: contact bloom plus an art splash that pulses (stronger where
			// the ray stopped short, i.e. on a wall).
			float stopT = MathHelper.Clamp(live / 900f, 0f, 1f);
			float pulseT = (Main.GameUpdateCount % 40) / 40f;
			float spot = 0.16f + 0.16f * (1f - stopT);
			MiniYoyoFx.DrawGlowAdditive(end, 24f + 40f * power01, core, 0.3f * grow);
			MagicalFx.Splat(end, 0f, 50f + 46f * pulseT, core * (spot * grow * 1.6f));
			MagicalFx.Shape(ShapeId.RingCracked, end, Main.GameUpdateCount * 0.03f,
				30f + 66f * pulseT, 30f + 66f * pulseT,
				Color.Lerp(core, Color.White, 0.4f) * (spot * grow));
		}
	}

	/// <summary>
	/// Ability registry, keyed by the source yoyo's projectile type. Anything not
	/// registered uses the shared Orbital Convergence default, so new yoyos work
	/// immediately and unique abilities can be dropped in without touching the
	/// controller, beam or input code.
	/// </summary>
	public static class MagicalAbilityRegistry
	{
		private static readonly Dictionary<int, MagicalAbilityProfile> bySource = new();

		public static readonly MagicalAbilityProfile Default = new OrbitalConvergenceProfile();

		public static void Register(int sourceProjType, MagicalAbilityProfile profile)
		{
			if (sourceProjType > ProjectileID.None && profile != null)
				bySource[sourceProjType] = profile;
		}

		public static bool HasCustom(int sourceProjType)
		{
			return sourceProjType > ProjectileID.None && bySource.ContainsKey(sourceProjType);
		}

		public static MagicalAbilityProfile Get(int sourceProjType)
		{
			if (sourceProjType > ProjectileID.None && bySource.TryGetValue(sourceProjType, out MagicalAbilityProfile profile))
				return profile;
			return Default;
		}
	}
}
