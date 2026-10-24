using System;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using BiltonsYoyoMod.Content.NPCs.Bosses;

namespace BiltonsYoyoMod.Content.Systems
{
    public class YoyoGuardianPhase1Music : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Content/Music/YGP1");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override bool IsSceneEffectActive(Player player) => YoyoGuardianMusicHelper.NearbyGuardianInPhase(player, phase1: true);
    }

    public class YoyoGuardianPhase2Music : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Content/Music/YGP2");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override bool IsSceneEffectActive(Player player) => YoyoGuardianMusicHelper.NearbyGuardianInPhase(player, phase1: false);
    }

    // Addition #2: 37s track for the DesperationPre -> Phase 3 transition. The "fades out at
    // 35s" part of the spec is expected to be baked into the YGTP3 audio file itself (the same
    // way every other track in this fight is just a plain slot with no manual fade code) rather
    // than driven from here.
    public class YoyoGuardianTransitionToPhase3Music : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Content/Music/YGTP3");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override bool IsSceneEffectActive(Player player) => YoyoGuardianMusicHelper.NearbyGuardianInPhase3Transition(player);
    }

    // Addition #5: Phase 3's own looping track. Same note as above re: fade in/out being an
    // audio-file concern.
    public class YoyoGuardianPhase3Music : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot(Mod, "Content/Music/YGP3");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override bool IsSceneEffectActive(Player player) => YoyoGuardianMusicHelper.NearbyGuardianInPhase3(player);
    }

    public static class YoyoGuardianMusicHelper
    {
        public static bool NearbyGuardianInPhase(Player player, bool phase1)
        {
            int type = ModContent.NPCType<YoyoGuardian>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != type) continue;
                if (npc.ModNPC is not YoyoGuardian guardian) continue;
                if (!guardian.IsRealBoss) continue;

                bool inPhase1 = guardian.Phase == YoyoGuardianPhase.Phase1 || guardian.Phase == YoyoGuardianPhase.Transition1;
                bool inPhase2Plus = !inPhase1;

                if ((phase1 && !inPhase1) || (!phase1 && !inPhase2Plus)) continue;

                // Don't let this catch Phase 3+ states — those have their own tracks below.
                if (!phase1 && guardian.Phase >= YoyoGuardianPhase.DesperationPre) continue;

                if (Microsoft.Xna.Framework.Vector2.Distance(player.Center, npc.Center) < 4000f)
                    return true;
            }

            return false;
        }

        public static bool NearbyGuardianInPhase3Transition(Player player) =>
            NearbyGuardianInAnyPhase(player, YoyoGuardianPhase.DesperationPre, YoyoGuardianPhase.TransitionToPhase3);

        public static bool NearbyGuardianInPhase3(Player player) =>
            NearbyGuardianInAnyPhase(player, YoyoGuardianPhase.Phase3, YoyoGuardianPhase.Phase3DeathSequence);

        private static bool NearbyGuardianInAnyPhase(Player player, params YoyoGuardianPhase[] phases)
        {
            int type = ModContent.NPCType<YoyoGuardian>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != type) continue;
                if (npc.ModNPC is not YoyoGuardian guardian) continue;
                if (!guardian.IsRealBoss) continue;
                if (Array.IndexOf(phases, guardian.Phase) < 0) continue;

                if (Microsoft.Xna.Framework.Vector2.Distance(player.Center, npc.Center) < 4000f)
                    return true;
            }

            return false;
        }
    }
}
