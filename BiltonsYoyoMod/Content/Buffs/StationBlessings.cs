using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Content.Buffs
{
	// Station blessings: earned by beating a station's resonance minigame in its UI.
	// Each lasts 4 minutes and carries a buff unique to that machine. Icons are
	// placeholders (copy any 32x32 buff art over the matching *Buff.png to reskin).
	public abstract class StationBlessingBase : ModBuff
	{
		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = false;
		}

		protected Item HeldYoyo(Player player)
		{
			Item held = player.HeldItem;
			if (held != null && !held.IsAir && held.shoot > ProjectileID.None
				&& held.shoot < ProjectileID.Sets.YoyosMaximumRange.Length
				&& ProjectileID.Sets.YoyosMaximumRange[held.shoot] > 0f)
				return held;
			return null;
		}
	}

	public class StoneskinPoiseBuff : StationBlessingBase
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/StoneskinPoiseBuff";
		public override void Update(Player player, ref int buffIndex)
		{
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.08f;
			player.statDefense += 4;
		}
	}

	public class LightRiggingBuff : StationBlessingBase
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/LightRiggingBuff";
		public override void Update(Player player, ref int buffIndex)
		{
			player.moveSpeed += 0.08f;
			Item held = HeldYoyo(player);
			if (held != null)
				YoyoStaticBoosts.BoostTopSpeed(held.shoot, 1.12f);
		}
	}

	public class DuelistEyeBuff : StationBlessingBase
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/DuelistEyeBuff";
		public override void Update(Player player, ref int buffIndex)
		{
			player.GetCritChance(DamageClass.MeleeNoSpeed) += 8f;
		}
	}

	public class LongLensesBuff : StationBlessingBase
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/LongLensesBuff";
		public override void Update(Player player, ref int buffIndex)
		{
			player.dangerSense = true;
			Item held = HeldYoyo(player);
			if (held != null)
				YoyoStaticBoosts.BoostRange(held.shoot, 24f);
		}
	}

	public class OverdriveBuff : StationBlessingBase
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/OverdriveBuff";
		public override void Update(Player player, ref int buffIndex)
		{
			Item held = HeldYoyo(player);
			if (held != null) {
				YoyoStaticBoosts.BoostTopSpeed(held.shoot, 1.08f);
				YoyoStaticBoosts.BoostRange(held.shoot, 16f);
			}
			if (Main.rand.NextBool(12)) {
				Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Electric, 0f, -1f, 120, default, 0.7f);
				d.noGravity = true;
			}
		}
	}

	public class FoldMendingBuff : StationBlessingBase
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/FoldMendingBuff";
		public override void Update(Player player, ref int buffIndex)
		{
			player.GetDamage(DamageClass.MeleeNoSpeed) += 0.06f;
			player.lifeRegen += 3;
			Lighting.AddLight(player.Center, 0.2f, 0.1f, 0.3f);
		}
	}
}
