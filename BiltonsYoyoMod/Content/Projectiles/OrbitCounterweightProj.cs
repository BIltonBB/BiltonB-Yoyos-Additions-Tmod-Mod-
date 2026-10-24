using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Utils;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Content.Projectiles
{
	// Ancient Counterweight's answer to the Odd Golden one: instead of being
	// thrown, it snaps into orbit the first time your yoyo hits something.
	// ai[0] = orbit radius, ai[1] = orbit speed (signed), ai[2] = angle.
	// Releasing the yoyo (no active yoyo) despawns it, resetting for next hit.
	public class OrbitCounterweightProj : ModProjectile
	{
		// Shares the thrown Ancient counterweight's sprite.
		public override string Texture => "BiltonsYoyoMod/Content/Projectiles/AncientCounterweightProj";
		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 60;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			bool valid = owner.active && !owner.dead && owner.GetModPlayer<MarketPlayer>().AncientCounterweight;
			if (valid) {
				bool yoyoOut = false;
				for (int i = 0; i < Main.maxProjectiles; i++) {
					Projectile proj = Main.projectile[i];
					if (proj.active && proj.owner == Projectile.owner && proj.aiStyle == ProjAIStyleID.Yoyo) {
						yoyoOut = true;
						break;
					}
				}
				valid = yoyoOut;
			}
			if (!valid) {
				Projectile.Kill();
				return;
			}
			Projectile.timeLeft = 60;

			Projectile.ai[2] += Projectile.ai[1];
			Projectile.Center = owner.Center + new Vector2(Projectile.ai[0], 0f).RotatedBy(Projectile.ai[2]);
			Projectile.rotation += 0.4f;
			Lighting.AddLight(Projectile.Center, 0.1f, 0.25f, 0.22f);
			if (Main.rand.NextBool(10)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.MagicMirror, 0f, 0f, 120, default, 0.7f);
				dust.noGravity = true;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Smacked something: swing the orbit the other way.
			Projectile.ai[1] = -Projectile.ai[1];
			for (int i = 0; i < 10; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.MagicMirror, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 120, default, 1f);
				dust.noGravity = true;
			}
			SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.Center);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Player owner = Main.player[Projectile.owner];
			if (owner.active)
				YoyoDrawing.DrawString(owner.Center, Projectile.Center, new Color(170, 235, 225), 1.6f);
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
			return false;
		}
	}
}
