using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod
{
	/// <summary>
	/// One source of truth for training-dummy motion. A Target Dummy's sprite is
	/// anchored to its placement, but its hitbox is the live NPC rectangle. Any
	/// knockback or hand-written velocity/pull moves that rectangle out from
	/// under the sprite, so later shots look like they miss. Every manual motion
	/// force must ask this helper first; the GlobalNPC below is the safety net
	/// for forces added by future code or by other mods.
	/// </summary>
	internal static class DummySafety
	{
		public static bool IsTrainingDummy(NPC npc)
		{
			if (npc == null || !npc.active)
				return false;

			// Vanilla's Target Dummy is immortal. Modded test dummies follow the
			// same "immortal but damageable, not a boss/town NPC" shape.
			if (npc.type == NPCID.TargetDummy)
				return true;

			return npc.immortal && !npc.boss && !npc.friendly && !npc.townNPC
				&& npc.lifeMax > 5 && !npc.dontTakeDamage;
		}

		/// <summary>
		/// Stops a dummy dead in its tracks. Returns true when the caller should
		/// skip its own push entirely (so it cannot re-add velocity this tick).
		/// </summary>
		public static bool Pin(NPC npc)
		{
			if (!IsTrainingDummy(npc))
				return false;

			npc.velocity = Vector2.Zero;
			return true;
		}

		public static float SafeKnockback(NPC npc, float knockback)
			=> IsTrainingDummy(npc) ? 0f : knockback;
	}

	/// <summary>
	/// Global stop for dummy motion. ModifyIncomingHit catches every damage
	/// source - including direct NPC.SimpleStrikeNPC/StrikeNPC calls that never
	/// pass through GlobalProjectile.ModifyHitNPC - while PreAI/PostAI neutralise
	/// hand-written pulls before and after the dummy's own update.
	/// </summary>
	public sealed class DummyMotionGuard : GlobalNPC
	{
		public override bool PreAI(NPC npc)
		{
			if (DummySafety.IsTrainingDummy(npc))
				npc.velocity = Vector2.Zero;
			return true;
		}

		public override void PostAI(NPC npc)
		{
			if (DummySafety.IsTrainingDummy(npc))
				npc.velocity = Vector2.Zero;
		}

		public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
		{
			if (DummySafety.IsTrainingDummy(npc))
				modifiers.Knockback *= 0f;
		}
	}
}
