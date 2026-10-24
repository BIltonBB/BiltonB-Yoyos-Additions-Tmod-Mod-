using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Items
{
    public abstract class YoyoStorageCase : ModItem
    {
        public abstract int StorageTier { get; }
        public abstract int[] StorageSlots { get; }
        public virtual float TrickDamageBonus => 0f;
        public virtual float YoyoDamageBonus => 0f;
        public virtual bool YoyosPassThroughBlocks => false;

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(gold: StorageTier + 1);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "YoyoStorage", $"Yoyo Storage Tier {StorageTier}"));
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<YoyoStoragePlayer>().Activate(this);
        }
    }

}
