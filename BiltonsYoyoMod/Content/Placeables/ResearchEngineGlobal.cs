using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Research charging + release via yoyo impacts.
	//
	// Direct-impact requirement: custom AABB overlap between the yoyo and
	// each nearby Engine tile (not tile collision), with a per-engine
	// cooldown per projectile so contact is distinct hits, never per-tick.
	// Each valid hit: +charge, +3000 ticks lifetime (added, not reset,
	// clamped, once per hit), controlled rebound away from the block.
	// No dummy NPC; the block stays a genuine tile.
	//
	// tModLoader lifetime notes: yoyo flight time is projectile.timeLeft
	// (initialised from YoyosLifeTimeMultiplier at spawn, then ticks down;
	// vanilla AI does not refill it while channeling). Adding to the
	// existing timeLeft therefore extends the throw. We clamp to 36000
	// (10 min, as used elsewhere in the mod) to avoid overflow/unsafe
	// values, and skip infinite-lifetime yoyos (timeLeft already huge).
	public class ResearchEngineGlobalProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		private readonly Dictionary<Point, int> engineCd = new();
		private readonly Dictionary<int, int> burstCd = new();
		private int momentumFx;
		private bool impactArmed;
		private bool impactWasOvertime;
		private bool launchArmed;
		private bool launchWasOvertime;
		private bool volatileArmed;
		private float volMult;
		private float volRadius;
		private float volKb;
		private bool volDrip;
		private bool gildArmed;
		private bool singArmed;

		public const int LifetimePerHit = 3000; // 50s at 60tps
		public const int MaxSafeTimeLeft = 36000;
		public const int HitCooldown = 45;

		public override bool AppliesToEntity(Projectile projectile, bool lateInstantiation)
		{
			return projectile.aiStyle == ProjAIStyleID.Yoyo;
		}

		public override void AI(Projectile projectile)
		{
			// Tick down per-engine cooldowns.
			if (engineCd.Count > 0 && projectile.owner == Main.myPlayer) {
				var drop = new List<Point>();
				foreach (var kv in engineCd) {
					if (kv.Value - 1 <= 0)
						drop.Add(kv.Key);
					else
						engineCd[kv.Key] = kv.Value - 1;
				}
				foreach (var k in drop)
					engineCd.Remove(k);
			}
			// Tick down over-time burst immunity (never stale).
			if (burstCd.Count > 0) {
				var dropB = new List<int>();
				foreach (var kv in burstCd) {
					if (kv.Value - 1 <= 0)
						dropB.Add(kv.Key);
					else
						burstCd[kv.Key] = kv.Value - 1;
				}
				foreach (int k in dropB)
					burstCd.Remove(k);
			}
			// Momentum release effect: temporary yoyo speed (distinct from
			// permanent Wind Vanes/Overclocked; only after a Research release).
			if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers) {
				var tp = Main.player[projectile.owner].GetModPlayer<ResearchEnginePlayer>();
				if (tp.MomentumTimer > 0) {
					Common.YoyoStaticBoosts.BoostTopSpeed(projectile.type, tp.MomentumMult);
					// Cyan slipstream while momentum is live.
					if (!Main.dedServ && Main.rand.NextBool(3)) {
						Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.Electric, -projectile.velocity.X * 0.1f, -projectile.velocity.Y * 0.1f, 120, default, 0.8f);
						d.noGravity = true;
					}
				}
			}
			if (momentumFx > 0)
				momentumFx--;
			// Only the owner registers charge (prevents MP double-count).
			if (Main.myPlayer != projectile.owner)
				return;
			string ns = projectile.ModProjectile?.GetType().Namespace ?? string.Empty;
			if (ns.Contains("TownNPC") || projectile.hostile)
				return;
			// Scan engine tiles near the yoyo (3x2 tiles + margin).
			Point pc = projectile.Center.ToTileCoordinates();
			const int r = 4;
			for (int tx = pc.X - r; tx <= pc.X + r; tx++) {
				for (int ty = pc.Y - r; ty <= pc.Y + r; ty++) {
					if (!WorldGen.InWorld(tx, ty))
						continue;
					Tile t = Framing.GetTileSafely(tx, ty);
					if (!t.HasTile || t.TileType != ModContent.TileType<Tiles.Placeables.ResearchEngineTile>())
						continue;
					Point origin = Tiles.Placeables.ResearchEngineTile.TileOriginAt(tx, ty);
					if (engineCd.ContainsKey(origin))
						continue;
					Rectangle rect = ResearchEngineSystem.EngineRect(origin);
					Rectangle projBox = new((int)projectile.position.X, (int)projectile.position.Y, projectile.width, projectile.height);
					if (!rect.Intersects(projBox))
						continue;
					RegisterImpact(projectile, origin, rect);
				}
			}
		}

		private void RegisterImpact(Projectile projectile, Point origin, Rectangle rect)
		{
			engineCd[origin] = HitCooldown;
			// Charge coils (#23): without them, yoyo hits do nothing.
			var edata = ResearchEngineSystem.GetOrCreate(origin);
			if (!ResearchTech.TechOn(edata, ResearchTech.ChargeCoils))
				return;
			// 1) Charge. Optimistic local add for instant UI feedback;
			// server rebroadcast converges (overwrite, never double-add).
			int prev = edata.Charge;
			// Nearby wards feed extra charge per hit (#6 station synergy).
			int gain = ResearchEngineSystem.ChargePerHit(edata.Mode) + ResearchNetwork.ChargeBonus(edata);
			if (ResearchTech.TechOn(edata, ResearchTech.ChargeWind))
				gain += 1;
			if (ResearchTech.TechOn(edata, ResearchTech.ChargeCond))
				gain += 1;
			// Boss lockout (#7): a player who already completed a full
			// charge during this boss fight cannot re-reach full while the
			// boss lives. The flag clears when the fight ends.
			int fullAt = ResearchEngineSystem.MaxChargeOf(edata);
			var rplayer = Main.player[projectile.owner].GetModPlayer<ResearchEnginePlayer>();
			if (rplayer.BossCharged && ResearchEngineSystem.AnyBossActive())
				gain = System.Math.Min(gain, System.Math.Max(0, fullAt - 1 - edata.Charge));
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				ResearchEngineSystem.AddCharge(origin, gain);
				PlaceableNet.SendResearchCharge(origin, gain);
			}
			else {
				ResearchEngineSystem.AddCharge(origin, gain);
			}
			// Announce the empty -> full transition once, not every farmed hit at max.
			int now = ResearchEngineSystem.GetOrCreate(origin).Charge;
			if (prev < fullAt && now >= fullAt) {
				CombatText.NewText(new Rectangle((int)projectile.Center.X - 40, (int)projectile.Center.Y - 40, 80, 30), Color.Gold, "CHARGED!");
				for (int i = 0; i < 12; i++) {
					Dust d = Dust.NewDustDirect(projectile.Center - new Vector2(4, 4), 8, 8, DustID.Torch, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.2f);
					d.noGravity = true;
				}
				// Completing a full charge mid-fight arms this player's
				// boss lockout (#7): no more 100s until the fight ends.
				if (ResearchEngineSystem.AnyBossActive())
					rplayer.BossCharged = true;
			}
			// 2) Lifetime: ADD 3000 ticks to existing, never reset to 3000.
			// Clamp to avoid overflow/unsafe values; skip if already huge.
			if (projectile.timeLeft > 0 && projectile.timeLeft < MaxSafeTimeLeft) {
				int add = LifetimePerHit;
				if (projectile.timeLeft > MaxSafeTimeLeft - add)
					projectile.timeLeft = MaxSafeTimeLeft;
				else
					projectile.timeLeft += add;
				projectile.netUpdate = true;
			}
			// 3) Controlled rebound away from the block center.
			// Strength follows the block's Rebound setting so farmers can
			// pick a gentle loop or a lively kick. Still capped per yoyo.
			Vector2 center = new(rect.Center.X, rect.Center.Y);
			Vector2 away = (projectile.Center - center).SafeNormalize(Vector2.UnitY * -1f);
			var (lo, hi) = ResearchEngineSystem.ReboundRange(ResearchEngineSystem.GetOrCreate(origin).Rebound);
			float speed = System.Math.Clamp(projectile.velocity.Length(), lo, hi);
			projectile.velocity = away * speed;
			// Cap against the yoyo's own class so no runaway.
			float cap = hi + 2f;
			try {
				if (projectile.type >= ProjectileID.None && projectile.type < ProjectileID.Sets.YoyosTopSpeed.Length)
					cap = System.Math.Max(Common.YoyoStaticBoosts.BaseTopSpeed(projectile.type) * 1.6f, hi + 2f);
			}
			catch { }
			if (projectile.velocity.Length() > cap)
				projectile.velocity = projectile.velocity.SafeNormalize(Vector2.UnitX) * cap;
			projectile.netUpdate = true;
			// Impact sparks scale with stored charge: pale while filling,
			// fierce near full, gold burst on the hit that tops it off.
			int nowMax = ResearchEngineSystem.MaxChargeOf(ResearchEngineSystem.GetOrCreate(origin));
			int nowCharge = ResearchEngineSystem.GetOrCreate(origin).Charge;
			int sparkType = nowCharge >= nowMax ? DustID.GoldCoin : nowCharge >= nowMax / 2 ? DustID.Torch : DustID.Smoke;
			float sparkScale = nowCharge >= nowMax ? 1.2f : 0.9f;
			for (int i = 0; i < 8; i++) {
				Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, sparkType, -projectile.velocity.X * 0.2f, -projectile.velocity.Y * 0.2f, 100, default, sparkScale);
				d.noGravity = true;
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, projectile.Center);
			if (!Main.dedServ)
				Lighting.AddLight(new Vector2(rect.Center.X, rect.Center.Y), 0.8f, 0.5f, 0.2f);
		}

		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			// Impact (0) and Launch (3) empower the triggering strike itself,
			// so they consume HERE: piercing yoyos cannot double-dip one
			// charge. Burst (1) / Momentum (2) release in OnHit instead.
			// One Hit spends a full engine; drips (Over Time / Surge) burn
			// a chunk per hit at reduced effectiveness.
			if (projectile.owner != Main.myPlayer)
				return;
			if (!FindReleaseEngine(projectile.Center, out Point key, out ResearchData data))
				return;
			if (data.Effect != 0 && data.Effect != 3)
				return;
			float power = 1f;
			bool drip = ResearchEngineSystem.StyleDrips(data);
			if (drip) {
				int burned = ConsumeTick(key);
				if (burned <= 0)
					return;
				power = ResearchEngineSystem.StylePower(data) * burned / ResearchEngineSystem.StyleDrain(data);
				data = ResearchEngineSystem.GetOrCreate(key);
			}
			else {
				if (!ConsumeFull(key))
					return;
			}
			// Nearby rift anchors strengthen the release (#6 synergy).
			power *= ResearchNetwork.ReleasePowerMult(data);
			if (data.Effect == 0) {
				modifiers.SourceDamage *= ImpactMult(data) * power;
				float wantKB = ImpactKB(data) * (drip ? power : 1f);
				if (wantKB > modifiers.Knockback.Base)
					modifiers.Knockback.Base = wantKB;
				if (ResearchTech.TechOn(data, ResearchTech.ImpCrit) && Main.rand.NextFloat(100f) < 20f)
					modifiers.SetCrit();
				impactArmed = true;
				impactWasOvertime = drip;
				gildArmed = !drip && ResearchTech.TechOn(data, ResearchTech.LaunchGilded);
				singArmed = ResearchTech.TechOn(data, ResearchTech.ImpSing);
				// Volatile Mix: stash a secondary burst for OnHit.
				if (ResearchTech.TechOn(data, ResearchTech.ImpVolatile)) {
					volatileArmed = true;
					volMult = BurstMult(data) * 0.4f * (drip ? power : 1f);
					volRadius = BurstRadius(data) * 0.7f;
					volKb = 6f;
					volDrip = drip;
				}
			}
			else {
				modifiers.SourceDamage *= LaunchMult(data) * (drip ? power : 1f);
				float wantKB = LaunchKB(data) * (drip ? power : 1f);
				if (wantKB > modifiers.Knockback.Base)
					modifiers.Knockback.Base = wantKB;
				// Guaranteed shove: the stat line above dies with the target
				// on 500% hits and bosses ignore it, so Launch also kicks
				// survivors directly (never bosses, never the immune).
				if (!target.boss && target.knockBackResist < 0.9f) {
					float shove = System.Math.Min(wantKB * 0.6f * (1f - target.knockBackResist), 12f);
					if (shove > 0.5f) {
						int dir = target.Center.X >= projectile.Center.X ? 1 : -1;
						target.velocity.X += dir * shove;
						target.velocity.Y -= shove * 0.35f;
						target.netUpdate = true;
					}
				}
				launchArmed = true;
				launchWasOvertime = drip;
				gildArmed = !drip && ResearchTech.TechOn(data, ResearchTech.LaunchGilded);
			}
			if (!Main.dedServ)
				Lighting.AddLight(projectile.Center, 0.6f, 0.35f, 0.1f);
		}

		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (projectile.owner != Main.myPlayer)
				return;
			string ns = projectile.ModProjectile?.GetType().Namespace ?? string.Empty;
			if (ns.Contains("TownNPC") || projectile.hostile)
				return;
			// Impact / Launch text rides the flags armed in ModifyHit.
			if (impactArmed) {
				impactArmed = false;
				if (!impactWasOvertime) {
					CombatText.NewText(new Rectangle((int)projectile.Center.X - 40, (int)projectile.Center.Y - 40, 80, 30), Color.Orange, "IMPACT!");
					if (!Main.dedServ) {
						PlaceableUI.DustRing(projectile.Center, 70f, DustID.Torch, PlaceableUI.RingCount(70f), 1.2f);
						Lighting.AddLight(projectile.Center, 1f, 0.6f, 0.2f);
					}
				}
				if (singArmed) {
					singArmed = false;
					VacuumPull(projectile.Center, 150f, 6f);
					if (!Main.dedServ)
						PlaceableUI.DustRing(projectile.Center, 150f, DustID.PurpleTorch, PlaceableUI.RingCount(150f), 1f);
				}
				if (volatileArmed) {
					volatileArmed = false;
					ResearchBurst(projectile, null, volMult, volRadius, volKb, volDrip);
				}
				if (gildArmed) {
					gildArmed = false;
					MaybeGildedShower(projectile, target);
				}
				return;
			}
			if (launchArmed) {
				launchArmed = false;
				if (!launchWasOvertime) {
					CombatText.NewText(new Rectangle((int)projectile.Center.X - 40, (int)projectile.Center.Y - 40, 80, 30), Color.Lime, "YEET!");
					if (!Main.dedServ) {
						PlaceableUI.DustRing(projectile.Center, 60f, DustID.MagicMirror, PlaceableUI.RingCount(60f), 1.1f);
						Lighting.AddLight(projectile.Center, 0.4f, 1f, 0.4f);
					}
				}
				if (gildArmed) {
					gildArmed = false;
					MaybeGildedShower(projectile, target);
				}
				return;
			}
			if (!FindReleaseEngine(projectile.Center, out Point key, out ResearchData data))
				return;
			if (data.Effect == 0 || data.Effect == 3)
				return; // Handled in ModifyHit above.
			float power = 1f;
			bool drip = ResearchEngineSystem.StyleDrips(data);
			if (drip) {
				int burned = ConsumeTick(key);
				if (burned <= 0)
					return;
				power = ResearchEngineSystem.StylePower(data) * burned / ResearchEngineSystem.StyleDrain(data);
				data = ResearchEngineSystem.GetOrCreate(key);
			}
			else {
				if (!ConsumeFull(key))
					return;
			}
			// Nearby rift anchors strengthen the release (#6 synergy).
			power *= ResearchNetwork.ReleasePowerMult(data);
			volatileArmed = false;
			singArmed = false;
			if (data.Effect == 1) {
				ResearchBurst(projectile, data, BurstMult(data) * power, BurstRadius(data), BurstKB(data) * (drip ? power : 1f), drip);
				if (!drip && ResearchTech.TechOn(data, ResearchTech.LaunchGilded))
					MaybeGildedShower(projectile, target);
			}
			else {
				var tp = Main.player[projectile.owner].GetModPlayer<ResearchEnginePlayer>();
				tp.MomentumMult = 1f + MomBonus(data) * power;
				tp.MomentumTimer = drip ? 150 : (int)MomTime(data);
				if (!drip) {
					CombatText.NewText(new Rectangle((int)projectile.Center.X - 40, (int)projectile.Center.Y - 40, 80, 30), Color.Cyan, "MOMENTUM!");
					if (ResearchTech.TechOn(data, ResearchTech.LaunchGilded))
						MaybeGildedShower(projectile, target);
				}
			}
		}

		// Gilded Vent: full releases shower pocket change. Stonks.
		private static void MaybeGildedShower(Projectile projectile, NPC target)
		{
			for (int s = 0; s < 2; s++) {
				int stack = Main.rand.Next(1, 3);
				Item.NewItem(projectile.GetSource_OnHit(target), (int)projectile.Center.X, (int)projectile.Center.Y, 8, 8, ItemID.GoldCoin, stack);
			}
		}

		// Release math (tech-scaled). Overdrive Core x1.5s damage numbers.
		private static float ImpactMult(ResearchData d)
		{
			float m = 5f * ResearchEngineSystem.ReleaseMult(d.Mode);
			if (ResearchTech.TechOn(d,ResearchTech.ImpDmg1))
				m += 1.5f;
			if (ResearchTech.TechOn(d,ResearchTech.ImpDmg2))
				m += 1.5f;
			if (ResearchTech.TechOn(d,ResearchTech.Overdrive))
				m *= 1.5f;
			return m;
		}

		private static float ImpactKB(ResearchData d) => ResearchTech.TechOn(d,ResearchTech.ImpKb) ? 28f : 18f;

		private static float BurstMult(ResearchData d)
		{
			float m = 2.5f * ResearchEngineSystem.ReleaseMult(d.Mode);
			if (ResearchTech.TechOn(d,ResearchTech.BurstDmg))
				m += 1f;
			if (ResearchTech.TechOn(d,ResearchTech.Overdrive))
				m *= 1.5f;
			return m;
		}

		private static float BurstRadius(ResearchData d) => 150f + (ResearchTech.TechOn(d,ResearchTech.BurstRad) ? 40f : 0f);
		private static float BurstKB(ResearchData d) => ResearchTech.TechOn(d,ResearchTech.BurstKb) ? 25f : 15f;

		private static float MomBonus(ResearchData d)
		{
			float b = 0.2f * ResearchEngineSystem.ReleaseMult(d.Mode);
			if (ResearchTech.TechOn(d,ResearchTech.MomMag))
				b += 0.1f;
			if (ResearchTech.TechOn(d,ResearchTech.MomRedline))
				b += 0.15f;
			if (ResearchTech.TechOn(d,ResearchTech.Overdrive))
				b *= 1.5f;
			return b;
		}

		private static float MomTime(ResearchData d)
		{
			float t = 480f * ResearchEngineSystem.ReleaseMult(d.Mode);
			if (ResearchTech.TechOn(d,ResearchTech.MomDur))
				t *= 1.5f;
			if (ResearchTech.TechOn(d,ResearchTech.MomRedline))
				t *= 0.6f;
			return t;
		}

		private static float LaunchMult(ResearchData d)
		{
			return (ResearchTech.TechOn(d,ResearchTech.LaunchDmg) ? 0.5f : 0.1f) * ResearchEngineSystem.ReleaseMult(d.Mode);
		}

		private static float LaunchKB(ResearchData d) => ResearchTech.TechOn(d,ResearchTech.LaunchKb) ? 35f : 22f;

		// Nearest usable engine: full for One Hit, any charge for drips.
		// Only one engine releases per hit, so multiples never stack.
		// Note: coils gate CHARGING (hits on the block), never spending —
		// earned charge always stays releasable, even without coils.
		private static bool FindReleaseEngine(Vector2 from, out Point key, out ResearchData data)
		{
			key = default;
			data = null;
			float bestD2 = 800f * 800f;
			foreach (var kv in ResearchEngineSystem.Tiles) {
				if (!ResearchEngineSystem.IsTileStillEngine(kv.Key))
					continue;
				bool ready = ResearchEngineSystem.StyleDrips(kv.Value) ? kv.Value.Charge > 0 : kv.Value.Charge >= ResearchEngineSystem.MaxChargeOf(kv.Value);
				if (!ready)
					continue;
				Vector2 c = new(kv.Key.X * 16f + 24f, kv.Key.Y * 16f + 16f);
				float d2 = Vector2.DistanceSquared(from, c);
				if (d2 < bestD2) {
					bestD2 = d2;
					key = kv.Key;
					data = kv.Value;
				}
			}
			return data != null;
		}

		private static bool ConsumeFull(Point key)
		{
			var d = ResearchEngineSystem.GetOrCreate(key);
			if (d.Charge < ResearchEngineSystem.MaxChargeOf(d) || !d.AutoRelease)
				return false;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchConsume(key);
			ResearchEngineSystem.TryConsumeFull(key);
			return true;
		}

		private static int ConsumeTick(Point key)
		{
			// Returns the charge actually burned (0 when held/empty/gone).
			// Client applies optimistically like charge adds; the server
			// rebroadcast converges.
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				PlaceableNet.SendResearchTick(key);
				int before = ResearchEngineSystem.GetOrCreate(key).Charge;
				ResearchEngineSystem.TryConsumeTick(key);
				return System.Math.Max(0, before - ResearchEngineSystem.GetOrCreate(key).Charge);
			}
			else {
				int before = ResearchEngineSystem.GetOrCreate(key).Charge;
				ResearchEngineSystem.TryConsumeTick(key);
				return System.Math.Max(0, before - ResearchEngineSystem.GetOrCreate(key).Charge);
			}
		}

		// Black-hole helper for the vacuum techs: drags non-boss,
		// non-immune enemies toward a point (resist softens the pull).
		private static void VacuumPull(Vector2 center, float radius, float strength)
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.boss || npc.dontTakeDamage || npc.lifeMax <= 5 || npc.life <= 0)
					continue;
				if (npc.knockBackResist >= 1f)
					continue;
				// Vacuum techs must never drag a training dummy's hitbox away
				// from its fixed sprite.
				if (DummySafety.Pin(npc))
					continue;
				Vector2 delta = center - npc.Center;
				float dist = delta.Length();
				if (dist > radius || dist < 8f)
					continue;
				npc.velocity += delta / dist * strength * (1f - npc.knockBackResist * 0.75f);
				npc.netUpdate = true;
			}
		}

		private void ResearchBurst(Projectile projectile, ResearchData data, float mult, float radius, float kb, bool drip)
		{
			// Heavy-yoyo style shockwave: twin expanding dust rings, big
			// flash, deep boom. Overtime ticks skip the text (spam).
			// Event Horizon drags the crowd in before the shock lands.
			if (ResearchTech.TechOn(data, ResearchTech.BurstVac))
				VacuumPull(projectile.Center, radius + 60f, 6f);
			if (!Main.dedServ) {
				PlaceableUI.DustRing(projectile.Center, radius, DustID.Torch, PlaceableUI.RingCount(radius), 1.4f);
				PlaceableUI.DustRing(projectile.Center, radius * 0.55f, DustID.Smoke, PlaceableUI.RingCount(radius * 0.55f), 1.2f);
				Lighting.AddLight(projectile.Center, 1.2f, 0.7f, 0.25f);
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, projectile.Center);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, projectile.Center);
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5 || npc.life <= 0)
					continue;
				if (burstCd.ContainsKey(i))
					continue;
				if (Vector2.DistanceSquared(projectile.Center, npc.Center) > radius * radius)
					continue;
				if (drip)
					burstCd[i] = 20;
				int dmg = System.Math.Max(1, (int)(projectile.damage * mult));
				NPC.HitInfo h = new() {
					Damage = dmg,
					Knockback = kb,
					HitDirection = System.Math.Sign(npc.Center.X - projectile.Center.X),
					Crit = false
				};
				npc.StrikeNPC(h, false, false);
				if (ResearchTech.TechOn(data, ResearchTech.BurstAfter)) {
					npc.AddBuff(BuffID.Slow, 120);
					npc.AddBuff(BuffID.Bleeding, 240);
				}
				npc.netUpdate = true;
			}
			if (!drip)
				CombatText.NewText(new Rectangle((int)projectile.Center.X - 40, (int)projectile.Center.Y - 40, 80, 30), Color.Orange, "BURST!");
		}
	}
}
