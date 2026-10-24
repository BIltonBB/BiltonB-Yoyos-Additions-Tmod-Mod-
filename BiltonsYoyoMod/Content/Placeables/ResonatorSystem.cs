using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Yoyo Resonator world data: per-tile upgrade levels.
	//
	// UNIQUE EFFECT (chosen after codebase audit):
	// Dissonant Hum — yoyo hits weaken enemies, reducing the contact
	// damage they deal to players. Audit found damage, speed, range,
	// knockback, crit, defense, regen, mana, danger/treasure sense,
	// pickup magnets, pierce, phase-through, orbiting shields, periodic
	// shockwaves, WASD steering, crit ramps, string bleeds, debuff spells,
	// counterweight throws and life-regen-while-deployed all heavily used.
	// Enemy-damage weakening via yoyo hits was not substantially
	// represented, so it is the Resonator-exclusive mechanic. It is
	// yoyo-only (only yoyo projectiles apply it), radius-gated (attacker
	// must be near a Resonator), and distinct from Ward (ally buffs),
	// Rift (reposition burst) and Research (charge via impacts).
	//
	// Upgrades complement the weaken mechanic directly (no generic
	// damage/speed/range/knockback): Potency (% reduction), Duration,
	// Radius. Costs use existing mod mats + coins. Refund 70% on downgrade.
	// Breaking a block forgets its data with no refund (prevents
	// break/replace duplication); refunds only via UI downgrade.
	public class ResonatorData
	{
		public int Potency; // 0..3
		public int Duration; // 0..3
		public int Radius; // 0..12
		public int Focus; // 0 Balanced, 1 Wide, 2 Focused
		public int Feedback; // 0..3 bonus yoyo damage vs weakened foes
	}

	public class ResonatorSystem : ModSystem
	{
		public static readonly Dictionary<Point, ResonatorData> Tiles = new();

		public const int MaxPotency = 3;
		public const int MaxDuration = 3;
		public const int MaxRadius = 12;
		public const int MaxFocus = 2;
		public const int MaxFeedback = 3;

		// Base values at level 0.
		public static float WeakenPct(int potency) => potency switch {
			0 => 0.06f,
			1 => 0.10f,
			2 => 0.14f,
			_ => 0.18f,
		};

		public static int WeakenTicks(int duration) => duration switch {
			0 => 180, // 3s
			1 => 270, // 4.5s
			2 => 360, // 6s
			_ => 480, // 8s
		};

		// Radius: +2 tiles per level, 12 tiles at base up to 36 at max.
		// Tiers: 1-3 scraps, 4-6 parts, 7-9 advanced, 10-12 ancient.
		public static float RadiusPixels(int radius)
		{
			return 192f + System.Math.Clamp(radius, 0, MaxRadius) * 32f;
		}

		public static string FocusName(int focus) => focus switch {
			1 => "Wide",
			2 => "Focused",
			_ => "Balanced",
		};

		public static string FocusTip(int focus) => focus switch {
			1 => "Wide: +25% radius, -20% weaken. Blanket a fight.",
			2 => "Focused: +25% weaken, -20% radius. Guard one spot.",
			_ => "Balanced: full weaken at full radius.",
		};

		// Effective stats after the block's Hum Focus. Same mechanic,
		// retuned per block: no new systems, no generic stat soup.
		public static float EffWeakenPct(ResonatorData d)
		{
			return EffWeakenPct(d, 0);
		}

		// potencyBonus (+1 while station-networked, #12) raises the
		// effective Potency track, capped at max like a real upgrade.
		public static float EffWeakenPct(ResonatorData d, int potencyBonus)
		{
			int eff = System.Math.Clamp(d.Potency + potencyBonus, 0, MaxPotency);
			float pct = WeakenPct(eff);
			if (d.Focus == 2)
				pct *= 1.25f;
			else if (d.Focus == 1)
				pct *= 0.8f;
			return pct;
		}

		public static float EffRadiusPx(ResonatorData d)
		{
			float r = RadiusPixels(d.Radius);
			if (d.Focus == 1)
				r *= 1.25f;
			else if (d.Focus == 2)
				r *= 0.8f;
			return r;
		}

		// Upgrade costs: (coins copper, material type, count).
		public static (int coins, int mat, int count) PotencyCost(int nextLevel) => nextLevel switch {
			1 => (Item.buyPrice(gold: 2), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 10),
			2 => (Item.buyPrice(gold: 5), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 6),
			_ => (Item.buyPrice(gold: 12), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>(), 4),
		};

		public static (int coins, int mat, int count) DurationCost(int nextLevel) => nextLevel switch {
			1 => (Item.buyPrice(gold: 2), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 8),
			2 => (Item.buyPrice(gold: 5), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 5),
			_ => (Item.buyPrice(gold: 10), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>(), 3),
		};

		public static float FeedbackPct(int feedback) => feedback switch {
			1 => 0.08f,
			2 => 0.15f,
			3 => 0.25f,
			_ => 0f,
		};

		public static (int coins, int mat, int count) FeedbackCost(int nextLevel) => nextLevel switch {
			1 => (Item.buyPrice(gold: 5), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 6),
			2 => (Item.buyPrice(gold: 12), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>(), 4),
			_ => (Item.buyPrice(gold: 25), ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AncientScraps>(), 5),
		};

		public static (int coins, int mat, int count) RadiusCost(int nextLevel)
		{
			int scraps = ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>();
			int parts = ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>();
			int adv = ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>();
			int ancient = ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AncientScraps>();
			return nextLevel switch {
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

		public static ResonatorData GetOrCreate(Point key)
		{
			if (!Tiles.TryGetValue(key, out var data)) {
				data = new ResonatorData();
				Tiles[key] = data;
			}
			return data;
		}

		public static void ForgetTile(Point key)
		{
			if (Tiles.Remove(key))
				SyncWorld();
		}

		public static void SetFocus(Point key, int focus)
		{
			if (!IsTileStillResonator(key))
				return;
			GetOrCreate(key).Focus = System.Math.Clamp(focus, 0, MaxFocus);
			SyncWorld();
		}

		public static void SyncWorld()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		// True if player is within any Resonator radius. Returns the
		// strongest (highest potency, then duration) covering tile so
		// multiple Resonators do not stack — one effect applies.
		public static bool TryGetCoveringResonator(Player player, out Point tile, out ResonatorData data)
		{
			tile = default;
			data = null;
			float bestScore = -1f;
			foreach (var kv in Tiles) {
				if (!IsTileStillResonator(kv.Key))
					continue;
				Vector2 center = ResonatorCenter(kv.Key);
				float radius = EffRadiusPx(kv.Value);
				if (Vector2.DistanceSquared(player.Center, center) > radius * radius)
					continue;
				float score = EffWeakenPct(kv.Value, StationNetwork.ResonatorBonus(kv.Key)) * 1000f + WeakenTicks(System.Math.Min(kv.Value.Duration + StationNetwork.ResonatorBonus(kv.Key), MaxDuration));
				if (score > bestScore) {
					bestScore = score;
					tile = kv.Key;
					data = kv.Value;
				}
			}
			return data != null;
		}

		// Center of the 2x2 multitile in world pixels.
		public static Vector2 ResonatorCenter(Point key)
		{
			return new Vector2(key.X * 16f + 16f, key.Y * 16f + 16f);
		}

		public static bool IsTileStillResonator(Point key)
		{
			if (!WorldGen.InWorld(key.X, key.Y))
				return false;
			Tile t = Framing.GetTileSafely(key.X, key.Y);
			return t.HasTile && t.TileType == ModContent.TileType<Tiles.Placeables.YoyoResonatorTile>();
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
			var pot = new List<int>();
			var dur = new List<int>();
			var rad = new List<int>();
			var foc = new List<int>();
			var fb = new List<int>();
			foreach (var kv in Tiles) {
				if (!IsTileStillResonator(kv.Key))
					continue; // Never persist ghosts for destroyed blocks.
				xs.Add(kv.Key.X);
				ys.Add(kv.Key.Y);
				pot.Add(kv.Value.Potency);
				dur.Add(kv.Value.Duration);
				rad.Add(kv.Value.Radius);
				foc.Add(kv.Value.Focus);
				fb.Add(kv.Value.Feedback);
			}
			tag["resoX"] = xs;
			tag["resoY"] = ys;
			tag["resoP"] = pot;
			tag["resoD"] = dur;
			tag["resoR"] = rad;
			tag["resoF"] = foc;
			tag["resoFb"] = fb;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			Tiles.Clear();
			if (!tag.ContainsKey("resoX"))
				return;
			var xs = tag.Get<List<int>>("resoX");
			var ys = tag.Get<List<int>>("resoY");
			var pot = tag.Get<List<int>>("resoP");
			var dur = tag.Get<List<int>>("resoD");
			var rad = tag.Get<List<int>>("resoR");
			List<int> foc = tag.ContainsKey("resoF") ? tag.Get<List<int>>("resoF") : null;
			List<int> fb = tag.ContainsKey("resoFb") ? tag.Get<List<int>>("resoFb") : null;
			int n = System.Math.Min(xs.Count, System.Math.Min(ys.Count, System.Math.Min(pot.Count, System.Math.Min(dur.Count, rad.Count))));
			for (int i = 0; i < n; i++) {
				Tiles[new Point(xs[i], ys[i])] = new ResonatorData {
					Potency = System.Math.Clamp(pot[i], 0, MaxPotency),
					Duration = System.Math.Clamp(dur[i], 0, MaxDuration),
					Radius = System.Math.Clamp(rad[i], 0, MaxRadius),
					Focus = foc == null || i >= foc.Count ? 0 : System.Math.Clamp(foc[i], 0, MaxFocus),
					Feedback = fb == null || i >= fb.Count ? 0 : System.Math.Clamp(fb[i], 0, MaxFeedback),
				};
			}
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(Tiles.Count);
			foreach (var kv in Tiles) {
				writer.Write(kv.Key.X);
				writer.Write(kv.Key.Y);
				writer.Write(kv.Value.Potency);
				writer.Write(kv.Value.Duration);
				writer.Write(kv.Value.Radius);
				writer.Write(kv.Value.Focus);
				writer.Write(kv.Value.Feedback);
			}
		}

		public override void NetReceive(BinaryReader reader)
		{
			Tiles.Clear();
			int n = reader.ReadInt32();
			for (int i = 0; i < n; i++) {
				var key = new Point(reader.ReadInt32(), reader.ReadInt32());
				Tiles[key] = new ResonatorData {
					Potency = System.Math.Clamp(reader.ReadInt32(), 0, MaxPotency),
					Duration = System.Math.Clamp(reader.ReadInt32(), 0, MaxDuration),
					Radius = System.Math.Clamp(reader.ReadInt32(), 0, MaxRadius),
					Focus = System.Math.Clamp(reader.ReadInt32(), 0, MaxFocus),
					Feedback = System.Math.Clamp(reader.ReadInt32(), 0, MaxFeedback),
				};
			}
		}
	}
}
