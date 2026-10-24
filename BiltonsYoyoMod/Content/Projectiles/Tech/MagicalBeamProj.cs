using System;
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
	/// Orbital Convergence release: a sustained, aim-following beam fired from
	/// the charged core. Low damage by design (fraction of the yoyo's damage),
	/// but hits through the target's normal immunity frames using CONTROLLED
	/// local immunity (a per-target cooldown), never an unrestricted per-tick
	/// application. Lives at most 5s and dies with its controller.
	/// </summary>
	public class MagicalBeamProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		// ai[0]: controller identity (stable across clients, unlike whoAmI)

		private const int RefreshClockRate = 8;

		private Projectile controller;
		private MagicalAbilityProfile profile;
		private int srcType;
		private Color accent = new(150, 180, 255);
		private Vector2 aim = Vector2.UnitX;
		private float liveLength;
		private float grow;
		private float displayPower;
		private int ageTicks;
		private int damageClock;
		private int refreshClock;
		private int fxClock;
		private bool startFxDone;

		// Read-only facts for end-puddle anchors (Prism Gate, Circuit Rune).
		// The anchor follows the live tip instead of baking a position at spawn.
		public Vector2 AimDirection => aim;
		public float LiveLength => liveLength;
		public Vector2 EndPoint => Projectile.Center + aim * liveLength;
		public float DisplayPower => displayPower;
		public int SourceType => srcType;
		public bool HasEndPuddle => profile != null && profile.BeamHasEndPuddle;

		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = MagicalOrbit.BeamMaxLifeTicks; // 5s hard cap
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = MagicalOrbit.BeamHitCooldownDefault;
		}

		private Player FindOwner()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player owner = Main.player[Projectile.owner];
			return owner != null && owner.active && !owner.dead ? owner : null;
		}

		private bool UpdateController()
		{
			int ctrlType = ModContent.ProjectileType<MagicalConvergenceProj>();
			int wantIdentity = (int)Projectile.ai[0];
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.type == ctrlType && p.owner == Projectile.owner && p.identity == wantIdentity) {
					controller = p;
					return true;
				}
			}
			return false;
		}

		public override void AI()
		{
			if (Projectile.ai == null || Projectile.ai.Length == 0) {
				Projectile.Kill();
				return;
			}
			Player owner = FindOwner();
			if (owner == null || !UpdateController()) {
				Projectile.Kill();
				return;
			}

			ageTicks++;
			Projectile.Center = controller.Center;
			Projectile.rotation = aim.ToRotation();

			// Aim: owner drives, everyone follows the synced velocity.
			if (Projectile.owner == Main.myPlayer) {
				aim = MagicalOrbit.AimDirection(owner, Projectile.Center, aim);
				Projectile.velocity = aim;
				if (++refreshClock >= RefreshClockRate) {
					refreshClock = 0;
					Projectile.netUpdate = true;
				}
			}
			else {
				aim = Projectile.velocity.SafeNormalize(aim);
			}

			// Ability identity + live power (shrinks as orbit yoyos die).
			srcType = MagicalOrbit.PrimaryOrbitSourceType(owner, srcType > 0 ? srcType : 0);
			profile = MagicalAbilityRegistry.Get(srcType);
			accent = profile.AccentColor(srcType);
			int orbitCount = MagicalOrbit.CountOrbitYoyos(owner);
			MagicalOrbit.CapStats(owner, out _, out int cap);
			float powerTarget = MathHelper.Clamp(orbitCount / (float)Math.Max(1, cap), 0f, 1f);
			displayPower = ageTicks <= 2 ? powerTarget : MathHelper.Lerp(displayPower, powerTarget, 0.08f);

			// Damage refresh: deterministic on every side from synced inputs.
			if (++damageClock >= 12) {
				damageClock = 0;
				float factor = MathHelper.Lerp(0.6f, 1f, displayPower);
				Projectile.damage = Math.Max(1, (int)(MagicalOrbit.AverageOrbitDamage(owner) * profile.BeamDamageFraction * factor));
				Projectile.CritChance = (int)owner.GetTotalCritChance(DamageClass.MeleeNoSpeed);
			}
			Projectile.localNPCHitCooldown = profile.BeamHitCooldown;

			// Charge-in / collapse shape.
			grow = MathHelper.Clamp(ageTicks / 9f, 0f, 1f);
			if (Projectile.timeLeft < 10)
				grow = MathHelper.Min(grow, Projectile.timeLeft / 10f);

			// Clamp reach at tiles: the visual and the hit line agree.
			float reach = profile.BeamRange * MathHelper.Max(grow, 0.15f);
			liveLength = RaycastLength(Projectile.Center, aim, reach);

			if (!startFxDone) {
				startFxDone = true;
				if (!Main.dedServ)
					SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = 0.1f }, Projectile.Center);
			}
			BeamAmbience();
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.5f + 0.5f * displayPower));
			Vector2 tip = Projectile.Center + aim * liveLength;
			Lighting.AddLight(tip, accent.ToVector3() * 0.5f);

			// B-P beam: hand the live tip to the profile so it can keep an end
			// puddle anchored there. B-N (Lord's Deathray) does nothing here.
			if (profile != null && profile.BeamHasEndPuddle) {
				MagicalAbilityContext endCtx = new(owner, controller, Projectile.Center, aim,
					orbitCount, cap, srcType, 1f, displayPower,
					MagicalTier.AbilityTier01(srcType), accent);
				profile.OnBeamEndTick(Projectile, endCtx, tip, liveLength);
			}
		}

		/// <summary>Stepped line probe so the beam stops on solid tiles.</summary>
		private static float RaycastLength(Vector2 start, Vector2 dir, float maxLen)
		{
			const float step = 20f;
			for (float d = step; d <= maxLen; d += step) {
				Vector2 p = start + dir * d;
				if (Collision.SolidCollision(p - new Vector2(4f, 4f), 8, 8))
					return MathHelper.Max(24f, d - 8f);
			}
			return maxLen;
		}

		private void BeamAmbience()
		{
			if (Main.dedServ || !MiniYoyoFx.EffectsEnabled())
				return;
			fxClock++;
			Vector2 tip = Projectile.Center + aim * liveLength;
			Vector2 side = new(-aim.Y, aim.X);

			// Motes crawling origin -> tip (flow read).
			if (fxClock % 3 == 0) {
				Vector2 pos = Projectile.Center + aim * Main.rand.NextFloat(0f, liveLength);
				YoyoParticles.SpawnSpark(pos, aim * Main.rand.NextFloat(1.5f, 3f) + side * Main.rand.NextFloat(-0.8f, 0.8f),
					accent, 0.7f, 16, 0.95f);
			}
			// End impact bloom, stronger on solid tiles (wall splash).
			if (fxClock % 10 == 0) {
				float endDepth = liveLength / MathHelper.Max(1f, profile?.BeamRange ?? 900f);
				float scale = 0.5f + 0.7f * displayPower;
				YoyoParticles.SpawnBloom(tip, 4f, (18f + 14f * displayPower) * (endDepth < 0.98f ? 1.35f : 1f),
					16, accent, 0.42f * scale);
				if (endDepth < 0.98f) {
					for (int i = 0; i < 3; i++)
						YoyoParticles.SpawnSpark(tip, side * Main.rand.NextFloat(-3f, 3f) - aim * Main.rand.NextFloat(0.5f, 2.5f),
							accent, 0.8f, 14, 0.92f, 0.12f);
				}
			}
			// One soft dust thread for volume where pooled FX are light.
			if (YoyoParticles.ActiveCount < 8 && Main.rand.NextBool(4)) {
				Dust d = Dust.NewDustPerfect(Projectile.Center + aim * Main.rand.NextFloat(0f, liveLength),
					DustID.MagicMirror, -aim * 1.5f, 120, default, 0.75f);
				d.noGravity = true;
			}
		}

		// ---- Combat: line hitbox + controlled local immunity ----

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (grow < 0.35f)
				return false;
			float point = 0f;
			float thickness = profile?.BeamThickness ?? 18f;
			return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
				Projectile.Center, Projectile.Center + aim * liveLength, thickness, ref point);
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			Player owner = FindOwner();
			if (owner == null)
				return;
			MagicalAbilityContext ctx = new(owner, controller, Projectile.Center, aim,
				0, 3, srcType, 1f, displayPower, accent);
			profile?.OnBeamHit(target, Projectile, ctx);
		}

		public override void OnKill(int timeLeft)
		{
			Player owner = FindOwner();
			if (profile == null)
				profile = MagicalAbilityRegistry.Get(srcType);
			Vector2 tip = Projectile.Center + aim * liveLength;
			if (profile != null && profile.BeamHasEndPuddle && owner != null) {
				MagicalAbilityContext endCtx = new(owner, controller, Projectile.Center, aim,
					0, 3, srcType, 1f, displayPower,
					MagicalTier.AbilityTier01(srcType), accent);
				profile.OnBeamEndKill(Projectile, endCtx, tip);
			}

			if (Main.dedServ)
				return;
			YoyoParticles.SpawnRing(tip, 6f, 30f + 22f * displayPower, 16, accent, 2.4f);
			YoyoParticles.SpawnBloom(tip, 4f, 26f, 16, accent, 0.35f);
			SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.4f, Pitch = -0.5f }, Projectile.Center);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (profile == null)
				profile = MagicalAbilityRegistry.Get(srcType);
			Vector2 end = Projectile.Center + aim * liveLength;
			profile.DrawBeam(Projectile, Projectile.Center, end, accent, displayPower, grow);
			if (profile.BeamHasEndPuddle)
				profile.DrawBeamEnd(Projectile, end, accent, displayPower, grow);
			return false;
		}
	}
}
