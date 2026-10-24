using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;

namespace BiltonsYoyoMod.Content.Customization
{
	// Authoritative application logic for station modifiers + reforge.
	// Browse purchases cost coins AND family resources (scaling with stars);
	// UI minigame wins waive both. Every apply validates the held yoyo, the
	// boss gate, THIS block's attunement and the wallet on the calling side.
	public static class YoyoStationActions
	{
		public static Item HeldYoyo(Player player)
		{
			Item held = player.HeldItem;
			if (held != null && !held.IsAir && YoyoTownShared.IsYoyo(held) && YoyoTownShared.IsYoyoShape(held))
				return held;
			return null;
		}

		public static int ScaleCoins(int baseCopper, int stars) => baseCopper + baseCopper * (stars - 1) / 2;

		public static int TuningPrice(bool extended, int stars = 1) => ScaleCoins(extended ? Item.buyPrice(platinum: 1) : Item.buyPrice(gold: 10), stars);
		public static int CalibrationPrice(bool extended, int stars = 1) => ScaleCoins(extended ? Item.buyPrice(platinum: 1) : Item.buyPrice(gold: 8), stars);
		public static int MasteryPrice(bool trueMastery, int stars = 1) => ScaleCoins(trueMastery ? Item.buyPrice(platinum: 3) : Item.buyPrice(platinum: 1), stars);

		private static string FamilyOf(bool tuning, bool calib, bool mastery, bool extendedOrTrue)
		{
			if (tuning) return extendedOrTrue ? "exttuning" : "tuning";
			if (calib) return extendedOrTrue ? "extcalib" : "calib";
			return extendedOrTrue ? "true" : "mastery";
		}

		// Cost helpers shared by browse purchases and minigame wins.
		// Browse pays full coins + full resources. A minigame win waives the
		// coins but still demands HALF the resources (rounded up).
		public static (int coins, int mat, int matCount) CostFor(int kind, int id)
		{
			bool ext = id >= 100;
			int stars = YoyoDifficulty.Stars(kind, id);
			int coins = kind == 0 ? TuningPrice(ext, stars) : kind == 1 ? CalibrationPrice(ext, stars) : MasteryPrice(ext, stars);
			var (mat, n) = YoyoDifficulty.CostMat(kind, ext, stars);
			return (coins, mat, n);
		}

		public static bool CheckCosts(Player player, int kind, bool extOrTrue, int id)
		{
			// Minigame path: coins waived, half resources (rounded up) required.
			int stars = YoyoDifficulty.Stars(kind, id);
			var (mat, n) = CostMatOnly(kind, extOrTrue, stars);
			int half = (n + 1) / 2;
			int have = YoyoTownShared.CountInInventory(player, mat);
			if (have < half) {
				Item tmp = new Item(mat);
				YoyoStationUISystem.SetMessage($"Needs {half}x {tmp.Name} for the rite (have {have}). No coins needed on a win.");
				return false;
			}
			return true;
		}

		public static bool PayCosts(Player player, int kind, bool extOrTrue, int id)
		{
			int stars = YoyoDifficulty.Stars(kind, id);
			var (mat, n) = CostMatOnly(kind, extOrTrue, stars);
			int half = (n + 1) / 2;
			int have = YoyoTownShared.CountInInventory(player, mat);
			if (have < half) {
				Item tmp = new Item(mat);
				YoyoStationUISystem.SetMessage($"Needs {half}x {tmp.Name} (have {have}).");
				return false;
			}
			YoyoTownShared.TryConsumeFromInventory(player, mat, half);
			return true;
		}

		private static (int mat, int matCount) CostMatOnly(int kind, bool extOrTrue, int stars) => YoyoDifficulty.CostMat(kind, extOrTrue, stars);

		private static bool TryPay(Player player, int coins, int mat, int matCount)
		{
			if (!player.CanAfford(coins)) {
				YoyoStationUISystem.SetMessage($"Needs {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(coins)}.");
				return false;
			}
			int have = YoyoTownShared.CountInInventory(player, mat);
			if (have < matCount) {
				Item tmp = new Item(mat);
				YoyoStationUISystem.SetMessage($"Needs {matCount}x {tmp.Name} (have {have}).");
				return false;
			}
			player.PayCurrency(coins);
			YoyoTownShared.TryConsumeFromInventory(player, mat, matCount);
			return true;
		}

