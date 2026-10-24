using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
	// The Yoyo Cultist's relic line: vanilla yoyo physics with ancient
	// dust, trails, homing glamour and forbidden on-hit effects.
	public abstract class AncientYoyoProjBase : ModProjectile
	{
		public virtual int HitDust => DustID.MagicMirror;
		public virtual Color TrailTint => new(180, 160, 255);

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.aiStyle = ProjAIStyleID.Yoyo;
			Projectile.friendly = true;
			Projectile.penetrate = -1;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
		}

		public override void AI()
		{
			Lighting.AddLight(Projectile.Center, TrailTint.ToVector3() * 0.7f);
			if (Main.rand.NextBool(5)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, HitDust, 0f, 0f, 120, default, 0.8f);
				dust.noGravity = true;
			}
			// Noticeable relic aura: slow rune motes orbiting the yoyo
			// (stateless angle: vanilla yoyo AI owns the ai slots).
			Vector2 halo = Projectile.Center + new Vector2(26f, 0f).RotatedBy(Main.GameUpdateCount * 0.08f + Projectile.whoAmI);
			Lighting.AddLight(halo, TrailTint.ToVector3() * 0.4f);
			if (Main.rand.NextBool(6)) {
				Dust mote = Dust.NewDustDirect(halo - new Vector2(4, 4), 8, 8, HitDust, 0f, -0.5f, 120, default, 0.9f);
				mote.noGravity = true;
			}
			SteerAI();
		}

		protected void ImpactBurst(int count = 8)
		{
			for (int i = 0; i < count; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, HitDust, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1f);
				dust.noGravity = true;
			}
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.NPCHit1, Projectile.Center);
		}

		protected virtual void SteerAI()
		{
		}

		protected NPC FindEnemy(float range)
		{
			NPC best = null;
			float bestDist = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;
				float dist = Vector2.DistanceSquared(Projectile.Center, npc.Center);
				if (dist < bestDist) {
					bestDist = dist;
					best = npc;
				}
			}
			return best;
		}

		protected void HomeToward(float strength, float range)
		{
			NPC enemy = FindEnemy(range);
			if (enemy == null)
				return;
			Vector2 desired = (enemy.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
			Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, strength);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
			Vector2 origin = texture.Size() * 0.5f;
			for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
				if (Projectile.oldPos[i] == Vector2.Zero)
					continue;
				Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
				float alpha = (1f - (float)i / Projectile.oldPos.Length) * 0.4f;
				Main.EntitySpriteDraw(texture, drawPos, null, TrailTint * alpha, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
			}
			return true;
		}
	}

	public class EnchantedYoyoProj : AncientYoyoProjBase
	{
		public override int HitDust => DustID.Enchanted_Pink;
		public override Color TrailTint => new(150, 230, 250);

		public override void SetDefaults()
		{
			base.SetDefaults();
			ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 7f;
			ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 190f;
			ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 11f;
		}

		protected override void SteerAI()
		{
			HomeToward(0.05f, 300f); // Gentle self-guidance.
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ImpactBurst(6);
			// Every hit calls a small star down onto the victim.
			if (Main.myPlayer == Projectile.owner) {
				Vector2 sky = target.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), -320f);
				Vector2 dir = (target.Center - sky).SafeNormalize(Vector2.UnitY);
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), sky, dir * 14f,
					ModContent.ProjectileType<SkyfallStarProj>(), (int)(Projectile.damage * 0.1f), 1f, Projectile.owner);
			}
		}
	}

	public class SkyfallStarProj : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 4; // Rains through packs, not just one enemy.
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 180;
			Projectile.extraUpdates = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 15;
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
		}

		public override void AI()
		{
			Projectile.rotation += 0.5f;
			Lighting.AddLight(Projectile.Center, 0.3f, 0.4f, 0.45f);
			if (Main.rand.NextBool(3)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Enchanted_Pink, 0f, 0f, 120, default, 0.8f);
				dust.noGravity = true;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 6; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Enchanted_Pink, 0f, -1f, 120, default, 0.9f);
				dust.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = tex.Size() * 0.5f;
			// Afterimages fade with DISTANCE from the head and shade with the
			// surrounding lighting so the trail reads in dark caves too.
			for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
				if (Projectile.oldPos[i] == Vector2.Zero)
					continue;
				Vector2 ghostCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
				float dist = Vector2.Distance(ghostCenter, Projectile.Center);
				float alpha = (1f - (float)i / Projectile.oldPos.Length) * (1f - dist / 220f);
				if (alpha <= 0f)
					continue;
				Color tint = ShadeByLight(new Color(170, 240, 255, 220), lightColor) * (alpha * 0.55f);
				Main.EntitySpriteDraw(tex, ghostCenter - Main.screenPosition, null, tint, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
			}
			Color head = ShadeByLight(new Color(170, 240, 255, 220), lightColor);
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, head, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
			return false;
		}

		private static Color ShadeByLight(Color tint, Color light)
		{
			return new Color(
				(byte)(tint.R * (0.25f + 0.75f * light.R / 255f)),
				(byte)(tint.G * (0.25f + 0.75f * light.G / 255f)),
				(byte)(tint.B * (0.25f + 0.75f * light.B / 255f)),
				tint.A);
		}
	}

	public class CultistWispProj : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = false; // 1s grace: materializes before it can bite.
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 180; // 3s total, fading out at the end.
			Projectile.extraUpdates = 1;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
		}

		public override void AI()
		{
			// Arm after the 1s grace period.
			if (Projectile.timeLeft <= 120)
				Projectile.friendly = true;
			Projectile.rotation += 0.35f;
			Lighting.AddLight(Projectile.Center, 0.35f, 0.15f, 0.5f);
			// No homing while materializing: drifts straight for 1s.
			if (Projectile.timeLeft > 120)
				return;
			NPC best = null;
			float bestDist = 600f * 600f;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;
				float dist = Vector2.DistanceSquared(Projectile.Center, npc.Center);
				if (dist < bestDist) {
					bestDist = dist;
					best = npc;
				}
			}
			if (best != null) {
				Vector2 desired = (best.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 13f;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);
			}
			if (Main.rand.NextBool(3)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 120, default, 0.8f);
				dust.noGravity = true;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			target.AddBuff(BuffID.ShadowFlame, 120);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = tex.Size() * 0.5f;
			for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
				if (Projectile.oldPos[i] == Vector2.Zero)
					continue;
				Vector2 ghostCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
				float dist = Vector2.Distance(ghostCenter, Projectile.Center);
				float alpha = (1f - (float)i / Projectile.oldPos.Length) * (1f - dist / 200f);
				if (alpha <= 0f)
					continue;
				Color tint = Content.Utils.YoyoDrawing.ShadeByLight(new Color(200, 140, 255, 220), ghostCenter) * (alpha * 0.5f);
				Main.EntitySpriteDraw(tex, ghostCenter - Main.screenPosition, null, tint, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
			}
			Texture2D tex2 = tex;
			// Fade in during grace, fade out over the last second.
			float alpha2 = 220f;
			if (Projectile.timeLeft > 120)
				alpha2 = 220f * (1f - (Projectile.timeLeft - 120f) / 60f);
			else if (Projectile.timeLeft < 60)
				alpha2 = 220f * (Projectile.timeLeft / 60f);
			Color head = Content.Utils.YoyoDrawing.ShadeByLight(new Color(200, 140, 255, (int)System.Math.Max(0f, alpha2)), Projectile.Center);
			Main.spriteBatch.Draw(tex2, Projectile.Center - Main.screenPosition, null, head, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
			return false;
		}
	}

	public class CultistYoyoProj : AncientYoyoProjBase
	{
		public override int HitDust => DustID.Shadowflame;
		public override Color TrailTint => new(170, 90, 230);

		public override void SetDefaults()
		{
			base.SetDefaults();
			ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 9f;
			ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 250f;
			ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 12.5f;
		}

		protected override void SteerAI()
		{
			HomeToward(0.1f, 450f);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ImpactBurst();
			target.AddBuff(BuffID.ShadowFlame, 180);
			// 1-in-5 hits loose a seeking wisp.
			if (Main.rand.NextBool(5) && Main.myPlayer == Projectile.owner) {
				Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 9f,
					ModContent.ProjectileType<CultistWispProj>(), (int)(Projectile.damage * 0.4f), 1f, Projectile.owner);
			}
		}
	}

	public class ForbiddenYoyoProj : AncientYoyoProjBase
	{
		public override int HitDust => DustID.Blood;
		public override Color TrailTint => new(200, 40, 50);

		public override void SetDefaults()
		{
			base.SetDefaults();
			ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 8f;
			ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 240f;
			ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 13f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ImpactBurst(10);
			target.AddBuff(BuffID.CursedInferno, 240);
			if (Main.rand.NextBool(5))
				target.AddBuff(BuffID.BetsysCurse, 180);
			// Forbidden siphon: drink a little life, sometimes it drinks back.
			if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers) {
				Player owner = Main.player[Projectile.owner];
				if (owner.active && !owner.dead) {
					if (Main.rand.NextBool(30)) { // Siphon bites back less often now.
						owner.AddBuff(BuffID.CursedInferno, 120, false);
						CombatText.NewText(owner.getRect(), new Color(200, 60, 60), "Backlash!");
					}
					else if (owner.statLife < owner.statLifeMax2) {
						owner.statLife = System.Math.Min(owner.statLifeMax2, owner.statLife + 2);
						owner.HealEffect(2, false);
					}
				}
			}
		}
	}

	public class SeleneCoilProj : AncientYoyoProjBase
	{
		public override int HitDust => DustID.SilverCoin;
		public override Color TrailTint => new(220, 235, 255);

		public override void SetDefaults()
		{
			base.SetDefaults();
			ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 10f;
			ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 280f;
			ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 14f;
		}

		public override void AI()
		{
			base.AI();
			if (Main.rand.NextBool(8)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.MagicMirror, 0f, -1f, 120, default, 0.8f);
				dust.noGravity = true;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ImpactBurst(10);
			// Full frostbite by night, only a chill by day.
			if (Main.dayTime)
				target.AddBuff(BuffID.Chilled, 120);
			else
				target.AddBuff(BuffID.Frostburn, 180);
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			// Moonlight waxes at night (+25%), wanes by day (-10%).
			modifiers.SourceDamage *= Main.dayTime ? 0.9f : 1.25f;
		}

		private int pulseTimer;

		protected override void SteerAI()
		{
			// Selene's special: on moonlit nights the coil exhales frost every 5s.
			pulseTimer++;
			if (!Main.dayTime && pulseTimer >= 300) {
				pulseTimer = 0;
				for (int i = 0; i < 12; i++) {
					Dust dust = Dust.NewDustDirect(Projectile.Center - new Vector2(50, 50), 100, 100, DustID.MagicMirror, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1.1f);
					dust.noGravity = true;
				}
				SoundEngine.PlaySound(SoundID.Item8, Projectile.Center);
				if (Main.myPlayer != Projectile.owner)
					return;
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC npc = Main.npc[i];
					if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
						continue;
					if (Vector2.DistanceSquared(Projectile.Center, npc.Center) > 130f * 130f)
						continue;
					int dmg = (int)(Projectile.damage * 0.4f);
					NPC.HitInfo hit = new() { Damage = dmg, Knockback = 2f, HitDirection = System.Math.Sign(npc.Center.X - Projectile.Center.X) };
					npc.StrikeNPC(hit, false, false);
					npc.netUpdate = true;
					npc.AddBuff(BuffID.Frostburn, 120);
				}
			}
		}
	}
}
