using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Common
{
	// On-hit procs for the black-market and relic accessories.
	public class MarketProcGlobalProjectile : GlobalProjectile
	{
		public override bool AppliesToEntity(Projectile projectile, bool lateInstantiation)
		{
			return projectile.aiStyle == ProjAIStyleID.Yoyo && projectile.friendly && projectile.owner >= 0;
		}

		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;
			Player owner = Main.player[projectile.owner];
			if (owner == null || !owner.active)
				return;
			MarketPlayer market = owner.GetModPlayer<MarketPlayer>();

			if (market.GoldenCounterweight && Main.rand.NextBool(7)) { // ~15% chance.
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					int coinType = Main.rand.NextBool(6) ? ItemID.SilverCoin : ItemID.CopperCoin;
					int stack = coinType == ItemID.SilverCoin ? 1 : Main.rand.Next(1, 4);
					Item.NewItem(projectile.GetSource_OnHit(target), target.Center, coinType, stack);
				}
			}

			// Ancient Relic: yoyo hits shed orbiting relic sparks.
			if (market.AncientRelic && Main.rand.NextFloat(100f) < 30f && Main.myPlayer == projectile.owner) {
				// Spread the three sparks around the orbit (separate ai[1]
				// phases): spawning them all at phase 0 stacked them exactly
				// on top of each other, reading as one doubled blob dealing
				// triple contact damage. Matches the glove kill-burst pattern.
				float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
				for (int s = 0; s < 3; s++) {
					float phase = baseAngle + s / 3f * MathHelper.TwoPi;
					Vector2 dir = new Vector2(6f, 0f).RotatedBy(phase);
					Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, dir,
						ModContent.ProjectileType<Projectiles.AncientSparkProj>(), (int)(projectile.damage * 0.2f), 1f, projectile.owner,
						ai0: projectile.whoAmI, ai1: phase);
				}
			}

			if (market.CelestialBlessing && Main.rand.NextFloat(100f) < 8f) {
				// Hearts and stars fall only for the main yoyo's kills.
				if (Main.netMode != NetmodeID.MultiplayerClient
					&& YoyoTownShared.IsMainYoyo(projectile)) {
					int pickup = Main.rand.NextBool() ? ItemID.Heart : ItemID.Star;
					Item.NewItem(projectile.GetSource_OnHit(target), target.Center, pickup);
				}
			}

			// Ancient Counterweight: first yoyo hit snaps a weight into orbit
			// (random distance + speed, like a true vanilla counterweight).
			if (market.AncientCounterweight && Main.myPlayer == projectile.owner) {
				bool orbiting = false;
				for (int i = 0; i < Main.maxProjectiles; i++) {
					Projectile proj = Main.projectile[i];
					if (proj.active && proj.owner == projectile.owner && proj.ModProjectile is Content.Projectiles.OrbitCounterweightProj) {
						orbiting = true;
						break;
					}
				}
				if (!orbiting) {
					float radius = Main.rand.NextFloat(70f, 120f);
					float speed = Main.rand.NextFloat(0.08f, 0.16f) * (Main.rand.NextBool() ? 1f : -1f);
					Projectile.NewProjectile(projectile.GetSource_FromThis(), owner.Center, Vector2.Zero,
						ModContent.ProjectileType<Content.Projectiles.OrbitCounterweightProj>(), (int)(projectile.damage * 0.6f), 3f, projectile.owner,
						ai0: radius, ai1: speed);
				}
			}
		}
	}
}
