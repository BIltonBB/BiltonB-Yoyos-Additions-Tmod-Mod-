using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Systems
{
    public class RevampYoyoRangeSystem : ModSystem
    {
        private readonly Dictionary<int, float> baseRanges = new Dictionary<int, float>();
        private int lastAppliedBonus = int.MinValue;

        public override void PostSetupContent()
        {
            baseRanges.Clear();
            lastAppliedBonus = int.MinValue;

            for (int type = 0; type < ProjectileLoader.ProjectileCount; type++)
            {
                float range = ProjectileID.Sets.YoyosMaximumRange[type];
                if (range > 0f)
                    baseRanges[type] = range;
            }
        }

        public override void PostUpdatePlayers()
        {
            int bonus = 0;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player == null || !player.active || player.dead)
                    continue;

                int playerBonus = player.GetModPlayer<RevampPlayer>().ExtraYoyoRange;
                if (playerBonus > bonus)
                    bonus = playerBonus;
            }

            if (bonus == lastAppliedBonus)
                return;

            lastAppliedBonus = bonus;
            foreach (KeyValuePair<int, float> pair in baseRanges)
                ProjectileID.Sets.YoyosMaximumRange[pair.Key] = pair.Value + bonus;
        }

        public override void OnModUnload()
        {
            foreach (KeyValuePair<int, float> pair in baseRanges)
                ProjectileID.Sets.YoyosMaximumRange[pair.Key] = pair.Value;

            baseRanges.Clear();
            lastAppliedBonus = int.MinValue;
        }
    }
}
