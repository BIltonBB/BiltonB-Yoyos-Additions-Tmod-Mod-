using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    // Expert/Master/Revengeance: bosses drop Treasure Bags instead of
    // direct loot, so the yoyos are added to the bags here.
    public class BossBagLoot : GlobalItem
    {
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            // 1-in-4 = 25% per bag, matching the Classic drop chance.
            void AddYoyo(int type) => itemLoot.Add(ItemDropRule.Common(type, 4));

            switch (item.type)
            {
                case ItemID.KingSlimeBossBag:
                    AddYoyo(ModContent.ItemType<RoyalYoyo>());
                    break;

                case ItemID.EyeOfCthulhuBossBag:
                    itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<EyeYoyo>(), 3));
                    break;

                case ItemID.DeerclopsBossBag:
                    AddYoyo(ModContent.ItemType<DeerYoyo>());
                    break;

                case ItemID.SkeletronBossBag:
                    AddYoyo(ModContent.ItemType<SkeletronYoyo>());
                    break;

                case ItemID.QueenBeeBossBag:
                    AddYoyo(ModContent.ItemType<BeeYoyo>());
                    break;

                case ItemID.EaterOfWorldsBossBag:
                    AddYoyo(ModContent.ItemType<CorruptionYoyo>());
                    break;

                case ItemID.BrainOfCthulhuBossBag:
                    AddYoyo(ModContent.ItemType<CrimsonYoyo>());
                    break;

                case ItemID.WallOfFleshBossBag:
                    AddYoyo(ModContent.ItemType<FleshYoyo>());
                    AddYoyo(ModContent.ItemType<YoyoSubEmblem>());
                    break;

                case ItemID.TwinsBossBag:
                    AddYoyo(ModContent.ItemType<TwinsYoyo>());
                    break;

                case ItemID.DestroyerBossBag:
                    AddYoyo(ModContent.ItemType<DestroyerYoyo>());
                    break;

                case ItemID.SkeletronPrimeBossBag:
                    AddYoyo(ModContent.ItemType<PrimeYoyo>());
                    break;

                case ItemID.QueenSlimeBossBag:
                    AddYoyo(ModContent.ItemType<HallowYoyo>());
                    AddYoyo(ModContent.ItemType<YoyoAdrenalineA>());
                    break;

                case ItemID.PlanteraBossBag:
                    AddYoyo(ModContent.ItemType<PlanteraYoyo>());
                    break;

                case ItemID.GolemBossBag:
                    AddYoyo(ModContent.ItemType<GolemYoyo>());
                    break;

                case ItemID.FairyQueenBossBag:
                    AddYoyo(ModContent.ItemType<EmpressYoyo>());
                    break;

                case ItemID.FishronBossBag:
                    AddYoyo(ModContent.ItemType<DukeYoyo>());
                    break;

                case ItemID.MoonLordBossBag:
                    AddYoyo(ModContent.ItemType<LordYoyo>());
                    break;
            }
        }
    }
}