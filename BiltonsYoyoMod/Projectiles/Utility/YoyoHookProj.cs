using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles.Utility
{
	/// <summary>
	/// The Yoyo Hook's grappling head.
	/// <para/>
	/// This is deliberately NOT a vanilla grappling hook (aiStyle 7). Vanilla
	/// grapples call <c>Player.GetGrapplingForces</c>, which overwrites the
	/// player's velocity every tick and reels them in at a fixed rate - that
	/// destroys the momentum swing we want. Instead the head is a plain
	/// projectile that only declares "the rope is anchored here"; all player
	/// motion is produced by <see cref="Players.YoyoHookPlayer"/> from gravity,
	/// input and rope tension.
	/// <para/>
	/// State lives in <c>Projectile.ai[0]</c> (see the <c>State*</c> constants)
	/// and the commanded rope rest length lives in <c>Projectile.ai[1]</c>. Both
	/// slots are part of the vanilla projectile sync message, so simply setting
	/// <c>Projectile.netUpdate</c> keeps every client in agreement.
	/// </summary>
	public class YoyoHookProj : ModProjectile
	{
		// ai[0] states.
		public const float StateFlying = 0f;
		public const float StateRetracting = 1f;
		public const float StateAnchored = 2f;

		/// <summary>Shortest the rope may ever be retracted to (3 tiles).</summary>
		public const float MinLength = 48f;
		/// <summary>Longest the rope may ever be extended to (32 tiles).</summary>
		public const float MaxLength = 512f;
		/// <summary>Rope length change per tick while W/S are held.</summary>
		public const float RopeSpeed = 3.5f;
		/// <summary>Speed of the head while flying back after a miss/release.</summary>
		public const float ReturnSpeed = 22f;

		/// <summary>Maximum points <see cref="BuildRopePath"/> may write (two ends plus bends).</summary>
		public const int MaxRopePathPoints = 12;
		/// <summary>Most times the seeded latch route may bend around a tile.</summary>
		private const int MaxRopeBends = 8;
		/// <summary>How far the seeded route is held off a tile face it wraps around.</summary>
		private const float RopeCornerMargin = 2.5f;
		private static readonly Color RopeOuter = new Color(44, 24, 62);
		private static readonly Color RopeInner = new Color(190, 144, 255);
		private static readonly Color AuraColor = new Color(152, 108, 255);

		// Layered low-alpha radial glows. MiniYoyoGlow's alpha is a linear cone,
		// which on its own fades to a hard-edged disc; stacking three at falling
		// radii approximates a smooth curve so the aura reads as a diffuse halo.
		private static readonly float[] AuraRadius = { 60f, 40f, 26f };
		private static readonly float[] AuraAlpha = { 0.10f, 0.15f, 0.20f };

		// Scratch used only while latching (single-threaded, immediately consumed).
		private static readonly Vector2[] PathScratch = new Vector2[MaxRopePathPoints];
		/// <summary>
		/// Live rope. The chain keeps its own shape between ticks; the old
		/// shortest-path route is now used only once, to seed the latch pose.
		/// </summary>
		private readonly HookRope rope = new HookRope(IsRopeBlocking);

		private static int cachedType = -1;

		/// <summary>Cached <see cref="ModContent.ProjectileType{T}()"/> lookup.</summary>
		public static int HookType
		{
			get
			{
				if (cachedType < 0)
					cachedType = ModContent.ProjectileType<YoyoHookProj>();
				return cachedType;
			}
		}

		public override void SetStaticDefaults()
		{
			// tModLoader only flags `aiStyle == 7` projectiles as hooks, and this
			// head deliberately is not one (see the class summary), so claim the
			// flag by hand. `Main.projHook` is what lets the item sit in the
			// grappling hook slot (ItemSlot.IsMiscEquipment / PickItemMovementAction)
			// and what lets the grapple hotkey fire it (Player.QuickGrapple_GetItemToUse).
			// Player.grappling[] is only ever set by vanilla's aiStyle-7 AI, which
			// never runs for us, so vanilla's rope movement stays out of the way
			// and YoyoHookPlayer remains the only thing moving the player.
			if (Type > 0 && Main.projHook != null && Type < Main.projHook.Length)
				Main.projHook[Type] = true;
		}

		/// <summary>
		/// Grapple hotkey press. A press while the player already has a hook out
		/// is treated as "let go" - exactly what clicking the item again does -
		/// instead of letting vanilla fire a second head onto the same rope.
		/// </summary>
		public override bool? CanUseGrapple(Player player)
		{
			if (Main.dedServ || player.whoAmI != Main.myPlayer)
				return null;
			if (FindLive(player.whoAmI) == null)
				return null;
			ReleaseHook(player.whoAmI);
			return false;
		}

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.aiStyle = 0;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 60;
			Projectile.netImportant = true;
		}

		// ---------------------------------------------------------------- helpers

		/// <summary>The owner's hook, whatever state it is in, or null.</summary>
		public static Projectile FindLive(int owner)
		{
			if (owner < 0 || owner >= Main.maxPlayers)
				return null;
			int type = HookType;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p != null && p.active && p.owner == owner && p.type == type)
					return p;
			}
			return null;
		}

		/// <summary>The owner's hook only if it is currently anchored, else null.</summary>
		public static Projectile FindAnchored(int owner)
		{
			Projectile p = FindLive(owner);
			return (p != null && (int)p.ai[0] == (int)StateAnchored) ? p : null;
		}

		/// <summary>
		/// Destroy the owner's hook without touching their velocity, so all of
		/// their momentum carries into the release. Returns true if one existed.
		/// </summary>
		public static bool ReleaseHook(int owner)
		{
			Projectile p = FindLive(owner);
			if (p == null)
				return false;
			p.Kill();
			return true;
		}

		/// <summary>
		/// Mirror of vanilla's <c>AI_007_GrapplingHooks_CanTileBeLatchedOnTo</c>:
		/// fully solid tiles (and Crispy Honey), but not platforms.
		/// </summary>
		public static bool IsAnchorable(Vector2 worldPos)
		{
			return TryGetAnchorTile(worldPos, out _, out _);
		}

		/// <summary>
		/// The latchable tile under a world position, if any. The coordinates are
		/// remembered on the projectile so an anchored hook can verify its anchor
		/// directly instead of guessing from the current rope angle.
		/// </summary>
		public static bool TryGetAnchorTile(Vector2 worldPos, out int x, out int y)
		{
			x = (int)Math.Floor(worldPos.X / 16f);
			y = (int)Math.Floor(worldPos.Y / 16f);
			return IsAnchorTile(x, y);
		}

		/// <summary>Whether a specific tile can hold the hook.</summary>
		public static bool IsAnchorTile(int x, int y)
		{
			if (!WorldGen.InWorld(x, y, 1))
				return false;
			Tile tile = Main.tile[x, y];
			if (tile == null || !tile.HasUnactuatedTile)
				return false;
			int type = tile.TileType;
			// Platforms hold the head exactly like a solid block (the rope still
			// passes straight through them - see IsRopeBlocking), and minecart
			// tracks count as railing.
			return Main.tileSolid[type] || TileID.Sets.Platforms[type] || type == TileID.MinecartTrack;
		}

		/// <summary>
		/// True for tiles the rope cannot pass through. Platforms are deliberately
		/// NOT blockers: a rope laid across planks lies flat on them like it would
		/// in the world, and wrapping a rope around a 1-tile platform was what made
		/// the hook pivot off onto a stray plank.
		/// </summary>
		public static bool IsRopeBlocking(int x, int y)
		{
			if (!WorldGen.InWorld(x, y, 1))
				return false;
			Tile tile = Main.tile[x, y];
			if (tile == null || !tile.HasUnactuatedTile)
				return false;
			return Main.tileSolid[tile.TileType] && !TileID.Sets.Platforms[tile.TileType];
		}

		/// <summary>
		/// Grid walk from <paramref name="start"/> to <paramref name="end"/>.
		/// Returns the first blocking tile the line enters, plus the point where it
		/// enters. The start tile is skipped (the rope begins in the player) and the
		/// endpoint is only ever reached through air: the hook always sits on the
		/// free side of the surface it latched to.
		/// </summary>
		private static bool RaycastRope(Vector2 start, Vector2 end, out Vector2 hit, out int hitX, out int hitY)
		{
			hit = end;
			hitX = hitY = 0;

			Vector2 delta = end - start;
			if (delta.LengthSquared() < 0.0001f)
				return false;

			int startX = (int)Math.Floor(start.X / 16f);
			int startY = (int)Math.Floor(start.Y / 16f);
			int endX = (int)Math.Floor(end.X / 16f);
			int endY = (int)Math.Floor(end.Y / 16f);

			int stepX = delta.X > 0f ? 1 : delta.X < 0f ? -1 : 0;
			int stepY = delta.Y > 0f ? 1 : delta.Y < 0f ? -1 : 0;

			float tDeltaX = stepX == 0 ? float.MaxValue : Math.Abs(16f / delta.X);
			float tDeltaY = stepY == 0 ? float.MaxValue : Math.Abs(16f / delta.Y);

			float tMaxX = stepX > 0
				? ((startX + 1) * 16f - start.X) / delta.X
				: stepX < 0 ? (startX * 16f - start.X) / delta.X : float.MaxValue;
			float tMaxY = stepY > 0
				? ((startY + 1) * 16f - start.Y) / delta.Y
				: stepY < 0 ? (startY * 16f - start.Y) / delta.Y : float.MaxValue;

			int x = startX;
			int y = startY;

			// Bounded walk: a rope is never longer than a screen and this keeps a
			// malformed start/end pair from ever looping.
			for (int guard = 0; guard < 1024; guard++)
			{
				float t;
				if (tMaxX < tMaxY)
				{
					t = tMaxX;
					tMaxX += tDeltaX;
					x += stepX;
				}
				else
				{
					t = tMaxY;
					tMaxY += tDeltaY;
					y += stepY;
				}

				if (t > 1f)
					break;

				if (IsRopeBlocking(x, y))
				{
					hit = start + delta * MathHelper.Clamp(t, 0f, 1f);
					hitX = x;
					hitY = y;
					return true;
				}

				if (x == endX && y == endY)
					break;
			}
			return false;
		}

		private static bool IsRopeBlocked(Vector2 start, Vector2 end)
		{
			return RaycastRope(start, end, out _, out _, out _);
		}

		private static void ConsiderRopeCorner(Vector2 from, Vector2 target, Vector2 candidate,
			ref bool found, ref float bestScore, ref Vector2 bestCorner)
		{
			// A corner the rope cannot even reach from its current point is
			// useless: choosing it would draw the rope through the tile.
			if (IsRopeBlocked(from, candidate))
				return;

			float score = Vector2.Distance(from, candidate) + Vector2.Distance(candidate, target);

			// Never walk backwards around an obstacle: that is what turns a long
			// ceiling into a zig-zag loop instead of sliding along to its edge.
			if (Vector2.Dot(candidate - from, target - from) <= 0f)
				score += 100000f;

			// Prefer a corner that can see the target, but keep the bonus small
			// enough that a much shorter route around the other edge still wins.
			if (!IsRopeBlocked(candidate, target))
				score -= 48f;

			if (!found || score < bestScore)
			{
				found = true;
				bestScore = score;
				bestCorner = candidate;
			}
		}

		/// <summary>Most corners one wrap step may choose between (4 exposed faces x 2 ends).</summary>
		private const int MaxWrapCandidates = 8;
		/// <summary>How close a corner must be to last tick's bend to count as the same wrap.</summary>
		private const float PreferredBendTolerance = 8f;

		// Candidate corners for the current wrap step (single-threaded scratch).
		private static readonly Vector2[] WrapCandidates = new Vector2[MaxWrapCandidates];

		private static void AddCandidate(Vector2 candidate, ref int count)
		{
			if (count >= WrapCandidates.Length)
				return;
			for (int i = 0; i < count; i++)
			{
				if (Vector2.DistanceSquared(WrapCandidates[i], candidate) < 1f)
					return;
			}
			WrapCandidates[count++] = candidate;
		}

		private static void AddTileCorners(int tileX, int tileY, ref int count)
		{
			float left = tileX * 16f - RopeCornerMargin;
			float right = (tileX + 1) * 16f + RopeCornerMargin;
			float top = tileY * 16f - RopeCornerMargin;
			float bottom = (tileY + 1) * 16f + RopeCornerMargin;

			AddCandidate(new Vector2(left, top), ref count);
			AddCandidate(new Vector2(right, top), ref count);
			AddCandidate(new Vector2(left, bottom), ref count);
			AddCandidate(new Vector2(right, bottom), ref count);
		}

		/// <summary>
		/// Adds the two ends of the contiguous solid run along a horizontal face of
		/// the hit tile. Sliding straight to the end of a floor/ceiling in one step
		/// is what keeps the rope from crawling tile by tile (and doubling back)
		/// when the target is past the far edge.
		/// </summary>
		private static void AddHorizontalFace(int tileX, int tileY, bool topFace, ref int count)
		{
			int minX = tileX;
			int maxX = tileX;
			for (int guard = 0; guard < 64 && IsRopeBlocking(minX - 1, tileY); guard++)
				minX--;
			for (int guard = 0; guard < 64 && IsRopeBlocking(maxX + 1, tileY); guard++)
				maxX++;

			float y = topFace ? tileY * 16f - RopeCornerMargin : (tileY + 1) * 16f + RopeCornerMargin;
			AddCandidate(new Vector2(minX * 16f - RopeCornerMargin, y), ref count);
			AddCandidate(new Vector2((maxX + 1) * 16f + RopeCornerMargin, y), ref count);
		}

		/// <summary>Vertical counterpart of <see cref="AddHorizontalFace"/>.</summary>
		private static void AddVerticalFace(int tileX, int tileY, bool leftFace, ref int count)
		{
			int minY = tileY;
			int maxY = tileY;
			for (int guard = 0; guard < 64 && IsRopeBlocking(tileX, minY - 1); guard++)
				minY--;
			for (int guard = 0; guard < 64 && IsRopeBlocking(tileX, maxY + 1); guard++)
				maxY++;

			float x = leftFace ? tileX * 16f - RopeCornerMargin : (tileX + 1) * 16f + RopeCornerMargin;
			AddCandidate(new Vector2(x, minY * 16f - RopeCornerMargin), ref count);
			AddCandidate(new Vector2(x, (maxY + 1) * 16f + RopeCornerMargin), ref count);
		}

		/// <summary>True when <paramref name="point"/> is one the current route already uses.</summary>
		private static bool IsPointUsed(Vector2 point, Vector2[] used, int usedCount)
		{
			for (int i = 0; i < usedCount; i++)
			{
				if (Vector2.DistanceSquared(point, used[i]) < 4f)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Hysteresis: if a corner used by last tick's route is available for this
		/// step, keep it. Matching against the whole previous route - not just its
		/// first bend - is what lets the rope stay wound around an island: pinning
		/// only bend 0 still let bends 1+ re-solve to the short way round, which is
		/// what made the string jump to the other side mid-loop.
		/// </summary>
		private static bool PickPreferredBend(Vector2 from, int count,
			Vector2[] previous, int previousCount, Vector2[] used, int usedCount, out Vector2 corner)
		{
			corner = default;
			if (previous == null || previousCount < 2)
				return false;

			float tolerance = PreferredBendTolerance * PreferredBendTolerance;
			float bestMatch = float.MaxValue;
			bool found = false;

			for (int p = 1; p < previousCount; p++)
			{
				for (int i = 0; i < count; i++)
				{
					Vector2 candidate = WrapCandidates[i];
					float match = Vector2.DistanceSquared(candidate, previous[p]);
					if (match > tolerance || match >= bestMatch)
						continue;
					// "Behind the player" is deliberately allowed here: a wound
					// rope's contact point is often behind the anchor direction.
					// A corner this route has already bent around is not available
					// again though - folding onto one is what used to clip the rope
					// at the wall instead of letting it take the next corner round.
					if (IsPointUsed(candidate, used, usedCount))
						continue;
					if (IsRopeBlocked(from, candidate))
						continue;
					bestMatch = match;
					corner = candidate;
					found = true;
				}
			}
			return found;
		}

		/// <summary>
		/// Chooses the corner of the surface the rope should bend around. Every
		/// exposed face of the hit tile is considered, and for each face the rope
		/// slides to the ends of the contiguous solid run along it. This handles
		/// diagonal corner hits (a floor met exactly at its top-left tile) the same
		/// way as flat-face hits, so a run is rounded in one bend instead of being
		/// traversed tile by tile.
		/// </summary>
		private static bool TryPickWrapCorner(Vector2 from, Vector2 target, int tileX, int tileY,
			Vector2[] previous, int previousCount, Vector2[] used, int usedCount, out Vector2 corner)
		{
			int count = 0;
			if (!IsRopeBlocking(tileX, tileY - 1))
				AddHorizontalFace(tileX, tileY, true, ref count);
			if (!IsRopeBlocking(tileX, tileY + 1))
				AddHorizontalFace(tileX, tileY, false, ref count);
			if (!IsRopeBlocking(tileX - 1, tileY))
				AddVerticalFace(tileX, tileY, true, ref count);
			if (!IsRopeBlocking(tileX + 1, tileY))
				AddVerticalFace(tileX, tileY, false, ref count);

			// A tile with no exposed face should be impossible for a raycast to
			// enter, but fall back to its own corners rather than give up.
			if (count == 0)
				AddTileCorners(tileX, tileY, ref count);

			if (PickPreferredBend(from, count, previous, previousCount, used, usedCount, out corner))
				return true;

			bool found = false;
			float bestScore = float.MaxValue;
			for (int i = 0; i < count; i++)
			{
				// A used corner is not a corner any more: the rope has already
				// bent around it, and folding back onto it cannot pull through.
				if (IsPointUsed(WrapCandidates[i], used, usedCount))
					continue;
				ConsiderRopeCorner(from, target, WrapCandidates[i], ref found, ref bestScore, ref corner);
			}
			return found;
		}

		/// <summary>
		/// Builds the rope's centreline from the player to the hook, bending it
		/// around solid tiles so the rope never passes through terrain. Returns the
		/// number of points written to <paramref name="buffer"/>. The final point is
		/// the hook, or the wall contact if no route around the obstacle exists.
		/// <paramref name="previous"/>/<paramref name="previousCount"/> are last
		/// tick's route: every bend prefers a corner that route already used, so the
		/// rope stays wound the way it is wound instead of re-solving to the
		/// shortest way round (see <see cref="PickPreferredBend"/>).
		/// </summary>
		public static int BuildRopePath(Vector2 start, Vector2 end, Vector2[] buffer,
			Vector2[] previous = null, int previousCount = 0)
		{
			if (buffer == null || buffer.Length < 2)
				return 0;

			buffer[0] = start;
			int count = 1;
			Vector2 current = start;

			for (int bend = 0; bend < MaxRopeBends && count < buffer.Length - 1; bend++)
			{
				if (!RaycastRope(current, end, out Vector2 contact, out int tileX, out int tileY))
				{
					buffer[count++] = end;
					return count;
				}

				if (!TryPickWrapCorner(current, end, tileX, tileY, previous, previousCount, buffer, count, out Vector2 corner))
					return ClipRopeAtWall(buffer, count, current, end, contact);

				// Never step onto a point already in the path: that is either the
				// point we just came from or a loop around a concave pocket, and in
				// both cases the rope cannot pull through. Clip it at the wall.
				for (int i = 0; i < count; i++)
				{
					if (Vector2.DistanceSquared(corner, buffer[i]) < 4f)
						return ClipRopeAtWall(buffer, count, current, end, contact);
				}

				buffer[count++] = corner;
				current = corner;

				if (Vector2.DistanceSquared(current, end) < 4f)
				{
					buffer[count++] = end;
					return count;
				}
			}

			// Out of bends: stop at the first remaining obstacle rather than
			// drawing a straight line through it.
			if (RaycastRope(current, end, out Vector2 lastContact, out _, out _))
				return ClipRopeAtWall(buffer, count, current, end, lastContact);

			if (count < buffer.Length)
				buffer[count++] = end;
			return count;
		}

		private static int ClipRopeAtWall(Vector2[] buffer, int count, Vector2 current, Vector2 end, Vector2 contact)
		{
			Vector2 direction = (end - current).SafeNormalize(Vector2.UnitX);
			// Keep the visible line's half-width out of the tile.
			buffer[count++] = contact - direction * 2f;
			return count;
		}

		// -------------------------------------------------------------- lifecycle

		public override void OnKill(int timeLeft)
		{
			// Clear the owner's cached index so the rope physics cleanly stops.
			if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
			{
				var mp = Main.player[Projectile.owner].GetModPlayer<global::BiltonsYoyoMod.Players.YoyoHookPlayer>();
				if (mp.HookIndex == Projectile.whoAmI)
					mp.HookIndex = -1;
			}
			rope.Clear();
			for (int i = 0; i < 8; i++)
			{
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Shadowflame, 0f, 0f, 120, default, 0.8f);
				d.noGravity = true;
				d.velocity = Main.rand.NextVector2Circular(2f, 2f);
			}
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];

			// Dead / immobilised / gone owner: bail out safely. Mirrors vanilla's
			// grapple self-kill so the rope never outlives its anchor point.
			if (owner == null || !owner.active || owner.dead
				|| owner.stoned || owner.webbed || owner.frozen)
			{
				Projectile.Kill();
				return;
			}

			// Firing from the grapple slot bypasses the item's Shoot(), so the
			// player may not know the index yet. Cache it here; otherwise
			// YoyoHookPlayer.AnchoredHook has to scan every projectile each tick.
			if (Projectile.owner == Main.myPlayer)
			{
				var hookPlayer = owner.GetModPlayer<global::BiltonsYoyoMod.Players.YoyoHookPlayer>();
				if (hookPlayer.HookIndex != Projectile.whoAmI)
					hookPlayer.HookIndex = Projectile.whoAmI;
			}

			switch ((int)Projectile.ai[0])
			{
				case (int)StateFlying:
					UpdateFlying(owner);
					break;
				case (int)StateRetracting:
					UpdateRetracting(owner);
					break;
				default:
					UpdateAnchored(owner);
					break;
			}
		}

		// ------------------------------------------------------------------ states

		private void UpdateFlying(Player owner)
		{
			Projectile.timeLeft = 60;
			Projectile.rotation = Projectile.velocity.ToRotation();
			Lighting.AddLight(Projectile.Center, 0.42f, 0.28f, 0.70f);

			if (Main.rand.NextBool(2))
			{
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Shadowflame, 0f, 0f, 150, default, 0.75f);
				d.noGravity = true;
				d.velocity *= 0.15f;
			}

			if (Vector2.Distance(Projectile.Center, owner.MountedCenter) > MaxLength)
			{
				StartReturn();
				return;
			}

			// Only the controlling client does the latch trace; everyone else just
			// lets Projectile.velocity carry the head until the sync arrives.
			if (Projectile.owner != Main.myPlayer)
				return;

			float speed = Projectile.velocity.Length();
			if (speed < 0.01f)
			{
				StartReturn();
				return;
			}

			// Sub-step the flight path so a fast hook can never tunnel through a
			// thin wall or skip past a latchable tile between frames.
			int steps = (int)(speed / 6f) + 1;
			Vector2 step = Projectile.velocity / steps;
			Vector2 previous = Projectile.Center;
			Vector2 probe = previous;
			for (int s = 0; s < steps; s++)
			{
				previous = probe;
				probe += step;

				// Don't latch beyond the rope's reach: stop here and fly back so the
				// player never gets yanked by a hook they cannot actually be tied to.
				if (Vector2.Distance(probe, owner.MountedCenter) > MaxLength)
				{
					Projectile.Center = probe;
					StartReturn();
					return;
				}

				if (TryGetAnchorTile(probe, out int anchorX, out int anchorY))
				{
					Latch(previous, owner, anchorX, anchorY);
					return;
				}
			}
		}

		private void UpdateRetracting(Player owner)
		{
			// timeLeft intentionally not refreshed - it is the retract timeout.
			Vector2 toOwner = owner.MountedCenter - Projectile.Center;
			float dist = toOwner.Length();
			Projectile.rotation = dist > 0.01f ? toOwner.ToRotation() : Projectile.rotation;

			if (dist < 18f || Projectile.timeLeft <= 1)
			{
				Projectile.Kill();
				return;
			}
			Projectile.velocity = toOwner / dist * ReturnSpeed;
		}

		private void UpdateAnchored(Player owner)
		{
			// Refresh the timer every tick: an anchored hook is permanent until
			// the player releases it, the latched tile is removed, they die, or
			// they teleport away. Hanging has no time limit.
			Projectile.timeLeft = 60;
			Projectile.velocity = Vector2.Zero;

			Vector2 toOwner = owner.MountedCenter - Projectile.Center;
			// The claw points away from the player, into the surface it grips.
			Projectile.rotation = (Projectile.Center - owner.MountedCenter).ToRotation();

			// Mounts override movement entirely; let go rather than fight them.
			if (owner.mount.Active)
			{
				StartReturn();
				return;
			}

			if (Projectile.owner == Main.myPlayer)
			{
				float len = Projectile.ai[1];
				if (len <= 0f)
					len = MathHelper.Clamp(toOwner.Length(), MinLength, MaxLength);

				// W retracts (shorter rope, reels the player in), S extends.
				if (owner.controlUp)
					len -= RopeSpeed;
				if (owner.controlDown)
					len += RopeSpeed;
				len = MathHelper.Clamp(len, MinLength, MaxLength);

				if (Math.Abs(len - Projectile.ai[1]) > 0.01f)
				{
					Projectile.ai[1] = len;
					Projectile.netUpdate = true;
				}

				// The block we latched to may have been mined away.
				if (!AnchorStillValid(owner))
				{
					StartReturn();
					return;
				}
			}
			else if (Projectile.ai[1] <= 0f)
			{
				Projectile.ai[1] = MathHelper.Clamp(toOwner.Length(), MinLength, MaxLength);
			}

			// No range-based auto-release. A fast swing can briefly stretch past
			// MaxLength before the player-side rope correction folds it back, and
			// treating that as "out of range" was cutting swings short. The rope
			// physics enforces the length instead; YoyoHookPlayer breaks the hook
			// on genuine teleports.

			Lighting.AddLight(Projectile.Center, 0.40f, 0.26f, 0.66f);
			if (Main.rand.NextBool(4))
			{
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Shadowflame, 0f, 0f, 140, default, 0.55f);
				d.noGravity = true;
				d.velocity *= 0.3f;
			}
		}

		// ----------------------------------------------------------------- actions

		private void StartReturn()
		{
			if ((int)Projectile.ai[0] == (int)StateRetracting)
				return;
			Projectile.ai[0] = StateRetracting;
			Projectile.timeLeft = 45;
			Projectile.localAI[2] = 0f; // no longer holding the cached anchor tile
			rope.Clear();
			Projectile.netUpdate = true;

			Vector2 toOwner = Main.player[Projectile.owner].MountedCenter - Projectile.Center;
			if (toOwner.LengthSquared() > 0.01f)
				Projectile.velocity = Vector2.Normalize(toOwner) * ReturnSpeed;
		}

		/// <summary>
		/// Snap the head to the last free point before the wall so the claw reads
		/// as gripping the surface, then hand the rope over to the player physics.
		/// </summary>
		private void Latch(Vector2 surfacePoint, Player owner, int anchorX, int anchorY)
		{
			Projectile.Center = surfacePoint;
			Projectile.velocity = Vector2.Zero;
			Projectile.ai[0] = StateAnchored;

			// The commanded length is measured along the rope, not as a straight
			// line: if the rope already has to bend around terrain, the player
			// must be given the full path length or they are yanked on attach.
			int pathCount = BuildRopePath(owner.MountedCenter, surfacePoint, PathScratch);
			float ropeLength = 0f;
			for (int i = 1; i < pathCount; i++)
				ropeLength += Vector2.Distance(PathScratch[i - 1], PathScratch[i]);
			if (ropeLength <= 0f)
				ropeLength = Vector2.Distance(surfacePoint, owner.MountedCenter);

			Projectile.ai[1] = MathHelper.Clamp(ropeLength, MinLength, MaxLength);

			// Seed the simulated chain along the path that produced that length.
			// The first frame then already lies on the correct side of the
			// terrain instead of starting through an island and popping out.
			rope.Reset(owner.MountedCenter, surfacePoint, ropeLength,
				pathCount >= 2 ? PathScratch : null, pathCount);

			// Remember exactly which tile was gripped. Re-deriving it from the
			// current rope angle (the old behaviour) failed whenever the player
			// swung sideways along a thin surface, which detached the hook
			// mid-swing.
			Projectile.localAI[0] = anchorX;
			Projectile.localAI[1] = anchorY;
			Projectile.localAI[2] = 1f;
			Projectile.netUpdate = true;

			SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);
			for (int i = 0; i < 12; i++)
			{
				Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(6f), 12, 12,
					DustID.SilverCoin, 0f, 0f, 110, default, 1f);
				d.noGravity = true;
				d.velocity = Main.rand.NextVector2Circular(2f, 2f);
			}
		}

		private bool AnchorStillValid(Player owner)
		{
			// Fast, exact path: the hook recorded the tile it gripped when it
			// latched. This is independent of where the rope currently points, so
			// swinging along a wall can no longer look like a lost anchor.
			if (Projectile.localAI[2] != 0f)
			{
				return IsAnchorTile((int)Projectile.localAI[0], (int)Projectile.localAI[1]);
			}

			// Fallback for hooks that were already anchored before this cache
			// existed. Probe outward once, then remember the tile on success.
			Vector2 outward = (Projectile.Center - owner.MountedCenter).SafeNormalize(-Vector2.UnitY);
			for (float d = 2f; d <= 16f; d += 4f)
			{
				if (TryGetAnchorTile(Projectile.Center + outward * d, out int x, out int y))
				{
					Projectile.localAI[0] = x;
					Projectile.localAI[1] = y;
					Projectile.localAI[2] = 1f;
					return true;
				}
			}
			return false;
		}

		// ------------------------------------------------------------- rope state

		/// <summary>
		/// Advance every visible hook rope once per tick, after players and
		/// projectiles have moved. The player-side pendulum reads the rope from
		/// this pass on the next tick, so it always pivots on a real bend rather
		/// than on a route that is about to be re-solved.
		/// </summary>
		public static void UpdateVisibleRopes()
		{
			if (Main.dedServ)
				return;

			int type = HookType;
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p == null || !p.active || p.type != type)
					continue;
				if (p.ModProjectile is YoyoHookProj hook)
					hook.UpdateRopeFromWorld();
			}
		}

		private void UpdateRopeFromWorld()
		{
			if ((int)Projectile.ai[0] != (int)StateAnchored)
			{
				rope.Clear();
				return;
			}

			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return;
			Player owner = Main.player[Projectile.owner];
			if (owner == null || !owner.active || owner.dead)
				return;

			float length = Projectile.ai[1];
			if (length <= 0f)
				length = Vector2.Distance(owner.MountedCenter, Projectile.Center);
			length = MathHelper.Clamp(length, MinLength, MaxLength);

			if (!rope.Initialized)
				rope.Reset(owner.MountedCenter, Projectile.Center, length);

			rope.Step(owner.MountedCenter, Projectile.Center, length);
		}

		/// <summary>
		/// Physics-facing pivot: the first terrain bend in the simulated rope,
		/// or the hook when the rope is straight/sagging in open air.
		/// </summary>
		public void GetRopePivot(out Vector2 pivot, out float wrappedLength)
		{
			if ((int)Projectile.ai[0] == (int)StateAnchored && rope.Initialized
				&& rope.TryGetPivot(Projectile.Center, out pivot, out wrappedLength))
			{
				return;
			}

			pivot = Projectile.Center;
			wrappedLength = 0f;
		}

		/// <summary>Total length of a rope polyline, in pixels.</summary>
		private static float MeasurePath(Vector2[] path, int count)
		{
			float length = 0f;
			for (int i = 1; i < count; i++)
				length += Vector2.Distance(path[i - 1], path[i]);
			return length;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Player owner = Main.player[Projectile.owner];
			bool ownerOk = owner != null && owner.active && !owner.dead;

			bool anchored = (int)Projectile.ai[0] == (int)StateAnchored;

			if (ownerOk)
			{
				// The anchored rope is a persistent simulated chain. While the
				// head is flying/retracting there is no chain to wrap around
				// anything yet, so it draws as a straight line.
				if (anchored && rope.Initialized)
					DrawSimulatedRope();
				else
					DrawStraightRope(owner.MountedCenter, Projectile.Center);
			}

			DrawAura(anchored);

			Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
			Vector2 origin = tex.Size() * 0.5f;
			Color col = Color.Lerp(lightColor, Color.White, 0.25f);
			float pulse = 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f);
			Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, col,
				Projectile.rotation, origin, pulse, SpriteEffects.None, 0f);
			return false;
		}

		/// <summary>
		/// Soft, centred aura behind the hook. Drawn from several stacked glows so
		/// the falloff is gradual; it never leaves a flat translucent circle.
		/// </summary>
		private void DrawAura(bool anchored)
		{
			float breath = 1f + 0.05f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f);
			float strength = anchored ? 1f : 0.7f;
			for (int i = 0; i < AuraRadius.Length; i++)
				MiniYoyoFx.DrawGlow(Projectile.Center, AuraRadius[i] * breath, AuraColor, AuraAlpha[i] * strength);
		}

		private void DrawSimulatedRope()
		{
			Vector2[] points = rope.Points;
			for (int i = 1; i < points.Length; i++)
			{
				Vector2 a = points[i - 1];
				Vector2 b = points[i];
				if (Vector2.DistanceSquared(a, b) < 0.25f)
					continue;

				StrokeRope(a, b, 3.2f, RopeOuter, 0.95f);
				StrokeRope(a, b, 1.4f, RopeInner, 1f);
			}
		}

		private static void DrawStraightRope(Vector2 start, Vector2 end)
		{
			StrokeRope(start, end, 3.2f, RopeOuter, 0.95f);
			StrokeRope(start, end, 1.4f, RopeInner, 1f);
		}

		/// <summary>
		/// Strokes one piece of rope with both ends extended by half a width. Each
		/// stroke is a rotated quad, so abutting quads leave a transparent hairline
		/// between them - and on the short quads of a rounded bend the square ends
		/// showed as little notches/squares along the rope. Overlapping the ends
		/// hides both seams without needing a custom shader.
		/// </summary>
		private static void StrokeRope(Vector2 a, Vector2 b, float width, Color color, float alpha)
		{
			Vector2 diff = b - a;
			float length = diff.Length();
			if (length < 0.001f)
				return;
			Vector2 pad = diff / length * (width * 0.5f);
			MiniYoyoFx.DrawLineWorld(a - pad, b + pad, width, color, alpha);
		}
	}
}
