using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.TierEmblems
{
    public abstract class TierEmblem : ModItem
    {
        public abstract int Tier { get; }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(0, Tier, 0, 0);
            Item.rare = Utils.Clamp(ItemRarityID.White + Tier, ItemRarityID.White, ItemRarityID.Red);
        }
    }
}