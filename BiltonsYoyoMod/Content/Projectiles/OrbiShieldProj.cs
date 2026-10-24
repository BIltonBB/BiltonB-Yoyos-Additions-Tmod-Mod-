using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Projectiles
{
	// Mechanical ring deployed by the Orbi-Shield Counterweight modification.
	// Orbits its yoyo (ai[0] = yoyo projectile whoAmI) and smashes minor
	// hostile projectiles on contact.
	public class OrbiShieldProj : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 26;
			Projectile.height = 26;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 60;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
		}

		public override void AI()
		{
			int yoyoWho = (int)Projectile.ai[0];
			Projectile yoyo = null;
			if (yoyoWho >= 0 && yoyoWho < Main.maxProjectiles) {
				Projectile candidate = Main.projectile[yoyoWho];
				if (candidate.active && candidate.owner == Projectile.owner && candidate.aiStyle == ProjAIStyleID.Yoyo)
					yoyo = candidate;
			}
			if (yoyo == null) {
				Projectile.Kill();
				return;
			}
			Projectile.timeLeft = 60; // Persist while the yoyo is deployed.

			// Deployment flash so the ring reads the moment it snaps in.
			if (Projectile.localAI[0] == 0f) {
				Projectile.localAI[0] = 1f;
				Lighting.AddLight(Projectile.Center, 1f, 0.9f, 0.6f);
				for (int i = 0; i < 14; i++) {
					Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.1f);
					dust.noGravity = true;
				}
				SoundEngine.PlaySound(SoundID.Item37, Projectile.Center);
			}

			Projectile.ai[1] += 0.12f;
			Projectile.Center = yoyo.Center + new Vector2(56f, 0f).RotatedBy(Projectile.ai[1]);
			Projectile.rotation += 0.3f;
			// Breathing ring: gentle scale pulse + constant spark trail.
			Projectile.scale = 1f + 0.08f * (float)System.Math.Sin(Main.GameUpdateCount * 0.2f);
			Lighting.AddLight(Projectile.Center, 0.35f, 0.3f, 0.2f);
			if (Main.rand.NextBool(3)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 120, default, 0.8f);
				dust.noGravity = true;
			}

			// Smash minor hostile projectiles (server-authoritative so they stay dead).
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile hostile = Main.projectile[i];
				if (hostile == null || !hostile.active || !hostile.hostile || hostile.damage > 150)
					continue;
				if (Vector2.DistanceSquared(hostile.Center, Projectile.Center) > 36f * 36f)
					continue;
				for (int d = 0; d < 6; d++) {
					Dust dust = Dust.NewDustDirect(hostile.position, hostile.width, hostile.height, DustID.GoldCoin, 0f, 0f, 100, default, 0.8f);
					dust.noGravity = true;
				}
				SoundEngine.PlaySound(SoundID.Item1, hostile.Center);
				if (Main.netMode != NetmodeID.MultiplayerClient)
					hostile.Kill();
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 4; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, 0f, 0f, 120, default, 0.7f);
				dust.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White * 0.9f, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
			return false;
		}
	}
}
