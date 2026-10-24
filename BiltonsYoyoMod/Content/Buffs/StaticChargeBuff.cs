using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Buffs
{
	/// <summary>
	/// Applied by Static Trail. The next Magical Tech lightning hit on the
	/// marked enemy is amplified and discharges one short arc to a nearby
	/// enemy. It is deliberately a real buff (synced by Terraria) rather than
	/// a client-side timer, so multiplayer sees the same mark and the same
	/// discharge.
	/// </summary>
	public class StaticChargeBuff : ModBuff
	{
		// Reuse an existing static-looking mod icon; a missing texture would
		// otherwise make the debuff icon invisible in the buff strip.
		public override string Texture => "BiltonsYoyoMod/Content/Buffs/SnapSurgeBuff";

		public override void SetStaticDefaults()
		{
			Main.debuff[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
			Main.buffNoSave[Type] = true;
		}
	}
}
