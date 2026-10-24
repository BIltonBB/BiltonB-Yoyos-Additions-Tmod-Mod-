using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
	// Shared base for the black-market yoyo line: vanilla yoyo physics with
	// themed dust, trails and on-hit effects.
	public abstract class BlackMarketYoyoProjBase : ModProjectile
	{
		public virtual int HitDust => DustID.GoldCoin;
		public virtual Color TrailTint => Color.Gold;

		private int specialTimer;

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
			Lighting.AddLight(Projectile.Center, TrailTint.ToVector3() * 0.35f);
			if (Main.rand.NextBool(5)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, HitDust, 0f, 0f, 120, default, 0.8f);
				dust.noGravity = true;
			}
			SpecialAI(ref specialTimer);
		}

		protected void ImpactBurst(int count = 8)
		{
			for (int i = 0; i < count; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, HitDust, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1f);
				dust.noGravity = true;
			}
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.NPCHit1, Projectile.Center);
		}

		// Per-yoyo extras (coin bolts, arming, ...). Runs every tick.
		protected virtual void SpecialAI(ref int timer)
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

	public class GoldYoyoProj : BlackMarketYoyoProjBase
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 6f;
			ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 200f;
			ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 11f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ImpactBurst();
			if (Main.rand.NextBool(4))
				target.AddBuff(BuffID.Midas, 180);
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			// Greed scaling: a fat wallet swings harder, up to +50%.
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return;
			Player owner = Main.player[Projectile.owner];
			if (owner == null || !owner.active)
				return;
			long copper = 0;
			foreach (Item item in owner.inventory) {
				if (item == null || item.IsAir)
					continue;
				if (item.type == ItemID.CopperCoin) copper += item.stack;
				else if (item.type == ItemID.SilverCoin) copper += item.stack * 100L;
				else if (item.type == ItemID.GoldCoin) copper += item.stack * 10000L;
				else if (item.type == ItemID.PlatinumCoin) copper += item.stack * 1000000L;
			}
			float bonus = System.Math.Min(0.5f, copper / 10000f * 0.005f);
			if (bonus > 0.01f)
				modifiers.SourceDamage *= 1f + bonus;
		}
	}

	public class LuckyYoyoProj : BlackMarketYoyoProjBase
	{
		public override int HitDust => DustID.Clentaminator_Green;
		public override Color TrailTint => new(120, 230, 140);

		public override void SetDefaults()
		{
			base.SetDefaults();
			ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 8f;
			ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 240f;
			ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 12.5f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ImpactBurst(10); // Lucky starburst.
			// Seeking stars only awaken in Hardmode.
			if (hit.Crit && Main.hardMode && Main.myPlayer == Projectile.owner) {
				Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
				Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 10f,
					ModContent.ProjectileType<LuckyStarProj>(), (int)(Projectile.damage * 0.6f), 2f, Projectile.owner);
			}
		}

		public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
		{
			// Damage scales with progression: +8% per major milestone.
			int milestones = 0;
			if (NPC.downedBoss1) milestones++;
			if (NPC.downedBoss2) milestones++;
			if (NPC.downedBoss3) milestones++;
			if (NPC.downedQueenBee) milestones++;
			if (Main.hardMode) milestones++;
			if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3) milestones++;
			if (NPC.downedPlantBoss) milestones++;
			if (NPC.downedGolemBoss) milestones++;
			if (NPC.downedAncientCultist) milestones++;
			if (NPC.downedMoonlord) milestones++;
			modifiers.SourceDamage *= 1f + milestones * 0.08f;
		}
	}

	public class LuckyStarProj : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 240;
			Projectile.extraUpdates = 1;
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
		}

		public override void AI()
		{
			Projectile.rotation += 0.4f;
			Lighting.AddLight(Projectile.Center, 0.3f, 0.4f, 0.2f);
			// 1s arm: flies straight and harmless, then seeks and hits.
			// (The spawn-grace global covers the no-hit half.)
			Projectile.ai[0]++;
			if (Projectile.ai[0] < 60)
				return;
			NPC best = null;
			float bestDist = 500f * 500f;
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
				Vector2 desired = (best.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 12f;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.12f);
			}
			if (Main.rand.NextBool(4)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Clentaminator_Green, 0f, 0f, 120, default, 0.7f);
				dust.noGravity = true;
			}
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
				float alpha = (1f - (float)i / Projectile.oldPos.Length) * (1f - dist / 220f);
				if (alpha <= 0f)
					continue;
				Color tint = ShadeByLight(new Color(150, 255, 170, 200), lightColor) * (alpha * 0.55f);
				Main.EntitySpriteDraw(tex, ghostCenter - Main.screenPosition, null, tint, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
			}
			Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, ShadeByLight(new Color(150, 255, 170, 200), lightColor), Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
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

	public class IllegalYoyoProj : BlackMarketYoyoProjBase
	{
		public override int HitDust => DustID.Blood;
		public override Color TrailTint => new(220, 60, 70);

		private bool armed;

		public override void SetDefaults()
		{
			base.SetDefaults();
			ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 8f;
			ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 230f;
			ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 12f;
		}

		public override void AI()
		{
			if (!armed) {
				armed = true;
				Projectile.ArmorPenetration = 10;
			}
			base.AI();
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ImpactBurst(10);
		}

		protected override void SpecialAI(ref int timer)
		{
			// Pressure wave: every ~1.6s it always detonates (damage only
			// lands if something is actually in radius). Kept well under
			// the yoyo's own lifetime so it can actually fire.
			timer++;
			// Telegraph: sparks gather during the last half second.
			if (timer > 70 && Main.rand.NextBool(3)) {
				Vector2 edge = Projectile.Center + Main.rand.NextVector2Circular(70f, 70f);
				Vector2 inward = (Projectile.Center - edge).SafeNormalize(Vector2.Zero);
				Dust dust = Dust.NewDustDirect(edge - new Vector2(4, 4), 8, 8, DustID.Blood, inward.X * 4f, inward.Y * 4f, 120, default, 1f);
				dust.noGravity = true;
			}
			if (timer < 100)
				return;
			timer = 0;
			// Visible shockwave: expanding blood-gold ring + white flash.
			Lighting.AddLight(Projectile.Center, 1.2f, 0.5f, 0.4f);
			for (int i = 0; i < 26; i++) {
				Vector2 dir = new Vector2(110f, 0f).RotatedBy(i / 26f * MathHelper.TwoPi);
				Dust dust = Dust.NewDustDirect(Projectile.Center - new Vector2(4, 4), 8, 8, i % 2 == 0 ? DustID.Blood : DustID.GoldCoin, dir.X * 0.09f, dir.Y * 0.09f, 100, default, 1.2f);
				dust.noGravity = true;
			}
			for (int i = 0; i < 14; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.Center - new Vector2(40, 40), 80, 80, DustID.Blood, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.1f);
				dust.noGravity = true;
			}
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item14, Projectile.Center);
			if (Main.myPlayer != Projectile.owner)
				return;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;
				if (Vector2.DistanceSquared(Projectile.Center, npc.Center) > 110f * 110f)
					continue;
				int dmg = (int)(Projectile.damage * 0.5f);
				// Dummies stay put: full knockback everywhere else.
				float kb = npc.type == NPCID.TargetDummy ? 0f : 14f;
				NPC.HitInfo hit = new() { Damage = dmg, Knockback = kb, HitDirection = System.Math.Sign(npc.Center.X - Projectile.Center.X) };
				npc.StrikeNPC(hit, false, false);
				npc.netUpdate = true;
			}
		}
	}

	public class StringersSpecialProj : BlackMarketYoyoProjBase
	{
		public override int HitDust => DustID.PurpleTorch;
		public override Color TrailTint => new(180, 110, 240);

		public override void SetDefaults()
		{
			base.SetDefaults();
			ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 9f;
			ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 250f;
			ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 13f;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ImpactBurst();
			if (Main.rand.NextBool(2))
				target.AddBuff(BuffID.Midas, 120);
		}

		protected override void SpecialAI(ref int timer)
		{
			timer++;
			if (timer < 45)
				return;
			timer = 0;
			if (Main.myPlayer != Projectile.owner)
				return;
			NPC enemy = FindEnemy(500f);
			if (enemy == null)
				return;
			Vector2 dir = (enemy.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
			Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 12f,
				ModContent.ProjectileType<CoinBoltProj>(), Projectile.damage / 2, 2f, Projectile.owner);
		}
	}

	public class CoinBoltProj : ModProjectile
	{
		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 1;
			Projectile.tileCollide = true;
			Projectile.timeLeft = 180;
			Projectile.extraUpdates = 1;
		}

		public override void AI()
		{
			Projectile.rotation += 0.6f;
			Lighting.AddLight(Projectile.Center, 0.3f, 0.25f, 0.05f);
			if (Main.rand.NextBool(3)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, 0f, 0f, 120, default, 0.7f);
				dust.noGravity = true;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.rand.NextBool(3))
				target.AddBuff(BuffID.Midas, 120);
		}
	}

	// (SuperGoldYoyoProj lives in its own file now: Projectiles/SuperGoldYoyoProj.cs.)
}
