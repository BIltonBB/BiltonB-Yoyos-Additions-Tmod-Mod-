using System;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Settings
{
	public class YoyoTechKeybind : ModSystem
	{
		public static ModKeybind CycleTechKey { get; private set; }
		/// <summary>One-key ON/OFF for the Magical yoyo tech specifically, so
		/// the player never has to cycle through the other techs to silence it.</summary>
		public static ModKeybind MagicalTechKey { get; private set; }
		public static ModKeybind SwapYoyoKey { get; private set; }
		/// <summary>Optional emergency swap. Unbound by default so it never
		/// steals a key from another mod; assign it in Controls.</summary>
		public static ModKeybind PanicYoyoKey { get; private set; }
		/// <summary>Optional instant next/previous carried yoyo, without the
		/// wheel. Unbound by default.</summary>
		public static ModKeybind QuickCycleYoyoKey { get; private set; }
		/// <summary>Optional one-key advance through the active board's
		/// curated splice sequence. Unbound by default.</summary>
		public static ModKeybind SpliceSequenceKey { get; private set; }
		/// <summary>Optional Dowsing String sonar pulse (Tier 1+). Unbound by
		/// default so it never competes with another mod's controls.</summary>
		public static ModKeybind DowsingSonarKey { get; private set; }

		/// <summary>The player's actual Swap Yoyo key, ready for tooltips and
		/// wheel hints. Never hard-code "C" in UI copy.</summary>
		public static string SwapKeyLabel
		{
			get {
				try {
					var keys = SwapYoyoKey?.GetAssignedKeys();
					if (keys == null || keys.Count == 0)
						return "Swap Yoyo key";
					return FriendlyKey(keys[0]);
				}
				catch {
					return "Swap Yoyo key";
				}
			}
		}

		/// <summary>True when a printable character is bound to Swap Yoyo. The
		/// Splicer search field deliberately refuses that character so pressing
		/// the hotkey always closes the wheel instead of silently appending a
		/// letter to the query first.</summary>
		public static bool IsSwapCharacter(char c) => IsBoundCharacter(SwapYoyoKey, c);

		/// <summary>Panic Equip is also a modal hotkey while the wheel is open,
		/// so its printable character must not leak into the search query.</summary>
		public static bool IsPanicCharacter(char c) => IsBoundCharacter(PanicYoyoKey, c);

		private static bool IsBoundCharacter(ModKeybind keybind, char c)
		{
			if (char.IsControl(c))
				return false;
			try {
				var keys = keybind?.GetAssignedKeys();
				if (keys == null)
					return false;
				foreach (string raw in keys) {
					string key = raw?.Trim() ?? "";
					if (key.Length == 1
						&& char.ToUpperInvariant(key[0]) == char.ToUpperInvariant(c))
						return true;
				}
			}
			catch { }
			return false;
		}

		public static string FriendlyKey(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				return "?";
			string k = key.Trim();
			if (k.Equals("Mouse1", StringComparison.OrdinalIgnoreCase)
				|| k.Equals("Left Mouse", StringComparison.OrdinalIgnoreCase)
				|| k.Equals("Mouse Left", StringComparison.OrdinalIgnoreCase))
				return "LMB";
			if (k.Equals("Mouse2", StringComparison.OrdinalIgnoreCase)
				|| k.Equals("Right Mouse", StringComparison.OrdinalIgnoreCase)
				|| k.Equals("Mouse Right", StringComparison.OrdinalIgnoreCase))
				return "RMB";
			if (k.Equals("Mouse3", StringComparison.OrdinalIgnoreCase)
				|| k.Equals("Middle Mouse", StringComparison.OrdinalIgnoreCase))
				return "MMB";
			return k;
		}

		public override void Load()
		{
			CycleTechKey = KeybindLoader.RegisterKeybind(Mod, "Cycle Yoyo Tech", "X");
			MagicalTechKey = KeybindLoader.RegisterKeybind(Mod, "Toggle Magical Yoyo Tech", "V");
			SwapYoyoKey = KeybindLoader.RegisterKeybind(Mod, "Swap Yoyo", "C");
			PanicYoyoKey = KeybindLoader.RegisterKeybind(Mod, "Panic Equip Yoyo",
				Microsoft.Xna.Framework.Input.Keys.None);
			QuickCycleYoyoKey = KeybindLoader.RegisterKeybind(Mod, "Quick Cycle Yoyo",
				Microsoft.Xna.Framework.Input.Keys.None);
			SpliceSequenceKey = KeybindLoader.RegisterKeybind(Mod, "Advance Splice Sequence",
				Microsoft.Xna.Framework.Input.Keys.None);
			DowsingSonarKey = KeybindLoader.RegisterKeybind(Mod, "Dowsing Sonar Ping",
				Microsoft.Xna.Framework.Input.Keys.None);
		}

		public override void Unload()
		{
			CycleTechKey = null;
			MagicalTechKey = null;
			SwapYoyoKey = null;
			PanicYoyoKey = null;
			QuickCycleYoyoKey = null;
			SpliceSequenceKey = null;
			DowsingSonarKey = null;
		}
	}
}
