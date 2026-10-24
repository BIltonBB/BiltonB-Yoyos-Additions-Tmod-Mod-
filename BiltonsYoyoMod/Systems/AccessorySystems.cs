using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Systems
{
    public class AccessorySystems : GlobalItem
    {
        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player)
        {
            if (equippedItem.IsAir || incomingItem.IsAir)
                return true;

            if (!equippedItem.accessory || !incomingItem.accessory)
                return true;

            if (equippedItem.ModItem?.Mod.Name != "BiltonsYoyoMod")
                return true;

            if (incomingItem.ModItem?.Mod.Name != "BiltonsYoyoMod")
                return true;

            if (equippedItem.type == incomingItem.type)
                return false;

            return true;
        }
    }
}