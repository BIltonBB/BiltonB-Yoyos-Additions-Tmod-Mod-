using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Tech;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Instant beam. It is spawned AT its origin with a velocity equal to the
	/// full offset to the target, so on the first update it lands exactly on the
	/// target; the first AI tick then rewinds to recover both ends, zeroes the
	/// velocity and deals one line hit. Lives just long enough to draw, then
	/// fades. Used by Vertex Well (micro-bolts) and Pulse Cascade.
	///
	/// ai[0]: style (0 lance, 1 micro). ai[1]: power01. ai[2]: source.
	/// </summary>
	public class MagicalPulseBeamProj : ModProjectile
	{
		private const int LifeTicks = 10;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private Vector2 origin;
		private Vector2 end;
		private bool fired;
		private Color accent = new(150, 200, 255);

		private int Style => (int)Projectile.ai[0];
		private float Age => LifeTicks - Projectile.timeLeft;

		private Vector2 spawnPoint;
		private Vector2 plannedOffset;

		public override void OnSpawn(IEntitySource source)
		{
			// Capture both ends, then pin: the line is origin -> origin+offset
			// no matter whether the engine applied the spawn velocity yet.
			spawnPoint = Projectile.Center;
			plannedOffset = Projectile.velocity;
			Projectile.velocity = Vector2.Zero;
		}

		public override void SetDefaults()
		{
			Projectile.width = 8;
			Projectile.height = 8;
			Projectile.friendly = false; // the line hit carries the damage
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = LifeTicks;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);

			if (!fired) {
				fired = true;
				origin = spawnPoint;
				end = spawnPoint + plannedOffset;
				Projectile.Center = origin;

				if (MagicalReleaseCommon.CanMoveNpcs) {
					MagicalReleaseCommon.LineHit(OwnerOf(), origin, end,
						Style == 1 ? 11f : 15f, Projectile.damage, 1.8f);
				}
				if (!Main.dedServ) {
					MiniYoyoFx.FlashFX(origin, Color.White, 0.45f);
					MiniYoyoFx.FlashFX(end, Color.White, 0.7f);
					MagicalFx.CastFlash(end, accent, 0.5f);
					YoyoParticles.SpawnRing(end, 6f, Style == 1 ? 34f : 48f, 12, accent, 2.4f);
					MiniYoyoFx.Shake(Style == 1 ? 1 : 2, 4, end);
					if (Style == 0) {
						SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.42f, Pitch = 0.1f }, end);
						MagicalFx.Fan(ShapeId.SplatStar, end, Color.White, 5, 4.2f, 18, 0.8f);
					}
				}
			}
			Lighting.AddLight(end, accent.ToVector3() * 0.85f);
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t = 1f - MathHelper.Clamp(Age / LifeTicks, 0f, 1f);
			if (t <= 0f)
				return false;
			Color hot = Color.Lerp(accent, Color.White, 0.65f);
			float width = Style == 1 ? 5.5f : 9f;
			// Lightning stroke first (thin, jagged), then a hot bar core, then
			// the two end nodes: reads as a discharge, not a beam sprite.
			MiniYoyoFx.DrawLightning(origin, end, width * (0.35f + 0.65f * t), accent, 0.9f * t,
				Style == 1 ? 0.5f : 0.12f, 3, (int)(Projectile.whoAmI * 31), Style == 1);
			Vector2 mid = (origin + end) * 0.5f;
			float rot = (end - origin).ToRotation();
			MiniYoyoFx.DrawSoftBar(mid, rot, Vector2.Distance(origin, end),
				width * 0.8f * (0.3f + 0.7f * t), hot, 0.85f * t);
			MiniYoyoFx.DrawGlowAdditive(end, (Style == 1 ? 46f : 70f) * (0.5f + 0.5f * t), hot, 0.5f * t);
			return false;
		}
	}

	/// <summary>
	/// Rapid bolt: the fast straight round a Rapid Well spits. Pure velocity,
	/// no steering; the trail does the talking.
	/// </summary>
	public class MagicalRapidBoltProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private Color accent = new(150, 220, 255);
		private readonly List<Vector2> trail = new();

		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 1;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 8;
			Projectile.timeLeft = 80;
		}

		public override void AI()
		{
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			Projectile.rotation = Projectile.velocity.ToRotation();
			trail.Insert(0, Projectile.Center);
			if (trail.Count > 10)
				trail.RemoveAt(trail.Count - 1);
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.6f);
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			MagicalFx.CastFlash(Projectile.Center, accent, 0.35f);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 2, 1.4f, 12, 0.45f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			Color hot = Color.Lerp(accent, Color.White, 0.55f);
			// Stretched comet: the wisp grows out of the trail, the head sits on
			// the tip. No spinning sprite, so it reads as speed.
			Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			MagicalFx.Wisp(Projectile.Center - back * 22f, back.ToRotation(), 54f, 12f, accent * 0.55f);
			MagicalFx.StarFlare(Projectile.Center, hot, 26f, Projectile.rotation, 0.85f);
			MagicalFx.Bloom(Projectile.Center, 26f, accent, 0.35f);
			return false;
		}
	}

	/// <summary>
	/// Weaver: a ribbon that writes a parametrised path through the air instead
	/// of flying straight. The original six shapes (sine, spiral, saw, triangle,
	/// damped, figure-eight) are joined by four math-curve shapes: interference,
	/// astroid, ranunculoid and a 3:2 Lissajous knot. Each shape has two phases
	/// so a cast pair braids around the same axis and crosses at the middle of
	/// every wave.
	///
	/// ai[0]: variant. ai[1]: power01. ai[2]: source.
	/// </summary>
	public class MagicalWeaverProj : ModProjectile
	{
		public const float ModeSineA = 0f;
		public const float ModeSineB = 1f;
		public const float ModeSpiralA = 2f;
		public const float ModeSpiralB = 3f;
		public const float ModeSawA = 4f;
		public const float ModeSawB = 5f;
		public const float ModeTriangleA = 6f;
		public const float ModeTriangleB = 7f;
		public const float ModeDampedA = 8f;
		public const float ModeDampedB = 9f;
		public const float ModeFigureA = 10f;
		public const float ModeFigureB = 11f;
		// Math-curve pass (2026-10-21). Appended, so old variant ids keep
		// their meaning for existing saves and assignment files.
		public const float ModeInterferenceA = 12f;
		public const float ModeInterferenceB = 13f;
		public const float ModeAstroidA = 14f;
		public const float ModeAstroidB = 15f;
		public const float ModeLimaconA = 16f;
		public const float ModeLimaconB = 17f;
		public const float ModeRanunculoidA = 18f;
		public const float ModeRanunculoidB = 19f;
		public const float ModeKnotA = 20f;
		public const float ModeKnotB = 21f;
		public const float ModeSpirographA = 22f;
		public const float ModeSpirographB = 23f;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private readonly List<Vector2> trail = new();
		private readonly List<Vector2> ghostTrailA = new();
		private readonly List<Vector2> ghostTrailB = new();
		private readonly HashSet<int> struck = new();
		private static readonly Vector2[] drawPts = new Vector2[26];
		private static readonly Vector2[] ghostPtsA = new Vector2[26];
		private static readonly Vector2[] ghostPtsB = new Vector2[26];

		private Vector2 origin;
		private Vector2 dir;
		private int age;
		private float power;
		private Color accent = new(180, 220, 255);
		private int crossFlash;
		private int styleFlash;
		private float knotDepth = 1f;

		private int Mode => (int)Projectile.ai[0];
		private bool Second => (Mode & 1) == 1;
		private int Family => Mode / 2;

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 14;
			Projectile.timeLeft = 140;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			if (Projectile.ai == null || Projectile.ai.Length < 3) {
				Projectile.Kill();
				return;
			}
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);

			if (age == 0) {
				// Rewind the landing step so the path starts at the caster.
				origin = Projectile.Center - Projectile.velocity;
				dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			}
			age++;

			float t = age * 7f;
			float lateral;
			float rise;
			knotDepth = 1f;
			float ghostALateral = 0f;
			float ghostARise = 0f;
			float ghostBLateral = 0f;
			float ghostBRise = 0f;
			bool showGhosts = false;
			bool curveFlash = false;

			switch (Family) {
				case 1: { // spiral
					float radius = 32f + 18f * power;
					float phase = t * 0.05f + (Second ? MathHelper.Pi : 0f);
					lateral = (float)Math.Sin(phase) * radius;
					rise = (float)Math.Cos(phase) * radius * 0.62f;
					break;
				}
				case 2: { // sawtooth: hard ramp, snap back, mirrored partner
					float amp = 46f + 28f * power;
					float period = 170f;
					float u = (t / period + (Second ? 0.5f : 0f)) % 1f;
					lateral = (u * 2f - 1f) * amp;
					rise = (float)Math.Sin(t * 0.022f) * 8f;
					break;
				}
				case 3: { // triangle: linear up/down with no dwell at the peaks
					float amp = 52f + 30f * power;
					float period = 200f;
					float u = (t / period + (Second ? 0.5f : 0f)) % 1f;
					lateral = (u < 0.5f ? (u * 4f - 1f) : (3f - u * 4f)) * amp;
					rise = (float)Math.Sin(t * 0.026f) * 9f;
					break;
				}
				case 4: { // damped: a hard first swing that bleeds out, then settles
					float amp = 64f + 34f * power;
					float u = t / 240f;
					float decay = (float)Math.Exp(-u * 0.55f);
					lateral = (float)Math.Sin(u * MathHelper.TwoPi * 2.1f + (Second ? MathHelper.Pi : 0f)) * amp * decay;
					rise = (float)Math.Sin(t * 0.028f) * 12f * decay;
					break;
				}
				case 5: { // figure-eight: 1:2 Lissajous, the pair braids a knot
					float rx = 46f + 26f * power;
					float ry = 28f + 16f * power;
					float phase = t * 0.042f + (Second ? MathHelper.PiOver2 : 0f);
					lateral = (float)Math.Sin(phase) * rx;
					rise = (float)Math.Sin(phase * 2f) * ry;
					break;
				}
				case 6: { // interference / beat: two ribbons, one bright seam
					float amp = 44f + 26f * power;
					float phase = Second ? MathHelper.Pi : 0f;
					float a = (float)Math.Sin(t * 0.052f + phase) * amp * 0.62f;
					float b = (float)Math.Sin(t * 0.073f + phase * 1.6f + age * 0.035f) * amp * 0.38f;
					lateral = a + b;
					rise = (float)Math.Sin(t * 0.031f + phase) * 10f;
					ghostALateral = a;
					ghostARise = rise + 3f;
					ghostBLateral = b;
					ghostBRise = rise - 3f;
					showGhosts = true;
					if (Math.Abs(a - b) < 5f)
						curveFlash = true;
					break;
				}
				case 7: { // astroid: 4-cusped star loop
					float radius = 32f + 18f * power;
					float u = t * 0.048f + (Second ? MathHelper.PiOver2 : 0f);
					float c = (float)Math.Cos(u);
					float s = (float)Math.Sin(u);
					lateral = c * c * c * radius;
					rise = s * s * s * radius;
					if (Math.Abs(c) < 0.035f || Math.Abs(s) < 0.035f)
						curveFlash = true;
					break;
				}
				case 9: { // ranunculoid: five-cusped epicycloid (the n=5 flower)
					float radius = 34f + 18f * power;
					float u = t * 0.044f + (Second ? MathHelper.PiOver2 : 0f);
					float ct = (float)Math.Cos(u);
					float st = (float)Math.Sin(u);
					float c6 = (float)Math.Cos(6f * u);
					float s6 = (float)Math.Sin(6f * u);
					lateral = (6f * ct - c6) / 7f * radius;
					rise = (6f * st - s6) / 7f * radius;
					if (Math.Abs((float)Math.Sin(5f * u)) < 0.045f)
						curveFlash = true;
					break;
				}
				case 10: { // Lissajous knot: 3:2 projection with a 5th depth axis
					float rx = 46f + 22f * power;
					float ry = 30f + 14f * power;
					float u = t * 0.039f + (Second ? MathHelper.PiOver2 : 0f);
					knotDepth = (float)Math.Cos(5f * u);
					lateral = (float)Math.Sin(3f * u + 0.4f) * rx + knotDepth * 3f;
					rise = (float)Math.Sin(2f * u + 0.9f) * ry;
					if (Math.Abs(knotDepth) > 0.95f)
						curveFlash = true;
					break;
				}
				default: { // sine
					float amp = 54f + 32f * power;
					lateral = (float)Math.Sin(t * 0.045f + (Second ? MathHelper.Pi : 0f)) * amp;
					rise = (float)Math.Sin(t * 0.03f) * 11f;
					break;
				}
			}

			Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
			Projectile.Center = origin + dir * t + perp * lateral - Vector2.UnitY * rise;
			Projectile.rotation = dir.ToRotation();
			trail.Insert(0, Projectile.Center);
			if (trail.Count > 24)
				trail.RemoveAt(trail.Count - 1);

			if (showGhosts) {
				Vector2 ga = origin + dir * t + perp * ghostALateral - Vector2.UnitY * ghostARise;
				Vector2 gb = origin + dir * t + perp * ghostBLateral - Vector2.UnitY * ghostBRise;
				ghostTrailA.Insert(0, ga);
				ghostTrailB.Insert(0, gb);
				if (ghostTrailA.Count > 24)
					ghostTrailA.RemoveAt(ghostTrailA.Count - 1);
				if (ghostTrailB.Count > 24)
					ghostTrailB.RemoveAt(ghostTrailB.Count - 1);
			}
			else {
				ghostTrailA.Clear();
				ghostTrailB.Clear();
			}

			// The braid crossing is the payoff frame: flash when the ribbon cuts
			// back through the axis. The new math styles use their own accent
			// events (seam, cusp, petal tip, knot depth) instead.
			if (Family <= 5 && age > 5 && Math.Abs(lateral) < 6f && crossFlash <= 0) {
				crossFlash = 8;
				if (!Main.dedServ) {
					MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.4f);
					YoyoParticles.SpawnRing(Projectile.Center, 4f, 30f, 10, accent, 1.8f);
				}
			}
			if (crossFlash > 0)
				crossFlash--;

			if (curveFlash && styleFlash <= 0) {
				styleFlash = Family == 10 ? 6 : 8;
				if (!Main.dedServ) {
					if (Family == 6) {
						YoyoParticles.SpawnRing(Projectile.Center, 3f, 36f, 12, accent, 1.7f);
						MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.28f);
					}
					else if (Family == 7) {
						MagicalFx.QueueStarFlare(Projectile.Center, Color.White, 34f, 0f, 0.35f);
						YoyoParticles.SpawnSpark(Projectile.Center, -dir * 0.8f, accent, 0.6f, 10, 0.94f);
					}
					else if (Family == 9) {
						MagicalFx.QueueStarFlare(Projectile.Center, Color.White, 38f, 0f, 0.4f);
						YoyoParticles.SpawnRing(Projectile.Center, 3f, 26f, 8, accent, 1.5f);
					}
					else {
						MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.24f);
						YoyoParticles.SpawnBloom(Projectile.Center, 2f, 26f, 8, accent, 0.35f);
					}
				}
			}
			if (styleFlash > 0)
				styleFlash--;

			if (!Main.dedServ && age % 3 == 0)
				YoyoParticles.SpawnSpark(Projectile.Center, -dir * 1.2f, accent, 0.5f, 12, 0.95f);

			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.7f);
			if (t > 640f)
				Projectile.Kill();
		}

		public override bool? CanHitNPC(NPC target)
			=> struck.Contains(target.whoAmI) ? false : null;

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (!struck.Add(target.whoAmI))
				return;
			if (Main.dedServ)
				return;

			Color hot = Color.Lerp(accent, Color.White, 0.55f);
			MagicalFx.QueueStarFlare(target.Center, hot,
				Family == 9 ? 42f : (Family == 7 ? 34f : 26f), 0f, 0.35f);
			YoyoParticles.SpawnSpark(target.Center, -dir * 1.4f, accent, 0.7f, 12, 0.95f);
			if (Family == 6) {
				// The seam bite is the signature hit for interference.
				MagicalFx.QueueShape(ShapeId.NeedleLong, target.Center, 0f,
					14f, 38f, Color.White * 0.55f);
			}
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			MagicalFx.Pop(Projectile.Center, 44f, accent, 0.35f);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 3, 2.2f, 16, 0.5f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			Color hot = Color.Lerp(accent, Color.White, 0.55f);

			// Interference keeps its two source ribbons visible at low alpha;
			// the bright primary is their sum, so the seam is readable without
			// adding a second damage path.
			if (Family == 6 && ghostTrailA.Count >= 2) {
				int ca = 0;
				for (int i = 0; i < ghostTrailA.Count && ca < ghostPtsA.Length; i++)
					ghostPtsA[ca++] = ghostTrailA[i];
				int cb = 0;
				for (int i = 0; i < ghostTrailB.Count && cb < ghostPtsB.Length; i++)
					ghostPtsB[cb++] = ghostTrailB[i];
				if (ca >= 2) {
					MiniYoyoFx.DrawPathBand(ghostPtsA, ca, 3f, 12f, accent * 0.16f, 0.45f);
					MiniYoyoFx.DrawPathBand(ghostPtsA, ca, 1.2f, 4f, hot * 0.28f, 0.5f);
				}
				if (cb >= 2) {
					MiniYoyoFx.DrawPathBand(ghostPtsB, cb, 3f, 12f, accent * 0.13f, 0.4f);
					MiniYoyoFx.DrawPathBand(ghostPtsB, cb, 1.2f, 4f, hot * 0.24f, 0.45f);
				}
			}

			int count = 0;
			drawPts[count++] = Projectile.Center;
			for (int i = 0; i < trail.Count && count < drawPts.Length; i++)
				drawPts[count++] = trail[i];

			if (count >= 2) {
				// A woven ribbon: soft outer band, saturated body, white filament.
				// Closed curves get a slightly wider outer bloom so their cusps
				// and inner loops separate from the background.
				bool closed = Family == 7 || Family == 9;
				float outer = closed ? 20f : 16f;
				MiniYoyoFx.DrawPathBand(drawPts, count, 4f, outer, accent * 0.35f, 0.5f);
				MiniYoyoFx.DrawPathBand(drawPts, count, 2.5f, closed ? 10f : 9f, accent, 0.85f);
				MiniYoyoFx.DrawPathBand(drawPts, count, 1f, 3f, hot, 0.8f);
			}

			float spin = Main.GameUpdateCount * 0.12f + (Second ? 1.1f : 0f);
			float depth = Family == 10 ? 0.82f + 0.18f * knotDepth : 1f;
			float headW = 30f * depth;
			float headH = 14f * depth;
			Color headColor = Family == 10 ? Color.Lerp(hot, accent, 0.22f) : hot;
			MagicalFx.Shape(ShapeId.WispTaper, Projectile.Center, spin, headW, headH, headColor * 0.9f);
			MagicalFx.Bloom(Projectile.Center, 34f * depth, accent, 0.4f);

			if (crossFlash > 0) {
				float k = crossFlash / 8f;
				MagicalFx.StarFlare(Projectile.Center, Color.White, 40f * k, spin, 0.7f * k);
				MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 60f * k, hot, 0.4f * k);
			}

			if (styleFlash > 0) {
				float k = styleFlash / (Family == 10 ? 6f : 8f);
				if (Family == 6) {
					// A thin vertical shimmer marks the beat seam.
					MagicalFx.Shape(ShapeId.NeedleLong, Projectile.Center, 0f,
						12f + 8f * k, 42f * k, Color.White * (0.6f * k));
					MagicalFx.StarFlare(Projectile.Center, Color.White, 34f * k, 0f, 0.55f * k);
				}
				else if (Family == 7) {
					MagicalFx.StarFlare(Projectile.Center, Color.White, 44f * k, spin, 0.8f * k);
					MagicalFx.Shape(ShapeId.SplatStar, Projectile.Center, spin,
						24f * k, 24f * k, hot * (0.5f * k));
				}
				else if (Family == 9) {
					// Five petal tips: the accent frame for the n=5 curve.
					MagicalFx.StarFlare(Projectile.Center, Color.White, 48f * k, spin, 0.85f * k);
					MagicalFx.Shape(ShapeId.SplatStar, Projectile.Center, spin + MathHelper.PiOver4,
						30f * k, 30f * k, accent * (0.55f * k));
				}
				else {
					MagicalFx.StarFlare(Projectile.Center, Color.White, 38f * k, spin, 0.7f * k);
					MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 52f * k, hot, 0.35f * k);
				}
			}
			return false;
		}
	}

	/// <summary>
	/// Growing Star: cast home toward the cursor, then feed it. It leaves the
	/// caster small, swells for ~2.7 seconds, drifts after enemies while it is
	/// out, and detonates for everything its size bought it. Fires early if it
	/// rams something or hits a wall, so camping the cursor on a target still
	/// pays off immediately.
	///
	/// ai[1]: power01. ai[2]: source.
	/// </summary>
	public class MagicalGrowOrbProj : ModProjectile
	{
		private const int GrowTicks = 160;
		private const int LifeTicks = 190;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int age;
		private int beatClock;
		private bool exploded;
		private float power;
		private Color accent = new(210, 180, 255);

		private float Grown => MathHelper.Clamp(age / (float)GrowTicks, 0f, 1f);

		public override void SetDefaults()
		{
			Projectile.width = 26;
			Projectile.height = 26;
			Projectile.friendly = false; // it hurts only through the blast
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = LifeTicks;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			age++;

			float g = Grown;
			Projectile.scale = 0.55f + 1.45f * g;
			Projectile.rotation = Projectile.velocity.ToRotation();

			// Lazy homing while it feeds, slowing as it grows: it gets heavier.
			NPC target = NearestTarget(720f);
			if (target != null) {
				Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity);
				Projectile.velocity = MagicalOrbit
					.LerpAngle(Projectile.velocity.ToRotation(), want.ToRotation(), 0.028f)
					.ToRotationVector2() * MathHelper.Clamp(Projectile.velocity.Length(), 4.6f, 7.4f);
			}

			if (++beatClock >= 26) {
				beatClock = 0;
				if (!Main.dedServ) {
					YoyoParticles.SpawnRing(Projectile.Center, Projectile.width * 0.4f,
						Projectile.width * 0.4f + 26f + 30f * g, 12, accent, 2f + 2f * g);
					MagicalFx.Bloom(Projectile.Center, 36f + 40f * g, accent, 0.2f + 0.2f * g); // per-tick: stays a queued glow
					if (g > 0.5f)
						MiniYoyoFx.Shake(g > 0.85f ? 2 : 1, 4, Projectile.Center);
				}
			}

			// Ram check: pound it into an enemy and it goes off early.
			if (MagicalReleaseCommon.CanMoveNpcs) {
				float touch = 26f + 40f * g;
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC npc = Main.npc[i];
					if (!MagicalReleaseCommon.Hittable(npc))
						continue;
					float reach = touch + Math.Max(npc.width, npc.height) * 0.5f;
					if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > reach * reach)
						continue;
					Explode(g);
					return;
				}
			}

			if (age >= GrowTicks)
				Explode(g);

			// Wind-up cue: a rising tick as the star commits to detonating.
			if (!Main.dedServ && age == GrowTicks - 15)
				SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.35f, Pitch = 0.45f }, Projectile.Center);
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.6f + 0.7f * g));
		}

		private NPC NearestTarget(float range)
		{
			NPC best = null;
			float bestD = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Targetable(npc))
					continue;
				float d = Vector2.DistanceSquared(npc.Center, Projectile.Center);
				if (d < bestD) {
					bestD = d;
					best = npc;
				}
			}
			return best;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (!exploded)
				Explode(Grown);
			return true;
		}

		private void Explode(float g)
		{
			if (exploded)
				return;
			exploded = true;

			float radius = 120f + 155f * g;
			int dmg = Math.Max(1, (int)(Projectile.damage * (0.9f + 1.35f * g)));
			if (MagicalReleaseCommon.CanMoveNpcs)
				MagicalReleaseCommon.RadialHit(OwnerOf(), Projectile.Center, radius + 30f, dmg, 6f);

			if (!Main.dedServ) {
				float s = 0.9f + 0.8f * g;
				MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 1.3f * s);
				MagicalFx.CastFlash(Projectile.Center, accent, 1.1f * s);
				// The flat bloom was 120 + 220g (up to a 340px disc) at 0.5-0.8
				// alpha, on top of FlashFX + CastFlash + three rings + a screen
				// pulse in the same frame. That is a white-out, not an explosion -
				// the disc is the least informative layer here and it was burying
				// the layers that actually have SHAPE. Calamity's blasts carry
				// their silhouette with a shockwave ring and shards, and use the
				// bloom only as a soft underlay, so the flat layer is now small
				// and dim and the rings read.
				MagicalFx.Pop(Projectile.Center, 90f + 130f * g, accent, 0.34f + 0.22f * g);
				YoyoParticles.SpawnRing(Projectile.Center, 10f, radius * 0.6f, 16, accent, 2.6f);
				YoyoParticles.SpawnRing(Projectile.Center, 10f, radius * 1.25f, 22, accent, 4f + 3f * g);
				YoyoParticles.SpawnRing(Projectile.Center, 6f, radius * 0.8f, 18, Color.White, 3f + 2f * g);
				MagicalFx.Scorch(Projectile.Center, accent, s, crack: true);
				MagicalFx.Fan(ShapeId.SplatStar, Projectile.Center, Color.Lerp(accent, Color.White, 0.3f),
					6, 3.6f + 2.4f * g, 22, 0.65f);
				MagicalFx.Fan(ShapeId.WispTaper, Projectile.Center, accent, 9, 5.5f, 22, 0.5f,
					Projectile.velocity.ToRotation() + MathHelper.Pi, 2.4f);
				MagicalFx.ScreenPulse(Projectile.Center, 0.25f + 0.45f * g, accent);
				MiniYoyoFx.Shake(3 + (int)(4f * g), 10, Projectile.Center);
				SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f + 0.3f * g, Pitch = 0.1f }, Projectile.Center);
			}
			Projectile.Kill();
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		public override void OnKill(int timeLeft)
		{
			if (exploded || Main.dedServ)
				return;
			MagicalFx.Pop(Projectile.Center, 44f, accent, 0.3f);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 3, 2f, 14, 0.45f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			float g = Grown;
			Color hot = Color.Lerp(accent, Color.White, 0.4f + 0.45f * g);
			Vector2 c = Projectile.Center;
			float r = (14f + 46f * g) * (1f + 0.045f * (float)Math.Sin(t2 * 0.16f));

			// A seed that visibly cooks: dark heart, growing shell, hot rim.
			MiniYoyoFx.DrawGlowAdditive(c, r * 4.4f, accent, 0.25f + 0.3f * g);
			MiniYoyoFx.DrawSolidEllipse(c, r * 2f, r * 2f, 0f, accent * 0.35f, 0.85f);
			MiniYoyoFx.DrawSolidEllipse(c, r * 1.15f, r * 1.15f, 0f, Color.Lerp(accent, Color.Black, 0.55f), 0.7f);
			MiniYoyoFx.DrawSolidEllipse(c, r * 0.8f, r * 0.8f, 0f, Color.Lerp(accent, Color.White, 0.25f), 0.5f);

			// Cracks that widen with the charge.
			MagicalFx.Shape(ShapeId.RingCracked, c, t2 * 0.03f + g, r * 2.6f, r * 2.6f, hot * (0.2f + 0.55f * g));
			if (g > 0.25f) {
				for (int i = 0; i < 3; i++) {
					float a = t2 * 0.05f + i * MathHelper.TwoPi / 3f;
					MagicalFx.Shape(ShapeId.NeedleLong, c + a.ToRotationVector2() * (r * 1.3f), a,
						r * 1.1f, r * 0.5f, hot * (0.35f + 0.4f * g));
				}
			}
			// Heartbeat shell snapping outward.
			float since = beatClock / 26f;
			if (since < 0.3f) {
				float k = 1f - since / 0.3f;
				float baseR = Projectile.width * 0.4f + 30f * g;
				MagicalFx.Ring(c, baseR + (1f - k) * (34f + 66f * g), t2 * 0.02f, 0.55f * k, hot);
			}

			// Final wind-up: the shell contracts, the core goes white, and the
			// last quarter second reads as "this is about to go off".
			int left = GrowTicks - age;
			if (left > 0 && left <= 15) {
				float k = 1f - left / 15f;
				MagicalFx.Ring(c, r * (3.4f - 2f * k), t2 * 0.06f, 0.5f + 0.4f * k, hot);
				MagicalFx.Ring(c, r * (2.2f - 1.2f * k), -t2 * 0.08f, 0.5f + 0.4f * k, Color.White);
				MiniYoyoFx.DrawGlowAdditive(c, r * (3f + 2.5f * k), Color.White, 0.2f + 0.45f * k);
			}

			MagicalFx.StarFlare(c, Color.White, 26f + 46f * g, t2 * 0.2f, 0.3f + 0.5f * g);
			MagicalFx.Motes(c, r * 2.2f, accent, 0.3f, 4, 1.0f + g);
			return false;
		}
	}

	/// <summary>
	/// Feeder: one orbiting yoyo leaves the ring and is thrown at the cursor
	/// point, bouncing off walls like a real yoyo, then reels itself home. The
	/// real orbiter is hidden (never damaged or removed) while its feeder is
	/// out, so the ring visibly thins and refills. The feeder carries the
	/// orbiter's own colours, so you can tell whose feeder is whose.
	///
	/// ai[0]: launch delay in ticks. ai[1]: orbiter projectile index (-1 = no
	/// orbiter to restore). ai[2]: orbiter source type (accent).
	/// </summary>
	public class MagicalFeederProj : ModProjectile
	{
		private const int LifeTicks = 260;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private readonly List<Vector2> trail = new();
		private Vector2 launchDir;
		private bool launched;
		private bool returning;
		private bool restored;
		private Color accent = new(200, 200, 255);

		private float AgeF => LifeTicks - Projectile.timeLeft;
		private float Delay => Math.Max(0f, Projectile.ai[0]);

		public override void OnSpawn(IEntitySource source)
		{
			// The launch spoke is captured at birth; velocity is pinned so the
			// held feeder never drifts off its hover point.
			launchDir = Projectile.velocity.LengthSquared() > 0.01f
				? Projectile.velocity.SafeNormalize(Vector2.UnitX)
				: Vector2.UnitX;
			Projectile.velocity = Vector2.Zero;
		}

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 4;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 12;
			Projectile.timeLeft = LifeTicks;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			Player owner = OwnerOf();
			Projectile.rotation += returning ? 0.32f : 0.22f;

			if (!launched) {
				// Charging: hover in front of the caster along the chosen spoke,
				// stagger by delay so the feeders leave one after another.
				Vector2 home = owner != null
					? owner.Center + launchDir * (36f + Projectile.ai[0] * 0.5f)
					: Projectile.Center;
				Projectile.Center = Vector2.Lerp(Projectile.Center, home, 0.3f);
				if (!Main.dedServ && Main.rand.NextBool(2)) {
					YoyoParticles.SpawnSpark(Projectile.Center - launchDir * 8f, -launchDir * 1.4f,
						accent, 0.6f, 14, 0.95f);
				}
				if (AgeF >= Delay) {
					launched = true;
					Projectile.friendly = true;
					Projectile.velocity = launchDir * 15f;
					Projectile.netUpdate = true;
					if (!Main.dedServ) {
						MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.7f);
						MagicalFx.CastFlash(Projectile.Center, accent, 0.6f);
						MagicalFx.Fan(ShapeId.WispTaper, Projectile.Center, accent, 6, 5f, 20, 0.5f,
							launchDir.ToRotation(), 0.7f);
						SoundEngine.PlaySound(SoundID.Item19 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
					}
				}
				return;
			}

			trail.Insert(0, Projectile.Center);
			if (trail.Count > 14)
				trail.RemoveAt(trail.Count - 1);

			if (!returning
				&& (Projectile.localAI[0] >= 3f || AgeF > Delay + 115f))
				returning = true;

			if (returning && owner != null) {
				Projectile.tileCollide = false;
				Vector2 to = (owner.Center - Projectile.Center).SafeNormalize(Projectile.velocity);
				Projectile.velocity = MagicalOrbit
					.LerpAngle(Projectile.velocity.ToRotation(), to.ToRotation(), 0.085f)
					.ToRotationVector2() * 13.5f;
				if (Vector2.DistanceSquared(Projectile.Center, owner.Center) < 30f * 30f) {
					Reel();
					return;
				}
			}

			if (!Main.dedServ && Main.rand.NextBool(3))
				YoyoParticles.SpawnSpark(Projectile.Center, -Projectile.velocity * 0.05f, accent, 0.55f, 13, 0.95f);
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.8f);
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			// Bounce like a yoyo off a wall instead of dying.
			Projectile.localAI[0] += 1f;
			Projectile.netUpdate = true; // keep remote copies on the same bounce
			float speed = Projectile.velocity.Length();
			if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.01f)
				Projectile.velocity.X = -oldVelocity.X;
			if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.01f)
				Projectile.velocity.Y = -oldVelocity.Y;
			Projectile.velocity = Projectile.velocity.SafeNormalize(oldVelocity) * speed;
			if (!Main.dedServ) {
				MiniYoyoFx.FlashFX(Projectile.Center, accent, 0.5f);
				MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center,
					Color.Lerp(accent, Color.White, 0.3f), 3, 1.8f, 14, 0.5f);
				SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.35f }, Projectile.Center);
			}
			return false;
		}

		private void Reel()
		{
			RestoreOrbiter();
			if (!Main.dedServ) {
				MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.6f);
				YoyoParticles.SpawnRing(Projectile.Center, 4f, 44f, 12, accent, 2.2f);
				MagicalFx.Pop(Projectile.Center, 40f, accent, 0.3f);
			}
			Projectile.Kill();
		}

		private void RestoreOrbiter()
		{
			if (restored)
				return;
			restored = true;
			int index = (int)Projectile.ai[1];
			if (index < 0 || index >= Main.maxProjectiles)
				return;
			Projectile orb = Main.projectile[index];
			if (!orb.active || orb.type != ModContent.ProjectileType<MagicalYoyoProj>())
				return;
			if (orb.ModProjectile is MagicalYoyoProj yoyo)
				yoyo.FeederAway = false;
			orb.netUpdate = true;
		}

		public override void OnKill(int timeLeft)
		{
			RestoreOrbiter();
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			Color hot = Color.Lerp(accent, Color.White, 0.6f);
			Vector2 c = Projectile.Center;
			Vector2 dir = (returning ? -Projectile.velocity : Projectile.velocity).SafeNormalize(
				launchDir.LengthSquared() > 0.01f ? launchDir : Vector2.UnitX);

			if (trail.Count >= 2) {
				int count = 0;
				Vector2[] pts = new Vector2[Math.Min(trail.Count + 1, 16)];
				pts[count++] = c;
				for (int i = 0; i < trail.Count && count < pts.Length; i++)
					pts[count++] = trail[i];
				MiniYoyoFx.DrawPathBand(pts, count, 10f, 2f, accent * 0.5f, 0.55f);
				MiniYoyoFx.DrawPathBand(pts, count, 5f, 1f, hot, 0.75f);
			}

			// It is a yoyo, not a bullet: core disc + crescent shell + string glow.
			Vector2 stringDir = returning ? dir : -dir;
			MiniYoyoFx.DrawSoftBar(c + stringDir * 20f, stringDir.ToRotation(), 40f, 3.4f, accent, 0.35f);
			MiniYoyoFx.DrawSolidEllipse(c, 24f, 24f, Projectile.rotation, Color.Lerp(accent, Color.Black, 0.35f), 0.9f);
			MiniYoyoFx.DrawSolidEllipse(c, 17f, 17f, -Projectile.rotation * 1.4f, accent, 0.85f);
			MagicalFx.Shape(ShapeId.SmearCrescent, c, Projectile.rotation, 26f, 15f, hot);
			MagicalFx.Shape(ShapeId.SmearCrescent, c, Projectile.rotation + MathHelper.Pi, 22f, 12f, hot * 0.8f);
			MagicalFx.Bloom(c, 40f, accent, 0.35f);
			MagicalFx.Ring(c, 15f, t2 * 0.06f, 0.35f, hot);
			return false;
		}
	}

	/// <summary>
	/// Pulse Cascade: a short-lived focus that rides in front of the caster and
	/// spits instant beams at the live cursor point every 0.1s for two seconds.
	/// Because the beams are instant, sweeping the cursor drags a curtain of
	/// light across the field. Only the owner's client spawns them.
	///
	/// ai[1]: power01. ai[2]: source.
	/// </summary>
	public class MagicalPulseCascadeProj : ModProjectile
	{
		private const int LifeTicks = 132;
		private const int Interval = 6;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int age;
		private int shot;
		private Color accent = new(160, 210, 255);
		private readonly List<Vector2> sweep = new();
		private static readonly Vector2[] sweepPts = new Vector2[14];

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = LifeTicks;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			Player owner = OwnerOf();
			if (owner == null) {
				Projectile.Kill();
				return;
			}

			// Ride the caster's chest so the curtain always launches from their
			// core, wherever the fight dragged them.
			Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center - Vector2.UnitY * 16f, 0.4f);
			age++;
			if (age == 1 && !Main.dedServ) {
				MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.8f);
				MagicalFx.CastFlash(Projectile.Center, accent, 0.8f);
				SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.45f, Pitch = -0.3f }, Projectile.Center);
			}

			if (age % Interval == 0 && age < LifeTicks - 10) {
				Vector2 to = (Main.MouseWorld - Projectile.Center);
				// Owner-side only: Spawn() refuses everyone but the owner, so the
				// swept target is always the casting player's cursor.
				MagicalReleaseCommon.Spawn(owner, Projectile.Center, to,
					ModContent.ProjectileType<MagicalPulseBeamProj>(),
					Math.Max(1, Projectile.damage), 0f, Projectile.ai[1], Projectile.ai[2]);
				shot++;
				if (Projectile.owner == Main.myPlayer) {
					// Remember where the cursor has been: the sweep draws as one
					// fading arc instead of disconnected muzzle flashes.
					sweep.Insert(0, Projectile.Center + to);
					if (sweep.Count > sweepPts.Length)
						sweep.RemoveAt(sweep.Count - 1);
				}
				if (!Main.dedServ) {
					MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.4f);
					YoyoParticles.SpawnRing(Projectile.Center, 4f, 30f, 12, accent, 2.2f);
				}
			}
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.9f);
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			float fade = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
			Color hot = Color.Lerp(accent, Color.White, 0.6f);
			Vector2 c = Projectile.Center;
			bool flash = age % Interval < 3;

			// The curtain: the cursor's own path, redrawn as a tapering arc.
			if (sweep.Count >= 2) {
				int count = 0;
				for (int i = 0; i < sweep.Count && count < sweepPts.Length; i++)
					sweepPts[count++] = sweep[i];
				MiniYoyoFx.DrawPathBand(sweepPts, count, 2f, 18f, accent, 0.4f * fade);
				MiniYoyoFx.DrawPathBand(sweepPts, count, 1f, 7f, hot, 0.55f * fade);
				for (int i = 0; i < count; i += 3)
					MagicalFx.Bloom(sweepPts[i], 26f * (1f - i / (float)count), accent, 0.22f * fade);
			}

			MagicalFx.Rune(c, 20f, t2 * 0.12f, 0.6f * fade, hot, 0.7f);
			MagicalFx.Ring(c, 24f, -t2 * 0.09f, 0.5f * fade, accent);
			MagicalFx.Ring(c, 13f, t2 * 0.15f, 0.7f * fade, hot);
			MagicalFx.Converge(c, 34f, 6f, accent, 0.5f * fade, 6, t2 * 0.04f, 1.7f);
			MiniYoyoFx.DrawGlowAdditive(c, 56f + (flash ? 40f : 0f), hot, (0.35f + (flash ? 0.4f : 0f)) * fade);
			if (shot > 0)
				MagicalFx.StarFlare(c, Color.White, 30f, t2 * 0.4f, 0.4f * fade);
			return false;
		}
	}

	/// <summary>
	/// Lifedrain Nova: a crimson detonation at the cursor that rips health out
	/// of everything it catches and pours it back into the caster - capped, so
	/// it stays a burst heal and not a tank. Gold streamers run from every
	/// victim to the owner as the payoff.
	///
	/// ai[1]: power01. ai[2]: source.
	/// </summary>
	public class MagicalLifedrainProj : ModProjectile
	{
		private const int LifeTicks = 42;
		private const int MaxHeal = 55;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private Vector2 anchor;
		private float radius;
		private int age;
		private bool struck;
		private int healed;
		private Color accent = new(255, 70, 90);
		private readonly List<Vector2> drains = new();

		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			Projectile.friendly = false; // manual radial strike
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = LifeTicks;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			if (age == 0) {
				anchor = Projectile.Center;
				Projectile.velocity = Vector2.Zero;
				radius = 190f + 80f * MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
				Strike();
				if (!Main.dedServ)
					SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.6f, Pitch = -0.25f }, anchor);
			}
			age++;
			if (!Main.dedServ) {
				float t = age / (float)LifeTicks;
				if (age % 4 == 0)
					YoyoParticles.SpawnRing(anchor, radius * 0.3f * t, radius * (0.35f + 0.8f * t), 14,
						Color.Lerp(accent, new Color(255, 205, 120), t), 3.5f);
			}
			Lighting.AddLight(anchor, accent.ToVector3() * 1.1f);
		}

		private void Strike()
		{
			Player owner = OwnerOf();
			if (owner == null || !MagicalReleaseCommon.CanMoveNpcs)
				return;
			struck = true;
			int hitCount = 0;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc))
					continue;
				float reach = radius + Math.Max(npc.width, npc.height) * 0.5f;
				if (Vector2.DistanceSquared(npc.Center, anchor) > reach * reach)
					continue;
				// One tech hit per enemy per window, with the hit pop handled by
				// the shared lock. Damage first, then read the health swing for
				// the leech amount.
				int before = npc.life;
				if (!MagicalReleaseCommon.Strike(owner, npc, Projectile.damage, 4f, accent, anchor))
					continue;
				int dealt = Math.Max(0, before - npc.life);
				int heal = Math.Clamp((int)(dealt * 0.055f), 2, 9);
				healed += heal;
				hitCount++;
				drains.Add(npc.Center);
			}
			if (hitCount == 0)
				return;
			healed = Math.Min(healed, MaxHeal);
			int restore = Math.Min(healed, owner.statLifeMax2 - owner.statLife);
			if (restore > 0) {
				owner.statLife += restore;
				owner.HealEffect(restore, true);
			}
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			float t = MathHelper.Clamp(age / (float)LifeTicks, 0f, 1f);
			float ease = 1f - (1f - t) * (1f - t);
			float fade = 1f - t;
			Color gold = new(255, 205, 120);
			Color mix = Color.Lerp(accent, gold, t * 0.8f);

			// Two rings: a hard crimson shell, a slower gold one behind it.
			MagicalFx.Ring(anchor, radius * ease, t2 * 0.02f, 0.75f * fade, accent);
			MagicalFx.Ring(anchor, radius * ease * 0.72f, -t2 * 0.03f, 0.6f * fade, gold);
			MagicalFx.Shockwave(anchor, radius * ease, t, mix, 0.8f * fade, 10f * (1f - t) + 4f);
			if (t < 0.5f)
				MiniYoyoFx.DrawGlowAdditive(anchor, (140f + 200f * t) * (1f - t * 1.6f), accent,
					0.55f * (1f - t * 1.8f));
			if (t < 0.25f)
				MiniYoyoFx.FlashFX(anchor, Color.White, 1.1f * (1f - t * 4f));

			// Leech tethers: drawn from every victim once, then faded to the owner.
			if (struck) {
				Player owner = OwnerOf();
				if (owner != null) {
					float stream = MathHelper.Clamp(1f - t * 2.4f, 0f, 1f);
					for (int i = 0; i < drains.Count && i < 12; i++) {
						Vector2 from = drains[i];
						MagicalFx.Bolt(from, owner.Center, gold, 0.5f * stream, 2.2f, 900 + i * 17);
						if (stream > 0f)
							MiniYoyoFx.DrawGlowAdditive(Vector2.Lerp(from, owner.Center, t * 2.2f % 1f),
								44f * stream, gold, 0.4f * stream);
					}
					MiniYoyoFx.DrawGlowAdditive(owner.Center, 60f * stream, gold, 0.4f * stream);
				}
			}
			MagicalFx.Converge(anchor, radius * 0.8f * (0.5f + 0.5f * ease), 8f, accent,
				0.4f * fade, 8, -t2 * 0.04f, 1.6f);
			return false;
		}
	}
}
