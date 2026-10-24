using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.NPCs
{
    public class YoyoDrops : GlobalNPC
    {
        // 1-in-4 = 25%
        private const int DropChance = 4;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // Pre-Hardmode

            // King Slime → Royal Yoyo
            if (npc.type == NPCID.KingSlime)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.RoyalYoyo>());

            // Eye of Cthulhu → Eye Yoyo
            if (npc.type == NPCID.EyeofCthulhu)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.EyeYoyo>(), 3);

            // Deerclops → Deer Yoyo
            if (npc.type == NPCID.Deerclops)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.DeerYoyo>());

            // Skeletron → Skeletron Yoyo
            if (npc.type == NPCID.SkeletronHead)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.SkeletronYoyo>());

            // Queen Bee → Bee Yoyo
            if (npc.type == NPCID.QueenBee)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.BeeYoyo>());

            // Eater of Worlds → Corruption Yoyo
            // Worm boss: only roll on the FINAL segment, otherwise every
            // segment death would roll for a yoyo.
            if (npc.type == NPCID.EaterofWorldsHead || npc.type == NPCID.EaterofWorldsBody || npc.type == NPCID.EaterofWorldsTail)
            {
                if (IsFinalSegment(npc, NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail))
                    ClassicDrop(npcLoot, ModContent.ItemType<Items.CorruptionYoyo>());
            }

            // Brain of Cthulhu → Crimson Yoyo
            if (npc.type == NPCID.BrainofCthulhu)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.CrimsonYoyo>());

            // Wall of Flesh → Flesh Yoyo + Yoyo Emblem
            if (npc.type == NPCID.WallofFlesh)
            {
                ClassicDrop(npcLoot, ModContent.ItemType<Items.FleshYoyo>());
                ClassicDrop(npcLoot, ModContent.ItemType<Items.YoyoSubEmblem>());
            }

            // Hardmode

            // The Twins → Twins Yoyo (roll only when the last twin dies)
            if (npc.type == NPCID.Retinazer || npc.type == NPCID.Spazmatism)
            {
                int otherTwin = npc.type == NPCID.Retinazer ? NPCID.Spazmatism : NPCID.Retinazer;
                if (!NPC.AnyNPCs(otherTwin))
                    ClassicDrop(npcLoot, ModContent.ItemType<Items.TwinsYoyo>());
            }

            // The Destroyer → Destroyer Yoyo (worm: final segment only)
            if (npc.type == NPCID.TheDestroyer && IsFinalSegment(npc, NPCID.TheDestroyer))
                ClassicDrop(npcLoot, ModContent.ItemType<Items.DestroyerYoyo>());

            // Skeletron Prime → Prime Yoyo
            if (npc.type == NPCID.SkeletronPrime)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.PrimeYoyo>());

            // Queen Slime → Hallow Yoyo + Yoyo Adrenaline
            if (npc.type == NPCID.QueenSlimeBoss)
            {
                ClassicDrop(npcLoot, ModContent.ItemType<Items.HallowYoyo>());
                ClassicDrop(npcLoot, ModContent.ItemType<Items.YoyoAdrenalineA>());
            }

            // Plantera → Plantera Yoyo
            if (npc.type == NPCID.Plantera)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.PlanteraYoyo>());

            // Golem → Golem Yoyo
            if (npc.type == NPCID.Golem)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.GolemYoyo>());

            // Empress of Light → Empress Yoyo
            if (npc.type == NPCID.HallowBoss)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.EmpressYoyo>());

            // Duke Fishron → Duke Yoyo
            if (npc.type == NPCID.DukeFishron)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.DukeYoyo>());

            // Lunatic Cultist → Lunatic Yoyo
            // The Cultist has no vanilla Treasure Bag, so this drops
            // directly in every difficulty.
            if (npc.type == NPCID.CultistBoss)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.LunaticYoyo>(), DropChance));

            // Moon Lord → Lord Yoyo
            if (npc.type == NPCID.MoonLordCore)
                ClassicDrop(npcLoot, ModContent.ItemType<Items.LordYoyo>());

            // Mini-boss / event yoyos (no vanilla Treasure Bags — drop directly, all difficulties)
            if (npc.type == NPCID.DD2DarkMageT1 || npc.type == NPCID.DD2DarkMageT3)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.DarkMageYoyo>(), DropChance));

            if (npc.type == NPCID.DD2OgreT2 || npc.type == NPCID.DD2OgreT3)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.OgreYoyo>(), DropChance));

            if (npc.type == NPCID.DD2Betsy)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.BetsyYoyo>(), DropChance));

            if (npc.type == NPCID.PirateShip)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.DutchmanYoyo>(), DropChance));

            if (npc.type == NPCID.MourningWood)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.MourningWoodYoyo>(), DropChance));

            if (npc.type == NPCID.Pumpking)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.PumpkingYoyo>(), DropChance));

            if (npc.type == NPCID.Everscream)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.EverscreamYoyo>(), DropChance));

            if (npc.type == NPCID.SantaNK1)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.SantaYoyo>(), DropChance));

            if (npc.type == NPCID.IceQueen)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.IceQueenYoyo>(), DropChance));

            if (npc.type == NPCID.MartianSaucer || npc.type == NPCID.MartianSaucerCore)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.MartianYoyo>(), DropChance));

            if (npc.type == NPCID.BloodNautilus)
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.DreadnautilusYoyo>(), DropChance));
        }

        // Classic-only direct drop. In Expert/Master the yoyo goes into
        // the boss's Treasure Bag instead (see Items/LootBagYoyoDrops.cs).
        private static void ClassicDrop(NPCLoot npcLoot, int itemType, int chanceDenominator = DropChance)
        {
            // ✅ FIXED: the rule is now actually added to the loot table!
            npcLoot.Add(ItemDropRule.ByCondition(new Conditions.NotExpert(), itemType, chanceDenominator));
        }

        // Returns true only if no other segment of this worm is alive,
        // so worm bosses drop loot once per kill instead of per segment.
        private static bool IsFinalSegment(NPC dying, params int[] segmentTypes)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (i == dying.whoAmI)
                    continue;

                NPC other = Main.npc[i];
                if (!other.active)
                    continue;

                foreach (int type in segmentTypes)
                {
                    if (other.type == type)
                        return false;
                }
            }
            return true;
        }
    }
}