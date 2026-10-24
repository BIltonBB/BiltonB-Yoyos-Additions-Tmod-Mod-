using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Authoritative purchase/downgrade logic for the Yoyo Ward.
	// Client validates wallet + inventory, deducts only on success.
	// World levels sync via YoyoWardSystem.SyncWorld (server broadcast);
	// clients forward purchases to the server via PlaceableNet packets.
	public static class WardActions
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
				Item tmp = new Item();
				tmp.SetDefaults(mat);
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
				player.QuickSpawnItem(player.GetSource_Misc("WardRefund"), mat, rm);
		}

		private static void SpawnCoins(Player player, int copper)
		{
			var source = player.GetSource_Misc("WardRefund");
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
				WardUISystem.SetMessage("Invalid upgrade.");
				return;
			}
			// Validate BEFORE creating storage: never leave ghost entries
			// for destroyed tiles (they would persist via world save).
			if (!YoyoWardSystem.IsTileStillWard(tile)) {
				WardUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = YoyoWardSystem.GetOrCreate(tile);
			int next;
			(int coins, int mat, int count) cost;
			if (track == 0) {
				if (data.Damage >= YoyoWardSystem.MaxDamage) { WardUISystem.SetMessage("Damage is maxed."); return; }
				next = data.Damage + 1;
				cost = YoyoWardSystem.DamageCost(next);
			}
			else if (track == 1) {
				if (data.Defense >= YoyoWardSystem.MaxDefense) { WardUISystem.SetMessage("Defense is maxed."); return; }
				next = data.Defense + 1;
				cost = YoyoWardSystem.DefenseCost(next);
			}
			else if (track == 2) {
				if (data.Vitality >= YoyoWardSystem.MaxVitality) { WardUISystem.SetMessage("Vitality is maxed."); return; }
				next = data.Vitality + 1;
				cost = YoyoWardSystem.VitalityCost(next);
			}
			else {
				if (data.Radius >= YoyoWardSystem.MaxRadius) { WardUISystem.SetMessage("Radius is maxed."); return; }
				next = data.Radius + 1;
				cost = YoyoWardSystem.RadiusCost(next);
			}
			if (!TryPay(player, cost.coins, cost.mat, cost.count, out string err)) {
				WardUISystem.SetMessage(err);
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				// Optimistic local apply + forward to server for authority.
				ApplyLevel(tile, track, next);
				PlaceableNet.SendWardBuy(tile, track, next);
			}
			else {
				ApplyLevel(tile, track, next);
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup, player.Center);
			WardUISystem.SetMessage("Upgrade installed.");
		}

		public static void DowngradeTrack(Player player, Microsoft.Xna.Framework.Point tile, int track)
		{
			if (!YoyoWardSystem.Tiles.TryGetValue(tile, out var data)) {
				WardUISystem.SetMessage("No upgrades on this block.");
				return;
			}
			int cur;
			(int coins, int mat, int count) cost;
			if (track == 0) {
				cur = data.Damage;
				if (cur <= 0) { WardUISystem.SetMessage("Damage already at base."); return; }
				cost = YoyoWardSystem.DamageCost(cur);
			}
			else if (track == 1) {
				cur = data.Defense;
				if (cur <= 0) { WardUISystem.SetMessage("Defense already at base."); return; }
				cost = YoyoWardSystem.DefenseCost(cur);
			}
			else if (track == 2) {
				cur = data.Vitality;
				if (cur <= 0) { WardUISystem.SetMessage("Vitality already at base."); return; }
				cost = YoyoWardSystem.VitalityCost(cur);
			}
			else {
				cur = data.Radius;
				if (cur <= 0) { WardUISystem.SetMessage("Radius already at base."); return; }
				cost = YoyoWardSystem.RadiusCost(cur);
			}
			int next = cur - 1;
			Refund(player, cost.coins, cost.mat, cost.count);
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				ApplyLevel(tile, track, next);
				PlaceableNet.SendWardBuy(tile, track, next);
			}
			else {
				ApplyLevel(tile, track, next);
			}
			Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
			WardUISystem.SetMessage($"Downgraded (70% refund: {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins((int)(cost.coins * RefundFraction))} + {(int)(cost.count * RefundFraction)}x mats).");
		}

		// Server-authoritative apply (bounds-checked, tile must exist).
		public static void ApplyLevel(Microsoft.Xna.Framework.Point tile, int track, int level)
		{
			if (!YoyoWardSystem.IsTileStillWard(tile))
				return;
			var data = YoyoWardSystem.GetOrCreate(tile);
			if (track == 0)
				data.Damage = System.Math.Clamp(level, 0, YoyoWardSystem.MaxDamage);
			else if (track == 1)
				data.Defense = System.Math.Clamp(level, 0, YoyoWardSystem.MaxDefense);
			else if (track == 2)
				data.Vitality = System.Math.Clamp(level, 0, YoyoWardSystem.MaxVitality);
			else
				data.Radius = System.Math.Clamp(level, 0, YoyoWardSystem.MaxRadius);
			YoyoWardSystem.SyncWorld();
		}
	}
}
