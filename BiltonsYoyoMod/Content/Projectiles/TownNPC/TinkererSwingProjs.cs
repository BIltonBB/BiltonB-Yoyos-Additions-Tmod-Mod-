using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;

namespace BiltonsYoyoMod.Content.Projectiles.TownNPC
{
	// Close-range swinging yoyo used by the Yoyo Tinkerer.
	// NOT a deployed yoyo: telegraphs briefly, sweeps a melee arc toward the
	// target with a visible swoosh, sparks on hit, then vanishes until the
	// next swing cooldown.
	public abstract class TinkererSwingProj : ModProjectile
	{
		public virtual int SwingTime => 22;
		public virtual Color SwooshColor => new(255, 220, 150);
		public virtual int SparkDust => DustID.Torch;

		private NPC resolvedOwner;
		private bool whooshed;

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 60;
			// NOTE: no extraUpdates. SwingTime progress (ai[2]) must advance once
			// per tick or the swing plays at double speed and is barely visible.
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
		}

		public override void OnSpawn(IEntitySource source)
		{
			// Slots are pooled: never inherit a previous swing's owner/sound flag.
			resolvedOwner = null;
			whooshed = false;
		}

		private NPC Owner()
		{
			if (resolvedOwner != null && resolvedOwner.active)
				return resolvedOwner;
			int ownerIndex = (int)Projectile.ai[0];
			if (ownerIndex > 0 && ownerIndex < Main.maxNPCs && Main.npc[ownerIndex].active) {
				resolvedOwner = Main.npc[ownerIndex];
				return resolvedOwner;
			}
			resolvedOwner = NPCOwnerFinder.NearestOfTypes(Projectile.Center, 400f, ModContent.NPCType<YoyoTinkerer>());
			return resolvedOwner;
		}

		public override void AI()
		{
			NPC owner = Owner();
			if (owner == null) {
				Projectile.Kill();
				return;
			}

			// Aim at first tick: face the owner's current target direction.
			if (Projectile.ai[2] == 0) {
				NPC enemy = NearestEnemy(owner.Center, 500f);
				Vector2 aim = enemy != null ? (enemy.Center - owner.Center) : new Vector2(owner.direction * 60f, 0f);
				Projectile.ai[1] = aim.ToRotation();
			}

			Projectile.ai[2]++;
			float progress = Projectile.ai[2] / SwingTime;

			// Telegraph sparks during the first quarter of the swing.
			if (progress < 0.3f && Projectile.ai[2] % 2 == 0) {
				Dust dust = Dust.NewDustDirect(owner.Center - new Vector2(10, 10), 20, 20, SparkDust, 0f, 0f, 120, default, 0.7f);
				dust.noGravity = true;
			}
			if (!whooshed && progress > 0.15f) {
				whooshed = true;
				SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
			}

			if (progress >= 1f) {
				Projectile.Kill();
				return;
			}

			// Sweep from -70deg to +70deg around the stored aim direction (ai[1]).
			float baseAngle = Projectile.ai[1];
			float swing = MathHelper.Lerp(-1.2f, 1.2f, progress);
			float radius = 70f;
			Vector2 offset = (baseAngle + swing).ToRotationVector2() * radius;
			Projectile.Center = owner.Center + offset;
			Projectile.rotation += 0.6f;
			Lighting.AddLight(Projectile.Center, 0.25f, 0.18f, 0.1f);
		}

		private static NPC NearestEnemy(Vector2 from, float range)
		{
			NPC best = null;
			float bestDist = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;
				float dist = Vector2.DistanceSquared(from, npc.Center);
				if (dist < bestDist) {
					bestDist = dist;
					best = npc;
				}
			}
			return best;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 6; i++) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, SparkDust, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1f);
				dust.noGravity = true;
			}
			SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.Center);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// Swoosh: ghost copies along the swept arc behind the head.
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
			float progress = MathHelper.Clamp(Projectile.ai[2] / SwingTime, 0f, 1f);
			NPC owner = Owner();
			if (owner != null) {
				float baseAngle = Projectile.ai[1];
				for (int g = 3; g >= 1; g--) {
					float past = MathHelper.Clamp(progress - g * 0.09f, 0f, 1f);
					float swing = MathHelper.Lerp(-1.2f, 1.2f, past);
					Vector2 pos = owner.Center + (baseAngle + swing).ToRotationVector2() * 70f - Main.screenPosition;
					Main.spriteBatch.Draw(tex, pos, null, SwooshColor * (0.28f - g * 0.06f), Projectile.rotation - g * 0.25f, origin, Projectile.scale * 0.9f, SpriteEffects.None, 0f);
				}
			}
			return true;
		}
	}

	public class MWoodenYoyoProj : TinkererSwingProj
	{
		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.damage = 35;
		}
	}

	public class MChlorophyteYoyoProj : TinkererSwingProj
	{
		public override int SwingTime => 16;
		public override Color SwooshColor => new(150, 255, 150);
		public override int SparkDust => DustID.Chlorophyte;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.damage = 65;
			Projectile.scale = 1.15f;
		}

		public override void AI()
		{
			base.AI();
			Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.1f);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			base.OnHitNPC(target, hit, damageDone);
			target.AddBuff(BuffID.Poisoned, 240);
		}
	}
}
