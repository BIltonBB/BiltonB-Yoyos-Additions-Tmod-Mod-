using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Swing Tech shell: a true-melee arc like the Yoyo Tinkerer's swing.
	/// Sweeps -70..+70 deg around the throw aim for ~22 ticks, then vanishes.
	/// Damage 2x (at spawn). Radius 80 + yoyo range bonuses. Pierce infinite
	/// with local immunity. Re-swing by re-using the yoyo (autoReuse friendly).
	/// Assumed swing spec: 22-tick sweep, 80px radius, telegraph sparks + whoosh,
	/// swoosh ghosts, follows the player so movement works.
	/// </summary>
	public class SwingYoyoProj : TechProjBase
	{
		public int SwingTime => 22;
		private bool whooshed;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 6;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 34;
			Projectile.height = 34;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 60;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
			// ai0 = source item, ai1 = source proj, ai2 = aim rotation.
		}

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			SourceProjType = (int)Projectile.ai[1];
			global::BiltonsYoyoMod.Content.Tech.YoyoTechCommon.ApplyEnhancementToSpawn(Projectile, null, ModId);
			whooshed = false;
		}

		public override void AI()
		{
			Player owner = OwnerPlayer();
			if (owner == null || !owner.active || owner.dead) {
				Projectile.Kill();
				return;
			}
			SourceProjType = SourceProjType == 0 ? (int)Projectile.ai[1] : SourceProjType;
			deployTicks++;
			float progress = deployTicks / (float)SwingTime;

			SwingParams(owner, out bool spinAll, out float radius, out int arcDust, out Color swoosh);

			// Opening flash so the swing starts with a crack.
			if (deployTicks == 1) {
				for (int i = 0; i < 8; i++) {
					Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(4, 4), 8, 8,
						arcDust, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 120, default, 1f);
					d.noGravity = true;
				}
			}
			if (progress < 0.3f && deployTicks % 2 == 0) {
				Dust d = Dust.NewDustDirect(owner.Center - new Vector2(10, 10), 20, 20,
					arcDust, 0f, 0f, 120, default, 0.7f);
				d.noGravity = true;
			}
			// Opening slash ring: the full arc flashes once so the swing reads.
			if (deployTicks == 2 && Projectile.owner == Main.myPlayer) {
				float baseA = Projectile.ai[2];
				for (int i = 0; i <= 12; i++) {
					float a = MathHelper.Lerp(baseA - 1.2f, baseA + 1.2f, i / 12f);
					Vector2 pos = owner.Center + a.ToRotationVector2() * radius;
					Dust d = Dust.NewDustDirect(pos - new Vector2(4, 4), 8, 8,
						DustID.GoldCoin, 0f, -0.5f, 120, default, 1.1f);
					d.noGravity = true;
					d.velocity = Vector2.Zero;
				}
			}
			if (!whooshed && progress > 0.15f) {
				whooshed = true;
				SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
			}
			if (progress >= 1f) {
				// Swing's end: shockwave ring visual (no damage, pure flourish).
				for (int i = 0; i < 12; i++) {
					Vector2 ring = new Vector2(80f, 0f).RotatedBy(i / 12f * MathHelper.TwoPi);
					Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(4, 4), 8, 8,
						arcDust, ring.X * 0.05f, ring.Y * 0.05f, 120, default, 1f);
					d.noGravity = true;
				}
				Projectile.Kill();
				return;
			}

			float baseAngle = Projectile.ai[2];
			// Glove/Bag: full 360 spin. Otherwise the classic 140-degree arc.
			float swing = spinAll
				? baseAngle + progress * MathHelper.TwoPi
				: baseAngle + MathHelper.Lerp(-1.2f, 1.2f, progress);
			Vector2 offset = swing.ToRotationVector2() * radius;
			Projectile.Center = owner.Center + offset;
			Projectile.rotation += 0.6f;
			Lighting.AddLight(Projectile.Center, 0.35f, 0.25f, 0.12f);

			// Melee swing dust along the arc, every tick.
			{
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					arcDust, 0f, 0f, 120, default, 0.8f);
				d.noGravity = true;
			}

			GenericEchoShot(50, 9999); // Swings are 22 ticks: echo never fires mid-swing (kept for API parity).
		}

		/// <summary>
		/// Accessories reshape the swing: Glove/Bag spins the full circle,
		/// counterweights widen the reach, strings tint the arc.
		/// </summary>
		private static void SwingParams(Player owner, out bool spinAll, out float radius, out int dust, out Color swoosh)
		{
			spinAll = owner.yoyoGlove;
			radius = 80f;
			var market = owner.GetModPlayer<Players.MarketPlayer>();
			if (market.AncientRange)
				radius += 24f;
			var revamp = owner.GetModPlayer<Players.RevampPlayer>();
			radius += revamp.ExtraYoyoRange * 0.15f;
			if (global::BiltonsYoyoMod.Content.Tech.YoyoTechCommon.HasCounterweightAcc(owner))
				radius += 20f;
			dust = DustID.Torch;
			swoosh = new Color(255, 220, 150);
			if (revamp.Barbed) {
				dust = DustID.Blood;
				swoosh = new Color(255, 120, 120);
			}
			else if (revamp.Tangled) {
				dust = DustID.SilverCoin;
				swoosh = new Color(220, 220, 230);
			}
			else if (market.GoldenString) {
				dust = DustID.GoldCoin;
				swoosh = new Color(255, 210, 110);
			}
			else if (market.AncientString) {
				dust = DustID.MagicMirror;
				swoosh = new Color(170, 190, 255);
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ApplySpellDebuff(target);
			EchoHit(target);
			for (int i = 0; i < 12; i++) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Torch, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.1f);
				d.noGravity = true;
			}
			SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.Center);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// Swoosh ghosts along the swept arc.
			Player owner = OwnerPlayer();
			Microsoft.Xna.Framework.Graphics.Texture2D tex = null;
			if (SourceProjType > ProjectileID.None
				&& SourceProjType < Terraria.GameContent.TextureAssets.Projectile.Length) {
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
			float progress = MathHelper.Clamp(deployTicks / (float)SwingTime, 0f, 1f);
			if (owner != null) {
				SwingParams(owner, out bool spinAll, out float radius, out _, out Color swoosh);
				float baseAngle = Projectile.ai[2];
				// Slash arc: a chain of soft additive glows from the swing start to
				// the head, fattening and heating up as it goes. This USED to stamp
				// 3-5px MagicPixel quads along the arc - a stretched 1x1 opaque
				// pixel IS a hard square, which is exactly what you could see
				// trailing the throw. Light comes from the glow art, never from
				// flat pixel geometry.
				const int segs = 16;
				for (int s = 0; s <= segs; s++) {
					float past = progress * (s / (float)segs);
					float swing = spinAll
						? baseAngle + past * MathHelper.TwoPi
						: baseAngle + MathHelper.Lerp(-1.2f, 1.2f, past);
					Vector2 pos = owner.Center + swing.ToRotationVector2() * radius;
					float heat = s / (float)segs; // 0 tail -> 1 head.
					Color c = Color.Lerp(swoosh * 0.35f, Color.White, heat * heat);
					float dia = MathHelper.Lerp(13f, 27f, heat) * Projectile.scale;
					MiniYoyoFx.DrawGlowAdditive(pos, dia, c, 0.10f + 0.22f * heat * heat);
				}
				// Hot head: a soft bloom, not a 7x7 white square.
				MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 48f * Projectile.scale,
					Color.Lerp(swoosh, Color.White, 0.55f), 0.42f);
				MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 21f * Projectile.scale, Color.White, 0.50f);
			}
			// Motion trail along the flight path.
			for (int i = 0; i < Projectile.oldPos.Length; i++) {
				Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
				if (oldCenter == Vector2.Zero || Vector2.DistanceSquared(oldCenter, Projectile.Center) < 16f)
					continue;
				float a = 0.25f * (1f - i / (float)Projectile.oldPos.Length);
				Main.EntitySpriteDraw(tex, oldCenter - Main.screenPosition, null,
					new Color(255, 200, 130) * a, Projectile.oldRot[i], origin, Projectile.scale * 0.9f,
					Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
			}
			DrawSourceSprite(lightColor);
			return false;
		}
	}
}
