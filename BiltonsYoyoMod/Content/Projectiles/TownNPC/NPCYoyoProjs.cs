using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;
using BiltonsYoyoMod.Content.Utils;

namespace BiltonsYoyoMod.Content.Projectiles.TownNPC
{
	// Finds the NPC that owns an NPC-thrown projectile (vanilla town attacks
	// don't forward ai values, so owner is resolved by type + proximity).
	public static class NPCOwnerFinder
	{
		public static NPC NearestOfTypes(Vector2 pos, float range, params int[] types)
		{
			NPC best = null;
			float bestDist = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active)
					continue;
				bool match = false;
				foreach (int t in types) {
					if (npc.type == t) {
						match = true;
						break;
					}
				}
				if (!match)
					continue;
				float dist = Vector2.DistanceSquared(pos, npc.Center);
				if (dist < bestDist) {
					bestDist = dist;
					best = npc;
				}
			}
			return best;
		}
	}

	// Base class for yoyos thrown by Town NPCs (Yoyo Master / Ancient Yo-yoist).
	// Launch toward the target -> hover/deployed for Lifetime (with light enemy
	// tracking) -> accelerating return to the NPC, with string, trail and sparks.
	public abstract class NPCOwnedYoyoProj : ModProjectile
	{
		public virtual int LifetimeTicks => 300; // 5 seconds.
		public virtual float MaxRange => 400f;
		public virtual float LaunchSpeed => 12f;
		public virtual Color StringColor => Color.White;
		public virtual Color GlowColor => new(255, 255, 255);
		public virtual int TrailDust => DustID.SilverCoin;

		private const int StateLaunch = 0;
		private const int StateDeployed = 1;
		private const int StateReturn = 2;

		private readonly List<Vector2> trail = new();
		private NPC resolvedOwner;
		// Deployed-flail anchor: offset from the owner captured on deploy, plus sweep phase.
		private Vector2 anchorOffset;
		private float sweepPhase;
		private float sweepDir = 1f;

		public override void SetDefaults()
		{
			Projectile.width = 22;
			Projectile.height = 22;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = 3;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 1200;
			// NOTE: no extraUpdates. Timers (launch 40t, lifetime) and speeds are
			// authored as single-update values; extra updates would run ai[2]
			// twice per tick (halving lifetimes) and double all velocities.
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 15;
		}

		public override void OnSpawn(IEntitySource source)
		{
			// Slots are pooled: never inherit a previous life's trail/owner/anchor.
			trail.Clear();
			resolvedOwner = null;
			anchorOffset = Vector2.Zero;
			sweepPhase = 0f;
			sweepDir = 1f;
		}

		private NPC Owner()
		{
			if (resolvedOwner != null && resolvedOwner.active)
				return resolvedOwner;
			// Explicit owner (traveler/custom spawns pass whoAmI in ai[0]).
			int ownerIndex = (int)Projectile.ai[0];
			if (ownerIndex > 0 && ownerIndex < Main.maxNPCs && Main.npc[ownerIndex].active) {
				resolvedOwner = Main.npc[ownerIndex];
				return resolvedOwner;
			}
			resolvedOwner = NPCOwnerFinder.NearestOfTypes(Projectile.Center, 900f,
				ModContent.NPCType<YoyoMaster>(), ModContent.NPCType<AncientYoyoist>());
			return resolvedOwner;
		}

		public override void AI()
		{
			NPC owner = Owner();
			if (owner == null) {
				Projectile.Kill();
				return;
			}

			CacheTrail();
			Lighting.AddLight(Projectile.Center, GlowColor.ToVector3() * 0.45f);

			float state = Projectile.ai[1];
			Projectile.ai[2]++;

			if (state == StateLaunch) {
				if (Projectile.ai[2] == 2)
					SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
				Projectile.velocity *= 1.01f;
				if (Projectile.ai[2] % 4 == 0)
					SpawnSpark(Projectile.velocity * 0.2f, 0.7f);
				if (Projectile.ai[2] > 40 || Vector2.Distance(Projectile.Center, owner.Center) > MaxRange) {
					Projectile.ai[1] = StateDeployed;
					Projectile.ai[2] = 0;
					Projectile.velocity *= 0.2f;
					// Capture the flail anchor so the deployed yoyo swings around it.
					anchorOffset = Projectile.Center - owner.Center;
					if (anchorOffset.Length() > MaxRange)
						anchorOffset = anchorOffset.SafeNormalize(Vector2.Zero) * MaxRange;
					sweepPhase = Main.rand.NextFloat(MathHelper.TwoPi);
					sweepDir = Main.rand.NextBool() ? 1f : -1f;
					Burst(8);
				}
			}
			else if (state == StateDeployed) {
				// Flail swing: the yoyo sweeps in wide arcs around its anchor point
				// like a real yoyo on a string, drifting toward nearby enemies.
				sweepPhase += sweepDir * 0.075f;
				Vector2 anchorWorld = owner.Center + anchorOffset;
				NPC enemy = NearestEnemy(260f);
				if (enemy != null)
					anchorWorld = Vector2.Lerp(anchorWorld, enemy.Center, 0.04f);
				float radius = MathHelper.Min(150f, 55f + Projectile.ai[2] * 0.45f);
				Vector2 radial = (Projectile.Center - anchorWorld).SafeNormalize(Vector2.UnitY);
				Vector2 tangent = radial.RotatedBy(sweepDir * MathHelper.PiOver2);
				Vector2 desired = anchorWorld + radial * radius * (0.75f + 0.25f * (float)System.Math.Sin(sweepPhase)) + tangent * 40f;
				Vector2 steer = (desired - Projectile.Center) * 0.22f;
				if (steer.Length() > 8f)
					steer = steer.SafeNormalize(Vector2.Zero) * 8f;
				Projectile.velocity = steer;
				// Stay leashed to the owner.
				Vector2 fromOwner = Projectile.Center - owner.Center;
				if (fromOwner.Length() > MaxRange) {
					Projectile.Center = owner.Center + fromOwner.SafeNormalize(Vector2.Zero) * MaxRange;
					anchorOffset = Projectile.Center - owner.Center;
				}
				if (Projectile.ai[2] % 6 == 0)
					SpawnSpark(Vector2.Zero, 0.6f);
				if (Projectile.ai[2] >= LifetimeTicks) {
					Projectile.ai[1] = StateReturn;
					Projectile.ai[2] = 0;
				}
			}
			else {
				// Accelerating return to the NPC.
				Vector2 toOwner = owner.Center - Projectile.Center;
				if (toOwner.Length() < 34f) {
					Burst(6);
					SoundEngine.PlaySound(SoundID.CoinPickup, Projectile.Center);
					Projectile.Kill();
					return;
				}
				float speed = MathHelper.Min(10f + Projectile.ai[2] * 0.35f, LaunchSpeed + 10f);
				Projectile.velocity = toOwner.SafeNormalize(Vector2.Zero) * speed;
			}

			Projectile.rotation += 0.35f;
		}

		private NPC NearestEnemy(float range)
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

		private void CacheTrail()
		{
			trail.Add(Projectile.Center);
			while (trail.Count > 9)
				trail.RemoveAt(0);
		}

		private void SpawnSpark(Vector2 vel, float scale)
		{
			Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, TrailDust, vel.X, vel.Y, 120, default, scale);
			dust.noGravity = true;
		}

		private void Burst(int count)
		{
			for (int i = 0; i < count; i++) {
				Vector2 vel = Main.rand.NextVector2Circular(3f, 3f);
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, TrailDust, vel.X, vel.Y, 100, default, 0.9f);
				dust.noGravity = true;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			Burst(5);
			SoundEngine.PlaySound(SoundID.NPCHit1, Projectile.Center);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			NPC owner = Owner();
			if (owner != null) {
				Vector2 a = owner.Center - Main.screenPosition;
				Vector2 b = Projectile.Center - Main.screenPosition;
				// Two-pass string: dark outline + colored core, both lit by surroundings.
				Color coreLit = Utils.YoyoDrawing.ShadeByLight(StringColor, (owner.Center + Projectile.Center) * 0.5f);
				DrawThickLine(a, b, Color.Black * 0.6f, 3f);
				DrawThickLine(a, b, coreLit, 1.4f);
			}
			// Fading trail ghosts, dimmed by local light.
			Color light = Utils.YoyoDrawing.ShadeByLight(Color.White, Projectile.Center);
			Texture2D tex = TextureAssets.Projectile[Type].Value;
			Vector2 origin = new(tex.Width / 2f, tex.Height / 2f);
			for (int i = 0; i < trail.Count; i++) {
				float alpha = (i + 1) / (float)trail.Count * 0.35f;
				Color ghost = GlowColor * alpha;
				ghost = new Color((int)(ghost.R * light.R / 255f), (int)(ghost.G * light.G / 255f), (int)(ghost.B * light.B / 255f), ghost.A);
				Main.spriteBatch.Draw(tex, trail[i] - Main.screenPosition, null, ghost, Projectile.rotation, origin, Projectile.scale * 0.8f, SpriteEffects.None, 0f);
			}
			return true;
		}

		private static void DrawThickLine(Vector2 a, Vector2 b, Color color, float width)
		{
			Vector2 diff = b - a;
			float len = diff.Length();
			if (len < 1f)
				return;
			Texture2D px = TextureAssets.MagicPixel.Value;
			Main.spriteBatch.Draw(px, a, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len, width), SpriteEffects.None, 0f);
		}
	}

	public class NPCWoodenYoyoProj : NPCOwnedYoyoProj
	{
		public override Color StringColor => new(235, 220, 190);
		public override Color GlowColor => new(255, 240, 200);
		public override int TrailDust => DustID.WoodFurniture;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.damage = 25;
			Projectile.penetrate = 3;
		}
	}

	public class NPCAdamantiteYoyoProj : NPCOwnedYoyoProj
	{
		public override int LifetimeTicks => 420;
		public override float MaxRange => 560f;
		public override float LaunchSpeed => 16f;
		public override Color StringColor => new(160, 230, 255);
		public override Color GlowColor => new(150, 220, 255);
		public override int TrailDust => DustID.Adamantite;

		public override void SetDefaults()
		{
			base.SetDefaults();
			Projectile.damage = 90;
			Projectile.penetrate = 5;
			Projectile.width = 26;
			Projectile.height = 26;
		}
	}
}
