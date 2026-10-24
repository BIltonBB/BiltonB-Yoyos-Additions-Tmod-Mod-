using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Tech;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// The live end-puddle of a B-P beam. Two modes:
	///   Prism   - a cut-gem lens that fires three (or five) short refracted rays.
	///   Circuit - a gold/white rune triangle that zaps crossers and discharges.
	/// The anchor follows the live beam tip; it is not a cursor puddle cast.
	/// </summary>
	public class MagicalBeamAnchorProj : ModProjectile
	{
		public const float ModePrism = 0f;
		public const float ModeCircuit = 1f;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int mode;
		private float power;
		private int srcType;
		private MagicalBeamProj beam;
		private Vector2 aim = Vector2.UnitX;
		private Color accent = new(160, 220, 255);
		private int age;
		private int fxClock;
		private readonly List<int> circuitStruck = new();
		private bool discharged;

		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			// A controller: damage is applied through MagicalReleaseCommon.Strike,
			// never through a contact hitbox that could double-dip the beam.
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.netImportant = true;
			Projectile.timeLeft = 96;
		}

		public override void AI()
		{
			// Projectile.ai has three vanilla slots. Never read ai[3] here:
			// it is both out of bounds and not part of the vanilla sync packet.
			if (Projectile.ai == null || Projectile.ai.Length < 3) {
				Projectile.Kill();
				return;
			}
			mode = (int)Projectile.ai[0];
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			srcType = (int)Projectile.ai[2];
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);

			Player owner = OwnerOf();
			if (owner == null) {
				Projectile.Kill();
				return;
			}

			beam = FindBeam(owner);
			if (beam != null && beam.Projectile.active) {
				Projectile.Center = beam.EndPoint;
				aim = beam.AimDirection;
				Projectile.damage = beam.Projectile.damage;
			}
			else {
				beam = null;
				if (Projectile.timeLeft > 18)
					Projectile.timeLeft = 18;
			}

			age++;
			if (mode == (int)ModePrism)
				TickPrism(owner);
			else
				TickCircuit(owner);

			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.45f + 0.35f * power));
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active && !p.dead ? p : null;
		}

		private MagicalBeamProj FindBeam(Player owner)
		{
			int type = ModContent.ProjectileType<MagicalBeamProj>();
			MagicalBeamProj best = null;
			float bestDistSq = float.MaxValue;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (!p.active || p.type != type || p.owner != owner.whoAmI)
					continue;
				if (p.ModProjectile is not MagicalBeamProj candidate || !candidate.HasEndPuddle)
					continue;
				// More than one B-P beam can theoretically be alive at once.
				// The anchor was spawned at a specific tip, so follow the
				// nearest matching tip instead of an unsynced identity slot.
				float distSq = Vector2.DistanceSquared(Projectile.Center, candidate.EndPoint);
				if (distSq >= bestDistSq)
					continue;
				best = candidate;
				bestDistSq = distSq;
			}
			return best;
		}

		private void TickPrism(Player owner)
		{
			if (beam == null || owner.whoAmI != Main.myPlayer)
				return;
			fxClock++;
			if (age > 78 || fxClock % 15 != 0)
				return;

			float tier = MagicalTier.AbilityTier01(srcType);
			int rays = tier >= 0.66f ? 5 : 3;
			float[] angles = rays == 5
				? new[] { -1.05f, -0.55f, 0f, 0.55f, 1.05f }
				: new[] { -0.55f, 0f, 0.55f };

			int type = ModContent.ProjectileType<MagicalPrismRayProj>();
			int damage = Math.Max(1, (int)(Projectile.damage * 0.55f));
			float length = 340f + 120f * power + 70f * tier;
			for (int i = 0; i < angles.Length; i++) {
				Vector2 dir = aim.RotatedBy(angles[i]);
				MagicalReleaseCommon.Spawn(owner, Projectile.Center, dir, type, damage,
					length, power, srcType);
			}

			if (!Main.dedServ) {
				Color hot = Color.Lerp(accent, Color.White, 0.55f);
				YoyoParticles.SpawnRing(Projectile.Center, 8f, 44f + 22f * power, 12, hot, 1.8f);
				YoyoParticles.SpawnBloom(Projectile.Center, 6f, 30f + 14f * power, 12, accent, 0.3f);
				SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.22f, Pitch = 0.35f }, Projectile.Center);
			}
		}

		private void TickCircuit(Player owner)
		{
			CircuitNodes(out Vector2 end, out Vector2 a, out Vector2 b);

			if (owner.whoAmI == Main.myPlayer)
				fxClock++;
			if (owner.whoAmI == Main.myPlayer && fxClock % 10 == 0) {
				Vector2 mid = (a + b) * 0.5f;
				YoyoParticles.SpawnSpark(mid + Main.rand.NextVector2Circular(20f, 20f),
					Main.rand.NextVector2Circular(1.4f, 1.4f), accent, 0.75f, 14, 0.92f);
			}

			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			// Crossing a closed segment is one zap per enemy per circuit.
			if (age % 8 == 0) {
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC npc = Main.npc[i];
					if (!MagicalReleaseCommon.Hittable(npc) || circuitStruck.Contains(i))
						continue;
					if (!LineHits(end, a, npc) && !LineHits(a, b, npc) && !LineHits(b, end, npc))
						continue;
					circuitStruck.Add(i);
					MagicalReleaseCommon.Strike(owner, npc,
						Math.Max(1, (int)(Projectile.damage * 0.6f)), 1.1f,
						accent, end, pierceArmor: true);
					if (!Main.dedServ)
						MiniYoyoFx.FlashFX(npc.Center, Color.Lerp(accent, Color.White, 0.5f), 0.25f);
				}
			}

			if (age >= 48)
				DischargeCircuit(owner, end, a, b);
		}

		private void CircuitNodes(out Vector2 end, out Vector2 a, out Vector2 b)
		{
			end = Projectile.Center;
			Vector2 side = new(-aim.Y, aim.X);
			float radius = 78f + 26f * power;
			a = end + side * radius - aim * 20f;
			b = end - side * radius - aim * 20f;
		}

		private static bool LineHits(Vector2 a, Vector2 b, NPC npc)
		{
			float point = 0f;
			return Collision.CheckAABBvLineCollision(npc.Hitbox.TopLeft(), npc.Hitbox.Size(),
				a, b, 10f, ref point);
		}

		private void DischargeCircuit(Player owner, Vector2 end, Vector2 a, Vector2 b)
		{
			if (discharged)
				return;
			discharged = true;
			if (Main.netMode == NetmodeID.MultiplayerClient || owner == null) {
				Projectile.Kill();
				return;
			}

			int damage = Math.Max(1, (int)(Projectile.damage * 2.1f));
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc))
					continue;
				float dEnd = Vector2.DistanceSquared(npc.Center, end);
				float dA = Vector2.DistanceSquared(npc.Center, a);
				float dB = Vector2.DistanceSquared(npc.Center, b);
				if (dEnd > 160f * 160f && dA > 105f * 105f && dB > 105f * 105f)
					continue;
				MagicalReleaseCommon.Strike(owner, npc, damage, 2.4f, accent, end, pierceArmor: true);
			}

			if (!Main.dedServ) {
				Color gold = new Color(255, 226, 140);
				YoyoParticles.SpawnRing(end, 14f, 150f, 26, Color.White, 3.6f);
				YoyoParticles.SpawnRing(end, 6f, 104f, 22, gold, 2.4f);
				YoyoParticles.SpawnBloom(end, 12f, 70f, 22, gold, 0.45f);
				SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.6f, Pitch = 0.1f }, end);
				MiniYoyoFx.Shake(3, 8, end);
			}
			// Keep the controller alive for a few frames so PreDraw can lay the
			// discharge bolt over the triangle. No SpriteBatch draw from AI.
			Projectile.timeLeft = 8;
		}

		public override void OnKill(int timeLeft)
		{
			if (mode == (int)ModeCircuit && !discharged) {
				Player owner = OwnerOf();
				CircuitNodes(out Vector2 end, out Vector2 a, out Vector2 b);
				DischargeCircuit(owner, end, a, b);
				discharged = true;
				return;
			}
			if (Main.dedServ)
				return;
			if (mode == (int)ModePrism) {
				YoyoParticles.SpawnRing(Projectile.Center, 8f, 54f, 18, accent, 2.2f);
				YoyoParticles.SpawnBloom(Projectile.Center, 6f, 34f, 16, accent, 0.35f);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 c = Projectile.Center;
			Color hot = Color.Lerp(accent, Color.White, 0.55f);
			float pulse = 1f + 0.06f * (float)Math.Sin(Main.GameUpdateCount * 0.18f + Projectile.whoAmI);

			// Shared puddle body: a soft lens plus a bright heart. No flat disc.
			MiniYoyoFx.DrawGlowAdditive(c, (58f + 18f * power) * pulse, accent, 0.30f);
			MiniYoyoFx.DrawGlowAdditive(c, (30f + 10f * power) * pulse, hot, 0.48f);
			MiniYoyoFx.DrawSolidEllipse(c, 42f + 12f * power, 24f + 8f * power,
				aim.ToRotation(), accent, 0.13f);

			if (mode == (int)ModePrism) {
				float rot = Main.GameUpdateCount * 0.035f + Projectile.whoAmI;
				float radius = 30f + 12f * power;
				Vector2[] pts = new Vector2[6];
				for (int i = 0; i < 6; i++) {
					pts[i] = c + (rot + MathHelper.TwoPi * i / 6f).ToRotationVector2() * radius;
					MiniYoyoFx.DrawGlowAdditive(pts[i], 10f + 5f * power, hot, 0.35f);
				}
				MiniYoyoFx.DrawSolidPolygon(pts, accent, 0.10f);
				for (int i = 0; i < 6; i += 2) {
					MiniYoyoFx.DrawSoftBar((pts[i] + pts[(i + 1) % 6]) * 0.5f,
						(pts[(i + 1) % 6] - pts[i]).ToRotation(),
						Vector2.Distance(pts[i], pts[(i + 1) % 6]), 2.4f, hot, 0.55f);
				}
				MiniYoyoFx.DrawDashedRing(c, radius + 7f, 1.8f, accent, 8, rot);
			}
			else {
				CircuitNodes(out Vector2 end, out Vector2 a, out Vector2 b);
				Color gold = new Color(255, 226, 140);
				MiniYoyoFx.DrawSolidTriangle(end, a, b, gold, 0.055f);
				DrawCircuitEdge(end, a, gold, 0.9f);
				DrawCircuitEdge(a, b, gold, 0.9f);
				DrawCircuitEdge(b, end, gold, 0.9f);
				MiniYoyoFx.DrawDashedRing(end, 26f + 8f * power, 2.2f, gold, 10,
					Main.GameUpdateCount * 0.04f);
				MiniYoyoFx.DrawGlowAdditive(a, 18f, gold, 0.35f);
				MiniYoyoFx.DrawGlowAdditive(b, 18f, gold, 0.35f);

				if (discharged) {
					float a01 = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
					int seed = (int)Main.GameUpdateCount + Projectile.whoAmI;
					MiniYoyoFx.DrawLightning(end, a, 3f, Color.White, 0.85f * a01, 0.22f, 3, seed);
					MiniYoyoFx.DrawLightning(a, b, 3f, gold, 0.85f * a01, 0.22f, 3, seed + 1);
					MiniYoyoFx.DrawLightning(b, end, 3f, Color.White, 0.85f * a01, 0.22f, 3, seed + 2);
				}
			}
			return false;
		}

		private static void DrawCircuitEdge(Vector2 a, Vector2 b, Color gold, float alpha)
		{
			MiniYoyoFx.DrawCapsule(a, b, 2.6f, gold, alpha * 0.7f, softCaps: true);
			MiniYoyoFx.DrawGlowAdditive((a + b) * 0.5f, 12f, gold, alpha * 0.22f);
			float t = (Main.GameUpdateCount * 0.02f) % 1f;
			Vector2 p = Vector2.Lerp(a, b, t);
			MiniYoyoFx.DrawGlowAdditive(p, 9f, Color.Lerp(gold, Color.White, 0.4f), alpha * 0.5f);
		}
	}

	/// <summary>
	/// A short refracted ray from Prism Gate. It is a controller (friendly =
	/// false) and lands one hit per enemy through Strike, so the body can be a
	/// pure VFX bead chain without a contact hitbox.
	/// </summary>
	public class MagicalPrismRayProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float angle;
		private float liveLength;
		private float grow;
		private bool born;
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
			Projectile.timeLeft = 16;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
		}

		public override void AI()
		{
			if (!born) {
				born = true;
				angle = Projectile.velocity.ToRotation();
				Projectile.velocity = Vector2.Zero;
			}
			Player owner = OwnerOf();
			if (owner == null) {
				Projectile.Kill();
				return;
			}

			Projectile.rotation = angle;
			Vector2 dir = angle.ToRotationVector2();
			liveLength = Raycast(Projectile.Center, dir, Math.Max(48f, Projectile.ai[0]));
			grow = MathHelper.Clamp((16 - Projectile.timeLeft) / 3f, 0f, 1f);

			Color accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			Vector2 end = Projectile.Center + dir * liveLength;
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.35f);
			Lighting.AddLight(end, accent.ToVector3() * 0.35f);

			if (Main.netMode != NetmodeID.MultiplayerClient && Projectile.timeLeft % 3 == 0) {
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC npc = Main.npc[i];
					if (!MagicalReleaseCommon.Hittable(npc) || struck.Contains(i))
						continue;
					float point = 0f;
					if (!Collision.CheckAABBvLineCollision(npc.Hitbox.TopLeft(), npc.Hitbox.Size(),
						Projectile.Center, end, 10f, ref point))
						continue;
					struck.Add(i);
					MagicalReleaseCommon.Strike(owner, npc, Projectile.damage, 1.4f, accent,
						Projectile.Center, pierceArmor: true);
				}
			}
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active && !p.dead ? p : null;
		}

		private static float Raycast(Vector2 start, Vector2 dir, float maxLen)
		{
			const float step = 16f;
			for (float d = step; d <= maxLen; d += step) {
				Vector2 p = start + dir * d;
				if (Collision.SolidCollision(p - new Vector2(4f, 4f), 8, 8))
					return MathHelper.Max(20f, d - 8f);
			}
			return maxLen;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (liveLength < 4f)
				return false;
			Color accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			Color core = Color.Lerp(accent, Color.White, 0.65f);
			Vector2 end = Projectile.Center + angle.ToRotationVector2() * liveLength;
			float width = 4.5f * grow;
			MiniYoyoFx.DrawBeamV2(Projectile.Center, end, core, accent, width, grow, 1.1f);
			MiniYoyoFx.DrawBeamHeatShimmer(Projectile.Center, end, accent, width, grow);
			MiniYoyoFx.DrawChromaticSplit((Projectile.Center + end) * 0.5f, angle,
				liveLength * grow, width * 0.8f, core, 0.20f * grow, 2f);
			MiniYoyoFx.DrawGlowAdditive(end, 18f + 8f * grow, core, 0.28f * grow);
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			Color accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			Vector2 end = Projectile.Center + angle.ToRotationVector2() * liveLength;
			YoyoParticles.SpawnBloom(end, 5f, 24f, 12, accent, 0.28f);
			YoyoParticles.SpawnRing(end, 4f, 30f, 12, accent, 1.6f);
		}
	}
}
