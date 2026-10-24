using BiltonsYoyoMod.Players;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.GlobalProjectiles
{
    public class YoyoStorageGlobalItem : GlobalItem
    {
        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            if (item.channel && item.DamageType.CountsAsClass(DamageClass.Melee) && item.shoot > ProjectileID.None)
                damage += player.GetModPlayer<YoyoStoragePlayer>().YoyoDamageBonus;
        }
    }
}
