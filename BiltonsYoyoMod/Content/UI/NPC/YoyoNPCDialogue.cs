using System.Collections.Generic;
using Terraria;

using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;
using BiltonsYoyoMod.Content.NPCs.Town.Pets;

namespace BiltonsYoyoMod.Content.UI.YoyoNPC
{
	// Central read access to every revamp NPC's dialogue lines.
	public static class YoyoNPCDialogue
	{
		public static string Pick(string[] list) => list[Main.rand.Next(list.Length)];

		public static bool AnyActive<T>() where T : ModNPC
		{
			int type = ModContent.NPCType<T>();
			for (int i = 0; i < Main.maxNPCs; i++) {
				if (Main.npc[i].active && Main.npc[i].type == type)
					return true;
			}
			return false;
		}

		public static string RandomGreeting(int npcType)
		{
			if (npcType == ModContent.NPCType<YoyoMaster>()) {
				if (Main.rand.NextBool(6)) {
					if (AnyActive<YoyoTinkerer>()) return Pick(YoyoMaster.PairWithTinkererMaster);
					if (AnyActive<AncientYoyoist>()) return Pick(YoyoMaster.PairWithAncientMaster);
					if (AnyActive<SixStringer>()) return Pick(YoyoMaster.PairWithSixStringerMaster);
				}
				return Pick(YoyoMaster.Greetings);
			}
			if (npcType == ModContent.NPCType<YoyoTinkerer>()) {
				if (Main.rand.NextBool(6) && AnyActive<YoyoMaster>()) return Pick(YoyoTinkerer.PairWithMasterTinkerer);
				if (Main.rand.NextBool(6) && AnyActive<AncientYoyoist>()) return Pick(YoyoTinkerer.PairWithTinkererAncient);
				return Pick(YoyoTinkerer.Greetings);
			}
			if (npcType == ModContent.NPCType<AncientYoyoist>()) {
				if (Main.rand.NextBool(6) && AnyActive<YoyoMaster>()) return Pick(AncientYoyoist.PairWithMasterAncient);
				if (Main.rand.NextBool(6) && AnyActive<YoyoTinkerer>()) return Pick(AncientYoyoist.PairWithTinkererAncient);
				if (Main.rand.NextBool(8) && AnyActive<YoyoCultist>()) return Pick(AncientYoyoist.PairWithCultistAncient);
				return Pick(AncientYoyoist.Greetings);
			}
			if (npcType == ModContent.NPCType<SixStringer>()) {
				if (Main.rand.NextBool(4) && AnyActive<YoyoMaster>()) return Pick(SixStringer.PairWithMasterSixStringer);
				return Pick(SixStringer.Greetings);
			}
			if (npcType == ModContent.NPCType<YoyoCultist>()) {
				if (Main.rand.NextBool(5) && AnyActive<AncientYoyoist>()) return Pick(YoyoCultist.PairWithAncientCultist);
				return Pick(YoyoCultist.Greetings);
			}
			if (npcType == ModContent.NPCType<BillyTheBearing>()) return "*Bearing noises*";
			if (npcType == ModContent.NPCType<MagicalYoyoPet>()) return Pick(MagicalYoyoPet.Lines);
			return "...";
		}

