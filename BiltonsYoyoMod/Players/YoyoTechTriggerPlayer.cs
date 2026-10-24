using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Tech;
using BiltonsYoyoMod.Settings;

namespace BiltonsYoyoMod.Players
{
	/// <summary>
	/// Local-only hotkeys so techs and tools work while a yoyo is held:
	/// no inventory juggling, no held-item conflicts.
	/// </summary>
	public class YoyoTechTriggerPlayer : ModPlayer
	{
		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			// Keybinds are local input: only the local player acts on them.
			if (Player.whoAmI != Main.myPlayer || Player.dead || !Player.active)
				return;
			var splicer = Player.GetModPlayer<Items.Utility.StringSplicerPlayer>();

			// The guidebook is modal. Its C/F/B/T keys must not also fire
			// gameplay hotkeys (C is the default Swap Yoyo key).
			if (Content.Guidebook.YoyoGuidebookUISystem.Open) {
				splicer.HardReset();
				return;
			}
			// Panic Equip works even while the wheel is open; it is the one
			// combat key that should never be blocked by a modal panel.
			var panicKey = YoyoTechKeybind.PanicYoyoKey;
			if (panicKey != null && panicKey.JustPressed) {
				splicer.EquipPanicYoyo(Player);
				return;
			}

			// The Splicer is modal. While its wheel is up, gameplay hotkeys
			// (tech cycling, magical tech, etc.) must not fire. Swap still closes
			// the wheel even while the search field is focused, but now confirms
			// the highlighted yoyo instead of throwing the selection away. The
			// search field refuses the Swap character, so the press cannot leak
			// into the query before the wheel closes.
			if (splicer.WheelOpen) {
				var closeKey = YoyoTechKeybind.SwapYoyoKey;
				if (closeKey != null && closeKey.JustPressed)
					splicer.ToggleWheel(Player);
				return;
			}

			// The active board's splice sequence is a curated short list. This
			// key walks it in a loop and is unbound by default, so it never
			// competes with another mod's controls until the player assigns it.
			var spliceKey = YoyoTechKeybind.SpliceSequenceKey;
			if (spliceKey != null && spliceKey.JustPressed) {
				splicer.AdvanceSequence(Player);
				return;
			}

			// Instant carried-yoyo cycle. Shift reverses the direction. This is
			// deliberately outside the wheel: it is for players who carry yoyos
			// beyond the ten hotbar slots and do not want a modal panel.
			var quickCycle = YoyoTechKeybind.QuickCycleYoyoKey;
			if (quickCycle != null && quickCycle.JustPressed) {
				bool reverse = Main.keyState.IsKeyDown(Keys.LeftShift)
					|| Main.keyState.IsKeyDown(Keys.RightShift);
				splicer.CycleCarriedYoyo(Player, reverse ? -1 : 1);
				return;
			}

			// Dowsing sonar is a short informational reveal; it does not touch
			// items or spawns, so it is safe to fire alongside normal combat.
			var sonarKey = YoyoTechKeybind.DowsingSonarKey;
			if (sonarKey != null && sonarKey.JustPressed) {
				Player.GetModPlayer<Items.Utility.DowsingPlayer>().PingSonar(Player);
				return;
			}

			if (YoyoTechKeybind.CycleTechKey != null && YoyoTechKeybind.CycleTechKey.JustPressed)
				Player.GetModPlayer<YoyoTechPlayer>().CycleTech();
			if (YoyoTechKeybind.MagicalTechKey != null && YoyoTechKeybind.MagicalTechKey.JustPressed)
				Player.GetModPlayer<YoyoTechPlayer>().ToggleMagicalTech();

			// Swap Yoyo opens the radial wheel. It stays open until the player
			// clicks a yoyo, presses Enter, cancels, or presses Swap again.
			var swap = YoyoTechKeybind.SwapYoyoKey;
			if (swap != null && swap.JustPressed)
				splicer.ToggleWheel(Player);
		}
	}
}
