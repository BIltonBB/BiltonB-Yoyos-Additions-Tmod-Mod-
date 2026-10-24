using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Buffs;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Content.Projectiles.Tech
{
	/// <summary>
	/// The lightning archetype: a bolt races out of the charged core, always born
	/// aimed at the cursor. Three flavours, chosen by ai[0]:
	///
	///   Straight      - keeps going through everything, damaging along the path.
	///   Chain         - keeps flying straight; when it BITES an enemy it spawns
	///                   ONE sub-lightning at the bite, aimed at the nearest
	///                   enemy the family has not bitten yet. The original
	///                   carries on exactly as it was.
	///   CompleteChain - the original stops dead on its victim (holding there
	///                   for a beat); its sub-lightning is a normal CHAIN bolt,
	///                   so the sub-lightning is the thing that continues and
	///                   forks again. (A CompleteChain sub-bolt used to inherit
	///                   CompleteChain, which made every link park and die at
	///                   its first victim - the chain visually stopped after
	///                   one hop.)
	///
	/// This is Notes5's rule verbatim: "chain is like when the lightning hits an
	/// enemy, it hops to the nearest enemy in a radius as a sub lightning while
	/// the original lightning continues its path. Complete chain is chain but the
	/// original lightning doesnt continue and its sub lightning is the only one
	/// that continues."
	///
	/// Sub-bolts are spawned ONLY by a landed bite - that is the difference
	/// between a chain and the earlier "spread": an old build forked at CAST
	/// time, so a bolt visibly burst into several strands before it had touched
	/// anything. Now nothing forks without a hit, one child per hit, and a
	/// cast is capped at <see cref="MaxGenerations"/> links.
	///
	/// Re-hitting is prevented outright: the bolt remembers every npc it has
	/// bitten and CanHitNPC refuses those for the rest of its life, and every
	/// child INHERITS that list, so a chain can never double back onto a body
	/// the family already struck (a sub-bolt is born inside the enemy its
	/// parent just hit - without the inheritance it would immediately re-bite
	/// it, which is what made chaining read as "the same enemy over and over").
	///
	/// The flight is a straight copy of the mod's own Lunatic bolt: it HOPS.
	/// Every 6 ticks Center jumps StepMin..StepMax forward plus a
	/// SideOffsetMin..SideOffsetMax lateral kick off the locked bearing, and the
	/// polyline of those jump points IS the bolt. Nothing is tweened, there is
	/// no velocity, and the whole route is swept for collision, so a hop bites
	/// everything it passed.
	/// </summary>
	public class MagicalLightningProj : ModProjectile
	{
		public const float ModeStraight = 0f;
		public const float ModeChain = 1f;
		public const float ModeCompleteChain = 2f;
		// Ability variants. They are still Straight/Chain at their core:
		// Relay is a Straight bolt that plants a return-strike node; Faraday
		// and Polarity are Chain bolts that leave anchors for their finisher.
		public const float ModeRelay = 3f;
		public const float ModeFaraday = 4f;
		public const float ModePolarity = 5f;

		/// <summary>Travel speed of every bolt (pixels per tick).</summary>
		public const float BoltSpeed = 21f;
		/// <summary>How far a chaining bolt looks for its next victim.</summary>
		public const float HopRadius = 340f;
		/// <summary>Cap on chain links: the original plus this many sub-bolts.</summary>
		public const int MaxGenerations = 3;
		/// <summary>Flight time before a bolt fizzles out.</summary>
		public const int MaxLife = 60;

		/// <summary>Flight time a bolt is granted every time it links.</summary>
		private const int LinkLifeBonus = 16;
		/// <summary>How long a complete-chain bolt holds on its target.</summary>
		private const int CompleteChainHold = 9;
		/// <summary>Longest route drawn, in points. The Lunatic caps at 32 too.</summary>
		private const int MaxTrailPoints = 32;
		/// <summary>Bolt thins out over its last half second, like the Lunatic's.</summary>
		private const int FadeTicks = 30;

		// ---- Lunatic cadence (copied from LunaticYoyoProjL) -------------------
		// That bolt does not glide, it HOPS. Every 6 ticks its Center JUMPS
		// 55-110px along its locked direction with a 10-24px lateral offset on
		// the jump, and the polyline of those jump points IS the bolt: nothing is
		// tweened, there is no velocity, and between hops it is stationary. That
		// discrete re-commit is why it reads as lightning - each leg is a
		// straight jump at a visibly different bearing from the last, so the
		// silhouette is jagged by construction. (A bolt that only re-aims while
		// gliding draws a smooth curve, i.e. still looks like it goes straight.)
		// These are the Lunatic's own numbers; the only change is that the
		// offsets come from this bolt's seed instead of Main.rand, because this
		// bolt has to aim as well as look right.
		private const int HopInterval = 6;
		private const float StepMin = 55f;
		private const float StepMax = 110f;
		private const float SideOffsetMin = 10f;
		private const float SideOffsetMax = 24f;
		/// <summary>Collision pad around a path segment (the Lunatic's HitRadius).</summary>
		private const float HitRadius = 8f;

		public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

		private readonly List<int> struck = new();
		private readonly List<Vector2> trail = new();
		private float seed;
		private bool born;
		private int pauseTicks;
		/// <summary>Ticks flown in the current leg; a new leg is committed at HopInterval.</summary>
		private int hopTimer;
		/// <summary>Legs flown so far (drives the deterministic side kick).</summary>
		private int hopIndex;
		/// <summary>Bearing the weave hangs off: the aim line, or the latest link.</summary>
		private Vector2 legDir = Vector2.UnitX;
		/// <summary>Bearing captured when the bolt was born (synced via velocity).</summary>
		private float castAngle;

		private float Power01 => MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
		private float Mode => Projectile.ai[0];
		private bool Chaining => Mode == ModeChain || Mode == ModeCompleteChain
			|| Mode == ModeFaraday || Mode == ModePolarity;

		// ---- Chain depth ----------------------------------------------------
		// ai[2] carries the caster's source projectile type and is read as
		// (int)ai[2] for the accent, so the link depth rides in its FRACTION: a
		// sub-bolt is spawned with ai[2] = srcType + 0.25 * depth, which
		// truncates back to srcType for every other reader and still gives each
		// generation a value that survives the network (ai is synced; localAI
		// is not, so it could never hold a chain depth).
		private float SrcType => MathF.Floor(Projectile.ai[2]);
		private int Generation => (int)MathF.Round((Projectile.ai[2] - SrcType) * 4f);
		private float ChildAi2 => SrcType + 0.25f * (Generation + 1);

		// (A length-bounded draw used to live here: the bolt was a short dart
		// because drawing a GLIDING bolt's whole route smeared into one loose
		// line. A HOPPED route is jagged, so this bolt keeps its whole route the
		// way the Lunatic does - the cap is MaxTrailPoints, not a length.)

		public override void SetDefaults()
		{
			Projectile.width = 18;
			Projectile.height = 18;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.penetrate = -1;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.netImportant = true;
			Projectile.timeLeft = MaxLife;
			Projectile.DamageType = DamageClass.MeleeNoSpeed;
			Projectile.usesLocalNPCImmunity = true;
			// One bite per enemy per bolt. Long enough that a bolt which bends back
			// across a crowd cannot chew the same body twice.
			Projectile.localNPCHitCooldown = 40;
			// Fire the first hop on the first AI tick: the bolt is born already
			// moving (a strike, not a mote that sits at the core for six ticks).
			hopTimer = HopInterval - 1;
		}

		public override void AI()
		{
			if (!born) {
				born = true;
				seed = Projectile.identity * 7.31f;
				trail.Clear();
				trail.Add(Projectile.Center);
				// The aim line the whole weave is hung off. A bolt is always
				// born flying at the cursor, so the first leg is exactly that.
				legDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
				castAngle = legDir.ToRotation();
				if (!Main.dedServ)
					SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
			}

			Color accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			Lighting.AddLight(Projectile.Center, accent.ToVector3() * (0.55f + 0.35f * Power01));

			// A discharge is alive: sparks shed off the head the whole flight.
			// They trail AGAINST the leg direction (velocity is always zero now,
			// so the bias comes from the leg the bolt is flying).
			if (!Main.dedServ && Main.rand.NextBool(2)) {
				Vector2 v = -legDir * Main.rand.NextFloat(0.6f, 2.2f) + Main.rand.NextVector2Circular(1.6f, 1.6f);
				YoyoParticles.SpawnSpark(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), v,
					accent, 0.8f + 0.3f * Power01, 16, 0.94f);
			}

			// A complete-chain bolt stops DEAD on its victim and holds there for a
			// beat - visibly biting while the sub-lightning it just spawned flies
			// on - and then it dies. "Complete chain is chain but the original
			// lightning doesnt continue and its sub lightning is the only one
			// that continues." (It used to hold and then leap itself, which is
			// just Chain with a delay.)
			if (pauseTicks > 0) {
				pauseTicks--;
				Projectile.velocity = Vector2.Zero;
				Projectile.rotation = legDir.ToRotation();
				if (!Main.dedServ && Main.rand.NextBool(2)) {
					YoyoParticles.SpawnSpark(Projectile.Center + legDir * Main.rand.NextFloat(4f, 22f),
						legDir * Main.rand.NextFloat(1f, 3f), accent, 1f, 18, 0.92f);
				}
				RecordTrail();
				if (pauseTicks == 0) {
					ImpactFx(Projectile.Center);
					Projectile.Kill();
				}
				return;
			}

			// THE LUNATIC CADENCE: hold, hop, hold, hop. The bolt carries no
			// velocity at all - Center is moved in whole jumps, so between hops
			// it is genuinely stationary. (Gliding while re-aiming the heading is
			// what the first attempt did: that just draws a smooth curve, which
			// is why every bolt still looked like it went straight forward.)
			Projectile.velocity = Vector2.Zero;
			Projectile.rotation = legDir.ToRotation();

			hopTimer++;
			if (hopTimer >= HopInterval) {
				hopTimer = 0;
				Hop(accent);
				RecordTrail();
			}
		}

		/// <summary>
		/// One Lunatic hop: a whole jump, nothing tweened. Forward StepMin..StepMax
		/// plus a SideOffsetMin..SideOffsetMax lateral kick off the leg's bearing,
		/// sign re-rolled every hop - the exact move LunaticYoyoProjL.MoveLightning
		/// makes. The offsets are hashed off this bolt's seed instead of Main.rand
		/// so every client hops the identical route (the Lunatic can afford random
		/// offsets because nothing aims with it; this bolt does aim).
		/// </summary>
		private void Hop(Color accent)
		{
			hopIndex++;
			Vector2 perp = new(-legDir.Y, legDir.X);
			float step = MathHelper.Lerp(StepMin, StepMax, Hash01(seed + hopIndex * 12.9898f));
			float side = MathHelper.Lerp(SideOffsetMin, SideOffsetMax, Hash01(seed * 1.37f + hopIndex * 78.233f));
			if (Hash01(seed + hopIndex * 3.71f) < 0.5f)
				side = -side;
			// Leg 1 is the aim line exactly: a bolt has to be seen to leave the
			// core along the direction it was cast before it starts to crackle.
			if (hopIndex <= 1)
				side = 0f;

			Vector2 from = Projectile.Center;
			Vector2 to = from + legDir * step + perp * side;

			// A stationary bolt never trips the engine's own tile collision, so
			// the JUMP is swept here: a wall stops it mid-leap instead of letting
			// it hop through.
			int samples = Math.Max(1, (int)(Vector2.Distance(from, to) / 8f));
			for (int s = 1; s <= samples; s++) {
				Vector2 p = Vector2.Lerp(from, to, s / (float)samples);
				if (Collision.SolidCollision(p - Projectile.Size * 0.5f, Projectile.width, Projectile.height)) {
					Projectile.Center = p;
					ImpactFx(p);
					Projectile.Kill();
					return;
				}
			}

			Projectile.Center = to;
			Projectile.rotation = legDir.ToRotation();

			if (Main.dedServ)
				return;
			// Corner read: the shape is re-read from HERE, so every kink gets its
			// own strike - a bloom where the bolt landed, a flare along the leg it
			// just flew, and the sparks it sheds.
			MagicalFx.Bloom(Projectile.Center, 26f + 16f * Power01, Color.Lerp(accent, Color.White, 0.62f), 0.24f);
			// QueueStarFlare, not StarFlare: this runs from AI, and StarFlare
			// stamps into the live sprite batch (calling it here threw "Draw was
			// called, but Begin has not yet been called", which aborted the rest
			// of that tick's AI - the trail record and collision route after it).
			MagicalFx.QueueStarFlare(Projectile.Center, Color.Lerp(accent, Color.White, 0.6f),
				11f + 7f * Power01, legDir.ToRotation(), 0.22f);
			if (Main.rand.NextBool(2))
				YoyoParticles.SpawnSpark(Projectile.Center,
					-legDir * Main.rand.NextFloat(0.8f, 2.2f) + Main.rand.NextVector2Circular(1.1f, 1.1f),
					accent, 0.75f, 12, 0.94f);
		}

		/// <summary>
		/// One bite per enemy per bolt, for good. The strike now damages along the
		/// whole recorded route (see Colliding), and the route lives as long as the
		/// bolt does - so a 40-tick local cooldown would let a body sitting on an
		/// early hop be bitten a second time. The struck list already existed to
		/// stop chains doubling back; this makes it absolute.
		/// </summary>
		public override bool? CanHitNPC(NPC target) => !struck.Contains(target.whoAmI);

		/// <summary>
		/// Path collision, exactly like LunaticYoyoProjL.Colliding(): the bolt
		/// damages along the WHOLE recorded polyline, not just where its head is.
		/// A hopping bolt teleports 55-110px at a time, so hitbox-only collision
		/// would step straight over anything between two points; sweeping the
		/// segments is what makes a hop bite everything it passed.
		/// </summary>
		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			Rectangle box = targetHitbox;
			box.Inflate((int)HitRadius, (int)HitRadius);
			for (int i = 0; i < trail.Count; i++) {
				if (box.Contains(trail[i].ToPoint()))
					return true;
				if (i > 0 && SegmentHitsRect(trail[i - 1], trail[i], box))
					return true;
			}
			return false;
		}

		/// <summary>Liang-Barsky slab clip: does segment ab cross the box?</summary>
		private static bool SegmentHitsRect(Vector2 a, Vector2 b, Rectangle r)
		{
			float dx = b.X - a.X, dy = b.Y - a.Y;
			float t0 = 0f, t1 = 1f;
			float[] p = { -dx, dx, -dy, dy };
			float[] q = { a.X - r.Left, r.Right - a.X, a.Y - r.Top, r.Bottom - a.Y };
			for (int i = 0; i < 4; i++) {
				if (Math.Abs(p[i]) < 1e-6f) {
					if (q[i] < 0f)
						return false;
					continue;
				}
				float t = q[i] / p[i];
				if (p[i] < 0f) {
					if (t > t1)
						return false;
					if (t > t0)
						t0 = t;
				}
				else {
					if (t < t0)
						return false;
					if (t < t1)
						t1 = t;
				}
			}
			return true;
		}

		/// <summary>Stable 0..1 hash. Same value on every client, no shared RNG.</summary>
		private static float Hash01(float v)
		{
			float s = MathF.Sin(v * 12.9898f) * 43758.5453f;
			return s - MathF.Floor(s);
		}

		/// <summary>
		/// Keep the recorded route inside the draw budget. The route IS the hop
		/// points, so there is nothing to sample - only the count is capped
		/// (which is exactly what the Lunatic does with its 32-point path).
		/// </summary>
		private void RecordTrail()
		{
			// One point per hop, nothing sampled. This is the only place the route
			// is written, so the drawn polyline and the collision polyline can
			// never disagree - the hop that moves Center is the hop that is
			// recorded, on every client and on the server.
			if (trail.Count == 0 || Vector2.DistanceSquared(trail[^1], Projectile.Center) > 0.01f)
				trail.Add(Projectile.Center);
			if (trail.Count > MaxTrailPoints)
				trail.RemoveRange(0, trail.Count - MaxTrailPoints);
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			ImpactFx(Projectile.Center);
			return true;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			bool firstBite = !struck.Contains(target.whoAmI);
			if (firstBite)
				struck.Add(target.whoAmI);

			// Decide the discharge before drawing, so the impact is ONE effect.
			// The old path drew ImpactFx's Pop, then another Pop plus a ring in
			// this branch: three overlapping circular blooms on the same pixel.
			bool staticPayoff = firstBite
				&& target.HasBuff(ModContent.BuffType<StaticChargeBuff>());
			ImpactFx(target.Center, staticPayoff);

			Player hitOwner = Main.player[Projectile.owner];

			// Static Trail's payoff: the first lightning hit on a marked enemy
			// consumes the mark and discharges one short arc to the next enemy
			// in the chain's not-yet-bitten set. The +25% damage is applied by
			// StaticChargeGlobalNPC.ModifyHitByProjectile.
			if (staticPayoff) {
				ClearStaticCharge(target);
				// The normal chain fork will take the nearest victim. Send the
				// Static Charge arc to a DIFFERENT victim so one ability instance
				// never puts two bolts into the same enemy on the same beat.
				int chainNext = FindNextVictim(target.Center);
				int arcTarget = FindNextVictim(target.Center, chainNext);
				if (arcTarget >= 0) {
					Vector2 arcDir = (Main.npc[arcTarget].Center - target.Center).SafeNormalize(legDir);
					int arc = MagicalReleaseCommon.SpawnChild(hitOwner, target.Center,
						arcDir * BoltSpeed, Projectile.type,
						Math.Max(1, (int)(Projectile.damage * 0.55f)),
						ModeStraight, Power01, ChildAi2);
					if (arc >= 0 && arc < Main.maxProjectiles) {
						if (Main.projectile[arc].ModProjectile is MagicalLightningProj arcBolt)
							arcBolt.InheritChain(struck);
						Main.projectile[arc].netUpdate = true;
					}
				}
			}

			// Relay Storm is a Straight bolt whose hits leave a small storm cell.
			// The cell fires its return bolt along the captured cast bearing, not
			// a per-client cursor position, so multiplayer stays deterministic.
			if (firstBite && Mode == ModeRelay && hitOwner != null && struck.Count <= 4) {
				// castAngle comes from the bolt's synced initial velocity, so
				// this is identical on the owner, server and every client.
				float angle = castAngle;
				MagicalReleaseCommon.SpawnChild(hitOwner, target.Center, Vector2.Zero,
					ModContent.ProjectileType<MagicalRelayNodeProj>(),
					Math.Max(1, (int)(Projectile.damage * 0.35f)), angle, Power01, SrcType);
			}

			// Faraday and Polarity are Chain bolts that leave one anchor per bite.
			// Their field controller draws the edges/finisher; the bolt itself
			// still obeys the normal Chain rule (original continues, one child).
			if (firstBite && (Mode == ModeFaraday || Mode == ModePolarity)) {
				float fieldMode = Mode == ModePolarity
					? MagicalChainFieldProj.ModePolarity : MagicalChainFieldProj.ModeFaraday;
				MagicalReleaseCommon.SpawnChild(hitOwner, target.Center, Vector2.Zero,
					ModContent.ProjectileType<MagicalChainAnchorProj>(),
					Math.Max(1, (int)(Projectile.damage * 0.6f)),
					fieldMode, struck.Count - 1, SrcType);
			}

			// Complete Chain's ORIGINAL always parks on the victim it bit,
			// even when there is nobody left to fork to. It used to park only
			// after a successful fork, so an isolated victim made the bolt fly
			// on and read as a normal Chain. The sub-bolt (spawned below) is a
			// Chain child and is the only part that continues.
			if (Mode == ModeCompleteChain) {
				pauseTicks = CompleteChainHold;
				Projectile.velocity = Vector2.Zero;
			}

			if (!Chaining || Generation >= MaxGenerations)
				return;

			// A sub-lightning needs somewhere to fly. With no next victim there
			// is NO fork at all: a child spawned with nowhere to go is exactly
			// the "it spread but never chained" read the note complains about.
			int next = FindNextVictim(target.Center);
			if (next < 0)
				return;

			Vector2 dir = (Main.npc[next].Center - target.Center).SafeNormalize(legDir);

			// OnHitNPC for a player projectile resolves on the side that owns
			// the hit, which in multiplayer is the server - exactly the side
			// SpawnChild is written for (the owner client would duplicate the
			// child or miss it entirely).
			//
			// Complete Chain hands the relay to a CHAIN child: the original
			// parks here, but the sub-lightning must keep travelling and fork
			// again. Passing CompleteChain down made every generation stop on
			// its first victim, so the chain died after one hop.
			float childMode = Mode == ModeCompleteChain ? ModeChain : Mode;
			Player owner = Main.player[Projectile.owner];
			int child = MagicalReleaseCommon.SpawnChild(owner, target.Center, dir * BoltSpeed,
				Projectile.type, Math.Max(1, (int)(Projectile.damage * 0.8f)),
				childMode, Power01, ChildAi2);
			if (child >= 0 && child < Main.maxProjectiles) {
				if (Main.projectile[child].ModProjectile is MagicalLightningProj bolt)
					bolt.InheritChain(struck);
				Main.projectile[child].netUpdate = true;
			}

			// The fork itself is a strike: a directional fan thrown down the new
			// bearing, so a chain visibly BRANCHES instead of a second strand
			// simply appearing out of nowhere.
			if (!Main.dedServ) {
				Color forkHot = Color.Lerp(MagicalReleaseCommon.AbilityAccent(Projectile), Color.White, 0.55f);
				MagicalFx.Fan(ShapeId.WispTaper, target.Center, forkHot, 5, 3.2f, 18, 0.45f,
					dir.ToRotation(), 0.7f);
			}

			// The original keeps enough flight time left to carry on with.
			Projectile.timeLeft = Math.Max(Projectile.timeLeft, LinkLifeBonus + 6);

			// Chain: NOTHING else changes. Not legDir, not the hop clock, not the
			// trail - the original flies on exactly as it was, and the child
			// starts its own route from the bite. (This used to re-aim the
			// original in place, which made a chain read as one bolt curling
			// instead of a bolt plus a sub-lightning.)
			Projectile.netUpdate = true;
		}

		/// <summary>
		/// Nearest enemy to <paramref name="from"/> that the family has not bitten
		/// yet - the next link's mark, or -1 when there is nobody to chain to.
		/// </summary>
		private int FindNextVictim(Vector2 from, int exclude = -1)
		{
			int best = -1;
			float bestSq = HopRadius * HopRadius;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!MagicalReleaseCommon.Hittable(npc))
					continue;
				if (i == exclude)
					continue;
				if (struck.Contains(i))
					continue;
				float d = Vector2.DistanceSquared(npc.Center, from);
				if (d < bestSq) {
					bestSq = d;
					best = i;
				}
			}
			return best;
		}

		/// <summary>
		/// A freshly spawned sub-bolt inherits the family's bite list, so it can
		/// never land on an enemy a parent already struck. It is born inside the
		/// parent's victim, so without this it would bite that same body on its
		/// first hop - which is what made chaining look like one enemy being hit
		/// over and over.
		/// </summary>
		private void InheritChain(List<int> parentStruck)
		{
			foreach (int who in parentStruck) {
				if (!struck.Contains(who))
					struck.Add(who);
			}
		}

		private static void ClearStaticCharge(NPC npc)
		{
			int buffType = ModContent.BuffType<StaticChargeBuff>();
			for (int i = 0; i < npc.buffType.Length; i++) {
				if (npc.buffType[i] != buffType)
					continue;
				npc.DelBuff(i);
				npc.netUpdate = true;
				return;
			}
		}

		private void ImpactFx(Vector2 at, bool discharge = false)
		{
			if (Main.dedServ)
				return;
			Color accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			MiniYoyoFx.FlashFX(at, Color.Lerp(accent, Color.White, 0.5f), 0.35f);
			// One shared lightning bite: star + streaks + sparks. No Pop disc, so
			// a Static Trail payoff can add its one mechanic ring without the
			// old double-circle stack.
			MagicalFx.LightningHit(at, accent, Power01, legDir.ToRotation(), discharge);
			SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.22f, Pitch = 0.6f }, at);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			if (trail.Count < 2)
				return false;

			// Cubic fade, taken from Calamity's StaticGlowLine:
			//   Color = Color.Lerp(InitialColor, Color.Transparent, pow(t, 3))
			// This used to be a LINEAR timeLeft/8, which starts dimming the bolt
			// the moment it stops travelling - the whole strike then looks like it
			// is being switched off rather than discharged. Cubic holds full
			// brightness almost to the end and drops it in the last couple of
			// frames, so the bolt reads as snapping out. The width shrinks on a
			// gentler curve than the alpha, which is what stops it looking like
			// it is simply being scaled down.
			// Cubic fade over the Lunatic's last-half-second window: full
			// brightness for the whole flight, then the discharge thins out and
			// is gone. (A short linear ramp reads as the bolt being switched off.)
			float life = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTicks, 0f, 1f);
			float fade = 1f - (1f - life) * (1f - life) * (1f - life);
			if (fade <= 0.01f)
				return false;

			Color accent = MagicalReleaseCommon.AbilityAccent(Projectile);
			// Lunatic proportions: a 12px outer sheath around a 4px core. This
			// mod's channel stacks three passes, so the base width is set so the
			// broadest lands near the Lunatic's outer band instead of doubling it.
			float width = (3.4f + 3.4f * Power01) * (0.5f + 0.5f * fade);

			// Draw the recorded route, ending exactly on the bolt so the head never
			// lags a tick behind its own damage.
			Span<Vector2> buffer = stackalloc Vector2[MaxTrailPoints];
			int n = Math.Min(trail.Count, MaxTrailPoints);
			int start = trail.Count - n;
			for (int i = 0; i < n; i++)
				buffer[i] = trail[start + i];
			buffer[n - 1] = Projectile.Center;

			MagicalFx.SkyfangPath(buffer.Slice(0, n), accent, 0.95f * fade, width, (int)seed, 2);

			// Head corona, two pans: a wide dim pan for depth and a tight white
			// one for the point the damage is actually at. The star flare rides
			// on top, so the head is by far the brightest thing on the bolt and
			// the eye tracks it through the kinks.
			MagicalFx.Bloom(Projectile.Center, 34f + 20f * Power01, accent, 0.26f * fade);
			MagicalFx.Bloom(Projectile.Center, 13f + 7f * Power01, Color.White, 0.40f * fade);
			MagicalFx.StarFlare(Projectile.Center, Color.Lerp(accent, Color.White, 0.72f),
				16f + 10f * Power01, Projectile.rotation, 0.4f * fade);
			return false;
		}
	}
}
