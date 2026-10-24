using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Customization
{
	// Attunement ledger: every PLACED station block unlocks separately. Clearing a
	// block's UI trial attunes THAT block permanently; a second block of the same
	// station starts locked and must earn its own attunement. Scope is per-tile,
	// saved per world and synced to clients. Breaking a block forgets its entry.
	public class YoyoCustomizationSystem : ModSystem
	{
		public static readonly Dictionary<Point, HashSet<string>> TileUnlocks = new();

		// Adjustment Machine daily reforge state: persists, rerolls only on new day.
		public static int ReforgeDay = -1;
		public static float ReforgeMult = 1f;
		public static int ReforgeLowCopper = 20000; // 2 gold
		public static int ReforgeHighCopper = 150000; // 15 gold

		public static int CurrentDay => (int)(Main.GameUpdateCount / 86400L);

		// Family keys: tuning, calib, mastery, exttuning, extcalib, true.
		public static string[] StationFamilies(int station) => station switch {
			1 => new[] { "tuning" },
			2 => new[] { "mastery" },
			3 => new[] { "calib" },
			4 => new[] { "exttuning", "extcalib" },
			5 => new[] { "mastery", "true" },
			_ => new string[0],
		};

		public static string FamilyTitle(string fam) => fam switch {
			"tuning" => "Tuning",
			"calib" => "Calibration",
			"mastery" => "Mastery",
			"exttuning" => "Extended Tuning",
			"extcalib" => "Extended Calibration",
			"true" => "True Mastery",
			_ => fam,
		};

		// Boss-gate trial index per family (shared with the old trial table).
		public static int FamilyTrial(string fam) => fam switch {
			"tuning" => 0,
			"calib" => 1,
			"mastery" => 2,
			"exttuning" => 3,
			"extcalib" => 4,
			_ => 5,
		};

		public static bool IsTileUnlocked(Point key, string fam)
		{
			return TileUnlocks.TryGetValue(key, out var set) && set.Contains(fam);
		}

		public static void UnlockTile(Point key, string fam)
		{
			if (!TileUnlocks.TryGetValue(key, out var set)) {
				set = new HashSet<string>();
				TileUnlocks[key] = set;
			}
			set.Add(fam);
			SyncWorld();
		}

		public static void ForgetTile(Point key)
		{
			if (TileUnlocks.Remove(key))
				SyncWorld();
		}

		// Boss gate first, then this-block attunement.
		public static string TileFamilyLock(Point key, string fam)
		{
			int trial = FamilyTrial(fam);
			if (!Trials.YoyoTrialActions.TrialRequirementMet(Main.LocalPlayer, trial))
				return "Defeat: " + Trials.YoyoTrialActions.TrialRequirementText(trial);
			if (!IsTileUnlocked(key, fam))
				return $"This block is not attuned ({FamilyTitle(fam)}). Clear its attunement trial below.";
			return null;
		}

		public override void OnWorldLoad()
		{
			TileUnlocks.Clear();
			ReforgeDay = -1;
			ReforgeMult = 1f;
		}

		public override void SaveWorldData(TagCompound tag)
		{
			var keys = new List<int>();
			var fams = new List<string>();
			foreach (var kv in TileUnlocks) {
				foreach (string fam in kv.Value) {
					keys.Add(kv.Key.X);
					keys.Add(kv.Key.Y);
					fams.Add(fam);
				}
			}
			tag["yoyoTileKeys"] = keys;
			tag["yoyoTileFams"] = fams;
			tag["yoyoReforgeDay"] = ReforgeDay;
			tag["yoyoReforgeMult"] = ReforgeMult;
		}

		public override void LoadWorldData(TagCompound tag)
		{
			TileUnlocks.Clear();
			if (tag.ContainsKey("yoyoTileKeys") && tag.ContainsKey("yoyoTileFams")) {
				var keys = tag.Get<List<int>>("yoyoTileKeys");
				var fams = tag.Get<List<string>>("yoyoTileFams");
				int n = System.Math.Min(fams.Count, keys.Count / 2);
				for (int i = 0; i < n; i++) {
					var key = new Point(keys[i * 2], keys[i * 2 + 1]);
					if (!TileUnlocks.TryGetValue(key, out var set)) {
						set = new HashSet<string>();
						TileUnlocks[key] = set;
					}
					set.Add(fams[i]);
				}
			}
			ReforgeDay = tag.ContainsKey("yoyoReforgeDay") ? tag.GetInt("yoyoReforgeDay") : -1;
			ReforgeMult = tag.ContainsKey("yoyoReforgeMult") ? tag.GetFloat("yoyoReforgeMult") : 1f;
		}

		public override void NetSend(BinaryWriter writer)
		{
			writer.Write(TileUnlocks.Count);
			foreach (var kv in TileUnlocks) {
				writer.Write(kv.Key.X);
				writer.Write(kv.Key.Y);
				writer.Write(kv.Value.Count);
				foreach (string fam in kv.Value)
					writer.Write(fam);
			}
			writer.Write(ReforgeDay);
			writer.Write(ReforgeMult);
		}

		public override void NetReceive(BinaryReader reader)
		{
			TileUnlocks.Clear();
			int tiles = reader.ReadInt32();
			for (int i = 0; i < tiles; i++) {
				var key = new Point(reader.ReadInt32(), reader.ReadInt32());
				int fams = reader.ReadInt32();
				var set = new HashSet<string>();
				for (int f = 0; f < fams; f++)
					set.Add(reader.ReadString());
				TileUnlocks[key] = set;
			}
			ReforgeDay = reader.ReadInt32();
			ReforgeMult = reader.ReadSingle();
		}

		public static void SyncWorld()
		{
			if (Main.netMode == NetmodeID.Server)
				NetMessage.SendData(MessageID.WorldData);
		}

		// Daily reforge multiplier in [0.7, 1.3]. Only rerolls when the day flips.
		public static float GetReforgeMult()
		{
			int day = CurrentDay;
			if (ReforgeDay != day) {
				ReforgeDay = day;
				ReforgeMult = Main.rand.NextFloat(0.7f, 1.3f);
				SyncWorld();
			}
			return ReforgeMult;
		}

		// Goblin-style base scaled by item value, clamped into the daily low/high band.
		public static int GetReforgePrice(Item item)
		{
			float mult = GetReforgeMult();
			int vanBase = System.Math.Max(5000, item.value / 3);
			int price = (int)(vanBase * mult);
			price = System.Math.Clamp(price, ReforgeLowCopper, ReforgeHighCopper);
			return price;
		}
	}
}
