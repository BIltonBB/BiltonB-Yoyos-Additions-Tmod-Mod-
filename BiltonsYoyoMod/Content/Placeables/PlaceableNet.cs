using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Minimal MP packets for placeable blocks. Resource transactions stay
	// client-authoritative (like station purchases); world state (levels,
	// charge, config) is server-authoritative and rebroadcast via WorldData.
	// All state changes are bounds-checked and tile-validated to prevent
	// duplication and inconsistent states.
	public static class PlaceableNet
	{
		public enum Msg : byte
		{
			ResonatorBuy = 0,
			ResearchCharge = 1,
			ResearchConsume = 2,
			ResearchConfig = 3,
			RiftSync = 4,
			WardBuy = 5,
			ResearchVent = 6,
			ResonatorMode = 7,
			ResearchTech = 8,
			ResearchTick = 9,
			ResearchStyle = 10,
			ResearchToggle = 11,
			ResearchRespec = 12,
			ResearchCancel = 13,
			CowboyDowned = 14,
			ResearchRush = 15,
			ResearchCap = 16,
			ResearchLink = 17,
			ResearchUnlink = 18,
			ResearchField = 19,
			ResearchReroll = 20,
			ResearchAccept = 21,
			ResearchRestore = 22,
			ResearchEnergize = 23,
			ResearchMeshToggle = 24,
			ResearchAutoService = 25,
		}

		private static Mod Mod => ModContent.GetInstance<BiltonsYoyoMod>();

		public static void SendResonatorMode(Point tile, int focus)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResonatorMode);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(focus);
			p.Send();
		}

		public static void SendResonatorBuy(Point tile, int track, int level)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResonatorBuy);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(track);
			p.Write(level);
			p.Send();
		}

		public static void SendResearchCharge(Point tile, int amount)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchCharge);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(amount);
			p.Send();
		}

		public static void SendResearchConsume(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchConsume);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Send();
		}

		public static void SendResearchTick(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchTick);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Send();
		}

		public static void SendResearchStyle(Point tile, int style)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchStyle);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(style);
			p.Send();
		}

		public static void SendResearchToggle(Point tile, string id)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchToggle);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(id ?? "");
			p.Send();
		}

		public static void SendResearchRespec(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchRespec);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Send();
		}

		public static void SendResearchCancel(Point tile, string id)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchCancel);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(id ?? "");
			p.Send();
		}

		public static void SendResearchConfig(Point tile, int mode, int effect, bool auto, int rebound)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchConfig);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(mode);
			p.Write(effect);
			p.Write(auto ? 1 : 0);
			p.Write(rebound);
			p.Send();
		}

		public static void SendResearchVent(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchVent);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Send();
		}

		public static void SendResearchTech(Point tile, string id, int pointsCost)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchTech);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(id ?? "");
			p.Write(pointsCost);
			p.Send();
		}

		public static void SendResearchRush(Point tile, string id)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchRush);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(id ?? "");
			p.Send();
		}

		public static void SendResearchCap(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchCap);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Send();
		}

		public static void SendResearchLink(Point tile, Point target)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchLink);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(target.X);
			p.Write(target.Y);
			p.Send();
		}

		public static void SendResearchUnlink(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchUnlink);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Send();
		}

		public static void SendResearchField(Point tile, int amount)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchField);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(amount);
			p.Send();
		}

		public static void SendResearchReroll(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchReroll);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Send();
		}

		public static void SendResearchEnergize(Point tile, int playerWho)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchEnergize);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(playerWho);
			p.Send();
		}

		public static void SendResearchMeshToggle(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchMeshToggle);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Send();
		}

		public static void SendResearchAutoService(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchAutoService);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Send();
		}

		// Server-authoritative auto-service flip (free toggle, no node gate:
		// without an Aegis mesh it simply does nothing).
		public static void ApplyResearchAutoService(Point tile)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile))
				return;
			var data = ResearchEngineSystem.GetOrCreate(tile);
			data.AutoService = !data.AutoService;
			ResearchEngineSystem.SyncWorld();
		}

		public static void SendResearchAccept(Point tile, int slot)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchAccept);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(slot);
			p.Send();
		}

		public static void SendResearchRestore(Point tile, List<string> tech, List<string> off, int up, int points, int life, List<string> queue, List<string> jobIds, List<int> jobLefts, int charge, int mode, int effect, int style, int auto, int rebound)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.ResearchRestore);
			p.Write(tile.X);
			p.Write(tile.Y);
			WriteIdList(p, tech);
			WriteIdList(p, off);
			p.Write(up);
			p.Write(points);
			p.Write(life);
			WriteIdList(p, queue);
			var ids = jobIds ?? new List<string>();
			var lefts = jobLefts ?? new List<int>();
			int jobs = System.Math.Min(System.Math.Min(ids.Count, lefts.Count), 8);
			p.Write(jobs);
			for (int i = 0; i < jobs; i++) {
				p.Write(ids[i] ?? "");
				p.Write(lefts[i]);
			}
			p.Write(charge);
			p.Write(mode);
			p.Write(effect);
			p.Write(style);
			p.Write(auto);
			p.Write(rebound);
			p.Send();
		}

		private static void WriteIdList(ModPacket p, List<string> ids)
		{
			var list = ids ?? new List<string>();
			int n = System.Math.Min(list.Count, 64);
			p.Write(n);
			for (int i = 0; i < n; i++)
				p.Write(list[i] ?? "");
		}

		private static List<string> ReadIdList(BinaryReader reader)
		{
			int n = System.Math.Clamp(reader.ReadInt32(), 0, 64);
			var list = new List<string>(n);
			for (int i = 0; i < n; i++)
				list.Add(reader.ReadString() ?? "");
			return list;
		}

		public static void SendWardBuy(Point tile, int track, int level)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.WardBuy);
			p.Write(tile.X);
			p.Write(tile.Y);
			p.Write(track);
			p.Write(level);
			p.Send();
		}

		// Client -> server: the last cowboy died on this client.
		public static void SendCowboyDowned()
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)Msg.CowboyDowned);
			p.Send();
		}

		public static void HandlePacket(BinaryReader reader, int whoAmI)
		{
			try {
				HandlePacketInner(reader, whoAmI);
			}
			catch {
				// Malformed/truncated packets must never crash the game.
			}
		}

		private static void HandlePacketInner(BinaryReader reader, int whoAmI)
		{
			Msg msg = (Msg)reader.ReadByte();
			switch (msg) {
				case Msg.ResonatorBuy: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					int track = System.Math.Clamp(reader.ReadInt32(), 0, 3);
					int level = reader.ReadInt32(); // clamped inside ApplyLevel
					if (Main.netMode == NetmodeID.Server) {
						// Validate tile + bounds; resources already paid client-side.
						ResonatorActions.ApplyLevel(tile, track, level);
						// Rebroadcast via WorldData (SyncWorld inside ApplyLevel).
					}
					else {
						ResonatorActions.ApplyLevel(tile, track, level);
					}
					break;
				}
				case Msg.ResearchCharge: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					int amount = reader.ReadInt32();
					// Max gain: Rapid 3 + 2 wards x2 synergy + windings + conductors = 9.
					amount = System.Math.Clamp(amount, 0, 12);
					if (Main.netMode == NetmodeID.Server) {
						ResearchEngineSystem.AddCharge(tile, amount);
					}
					else {
						ResearchEngineSystem.AddCharge(tile, amount);
					}
					break;
				}
				case Msg.ResearchConsume: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					if (Main.netMode == NetmodeID.Server)
						ResearchEngineSystem.TryConsumeFull(tile);
					else
						ResearchEngineSystem.TryConsumeFull(tile);
					break;
				}
				case Msg.ResearchTick: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					if (Main.netMode == NetmodeID.Server)
						ResearchEngineSystem.TryConsumeTick(tile);
					else
						ResearchEngineSystem.TryConsumeTick(tile);
					break;
				}
				case Msg.ResearchStyle: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					int style = System.Math.Clamp(reader.ReadInt32(), 0, 2);
					ResearchEngineSystem.SetStyle(tile, style);
					break;
				}
				case Msg.ResearchToggle: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					string id = reader.ReadString();
					ResearchTech.ToggleOff(tile, id);
					break;
				}
				case Msg.ResearchRespec: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					ResearchTech.ClearAll(tile);
					break;
				}
				case Msg.ResearchConfig: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					int mode = System.Math.Clamp(reader.ReadInt32(), 0, 2);
					int effect = System.Math.Clamp(reader.ReadInt32(), 0, 3);
					bool auto = reader.ReadInt32() != 0;
					int rebound = System.Math.Clamp(reader.ReadInt32(), 0, 2);
					if (!ResearchEngineSystem.IsTileStillEngine(tile))
						break;
					var d = ResearchEngineSystem.GetOrCreate(tile);
					d.Mode = mode;
					d.Effect = effect;
					d.AutoRelease = auto;
					d.Rebound = rebound;
					ResearchEngineSystem.SyncWorld();
					break;
				}
				case Msg.ResearchVent: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					if (Main.netMode == NetmodeID.Server)
						ResearchEngineSystem.VentCharge(tile);
					else
						ResearchEngineSystem.VentCharge(tile);
					break;
				}
				case Msg.WardBuy: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					int track = System.Math.Clamp(reader.ReadInt32(), 0, 3);
					int level = reader.ReadInt32(); // clamped inside ApplyLevel
					if (Main.netMode == NetmodeID.Server) {
						// Validate tile + bounds; resources already paid client-side.
						WardActions.ApplyLevel(tile, track, level);
						// Rebroadcast via WorldData (SyncWorld inside ApplyLevel).
					}
					else {
						WardActions.ApplyLevel(tile, track, level);
					}
					break;
				}
				case Msg.RiftSync: {
					byte playerWho = reader.ReadByte();
					bool has = reader.ReadBoolean();
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					int cd = reader.ReadInt32();
					int burst = System.Math.Clamp(reader.ReadInt32(), 0, 1);
					int pace = System.Math.Clamp(reader.ReadInt32(), 0, 1);
					int hold = System.Math.Clamp(reader.ReadInt32(), 0, 2);
					if (playerWho < Main.maxPlayers && Main.player[playerWho].active) {
						var rp = Main.player[playerWho].GetModPlayer<RiftAnchorPlayer>();
						rp.HasActive = has;
						rp.ActiveTile = tile;
						rp.SnapCooldown = cd;
						rp.BurstMode = burst;
						rp.Pace = pace;
						rp.HoldMode = hold;
						if (Main.netMode == NetmodeID.Server)
							rp.SyncPlayer(-1, whoAmI, false);
					}
					break;
				}
				case Msg.ResonatorMode: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					int focus = System.Math.Clamp(reader.ReadInt32(), 0, ResonatorSystem.MaxFocus);
					if (Main.netMode == NetmodeID.Server)
						ResonatorActions.ApplyFocus(tile, focus);
					else
						ResonatorActions.ApplyFocus(tile, focus);
					break;
				}
				case Msg.ResearchTech: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					string id = reader.ReadString();
					int pointsCost = System.Math.Clamp(reader.ReadInt32(), 0, 1000);
					// Starts timed research, or queues when bays are full
					// (payment happened client-side; pool points spend here).
					ResearchTech.StartOrQueue(tile, id);
					if (pointsCost > 0 && ResearchEngineSystem.IsTileStillEngine(tile)) {
						var data = ResearchEngineSystem.GetOrCreate(tile);
						data.Points = System.Math.Max(0, data.Points - pointsCost);
						ResearchEngineSystem.SyncWorld();
					}
					break;
				}
				case Msg.ResearchCancel: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					string id = reader.ReadString();
					ResearchTech.DropJobOrQueued(tile, id);
					break;
				}
				case Msg.ResearchRush: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					string id = reader.ReadString();
					// Spends from the server copy (payment happened client-side).
					ResearchPoints.ApplyRush(tile, id);
					break;
				}
				case Msg.ResearchCap: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					// Storage level-up (payment happened client-side).
					ResearchPoints.ApplyCapUpgrade(tile);
					break;
				}
				case Msg.ResearchLink: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					Point target = new(reader.ReadInt32(), reader.ReadInt32());
					// Bilateral link + tech union (free op, no payment).
					ResearchNetwork.ApplyLink(tile, target);
					break;
				}
				case Msg.ResearchUnlink: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					ResearchNetwork.ApplyUnlink(tile);
					break;
				}
				case Msg.ResearchField: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					// Max legit: bulk salvage 99 x core 15 x 2.5 rate = 3712.
					int amount = System.Math.Clamp(reader.ReadInt32(), 0, 4000);
					// Owner-side field report (filed client-side).
					if (ResearchEngineSystem.IsTileStillEngine(tile))
						ResearchPoints.Add(ResearchEngineSystem.GetOrCreate(tile), amount);
					break;
				}
				case Msg.ResearchReroll: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					// Server rolls authoritatively (points spent server-side).
					ResearchTrials.ApplyReroll(tile);
					break;
				}
				case Msg.ResearchEnergize: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					int playerWho = reader.ReadInt32();
					// Points spent from the server copy (charge added there).
					ResearchPoints.ApplyEnergize(tile, playerWho);
					break;
				}
				case Msg.ResearchMeshToggle: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					// Free toggle (node validated inside).
					StationNetwork.ApplyMeshToggle(tile);
					break;
				}
				case Msg.ResearchAutoService: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					ApplyResearchAutoService(tile);
					break;
				}
				case Msg.ResearchAccept: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					int slot = System.Math.Clamp(reader.ReadInt32(), 0, ResearchTrials.OfferSlots - 1);
					// Payment happened client-side (pool costs spent server-side).
					ResearchTrials.ApplyAccept(tile, slot);
					break;
				}
				case Msg.ResearchRestore: {
					Point tile = new(reader.ReadInt32(), reader.ReadInt32());
					var tech = ReadIdList(reader);
					var off = ReadIdList(reader);
					int up = reader.ReadInt32();
					int points = reader.ReadInt32();
					int life = reader.ReadInt32();
					var queue = ReadIdList(reader);
					int jobs = System.Math.Clamp(reader.ReadInt32(), 0, 8);
					var jobIds = new List<string>(jobs);
					var jobLefts = new List<int>(jobs);
					for (int i = 0; i < jobs; i++) {
						jobIds.Add(reader.ReadString() ?? "");
						jobLefts.Add(reader.ReadInt32());
					}
					int charge = reader.ReadInt32();
					int mode = reader.ReadInt32();
					int effect = reader.ReadInt32();
					int style = reader.ReadInt32();
					int auto = reader.ReadInt32();
					int rebound = reader.ReadInt32();
					// Free op: the backup lived player-side (validated inside).
					TechSaving.ApplyRestore(tile, tech, off, up, points, life, queue, jobIds, jobLefts, charge, mode, effect, style, auto, rebound);
					break;
				}
				case Msg.CowboyDowned: {
					global::BiltonsYoyoMod.Content.YoyoGuardianWorld.downedCowboyOfYoyos = true;
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.WorldData);
					break;
				}
			}
		}
	}
}
