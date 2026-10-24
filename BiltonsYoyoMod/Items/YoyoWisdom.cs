using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items
{
    public class YoyoWisdom : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 999;
            Item.consumable = true;

            Item.useStyle = ItemUseStyleID.DrinkLong;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.UseSound = SoundID.Item3;

            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(gold: 5);
        }

        public override bool CanUseItem(Player player)
        {
            if (player.GetModPlayer<RevampPlayer>().WisdomConsumed)
                return false;

            return base.CanUseItem(player);
        }

        public override bool? UseItem(Player player)
        {
            player.GetModPlayer<RevampPlayer>().WisdomConsumed = true;
            return true;
        }
    }
}
