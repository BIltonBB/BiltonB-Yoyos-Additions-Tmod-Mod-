using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using BiltonsYoyoMod.Content.Items;
using BiltonsYoyoMod.Content.NPCs.Bosses;
using BiltonsYoyoMod.Content.NPCs.Mobs;
using BiltonsYoyoMod.Content.NPCs.SubBosses;
using BiltonsYoyoMod.Content.Systems;

namespace BiltonsYoyoMod.Content
{
    public class YoyoGuardianWorld : ModSystem
    {
        public static bool downedYoyoGuardian;
        public static bool downedGiantBearing;
        public static bool downedCowboyOfYoyos;
        public static bool downedAncientDuelist;

        public override void OnWorldLoad()
        {
            downedYoyoGuardian = false;
            downedGiantBearing = false;
            downedCowboyOfYoyos = false;
            downedAncientDuelist = false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["downedYoyoGuardian"] = downedYoyoGuardian;
            tag["downedGiantBearing"] = downedGiantBearing;
            tag["downedCowboyOfYoyos"] = downedCowboyOfYoyos;
            tag["downedAncientDuelist"] = downedAncientDuelist;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            downedYoyoGuardian = tag.GetBool("downedYoyoGuardian");
            downedGiantBearing = tag.GetBool("downedGiantBearing");
            downedCowboyOfYoyos = tag.GetBool("downedCowboyOfYoyos");
            downedAncientDuelist = tag.GetBool("downedAncientDuelist");
        }

        // WorldData sync was missing entirely: without this, flags set on
        // the server never reach clients (checklist/UI stay unchecked in MP).
        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(downedYoyoGuardian);
            writer.Write(downedGiantBearing);
            writer.Write(downedCowboyOfYoyos);
            writer.Write(downedAncientDuelist);
        }

