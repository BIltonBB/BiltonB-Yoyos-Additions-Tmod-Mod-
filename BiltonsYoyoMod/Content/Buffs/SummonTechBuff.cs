using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Buffs
{
	/// <summary>
	/// Summon Tech attunement marker: shown while summon minions are out.
	/// Right-click the icon to dismiss every summon (tech OFF).
	/// No stats: pure status + dismiss switch. Never saved.
	/// </summary>
	public class SummonTechBuff : ModBuff
	{
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/SummonTechBuff";

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = false;
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
		}
	}
}
