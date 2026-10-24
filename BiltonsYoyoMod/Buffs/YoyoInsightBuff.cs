using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Buffs
{
    public class YoyoInsightBuff : ModBuff
    {
        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<Players.RevampPlayer>().Insight = true;
        }
    }
}
