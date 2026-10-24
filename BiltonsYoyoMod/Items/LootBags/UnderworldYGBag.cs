using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.LootBags
{
    // Expert+ bag for the Yoyo Guardian killed in the Underworld.
    // Same drops as Classic, with slightly higher amounts.
    public class UnderworldYGBag : ModItem
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
            // Guardian Yoyo, 100%.
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<CGuardianYoyo>(), 1));
            // Guardian Core, 100%, slightly more than Classic's 1-10.
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<Materials.GuardianCoreM>(), 1, 12, 25));
            // Pure Emblem, 100% x1 (Classic is 50% x1).
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<Materials.PureEmblemY>(), 1, 1, 2));
        }
    }
}
