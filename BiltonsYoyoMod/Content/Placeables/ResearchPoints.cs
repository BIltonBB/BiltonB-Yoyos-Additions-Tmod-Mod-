using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Research Points (#2): per-block earned/spent pool.
	//
	// Gains depend on research: completing a research grants points scaled
	// by that research's length, and passive study trickles points every
	// 10s scaled by owned tech count (needs 1+ techs to start). Points live
	// in the block that earned them and are spent there (rushing active
	// jobs, trial Rush Orders and Fresh Leads, energizing charge). Storage
	// is capped; the cap is upgraded in the Research tab.
	// Later branches (#5 storage/speed, #9 field research, #10 salvaging,
	// #11 experimental) all feed this same pool.
	public static class ResearchPoints
	{
		public const int BaseCap = 500;
		public const int CapPerUpgrade = 250;
		public const int MaxUpgrades = 8;
		public const int RushCost = 25;
		public const int RushTicks = 1800; // 30s off the job
		public const int TrickleEveryTicks = 600; // 10s
		public const int RushCostEfficient = 18;
		public const int VaultBonus = 500;
		public const int EnergizeCost = 20;
		public const int EnergizeCharge = 8;

		private static int PartsType() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>();

		public static int CapOf(ResearchData d)
		{
			int cap = BaseCap + CapPerUpgrade * System.Math.Clamp(d?.PointsUp ?? 0, 0, MaxUpgrades);
			if (ResearchTech.TechOn(d, ResearchTech.YieldT1))
				cap += VaultBonus;
			return cap;
		}

		// Completion award multiplier (#5 gains): +25% per gains tech.
		public static float AwardMult(ResearchData d)
		{
			float mult = 1f;
			if (ResearchTech.TechOn(d, ResearchTech.YieldG1))
				mult += 0.25f;
			if (ResearchTech.TechOn(d, ResearchTech.YieldG2))
				mult += 0.25f;
			return mult;
		}

		// Rush cost (#5 consumption): 25, or 18 with Efficiency.
		public static int RushCostOf(ResearchData d)
		{
			return ResearchTech.TechOn(d, ResearchTech.YieldC1) ? RushCostEfficient : RushCost;
		}

		// Completion award scales with research length: 60s -> 30,
		// 120s -> 40, 240s -> 60, 360s -> 80, 600s -> 120.
		public static int AwardFor(ResearchTechNode node)
		{
			if (node == null)
				return 0;
			return 20 + ResearchTech.ResearchSecs(node) / 6;
		}

		// Passive study per trickle: nothing with no techs, then
		// 1 + techs/2 (1 tech -> 1, 6 techs -> 4, 12 techs -> 7),
		// plus #5 speed techs (+2 each).
		public static int PassivePerTrickle(ResearchData d)
		{
			int n = d?.TechCount ?? 0;
			if (n < 1)
				return 0;
			int gain = 1 + n / 2;
			if (ResearchTech.TechOn(d, ResearchTech.YieldS1))
				gain += 2;
			if (ResearchTech.TechOn(d, ResearchTech.YieldS2))
				gain += 2;
			// Timed event condition (#8: High Tide/Drought) and research
			// scaling (#14: slightly more per owned tech).
			return System.Math.Max(0, (int)System.Math.Round(gain * ResearchEvents.ActiveTrickleMult(d) * ResearchTech.ScalingMult(d)));
		}

		// Stores up to the cap. Returns the amount actually stored.
		public static int Add(ResearchData d, int amount)
		{
			if (d == null || amount <= 0)
				return 0;
			int room = System.Math.Max(0, CapOf(d) - d.Points);
			int stored = System.Math.Min(room, amount);
			if (stored > 0) {
				d.Points += stored;
				d.LifetimePoints = System.Math.Max(0, d.LifetimePoints + stored);
				ResearchEngineSystem.SyncWorld();
			}
			return stored;
		}

		public static bool TrySpend(ResearchData d, int cost)
		{
			if (d == null || cost <= 0)
				return false;
			if (d.Points < cost)
				return false;
			d.Points -= cost;
			ResearchEngineSystem.SyncWorld();
			return true;
		}

		// Completion award. Returns (stored, lost-to-full-cap).
		// Deterministic per node, so the tick runs it on all sides and
		// the server rebroadcast converges (same pattern as Unlock).
		public static (int stored, int lost) OnComplete(ResearchData d, ResearchTechNode node)
		{
			if (d == null || node == null)
				return (0, 0);
			int award = (int)(AwardFor(node) * AwardMult(d) * ResearchTech.ScalingMult(d));
			int stored = Add(d, award);
			return (stored, award - stored);
		}

		// Server/single-player only; clients converge via WorldData.
		public static void Trickle(ResearchData d)
		{
			int gain = PassivePerTrickle(d);
			if (gain > 0)
				Add(d, gain);
		}

		public static int CapCostCoins(ResearchData d)
		{
			int level = System.Math.Clamp(d?.PointsUp ?? 0, 0, MaxUpgrades);
			return Item.buyPrice(gold: 5) * (level + 1);
		}

		public static int CapCostMats(ResearchData d)
		{
			int level = System.Math.Clamp(d?.PointsUp ?? 0, 0, MaxUpgrades);
			return 5 * (level + 1);
		}

		public static string CapCostText(ResearchData d)
		{
			Item tmp = new Item();
			tmp.SetDefaults(PartsType());
			return $"{Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(CapCostCoins(d))} + {CapCostMats(d)}x {tmp.Name}";
		}

		// Client-side purchase: pays locally, applies optimistically,
		// server applies authoritatively via packet (like research buys).
		public static void BuyCapUpgrade(Player player, Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (data.PointsUp >= MaxUpgrades) {
				ResearchEngineUISystem.SetMessage("Storage maxed.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			int costCoins = CapCostCoins(data);
			int costMats = CapCostMats(data);
			if (!player.CanAfford(costCoins)) {
				ResearchEngineUISystem.SetMessage($"Needs {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(costCoins)}.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			int have = Content.NPCs.Town.YoyoTownShared.CountInInventory(player, PartsType());
			if (have < costMats) {
				Item tmp = new Item();
				tmp.SetDefaults(PartsType());
				ResearchEngineUISystem.SetMessage($"Needs {costMats}x {tmp.Name} (have {have}).");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			player.PayCurrency(costCoins);
			Content.NPCs.Town.YoyoTownShared.TryConsumeFromInventory(player, PartsType(), costMats);
			ApplyCapUpgrade(tile);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchCap(tile);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup, player.Center);
			ResearchEngineUISystem.SetMessage($"Storage expanded: cap {CapOf(data)} (Lv {data.PointsUp}).");
		}

		// Authoritative state change (bounds-checked, tile-validated).
		public static void ApplyCapUpgrade(Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (data.PointsUp >= MaxUpgrades)
				return;
			data.PointsUp++;
			ResearchEngineSystem.SyncWorld();
		}

		// Spends points to shave 30s off one active job.
		public static void RushJob(Player player, Point tile, string id)
		{
			var node = ResearchTech.ById(id);
			if (node == null)
				return;
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			var job = ResearchTech.FindJob(data, id);
			if (job == null || job.Left <= 0) {
				ResearchEngineUISystem.SetMessage(job == null ? "Nothing rushing." : "Already done.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			int cost = RushCostOf(data);
			if (!TrySpend(data, cost)) {
				ResearchEngineUISystem.SetMessage($"Needs {cost} research points (have {data.Points}).");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			job.Left = System.Math.Max(0, job.Left - RushTicks);
			ResearchEngineSystem.SyncWorld();
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchRush(tile, id);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, player.Center);
			ResearchEngineUISystem.SetMessage($"Rushed {node.Name} (-30s).");
		}

		// Authoritative rush (payment happened client-side; the server
		// spends from its own copy so the rebroadcast converges).
		public static void ApplyRush(Point tile, string id)
		{
			var node = ResearchTech.ById(id);
			if (node == null || !ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			var job = ResearchTech.FindJob(data, id);
			if (job == null || job.Left <= 0)
				return;
			if (!TrySpend(data, RushCostOf(data)))
				return;
			job.Left = System.Math.Max(0, job.Left - RushTicks);
			ResearchEngineSystem.SyncWorld();
		}

		// Energize (#16 sink): turns points straight into charge.
		// Respects the boss-fight 100 lockout (#7) like yoyo hits do.
		public static void Energize(Player player, Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			var rp = player.GetModPlayer<ResearchEnginePlayer>();
			int add = EnergizeCharge;
			if (rp.BossCharged && ResearchEngineSystem.AnyBossActive())
				add = System.Math.Min(add, System.Math.Max(0, ResearchEngineSystem.MaxChargeOf(data) - 1 - data.Charge));
			if (add <= 0) {
				ResearchEngineUISystem.SetMessage("Boss lockout: no full charge this fight.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			if (data.Charge >= ResearchEngineSystem.MaxChargeOf(data)) {
				ResearchEngineUISystem.SetMessage("Already fully charged.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			if (!TrySpend(data, EnergizeCost)) {
				ResearchEngineUISystem.SetMessage($"Needs {EnergizeCost} research points (have {data.Points}).");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			ResearchEngineSystem.AddCharge(tile, add);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchEnergize(tile, player.whoAmI);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, player.Center);
			ResearchEngineUISystem.SetMessage($"Energized +{add} charge.");
		}

		// Authoritative energize (points spent from the server copy).
		public static void ApplyEnergize(Point tile, int playerWho)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			int add = EnergizeCharge;
			if (playerWho >= 0 && playerWho < Main.maxPlayers) {
				Player p = Main.player[playerWho];
				if (p != null && p.active) {
					var rp = p.GetModPlayer<ResearchEnginePlayer>();
					if (rp != null && rp.BossCharged && ResearchEngineSystem.AnyBossActive())
						add = System.Math.Min(add, System.Math.Max(0, ResearchEngineSystem.MaxChargeOf(data) - 1 - data.Charge));
				}
			}
			if (add <= 0)
				return;
			if (data.Charge >= ResearchEngineSystem.MaxChargeOf(data))
				return;
			if (!TrySpend(data, EnergizeCost))
				return;
			ResearchEngineSystem.AddCharge(tile, add);
		}
	}
}
