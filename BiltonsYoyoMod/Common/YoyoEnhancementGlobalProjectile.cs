using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;
using BiltonsYoyoMod.Content.Projectiles;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Common
{
	// Per-tick static yoyo stat manager. Vanilla yoyo range/top-speed live in
	// static sets, so boosted values are re-asserted every tick by active
	// projectiles and reset to base every tick by YoyoBoostSystem.
	public static class YoyoStaticBoosts
	{
		private static readonly Dictionary<int, float> baseRange = new();
		private static readonly Dictionary<int, float> baseTop = new();
		private static readonly Dictionary<int, float> baseLife = new();

		private static void EnsureBase(int type)
		{
			// Bases are pre-captured clean at load/world-load (see YoyoBoostSystem);
			// this only backfills types registered after load (e.g. late content).
			if (!baseRange.ContainsKey(type))
				baseRange[type] = ProjectileID.Sets.YoyosMaximumRange[type];
			if (!baseTop.ContainsKey(type))
				baseTop[type] = ProjectileID.Sets.YoyosTopSpeed[type];
			if (!baseLife.ContainsKey(type))
				baseLife[type] = ProjectileID.Sets.YoyosLifeTimeMultiplier[type];
		}

		// Captures clean base stats for every yoyo type. Must run when no boosted
		// projectile can be active (mod load / world load), otherwise a mid-boost
		// snapshot would leak inflated values into every later reset.
		public static void CaptureCleanBases()
		{
			ForgetAll();
			int n = ProjectileID.Sets.YoyosMaximumRange.Length;
			for (int type = 0; type < n; type++) {
				if (ProjectileID.Sets.YoyosMaximumRange[type] > 0f)
					EnsureBase(type);
			}
		}

		public static void BoostRange(int type, float extra)
		{
			EnsureBase(type);
			ProjectileID.Sets.YoyosMaximumRange[type] = baseRange[type] + extra;
		}

		public static void BoostTopSpeed(int type, float mult)
		{
			EnsureBase(type);
			ProjectileID.Sets.YoyosTopSpeed[type] = baseTop[type] * mult;
		}

		public static void BoostLifetime(int type, float mult)
		{
			EnsureBase(type);
			ProjectileID.Sets.YoyosLifeTimeMultiplier[type] = baseLife[type] * mult;
		}

		public static float BaseTopSpeed(int type)
		{
			EnsureBase(type);
			return baseTop[type];
		}

		public static float BaseRange(int type)
		{
			EnsureBase(type);
			return baseRange[type];
		}

		public static void ResetTracked()
		{
			foreach (var kv in baseRange)
				ProjectileID.Sets.YoyosMaximumRange[kv.Key] = kv.Value;
			foreach (var kv in baseTop)
				ProjectileID.Sets.YoyosTopSpeed[kv.Key] = kv.Value;
			foreach (var kv in baseLife)
				ProjectileID.Sets.YoyosLifeTimeMultiplier[kv.Key] = kv.Value;
		}

		public static void ForgetAll()
		{
			ResetTracked();
			baseRange.Clear();
			baseTop.Clear();
			baseLife.Clear();
		}
	}

	public class YoyoBoostSystem : ModSystem
	{
		public override void Load()
		{
			YoyoStaticBoosts.CaptureCleanBases();
		}

		public override void OnWorldLoad()
		{
			YoyoStaticBoosts.CaptureCleanBases();
		}

		public override void PostUpdateEverything()
		{
			YoyoStaticBoosts.ResetTracked();
		}

		public override void OnWorldUnload()
		{
			YoyoStaticBoosts.ForgetAll();
		}

		public override void Unload()
		{
			YoyoStaticBoosts.ForgetAll();
		}
	}

	// Makes Tinkerer modifications and Ancient spells actually change how a
	// player-thrown yoyo behaves. Reads the source item's enhancement data on
	// spawn and applies per-tick effects while deployed.
	public class YoyoEnhancementGlobalProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		public int ModId = -1;
		public int SpellId = -1;

		private int deployTicks;
		private float travelDist;
		private int origPen = int.MinValue;
		private bool harpoonDone;
		private int snapTimer;
		private int shieldWho = -1;
		private int echoTimer;
		private readonly System.Collections.Generic.Dictionary<int, int> stringHitCd = new();

		public override bool AppliesToEntity(Projectile projectile, bool lateInstantiation)
		{
			return projectile.aiStyle == ProjAIStyleID.Yoyo;
		}

		public override void OnSpawn(Projectile projectile, IEntitySource source)
		{
			Item src = null;
			if (source is EntitySource_ItemUse_WithAmmo ammoUse)
				src = ammoUse.Item;
			else if (source is EntitySource_ItemUse itemUse)
				src = itemUse.Item;
			if (src == null || src.IsAir)
				return;
			// Final gate: the source item itself must be yoyo-shaped, so a
			// modded melee weapon can never drive yoyo behavior downstream.
			if (!YoyoTownShared.IsYoyoShape(src))
				return;
			if (!src.TryGetGlobalItem<YoyoEnhancementGlobalItem>(out var enh))
				return;
			ModId = enh.ModificationId;
			SpellId = enh.SpellId;

			// Ancient Yoyo Glove: guides EVERY yoyo through enemies (+1 pierce
			// when finite) and 12% faster out of the hand - no mod required.
			if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers) {
				Player owner = Main.player[projectile.owner];
				if (owner != null && owner.active && owner.GetModPlayer<Players.MarketPlayer>().AncientGlove) {
					if (projectile.penetrate != -1)
						projectile.penetrate++;
					projectile.velocity *= 1.12f;
				}
			}

			var def = YoyoModificationDefs.ById(ModId);
			if (def == null)
				return;
			if (def.DamageMultiplier != 1f) // Heavy Core, Overclocked Bearings.
				projectile.damage = (int)(projectile.damage * def.DamageMultiplier);
			if (def.LaunchSpeedMultiplier != 1f) // Weighted Zinc Clutch, Heavy Core.
				projectile.velocity *= def.LaunchSpeedMultiplier;
			if (def.Id == 1) { // Weighted Zinc Clutch: bigger, +3 damage.
				projectile.scale *= def.SizeMultiplier;
				projectile.width = (int)(projectile.width * def.SizeMultiplier);
				projectile.height = (int)(projectile.height * def.SizeMultiplier);
				projectile.damage += def.BonusDamage;
			}
			if (def.Id == 4) { // Spoked Harpoon Axle: piercing spear for the first 30 tiles.
				origPen = projectile.penetrate;
				if (origPen != -1)
					projectile.penetrate = 99;
			}
			if (def.Id == 9) // Dimensional Quantum Spool: phases through blocks.
				projectile.tileCollide = false;
		}

		public override void AI(Projectile projectile)
		{
			// Ancient Yoyo Accessory: +3 tiles of reach on EVERY yoyo.
			if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers) {
				Player rangeOwner = Main.player[projectile.owner];
				if (rangeOwner != null && rangeOwner.active && rangeOwner.GetModPlayer<Players.MarketPlayer>().AncientRange) {
					YoyoStaticBoosts.BoostRange(projectile.type, 48f);
					YoyoStaticBoosts.BoostTopSpeed(projectile.type, 1.15f);
					YoyoStaticBoosts.BoostLifetime(projectile.type, 1.4f);
				}
			}
			// String accessories work on EVERY deployed yoyo, modded or not.
			StringEffects(projectile);
			StringContactDamage(projectile);
			if (ModId < 0 && SpellId < 0)
				return;
			deployTicks++;

			if (ModId == 0) // High-Tensile Spool: +2 tiles range.
				YoyoStaticBoosts.BoostRange(projectile.type, 32f);
			var activeDef = YoyoModificationDefs.ById(ModId);
			if (activeDef != null && activeDef.TopSpeedMultiplier != 1f) // Wind Vanes, Overclocked Bearings.
				YoyoStaticBoosts.BoostTopSpeed(projectile.type, activeDef.TopSpeedMultiplier);
			if (activeDef != null && activeDef.LifetimeMultiplier != 1f) // Second Wind.
				YoyoStaticBoosts.BoostLifetime(projectile.type, activeDef.LifetimeMultiplier);

			if (ModId == 12) // Echo Chamber: shockwave pulse every 2 seconds.
				EchoPulse(projectile);

			if (ModId == 4 && !harpoonDone) { // Spoked Harpoon Axle.
				travelDist += projectile.velocity.Length();
				projectile.ArmorPenetration = 30;
				if (travelDist >= 480f) {
					harpoonDone = true;
					projectile.ArmorPenetration = 0;
					if (origPen != int.MinValue)
						projectile.penetrate = origPen;
				}
			}

			if (snapTimer > 0) { // Kinetic Snap-Spring: +30% velocity for 1.5s after a hit.
				snapTimer--;
				float top = YoyoStaticBoosts.BaseTopSpeed(projectile.type) * 1.3f;
				if (projectile.velocity.Length() < top && projectile.velocity.Length() > 0.5f) {
					projectile.velocity *= 1.03f;
					if (Main.rand.NextBool(4)) {
						Dust dust = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.Electric, 0f, 0f, 120, default, 0.7f);
						dust.noGravity = true;
					}
				}
			}

			if (ModId == 5) // Magnetic Tractor Wire: pull pickups to the yoyo.
				TractorPull(projectile);

			if (ModId == 6) // Orbi-Shield Counterweight: maintain the orbiting ring.
				MaintainShield(projectile);

			if (ModId == 8 && projectile.owner == Main.myPlayer) { // Thrust Vector Thrusters: strong WASD steering.
				var triggers = PlayerInput.Triggers.Current;
				Vector2 push = Vector2.Zero;
				if (triggers.Up) push.Y -= 1f;
				if (triggers.Down) push.Y += 1f;
				if (triggers.Left) push.X -= 1f;
				if (triggers.Right) push.X += 1f;
				if (push != Vector2.Zero) {
					push = push.SafeNormalize(Vector2.Zero);
					// Thrust scales with the HELD yoyo's top speed (live set
					// value, so spools count) so fast yoyos stay steerable
					// and slow ones still feel the push.
					Player owner = Main.player[projectile.owner];
					float topSpeed = YoyoStaticBoosts.BaseTopSpeed(projectile.type);
					Item held = owner.HeldItem;
					if (held != null && !held.IsAir && held.shoot > ProjectileID.None
						&& held.shoot < ProjectileID.Sets.YoyosTopSpeed.Length)
						topSpeed = ProjectileID.Sets.YoyosTopSpeed[held.shoot];
					float speedScale = topSpeed / 11f;
					speedScale = System.Math.Clamp(speedScale, 0.75f, 2f);
					Vector2 target = projectile.Center + push * (2.0f + 4.5f * speedScale);
					// Leash against the LIVE range so a spooled yoyo keeps its bonus reach.
					float maxRange = ProjectileID.Sets.YoyosMaximumRange[projectile.type];
					if (Vector2.Distance(target, owner.Center) <= maxRange + 32f)
						projectile.Center = target;
					projectile.velocity += push * (0.5f + 0.8f * speedScale);
					if (Main.rand.NextBool(6)) {
						Dust dust = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.Torch, -push.X * 2f, -push.Y * 2f, 120, default, 0.7f);
						dust.noGravity = true;
					}
				}
			}
		}

		private static bool IsPickup(int type)
		{
			return type == ItemID.CopperCoin || type == ItemID.SilverCoin || type == ItemID.GoldCoin || type == ItemID.PlatinumCoin
				|| type == ItemID.Heart || type == ItemID.Star;
		}

		// String visuals + string-accessory hit behavior for every deployed yoyo:
		// golden/celestial dust along the line, spiky barbed barbs, tangled wisps,
		// Tangled range, Barbed/Tangled bleeds, and the Ancient-string magnet that
		// routes pickups through the main yoyo for instant collection.
		private static void StringEffects(Projectile projectile)
		{
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;
			// Player strings only: NPC-cast yoyos share the owner's index but
			// must not inherit string accessories, dust or magnets.
			string ns = projectile.ModProjectile?.GetType().Namespace ?? string.Empty;
			if (ns.Contains("TownNPC") || projectile.hostile)
				return;
			Player owner = Main.player[projectile.owner];
			if (owner == null || !owner.active)
				return;
			MarketPlayer market = owner.GetModPlayer<MarketPlayer>();
			RevampPlayer revamp = owner.GetModPlayer<RevampPlayer>();

			if (revamp.Tangled)
				YoyoStaticBoosts.BoostRange(projectile.type, 50f);

			if ((market.GoldenString || market.AncientString) && Main.rand.NextBool(4)) {
				float t = Main.rand.NextFloat(0.1f, 0.95f);
				Vector2 pos = Vector2.Lerp(owner.Center, projectile.Center, t);
				Dust dust = Dust.NewDustDirect(pos - new Vector2(4, 4), 8, 8,
					market.AncientString ? DustID.MagicMirror : DustID.GoldCoin, 0f, -0.4f, 120, default, 0.7f);
				dust.noGravity = true;
			}

			if (market.AncientString)
				AncientMagnet(projectile, owner);
		}

		// The main yoyo is the owner's lowest-slot active yoyo: duplicates
		// from the glove/bag spawn after it, so they never steal pickups.
		private static bool IsMainYoyo(Projectile projectile)
		{
			for (int i = 0; i < Main.maxProjectiles; i++) {
				if (i == projectile.whoAmI)
					return true;
				Projectile other = Main.projectile[i];
				if (other.active && other.owner == projectile.owner && other.aiStyle == ProjAIStyleID.Yoyo)
					return false;
			}
			return true;
		}

		// Barbed/Tangled strings are real weapons: the line itself cuts
		// anything it touches, dummies included. Per-NPC cooldown lives on
		// the yoyo instance.
		private void StringContactDamage(Projectile projectile)
		{
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return;
			if (!projectile.friendly || projectile.hostile)
				return;
			string ns = projectile.ModProjectile?.GetType().Namespace ?? string.Empty;
			if (ns.Contains("TownNPC"))
				return;
			Player owner = Main.player[projectile.owner];
			if (owner == null || !owner.active)
				return;
			Players.RevampPlayer revamp = owner.GetModPlayer<Players.RevampPlayer>();
			if (!revamp.Barbed && !revamp.Tangled)
				return;
			// Tick down per-NPC cooldowns.
			var expired = new System.Collections.Generic.List<int>();
			foreach (var kv in stringHitCd) {
				int left = kv.Value - 1;
				if (left <= 0)
					expired.Add(kv.Key);
				else
					stringHitCd[kv.Key] = left;
			}
			foreach (int key in expired)
				stringHitCd.Remove(key);
			if (Main.myPlayer != projectile.owner)
				return;
			float mult = revamp.Barbed ? 0.7f : 0.2f;
			int bleed = revamp.Barbed ? 180 : 120;
			Vector2 start = owner.Center;
			Vector2 diff = projectile.Center - start;
			float length = diff.Length();
			if (length < 20f)
				return;
			int steps = System.Math.Min(40, (int)(length / 10f));
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;
				if (stringHitCd.ContainsKey(i))
					continue;
				bool touched = false;
				for (int s = 1; s <= steps; s++) {
					Vector2 p = start + diff * (s / (float)steps);
					if (Vector2.DistanceSquared(p, npc.Center) < 14f * 14f) {
						touched = true;
						break;
					}
				}
				if (!touched)
					continue;
				stringHitCd[i] = 25;
				int dmg = System.Math.Max(1, (int)(projectile.damage * mult));
				NPC.HitInfo hit = new() {
					Damage = dmg,
					Knockback = npc.type == NPCID.TargetDummy ? 0f : 2f,
					HitDirection = System.Math.Sign(npc.Center.X - start.X),
					Crit = false
				};
				npc.StrikeNPC(hit, false, false);
				npc.AddBuff(BuffID.Bleeding, bleed);
				npc.netUpdate = true;
				for (int d = 0; d < 4; d++) {
					Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
						revamp.Barbed ? DustID.Blood : DustID.WoodFurniture, 0f, -1f, 100, default, 0.8f);
					dust.noGravity = true;
				}
			}
		}

		private static void AncientMagnet(Projectile projectile, Player owner)
		{			// Pickups route through the MAIN deployed yoyo only: nothing
			// moves while no yoyo is out, and duplicates never split the pull.
			if (!IsMainYoyo(projectile))
				return;
			for (int i = 0; i < Main.maxItems; i++) {
				Item item = Main.item[i];
				if (item == null || !item.active || item.IsAir || !IsPickup(item.type))
					continue;
				Vector2 toYoyo = projectile.Center - item.Center;
				float dist = toYoyo.Length();
				if (dist > 192f)
					continue;
				if (dist < 26f) {
					// Touching the main yoyo: hand straight to the player, collected instantly.
					item.Center = owner.Center;
					item.velocity = Vector2.Zero;
					continue;
				}
				item.velocity += toYoyo.SafeNormalize(Vector2.Zero) * 0.9f;
				if (item.velocity.Length() > 10f)
					item.velocity = item.velocity.SafeNormalize(Vector2.Zero) * 10f;
			}
		}

		private static void TractorPull(Projectile projectile)
		{
			const float radius = 192f; // 12 tiles.
			for (int i = 0; i < Main.maxItems; i++) {
				Item item = Main.item[i];
				if (item == null || !item.active || item.IsAir || !IsPickup(item.type))
					continue;
				Vector2 toYoyo = projectile.Center - item.Center;
				if (toYoyo.Length() > radius || toYoyo.Length() < 4f)
					continue;
				item.velocity += toYoyo.SafeNormalize(Vector2.Zero) * 0.8f;
				if (item.velocity.Length() > 9f)
					item.velocity = item.velocity.SafeNormalize(Vector2.Zero) * 9f;
			}
		}

		private void MaintainShield(Projectile projectile)
		{
			if (projectile.owner != Main.myPlayer)
				return;
			if (shieldWho >= 0 && shieldWho < Main.maxProjectiles) {
				Projectile shield = Main.projectile[shieldWho];
				if (shield.active && shield.ModProjectile is OrbiShieldProj && shield.ai[0] == projectile.whoAmI)
					return;
			}
			shieldWho = -1;
			// Deploy after 1 full second of active yoyo, not on a timeLeft
			// guess (short-lived yoyos never reached the old gate).
			if (deployTicks < 60)
				return;
			int idx = Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, Vector2.Zero,
				ModContent.ProjectileType<OrbiShieldProj>(), projectile.damage / 2, 2f, projectile.owner, ai0: projectile.whoAmI);
			if (idx >= 0)
				shieldWho = idx;
		}

		private void EchoPulse(Projectile projectile)
		{
			echoTimer++;
			if (echoTimer < 120)
				return;
			echoTimer = 0;
			for (int i = 0; i < 16; i++) {
				Vector2 dir = new Vector2(110f, 0f).RotatedBy(i / 16f * MathHelper.TwoPi);
				Dust dust = Dust.NewDustDirect(projectile.Center - new Vector2(4, 4), 8, 8, DustID.GoldCoin, dir.X * 0.06f, dir.Y * 0.06f, 100, default, 1.1f);
				dust.noGravity = true;
			}
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item94, projectile.Center);
			if (Main.myPlayer != projectile.owner)
				return;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;
				if (Vector2.DistanceSquared(projectile.Center, npc.Center) > 110f * 110f)
					continue;
				int dmg = (int)(projectile.damage * 0.5f);
				NPC.HitInfo hit = new() { Damage = dmg, Knockback = 3f, HitDirection = System.Math.Sign(npc.Center.X - projectile.Center.X) };
				npc.StrikeNPC(hit, false, false);
				npc.netUpdate = true;
			}
		}

		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (ModId == 7) { // Centrifugal Shredder Flaps: crit ramps to +15% over 4s deployed.
				float bonus = System.Math.Min(15f, deployTicks / 240f * 15f);
				if (bonus > 0.5f && Main.rand.NextFloat(100f) < bonus)
					modifiers.SetCrit();
			}
			if (ModId == 9) { // Quantum Spool: +30% damage while inside solid blocks.
				Point tile = projectile.Center.ToTileCoordinates();
				Tile t = Framing.GetTileSafely(tile.X, tile.Y);
				if (t.HasTile && Main.tileSolid[t.TileType])
					modifiers.SourceDamage *= 1.3f;
			}
		}

		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (ModId == 2) // Kinetic Snap-Spring triggers on hit.
				snapTimer = 90;
			// Ancient Yoyo Glove: hits spark off orbiting relic embers.
			if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers && Main.myPlayer == projectile.owner) {
				Player owner = Main.player[projectile.owner];
				if (owner != null && owner.active && owner.GetModPlayer<Players.MarketPlayer>().AncientGlove && Main.rand.NextBool(8)) {
					Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, Vector2.Zero,
						ModContent.ProjectileType<Projectiles.AncientSparkProj>(), (int)(projectile.damage * 0.35f), 1f, projectile.owner, ai0: projectile.whoAmI);
				}
				// Ancient Execution: yoyo kills burst into relic embers.
				if (owner != null && owner.active && target.life <= 0 && owner.GetModPlayer<Players.MarketPlayer>().AncientGlove) {
					for (int s = 0; s < 4; s++) {
						Projectile.NewProjectile(projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
							ModContent.ProjectileType<Projectiles.AncientSparkProj>(), (int)(projectile.damage * 0.25f), 1f, projectile.owner,
							ai0: projectile.whoAmI, ai1: Main.rand.NextFloat(MathHelper.TwoPi));
					}
				}
			}
			// Barbed/Tangled string hit effects.
			if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers) {
				Player owner = Main.player[projectile.owner];
				if (owner != null && owner.active) {
					RevampPlayer revamp = owner.GetModPlayer<RevampPlayer>();
					if (revamp.Barbed) {
						target.AddBuff(BuffID.Bleeding, 300);
						for (int i = 0; i < 10; i++) {
							int dtype = Main.rand.NextBool(3) ? DustID.Blood : DustID.SilverCoin;
							Dust dust = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, dtype, Main.rand.NextFloat(-3.5f, 3.5f), Main.rand.NextFloat(-3.5f, 3.5f), 100, default, Main.rand.NextFloat(0.8f, 1.2f));
							dust.noGravity = true;
						}
					}
					else if (revamp.Tangled) {
						target.AddBuff(BuffID.Bleeding, 180);
						for (int i = 0; i < 8; i++) {
							Dust dust = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.WoodFurniture, Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2.5f, -0.5f), 120, default, Main.rand.NextFloat(0.7f, 1.1f));
							dust.noGravity = true;
						}
					}
				}
			}
			if (SpellId < 0)
				return;
			var spell = YoyoSpellDefs.ById(SpellId);
			if (spell == null)
				return;
			foreach (int buff in spell.Debuffs)
				target.AddBuff(buff, buff == BuffID.Daybreak ? 600 : 300);
		}

	}
}
