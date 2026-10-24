using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Tech;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// Shared helpers for the per-yoyo ability releases. Everything here is
	/// driven by the Magical Tech controller: spawns happen owner-side, the
	/// visuals self-guard on dedicated servers, and NPC movement effects never
	/// run on a remote client (NPC motion is server authority).
	///
	/// Visual language: every release is drawn from <see cref="MagicalFx"/> -
	/// soft shape-bank art (crescents, runes, wisps, embers, bolts) plus pooled
	/// particles and glows. No flat lines, no bare stroked circles.
	/// </summary>
	public static class MagicalReleaseCommon
	{
		/// <summary>Spawn a release projectile (owner side only) and flag it for sync.</summary>
		public static int Spawn(Player owner, Vector2 pos, Vector2 vel, int type, int damage,
			float ai0 = 0f, float ai1 = 0f, float ai2 = 0f)
		{
			if (owner == null || owner.whoAmI != Main.myPlayer)
				return -1;
			int idx = Projectile.NewProjectile(owner.GetSource_Misc("MagicalTechRelease"), pos, vel, type,
				Math.Max(1, damage), 0f, owner.whoAmI, ai0, ai1, ai2);
			if (idx >= 0 && idx < Main.maxProjectiles)
				Main.projectile[idx].netUpdate = true;
			return idx;
		}

		/// <summary>
		/// Spawn a CHILD projectile (splits, returns, respawns) from a hit or
		/// timer event. Only the server may create these: in multiplayer the
		/// owner client never resolves NPC hits, so letting it spawn here would
		/// either duplicate the child or miss it entirely.
		/// </summary>
		public static int SpawnChild(Player owner, Vector2 pos, Vector2 vel, int type, int damage,
			float ai0 = 0f, float ai1 = 0f, float ai2 = 0f)
		{
			if (owner == null || Main.netMode == NetmodeID.MultiplayerClient)
				return -1;
			int idx = Projectile.NewProjectile(owner.GetSource_Misc("MagicalTechRelease"), pos, vel, type,
				Math.Max(1, damage), 0f, owner.whoAmI, ai0, ai1, ai2);
			if (idx >= 0 && idx < Main.maxProjectiles)
				Main.projectile[idx].netUpdate = true;
			return idx;
		}

		/// <summary>Server/SP authority for pushing NPCs around.</summary>
		public static bool CanMoveNpcs => Main.netMode != NetmodeID.MultiplayerClient;

		/// <summary>
		/// Can a tech effect land on this enemy at all?
		///
		/// Target Dummies are spelled out here ON PURPOSE. They are the game's
		/// standard test target - the thing a player points an ability at when
		/// they want to know whether it works - and they are not friendly and do
		/// not count as critters, so the only filter that can ever exclude them
		/// is a blunt dontTakeDamage / CanBeChasedBy check. An ability that
		/// silently refuses to touch a dummy looks completely dead while every
		/// number behind it is correct, which is indistinguishable from a broken
		/// ability and produces the "it does no damage, unless it just does not
		/// shoot dummies?" question. So the dummy case is explicit and lives in
		/// exactly one place, and every hit path in the tech tree asks here.
		/// </summary>
		public static bool Hittable(NPC npc)
		{
			if (npc == null || !npc.active || npc.life <= 0 || npc.friendly)
				return false;
			if (npc.type == NPCID.TargetDummy)
				return true;
			return !npc.dontTakeDamage && npc.CanBeChasedBy();
		}

		/// <summary>Hittable, but never wildlife: aimed shots leave critters alone.</summary>
		public static bool Targetable(NPC npc) => Hittable(npc) && !npc.CountsAsACritter;

		/// <summary>Warm pop colour used when the caller has no accent to hand.</summary>
		private static readonly Color HitPopColor = new(255, 236, 190);

		/// <summary>
		/// Land one tech hit on one enemy: applies the strike and pops a visible
		/// hit reaction (plus a heavier one on a kill) so a landed strike always
		/// reads as damage.
		///
		/// NOTE: there is deliberately NO shared global hit lock here. An earlier
		/// version gated EVERY tech hit on one 10-tick per-enemy lock, which
		/// silently starved abilities that are supposed to tick (wells, brands,
		/// tethers) and made them look like they did nothing. Each executor owns
		/// its own cadence; chain lightning re-hitting one enemy is fixed inside
		/// the bolt itself (see MagicalLightningProj), not here.
		/// </summary>
		public static bool Strike(Player owner, NPC npc, int damage, float knockback,
			Color? accent = null, Vector2? fromAt = null, bool healOwnerOnKill = false,
			bool pierceArmor = false, bool hitFx = true)
		{
			if (!CanMoveNpcs || owner == null || !Hittable(npc))
				return false;

			Vector2 src = fromAt ?? owner.Center;
			Vector2 push = npc.Center - src;
			int dir = push.X >= 0f ? 1 : -1;
			int before = npc.life;
			// ARMOUR COMPENSATION. SimpleStrikeNPC subtracts defense/2 from EVERY
			// hit, so a small TICKING hit - a well pulse, a brand bite, a leech
			// drain - gets silently rounded down to 1 damage against anything past
			// early Hardmode. The visual then ticks for three seconds and the
			// health bar does not move, which is exactly what "it draws a trail to
			// the cursor but does no damage" is. Tick executors ask for pierceArmor
			// so the number the player was promised is the number that lands.
			int raw = Math.Max(1, damage);
			if (pierceArmor) {
				raw += (int)Math.Ceiling(npc.defense * 0.5f);
				raw = (int)Math.Ceiling(raw / Math.Max(0.05f, npc.takenDamageMultiplier));
			}
			// Direct SimpleStrikeNPC bypasses GlobalProjectile.ModifyHitNPC, so
			// the dummy's knockback must be stopped here as well. The global
			// DummyMotionGuard is the backstop for every other direct strike.
			npc.SimpleStrikeNPC(raw, dir, false, DummySafety.SafeKnockback(npc, knockback));
			bool killed = npc.life <= 0 && before > 0;

			if (!Main.dedServ && hitFx) {
				Color pop = accent ?? HitPopColor;
				// IMPACT TIERS. Calamity's Particles folder does exactly this with
				// DetailedExplosion / PlasmaExplosion / FlameExplosion /
				// SmokeExplosion: separate reaction art per weight class. Without
				// it a 200-damage tick and a 3-damage tick produce an identical
				// pop, so the player cannot tell whether the ability is working.
				// 60 damage is the 1.0 reference.
				float weight = MathHelper.Clamp(damage / 60f, 0f, 2.5f);
				float tier = 0.55f + 0.42f * weight;
				// A kill gets its own, heavier reaction: a hotter pop plus a shock
				// ring and shards, so finishing something reads instantly and the
				// last hit of a fight never looks like every other hit.
				if (killed)
					tier += 0.40f;
				MagicalFx.Impact(npc.Center, pop, MathHelper.Clamp(tier, 0.45f, 1.75f), owner.whoAmI,
					push.SafeNormalize(Vector2.UnitX * owner.direction).ToRotation());
				// A heavy bite gets a ring, so the big ticks are unmistakable.
				if (!killed && weight >= 1.2f) {
					MagicalFx.QueueRing(npc.Center, MathHelper.Max(18f, npc.width * 0.5f),
						Color.Lerp(pop, Color.White, 0.35f), 0.34f, 18, 1.0f);
				}
				if (killed) {
					MagicalFx.QueueRing(npc.Center, MathHelper.Max(20f, npc.width * 0.7f),
						Color.Lerp(pop, Color.White, 0.4f), 0.5f, 22, 1.1f);
					MagicalFx.Shards(npc.Center, Color.Lerp(pop, Color.White, 0.25f), 6, 4.5f, 22, 0.6f);
				}
			}

			if (healOwnerOnKill && killed && owner.active && !owner.dead) {
				if (owner.statLife < owner.statLifeMax2)
					owner.statLife = Math.Min(owner.statLifeMax2, owner.statLife + 2);
			}
			return true;
		}

		/// <summary>Damage every hostile NPC whose hitbox crosses a segment (server/SP only).</summary>
		public static void LineHit(Player owner, Vector2 a, Vector2 b, float halfWidth, int damage,
			float knockback = 2.4f, Color? accent = null)
		{
			if (!CanMoveNpcs || owner == null)
				return;
			Vector2 seg = b - a;
			float lenSq = seg.LengthSquared();
			if (lenSq < 1f)
				return;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc))
					continue;
				float t = MathHelper.Clamp(Vector2.Dot(npc.Center - a, seg) / lenSq, 0f, 1f);
				Vector2 closest = a + seg * t;
				float reach = halfWidth + Math.Max(npc.width, npc.height) * 0.5f;
				if (Vector2.DistanceSquared(npc.Center, closest) > reach * reach)
					continue;
				Strike(owner, npc, damage, knockback, accent, closest);
			}
		}

		/// <summary>Accent of a release projectile: whatever its owning profile wants.</summary>
		public static Color AbilityAccent(Projectile projectile)
		{
			int srcType = (int)projectile.ai[2];
			MagicalAbilityProfile profile = MagicalAbilityRegistry.Get(srcType);
			return profile.AccentColor(srcType);
		}

		/// <summary>Damage every hostile NPC inside a radius (server/SP only).</summary>
		public static void RadialHit(Player owner, Vector2 centre, float radius, int damage,
			float knockback = 2f, bool healOwnerOnKill = false, Color? accent = null,
			bool pierceArmor = false, float minRadius = 0f)
		{
			if (!CanMoveNpcs || owner == null)
				return;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc))
					continue;
				float d2 = Vector2.DistanceSquared(npc.Center, centre);
				if (d2 > radius * radius)
					continue;
				// Inner edge of an expanding ring. An expanding shockwave has to
				// damage the BAND it is currently sweeping, not the whole disc,
				// or the hit lands before the wave does and the wave itself is
				// harmless. See ArcHit's minReach note.
				if (minRadius > 0f) {
					float inner = Math.Max(0f, minRadius - npc.width * 0.5f);
					if (d2 < inner * inner)
						continue;
				}
				Strike(owner, npc, damage, knockback, accent, centre, healOwnerOnKill, pierceArmor);
			}
		}

		/// <summary>Damage every hostile NPC inside a wedge facing `aim` (server/SP only).</summary>
		/// <param name="minReach">
		/// Inner edge of the damage band. A wedge that grows over time MUST call
		/// this on every tick with minReach = wherever the front was on the
		/// previous tick, so each victim is hit exactly once as the front sweeps
		/// over it and the damage always tracks what the player can see. Calling
		/// it a single time with the FINAL reach - which is what ConeTick used to
		/// do - damages a zero-radius disc on frame one and then lets a
		/// full-size, fully visible cone expand across the screen doing nothing
		/// at all, which is the worst instance of "the ability draws but never
		/// does damage".
		/// </param>
		public static void ArcHit(Player owner, Vector2 centre, Vector2 aim, float reach, float halfAngle,
			int damage, float knockback = 3f, bool healOwnerOnKill = false, Color? accent = null,
			float minReach = 0f)
		{
			if (!CanMoveNpcs || owner == null || reach <= 1f || halfAngle <= 0f)
				return;
			float aimRot = aim.LengthSquared() > 0.001f ? aim.ToRotation() : 0f;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc))
					continue;
				Vector2 to = npc.Center - centre;
				float dist = to.Length();
				float pad = npc.width * 0.5f;
				if (dist > reach + pad)
					continue;
				if (dist < minReach - pad)
					continue;
				if (dist > 1f && Math.Abs(MathHelper.WrapAngle(to.ToRotation() - aimRot)) > halfAngle)
					continue;
				Strike(owner, npc, damage, knockback, accent, centre, healOwnerOnKill);
			}
		}

		/// <summary>Pull/push every hostile NPC inside a radius toward (or from) a point.</summary>
		public static void RadialGrab(Vector2 centre, float radius, float accel, bool towardCentre)
		{
			if (!CanMoveNpcs)
				return;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.boss)
					continue;
				if (!npc.CanBeChasedBy())
					continue;
				// A training dummy is damageable but not movable: its hitbox
				// would slide out from under the fixed sprite.
				if (DummySafety.Pin(npc))
					continue;
				float dist = Vector2.Distance(npc.Center, centre);
				if (dist > radius || dist < 1f)
					continue;
				Vector2 dir = (npc.Center - centre) / dist;
				if (towardCentre)
					dir = -dir;
				float falloff = 1f - dist / Math.Max(1f, radius);
				npc.velocity += dir * accel * (0.35f + 0.65f * falloff);
				npc.netUpdate = true;
			}
		}

		/// <summary>Recolour a trail/dust burst in the ability's accent.</summary>
		public static void Puff(Vector2 pos, Color color, int count = 6, float speed = 2.4f, float scale = 0.85f)
		{
			if (Main.dedServ)
				return;
			YoyoParticles.SpawnBurst(pos, count, speed, color, scale, 16);
		}

		/// <summary>
		/// Inward comet burst: pooled sparks dragged in from a rim, so the funnel
		/// is made of travelling light instead of stamped rods. Spawn side only.
		/// </summary>
		public static void InfallWisps(Vector2 centre, float radius, Color color, int count, float pull = 1f)
		{
			if (Main.dedServ)
				return;
			for (int i = 0; i < count; i++) {
				float a = Main.rand.NextFloat(MathHelper.TwoPi);
				Vector2 p = centre + a.ToRotationVector2() * radius * Main.rand.NextFloat(0.55f, 1.05f);
				Vector2 v = (centre - p) * (0.05f * pull)
					+ new Vector2(-(float)Math.Sin(a), (float)Math.Cos(a)) * Main.rand.NextFloat(0.5f, 1.4f);
				YoyoParticles.SpawnSpark(p, v, color, 0.9f, Main.rand.Next(18, 28), 0.97f);
			}
		}

		public static Vector2 SafeAim(Player owner, Vector2 from, Vector2 fallback)
		{
			if (owner != null && owner.whoAmI == Main.myPlayer) {
				Vector2 to = Main.MouseWorld - from;
				if (to.LengthSquared() > 20f * 20f)
					return to.SafeNormalize(fallback);
			}
			return fallback.LengthSquared() > 0.01f ? fallback.SafeNormalize(Vector2.UnitX) : Vector2.UnitX;
		}
	}

	/// <summary>
	/// Expanding radial / directional bursts. One projectile covers novas,
	/// implosions, cones, ground geysers, sky lances, chain lattices and sweep
	/// slashes; the mode decides the shape, the coupling to orbit power arrives
	/// through ai1 (0..1).
	/// ai[0] = mode, ai[1] = power01, ai[2] = source identity (accent lookup).
	/// Radius (px) is derived per mode from power inside AI.
	/// </summary>
	public class MagicalBurstProj : ModProjectile
	{
		public const float ModeNova = 0f;       // expanding ring, hits everything it sweeps
		public const float ModeImplosion = 1f;  // drags inward, then detonates
		public const float ModeBloom = 2f;      // soft ring, low damage, big visual
		public const float ModeCone = 3f;       // wedge in the aim direction
		public const float ModeGeyser = 4f;     // column of eruptions walking toward the cursor
		public const float ModeLance = 5f;      // sky pillar: warn marker, then strike
		public const float ModeChain = 6f;      // lattice that hops enemy to enemy
		public const float ModeSlash = 7f;      // wide crescent sweep in the aim direction

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float mode;
		private float power;
		private float maxRadius;
		private float progress;
		private bool struck;
		private int age;
		private Vector2 anchor;
		private Color accent = new(150, 180, 255);
		private Vector2 aimDir = Vector2.UnitX;
		private List<Vector2> chainNodes;

		public override void SetDefaults()
		{
			Projectile.width = 8;
			Projectile.height = 8;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 24;
			Projectile.timeLeft = 90;
		}

		public override void AI()
		{
			Player owner = OwnerOf();
			mode = Projectile.ai[0];
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			if (age++ == 0) {
				// Every burst detonates where it was cast; the aim only sets the
				// facing for the cone / geyser / lance / slash marks.
				anchor = Projectile.Center;
				aimDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
				Projectile.velocity = Vector2.Zero;
			}
			Projectile.Center = anchor;

			// Radius profile per mode (shared by the cast flourish and the tick).
			switch (mode) {
				case ModeLance:
					maxRadius = 78f + 90f * power;
					break;
				case ModeCone:
					maxRadius = 210f + 260f * power;
					break;
				case ModeGeyser:
					maxRadius = 74f + 60f * power;
					break;
				case ModeChain:
					maxRadius = 360f;
					break;
				case ModeImplosion:
					maxRadius = 190f + 220f * power;
					break;
				case ModeSlash:
					maxRadius = 200f + 240f * power;
					break;
				default:
					maxRadius = (mode == ModeBloom ? 150f : 175f) + 215f * power;
					break;
			}
			if (age == 1)
				CastFlourish();

			switch (mode) {
				case ModeLance:
					LanceTick();
					break;
				case ModeCone:
					ConeTick(owner);
					break;
				case ModeGeyser:
					GeyserTick();
					break;
				case ModeChain:
					ChainTick();
					break;
				case ModeImplosion:
					ImplosionTick();
					break;
				case ModeSlash:
					SlashTick(owner);
					break;
				default:
					NovaTick();
					break;
			}
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		/// <summary>
		/// Cast-side flourish, spawned once when the release opens: the visual
		/// "tell" that this ability just went off. Purely pooled art, so it is
		/// safe to fire from AI on any side.
		/// </summary>
		private void CastFlourish()
		{
			if (Main.dedServ)
				return;
			float aimRot = aimDir.ToRotation();
			switch (mode) {
				case ModeCone:
					MagicalFx.CastFlash(anchor, accent, 0.9f);
					MagicalFx.Fan(ShapeId.SmearCrescent, anchor, accent, 14, 5.2f, 26, 0.38f, aimRot, 1.1f);
					MagicalFx.Shards(anchor, accent, 7, 4.2f, 24, 0.5f, aimRot, 1.0f);
					MagicalFx.Scorch(anchor, accent, 0.8f);
					break;
				case ModeGeyser:
					// The old tell was six ember puffs and a scorch decal for a
					// 350px molten eruption, which is why the ability felt like it
					// did nothing even though it was hitting the whole line. Give
					// the cast weight: a vent flash at the mouth, a rising plume of
					// embers at every vent, cracked ground all the way along, and a
					// screen kick so the eruption lands physically.
					MagicalFx.CastFlash(anchor, accent, 1.15f);
					MagicalFx.Scorch(anchor, accent, 1.05f, crack: true);
					for (int i = 0; i < 6; i++) {
						Vector2 at = anchor + aimDir * (i * 70f);
						MagicalFx.Embers(at, accent, 7, 3.4f, 30, 0.75f, -MathHelper.PiOver2, 1.15f);
						MagicalFx.Embers(at, Color.Lerp(accent, Color.White, 0.5f), 3, 5.2f, 20, 0.5f,
							-MathHelper.PiOver2, 0.75f);
						MagicalFx.Scorch(at, accent, 0.8f, crack: i % 2 == 0);
						if (i % 2 == 0)
							MagicalFx.Shards(at - new Vector2(0f, 6f), accent, 3, 3.6f, 22, 0.45f,
								-MathHelper.PiOver2, 1.5f);
					}
					MagicalFx.ScreenPulse(anchor, 0.30f, accent);
					if (Projectile.owner == Main.myPlayer)
						MiniYoyoFx.Shake(4, 12);
					break;
				case ModeLance:
					MagicalReleaseCommon.InfallWisps(anchor, maxRadius * 1.6f, accent, 10, 1.2f);
					MagicalFx.Scorch(anchor, accent, 1.1f, crack: true);
					break;
				case ModeChain:
					MagicalFx.CastFlash(anchor, accent, 0.8f);
					break;
				case ModeSlash:
					MagicalFx.CastFlash(anchor, accent, 0.9f);
					MagicalFx.Shards(anchor, accent, 9, 3.8f, 22, 0.55f, aimRot, 1.5f);
					if (Projectile.owner == Main.myPlayer)
						MiniYoyoFx.SpawnSlash(anchor + aimDir * 34f, aimDir, accent, 1.5f, Projectile.owner);
					break;
				case ModeImplosion:
					MagicalReleaseCommon.InfallWisps(anchor, maxRadius * 0.9f, accent, 12, 1f);
					MagicalFx.Scorch(anchor, accent, 1f);
					break;
				default:
					MagicalFx.CastFlash(anchor, accent, 1f);
					MagicalFx.Scorch(anchor, accent, 1f, crack: mode == ModeNova);
					break;
			}
		}

		private void NovaTick()
		{
			float before = progress;
			progress = MathHelper.Clamp(progress + 0.045f + 0.02f * power, 0f, 1f);
			Lighting.AddLight(anchor, accent.ToVector3() * (0.8f * (1f - progress)));
			// The sweep used to fire ONCE, on the first live frame, at the FULL
			// maxRadius - so the nova "detonated" at its final size while the ring
			// was still a 10px dot, and the entire expanding ring that follows
			// carried no damage at all. The ring now damages only the band it is
			// currently sweeping, using the exact curve DrawNova renders, so what
			// the player sees the wave pass over is what it burns. Every victim is
			// still hit exactly once.
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				float r0 = MathHelper.Lerp(10f, maxRadius,
					MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, MathHelper.Clamp(before, 0f, 1f)));
				float r1 = MathHelper.Lerp(10f, maxRadius,
					MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, MathHelper.Clamp(progress, 0f, 1f)));
				MagicalReleaseCommon.RadialHit(OwnerOf(), anchor, MathHelper.Max(r1, 12f), Projectile.damage,
					3.4f, minRadius: r0);
			}
			if (progress >= 1f)
				Projectile.Kill();
		}

		private void ImplosionTick()
		{
			progress += 0.03f;
			if (progress < 0.7f) {
				// Suck phase: visibly drags before it closes.
				MagicalReleaseCommon.RadialGrab(anchor, maxRadius, 0.42f, true);
				return;
			}
			if (!struck) {
				struck = true;
				MagicalReleaseCommon.RadialHit(OwnerOf(), anchor, maxRadius * 0.85f, Projectile.damage, 5f);
				MagicalReleaseCommon.RadialGrab(anchor, maxRadius, 3.4f, false);
				if (!Main.dedServ) {
					MagicalFx.Fan(ShapeId.SplatSoft, anchor, Color.White, 5, 5.6f, 22, 0.7f);
					MagicalFx.Fan(ShapeId.SmearCrescent, anchor, accent, 12, 6.4f, 26, 0.4f);
					MagicalFx.Scorch(anchor, accent, 1.2f, crack: true);
					MiniYoyoFx.FlashFX(anchor, Color.White, 1.2f);
					MiniYoyoFx.Shake(3, 9, anchor);
					MagicalFx.ScreenPulse(anchor, 0.24f, accent);
				}
			}
			progress += 0.05f;
			if (progress > 1.15f)
				Projectile.Kill();
		}

		private void ConeTick(Player owner)
		{
			float before = progress;
			progress += 0.05f;
			float reachBefore = maxRadius * MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, MathHelper.Clamp(before, 0f, 1f));
			float reach = maxRadius * MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, MathHelper.Clamp(progress, 0f, 1f));
			// Band sweep - each victim is hit once, on the tick the cone front
			// reaches it. This used to be a single ArcHit on frame one, when
			// reach was still a fraction of maxRadius, so the cone then expanded
			// across the whole screen hurting nothing.
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				MagicalReleaseCommon.ArcHit(owner, anchor, aimDir, reach, 0.55f, Projectile.damage, 3f,
					minReach: reachBefore);
			}
			Lighting.AddLight(anchor + aimDir * reach * 0.5f, accent.ToVector3() * 0.9f);
			if (progress >= 1.15f)
				Projectile.Kill();
		}

		private void GeyserTick()
		{
			float before = progress;
			progress += 0.06f;
			// VENT-BY-VENT. This used to set struck = true on the FIRST tick and
			// hit all six vents at once, so the whole 350px line took its damage
			// roughly 17 ticks BEFORE the fire visibly got there. From the
			// player's side the eruption marches out to the cursor and nothing
			// ever reacts to it - which is the "makes a trail to the cursor but
			// does no damage" report. Each vent now damages on the tick the wave
			// front actually REACHES it, so the fire and the hit are the same
			// event. Derived straight from progress, so it stays stateless and
			// deterministic - no new synced field.
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				int from = (int)Math.Floor(before * 6f);
				int to = (int)Math.Floor(MathHelper.Clamp(progress, 0f, 1f) * 6f);
				for (int i = Math.Max(0, from); i <= to && i < 6; i++) {
					Vector2 at = anchor + aimDir * (i * 70f);
					MagicalReleaseCommon.RadialHit(OwnerOf(), at, 62f + 40f * power, Projectile.damage, 3f);
				}
			}
			if (progress > 1f)
				Projectile.Kill();
			Lighting.AddLight(anchor + aimDir * 175f, accent.ToVector3() * 0.8f);
		}

		private void LanceTick()
		{
			// Warn, then strike.
			progress += 0.04f;
			if (progress < 0.55f)
				return;
			if (!struck) {
				struck = true;
				MagicalReleaseCommon.RadialHit(OwnerOf(), anchor, maxRadius, Projectile.damage, 4.5f);
				if (!Main.dedServ) {
					MiniYoyoFx.FlashFX(anchor, Color.White, 1f);
					MagicalFx.Fan(ShapeId.SplatSoft, anchor, Color.White, 4, 4.4f, 20, 0.65f);
					MagicalFx.Fan(ShapeId.BoltZig, anchor, accent, 6, 3.6f, 20, 0.5f);
					MagicalFx.Scorch(anchor, accent, 1.3f, crack: true);
					MiniYoyoFx.Shake(4, 10, anchor);
					MagicalFx.ScreenPulse(anchor, 0.26f, accent);
				}
			}
			Lighting.AddLight(anchor, accent.ToVector3() * 1.1f);
			if (progress >= 1.35f)
				Projectile.Kill();
		}

		/// <summary>
		/// Wide crescent sweep: one arc check in front of the caster, drawn as a
		/// real blade smear (shape-bank crescents) instead of a wedge outline.
		/// </summary>
		private void SlashTick(Player owner)
		{
			float before = progress;
			progress = MathHelper.Clamp(progress + 0.085f, 0f, 1f);
			float reachBefore = maxRadius * MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, before);
			float reach = maxRadius * MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, progress);
			// Band sweep, same reason as ConeTick: the crescent is the hitbox.
			// A single ArcHit on frame one left the entire expanding arc - the
			// part the player actually watches - completely harmless.
			if (Main.netMode != NetmodeID.MultiplayerClient) {
				MagicalReleaseCommon.ArcHit(owner, anchor, aimDir, reach, 0.78f, Projectile.damage, 4.4f,
					healOwnerOnKill: true, minReach: reachBefore);
				if (!Main.dedServ && before <= 0f) {
					MiniYoyoFx.FlashFX(anchor + aimDir * 46f, Color.White, 1f);
					MiniYoyoFx.Shake(3, 8, anchor);
				}
			}
			Lighting.AddLight(anchor + aimDir * reach * 0.6f, accent.ToVector3() * 0.9f);
			if (progress >= 1f)
				Projectile.Kill();
		}

		/// <summary>
		/// Chain lattice: hops from the burst point to the nearest enemy, then to
		/// the nearest one it has not touched yet, up to 6 links. Damage lands on
		/// the build tick (one pass, no repeats) and the drawn arcs persist for
		/// the release window so the path stays legible.
		/// </summary>
		private void ChainTick()
		{
			progress += 0.045f;
			if (chainNodes == null)
				BuildChain();
			if (progress >= 1f) {
				Projectile.Kill();
				return;
			}
			if (chainNodes.Count >= 2)
				Lighting.AddLight(chainNodes[chainNodes.Count - 1], accent.ToVector3() * 0.9f);
		}

		private void BuildChain()
		{
			chainNodes = new List<Vector2> { anchor };
			Vector2 cursor = anchor;
			var hit = new HashSet<int>();
			int links = 3 + (int)(3f * power);
			for (int step = 0; step < links; step++) {
				NPC best = null;
				float bestD = 380f * 380f;
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC npc = Main.npc[i];
					if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.CountsAsACritter
						|| !npc.CanBeChasedBy() || hit.Contains(i))
						continue;
					float d = Vector2.DistanceSquared(npc.Center, cursor);
					if (d < bestD) {
						bestD = d;
						best = npc;
					}
				}
				if (best == null)
					break;
				hit.Add(best.whoAmI);
				chainNodes.Add(best.Center);
				Vector2 from = cursor;
				cursor = best.Center;
				MagicalReleaseCommon.Strike(OwnerOf(), best, Math.Max(1, Projectile.damage * 2 / 3),
					1.5f, accent, from);
			}
		}

		// ---- Drawing (PreDraw only: the sprite batch is live here) ----

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			switch (mode) {
				case ModeLance:
					DrawLance();
					break;
				case ModeCone:
					DrawCone();
					break;
				case ModeGeyser:
					DrawGeyser();
					break;
				case ModeChain:
					DrawChain();
					break;
				case ModeImplosion:
					DrawImplosion();
					break;
				case ModeBloom:
					DrawBloom();
					break;
				case ModeSlash:
					DrawSlash();
					break;
				default:
					DrawNova();
					break;
			}
			return false;
		}

		private void DrawNova()
		{
			float t = MathHelper.Clamp(progress, 0f, 1f);
			float r = MathHelper.Lerp(10f, maxRadius, MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, t));
			float fade = 1f - t;
			MagicalFx.Shockwave(anchor, r, progress, accent, 1f, 3.6f);
			MagicalFx.Splat(anchor, 0f, MathHelper.Min(r * 0.9f, 150f),
				accent * (0.34f * fade));
			MagicalFx.Shape(ShapeId.RingCracked, anchor, Main.GameUpdateCount * -0.02f,
				MathHelper.Min(r * 1.2f, 190f), MathHelper.Min(r * 1.2f, 190f),
				Color.Lerp(accent, Color.White, 0.35f) * (0.18f * fade));
			MagicalFx.Bloom(anchor, MathHelper.Min(r * 1.6f, 260f), accent, 0.10f + 0.14f * fade);
			// Comet halo riding the front edge.
			int n = 8;
			for (int i = 0; i < n; i++) {
				float a = Main.GameUpdateCount * 0.04f + i * MathHelper.TwoPi / n;
				Vector2 p = anchor + a.ToRotationVector2() * r;
				MagicalFx.Wisp(p, a + MathHelper.PiOver2, 24f + 14f * fade, 9f,
					Color.Lerp(accent, Color.White, 0.3f) * (0.34f * fade));
			}
		}

		private void DrawImplosion()
		{
			float t2 = Main.GameUpdateCount;
			if (progress < 0.7f) {
				float c = progress / 0.7f;
				float r = MathHelper.Lerp(maxRadius, 46f, c);
				MagicalFx.Converge(anchor, r, 18f, accent, 0.42f + 0.18f * c, 14, t2 * 0.012f, 1.6f + c);
				MagicalFx.Ring(anchor, 52f + 50f * power, -t2 * 0.02f, 0.20f + 0.18f * c, accent);
				MiniYoyoFx.DrawSolidEllipse(anchor, 78f + 46f * c, 60f + 36f * c, 0f, new Color(12, 6, 22), 0.30f);
				MagicalFx.Bloom(anchor, 62f + 80f * power, accent, 0.18f + 0.20f * c);
				return;
			}
			float b = MathHelper.Clamp((progress - 0.7f) / 0.45f, 0f, 1f);
			float fade = 1f - b;
			MagicalFx.Shockwave(anchor, maxRadius * (0.7f + 0.35f * b), b, accent, 1f, 5f);
			MagicalFx.Splat(anchor, 0f, MathHelper.Min(maxRadius * 0.95f, 150f),
				Color.Lerp(accent, Color.White, 0.25f) * (0.36f * fade));
			MagicalFx.Bloom(anchor, 100f + 130f * b, accent, 0.28f * fade);
		}

		private void DrawBloom()
		{
			float t = MathHelper.Clamp(progress, 0f, 1f);
			float r = MathHelper.Lerp(12f, maxRadius, MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, t));
			float fade = 1f - t;
			MagicalFx.Shockwave(anchor, r, progress, accent, 0.85f, 3f);
			MagicalFx.Ring(anchor, r * 0.75f, Main.GameUpdateCount * 0.012f, 0.24f * fade, accent);
			int n = 12;
			for (int i = 0; i < n; i++) {
				float a = i * MathHelper.TwoPi / n + Main.GameUpdateCount * 0.01f;
				Vector2 p = anchor + a.ToRotationVector2() * r * 0.72f;
				MagicalFx.Shape(ShapeId.SmearCrescent, p, a + MathHelper.PiOver2, 40f + 16f * fade, 26f + 9f * fade,
					Color.Lerp(accent, Color.White, 0.25f) * (0.42f * fade));
			}
			MagicalFx.Bloom(anchor, r * 1.7f, accent, 0.12f + 0.14f * fade);
			MagicalFx.StarFlare(anchor, Color.Lerp(accent, Color.White, 0.4f), 42f + 22f * fade,
				0f, 0.45f * fade);
		}

		private void DrawCone()
		{
			float t = MathHelper.Clamp(progress, 0f, 1f);
			float reach = maxRadius * MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, t);
			float rot = aimDir.ToRotation();
			float fade = 1f - 0.45f * t;
			MagicalFx.Stream(anchor, rot, 1.0f, reach * 0.95f, accent, 0.5f * fade, 16, 1.15f, 1f);
			for (int k = -1; k <= 1; k += 2) {
				float a = rot + k * 0.44f;
				Vector2 p = anchor + a.ToRotationVector2() * (reach * 0.70f);
				MagicalFx.Shape(ShapeId.SmearCrescent, p, a, 84f + 32f * t, 52f + 18f * t,
					Color.Lerp(accent, Color.White, 0.2f) * (0.36f * fade));
			}
			MagicalFx.Glow(anchor + aimDir * reach * 0.4f, MathHelper.Min(reach * 0.9f, 420f), accent, 0.08f * fade);
			MagicalFx.StarFlare(anchor, Color.Lerp(accent, Color.White, 0.4f), 34f + 16f * t, rot, 0.45f * fade);
		}

		private void DrawGeyser()
		{
			float t = MathHelper.Clamp(progress, 0f, 1f);
			float t2 = Main.GameUpdateCount;
			Color hot = Color.Lerp(accent, Color.White, 0.55f);
			Color core = Color.Lerp(accent, new Color(255, 245, 220), 0.75f);
			float mar = 5f * 70f;

			// 1) THE WOUND IN THE GROUND. A burning seam, drawn as a chain of glows
			//    that opens ahead of the wave - never a stroked line. Heat pulses
			//    ALONG it so the ground reads as being torn open by something
			//    travelling, which is the whole point of a geyser that marches.
			float open = MathHelper.Clamp(t * 1.35f, 0f, 1f) * mar;
			int seam = 18;
			for (int i = 0; i <= seam; i++) {
				float f = i / (float)seam;
				float d = f * mar;
				if (d > open)
					break;
				Vector2 p = anchor + aimDir * d;
				float wave = (float)Math.Sin((d * 0.045f - t * 7.5f));
				float heat = MathHelper.Clamp(0.30f + 0.70f * wave, 0f, 1f);
				MagicalFx.Bloom(p, 15f + 17f * heat, accent, 0.10f + 0.14f * heat);
				if (i % 3 == 0)
					MagicalFx.Shape(ShapeId.BoltZig, p,
						aimDir.ToRotation() + (i % 2 == 0 ? 0f : MathHelper.PiOver2),
						28f + 14f * heat, 12f + 6f * heat, accent * (0.22f * heat));
			}

			// 2) THE ERUPTIONS. This is what was missing: the old pass drew three
			//    ember curls per vent sitting on the floor, so a 350px molten
			//    eruption read as a smolder. A geyser is a rising WALL of melt -
			//    stacked glows that fatten at the vent and thin at the crown, plus
			//    a white-hot throat and a crown rim.
			for (int i = 0; i < 6; i++) {
				Vector2 at = anchor + aimDir * (i * 70f);
				float f = MathHelper.Clamp((t - i * 0.10f) / 0.42f, 0f, 1f);
				float alive = (float)Math.Sin(f * Math.PI);
				MagicalFx.Rune(at, 30f + 14f * power, t2 * 0.03f + i, 0.20f * alive, accent, 0.55f);
				if (alive <= 0.02f)
					continue;

				float h = 78f + 150f * power;
				const int steps = 8;
				float rise = 0.35f + 0.65f * f;
				for (int k = 0; k <= steps; k++) {
					float u = k / (float)steps;
					float col = (float)Math.Sin(MathHelper.Clamp(f * 1.55f - u * 0.55f, 0f, 1f) * MathHelper.Pi);
					if (col <= 0.02f)
						continue;
					Vector2 p = at - new Vector2(0f, h * u * rise);
					float taper = 1f - u * 0.62f;
					MagicalFx.Bloom(p, (56f + 34f * power) * taper * (0.55f + 0.45f * col),
						u > 0.68f ? hot : accent, (0.14f + 0.16f * col) * alive);
				}

				// Molten tongues peeling off the column - still the mod's own ember
				// art, because art beats geometry for anything organic.
				for (int k = 0; k < 4; k++) {
					float fk = MathHelper.Clamp(f - k * 0.13f, 0f, 1f);
					if (fk <= 0.02f)
						continue;
					float ember = (float)Math.Sin(fk * Math.PI);
					float side = k % 2 == 0 ? 1f : -1f;
					Vector2 p = at - new Vector2(0f, h * fk * 0.9f)
						+ new Vector2(side * (10f + 12f * fk * power) * (1f - fk * 0.5f), 0f);
					MagicalFx.Shape(ShapeId.EmberCurl, p, i * 1.7f + k * 1.1f + t2 * 0.02f,
						42f + 20f * ember, 28f + 13f * ember,
						Color.Lerp(accent, hot, fk) * (0.52f * ember * alive));
				}

				// The two reads that say MOLT UNDER PRESSURE: a white-hot throat at
				// the vent, and a crown rim where the column breaks.
				MagicalFx.Bloom(at, 34f + 22f * power, core, 0.34f * alive);
				MagicalFx.Crescent(at - new Vector2(0f, h * f * 0.9f), t2 * 0.02f + i,
					54f + 26f * alive, hot * (0.30f * alive));
				MagicalFx.Splat(at, 0f, 56f + 26f * power, accent * (0.26f * alive));
			}

			// 3) THE HIT READ. GeyserTick damages vent `i` on the exact tick
			//    progress crosses i/6, so the eruption IS the hit - but a rising
			//    wall of light on its own is scenery, and "the fire is pretty"
			//    is not the same report as "the ability did something". Each
			//    vent therefore kicks its own shock ring and flash the moment its
			//    damage lands, off the same progress value GeyserTick uses (no
			//    new state, no desync): six separate reads marching out along the
			//    seam, each of which is a real hit landing on whatever is there.
			for (int i = 0; i < 6; i++) {
				float since = t - i / 6f;
				if (since < 0f || since > 0.30f)
					continue;
				float k = 1f - since / 0.30f;
				Vector2 at = anchor + aimDir * (i * 70f);
				MagicalFx.QueueRing(at, 36f + 30f * (1f - k) * power,
					Color.Lerp(accent, Color.White, 0.40f), 0.42f * k, 16, 1.05f);
				MagicalFx.Bloom(at, 46f + 38f * power, core, 0.28f * k);
			}
		}

		private void DrawLance()
		{
			float t2 = Main.GameUpdateCount;
			if (progress < 0.55f) {
				float c = progress / 0.55f;
				MagicalFx.Converge(anchor, maxRadius * 1.45f, 20f, accent, 0.40f + 0.25f * c, 12, t2 * 0.012f, 1.25f);
				MagicalFx.Ring(anchor, maxRadius * (0.42f + 0.18f * c), t2 * 0.02f, 0.24f + 0.26f * c, accent);
				MagicalFx.Shape(ShapeId.RingCracked, anchor, -t2 * 0.015f,
					MathHelper.Min(maxRadius * 1.05f, 150f), MathHelper.Min(maxRadius * 1.05f, 150f),
					accent * (0.16f * c));
				MagicalFx.StarFlare(anchor, Color.Lerp(accent, Color.White, 0.45f), 34f + 30f * c, 0f,
					0.35f + 0.4f * c);
				MagicalFx.Light(anchor, accent, 0.4f + 0.7f * c);
				return;
			}
			float strike = MathHelper.Clamp((progress - 0.55f) / 0.30f, 0f, 1f);
			float fade = 1f - MathHelper.Clamp((progress - 0.85f) / 0.50f, 0f, 1f);
			float width = (18f + 26f * power) * (0.7f + 0.3f * strike);
			Vector2 top = anchor - new Vector2(0f, 760f);
			MiniYoyoFx.DrawBeamV2(top, anchor, Color.Lerp(accent, Color.White, 0.45f), accent, width, strike * fade, 1.1f);
			MagicalFx.Bolt(top, anchor, Color.Lerp(accent, Color.White, 0.6f), 0.8f * fade, width * 0.5f,
				(int)t2, 0.14f);
			MagicalFx.Shockwave(anchor, maxRadius, strike, accent, 0.9f * fade, 4.2f);
			MagicalFx.Ring(anchor, maxRadius * 0.72f, -t2 * 0.03f, 0.28f * fade, Color.Lerp(accent, Color.White, 0.3f));
			MagicalFx.Splat(anchor, 0f, MathHelper.Min(maxRadius * 0.9f, 140f), accent * (0.34f * fade));
			MagicalFx.Bloom(anchor, 96f + 90f * power, accent, 0.28f * fade);
		}

		private void DrawChain()
		{
			if (chainNodes == null || chainNodes.Count < 2)
				return;
			float t2 = Main.GameUpdateCount;
			float fade = 1f - progress * 0.45f;
			for (int i = 0; i + 1 < chainNodes.Count; i++) {
				Vector2 a = chainNodes[i];
				Vector2 b = chainNodes[i + 1];
				float flicker = 0.7f + 0.3f * (float)Math.Sin(t2 * 0.9f + i * 2.1f);
				MagicalFx.Bolt(a, b, Color.Lerp(accent, Color.White, 0.35f), 0.85f * flicker * fade,
					(4f + 4f * power) * flicker, (int)t2 + i * 13, 0.30f);
				MagicalFx.Splat(b, t2 * 0.05f + i * 1.3f, 44f * flicker, accent * (0.30f * fade));
				MiniYoyoFx.DrawGlowAdditive(b, 32f * flicker, accent, 0.26f * fade);
			}
		}

		private void DrawSlash()
		{
			float t = MathHelper.Clamp(progress, 0f, 1f);
			float reach = maxRadius * MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, t);
			float rot = aimDir.ToRotation();
			float fade = 1f - 0.75f * t;
			for (int k = -1; k <= 1; k++) {
				float a = rot + k * 0.32f;
				Vector2 p = anchor + a.ToRotationVector2() * (reach * 0.62f);
				MagicalFx.Shape(ShapeId.SmearCrescent, p, a, 104f + 40f * t, 66f + 24f * t,
					Color.Lerp(accent, Color.White, k == 0 ? 0.5f : 0.2f) * (0.42f * fade));
			}
			MagicalFx.Stream(anchor, rot, 1.1f, reach, accent, 0.5f * fade, 12, 1.35f, 1f);
			MagicalFx.StarFlare(anchor + aimDir * reach * 0.72f, Color.Lerp(accent, Color.White, 0.5f), 46f, rot,
				0.55f * fade);
			MagicalFx.Glow(anchor + aimDir * reach * 0.5f, MathHelper.Min(reach * 0.9f, 420f), accent, 0.08f * fade);
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			// Slash / cone / geyser have already spent their visual; the rest pop.
			if (mode == ModeSlash || mode == ModeCone || mode == ModeGeyser)
				return;
			YoyoParticles.SpawnRing(anchor, 6f, Math.Max(24f, maxRadius * 0.4f), 16, accent, 2.6f);
			MagicalFx.Fan(ShapeId.SplatSoft, anchor, accent, 3, 2.2f, 22, 0.7f);
		}
	}

	/// <summary>
	/// Seeking / falling / boomerang / saw star. One type, several behaviours:
	/// ai[0] = mode, ai[1] = power01, ai[2] = source identity for the accent.
	/// Every mode draws real art - a star flare, a meteor, a crescent blade or a
	/// shard - never a bare dot.
	/// </summary>
	public class MagicalStarProj : ModProjectile
	{
		public const float ModeHoming = 0f;
		public const float ModeFall = 1f;
		public const float ModeBoomerang = 2f;
		public const float ModeSaw = 3f;   // spinning blade that curves out and reels back
		public const float ModeShard = 4f; // straight, heavy, pierces
		public const float ModeSteer = 5f; // steering orb: bends toward the cursor

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float mode;
		private int age;
		private Color accent = new(150, 180, 255);
		private readonly List<Vector2> steerTrail = new();
		private static readonly Vector2[] trailPts = new Vector2[40];

		public override void SetDefaults()
		{
			Projectile.width = 14;
			Projectile.height = 14;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = 2;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 18;
			Projectile.timeLeft = 300;
		}

		public override void AI()
		{
			mode = Projectile.ai[0];
			age++;
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			if (age == 1) {
				Projectile.penetrate = mode == ModeSaw ? 6
					: mode == ModeShard ? 4
					: mode == ModeFall ? -1 : 2;
			}
			if (mode != ModeSaw)
				Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			else
				Projectile.rotation += 0.42f;

			if (mode == ModeHoming) {
				NPC target = FindTarget(760f);
				if (target != null) {
					Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity);
					Projectile.velocity = Vector2.Lerp(Projectile.velocity, want * Math.Max(9f, Projectile.velocity.Length()), 0.11f);
				}
			}
			else if (mode == ModeBoomerang) {
				// Out, wide loop, back to the owner's hand.
				Player owner = OwnerOf();
				if (age < 26) {
					Projectile.velocity *= 0.965f;
				}
				else if (owner != null && owner.active) {
					if (age == 26 && !Main.dedServ) {
						// The turn: a soft ring at the apex, so the outbound and
						// return legs read as two separate beats.
						YoyoParticles.SpawnRing(Projectile.Center, 4f, 58f, 16, accent, 2.6f);
						MagicalFx.Bloom(Projectile.Center, 46f, accent, 0.3f); // per-tick: stays a queued glow
					}
					Vector2 to = (owner.Center - Projectile.Center).SafeNormalize(Projectile.velocity);
					Projectile.velocity = Vector2.Lerp(Projectile.velocity, to * 14f, 0.12f);
					if (Vector2.DistanceSquared(Projectile.Center, owner.Center) < 26f * 26f)
						Projectile.Kill();
				}
			}
			else if (mode == ModeSaw) {
				// Curves wide, then reels back to the hand like a return throw.
				Player owner = OwnerOf();
				if (age < 32) {
					Projectile.velocity = Projectile.velocity.RotatedBy(0.030f) * 0.996f;
				}
				else if (owner != null && owner.active) {
					Vector2 to = (owner.Center - Projectile.Center).SafeNormalize(Projectile.velocity);
					Projectile.velocity = Vector2.Lerp(Projectile.velocity, to * 13f, 0.10f);
					if (age > 54 && Vector2.DistanceSquared(Projectile.Center, owner.Center) < 30f * 30f)
						Projectile.Kill();
				}
			}
			else if (mode == ModeShard) {
				// Straight and heavy with a lazy drop: a thrown shard, not a missile.
				Projectile.velocity.Y += 0.05f;
			}
			else if (mode == ModeSteer) {
				// Steering orb: bends toward the owner's cursor with a max turn
				// angle. Only the owner's machine reads the cursor; every other
				// side keeps the synced heading.
				if (Projectile.owner == Main.myPlayer) {
					Vector2 want = (Main.MouseWorld - Projectile.Center).SafeNormalize(Projectile.velocity);
					float speed = MathHelper.Clamp(Projectile.velocity.Length(), 8.5f, 11.5f);
					Projectile.velocity = MagicalOrbit.LerpAngle(Projectile.velocity.ToRotation(), want.ToRotation(), 0.065f)
						.ToRotationVector2() * speed;
				}
				steerTrail.Add(Projectile.Center);
				if (steerTrail.Count > 40)
					steerTrail.RemoveAt(0);
				if (age > 130)
					Projectile.Kill();
			}
			else if (mode == ModeFall) {
				Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.22f, 22f);
			}

			if (!Main.dedServ) {
				MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 34f, accent, 0.22f);
				if (age % 2 == 0)
					YoyoParticles.SpawnSpark(Projectile.Center, -Projectile.velocity * 0.06f, accent, 0.7f, 14, 0.94f);
				if (mode == ModeSaw && age % 3 == 0)
					YoyoParticles.SpawnSpark(Projectile.Center,
						new Vector2(-Projectile.velocity.Y, Projectile.velocity.X) * 0.05f, Color.White, 0.5f, 10, 0.9f);
			}
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.7f);
		}

		private Player OwnerOf()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		private NPC FindTarget(float range)
		{
			NPC best = null;
			float bestDist = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc))
					continue;
				float d = Vector2.DistanceSquared(npc.Center, Projectile.Center);
				if (d < bestDist) {
					bestDist = d;
					best = npc;
				}
			}
			return best;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.dedServ)
				return;
			MagicalFx.Impact(target.Center, accent, 0.7f, Projectile.owner,
				Projectile.velocity.ToRotation());
			if (mode == ModeFall)
				MiniYoyoFx.Shake(3, 7, target.Center);
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			if (mode == ModeFall)
				MagicalFx.Scorch(Projectile.Center, accent, 1.1f, crack: true);
			else if (mode == ModeShard)
				MagicalFx.Scorch(Projectile.Center, accent, 0.7f);
			YoyoParticles.SpawnRing(Projectile.Center, 6f, mode == ModeFall ? 74f : 42f, 14, accent, 2.4f);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 3, 2.4f, 18, 0.4f);
			if (mode == ModeFall) {
				MiniYoyoFx.Shake(3, 7, Projectile.Center);
				MagicalFx.ScreenPulse(Projectile.Center, 0.22f, accent);
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 vel = Projectile.velocity;
			float dir = vel.LengthSquared() > 0.01f ? vel.ToRotation() : Projectile.rotation;
			Vector2 back = vel.LengthSquared() > 0.01f ? vel.SafeNormalize(Vector2.UnitX) : Vector2.UnitX;
			switch (mode) {
				case ModeFall:
					// Meteor: real falling-star art with a stretched comet tail.
					MagicalFx.Wisp(Projectile.Center - back * 24f, dir, 56f, 20f, accent * 0.45f);
					MagicalFx.Sprite(MagicalFx.ArtMeteor, Projectile.Center, Color.White, dir + MathHelper.PiOver2,
						46f, 46f);
					MagicalFx.Bloom(Projectile.Center, 52f, accent, 0.32f);
					break;
				case ModeShard:
					MagicalFx.Wisp(Projectile.Center - back * 14f, dir, 40f, 13f, accent * 0.4f);
					MagicalFx.Sprite(MagicalFx.ArtShard, Projectile.Center, Color.White, Projectile.rotation,
						10f, 38f);
					MagicalFx.Bloom(Projectile.Center, 30f, accent, 0.3f);
					break;
				case ModeSaw:
					// Twin spinning crescent blades.
					MagicalFx.Shape(ShapeId.SmearCrescent, Projectile.Center, Projectile.rotation, 46f, 32f,
						Color.Lerp(accent, Color.White, 0.35f));
					MagicalFx.Shape(ShapeId.SmearCrescent, Projectile.Center, Projectile.rotation + MathHelper.Pi,
						36f, 24f, accent * 0.8f);
					MagicalFx.Bloom(Projectile.Center, 38f, accent, 0.25f);
					break;
				case ModeBoomerang:
					MagicalFx.Shape(ShapeId.SmearCrescent, Projectile.Center, Projectile.rotation, 60f, 42f,
						Color.Lerp(accent, Color.White, 0.5f));
					MagicalFx.Wisp(Projectile.Center - back * 20f, dir, 52f, 16f, accent * 0.45f);
					MagicalFx.Bloom(Projectile.Center, 48f, accent, 0.32f);
					break;
				case ModeSteer: {
					// Steering orb: a long curved ribbon behind it, so the path you
					// actually steered stays readable on screen.
					int count = 0;
					for (int i = 0; i < steerTrail.Count && count < trailPts.Length; i++)
						trailPts[count++] = steerTrail[i];
					if (count >= 2) {
						MiniYoyoFx.DrawPathBand(trailPts, count, 2.6f, 12f, accent, 0.24f);
						MiniYoyoFx.DrawPathBand(trailPts, count, 4.6f, 1.4f, accent, 0.7f);
						MiniYoyoFx.DrawPathBand(trailPts, count, 1.8f, 0.5f,
							Color.Lerp(accent, Color.White, 0.6f), 0.8f);
					}
					MagicalFx.StarFlare(Projectile.Center, Color.Lerp(accent, Color.White, 0.45f), 34f,
						Projectile.rotation, 0.85f);
					MagicalFx.Bloom(Projectile.Center, 36f, accent, 0.35f);
					break;
				}
				default:
					// Homing mote: star flare + comet head.
					MagicalFx.Wisp(Projectile.Center - back * 13f, dir, 40f, 13f, accent * 0.5f);
					MagicalFx.StarFlare(Projectile.Center, Color.Lerp(accent, Color.White, 0.4f), 30f,
						Projectile.rotation, 0.85f);
					MagicalFx.Bloom(Projectile.Center, 30f, accent, 0.35f);
					break;
			}
			return false;
		}
	}

	/// <summary>
	/// Persistent field / trap / well. ai[0] = mode, ai[1] = power01,
	/// ai[2] = source identity for the accent.
	/// Modes: 0 sanctum (mends you, burns them), 1 void well (pulls), 2 bramble
	/// (roots), 3 sigil (waits, then detonates), 4 chrono (slows them),
	/// 5 pyre (a standing flame column that scorches whatever stands in it),
	/// 6 bubble (a drifting bubble field that pops on contact).
	/// </summary>
	public class MagicalZoneProj : ModProjectile
	{
		public const float ModeSanctum = 0f;
		public const float ModeVoidWell = 1f;
		public const float ModeBramble = 2f;
		public const float ModeSigil = 3f;
		public const float ModeChrono = 4f;
		public const float ModePyre = 5f;
		public const float ModeBubble = 6f;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float mode;
		private float power;
		private int age;
		private Vector2 home;
		private Color accent = new(150, 180, 255);
		private bool detonated;

		public override void SetDefaults()
		{
			Projectile.width = 8;
			Projectile.height = 8;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 26;
			Projectile.timeLeft = 300;
		}

		private float Radius => ((int)Projectile.ai[0] == (int)ModeVoidWell ? 250f
			: (int)Projectile.ai[0] == (int)ModeSigil ? 150f
			: (int)Projectile.ai[0] == (int)ModePyre ? 130f : 215f)
			* (0.7f + 0.5f * MathHelper.Clamp(Projectile.ai[1], 0f, 1f));

		public override void AI()
		{
			mode = Projectile.ai[0];
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			age++;

			float r = Radius;
			if (age == 1)
				home = Projectile.Center;
			// The field sits still in the world; the hitbox is its own disc
			// (Colliding() does the shape test) and is kept centred on `home`.
			Projectile.width = Projectile.height = (int)(r * 2f);
			Projectile.position = home - new Vector2(Projectile.width * 0.5f, Projectile.height * 0.5f);

			if (mode == ModeSigil) {
				if (!detonated) {
					bool inside = false;
					for (int i = 0; i < Main.maxNPCs && !inside; i++) {
						NPC npc = Main.npc[i];
						if (npc.active && !npc.friendly && npc.CanBeChasedBy()
							&& Vector2.DistanceSquared(npc.Center, Projectile.Center) < r * r)
							inside = true;
					}
					if (inside || age > 150) {
						detonated = true;
						MagicalReleaseCommon.RadialHit(Owner(), Projectile.Center, r, Projectile.damage * 2, 5f);
						if (!Main.dedServ) {
							MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, Color.White, 6, 5.6f, 24, 0.7f);
							MagicalFx.Fan(ShapeId.NeedleLong, Projectile.Center, accent, 10, 5f, 22, 0.55f);
							MagicalFx.Scorch(Projectile.Center, accent, 1.3f, crack: true);
							MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 1.1f);
							MiniYoyoFx.Shake(5, 12, Projectile.Center);
							MagicalFx.ScreenPulse(Projectile.Center, 0.3f, accent);
							YoyoParticles.SpawnRing(Projectile.Center, 12f, r * 1.6f, 18, accent, 4f);
						}
						Projectile.Kill();
					}
				}
			}
			else {
				switch (mode) {
					case ModeVoidWell:
						MagicalReleaseCommon.RadialGrab(Projectile.Center, r, 0.5f, true);
						break;
					case ModeBramble:
						SlowNpcs(r, 0.55f);
						break;
					case ModeChrono:
						SlowNpcs(r, 0.35f);
						SpeedOwner(0.9f);
						break;
					case ModeSanctum:
						MendOwner();
						break;
					case ModePyre:
						PyreTick(r);
						break;
				}
			}

			AmbientFx(r);
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (mode == ModeVoidWell ? 0.35f : 0.6f));
		}

		private Player Owner()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		private void SlowNpcs(float radius, float factor)
		{
			if (!MagicalReleaseCommon.CanMoveNpcs)
				return;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.boss || !npc.CanBeChasedBy())
					continue;
				if (DummySafety.Pin(npc))
					continue;
				if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radius * radius)
					continue;
				if (npc.velocity.LengthSquared() > 0.04f)
					npc.velocity *= factor;
				npc.netUpdate = true;
			}
		}

		/// <summary>Pyre: everything standing in the flame catches, and embers spit.</summary>
		private void PyreTick(float radius)
		{
			if (!MagicalReleaseCommon.CanMoveNpcs || age % 20 != 0)
				return;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.boss || !npc.CanBeChasedBy())
					continue;
				if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radius * radius)
					continue;
				npc.AddBuff(BuffID.OnFire3, 180);
				npc.netUpdate = true;
			}
		}

		private void SpeedOwner(float factor)
		{
			if (Projectile.owner != Main.myPlayer)
				return;
			Player owner = Owner();
			if (owner == null || !owner.active)
				return;
			if (Vector2.DistanceSquared(owner.Center, home) > Radius * Radius)
				return;
			// Chrono field: your own movement reads faster inside the bubble.
			if (owner.velocity.LengthSquared() > 1f)
				owner.velocity *= 1f + (1f - factor) * 0.35f;
		}

		private void MendOwner()
		{
			if (Projectile.owner != Main.myPlayer || age % 20 != 0)
				return;
			Player owner = Owner();
			if (owner == null || !owner.active)
				return;
			bool inside = Vector2.DistanceSquared(owner.Center, home) < Radius * Radius;
			if (inside && owner.statLife < owner.statLifeMax2) {
				owner.statLife = Math.Min(owner.statLifeMax2, owner.statLife + 2);
				if (!Main.dedServ)
					MagicalFx.CastFlash(owner.Center, accent, 0.4f);
			}
		}

		/// <summary>Ambient field life: a slow trickle of travelling motes per mode.</summary>
		private void AmbientFx(float r)
		{
			if (Main.dedServ || age % 8 != 0)
				return;
			switch (mode) {
				case ModeVoidWell:
					MagicalReleaseCommon.InfallWisps(Projectile.Center, r, accent, 2, 0.8f);
					break;
				case ModePyre:
					MagicalFx.Embers(Projectile.Center + Main.rand.NextVector2Circular(r * 0.5f, r * 0.3f),
						accent, 2, 2.2f, 22, 0.5f, -MathHelper.PiOver2, 0.8f);
					break;
				case ModeBramble:
					MagicalFx.Shards(Projectile.Center + Main.rand.NextVector2Circular(r * 0.7f, r * 0.7f),
						accent, 1, 2.6f, 20, 0.4f);
					break;
				case ModeChrono:
					MagicalFx.Fan(ShapeId.SmearCrescent, Projectile.Center + Main.rand.NextVector2Circular(r * 0.6f, r * 0.6f),
						accent, 1, 1.2f, 22, 0.3f, -MathHelper.PiOver2, 0.7f);
					break;
				case ModeSanctum:
					MagicalFx.Fan(ShapeId.SmearCrescent, Projectile.Center + Main.rand.NextVector2Circular(r * 0.7f, r * 0.4f),
						accent, 1, 1.4f, 24, 0.32f, -MathHelper.PiOver2, 0.8f);
					break;
				default:
					MagicalFx.Fan(ShapeId.SmearCrescent, Projectile.Center + Main.rand.NextVector2Circular(r * 0.6f, r * 0.6f),
						accent, 1, 1.6f, 20, 0.3f);
					break;
			}
		}

		private void ZoneVisual(float r)
		{
			if (Main.dedServ)
				return;
			float t2 = Main.GameUpdateCount;
			float breathe = 1f + 0.03f * (float)Math.Sin(t2 * 0.09f);
			float spin = t2 * 0.006f;
			if (mode == ModeVoidWell) {
				// A tear, not a puddle: a deep throat, a torn cracked rim and a
				// legible intake so the pull reads instantly.
				MiniYoyoFx.DrawSolidEllipse(Projectile.Center, r * 1.15f, r * 0.82f, 0f, new Color(10, 5, 20), 0.40f);
				MiniYoyoFx.DrawSolidEllipse(Projectile.Center, r * 0.60f, r * 0.40f, 0f, new Color(4, 2, 9), 0.55f);
				MagicalFx.Shape(ShapeId.RingCracked, Projectile.Center, -spin, r * 1.30f, r * 0.98f, accent * 0.26f);
				MagicalFx.Converge(Projectile.Center, r * 0.95f, 14f, accent, 0.5f, 14, spin, 1.5f);
				MagicalFx.Ring(Projectile.Center, r * 0.92f, spin * 0.6f, 0.18f, Color.Lerp(accent, Color.White, 0.4f));
				MagicalFx.Ring(Projectile.Center, r * 0.72f, -spin, 0.30f, accent);
				MagicalFx.Rune(Projectile.Center, r * 0.24f, spin * 1.7f, 0.18f, Color.Lerp(accent, Color.White, 0.4f));
			}
			else if (mode == ModeSigil) {
				MagicalFx.Ring(Projectile.Center, r * 0.8f, spin, 0.30f + 0.10f * (float)Math.Sin(t2 * 0.1f), accent);
				MagicalFx.Rune(Projectile.Center, r * 0.26f, -spin * 1.5f, 0.20f, Color.Lerp(accent, Color.White, 0.4f));
				MagicalFx.Shape(ShapeId.RingCracked, Projectile.Center, spin * 0.7f,
					MathHelper.Min(r * 1.15f, 150f), MathHelper.Min(r * 1.15f, 150f), accent * 0.14f);
			}
			else if (mode == ModePyre) {
				MiniYoyoFx.DrawSolidEllipse(Projectile.Center, r * 0.72f, r * 0.36f, 0f, new Color(30, 10, 6), 0.30f);
				for (int i = 0; i < 5; i++) {
					float f = (t2 * 0.013f + i * 0.2f) % 1f;
					Vector2 p = Projectile.Center + new Vector2((i - 2f) * 22f, -f * 160f);
					float ember = (float)Math.Sin(f * Math.PI);
					MagicalFx.Shape(ShapeId.EmberCurl, p, i * 1.3f + t2 * 0.02f, 44f + 24f * ember, 32f + 18f * ember,
						accent * (0.44f * ember));
				}
				MagicalFx.Ring(Projectile.Center, r * 0.6f, spin, 0.2f, accent);
			}
			else {
				MagicalFx.Ring(Projectile.Center, r * 0.78f * breathe, spin * (mode == ModeChrono ? 1.8f : 0.8f), 0.26f, accent);
				if (mode != ModeChrono && mode != ModeBubble)
					MagicalFx.Rune(Projectile.Center, r * 0.24f, -spin * 1.4f, 0.16f, Color.Lerp(accent, Color.White, 0.3f));
				MagicalFx.Motes(Projectile.Center, r, accent, 0.42f, 10, 0.9f, 0.9f);
				MiniYoyoFx.DrawSolidEllipse(Projectile.Center, r * 1.2f, r * 0.7f, 0f, accent, 0.045f);
				if (mode == ModeBramble) {
					for (int i = 0; i < 8; i++) {
						float a = t2 * 0.004f + i * MathHelper.TwoPi / 8f;
						Vector2 p = Projectile.Center + a.ToRotationVector2() * (r * 0.82f);
						MagicalFx.Shape(ShapeId.NeedleLong, p, a,
							52f + 14f * (float)Math.Sin(t2 * 0.02f + i), 16f,
							accent * (0.34f + 0.1f * (float)Math.Sin(t2 * 0.03f + i)));
					}
				}
				else if (mode == ModeChrono) {
					MiniYoyoFx.DrawRingGradient(Projectile.Center, r * 0.75f, 2.2f,
						Color.Lerp(accent, Color.White, 0.45f), accent, 0.28f, 40);
					MiniYoyoFx.DrawRingGradient(Projectile.Center, r * 0.46f, 1.6f,
						accent, Color.Lerp(accent, Color.White, 0.5f), 0.2f, 30);
				}
				else if (mode == ModeBubble) {
					// A bubble field is a FIELD OF SPHERES, not another ring with
					// blades stuck in it. This mode exists because Bubble Prison
					// used to deploy the BRAMBLE zone - the same eight spinning
					// NeedleLong sprites as Leech Bramble, Crystal Garden and
					// Frostbound Grasp. Those are 52x16 hard-edge blades, so
					// rotating them around a small zone read as a heap of blocks:
					// Santa's bubbles were thorns, and thorns looked like squares.
					Color glass = Color.Lerp(accent, Color.White, 0.30f);
					for (int i = 0; i < 12; i++) {
						float ph = i * 1.7f;
						float rise = (t2 * (0.0038f + 0.0014f * (i % 3)) + i * 0.37f) % 1f;
						float rad = r * (0.20f + 0.62f * ((i * 0.618f) % 1f));
						float ang = ph + t2 * 0.0011f * (i % 2 == 0 ? 1f : -1f);
						float bob = (float)Math.Sin(t2 * 0.021f + ph) * (3f + 2f * (i % 3));
						float y = MathHelper.Lerp(r * 0.60f, -r * 0.70f, rise);
						Vector2 p = Projectile.Center + ang.ToRotationVector2() * rad * 0.55f
							+ new Vector2(0f, y + bob);
						float size = 12f + 9f * ((i * 0.377f) % 1f);
						// Fade in off the floor and out at the ceiling, so bubbles
						// are born and pop instead of sliding through each other.
						float vis = MathHelper.Clamp(rise / 0.12f, 0f, 1f)
							* MathHelper.Clamp((1f - rise) / 0.16f, 0f, 1f);
						MagicalFx.Bloom(p, size * 2.8f, glass, 0.14f * vis);
						MiniYoyoFx.DrawRingGradient(p, size * 0.86f, 1.4f,
							Color.Lerp(accent, Color.White, 0.66f), accent, 0.32f * vis, 14);
						MagicalFx.Bloom(p - new Vector2(size * 0.30f, size * 0.34f),
							size * 0.66f, Color.White, 0.36f * vis);
					}
					// One skin over the whole volume so it reads as a single body
					// of trapped foam rather than twelve loose spheres.
					MiniYoyoFx.DrawRingGradient(Projectile.Center, r * 0.92f, 1.8f, accent,
						Color.Lerp(accent, Color.White, 0.5f), 0.16f, 44);
				}
			}
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (mode == ModeSigil)
				return null; // the sigil detonates instead of ticking
			float r = Radius;
			return Vector2.DistanceSquared(targetHitbox.Center.ToVector2(), Projectile.Center) < r * r;
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (mode == ModeSigil)
				return false; // the sigil only detonates, no contact ticks
			if (target.CountsAsACritter)
				return false;
			return null;
		}

		public override void OnKill(int timeLeft)
		{
			// A well that closes collapses: the inward pull reverses into an
			// outward blast, so the field has a payoff either way.
			if (mode != ModeVoidWell || Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return;
			MagicalReleaseCommon.Spawn(Main.player[Projectile.owner], Projectile.Center, Vector2.Zero,
				ModContent.ProjectileType<MagicalBurstProj>(), Projectile.damage,
				MagicalBurstProj.ModeImplosion, power, Projectile.ai[2]);
		}

		// ---- Drawing (PreDraw only: the sprite batch is live here) ----

		public override bool PreDraw(ref Color lightColor)
		{
			if (!Main.dedServ)
				ZoneVisual(Radius);
			return false;
		}
	}

	/// <summary>
	/// Deployable: totem / storm cloud / portal / mirror wall / orbiting shield
	/// / prism cage. ai[0] = mode, ai[1] = power01, ai[2] = source identity.
	/// Every deployable is felt on contact now; the cage also ticks damage on
	/// whatever stands inside it and slams shut at the end of its life.
	/// </summary>
	public class MagicalDeployProj : ModProjectile
	{
		public const float ModeTotem = 0f;     // fixed, fires at the nearest enemy
		public const float ModeCloud = 1f;     // follows the owner, calls strikes
		public const float ModePortal = 2f;    // fixed, streams bolts at the cursor
		public const float ModeWall = 3f;      // moving barrier that reflects projectiles
		public const float ModeShield = 4f;    // orbits the owner, absorbs hits
		public const float ModeCage = 5f;      // six light walls that close and detonate

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float mode;
		private float power;
		private int age;
		private int fireClock;
		private int hitsLeft = 3;
		// Cloud only: the ONE victim the storm has committed to. -1 = none yet.
		private int lockedStrikeTarget = -1;
		private int lockedStrikeType;
		private Vector2 strikePos;
		private int strikeFlash;
		private int blockFlash;
		private Color accent = new(150, 180, 255);

		public override void SetDefaults()
		{
			Projectile.width = 26;
			Projectile.height = 26;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 30;
			Projectile.timeLeft = 420;
		}

		public override void AI()
		{
			mode = Projectile.ai[0];
			power = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			age++;
			fireClock++;
			if (strikeFlash > 0)
				strikeFlash--;
			if (blockFlash > 0)
				blockFlash--;
			Player owner = Owner();
			// Server-side safety net: if the tech went OFF (the owner client
			// already dismisses its copies), a deployable must not keep orbiting
			// or firing on the authority side either. Without this a Shield in
			// multiplayer could keep absorbing hits after the OFF switch.
			if (owner != null && (!YoyoTechCommon.TechActiveFor(owner, out YoyoTechType deployTech)
				|| deployTech != YoyoTechType.Magical)) {
				Projectile.Kill();
				return;
			}
			// Cloud: commit ONCE, at cast time, from the CURSOR. Waiting for the
			// first periodic strike meant the victim was chosen from wherever
			// the mouse happened to be a second later, not where the player
			// aimed the cast. The lock then holds until the victim dies or
			// leaves range (see LockedStrikeTarget).
			if (mode == ModeCloud && age == 1 && Projectile.owner == Main.myPlayer)
				TryLockCloudTargetFromCursor();

			if (mode == ModeCloud) {
				if (owner != null && owner.active) {
					Vector2 want = owner.Center - new Vector2(0f, 150f);
					Projectile.velocity = Vector2.Lerp(Projectile.velocity, (want - Projectile.Center) * 0.06f, 0.12f);
					Projectile.Center += Projectile.velocity;
				}
				if (fireClock >= 54) {
					fireClock = 0;
					StrikeRandom(owner);
				}
			}
			else if (mode == ModeShield) {
				if (owner != null && owner.active) {
					// Spawn velocity carries a phase offset so multiple shields
					// (Echo Twins) sit at different points on the same orbit.
					float a = Main.GameUpdateCount * 0.06f + Projectile.velocity.X * 0.02f;
					Projectile.Center = owner.Center + a.ToRotationVector2() * 74f;
				}
				AbsorbProjectiles();
			}
			else if (mode == ModeWall) {
				Projectile.rotation = Projectile.velocity.ToRotation();
				ReflectProjectiles();
				Projectile.velocity *= 0.985f;
				if (Projectile.velocity.LengthSquared() > 0.02f)
					Projectile.Center += Projectile.velocity;
			}
			else if (mode == ModeCage) {
				CageTick(owner);
			}
			else if (Projectile.owner == Main.myPlayer && fireClock >= (mode == ModePortal ? 26 : 46)) {
				fireClock = 0;
				FireVolley(owner);
			}

			if (age == 1 && !Main.dedServ) {
				MagicalFx.CastFlash(Projectile.Center, accent, 0.8f);
				if (mode == ModeCloud)
					MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, Color.Lerp(accent, Color.White, 0.3f),
						4, 1.6f, 26, 1f);
			}

			if (mode != ModeWall && mode != ModeShield)
				Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.7f);
		}

		private Player Owner()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		/// <summary>
		/// Prism cage: ticks damage on whatever is caught inside, then slams the
		/// six walls shut and detonates.
		/// </summary>
		private void CageTick(Player owner)
		{
			float r = 130f + 40f * power;
			if (!MagicalReleaseCommon.CanMoveNpcs || owner == null)
				return;
			if (age % 20 == 0 && age <= 160)
				MagicalReleaseCommon.RadialHit(owner, Projectile.Center, r, Math.Max(1, Projectile.damage / 2), 1.4f,
					pierceArmor: true);
			if (age == 161) {
				MagicalReleaseCommon.RadialHit(owner, Projectile.Center, r * 1.15f, Projectile.damage * 2, 6f);
				if (!Main.dedServ) {
					MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, Color.White, 6, 5.4f, 24, 0.7f);
					MagicalFx.Fan(ShapeId.RingCracked, Projectile.Center, accent, 5, 4.6f, 22, 0.8f);
					MiniYoyoFx.FlashFX(Projectile.Center, Color.White, 1.2f);
					MiniYoyoFx.Shake(4, 10, Projectile.Center);
					MagicalFx.ScreenPulse(Projectile.Center, 0.28f, accent);
					YoyoParticles.SpawnRing(Projectile.Center, 12f, 200f, 20, accent, 4f);
				}
				Projectile.Kill();
			}
		}

		private void FireVolley(Player owner)
		{
			if (owner == null)
				return;
			int bolt = ModContent.ProjectileType<MagicalStarProj>();
			Vector2 dir;
			if (mode == ModePortal) {
				dir = MagicalReleaseCommon.SafeAim(owner, Projectile.Center, new Vector2(owner.direction, 0f));
				for (int i = -1; i <= 1; i++) {
					Vector2 d = dir.RotatedBy(i * 0.09f);
					MagicalReleaseCommon.Spawn(owner, Projectile.Center, d * 15f, bolt, Projectile.damage,
						MagicalStarProj.ModeHoming, power, Projectile.ai[2]);
				}
			}
			else {
				NPC target = NearestTarget(760f);
				if (target == null)
					return;
				dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
				MagicalReleaseCommon.Spawn(owner, Projectile.Center, dir * 13f, bolt, Projectile.damage,
					MagicalStarProj.ModeHoming, power, Projectile.ai[2]);
			}
			if (!Main.dedServ)
				MagicalFx.CastFlash(Projectile.Center, accent, 0.5f);
		}

		private void StrikeRandom(Player owner)
		{
			NPC target = LockedStrikeTarget();
			// ONLY THE OWNER CLIENT CAN SEE THE CURSOR. It picks the victim once,
			// from the cursor outward, and writes the lock into SendExtraAI. The
			// server used to run this same search with Main.MouseWorld == (0,0),
			// so in multiplayer the bolt the player saw and the NPC the server
			// damaged were chosen from two different origins. A remote side now
			// waits for the owner's lock instead of guessing.
			if (target == null && Projectile.owner == Main.myPlayer) {
				if (!TryLockCloudTargetFromCursor())
					return;
				target = LockedStrikeTarget();
			}
			if (target == null)
				return;
			// Visuals: every client runs its own clock and draws its own bolt.
			if (!Main.dedServ) {
				MagicalFx.Impact(target.Center, accent, 0.8f, Projectile.owner, 0f);
				MiniYoyoFx.FlashFX(target.Center, Color.White, 0.9f);
				strikePos = target.Center;
				strikeFlash = 9;
			}
			// Damage: server/SP authority only, so MP clients never double-dip.
			MagicalReleaseCommon.RadialHit(owner, target.Center, 84f + 50f * power, Projectile.damage, 3f);
		}

		/// <summary>Commit the storm to the hittable enemy nearest the cursor.
		/// Called once at cast time, and again only when the previous lock went
		/// stale. Returns false when the cursor has no candidate this tick.</summary>
		private bool TryLockCloudTargetFromCursor()
		{
			NPC target = NearestTargetFrom(Main.MouseWorld, 900f);
			if (target == null)
				return false;
			lockedStrikeTarget = target.whoAmI;
			lockedStrikeType = target.type;
			Projectile.netUpdate = true;
			return true;
		}

		// The cloud's commitment is server state, so it must ride the projectile's
		// net update rather than stay a private client-side field.
		public override void SendExtraAI(BinaryWriter writer)
		{
			writer.Write((short)lockedStrikeTarget);
			writer.Write((short)lockedStrikeType);
		}

		public override void ReceiveExtraAI(BinaryReader reader)
		{
			lockedStrikeTarget = reader.ReadInt16();
			lockedStrikeType = reader.ReadInt16();
		}

		/// <summary>The victim the cloud already committed to, or null when the
		/// lock is stale (dead, out of range, or its slot was recycled).</summary>
		private NPC LockedStrikeTarget()
		{
			if (lockedStrikeTarget < 0 || lockedStrikeTarget >= Main.maxNPCs)
				return null;
			NPC npc = Main.npc[lockedStrikeTarget];
			if (!npc.active || npc.type != lockedStrikeType
				|| !MagicalReleaseCommon.Hittable(npc)
				|| Vector2.DistanceSquared(npc.Center, Projectile.Center) > 900f * 900f) {
				lockedStrikeTarget = -1;
				return null;
			}
			return npc;
		}

		private NPC NearestTarget(float range)
		{
			return NearestTargetFrom(Projectile.Center, range);
		}

		private NPC NearestTargetFrom(Vector2 from, float range)
		{
			NPC best = null;
			float bestD = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc))
					continue;
				float d = Vector2.DistanceSquared(npc.Center, from);
				if (d < bestD) {
					bestD = d;
					best = npc;
				}
			}
			return best;
		}

		/// <summary>Mirror wall: turns hostile projectiles around, capped per wall.</summary>
		private void ReflectProjectiles()
		{
			// Server authority: a client-side kill/spawn pair would fight the
			// server's own copy of the hostile projectile.
			if (!MagicalReleaseCommon.CanMoveNpcs)
				return;
			if (hitsLeft <= 0) {
				PopAndDie();
				return;
			}
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (!p.active || p.hostile || p.owner == Projectile.owner || p.damage <= 0)
					continue;
				if (Vector2.DistanceSquared(p.Center, Projectile.Center) > 46f * 46f)
					continue;
				// Turn it around and make it yours.
				Vector2 speed = p.velocity.Length() > 0.1f ? -p.velocity : Vector2.UnitX * 8f;
				int damage = Math.Max(1, p.damage);
				int type = p.type;
				Vector2 pos = p.Center;
				p.Kill();
				int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), pos, speed, type,
					damage, 3f, Projectile.owner);
				if (idx >= 0 && idx < Main.maxProjectiles) {
					Projectile mirror = Main.projectile[idx];
					mirror.hostile = false;
					mirror.friendly = true;
					mirror.netUpdate = true;
				}
				hitsLeft--;
				if (!Main.dedServ)
					MagicalFx.Impact(pos, accent, 0.7f, Projectile.owner, speed.ToRotation());
				if (hitsLeft <= 0)
					break;
			}
		}

		/// <summary>Shield: swallows hostile shots that touch it, then pops.</summary>
		private void AbsorbProjectiles()
		{
			if (!MagicalReleaseCommon.CanMoveNpcs || hitsLeft <= 0)
				return;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (!p.active || !p.hostile || p.damage <= 0)
					continue;
				if (Vector2.DistanceSquared(p.Center, Projectile.Center) > 52f * 52f)
					continue;
				p.Kill();
				hitsLeft--;
				blockFlash = 14;
				if (!Main.dedServ)
					MagicalFx.Impact(Projectile.Center, accent, 0.7f, Projectile.owner, 0f);
				if (hitsLeft <= 0) {
					PopAndDie();
					return;
				}
			}
		}

		private void PopAndDie()
		{
			if (!Main.dedServ) {
				YoyoParticles.SpawnRing(Projectile.Center, 6f, 76f, 16, accent, 3f);
				MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 4, 3.4f, 20, 0.7f);
			}
			Projectile.Kill();
		}

		public override bool? CanHitNPC(NPC target)
		{
			// Every deployable is meant to be felt now - the walls and the cloud
			// slice whatever walks into them instead of being pure decoration.
			if (target.CountsAsACritter)
				return false;
			return null;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Main.dedServ)
				return;
			if (mode == ModeWall || mode == ModeCloud)
				MiniYoyoFx.SlashFX(target.Center, (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX),
					accent, 0.8f, -1);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			float pulse = 1f + 0.06f * (float)Math.Sin(t2 * 0.2f);
			// Storm strikes, drawn from the flash mark left when a strike landed.
			// A bounded Skyfang column (not a 700px ruler across the screen) plus
			// a ground read, so the bolt lands *on* something.
			if (strikeFlash > 0) {
				float t = strikeFlash / 9f;
				Vector2 top = strikePos - new Vector2(0f, 320f);
				MagicalFx.Skyfang(top, strikePos, Color.Lerp(accent, Color.White, 0.35f), t,
					9f + 8f * power, (int)(strikePos.X + strikePos.Y * 3f), 3);
				MagicalFx.Shockwave(strikePos + new Vector2(0f, 6f), 170f * t,
					MathHelper.Clamp(1f - t, 0f, 1f), accent, 0.5f * t, 13f);
				MagicalFx.Splat(strikePos, 0f, 70f * t, accent * (0.5f * t));
				MiniYoyoFx.DrawGlowAdditive(strikePos, 96f * t, Color.Lerp(accent, Color.White, 0.4f), 0.34f * t);
			}
			switch (mode) {
				case ModeWall: {
					// Bulwark: a BOWED shield facing the way you rammed, built
					// from large overlapping mirror panels that taper toward the
					// tips, over a chain of soft glows. The old version was a row
					// of little chevrons sitting on a bare stroke - which is
					// exactly why it read as "a straight line".
					Vector2 fwd = Projectile.rotation.ToRotationVector2();
					Vector2 side = new(-fwd.Y, fwd.X);
					float half = 104f + 46f * power;

					// Sheen band under the panels: volume, not an outline.
					for (int i = 0; i <= 18; i++) {
						float f = i / 18f;
						float off = (f - 0.5f) * 2f;
						Vector2 p = Projectile.Center + side * (off * half) - fwd * ((1f - off * off) * 30f);
						float shimmer = 0.5f + 0.5f * (float)Math.Sin(t2 * 0.14f - off * 4.2f);
						MagicalFx.Bloom(p, (50f + 26f * (1f - Math.Abs(off))) * (0.85f + 0.3f * shimmer),
							accent, 0.13f + 0.09f * shimmer);
					}
					// Mirror panels: tallest in the middle, tapering to the tips,
					// so the barrier is a bowed bulwark rather than a plank.
					int n = 9;
					for (int i = 0; i < n; i++) {
						float f = i / (float)(n - 1);
						float off = (f - 0.5f) * 2f;
						Vector2 p = Projectile.Center + side * (off * half) - fwd * ((1f - off * off) * 30f);
						float hgt = (94f + 54f * power) * (0.52f + 0.48f * (1f - Math.Abs(off)));
						float shimmer = 0.5f + 0.5f * (float)Math.Sin(t2 * 0.14f - off * 4.2f);
						MagicalFx.Shape(ShapeId.Chevron, p, Projectile.rotation, hgt * 0.62f, hgt,
							Color.Lerp(accent, Color.White, 0.34f) * (0.34f + 0.22f * shimmer));
						// Rim light riding the leading edge.
						MagicalFx.Crescent(p + fwd * 13f, Projectile.rotation, hgt * 0.9f, accent * 0.42f);
					}
					// Hot core where the ram actually lands, plus an echo ring so
					// the shield has a front and a back.
					MagicalFx.Bloom(Projectile.Center + fwd * 28f, 100f,
						Color.Lerp(accent, Color.White, 0.35f), 0.26f);
					MagicalFx.Ring(Projectile.Center - fwd * 8f, (98f + 40f * power) * 0.55f,
						t2 * 0.01f, 0.22f, accent);
					Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.9f);
					break;
				}
				case ModeShield: {
					// Aegis dome: a layered energy bubble, not a bare wire ring.
					// Soft veil for volume, six facet plates riding the rim, two
					// counter-rotating rings, and a hard white PING whenever the
					// dome swallows a shot - a block should never be missable.
					float block = blockFlash / 14f;
					float domeR = 52f * pulse;
					MagicalFx.Shape(ShapeId.SplatSoft, Projectile.Center, 0f, domeR * 2.2f, domeR * 2.2f,
						accent * (0.13f + 0.12f * block));
					for (int i = 0; i < 6; i++) {
						float a = t2 * 0.05f + i * MathHelper.TwoPi / 6f;
						Vector2 p = Projectile.Center + a.ToRotationVector2() * domeR;
						float shimmer = 0.5f + 0.5f * (float)Math.Sin(t2 * 0.16f + i * 1.7f);
						MagicalFx.Shape(ShapeId.Chevron, p, a + MathHelper.PiOver2, 28f, 32f,
							Color.Lerp(accent, Color.White, 0.35f) * (0.26f + 0.24f * shimmer + 0.45f * block));
						MagicalFx.Bloom(p, 34f, accent, 0.09f + 0.16f * block);
					}
					MagicalFx.Ring(Projectile.Center, domeR * 0.98f, -t2 * 0.03f, 0.32f + 0.30f * block, accent);
					MagicalFx.Ring(Projectile.Center, domeR * 0.70f, t2 * 0.02f, 0.15f,
						Color.Lerp(accent, Color.White, 0.3f));
					MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 96f * pulse, accent, 0.30f + 0.30f * block);
					if (block > 0f) {
						MagicalFx.Ring(Projectile.Center, domeR * (1.1f + 0.6f * (1f - block)),
							-t2 * 0.06f, 0.55f * block, Color.White);
						MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 80f + 70f * block,
							Color.Lerp(accent, Color.White, 0.55f), 0.30f * block);
					}
					Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.7f + 0.7f * block));
					break;
				}
				case ModeCloud:
					// Soft ruffled cloud body stamped from blot art.
					MagicalFx.Shape(ShapeId.SplatSoft, Projectile.Center, 0f, 128f, 66f, new Color(52, 56, 88) * 0.55f);
					for (int i = 0; i < 5; i++) {
						float a = i * 1.2566f;
						Vector2 p = Projectile.Center
							+ new Vector2((float)Math.Cos(a) * 40f, (float)Math.Sin(a) * 13f) + new Vector2(0f, 4f);
						MagicalFx.Shape(ShapeId.SplatSoft, p, a, 84f, 52f, new Color(70, 74, 110) * 0.5f);
					}
					MagicalFx.Motes(Projectile.Center + new Vector2(0f, 18f), 56f, accent, 0.32f, 6, 0.8f);
					MiniYoyoFx.DrawGlowAdditive(Projectile.Center + new Vector2(0f, 10f), 78f, accent, 0.16f);
					break;
				case ModePortal:
					MagicalFx.Rune(Projectile.Center, 36f, t2 * 0.04f, 0.5f, accent, 1.9f);
					MagicalFx.Shape(ShapeId.RingCracked, Projectile.Center, -t2 * 0.02f, 100f, 162f, accent * 0.28f);
					MagicalFx.Converge(Projectile.Center, 48f, 12f, accent, 0.6f, 8, t2 * 0.02f, 1.2f);
					MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 60f, accent, 0.22f);
					Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.8f);
					break;
				case ModeCage: {
					float c = MathHelper.Clamp(age / 160f, 0f, 1f);
					float cr = (130f + 40f * power) * (1f - c * 0.22f);
					MagicalFx.Ring(Projectile.Center, cr * 0.9f, t2 * 0.01f, 0.22f + 0.2f * c, accent);
					for (int i = 0; i < 6; i++) {
						float a = i * MathHelper.TwoPi / 6f + t2 * 0.01f;
						Vector2 p = Projectile.Center + a.ToRotationVector2() * cr;
						MagicalFx.Shape(ShapeId.Chevron, p, a + MathHelper.PiOver2, 44f, 60f,
							Color.Lerp(accent, Color.White, 0.25f) * (0.34f + 0.26f * c));
						MagicalFx.Bloom(p, 50f, accent, 0.14f);
					}
					MagicalFx.Converge(Projectile.Center, cr * 1.2f, 20f, accent, 0.35f, 8, t2 * 0.02f, 1.2f);
					break;
				}
				default: {
					// Totem: a floating crystal over a compact art ring, threaded
					// by a beacon of stacked rising glows. The beacon used to be
					// one flat 120px bar; stacking soft glows up the column gives
					// the same read as a pillar of light without the stick.
					float beacon = 0.75f + 0.25f * (float)Math.Sin(t2 * 0.05f);
					for (int i = 0; i <= 7; i++) {
						float f = i / 7f;
						float w = (1f - f) * (1f - f);
						Vector2 p = Projectile.Center + new Vector2(
							(float)Math.Sin(t2 * 0.03f + f * 4.4f) * 3.4f, -f * 118f);
						MagicalFx.Bloom(p, (34f + 24f * w) * (0.7f + 0.4f * beacon), accent,
							0.05f + 0.15f * w * beacon);
					}
					// Ground halo: the crystal is anchored to something.
					MagicalFx.Splat(Projectile.Center + new Vector2(0f, 20f), 0f, 54f * pulse, accent * 0.16f);
					MagicalFx.Ring(Projectile.Center, 40f * pulse, t2 * 0.01f, 0.3f, accent);
					MagicalFx.Shape(ShapeId.FlakeSix, Projectile.Center, t2 * 0.02f, 92f * pulse, 92f * pulse,
						Color.Lerp(accent, Color.White, 0.45f));
					MagicalFx.StarFlare(Projectile.Center, accent, 26f, 0.4f, 0.6f);
					MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 84f * pulse, accent, 0.4f);
					Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.9f);
					break;
				}
			}
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			if (Main.dedServ)
				return;
			YoyoParticles.SpawnRing(Projectile.Center, 8f, 40f, 14, accent, 2.4f);
			MagicalFx.Fan(ShapeId.SplatSoft, Projectile.Center, accent, 3, 2.6f, 20, 0.6f);
		}
	}

	/// <summary>
	/// Link between you and an enemy (or a point). ai[0] = mode, ai[1] = power01,
	/// ai[2] = source identity. Modes: 0 leech (drain + mend), 1 drag (reel them
	/// in), 2 siphon (drain, no mend).
	/// </summary>
	public class MagicalTetherProj : ModProjectile
	{
		public const float ModeLeech = 0f;
		public const float ModeDrag = 1f;
		public const float ModeSiphon = 2f;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private float mode;
		private int age;
		private Color accent = new(150, 180, 255);
		private float explicitPower;
		private int biteFlash;

		public override void SetDefaults()
		{
			Projectile.width = 12;
			Projectile.height = 12;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 20;
			Projectile.timeLeft = 240;
		}

		public override void AI()
		{
			mode = Projectile.ai[0];
			explicitPower = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			age++;
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			Player owner = Owner();
			if (owner == null || !owner.active || owner.dead) {
				Projectile.Kill();
				return;
			}

			// The tether lives at the midpoint and tracks its anchor NPC.
			NPC anchor = Anchor();
			if (anchor == null || !anchor.active) {
				Projectile.Kill();
				return;
			}
			Projectile.Center = (owner.Center + anchor.Center) * 0.5f;
			Projectile.velocity = Vector2.Zero;

			// Draining life into the wielder. The Strike() lock keeps the leech
			// to one bite per window and pops a hit reaction on the victim; the
			// bite also flashes the cord so a drain is never invisible. The bite
			// is a real hit on the yoyo's damage, not a token tick.
			if (biteFlash > 0)
				biteFlash--;
			if (age % 12 == 0 && MagicalReleaseCommon.CanMoveNpcs
				&& MagicalReleaseCommon.Strike(owner, anchor, Math.Max(1, (int)(Projectile.damage * 0.6f)), 0.6f,
					accent, Projectile.Center)) {
				biteFlash = 10;
				if (mode == ModeLeech && Projectile.owner == Main.myPlayer && owner.statLife < owner.statLifeMax2)
					owner.statLife = Math.Min(owner.statLifeMax2, owner.statLife + 3);
				if (mode != ModeLeech)
					anchor.AddBuff(BuffID.BrokenArmor, 60);
				if (mode == ModeLeech && !Main.dedServ)
					MagicalFx.CastFlash(owner.Center, accent, 0.4f);
			}
			// Movement: ONLY Drag pulls the victim. The old code also added a
			// constant velocity to Leech/Siphon, which slid a training dummy's
			// hitbox out from under its sprite (per-tick netUpdate corrections
			// rubber-banded the hitbox away and back). A drain is damage plus
			// healing, not a grapple - and its real knockback already comes from
			// Strike -> SimpleStrikeNPC, the channel the game syncs for NPCs.
			if (mode == ModeDrag && MagicalReleaseCommon.CanMoveNpcs && !DummySafety.Pin(anchor)) {
				Vector2 dir = (owner.Center - anchor.Center).SafeNormalize(Vector2.Zero);
				anchor.velocity += dir * 0.42f;
				float cap = 7f;
				if (anchor.velocity.LengthSquared() > cap * cap)
					anchor.velocity = anchor.velocity.SafeNormalize(Vector2.Zero) * cap;
				// Sync on a cadence. netUpdate every tick is what made the
				// server correction and the pull fight each other in MP.
				if (age % 12 == 0)
					anchor.netUpdate = true;
			}

			Lighting.AddLight(Projectile.Center, accent.ToVector3() * 0.6f);
		}

		private Player Owner()
		{
			if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
				return null;
			Player p = Main.player[Projectile.owner];
			return p != null && p.active ? p : null;
		}

		/// <summary>Anchor: the NPC nearest the owner at spawn, kept by identity.</summary>
		private NPC Anchor()
		{
			// Projectile.ai[0] holds the mode, so the anchor is the closest live
			// enemy to the owner - stable enough for a short link.
			NPC best = null;
			float bestD = 900f * 900f;
			Player owner = Owner();
			Vector2 from = owner != null ? owner.Center : Projectile.Center;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc))
					continue;
				float d = Vector2.DistanceSquared(npc.Center, from);
				if (d < bestD) {
					bestD = d;
					best = npc;
				}
			}
			return best;
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			Player owner = Owner();
			NPC anchor = Anchor();
			if (owner == null || anchor == null)
				return false;
			Vector2 a = owner.Center;
			Vector2 b = anchor.Center;
			// A sagging ribbon, not a straight line, with siphon comets running
			// along it: the direction flips while the tether is reeling them in.
			Vector2 mid = (a + b) * 0.5f + new Vector2(0f, -26f - 20f * explicitPower);
			float w = 5.4f + 3f * explicitPower;
			Vector2 prev = a;
			for (int i = 1; i <= 7; i++) {
				float f = i / 7f;
				Vector2 p = MiniYoyoFx.Bez2(a, mid, b, f);
				float pw = w * (1f - 0.55f * (i - 1) / 7f);
				float cw = w * (1f - 0.55f * f);
				MiniYoyoFx.DrawTaperedStroke(prev, pw, p, cw, accent, 0.30f);
				prev = p;
			}
			// Every mode FEEDS YOU: the siphons run from the victim back down the
			// cord to the wielder, so the drain direction is unmistakable.
			for (int k = 0; k < 2; k++) {
				float f = 1f - (Main.GameUpdateCount * 0.02f + k * 0.5f) % 1f;
				Vector2 p = MiniYoyoFx.Bez2(a, mid, b, f);
				Vector2 pPrev = MiniYoyoFx.Bez2(a, mid, b, Math.Min(1f, f + 0.06f));
				float head = (float)Math.Sin(f * Math.PI);
				MagicalFx.Wisp(p, (p - pPrev).ToRotation(), 34f, 13f,
					Color.Lerp(accent, Color.White, 0.4f) * (0.55f * (0.35f + 0.65f * head)));
				MagicalFx.Bloom(p, 20f, accent, 0.2f * head);
			}
			// Bite. This used to stack a rune + a bloom + a star flare + a second
			// bloom on the SAME point at ALL times - three effects arguing over
			// one spot, which is clutter rather than detail. Calamity's line
			// language (StaticGlowLine / BloomLineVFX / BloomLineCap) is the
			// opposite: one element, well weighted, plus a cap. So the victim now
			// carries a calm seam at rest and flares only while a drain is
			// actually landing - which also makes every tick readable instead of
			// decorative.
			float bite = biteFlash / 10f;
			MagicalFx.Bloom(b, 30f + 46f * bite, Color.Lerp(accent, Color.White, 0.35f), 0.18f + 0.5f * bite);
			if (bite > 0.01f) {
				MagicalFx.Rune(b, 22f + 14f * bite, Main.GameUpdateCount * 0.04f, 0.5f * bite, accent);
				MagicalFx.StarFlare(b, Color.Lerp(accent, Color.White, 0.5f), 22f + 30f * bite,
					Main.GameUpdateCount * 0.09f, 0.6f * bite);
			}
			MagicalFx.Bloom(a, 26f + 24f * bite, accent, 0.16f + 0.3f * bite);
			return false;
		}
	}

	/// <summary>
	/// The shared cast language of the whole ability library. Every release -
	/// beam or custom - stamps a glyph at the cursor, drags a light arc from
	/// the caster to that glyph and kicks the camera; the weight of all three
	/// grows with the source yoyo's progression tier. One hook in
	/// <see cref="MagicalReleaseProfile.OnChargeComplete"/> gives the entire
	/// library this, including abilities written long before it existed.
	/// </summary>
	public static class MagicalCast
	{
		public static void Kick(MagicalAbilityProfile profile, in MagicalAbilityContext ctx)
		{
			if (Main.dedServ || profile == null || ctx.Owner == null
				|| ctx.Owner.whoAmI != Main.myPlayer)
				return;
			Vector2 at = CastPoint(ctx);
			Color accent = profile.AccentColor(ctx.SourceProjType);
			int family = StableSeed(profile.GetType().Name) % 7;
			int type = ModContent.ProjectileType<MagicalCastSigilProj>();
			MagicalReleaseCommon.Spawn(ctx.Owner, at, at - ctx.Center, type, 1,
				family, ctx.Tier01, ctx.SourceProjType);

			float weight = 0.55f + 0.45f * ctx.Tier01;
			MiniYoyoFx.FlashFX(at, Color.White, 0.35f + 0.45f * weight);
			MiniYoyoFx.Shake(2 + (int)(2f * weight), 6 + (int)(3f * weight), at);
			MagicalFx.ScreenPulse(at, 0.08f + 0.14f * weight, accent);
			SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.22f, Pitch = 0.55f - 0.4f * ctx.Tier01 }, at);
		}

		/// <summary>Where the release will land: the cursor, clamped to a sane
		/// cast range; or a point along the aim for non-owner replays.</summary>
		private static Vector2 CastPoint(in MagicalAbilityContext ctx)
		{
			if (ctx.Owner != null && ctx.Owner.whoAmI == Main.myPlayer) {
				Vector2 off = Main.MouseWorld - ctx.Center;
				float len = MathHelper.Clamp(off.Length(), 48f, 640f);
				return ctx.Center + off.SafeNormalize(ctx.Aim) * len;
			}
			return ctx.Center + ctx.Aim.SafeNormalize(Vector2.UnitX) * 280f;
		}

		/// <summary>Stable across runs and platforms: the glyph family (a shape
		/// language per ability class) so the same yoyo always stamps the same
		/// sigil.</summary>
		private static int StableSeed(string name)
		{
			int seed = 7;
			if (name != null) {
				foreach (char c in name)
					seed = (seed * 31 + c) & 0x3fffffff;
			}
			return seed;
		}
	}

	/// <summary>
	/// The cursor glyph: a short-lived sigil stamped where a release lands,
	/// connected to the caster by a light arc. Purely cosmetic (the payload
	/// carries the damage); seven shape families keep every ability's stamp
	/// recognisable at a glance. ai[0] family, ai[1] tier01, ai[2] source.
	/// </summary>
	public class MagicalCastSigilProj : ModProjectile
	{
		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private int age;
		private float tier;
		private Color accent = new(180, 180, 255);
		private Vector2 from;
		private Vector2 at;

		private int Family => (int)Projectile.ai[0];

		public override void SetDefaults()
		{
			Projectile.width = 8;
			Projectile.height = 8;
			Projectile.friendly = false;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.timeLeft = 32;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
		}

		public override void OnSpawn(IEntitySource source)
		{
			// Spawn convention: velocity points from the caster to the glyph.
			from = Projectile.Center - Projectile.velocity;
			if (Projectile.velocity.LengthSquared() < 0.01f)
				from = Projectile.Center + new Vector2(0f, -48f);
			at = Projectile.Center;
			Projectile.velocity = Vector2.Zero;
		}

		public override void AI()
		{
			age++;
			tier = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
			accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			if (age == 1 && !Main.dedServ) {
				MagicalFx.CastFlash(at, accent, 0.5f);
				YoyoParticles.SpawnRing(at, 6f, 92f + 40f * tier, 14, accent, 2.6f);
			}
			Lighting.AddLight(at, accent.ToVector3() * (0.75f + 0.35f * tier));
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (Main.dedServ)
				return false;
			float t2 = Main.GameUpdateCount;
			// Envelope: strike in over the first four ticks, hold, then let go.
			float inK = MathHelper.Clamp(age / 4f, 0f, 1f);
			float outK = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
			float k = Math.Min(inK, outK);
			if (k <= 0.01f)
				return false;
			Color hot = Color.Lerp(accent, Color.White, 0.55f);
			float size = (0.8f + 0.5f * tier) * (0.7f + 0.3f * inK);
			float grow = 1f - outK;

			// The connector: NOT the same arm every time. Each glyph family
			// reaches its mark a different way, so a cast is recognisable by the
			// line it draws as much as by the stamp it leaves.
			Vector2 d = at - from;
			float len = d.Length();
			if (len > 40f) {
				Vector2 dir = d.SafeNormalize(Vector2.UnitX);
				switch (Family) {
					case 1: // star - a straight lance the star rides in on
						MiniYoyoFx.DrawSoftBar(from + dir * (len * 0.5f), d.ToRotation(), len,
							(7f + 5f * tier) * k, accent, 0.30f * k);
						MagicalFx.StarFlare(from + dir * (len * 0.5f), accent, len * 0.85f,
							d.ToRotation(), 0.18f * k);
						break;
					case 2: // blades - a double arc, the pair bowed apart
						for (int b = -1; b <= 1; b += 2) {
							Vector2 mid = (from + at) * 0.5f
								+ dir.RotatedBy(MathHelper.PiOver2) * (len * 0.09f * b);
							Vector2 prev = from;
							for (int i = 1; i <= 12; i++) {
								float f = i / 12f;
								Vector2 p = MiniYoyoFx.Bez2(from, mid, at, f);
								float w = (4.5f + 3.5f * tier) * (float)Math.Sin(f * Math.PI) * k;
								MiniYoyoFx.DrawTaperedStroke(prev, w * 0.8f, p, w, accent, 0.34f * k);
								prev = p;
							}
						}
						break;
					case 3: // void lens - no connector at all: it hangs in space
						break;
					case 4: // frost - a thin line seeded with ice flakes
						MiniYoyoFx.DrawSoftBar((from + at) * 0.5f, d.ToRotation(), len,
							(4f + 3f * tier) * k, Color.Lerp(accent, Color.White, 0.4f), 0.26f * k);
						for (int i = 1; i <= 4; i++) {
							float f = i / 5f;
							MagicalFx.Shape(ShapeId.FlakeSix, from + d * f, 0f,
								10f + 6f * tier, 10f + 6f * tier, accent * (0.4f * k));
						}
						break;
					case 5: // storm - a jagged chain of arcs
						for (int i = 0; i < 5; i++) {
							Vector2 a0 = from + d * (i / 5f);
							Vector2 a1 = from + d * ((i + 1) / 5f)
								+ dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-14f, 14f);
							MagicalFx.Bolt(a0, a1, Color.Lerp(accent, Color.White, 0.3f),
								0.5f * k, (1.6f + tier) * k, i * 37);
						}
						break;
					case 6: // ember - a broken trail of curls with gaps
						for (int i = 0; i < 6; i += 2) {
							float f = (i + 0.5f) / 6f;
							MagicalFx.Shape(ShapeId.EmberCurl,
								from + d * f - new Vector2(0f, 6f * (float)Math.Sin(f * Math.PI)),
								d.ToRotation() + i, 22f + 10f * tier, 12f, accent * (0.42f * k));
						}
						break;
					default: { // arcane seal - the original bowed ribbon
						Vector2 mid = (from + at) * 0.5f
							+ dir.RotatedBy(MathHelper.PiOver2) * (len * 0.09f);
						const int segs = 16;
						Vector2 prev = from;
						for (int i = 1; i <= segs; i++) {
							float f = i / (float)segs;
							Vector2 p = MiniYoyoFx.Bez2(from, mid, at, f);
							float w = (5.5f + 4.5f * tier) * (float)Math.Sin(f * Math.PI) * k;
							MiniYoyoFx.DrawTaperedStroke(prev, w * 0.8f, p, w, accent, 0.42f * k);
							prev = p;
						}
						break;
					}
				}
				MagicalFx.Bloom(from, 34f + 20f * tier, accent, 0.30f * k);
			}

			// The glyph itself: one shape family per ability, expanding outward.
			float r = (26f + 20f * tier) * (1f + 0.55f * grow);
			switch (Family) {
				case 1: // star
					MagicalFx.Shape(ShapeId.SplatStar, at, t2 * 0.02f, r * 1.5f, r * 1.5f, hot * (0.75f * k));
					MagicalFx.StarFlare(at + new Vector2(0f, 2f), Color.White, r * 0.9f, 0.75f, 0.5f * k);
					break;
				case 2: // blades
					for (int i = 0; i < 4; i++) {
						float a = 0.78f + i * MathHelper.PiOver2;
						MagicalFx.Shape(ShapeId.SmearCrescent,
							at + (a + MathHelper.Pi).ToRotationVector2() * (r * 0.72f),
							a + MathHelper.PiOver2, r * 1.1f, r * 0.62f, hot * (0.6f * k));
					}
					break;
				case 3: // void lens
					MiniYoyoFx.DrawSolidEllipse(at, r * 0.85f, r * 0.85f, 0f, new Color(10, 6, 18), 0.7f * k);
					MagicalFx.Shape(ShapeId.RingCracked, at, t2 * 0.02f, r * 1.35f, r * 1.35f, accent * (0.6f * k));
					break;
				case 4: // frost
					for (int i = 0; i < 6; i++) {
						float a = i * MathHelper.Pi / 3f;
						MagicalFx.Shape(ShapeId.FlakeSix, at + a.ToRotationVector2() * (r * 0.6f),
							a, r * 0.7f, r * 0.7f, hot * (0.5f * k));
					}
					break;
				case 5: // storm
					for (int i = 0; i < 3; i++) {
						float a0 = i * MathHelper.TwoPi / 3f + 0.5f;
						float a1 = a0 + 2.2f;
						MagicalFx.Bolt(at + a0.ToRotationVector2() * (r * 0.9f),
							at + a1.ToRotationVector2() * (r * 0.9f), hot, 0.5f * k, 1.8f, i * 53);
					}
					break;
				case 6: // ember
					for (int i = 0; i < 5; i++) {
						float a = i * MathHelper.TwoPi / 5f + 0.3f;
						MagicalFx.Shape(ShapeId.EmberCurl,
							at + a.ToRotationVector2() * (r * 0.62f) - new Vector2(0f, 6f),
							a, r * 0.8f, r * 0.55f, accent * (0.55f * k));
					}
					break;
				default: // arcane seal
					MagicalFx.Shape(ShapeId.SealRune, at, t2 * 0.015f, r * 1.4f, r * 1.4f, hot * (0.65f * k));
					MagicalFx.StarFlare(at, Color.White, r * 0.6f, 0.4f, 0.4f * k);
					break;
			}

			MiniYoyoFx.DrawGlowAdditive(at, r * 2.2f, accent, 0.28f * k);
			MiniYoyoFx.DrawGlowAdditive(at, r * 0.9f, Color.White, 0.35f * k);
			return false;
		}
	}
}
