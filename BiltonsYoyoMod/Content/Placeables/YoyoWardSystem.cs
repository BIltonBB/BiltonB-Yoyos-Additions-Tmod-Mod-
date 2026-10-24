using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Yoyo Ward world data: per-tile upgrade levels.
	//
	// Unlike the Resonator (yoyo debuff), the Ward buffs allies.
	// Covers housed town NPCs, night visitors AND town pets of this mod
	// (never hostiles or bosses).
	// Base bonuses (level 0, modest): +12% damage, +6 defense, +20% max
	// life, 12-tile radius. Upgrades complement those exact stats
	// (no new mechanics): Damage / Defense / Vitality / Radius tracks.
	// Costs use existing mod mats + coins, 70% refund on downgrade.
	// Breaking forgets with no refund (prevents break/replace dup).
	// Multiple wards never stack: strongest covering ward wins.
	public class WardData
	{
		public int Damage; // 0..3
		public int Defense; // 0..3
		public int Vitality; // 0..3
		public int Radius; // 0..2
	}

	public class YoyoWardSystem : ModSystem
	{
		public static readonly Dictionary<Point, WardData> Tiles = new();

		public const int MaxDamage = 3;
		public const int MaxDefense = 3;
		public const int MaxVitality = 3;
		public const int MaxRadius = 12;

		public static float DamagePct(int l) => l switch {
			1 => 16f,
			2 => 20f,
			3 => 25f,
			_ => 12f,
		};

		public static int DefenseBonus(int l) => l switch {
			1 => 9,
			2 => 12,
			3 => 15,
			_ => 6,
		};

		public static float LifePct(int l) => l switch {
			1 => 28f,
			2 => 36f,
			3 => 45f,
			_ => 20f,
		};

		// Radius: +2 tiles per level, 12 tiles at base up to 36 at max.
		// Tiers: 1-3 scraps, 4-6 parts, 7-9 advanced, 10-12 ancient.
		public static float RadiusPixels(int l)
		{
			return 192f + System.Math.Clamp(l, 0, MaxRadius) * 32f;
		}

		public static (int coins, int mat, int count) DamageCost(int next) => next switch {
			1 => (Item.buyPrice(gold: 2), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 10),
			2 => (Item.buyPrice(gold: 5), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 6),
			_ => (Item.buyPrice(gold: 12), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>(), 4),
		};

		public static (int coins, int mat, int count) DefenseCost(int next) => next switch {
			1 => (Item.buyPrice(gold: 2), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 8),
			2 => (Item.buyPrice(gold: 5), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 5),
			_ => (Item.buyPrice(gold: 10), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>(), 3),
		};

		public static (int coins, int mat, int count) VitalityCost(int next) => next switch {
			1 => (Item.buyPrice(gold: 2), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 8),
			2 => (Item.buyPrice(gold: 6), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 5),
			_ => (Item.buyPrice(gold: 12), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>(), 4),
		};

		public static (int coins, int mat, int count) RadiusCost(int next)
		{
			int scraps = ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>();
			int parts = ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>();
			int adv = ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>();
			int ancient = ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AncientScraps>();
			return next switch {
				1 => (Item.buyPrice(gold: 2), scraps, 8),
				2 => (Item.buyPrice(gold: 3), scraps, 12),
				3 => (Item.buyPrice(gold: 5), scraps, 16),
				4 => (Item.buyPrice(gold: 6), parts, 6),
				5 => (Item.buyPrice(gold: 8), parts, 10),
				6 => (Item.buyPrice(gold: 10), parts, 14),
				7 => (Item.buyPrice(gold: 12), adv, 4),
				8 => (Item.buyPrice(gold: 16), adv, 6),
				9 => (Item.buyPrice(gold: 20), adv, 8),
				10 => (Item.buyPrice(gold: 25), ancient, 5),
				11 => (Item.buyPrice(gold: 35), ancient, 8),
				_ => (Item.buyPrice(gold: 50), ancient, 12),
			};
		}

		public static WardData GetOrCreate(Point key)
		{
			if (!Tiles.TryGetValue(key, out var data)) {
				data = new WardData();
				Tiles[key] = data;
			}
			return data;
		}

		public static void ForgetTile(Point key)
		{
			if (Tiles.Remove(key))
				SyncWorld();
		}

		public static void SyncWorld()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		public static bool IsTileStillWard(Point key)
		{
			if (!WorldGen.InWorld(key.X, key.Y))
				return false;
			Tile t = Framing.GetTileSafely(key.X, key.Y);
			return t.HasTile && t.TileType == ModContent.TileType<Tiles.Placeables.YoyoWardTile>();
		}

		// Effective coverage radius: Sanctuary capstone widens full-mesh
		// wards by 50%. Display and coverage share it (no under-report).
		public static float EffRadiusPx(WardData data, Point tile)
		{
			float radius = RadiusPixels(data.Radius);
			if (StationNetwork.IsR30(tile))
				radius *= 1.5f;
			return radius;
		}

		// Center of the 2x2 multitile in world pixels.
		public static Vector2 WardCenter(Point key)
		{
			return new Vector2(key.X * 16f + 16f, key.Y * 16f + 16f);
		}

		// Strongest covering ward for a position (no stacking).
		public static bool TryGetCoveringWard(Vector2 pos, out Point tile, out WardData data)
		{
			tile = default;
			data = null;
			float best = -1f;
			foreach (var kv in Tiles) {
				if (!IsTileStillWard(kv.Key))
					continue;
				float radius = EffRadiusPx(kv.Value, kv.Key);
				if (Vector2.DistanceSquared(pos, WardCenter(kv.Key)) > radius * radius)
					continue;
				int net = StationNetwork.WardBonus(kv.Key);
				int effDmg = System.Math.Min(kv.Value.Damage + net, MaxDamage);
				int effDef = System.Math.Min(kv.Value.Defense + net, MaxDefense);
				int effVit = System.Math.Min(kv.Value.Vitality + net, MaxVitality);
				float score = DamagePct(effDmg) + DefenseBonus(effDef) + LifePct(effVit);
				if (score > best) {
					best = score;
					tile = kv.Key;
					data = kv.Value;
				}
			}
			return data != null;
		}

		public override void PostUpdateEverything()
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active)
					continue;
				if (!IsEligible(npc))
					continue;
				var g = npc.GetGlobalNPC<YoyoWardGlobalNPC>();
				if (TryGetCoveringWard(npc.Center, out var wtile, out var data))
					g.ApplyWard(npc, data, StationNetwork.WardBonus(wtile));
				else
					g.RemoveWard(npc);
			}
		}

		public static bool IsEligible(NPC npc)
		{
			if (!npc.active || npc.boss)
				return false;
			if (npc.ModNPC == null)
				return false;
			// Only this mod's yoyo NPCs.
			if (npc.ModNPC.Mod != ModContent.GetInstance<BiltonsYoyoMod>())
				return false;
			string name = npc.ModNPC.GetType().Name;
			if (npc.lifeMax <= 5 || npc.dontTakeDamage)
				return false;
			// Town pets share the ward's shelter (Billy, Magical Yoyo).
			if (name.Contains("Billy") || name.Contains("MagicalYoyoPet") || (npc.townNPC && NPCID.Sets.IsTownPet[npc.type]))
				return true;
			// Housed town yoyo NPCs (Master, Tinkerer, Ancient Yoyoist).
			if (npc.townNPC && npc.friendly)
				return true;
			// Night visitors defend the settlement too (Six Stringer, Cultist).
			if (!npc.townNPC && npc.friendly && (name.Contains("SixStringer") || name.Contains("YoyoCultist")))
				return true;
			return false;
		}

		// Optional small bonus for NPCs that actively fight: +5% damage
		// while their target is active. Kept tiny; no combat overhaul.
		public static bool IsFighting(NPC npc)
		{
			return npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].active;
		}

		public static int CountAffected(Point origin)
		{
			if (!Tiles.TryGetValue(origin, out var data))
				return 0;
			float radius = EffRadiusPx(data, origin);
			Vector2 center = WardCenter(origin);
			int n = 0;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || !IsEligible(npc))
					continue;
				if (Vector2.DistanceSquared(npc.Center, center) <= radius * radius)
					n++;
			}
			return n;
		}

		public static System.Collections.Generic.List<string> GetAffectedNames(Point origin, int max)
		{
			var names = new System.Collections.Generic.List<string>();
			foreach (var a in GetAffectedAllies(origin, max))
				names.Add(a.name);
			return names;
		}

		public static System.Collections.Generic.List<(string name, int life, int lifeMax)> GetAffectedAllies(Point origin, int max)
		{
			var allies = new System.Collections.Generic.List<(string name, int life, int lifeMax)>();
			if (!Tiles.TryGetValue(origin, out var data))
				return allies;
			float radius = EffRadiusPx(data, origin);
			Vector2 center = WardCenter(origin);
			for (int i = 0; i < Main.maxNPCs && allies.Count < max; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || !IsEligible(npc))
					continue;
				if (Vector2.DistanceSquared(npc.Center, center) <= radius * radius)
					allies.Add((npc.FullName, npc.life, npc.lifeMax));
			}
			return allies;
		}

		public override void OnWorldLoad()
		{
			Tiles.Clear();
		}

		public override void OnWorldUnload()
		{
			Tiles.Clear();
		}

		public override void SaveWorldData(TagCompound tag)
		{
			var xs = new List<int>();
			var ys = new List<int>();
			var da = new List<int>();
			var de = new List<int>();
			var vi = new List<int>();
			var ra = new List<int>();
			foreach (var kv in Tiles) {
				if (!IsTileStillWard(kv.Key))
					continue; // Never persist ghosts for destroyed blocks.
				xs.Add(kv.Key.X);
				ys.Add(kv.Key.Y);
				da.Add(kv.Value.Damage);
				de.Add(kv.Value.Defense);
				vi.Add(kv.Value.Vitality);
				ra.Add(kv.Value.Radius);
			}
			tag["wardX"] = xs;
			tag["wardY"] = ys;
			tag["wardDa"] = da;
			tag["wardDe"] = de;
			tag["wardVi"] = vi;
			tag["wardRa"] = ra;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Tiles.Clear();
			if (!tag.ContainsKey("wardX"))
				return;
			var xs = tag.Get<List<int>>("wardX");
			var ys = tag.Get<List<int>>("wardY");
			var da = tag.Get<List<int>>("wardDa");
			var de = tag.Get<List<int>>("wardDe");
			var vi = tag.Get<List<int>>("wardVi");
			var ra = tag.Get<List<int>>("wardRa");
			int n = xs.Count;
			n = System.Math.Min(n, ys.Count);
			n = System.Math.Min(n, da.Count);
			n = System.Math.Min(n, de.Count);
			n = System.Math.Min(n, vi.Count);
			n = System.Math.Min(n, ra.Count);
			for (int i = 0; i < n; i++) {
				Tiles[new Point(xs[i], ys[i])] = new WardData {
					Damage = System.Math.Clamp(da[i], 0, MaxDamage),
					Defense = System.Math.Clamp(de[i], 0, MaxDefense),
					Vitality = System.Math.Clamp(vi[i], 0, MaxVitality),
					Radius = System.Math.Clamp(ra[i], 0, MaxRadius),
				};
			}
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(Tiles.Count);
			foreach (var kv in Tiles) {
				writer.Write(kv.Key.X);
				writer.Write(kv.Key.Y);
				writer.Write(kv.Value.Damage);
				writer.Write(kv.Value.Defense);
				writer.Write(kv.Value.Vitality);
				writer.Write(kv.Value.Radius);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			Tiles.Clear();
			int n = reader.ReadInt32();
			for (int i = 0; i < n; i++) {
				var key = new Point(reader.ReadInt32(), reader.ReadInt32());
				Tiles[key] = new WardData {
					Damage = System.Math.Clamp(reader.ReadInt32(), 0, MaxDamage),
					Defense = System.Math.Clamp(reader.ReadInt32(), 0, MaxDefense),
					Vitality = System.Math.Clamp(reader.ReadInt32(), 0, MaxVitality),
					Radius = System.Math.Clamp(reader.ReadInt32(), 0, MaxRadius),
				};
			}
		}
	}

	// Per-player Ward radius-ring preference. Persisted, client-side fx only.
	public class WardPlayer : ModPlayer
	{
		public bool ShowRing = true;

		public override void PostUpdate()
		{
			// Bright blue coverage rings around nearby wards. Count scales
			// with circumference so big radii read as clearly as small ones.
			// Rapid re-spawns interleave into a solid-looking band.
			if (Main.myPlayer != Player.whoAmI || Main.dedServ || !ShowRing)
				return;
			if (Main.GameUpdateCount % 6 != 0)
				return;
			foreach (var kv in YoyoWardSystem.Tiles) {
				if (!YoyoWardSystem.IsTileStillWard(kv.Key))
					continue;
				Vector2 center = YoyoWardSystem.WardCenter(kv.Key);
				if (Vector2.DistanceSquared(Player.Center, center) > 2500f * 2500f)
					continue;
				float radius = YoyoWardSystem.EffRadiusPx(kv.Value, kv.Key);
				PlaceableUI.DustRing(center, radius, DustID.Electric, PlaceableUI.RingCount(radius), 1.2f);
				Lighting.AddLight(center, 0.1f, 0.25f, 0.45f);
			}
		}

		public override void SaveData(TagCompound tag)
		{
			tag["wardRing"] = ShowRing;
		}

		public override void LoadData(TagCompound tag)
		{
			ShowRing = !tag.ContainsKey("wardRing") || tag.GetBool("wardRing");
		}
	}

	public class YoyoWardGlobalNPC : GlobalNPC
	{		public override bool InstancePerEntity => true;

		private bool warded;
		private int baseDamage;
		private int baseDefense;
		private int baseLifeMax;
		private bool hasBase;

		public void ApplyWard(NPC npc, WardData data)
		{
			ApplyWard(npc, data, 0);
		}

		// netBonus (+1 while station-networked, #12) raises the effective
		// Damage and Defense tracks, capped like real upgrades.
		public void ApplyWard(NPC npc, WardData data, int netBonus)
		{
			if (!hasBase) {
				baseDamage = npc.damage;
				baseDefense = npc.defense;
				baseLifeMax = npc.lifeMax;
				hasBase = true;
			}
			if (warded) {
				// Refresh fighting bonus state without re-applying.
				RefreshFighting(npc, data, netBonus);
				WardSparkle(npc);
				return;
			}
			warded = true;
			int effDmg = System.Math.Min(data.Damage + netBonus, YoyoWardSystem.MaxDamage);
			int effDef = System.Math.Min(data.Defense + netBonus, YoyoWardSystem.MaxDefense);
			int effVit = System.Math.Min(data.Vitality + netBonus, YoyoWardSystem.MaxVitality);
			int newDamage = (int)(baseDamage * (1f + YoyoWardSystem.DamagePct(effDmg) / 100f));
			// Fighting extension: +5% more while actively targeting.
			if (YoyoWardSystem.IsFighting(npc))
				newDamage = (int)(newDamage * 1.05f);
			npc.damage = System.Math.Max(1, newDamage);
			npc.defense = baseDefense + YoyoWardSystem.DefenseBonus(effDef);
			int newMax = (int)(baseLifeMax * (1f + YoyoWardSystem.LifePct(effVit) / 100f));
			newMax = System.Math.Max(baseLifeMax + 1, newMax);
			int delta = newMax - npc.lifeMax;
			npc.lifeMax = newMax;
			npc.life = System.Math.Clamp(npc.life + System.Math.Max(0, delta), 1, npc.lifeMax);
			WardSparkle(npc, burst: true);
		}

		private void RefreshFighting(NPC npc, WardData data)
		{
			RefreshFighting(npc, data, 0);
		}

		private void RefreshFighting(NPC npc, WardData data, int netBonus)
		{
			// Keep damage in sync with fighting state without compounding:
			// always recompute from base.
			int effDmg = System.Math.Min(data.Damage + netBonus, YoyoWardSystem.MaxDamage);
			int effDef = System.Math.Min(data.Defense + netBonus, YoyoWardSystem.MaxDefense);
			int newDamage = (int)(baseDamage * (1f + YoyoWardSystem.DamagePct(effDmg) / 100f));
			if (YoyoWardSystem.IsFighting(npc))
				newDamage = (int)(newDamage * 1.05f);
			npc.damage = System.Math.Max(1, newDamage);
			npc.defense = baseDefense + YoyoWardSystem.DefenseBonus(effDef);
			// Vitality follows the live bonus both ways: grows heal up,
			// shrinks never cut current life below the new max.
			int effVit = System.Math.Min(data.Vitality + netBonus, YoyoWardSystem.MaxVitality);
			int newMax = System.Math.Max(baseLifeMax + 1, (int)(baseLifeMax * (1f + YoyoWardSystem.LifePct(effVit) / 100f)));
			int delta = newMax - npc.lifeMax;
			npc.lifeMax = newMax;
			npc.life = System.Math.Clamp(npc.life + System.Math.Max(0, delta), 1, npc.lifeMax);
		}

		private static void WardSparkle(NPC npc, bool burst = false)
		{
			if (Main.dedServ)
				return;
			int chance = burst ? 1 : 60;
			if (!Main.rand.NextBool(chance))
				return;
			int n = burst ? 8 : 1;
			for (int i = 0; i < n; i++) {
				Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Grass, 0f, -1f, 120, default, 0.9f);
				d.noGravity = true;
				d.velocity *= 0.4f;
			}
		}

		public void RemoveWard(NPC npc)
		{
			if (!warded || !hasBase)
				return;
			warded = false;
			npc.damage = baseDamage;
			npc.defense = baseDefense;
			npc.life = System.Math.Clamp(npc.life, 1, baseLifeMax);
			npc.lifeMax = baseLifeMax;
		}

		public override void OnKill(NPC npc)
		{
			warded = false;
			hasBase = false;
		}
	}
}
