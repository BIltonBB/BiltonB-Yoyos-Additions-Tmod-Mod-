using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Traps
{
	// Trap multiplayer packets. Server is authoritative for activation,
	// pairing, wiring, upgrades, and config. Inputs are validated.
	public static class TrapNet
	{
		public enum Msg : byte
		{
			Config = 0,
			Upgrade = 1,
			Pair = 2,
			Unlink = 3,
			Wire = 4,
			Overcharge = 5,
			Experiment = 6,
			Track = 7,
			Breaker = 8,
			Surge = 9,
			Reinforce = 10,
			PairDenied = 11,
		}

		private static Mod Mod => ModContent.GetInstance<BiltonsYoyoMod>();

		public static void SendConfig(Point tile, TrapState s)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250); // trap channel discriminator
			p.Write((byte)Msg.Config);
			p.Write(tile.X); p.Write(tile.Y);
			WriteState(p, s);
			p.Send();
		}

		public static void SendUpgrade(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.Upgrade);
			p.Write(tile.X); p.Write(tile.Y);
			p.Send();
		}

		public static void SendLinkPair(Point a, Point b)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.Pair);
			p.Write(a.X); p.Write(a.Y);
			p.Write(b.X); p.Write(b.Y);
			p.Send();
		}

		public static void SendUnlink(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.Unlink);
			p.Write(tile.X); p.Write(tile.Y);
			p.Send();
		}

		public static void SendLinkWire(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.Wire);
			p.Write(tile.X); p.Write(tile.Y);
			p.Send();
		}

		public static void SendOvercharge(Point tile, bool on)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.Overcharge);
			p.Write(tile.X); p.Write(tile.Y);
			p.Write(on);
			p.Send();
		}

		public static void SendExperiment(Point tile, int effect)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.Experiment);
			p.Write(tile.X); p.Write(tile.Y);
			p.Write(effect);
			p.Send();
		}

		public static void SendTrack(Point tile, int track)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.Track);
			p.Write(tile.X); p.Write(tile.Y);
			p.Write(System.Math.Clamp(track, 0, 9));
			p.Send();
		}

		public static void SendBreaker(Point engine, bool arm)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.Breaker);
			p.Write(engine.X); p.Write(engine.Y);
			p.Write(arm);
			p.Send();
		}

		public static void SendSurge(Point engine)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.Surge);
			p.Write(engine.X); p.Write(engine.Y);
			p.Send();
		}

		public static void SendReinforce(Point tile)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.Reinforce);
			p.Write(tile.X); p.Write(tile.Y);
			p.Send();
		}

		public static void SendPairDenied(int toClient, string error)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)250);
			p.Write((byte)Msg.PairDenied);
			p.Write(error ?? "");
			p.Send(toClient);
		}

		private static void WriteState(ModPacket p, TrapState s)
		{
			p.Write(s.Range); p.Write(s.Damage); p.Write(s.Duration); p.Write(s.CooldownMax);
			p.Write(s.StringVariant); p.Write(s.BearingTier); p.Write(s.CounterType);
			p.Write(s.Priority); p.Write(s.Behavior); p.Write(s.Restraint); p.Write(s.Bend);
			p.Write(s.MaxLen); p.Write(s.OutSpeed); p.Write(s.ReturnSpeed);
			p.Write(s.HitPlayers); p.Write(s.HitNPCs); p.Write(s.HitTown);
			p.Write(s.HitHostile); p.Write(s.HitFriendly); p.Write(s.HitAll);
			p.Write(s.Overcharge);
			p.Write(s.SentryAegis); p.Write(s.WardLattice); p.Write(s.SnareTip); p.Write(s.SentryBunker); p.Write(s.LongSpan); p.Write(s.HairTrigger); p.Write(s.BarbedWire); p.Write(s.TracerFire); p.Write(s.TwinThrow);
			p.Write(s.AutoRewire); p.Write(s.SentryAnchor); p.Write(System.Math.Clamp(s.TripStyle, 0, 7)); p.Write(System.Math.Clamp(s.TripString, 0, 24)); p.Write(s.Enabled); p.Write(s.Name ?? ""); p.Write(s.AutoOC); p.Write(System.Math.Clamp(s.ShareMode, 0, 2)); p.Write(s.AlarmIdleOnly); p.Write(System.Math.Clamp(s.SentryString, 0, 7)); p.Write(System.Math.Clamp(s.LinkTreat, 0, 2)); p.Write(System.Math.Clamp(s.Sched, 0, 3)); p.Write(s.PulsePeriod == 10 ? 10 : s.PulsePeriod == 60 ? 60 : 30);
		}

		private static TrapState ReadState(BinaryReader r)
		{
			var st = new TrapState {
				Range = r.ReadInt32(), Damage = r.ReadInt32(), Duration = r.ReadInt32(), CooldownMax = r.ReadInt32(),
				StringVariant = r.ReadInt32(), BearingTier = r.ReadInt32(), CounterType = r.ReadInt32(),
				Priority = r.ReadInt32(), Behavior = r.ReadInt32(), Restraint = r.ReadInt32(), Bend = r.ReadInt32(),
				MaxLen = r.ReadInt32(), OutSpeed = r.ReadInt32(), ReturnSpeed = r.ReadInt32(),
				HitPlayers = r.ReadBoolean(), HitNPCs = r.ReadBoolean(), HitTown = r.ReadBoolean(),
				HitHostile = r.ReadBoolean(), HitFriendly = r.ReadBoolean(), HitAll = r.ReadBoolean(),
				Overcharge = r.ReadBoolean(),
				SentryAegis = r.ReadBoolean(), WardLattice = r.ReadBoolean(), SnareTip = r.ReadBoolean(), SentryBunker = r.ReadBoolean(), LongSpan = r.ReadBoolean(), HairTrigger = r.ReadBoolean(), BarbedWire = r.ReadBoolean(), TracerFire = r.ReadBoolean(), TwinThrow = r.ReadBoolean(),
			};
			try {
				st.AutoRewire = r.ReadBoolean();
				st.SentryAnchor = System.Math.Clamp(r.ReadInt32(), 0, 2);
			}
			catch { }
			try { st.TripStyle = System.Math.Clamp(r.ReadInt32(), 0, 7); } catch { }
			try { st.TripString = System.Math.Clamp(r.ReadInt32(), 0, 24); } catch { }
			try { st.Enabled = r.ReadBoolean(); } catch { }
			try {
				st.Name = r.ReadString() ?? "";
				if (st.Name.Length > 24)
					st.Name = st.Name.Substring(0, 24);
			}
			catch { }
			try { st.AutoOC = r.ReadBoolean(); } catch { }
			try { st.ShareMode = System.Math.Clamp(r.ReadInt32(), 0, 2); } catch { }
			try { st.AlarmIdleOnly = r.ReadBoolean(); } catch { }
			try { st.SentryString = System.Math.Clamp(r.ReadInt32(), 0, 7); } catch { }
			try { st.LinkTreat = System.Math.Clamp(r.ReadInt32(), 0, 2); } catch { }
			try { st.Sched = System.Math.Clamp(r.ReadInt32(), 0, 3); } catch { }
			try {
				int pp = r.ReadInt32();
				st.PulsePeriod = pp == 10 ? 10 : pp == 60 ? 60 : 30;
			}
			catch { }
			return st;
		}

		public static void Handle(BinaryReader reader, int whoAmI)
		{
			try { HandleInner(reader, whoAmI); }
			catch { }
		}

		private static void HandleInner(BinaryReader reader, int whoAmI)
		{
			Msg msg = (Msg)reader.ReadByte();
			switch (msg) {
				case Msg.Config: {
					Point tile = new Point(reader.ReadInt32(), reader.ReadInt32());
					TrapState fresh = ReadState(reader);
					// Validate tile exists and is a trap.
					if (!TrapSystem.Traps.ContainsKey(tile))
						break;
					TrapActions.ApplyConfig(tile, fresh);
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.WorldData);
					break;
				}
				case Msg.Upgrade: {
					Point tile = new Point(reader.ReadInt32(), reader.ReadInt32());
					if (Main.netMode == NetmodeID.Server) {
						// Resources were paid client-side; validate level/uses/mesh gates.
						if (TrapSystem.Traps.TryGetValue(tile, out var s)) {
							bool gated = TrapActions.UpgradeRequirementMet(tile, s, out _);
							// Offensive upgrades only ever originate from a readiness flag.
							if (gated && (s.Kind == TrapKind.Sentry || s.Kind == TrapKind.Link || s.Kind == TrapKind.Tripwire || s.UpgradePending))
								TrapActions.ApplyUpgrade(tile);
						}
					}
					else {
						TrapActions.ApplyUpgrade(tile);
					}
					break;
				}
				case Msg.Pair: {
					Point a = new Point(reader.ReadInt32(), reader.ReadInt32());
					Point b = new Point(reader.ReadInt32(), reader.ReadInt32());
					string err;
					// Server validates range/duplicates.
					if (!TrapActions.TryPair(a, b, out err)) {
						// Never fail silently: the client already cleared its
						// pending endpoint, so tell the requester why.
						if (Main.netMode == NetmodeID.Server)
							SendPairDenied(whoAmI, err);
					}
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.WorldData);
					break;
				}
				case Msg.PairDenied: {
					string err = "";
					try {
						err = reader.ReadString();
					}
					catch { }
					if (!string.IsNullOrEmpty(err))
						Main.NewText("Linker: " + err, 255, 150, 150);
					break;
				}
				case Msg.Unlink: {
					Point tile = new Point(reader.ReadInt32(), reader.ReadInt32());
					TrapActions.ApplyUnlink(tile);
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.WorldData);
					break;
				}
				case Msg.Wire: {
					Point tile = new Point(reader.ReadInt32(), reader.ReadInt32());
					TrapActions.ApplyLinkWire(tile);
					break;
				}
				case Msg.Overcharge: {
					Point tile = new Point(reader.ReadInt32(), reader.ReadInt32());
					bool on = reader.ReadBoolean();
					if (TrapSystem.Traps.TryGetValue(tile, out var s)) {
						s.Overcharge = on;
						TrapSystem.SyncWorld();
					}
					break;
				}
				case Msg.Experiment: {
					Point tile = new Point(reader.ReadInt32(), reader.ReadInt32());
					int effect = System.Math.Clamp(reader.ReadInt32(), 0, 17);
					// Payment happened client-side; apply the validated effect here.
					TrapActions.ApplyExperiment(tile, effect);
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.WorldData);
					break;
				}
				case Msg.Track: {
					Point tile = new Point(reader.ReadInt32(), reader.ReadInt32());
					int track = System.Math.Clamp(reader.ReadInt32(), 0, 9);
					if (Main.netMode == NetmodeID.Server) {
						// Paid client-side; server validates gates then applies.
						if (TrapSystem.Traps.TryGetValue(tile, out _))
							TrapActions.ApplyTrack(tile, track);
					}
					else {
						TrapActions.ApplyTrack(tile, track);
					}
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.WorldData);
					break;
				}
				case Msg.Breaker: {
					Point engine = new Point(reader.ReadInt32(), reader.ReadInt32());
					bool arm = reader.ReadBoolean();
					TrapActions.ApplyBreaker(engine, arm);
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.WorldData);
					break;
				}
				case Msg.Surge: {
					Point engine = new Point(reader.ReadInt32(), reader.ReadInt32());
					TrapActions.ApplySurge(engine);
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.WorldData);
					break;
				}
				case Msg.Reinforce: {
					Point tile = new Point(reader.ReadInt32(), reader.ReadInt32());
					if (Main.netMode == NetmodeID.Server) {
						// Paid client-side; server validates kind/cap then applies.
						if (TrapSystem.Traps.TryGetValue(tile, out _))
							TrapActions.ApplyReinforce(tile);
					}
					else {
						TrapActions.ApplyReinforce(tile);
					}
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.WorldData);
					break;
				}
			}
		}
	}
}
