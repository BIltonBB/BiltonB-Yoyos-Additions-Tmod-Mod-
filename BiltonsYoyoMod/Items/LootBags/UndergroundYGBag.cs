using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.LootBags
{
    // Expert+ bag for the Yoyo Guardian killed underground (or on the
    // surface while debugging). Same drops as Classic, slightly higher amounts.
    public class UndergroundYGBag : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.expert = true;
            Item.maxStack = 999;
            Item.rare = ItemRarityID.Expert;
            Item.value = 0;
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot)
        {
            // Charged Guardian Yoyo, 100%.
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<GuardianYoyo>(), 1));
            // Guardian Core, 100%, slightly more than Classic's 5-20.
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<Materials.GuardianCoreM>(), 1, 8, 16));
            // Pure Emblem, 100% x1-2 (Classic is 75% x1-2).
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<Materials.PureEmblemY>(), 1));
        }
    }
}
