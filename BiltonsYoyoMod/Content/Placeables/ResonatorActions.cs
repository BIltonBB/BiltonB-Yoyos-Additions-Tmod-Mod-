using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Authoritative purchase/downgrade logic for the Resonator.
	// Client validates wallet + inventory, deducts only on success.
	// World levels sync via ResonatorSystem.SyncWorld (server broadcast);
	// clients forward purchases to the server via PlaceableNet packets.
	public static class ResonatorActions
	{
		public const float RefundFraction = 0.7f;

		private static bool TryPay(Player player, int coins, int mat, int count, out string error)
		{
			error = "";
			if (!player.CanAfford(coins)) {
				error = $"Needs {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(coins)}.";
				return false;
			}
			int have = YoyoTownShared.CountInInventory(player, mat);
			if (have < count) {
				Item tmp = new Item(mat);
				error = $"Needs {count}x {tmp.Name} (have {have}).";
				return false;
			}
			player.PayCurrency(coins);
			YoyoTownShared.TryConsumeFromInventory(player, mat, count);
			return true;
		}

		private static void Refund(Player player, int coins, int mat, int count)
		{
			int rc = (int)(coins * RefundFraction);
			int rm = (int)(count * RefundFraction);
			if (rc > 0)
				SpawnCoins(player, rc);
			if (rm > 0)
				player.QuickSpawnItem(player.GetSource_Misc("ResonatorRefund"), mat, rm);
		}

		private static void SpawnCoins(Player player, int copper)
		{
			var source = player.GetSource_Misc("ResonatorRefund");
			while (copper >= 1000000) {
				player.QuickSpawnItem(source, ItemID.PlatinumCoin, 1);
				copper -= 1000000;
			}
			while (copper >= 10000) {
				player.QuickSpawnItem(source, ItemID.GoldCoin, 1);
				copper -= 10000;
			}
			while (copper >= 100) {
				player.QuickSpawnItem(source, ItemID.SilverCoin, 1);
				copper -= 100;
			}
			while (copper >= 1) {
				player.QuickSpawnItem(source, ItemID.CopperCoin, 1);
				copper -= 1;
			}
		}

		public static void BuyTrack(Player player, Microsoft.Xna.Framework.Point tile, int track)
		{
			if (track < 0 || track > 3) {
				ResonatorUISystem.SetMessage("Invalid upgrade.");
				return;
			}
			// Validate BEFORE creating storage: never leave ghost entries
			// for destroyed tiles (they would persist via world save).
			if (!ResonatorSystem.IsTileStillResonator(tile)) {
				ResonatorUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResonatorSystem.GetOrCreate(tile);
			int next;
			(int coins, int mat, int count) cost;
			if (track == 0) {
				if (data.Potency >= ResonatorSystem.MaxPotency) { ResonatorUISystem.SetMessage("Potency is maxed."); return; }
				next = data.Potency + 1;
				cost = ResonatorSystem.PotencyCost(next);
			}
			else if (track == 1) {
				if (data.Duration >= ResonatorSystem.MaxDuration) { ResonatorUISystem.SetMessage("Duration is maxed."); return; }
				next = data.Duration + 1;
				cost = ResonatorSystem.DurationCost(next);
			}
			else if (track == 2) {
				if (data.Radius >= ResonatorSystem.MaxRadius) { ResonatorUISystem.SetMessage("Radius is maxed."); return; }
				next = data.Radius + 1;
				cost = ResonatorSystem.RadiusCost(next);
			}
			else {
				if (data.Feedback >= ResonatorSystem.MaxFeedback) { ResonatorUISystem.SetMessage("Feedback is maxed."); return; }
				next = data.Feedback + 1;
				cost = ResonatorSystem.FeedbackCost(next);
			}
			if (!TryPay(player, cost.coins, cost.mat, cost.count, out string err)) {
				ResonatorUISystem.SetMessage(err);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				// Optimistic local apply + forward to server for authority.
				ApplyLevel(tile, track, next);
				PlaceableNet.SendResonatorBuy(tile, track, next);
			}
			else {
				ApplyLevel(tile, track, next);
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup, player.Center);
			ResonatorUISystem.SetMessage("Upgrade installed.");
		}

		public static void DowngradeTrack(Player player, Microsoft.Xna.Framework.Point tile, int track)
		{
			if (!ResonatorSystem.Tiles.TryGetValue(tile, out var data)) {
				ResonatorUISystem.SetMessage("No upgrades on this block.");
				return;
			}
			int cur;
			(int coins, int mat, int count) cost;
			if (track == 0) {
				cur = data.Potency;
				if (cur <= 0) { ResonatorUISystem.SetMessage("Potency already at base."); return; }
				cost = ResonatorSystem.PotencyCost(cur);
			}
			else if (track == 1) {
				cur = data.Duration;
				if (cur <= 0) { ResonatorUISystem.SetMessage("Duration already at base."); return; }
				cost = ResonatorSystem.DurationCost(cur);
			}
			else if (track == 2) {
				cur = data.Radius;
				if (cur <= 0) { ResonatorUISystem.SetMessage("Radius already at base."); return; }
				cost = ResonatorSystem.RadiusCost(cur);
			}
			else {
				cur = data.Feedback;
				if (cur <= 0) { ResonatorUISystem.SetMessage("Feedback already at base."); return; }
				cost = ResonatorSystem.FeedbackCost(cur);
			}
			int next = cur - 1;
			Refund(player, cost.coins, cost.mat, cost.count);
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				ApplyLevel(tile, track, next);
				PlaceableNet.SendResonatorBuy(tile, track, next);
			}
			else {
				ApplyLevel(tile, track, next);
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			ResonatorUISystem.SetMessage($"Downgraded (70% refund: {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins((int)(cost.coins * RefundFraction))} + {(int)(cost.count * RefundFraction)}x mats).");
		}

		// Server-authoritative apply (bounds-checked, tile must exist).
		public static void ApplyLevel(Microsoft.Xna.Framework.Point tile, int track, int level)
		{
			if (!ResonatorSystem.IsTileStillResonator(tile))
				return;
			var data = ResonatorSystem.GetOrCreate(tile);
			if (track == 0)
				data.Potency = System.Math.Clamp(level, 0, ResonatorSystem.MaxPotency);
			else if (track == 1)
				data.Duration = System.Math.Clamp(level, 0, ResonatorSystem.MaxDuration);
			else if (track == 2)
				data.Radius = System.Math.Clamp(level, 0, ResonatorSystem.MaxRadius);
			else
				data.Feedback = System.Math.Clamp(level, 0, ResonatorSystem.MaxFeedback);
			ResonatorSystem.SyncWorld();
		}

		// Hum Focus is a free retune (no cost): same mechanic, new shape.
		public static void ApplyFocus(Microsoft.Xna.Framework.Point tile, int focus)
		{
			if (!ResonatorSystem.IsTileStillResonator(tile))
				return;
			ResonatorSystem.SetFocus(tile, focus);
		}
	}
}
