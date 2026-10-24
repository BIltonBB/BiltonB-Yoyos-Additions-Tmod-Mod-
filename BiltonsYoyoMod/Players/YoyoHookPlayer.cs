using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
	/// <summary>
	/// Swinging physics for the Yoyo Hook.
	/// <para/>
	/// Runs only on the owner's client (player movement is client-authoritative),
	/// so nothing here can desync a server or fight another player's input. It is
	/// a genuine pendulum: gravity keeps doing its normal job, and this class only
	/// (a) cancels the rope's outward pull so the rope can never push, (b) reels
	/// the rope in/out for W/S, (c) adds a small A/D lateral nudge, and (d) folds
	/// the player back onto the rope circle when they would otherwise stretch it.
	/// It never adds an auto-reel toward the anchor, so all swing momentum comes
	/// from the player's own motion.
	/// </summary>
	public class YoyoHookPlayer : ModPlayer
	{
		/// <summary>Horizontal thrust per tick while A/D are held (on top of vanilla air control).</summary>
		public const float LateralAssist = 0.2f;
		/// <summary>Safety ceiling; nothing in a normal swing should reach this.</summary>
		public const float MaxSwingSpeed = 26f;
		/// <summary>Cap on how far the rope correction may move the player in one tick.</summary>
		public const float MaxRopeCorrection = 12f;
		/// <summary>Fraction of vanilla's airborne horizontal drag that is given back while swinging.</summary>
		public const float MomentumRetention = 0.75f;
		/// <summary>
		/// A single-tick position change larger than this is a teleport (mirror,
		/// rod, portal), not a swing. Breaking on it avoids yanking the player
		/// back across the map after they teleport.
		/// </summary>
		public const float TeleportBreakDistance = 160f;

		/// <summary>Index of this player's hook in <see cref="Main.projectile"/>, or -1.</summary>
		public int HookIndex = -1;

		private float preMoveVelocityX;
		private bool prevControlJump;
		private int airborneTicks;
		private Vector2 lastCenter;
		private bool hasLastCenter;

		/// <summary>The player's hook, but only while it is actually anchored.</summary>
		public Projectile AnchoredHook
		{
			get
			{
				// Fast path: the item's Shoot() caches the index it spawned.
				int idx = HookIndex;
				if (idx >= 0 && idx < Main.maxProjectiles)
				{
					Projectile p = Main.projectile[idx];
					if (p != null && p.active && p.owner == Player.whoAmI
						&& p.type == Projectiles.Utility.YoyoHookProj.HookType
						&& (int)p.ai[0] == (int)Projectiles.Utility.YoyoHookProj.StateAnchored)
						return p;
				}
				// Slow path: firing from the grappling hook slot goes through
				// vanilla's QuickGrapple, which builds the projectile itself and
				// never tells us the index, so scan for it instead and cache the
				// result. Without this cache an idle player with no hook would
				// scan all 1000 projectile slots every tick.
				Projectile found = Projectiles.Utility.YoyoHookProj.FindAnchored(Player.whoAmI);
				if (found != null)
					HookIndex = found.whoAmI;
				return found;
			}
		}

		public override void PostUpdateRunSpeeds()
		{
			// Snapshot the horizontal velocity just before vanilla's
			// HorizontalMovement() applies its ground/air drag, so the swing can
			// hand most of that drag straight back and keep its momentum.
			preMoveVelocityX = Player.velocity.X;
		}

		public override void PreUpdateMovement()
		{
			// Client-authoritative movement: only the owner's own client simulates.
			if (Main.dedServ || Player.whoAmI != Main.myPlayer)
			{
				prevControlJump = Player.controlJump;
				return;
			}

			if (!Player.active || Player.dead)
			{
				HookIndex = -1;
				hasLastCenter = false;
				prevControlJump = Player.controlJump;
				return;
			}

			// A teleport moves farther in one tick than any swing can. Let go
			// instead of reeling the player back across the map.
			Vector2 center = Player.MountedCenter;
			if (hasLastCenter && Vector2.DistanceSquared(center, lastCenter)
				> TeleportBreakDistance * TeleportBreakDistance)
			{
				Projectiles.Utility.YoyoHookProj.ReleaseHook(Player.whoAmI);
				HookIndex = -1;
				lastCenter = center;
				prevControlJump = Player.controlJump;
				return;
			}
			lastCenter = center;
			hasLastCenter = true;

			Projectile hook = AnchoredHook;

			// A fresh jump detaches the hook and launches, vanilla-grapple style,
			// carrying the whole swing's momentum into the jump.
			if (hook != null && Player.controlJump && !prevControlJump)
			{
				Projectiles.Utility.YoyoHookProj.ReleaseHook(Player.whoAmI);
				prevControlJump = true;
				return;
			}
			prevControlJump = Player.controlJump;

			if (hook == null || hook.ModProjectile is not Projectiles.Utility.YoyoHookProj hookProj)
				return;

			// Swing around the first real bend in the simulated rope, not the
			// hook. The chain from that bend to the hook is already spent; only
			// the free segment from the player to the bend can swing. Because
			// the bend comes from the persistent chain, it cannot hop to the
			// other side of an island when a shorter route appears.
			hookProj.GetRopePivot(out Vector2 pivot, out float wrappedLength);

			Vector2 toPlayer = center - pivot;
			float dist = toPlayer.Length();
			if (dist < 0.001f)
				return;
			Vector2 dir = toPlayer / dist;

			// Track "recently airborne" so momentum retention can't turn grounded
			// walking with a taut rope into an ice rink (velocity.Y is briefly 0 at
			// the bottom of a swing, so a single-tick test is not enough).
			if (Player.velocity.Y != 0f)
				airborneTicks = 8;
			else if (airborneTicks > 0)
				airborneTicks--;

			float len = MathHelper.Clamp(hook.ai[1],
				Projectiles.Utility.YoyoHookProj.MinLength, Projectiles.Utility.YoyoHookProj.MaxLength);

			// The wrapped part of the rope is not available to the pendulum. A
			// small minimum keeps the first-contact pivot stable if W is held
			// until the free segment is almost gone; the correction is still
			// capped per tick, so this never reads as a teleport.
			float freeLength = MathHelper.Clamp(len - wrappedLength, 12f, len);

			// A/D lateral control. Applied before the tension pass so only its
			// tangential component survives - near the ends of the arc it naturally
			// does less, exactly like pushing a real swing.
			if (Player.controlLeft)
				Player.velocity.X -= LateralAssist;
			if (Player.controlRight)
				Player.velocity.X += LateralAssist;

			bool taut = dist >= freeLength - 1.5f;

			if (taut)
			{
				// Rope tension: cancel any velocity pulling the player away from the
				// pivot. The rope can only pull, never push, so inward motion is
				// left alone and the rope simply goes slack.
				float radial = Vector2.Dot(Player.velocity, dir);
				if (radial > 0f)
					Player.velocity -= dir * radial;

				// Give back most of vanilla's airborne horizontal drag, but only for
				// genuine swing speed (above run speed) while actually off the ground,
				// so walking with a taut rope still stops normally.
				if (airborneTicks > 0 && !Player.controlLeft && !Player.controlRight && !Player.mount.Active
					&& Math.Abs(Player.velocity.X) > Player.maxRunSpeed)
				{
					float lost = preMoveVelocityX - Player.velocity.X;
					Player.velocity.X += lost * MomentumRetention;
				}
			}

			// Belt-and-braces speed cap for stability.
			float speed = Player.velocity.Length();
			if (speed > MaxSwingSpeed)
				Player.velocity *= MaxSwingSpeed / speed;

			// Hard length constraint: if the rope is stretched, fold the player back
			// onto the circle around the pivot. Tile collision is honoured so this
			// can never push the player through terrain, even during a fast swing.
			float stretch = dist - freeLength;
			if (stretch > 2f)
			{
				// Proportional and capped so a sudden overshoot (a fast fall, or the
				// hook reeling in) reads as the rope coming taut instead of as a
				// teleport onto the circle.
				float correction = MathHelper.Min(stretch * 0.5f, MaxRopeCorrection);
				Vector2 inward = dir * -correction;
				Vector2 allowed = Collision.TileCollision(Player.position, inward,
					Player.width, Player.height, false, false, (int)Player.gravDir);
				Player.position += allowed;
			}
		}
	}
}
