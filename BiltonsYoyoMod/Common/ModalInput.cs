using Terraria;

namespace BiltonsYoyoMod.Common
{
	/// <summary>
	/// Shared gate for gameplay hotkeys. The guidebook and the String Splicer
	/// wheel are both modal interfaces: while either is open, pressing a
	/// gameplay key must not also cut strings, warp, snap or start a trick.
	/// </summary>
	public static class ModalInput
	{
		public static bool GameplayHotkeysBlocked(Player player)
		{
			if (player == null || !player.active)
				return true;
			return player.GetModPlayer<Items.Utility.StringSplicerPlayer>().WheelOpen
				|| Content.Guidebook.YoyoGuidebookUISystem.Open;
		}
	}
}
