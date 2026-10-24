using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items
{
    public class YoyoOfInsight : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 9999;
            Item.consumable = true;

            Item.useStyle = ItemUseStyleID.DrinkLong;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.UseSound = SoundID.Item3;

            Item.buffType = ModContent.BuffType<Buffs.YoyoInsightBuff>();
            Item.buffTime = 10 * 60 * 60;

            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(silver: 75);
        }
    }
}
