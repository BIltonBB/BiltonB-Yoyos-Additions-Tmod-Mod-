using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.NPCs
{
    public class RevampInsightGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool markedByInsightYoyo;
        private int markingPlayer = -1;
        private int markTimer;

        private const int MarkDuration = 10 * 60;

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            if (projectile == null || !projectile.active)
                return;

            if (projectile.aiStyle != ProjAIStyleID.Yoyo)
                return;

            if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return;

            Player owner = Main.player[projectile.owner];
            if (owner == null || !owner.active)
                return;

            if (!owner.HasBuff(ModContent.BuffType<Buffs.YoyoInsightBuff>()))
                return;

            markedByInsightYoyo = true;
            markingPlayer = projectile.owner;
            markTimer = MarkDuration;
        }

        public override void PostAI(NPC npc)
        {
            if (!markedByInsightYoyo)
                return;

            markTimer--;
            if (markTimer <= 0)
            {
                markedByInsightYoyo = false;
                markingPlayer = -1;
            }
        }

        public override void OnKill(NPC npc)
        {
            if (!markedByInsightYoyo)
                return;

            if (markingPlayer < 0 || markingPlayer >= Main.maxPlayers)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Player player = Main.player[markingPlayer];
            if (player == null || !player.active)
                return;

            if (!player.HasBuff(ModContent.BuffType<Buffs.YoyoInsightBuff>()))
                return;

            // Insight bonus: yoyo kills pay out extra coins.
            int coinType = npc.boss ? ItemID.GoldCoin : ItemID.SilverCoin;
            int stack = npc.boss ? Main.rand.Next(5, 11) : Main.rand.Next(1, 4);
            Item.NewItem(npc.GetSource_Loot(), npc.Center, coinType, stack);
        }
    }
}
