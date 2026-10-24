using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Cursor puddle - the shared body of the whole well family. Every mode is
	/// cast AT the cursor: a tapered beam snaps from the caster to the exact
	/// cursor point, stops there, and blooms into a layered whirlpool. What the
	/// well does afterwards is the mode:
	///
	///   Vertex   - the eye auto-fires instant micro-beams and pulses its bowl.
	///   Steer    - lobs orbs you steer with the cursor (max turn angle).
	///   Seek     - lobs homing orbs.
	///   Rapid    - a fast straight stream at whatever is nearest.
	///   Worm     - drips a growing worm that hunts, pierces and dies with the well.
	///   Split    - spits fracture seeds that burst into shards.
	///   Storm    - a crackling bowl that spits chain lightning at the nearest
	///              victims; with Vertex (micro-beams) and Rapid (stream) the
	///              pool now covers all three fire flavours.
	///
	/// Spawns are owner-side; damage rides the child projectiles (or the
	/// server-authority pulse helpers). The pool dries out over its last frames.
	/// </summary>
	public class MagicalPuddleProj : ModProjectile
	{
		public const float ModeVertex = 0f;
		public const float ModeSteer = 1f;
		public const float ModeSeek = 2f;
		public const float ModeRapid = 3f;
		public const float ModeWorm = 4f;
		public const float ModeSplit = 5f;
		public const float ModeStorm = 6f;

		private const int BeamTicks = 15;
		private const int LifeTicks = 210;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float mode;
		private float power;
		private int age;
		private int fireClock;
		private int pulseClock;
		private float spin;
		private Color accent = new(150, 180, 255);
		private Vector2 origin;
		private Vector2 aimDir;
		private Vector2 lastFireDir = Vector2.UnitX;
		private int wormIndex = -1;

		public override void SetDefaults()
		{
			Projectile.width = 26;
			Projectile.height = 26;
			Projectile.friendly = false; // the payloads carry the damage
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = LifeTicks;
			Projectile.netImportant = true;
		}

		/// <summary>The spawn velocity carries the offset back to the caster, so
		/// the pool can be born exactly on the cursor point and still draw the
		/// arrival beam. Captured before anything can move the projectile.</summary>
		public override void OnSpawn(IEntitySource source)
		{
			origin = Projectile.Center - Projectile.velocity;
			if (Projectile.velocity.LengthSquared() < 0.01f)
				origin = Projectile.Center;
			Projectile.velocity = Vector2.Zero;
		}

		private int Cadence => mode == ModeRapid ? 7
			: mode == ModeVertex ? 30
			: mode == ModeSteer ? 15
			: mode == ModeSeek ? 14
			: mode == ModeSplit ? 34
			: mode == ModeStorm ? 16
			: 24;

		public override void AI()
		{
			mode = Projectile.ai[0];
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);

			if (age++ == 0) {
				aimDir = (Projectile.Center - origin).SafeNormalize(Vector2.UnitX);
				if (!Main.dedServ) {
					// The snap has to LAND: white flash + arrival ring + a crown
					// of light thrown up out of the new pool.
					MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.95f);
					MagicalFx.CastFlash(Projectile.Center, accent, 0.9f);
					YoyoParticles.SpawnRing(Projectile.Center, 10f, 110f, 16, accent, 3.2f);
					MagicalFx.Fan(ShapeId.WispTaper, Projectile.Center, accent, 9, 6.5f, 22, 0.5f);
					MiniYoyoFx.Shake(2, 6, Projectile.Center);
					SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = -0.2f }, Projectile.Center);
				}
			}

			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.9f);
			if (!Main.dedServ && Main.rand.NextBool(3)) {
				YoyoParticles.SpawnSpark(Projectile.Center + Main.rand.NextVector2Circular(20f, 12f),
					Main.rand.NextVector2Circular(1.4f, 0.7f) - Vector2.UnitY * 0.9f,
					accent, 0.7f, 18, 0.94f);
			}

			// THE SNAP BEAM HITS. While the arrival beam is drawn it is a weapon,
			// not a light show: one clean strike along its whole length, at one
			// authored beat (not a per-tick tickle, so a single cast can never
			// chew the same enemy repeatedly). This closes the gap where a beam
			// visibly crossed an enemy and did nothing. The pool itself stays
			// friendly = false - its payloads carry the rest of the damage.
			if (age == BeamTicks - 4) {
				Player beamOwner = OwnerOf();
				if (beamOwner != null) {
					MagicalReleaseCommon.LineHit(beamOwner, BeamFrom(beamOwner), Projectile.Center,
						14f + 10f * power, Math.Max(1, Projectile.damage), 2f, accent);
				}
			}

			if (mode == ModeWorm) {
				// The brood well births exactly one worm.
				if (age == 46)
					SpawnWorm();
				if (!Main.dedServ && age % 9 == 0) {
					Vector2 drip = Projectile.Center + Main.rand.NextVector2Circular(18f, 10f);
					MiniBossShapeBank.Spawn(ShapeId.SplatSoft, drip, 0f, new Vector2(12f / 64f, 18f / 64f),
						accent * 0.45f, 22, Main.rand.NextVector2Circular(0.3f, 0.3f), 0f, 0.01f);
				}
				return;
			}

			if (age <= BeamTicks || age >= LifeTicks - 26)
				return;

			fireClock++;
			if (fireClock >= Cadence) {
				fireClock = 0;
				Fire();
			}

			if (mode == ModeVertex && ++pulseClock >= 40) {
				pulseClock = 0;
				PulseBowl();
			}
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		/// <summary>
		/// Where the arrival beam starts. Remote clients receive the pool with
		/// its velocity already zeroed, which collapses the captured origin onto
		/// the pool itself, so the caster's position is the fallback - the same
		/// rule the draw uses, so the beam that damages is the beam that is drawn.
		/// </summary>
		private Vector2 BeamFrom(Player caster)
		{
			return Vector2.DistanceSquared(origin, Projectile.Center) < 16f
				? caster.Center
				: origin;
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

		private void Fire()
		{
			Player owner = OwnerOf();
			if (owner == null)
				return;
			int dmg = Math.Max(1, Projectile.damage);
			int star = ModContent.ProjectileType<MagicalStarProj>();
			float src = Projectile.ai[2];

			switch ((int)mode) {
				case (int)ModeVertex: {
					NPC target = NearestTarget(560f);
					if (target == null)
						return;
					Vector2 to = target.Center - Projectile.Center;
					Vector2 dir = to.SafeNormalize(Vector2.UnitX);
					lastFireDir = dir;
					MagicalReleaseCommon.Spawn(owner, Projectile.Center + dir * 16f, to,
						ModContent.ProjectileType<MagicalPulseBeamProj>(), Math.Max(1, dmg / 4), 1f, power, src);
					break;
				}
				case (int)ModeSteer: {
					// Rosette launch: the orb leaves on a rotating spoke and then
					// bends to wherever the cursor is, live.
					spin += 0.83f;
					Vector2 dir = spin.ToRotationVector2();
					lastFireDir = dir;
					MagicalReleaseCommon.Spawn(owner, Projectile.Center + dir * 14f, dir * 10.5f, star, dmg,
						MagicalStarProj.ModeSteer, power, src);
					break;
				}
				case (int)ModeSeek: {
					NPC target = NearestTarget(640f);
					Vector2 dir = target != null
						? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
						: (aimDir + Main.rand.NextVector2Circular(0.7f, 0.2f)).SafeNormalize(Vector2.UnitY);
					lastFireDir = dir;
					MagicalReleaseCommon.Spawn(owner, Projectile.Center + dir * 14f, dir * 9.5f, star, dmg,
						MagicalStarProj.ModeHoming, power, src);
					break;
				}
				case (int)ModeRapid: {
					NPC target = NearestTarget(580f);
					Vector2 dir = target != null
						? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
						: aimDir;
					lastFireDir = dir;
					MagicalReleaseCommon.Spawn(owner, Projectile.Center + dir * 14f, dir * 14.5f,
						ModContent.ProjectileType<MagicalRapidBoltProj>(), dmg, power, src, 0f);
					break;
				}
				case (int)ModeStorm: {
					NPC target = NearestTarget(620f);
					Vector2 to = target != null
						? target.Center - Projectile.Center
						: aimDir * 400f;
					Vector2 dir = to.SafeNormalize(aimDir);
					lastFireDir = dir;
					int idx = MagicalReleaseCommon.Spawn(owner, Projectile.Center + dir * 16f,
						dir * MagicalLightningProj.BoltSpeed,
						ModContent.ProjectileType<MagicalLightningProj>(), dmg,
						MagicalLightningProj.ModeChain, power, src);
					if (idx >= 0 && idx < Main.maxProjectiles)
						Main.projectile[idx].localAI[1] = 0f;
					break;
				}
				default: { // ModeSplit
					Vector2 dir = aimDir;
					if (Projectile.owner == Main.myPlayer)
						dir = (Main.MouseWorld - Projectile.Center).SafeNormalize(aimDir);
					lastFireDir = dir;
					MagicalReleaseCommon.Spawn(owner, Projectile.Center + dir * 12f, dir * 8.6f,
						ModContent.ProjectileType<MagicalSeedProj>(), dmg, 0f, power, src);
					break;
				}
			}

			if (!Main.dedServ)
				Muzzle(Projectile.Center + lastFireDir * 18f, lastFireDir);
		}

		private static void Muzzle(Vector2 at, Vector2 dir)
		{
			MagicalFx.CastFlash(at, Color.White, 0.4f);
			MagicalFx.Fan(ShapeId.SplatSoft, at, Color.White, 2, 1.6f, 14, 0.5f, dir.ToRotation(), 0.7f);
		}

		private void PulseBowl()
		{
			MagicalReleaseCommon.RadialHit(OwnerOf(), Projectile.Center, 84f,
				Math.Max(1, Projectile.damage / 2), 1f, pierceArmor: true);
			if (Main.dedServ)
				return;
			YoyoParticles.SpawnRing(Projectile.Center, 12f, 96f, 18, accent, 2.6f);
			MagicalFx.Pop(Projectile.Center, 92f, accent, 0.32f);
			MiniYoyoFx.FlashFX(Projectile.Center, accent, 0.35f);
		}

		private void SpawnWorm()
		{
			Player owner = OwnerOf();
			if (owner == null)
				return;
			int idx = MagicalReleaseCommon.Spawn(owner, Projectile.Center, Vector2.Zero,
				ModContent.ProjectileType<MagicalWormProj>(), Projectile.damage,
				Projectile.whoAmI, power, Projectile.ai[2]);
			if (idx >= 0)
				wormIndex = idx;
			if (Main.dedServ)
				return;
			MagicalFx.Scorch(Projectile.Center, accent, 0.9f, crack: true);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 5, 3.4f, 20, 0.6f);
			MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.8f);
			MiniYoyoFx.Shake(2, 6, Projectile.Center);
		}

		public override void OnKill(int timeLeft)
		{
			// The worm is part of the well: when the bowl dries it burrows away.
			if (wormIndex >= 0 && wormIndex < Main.maxProjectiles) {
				Projectile worm = Main.projectile[wormIndex];
				if (worm.active && worm.type == ModContent.ProjectileType<MagicalWormProj>())
					worm.Kill();
			}
			if (Main.dedServ)
				return;
			YoyoParticles.SpawnRing(Projectile.Center, 8f, 72f, 16, accent, 2.8f);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 4, 2.8f, 18, 0.5f);
			MagicalFx.Pop(Projectile.Center, 60f, accent, 0.3f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			Color hot = Color.Lerp(accent, Color.White, 0.6f);
			float fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);

			// ---- The snap beam: a clean tapered run that ends AT the pool ----
			if (age <= BeamTicks && fade > 0f) {
				// Remote clients receive the pool with its velocity already
				// zeroed, so the captured origin can collapse onto the pool.
				// Fall back to the caster's position so the snap still reads.
				Vector2 from = origin;
				if (Vector2.DistanceSquared(from, Projectile.Center) < 16f) {
					Player caster = OwnerOf();
					if (caster != null)
						from = caster.Center;
				}
				float t = 1f - age / (float)BeamTicks;
				float dist = Vector2.Distance(from, Projectile.Center);
				Vector2 mid = (from + Projectile.Center) * 0.5f;
				float rot = (Projectile.Center - from).ToRotation();
				MiniYoyoFx.DrawSoftBar(mid, rot, dist, (18f + 22f * power) * (0.35f + 0.65f * t),
					accent, 0.18f + 0.42f * t);
				MiniYoyoFx.DrawSoftBar(mid, rot, dist * 0.94f, (7f + 9f * power) * (0.4f + 0.6f * t),
					hot, 0.5f * t);
				float rr = MathHelper.Lerp(12f, 96f, age / (float)BeamTicks);
				MagicalFx.Ring(Projectile.Center, rr, t2 * 0.02f, 0.45f * t, accent);
				MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 42f + 44f * (1f - t), hot, 0.5f * t);
				// Where the beam LANDS: a flaring mouth at the pool, so the
				// strike reads as arriving into it instead of being drawn over
				// it. (The beam also lands damage on one authored beat - see the
				// snap-beam block in AI.)
				MagicalFx.Bloom(Projectile.Center, 34f + 26f * power, hot, 0.24f * t);
				MagicalFx.StarFlare(Projectile.Center, Color.Lerp(accent, Color.White, 0.75f),
					20f + 16f * power, rot, 0.34f * t);
			}

			// ---- The pool: a layered swirl, kicked on every shot ----
			float pulse = 1f + 0.08f * (float)Math.Sin(t2 * 0.18f);
			float kick = fireClock <= 2 ? (3f - fireClock) / 3f : 0f;
			float r = (56f + 16f * power) * pulse * (0.88f + 0.12f * fade) + kick * 7f;
			Vector2 c = Projectile.Center;
			MiniYoyoFx.DrawSolidEllipse(c, r * 1.35f, r * 0.70f, 0f, new Color(8, 6, 18), 0.42f * fade);
			MiniYoyoFx.DrawSolidEllipse(c, r * 0.88f, r * 0.48f, 0f, accent, 0.10f * fade);

			// ---- Mode identity inside the bowl ----
			switch ((int)mode) {
				case (int)ModeVertex:
					MagicalFx.Converge(c, r * 0.82f, 8f, accent, 0.55f * fade, 10, t2 * 0.02f, 1.2f);
					MagicalFx.Rune(c, r * 0.34f, t2 * 0.04f, 0.55f * fade, hot, 0.8f);
					MiniYoyoFx.DrawGlowAdditive(c + new Vector2(0f, 2f), r * 0.8f + kick * 20f,
						Color.White, 0.35f * fade + 0.25f * kick);
					break;
				case (int)ModeSteer:
					for (int i = 0; i < 4; i++) {
						float a = t2 * 0.05f + i * MathHelper.PiOver2;
						Vector2 p = c + a.ToRotationVector2() * (r * 0.62f);
						MagicalFx.Shape(ShapeId.SmearCrescent, p, a + MathHelper.PiOver2, 26f, 12f,
							Color.Lerp(accent, Color.White, 0.35f) * (0.5f * fade));
					}
					MagicalFx.Ring(c, r * 0.55f, -t2 * 0.05f, 0.3f * fade, hot);
					break;
				case (int)ModeSeek:
					for (int i = 0; i < 3; i++) {
						float a = t2 * 0.07f + i * MathHelper.TwoPi / 3f;
						Vector2 p = c + a.ToRotationVector2() * (r * 0.55f) + new Vector2(0f, -6f);
						MagicalFx.Bloom(p, 20f, hot, 0.3f * fade);
						MagicalFx.StarFlare(p, Color.White, 12f, a, 0.35f * fade);
					}
					MagicalFx.Converge(c, r * 0.7f, 14f, accent, 0.3f * fade, 6, -t2 * 0.03f, 1.5f);
					break;
				case (int)ModeRapid:
					for (int i = 0; i < 6; i++) {
						float a = t2 * 0.09f + i * MathHelper.TwoPi / 6f;
						Vector2 p = c + a.ToRotationVector2() * (r * 0.7f);
						MagicalFx.Wisp(p, a, 14f + 10f * kick, 4f, hot * (0.35f * fade));
					}
					MagicalFx.Ring(c, r * (0.5f + 0.15f * kick), t2 * 0.12f, 0.28f * fade, accent);
					break;
				case (int)ModeWorm:
					MiniYoyoFx.DrawSolidEllipse(c + new Vector2(0f, 5f), r * 0.9f, r * 0.42f, 0f,
						new Color(10, 6, 16), 0.55f);
					MagicalFx.Ring(c + new Vector2(0f, 5f), r * 0.8f,
						0.4f + (float)Math.Sin(t2 * 0.05f) * 0.1f, 0.30f * fade, accent);
					for (int i = 0; i < 3; i++) {
						float f = (t2 * 0.012f + i * 0.33f) % 1f;
						Vector2 p = c + new Vector2((i - 1f) * 22f, 8f + f * 26f);
						MagicalFx.Shape(ShapeId.SplatSoft, p, i, 10f, 16f * (1f - f * 0.5f),
							accent * (0.3f * (1f - f)));
					}
					break;
				case (int)ModeStorm: {
					// A live storm bowl. Deliberately NOT a ring of arcs seated on
					// the rim: eight symmetric arcs marching around a circle is the
					// "orbiting things" read. A storm is a RADIAL discharge, so the
					// bowl now throws streamers OUT of its core, each one only lit
					// for a slice of its own cycle, plus a granulating plasma core.
					MiniYoyoFx.DrawGlowAdditive(c, r * (0.62f + 0.20f * kick), Color.White,
						0.26f * fade + 0.30f * kick);

					// Plasma core granulation: stable slots, alpha-gated so the heat
					// boils instead of strobing.
					for (int i = 0; i < 6; i++) {
						float a = i * 1.047f + t2 * 0.006f;
						float boil = 0.35f + 0.65f * (float)Math.Sin(t2 * 0.17f + i * 1.7f);
						Vector2 p = c + a.ToRotationVector2() * (r * (0.16f + 0.22f * boil));
						MagicalFx.Bloom(p, 18f + 20f * boil, Color.Lerp(accent, Color.White, 0.6f),
							0.20f * fade * boil);
					}

					// Radial streamers: stable seeds, gated alpha, reaching out past
					// the rim so the bowl looks like it is venting charge.
					for (int i = 0; i < 9; i++) {
						float cycle = (t2 * 0.055f + i * 0.62f) % 1f;
						float lit = (float)Math.Sin(MathHelper.Clamp(cycle * 2.6f, 0f, 1f) * MathHelper.Pi);
						if (lit <= 0.06f)
							continue;
						float a = i * 0.698f + t2 * 0.008f + (float)Math.Sin(i * 3.1f) * 0.35f;
						float reach = r * (0.55f + 0.60f * lit);
						Vector2 p0 = c + a.ToRotationVector2() * (r * 0.12f);
						Vector2 p1 = c + (a + (float)Math.Sin(i * 2.3f) * 0.22f).ToRotationVector2() * reach;
						MagicalFx.Bolt(p0, p1, Color.Lerp(accent, Color.White, 0.6f), 0.62f * fade * lit, 4.6f, 131 + i * 17);
						MagicalFx.Bloom(p1, 16f + 14f * lit, accent, 0.24f * fade * lit);
					}

					// THE SHOT IS ALREADY REAL. AI() spawns an actual chaining
					// MagicalLightningProj down lastFireDir, so this must NOT draw
					// a second, decorative bolt. It used to: a Skyfang built from
					// r*0.8 out along the barrel, while the real bolt is born 16px
					// from the bowl's centre - so the decoration never lined up
					// with the bolt that actually carries the damage. That is the
					// storm reading "off centre" and looking like it fires a
					// custom lightning shape that chains into nothing. All this
					// needs to do is flare at the real spawn point.
					if (kick > 0f) {
						Vector2 throat = c + lastFireDir * 16f;
						MagicalFx.Bloom(throat, 34f + 26f * kick, hot, 0.55f * kick);
						MagicalFx.Fan(ShapeId.BoltZig, throat, hot, 3, 2.4f + 1.4f * kick, 16, 0.6f * kick);
					}
					break;
				}
				default: { // ModeSplit
					MagicalFx.Shape(ShapeId.RingCracked, c, t2 * 0.02f, r * 1.5f, r * 1.5f,
						accent * (0.25f * fade));
					for (int i = 0; i < 3; i++) {
						float a = -t2 * 0.04f + i * MathHelper.TwoPi / 3f;
						Vector2 p = c + a.ToRotationVector2() * (r * 0.62f);
						MagicalFx.Shape(ShapeId.SplatStar, p, a, 18f, 18f, hot * (0.4f * fade));
					}
					break;
				}
			}

			// Collar rings. Everywhere else they rotate; the storm bowl must NOT,
			// because two counter-rotating rings around a bowl of lightning read
			// as ornaments orbiting the effect - which is the exact "ugly orbiting
			// shit" complaint. The storm gets a STATIC containment collar instead,
			// so nothing in it appears to revolve.
			if (mode == (int)ModeStorm) {
				MagicalFx.Ring(c, r * 0.80f, 0f, 0.26f * fade, accent);
				MagicalFx.Ring(c, r * 1.06f, 0f, 0.16f * fade, hot);
			}
			else {
				MagicalFx.Ring(c, r * 0.80f, -t2 * 0.03f, 0.32f * fade, accent);
				MagicalFx.Ring(c, r * 1.06f, t2 * 0.014f, 0.20f * fade, hot);
			}
			MiniYoyoFx.DrawGlowAdditive(c + new Vector2(0f, 4f), r * 1.9f, accent, 0.30f * fade);
			return false;
		}
	}

	/// <summary>
	/// Brand of Ruin. The cursor is the brand: stick it on an enemy and a sigil
	/// bites that enemy every 0.1s; sweep the cursor onto another enemy and the
	/// brand jumps there (the old one puffs out). Over bare ground it becomes a
	/// burning circle instead. Flavours (ember, frost, storm, void) are chosen by
	/// the ability, so different yoyos stamp different marks.
	///
	/// ai[0]: flavour. ai[1]: power01. ai[2]: source identity for the accent.
	/// </summary>
	public class MagicalBrandProj : ModProjectile
	{
		public const float FlavorEmber = 0f;
		public const float FlavorFrost = 1f;
		public const float FlavorStorm = 2f;
		public const float FlavorVoid = 3f;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int age;
		private int pulse;
		private float power;
		private Color accent = new(255, 140, 80);
		private Color hot = new(255, 230, 160);
		private NPC stick;
		private Vector2 jumpFrom;
		private int jumpFlash;
		private int syncClock;
		private int noTarget;
		private float bite;
		private Vector2 arcFrom;
		private Vector2 arcTip;
		private int arcFlash;

		private int Flavor => (int)Projectile.ai[0];
		private float Progress => MathHelper.Clamp(1f - Projectile.timeLeft / 180f, 0f, 1f);

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 180;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			age++;
			pulse++;
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			Palette();

			// The sigil rides the cursor (owner side) so sweeping it across a pack
			// re-stamps the brand. Everyone else follows the synced position.
			if (Projectile.owner == Main.myPlayer) {
				Vector2 want = Main.MouseWorld;
				bool moving = Vector2.DistanceSquared(Projectile.Center, want) > 4f;
				Projectile.Center = Vector2.Lerp(Projectile.Center, want, 0.55f);
				// Throttled: one sync packet every few ticks while travelling,
				// never one per tick.
				if (moving && ++syncClock >= 4) {
					syncClock = 0;
					Projectile.netUpdate = true;
				}
			}

			NPC found = FindUnderCursor();
			if (found != null) {
				noTarget = 0;
				if (found != stick) {
					if (stick != null) {
						jumpFrom = stick.Center;
						jumpFlash = 12;
						if (!Main.dedServ) {
							YoyoParticles.SpawnRing(stick.Center, 6f, 46f, 12, accent, 2f);
							MagicalFx.Bloom(stick.Center, 40f, accent, 0.3f); // per-tick: stays a queued glow
						}
					}
					stick = found;
				}
			}
			else if (stick != null
				&& (++noTarget > 10 || !stick.active || stick.life <= 0)) {
				// The cursor left the pack: the old mark burns off and the brand
				// drops back to the ground under the cursor.
				if (!Main.dedServ) {
					YoyoParticles.SpawnRing(stick.Center, 6f, 40f, 10, accent, 1.8f);
					MagicalFx.Bloom(stick.Center, 36f, accent, 0.25f); // per-tick: stays a queued glow
				}
				stick = null;
				noTarget = 0;
			}

			Vector2 at = stick != null ? stick.Center : Projectile.Center;
			float shimmer = 0.35f + 0.15f * (float)Math.Sin(age * 0.11f);
			Lighting.AddLight(at, accent.ToVector3() * (0.55f + shimmer));

			if (pulse >= 6) {
				pulse = 0;
				Tick(at);
			}
			if (bite > 0f)
				bite = Math.Max(0f, bite - 1f / 6f);

			if (jumpFlash > 0)
				jumpFlash--;
			if (arcFlash > 0)
				arcFlash--;
		}

		private void Palette()
		{
			Color source = MagicalReleaseCommon.AbilityAccent(Projectile);
			Color baseCol = Flavor switch {
				(int)FlavorFrost => new Color(150, 215, 255),
				(int)FlavorStorm => new Color(178, 205, 255),
				(int)FlavorVoid => new Color(178, 120, 255),
				_ => new Color(255, 148, 64),
			};
			accent = Color.Lerp(baseCol, source, 0.28f);
			hot = Color.Lerp(baseCol, Color.White, 0.62f);
		}

		private NPC FindUnderCursor()
		{
			// Hysteresis: if the cursor is still on the current mark, keep it, so
			// two enemies standing together cannot make the sigil flicker.
			if (stick != null && stick.active && stick.life > 0) {
				Rectangle keep = stick.Hitbox;
				keep.Inflate(16, 16);
				if (keep.Contains(Projectile.Center.ToPoint()))
					return stick;
			}
			NPC nearest = null;
			float best = 62f * 62f;
			Point point = Projectile.Center.ToPoint();
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Targetable(npc))
					continue;
				Rectangle box = npc.Hitbox;
				box.Inflate(14, 14);
				if (box.Contains(point))
					return npc;
				float d = Vector2.DistanceSquared(npc.Center, Projectile.Center);
				if (d < best) {
					best = d;
					nearest = npc;
				}
			}
			return nearest;
		}

		private void Tick(Vector2 at)
		{
			bool locked = stick != null && stick.active;
			float reach = locked ? 34f : 58f;
			bite = 1f;
			if (MagicalReleaseCommon.CanMoveNpcs) {
				MagicalReleaseCommon.RadialHit(OwnerOf(), at, reach,
					Math.Max(1, Projectile.damage / (locked ? 2 : 3)), 0.4f, pierceArmor: true);
			}
			if (Main.dedServ)
				return;

			// The bite must be legible: every victim the mark reaches gets a hit
			// pop on screen every 0.1s, so "does it damage?" is answered visibly.
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc))
					continue;
				float rr = reach + Math.Max(npc.width, npc.height) * 0.5f;
				if (Vector2.DistanceSquared(npc.Center, at) > rr * rr)
					continue;
				MagicalFx.Impact(npc.Center, accent, 0.75f, Projectile.owner,
					(npc.Center - at).ToRotation());
			}

			YoyoParticles.SpawnRing(at, 5f, locked ? 44f : 62f, 11, accent, 2.1f);
			MagicalFx.Pop(at, locked ? 44f : 60f, accent, 0.2f + 0.14f * Progress);
			switch (Flavor) {
				case (int)FlavorFrost:
					MagicalFx.Shards(at, hot, 3, 2.4f, 14, 0.8f);
					break;
				case (int)FlavorStorm: {
					// A real arc: the mark strikes the nearest victim, not empty air.
					NPC arc = NearestInRange(at, 260f);
					Vector2 tip = arc != null ? arc.Center : at + Main.rand.NextVector2Circular(34f, 34f);
					arcFrom = at;
					arcTip = tip;
					arcFlash = 6;
					if (arc != null)
						MagicalFx.Impact(arc.Center, hot, 0.7f, Projectile.owner,
							(arc.Center - at).ToRotation());
					break;
				}
				case (int)FlavorVoid:
					YoyoParticles.SpawnBurst(at, 3, 1.7f, accent, 0.5f, 24);
					break;
				default:
					MagicalFx.Embers(at, accent, 4, 2.2f, 22, 0.8f);
					break;
			}
		}

		/// <summary>Nearest valid victim to a point, for flavour FX that must
		/// land on a real enemy (storm arcs).</summary>
		private static NPC NearestInRange(Vector2 from, float range)
		{
			NPC best = null;
			float bestD = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Targetable(npc))
					continue;
				float d = Vector2.DistanceSquared(npc.Center, from);
				if (d < bestD) {
					bestD = d;
					best = npc;
				}
			}
			return best;
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
			Vector2 at = stick != null ? stick.Center : Projectile.Center;
			if (MagicalReleaseCommon.CanMoveNpcs) {
				MagicalReleaseCommon.RadialHit(OwnerOf(), at, 150f,
					Math.Max(1, Projectile.damage * 3 / 2), 4f);
			}
			if (Main.dedServ)
				return;
			float scale = 1.15f + 0.3f * (1f - Progress);
			YoyoParticles.SpawnRing(at, 10f, 160f, 22, accent, 4f);
			MagicalFx.Fan(ShapeId.SplatSoft, at, Color.Lerp(accent, Color.White, 0.4f), 5, 4.2f, 22, 0.6f);
			MagicalFx.Scorch(at, accent, scale, crack: true);
			MiniYoyoFx.FlashFX(at, Color.White, 1.1f);
			MiniYoyoFx.Shake(3, 8, at);
			MagicalFx.ScreenPulse(at, 0.2f, accent);
			switch (Flavor) {
				case (int)FlavorFrost:
					MagicalFx.Shards(at, hot, 12, 4.2f, 26, 1.2f);
					break;
				case (int)FlavorStorm:
					MagicalFx.Fan(ShapeId.NeedleLong, at, hot, 10, 5.4f, 22, 0.7f);
					break;
				case (int)FlavorVoid:
					YoyoParticles.SpawnRing(at, 130f, 10f, 18, accent, 2.6f);
					break;
				default:
					MagicalFx.Embers(at, accent, 14, 4.4f, 30, 1.2f);
					break;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			float ramp = Progress;
			Color drawHot = Color.Lerp(accent, Color.White, 0.35f + 0.4f * ramp);
			bool locked = stick != null && stick.active;
			Vector2 at = locked ? stick.Center : Projectile.Center;

			// Jump streak between the old victim and the new one.
			if (jumpFlash > 0 && locked) {
				float k = jumpFlash / 12f;
				MagicalFx.Bolt(jumpFrom, at, drawHot, 0.55f * k, 2.4f * k, 17);
				MiniYoyoFx.DrawGlowAdditive(jumpFrom, 50f * k, drawHot, 0.3f * k);
			}

			float baseR = locked ? 30f + 8f * power : 46f + 12f * power;
			float r = baseR * (1.15f - 0.25f * ramp);

			// Clean brand rings that breathe in place - no rotating tick marks,
			// nothing decorated around the radius.
			float breathe = 1f + 0.06f * (float)Math.Sin(t2 * 0.12f);
			MagicalFx.Ring(at, r * breathe, 0f, 0.34f + 0.3f * ramp, accent);
			MagicalFx.Ring(at, r * 1.28f * breathe, 0f, 0.18f + 0.18f * ramp, drawHot);
			MagicalFx.Ring(at, r * 0.52f * breathe, 0f, 0.22f + 0.2f * ramp, drawHot);

			// The bite: two jaws close on the mark every 0.1s (bite runs 1 -> 0
			// between ticks), making the damage cadence readable at a glance.
			float open = 0.15f + 0.85f * bite;
			for (int side = -1; side <= 1; side += 2) {
				float jaw = MathHelper.PiOver2 * side;
				MagicalFx.Shape(ShapeId.SmearCrescent,
					at + jaw.ToRotationVector2() * (r * (0.55f + 0.5f * open)),
					jaw + (side < 0 ? 0f : MathHelper.Pi), r * 1.15f, r * 0.7f,
					drawHot * (0.55f * (0.3f + 0.7f * bite)));
			}

			// Pulse read: a white ring snaps outward right after each 0.1s tick.
			float since = pulse / 6f;
			if (since < 0.42f) {
				float k = 1f - since / 0.42f;
				MagicalFx.Ring(at, r * (1f + 0.75f * (1f - k)), 0f, 0.5f * k, Color.White);
				MiniYoyoFx.DrawGlowAdditive(at, 90f * k + 40f, drawHot, 0.35f * k);
			}

			switch (Flavor) {
				case (int)FlavorFrost: {
					// A static crystal crown: spikes fixed to the mark (growing
					// with Progress), plus a rising crystal column. No orbiting.
					for (int i = 0; i < 6; i++) {
						float a = i * MathHelper.TwoPi / 6f + MathHelper.Pi / 6f;
						Vector2 p = at + a.ToRotationVector2() * (r * 0.92f);
						MagicalFx.Shape(ShapeId.NeedleLong, p, a + MathHelper.Pi,
							12f, 26f + 20f * ramp,
							(Color.Lerp(accent, Color.White, 0.5f)) * (0.45f * (0.4f + 0.6f * ramp)));
						MagicalFx.Shape(ShapeId.FlakeSix, p, a, 16f + 8f * ramp, 16f + 8f * ramp,
							accent * (0.28f * (0.35f + 0.65f * ramp)));
					}
					for (int i = 0; i < 3; i++) {
						float f = (t2 * 0.015f + i * 0.33f) % 1f;
						MagicalFx.Shape(ShapeId.NeedleLong, at + new Vector2((i - 1f) * 14f, -f * 44f),
							-MathHelper.PiOver2, 10f, 30f + 18f * (1f - f),
							accent * (0.35f * (1f - f)));
					}
					break;
				}
				case (int)FlavorStorm: {
					// Fixed crackle crown (angles never rotate) + a live arc to a
					// real victim on every tick: the storm is something that
					// HAPPENS, not a decoration.
					for (int i = 0; i < 4; i++) {
						float a = i * MathHelper.PiOver2 + 0.42f;
						Vector2 p0 = at + a.ToRotationVector2() * (r * 1.0f);
						Vector2 p1 = at + (a + 0.95f).ToRotationVector2() * (r * 1.0f);
						MagicalFx.Bolt(p0, p1, drawHot,
							(0.3f + 0.26f * ramp) * (0.55f + 0.45f * bite), 1.6f,
							17 + i * 61 + (int)(t2 * 0.1f));
					}
					if (arcFlash > 0) {
						float ak = arcFlash / 6f;
						MagicalFx.Bolt(arcFrom, arcTip, drawHot, 0.75f * ak, 2.1f * ak,
							(int)(t2 * 7f));
					}
					MiniYoyoFx.DrawGlowAdditive(at, r * 2.2f, accent, 0.18f + 0.12f * ramp);
					break;
				}
				case (int)FlavorVoid: {
					MagicalFx.Converge(at, r * 1.35f, 8f, accent, 0.5f + 0.2f * ramp, 9, -t2 * 0.03f, 1.4f);
					MagicalFx.Shape(ShapeId.RingCracked, at, t2 * 0.02f, r * 1.1f, r * 1.1f,
						(Color.Lerp(accent, Color.Black, 0.4f)) * 0.5f);
					break;
				}
				default: {
					// Ember curls climbing off the mark.
					for (int i = 0; i < 4; i++) {
						float f = (t2 * 0.02f + i * 0.25f) % 1f;
						Vector2 p = at + new Vector2((i - 1.5f) * 12f, -f * 36f);
						MagicalFx.Shape(ShapeId.EmberCurl, p, i * 1.3f + t2 * 0.03f,
							30f + 16f * (1f - f), 22f,
							accent * (0.5f * (1f - f) * (0.5f + 0.5f * ramp)));
					}
					break;
				}
			}

			MagicalFx.Motes(at, r * 1.4f, accent, 0.22f + 0.15f * ramp, 4, 0.9f + (Flavor == (int)FlavorVoid ? 0.6f : 0f));
			MiniYoyoFx.DrawGlowAdditive(at, 70f + 30f * ramp, accent, 0.25f + 0.25f * ramp);
			return false;
		}
	}

	/// <summary>
	/// Fracture Seed: spat toward the cursor direction by a well (or cast on its
	/// own). The seed cracks into three shards; each shard cracks once more into
	/// two weak motes - 1 -> 3 -> 6 - carpeting the spot you aimed at. Children
	/// are spawned by the server (SpawnChild) so hits can never duplicate in MP.
	///
	/// ai[0]: generation (0 seed, 1 shard). ai[1]: power01. ai[2]: source.
	/// </summary>
	public class MagicalSeedProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int age;
		private float power;
		private Color accent = new(150, 230, 170);
		private bool cracked;

		private int Generation => (int)Projectile.ai[0];

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 4;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
			Projectile.timeLeft = 120;
		}

		public override void AI()
		{
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			age++;
			Projectile.rotation += 0.16f;
			Projectile.velocity.Y += Generation == 0 ? 0.06f : 0.035f;

			int fuse = Generation == 0 ? 34 : 24;
			if (age >= fuse) {
				Crack();
				return;
			}

			if (!Main.dedServ && age % 3 == 0)
				YoyoParticles.SpawnSpark(Projectile.Center, -Projectile.velocity * 0.05f, accent, 0.55f, 12, 0.95f);
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.7f);
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			Crack();
			return true;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			Crack();
		}

		private void Crack()
		{
			if (cracked)
				return;
			cracked = true;

			if (!Main.dedServ) {
				// Every split is a visible pop: crack-flash + splinter fan.
				MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.5f);
				MagicalFx.CastFlash(Projectile.Center, accent, 0.5f);
				MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, Color.Lerp(accent, Color.White, 0.3f),
					3, 2.0f, 16, 0.55f);
				YoyoParticles.SpawnRing(Projectile.Center, 4f, 44f, 12, accent, 2f);
			}
			if (MagicalReleaseCommon.CanMoveNpcs)
				MagicalReleaseCommon.RadialHit(OwnerOf(), Projectile.Center, 40f,
					Math.Max(1, Projectile.damage / 2), 1f);

			Player owner = OwnerOf();
			if (owner != null) {
				float baseRot = Projectile.velocity.LengthSquared() > 0.01f
					? Projectile.velocity.ToRotation()
					: Projectile.rotation;
				if (Generation == 0) {
					// Seed -> three shards fanned around the travel direction.
					for (int i = 0; i < 3; i++) {
						Vector2 dir = (baseRot + (i - 1) * 0.42f).ToRotationVector2();
						MagicalReleaseCommon.SpawnChild(owner, Projectile.Center,
							dir * (6.2f + Main.rand.NextFloat(1.6f)),
							Projectile.type, Math.Max(1, (int)(Projectile.damage * 0.6f)),
							1f, Projectile.ai[1], Projectile.ai[2]);
					}
				}
				else {
					// Shard -> two weak homing motes.
					for (int i = 0; i < 2; i++) {
						Vector2 dir = (baseRot + (i == 0 ? -0.34f : 0.34f)).ToRotationVector2();
						MagicalReleaseCommon.SpawnChild(owner, Projectile.Center, dir * 8.4f,
							ModContent.ProjectileType<MagicalStarProj>(),
							Math.Max(1, (int)(Projectile.damage * 0.75f)),
							MagicalStarProj.ModeHoming, Projectile.ai[1], Projectile.ai[2]);
					}
				}
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

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			Color hot = Color.Lerp(accent, Color.White, 0.45f);
			int fuse = Generation == 0 ? 34 : 24;
			float fuseT = MathHelper.Clamp(age / (float)fuse, 0f, 1f);
			// The pod visibly swells and brightens before it cracks.
			float size = (Generation == 0 ? 22f : 17f) * (1f + 0.45f * fuseT);
			MagicalFx.Shape(ShapeId.SplatSoft, Projectile.Center, Projectile.rotation, size * 1.2f, size,
				accent * (0.5f + 0.35f * fuseT));
			MagicalFx.Bloom(Projectile.Center, size * 1.6f, hot, 0.25f + 0.35f * fuseT);
			MagicalFx.Shape(ShapeId.RingCracked, Projectile.Center, Projectile.rotation, size * 1.5f,
				size * 1.5f, hot * (0.25f + 0.4f * fuseT));
			if (fuseT > 0.6f)
				MagicalFx.StarFlare(Projectile.Center, hot, 22f + 14f * fuseT, t2 * 0.1f, 0.4f + 0.5f * fuseT);
			return false;
		}
	}
}
