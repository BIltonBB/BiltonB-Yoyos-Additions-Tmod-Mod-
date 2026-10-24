using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Tech salvaging (#10): turn compatible resources into research
	// points with an up/down/double stepper. Rates improve with branch
	// techs; Wide Nets accepts rarer mats; Bulk raises the per-op units.
	// Client consumes from inventory (like purchases) and applies
	// locally; the server owns the pool via the shared field packet.
	public static class TechSalvage
	{
		public const int BaseMaxUnits = 25;
		public const int BulkMaxUnits = 99;

		private static int Scraps() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>();
		private static int Parts() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>();
		private static int Adv() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>();
		private static int Ancient() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AncientScraps>();
		private static int Illegal() => ModContent.ItemType<global::BiltonsYoyoMod.Items.BlackMarket.IllegalYoyoParts>();
		private static int Core() => ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.GuardianCoreM>();

		// Accepted (type, points per unit). Rarer mats need Wide Nets.
		public static List<(int type, int value)> Accepted(ResearchData d)
		{
			var list = new List<(int, int)> {
				(Scraps(), 1),
				(Parts(), 3),
				(Adv(), 8),
			};
			if (ResearchTech.TechOn(d, ResearchTech.SalvWide)) {
				list.Add((Ancient(), 6));
				list.Add((Illegal(), 4));
				list.Add((Core(), 15));
			}
			return list;
		}

		public static float RateMult(ResearchData d)
		{
			float m = 1f;
			if (ResearchTech.TechOn(d, ResearchTech.SalvEff))
				m += 0.5f;
			if (ResearchTech.TechOn(d, ResearchTech.SalvRec))
				m += 1f;
			return m;
		}

		public static int MaxUnits(ResearchData d)
		{
			return ResearchTech.TechOn(d, ResearchTech.SalvBulk) ? BulkMaxUnits : BaseMaxUnits;
		}

		public static string MatName(int type)
		{
			Item tmp = new Item();
			tmp.SetDefaults(type);
			return tmp.Name;
		}

		public static int Preview(ResearchData d, int type, int units)
		{
			int value = 0;
			foreach (var a in Accepted(d)) {
				if (a.type == type)
					value = a.value;
			}
			if (value <= 0 || units <= 0)
				return 0;
			return (int)(units * value * RateMult(d));
		}

		public static void Salvage(Player player, Point tile, int type, int units)
		{
			if (!ResearchEngineSystem.IsTileStillEngine(tile)) {
				ResearchEngineUISystem.SetMessage("Block is gone.");
				return;
			}
			var data = ResearchEngineSystem.GetOrCreate(tile);
			if (!ResearchTech.TechOn(data, ResearchTech.Salvage)) {
				ResearchEngineUISystem.SetMessage("Research Salvage first (Tech tab).");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			bool accepted = false;
			foreach (var a in Accepted(data)) {
				if (a.type == type)
					accepted = true;
			}
			if (!accepted) {
				ResearchEngineUISystem.SetMessage("That resource is not accepted.");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			units = System.Math.Clamp(units, 1, MaxUnits(data));
			int have = Content.NPCs.Town.YoyoTownShared.CountInInventory(player, type);
			if (have < units) {
				ResearchEngineUISystem.SetMessage($"Needs {units}x {MatName(type)} (have {have}).");
				Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
				return;
			}
			Content.NPCs.Town.YoyoTownShared.TryConsumeFromInventory(player, type, units);
			int pts = Preview(data, type, units);
			ResearchPoints.Add(data, pts);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				PlaceableNet.SendResearchField(tile, pts);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.CoinPickup, player.Center);
			ResearchEngineUISystem.SetMessage($"Salvaged {units}x {MatName(type)} -> +{pts} pts.");
		}
	}
}