        public override void NetReceive(BinaryReader reader)
        {
            downedYoyoGuardian = reader.ReadBoolean();
            downedGiantBearing = reader.ReadBoolean();
            downedCowboyOfYoyos = reader.ReadBoolean();
            downedAncientDuelist = reader.ReadBoolean();
        }
    }

    public class BossChecklistSupport : ModSystem
    {
        public override void PostSetupContent()
        {
            if (!ModLoader.TryGetMod("BossChecklist", out Mod bossChecklist))
                return;

            // Yoyo Guardian
            bossChecklist.Call(
                "LogBoss",
                Mod,
                "YoyoGuardian",
                13.5f,
                (Func<bool>)(() => YoyoGuardianWorld.downedYoyoGuardian),
                ModContent.NPCType<YoyoGuardian>(),
                new Dictionary<string, object>
                {
                    ["spawnItems"] = new List<int>
                    {
                        ModContent.ItemType<TrialOfTheGuardian>()
                    },

                    ["spawnInfo"] = ModContent.GetInstance<YoyoGuardian>()
                        .GetLocalization("BossChecklistIntegration.SpawnInfo")
                }
            );

            // Giant Bearing
            bossChecklist.Call(
                "LogMiniBoss",
                Mod,
                "GiantBearing",
                5.01f,
                (Func<bool>)(() => YoyoGuardianWorld.downedGiantBearing),
                ModContent.NPCType<GiantBearing>(),
                new Dictionary<string, object>
                {
                    ["spawnInfo"] = ModContent.GetInstance<GiantBearing>()
                        .GetLocalization("BossChecklistIntegration.SpawnInfo")
                }
            );

            // Cowboy of Yoyos
            bossChecklist.Call(
                "LogMiniBoss",
                Mod,
                "CowboyOfYoyos",
                1.1f,
                (Func<bool>)(() => YoyoGuardianWorld.downedCowboyOfYoyos),
                ModContent.NPCType<CowboyOfYoyos1>(),
                new Dictionary<string, object>
                {
                    ["displayName"] = "Cowboys of Yoyos (all three)",

                    ["spawnItems"] = new List<int>
                    {
                        ModContent.ItemType<OldWantedPaper>()
                    },

                    ["spawnInfo"] = ModContent.GetInstance<CowboyOfYoyos1>()
                        .GetLocalization("BossChecklistIntegration.SpawnInfo")
                }
            );

			// Ancient Duelist
			bossChecklist.Call(
				"LogMiniBoss",
				Mod,
				"AncientDuelist",
				13.4f,
				(Func<bool>)(() => YoyoGuardianWorld.downedAncientDuelist),
				ModContent.NPCType<AncientDuelist>(),
				new Dictionary<string, object>
				{
					["spawnItems"] = new List<int>
					{
						ModContent.ItemType<Content.Items.AncientWantedPaper>()
					},

					["spawnInfo"] = ModContent.GetInstance<AncientDuelist>()
						.GetLocalization("BossChecklistIntegration.SpawnInfo")
				}
			);

            // Bearing Party
            try
            {
                bossChecklist.Call(
                    "LogEvent",
                    Mod,
                    "BearingParty",
                    5.02f,
                    (Func<bool>)(() => BearingPartyEvent.Active),
                    new List<int>
                    {
                        ModContent.NPCType<WildBearing>()
                    },
                    new Dictionary<string, object>
                    {
                        ["spawnItems"] = new List<int>
                        {
                            ModContent.ItemType<BearingCaller>()
                        }
                    }
                );
            }
            catch
            {
                // The event still works if this Boss Checklist version
                // does not support the event registration.
            }
        }
    }

    public class YoyoGuardianChecklistGlobalNPC : GlobalNPC
    {
        public override void OnKill(NPC npc)
        {
            if (npc.type == ModContent.NPCType<YoyoGuardian>())
            {
                if (npc.ModNPC is YoyoGuardian guardian && guardian.IsRealBoss)
                    YoyoGuardianWorld.downedYoyoGuardian = true;

                return;
            }

            if (npc.type == ModContent.NPCType<GiantBearing>())
            {
                YoyoGuardianWorld.downedGiantBearing = true;
                RollOreShower(npc);
                return;
            }

            // Cowboy of Yoyos uses three separate NPC types. The trio counts
            // only when the last one dies: the dying NPC still reads as
            // active inside OnKill, so it must be excluded or the set can
            // never complete (killed all three, nothing credited).
            if (npc.type == ModContent.NPCType<CowboyOfYoyos1>() ||
                npc.type == ModContent.NPCType<CowboyOfYoyos2>() ||
                npc.type == ModContent.NPCType<CowboyOfYoyos3>())
            {
                bool anyStillAlive =
                    AnyActiveExcept(ModContent.NPCType<CowboyOfYoyos1>(), npc.whoAmI) ||
                    AnyActiveExcept(ModContent.NPCType<CowboyOfYoyos2>(), npc.whoAmI) ||
                    AnyActiveExcept(ModContent.NPCType<CowboyOfYoyos3>(), npc.whoAmI);

                if (!anyStillAlive && !YoyoGuardianWorld.downedCowboyOfYoyos) {
                    YoyoGuardianWorld.downedCowboyOfYoyos = true;
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        global::BiltonsYoyoMod.Content.Placeables.PlaceableNet.SendCowboyDowned();
                    else if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.WorldData);
                }

                return;
            }

            if (npc.type == ModContent.NPCType<AncientDuelist>())
            {
                YoyoGuardianWorld.downedAncientDuelist = true;
            }
        }

        // Loot rules (not OnKill rolls) so Boss Checklist displays the
        // same tables the player actually gets.
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            int cowboy1 = ModContent.NPCType<CowboyOfYoyos1>();
            int cowboy2 = ModContent.NPCType<CowboyOfYoyos2>();
            int cowboy3 = ModContent.NPCType<CowboyOfYoyos3>();

            if (npc.type == cowboy1 || npc.type == cowboy2 || npc.type == cowboy3) {
                // Trio table once: only when the other two are already down.
                bool othersAlive = false;
                foreach (int t in new int[] { cowboy1, cowboy2, cowboy3 }) {
                    if (t != npc.type && AnyActive(t)) {
                        othersAlive = true;
                        break;
                    }
                }
                if (!othersAlive) {
                    npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.Accessories.TangledStrings>(), 2, 1, 1));
                    npcLoot.Add(new Common.PercentDropRule(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 75, 5, 20));
                    npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 4, 1, 5));
                    npcLoot.Add(ItemDropRule.Common(Terraria.ID.ItemID.DesertFossil, 10, 1, 99));
                    npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.SpecializedYoyo>(), 20, 1, 1));
                }
                return;
            }

            if (npc.type == ModContent.NPCType<GiantBearing>()) {
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 1, 10, 30));
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AdvancedYoyoParts>(), 5, 1, 2));
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.HeavyYoyo>(), 4, 1, 1));
                return;
            }

            if (npc.type == ModContent.NPCType<AncientDuelist>()) {
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.AncientYoyo>(), 2, 1, 1));
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.MagicalEssence>(), 20, 1, 5));
                npcLoot.Add(new Common.PercentDropRule(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AncientScraps>(), 90, 1, 25));
                npcLoot.Add(new Common.PercentDropRule(ModContent.ItemType<global::BiltonsYoyoMod.Items.YoyoWisdom>(), 85, 1, 10));
                return;
            }
        }

        // Bonus ore shower has no rule equivalent: direct roll on the kill.
        private static void RollOreShower(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || Main.rand.Next(100) >= 20)
                return;
            int[] ores = new int[] {
                Terraria.ID.ItemID.IronOre, Terraria.ID.ItemID.LeadOre,
                Terraria.ID.ItemID.SilverOre, Terraria.ID.ItemID.TungstenOre,
                Terraria.ID.ItemID.GoldOre, Terraria.ID.ItemID.PlatinumOre
            };
            Item.NewItem(npc.GetSource_Death(), npc.Center, Main.rand.Next(ores), Main.rand.Next(5, 21));
        }

        private static bool AnyActive(int type)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == type)
                    return true;
            }

            return false;
        }

        // Same scan minus the dying NPC: OnKill still sees it as active.
        private static bool AnyActiveExcept(int type, int exceptWhoAmI)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (i == exceptWhoAmI)
                    continue;
                if (Main.npc[i].active && Main.npc[i].type == type)
                    return true;
            }

            return false;
        }
    }
}