		public static string RandomTalk(int npcType)
		{
			if (npcType == ModContent.NPCType<YoyoMaster>()) {
				if (Main.rand.NextBool(6)) {
					if (AnyActive<YoyoTinkerer>()) return Pick(YoyoMaster.PairWithTinkererMaster);
					if (AnyActive<AncientYoyoist>()) return Pick(YoyoMaster.PairWithAncientMaster);
					if (AnyActive<SixStringer>()) return Pick(YoyoMaster.PairWithSixStringerMaster);
				}
				return Pick(YoyoMaster.Talk);
			}
			if (npcType == ModContent.NPCType<YoyoTinkerer>()) {
				if (Main.rand.NextBool(6) && AnyActive<YoyoMaster>()) return Pick(YoyoTinkerer.PairWithMasterTinkerer);
				if (Main.rand.NextBool(6) && AnyActive<AncientYoyoist>()) return Pick(YoyoTinkerer.PairWithTinkererAncient);
				return Pick(YoyoTinkerer.Talk);
			}
			if (npcType == ModContent.NPCType<AncientYoyoist>()) {
				if (Main.rand.NextBool(6) && AnyActive<YoyoMaster>()) return Pick(AncientYoyoist.PairWithMasterAncient);
				if (Main.rand.NextBool(6) && AnyActive<YoyoTinkerer>()) return Pick(AncientYoyoist.PairWithTinkererAncient);
				if (Main.rand.NextBool(8) && AnyActive<YoyoCultist>()) return Pick(AncientYoyoist.PairWithCultistAncient);
				return Pick(AncientYoyoist.Talk);
			}
			if (npcType == ModContent.NPCType<SixStringer>()) {
				if (Main.rand.NextBool(4) && AnyActive<YoyoMaster>()) return Pick(SixStringer.PairWithMasterSixStringer);
				return Pick(SixStringer.Talk);
			}
			if (npcType == ModContent.NPCType<YoyoCultist>()) {
				if (Main.rand.NextBool(5) && AnyActive<AncientYoyoist>()) return Pick(YoyoCultist.PairWithAncientCultist);
				return Pick(YoyoCultist.Talk);
			}
			if (npcType == ModContent.NPCType<BillyTheBearing>()) return Main.rand.NextBool() ? "*Bearing noises*" : "*Bearing noises #2*";
			if (npcType == ModContent.NPCType<MagicalYoyoPet>()) return Pick(MagicalYoyoPet.Lines);
			return "...";
		}

		public static List<string> HelpLines(int npcType)
		{
			if (npcType == ModContent.NPCType<YoyoTinkerer>()) return new List<string>(YoyoTinkerer.Help);
			if (npcType == ModContent.NPCType<AncientYoyoist>()) return new List<string>(AncientYoyoist.Help);
			if (npcType == ModContent.NPCType<SixStringer>()) return new List<string>(SixStringer.Help);
			if (npcType == ModContent.NPCType<YoyoCultist>()) return new List<string>(YoyoCultist.Help);
			if (npcType == ModContent.NPCType<BillyTheBearing>()) return new List<string> {
				"Billy orbits you during boss fights and body-slams enemies.",
				"Feed him one of each loose bearing and tension bearing type.",
				"Each install raises follow speed, orbit speed, damage and knockback.",
				"Half the upgrades unlocks bearing shots; full unlocks twin shots.",
			};
			if (npcType == ModContent.NPCType<MagicalYoyoPet>()) return new List<string> {
				"Pet the yoyo to gain Magical Essence: +5% yoyo damage for 8 hours.",
				"Pet it again any time to refresh the blessing.",
			};
			return new List<string>();
		}

		public static string NpcDisplayName(int npcType)
		{
			if (npcType == ModContent.NPCType<YoyoMaster>()) return "Yoyo Master";
			if (npcType == ModContent.NPCType<YoyoTinkerer>()) return "Yoyo Tinkerer";
			if (npcType == ModContent.NPCType<AncientYoyoist>()) return "Ancient Yo-yoist";
			if (npcType == ModContent.NPCType<SixStringer>()) return "Six Stringer";
			if (npcType == ModContent.NPCType<YoyoCultist>()) return "Yoyo Cultist";
			if (npcType == ModContent.NPCType<BillyTheBearing>()) return "Billy the Bearing";
			if (npcType == ModContent.NPCType<MagicalYoyoPet>()) return "Magical Yoyo";
			return "???";
		}

		public static string NpcSubtitle(int npcType)
		{
			if (npcType == ModContent.NPCType<YoyoMaster>()) return "Teaches control. Sells strings, cases, tricks and emblems.";
			if (npcType == ModContent.NPCType<YoyoTinkerer>()) return "Installs mechanical modifications onto held yoyos.";
			if (npcType == ModContent.NPCType<AncientYoyoist>()) return "Casts spells onto held yoyos. Higher tiers, rarer magic.";
			if (npcType == ModContent.NPCType<SixStringer>()) return "Night black market. Gone by dawn. No receipts.";
			if (npcType == ModContent.NPCType<YoyoCultist>()) return "Wandering relic dealer. Rare visitor, rarer wares.";
			if (npcType == ModContent.NPCType<BillyTheBearing>()) return "Boss-fight bodyguard. Feed him bearings.";
			if (npcType == ModContent.NPCType<MagicalYoyoPet>()) return "Pet for +5% yoyo damage.";
			return "";
		}
	}
}
