using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
	// Super Gold Yoyo: heavy golden slammer. No trail ghosts (they read as
	// a second yoyo), no hitbox dragging - knockback only, plus Midas.
	public class SuperGoldYoyoProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/SuperGoldYoyoProj";

		public override void SetDefaults()
		{
			Projectile.width = 22;
			Projectile.height = 22;
			Projectile.scale = 1.3f;
			Projectile.aiStyle = ProjAIStyleID.Yoyo;
			Projectile.friendly = true;
			Projectile.penetrate = -1;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 7f;
			ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 265f;
			ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 9f;
		}

		public override void AI()
		{
			Lighting.AddLight(Projectile.Center, 0.3f, 0.25f, 0.08f);
			if (Main.rand.NextBool(5)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, 0f, 0f, 120, default, 0.8f);
				dust.noGravity = true;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 14; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1f);
				dust.noGravity = true;
			}
			target.AddBuff(BuffID.Midas, 120);
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.NPCHit1, Projectile.Center);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation,
				tex.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0f);
			return false;
		}
	}
}