		public static void ApplyTuning(Player player, YoyoTuningDef def)
		{
			Item held = HeldYoyo(player);
			if (held == null) {
				YoyoStationUISystem.SetMessage("Hold a yoyo in your hand first.");
				return;
			}
			string fam = FamilyOf(true, false, false, def.Extended);
			string locked = YoyoCustomizationSystem.TileFamilyLock(YoyoStationUISystem.OpenTile, fam);
			if (locked != null) {
				YoyoStationUISystem.SetMessage(locked);
				return;
			}
			// Extended tunings need a networked Research Engine (#17:
			// Station Networking LV1+ in range of this station).
			if (def.Extended && !global::BiltonsYoyoMod.Content.Placeables.StationNetwork.ModifierNetworkOk(out string netWhy)) {
				YoyoStationUISystem.SetMessage(netWhy);
				return;
			}
			int stars = YoyoDifficulty.Stars(0, def.Id);
			var (mat, n) = YoyoDifficulty.CostMat(0, def.Extended, stars);
			if (!TryPay(player, TuningPrice(def.Extended, stars), mat, n))
				return;
			var cust = held.GetGlobalItem<YoyoCustomizationGlobalItem>();
			string replaced = cust.TuningId >= 0 ? YoyoTuningDefs.ById(cust.TuningId)?.Name : null;
			cust.TuningId = def.Id; // Normal/Extended share the slot.
			YoyoCustomizationGlobalItem.RefreshNameOverride(held);
			SoundEngine.PlaySound(SoundID.CoinPickup);
			YoyoStationUISystem.SetMessage(replaced == null
				? $"Tuned {held.Name} with {def.Name}."
				: $"Replaced {replaced} with {def.Name} on {held.Name}. Other modifiers kept.");
		}

		public static void ApplyCalibration(Player player, YoyoCalibrationDef def)
		{
			Item held = HeldYoyo(player);
			if (held == null) {
				YoyoStationUISystem.SetMessage("Hold a yoyo in your hand first.");
				return;
			}
			string fam = FamilyOf(false, true, false, def.Extended);
			string locked = YoyoCustomizationSystem.TileFamilyLock(YoyoStationUISystem.OpenTile, fam);
			if (locked != null) {
				YoyoStationUISystem.SetMessage(locked);
				return;
			}
			// Extended calibrations need a networked Research Engine (#17).
			if (def.Extended && !global::BiltonsYoyoMod.Content.Placeables.StationNetwork.ModifierNetworkOk(out string netWhy)) {
				YoyoStationUISystem.SetMessage(netWhy);
				return;
			}
			int stars = YoyoDifficulty.Stars(1, def.Id);
			var (mat, n) = YoyoDifficulty.CostMat(1, def.Extended, stars);
			if (!TryPay(player, CalibrationPrice(def.Extended, stars), mat, n))
				return;
			var cust = held.GetGlobalItem<YoyoCustomizationGlobalItem>();
			string replaced = cust.CalibrationId >= 0 ? YoyoCalibrationDefs.ById(cust.CalibrationId)?.Name : null;
			cust.CalibrationId = def.Id;
			YoyoCustomizationGlobalItem.RefreshNameOverride(held);
			SoundEngine.PlaySound(SoundID.CoinPickup);
			YoyoStationUISystem.SetMessage(replaced == null
				? $"Calibrated {held.Name} with {def.Name}."
				: $"Replaced {replaced} with {def.Name} on {held.Name}. Other modifiers kept.");
		}

		public static void ApplyMastery(Player player, YoyoMasteryDef def)
		{
			Item held = HeldYoyo(player);
			if (held == null) {
				YoyoStationUISystem.SetMessage("Hold a yoyo in your hand first.");
				return;
			}
			string fam = FamilyOf(false, false, true, def.TrueMastery);
			string locked = YoyoCustomizationSystem.TileFamilyLock(YoyoStationUISystem.OpenTile, fam);
			if (locked != null) {
				YoyoStationUISystem.SetMessage(locked);
				return;
			}
			// True masteries need a networked Research Engine (#17).
			if (def.TrueMastery && !global::BiltonsYoyoMod.Content.Placeables.StationNetwork.ModifierNetworkOk(out string netWhy)) {
				YoyoStationUISystem.SetMessage(netWhy);
				return;
			}
			int stars = YoyoDifficulty.Stars(2, def.Id);
			var (mat, n) = YoyoDifficulty.CostMat(2, def.TrueMastery, stars);
			if (!TryPay(player, MasteryPrice(def.TrueMastery), mat, n))
				return;
			var cust = held.GetGlobalItem<YoyoCustomizationGlobalItem>();
			string replaced = cust.MasteryId >= 0 ? YoyoMasteryDefs.ById(cust.MasteryId)?.Name : null;
			cust.MasteryId = def.Id; // Mastery/True share the slot.
			YoyoCustomizationGlobalItem.RefreshNameOverride(held);
			SoundEngine.PlaySound(SoundID.CoinPickup);
			YoyoStationUISystem.SetMessage(replaced == null
				? $"Taught {held.Name} {def.Name}."
				: $"Replaced {replaced} with {def.Name} on {held.Name}. Other modifiers kept.");
		}

