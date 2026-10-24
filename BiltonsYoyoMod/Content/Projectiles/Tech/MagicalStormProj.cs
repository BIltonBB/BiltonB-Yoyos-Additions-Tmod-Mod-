using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Buffs;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Shared storm visual helpers. Every random-looking value comes from
	/// Main.GameUpdateCount or the projectile identity, never Main.rand, so
	/// the same strike looks the same on every client.
	/// </summary>
	internal static class StormFx
	{
		public static float Pulse(int seed, float speed = 0.08f)
			=> 0.5f + 0.5f * (float)Math.Sin(Main.GameUpdateCount * speed + seed * 0.73f);

		public static Vector2 Orbit(int seed, float angle, float radius)
			=> angle.ToRotationVector2() * radius;

		public static Color Hot(Color accent) => Color.Lerp(accent, Color.White, 0.7f);
	}

	/// <summary>
	/// Relay Storm's return-strike cell. A Straight bolt leaves one of these
	/// on each enemy it bites. After a short wind-up the cell fires a single
	/// Straight return bolt along the bearing captured at cast time.
	/// </summary>
	public class MagicalRelayNodeProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float angle;
		private float power;
		private int srcType;
		private int age;
		private bool fired;
		private Color accent = new(130, 190, 255);

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.netImportant = true;
			Projectile.timeLeft = 78;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
		}

		public override void AI()
		{
			angle = Projectile.ai[0];
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			srcType = (int)Projectile.ai[2];
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			age++;
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.32f + 0.3f * power));

			if (!fired && age >= 18) {
				fired = true;
				Projectile.netUpdate = true;
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					Player owner = Main.player[Projectile.owner];
					if (owner != null && owner.active && !owner.dead) {
						Vector2 dir = angle.ToRotationVector2();
						// The child captures this velocity as its own return
						// bearing; ai[3] is not part of Projectile's three
						// vanilla slots or of the vanilla sync packet.
						MagicalReleaseCommon.SpawnChild(owner, Projectile.Center,
							dir * MagicalLightningProj.BoltSpeed,
							ModContent.ProjectileType<MagicalLightningProj>(),
							Math.Max(1, (int)(Projectile.damage * 0.8f)),
							MagicalLightningProj.ModeStraight, power, srcType);
					}
				}
				Projectile.timeLeft = Math.Min(Projectile.timeLeft, 24);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 c = Projectile.Center;
			float pulse = StormFx.Pulse(Projectile.identity);
			float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
			Color hot = StormFx.Hot(accent);

			// Storm-cell body: dark puffs under a hard electric ring.
			for (int i = 0; i < 4; i++) {
				float a = Main.GameUpdateCount * 0.025f + i * MathHelper.PiOver2;
				Vector2 p = c + a.ToRotationVector2() * (6f + 7f * pulse);
				MiniYoyoFx.DrawSolidEllipse(p, 34f + 8f * pulse, 20f + 5f * pulse,
					a * 0.35f, new Color(38, 58, 92), 0.34f * fade);
			}
			MiniYoyoFx.DrawGlowAdditive(c, 46f + 14f * pulse, accent, 0.26f * fade);
			MiniYoyoFx.DrawGlowAdditive(c, 24f + 8f * pulse, hot, 0.34f * fade);
			MiniYoyoFx.DrawDashedRing(c, 18f + 3f * pulse, 2.2f, accent, 8,
				Main.GameUpdateCount * 0.045f);

			// Three rotating feelers, then the bright charge point.
			for (int i = 0; i < 3; i++) {
				float a = Main.GameUpdateCount * 0.055f + i * MathHelper.TwoPi / 3f;
				Vector2 p = c + a.ToRotationVector2() * (26f + 10f * pulse);
				MiniYoyoFx.DrawLightning(c, p, 1.8f, accent, 0.55f * fade,
					0.26f, 3, Projectile.identity * 13 + i);
				MiniYoyoFx.DrawGlowAdditive(p, 9f, hot, 0.26f * fade);
			}

			if (age < 18) {
				float charge = MathHelper.Clamp(age / 18f, 0f, 1f);
				MiniYoyoFx.DrawDashedRing(c, 24f + 10f * charge, 1.4f, hot, 12,
					-Main.GameUpdateCount * 0.09f);
			}
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			MagicalFx.Pop(Projectile.Center, 28f, accent, 0.28f);
			for (int i = 0; i < 5; i++) {
				float a = i * MathHelper.TwoPi / 5f;
				YoyoParticles.SpawnSpark(Projectile.Center,
					a.ToRotationVector2() * 2.4f, accent, 0.7f, 12, 0.8f);
			}
		}
	}

	/// <summary>
	/// One bite mark for Faraday Lattice / Polarity Reversal. ai[0] carries
	/// the field mode, ai[1] the bite order and ai[2] the source type. The
	/// field controller reads them; the anchor itself never deals contact
	/// damage. (ai[3] does not exist on a vanilla projectile and is not
	/// included in the vanilla sync packet.)
	/// </summary>
	public class MagicalChainAnchorProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float order;
		private int mode;
		private int age;
		private Color accent = new(130, 190, 255);

		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.netImportant = true;
			Projectile.timeLeft = 86;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
		}

		public override void AI()
		{
			if (Projectile.ai == null || Projectile.ai.Length < 3) {
				Projectile.Kill();
				return;
			}
			mode = (int)Projectile.ai[0];
			order = Projectile.ai[1];
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			age++;
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.35f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 c = Projectile.Center;
			float pulse = StormFx.Pulse(Projectile.identity, 0.11f);
			float fade = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
			Color hot = StormFx.Hot(accent);
			Color modeColor = mode == (int)MagicalChainFieldProj.ModePolarity
				? new Color(255, 120, 210) : accent;

			MiniYoyoFx.DrawGlowAdditive(c, 32f + 10f * pulse, modeColor, 0.26f * fade);
			MiniYoyoFx.DrawGlowAdditive(c, 14f + 5f * pulse, hot, 0.4f * fade);
			MiniYoyoFx.DrawDashedRing(c, 13f + 3f * pulse, 1.6f, modeColor, 6,
				Main.GameUpdateCount * 0.06f + order);

			// A tiny rune stroke differs by mode: Faraday is a triangle,
			// Polarity is a pair of opposed crescents.
			if (mode == (int)MagicalChainFieldProj.ModePolarity) {
				for (int i = 0; i < 2; i++) {
					float a = Main.GameUpdateCount * 0.04f + i * MathHelper.Pi;
					Vector2 p = c + a.ToRotationVector2() * 9f;
					MiniYoyoFx.DrawSolidDisc(p, 3.4f, modeColor, 0.8f * fade);
				}
			}
			else {
				Vector2[] tri = new Vector2[3];
				for (int i = 0; i < 3; i++)
					tri[i] = c + (Main.GameUpdateCount * 0.035f + i * MathHelper.TwoPi / 3f)
						.ToRotationVector2() * 9f;
				MiniYoyoFx.DrawSolidPolygon(tri, modeColor, 0.16f * fade);
			}
			return false;
		}
	}

	/// <summary>
	/// The field controller for the two chain overhauls.
	///
	/// Faraday Lattice: every bite anchors a node; the controller draws the
	/// lightning lattice between consecutive bites, damages enemies crossing
	/// an edge once, then discharges the whole chain.
	///
	/// Polarity Reversal: bites become alternating pull/push poles; the field
	/// drags enemies between poles and ends with one snap toward the last
	/// bite. The two abilities share the chain bolt's actual hit order, so the
	/// effect is never a disconnected decoration.
	/// </summary>
	public class MagicalChainFieldProj : ModProjectile
	{
		public const float ModeFaraday = 0f;
		public const float ModePolarity = 1f;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int mode;
		private int srcType;
		private int age;
		private bool purgedOldAnchors;
		private Color accent = new(130, 190, 255);
		private readonly List<int> struck = new();

		public override void SetDefaults()
		{
			Projectile.width = 8;
			Projectile.height = 8;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.netImportant = true;
			Projectile.timeLeft = 88;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
		}

		public override void AI()
		{
			if (Projectile.ai == null || Projectile.ai.Length < 3) {
				Projectile.Kill();
				return;
			}
			mode = (int)Projectile.ai[0];
			srcType = (int)Projectile.ai[2];
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);

			// The field is spawned immediately before its bolt, so any anchor
			// that already exists with this owner/mode is from an older cast.
			// Purge those once, before the first new bite can land. This is
			// cheaper and more reliable than an unsynced cast id.
			if (!purgedOldAnchors) {
				purgedOldAnchors = true;
				PurgeOldAnchors();
			}
			age++;

			List<(float Order, Vector2 Pos)> anchors = GatherAnchors();
			if (mode == (int)ModeFaraday)
				TickFaraday(anchors);
			else
				TickPolarity(anchors);

			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.32f);
			if (age >= 70)
				Projectile.Kill();
		}

		private void PurgeOldAnchors()
		{
			int type = ModContent.ProjectileType<MagicalChainAnchorProj>();
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (!p.active || p.type != type || p.owner != Projectile.owner)
					continue;
				if (p.ai == null || p.ai.Length < 3 || (int)p.ai[0] != mode)
					continue;
				p.Kill();
			}
		}

		private List<(float Order, Vector2 Pos)> GatherAnchors()
		{
			List<(float Order, Vector2 Pos)> list = new();
			int type = ModContent.ProjectileType<MagicalChainAnchorProj>();
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (!p.active || p.type != type || p.owner != Projectile.owner)
					continue;
				if (p.ai == null || p.ai.Length < 3 || (int)p.ai[0] != mode)
					continue;
				list.Add((p.ai[1], p.Center));
			}
			list.Sort((a, b) => a.Order.CompareTo(b.Order));
			return list;
		}

		private void TickFaraday(List<(float Order, Vector2 Pos)> anchors)
		{
			if (anchors.Count == 0)
				return;

			if (age >= 18 && age % 6 == 0) {
				int damage = Math.Max(1, (int)(Projectile.damage * 0.7f));
				for (int i = 1; i < anchors.Count; i++) {
					Vector2 a = anchors[i - 1].Pos;
					Vector2 b = anchors[i].Pos;
					for (int n = 0; n < Main.maxNPCs; n++) {
						NPC npc = Main.npc[n];
						if (!MagicalReleaseCommon.Hittable(npc) || struck.Contains(npc.whoAmI))
							continue;
						if (!LineHits(a, b, npc, 24f))
							continue;
						if (MagicalReleaseCommon.Strike(Main.player[Projectile.owner], npc, damage,
							1.5f, accent, a, pierceArmor: true))
							struck.Add(npc.whoAmI);
					}
				}
			}

			if (age == 54) {
				int damage = Math.Max(1, (int)(Projectile.damage * 1.45f));
				for (int i = 0; i < anchors.Count; i++) {
					Vector2 a = anchors[i].Pos;
					for (int n = 0; n < Main.maxNPCs; n++) {
						NPC npc = Main.npc[n];
						if (!MagicalReleaseCommon.Hittable(npc) || struck.Contains(npc.whoAmI))
							continue;
						if (Vector2.DistanceSquared(npc.Center, a) > 78f * 78f)
							continue;
						// The field draws one ring per anchor after this loop.
						// Suppress Strike's own heavy-hit ring so the finisher
						// cannot stack two concentric circles on that anchor.
						if (MagicalReleaseCommon.Strike(Main.player[Projectile.owner], npc, damage,
							2.2f, accent, a, pierceArmor: true, hitFx: false))
							struck.Add(npc.whoAmI);
					}
				}
				if (!Main.dedServ) {
					for (int i = 0; i < anchors.Count; i++)
						MagicalFx.QueueRing(anchors[i].Pos, 54f, StormFx.Hot(accent), 0.4f, 14, 1.1f);
				}
			}
		}

		private void TickPolarity(List<(float Order, Vector2 Pos)> anchors)
		{
			if (anchors.Count == 0)
				return;

			if (age >= 12 && age % 6 == 0 && MagicalReleaseCommon.CanMoveNpcs) {
				for (int i = 0; i < anchors.Count; i++) {
					Vector2 a = anchors[i].Pos;
					// Even poles pull toward the NEXT anchor; odd poles push away
					// from the caster. That is the red-link/blue-link grammar.
					Vector2 poleTarget = i + 1 < anchors.Count ? anchors[i + 1].Pos : a;
					bool pull = i % 2 == 0;
					for (int n = 0; n < Main.maxNPCs; n++) {
						NPC npc = Main.npc[n];
						if (!MagicalReleaseCommon.Hittable(npc))
							continue;
						float d2 = Vector2.DistanceSquared(npc.Center, a);
						if (d2 > 110f * 110f)
							continue;
						if (DummySafety.Pin(npc))
							continue;
						Vector2 dir = pull ? (poleTarget - npc.Center).SafeNormalize(Vector2.UnitX) : (npc.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
						npc.velocity += dir * (pull ? 0.32f : 0.38f);
					}
				}
			}

			if (age == 44) {
				int damage = Math.Max(1, (int)(Projectile.damage * 1.35f));
				Vector2 last = anchors[anchors.Count - 1].Pos;
				for (int i = 0; i < anchors.Count; i++) {
					Vector2 a = anchors[i].Pos;
					for (int n = 0; n < Main.maxNPCs; n++) {
						NPC npc = Main.npc[n];
						if (!MagicalReleaseCommon.Hittable(npc) || struck.Contains(npc.whoAmI))
							continue;
						if (Vector2.DistanceSquared(npc.Center, a) > 96f * 96f)
							continue;
						if (MagicalReleaseCommon.CanMoveNpcs && !DummySafety.Pin(npc)) {
							Vector2 dir = (last - npc.Center).SafeNormalize(Vector2.UnitX);
							npc.velocity += dir * 4.2f;
						}
						// Same one-ring rule as the Faraday finisher.
						if (MagicalReleaseCommon.Strike(Main.player[Projectile.owner], npc, damage,
							2.8f, accent, a, pierceArmor: true, hitFx: false))
							struck.Add(npc.whoAmI);
					}
				}
				if (!Main.dedServ) {
					for (int i = 0; i < anchors.Count; i++)
						MagicalFx.QueueRing(anchors[i].Pos, 48f, new Color(255, 120, 210), 0.4f, 12, 1.2f);
				}
			}
		}

		private static bool LineHits(Vector2 a, Vector2 b, NPC npc, float halfWidth)
		{
			Vector2 seg = b - a;
			float lenSq = seg.LengthSquared();
			if (lenSq < 1f)
				return false;
			float t = MathHelper.Clamp(Vector2.Dot(npc.Center - a, seg) / lenSq, 0f, 1f);
			Vector2 closest = a + seg * t;
			float reach = halfWidth + Math.Max(npc.width, npc.height) * 0.5f;
			return Vector2.DistanceSquared(npc.Center, closest) <= reach * reach;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			List<(float Order, Vector2 Pos)> anchors = GatherAnchors();
			float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
			Color hot = StormFx.Hot(accent);
			Color modeColor = mode == (int)ModePolarity ? new Color(255, 120, 210) : accent;

			if (anchors.Count < 2) {
				MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 54f, modeColor, 0.18f * fade);
				MiniYoyoFx.DrawDashedRing(Projectile.Center, 30f, 1.8f, modeColor, 10,
					Main.GameUpdateCount * 0.04f);
				return false;
			}

			// Fill the closed lattice very faintly, so the eye reads a shape
			// rather than a loose zigzag.
			Vector2[] poly = new Vector2[anchors.Count];
			for (int i = 0; i < anchors.Count; i++)
				poly[i] = anchors[i].Pos;
			MiniYoyoFx.DrawSolidPolygon(poly, modeColor, 0.045f * fade);

			for (int i = 1; i < anchors.Count; i++) {
				Vector2 a = anchors[i - 1].Pos;
				Vector2 b = anchors[i].Pos;
				MiniYoyoFx.DrawBeamV2(a, b, hot, modeColor, 2.2f + 1.2f * fade, fade, 0.8f);
				MiniYoyoFx.DrawLightning(a, b, 2.4f, modeColor, 0.72f * fade,
					0.18f, 4, Projectile.identity * 31 + i);
				MiniYoyoFx.DrawGlowAdditive((a + b) * 0.5f, 12f, hot, 0.18f * fade);
			}

			// Alternating poles for Polarity; a bright lattice joint for
			// Faraday. Both pulse on the same clock so the field feels alive.
			for (int i = 0; i < anchors.Count; i++) {
				float pulse = StormFx.Pulse(Projectile.identity + i, 0.13f);
				Vector2 p = anchors[i].Pos;
				MiniYoyoFx.DrawGlowAdditive(p, 24f + 8f * pulse, modeColor, 0.28f * fade);
				if (mode == (int)ModePolarity) {
					float sign = (i % 2 == 0) ? -1f : 1f;
					MiniYoyoFx.DrawSolidDisc(p + new Vector2(sign * 7f, 0f), 3.2f,
						sign < 0f ? new Color(120, 220, 255) : new Color(255, 120, 210), 0.8f * fade);
				}
			}

			if (age >= 54) {
				float flash = MathHelper.Clamp((age - 54) / 8f, 0f, 1f);
				for (int i = 0; i < anchors.Count; i++) {
					MiniYoyoFx.DrawGlowAdditive(anchors[i].Pos, 70f * flash, hot, 0.3f * (1f - flash));
					MiniYoyoFx.DrawShockwaveRing(anchors[i].Pos, 10f, 64f, 0.8f, flash, modeColor, 2.5f);
				}
			}
			return false;
		}
	}

	/// <summary>
	/// Ball Lightning. Real gravity, real tile bounces, a rolling spin and a
	/// one-hit-per-enemy contact list. It deliberately does not fire extra
	/// bolts: the orb itself is the whole attack.
	/// </summary>
	public class MagicalBallLightningProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float power;
		private int srcType;
		private Color accent = new(130, 190, 255);
		private readonly List<int> hit = new();
		private int age;
		private int sparkClock;

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.netImportant = true;
			Projectile.timeLeft = 210;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 60;
		}

		public override void AI()
		{
			power = MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
			srcType = (int)Projectile.ai[2];
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			age++;

			// Rolling ball: gravity, air drag, a real bounce, and spin driven
			// by horizontal travel. This is what separates it from the old
			// "floating orb that slides at a dummy" read.
			Projectile.velocity.Y += 0.20f;
			if (Projectile.velocity.Y > 16f)
				Projectile.velocity.Y = 16f;
			Projectile.velocity.X *= 0.992f;
			Projectile.rotation += Projectile.velocity.X * 0.035f;

			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.5f + 0.35f * power));

			if (!Main.dedServ && ++sparkClock >= 5) {
				sparkClock = 0;
				float a = Main.GameUpdateCount * 0.31f + Projectile.identity;
				Vector2 v = new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * 1.1f;
				YoyoParticles.SpawnSpark(Projectile.Center + v * 1.6f, -v, accent, 0.55f, 12, 0.75f);
			}
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			if (Projectile.velocity.X != oldVelocity.X)
				Projectile.velocity.X = -oldVelocity.X * 0.82f;
			if (Projectile.velocity.Y != oldVelocity.Y)
				Projectile.velocity.Y = -oldVelocity.Y * 0.76f;
			if (Math.Abs(Projectile.velocity.Y) < 1.4f)
				Projectile.velocity.Y = -3.2f;

			if (!Main.dedServ) {
				SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.25f, Pitch = 0.5f },
					Projectile.Center);
				for (int i = 0; i < 5; i++) {
					float a = i * MathHelper.TwoPi / 5f + Main.GameUpdateCount * 0.07f;
					YoyoParticles.SpawnSpark(Projectile.Center,
						a.ToRotationVector2() * 2.4f, accent, 0.65f, 14, 0.8f);
				}
			}
			return false;
		}

		public override bool? CanHitNPC(NPC target)
			=> hit.Contains(target.whoAmI) ? false : null;

		public override void OnHitNPC(NPC target, NPC.HitInfo hitInfo, int damageDone)
		{
			if (!hit.Contains(target.whoAmI))
				hit.Add(target.whoAmI);
			if (Main.dedServ)
				return;
			// A Pop disc plus a QueueRing read as two concentric circles. One
			// star-and-spark bite is the same hit beat with a single silhouette.
			MagicalFx.LightningHit(target.Center, accent, power,
				Projectile.rotation + Main.GameUpdateCount * 0.05f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 c = Projectile.Center;
			float pulse = StormFx.Pulse(Projectile.identity, 0.16f);
			Color hot = StormFx.Hot(accent);
			float fade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);

			// Short ionised wake, drawn from the live velocity rather than a
			// per-client particle trail.
			for (int i = 1; i <= 4; i++) {
				Vector2 p = c - Projectile.velocity * (i * 0.42f);
				MiniYoyoFx.DrawGlowAdditive(p, 30f - i * 4f, accent, (0.16f - i * 0.025f) * fade);
			}

			MiniYoyoFx.DrawGlowAdditive(c, (58f + 16f * pulse) * fade, accent, 0.28f * fade);
			MiniYoyoFx.DrawGlowAdditive(c, (32f + 8f * pulse) * fade, hot, 0.4f * fade);
			MiniYoyoFx.DrawSolidDisc(c, 20f + 3f * pulse, Color.Lerp(accent, Color.White, 0.45f), 0.85f * fade);
			MiniYoyoFx.DrawSolidDisc(c, 11f + 2f * pulse, Color.White, 0.9f * fade);
			MiniYoyoFx.DrawDashedRing(c, 16f + 4f * pulse, 1.8f, hot, 8,
				Main.GameUpdateCount * 0.09f + Projectile.identity);

			// Internal arcs: deterministic spokes that rotate with the ball.
			for (int i = 0; i < 4; i++) {
				float a = Projectile.rotation + i * MathHelper.TwoPi / 4f;
				Vector2 p = c + a.ToRotationVector2() * (12f + 4f * pulse);
				MiniYoyoFx.DrawLightning(c, p, 1.8f, hot, 0.62f * fade,
					0.30f, 3, Projectile.identity * 19 + i);
			}
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			// Same single-silhouette discharge as a hit; no second Pop disc.
			MagicalFx.LightningHit(Projectile.Center, accent, power,
				Projectile.rotation, discharge: true);
		}
	}

	/// <summary>
	/// Thunderhead. A stationary storm cloud at the cursor that fires one
	/// Straight bolt at a different nearby enemy each pulse. Each enemy can
	/// be struck once by a given cloud; the cloud does not machine-gun one
	/// target. The cloud body, internal arcs, rain and eye are all drawn from
	/// the game clock so the silhouette is stable.
	/// </summary>
	public class MagicalThunderheadProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float power;
		private int srcType;
		private Color accent = new(130, 190, 255);
		private int age;
		private readonly List<int> struck = new();

		public override void SetDefaults()
		{
			Projectile.width = 20;
			Projectile.height = 20;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.netImportant = true;
			Projectile.timeLeft = 168;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
		}

		public override void AI()
		{
			power = MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
			srcType = (int)Projectile.ai[2];
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			age++;

			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.55f);
			if (age < 18 || age % 26 != 0 || struck.Count >= 5)
				return;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			NPC target = FindTarget();
			if (target == null)
				return;
			int damage = Math.Max(1, (int)(Projectile.damage * (0.85f + 0.2f * power)));
			Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
			// Direction is already carried by the spawned bolt's velocity.
			MagicalReleaseCommon.SpawnChild(Main.player[Projectile.owner],
				Projectile.Center + dir * 16f, dir * MagicalLightningProj.BoltSpeed,
				ModContent.ProjectileType<MagicalLightningProj>(), damage,
				MagicalLightningProj.ModeStraight, power, srcType);
			struck.Add(target.whoAmI);
		}

		private NPC FindTarget()
		{
			NPC best = null;
			float bestDist = 760f * 760f;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc) || struck.Contains(npc.whoAmI))
					continue;
				float d2 = Vector2.DistanceSquared(npc.Center, Projectile.Center);
				if (d2 >= bestDist)
					continue;
				if (!Collision.CanHitLine(Projectile.Center, 1, 1, npc.Center, 1, 1))
					continue;
				best = npc;
				bestDist = d2;
			}
			return best;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 c = Projectile.Center;
			float pulse = StormFx.Pulse(Projectile.identity, 0.09f);
			float fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
			Color hot = StormFx.Hot(accent);
			Vector2 bob = new(0f, (float)Math.Sin(Main.GameUpdateCount * 0.05f + Projectile.identity) * 4f);
			c += bob;

			// Cloud body: layered dark lobes with a cold rim.
			for (int i = 0; i < 6; i++) {
				float a = i * MathHelper.TwoPi / 6f + 0.35f;
				Vector2 p = c + a.ToRotationVector2() * (10f + 4f * pulse);
				float w = 44f + 10f * pulse + i % 2 * 10f;
				float h = 24f + 6f * pulse;
				MiniYoyoFx.DrawSolidEllipse(p, w, h, a * 0.2f, new Color(30, 45, 72), 0.32f * fade);
			}
			MiniYoyoFx.DrawSolidEllipse(c, 78f + 10f * pulse, 38f + 7f * pulse, 0f,
				new Color(18, 28, 50), 0.42f * fade);
			MiniYoyoFx.DrawGlowAdditive(c, 86f + 12f * pulse, accent, 0.16f * fade);

			// Internal charge and eye.
			MiniYoyoFx.DrawGlowAdditive(c + new Vector2(0f, 5f), 44f + 10f * pulse, hot, 0.28f * fade);
			MiniYoyoFx.DrawDashedRing(c + new Vector2(0f, 5f), 19f + 3f * pulse, 1.8f, hot, 10,
				Main.GameUpdateCount * 0.05f);
			for (int i = 0; i < 3; i++) {
				float a = Main.GameUpdateCount * 0.045f + i * MathHelper.TwoPi / 3f;
				Vector2 p = c + new Vector2((float)Math.Cos(a) * 22f, 4f + (float)Math.Sin(a) * 8f);
				MiniYoyoFx.DrawLightning(c + new Vector2(0f, 4f), p, 1.8f, accent,
					0.48f * fade, 0.28f, 3, Projectile.identity * 11 + i);
			}

			// Rain: short deterministic streaks under the cloud.
			for (int i = 0; i < 8; i++) {
				float a = Main.GameUpdateCount * 0.10f + i * 0.73f;
				float x = (float)Math.Sin(a) * (28f + i * 3f);
				float y = 18f + (i % 4) * 8f + (float)Math.Sin(a * 1.7f) * 3f;
				Vector2 p = c + new Vector2(x, y);
				MiniYoyoFx.DrawSoftBar(p, MathHelper.PiOver2, 9f, 1.2f, accent, 0.16f * fade);
			}

			// Muzzle flash as the next bolt charges.
			int phase = age % 26;
			if (phase > 18) {
				float charge = MathHelper.Clamp((phase - 18) / 8f, 0f, 1f);
				MiniYoyoFx.DrawGlowAdditive(c + new Vector2(0f, 6f), 28f + 28f * charge,
					hot, 0.22f * charge * fade);
			}
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			MagicalFx.Pop(Projectile.Center, 54f, accent, 0.28f);
			for (int i = 0; i < 6; i++) {
				float a = i * MathHelper.TwoPi / 6f;
				YoyoParticles.SpawnSpark(Projectile.Center,
					a.ToRotationVector2() * 3f, accent, 0.65f, 14, 0.8f);
			}
		}
	}

	/// <summary>
	/// Static Trail. A charged line along the cast aim. It hits each enemy
	/// once, applies Static Charge, and the next Magical Tech lightning hit on
	/// a charged enemy is amplified and discharges one short arc to a nearby
	/// enemy (handled in MagicalLightningProj). That discharge is the extra
	/// unique thing; the trail itself is not just another damage line.
	/// </summary>
	public class MagicalStaticTrailProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float power;
		private float tier;
		private int srcType;
		private Color accent = new(130, 190, 255);
		private readonly List<int> struck = new();
		private int age;
		private bool born;
		private float length;

		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.netImportant = true;
			Projectile.timeLeft = 92;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
		}

		public override void AI()
		{
			power = MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
			tier = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			srcType = (int)Projectile.ai[2];
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);

			if (!born) {
				born = true;
				length = 360f + 120f * power + 90f * tier;
				Projectile.rotation = Projectile.velocity.ToRotation();
				Projectile.velocity = Vector2.Zero;
				if (!Main.dedServ && Projectile.owner == Main.myPlayer)
					SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = 0.15f },
						Projectile.Center);
			}
			age++;

			Vector2 a = Projectile.Center;
			Vector2 b = a + Projectile.rotation.ToRotationVector2() * length;
			if (age >= 6 && age % 6 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
				int damage = Math.Max(1, (int)(Projectile.damage * 0.85f));
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC npc = Main.npc[i];
					if (!MagicalReleaseCommon.Hittable(npc) || struck.Contains(npc.whoAmI))
						continue;
					if (!LineHits(a, b, npc, 18f))
						continue;
					if (MagicalReleaseCommon.Strike(Main.player[Projectile.owner], npc, damage,
						1.2f, accent, a, pierceArmor: true)) {
						struck.Add(npc.whoAmI);
						npc.AddBuff(ModContent.BuffType<StaticChargeBuff>(), 150);
						npc.netUpdate = true;
					}
				}
			}
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.22f);
		}

		private static bool LineHits(Vector2 a, Vector2 b, NPC npc, float halfWidth)
		{
			Vector2 seg = b - a;
			float lenSq = seg.LengthSquared();
			if (lenSq < 1f)
				return false;
			float t = MathHelper.Clamp(Vector2.Dot(npc.Center - a, seg) / lenSq, 0f, 1f);
			Vector2 closest = a + seg * t;
			float reach = halfWidth + Math.Max(npc.width, npc.height) * 0.5f;
			return Vector2.DistanceSquared(npc.Center, closest) <= reach * reach;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (!born)
				return false;
			Vector2 a = Projectile.Center;
			Vector2 dir = Projectile.rotation.ToRotationVector2();
			Vector2 b = a + dir * length;
			float fade = MathHelper.Clamp(Projectile.timeLeft / 28f, 0f, 1f);
			Color hot = StormFx.Hot(accent);

			MiniYoyoFx.DrawBeamV2(a, b, hot, accent, 3.2f + 1.4f * fade, fade, 1.4f);
			MiniYoyoFx.DrawLightning(a, b, 3.0f, accent, 0.85f * fade,
				0.16f, 4, Projectile.identity * 17 + 3);

			// Two side forks make the trail read as a discharge path, not a
			// painted line.
			for (int i = 0; i < 2; i++) {
				float t = 0.34f + i * 0.32f;
				Vector2 p = a + dir * (length * t);
				Vector2 side = new Vector2(-dir.Y, dir.X) * (i == 0 ? 1f : -1f);
				Vector2 fork = p + side * (22f + 10f * power);
				MiniYoyoFx.DrawLightning(p, fork, 2.0f, hot, 0.55f * fade,
					0.3f, 3, Projectile.identity * 23 + i);
			}

			// Travelling beads.
			for (int i = 0; i < 6; i++) {
				float t = (i / 6f + Main.GameUpdateCount * 0.022f) % 1f;
				Vector2 p = a + dir * (length * t);
				MiniYoyoFx.DrawGlowAdditive(p, 8f + 4f * power, hot, 0.2f * fade);
			}

			MiniYoyoFx.DrawGlowAdditive(b, 30f + 10f * power, accent, 0.26f * fade);
			MiniYoyoFx.DrawDashedRing(b, 16f + 3f * power, 1.5f, hot, 8,
				Main.GameUpdateCount * 0.07f);
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			Vector2 b = Projectile.Center + Projectile.rotation.ToRotationVector2() * length;
			MagicalFx.Pop(b, 34f, accent, 0.26f);
		}
	}

	/// <summary>
	/// Static Charge payoff. The 25% damage bonus is server-authoritative and
	/// the mark is a real buff, so it is not a client-only decoration. The
	/// short arc spawn is done in MagicalLightningProj.OnHitNPC.
	/// </summary>
	public class StaticChargeGlobalNPC : GlobalNPC
	{
		public override void ModifyHitByProjectile(NPC npc, Projectile projectile,
			ref NPC.HitModifiers modifiers)
		{
			if (projectile.type == ModContent.ProjectileType<MagicalLightningProj>()
				&& npc.HasBuff(ModContent.BuffType<StaticChargeBuff>()))
				modifiers.FinalDamage *= 1.25f;
		}

		public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			if (!npc.HasBuff(ModContent.BuffType<StaticChargeBuff>()))
				return;
			float pulse = StormFx.Pulse(npc.whoAmI, 0.14f);
			Vector2 c = npc.Center;
			Color charge = new Color(120, 220, 255);
			for (int i = 0; i < 2; i++) {
				float a = Main.GameUpdateCount * 0.055f + i * MathHelper.Pi + npc.whoAmI;
				Vector2 p = c + a.ToRotationVector2() * (npc.width * 0.55f + 8f);
				MiniYoyoFx.DrawLightning(c, p, 1.6f, charge, 0.38f + 0.18f * pulse,
					0.3f, 3, npc.whoAmI * 19 + i);
			}
			MiniYoyoFx.DrawGlowAdditive(c, 34f + 8f * pulse, charge, 0.10f);
		}
	}
}
