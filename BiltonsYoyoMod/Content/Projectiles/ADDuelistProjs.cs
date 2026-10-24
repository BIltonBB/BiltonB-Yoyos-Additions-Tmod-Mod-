using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Utils;

namespace BiltonsYoyoMod.Content.Projectiles
{
	// The Ancient Duelist's thrown yoyo: hostile flail anchored to the boss,
	// with its own counterweight on a string. ai[0] = boss NPC whoAmI,
	// ai[1] = state (0 launch, 1 deployed, 2 return), ai[2] = timer.
	public class ADYoyoProj : ModProjectile
	{
		public virtual int LifetimeTicks => 600; // 10 seconds deployed.
		public virtual float MaxRange => 420f;
		public virtual float LaunchSpeed => 11f;
		public virtual Color StringColor => new(220, 150, 80);
		public virtual int TrailDust => DustID.Torch;

		private const int StateLaunch = 0;
		private const int StateDeployed = 1;
		private const int StateReturn = 2;

		private Vector2 anchorOffset;
		private float sweepPhase;
		private float sweepDir = 1f;

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 1200;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 15;
		}

		private NPC Owner()
		{
			int ownerIndex = (int)Projectile.ai[0];
			if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs && Main.npc[ownerIndex].active)
				return Main.npc[ownerIndex];
			return null;
		}

		public override void AI()
		{
			NPC owner = Owner();
			if (owner == null) {
				Projectile.Kill();
				return;
			}
			Lighting.AddLight(Projectile.Center, 0.35f, 0.2f, 0.08f);
			if (Main.rand.NextBool(5)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, TrailDust, 0f, 0f, 120, default, 0.8f);
				dust.noGravity = true;
			}

			int state = (int)Projectile.ai[1];
			Projectile.ai[2]++;

			if (state == StateLaunch) {
				if (Projectile.ai[2] == 2)
					SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
				Projectile.velocity *= 1.01f;
				if (Projectile.ai[2] > 40 || Vector2.Distance(Projectile.Center, owner.Center) > MaxRange) {
					Projectile.ai[1] = StateDeployed;
					Projectile.ai[2] = 0;
					Projectile.velocity *= 0.2f;
					anchorOffset = Projectile.Center - owner.Center;
					if (anchorOffset.Length() > MaxRange)
						anchorOffset = anchorOffset.SafeNormalize(Vector2.Zero) * MaxRange;
					sweepPhase = Main.rand.NextFloat(MathHelper.TwoPi);
					sweepDir = Main.rand.NextBool() ? 1f : -1f;
				}
			}
			else if (state == StateDeployed) {
				sweepPhase += sweepDir * 0.075f;
				Vector2 anchorWorld = owner.Center + anchorOffset;
				float radius = MathHelper.Min(150f, 55f + Projectile.ai[2] * 0.45f);
				Vector2 radial = (Projectile.Center - anchorWorld).SafeNormalize(Vector2.UnitY);
				Vector2 tangent = radial.RotatedBy(sweepDir * MathHelper.PiOver2);
				Vector2 desired = anchorWorld + radial * radius * (0.75f + 0.25f * (float)System.Math.Sin(sweepPhase)) + tangent * 40f;
				Vector2 steer = (desired - Projectile.Center) * 0.22f;
				if (steer.Length() > 8f)
					steer = steer.SafeNormalize(Vector2.Zero) * 8f;
				Projectile.velocity = steer;
				Vector2 fromOwner = Projectile.Center - owner.Center;
				if (fromOwner.Length() > MaxRange) {
					Projectile.Center = owner.Center + fromOwner.SafeNormalize(Vector2.Zero) * MaxRange;
					anchorOffset = Projectile.Center - owner.Center;
				}
				if (Projectile.ai[2] >= LifetimeTicks) {
					Projectile.ai[1] = StateReturn;
					Projectile.ai[2] = 0;
				}
			}
			else {
				Vector2 toOwner = owner.Center - Projectile.Center;
				if (toOwner.Length() < 34f) {
					Projectile.Kill();
					return;
				}
				float speed = MathHelper.Min(10f + Projectile.ai[2] * 0.35f, LaunchSpeed + 10f);
				Projectile.velocity = toOwner.SafeNormalize(Vector2.Zero) * speed;
			}

			Projectile.rotation += 0.35f;
		}

		public override void OnHitPlayer(Player target, Player.HurtInfo info)
		{
			for (int i = 0; i < 6; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, TrailDust, 0f, -1f, 120, default, 0.9f);
				dust.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			NPC owner = Owner();
			if (owner != null) {
				Vector2 a = owner.Center - Main.screenPosition;
				Vector2 b = Projectile.Center - Main.screenPosition;
				Vector2 diff = b - a;
				if (diff.Length() > 1f) {
					Texture2D px = TextureAssets.MagicPixel.Value;
					Main.spriteBatch.Draw(px, a, new Rectangle(0, 0, 1, 1), Color.Black * 0.6f, diff.ToRotation(), new Vector2(0f, 0.5f), new Vector2(diff.Length(), 3f), SpriteEffects.None, 0f);
					Main.spriteBatch.Draw(px, a, new Rectangle(0, 0, 1, 1), YoyoDrawing.ShadeByLight(StringColor, (owner.Center + Projectile.Center) * 0.5f), diff.ToRotation(), new Vector2(0f, 0.5f), new Vector2(diff.Length(), 1.4f), SpriteEffects.None, 0f);
				}
			}
			return true;
		}
	}

	public class ADYoyoProjFast : ADYoyoProj
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/ADYoyoProj";
		public override int LifetimeTicks => 600;
		public override float MaxRange => 480f;
		public override float LaunchSpeed => 14.5f;
		public override Color StringColor => new(255, 120, 90);
		public override int TrailDust => DustID.Torch;
	}

	// The duelist's counterweight: orbits its yoyo on a string, bites on
	// contact. ai[0] = yoyo projectile whoAmI.
	public class ADCounterweightProj : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 18;
			Projectile.height = 18;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 60;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
		}

		public override void AI()
		{
			int yoyoWho = (int)Projectile.ai[0];
			Projectile yoyo = null;
			if (yoyoWho >= 0 && yoyoWho < Main.maxProjectiles) {
				Projectile candidate = Main.projectile[yoyoWho];
				if (candidate.active && (candidate.type == ModContent.ProjectileType<ADYoyoProj>() || candidate.type == ModContent.ProjectileType<ADYoyoProjFast>()))
					yoyo = candidate;
			}
			if (yoyo == null) {
				Projectile.Kill();
				return;
			}
			Projectile.timeLeft = 60;
			Projectile.ai[1] += 0.13f;
			Projectile.Center = yoyo.Center + new Vector2(64f, 0f).RotatedBy(Projectile.ai[1]);
			Projectile.rotation += 0.5f;
			Lighting.AddLight(Projectile.Center, 0.3f, 0.18f, 0.08f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			int yoyoWho = (int)Projectile.ai[0];
			if (yoyoWho >= 0 && yoyoWho < Main.maxProjectiles && Main.projectile[yoyoWho].active)
				YoyoDrawing.DrawString(Main.projectile[yoyoWho].Center, Projectile.Center, new Color(220, 170, 110), 1.4f);
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, tex.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0f);
			return false;
		}
	}

	// Trail yoyo dropped by dashes: spins in place for 1s, then fires at the player.
	public class ADTrailYoyoProj : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 18;
			Projectile.height = 18;
			Projectile.hostile = true;
			Projectile.friendly = false;
			Projectile.penetrate = 1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 240;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
		}

		public override void AI()
		{
			Projectile.ai[0]++;
			Projectile.rotation += Projectile.ai[0] < 60 ? 0.5f : 0.9f;
			Lighting.AddLight(Projectile.Center, 0.3f, 0.15f, 0.1f);
			if (Projectile.ai[0] < 60) {
				// Waiting: hover and glow.
				Projectile.velocity *= 0.9f;
				if (Main.rand.NextBool(6)) {
					Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, -0.5f, 120, default, 0.8f);
					dust.noGravity = true;
				}
				if (Projectile.ai[0] == 60) {
					Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
					if (target.active && !target.dead) {
						float speed = Projectile.ai[1] > 0f ? Projectile.ai[1] : 9f;
						Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;
						SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
						for (int i = 0; i < 8; i++) {
							Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 120, default, 1f);
							dust.noGravity = true;
						}
					}
				}
			}
			else {
				// Fired: slight homing so sidesteps don't trivialize the volley.
				Player target = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
				if (target.active && !target.dead) {
					Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
					Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.05f);
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			float alpha = Projectile.ai[0] < 60 ? 0.75f : 1f;
			// Fade out over the last half second.
			if (Projectile.timeLeft < 30)
				alpha *= Projectile.timeLeft / 30f;
			Color tint = Color.White * alpha;
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, tint, Projectile.rotation, tex.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0f);
			return false;
		}
	}
}
