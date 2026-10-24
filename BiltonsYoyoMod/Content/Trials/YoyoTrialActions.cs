using Terraria;

namespace BiltonsYoyoMod.Content.Trials
{
	// Trial lore table: names, boss gates and family keys. The trials themselves
	// now run as UI attunement games inside the station panel (see
	// Minigames.StationMinigames); nothing spawns in the world anymore.
	public static class YoyoTrialActions
	{
		public static string TrialName(int trial) => trial switch {
			0 => "Broken Trajectory Engine",
			1 => "Pendulum Paradox",
			2 => "Mirror Duel",
			3 => "Clockwork Catastrophe",
			4 => "The Impossible Circuit",
			5 => "Worldfold Engine",
			_ => "Unknown Trial",
		};

		public static string TrialUnlockKey(int trial) => trial switch {
			0 => "tuning",
			1 => "calib",
			2 => "mastery",
			3 => "exttuning",
			4 => "extcalib",
			5 => "true",
			_ => "",
		};

		public static bool TrialRequirementMet(Player player, int trial)
		{
			return trial switch {
				0 => NPC.downedBoss1,
				1 => NPC.downedBoss3,
				2 => Main.hardMode,
				3 => Main.hardMode, // Wall of Flesh
				4 => NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3,
				5 => NPC.downedPlantBoss,
				_ => false,
			};
		}

		public static string TrialRequirementText(int trial) => trial switch {
			0 => "Defeat the Eye of Cthulhu",
			1 => "Defeat Skeletron",
			2 => "Defeat the Wall of Flesh",
			3 => "Defeat the Wall of Flesh",
			4 => "Defeat a mechanical boss",
			5 => "Defeat Plantera",
			_ => "",
		};
	}
}
