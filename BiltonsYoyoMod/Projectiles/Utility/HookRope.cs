using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace BiltonsYoyoMod.Projectiles.Utility
{
	/// <summary>
	/// Persistent position-based rope for the Yoyo Hook.
	/// <para/>
	/// The old hook rope was a shortest-path query rebuilt every tick. That has
	/// no notion of where the rope already lies, so as soon as the other way
	/// round an island became a few pixels shorter the whole rope jumped sides.
	/// This is a real chain of particles instead: both ends are pinned (player
	/// and hook), the middle falls under gravity, and every node is kept out of
	/// solid tiles. A rope that has been dragged around an island therefore has
	/// to be dragged back around it; it cannot teleport through the island.
	/// <para/>
	/// The solver is the same shape as the one used by WildStringBase:
	/// Verlet integration, repeated distance constraints, swept node movement,
	/// and a same-length angular search when a segment is wrapped around a
	/// corner. It is intentionally local to the hook so changing it cannot
	/// destabilise the Wild Strings.
	/// </summary>
	internal sealed class HookRope
	{
		public const int PointCount = 40;
		private const int Last = PointCount - 1;

		private const int Substeps = 3;
		private const int ConstraintIterations = 6;
		private const int WrapPasses = 2;

		/// <summary>Collision radius of a rope node. The drawn rope is thinner.</summary>
		private const float NodeRadius = 2f;
		private const float SegmentCollisionSpacing = 2f;
		private const float Gravity = 0.30f;
		private const float Damping = 0.998f;
		private const float Bounce = 0.05f;
		private const float Friction = 0.91f;

		private const int WrapSearchSteps = 120;
		private const float WrapSearchStepDegrees = 1.5f;
		private const float EscapeDistance = 42f;

		/// <summary>Angle at which a terrain-touching node counts as a bend/pivot.</summary>
		private const float PivotBendRadians = 0.30f;
		private const float PivotProbeRadius = NodeRadius + 4f;
		/// <summary>
		/// How much slack the player-to-pivot part may have before a contact is
		/// treated as a draped rope rather than a taut swing pivot. A rope lying
		/// on the ground still touches tiles, but it must not yank the player
		/// around the first one it happens to cross.
		/// </summary>
		private const float PivotTautness = 7f;

		/// <summary>
		/// How much longer the hook-side chain may be than a clear straight
		/// line before it is treated as slack rather than a wrap. A taut rope
		/// around a real obstacle has a blocked line of sight; if the line is
		/// clear and the chain is much longer, the extra length is just rope
		/// lying on the ground.
		/// </summary>
		private const float PivotSlackTolerance = 16f;

		private readonly Vector2[] points = new Vector2[PointCount];
		private readonly Vector2[] previous = new Vector2[PointCount];
		private readonly Func<int, int, bool> isBlocking;

		private float segmentLength;
		private Vector2 previousPlayer;
		private Vector2 previousHook;
		private int lastPivotIndex = -1;
		private Vector2 smoothedPivot;
		private float smoothedWrapped;
		private bool hasSmoothedPivot;
		private const float PivotStep = 14f;
		private const float WrappedStep = 8f;

		public HookRope(Func<int, int, bool> isBlocking)
		{
			this.isBlocking = isBlocking;
		}

		public bool Initialized { get; private set; }

		/// <summary>World-space nodes from the player (0) to the hook (last).</summary>
		public Vector2[] Points => points;

		public void Clear()
		{
			Initialized = false;
			lastPivotIndex = -1;
			hasSmoothedPivot = false;
		}

		/// <summary>
		/// Seed the chain along the route the hook latched through. Seeding on
		/// the route (rather than a straight line) means the very first frame
		/// already has the rope on the correct side of the terrain.
		/// </summary>
		public void Reset(Vector2 player, Vector2 hook, float commandedLength,
			Vector2[] route = null, int routeCount = 0)
		{
			float pathLength = route != null && routeCount >= 2
				? MeasurePath(route, routeCount)
				: 0f;
			if (pathLength < 1f)
				pathLength = Vector2.Distance(player, hook);
			if (pathLength < 1f)
				pathLength = commandedLength > 0f ? commandedLength : 1f;

			segmentLength = pathLength / Last;

			if (route != null && routeCount >= 2)
			{
				for (int i = 0; i < PointCount; i++)
				{
					float distance = pathLength * i / Last;
					points[i] = SamplePath(route, routeCount, distance);
				}
			}
			else
			{
				for (int i = 0; i < PointCount; i++)
					points[i] = Vector2.Lerp(player, hook, i / (float)Last);
			}

			points[0] = player;
			points[Last] = hook;
			for (int i = 0; i < PointCount; i++)
				previous[i] = points[i];

			previousPlayer = player;
			previousHook = hook;
			lastPivotIndex = -1;
			hasSmoothedPivot = false;
			Initialized = true;
		}

		/// <summary>
		/// Advance the rope one game tick. <paramref name="commandedLength"/> is
		/// the total rope length controlled by W/S and is used as the chain's
		/// rest length. Shortening it pulls the player through the separate
		/// player-side pendulum; the chain itself only reacts to the positions
		/// the player and hook actually reached this tick.
		/// </summary>
		public void Step(Vector2 player, Vector2 hook, float commandedLength)
		{
			if (!Initialized)
			{
				Reset(player, hook, commandedLength);
				return;
			}

			if (!IsFinite(player) || !IsFinite(hook))
			{
				Reset(player, hook, commandedLength);
				return;
			}

			// The commanded length is the rest length. Do not let accidental
			// stretch become the new rest length: doing that lets gravity
			// ratchet the rope longer and longer every tick.
			float restLength = commandedLength;
			if (restLength < 1f)
				restLength = MeasureChain();
			segmentLength = MathHelper.Clamp(restLength / Last, 0.5f, 48f);

			for (int substep = 0; substep < Substeps; substep++)
			{
				float progress = (substep + 1f) / Substeps;
				Vector2 subPlayer = Vector2.Lerp(previousPlayer, player, progress);
				Vector2 subHook = Vector2.Lerp(previousHook, hook, progress);

				for (int i = 1; i < Last; i++)
				{
					Vector2 current = points[i];
					Vector2 old = previous[i];

					Vector2 velocity = (current - old) * Damping;
					velocity.Y += Gravity / Substeps;

					previous[i] = current;

					Vector2 moved = MoveNode(current, current + velocity, ref velocity);
					points[i] = moved;
					previous[i] = moved - velocity;
				}

				points[0] = subPlayer;
				points[Last] = subHook;

				for (int iteration = 0; iteration < ConstraintIterations; iteration++)
					SolveConstraints(subPlayer, subHook, (iteration & 1) == 1);

				ResolveAllTiles();
			}

			// Both ends are authoritative; never let the solver leave them a
			// fraction of a pixel behind after the last substep.
			points[0] = player;
			points[Last] = hook;
			previousPlayer = player;
			previousHook = hook;

			for (int i = 0; i < PointCount; i++)
			{
				if (!IsFinite(points[i]))
				{
					Reset(player, hook, commandedLength);
					return;
				}
			}
		}

		/// <summary>
		/// First terrain bend between the player and hook, i.e. the point the
		/// free part of the rope should swing around. Returns false when the
		/// rope is straight or only sagging in open air, in which case the hook
		/// itself is the pivot.
		/// </summary>
		public bool TryGetPivot(Vector2 hook, out Vector2 pivot, out float wrappedLength)
		{
			pivot = hook;
			wrappedLength = 0f;
			if (!Initialized)
			{
				lastPivotIndex = -1;
				return false;
			}

			// Stick to the previous contact while it is still a real bend.
			// Two neighbouring corners on the same ledge can both qualify;
			// alternating between them every tick is what makes a physically
			// stable rope feel like it teleports.
			if (lastPivotIndex > 0 && lastPivotIndex < Last
				&& IsPivotCandidate(lastPivotIndex))
			{
				return ReturnPivot(lastPivotIndex, out pivot, out wrappedLength);
			}

			for (int i = 1; i < Last; i++)
			{
				if (!IsPivotCandidate(i))
					continue;

				lastPivotIndex = i;
				return ReturnPivot(i, out pivot, out wrappedLength);
			}

			lastPivotIndex = -1;
			hasSmoothedPivot = false;
			return false;
		}

		/// <summary>
		/// The raw contact can move from one corner of a face to the other in
		/// one tick even though the rope only slid a few pixels. Ease the
		/// physics pivot toward it so the player never receives a 100px
		/// instant change in swing centre. The drawn rope is not smoothed:
		/// it always shows the simulated chain.
		/// </summary>
		private bool ReturnPivot(int index, out Vector2 pivot, out float wrappedLength)
		{
			BuildPivot(index, out Vector2 rawPivot, out float rawWrapped);

			if (!hasSmoothedPivot)
			{
				smoothedPivot = rawPivot;
				smoothedWrapped = rawWrapped;
				hasSmoothedPivot = true;
			}
			else
			{
				smoothedPivot = MoveTowards(smoothedPivot, rawPivot, PivotStep);
				smoothedWrapped = MoveTowards(smoothedWrapped, rawWrapped, WrappedStep);
			}

			pivot = smoothedPivot;
			wrappedLength = smoothedWrapped;
			return true;
		}

		private static Vector2 MoveTowards(Vector2 from, Vector2 to, float maxStep)
		{
			Vector2 difference = to - from;
			float distance = difference.Length();
			if (distance <= maxStep || distance < 0.0001f)
				return to;
			return from + difference / distance * maxStep;
		}

		private static float MoveTowards(float from, float to, float maxStep)
		{
			float difference = to - from;
			if (Math.Abs(difference) <= maxStep)
				return to;
			return from + Math.Sign(difference) * maxStep;
		}

		private bool IsPivotCandidate(int index)
		{
			Vector2 first = points[index] - points[index - 1];
			Vector2 second = points[index + 1] - points[index];
			if (first.LengthSquared() < 0.25f || second.LengthSquared() < 0.25f)
				return false;

			float bend = Math.Abs(MathHelper.WrapAngle(
				second.ToRotation() - first.ToRotation()));
			if (bend < PivotBendRadians)
				return false;

			if (!NearBlockingTile(points[index], PivotProbeRadius))
				return false;

			// A rope lying across a flat floor or against a flat wall can
			// bend without being caught on anything; it should slide, not
			// pin the player. Only a contact at a convex corner has a
			// quadrant of solid tiles behind it and free space in the other
			// three. Without this check a long slack rope touches the floor
			// near the player's feet, reports almost the whole rope as
			// wrapped, and leaves the player unable to walk away.
			if (!NearConvexCorner(points[index], PivotProbeRadius + 2f))
				return false;

			// Only a straight, tensioned free segment can act as a
			// pendulum. If the rope is sagging between the player and this
			// contact, the contact is just where a slack rope happens to
			// touch the ground; using it would subtract almost the whole
			// rope as "wrapped" and produce a phantom taut constraint.
			float playerToNode = 0f;
			for (int i = 1; i <= index; i++)
				playerToNode += Vector2.Distance(points[i - 1], points[i]);

			if (playerToNode > Vector2.Distance(points[0], points[index]) + PivotTautness)
				return false;

			// A taut wrap must either have an obstacle between the contact
			// and the hook, or be no longer than the direct line. If the
			// line is clear and the chain is much longer, that length is a
			// pile of slack on the ground, not a wrap the player should be
			// pinned to.
			float wrapped = 0f;
			for (int i = index; i < Last; i++)
				wrapped += Vector2.Distance(points[i], points[i + 1]);

			float straight = Vector2.Distance(points[index], points[Last]);
			if (!LineBlocked(points[index], points[Last])
				&& wrapped > straight + PivotSlackTolerance)
			{
				return false;
			}

			return true;
		}

		/// <summary>
		/// True when exactly one diagonal quadrant around the point is solid.
		/// That is the tile signature of a convex outside corner. A flat
		/// floor has the two lower quadrants solid, a flat wall has the two
		/// side quadrants solid, and an inside corner has three; none of
		/// those should act as a fixed pendulum pivot.
		/// </summary>
		private bool NearConvexCorner(Vector2 position, float radius)
		{
			bool northWest = QuadrantBlocked(position, -1, -1, radius);
			bool northEast = QuadrantBlocked(position, 1, -1, radius);
			bool southWest = QuadrantBlocked(position, -1, 1, radius);
			bool southEast = QuadrantBlocked(position, 1, 1, radius);

			int solidQuadrants = (northWest ? 1 : 0) + (northEast ? 1 : 0)
				+ (southWest ? 1 : 0) + (southEast ? 1 : 0);
			return solidQuadrants == 1;
		}

		private bool QuadrantBlocked(Vector2 position, int signX, int signY, float radius)
		{
			for (float distance = 2f; distance <= radius; distance += 2f)
			{
				int x = (int)Math.Floor((position.X + signX * distance) / 16f);
				int y = (int)Math.Floor((position.Y + signY * distance) / 16f);
				if (isBlocking(x, y))
					return true;
			}
			return false;
		}

		private bool BuildPivot(int index, out Vector2 pivot, out float wrappedLength)
		{
			pivot = points[index];
			wrappedLength = 0f;
			for (int i = index; i < Last; i++)
				wrappedLength += Vector2.Distance(points[i], points[i + 1]);
			return true;
		}

		// ------------------------------------------------------------ seeding

		private static float MeasurePath(Vector2[] path, int count)
		{
			float length = 0f;
			for (int i = 1; i < count; i++)
				length += Vector2.Distance(path[i - 1], path[i]);
			return length;
		}

		private static Vector2 SamplePath(Vector2[] path, int count, float distance)
		{
			if (distance <= 0f)
				return path[0];

			float travelled = 0f;
			for (int i = 1; i < count; i++)
			{
				float segment = Vector2.Distance(path[i - 1], path[i]);
				if (segment < 0.0001f)
					continue;
				if (travelled + segment >= distance)
				{
					float t = (distance - travelled) / segment;
					return Vector2.Lerp(path[i - 1], path[i], t);
				}
				travelled += segment;
			}
			return path[count - 1];
		}

		private float MeasureChain()
		{
			float length = 0f;
			for (int i = 0; i < Last; i++)
				length += Vector2.Distance(points[i], points[i + 1]);
			return length;
		}

		// ----------------------------------------------------------- physics

		private void SolveConstraints(Vector2 player, Vector2 hook, bool reverse)
		{
			points[0] = player;
			points[Last] = hook;

			for (int pass = 0; pass < Last; pass++)
			{
				// Alternating sweep direction keeps tension from bunching at
				// whichever end was solved last. With both ends pinned this is
				// important: a one-way sweep can leave a single stretched
				// segment on the hook side while every other segment is tight.
				int i = reverse ? Last - 1 - pass : pass;
				Vector2 difference = points[i + 1] - points[i];
				float distance = difference.Length();
				if (distance < 0.001f)
					continue;

				float error = distance - segmentLength;
				Vector2 correction = difference / distance * error;

				if (i == 0)
				{
					points[i + 1] -= correction;
				}
				else if (i + 1 == Last)
				{
					points[i] += correction;
				}
				else
				{
					points[i] += correction * 0.5f;
					points[i + 1] -= correction * 0.5f;
				}
			}

			points[0] = player;
			points[Last] = hook;
		}

		private Vector2 MoveNode(Vector2 start, Vector2 target, ref Vector2 velocity)
		{
			Vector2 movement = target - start;
			if (movement.LengthSquared() < 0.0001f)
			{
				if (!RopeCollides(start))
					return start;
				return FindSafePosition(start);
			}

			Vector2 safe = start;
			bool collided = false;

			for (int step = 1; step <= 9; step++)
			{
				Vector2 test = Vector2.Lerp(start, target, step / 9f);
				if (RopeCollides(test))
				{
					collided = true;
					break;
				}
				safe = test;
			}

			if (!collided)
				return safe;

			Vector2 actual = safe - start;
			Vector2 blocked = movement - actual;
			Vector2 normal = SafeNormalize(blocked, -velocity, Vector2.UnitY);

			float into = Vector2.Dot(velocity, normal);
			if (into > 0f)
				velocity -= normal * into;

			Vector2 tangent = normal.RotatedBy(MathHelper.PiOver2);
			float tangentSpeed = Vector2.Dot(velocity, tangent);
			velocity = tangent * tangentSpeed * Friction + normal * Bounce;

			if (RopeCollides(safe))
				safe = FindSafePosition(safe);

			return safe;
		}

		private void ResolveAllTiles()
		{
			for (int i = 1; i < Last; i++)
			{
				if (RopeCollides(points[i]))
					PushNodeOutOfTiles(i);
			}

			for (int pass = 0; pass < WrapPasses; pass++)
			{
				for (int i = 0; i < Last; i++)
					ResolveRopeSegment(i, i + 1);
			}

			for (int i = 1; i < Last; i++)
			{
				if (RopeCollides(points[i]))
					PushNodeOutOfTiles(i);
			}
		}

		/// <summary>
		/// If a segment is cutting through a tile, rotate its free node around
		/// the fixed node by the smallest angle that puts the same-length
		/// segment outside the tile. Keeping the length is what lets the rope
		/// bend around a corner without visibly shortening.
		/// </summary>
		private bool ResolveRopeSegment(int firstIndex, int secondIndex)
		{
			if (firstIndex < 0 || secondIndex <= 0 || secondIndex > Last)
				return false;

			bool parentPinned = firstIndex == 0;
			bool childPinned = secondIndex == Last;
			if (parentPinned && childPinned)
				return false;

			int movable = childPinned ? firstIndex : secondIndex;
			int anchor = childPinned ? secondIndex : firstIndex;

			Vector2 anchorPosition = points[anchor];
			Vector2 difference = points[movable] - anchorPosition;
			float length = difference.Length();
			if (length < 0.001f)
				return false;

			if (!SegmentCollides(points[firstIndex], points[secondIndex])
				&& !RopeCollides(points[movable]))
			{
				return false;
			}

			float currentAngle = difference.ToRotation();
			Vector2 bestPosition = Vector2.Zero;
			float bestDifference = float.MaxValue;
			bool found = false;

			for (int step = 1; step <= WrapSearchSteps; step++)
			{
				float offset = step * WrapSearchStepDegrees * MathHelper.Pi / 180f;

				for (int direction = -1; direction <= 1; direction += 2)
				{
					float testAngle = currentAngle + offset * direction;
					Vector2 candidate = anchorPosition
						+ testAngle.ToRotationVector2() * length;

					if (RopeCollides(candidate))
						continue;
					if (SegmentCollides(anchorPosition, candidate))
						continue;

					float angularDifference = Math.Abs(MathHelper.WrapAngle(
						testAngle - currentAngle));
					if (angularDifference < bestDifference)
					{
						bestDifference = angularDifference;
						bestPosition = candidate;
						found = true;
					}
				}

				if (found)
					break;
			}

			if (!found)
				return false;

			points[movable] = bestPosition;

			Vector2 velocity = points[movable] - previous[movable];
			Vector2 radial = SafeNormalize(points[movable] - anchorPosition,
				Vector2.UnitX, Vector2.UnitX);
			Vector2 tangent = radial.RotatedBy(MathHelper.PiOver2);
			float tangentSpeed = Vector2.Dot(velocity, tangent);
			velocity = tangent * tangentSpeed * Friction;
			previous[movable] = points[movable] - velocity;

			return true;
		}

		private void PushNodeOutOfTiles(int index)
		{
			if (!RopeCollides(points[index]))
				return;

			Vector2 safe = FindSafePosition(points[index]);
			points[index] = safe;

			Vector2 velocity = safe - previous[index];
			velocity *= 0.35f;
			previous[index] = safe - velocity;
		}

		private Vector2 FindSafePosition(Vector2 position)
		{
			if (!RopeCollides(position))
				return position;

			const int AngleSamples = 24;
			for (float distance = 1f; distance <= EscapeDistance; distance += 1f)
			{
				for (int sample = 0; sample < AngleSamples; sample++)
				{
					float angle = MathHelper.TwoPi * sample / AngleSamples;
					Vector2 test = position + angle.ToRotationVector2() * distance;
					if (!RopeCollides(test))
						return test;
				}
			}

			return position;
		}

		// --------------------------------------------------------- collision

		/// <summary>
		/// Node-vs-tile test using the hook's own blocker rule, so platforms
		/// remain rope-passable while solid blocks hold the rope.
		/// </summary>
		private bool RopeCollides(Vector2 position)
		{
			float left = position.X - NodeRadius;
			float right = position.X + NodeRadius;
			float top = position.Y - NodeRadius;
			float bottom = position.Y + NodeRadius;

			int minX = (int)Math.Floor(left / 16f);
			int maxX = (int)Math.Floor(right / 16f);
			int minY = (int)Math.Floor(top / 16f);
			int maxY = (int)Math.Floor(bottom / 16f);

			for (int y = minY; y <= maxY; y++)
			{
				for (int x = minX; x <= maxX; x++)
				{
					if (isBlocking(x, y))
						return true;
				}
			}

			return false;
		}

		private bool NearBlockingTile(Vector2 position, float radius)
		{
			float left = position.X - radius;
			float right = position.X + radius;
			float top = position.Y - radius;
			float bottom = position.Y + radius;

			int minX = (int)Math.Floor(left / 16f);
			int maxX = (int)Math.Floor(right / 16f);
			int minY = (int)Math.Floor(top / 16f);
			int maxY = (int)Math.Floor(bottom / 16f);

			for (int y = minY; y <= maxY; y++)
			{
				for (int x = minX; x <= maxX; x++)
				{
					if (isBlocking(x, y))
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Thin line-of-sight test used only for pivot classification. This is
		/// deliberately not <see cref="SegmentCollides"/>: a taut rope resting
		/// just above a floor would graze it with the rope radius and look
		/// blocked, while the actual straight line is clear.
		/// </summary>
		private bool LineBlocked(Vector2 start, Vector2 end)
		{
			float length = Vector2.Distance(start, end);
			if (length < 0.001f)
				return false;

			int samples = Math.Max(2, (int)Math.Ceiling(length / 4f));
			for (int sample = 0; sample <= samples; sample++)
			{
				Vector2 point = Vector2.Lerp(start, end, sample / (float)samples);
				int x = (int)Math.Floor(point.X / 16f);
				int y = (int)Math.Floor(point.Y / 16f);
				if (isBlocking(x, y))
					return true;
			}

			return false;
		}

		private bool SegmentCollides(Vector2 start, Vector2 end)
		{
			Vector2 difference = end - start;
			float length = difference.Length();
			if (length < 0.001f)
				return RopeCollides(start);

			int samples = Math.Max(2,
				(int)Math.Ceiling(length / SegmentCollisionSpacing));
			for (int sample = 0; sample <= samples; sample++)
			{
				float progress = sample / (float)samples;
				if (RopeCollides(Vector2.Lerp(start, end, progress)))
					return true;
			}

			return false;
		}

		// ------------------------------------------------------------ helpers

		private static Vector2 SafeNormalize(Vector2 value, Vector2 fallback,
			Vector2 secondFallback)
		{
			float length = value.Length();
			if (length > 0.0001f)
				return value / length;
			length = fallback.Length();
			if (length > 0.0001f)
				return fallback / length;
			return secondFallback;
		}

		private static bool IsFinite(Vector2 value)
		{
			return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
				&& !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
		}
	}
}
