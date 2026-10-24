using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// Shared orbit math, caps, stat scaling and feedback for the Magical Tech.
	///
	/// Stat scaling is PERCENT-ONLY on purpose: orbiting lifetime and orbit speed
	/// scale with the player's yoyo damage bonus (flat damage is ignored), and are
	/// re-derived from the live stat every tick so losing a bonus shrinks them
	/// again instead of compounding on an already-scaled value.
	/// </summary>
	public static class MagicalOrbit
	{
		// ---- Formation ----
		public const float OrbitRadius = 92f;            // idle ring radius
		public const float OrbitRadiusConverged = 72f;   // ring radius while an ability pools at the centre
		public const float BaseOrbitSpeed = 0.045f;      // rad/tick at +0% damage bonus
		public const int InfiniteLifetimeTicks = 20 * 60; // 20s base orbit life for infinite-flight yoyos
		public const float StatScaleMin = 0.25f;          // -75% damage bonus floor (no zero/negative life)
		public const float StatScaleMax = 6f;             // +500% damage bonus ceiling (orbits never run forever)
		public const int MaxSafeTimeLeft = 36000;         // 10 min clamp, matches ResearchEngineGlobal

		// ---- Ability defaults (per-yoyo overrides live in MagicalAbilityProfile) ----
		public const int ChargeTicksDefault = 90;      // 1.5s default channel
		public const int BeamMaxLifeTicks = 5 * 60;    // sustained release hard cap: 5s
		public const float BeamDamageFraction = 0.15f; // conservative default damage cut
		public const int BeamHitCooldownDefault = 12;  // per-target beam hit interval
		public const int AbilityCooldownDefault = 60;  // 1s lockout after a release ends
		public const int AbilityCooldownMinimum = 60;  // never shorter than 1s

		private static readonly Dictionary<int, Color> accentCache = new();
		private static readonly Color FallbackAccent = new(150, 180, 255);

		/// <summary>Cap stats: 3 orbits / 1 per click, doubled by Yoyo Glove or Bag.</summary>
		public static void CapStats(Player player, out int deploy, out int cap)
		{
			bool glove = player != null && YoyoTechCommon.HasYoyoGloveOrBag(player);
			deploy = glove ? 2 : 1;
			cap = glove ? 6 : 3;
		}

		public static int CountOrbitYoyos(Player player)
		{
			if (player == null || !player.active)
				return 0;
			return YoyoTechCommon.CountOwnedType(player, ModContent.ProjectileType<Projectiles.Tech.MagicalYoyoProj>());
		}

		public static void CollectOrbitCenters(Player player, List<Vector2> into)
		{
			int type = ModContent.ProjectileType<Projectiles.Tech.MagicalYoyoProj>();
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == player.whoAmI && p.type == type)
					into.Add(p.Center);
			}
		}

		/// <summary>Source projectile of the oldest live orbit (the ability's identity).</summary>
		public static int PrimaryOrbitSourceType(Player player, int fallback)
		{
			int type = ModContent.ProjectileType<Projectiles.Tech.MagicalYoyoProj>();
			int bestId = int.MaxValue;
			int src = fallback;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (!p.active || p.owner != player.whoAmI || p.type != type)
					continue;
				if (p.ModProjectile is Projectiles.Tech.MagicalYoyoProj orb && orb.OrbitId < bestId) {
					bestId = orb.OrbitId;
					src = orb.SourceProjType;
				}
			}
			return src;
		}

		/// <summary>Next unique orbit id for a player (ordering only; resets when the orbit clears).</summary>
		public static int NextOrbitId(Player player)
		{
			int type = ModContent.ProjectileType<Projectiles.Tech.MagicalYoyoProj>();
			int max = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == player.whoAmI && p.type == type
					&& p.ModProjectile is Projectiles.Tech.MagicalYoyoProj orb && orb.OrbitId > max)
					max = orb.OrbitId;
			}
			return max + 1;
		}

		/// <summary>Fractional yoyo damage bonus (0.10 = +10%). Percent-only: flat damage stays out.</summary>
		public static float DamageBonusFraction(Player player)
		{
			if (player == null || !player.active)
				return 0f;
			return player.GetTotalDamage(DamageClass.MeleeNoSpeed).ApplyTo(1f) - 1f;
		}

		/// <summary>Lifetime + orbit speed scale (1 = base). Bounded against extreme builds.</summary>
		public static float StatScale(Player player)
		{
			return MathHelper.Clamp(1f + DamageBonusFraction(player), StatScaleMin, StatScaleMax);
		}

		/// <summary>
		/// Base (unscaled) orbit lifetime: 1/10 of the source yoyo's flight time
		/// (multiplier is in seconds, 60 ticks each, so x6 dials it to a tenth),
		/// or 20s when the source yoyo spins forever. A floor keeps a single guard
		/// alive long enough to complete its own charge.
		/// </summary>
		public static int BaseLifetimeTicks(int sourceProjType)
		{
			float seconds = LifetimeSeconds(sourceProjType);
			if (seconds <= 0f)
				return InfiniteLifetimeTicks;
			return Math.Max(MinimumLifetimeTicks, (int)(seconds * 6f));
		}

		/// <summary>Guards always live at least this long (~4s) so a lone guard can
		/// still fuel a charge; the 1/10 rule applies whenever it is longer.</summary>
		public const int MinimumLifetimeTicks = 4 * 60;

		public static float LifetimeSeconds(int sourceProjType)
		{
			if (sourceProjType <= ProjectileID.None)
				return -1f;
			try {
				float[] look = ProjectileID.Sets.YoyosLifeTimeMultiplier;
				if (sourceProjType < look.Length)
					return look[sourceProjType];
			}
			catch { }
			return -1f;
		}

		/// <summary>Rank/total of the live orbit, ordered by (OrbitId, whoAmI) on every side.</summary>
		public static void RankOf(Projectile self, out int rank, out int total)
		{
			rank = 0;
			total = 1;
			if (self.ModProjectile is not Projectiles.Tech.MagicalYoyoProj mine)
				return;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				if (i == self.whoAmI)
					continue;
				Projectile p = Main.projectile[i];
				if (!p.active || p.type != self.type || p.owner != self.owner)
					continue;
				if (p.ModProjectile is not Projectiles.Tech.MagicalYoyoProj other)
					continue;
				total++;
				if (other.OrbitId < mine.OrbitId || (other.OrbitId == mine.OrbitId && i < self.whoAmI))
					rank++;
			}
		}

		/// <summary>
		/// Shared formation phase: one spin clock for every orbit yoyo (double
		/// precision so hour-long sessions cannot quantise the angle), rank-spread
		/// evenly. Speed scales with the player's damage bonus.
		/// </summary>
		public static float OrbitPhase(Player owner, int rank, int total)
		{
			double spin = Main.GameUpdateCount * (double)BaseOrbitSpeed * StatScale(owner);
			return (float)(spin % MathHelper.TwoPi) + rank * MathHelper.TwoPi / Math.Max(1, total);
		}

		public static float LerpAngle(float from, float to, float t)
		{
			return from + MathHelper.WrapAngle(to - from) * t;
		}

		// ---- Accent colour sampled from the source yoyo sprite (cached per type) ----

		public static Color AccentOf(int sourceProjType)
		{
			if (sourceProjType <= ProjectileID.None)
				return FallbackAccent;
			if (accentCache.TryGetValue(sourceProjType, out Color cached))
				return cached;
			Color sampled = SampleAccent(sourceProjType);
			accentCache[sourceProjType] = sampled;
			return sampled;
		}

		private static Color SampleAccent(int sourceProjType)
		{
			if (Main.dedServ)
				return FallbackAccent;
			try {
				if (sourceProjType >= TextureAssets.Projectile.Length)
					return FallbackAccent;
				// Vanilla projectile textures are lazy; modded ones are
				// preloaded. Asset<Texture2D>.Value is a 1x1 TRANSPARENT texture
				// while unloaded, so load first and check IsLoaded before
				// sampling, or every vanilla yoyo silently gets the fallback.
				var sourceAsset = TextureAssets.Projectile[sourceProjType];
				if (!sourceAsset.IsLoaded && sourceProjType < ProjectileID.Count)
					Main.instance.LoadProjectile(sourceProjType);
				if (!sourceAsset.IsLoaded)
					return FallbackAccent;
				Texture2D tex = sourceAsset.Value;
				Color[] data = new Color[tex.Width * tex.Height];
				tex.GetData(data);
				double r = 0d, g = 0d, b = 0d;
				int n = 0;
				for (int i = 0; i < data.Length; i += 4) {
					Color c = data[i];
					if (c.A < 150)
						continue;
					if (c.R + c.G + c.B < 90)
						continue; // skip near-black outlines
					r += c.R;
					g += c.G;
					b += c.B;
					n++;
				}
				if (n == 0)
					return FallbackAccent;
				Color avg = new((int)(r / n), (int)(g / n), (int)(b / n));
				// Normalise brightness so dark textures still glow.
				int max = Math.Max(avg.R, Math.Max(avg.G, avg.B));
				if (max < 180) {
					float boost = 180f / Math.Max(1, max);
					avg = new Color(
						(int)Math.Min(255, avg.R * boost),
						(int)Math.Min(255, avg.G * boost),
						(int)Math.Min(255, avg.B * boost));
				}
				return avg;
			}
			catch {
				return FallbackAccent;
			}
		}

		// ---- Ability plumbing ----

		public static bool ControllerAlive(Player player)
		{
			if (player == null || !player.active)
				return false;
			return YoyoTechCommon.CountOwnedType(player, ModContent.ProjectileType<Projectiles.Tech.MagicalConvergenceProj>()) > 0;
		}

		/// <summary>Projectile type of the yoyo the player is holding, or 0.</summary>
		public static int HeldYoyoSourceType(Player player)
		{
			if (player == null || !player.active)
				return 0;
			try {
				Item held = player.HeldItem;
				if (held != null && !held.IsAir && YoyoTechCommon.IsYoyo(held) && held.shoot > ProjectileID.None)
					return held.shoot;
			}
			catch { }
			return 0;
		}

		/// <summary>Spawn the ability controller. Owner-side only; one per player.
		/// The held yoyo's projectile type is written to ai[1] so a controller
		/// with ZERO orbiters still knows which ability it is (Ice Queen's Brand
		/// Frost and every other no-orbit right-click used to fall back to the
		/// default beam, or die on the first tick).</summary>
		public static bool TrySpawnController(Player player, int sourceProjType)
		{
			if (player == null || !player.active || player.dead || player.whoAmI != Main.myPlayer)
				return false;
			if (ControllerAlive(player))
				return false;
			int type = ModContent.ProjectileType<Projectiles.Tech.MagicalConvergenceProj>();
			int idx = Projectile.NewProjectile(player.GetSource_Misc("MagicalTechOrbit"), player.Center, Vector2.Zero,
				type, 0, 0f, player.whoAmI, ai1: sourceProjType);
			if (idx < 0 || idx >= Main.maxProjectiles)
				return false;
			Main.projectile[idx].netUpdate = true;
			return true;
		}

		/// <summary>
		/// Hold-maintained ability start (owner side). While right-click is HELD,
		/// this is polled every tick, so the charge re-forms by itself whenever the
		/// orbits die out or a previous charge ended - no press-edge required, and
		/// therefore no dead state that only a manual re-click (or a tech switch)
		/// could clear. Pays the ability's mana start cost; refuses with a single
		/// throttled cue when mana or cooldown is missing.
		/// </summary>
		public static bool TryMaintainController(Player player)
		{
			if (player == null || !player.active || player.dead || player.whoAmI != Main.myPlayer)
				return false;
			if (!AbilitiesEnabled)
				return false;
			if (ControllerAlive(player))
				return true; // already charging/firing: nothing to do
			var tp = player.GetModPlayer<YoyoTechPlayer>();
			if (tp.MagicalRestartCd > 0 || tp.MagicalAbilityCooldown > 0)
				return false;
			// A Magical ability is cast THROUGH a yoyo. Before this guard the
			// oldest orbit could carry the identity while the player held a
			// pickaxe/other item, so a ring left in the world let the ability be
			// used from outside the yoyo tech entirely - the "I can bug it out
			// and cast with the orbiters out" report. No yoyo in hand, no cast.
			int held = HeldYoyoSourceType(player);
			if (held <= 0)
				return false;
			int orbitCount = CountOrbitYoyos(player);
			// Zero-orbit hand-cast is allowed once per right-click hold (Ice
			// Queen et al. must still work after the guards expire). The latch
			// stops a held button from re-forming charge after charge forever
			// with no orbiters; releasing the button arms it again. It is set
			// after EVERY successful cast, not only hand-casts: otherwise a
			// hold that started with orbiters could outlive the ring and then
			// silently transition into a free hand-cast - the "I can still use
			// it when the orbiters are out" report.
			if (orbitCount <= 0 && tp.MagicalHandCastLatch)
				return false;
			// Source of the ability: the oldest orbit when one is out, otherwise
			// the yoyo actually in hand.
			int src = PrimaryOrbitSourceType(player, held);
			MagicalAbilityProfile profile = MagicalAbilityRegistry.Get(src);
			float maxMana = ManaMaxOf(player);
			float tier = ManaTier01(AverageOrbitDamage(player));
			float startCost = Math.Max(1f, profile.ManaStartCost(maxMana, tier));
			// A worn Mana Flower tops the pool up before the ability refuses,
			// exactly like a failed magic cast would.
			ManaFlowerAssist(player, startCost);
			if (player.statMana < startCost) {
				CueNoManaSoft(player, tp);
				return false;
			}
			if (!TrySpawnController(player, src))
				return false;
			// Latch every cast for this hold. With orbiters alive the latch is
			// ignored, so normal hold-to-repeat still works; once the ring is
			// empty it prevents an unearned hand-cast until the button is
			// released and pressed again.
			tp.MagicalHandCastLatch = true;
			player.statMana = Math.Max(0, player.statMana - (int)startCost);
			player.manaRegenDelay = Math.Max(player.manaRegenDelay, 60);
			return true;
		}

		// ---- Ability mana economy ----
		// The one-off start cost scales with the yoyo (via its damage tier) AND the
		// player's max mana; the sustained cost is a per-second upkeep that varies
		// with the yoyo. Both are overridable per yoyo in MagicalAbilityProfile.

		public static float ManaMaxOf(Player player)
		{
			if (player == null)
				return 20f;
			return Math.Max(20f, player.statManaMax2);
		}

		/// <summary>0..1 cost tier: heavier (higher-damage) yoyos pay more.</summary>
		public static float ManaTier01(float avgOrbitDamage)
		{
			return MathHelper.Clamp((avgOrbitDamage - 10f) / 110f, 0f, 1f);
		}

		/// <summary>Right-click abilities are opt-in (Mod Configs -> MagicalTech).</summary>
		public static bool AbilitiesEnabled
		{
			get {
				try {
					Configuration.YoyoServerConfig cfg = ModContent.GetInstance<Configuration.YoyoServerConfig>();
					return cfg != null && cfg.EnableMagicalAbilities;
				}
				catch {
					return false;
				}
			}
		}

		/// <summary>Player-facing knob on top of the tuned costs (1 = default).</summary>
		public static float ManaMultiplier
		{
			get {
				try {
					Configuration.YoyoServerConfig cfg = ModContent.GetInstance<Configuration.YoyoServerConfig>();
					return cfg == null ? 1f : MathHelper.Clamp(cfg.MagicalAbilityManaMultiplier, 0.25f, 10f);
				}
				catch {
					return 1f;
				}
			}
		}

		public static float ManaStartCost(float maxMana, float tier01)
		{
			float raw = 25f + 22f * MathHelper.Clamp(tier01, 0f, 1f) + Math.Max(20f, maxMana) * 0.10f;
			return raw * ManaMultiplier;
		}

		public static float ManaUpkeepPerSecond(float maxMana, float tier01)
		{
			float raw = 12f + 14f * MathHelper.Clamp(tier01, 0f, 1f);
			return raw * ManaMultiplier;
		}

		/// <summary>
		/// Mana Flower assist. The ability's drain bypasses vanilla's item-use
		/// path - the only place the flower normally drinks from - so the drain
		/// nudges it directly: when the pool falls near what the ability still
		/// needs and a flower is worn, the flower uses a REAL mana potion (count
		/// drops, potion buff applies, sip sound plays) exactly like a failed
		/// magic cast would.
		/// </summary>
		public static void ManaFlowerAssist(Player player, float upcomingCost)
		{
			if (player == null || !player.manaFlower || player.dead)
				return;
			float threshold = Math.Max(ManaMaxOf(player) * 0.2f, upcomingCost * 2.5f);
			if (player.statMana > threshold)
				return;
			// Pick the smallest potion that still fills the pool (the flower's
			// conservation habit); if none fully fills it, take the biggest held.
			int fill = -1;
			int largest = -1;
			int need = Math.Max(1, player.statManaMax2 - player.statMana);
			for (int i = 0; i < 58; i++) {
				Item item = player.inventory[i];
				if (item == null || item.stack <= 0 || item.healMana <= 0)
					continue;
				if (item.healMana >= need && (fill < 0 || item.healMana < player.inventory[fill].healMana))
					fill = i;
				if (largest < 0 || item.healMana > player.inventory[largest].healMana)
					largest = i;
			}
			int use = fill >= 0 ? fill : largest;
			if (use >= 0)
				DrinkManaPotion(player, use);
		}

		/// <summary>Uses one real mana potion from the player's inventory with the
		/// same effects as drinking it: mana gain, the potion's own buff, the sip
		/// sound and the floating mana text. The stack actually shrinks.</summary>
		private static bool DrinkManaPotion(Player player, int slot)
		{
			if (slot < 0 || slot >= 58)
				return false;
			Item potion = player.inventory[slot];
			if (potion == null || potion.stack <= 0 || potion.healMana <= 0)
				return false;
			int gain = potion.healMana;
			player.statMana = Math.Min(player.statManaMax2, player.statMana + gain);
			player.ManaEffect(gain);
			if (potion.buffType > 0 && potion.buffTime > 0)
				player.AddBuff(potion.buffType, potion.buffTime);
			potion.stack--;
			if (potion.stack <= 0)
				potion.TurnToAir();
			SoundEngine.PlaySound(SoundID.Item3 with { Volume = 0.5f }, player.Center);
			return true;
		}

		/// <summary>Channel fatigue: +10% upkeep per second held, capped at 2.5x.</summary>
		public static float UpkeepRamp(int heldTicks)
		{
			return 1f + MathHelper.Clamp(heldTicks / 60f * 0.10f, 0f, 1.5f);
		}

		/// <summary>Average damage of the live orbit shells (already tech-scaled and enhanced).
		/// With no orbiters out, the held yoyo's own damage is the fallback: a
		/// right-click from the hand must not be stuck on the flat 10 damage a
		/// zero-orbit formation used to report.</summary>
		public static float AverageOrbitDamage(Player player)
		{
			int type = ModContent.ProjectileType<Projectiles.Tech.MagicalYoyoProj>();
			float total = 0f;
			int n = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == player.whoAmI && p.type == type) {
					total += p.damage;
					n++;
				}
			}
			if (n > 0)
				return total / n;
			try {
				Item held = player.HeldItem;
				if (held != null && !held.IsAir && YoyoTechCommon.IsYoyo(held))
					return Math.Max(10f, held.damage);
			}
			catch { }
			return 10f;
		}

		/// <summary>Cursor aim for the local player; falls back to the current facing/dir.</summary>
		public static Vector2 AimDirection(Player player, Vector2 from, Vector2 fallback)
		{
			Vector2 safe = fallback.LengthSquared() > 0.001f ? fallback.SafeNormalize(Vector2.UnitX) : Vector2.UnitX;
			if (player != null && player.whoAmI == Main.myPlayer) {
				Vector2 to = Main.MouseWorld - from;
				if (to.LengthSquared() > 16f * 16f)
					return to.SafeNormalize(safe);
			}
			return safe;
		}

		/// <summary>
		/// Shared mouse gate: focused window, world (no inventory / chat / UI)
		/// context. Keeps right-click from fighting chat or menus.
		/// </summary>
		public static bool CanMouseAct(Player player)
		{
			if (Main.dedServ || Main.gameMenu || !Main.hasFocus)
				return false;
			if (Main.playerInventory || Main.drawingPlayerChat)
				return false;
			if (player != null && player.mouseInterface)
				return false;
			return true;
		}

		/// <summary>True while the local player physically holds the right button for the ability.</summary>
		public static bool RightHeldByLocal(Player owner)
		{
			return owner != null && owner.whoAmI == Main.myPlayer
				&& Main.LocalPlayer.GetModPlayer<YoyoTechPlayer>().MagicalRightHeld;
		}

		// ---- Feedback (audio + visuals only; never chat) ----

		/// <summary>At-cap tell: soft tick + a ring of dust collapsing to the player (no chat).</summary>
		public static void CueCapped(Player player)
		{
			if (Main.dedServ || player.whoAmI != Main.myPlayer)
				return;
			SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.35f, Pitch = -0.5f }, player.Center);
			for (int i = 0; i < 8; i++) {
				float ang = MathHelper.TwoPi * i / 8f;
				Vector2 pos = player.Center + ang.ToRotationVector2() * OrbitRadius;
				Dust d = Dust.NewDustPerfect(pos, DustID.MagicMirror, -ang.ToRotationVector2() * 3.5f, 120, default, 0.85f);
				d.noGravity = true;
			}
		}

		/// <summary>No-mana tell: soft close + a dim blue puff (no chat).</summary>
		public static void CueNoMana(Player player)
		{
			if (Main.dedServ || player.whoAmI != Main.myPlayer)
				return;
			SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.4f, Pitch = -0.3f }, player.Center);
			for (int i = 0; i < 6; i++) {
				Dust d = Dust.NewDustDirect(player.Center - new Vector2(10f, 10f), 20, 20,
					DustID.MagicMirror, 0f, -0.6f, 120, default, 0.7f);
				d.noGravity = true;
			}
		}

		/// <summary>
		/// Throttled no-mana tell for the hold-maintained ability: at most one cue
		/// per cooldown window, so a held right-click cannot machine-gun the sound.
		/// </summary>
		public static void CueNoManaSoft(Player player, YoyoTechPlayer tp)
		{
			if (Main.dedServ || player == null || player.whoAmI != Main.myPlayer)
				return;
			if (tp == null || tp.MagicalCueCd > 0)
				return;
			tp.MagicalCueCd = 45;
			CueNoMana(player);
		}

		/// <summary>Sustained drain starved: the ability collapses with a mana tell.</summary>
		public static void CueManaStarvation(Player player)
		{
			if (Main.dedServ || player == null || player.whoAmI != Main.myPlayer)
				return;
			SoundEngine.PlaySound(SoundID.Item66 with { Volume = 0.55f, Pitch = -0.4f }, player.Center);
			for (int i = 0; i < 10; i++) {
				Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
				Dust d = Dust.NewDustDirect(player.Center - new Vector2(8f, 8f), 16, 16,
					DustID.MagicMirror, dir.X * 2.2f, dir.Y * 2.2f, 120, default, 0.85f);
				d.noGravity = true;
			}
		}
	}
}
