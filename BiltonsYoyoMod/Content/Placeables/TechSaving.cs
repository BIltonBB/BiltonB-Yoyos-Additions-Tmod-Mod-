using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Tech saving (#15): breaking an engine normally wipes everything
	// (charge, config, tech, points, jobs, queue). With Tech Saving
	// researched, the break instead files a one-shot backup on the
	// nearest player: Full Backup adds points, storage, queue and jobs;
	// Legacy Vault adds charge and full config. Restoring needs a fresh
	// engine and consumes the backup. Without the branch, destroying the
	// block gets rid of everything (only the block item itself drops).
	public static class TechSaving
	{
		// Fresh enough to receive a backup: no progress of any kind.
		public static bool IsFresh(ResearchData d)
		{
			if (d == null)
				return false;
			return d.TechCount == 0
				&& (d.Research == null || d.Research.Count == 0)
				&& (d.Queue == null || d.Queue.Count == 0)
				&& d.Points == 0 && d.Charge == 0;
		}

		// Destroy hook: call BEFORE the tile entry is forgotten (and
		// before Failover moves pool fields to a linked survivor — pool
		// fields are skipped here when a survivor will take them).
		public static void SaveOnBreak(Point origin)
		{
			if (!ResearchEngineSystem.Tiles.TryGetValue(origin, out var d))
				return;
			if (!ResearchTech.TechOn(d, ResearchTech.SaveTech) || d.TechCount == 0)
				return; // nothing worth saving: the wipe proceeds
			Player player = FieldResearch.NearestPlayer(new Vector2(origin.X * 16f + 24f, origin.Y * 16f + 16f));
			if (player == null)
				return;
			var bp = player.GetModPlayer<ResearchEnginePlayer>();
			bp.ClearBackup();
			bp.HasBackup = true;
			foreach (string id in d.Tech ?? new List<string>()) {
				if (ResearchTech.IsNode(id) && !bp.BpTech.Contains(id))
					bp.BpTech.Add(id);
			}
			foreach (string id in d.OffTech ?? new List<string>()) {
				if (ResearchTech.IsNode(id) && bp.BpTech.Contains(id) && !bp.BpOff.Contains(id))
					bp.BpOff.Add(id);
			}
			bool full = ResearchTech.TechOn(d, ResearchTech.SaveFull);
			bool vault = ResearchTech.TechOn(d, ResearchTech.SaveVault);
			// A live linked survivor inherits the pool via Failover, so
			// the backup keeps only the tech in that case (no dupes).
			bool survivor = ResearchNetwork.PartnerLive(origin, d);
			if (full && !survivor) {
				bp.BpUp = System.Math.Clamp(d.PointsUp, 0, ResearchPoints.MaxUpgrades);
				bp.BpPoints = System.Math.Max(0, d.Points);
				bp.BpLife = System.Math.Max(0, d.LifetimePoints);
				foreach (string id in d.Queue ?? new List<string>()) {
					if (bp.BpQueue.Count >= 6)
						break;
					if (ResearchTech.ById(id) != null && !bp.BpTech.Contains(id) && !bp.BpQueue.Contains(id))
						bp.BpQueue.Add(id);
				}
				if (d.Research != null) {
					foreach (var job in d.Research) {
						if (bp.BpJobIds.Count >= 3)
							break;
						var rn = ResearchTech.ById(job.Id);
						if (rn == null || bp.BpTech.Contains(job.Id) || bp.BpJobIds.Contains(job.Id))
							continue;
						bp.BpJobIds.Add(job.Id);
						bp.BpJobLefts.Add(System.Math.Clamp(job.Left, 1, ResearchTech.ResearchSecs(rn) * 60));
					}
				}
			}
			if (vault && !survivor) {
				bp.BpCharge = System.Math.Clamp(d.Charge, 0, ResearchEngineSystem.MaxCharge + 50);
				bp.BpMode = System.Math.Clamp(d.Mode, 0, 2);
				bp.BpEffect = System.Math.Clamp(d.Effect, 0, 3);
				bp.BpStyle = System.Math.Clamp(d.Style, 0, 2);
				bp.BpAuto = d.AutoRelease ? 1 : 0;
				bp.BpRebound = System.Math.Clamp(d.Rebound, 0, ResearchEngineSystem.MaxRebound);
			}
			if (player.whoAmI == Main.myPlayer && !Main.dedServ)
				CombatText.NewText(player.getRect(), Color.Gold, "Backup saved.");
		}

		public static void Restore(Player player, Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			var bp = player.GetModPlayer<ResearchEnginePlayer>();
			if (!bp.HasBackup || bp.BpTech.Count == 0) {
				ResearchEngineUISystem.SetMessage("No backup to restore.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			if (!IsFresh(data)) {
				ResearchEngineUISystem.SetMessage("Engine must be fresh (no tech, jobs, queue, points, charge).");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			ApplyBackup(data, bp);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchRestore(tile, bp.BpTech, bp.BpOff, bp.BpUp, bp.BpPoints, bp.BpLife, bp.BpQueue, bp.BpJobIds, bp.BpJobLefts, bp.BpCharge, bp.BpMode, bp.BpEffect, bp.BpStyle, bp.BpAuto, bp.BpRebound);
			bp.ClearBackup();
			ResearchEngineSystem.SyncWorld();
			Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup, player.Center);
			ResearchEngineUISystem.SetMessage($"Backup restored ({data.TechCount} techs).");
		}

		// Authoritative restore (free op: the backup lived player-side).
		public static void ApplyRestore(Point tile, List<string> tech, List<string> off, int up, int points, int life, List<string> queue, List<string> jobIds, List<int> jobLefts, int charge, int mode, int effect, int style, int auto, int rebound)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (!IsFresh(data))
				return;
			data.Tech ??= new List<string>();
			foreach (string id in tech ?? new List<string>()) {
				if (data.Tech.Count >= 64)
					break;
				if (ResearchTech.IsNode(id) && !data.Tech.Contains(id))
					data.Tech.Add(id);
			}
			if (data.Tech.Count == 0)
				return;
			data.OffTech ??= new List<string>();
			foreach (string id in off ?? new List<string>()) {
				if (ResearchTech.IsNode(id) && data.Tech.Contains(id) && !data.OffTech.Contains(id))
					data.OffTech.Add(id);
			}
			data.PointsUp = System.Math.Clamp(up, 0, ResearchPoints.MaxUpgrades);
			data.Points = System.Math.Clamp(points, 0, ResearchPoints.CapOf(data));
			data.LifetimePoints = System.Math.Max(0, life);
			data.Queue ??= new List<string>();
			foreach (string id in queue ?? new List<string>()) {
				if (data.Queue.Count >= 6)
					break;
				if (ResearchTech.ById(id) != null && !data.Tech.Contains(id) && !data.Queue.Contains(id))
					data.Queue.Add(id);
			}
			data.Research ??= new List<ResearchJob>();
			for (int i = 0; jobIds != null && jobLefts != null && i < jobIds.Count && i < jobLefts.Count && data.Research.Count < 3; i++) {
				var rn = ResearchTech.ById(jobIds[i]);
				if (rn == null || data.Tech.Contains(jobIds[i]) || ResearchTech.FindJob(data, jobIds[i]) != null)
					continue;
				data.Research.Add(new ResearchJob { Id = jobIds[i], Left = System.Math.Clamp(jobLefts[i], 1, ResearchTech.ResearchSecs(rn) * 60), Acc = 0f });
			}
			data.Charge = System.Math.Clamp(charge, 0, ResearchEngineSystem.MaxChargeOf(data));
			data.Mode = System.Math.Clamp(mode, 0, 2);
			data.Effect = System.Math.Clamp(effect, 0, 3);
			data.Style = System.Math.Clamp(style, 0, 2);
			data.AutoRelease = auto != 0;
			data.Rebound = System.Math.Clamp(rebound, 0, ResearchEngineSystem.MaxRebound);
			ResearchEngineSystem.SyncWorld();
		}

		private static void ApplyBackup(ResearchData data, ResearchEnginePlayer bp)
		{
			data.Tech ??= new List<string>();
			foreach (string id in bp.BpTech) {
				if (ResearchTech.IsNode(id) && !data.Tech.Contains(id))
					data.Tech.Add(id);
			}
			data.OffTech ??= new List<string>();
			foreach (string id in bp.BpOff) {
				if (ResearchTech.IsNode(id) && data.Tech.Contains(id) && !data.OffTech.Contains(id))
					data.OffTech.Add(id);
			}
			data.PointsUp = System.Math.Clamp(bp.BpUp, 0, ResearchPoints.MaxUpgrades);
			data.Points = System.Math.Clamp(bp.BpPoints, 0, ResearchPoints.CapOf(data));
			data.LifetimePoints = System.Math.Max(0, bp.BpLife);
			data.Queue ??= new List<string>();
			foreach (string id in bp.BpQueue) {
				if (data.Queue.Count >= 6)
					break;
				if (ResearchTech.ById(id) != null && !data.Tech.Contains(id) && !data.Queue.Contains(id))
					data.Queue.Add(id);
			}
			data.Research ??= new List<ResearchJob>();
			for (int i = 0; i < bp.BpJobIds.Count && i < bp.BpJobLefts.Count; i++) {
				var rn = ResearchTech.ById(bp.BpJobIds[i]);
				if (rn == null || data.Tech.Contains(bp.BpJobIds[i]) || ResearchTech.FindJob(data, bp.BpJobIds[i]) != null)
					continue;
				data.Research.Add(new ResearchJob { Id = bp.BpJobIds[i], Left = System.Math.Clamp(bp.BpJobLefts[i], 1, ResearchTech.ResearchSecs(rn) * 60), Acc = 0f });
			}
			data.Charge = System.Math.Clamp(bp.BpCharge, 0, ResearchEngineSystem.MaxChargeOf(data));
			data.Mode = System.Math.Clamp(bp.BpMode, 0, 2);
			data.Effect = System.Math.Clamp(bp.BpEffect, 0, 3);
			data.Style = System.Math.Clamp(bp.BpStyle, 0, 2);
			data.AutoRelease = bp.BpAuto != 0;
			data.Rebound = System.Math.Clamp(bp.BpRebound, 0, ResearchEngineSystem.MaxRebound);
		}
	}
}
