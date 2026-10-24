using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Experimental trials (#11): the Trial Board posts 3 quest-like
	// offers per engine, refreshed every in-game dawn. Accept one or
	// ignore it. Amounts randomize per board (high-cost/high-gain or the
	// opposite); mystery contracts hide a randomized benefit-or-negative
	// until accepted.
	//
	// Offers are instant transactions (no tracked objectives) packed as
	// "template:cost:mats:reward". Board rolls run server/single-side
	// only; clients converge via WorldData. Mystery outcomes derive
	// deterministically from the packed seed so both sides agree.
	public static class ResearchTrials
	{
		public const int OfferSlots = 3;
		public const int RerollCost = 50;
		public const int TrialFallbackPts = 100;

		public const string Funding = "funding";
		public const string Firesale = "firesale";
		public const string Trial = "trial";
		public const string Rushorder = "rushorder";
		public const string Mystery = "mystery";

		private static int Parts() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>();
		private static int Adv() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>();

		public static string OfferName(string template) => template switch {
			Funding => "Surge Funding",
			Firesale => "Fire Sale",
			Trial => "Live Trial",
			Rushorder => "Rush Order",
			Mystery => "Mystery Contract",
			_ => "Unknown Offer",
		};

		public static bool IsTemplate(string template)
		{
			return template == Funding || template == Firesale || template == Trial || template == Rushorder || template == Mystery;
		}

		// Packs "template:cost:mats:reward". Meaning per template:
		// funding/firesale = coins/mats/points, trial = charge/secs,
		// rushorder = points cost, mystery = coins/seed.
		public static bool TryParseOffer(string packed, out string template, out int cost, out int mats, out int reward)
		{
			template = "";
			cost = 0;
			mats = 0;
			reward = 0;
			if (string.IsNullOrEmpty(packed))
				return false;
			string[] bits = packed.Split(':');
			if (bits.Length != 4 || !IsTemplate(bits[0]))
				return false;
			if (!int.TryParse(bits[1], out cost) || !int.TryParse(bits[2], out mats) || !int.TryParse(bits[3], out reward))
				return false;
			if (cost < 0 || mats < 0)
				return false;
			template = bits[0];
			return true;
		}

		private static string Pack(string template, int cost, int mats, int reward)
		{
			return $"{template}:{cost}:{mats}:{reward}";
		}

		// Deals 3 distinct templates with randomized amounts (doubled
		// with High Stakes). Server/single-side only.
		public static void RollOffers(Point tile, ResearchData d)
		{
			if (d == null || !ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			int mult = ResearchTech.TechOn(d, ResearchTech.ExpStakes) ? 2 : 1;
			var pool = new List<string> { Funding, Firesale, Trial, Rushorder, Mystery };
			d.ExpOffers ??= new List<string>();
			d.ExpOffers.Clear();
			for (int k = 0; k < OfferSlots && pool.Count > 0; k++) {
				int pick = Main.rand.Next(pool.Count);
				string t = pool[pick];
				pool.RemoveAt(pick);
				d.ExpOffers.Add(Deal(t, mult));
			}
			while (d.ExpOffers.Count < OfferSlots)
				d.ExpOffers.Add("");
			ResearchEngineSystem.SyncWorld();
		}

		private static string Deal(string template, int mult)
		{
			return template switch {
				Funding => Pack(template, Item.buyPrice(platinum: Main.rand.Next(1, 3)) * mult, Main.rand.Next(10, 21) * mult, Main.rand.Next(200, 501) * mult),
				Firesale => Pack(template, Item.buyPrice(gold: Main.rand.Next(5, 16)) * mult, Main.rand.Next(3, 9) * mult, Main.rand.Next(20, 61) * mult),
				Trial => Pack(template, 50 * mult, 0, 120 * mult),
				Rushorder => Pack(template, Main.rand.Next(100, 201) * mult, 0, 0),
				Mystery => Pack(template, Item.buyPrice(gold: Main.rand.Next(30, 81)) * mult, 0, Main.rand.Next()),
				_ => "",
			};
		}

		public static string CostText(ResearchData d, string template, int cost, int mats)
		{
			string coins = Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(cost);
			return template switch {
				Trial => $"{cost} charge",
				Rushorder => $"{cost} pts",
				Funding or Firesale => mats > 0 ? $"{coins} + {mats}x {MatName(template)}" : coins,
				Mystery => coins,
				_ => coins,
			};
		}

		public static string RewardText(ResearchData d, string template, int reward)
		{
			return template switch {
				Funding or Firesale => $"+{reward} pts",
				Trial => $"-{reward}s all jobs",
				Rushorder => "Completes shortest job now",
				Mystery => ResearchTech.TechOn(d, ResearchTech.ExpIntel) ? $"Odds: {OddsText(d)}" : "Outcome unknown.",
				_ => "",
			};
		}

		private static string MatName(string template)
		{
			Item tmp = new Item();
			tmp.SetDefaults(template == Firesale ? Parts() : Adv());
			return tmp.Name;
		}

		private static int MatType(string template) => template == Firesale ? Parts() : Adv();

		// Mystery odds triplet (good/nothing/bad), Loaded Dice tilts good.
		public static (int good, int nothing, int bad) Odds(ResearchData d)
		{
			return ResearchTech.TechOn(d, ResearchTech.ExpDice) ? (75, 15, 10) : (60, 25, 15);
		}

		public static string OddsText(ResearchData d)
		{
			var (good, nothing, bad) = Odds(d);
			return $"{good}/{nothing}/{bad} good/quiet/backfire";
		}

		// Mystery outcome from the packed seed: deterministic so client
		// and server agree without an extra packet.
		private static int MysteryRoll(int seed, ResearchData d)
		{
			var (good, nothing, _) = Odds(d);
			int r = System.Math.Abs(seed) % 100;
			if (r < good)
				return 0; // jackpot
			if (r < good + nothing)
				return 1; // quiet
			return 2; // backfire
		}

		public static void Accept(Player player, Point tile, int slot)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (slot < 0 || slot >= OfferSlots || data.ExpOffers == null || slot >= data.ExpOffers.Count) {
				ResearchEngineUISystem.SetMessage("No such offer.");
				return;
			}
			if (!TryParseOffer(data.ExpOffers[slot], out string template, out int cost, out int mats, out int reward)) {
				ResearchEngineUISystem.SetMessage("Offer expired.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			if (!ApplyCost(player, tile, data, template, cost, mats))
				return; // message + tick already handled
			ApplyReward(player, tile, data, template, cost, mats, reward);
			data.ExpOffers[slot] = "";
			ResearchEngineSystem.SyncWorld();
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchAccept(tile, slot);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup, player.Center);
		}

		// Validates and pays the offer cost. False = told the player why.
		private static bool ApplyCost(Player player, Point tile, ResearchData data, string template, int cost, int mats)
		{
			if (template == Trial) {
				if (data.Charge < cost) {
					ResearchEngineUISystem.SetMessage($"Needs {cost} charge (have {data.Charge}). Farm it with yoyo hits.");
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
					return false;
				}
				data.Charge -= cost;
				return true;
			}
			if (template == Rushorder) {
				if (!ResearchPoints.TrySpend(data, cost)) {
					ResearchEngineUISystem.SetMessage($"Needs {cost} research points (have {data.Points}).");
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
					return false;
				}
				return true;
			}
			// Coin (+ optional mats) offers: funding, firesale, mystery.
			if (!player.CanAfford(cost)) {
				ResearchEngineUISystem.SetMessage($"Needs {Content.UI.YoyoNPC.YoyoNPCPanel.FormatCoins(cost)}.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return false;
			}
			if (mats > 0) {
				int type = MatType(template);
				int have = Content.NPCs.Town.YoyoTownShared.CountInInventory(player, type);
				if (have < mats) {
					Item tmp = new Item();
					tmp.SetDefaults(type);
					ResearchEngineUISystem.SetMessage($"Needs {mats}x {tmp.Name} (have {have}).");
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
					return false;
				}
			}
			player.PayCurrency(cost);
			if (mats > 0)
				Content.NPCs.Town.YoyoTownShared.TryConsumeFromInventory(player, MatType(template), mats);
			return true;
		}

		private static void ApplyReward(Player player, Point tile, ResearchData data, string template, int cost, int mats, int reward)
		{
			switch (template) {
				case Funding:
				case Firesale:
					ResearchPoints.Add(data, reward);
					ResearchEngineUISystem.SetMessage($"{OfferName(template)} accepted: +{reward} pts.");
					break;
				case Trial: {
					bool moved = false;
					if (data.Research != null) {
						foreach (var job in data.Research) {
							job.Left = System.Math.Max(0, job.Left - reward * 60);
							moved = true;
						}
					}
					if (!moved)
						ResearchPoints.Add(data, TrialFallbackPts);
					ResearchEngineUISystem.SetMessage(moved ? $"{OfferName(template)} accepted: jobs leap forward." : $"{OfferName(template)} accepted: no jobs, +{TrialFallbackPts} pts instead.");
					break;
				}
				case Rushorder: {
					// Completes the shortest job (two with High Stakes).
					int count = ResearchTech.TechOn(data, ResearchTech.ExpStakes) ? 2 : 1;
					int done = 0;
					for (int k = 0; k < count; k++) {
						ResearchJob shortest = null;
						if (data.Research != null) {
							foreach (var job in data.Research) {
								if (job.Left <= 0)
									continue;
								if (shortest == null || job.Left < shortest.Left)
									shortest = job;
							}
						}
						if (shortest == null)
							break;
						shortest.Left = 0;
						done++;
					}
					if (done == 0)
						ResearchPoints.Add(data, cost); // no jobs: refund the points
					ResearchEngineSystem.SyncWorld();
					ResearchEngineUISystem.SetMessage(done > 0 ? $"{OfferName(template)} accepted: {done} job(s) complete next tick." : $"{OfferName(template)} accepted: no jobs, points refunded.");
					break;
				}
				case Mystery: {
					int outcome = MysteryRoll(reward, data);
					if (outcome == 0) {
						int jackpot = 150 + System.Math.Abs(reward) % 251;
						if (ResearchTech.TechOn(data, ResearchTech.ExpStakes))
							jackpot *= 2;
						ResearchPoints.Add(data, jackpot);
						ResearchEngineUISystem.SetMessage($"Mystery Contract: JACKPOT +{jackpot} pts!");
					}
					else if (outcome == 1) {
						ResearchEngineUISystem.SetMessage("Mystery Contract: quiet... the sample escaped.");
					}
					else {
						data.Charge = System.Math.Max(0, data.Charge - 50);
						ResearchEngineSystem.SyncWorld();
						ResearchEngineUISystem.SetMessage("Mystery Contract: BACKFIRE -50 charge!");
					}
					break;
				}
			}
		}

		// Authoritative accept (payment happened client-side, except
		// charge/points costs which spend from the server copy).
		public static void ApplyAccept(Point tile, int slot)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (slot < 0 || slot >= OfferSlots || data.ExpOffers == null || slot >= data.ExpOffers.Count)
				return;
			if (!TryParseOffer(data.ExpOffers[slot], out string template, out int cost, out int mats, out int reward))
				return;
			// Server spends pool costs from its own copy (coins/mats were
			// paid client-side, like research buys).
			if (template == Trial) {
				if (data.Charge < cost)
					return;
				data.Charge -= cost;
			}
			else if (template == Rushorder) {
				if (!ResearchPoints.TrySpend(data, cost))
					return;
			}
			// Rewards apply through the shared path (messages are
			// client-side; the server copy just needs the state).
			ApplyRewardSilent(tile, data, template, cost, mats, reward);
			data.ExpOffers[slot] = "";
			ResearchEngineSystem.SyncWorld();
		}

		// Reward application without player messages (server side).
		private static void ApplyRewardSilent(Point tile, ResearchData data, string template, int cost, int mats, int reward)
		{
			switch (template) {
				case Funding:
				case Firesale:
					ResearchPoints.Add(data, reward);
					break;
				case Trial: {
					bool moved = false;
					if (data.Research != null) {
						foreach (var job in data.Research) {
							job.Left = System.Math.Max(0, job.Left - reward * 60);
							moved = true;
						}
					}
					if (!moved)
						ResearchPoints.Add(data, TrialFallbackPts);
					break;
				}
				case Rushorder: {
					int count = ResearchTech.TechOn(data, ResearchTech.ExpStakes) ? 2 : 1;
					int done = 0;
					for (int k = 0; k < count; k++) {
						ResearchJob shortest = null;
						if (data.Research != null) {
							foreach (var job in data.Research) {
								if (job.Left <= 0)
									continue;
								if (shortest == null || job.Left < shortest.Left)
									shortest = job;
							}
						}
						if (shortest == null)
							break;
						shortest.Left = 0;
						done++;
					}
					if (done == 0)
						ResearchPoints.Add(data, cost);
					ResearchEngineSystem.SyncWorld();
					break;
				}
				case Mystery: {
					int outcome = MysteryRoll(reward, data);
					if (outcome == 0) {
						int jackpot = 150 + System.Math.Abs(reward) % 251;
						if (ResearchTech.TechOn(data, ResearchTech.ExpStakes))
							jackpot *= 2;
						ResearchPoints.Add(data, jackpot);
					}
					else if (outcome == 2) {
						data.Charge = System.Math.Max(0, data.Charge - 50);
						ResearchEngineSystem.SyncWorld();
					}
					break;
				}
			}
		}

		public static void TryReroll(Player player, Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (!ResearchTech.TechOn(data, ResearchTech.ExpFresh)) {
				ResearchEngineUISystem.SetMessage("Research Fresh Leads first (Tech tab).");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			if (Main.netMode == NetmodeID.MultiplayerClient) {
				// Server rolls authoritatively (deterministic for all).
				if (data.Points < RerollCost) {
					ResearchEngineUISystem.SetMessage($"Needs {RerollCost} research points (have {data.Points}).");
					Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
					return;
				}
				PlaceableNet.SendResearchReroll(tile);
				ResearchEngineUISystem.SetMessage("Seeking fresh leads...");
				return;
			}
			if (!ResearchPoints.TrySpend(data, RerollCost)) {
				ResearchEngineUISystem.SetMessage($"Needs {RerollCost} research points (have {data.Points}).");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			RollOffers(tile, data);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup, player.Center);
			ResearchEngineUISystem.SetMessage("Fresh leads posted.");
		}

		// Authoritative re-roll (points spent from the server copy).
		public static void ApplyReroll(Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (!ResearchTech.TechOn(data, ResearchTech.ExpFresh))
				return;
			if (!ResearchPoints.TrySpend(data, RerollCost))
				return;
			RollOffers(tile, data);
		}
	}
}
