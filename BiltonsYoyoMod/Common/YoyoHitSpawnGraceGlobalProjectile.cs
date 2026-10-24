using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Common
{
	// Projectiles spawned by a deployed yoyo (stars, wisps, sparks, bolts,
	// summons) stay visible but harmless for 1 second so the player can see
	// them, then start hitting. Short-lived sparks (timeLeft < 2s) keep
	// instant hits so they never become harmless decorations.
	public class YoyoHitSpawnGraceGlobalProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		private int grace;

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			if (!projectile.friendly || projectile.hostile)
				return;
			if (projectile.timeLeft < 120)
				return;
			// Coin bolts hit instantly by design (and Six Stringer's own
			// throws were never graced: only yoyo-spawned children are).
			if (projectile.ModProjectile?.GetType().Name == "CoinBoltProj")
				return;
			// New mini-boss yoyo children opt out: they are designed for instant
			// point-blank contact (the yoyo parks on the enemy), so a 1s harmless
			// window reads as "never hits". Local immunity already throttles them.
			if (projectile.ModProjectile is IYoyoInstantHit)
				return;
			if (source is EntitySource_Parent parent
				&& parent.Entity is Projectile p
				&& p.aiStyle == ProjAIStyleID.Yoyo) {
				grace = 60;
				projectile.friendly = false;
			}
		}

		public override void AI(Projectile projectile)
		{
			if (grace <= 0)
				return;
			grace--;
			if (grace <= 0)
				projectile.friendly = true;
			else
				projectile.friendly = false;
		}
	}
}
