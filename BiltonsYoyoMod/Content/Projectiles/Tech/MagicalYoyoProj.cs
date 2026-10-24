using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Tech;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Magical Tech shell: one orbiting mirror guard, spawned per click (two with
	/// a Yoyo Glove, cap 3/6). Draws the SELECTED yoyo's projectile sprite with a
	/// fast spin, rides an evenly spaced formation that auto-reforms whenever the
	/// count changes, and feeds the Orbital Convergence core while it charges.
	///
	/// Lifetime: 1/10 of the source yoyo's flight time (20s if infinite), scaled
	/// by the player's yoyo damage bonus - both lifetime and orbit speed follow
	/// the live percentage bonus, so losing a bonus shrinks them back down.
	/// </summary>
	public class MagicalYoyoProj : TechProjBase
	{
		// ai[2]: 0 = flying out to slot, 1 = orbiting.

		// Synced (SendExtraAI): formation order + base lifetime + spawn scale.
		public int OrbitId;

		/// <summary>True while a Magical Feeder has this orbiter in flight. The
		/// orbiter keeps orbiting (damage and stats untouched) but stops drawing,
		/// so the ring visibly thins and re-fills as the feeders come home. Purely
		/// visual state, synced only when it changes.</summary>
		public bool FeederAway;
		public int BaseLifetimeTicks;
		public float SpawnScale;

		private float angle;              // chased formation angle (local)
		private bool angleInit;
		private float orbitRadius = MagicalOrbit.OrbitRadius;
		private float lastScale;          // scale the current lifetime was computed with
		private double lifetimeAge;       // ticks lived (scaled clock, remapped on rescale)
		private double lifetimeMax;       // current scaled lifetime in ticks
		private bool convergenceFlare;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 8;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 18;
			Projectile.height = 18;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.timeLeft = MagicalOrbit.InfiniteLifetimeTicks; // replaced in OnSpawn
			Projectile.tileCollide = false; // Phasing blades: the arcane signature.
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 12;
		}

		public override void SendExtraAI(BinaryWriter writer)
		{
			base.SendExtraAI(writer);
			writer.Write((short)OrbitId);
			writer.Write((short)BaseLifetimeTicks);
			writer.Write(SpawnScale);
			writer.Write(FeederAway);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			base.ReceiveExtraAI(reader);
			OrbitId = reader.ReadInt16();
			BaseLifetimeTicks = reader.ReadInt16();
			SpawnScale = reader.ReadSingle();
			FeederAway = reader.ReadBoolean();
		}

		public override void OnSpawn(IEntitySource source)
		{
			SourceProjType = SourceProjType == 0 ? (int)Projectile.ai[1] : SourceProjType;
			ResolveSourceProjType();
			YoyoTechCommon.ApplyEnhancementToSpawn(Projectile, null, ModId);
			Player owner = OwnerPlayer();

			if (Projectile.owner == Main.myPlayer && OrbitId <= 0 && owner != null)
				OrbitId = MagicalOrbit.NextOrbitId(owner);
			if (BaseLifetimeTicks <= 0)
				BaseLifetimeTicks = MagicalOrbit.BaseLifetimeTicks(SourceProjType);
			if (SpawnScale <= 0f)
				SpawnScale = owner != null ? MagicalOrbit.StatScale(owner) : 1f;
			lastScale = SpawnScale;
			lifetimeAge = 0d;
			lifetimeMax = LifeMaxFor(BaseLifetimeTicks, SpawnScale);
			// Failsafe only: expiry is driven by the local age clock below, so a
			// lost or unsynced timeLeft can never leave a ghost guard in orbit.
			Projectile.timeLeft = (int)Math.Clamp(lifetimeMax, 1d, MagicalOrbit.MaxSafeTimeLeft);

			// Arrival swirl drawn at the hand - each click reads as its own cast.
			if (!Main.dedServ) {
				Color accent = MagicalOrbit.AccentOf(SourceProjType);
				YoyoParticles.SpawnRing(Projectile.Center, 4f, 30f, 14, accent, 2.2f);
				YoyoParticles.SpawnBurst(Projectile.Center, 3, 2.4f, accent, 0.8f, 14);
				for (int i = 0; i < 6; i++) {
					Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
						DustID.MagicMirror, Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-2.5f, 2.5f), 120, default, 0.9f);
					d.noGravity = true;
				}
			}
		}

		/// <summary>The source projectile is the art/identity key. If the spawn
		/// path only carried the item (or a remote copy is missing ai[1]), derive
		/// the projectile from the synced source item. This is the vanilla-yoyo
		/// safety net: a real projectile sprite is always preferred to a blank.</summary>
		private void ResolveSourceProjType()
		{
			if (SourceProjType > ProjectileID.None || SourceItemType <= ItemID.None)
				return;
			if (ContentSamples.ItemsByType.TryGetValue(SourceItemType, out Item sample)
				&& sample != null && sample.shoot > ProjectileID.None) {
				SourceProjType = sample.shoot;
			}
		}

		/// <summary>Scaled lifetime cap (ticks) for a base lifetime + scale pair.</summary>
		private static double LifeMaxFor(int baseTicks, float scale)
		{
			return Math.Clamp(baseTicks * (double)Math.Max(scale, 0.05f), 1d, MagicalOrbit.MaxSafeTimeLeft);
		}

		/// <summary>
		/// Re-derives the scaled lifetime from the live stat and remaps the age so
		/// the REMAINING FRACTION is kept: gaining a damage bonus stretches the
		/// whole lifetime, losing it squeezes it back - neither compounds on an
		/// already-scaled value. Runs on every side, so all clients expire the
		/// same shell at the same moment.
		/// </summary>
		private void StepLifetime(Player owner)
		{
			float scaleNow = MagicalOrbit.StatScale(owner);
			double maxNow = LifeMaxFor(BaseLifetimeTicks, scaleNow);
			if (lifetimeMax > 0d && Math.Abs(scaleNow - lastScale) >= 0.002f) {
				lifetimeAge *= maxNow / lifetimeMax;
				lastScale = scaleNow;
			}
			lifetimeMax = maxNow;
			lifetimeAge += 1d;
			// Keep timeLeft comfortably above the age-driven kill so vanilla can
			// never expire the shell a tick early on one side but not the other.
			Projectile.timeLeft = (int)Math.Clamp(lifetimeMax - lifetimeAge + 2d, 1d, MagicalOrbit.MaxSafeTimeLeft);
		}

		public override void AI()
		{
			Player owner = OwnerPlayer();
			if (owner == null || !owner.active || owner.dead) {
				Projectile.Kill();
				return;
			}
			SourceProjType = SourceProjType == 0 ? (int)Projectile.ai[1] : SourceProjType;
			ResolveSourceProjType();
			deployTicks++;

			if (BaseLifetimeTicks <= 0)
				BaseLifetimeTicks = MagicalOrbit.BaseLifetimeTicks(SourceProjType);
			if (lastScale <= 0f)
				lastScale = SpawnScale > 0f ? SpawnScale : MagicalOrbit.StatScale(owner);
			StepLifetime(owner);
			if (lifetimeAge >= lifetimeMax) {
				// Timed life is up: expiring guards implode in OnKill.
				Projectile.Kill();
				return;
			}

			// ORBITERS EXPIRE WITH THE TECH - IMMEDIATELY.
			//
			// This used to set timeLeft = 60 every tick while the tech was off.
			// Vanilla decrements timeLeft before AI runs, so the clamp put the
			// full 60 ticks back on every frame: the "one second of grace"
			// refreshed itself forever and a shell whose tech was OFF never died.
			// That is why orbiters kept circling after the tech was switched off.
			//
			// Now any tech other than Magical (including None) kills the shell on
			// the next tick on every side. A swapped tech can never leave a guard
			// behind, and re-enabling the tech cannot resurrect a dead one.
			if (!YoyoTechCommon.TechActiveFor(owner, out YoyoTechType activeTech)
				|| activeTech != YoyoTechType.Magical) {
				Projectile.Kill();
				return;
			}

			// Ring pulls slightly tighter while an ability pools at the centre.
			bool converging = MagicalOrbit.ControllerAlive(owner);
			float radiusTarget = converging ? MagicalOrbit.OrbitRadiusConverged : MagicalOrbit.OrbitRadius;
			orbitRadius = deployTicks <= 2 ? radiusTarget : MathHelper.Lerp(orbitRadius, radiusTarget, 0.10f);

			Projectile.rotation += 0.9f;
			MagicalOrbit.RankOf(Projectile, out int rank, out int total);
			float wantAng = MagicalOrbit.OrbitPhase(owner, rank, total);

			if (Projectile.ai[2] == 0f) {
				// Fly out to the formation slot, then lock on.
				Vector2 slot = owner.Center + wantAng.ToRotationVector2() * orbitRadius;
				Vector2 toSlot = slot - Projectile.Center;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, toSlot.SafeNormalize(Vector2.Zero) * 20f, 0.25f);
				if (toSlot.Length() < 22f || deployTicks > 45) {
					Projectile.ai[2] = 1f;
					angle = (Projectile.Center - owner.Center).ToRotation();
					angleInit = true;
					Projectile.netUpdate = true;
					SpawnLockInFx();
				}
			}
			else {
				// Locked orbit: chase the shared formation angle. The chase is the
				// auto-reform - when the count changes every slot shifts a little
				// and the ring smooths into its new even spacing.
				if (!angleInit) {
					angle = (Projectile.Center - owner.Center).ToRotation();
					angleInit = true;
				}
				angle = MagicalOrbit.LerpAngle(angle, wantAng, 0.15f);
				Vector2 oldCenter = Projectile.Center;
				Projectile.Center = owner.Center + angle.ToRotationVector2() * orbitRadius;
				Projectile.velocity = (Projectile.Center - oldCenter) * 0.6f;
			}

			// The moment a convergence core forms, every guard gives a small tell.
			if (converging && !convergenceFlare) {
				convergenceFlare = true;
				if (!Main.dedServ)
					YoyoParticles.SpawnRing(Projectile.Center, 16f, 4f, 12, MagicalOrbit.AccentOf(SourceProjType), 2f);
			}
			else if (!converging && convergenceFlare) {
				convergenceFlare = false;
			}

			Color accent = MagicalOrbit.AccentOf(SourceProjType);
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.35f + (converging ? 0.25f : 0f)));

			if (MiniYoyoFx.EffectsEnabled() && Main.rand.NextBool(4))
				YoyoParticles.SpawnSpark(Projectile.Center,
					-Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.4f, 0.4f), accent, 0.75f, 20, 0.94f);
			else if (Main.rand.NextBool(6)) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Enchanted_Pink, 0f, -0.3f, 120, default, 0.8f);
				d.noGravity = true;
			}

			// Blades zap: aimed bolts while circling.
			GenericEchoShot(30, 120);
			EchoPassive(deployTicks);
			TechStringFx(owner);
		}

		private void SpawnLockInFx()
		{
			if (Main.dedServ)
				return;
			Color accent = MagicalOrbit.AccentOf(SourceProjType);
			YoyoParticles.SpawnRing(Projectile.Center, 6f, 26f, 14, accent, 2f);
			YoyoParticles.SpawnBurst(Projectile.Center, 4, 2.6f, accent, 0.8f, 16);
			SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.25f, Pitch = 0.3f }, Projectile.Center);
		}

		public override void OnKill(int timeLeft)
		{
			Color accent = MagicalOrbit.AccentOf(SourceProjType);
			if (!Main.dedServ) {
				// Expiry implosion: the guard collapses inward, no damage, pure read.
				YoyoParticles.SpawnRing(Projectile.Center, 34f, 4f, 16, accent, 2.6f);
				YoyoParticles.SpawnBloom(Projectile.Center, 4f, 26f, 18, accent, 0.5f);
			}
			for (int i = 0; i < 8; i++) {
				Vector2 dir = new Vector2(40f, 0f).RotatedBy(i / 8f * MathHelper.TwoPi);
				Dust d = Dust.NewDustDirect(Projectile.Center - new Vector2(4, 4), 8, 8,
					DustID.MagicMirror, -dir.X * 0.08f, -dir.Y * 0.08f, 120, default, 0.9f);
				d.noGravity = true;
			}
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			ApplySpellDebuff(target);
			EchoHit(target);
			for (int i = 0; i < 6; i++) {
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
					DustID.Enchanted_Pink, 0f, -1f, 120, default, 0.9f);
				d.noGravity = true;
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			// A feeder has this orbiter out on loan: hide the real one so the ring
			// thins. It is still orbiting and still hitting; only the draw is off.
			if (FeederAway)
				return false;

			// Expiry blink in the last second so the timed life reads at a glance.
			float fade = 1f;
			if (lifetimeMax - lifetimeAge < 60d) {
				float pulse = 0.5f + 0.5f * (float)Math.Sin(deployTicks * 0.8f);
				fade = MathHelper.Lerp(0.35f, 1f, pulse);
			}

			Color accent = MagicalOrbit.AccentOf(SourceProjType);
			// Same art resolution as the shell itself: the item icon when it
			// exists, a single cropped frame otherwise, size-capped. This is
			// what stops a modded sprite sheet becoming a transparent square.
			bool haveArt = TryGetSourceArt(out Texture2D tex, out Rectangle? frame, out float artScale);
			Vector2 artSize = haveArt
				? (frame.HasValue ? frame.Value.Size() : tex.Size())
				: Vector2.Zero;
			Vector2 origin = artSize * 0.5f;
			if (haveArt && Projectile.oldPos != null) {
				// Motion ghosts off the source sprite: spin + orbit in one read.
				for (int i = 1; i < Projectile.oldPos.Length; i += 2) {
					if (Projectile.oldPos[i] == Vector2.Zero)
						continue;
					float t = 1f - i / (float)Projectile.oldPos.Length;
					Vector2 ghost = Projectile.oldPos[i] + Projectile.Size * 0.5f;
					if (Vector2.DistanceSquared(ghost, Projectile.Center) < 12f)
						continue;
					Main.EntitySpriteDraw(tex, ghost - Main.screenPosition, frame,
						accent * (0.18f * t * fade), Projectile.oldRot[i], origin,
						artScale * (0.88f + 0.12f * t), SpriteEffects.None, 0);
				}
			}

			// Glow BEHIND the body: the old order drew the 40px additive halo on
			// top of a 16px vanilla yoyo sprite and washed it out, which is half
			// of the "vanilla orbiter is invisible" read. The body is also given
			// a small self-lit lift so a dark arena cannot swallow it.
			MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 40f, accent, 0.24f * fade);
			// Guaranteed visible core under the source sprite. Even if a source
			// texture is blank/tiny or a remote copy is missing its art ids, the
			// slot still reads as a live guard instead of an empty ring.
			MiniYoyoFx.DrawSolidEllipse(Projectile.Center, 11f, 11f, Projectile.rotation,
				Color.Lerp(accent, Color.Black, 0.35f) * fade, 0.95f);
			MiniYoyoFx.DrawSolidEllipse(Projectile.Center, 6f, 6f, -Projectile.rotation * 1.3f,
				Color.White * (0.80f * fade), 1f);
			Color bodyLight = Color.Lerp(lightColor, Color.White, 0.42f);
			DrawSourceSprite(bodyLight * fade);
			// While the guard is feeding a convergence core it wears a swept
			// crescent, so each participant reads as actively channelling.
			Player owner = OwnerPlayer();
			if (owner != null && MagicalOrbit.ControllerAlive(owner))
				MiniYoyoFx.DrawSoftArc(Projectile.Center, 25f, deployTicks * 0.05f, deployTicks * 0.05f + 2.6f,
					2f, accent, 0.30f * fade, 10);
			return false;
		}
	}
}
