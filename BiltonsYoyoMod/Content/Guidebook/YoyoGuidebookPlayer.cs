using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Guidebook
{
	/// <summary>
	/// Per-character reading state for the guidebook. Bookmarks are keyed by
	/// volume and authored page (not display page) so they survive re-wrapping
	/// and window resizes.
	/// </summary>
	public class YoyoGuidebookPlayer : ModPlayer
	{
		public readonly HashSet<string> Bookmarks = new();

		/// <summary>Last display page reached in each volume. It is a convenience
		/// marker, not a bookmark: opening a volume still shows its cover with a
		/// Resume control.</summary>
		public readonly Dictionary<string, int> LastPages = new();

		/// <summary>Volumes whose last visit used the two-page spread.</summary>
		public readonly HashSet<string> LastSpread = new();

		public static string Key(string book, int sourceIndex) => book + "|" + sourceIndex;

		public int RememberedPage(string book)
			=> !string.IsNullOrEmpty(book) && LastPages.TryGetValue(book, out int page) ? page : -1;

		public bool RememberedSpread(string book)
			=> !string.IsNullOrEmpty(book) && LastSpread.Contains(book);

		public void RememberPage(string book, int page, bool spread)
		{
			if (string.IsNullOrEmpty(book) || page < 0)
				return;
			LastPages[book] = page;
			if (spread)
				LastSpread.Add(book);
			else
				LastSpread.Remove(book);
		}

		public bool HasBookmark(string book, int sourceIndex)
			=> Bookmarks.Contains(Key(book, sourceIndex));

		/// <summary>Adds or removes a ribbon. Returns true if it is now set.</summary>
		public bool ToggleBookmark(string book, int sourceIndex)
		{
			string key = Key(book, sourceIndex);
			if (Bookmarks.Remove(key))
				return false;
			Bookmarks.Add(key);
			return true;
		}

		public override void SaveData(TagCompound tag)
		{
			if (Bookmarks.Count > 0)
				tag["guidebookBookmarks"] = new List<string>(Bookmarks);

			if (LastPages.Count > 0) {
				var pages = new List<string>();
				foreach (var (book, page) in LastPages) {
					if (!string.IsNullOrEmpty(book) && page >= 0)
						pages.Add(book + "|" + page);
				}
				if (pages.Count > 0)
					tag["guidebookLastPages"] = pages;
			}
			if (LastSpread.Count > 0)
				tag["guidebookLastSpread"] = new List<string>(LastSpread);
		}

		public override void LoadData(TagCompound tag)
		{
			Bookmarks.Clear();
			LastPages.Clear();
			LastSpread.Clear();

			if (tag.ContainsKey("guidebookBookmarks")) {
				foreach (string entry in tag.Get<List<string>>("guidebookBookmarks")) {
					if (!string.IsNullOrEmpty(entry))
						Bookmarks.Add(entry);
				}
			}

			if (tag.ContainsKey("guidebookLastPages")) {
				foreach (string entry in tag.Get<List<string>>("guidebookLastPages")) {
					if (string.IsNullOrEmpty(entry))
						continue;
					int bar = entry.LastIndexOf('|');
					if (bar <= 0 || !int.TryParse(entry.Substring(bar + 1), out int page) || page < 0)
						continue;
					LastPages[entry.Substring(0, bar)] = page;
				}
			}

			if (tag.ContainsKey("guidebookLastSpread")) {
				foreach (string entry in tag.Get<List<string>>("guidebookLastSpread")) {
					if (!string.IsNullOrEmpty(entry))
						LastSpread.Add(entry);
				}
			}
		}
	}
}
