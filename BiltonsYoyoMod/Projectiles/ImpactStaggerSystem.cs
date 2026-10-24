using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Single-player impact stagger (hitstop feel without touching the sim
    // globally): freezes the STRUCK npc's AI for a few ticks on heavy hits.
    // SP-only by design (NPC AI is server-authoritative in MP); heavies only.
    public class ImpactStaggerSystem : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public int staggerTicks;

        public override bool PreAI(NPC npc)
        {
            if (staggerTicks > 0)
            {
                staggerTicks--;
                return false;
            }
            return true;
        }

        public static void Stagger(NPC target, int ticks)
        {
            if (Main.netMode != NetmodeID.SinglePlayer)
                return;
            if (target == null || !target.active)
                return;
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                if (cfg != null && !cfg.Feedback.ImpactStagger)
                    return;
            }
            catch
            {
            }
            var g = target.GetGlobalNPC<ImpactStaggerSystem>();
            if (ticks > g.staggerTicks)
                g.staggerTicks = System.Math.Min(ticks, 4);
        }
    }
}