		public static void StripSlot(Player player, string slot)
		{
			Item held = HeldYoyo(player);
			if (held == null) {
				YoyoStationUISystem.SetMessage("Hold a yoyo in your hand first.");
				return;
			}
			var cust = held.GetGlobalItem<YoyoCustomizationGlobalItem>();
			bool has = slot switch {
				"tuning" => cust.TuningId >= 0,
				"calib" => cust.CalibrationId >= 0,
				"mastery" => cust.MasteryId >= 0,
				"curse" => cust.CurseId >= 0,
				_ => false,
			};
			if (!has) {
				YoyoStationUISystem.SetMessage("That slot is already empty.");
				return;
			}
			int price = Item.buyPrice(gold: 1);
			if (!player.CanAfford(price)) {
				YoyoStationUISystem.SetMessage("Stripping costs 1 gold.");
				return;
			}
			player.PayCurrency(price);
			switch (slot) {
				case "tuning": cust.TuningId = -1; break;
				case "calib": cust.CalibrationId = -1; break;
				case "mastery": cust.MasteryId = -1; break;
				case "curse": cust.CurseId = -1; break;
			}
			YoyoCustomizationGlobalItem.RefreshNameOverride(held);
			SoundEngine.PlaySound(SoundID.MenuTick);
			YoyoStationUISystem.SetMessage($"Stripped {slot} from {held.Name}. Others kept.");
		}

		public static void DoReforge(Player player)
		{
			Item held = HeldYoyo(player);
			if (held == null) {
				YoyoStationUISystem.SetMessage("Hold a yoyo in your hand first.");
				return;
			}
			int price = YoyoCustomizationSystem.GetReforgePrice(held);
			if (!player.CanAfford(price)) {
				YoyoStationUISystem.SetMessage($"Reforge needs {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(price)} (today's rate).");
				return;
			}
			player.PayCurrency(price);
			// Ordinary Terraria-compatible prefixes only, yoyo-filtered by vanilla.
			bool ok = held.Prefix(-2);
			if (held.TryGetGlobalItem<Common.YoyoEnhancementGlobalItem>(out _))
				Common.YoyoEnhancementGlobalItem.RefreshNameOverride(held);
			YoyoCustomizationGlobalItem.RefreshNameOverride(held);
			SoundEngine.PlaySound(ok ? SoundID.CoinPickup : SoundID.MenuTick);
			YoyoStationUISystem.SetMessage(ok
				? $"Reforged to {held.Name} for {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(price)}. Custom modifiers kept."
				: "The machine hums but finds no better prefix. Modifiers kept.");
		}

		// Opens the in-UI attunement trial for a family at the open block.
		public static void StartTrialFromStation(Player player, string family)
		{
			int station = YoyoStationUISystem.Station;
			int trial = YoyoCustomizationSystem.FamilyTrial(family);
			if (!Trials.YoyoTrialActions.TrialRequirementMet(player, trial)) {
				YoyoStationUISystem.SetMessage("Requires: " + Trials.YoyoTrialActions.TrialRequirementText(trial) + ".");
				return;
			}
			if (YoyoCustomizationSystem.IsTileUnlocked(YoyoStationUISystem.OpenTile, family)) {
				YoyoStationUISystem.SetMessage("This block is already attuned for " + YoyoCustomizationSystem.FamilyTitle(family) + ".");
				return;
			}
			YoyoStationUISystem.Tab = 1;
			Minigames.StationMinigames.StartTrial(station, family);
		}
	}
}
