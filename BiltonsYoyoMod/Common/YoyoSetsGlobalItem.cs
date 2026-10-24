using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Common
{
	// Registers every mod yoyo in the vanilla ItemID.Sets.Yoyo set so the
	// Yoyo Glove, Yoyo Bag and counterweights recognise them. Uses the
	// item-side yoyo shape (channelled MeleeNoSpeed thrower) instead of the
	// projectile sets so it works regardless of load order.
	public class YoyoSetsGlobalItem : GlobalItem
	{
		public override bool AppliesToEntity(Item entity, bool lateInstantiation)
		{
			return entity.ModItem != null
				&& entity.ModItem.Mod == Mod
				&& !entity.accessory
				&& entity.channel
				&& entity.noMelee
				&& entity.shoot > ProjectileID.None
				&& entity.DamageType == DamageClass.MeleeNoSpeed;
		}

		public override void SetDefaults(Item entity)
		{
			ItemID.Sets.Yoyo[entity.type] = true;
		}
	}
}
