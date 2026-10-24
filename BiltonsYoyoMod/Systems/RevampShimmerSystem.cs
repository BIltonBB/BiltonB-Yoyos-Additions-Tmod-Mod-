using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items.Materials;

namespace BiltonsYoyoMod.Systems
{
    public class RevampShimmerSystem : ModSystem
    {
        private static readonly int[] GemChoices = new int[]
        {
            ItemID.Amethyst,
            ItemID.Topaz,
            ItemID.Sapphire,
            ItemID.Emerald,
            ItemID.Ruby,
            ItemID.Diamond,
            ItemID.Amber
        };

        public override void PostUpdateWorld()
        {
            int essenceType = ModContent.ItemType<MagicalEssence>();

            for (int i = 0; i < Main.maxItems; i++)
            {
                Item item = Main.item[i];
                if (item == null || !item.active || item.IsAir)
                    continue;

                if (item.type != essenceType)
                    continue;

                if (!item.shimmerWet)
                    continue;

                if (Main.netMode == NetmodeID.MultiplayerClient)
                    continue;

                int stack = item.stack;
                item.SetDefaults(GemChoices[Main.rand.Next(GemChoices.Length)]);
                item.stack = stack;

                Item.ShimmerEffect(item.Center);

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, i);
            }
        }
    }
}
