using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.NPCs
{
    public class WildStringsGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private const int StackLifetime = 10 * 60;
        private const int MaxStacks = 32;
        private readonly int[] stackTimers = new int[MaxStacks];

        public int PolyesterDefenseReduction
        {
            get
            {
                int stacks = 0;
                for (int i = 0; i < stackTimers.Length; i++)
                {
                    if (stackTimers[i] > 0)
                        stacks++;
                }

                return stacks * 3;
            }
        }

        public void AddPolyesterStack()
        {
            // Each hit creates one independent -3 defense stack lasting 10s.
            for (int i = 0; i < stackTimers.Length; i++)
            {
                if (stackTimers[i] <= 0)
                {
                    stackTimers[i] = StackLifetime;
                    return;
                }
            }

            // Hard cap: replace the oldest remaining stack when full.
            int oldestIndex = 0;
            for (int i = 1; i < stackTimers.Length; i++)
            {
                if (stackTimers[i] < stackTimers[oldestIndex])
                    oldestIndex = i;
            }

            stackTimers[oldestIndex] = StackLifetime;
        }

        public override void PostAI(NPC npc)
        {
            for (int i = 0; i < stackTimers.Length; i++)
            {
                if (stackTimers[i] > 0)
                    stackTimers[i]--;
            }
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            int armorPenetration = PolyesterDefenseReduction;
            if (armorPenetration <= 0)
                return;

            // Never let the temporary AP effect exceed the NPC's current defense.
            // This prevents an artificial negative-defense state.
            int safeCap = npc.defense > 0 ? npc.defense : 0;
            armorPenetration = armorPenetration > safeCap ? safeCap : armorPenetration;
            if (armorPenetration <= 0)
                return;

            // Temporary armor penetration only; NPC.defense is never mutated.
            modifiers.ArmorPenetration += armorPenetration;
        }

        public override void OnKill(NPC npc)
        {
            for (int i = 0; i < stackTimers.Length; i++)
                stackTimers[i] = 0;
        }
    }
}
