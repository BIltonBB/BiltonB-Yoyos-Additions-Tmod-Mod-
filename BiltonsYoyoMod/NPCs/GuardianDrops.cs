using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.NPCs
{
    public class GuardianDrops : GlobalNPC
    {
        private static bool KilledInUnderworld(NPC npc)
            => npc.Center.Y > (Main.maxTilesY - 200) * 16f;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type != ModContent.NPCType<Content.NPCs.Bosses.YoyoGuardian>())
                return;

            var notExpert = new Conditions.NotExpert();

            // UNDERWORLD

            npcLoot.Add(
                ItemDropRule.ByCondition(
                    new GuardianUnderworldCondition(),
                    ModContent.ItemType<Items.GuardianYoyo>(),
                    1
                )
            );

            npcLoot.Add(
                ItemDropRule.ByCondition(
                    new GuardianUnderworldCondition(),
                    ModContent.ItemType<Items.Materials.GuardianCoreM>(),
                    1,
                    1,
                    10
                )
            );

            npcLoot.Add(
                ItemDropRule.ByCondition(
                    new GuardianUnderworldCondition(),
                    ModContent.ItemType<Items.Materials.PureEmblemY>(),
                    2
                )
            );

            npcLoot.Add(
                ItemDropRule.ByCondition(
                    new GuardianUnderworldCondition(),
                    ModContent.ItemType<Items.LootBags.UnderworldYGBag>(),
                    1
                )
            );

            // UNDERGROUND

            npcLoot.Add(
                ItemDropRule.ByCondition(
                    new GuardianUndergroundCondition(),
                    ModContent.ItemType<Items.CGuardianYoyo>(),
                    1
                )
            );

            npcLoot.Add(
                ItemDropRule.ByCondition(
                    new GuardianUndergroundCondition(),
                    ModContent.ItemType<Items.Materials.GuardianCoreM>(),
                    1,
                    5,
                    20
                )
            );

            npcLoot.Add(
                ItemDropRule.ByCondition(
                    new GuardianUndergroundCondition(),
                    ModContent.ItemType<Items.Materials.PureEmblemY>(),
                    4,
                    1,
                    2,
                    3
                )
            );

            npcLoot.Add(
                ItemDropRule.ByCondition(
                    new GuardianUndergroundCondition(),
                    ModContent.ItemType<Items.LootBags.UndergroundYGBag>(),
                    1
                )
            );
        }
    }

    public class GuardianUnderworldCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info)
        {
            return info.npc.Center.Y > (Main.maxTilesY - 200) * 16f;
        }

        public bool CanShowItemDropInUI()
        {
            return true;
        }

        public string GetConditionDescription()
        {
            return "Dropped when the Yoyo Guardian is defeated in the Underworld";
        }
    }

    public class GuardianUndergroundCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info)
        {
            return info.npc.Center.Y <= (Main.maxTilesY - 200) * 16f;
        }

        public bool CanShowItemDropInUI()
        {
            return true;
        }

        public string GetConditionDescription()
        {
            return "Dropped when the Yoyo Guardian is defeated underground";
        }
    }
}