using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Players
{
	// Tracks which Yoyo Master help stages the player has already viewed,
	// so unseen stages can show a yellow "?" marker.
	public class YoyoGuidePlayer : ModPlayer
	{
		public HashSet<int> SeenStages = new();

		public void MarkStageSeen(int stage) => SeenStages.Add(stage);

		public bool StageSeen(int stage) => SeenStages.Contains(stage);

		public override void SaveData(TagCompound tag)
		{
			tag["seenStages"] = new List<int>(SeenStages);
		}

		public override void LoadData(TagCompound tag)
		{
			SeenStages.Clear();
			if (!tag.ContainsKey("seenStages"))
				return;
			foreach (int stage in tag.Get<List<int>>("seenStages"))
				SeenStages.Add(stage);
		}
	}
}
