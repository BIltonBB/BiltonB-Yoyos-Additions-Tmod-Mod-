using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Index
{
	/// <summary>
	/// Tracks every yoyo item type the player has ever carried. Inventory scan
	/// each tick catches every acquisition path (craft/shop/drop/chest/shimmer).
	/// Rewards are QoL-only (tabs/hints/banners), never stats - per "utility, no power".
	/// </summary>
	public class YoyoIndexPlayer : ModPlayer
	{
		public HashSet<int> Obtained = new();
		private int lastMilestone;

		public override void PostUpdate()
		{
			if (Player.dead || !Player.active)
				return;
			bool added = false;
			for (int i = 0; i < Player.inventory.Length; i++) {
				Item it = Player.inventory[i];
				if (it == null || it.IsAir)
					continue;
				// Shape check covers vanilla + modded yoyos, never Meowmere-likes.
				if (!Content.NPCs.Town.YoyoTownShared.IsYoyoShape(it)) {
					// Fallback: vanilla set (some vanilla yoyos predate the shape convention).
					bool vanillaYoyo = false;
					try { vanillaYoyo = it.type < ItemID.Count && ItemID.Sets.Yoyo[it.type]; }
					catch { }
					if (!vanillaYoyo)
						continue;
				}
				if (Obtained.Add(it.type))
					added = true;
			}
			if (added && Player.whoAmI == Main.myPlayer && !Main.dedServ)
				CheckMilestones();
		}

		public float Completion(out int obtained, out int total)
		{
			var all = YoyoIndexRegistry.AllYoyoTypes();
			total = all.Count;
			obtained = 0;
			foreach (int t in all) {
				if (Obtained.Contains(t))
					obtained++;
			}
			return total <= 0 ? 0f : obtained / (float)total;
		}

		private void CheckMilestones()
		{
			float pct = Completion(out int obtained, out int total);
			int mark = pct >= 1f ? 100 : pct >= 0.75f ? 75 : pct >= 0.5f ? 50 : pct >= 0.25f ? 25 : 0;
			if (mark > lastMilestone) {
				lastMilestone = mark;
				Main.NewText($"Yoyo Index: {obtained}/{total} ({mark}%)!", 255, 230, 150);
				SoundEngine.PlaySound(SoundID.Item35, Player.Center);
				for (int i = 0; i < 12; i++) {
					Dust d = Dust.NewDustDirect(Player.Center - new Microsoft.Xna.Framework.Vector2(8, 8), 16, 16,
						DustID.GoldCoin, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 0f), 120, default, 0.9f);
					d.noGravity = true;
				}
			}
		}

		public override void SaveData(TagCompound tag)
		{
			tag["yoyoIndex"] = new List<int>(Obtained);
			tag["yoyoIndexMark"] = lastMilestone;
		}

		public override void LoadData(TagCompound tag)
		{
			Obtained.Clear();
			if (tag.ContainsKey("yoyoIndex")) {
				foreach (int t in tag.Get<List<int>>("yoyoIndex"))
					Obtained.Add(t);
			}
			lastMilestone = tag.ContainsKey("yoyoIndexMark") ? tag.GetInt("yoyoIndexMark") : 0;
		}
	}
}
