using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
	// Relic sparks shed by the Ancient Yoyo Relic: orbit the yoyo that shed
	// them and nip enemies. ai[0] = yoyo projectile whoAmI.
	public class AncientSparkProj : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 2;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 300;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 25;
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
			Projectile.ai[1] += 0.2f;
			Projectile.Center = yoyo.Center + new Vector2(44f, 0f).RotatedBy(Projectile.ai[1]);
			Projectile.rotation += 0.6f;
			Lighting.AddLight(Projectile.Center, 0.3f, 0.25f, 0.1f);
			if (Main.rand.NextBool(6)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Sandnado, 0f, 0f, 120, default, 0.7f);
				dust.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, new Color(255, 225, 160, 220), Projectile.rotation, tex.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0f);
			return false;
		}
	}
}
