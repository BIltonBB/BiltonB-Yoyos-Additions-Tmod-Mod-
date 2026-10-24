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
	/// Hollow Worm: the brood well's child, rebuilt as a REAL worm - a head
	/// projectile that swims with a serpentine gait, followed by a chain of
	/// segment projectiles (the same body-of-many-links the Destroyer yoyo
	/// uses). It spawns short, grows a new bead every half second, and once its
	/// well dries up the whole animal burrows into the ground and dies with it.
	///
	/// ai[0]: parent well projectile index. ai[1]: power01. ai[2]: source type.
	/// </summary>
	public class MagicalWormProj : ModProjectile
	{
		private const int GrowEvery = 26;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int age;
		private int orphan;
		private int segments = 1; // the head counts as index 0
		private int burrowing;
		private float bite;
		private float power;
		private float tier01 = 0.5f;
		private float heading;
		private Color accent = new(190, 150, 255);

		private float Speed => 6.6f + 0.45f * tier01 + 0.10f * segments;
		private int MaxSegments => 7 + (int)Math.Round(4f * tier01);

		public override void SetDefaults()
		{
			Projectile.width = 34;
			Projectile.height = 34;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false; // it swims through the ground
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 12;
			Projectile.timeLeft = 60 * 16;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			age++;

			// Parent well check: the worm is part of the well, not a pet - when
			// the bowl dries up the worm follows it into the ground.
			if (age % 10 == 0) {
				int parent = (int)Projectile.ai[0];
				bool alive = parent >= 0 && parent < Main.maxProjectiles
					&& Main.projectile[parent].active
					&& Main.projectile[parent].type == ModContent.ProjectileType<MagicalPuddleProj>()
					&& Main.projectile[parent].owner == Projectile.owner;
				orphan = alive ? 0 : orphan + 10;
				if (orphan > 40 && burrowing <= 0) {
					burrowing = 46;
					if (!Main.dedServ) {
						MagicalFx.Scorch(Projectile.Center, accent, 0.9f);
						YoyoParticles.SpawnRing(Projectile.Center, 8f, 70f, 14, accent, 2.4f);
						SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = -0.4f }, Projectile.Center);
					}
				}
			}

			if (age == 1) {
				tier01 = MagicalTier.AbilityTier01((int)Projectile.ai[2]);
				Vector2 dir = Projectile.velocity.LengthSquared() > 0.01f
					? Projectile.velocity.SafeNormalize(Vector2.UnitX)
					: Vector2.UnitX.RotatedBy(Main.rand.NextFloat(MathHelper.TwoPi));
				NPC first = NearestTarget(760f);
				if (first != null)
					dir = (first.Center - Projectile.Center).SafeNormalize(dir);
				heading = dir.ToRotation();
				Projectile.velocity = dir * Speed;
				if (!Main.dedServ) {
					MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 0.7f);
					MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 5, 3.2f, 20, 0.6f);
					YoyoParticles.SpawnRing(Projectile.Center, 8f, 84f, 14, accent, 2.8f);
					SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
				}
			}

			if (burrowing > 0) {
				burrowing--;
				heading = RotateToward(heading, MathHelper.PiOver2, 0.06f);
				Projectile.velocity = heading.ToRotationVector2() * (Speed * 1.25f);
				Projectile.rotation = Projectile.velocity.ToRotation();
				TunnelFx();
				if (burrowing == 0) {
					if (!Main.dedServ) {
						MagicalFx.Scorch(Projectile.Center, accent, 0.7f);
						MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 4, 2.6f, 20, 0.5f);
					}
					Projectile.Kill();
					return;
				}
				Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.7f);
				return;
			}

			// Grow: a new bead every half second, announced with a body pulse.
			if (age % GrowEvery == 0 && segments < MaxSegments) {
				segments++;
				SpawnSegment(segments);
			}

			// Hunt: nearest enemy, gentle turn cap, plus a serpentine wobble on
			// top of the heading so the whole chain slithers rather than darts.
			NPC target = NearestTarget(820f);
			float want = heading;
			if (target != null) {
				want = (target.Center - Projectile.Center).SafeNormalize(heading.ToRotationVector2()).ToRotation();
			}
			else {
				// Idle: a lazy circle so it never freezes in place.
				want = heading + 0.02f;
			}
			heading = RotateToward(heading, want, 0.075f);
			float wiggle = (float)Math.Sin(age * 0.09f) * 0.22f;
			Projectile.velocity = (heading + wiggle).ToRotationVector2() * Speed;
			Projectile.rotation = Projectile.velocity.ToRotation();

			if (bite > 0f)
				bite = Math.Max(0f, bite - 0.1f);

			if (!Main.dedServ) {
				if (age % 3 == 0) {
					Vector2 side = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
					YoyoParticles.SpawnSpark(Projectile.Center + side * Main.rand.NextFloat(-14f, 14f),
						-Projectile.velocity * 0.04f, accent, 0.6f, 14, 0.95f);
				}
				if (age % 5 == 0)
					YoyoParticles.SpawnBurst(Projectile.Center, 3, 1.6f, accent, 0.45f, 24);
			}
			TunnelFx();
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.7f);
		}

		private void TunnelFx()
		{
			if (Main.dedServ)
				return;
			if (Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)
				&& Main.rand.NextBool(2)) {
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Dirt, 0f, -1.4f, 110, default, 1.25f);
				dust.noGravity = true;
				dust.velocity += Projectile.velocity * 0.18f;
			}
		}

		/// <summary>Turn `from` toward `to` by at most `step` radians.</summary>
		private static float RotateToward(float from, float to, float step)
		{
			float delta = MathHelper.WrapAngle(to - from);
			return from + MathHelper.Clamp(delta, -step, step);
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

		/// <summary>The last bead of the chain, so the new one can sprout there.</summary>
		private Vector2 TailPos()
		{
			int segType = ModContent.ProjectileType<MagicalWormSegmentProj>();
			Vector2 best = Projectile.Center;
			int bestIdx = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (!p.active || p.type != segType || (int)p.ai[0] != Projectile.identity)
					continue;
				int idx = (int)p.ai[1];
				if (idx > bestIdx) {
					bestIdx = idx;
					best = p.Center;
				}
			}
			return best;
		}

		/// <summary>Grow a new bead. Children spawn server-side so the body is
		/// identical for every player (see MagicalReleaseCommon.SpawnChild).</summary>
		private void SpawnSegment(int index)
		{
			Player owner = OwnerOf();
			if (owner == null)
				return;
			Vector2 tail = TailPos();
			MagicalReleaseCommon.SpawnChild(owner, tail, Vector2.Zero,
				ModContent.ProjectileType<MagicalWormSegmentProj>(),
				Math.Max(1, Projectile.damage * 3 / 5), Projectile.identity, index, Projectile.ai[2]);
			if (!Main.dedServ) {
				YoyoParticles.SpawnRing(tail, 4f, 42f, 12, accent, 2f);
				MagicalFx.Pop(tail, 34f, accent, 0.3f);
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			bite = 1f;
			if (!Main.dedServ)
				MagicalFx.Impact(target.Center, accent, 0.7f, Projectile.owner, Projectile.rotation);
		}

		public override void OnKill(int timeLeft)
		{
			// The chain dies with the head: every bead lets go at once.
			int segType = ModContent.ProjectileType<MagicalWormSegmentProj>();
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.type == segType && (int)p.ai[0] == Projectile.identity)
					p.Kill();
			}
			if (Main.dedServ)
				return;
			MagicalFx.Pop(Projectile.Center, 46f, accent, 0.25f);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 4, 3f, 20, 0.55f);
			YoyoParticles.SpawnRing(Projectile.Center, 8f, 64f, 14, accent, 2.6f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			Color hot = Color.Lerp(accent, Color.White, 0.55f);
			Color chitin = new Color(16, 11, 22);
			Vector2 fwd = Projectile.velocity.SafeNormalize(Vector2.UnitX);
			float rot = fwd.ToRotation();
			Vector2 c = Projectile.Center;

			// Skull: chitin shell, mantle highlight, crest spikes.
			MiniYoyoFx.DrawSolidEllipse(c, 42f, 34f, rot, chitin, 0.96f);
			MiniYoyoFx.DrawSolidEllipse(c, 32f * (0.9f + 0.1f * power), 25f, rot, accent * 0.5f, 0.8f);
			Vector2 perp = fwd.RotatedBy(MathHelper.PiOver2);
			for (int i = -1; i <= 1; i++) {
				Vector2 p = c - fwd * 8f + perp * (i * 9f);
				MagicalFx.Shape(ShapeId.Chevron, p, rot + MathHelper.Pi,
					18f, 13f, chitin * (i == 0 ? 0.98f : 0.9f));
			}

			// Jaws: two crescents that snap with every bite, plus the maw glow.
			float jaw = 0.35f + 0.4f * bite;
			Vector2 jawAt = c + fwd * 9f;
			MagicalFx.Shape(ShapeId.SmearCrescent, jawAt, rot + jaw, 38f, 24f, hot);
			MagicalFx.Shape(ShapeId.SmearCrescent, jawAt, rot - jaw, 38f, 24f, hot);
			MagicalFx.Bloom(c + fwd * 16f, 34f + 20f * bite, accent, 0.4f + 0.3f * bite);
			MagicalFx.StarFlare(c + fwd * 11f, Color.White, 16f + 10f * bite, rot, 0.45f);

			// Eyes.
			Vector2 head = c + fwd * 3f;
			MagicalFx.Bloom(head + perp * 8f, 11f, Color.White, 0.6f);
			MagicalFx.Bloom(head - perp * 8f, 11f, Color.White, 0.6f);
			MagicalFx.Bloom(head + perp * 8f, 20f, accent, 0.3f);
			MagicalFx.Bloom(head - perp * 8f, 20f, accent, 0.3f);

			if (burrowing > 0)
				MagicalFx.Shape(ShapeId.SplatSoft, c + new Vector2(0f, 16f), 0f, 58f, 26f, accent * 0.4f);
			MagicalFx.Motes(c, 38f, accent, 0.22f, 3, 1.1f);
			MiniYoyoFx.DrawGlowAdditive(c, 76f, accent, 0.22f + 0.1f * (float)Math.Sin(t2 * 0.1f));
			return false;
		}
	}

	/// <summary>
	/// One bead of the Hollow Worm. It chases the link ahead of it with a
	/// spring (so the body whips around corners), slithers sideways on a sine,
	/// and dies instantly if its head is gone - the chain can never outlive
	/// the animal.
	///
	/// ai[0]: head identity. ai[1]: chain index. ai[2]: source type.
	/// </summary>
	public class MagicalWormSegmentProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int age;
		private int index;
		private Color accent = new(190, 150, 255);
		private Vector2 prevCenter;
		private float prevRotation;

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
			Projectile.localNPCHitCooldown = 16;
			Projectile.timeLeft = 120;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			age++;
			index = (int)Projectile.ai[1];
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);

			Projectile prev = FindPrev();
			if (prev == null) {
				// The head is gone: the bead lets go.
				Projectile.Kill();
				return;
			}
			Projectile.timeLeft = 60;
			prevCenter = prev.Center;
			prevRotation = prev.rotation;

			// Chain tension: sit a fixed spacing behind the link ahead, with a
			// hard catch-up when the gap stretches (so it never rubber-bands
			// into a straight line).
			Vector2 toPrev = prev.Center - Projectile.Center;
			float dist = toPrev.Length();
			Vector2 fallback = prev.rotation.ToRotationVector2();
			Vector2 dir = toPrev.SafeNormalize(fallback);
			float spacing = 20f + 2.5f * Math.Min(index, 8);
			Vector2 want = prev.Center - dir * spacing;
			Projectile.velocity = Projectile.velocity * 0.55f + (want - Projectile.Center) * 0.45f;
			// Slither: a travelling sine riding sideways on the chain.
			float side = (float)Math.Sin(Main.GameUpdateCount * 0.11f - index * 1.15f) * 0.55f;
			Projectile.velocity += dir.RotatedBy(MathHelper.PiOver2) * side;
			float max = dist > 64f ? 26f : 16f;
			if (Projectile.velocity.Length() > max)
				Projectile.velocity = Vector2.Normalize(Projectile.velocity) * max;
			Projectile.rotation = Projectile.velocity.LengthSquared() > 1f
				? Projectile.velocity.ToRotation()
				: prevRotation;

			if (!Main.dedServ) {
				if (age % 6 == index % 6)
					YoyoParticles.SpawnSpark(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
						-Projectile.velocity * 0.03f, accent, 0.45f, 11, 0.95f);
				if (Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)
					&& Main.rand.NextBool(3)) {
					Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
						DustID.Dirt, 0f, -1f, 120, default, 1.1f);
					dust.noGravity = true;
				}
			}
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.45f);
		}

		/// <summary>The link ahead: for bead 1 that is the head itself, for the
		/// rest it is the bead with the previous index on the same worm.</summary>
		private Projectile FindPrev()
		{
			int wormType = ModContent.ProjectileType<MagicalWormProj>();
			int segType = ModContent.ProjectileType<MagicalWormSegmentProj>();
			int headId = (int)Projectile.ai[0];
			if (index <= 1) {
				for (int i = 0; i < Main.maxProjectiles; i++) {
					Projectile p = Main.projectile[i];
					if (p.active && p.type == wormType && p.identity == headId)
						return p;
				}
				return null;
			}
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.type == segType && (int)p.ai[0] == headId && (int)p.ai[1] == index - 1)
					return p;
			}
			return null;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			MagicalFx.Pop(Projectile.Center, 22f, accent, 0.2f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			Color hot = Color.Lerp(accent, Color.White, 0.5f);
			Color chitin = new Color(16, 11, 22);
			float taper = MathHelper.Clamp(1.12f - 0.075f * index, 0.42f, 1f);
			float pulse = 0.94f + 0.06f * (float)Math.Sin(t2 * 0.2f - index * 0.9f);
			float r = 17f * taper * pulse;
			Vector2 c = Projectile.Center;
			float rot = Projectile.rotation;

			// Connective membrane to the link ahead: the body must have no gaps
			// even mid-dash, so every bead stitches itself to its predecessor.
			Vector2 d = prevCenter - c;
			float len = d.Length();
			if (len > 1f && len < 90f) {
				Vector2 mid = (c + prevCenter) * 0.5f;
				MiniYoyoFx.DrawTaperedStroke(c, r * 1.02f, mid, r * 1.22f, chitin, 0.85f);
				MiniYoyoFx.DrawTaperedStroke(mid, r * 1.22f, prevCenter, r * 1.32f, chitin, 0.85f);
				MiniYoyoFx.DrawTaperedStroke(c, r * 0.38f, prevCenter, r * 0.42f, accent, 0.45f);
			}

			// The bead: shell, mantle, hot core, dorsal plate, side spikes.
			MiniYoyoFx.DrawSolidEllipse(c, r * 2.05f, r * 1.8f, rot, chitin, 0.95f);
			MiniYoyoFx.DrawSolidEllipse(c, r * 1.58f, r * 1.36f, rot, accent * 0.55f, 0.85f);
			MiniYoyoFx.DrawSolidEllipse(c, r * 0.84f, r * 0.7f, rot, hot, 0.7f);
			MagicalFx.Shape(ShapeId.Chevron, c - rot.ToRotationVector2() * (r * 0.16f), rot + MathHelper.Pi,
				r * 1.6f, r * 1.1f, hot * 0.45f);
			Vector2 perp = rot.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
			MagicalFx.Shape(ShapeId.NeedleLong, c + perp * (r * 0.8f), rot + MathHelper.PiOver2,
				7f * taper + 3f, 14f * taper + 5f, chitin * 0.95f);
			MagicalFx.Shape(ShapeId.NeedleLong, c - perp * (r * 0.8f), rot - MathHelper.PiOver2,
				7f * taper + 3f, 14f * taper + 5f, chitin * 0.95f);
			if (index % 2 == 0)
				MiniYoyoFx.DrawGlowAdditive(c, r * 2.4f, accent, 0.13f);
			return false;
		}
	}
}
