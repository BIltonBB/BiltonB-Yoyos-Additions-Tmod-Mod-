using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Content.Buffs
{
	public class BearingGreaseBuff : ModBuff
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/BearingOilBuff";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			float boost = player.GetModPlayer<YoyoStoragePlayer>().PotionBoost;
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.10f * boost;
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 5f * boost;
		}
	}

	public class CultistFervorBuff : ModBuff
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/CultistsDraughtBuff";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			float boost = player.GetModPlayer<YoyoStoragePlayer>().PotionBoost;
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.12f * boost;
			player.statDefense -= 4;
		}
	}

	public class StringWaxBuff : ModBuff
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/StringWaxBuff";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			float boost = player.GetModPlayer<YoyoStoragePlayer>().PotionBoost;
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.08f * boost;
		}
	}

	public class MoonlitFocusBuff : ModBuff
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/MoonlightFlaskBuff";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = false;
		}

		public override void Update(Player player, ref int buffIndex)
		{
			float boost = player.GetModPlayer<YoyoStoragePlayer>().PotionBoost;
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 6f * boost;
			player.statManaMax2 += (int)(20f * boost);
		}
	}
}
