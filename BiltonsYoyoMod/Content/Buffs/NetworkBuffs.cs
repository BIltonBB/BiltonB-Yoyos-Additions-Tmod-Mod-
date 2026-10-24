using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Buffs
{
	// Station-network combo buffs (#12 expansion). Each has its own icon
	// (Content/Buffs) and hjson description. Granted by station pairs
	// sharing one mesh; see StationNetwork combo helpers.
	public class AnchorBlessingBuff : ModBuff
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/AnchorBlessingBuff";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			player.statDefense += 8;
		}
	}

	public class HarmonicHumBuff : ModBuff
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/HarmonicHumBuff";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.10f;
		}
	}

	public class SnapSurgeBuff : ModBuff
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/SnapSurgeBuff";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 10f;
		}
	}
}
