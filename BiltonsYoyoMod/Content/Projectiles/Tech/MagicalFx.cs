using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;
using BiltonsYoyoMod.Content.Systems;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// The Magical Tech's shared VFX vocabulary.
	///
	/// Everything here is built from SOFT ART - the baked MiniBossShapeBank
	/// shapes (crescents, runes, wisps, ember curls, cracked rings, bolts...) and
	/// the mod's own effect sprites - never from flat 1px geometry. That is the
	/// whole point: a ring drawn from MagicPixel reads as a circle outline, while
	/// the same ring stamped from the shape bank reads as light.
	///
	/// Draw* helpers must only be called from a live sprite batch (PreDraw).
	/// Spawn* helpers are pooled and Queue* helpers only append to the frame's
	/// additive queue, so both are safe from AI and from any side.
	/// </summary>
	internal static class MagicalFx
	{
		// ---- optional art (every one degrades to nothing if missing) ----
		public const string ArtStar = "BiltonsYoyoMod/Projectiles/YoyoStar";
		public const string ArtMeteor = "BiltonsYoyoMod/Projectiles/SkyfallStarProj";
		public const string ArtWisp = "BiltonsYoyoMod/Projectiles/CultistWispProj";
		public const string ArtBubble = "BiltonsYoyoMod/Projectiles/DukeYoyoProjBubble";
		public const string ArtShard = "BiltonsYoyoMod/Projectiles/IceQueenYoyoShardProj";
		public const string ArtRing = "BiltonsYoyoMod/Projectiles/WildStringRingProj";

		private static readonly Dictionary<string, Texture2D> cache = new();
		private static readonly HashSet<string> missing = new();

		public static Texture2D Art(string path)
		{
			if (string.IsNullOrEmpty(path) || missing.Contains(path))
				return null;
			if (cache.TryGetValue(path, out Texture2D tex) && tex != null && !tex.IsDisposed)
				return tex;
			try {
				if (ModContent.RequestIfExists<Texture2D>(path, out var asset) && asset?.Value != null) {
					Texture2D value = asset.Value;
					cache[path] = value;
					return value;
				}
			}
			catch {
			}
			missing.Add(path);
			return null;
		}

		// =====================================================================
		//  Soft shape art, sized in pixels (shape textures are 64x64 but the
		//  helper normalises through tex.Width so the read is size-independent).
		// =====================================================================

		public static void Shape(ShapeId id, Vector2 world, float rot, float width, float height, Color color)
		{
			if (color.A == 0 || width <= 0.1f || height <= 0.1f)
				return;
			Texture2D tex = MiniBossShapeBank.Get(id);
			if (tex == null || tex.IsDisposed)
				return;
			MiniBossShapeBank.Draw(id, world, rot, new Vector2(width / tex.Width, height / tex.Height), color);
		}

		/// <summary>
		/// Queued version of <see cref="Shape"/>. Safe to call from AI, hit
		/// hooks, and any other code that runs outside a live sprite batch.
		/// </summary>
		public static void QueueShape(ShapeId id, Vector2 world, float rot, float width, float height, Color color, int life = 10)
		{
			if (Main.dedServ || color.A == 0 || width <= 0.1f || height <= 0.1f)
				return;
			Texture2D tex = MiniBossShapeBank.Get(id);
			if (tex == null || tex.IsDisposed)
				return;
			MiniBossShapeBank.Spawn(id, world, rot,
				new Vector2(width / tex.Width, height / tex.Height), color, life);
		}

		public static void Wisp(Vector2 world, float rot, float length, float width, Color color)
			=> Shape(ShapeId.WispTaper, world, rot, length, width, color);

		public static void Crescent(Vector2 world, float rot, float size, Color color)
			=> Shape(ShapeId.SmearCrescent, world, rot, size, size, color);

		/// <summary>Soft splat blot. The spiky star stamp is retired here: at burst
		/// sizes it read as a spinning cross, so every splash is a smooth blot now.</summary>
		public static void Splat(Vector2 world, float rot, float size, Color color)
			=> Shape(ShapeId.SplatSoft, world, rot, size, size, color);

		public static void Rune(Vector2 world, float radius, float rot, float alpha, Color color, float squash = 1f)
			=> Shape(ShapeId.SealRune, world, rot, radius * 2f, radius * 2f * squash, color * alpha);

		/// <summary>Segmented art ring - the clean read for every large circle.
		/// Rune glyphs stay small (they get busy when scaled up).</summary>
		public static void Ring(Vector2 world, float radius, float rot, float alpha, Color color)
			=> Shape(ShapeId.RingCracked, world, rot, radius * 2f, radius * 2f, color * alpha);

		// =====================================================================
		//  Sprite art (the mod's own effect sprites).
		// =====================================================================

		/// <summary>Draws art at an explicit pixel size. NOTE: do not add an
		/// overload whose 4th parameter is a scale - a 6-argument call would then
		/// bind to it and treat the rotation as a scale (that bug drew the ice
		/// shards at rotation-times-size).</summary>
		public static void Sprite(string path, Vector2 world, Color color, float rot, float width, float height, float alpha = 1f)
		{
			Texture2D tex = Art(path);
			if (tex == null || tex.IsDisposed)
				return;
			Color tint = color * alpha;
			if (tint.A == 0)
				return;
			Main.spriteBatch.Draw(tex, world - Main.screenPosition, null, tint, rot, tex.Size() * 0.5f,
				new Vector2(width / tex.Width, height / tex.Height), SpriteEffects.None, 0f);
		}

		public static void SpriteScaled(string path, Vector2 world, Color color, float scale, float rot = 0f, float alpha = 1f)
		{
			Texture2D tex = Art(path);
			if (tex == null || tex.IsDisposed)
				return;
			Sprite(path, world, color, rot, tex.Width * scale, tex.Height * scale, alpha);
		}

		// =====================================================================
		//  Light, glow and flares.
		// =====================================================================

		public static void Glow(Vector2 world, float diameter, Color color, float alpha)
			=> MiniYoyoFx.DrawGlow(world, diameter, color, alpha);

		public static void Bloom(Vector2 world, float diameter, Color color, float alpha)
			=> MiniYoyoFx.DrawGlowAdditive(world, diameter, color, alpha);

		/// <summary>
		/// LINGERING BLOOM - the one-shot cousin of <see cref="Bloom"/>. Bloom
		/// (and the DrawGlowAdditive behind it) only APPENDS to the additive glow
		/// queue, so it is safe to call from AI, OnKill and Tick alike - but the
		/// queue is flushed once, so it is on screen for exactly one frame. A
		/// 16ms flash is easy to miss on an impact, which is what makes some
		/// hits feel like they did nothing. This spawns a real particle instead:
		/// it grows from 28% to 55% of the diameter while fading over `life`
		/// frames, so a kill, a bite or a bowl pulse leaves a mark you can read.
		/// Same argument order as Bloom, so a call site is a one-word change.
		/// ONLY for events that fire at most once per bloom life - kills,
		/// impacts, pulses on a 40+ tick cadence. Do NOT call it every tick: the
		/// blooms overlap and their alpha stacks into a white blob. Per-frame or
		/// continuous glows keep using <see cref="Bloom"/>.
		/// The particle pool is density-gated by the user's settings and capped,
		/// so treat this as a flourish on top of guaranteed feedback.
		/// </summary>
		public static void Pop(Vector2 world, float diameter, Color color, float alpha, int life = 12)
		{
			if (Main.dedServ || alpha <= 0.01f)
				return;
			YoyoParticles.SpawnBloom(world, diameter * 0.28f, diameter * 0.55f, life, color, alpha);
		}

		/// <summary>
		/// Queued SOFT RING: a circle of additive glows. Use this instead of
		/// <see cref="Ring"/> from AI / update code - Ring stamps shape art and
		/// therefore needs a live sprite batch, while this only appends to the
		/// glow queue. Overlapping glows composite into a continuous band of
		/// light, which is what an energy ring should look like anyway.
		/// </summary>
		public static void QueueRing(Vector2 world, float radius, Color color, float alpha,
			int count = 28, float thickness = 0.8f, float rot = 0f)
		{
			if (alpha <= 0.01f || radius < 2f)
				return;
			int n = Math.Clamp(count, 8, 64);
			float dia = MathHelper.Max(7f, radius * MathHelper.TwoPi / n * 1.55f * thickness);
			for (int i = 0; i < n; i++) {
				float a = rot + i * MathHelper.TwoPi / n;
				Bloom(world + a.ToRotationVector2() * radius, dia, color, alpha);
			}
		}

		/// <summary>
		/// Crossed star flare. `length` is the flare's long axis in PIXELS - it
		/// is divided by the star art's own height before drawing, so a caller
		/// passing 40 gets a 40px flare instead of a 40x screen-wide one.
		/// </summary>
		public static void StarFlare(Vector2 world, Color color, float length, float rot, float alpha)
		{
			if (alpha <= 0.01f || length <= 0.5f)
				return;
			Texture2D tex = Art(ArtStar);
			float basis = tex != null && !tex.IsDisposed ? tex.Height : 32f;
			MiniYoyoFx.DrawStarSparkle(world, color, length / basis, rot, alpha);
		}

		/// <summary>
		/// Queued crossed star flare - the AI-safe sibling of
		/// <see cref="StarFlare"/>. StarFlare stamps the star art into the batch
		/// that is currently open, so it may only run from a PreDraw; this
		/// appends to the frame's additive window instead and is safe from AI,
		/// OnHit, OnKill and any other update-side code. The draw geometry is
		/// identical.
		/// </summary>
		public static void QueueStarFlare(Vector2 world, Color color, float length, float rot, float alpha)
		{
			if (alpha <= 0.01f || length <= 0.5f)
				return;
			Texture2D tex = Art(ArtStar);
			float basis = tex != null && !tex.IsDisposed ? tex.Height : 32f;
			MiniYoyoFx.QueueStarSparkle(world, color, length / basis, rot, alpha);
		}

		public static void Light(Vector2 world, Color color, float intensity)
			=> Lighting.AddLight(world, color.ToVector3() * intensity);

		public static void ScreenPulse(Vector2 world, float amount, Color color)
			=> YoyoScreenShockwaveSystem.Trigger(world, amount, color);

		// =====================================================================
		//  Composite pieces.
		// =====================================================================

		/// <summary>Layered expanding front - the read that replaces a bare ring.</summary>
		public static void Shockwave(Vector2 world, float radius, float progress, Color color, float alpha, float width = 3f)
		{
			float p = MathHelper.Clamp(progress, 0f, 1f);
			MiniYoyoFx.DrawShockwaveRing(world, radius * 0.30f, radius, 26f, p * 26f, color * alpha, width);
			MiniYoyoFx.DrawShockwaveRing(world, radius * 0.10f, radius * 0.76f, 18f, p * 18f,
				Color.Lerp(color, Color.White, 0.55f) * (alpha * 0.7f), width * 0.5f);
		}

		/// <summary>
		/// Jagged two-pass discharge between two points. Kept as the plain form
		/// (no barbs, no strike head) for short internal arcs and for arcs that
		/// are stored and redrawn later; anything meant to read as a real
		/// lightning STRIKE should use <see cref="Skyfang"/>.
		/// </summary>
		public static void Bolt(Vector2 a, Vector2 b, Color color, float alpha, float width, int seed, float jitter = 0.24f)
		{
			if (alpha <= 0.01f || width <= 0.05f)
				return;
			// NOW DRAWN OUT OF LIGHT, LIKE EVERY OTHER TECH DISCHARGE.
			// This used to delegate to MiniYoyoFx.DrawLightning, which paints
			// each segment of a jagged polyline with the legacy hard-edged line
			// primitive. An arc built that way is half a dozen straight SOLID
			// planks meeting at angles, which is exactly what a row of rectangles
			// looks like - and the storm bowl's nine radial streamers are drawn
			// with this call, which is why that ability kept being reported as
			// having "a rectangle thing". DischargeRun draws the same shape out of
			// soft overlapping light instead: one stable seeded channel, an aura,
			// a braid and a hot core, so there is no hard edge and no corner to
			// read as a rectangle anywhere in the bolt.
			//
			// `jitter` stays in the signature for the call sites and now scales
			// the channel's WIDTH: the channel's shape is fixed-frequency by
			// design (that is what keeps a bolt's silhouette stable across its
			// whole life instead of strobing into per-frame noise), so jitter can
			// no longer mean "re-roll the zigzag".
			float amp = 0.60f + 0.80f * MathHelper.Clamp(jitter, 0.05f, 0.60f);
			DischargeRun(a, b, color, alpha, Math.Max(width, 1.2f) * amp, seed + 9001, false);
		}

		/// <summary>
		/// Sideways offset of a discharge channel at <paramref name="f"/> along its
		/// run, as a multiple of the channel half-width.
		///
		/// This is a sum of fixed-frequency sines keyed on the bolt's seed, which
		/// is what makes the bolt STABLE: the same shape is drawn every frame for
		/// the whole flight. Only the tiny `buzz` term moves, at ~5 Hz, so the
		/// bolt crackles instead of strobing into per-frame noise.
		/// </summary>
		private static float ChannelOffset(float f, float len, float width, int seed)
		{
			float swell = (float)Math.Sin(f * Math.PI); // pinned at both ends
			float off = (float)Math.Sin(f * 9.1f + seed * 0.61f) * 0.72f
				+ (float)Math.Sin(f * 21.7f + seed * 1.37f) * 0.30f;
			if (len > 60f)
				off += (float)Math.Sin(f * 3.9f + seed * 2.11f) * 0.62f;
			float buzz = (float)Math.Sin(Main.GameUpdateCount * 0.30f + seed * 0.71f) * 0.16f;
			return (off + buzz) * swell * width * 1.05f;
		}

		/// <summary>
		/// Soft additive glow chain along a polyline. Overlapping glows composite
		/// into a continuous band of light, which is the whole vocabulary here:
		/// a discharge is made of LIGHT, never of stroked lines or flat quads.
		/// </summary>
		private static void DrawFilament(ReadOnlySpan<Vector2> path, Color color, float alpha,
			float thickness, float spacing, float tailBias = 0f)
		{
			if (alpha <= 0.004f || thickness <= 0.05f)
				return;
			int last = path.Length - 1;
			float step = MathHelper.Max(1.5f, spacing);
			for (int i = 0; i < last; i++) {
				Vector2 a = path[i];
				Vector2 b = path[i + 1];
				int n = Math.Max(1, (int)(Vector2.Distance(a, b) / step));
				for (int k = 0; k < n; k++) {
					float f = (i + (k + 0.5f) / n) / last;
					// Tail-to-head ramp: the far end of a discharge is cooler,
					// thinner and dimmer than the end that is doing the striking.
					float ramp = tailBias <= 0.001f ? 1f : MathHelper.Lerp(1f - tailBias, 1f, f);
					Bloom(Vector2.Lerp(a, b, (k + 0.5f) / n), thickness * ramp, color, alpha * ramp);
				}
			}
		}

		/// <summary>
		/// ONE discharge run from `from` to `to`: a single stable, branch-free
		/// jagged polyline painted in three widths - wide dim aura, mid tint,
		/// white-hot core - plus a strike bloom on the head.
		///
		/// Branch-free is the point. Recursive fork generators (the old
		/// DrawLightning path) are what made these bolts read as "spread out and
		/// multiple": every generation added loose squiggles that had nothing to
		/// do with the strike. Three widths on one run is how the mod's own
		/// Lunatic bolt draws, and it is the only shape that stays legible while
		/// moving.
		/// </summary>
		private static void DischargeRun(Vector2 from, Vector2 to, Color color, float alpha,
			float width, int seed, bool headBloom)
		{
			Vector2 seg = to - from;
			float len = seg.Length();
			if (len < 8f || alpha <= 0.01f || width <= 0.05f)
				return;
			Vector2 dir = seg / len;
			Vector2 nrm = new(-dir.Y, dir.X);
			// REFERENCE (CalamityMod/Particles/ThunderBoltVFX.cs): it draws the
			// bolt with `Color.Lerp(Color.White, Color, Time / Lifetime)` - white
			// hot when it is born, tinting as it dies. `alpha` is this mod's
			// "remaining life", so the ramp rides it directly.
			Color hot = Color.Lerp(color, Color.White, 0.82f * MathHelper.Clamp(alpha, 0f, 1f));

			// SEGMENT DENSITY SCALES WITH LENGTH. This used to be
			//   segs = Clamp((int)(len / 26f), 3, 12)
			// i.e. a hard ceiling of TWELVE vertices for the whole channel. Past
			// ~310px every segment was therefore at least 26px of dead-straight
			// line, and because the aura runs at 3.2x width those corners read as
			// flat planks meeting at angles - the "blocky the further it is"
			// report. The 14px target now holds for the whole realistic range and
			// the ceiling is 48, so a screen-crossing discharge stays a curve.
			// Buffers below are sized to that ceiling.
			const int MaxSegs = 48;
			Span<Vector2> buffer = stackalloc Vector2[MaxSegs + 1];
			int segs = Math.Clamp((int)(len / 14f), 4, MaxSegs);
			for (int i = 0; i <= segs; i++) {
				float f = i / (float)segs;
				buffer[i] = from + dir * (len * f) + nrm * ChannelOffset(f, len, width, seed);
			}
			buffer[0] = from;
			buffer[segs] = to;
			Span<Vector2> run = buffer.Slice(0, segs + 1);

			// AURA. Second direct port of ThunderBoltVFX: it draws the same bolt
			// TWICE - a copy displaced by a random offset at 0.6x alpha behind the
			// crisp one - and the displacement is scaled by `1 - Time / Lifetime`,
			// so the bolt SETTLES as it dies instead of buzzing at full amplitude
			// for its whole life. Wander here rotates deterministically, so it
			// looks identical on every client.
			if (alpha > 0.02f) {
				float wander = width * 0.42f * MathHelper.Clamp(alpha, 0f, 1f);
				// Displaced aura copy. NOTE: this offset used to be
				//   (Main.GameUpdateCount * 0.55f + seed * 1.7f).ToRotationVector2() * wander
				// which sweeps a FULL CIRCLE every ~11 ticks - about five
				// revolutions a second - so the glow visibly ORBITS the bolt. That
				// is the "the lightning does not need orbiting ugly things"
				// report on the storm and chain abilities. The aura now slides
				// LATERALLY across the channel instead: it breathes from one side
				// of the bolt to the other and back, and never circles it.
				// Driven purely by GameUpdateCount + seed, so every client draws
				// the identical frame.
				float wobble = (float)Math.Sin(Main.GameUpdateCount * 0.16f + seed * 1.7f);
				Vector2 wob = nrm * (wander * wobble);
				Span<Vector2> auraBuf = stackalloc Vector2[MaxSegs + 1];
				for (int i = 0; i <= segs; i++)
					auraBuf[i] = run[i] + wob;
				DrawFilament(auraBuf.Slice(0, segs + 1), color, alpha * 0.30f, width * 3.20f, width * 1.00f, 0.72f);
			}
			DrawFilament(run, color, alpha * 0.72f, width * 1.30f, width * 0.52f, 0.80f);

			// BRAID. Real discharges read as two or three strands twisting inside
			// one aura; a single strand reads as a wire. The braid is the same
			// channel read with a second seed at partial amplitude, so it hugs the
			// bolt instead of wandering off it.
			Span<Vector2> braidBuf = stackalloc Vector2[MaxSegs + 1];
			for (int i = 0; i <= segs; i++) {
				float f = i / (float)segs;
				braidBuf[i] = from + dir * (len * f) + nrm * (
					ChannelOffset(f, len, width, seed) * 0.44f
					+ ChannelOffset(f, len, width * 0.8f, seed + 505) * 0.56f);
			}
			braidBuf[0] = from;
			braidBuf[segs] = to;
			DrawFilament(braidBuf.Slice(0, segs + 1), hot, alpha * 0.85f, width * 0.40f, width * 0.30f, 0f);
			DrawFilament(braidBuf.Slice(0, segs + 1), hot, alpha * 0.55f, width * 0.90f, width * 0.55f, 0.35f);

			// CORONA. Short needles thrown off the channel, alternating sides,
			// positioned off the channel's own offset function so they grow out of
			// the bolt. This is the single biggest tier tell in the whole file: a
			// bolt with a clean edge looks DRAWN, a bolt with a fuzzy edge looks
			// like electricity. Deterministic in `i` - no per-frame re-roll.
			if (width > 2.2f) {
				int needles = Math.Clamp((int)(len / 36f), 2, 7);
				for (int i = 0; i < needles; i++) {
					float f = 0.14f + 0.72f * ((i + 0.5f) / needles);
					Vector2 at = from + dir * (len * f) + nrm * ChannelOffset(f, len, width, seed);
					float side = i % 2 == 0 ? 1f : -1f;
					float nl = width * (0.85f + 0.75f * ((i * 0.37f) % 1f));
					Bloom(at + nrm * side * (nl * 0.5f), width * 1.25f, color, alpha * 0.20f);
					Bloom(at + nrm * side * nl, width * 0.85f, color, alpha * 0.12f);
				}
			}

			DrawFilament(run, hot, alpha * 0.95f, width * 0.46f, width * 0.32f, 0f);

			if (headBloom) {
				// Strike head: a hot core, a wide aura, and a flattened wash that
				// sells the landing without ever being a rectangle.
				Bloom(to, width * 5.4f, Color.Lerp(color, Color.White, 0.45f), alpha * 0.40f);
				Bloom(to, width * 2.1f, Color.White, alpha * 0.34f);
				Shape(ShapeId.SplatSoft, to, dir.ToRotation(), width * 9.5f, width * 2.4f,
					Color.Lerp(color, Color.White, 0.25f) * (alpha * 0.20f));
			}
		}

		/// <summary>
		/// "SKYFANG" - the storm discharge archetype, and the single lightning look
		/// the whole tech tree shares.
		///
		/// A discharge is not a line and it is not a trail. It is one run of
		/// jagged light with volume, plus a couple of short barbs hung near the
		/// head and a strike bloom where it lands. Every layer rides the SAME
		/// channel, and the channel keeps ONE shape for the bolt's whole life, so
		/// the whole thing composites into a single flash of electricity.
		///
		/// Callers must pass a BOUNDED tail. A bolt is a dart of plasma a few
		/// tiles long, never a smear drawn all the way back to where it was born.
		/// </summary>
		public static void Skyfang(Vector2 tail, Vector2 head, Color color, float alpha,
			float width, int seed, int barbs = 2)
		{
			Vector2 seg = head - tail;
			float len = seg.Length();
			if (alpha <= 0.01f || width <= 0.05f || len < 10f)
				return;
			Vector2 dir = seg / len;
			Vector2 nrm = new(-dir.Y, dir.X);

			// The run itself: this is the bolt.
			DischargeRun(tail, head, color, alpha, width, seed, true);

			// RUNNER: a small, very bright head sliding down the channel on a fixed
			// cycle, reading off the same offset function so it stays ON the bolt.
			// This is the layer that sells "current is flowing" instead of "someone
			// drew a zigzag".
			float rf = (Main.GameUpdateCount * 0.05f + seed * 0.137f) % 1f;
			Vector2 rp = Vector2.Lerp(tail, head, rf) + nrm * ChannelOffset(rf, len, width, seed);
			Bloom(rp, width * 2.2f, Color.White, alpha * 0.28f);
			Bloom(rp, width * 4.6f, Color.Lerp(color, Color.White, 0.5f), alpha * 0.15f);

			// Barbs: few, short, and hung ON the channel (same offset function the
			// run used) so a spur grows out of the bolt instead of floating beside
			// it. Depth is capped at one level - a barb never spawns its own barb.
			int n = Math.Clamp(barbs, 0, 3);
			for (int i = 0; i < n; i++) {
				float f = 0.52f + 0.20f * i;
				Vector2 at = Vector2.Lerp(tail, head, f) + nrm * ChannelOffset(f, len, width, seed);
				float side = i % 2 == 0 ? 1f : -1f;
				float ang = dir.ToRotation() + side * (0.62f + 0.24f * i);
				float blen = MathHelper.Clamp(len * 0.24f, 26f, 96f);
				DischargeRun(at, at + ang.ToRotationVector2() * blen, color, alpha * 0.62f,
					width * 0.42f, seed + i * 37 + 11, true);
			}
		}

		/// <summary>
		/// Skyfang along an EXISTING polyline: the trail form. Use this when the
		/// bolt has a recorded flight path (a chain that re-aims, a discharge that
		/// crawled) - the shape is then the PATH's shape, so a re-aiming strike
		/// bends around the corner it turned instead of snapping straight.
		/// </summary>
		public static void SkyfangPath(ReadOnlySpan<Vector2> path, Color color, float alpha,
			float width, int seed, int barbs = 2)
		{
			if (path.Length < 2 || alpha <= 0.01f || width <= 0.05f)
				return;
			Color hot = Color.Lerp(color, Color.White, 0.82f * MathHelper.Clamp(alpha, 0f, 1f));

			DrawFilament(path, color, alpha * 0.30f, width * 3.20f, width * 1.00f, 0.72f);
			DrawFilament(path, color, alpha * 0.72f, width * 1.30f, width * 0.52f, 0.80f);
			DrawFilament(path, hot, alpha * 0.95f, width * 0.46f, width * 0.32f, 0f);

			Vector2 head = path[path.Length - 1];
			Vector2 dir = (head - path[path.Length - 2]).SafeNormalize(Vector2.UnitX);

			// RUNNER + CORONA, same layers the straight form gets, sampled off the
			// recorded channel so a re-aiming bolt keeps them as it bends. This is
			// what the chain strike is drawn with, so it gets both.
			float rf = (Main.GameUpdateCount * 0.05f + seed * 0.137f) % 1f;
			int strip = path.Length - 1;
			int ri = Math.Clamp((int)(rf * strip), 0, strip - 1);
			float rl = rf * strip - ri;
			Vector2 rp = Vector2.Lerp(path[ri], path[ri + 1], rl);
			Bloom(rp, width * 2.2f, Color.White, alpha * 0.28f);
			Bloom(rp, width * 4.6f, Color.Lerp(color, Color.White, 0.5f), alpha * 0.15f);

			if (width > 2.2f) {
				int needles = Math.Clamp(strip / 2, 2, 8);
				for (int i = 0; i < needles; i++) {
					int idx = Math.Clamp((int)((i + 0.5f) / needles * strip), 0, strip - 1);
					Vector2 at = Vector2.Lerp(path[idx], path[idx + 1], 0.5f);
					Vector2 tan = (path[idx + 1] - path[idx]).SafeNormalize(Vector2.UnitX);
					Vector2 nn = new(-tan.Y, tan.X);
					float side = i % 2 == 0 ? 1f : -1f;
					float nl = width * (0.85f + 0.75f * ((i * 0.41f) % 1f));
					Bloom(at + nn * side * (nl * 0.5f), width * 1.25f, color, alpha * 0.20f);
					Bloom(at + nn * side * nl, width * 0.85f, color, alpha * 0.12f);
				}
			}

			int n = Math.Clamp(barbs, 0, 3);
			for (int i = 0; i < n; i++) {
				// Barb roots ride the recorded path, so a spur grows out of the
				// bolt instead of floating beside it.
				int idx = Math.Clamp(path.Length - 3 - i * 2, 0, path.Length - 1);
				Vector2 at = path[idx];
				float side = i % 2 == 0 ? 1f : -1f;
				float ang = dir.ToRotation() + side * (0.62f + 0.24f * i);
				float blen = MathHelper.Clamp(24f + width * 4f, 26f, 96f);
				DischargeRun(at, at + ang.ToRotationVector2() * blen, color, alpha * 0.62f,
					width * 0.42f, seed + i * 37 + 11, true);
			}

			Bloom(head, width * 5.4f, Color.Lerp(color, Color.White, 0.45f), alpha * 0.40f);
			Bloom(head, width * 2.1f, Color.White, alpha * 0.34f);
			Shape(ShapeId.SplatSoft, head, dir.ToRotation(), width * 9.5f, width * 2.4f,
				Color.Lerp(color, Color.White, 0.25f) * (alpha * 0.20f));
		}

		/// <summary>
		/// Inward vortex built entirely from light. Each arm is a chain of soft
		/// additive glows that fatten at the rim and shrink toward the core, so
		/// the swirl is overlapping radiance - never a stroked curve. A mouth
		/// halo, a counter-rotating under-layer, comet heads racing down the
		/// throat, infalling sparks and a stacked bright core finish the funnel.
		/// </summary>
		public static void Converge(Vector2 world, float outerR, float innerR, Color color, float alpha,
			int count, float spin, float speed, float sizeMul = 1f, float spiral = 2.7f)
		{
			if (color.A == 0 || alpha <= 0.01f || outerR <= innerR + 4f)
				return;
			float time = Main.GameUpdateCount * 0.016f;
			float flow = time * MathHelper.Clamp(speed, 0.15f, 3f);
			int arms = Math.Clamp(count / 3, 4, 6);

			// A vortex is not a bent hose. Two properties make it read as swirling
			// MATTER instead of a rigid pinwheel:
			//   1) LOG-SPIRAL radius stepping - the radius shrinks GEOMETRICALLY,
			//      so the pitch angle stays constant and the arm winds evenly
			//      instead of ballooning out at the rim. The glow size tapers
			//      with the same curve, so the overlap between neighbouring blobs
			//      never changes along the arm and the swirl stays seamless.
			//   2) DIFFERENTIAL rotation (Keplerian shear, omega ~ r^-1.5): the
			//      throat outruns the rim, so the arms visibly shear and wrap
			//      rather than turning like one solid disc.
			float ratio = MathHelper.Max(0.04f, innerR / outerR);
			Vector2 point(float spinAng, float armOff, float u, float dirSign)
			{
				float r = outerR * (float)Math.Pow(ratio, u);
				float ang = spinAng * MathHelper.Lerp(1.30f, 0.72f, u) + armOff + u * spiral * dirSign;
				return world + ang.ToRotationVector2() * r;
			}

			Color hot = Color.Lerp(color, Color.White, 0.55f);
			float rimGlow = 30f * sizeMul;
			float core = MathHelper.Max(24f, innerR * 3f) * sizeMul;

			// The mouth: a wide soft halo so the funnel's edge is made of light,
			// plus one soft art ring anchoring where the vortex begins.
			Bloom(world, outerR * 2.8f * sizeMul, color, alpha * 0.16f);
			Ring(world, outerR * 0.96f, spin, alpha * 0.32f, color);

			// Arms: radiance that tapers as it winds in - the swirl IS the glow.
			for (int arm = 0; arm < arms; arm++) {
				float armOff = arm * MathHelper.TwoPi / arms;
				const int steps = 14;
				for (int s = 0; s <= steps; s++) {
					float u = s / (float)steps;
					float w = MathHelper.Lerp(rimGlow, 7f * sizeMul, u);
					Bloom(point(spin, armOff, u, 1f), w, color, alpha * (0.32f - 0.17f * u));
					if ((s & 1) == 0)
						Bloom(point(spin, armOff, u, 1f), w * 0.5f, hot, alpha * (0.28f - 0.15f * u));
				}
				// Comet heads racing down the throat.
				int heads = Math.Max(2, count / 8);
				for (int h = 0; h < heads; h++) {
					float u = (flow * 0.8f + (h + 0.5f) / heads + arm * 0.23f) % 1f;
					float fade = (float)Math.Sin(u * Math.PI);
					if (fade <= 0.05f)
						continue;
					Bloom(point(spin, armOff, u, 1f), (18f + 12f * (1f - u)) * sizeMul, hot, alpha * fade * 0.6f);
				}
			}

			// Counter-rotating under-layer: fills the gaps so the stream reads as
			// one continuous swirl instead of a handful of spokes.
			int back = Math.Max(3, arms - 1);
			for (int arm = 0; arm < back; arm++) {
				float armOff = arm * MathHelper.TwoPi / back;
				const int steps = 12;
				for (int s = 0; s <= steps; s++) {
					float u = s / (float)steps;
					Bloom(point(-spin * 0.7f, armOff, u, -1f),
						MathHelper.Lerp(rimGlow * 0.7f, 6f * sizeMul, u),
						color, alpha * (0.18f - 0.10f * u));
				}
			}

			// Core: two stacked blooms so the throat bottoms out in light.
			Bloom(world, core, Color.Lerp(color, Color.White, 0.4f), alpha * 0.55f);
			Bloom(world, core * 0.5f, Color.White, alpha * 0.45f);

			// Infalling sparks give the funnel real motion, not just a spin.
			if (alpha > 0.25f && Main.GameUpdateCount % 3 == 0) {
				for (int i = 0; i < 3; i++) {
					float a = spin + Main.rand.NextFloat(MathHelper.TwoPi);
					Vector2 at = world + a.ToRotationVector2()
						* outerR * MathHelper.Lerp(0.55f, 1f, Main.rand.NextFloat());
					Vector2 inward = (world - at).SafeNormalize(Vector2.UnitX);
					YoyoParticles.SpawnSpark(at, inward.RotatedBy(0.6f) * 1.8f + inward * 1.2f,
						color, 0.5f * sizeMul, 18, 0.96f);
				}
			}
		}

		/// <summary>Thin soft bar (delegates to the shared streak primitive).</summary>
		private static void DrawBar(Vector2 mid, float rot, float length, float width, Color color, float alpha)
		{
			if (alpha <= 0.01f)
				return;
			MiniYoyoFx.DrawSoftBar(mid, rot, length, width, color, alpha);
		}

		/// <summary>
		/// Outward comet stream along a direction spread: soft tapered
		/// streak + round head per lane. Reads as thrown light, never as
		/// stamped rods.
		/// </summary>
		public static void Stream(Vector2 world, float rot, float spread, float reach, Color color, float alpha,
			int count, float speed, float sizeMul = 1f, float phase = 0f)
		{
			if (color.A == 0 || alpha <= 0.01f || reach <= 6f)
				return;
			int lanes = Math.Clamp(count / 3, 2, 5);
			float time = Main.GameUpdateCount * 0.016f * MathHelper.Clamp(speed, 0.15f, 3f);
			for (int i = 0; i < lanes; i++) {
				float lane = lanes == 1 ? 0f : i / (float)(lanes - 1) - 0.5f;
				float a = rot + lane * spread;
				float f = (time + i * 0.29f + phase) % 1f;
				float fade = (float)Math.Sin(f * Math.PI);
				if (fade <= 0.03f)
					continue;
				Vector2 dir = a.ToRotationVector2();
				float r = MathHelper.Lerp(6f, reach, f);
				float tailLen = (26f + 38f * f) * sizeMul;
				Vector2 head = world + dir * r;
				Vector2 tail = world + dir * MathHelper.Max(0f, r - tailLen);
				DrawBar((head + tail) * 0.5f, a, tailLen + 8f * sizeMul, (7f + 6f * f) * sizeMul,
					color, alpha * fade * 0.5f);
				Bloom(head, (13f + 13f * f) * sizeMul, Color.Lerp(color, Color.White, 0.3f), alpha * fade * 0.6f);
			}
		}

		/// <summary>Loose motes drifting up and outward (ambient field life).</summary>
		public static void Motes(Vector2 world, float radius, Color color, float alpha, int count, float speed, float sizeMul = 1f)
		{
			if (color.A == 0 || alpha <= 0.01f || count < 1)
				return;
			float t = Main.GameUpdateCount * speed * 0.01f;
			for (int i = 0; i < count; i++) {
				float f = (t + i * 0.6831f) % 1f;
				float ang = i * 2.399963f + f * 1.4f;
				Vector2 p = world + ang.ToRotationVector2() * (radius * (0.25f + 0.75f * f));
				p.Y -= f * radius * 0.45f;
				float fade = (float)Math.Sin(f * Math.PI);
				if (fade <= 0.03f)
					continue;
				float s = (12f + 9f * f) * sizeMul;
				Bloom(p, s, color, alpha * fade * 0.5f);
				DrawBar(p - new Vector2(0f, s * 0.55f), -MathHelper.PiOver2, s * 1.5f, s * 0.4f,
					color, alpha * fade * 0.35f);
			}
		}

		// =====================================================================
		//  Spawn-side kits (pooled; safe from AI, no-ops on servers).
		// =====================================================================

		/// <summary>Hit accent: sparks + a white streak + a soft splat stamp.</summary>
		public static void Impact(Vector2 world, Color color, float scale, int owner = -1, float angle = 0f)
		{
			if (Main.dedServ)
				return;
			YoyoParticles.SpawnBurst(world, (int)(7f * scale), 3.6f * scale, color, 0.95f, 18);
			YoyoParticles.SpawnSlashLine(world, angle, 30f * scale, color, 4f * scale, 8);
			YoyoParticles.SpawnSlashLine(world, angle, 26f * scale, Color.White, 1.6f * scale, 7);
			MiniBossShapeBank.Spawn(ShapeId.SplatSoft, world, angle, 0.45f * scale, color * 0.85f, 14,
				Main.rand.NextVector2Circular(0.6f, 0.6f), 0f, 0.02f);
			MiniYoyoFx.ImpactFX(world, color, scale, owner, angle, 1);
		}

		/// <summary>
		/// A lightning bite. Deliberately NOT a Pop+Bloom stack: that pair draws
		/// a soft disc and a ring on top of each other and reads as two ugly
		/// circles. A crossed star, a short ionised fan and sparks give the same
		/// "it landed" beat without any disc. The optional ring is reserved for
		/// the Static Charge payoff, where the circle is the mechanic itself.
		/// </summary>
		public static void LightningHit(Vector2 world, Color accent, float power, float rot = 0f,
			bool discharge = false)
		{
			if (Main.dedServ)
				return;
			Color hot = Color.Lerp(accent, Color.White, 0.62f);
			QueueStarFlare(world, hot, 20f + 16f * power,
				rot + Main.GameUpdateCount * 0.017f, 0.6f);
			Fan(ShapeId.WispTaper, world, accent, 4, 2.2f + 1.6f * power, 14, 0.42f,
				rot, MathHelper.PiOver2);
			YoyoParticles.SpawnBurst(world, 5, 3.3f + 1.2f * power, accent, 0.85f, 14);
			if (discharge) {
				QueueRing(world, 20f + 10f * power, hot, 0.34f, 14, 1.15f,
					rot + Main.GameUpdateCount * 0.03f);
			}
		}

		/// <summary>Ground mark: a scorch blot plus (optionally) a stress crack.</summary>
		public static void Scorch(Vector2 world, Color color, float scale, bool crack = false)
		{
			if (Main.dedServ)
				return;
			MiniYoyoFx.GroundScorch(world, color, scale);
			YoyoParticles.SpawnDecal(world, YoyoParticles.DecalStyle.Soft, new Color(28, 22, 34), 1.5f * scale, 0.4f, 200);
			if (crack)
				YoyoParticles.SpawnDecal(world, YoyoParticles.DecalStyle.Crack, color, 1.4f * scale, 0.5f, 220,
					Main.rand.NextFloat(MathHelper.TwoPi));
		}

		/// <summary>Cast burst at the caster: ring implosion + petal fan + light.</summary>
		public static void CastFlash(Vector2 world, Color color, float scale)
		{
			if (Main.dedServ)
				return;
			YoyoParticles.SpawnRing(world, 46f * scale, 12f * scale, 16, color, 2.6f);
			YoyoParticles.SpawnBloom(world, 8f * scale, 54f * scale, 18, color, 0.4f);
			MiniBossShapeBank.SpawnFan(ShapeId.SmearCrescent, world, color * 0.8f, 7, 3.0f * scale, 24, 0.30f * scale);
			Lighting.AddLight(world, color.ToVector3() * 0.9f);
		}

		/// <summary>Debris fan of a given shape (petals, embers, shards).</summary>
		public static void Fan(ShapeId id, Vector2 world, Color color, int count, float speed, int life,
			float scale, float baseRot = 0f, float spread = MathHelper.TwoPi)
		{
			if (Main.dedServ)
				return;
			MiniBossShapeBank.SpawnFan(id, world, color, count, speed, life, scale, baseRot, spread);
		}

		public static void Petals(Vector2 world, Color color, int count, float speed, int life, float scale)
			=> Fan(ShapeId.SmearCrescent, world, color, count, speed, life, scale);

		public static void Embers(Vector2 world, Color color, int count, float speed, int life, float scale, float rot = 0f, float spread = MathHelper.TwoPi)
			=> Fan(ShapeId.EmberCurl, world, color, count, speed, life, scale, rot, spread);

		public static void Shards(Vector2 world, Color color, int count, float speed, int life, float scale, float rot = 0f, float spread = MathHelper.TwoPi)
			=> Fan(ShapeId.NeedleLong, world, color, count, speed, life, scale, rot, spread);
	}
}
