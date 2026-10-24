using System;
using System.Collections.Generic;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Content.Guidebook
{
	/// <summary>
	/// One physical page after word wrapping and pagination. Several of these can
	/// come from a single authored <see cref="GuidePage"/> when its body is longer
	/// than the printed page.
	/// </summary>
	internal sealed class GuidebookDisplayPage
	{
		public string Title = "";
		public readonly List<string> Lines = new();
		public int[] Icons = Array.Empty<int>();
		public bool Continued;
		public int SourceIndex;
		public bool Divider;
		public string Overline = "";
	}

	/// <summary>
	/// Typesets the authored guidebook into fixed-size pages. The authored lines
	/// were hand-wrapped for a wide catalogue column, so drawing them verbatim in
	/// the narrower book page spilled text over the page edge (and any page with
	/// more lines than fit was silently cut off). This re-wraps each line to the
	/// measured column width and splits the result across as many pages as needed.
	/// </summary>
	internal static class YoyoGuidebookLayout
	{
		public static List<GuidebookDisplayPage> Build(List<GuidePage> source, float width, float scale,
			int linesPerPage, int linesPerPageWithIcons)
		{
			var pages = new List<GuidebookDisplayPage>();
			if (source == null || source.Count == 0)
				return pages;

			float safeWidth = Math.Max(32f, width);
			int plainCapacity = Math.Max(1, linesPerPage);
			int iconCapacity = Math.Max(1, linesPerPageWithIcons);

			for (int p = 0; p < source.Count; p++)
			{
				GuidePage raw = source[p];
				var wrapped = new List<string>();
				if (raw.Lines != null)
				{
					foreach (string line in MergeContinuations(raw.Lines))
					{
						if (string.IsNullOrEmpty(line))
						{
							wrapped.Add("");
							continue;
						}

						WrapLine(line, safeWidth, scale, wrapped);
					}
				}

				if (wrapped.Count == 0)
					wrapped.Add("");

				bool hasIcons = raw.Icons != null && raw.Icons.Length > 0 && !raw.Divider;
				int capacity = raw.Divider
					? Math.Max(1, wrapped.Count)
					: hasIcons ? iconCapacity : plainCapacity;

				for (int start = 0; start < wrapped.Count; start += capacity)
				{
					int end = Math.Min(wrapped.Count, start + capacity);
					var page = new GuidebookDisplayPage
					{
						Title = start == 0 ? raw.Title : raw.Title + " (cont.)",
						Continued = start > 0,
						SourceIndex = p,
						Divider = raw.Divider,
						Overline = raw.Overline ?? "",
					};

					for (int i = start; i < end; i++)
						page.Lines.Add(wrapped[i]);

					// The slot row belongs to the end of the entry, like a plate in
					// a catalogue, not to every continuation page.
					if (hasIcons && end >= wrapped.Count)
						page.Icons = raw.Icons;

					pages.Add(page);
				}
			}

			return pages;
		}

		/// <summary>
		/// The authored pages were hard-wrapped for a wide catalogue column, so a
		/// sentence can be split over two strings with no indent on the second.
		/// Join those back into logical paragraphs (and indented continuations of
		/// bullets) before re-wrapping; otherwise the book inherits ragged breaks
		/// in the middle of sentences. A new entry (capital letter, bullet, number)
		/// and a blank line both start fresh.
		/// </summary>
		private static List<string> MergeContinuations(string[] lines)
		{
			var merged = new List<string>();
			foreach (string rawLine in lines)
			{
				if (rawLine == null)
					continue;

				string line = rawLine.TrimEnd();
				if (line.Length == 0)
				{
					merged.Add("");
					continue;
				}

				bool indented = line.Length > 0 && (line[0] == ' ' || line[0] == '\t');
				string trimmed = line.TrimStart();
				bool startsLower = trimmed.Length > 0 && char.IsLower(trimmed[0]);
				bool lastIsEntry = merged.Count > 0
					&& merged[merged.Count - 1].Length > 0
					&& !EndsSentence(merged[merged.Count - 1]);

				if (merged.Count > 0 && merged[merged.Count - 1].Length > 0 && (indented || (startsLower && lastIsEntry)))
				{
					merged[merged.Count - 1] = merged[merged.Count - 1].TrimEnd() + " " + trimmed;
					continue;
				}

				merged.Add(trimmed);
			}

			return merged;
		}

		private static bool EndsSentence(string text)
		{
			if (string.IsNullOrEmpty(text))
				return false;
			char last = text[text.Length - 1];
			return last == '.' || last == '!' || last == '?' || last == ')'
				|| last == ']' || last == '"';
		}

		private static void WrapLine(string line, float width, float scale, List<string> output)
		{
			// Hanging indent: bullets and numbered steps keep their text aligned
			// under the first word instead of under the marker.
			string prefix = "";
			string body = line;
			if (line.StartsWith("- "))
			{
				prefix = "- ";
				body = line.Substring(2);
			}
			else
			{
				int digits = 0;
				while (digits < 2 && digits < line.Length && char.IsDigit(line[digits]))
					digits++;
				if (digits > 0 && digits + 1 < line.Length && line[digits] == '.' && line[digits + 1] == ' ')
				{
					prefix = line.Substring(0, digits + 2);
					body = line.Substring(digits + 2);
				}
			}

			float available = Math.Max(16f, width - Measure(prefix, scale));
			var bodyLines = new List<string>();
			WrapBody(body, available, scale, bodyLines);

			string hanging = new string(' ', prefix.Length);
			for (int i = 0; i < bodyLines.Count; i++)
				output.Add((i == 0 ? prefix : hanging) + bodyLines[i]);
		}

		private static void WrapBody(string line, float width, float scale, List<string> output)
		{
			string current = null;
			foreach (string word in line.Split(' '))
			{
				if (word.Length == 0)
					continue;

				string candidate = current == null ? word : current + " " + word;
				if (Measure(candidate, scale) <= width)
				{
					current = candidate;
					continue;
				}

				if (current != null)
				{
					output.Add(current);
					current = null;
				}

				// A single word wider than the column (a URL, a long item name)
				// has to be broken by character; everything else wraps on spaces.
				string rest = word;
				while (Measure(rest, scale) > width && rest.Length > 1)
				{
					int cut = rest.Length - 1;
					while (cut > 1 && Measure(rest.Substring(0, cut), scale) > width)
						cut--;

					output.Add(rest.Substring(0, cut));
					rest = rest.Substring(cut);
				}

				current = rest;
			}

			if (current != null)
				output.Add(current);
		}

		private static float Measure(string text, float scale)
		{
			if (string.IsNullOrEmpty(text))
				return 0f;
			return FontAssets.MouseText.Value.MeasureString(text).X * scale;
		}
	}
}
