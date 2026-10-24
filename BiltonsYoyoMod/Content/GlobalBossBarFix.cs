using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Systems;
using BiltonsYoyoMod.Content.NPCs.Mobs;
using BiltonsYoyoMod.Content.NPCs.SubBosses;
using BiltonsYoyoMod.Content.NPCs.Bosses;

namespace BiltonsYoyoMod.Content
{
    public class GlobalBossBarFix : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void SetDefaults(NPC npc)
        {
            if (npc.ModNPC == null ||
                npc.ModNPC.Mod != ModContent.GetInstance<BiltonsYoyoMod>())
                return;

            int cowboy1 = ModContent.NPCType<CowboyOfYoyos1>();
            int cowboy2 = ModContent.NPCType<CowboyOfYoyos2>();
            int cowboy3 = ModContent.NPCType<CowboyOfYoyos3>();
            int giantBearing = ModContent.NPCType<GiantBearing>();
            int yoyoGuardian = ModContent.NPCType<YoyoGuardian>();

            // All three Cowboys use one shared boss bar.
            if (npc.type == cowboy1 ||
                npc.type == cowboy2 ||
                npc.type == cowboy3)
            {
                npc.boss = true;
                npc.BossBar =
                    ModContent.GetInstance<CowboyOfYoyosBossBar>();
            }

            // Giant Bearing is a normal boss NPC with its vanilla boss bar.
            if (npc.type == giantBearing)
            {
                npc.boss = true;

                NPCID.Sets.MPAllowedEnemies[npc.type] = true;

                if (!NPCID.Sets.BossBestiaryPriority.Contains(npc.type))
                    NPCID.Sets.BossBestiaryPriority.Add(npc.type);
            }

            // Yoyo Guardian uses its normal boss bar.
            if (npc.type == yoyoGuardian)
            {
                npc.boss = true;

                NPCID.Sets.MPAllowedEnemies[npc.type] = true;

                if (!NPCID.Sets.BossBestiaryPriority.Contains(npc.type))
                    NPCID.Sets.BossBestiaryPriority.Add(npc.type);
            }

            // Ancient Duelist: vanilla boss bar (boss flag lives on the NPC),
            // same multiplayer + bestiary treatment as the other sub-bosses.
            if (npc.type == ModContent.NPCType<AncientDuelist>())
            {
                NPCID.Sets.MPAllowedEnemies[npc.type] = true;

                if (!NPCID.Sets.BossBestiaryPriority.Contains(npc.type))
                    NPCID.Sets.BossBestiaryPriority.Add(npc.type);
            }

            // These NPCs are never bosses.
            if (npc.type == ModContent.NPCType<WildBearing>() ||
                npc.type == ModContent.NPCType<GuardianOrbitShield>() ||
                npc.type == ModContent.NPCType<StarGuardianNPC>() ||
                npc.type == ModContent.NPCType<MagicDestroyerHead>() ||
                npc.type == ModContent.NPCType<MagicDestroyerBody>() ||
                npc.type == ModContent.NPCType<MagicDestroyerTail>())
            {
                npc.boss = false;
                npc.BossBar = null;
            }
        }

        public override void Load()
        {
            // Cowboy head icons.
            Mod.AddBossHeadTexture(
                "BiltonsYoyoMod/Content/NPCs/SubBosses/TCOY1_head",
                ModContent.NPCType<CowboyOfYoyos1>()
            );

            Mod.AddBossHeadTexture(
                "BiltonsYoyoMod/Content/NPCs/SubBosses/TCOY2_head",
                ModContent.NPCType<CowboyOfYoyos2>()
            );

            Mod.AddBossHeadTexture(
                "BiltonsYoyoMod/Content/NPCs/SubBosses/TCOY3_head",
                ModContent.NPCType<CowboyOfYoyos3>()
            );

            // Giant Bearing head icon.
            Mod.AddBossHeadTexture(
                "BiltonsYoyoMod/Content/NPCs/SubBosses/GiantBearing_Head_Boss",
                ModContent.NPCType<GiantBearing>()
            );

            // Ancient Duelist head icon.
            Mod.AddBossHeadTexture(
                "BiltonsYoyoMod/Content/NPCs/SubBosses/AncientDuelist_Head_Boss",
                ModContent.NPCType<AncientDuelist>()
            );

            // Yoyo Guardian head icons.
            Mod.AddBossHeadTexture(
                "BiltonsYoyoMod/Content/NPCs/Bosses/YoyoGuardian_Head_Boss",
                ModContent.NPCType<YoyoGuardian>()
            );

            Mod.AddBossHeadTexture(
                "BiltonsYoyoMod/Content/NPCs/Bosses/YoyoGuardian_Head_BossDP",
                ModContent.NPCType<YoyoGuardian>()
            );
        }

        public override void BossHeadSlot(NPC npc, ref int index)
        {
            if (npc.type == ModContent.NPCType<CowboyOfYoyos1>())
            {
                index = NPCHeadLoader.GetBossHeadSlot(
                    "BiltonsYoyoMod/Content/NPCs/SubBosses/TCOY1_head"
                );

                return;
            }

            if (npc.type == ModContent.NPCType<CowboyOfYoyos2>())
            {
                index = NPCHeadLoader.GetBossHeadSlot(
                    "BiltonsYoyoMod/Content/NPCs/SubBosses/TCOY2_head"
                );

                return;
            }

            if (npc.type == ModContent.NPCType<CowboyOfYoyos3>())
            {
                index = NPCHeadLoader.GetBossHeadSlot(
                    "BiltonsYoyoMod/Content/NPCs/SubBosses/TCOY3_head"
                );

                return;
            }

            if (npc.type == ModContent.NPCType<GiantBearing>())
            {
                index = NPCHeadLoader.GetBossHeadSlot(
                    "BiltonsYoyoMod/Content/NPCs/SubBosses/GiantBearing_Head_Boss"
                );

                return;
            }

            if (npc.type == ModContent.NPCType<AncientDuelist>())
            {
                index = NPCHeadLoader.GetBossHeadSlot(
                    "BiltonsYoyoMod/Content/NPCs/SubBosses/AncientDuelist_Head_Boss"
                );

                return;
            }

            if (npc.type == ModContent.NPCType<YoyoGuardian>())
            {
                bool desperation =
                    npc.ModNPC is YoyoGuardian guardian &&
                    guardian.Phase >= YoyoGuardianPhase.DesperationPre;

                index = NPCHeadLoader.GetBossHeadSlot(
                    desperation
                        ? "BiltonsYoyoMod/Content/NPCs/Bosses/YoyoGuardian_Head_BossDP"
                        : "BiltonsYoyoMod/Content/NPCs/Bosses/YoyoGuardian_Head_Boss"
                );
            }
        }

        public override void OnSpawn(
            NPC npc,
            Terraria.DataStructures.IEntitySource source)
        {
            if (npc.type != ModContent.NPCType<CowboyOfYoyos1>())
                return;

            int leader = -1;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (other.active &&
                    other.type == npc.type &&
                    (leader == -1 || i < leader))
                {
                    leader = i;
                }
            }

            npc.realLife =
                leader == npc.whoAmI
                    ? -1
                    : leader;
        }
    }
}