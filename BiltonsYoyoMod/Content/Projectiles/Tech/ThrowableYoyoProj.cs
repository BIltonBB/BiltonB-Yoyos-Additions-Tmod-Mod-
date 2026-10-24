using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Throwable Tech shell: heavy ember boomerang. Gravity drag, 3 tile
	/// bounces, slow but 1.5x damage, larger body. Glove/Bag fans it into
	/// a triple spread. Re-throwable; finish the throw, then it comes home.
	/// Preserves: mods (damage/launch/phase), spells (debuffs), strings (dust/magnet),
	/// glove spread, + generic echo shots.
	/// </summary>
	public class ThrowableYoyoProj : TechProjBase
	{
		private int bounces;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 8;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 3;
			Projectile.timeLeft = 600;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 12;
			Projectile.scale = 1.15f;
			// ai0 = source item type, ai1 = source proj type.
		}

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			SourceProjType = (int)Projectile.ai[1];
			global::BiltonsYoyoMod.Content.Tech.YoyoTechCommon.ApplyEnhancementToSpawn(Projectile, null, ModId);
			bounces = 0;
		}

		public override void AI()
		{
			Player owner = OwnerPlayer();
			if (owner == null || !owner.active || owner.dead) {
				Projectile.Kill();
				return;
			}
			// Tech item gone mid-flight: finish the throw, then despawn.
			var tp = owner.GetModPlayer<global::BiltonsYoyoMod.Content.Tech.YoyoTechPlayer>();
			bool suppressed = tp == null || tp.ActiveTech != global::BiltonsYoyoMod.Content.Tech.YoyoTechType.Throwable
				|| !global::BiltonsYoyoMod.Content.Tech.YoyoTechPlayer.HasTechItem(owner, global::BiltonsYoyoMod.Content.Tech.YoyoTechType.Throwable);

			SourceProjType = SourceProjType == 0 ? (int)Projectile.ai[1] : SourceProjType;
			deployTicks++;
			Projectile.rotation += 0.5f;

			bool returning = Projectile.ai[2] == 1f || deployTicks > 100 || suppressed;

			if (!returning) {
				// Heavy iron: strong gravity drag, capped pace, ember streaks.
				Projectile.velocity.Y += 0.18f;
				if (Projectile.velocity.Length() > 20f)
					Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
				Lighting.AddLight(Projectile.Center, 0.7f, 0.45f, 0.2f);
				for (int i = 0; i < 3; i++) {
					Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
						DustID.Torch, -Projectile.velocity.X * 0.15f, -Projectile.velocity.Y * 0.15f, 120, default, 1f);
					d.noGravity = true;
				}
				if (Main.rand.NextBool(5)) {
					Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
						DustID.Smoke, 0f, -0.5f, 120, default, 0.9f);
					d.noGravity = true;
				}
			}
			else {
				if (Projectile.ai[2] == 0f) {
					// First return tick: whistle home.
					Terraria.Audio.SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
				}
				Projectile.ai[2] = 1f;
				Projectile.tileCollide = false;
				Vector2 toOwner = owner.Center - Projectile.Center;
				float dist = toOwner.Length();
				if (dist < 32f) {
					// Caught: little catch puff.
					for (int i = 0; i < 6; i++) {
						Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
							DustID.Torch, Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f), 120, default, 0.8f);
						d.noGravity = true;
					}
					Projectile.Kill();
					return;
				}
				Vector2 want = toOwner.SafeNormalize(Vector2.Zero) * 18f;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.18f);
				Lighting.AddLight(Projectile.Center, 0.6f, 0.5f, 0.25f);
				// Golden home-stretch streak.
				if (Main.rand.NextBool(3)) {
					Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
						DustID.GoldCoin, -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f, 120, default, 0.8f);
					d.noGravity = true;
				}
			}

			TechStringFx(owner);
			GenericEchoShot(50, 140);

			// Mod: tractor pull + echo pulse keep working on the shell.
			if (ModId == 5)
				TractorPull();
			if (ModId == 12)
				EchoPulse();
		}

		private void TractorPull()
		{
			for (int i = 0; i < Main.maxItems; i++) {
				Item item = Main.item[i];
				if (item == null || !item.active || item.IsAir || !global::BiltonsYoyoMod.Content.Tech.YoyoTechCommon.IsPickup(item.type))
					continue;
				Vector2 toProj = Projectile.Center - item.Center;
				if (toProj.Length() > 192f || toProj.Length() < 4f)
					continue;
				item.velocity += toProj.SafeNormalize(Vector2.Zero) * 0.8f;
				if (item.velocity.Length() > 9f)
					item.velocity = item.velocity.SafeNormalize(Vector2.Zero) * 9f;
			}
		}

		private void EchoPulse()
		{
			echoTimer++;
			if (echoTimer < 120)
				return;
			echoTimer = 0;
			for (int i = 0; i < 16; i++) {
				Vector2 dir = new Vector2(110f, 0f).RotatedBy(i / 16f * MathHelper.TwoPi);
				Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(4, 4), 8, 8,
					DustID.GoldCoin, dir.X * 0.06f, dir.Y * 0.06f, 100, default, 1.1f);
				d.noGravity = true;
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, Projectile.Center);
			if (Main.myPlayer != Projectile.owner)
				return;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;
				if (Vector2.DistanceSquared(Projectile.Center, npc.Center) > 110f * 110f)
					continue;
				int dmg = (int)(Projectile.damage * 0.5f);
				NPC.HitInfo hit = new() { Damage = dmg, Knockback = 3f, HitDirection = System.Math.Sign(npc.Center.X - Projectile.Center.X) };
				npc.StrikeNPC(hit, false, false);
				npc.netUpdate = true;
			}
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.ai[2] == 1f)
				return false;
			bounces++;
			if (bounces >= 3) {
				Projectile.ai[2] = 1f; // Give up bouncing, return.
				Projectile.velocity *= 0.5f;
				return false;
			}
			if (System.Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon)
				Projectile.velocity.X = -oldVelocity.X * 0.6f;
			if (System.Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon)
				Projectile.velocity.Y = -oldVelocity.Y * 0.6f;
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);
			for (int i = 0; i < 10; i++) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Stone, 0f, 0f, 100, default, 0.8f);
				d.noGravity = true;
			}
			for (int i = 0; i < 6; i++) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Torch, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 120, default, 1f);
				d.noGravity = true;
			}
			return false;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ApplySpellDebuff(target);
			EchoHit(target);
			for (int i = 0; i < 10; i++) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Torch, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1f);
				d.noGravity = true;
			}
			for (int i = 0; i < 3; i++) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Smoke, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1.5f, -0.5f), 120, default, 1f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// Ember afterimages along the flight trail, then the yoyo itself.
			Microsoft.Xna.Framework.Graphics.Texture2D tex = null;
			if (SourceProjType > ProjectileID.None && SourceProjType < Terraria.GameContent.TextureAssets.Projectile.Length) {
				try {
					// Vanilla textures are lazy; modded textures are preloaded.
					// Value is a 1x1 transparent texture while unloaded, so check
					// IsLoaded instead of trusting a non-null Value.
					var sourceAsset = Terraria.GameContent.TextureAssets.Projectile[SourceProjType];
					if (!sourceAsset.IsLoaded && SourceProjType < ProjectileID.Count)
						Main.instance.LoadProjectile(SourceProjType);
					if (sourceAsset.IsLoaded)
						tex = sourceAsset.Value;
				}
				catch { tex = null; }
			}
			if (tex == null) {
				var shellAsset = Terraria.GameContent.TextureAssets.Projectile[Type];
				if (shellAsset.IsLoaded)
					tex = shellAsset.Value;
			}
			if (tex == null)
				return true;
			Vector2 origin = tex.Size() * 0.5f;
			for (int i = 0; i < Projectile.oldPos.Length; i++) {
				Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
				if (oldCenter == Vector2.Zero || Vector2.DistanceSquared(oldCenter, Projectile.Center) < 16f)
					continue;
				float a = 0.32f * (1f - i / (float)Projectile.oldPos.Length);
				Main.EntitySpriteDraw(tex, oldCenter - Main.screenPosition, null,
					new Color(255, 140, 50) * a, Projectile.oldRot[i], origin, Projectile.scale * 0.9f,
					Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
			}
			DrawSourceSprite(lightColor);
			return false;
		}
	}
}
