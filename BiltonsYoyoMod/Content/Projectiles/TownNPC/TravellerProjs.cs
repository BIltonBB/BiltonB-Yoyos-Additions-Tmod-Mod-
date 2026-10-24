using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Projectiles.TownNPC
{
	// Thrown (non-string) projectiles for Six Stringer and Yoyo Cultist.

	public class TGoldYoyoProj : ModProjectile
	{
		private readonly List<Vector2> trail = new();
		private int bounces; // Pre-Hardmode: weaker throw, 3 tile bounces then breaks.

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.damage = 25;
			Projectile.penetrate = 3; // 3 enemy hits (less than Hardmode infinite).
			Projectile.tileCollide = true;
			Projectile.timeLeft = 600; // 10 seconds.
			Projectile.aiStyle = ProjAIStyleID.ThrownProjectile; // gravity-affected, like shurikens.
			AIType = ProjectileID.Shuriken;
		}

		public override void OnSpawn(IEntitySource source)
		{
			trail.Clear(); // Slots are pooled: never inherit a previous life's trail.
			bounces = 0;
		}

		public override void AI()
		{
			trail.Add(Projectile.Center);
			while (trail.Count > 6)
				trail.RemoveAt(0);
			Lighting.AddLight(Projectile.Center, 0.2f, 0.15f, 0.05f);
			if (Main.rand.NextBool(6)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, 0f, 0f, 150, default, 0.7f);
				dust.noGravity = true;
			}
			Projectile.rotation += 0.5f;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			// Pre-Hardmode: bounce off tiles up to 3 times with coin sfx + gold effect, then break.
			if (bounces < 3) {
				bounces++;
				if (Projectile.velocity.X != oldVelocity.X)
					Projectile.velocity.X = -oldVelocity.X * 0.9f;
				if (Projectile.velocity.Y != oldVelocity.Y)
					Projectile.velocity.Y = -oldVelocity.Y * 0.9f;
				for (int i = 0; i < 5; i++)
					Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, Main.rand.NextFloat(-2f, 2f), -1f, 120, default, 0.8f);
				SoundEngine.PlaySound(SoundID.CoinPickup, Projectile.Center);
				return false;
			}
			for (int i = 0; i < 5; i++)
				Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, Main.rand.NextFloat(-2f, 2f), -1f, 120, default, 0.8f);
			SoundEngine.PlaySound(SoundID.CoinPickup, Projectile.Center);
			return true;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 5; i++)
				Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, 0f, -1f, 120, default, 0.8f);
			SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.Center);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
			for (int i = 0; i < trail.Count; i++) {
				float alpha = (i + 1) / (float)trail.Count * 0.3f;
				Main.spriteBatch.Draw(tex, trail[i] - Main.screenPosition, null, Color.Gold * alpha, Projectile.rotation, origin, Projectile.scale * 0.85f, SpriteEffects.None, 0f);
			}
			return true;
		}
	}

	public class TSuperGoldYoyoProj : ModProjectile
	{
		private readonly List<Vector2> trail = new();

		public override void SetDefaults()
		{
			Projectile.width = 22;
			Projectile.height = 22;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.damage = 65;
			Projectile.penetrate = -1; // Infinite piercing.
			Projectile.tileCollide = true;
			Projectile.timeLeft = 600;
			Projectile.extraUpdates = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 12;
		}

		public override void OnSpawn(IEntitySource source)
		{
			trail.Clear();
		}

		public override void AI()
		{
			trail.Add(Projectile.Center);
			while (trail.Count > 9)
				trail.RemoveAt(0);
			Projectile.rotation += 0.5f;
			Lighting.AddLight(Projectile.Center, 0.5f, 0.35f, 0.1f);
			Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldFlame, 0f, 0f, 100, default, 0.9f);
			dust.noGravity = true;
			// Keep momentum high like a richochet monster.
			if (Projectile.velocity.Length() < 10f)
				Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 10f;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			// Bounce off tiles with a golden crash.
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.98f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.98f;
			for (int i = 0; i < 8; i++)
				Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldFlame, 0f, 0f, 100, default, 1f);
			SoundEngine.PlaySound(SoundID.CoinPickup, Projectile.Center);
			return false;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 6; i++)
				Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldFlame, 0f, 0f, 100, default, 1f);
			SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.Center);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
			for (int i = 0; i < trail.Count; i++) {
				float alpha = (i + 1) / (float)trail.Count * 0.4f;
				Main.spriteBatch.Draw(tex, trail[i] - Main.screenPosition, null, Color.Gold * alpha, Projectile.rotation, origin, Projectile.scale * 0.9f, SpriteEffects.None, 0f);
			}
			return true;
		}
	}

	public abstract class CultistMagicProj : ModProjectile
	{
		public virtual float HomingStrength => 0.06f;
		public virtual int MagicDust => DustID.Shadowflame;

		private readonly List<Vector2> trail = new();

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = 1;
			Projectile.tileCollide = false; // Does not break on the ground.
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 600; // 10 seconds.
			Projectile.extraUpdates = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
		}

		public override void OnSpawn(IEntitySource source)
		{
			trail.Clear();
		}

		public override void AI()
		{
			trail.Add(Projectile.Center);
			while (trail.Count > 8)
				trail.RemoveAt(0);
			Projectile.rotation += 0.25f;
			Lighting.AddLight(Projectile.Center, 0.4f, 0.2f, 0.6f);
			if (Main.rand.NextBool(3)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, MagicDust, 0f, 0f, 120, default, 0.9f);
				dust.noGravity = true;
			}
			NPC target = null;
			float bestDist = 700f * 700f;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;
				float dist = Vector2.DistanceSquared(Projectile.Center, npc.Center);
				if (dist < bestDist) {
					bestDist = dist;
					target = npc;
				}
			}
			if (target != null) {
				// Homing tightens as it closes in.
				float closeness = MathHelper.Clamp(1f - (float)System.Math.Sqrt(bestDist) / 700f, 0f, 1f);
				float strength = HomingStrength * (1f + closeness * 1.5f);
				Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, strength);
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 8; i++)
				Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, MagicDust, 0f, 0f, 100, default, 1f);
			SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.Center);
		}

		public override void OnKill(int timeLeft)
		{
			for (int i = 0; i < 6; i++)
				Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, MagicDust, 0f, 0f, 150, default, 0.8f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
			for (int i = 0; i < trail.Count; i++) {
				float alpha = (i + 1) / (float)trail.Count * 0.4f;
				Main.spriteBatch.Draw(tex, trail[i] - Main.screenPosition, null, new Color(200, 150, 255) * alpha, Projectile.rotation, origin, Projectile.scale * 0.85f, SpriteEffects.None, 0f);
			}
			return true;
		}
	}

	public class TMagicYoyoProj : CultistMagicProj
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.damage = 35;
			// Pierces one enemy so homing survives the first hit.
			Projectile.penetrate = 2;
		}
	}

	public class TSuperMagicYoyoProj : CultistMagicProj
	{
		public override float HomingStrength => 0.12f; // Slight but certain homing in Hardmode.
		public override int MagicDust => DustID.PurpleTorch;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.damage = 80;
			// Pierces two enemies so homing survives the first hits.
			Projectile.penetrate = 3;
		}

		public override void AI()
		{
			base.AI();
			Lighting.AddLight(Projectile.Center, 0.5f, 0.25f, 0.7f);
		}
	}
}
