using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Ground Fault. A lightning bolt that treats the floor as part of the
	/// circuit rather than a wall.
	///
	/// ai[0]: 0 = Ground-Hugger, 1 = Gravity Bounce (synced from the server
	///        config at cast time).
	/// ai[1]: power01.
	/// ai[2]: source projectile type.
	///
	/// Ground-Hugger lands on the first floor under its path, runs along it,
	/// and periodically throws a short arc UP at a target above the floor.
	/// Gravity Bounce is a real projectile with gravity: it reflects off tiles,
	/// loses speed per bounce, and never chains.
	///
	/// No fourth ai slot is used: Projectile.ai has three vanilla slots and the
	/// vanilla sync packet carries ai[0..2] only.
	/// </summary>
	public class MagicalGroundFaultProj : ModProjectile
	{
		public const float ModeGroundHugger = 0f;
		public const float ModeGravityBounce = 1f;

		/// <summary>Six seconds is long enough for the crawler to cross a
		/// meaningful stretch of cave floor (and for the bounce to spend its
		/// bounces) without becoming a permanent hazard.</summary>
		public const int LifetimeTicks = 6 * 60;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private readonly List<Vector2> trail = new();
		private readonly List<int> struck = new();

		private float power;
		private int srcType;
		private int mode;
		private int age;
		private int seed;
		private int zapClock;
		private int bounceCount;
		private int dirX = 1;
		private bool born;
		private bool grounded;
		private Color accent = new(255, 196, 92);

		private float Power01 => MathHelper.Clamp(power, 0f, 1f);

		public override void SetDefaults()
		{
			Projectile.width = 18;
			Projectile.height = 18;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.netImportant = true;
			Projectile.timeLeft = LifetimeTicks;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 40;
		}

		public override void AI()
		{
			if (Projectile.ai == null || Projectile.ai.Length < 3
				|| Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers) {
				Projectile.Kill();
				return;
			}
			if (!born) {
				born = true;
				power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
				srcType = (int)Projectile.ai[2];
				mode = Projectile.ai.Length > 0 && Projectile.ai[0] >= 0.5f
					? (int)ModeGravityBounce : (int)ModeGroundHugger;
				seed = Projectile.identity * 7 + 19;
				dirX = Math.Sign(Projectile.velocity.X);
				if (dirX == 0)
					dirX = Main.player[Projectile.owner].direction;
				accent = MagicalReleaseCommon.AbilityAccent(Projectile);
				if (!Main.dedServ)
					SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.35f, Pitch = -0.1f }, Projectile.Center);
			}

			age++;
			if (mode == (int)ModeGroundHugger)
				GroundHuggerAI();
			else
				GravityBounceAI();

			RecordTrail();
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.55f + 0.35f * Power01));
		}

		private void GroundHuggerAI()
		{
			if (!grounded) {
				Projectile.tileCollide = true;
				Projectile.velocity.Y += 0.42f;
				if (Projectile.velocity.Y > 14f)
					Projectile.velocity.Y = 14f;
				Projectile.rotation = Projectile.velocity.ToRotation();
			}
			else {
				// Ground-hugging is a true surface walk, not a tile-ignoring
				// slide. The engine's collision stays on; this method moves one
				// checked step at a time and refuses any step that would put the
				// hitbox inside a wall or floor. Velocity is zero here because
				// the position has already been advanced manually - setting a
				// horizontal velocity as well made the bolt move twice per tick
				// and tunnel through thin walls.
				Projectile.tileCollide = true;
				float speed = 8f + 3.5f * Power01;
				Projectile.rotation = dirX > 0 ? 0f : MathHelper.Pi;
				// The manual step below owns the movement. Zero the engine
				// velocity first; MoveAlongGround may set a falling velocity if
				// the next column has no floor.
				Projectile.velocity = Vector2.Zero;
				MoveAlongGround(speed);

				// Only a grounded crawler throws upward arcs; a fall off a
				// ledge must not keep firing from mid-air.
				if (grounded) {
					zapClock++;
					if (zapClock >= 26) {
						zapClock = 0;
						ZapUpward();
					}
				}
			}

			if (age >= LifetimeTicks)
				Projectile.Kill();
		}

		private void MoveAlongGround(float speed)
		{
			float feet = Projectile.Center.Y + Projectile.height * 0.5f;
			float targetX = Projectile.Center.X + dirX * speed;

			// Look for the surface directly below the next column. A ledge that
			// disappears under us ends the crawl and hands control back to real
			// gravity.
			if (!TryFindGround(targetX, feet - 2f, out float surfaceY)) {
				StartFalling(speed);
				return;
			}

			// A drop larger than roughly two tiles is a ledge, not a step.
			if (surfaceY > feet + 30f) {
				StartFalling(speed);
				return;
			}

			// A rise taller than a single tile is a wall. Turn instead of
			// climbing it; the bolt must never enter the tile.
			if (surfaceY < feet - 24f) {
				dirX *= -1;
				return;
			}

			Vector2 candidateCenter = new(targetX, surfaceY - Projectile.height * 0.5f - 1f);
			Vector2 candidatePos = candidateCenter
				- new Vector2(Projectile.width * 0.5f, Projectile.height * 0.5f);

			// If the next stand would overlap solid tiles, try stepping up by
			// one tile. Otherwise reverse. This is what keeps the crawler out of
			// walls and off the inside of a floor.
			if (Collision.SolidCollision(candidatePos, Projectile.width, Projectile.height)) {
				bool stepped = false;
				for (int rise = 8; rise <= 24; rise += 8) {
					Vector2 lifted = candidatePos - new Vector2(0f, rise);
					if (Collision.SolidCollision(lifted, Projectile.width, Projectile.height))
						continue;
					candidatePos = lifted;
					stepped = true;
					break;
				}
				if (!stepped) {
					dirX *= -1;
					return;
				}
			}

			Projectile.Center = candidatePos
				+ new Vector2(Projectile.width * 0.5f, Projectile.height * 0.5f);
		}

		private void StartFalling(float speed)
		{
			grounded = false;
			Projectile.tileCollide = true;
			Projectile.velocity = new Vector2(dirX * speed * 0.7f, 1.5f);
		}

		private void GravityBounceAI()
		{
			Projectile.velocity.Y += 0.34f;
			if (Projectile.velocity.Y > 16f)
				Projectile.velocity.Y = 16f;
			Projectile.rotation = Projectile.velocity.ToRotation();

			if (age >= LifetimeTicks)
				Projectile.Kill();
		}

		private void ZapUpward()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			Player owner = Main.player[Projectile.owner];
			if (owner == null || !owner.active || owner.dead)
				return;

			NPC target = null;
			float best = 360f * 360f;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Targetable(npc) || struck.Contains(npc.whoAmI))
					continue;
				// The fantasy is an upward strike: do not zap things below
				// the floor the bolt is hugging.
				if (npc.Center.Y > Projectile.Center.Y - 12f)
					continue;
				if (!Collision.CanHitLine(Projectile.Center, 1, 1, npc.Center, 1, 1))
					continue;
				float d = Vector2.DistanceSquared(npc.Center, Projectile.Center);
				if (d < best) {
					best = d;
					target = npc;
				}
			}

			if (target == null)
				return;

			Vector2 start = Projectile.Center;
			Vector2 dir = (target.Center - start).SafeNormalize(-Vector2.UnitY);
			if (dir.Y > -0.2f)
				dir = -Vector2.UnitY;

			int damage = Math.Max(1, (int)(Projectile.damage * 0.72f));
			if (MagicalReleaseCommon.Strike(owner, target, damage, 2.4f, accent, start))
				struck.Add(target.whoAmI);

			// Visual-only bolt: the real damage came from Strike above. This
			// keeps the arc readable without a second damage source.
			int arc = MagicalReleaseCommon.SpawnChild(owner, start, dir * MagicalLightningProj.BoltSpeed,
				ModContent.ProjectileType<MagicalLightningProj>(), 1,
				MagicalLightningProj.ModeStraight, Power01, srcType);
			if (arc >= 0 && arc < Main.maxProjectiles) {
				Projectile p = Main.projectile[arc];
				p.friendly = false;
				p.hostile = false;
				p.damage = 0;
				p.tileCollide = false;
				p.timeLeft = 14;
				p.netUpdate = true;
			}

			if (!Main.dedServ) {
				MiniYoyoFx.FlashFX(start, Color.Lerp(accent, Color.White, 0.55f), 0.35f);
				YoyoParticles.SpawnRing(start, 5f, 34f, 10, accent, 1.8f);
			}
		}

		private static bool TryFindGround(float x, float fromY, out float surfaceY)
		{
			surfaceY = 0f;
			int tx = (int)(x / 16f);
			if (tx < 0 || tx >= Main.maxTilesX)
				return false;

			int startY = Math.Max(0, (int)(fromY / 16f));
			int endY = Math.Min(Main.maxTilesY - 1, startY + 24);
			for (int ty = startY; ty <= endY; ty++) {
				Tile tile = Main.tile[tx, ty];
				if (!tile.HasTile)
					continue;
				if (!Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
					continue;

				// Find the TOP of this solid column, not just the first solid
				// cell below the sample point. A one-tile step and a two-tile
				// wall must report different heights, or the crawler cannot
				// tell "step up" from "turn around".
				while (ty > 0) {
					Tile above = Main.tile[tx, ty - 1];
					if (!above.HasTile
						|| (!Main.tileSolid[above.TileType] && !Main.tileSolidTop[above.TileType]))
						break;
					ty--;
				}
				surfaceY = ty * 16f;
				return true;
			}
			return false;
		}

		private void RecordTrail()
		{
			// A step-up or a wall turn can move the bolt a tile in one tick. Do
			// not join across that jump: the trail is a continuous path, not a
			// "connect every stored point" scribble.
			if (trail.Count == 0
				|| Vector2.DistanceSquared(trail[^1], Projectile.Center) > 48f * 48f) {
				trail.Clear();
				trail.Add(Projectile.Center);
				return;
			}

			if (Vector2.DistanceSquared(trail[^1], Projectile.Center) > 1f)
				trail.Add(Projectile.Center);
			if (trail.Count > 30)
				trail.RemoveRange(0, trail.Count - 30);
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (mode == (int)ModeGroundHugger) {
				// Landing on a floor starts the crawl. Check the floor under the
				// CURRENT column; a side-wall hit while falling has no nearby
				// floor and simply reverses the horizontal heading.
				float feet = Projectile.Center.Y + Projectile.height * 0.5f;
				if (oldVelocity.Y > 0.1f
					&& TryFindGround(Projectile.Center.X, feet - 2f, out float surfaceY)
					&& surfaceY <= feet + 12f) {
					Projectile.Center = new Vector2(Projectile.Center.X,
						surfaceY - Projectile.height * 0.5f - 1f);
					grounded = true;
					Projectile.tileCollide = true;
					Projectile.velocity = Vector2.Zero;
					if (!Main.dedServ) {
						MagicalFx.QueueStarFlare(Projectile.Center,
							Color.Lerp(accent, Color.White, 0.5f), 20f, 0f, 0.28f);
						YoyoParticles.SpawnBloom(Projectile.Center, 3f, 24f, 12, accent, 0.42f);
					}
					return false;
				}

				// Wall hit: turn, but keep falling. No tile is ever disabled for
				// this mode, so the bolt cannot pass through the obstacle.
				dirX = oldVelocity.X >= 0f ? -1 : 1;
				Projectile.velocity = new Vector2(dirX * (4f + 2f * Power01),
					Math.Max(1.5f, Math.Abs(oldVelocity.Y) * 0.55f));
				return false;
			}

			// Gravity Bounce: lose 20% of the reflected component each impact.
			// Eight bounces lets the six-second lifetime actually play out on
			// flat ground; a nearly-dead ball still stops early.
			if (bounceCount >= 8 || (Math.Abs(Projectile.velocity.Y) < 1.6f && bounceCount >= 4)) {
				Projectile.Kill();
				return false;
			}

			bounceCount++;
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.9f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.8f;
			Projectile.velocity *= 0.94f;

			// A floor bounce that has almost run out gets a minimum kick so
			// the ball actually reads as bouncing rather than sticking.
			if (Math.Abs(Projectile.velocity.Y) < 2.2f && Projectile.velocity.Y > -0.1f)
				Projectile.velocity.Y = -3.4f;

			if (!Main.dedServ) {
				SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.22f, Pitch = 0.5f }, Projectile.Center);
				MagicalFx.QueueStarFlare(Projectile.Center,
					Color.Lerp(accent, Color.White, 0.5f), 18f, Projectile.rotation, 0.25f);
				YoyoParticles.SpawnRing(Projectile.Center, 4f, 28f, 8, accent, 1.5f);
			}
			return false;
		}

		public override bool? CanHitNPC(NPC target)
			=> struck.Contains(target.whoAmI) ? false : null;

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (struck.Contains(target.whoAmI))
				return;
			struck.Add(target.whoAmI);

			if (!Main.dedServ) {
				MagicalFx.Impact(target.Center, accent, 0.9f, Projectile.owner,
					(target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX).ToRotation());
				YoyoParticles.SpawnSpark(target.Center, -Projectile.velocity * 0.2f,
					accent, 0.9f, 16, 0.94f);
			}
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			MagicalFx.Pop(Projectile.Center, 38f, accent, 0.32f);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 3, 1.8f, 14, 0.45f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;

			Color hot = Color.Lerp(accent, Color.White, 0.6f);
			int count = 0;
			Vector2[] points = new Vector2[trail.Count + 1];
			// Oldest -> newest. The old order put the live centre first and the
			// oldest point second, which drew one long straight chord from the
			// bolt back to the tail. That chord was the "straight line connecting
			// the edge" artifact.
			for (int i = 0; i < trail.Count; i++)
				points[count++] = trail[i];
			points[count++] = Projectile.Center;

			if (count >= 2) {
				// Jagged lightning body: an offset inner filament over the real
				// route, so the bolt reads as electricity while the hitbox
				// remains the honest trail.
				Vector2[] jag = new Vector2[count];
				for (int i = 0; i < count; i++) {
					Vector2 a = points[Math.Max(0, i - 1)];
					Vector2 b = points[Math.Min(count - 1, i + 1)];
					Vector2 perp = (b - a).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
					float n = (float)Math.Sin(seed * 0.71f + i * 12.9898f + Main.GameUpdateCount * 0.23f);
					jag[i] = points[i] + perp * n * 4.2f;
				}
				MiniYoyoFx.DrawPathBand(jag, count, 5f, 18f, accent * 0.32f, 0.55f);
				MiniYoyoFx.DrawPathBand(jag, count, 2.6f, 9f, accent, 0.85f);
				MiniYoyoFx.DrawPathBand(jag, count, 1f, 3f, hot, 0.9f);
			}

			float spin = Main.GameUpdateCount * 0.15f + seed;
			MagicalFx.Shape(ShapeId.WispTaper, Projectile.Center, spin, 30f, 15f, hot * 0.9f);
			MagicalFx.Bloom(Projectile.Center, 32f, accent, 0.42f);
			return false;
		}
	}
}
