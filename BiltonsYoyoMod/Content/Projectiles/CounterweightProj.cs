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
	// Shared base for the Golden / Ancient counterweight accessories,
	// vanilla-style: hurled toward the cursor on a string, bounces
	// off tiles (reversing away), smacks enemies, then flies home.
	// ai[1]: 0 = flying out, 1 = returning.
	public abstract class CounterweightProjBase : ModProjectile
	{
		protected virtual bool Gold => true;
		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = true; // Bounces like vanilla.
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 600;
			// NOTE: no extraUpdates. The 90-tick flight timer and 14/16 throw
			// speeds are authored as single-update values.
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];
			bool gold = Gold;
			bool valid = owner.active && !owner.dead;
			if (valid) {
				var market = owner.GetModPlayer<MarketPlayer>();
				valid = gold ? market.GoldenCounterweight : market.AncientCounterweight;
			}
			if (!valid) {
				Projectile.Kill();
				return;
			}

			Projectile.ai[2]++;
			Projectile.rotation += 0.5f;
			if (gold)
				Lighting.AddLight(Projectile.Center, 0.25f, 0.2f, 0.05f);
			else
				Lighting.AddLight(Projectile.Center, 0.1f, 0.25f, 0.22f);
			if (Main.rand.NextBool(10)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					gold ? DustID.GoldCoin : DustID.MagicMirror, 0f, 0f, 120, default, 0.7f);
				dust.noGravity = true;
			}

			if (Projectile.ai[1] == 0) {
				// Flying out: slight gravity, then head home after ~1.5s.
				Projectile.velocity.Y += 0.12f;
				if (Projectile.velocity.Length() > 18f)
					Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 18f;
				if (Projectile.ai[2] > 90)
					Projectile.ai[1] = 1;
			}
			else {
				// Returning to the owner's hand: phase through tiles so the trip
				// home can never get stuck bouncing in a pit (and the string with it).
				Projectile.tileCollide = false;
				Vector2 toOwner = owner.Center - Projectile.Center;
				if (toOwner.Length() < 28f) {
					Projectile.Kill();
					return;
				}
				Projectile.velocity = toOwner.SafeNormalize(Vector2.Zero) * 16f;
			}
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			// Bounce away like a vanilla counterweight, keeping most momentum.
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.85f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.85f;
			for (int i = 0; i < 5; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					Gold ? DustID.GoldCoin : DustID.MagicMirror, 0f, -1f, 120, default, 0.8f);
				dust.noGravity = true;
			}
			SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);
			// A hard bounce sends it home early.
			if (oldVelocity.Length() > 12f)
				Projectile.ai[1] = 1;
			return false;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 5; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					Gold ? DustID.GoldCoin : DustID.MagicMirror, 0f, -1f, 120, default, 0.8f);
				dust.noGravity = true;
			}
			SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.Center);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Player owner = Main.player[Projectile.owner];
			if (owner.active) {
				Color stringTint = Gold ? new Color(255, 220, 130) : new Color(170, 235, 225);
				YoyoDrawing.DrawString(owner.Center, Projectile.Center, stringTint, 1.6f);
			}
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
			Color tint = Gold ? Color.White : new Color(170, 235, 225);
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, tint, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
			return false;
		}
	}
}
