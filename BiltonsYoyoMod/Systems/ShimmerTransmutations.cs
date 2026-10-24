using BiltonsYoyoMod.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Systems
{
    public class ShimmerTransmutations : ModSystem
    {
        public override void PostSetupContent()
        {
            // Emblem shimmering
            ItemID.Sets.ShimmerTransformToItem[ItemID.WarriorEmblem] = ModContent.ItemType<YoyoSubEmblem>();
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<YoyoSubEmblem>()] = ItemID.RangerEmblem;

            // Case shimmer line
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<YoyoBackPackA>()] = ModContent.ItemType<YoyoShimmerBackPackA>();
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<YoyoRuckSackA>()] = ModContent.ItemType<YoyoShimmerRuckSackA>();
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<YoyoVaultA>()] = ModContent.ItemType<YoyoShimmerVaultA>();
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<YoyoShimmerBackPackA>()] = ModContent.ItemType<YoyoBackPackA>();
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<YoyoShimmerRuckSackA>()] = ModContent.ItemType<YoyoRuckSackA>();
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<YoyoShimmerVaultA>()] = ModContent.ItemType<YoyoVaultA>();

            // Evil counterparts (same boss tier, mirrors the Master trade)
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<CorruptionYoyo>()] = ModContent.ItemType<CrimsonYoyo>();
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<CrimsonYoyo>()] = ModContent.ItemType<CorruptionYoyo>();

            // Counterweight sidegrades (same relic tier)
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<Items.BlackMarket.GoldenCounterweight>()] = ModContent.ItemType<Items.Ancient.AncientCounterweight>();
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<Items.Ancient.AncientCounterweight>()] = ModContent.ItemType<Items.BlackMarket.GoldenCounterweight>();

            // String sidegrades (same era)
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<Items.BlackMarket.GoldenString>()] = ModContent.ItemType<Items.Ancient.AncientString>();
            ItemID.Sets.ShimmerTransformToItem[ModContent.ItemType<Items.Ancient.AncientString>()] = ModContent.ItemType<Items.BlackMarket.GoldenString>();
        }
    }
}