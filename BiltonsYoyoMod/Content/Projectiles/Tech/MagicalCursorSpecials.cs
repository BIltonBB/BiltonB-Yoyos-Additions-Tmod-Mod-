using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Snaptrap Bloom: a spring-loaded flower planted AT the cursor. It blooms
	/// in place, lunges at whatever comes near with tooth-ringed jaws, and when
	/// its bites run out it bursts into seeds that crack into shards. The bite
	/// count grows with the source yoyo's progression tier.
	///
	/// ai[0]: power01. ai[1]: tier01. ai[2]: source type.
	/// </summary>
	public class MagicalSnapTrapProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int age;
		private float power;
		private float tier01;
		private float bloom;
		private float jaw;
		private int bitesLeft = 3;
		private int lungeTicks;
		private int restTicks;
		private NPC prey;
		private Color accent = new(150, 235, 120);

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 26;
			Projectile.timeLeft = 320;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			age++;
			power = MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
			tier01 = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			bloom = Math.Min(1f, bloom + 0.045f);
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.45f + 0.35f * bloom));

			if (age == 1) {
				bitesLeft = 3 + (int)Math.Round(2f * tier01);
				restTicks = 20;
				if (!Main.dedServ) {
					MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.7f);
					MagicalFx.CastFlash(Projectile.Center, accent, 0.7f);
					YoyoParticles.SpawnRing(Projectile.Center, 8f, 78f, 14, accent, 2.6f);
					MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 4, 2.8f, 20, 0.6f);
					MiniYoyoFx.Shake(2, 6, Projectile.Center);
					SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
				}
			}

			if (prey != null && (!prey.active || prey.life <= 0))
				prey = null;

			if (lungeTicks > 0) {
				// The spring fires: dash straight at the victim, jaws clamped shut.
				lungeTicks--;
				jaw = 1f;
				if (prey != null) {
					Vector2 want = prey.Center - Projectile.Center;
					Projectile.velocity = want * 0.34f;
				}
				if (!Main.dedServ && age % 2 == 0) {
					YoyoParticles.SpawnSpark(Projectile.Center + Projectile.velocity * 0.4f,
						-Projectile.velocity * 0.06f, accent, 0.7f, 12, 0.94f);
				}
				if (lungeTicks == 0) {
					restTicks = 26;
					Projectile.velocity *= 0.18f;
				}
			}
			else if (bitesLeft <= 0) {
				Pop();
				return;
			}
			else {
				Projectile.velocity *= 0.86f;
				jaw = Math.Max(0f, jaw - 0.08f);
				if (--restTicks <= 0) {
					prey = NearestTarget(240f + 140f * tier01);
					if (prey != null) {
						bitesLeft--;
						lungeTicks = 10;
						if (!Main.dedServ) {
							MagicalFx.Fan(ShapeId.SplatStar, Projectile.Center, Color.White, 5, 3.4f, 16, 0.7f);
							SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.3f, Pitch = 0.4f }, Projectile.Center);
						}
					}
					else {
						restTicks = 8;
					}
				}
			}

			if (!Main.dedServ && age % 7 == 0)
				YoyoParticles.SpawnBurst(Projectile.Center + Main.rand.NextVector2Circular(14f, 10f),
					2, 1.5f, accent, 0.45f, 24);
		}

		private NPC NearestTarget(float range)
		{
			NPC best = null;
			float bestD = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Targetable(npc))
					continue;
				float d = Vector2.DistanceSquared(npc.Center, Projectile.Center);
				if (d < bestD) {
					bestD = d;
					best = npc;
				}
			}
			return best;
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		/// <summary>End of the show: burst into six seeds that crack into shards.
		/// Children are spawned by the server (SpawnChild) so hits never
		/// duplicate in multiplayer.</summary>
		private void Pop()
		{
			Player owner = OwnerOf();
			if (owner != null) {
				int seed = ModContent.ProjectileType<MagicalSeedProj>();
				int damage = Math.Max(1, Projectile.damage * 3 / 5);
				for (int i = 0; i < 6; i++) {
					float a = i * MathHelper.TwoPi / 6f + Main.rand.NextFloat(0.5f);
					Vector2 dir = a.ToRotationVector2();
					MagicalReleaseCommon.SpawnChild(owner, Projectile.Center + dir * 14f,
						dir * Main.rand.NextFloat(6.5f, 9f), seed, damage, 0f, power, Projectile.ai[2]);
				}
			}
			if (!Main.dedServ) {
				YoyoParticles.SpawnBurst(Projectile.Center, 10, 3.4f, accent, 0.9f, 22);
				MagicalFx.Fan(ShapeId.SplatStar, Projectile.Center, accent, 6, 3.6f, 22, 0.7f);
				MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.7f);
				MiniYoyoFx.Shake(2, 6, Projectile.Center);
			}
			Projectile.Kill();
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			jaw = 1f;
			if (Main.dedServ)
				return;
			MagicalFx.Impact(target.Center, accent, 1f, Projectile.owner,
				(target.Center - Projectile.Center).ToRotation());
			MiniBossShapeBank.Spawn(ShapeId.Chevron, target.Center, Projectile.rotation,
				new Vector2(36f / 64f, 22f / 64f), Color.Lerp(accent, Color.White, 0.5f) * 0.8f,
				14, Vector2.Zero, 0f, 0.02f);
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			YoyoParticles.SpawnRing(Projectile.Center, 6f, 64f, 12, accent, 2.4f);
			MagicalFx.Fan(ShapeId.EmberCurl, Projectile.Center, accent, 5, 2.8f, 20, 0.6f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			Color hot = Color.Lerp(accent, Color.White, 0.5f);
			Vector2 c = Projectile.Center;
			float open = MathHelper.Clamp(0.25f + 0.75f * bloom - 0.45f * jaw, 0f, 1f);

			// Stalk rooted in the soil: a soft mound + a stem.
			MagicalFx.Shape(ShapeId.SplatSoft, c + new Vector2(0f, 14f), 0.3f, 58f, 26f, new Color(26, 38, 20) * 0.85f);
			MiniYoyoFx.DrawSoftBar(c + new Vector2(0f, 10f), MathHelper.PiOver2, 26f, 7f,
				Color.Lerp(accent, new Color(20, 30, 16), 0.45f), 0.9f);

			float bob = (float)Math.Sin(t2 * 0.06f + Projectile.whoAmI) * 3f;
			Vector2 head = c + new Vector2(0f, bob);

			// Six petals fan out from the head; they fold as the jaws clamp.
			for (int i = 0; i < 6; i++) {
				float a = -MathHelper.PiOver2 + (i - 2.5f) * 0.5f * (0.75f + 0.5f * open);
				float len = 26f + 18f * open;
				Vector2 p = head + a.ToRotationVector2() * (len * 0.58f);
				MagicalFx.Shape(ShapeId.SmearCrescent, p, a, len, 16f + 7f * open,
					Color.Lerp(accent, hot, (i % 2) * 0.5f) * (0.5f + 0.35f * open));
			}

			// Tooth ring: chevrons pointing inward, snapping shut on a bite.
			for (int i = 0; i < 5; i++) {
				float a = MathHelper.Pi * (0.15f + 0.7f * i / 4f);
				Vector2 p = head + (a + MathHelper.Pi).ToRotationVector2() * (11f - 5f * jaw);
				MagicalFx.Shape(ShapeId.Chevron, p, a + MathHelper.Pi,
					16f + 9f * (1f - jaw), 12f, hot * (0.55f + 0.35f * jaw));
			}

			MagicalFx.Bloom(head, 26f + 20f * jaw + 10f * open, accent, 0.4f);
			MiniYoyoFx.DrawGlowAdditive(head, 54f + 32f * jaw, hot, 0.22f + 0.28f * jaw);
			if (bloom >= 1f)
				MagicalFx.Ring(c, 30f + 4f * (float)Math.Sin(t2 * 0.1f), 0f, 0.22f + 0.22f * jaw, accent);
			return false;
		}
	}

	/// <summary>
	/// Anchor Chain: the cursor throws a hook. It steers toward the live mouse
	/// while it flies, bites into the first ground it meets, and once anchored
	/// the chain hauls the battlefield toward the mooring point. Purely
	/// cursor-directed: where you aim is where the chain ends.
	///
	/// ai[0]: power01. ai[1]: tier01. ai[2]: source type.
	/// </summary>
	public class MagicalAnchorProj : ModProjectile
	{
		public const float LaunchSpeed = 15f;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int age;
		private float power;
		private float tier01;
		private bool anchored;
		private int pullClock;
		private int syncClock;
		private Vector2 launchFrom;
		private Color accent = new(205, 185, 140);

		public override void SetDefaults()
		{
			Projectile.width = 24;
			Projectile.height = 24;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 24;
			Projectile.timeLeft = 420;
			Projectile.netImportant = true;
		}

		public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
		{
			launchFrom = Projectile.Center;
		}

		public override void AI()
		{
			age++;
			power = MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
			tier01 = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			Player owner = OwnerOf();
			if (owner == null) {
				Projectile.Kill();
				return;
			}
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.7f);

			if (!anchored) {
				Projectile.rotation = Projectile.velocity.ToRotation();
				// Steering follows the LIVE cursor on the owner's machine; the
				// position sync carries everyone else along.
				if (Projectile.owner == Main.myPlayer) {
					Vector2 fallback = Projectile.velocity.SafeNormalize(Vector2.UnitX);
					Vector2 want = (Main.MouseWorld - Projectile.Center).SafeNormalize(fallback);
					Vector2 dir = Vector2.Lerp(fallback, want, 0.06f).SafeNormalize(fallback);
					Projectile.velocity = dir * Math.Max(LaunchSpeed, Projectile.velocity.Length());
					if (++syncClock >= 5) {
						syncClock = 0;
						Projectile.netUpdate = true;
					}
				}
				if (!Main.dedServ && age % 3 == 0) {
					YoyoParticles.SpawnSpark(Projectile.Center - Projectile.velocity * 0.5f,
						-Projectile.velocity * 0.05f, accent, 0.6f, 13, 0.95f);
				}
				bool solid = Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height);
				float travelled = Vector2.Distance(Projectile.Center, launchFrom);
				float range = 330f + 240f * tier01;
				if (solid || travelled >= range || age >= 70)
					Land(owner);
			}
			else {
				Projectile.velocity = Vector2.Zero;
				pullClock++;
				if (MagicalReleaseCommon.CanMoveNpcs) {
					if (pullClock == 4)
						MagicalReleaseCommon.RadialHit(owner, Projectile.Center, 56f,
							Math.Max(1, Projectile.damage * 3 / 4), 2.4f);
					if (pullClock % 10 == 0)
						MagicalReleaseCommon.RadialGrab(Projectile.Center, 260f + 120f * tier01, 0.42f, towardCentre: true);
					if (pullClock % 24 == 0)
						MagicalReleaseCommon.RadialHit(owner, Projectile.Center, 40f,
							Math.Max(1, Projectile.damage / 3), 0.4f);
				}
				// The pull vortex itself is drawn in PreDraw (every frame, not on a
				// tick) - a draw helper must never be called from AI.
				if (!Main.dedServ && pullClock % 14 == 0)
					YoyoParticles.SpawnRing(Projectile.Center, 10f, 44f, 9, accent, 1.8f);
			}
		}

		private void Land(Player owner)
		{
			anchored = true;
			pullClock = 0;
			Projectile.velocity = Vector2.Zero;
			if (MagicalReleaseCommon.CanMoveNpcs)
				MagicalReleaseCommon.RadialHit(owner, Projectile.Center, 64f, Projectile.damage, 3f);
			if (Main.dedServ)
				return;
			MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.8f);
			MagicalFx.CastFlash(Projectile.Center, accent, 0.7f);
			YoyoParticles.SpawnRing(Projectile.Center, 6f, 110f, 16, accent, 3f);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 4, 3.4f, 22, 0.6f);
			MagicalFx.Scorch(Projectile.Center, accent, 0.8f, crack: true);
			MiniYoyoFx.Shake(3, 7, Projectile.Center);
			SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.45f, Pitch = -0.2f }, Projectile.Center);
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.dedServ)
				return;
			MagicalFx.Impact(target.Center, accent, 0.9f, Projectile.owner,
				(target.Center - Projectile.Center).ToRotation());
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			// Snap-back: the chain recoils toward the wielder.
			YoyoParticles.SpawnRing(Projectile.Center, 8f, 60f, 12, accent, 2.6f);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 3, 2.6f, 20, 0.55f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			Player owner = OwnerOf();
			if (owner == null)
				return false;
			float t2 = Main.GameUpdateCount;
			Color hot = Color.Lerp(accent, Color.White, 0.45f);
			Vector2 from = owner.Center;
			Vector2 to = Projectile.Center;
			Vector2 d = to - from;
			float len = d.Length();
			if (len < 10f)
				return false;
			Vector2 dir = d / len;

			// The chain: alternating links riding a slight sag, so it reads as
			// a real chain rather than a rope or a line.
			int links = Math.Clamp((int)(len / 15f), 2, 42);
			Vector2 sag = dir.RotatedBy(MathHelper.PiOver2) * (len * 0.04f) + new Vector2(0f, len * 0.05f);
			Vector2 mid = (from + to) * 0.5f + sag;
			Vector2 prev = from;
			for (int i = 1; i <= links; i++) {
				float f = i / (float)links;
				Vector2 p = MiniYoyoFx.Bez2(from, mid, to, f);
				float w = 4.5f * (1f - 0.4f * f);
				MiniYoyoFx.DrawTaperedStroke(prev, w, p, w * 0.94f, new Color(24, 20, 16), 0.75f);
				float a = (p - prev).ToRotation();
				MiniYoyoFx.DrawSolidEllipse(p, 11f + 3f * (1f - f), 6f, a, new Color(20, 16, 12), 0.95f);
				MiniYoyoFx.DrawSolidEllipse(p, 6f, 9f, a + MathHelper.PiOver2, accent * 0.55f, 0.9f);
				MiniYoyoFx.DrawSolidEllipse(p, 3.4f, 3.4f, 0f, hot, 0.6f);
				prev = p;
			}

			// The hook head: shank, crossbar, two flukes; a glint on the point.
			float rot = Projectile.rotation;
			MiniYoyoFx.DrawSoftBar(to, rot, 30f, 8f, new Color(24, 20, 16), 0.95f);
			MiniYoyoFx.DrawSoftBar(to, rot, 22f, 4f, accent, 0.85f);
			MiniYoyoFx.DrawSoftBar(to - dir * 14f, rot + MathHelper.PiOver2, 26f, 6f, accent, 0.8f);
			MagicalFx.Shape(ShapeId.SmearHook, to + dir.RotatedBy(2.4f) * 12f, rot + 2.2f, 26f, 16f, hot * 0.9f);
			MagicalFx.Shape(ShapeId.SmearHook, to + dir.RotatedBy(-2.4f) * 12f, rot - 2.2f, 26f, 16f, hot * 0.9f);
			MagicalFx.StarFlare(to, Color.White, 16f, rot, 0.5f);
			MiniYoyoFx.DrawGlowAdditive(to, 44f, accent, 0.35f);

			if (anchored) {
				float pulse = 0.5f + 0.5f * (float)Math.Sin(t2 * 0.08f);
				MagicalFx.Ring(to, 26f + 8f * pulse, 0f, 0.35f, accent);
				MagicalFx.Converge(to, 90f + 24f * pulse, 6f, accent, 0.35f, 6, 0f, 1.2f);
			}
			return false;
		}
	}

	/// <summary>
	/// Pocket Singularity: a black pocket opened AT the cursor. It drags the
	/// pack in with a real pull, chews on whatever arrives, then collapses -
	/// the collapse is the damage beat, blown back out as a shockwave.
	///
	/// ai[0]: power01. ai[1]: tier01. ai[2]: source type.
	/// </summary>
	public class MagicalSingularityProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private const int GrowTicks = 24;
		private const int CollapseAt = 112;
		private const int FadeTicks = 26;

		private int age;
		private float power;
		private float tier01;
		private float grow01;
		private bool collapsed;
		private Color accent = new(150, 90, 255);

		public override void SetDefaults()
		{
			Projectile.width = 44;
			Projectile.height = 44;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
			Projectile.timeLeft = 150;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			age++;
			power = MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
			tier01 = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = Color.Lerp(new Color(150, 90, 255), MagicalReleaseCommon.AbilityAccent(Projectile), 0.35f);
			grow01 = MathHelper.Clamp(age / (float)GrowTicks, 0f, 1f);
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.5f + 0.5f * grow01));

			if (age == 1 && !Main.dedServ) {
				MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.6f);
				MagicalFx.CastFlash(Projectile.Center, accent, 0.8f);
				MiniYoyoFx.Shake(2, 6, Projectile.Center);
				SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.45f, Pitch = -0.3f }, Projectile.Center);
			}

			if (!collapsed && age >= CollapseAt) {
				Collapse();
				return;
			}
			if (collapsed) {
				if (age > CollapseAt + FadeTicks + 4)
					Projectile.Kill();
				return;
			}

			// Hold: pull the pack toward the pocket and chew on arrivals.
			if (age >= GrowTicks) {
				if (MagicalReleaseCommon.CanMoveNpcs) {
					MagicalReleaseCommon.RadialGrab(Projectile.Center, 280f + 140f * tier01, 0.20f, towardCentre: true);
					if (age % 10 == 0) {
						Player owner = OwnerOf();
						if (owner != null)
							MagicalReleaseCommon.RadialHit(owner, Projectile.Center, 56f + 18f * tier01,
								Math.Max(1, Projectile.damage / 6), 0.35f);
					}
				}
			}
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		/// <summary>The payoff beat: everything inside is crushed, then the
		/// pocket blows its store outward in one shockwave.</summary>
		private void Collapse()
		{
			Player owner = OwnerOf();
			if (MagicalReleaseCommon.CanMoveNpcs && owner != null) {
				MagicalReleaseCommon.RadialHit(owner, Projectile.Center, 150f + 80f * tier01,
					Math.Max(1, (int)(Projectile.damage * 2.2f)), 6f);
				MagicalReleaseCommon.RadialGrab(Projectile.Center, 340f, 1.1f, towardCentre: false);
			}
			if (!Main.dedServ) {
				MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 1.5f);
				YoyoParticles.SpawnRing(Projectile.Center, 12f, 220f, 24, accent, 5f);
				MagicalFx.Fan(ShapeId.SplatStar, Projectile.Center, accent, 8, 6f, 26, 1f);
				MagicalFx.Shards(Projectile.Center, Color.Lerp(accent, Color.White, 0.4f), 12, 5.2f, 24, 1.1f);
				MagicalFx.Scorch(Projectile.Center, accent, 1.1f, crack: true);
				MiniYoyoFx.Shake(5, 11, Projectile.Center);
				MagicalFx.ScreenPulse(Projectile.Center, 0.28f, accent);
				SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = -0.4f }, Projectile.Center);
				SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
			}
			collapsed = true;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			MagicalFx.Pop(Projectile.Center, 60f, accent, 0.3f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			Color hot = Color.Lerp(accent, Color.White, 0.6f);
			float r = (14f + 34f * grow01) * (1f + 0.05f * (float)Math.Sin(t2 * 0.12f));
			Vector2 c = Projectile.Center;

			if (collapsed) {
				float f = MathHelper.Clamp((age - CollapseAt) / (float)FadeTicks, 0f, 1f);
				r *= (1f - f);
				if (r > 1f) {
					MiniYoyoFx.DrawSolidEllipse(c, r * 2f, r * 2f, 0f, new Color(8, 4, 14), 0.8f * (1f - f));
					MagicalFx.Ring(c, r * 2.4f + 40f * f, 0f, 0.6f * (1f - f), hot);
				}
				return false;
			}

			// Lensing ring + event horizon.
			MagicalFx.Ring(c, r * 1.45f, t2 * 0.01f, 0.4f, accent);
			MiniYoyoFx.DrawSolidEllipse(c, r * 1.7f, r * 1.7f, 0f, new Color(4, 2, 8), 0.92f);
			MiniYoyoFx.DrawGlowAdditive(c, r * 3.4f, accent, 0.22f);

			// The intake vortex: continuous, drawn here (never from AI), so the
			// funnel flows frame-for-frame instead of popping on a tick.
			MagicalFx.Converge(c, MathHelper.Max(40f, r * 2.5f), 8f, accent,
				0.45f + 0.25f * grow01, 10, t2 * 0.02f, 1.3f + 0.6f * grow01);

			// Accretion arms: two spirals of hot blobs, the pocket's signature.
			for (int arm = 0; arm < 2; arm++) {
				float baseA = t2 * 0.05f + arm * MathHelper.Pi;
				for (int i = 0; i < 9; i++) {
					float f = i / 8f;
					float a = baseA + f * 2.6f;
					Vector2 p = c + a.ToRotationVector2() * (r * (1.9f - 1.05f * f));
					float fade = (1f - f) * (0.5f + 0.5f * (float)Math.Sin(t2 * 0.2f + i));
					float sz = 14f * (1f - 0.55f * f);
					MagicalFx.Shape(ShapeId.SplatSoft, p, a, sz, sz * 0.8f,
						Color.Lerp(accent, hot, f) * (0.45f * fade));
				}
			}

			// Infall spokes: light bent toward the pocket, phase-animated.
			for (int i = 0; i < 6; i++) {
				float a = i * MathHelper.TwoPi / 6f + t2 * 0.01f;
				float f = (t2 * 0.02f + i * 0.37f) % 1f;
				Vector2 p = c + a.ToRotationVector2() * (r * (2.6f - 1.7f * f));
				float fade = (float)Math.Sin(f * Math.PI);
				MagicalFx.Wisp(p, a + MathHelper.Pi, 26f + 16f * fade, 5f, hot * (0.4f * fade));
			}

			MiniYoyoFx.DrawSolidEllipse(c, r * 0.62f, r * 0.62f, 0f, new Color(2, 1, 4), 0.95f);
			return false;
		}
	}
}
