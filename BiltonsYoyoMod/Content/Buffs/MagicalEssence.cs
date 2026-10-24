using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Buffs
{
	// Granted by petting the Magical Yoyo town pet. Yoyos deal +5% damage.
	public class MagicalEssence : ModBuff
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/MagicalEssenceBuff";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = false;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.05f;
		}
	}
}
