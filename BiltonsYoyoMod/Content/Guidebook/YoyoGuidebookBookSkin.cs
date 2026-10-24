using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using BiltonsYoyoMod.UI;

namespace BiltonsYoyoMod.Content.Guidebook
{
	/// <summary>
	/// The "Book" skin for the guidebook: a bound leather tome with a cream paper
	/// spread, a recessed gutter, brass corner protectors, stacked page edges and a
	/// ribbon marker - drawn entirely with primitives so the mod needs no new art.
	///
	/// Typography uses one clean face: <see cref="FontAssets.MouseText"/> at a
	/// measured body size for prose and at a larger fitted size for titles.
	/// Vanilla's distressed death face was too heavy to read at page scale, and
	/// <c>Utils.DrawBorderString</c> adds a two-pixel outline that swallows small
	/// UI type - so book text is drawn with plain <c>DrawString</c> and a soft
	/// paper shadow only where it helps the cover.
	/// </summary>
	internal static class YoyoGuidebookBookSkin
	{
		public static readonly Color Paper = new Color(233, 225, 204);
		public static readonly Color PaperShade = new Color(209, 200, 175);
		public static readonly Color PaperEdge = new Color(184, 171, 140);
		public static readonly Color Ink = new Color(48, 35, 24);
		public static readonly Color InkDim = new Color(104, 86, 64);
		public static readonly Color Cover = new Color(95, 58, 37);
		public static readonly Color CoverDark = new Color(49, 29, 18);
		public static readonly Color CoverLight = new Color(126, 81, 51);
		public static readonly Color Gold = new Color(222, 182, 102);
		public static readonly Color GoldDim = new Color(146, 112, 54);
		public static readonly Color Ribbon = new Color(150, 44, 52);

		private static void Fill(SpriteBatch sb, Rectangle rect, Color color)
		{
			if (rect.Width <= 0 || rect.Height <= 0)
				return;
			sb.Draw(TextureAssets.MagicPixel.Value, rect, new Rectangle(0, 0, 1, 1), color);
		}

		// ------------------------------------------------------------------ type

		/// <summary>
		/// The clean UI face used for every printed heading. The old code used the
		/// distressed death-message font here; its ragged, heavy strokes turned
		/// short titles into ink blots at page size.
		/// </summary>
		private static DynamicSpriteFont TitleFont
		{
			get
			{
				try { return FontAssets.MouseText.Value; }
				catch { return FontAssets.ItemStack.Value; }
			}
		}

		/// <summary>
		/// Draws a title centred on <paramref name="center"/>, scaled to fit inside
		/// the given box. Used for cover titles and page headings.
		/// </summary>
		public static void DrawSerifCentered(SpriteBatch sb, string text, Vector2 center, float maxWidth,
			float maxHeight, Color color, Color? shadow = null, Color? highlight = null)
		{
			if (string.IsNullOrEmpty(text))
				return;

			DynamicSpriteFont font = TitleFont;
			Vector2 size = font.MeasureString(text);
			if (size.X <= 0f || size.Y <= 0f)
				return;

			float scale = Math.Min(maxHeight / size.Y, maxWidth / size.X);
			scale = MathHelper.Clamp(scale, 0.12f, 1.5f);
			Vector2 origin = size * 0.5f;
			Vector2 pos = center - origin * scale;

			// Raised foil: a bright edge above, a soft cast below, then the face.
			if (highlight.HasValue)
				sb.DrawString(font, text, pos + new Vector2(-1f, -1f), highlight.Value, 0f, Vector2.Zero,
					scale, SpriteEffects.None, 0f);
			if (shadow.HasValue)
				sb.DrawString(font, text, pos + new Vector2(2f, 2f), shadow.Value, 0f, Vector2.Zero,
					scale, SpriteEffects.None, 0f);

			sb.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		/// <summary>Width of a string drawn with extra letter spacing (small caps look).</summary>
		public static float TrackWidth(string text, float scale, float tracking)
		{
			if (string.IsNullOrEmpty(text))
				return 0f;

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			float width = 0f;
			for (int i = 0; i < text.Length; i++)
			{
				width += font.MeasureString(text[i].ToString()).X * scale;
				if (i < text.Length - 1)
					width += tracking;
			}
			return width;
		}

		/// <summary>Draws a line with manual letter spacing. Small caps headers read as print.</summary>
		public static void DrawTracked(SpriteBatch sb, string text, Vector2 pos, Color color, float scale, float tracking)
		{
			if (string.IsNullOrEmpty(text))
				return;

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			float x = pos.X;
			for (int i = 0; i < text.Length; i++)
			{
				string glyph = text[i].ToString();
				sb.DrawString(font, glyph, new Vector2(x, pos.Y), color, 0f, Vector2.Zero,
					scale, SpriteEffects.None, 0f);
				x += font.MeasureString(glyph).X * scale + tracking;
			}
		}

		private static void DrawDisc(SpriteBatch sb, int cx, int cy, int radius, Color color)
		{
			for (int dy = -radius; dy <= radius; dy++)
			{
				int w = (int)Math.Sqrt(radius * radius - dy * dy);
				Fill(sb, new Rectangle(cx - w, cy + dy, w * 2 + 1, 1), color);
			}
		}

		private static void DrawRing(SpriteBatch sb, int cx, int cy, int radius, int thickness, Color color)
		{
			for (int dy = -radius; dy <= radius; dy++)
			{
				int outer = (int)Math.Sqrt(radius * radius - dy * dy);
				int innerR = radius - thickness;
				int inner = Math.Abs(dy) < innerR ? (int)Math.Sqrt(innerR * innerR - dy * dy) : 0;
				if (outer - inner <= 0)
					continue;
				Fill(sb, new Rectangle(cx - outer, cy + dy, outer - inner, 1), color);
				Fill(sb, new Rectangle(cx + inner, cy + dy, outer - inner, 1), color);
			}
		}

		private static void DrawDiamond(SpriteBatch sb, int cx, int cy, int size, Color color)
		{
			for (int dy = -size; dy <= size; dy++)
			{
				int w = size - Math.Abs(dy);
				Fill(sb, new Rectangle(cx - w, cy + dy, w * 2 + 1, 1), color);
			}
		}

		/// <summary>A little brass magnifier for the search field, drawn without art.</summary>
		public static void DrawSearchGlyph(SpriteBatch sb, Vector2 center, float radius, Color color)
		{
			int cx = (int)center.X;
			int cy = (int)center.Y;
			int r = Math.Max(2, (int)radius);
			DrawRing(sb, cx, cy, r, 1, color);
			for (int i = 0; i < 4; i++)
				Fill(sb, new Rectangle(cx + r + i - 1, cy + r + i - 1, 2, 2), color);
			Fill(sb, new Rectangle(cx - r + 2, cy - r + 2, 1, 1), new Color(color.R, color.G, color.B, (byte)(color.A / 2)));
		}

		/// <summary>Cheap layered drop shadow, so the book sits above the world.</summary>
		public static void DrawShadow(SpriteBatch sb, Rectangle r, float alpha)
		{
			for (int i = 3; i >= 0; i--)
			{
				int spread = i * 4;
				byte a = (byte)(30 + (3 - i) * 16);
				Fill(sb, new Rectangle(r.X + 8 - spread, r.Y + 10 - spread,
					r.Width + spread * 2, r.Height + spread * 2), new Color(0, 0, 0, a) * alpha);
			}
		}

		// ---------------------------------------------------------------- spread

		/// <summary>Open spread: boards, page block, two paper pages and the gutter.</summary>
		public static void DrawSpread(SpriteBatch sb, Rectangle r, float alpha, Color accent)
		{
			if (r.Width < 80 || r.Height < 80)
				return;

			// The book has real thickness: a dark underside sits slightly down
			// and to the right of the boards before the cover face begins.
			Fill(sb, new Rectangle(r.X + 7, r.Y + 8, r.Width, r.Height),
				new Color(24, 14, 8, 190) * alpha);

			// Leather boards: a lit bevel, the board face, then a dark lip around
			// the paper block so the pages look sunk into the cover.
			Fill(sb, r, CoverDark * alpha);
			Fill(sb, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4), Cover * alpha);
			Fill(sb, new Rectangle(r.X + 5, r.Y + 5, r.Width - 10, r.Height - 10), CoverLight * alpha);
			Fill(sb, new Rectangle(r.X + 8, r.Y + 8, r.Width - 16, r.Height - 16), new Color(76, 47, 29) * alpha);

			// Page block, offset to the right and bottom so the fore-edge shows.
			Rectangle block = new(r.X + 13, r.Y + 11, r.Width - 26, r.Height - 21);
			Fill(sb, new Rectangle(block.X + 2, block.Y + 2, block.Width + 2, block.Height + 2), PaperEdge * alpha);
			Fill(sb, block, PaperShade * alpha);

			// Stacked sheet edges.
			for (int i = 0; i < 4; i++)
			{
				byte a = (byte)(105 - i * 20);
				Color line = new Color(156, 141, 110, a) * alpha;
				Fill(sb, new Rectangle(block.Right - 1 - i * 2, block.Y + 3, 1, block.Height - 6), line);
				Fill(sb, new Rectangle(block.X + 3, block.Bottom - 1 - i * 2, block.Width - 6, 1), line);
			}

			int gutter = block.X + block.Width / 2;
			Rectangle left = new(block.X + 3, block.Y + 3, gutter - block.X - 7, block.Height - 6);
			Rectangle right = new(gutter + 4, block.Y + 3, block.Right - 4 - (gutter + 4), block.Height - 6);
			if (left.Width <= 12 || right.Width <= 12)
				return;

			Fill(sb, left, Paper * alpha);
			Fill(sb, right, Paper * alpha);

			// Round the outside corners of the page block by carving the board
			// colour back over stepped corner pixels. The paper now reads as a
			// bound block instead of two square cream fields.
			CutPageCorner(sb, block.X, block.Y, 1, 1, 8, alpha);
			CutPageCorner(sb, block.Right - 1, block.Y, -1, 1, 8, alpha);
			CutPageCorner(sb, block.X, block.Bottom - 1, 1, -1, 8, alpha);
			CutPageCorner(sb, block.Right - 1, block.Bottom - 1, -1, -1, 8, alpha);

			// Gilded page edges: a warm hairline where the paper block meets the
			// leather, with small glints at the corners.
			Color gild = new Color(201, 168, 99, 92) * alpha;
			Fill(sb, new Rectangle(block.Right - 1, block.Y + 9, 1, block.Height - 18), gild);
			Fill(sb, new Rectangle(block.X + 9, block.Bottom - 1, block.Width - 18, 1), gild);
			Fill(sb, new Rectangle(block.Right - 3, block.Y + 13, 1, 26), Gold * (0.18f * alpha));
			Fill(sb, new Rectangle(block.X + 16, block.Bottom - 3, 34, 1), Gold * (0.16f * alpha));

			// Paper fibre: deterministic speckle plus faint laid lines. This runs
			// once into the baked skin target, so it can be dense enough to read
			// as paper rather than as a flat cream rectangle.
			for (int i = 0; i < 620; i++)
			{
				int hx = block.X + 6 + (i * 7919) % Math.Max(1, block.Width - 12);
				int hy = block.Y + 6 + (i * 104729) % Math.Max(1, block.Height - 12);
				if (!left.Contains(hx, hy) && !right.Contains(hx, hy))
					continue;
				byte a = (byte)(16 + (i * 37) % 26);
				Fill(sb, new Rectangle(hx, hy, 1, 1), new Color(150, 132, 100, a) * alpha);
				if (i % 7 == 0)
					Fill(sb, new Rectangle(hx, hy, 2, 1), new Color(150, 132, 100, (byte)(a / 2)) * alpha);
			}

			for (int i = 0; i < 24; i++)
			{
				Rectangle sheet = i % 2 == 0 ? left : right;
				int fy = sheet.Y + 24 + (i * 6151) % Math.Max(1, sheet.Height - 48);
				int fx = sheet.X + 18 + (i * 3571) % 150;
				Fill(sb, new Rectangle(fx, fy, 56 + (i * 23) % 90, 1),
					new Color(166, 148, 116, 12) * alpha);
			}

			// Real paper grain, tiled at native crispness. It is baked into the
			// skin once, so this can be dense without costing a frame.
			DrawPaperGrain(sb, left, alpha);
			DrawPaperGrain(sb, right, alpha);

			// A soft lamp sheen across the upper left of each page. This is the
			// difference between flat cream rectangles and lit leaves.
			UiArt.DrawGlow(sb, new Vector2(left.X + left.Width * 0.30f, left.Y + left.Height * 0.20f),
				left.Width * 0.60f, new Color(255, 244, 208) * (0.035f * alpha));
			UiArt.DrawGlow(sb, new Vector2(right.X + right.Width * 0.34f, right.Y + right.Height * 0.18f),
				right.Width * 0.58f, new Color(255, 244, 208) * (0.030f * alpha));

			// Page-edge vignette: keeps text from butting into the cut edge.
			for (int i = 0; i < 6; i++)
			{
				byte a = (byte)(18 - i * 3);
				Color shade = new Color(140, 120, 88, a) * alpha;
				Fill(sb, new Rectangle(left.X + i, left.Y + 2, 1, left.Height - 4), shade);
				Fill(sb, new Rectangle(right.Right - 1 - i, right.Y + 2, 1, right.Height - 4), shade);
				Fill(sb, new Rectangle(right.X + i, right.Y + 2, 1, right.Height - 4), shade);
				Fill(sb, new Rectangle(left.Right - 1 - i, left.Y + 2, 1, left.Height - 4), shade);
			}

			// Curvature shading either side of the gutter.
			for (int i = 0; i < 10; i++)
			{
				byte a = (byte)(46 * (1f - i / 10f));
				Color shade = new Color(120, 102, 74, a) * alpha;
				Fill(sb, new Rectangle(gutter - 6 - i, left.Y + 1, 1, left.Height - 2), shade);
				Fill(sb, new Rectangle(gutter + 5 + i, right.Y + 1, 1, right.Height - 2), shade);
			}

			// Visible spine: a narrow leather strip in the gutter with a rounded
			// highlight, raised bands and cloth headbands at head and tail. The
			// pages still curl into it via the shading above.
			const int spineW = 14;
			int spineX = gutter - spineW / 2;
			Rectangle spine = new(spineX, block.Y + 2, spineW, block.Height - 4);
			Fill(sb, spine, new Color(66, 41, 27) * alpha);
			for (int i = 0; i < spineW; i++) {
				float k = i / (float)(spineW - 1);
				float sheen = 0.5f + 0.5f * (float)Math.Cos((k - 0.42f) * Math.PI * 1.4f);
				byte a = (byte)(36 + 34 * sheen);
				Fill(sb, new Rectangle(spine.X + i, spine.Y, 1, spine.Height),
					new Color(24, 13, 8, a) * alpha);
			}
			Fill(sb, new Rectangle(spine.X + 1, spine.Y, 1, spine.Height),
				new Color(150, 102, 63, 48) * alpha);
			Fill(sb, new Rectangle(spine.Right - 2, spine.Y, 1, spine.Height),
				new Color(22, 12, 7, 120) * alpha);
			// Raised bands: two dark ridges with brass hairlines.
			for (int b = 0; b < 2; b++) {
				int by = spine.Y + (int)(spine.Height * (b == 0 ? 0.20f : 0.78f));
				Fill(sb, new Rectangle(spine.X - 1, by, spine.Width + 2, 5), new Color(42, 25, 15) * alpha);
				Fill(sb, new Rectangle(spine.X - 1, by, spine.Width + 2, 1), GoldDim * (0.7f * alpha));
				Fill(sb, new Rectangle(spine.X - 1, by + 4, spine.Width + 2, 1), new Color(20, 11, 6, 120) * alpha);
			}
			// Headbands: tiny woven cloth bars at the top and bottom of the spine.
			for (int end = 0; end < 2; end++) {
				int hy = end == 0 ? spine.Y : spine.Bottom - 7;
				for (int i = 0; i < spine.Width; i++) {
					Color band = i % 2 == 0
						? Color.Lerp(accent, Gold, 0.35f)
						: new Color(64, 40, 26);
					Fill(sb, new Rectangle(spine.X + i, hy, 1, 6), band * (0.9f * alpha));
				}
				Fill(sb, new Rectangle(spine.X, hy + (end == 0 ? 6 : -1), spine.Width, 1),
					GoldDim * (0.8f * alpha));
			}
			// Fine brass rules down the spine edges.
			Fill(sb, new Rectangle(spine.X - 2, spine.Y + 8, 1, spine.Height - 16),
				GoldDim * (0.45f * alpha));
			Fill(sb, new Rectangle(spine.Right + 1, spine.Y + 8, 1, spine.Height - 16),
				GoldDim * (0.38f * alpha));
			// Deep gutter shadow just outside the spine so paper curves into it.
			for (int i = 0; i < 8; i++) {
				byte a = (byte)(38 * (1f - i / 8f));
				Fill(sb, new Rectangle(spine.X - 3 - i, left.Y + 1, 1, left.Height - 2),
					new Color(74, 56, 38, a) * alpha);
				Fill(sb, new Rectangle(spine.Right + 2 + i, right.Y + 1, 1, right.Height - 2),
					new Color(74, 56, 38, a) * alpha);
			}

			// Ribbon marker: a short tail peeking out of the bottom of the gutter,
			// not a coloured stripe running the whole height of the spread.
			Rectangle ribbon = new(gutter - 3, block.Bottom - 52, 6, 52 + 10);
			Color ribbonColor = Color.Lerp(Ribbon, accent, 0.2f) * alpha;
			Fill(sb, ribbon, ribbonColor);
			Fill(sb, new Rectangle(ribbon.X, ribbon.Y, 1, ribbon.Height), new Color(0, 0, 0, 70) * alpha);
			Fill(sb, new Rectangle(ribbon.Right - 1, ribbon.Y, 1, ribbon.Height), new Color(255, 240, 210, 40) * alpha);
			// Stepped tail so the ribbon tapers to a point instead of ending square.
			Fill(sb, new Rectangle(ribbon.X + 1, ribbon.Bottom - 8, 4, 4), ribbonColor);
			Fill(sb, new Rectangle(ribbon.X + 2, ribbon.Bottom - 4, 2, 4), ribbonColor);

			// Brass corner protectors on the boards, not on the paper.
			DrawBrassCorner(sb, r.X + 6, r.Y + 6, 1, 1, alpha);
			DrawBrassCorner(sb, r.Right - 6, r.Y + 6, -1, 1, alpha);
			DrawBrassCorner(sb, r.X + 6, r.Bottom - 6, 1, -1, alpha);
			DrawBrassCorner(sb, r.Right - 6, r.Bottom - 6, -1, -1, alpha);
		}

		private static void CutPageCorner(SpriteBatch sb, int x, int y, int dirX, int dirY,
			int radius, float alpha)
		{
			Color board = new Color(76, 47, 29);
			for (int i = 0; i < radius; i++) {
				int w = radius - i;
				int px = dirX > 0 ? x : x - w + 1;
				int py = dirY > 0 ? y + i : y - i;
				Fill(sb, new Rectangle(px, py, w, 1), board * alpha);
			}
		}

		private static void DrawPaperGrain(SpriteBatch sb, Rectangle page, float alpha)
		{
			Texture2D grain = UiArt.Grain;
			if (grain == null || page.Width <= 0 || page.Height <= 0)
				return;
			Color tint = new Color(126, 106, 74, 82) * alpha;
			for (int ty = page.Y; ty < page.Bottom; ty += grain.Height) {
				for (int tx = page.X; tx < page.Right; tx += grain.Width) {
					Rectangle dest = Rectangle.Intersect(
						new Rectangle(tx, ty, grain.Width, grain.Height), page);
					if (dest.Width <= 0 || dest.Height <= 0)
						continue;
					Rectangle src = new Rectangle(0, 0, dest.Width, dest.Height);
					sb.Draw(grain, dest, src, tint);
				}
			}
		}

		private static void DrawBrassCorner(SpriteBatch sb, int x, int y, int dirX, int dirY, float alpha)
		{
			const int len = 26;
			const int thick = 3;

			int ox = dirX > 0 ? x : x - len;
			int oy = dirY > 0 ? y : y - len;
			int hy = dirY > 0 ? y : y - thick;
			int vx = dirX > 0 ? x : x - thick;

			Fill(sb, new Rectangle(ox, hy, len, thick), GoldDim * alpha);
			Fill(sb, new Rectangle(vx, oy, thick, len), GoldDim * alpha);

			int ox2 = dirX > 0 ? x + 1 : x - len + 4;
			int oy2 = dirY > 0 ? y + 1 : y - len + 4;
			int hy2 = dirY > 0 ? y + 1 : y - thick + 1;
			int vx2 = dirX > 0 ? x + 1 : x - thick + 1;
			Fill(sb, new Rectangle(ox2, hy2, len - 5, 1), Gold * alpha);
			Fill(sb, new Rectangle(vx2, oy2, 1, len - 5), Gold * alpha);
		}

		// ---------------------------------------------------------- closed cover

		/// <summary>Closed front cover: gold tooling, sigil, serif title. Drawn over the whole book rect.</summary>
		public static void DrawClosedCover(SpriteBatch sb, Rectangle r, float alpha, Color accent,
			string title, string subtitle, Texture2D sigil, bool prompt)
		{
			Fill(sb, r, CoverDark * alpha);
			Fill(sb, new Rectangle(r.X + 4, r.Y + 4, r.Width - 8, r.Height - 8), Cover * alpha);
			Fill(sb, new Rectangle(r.X + 7, r.Y + 7, r.Width - 14, r.Height - 14), new Color(116, 75, 48) * alpha);

			// Fore-edge: the closed book's page block, on the side away from the spine.
			Rectangle edge = new(r.Right - 15, r.Y + 10, 8, r.Height - 20);
			Fill(sb, edge, PaperShade * alpha);
			for (int i = 0; i < 4; i++)
			{
				Fill(sb, new Rectangle(edge.X + i * 2, edge.Y + i, 1, edge.Height - i * 2),
					new Color(156, 141, 110, 110 - i * 20) * alpha);
			}

			// Spine down the left edge with gold bands.
			const int spineW = 30;
			Fill(sb, new Rectangle(r.X + 4, r.Y + 4, spineW, r.Height - 8), new Color(46, 28, 17) * alpha);
			Fill(sb, new Rectangle(r.X + 4 + spineW, r.Y + 4, 2, r.Height - 8), new Color(22, 13, 7) * alpha);
			for (int i = 0; i < 5; i++)
			{
				int bandY = r.Y + 26 + i * (r.Height - 52) / 4;
				Fill(sb, new Rectangle(r.X + 8, bandY, spineW - 8, 3), GoldDim * alpha);
				Fill(sb, new Rectangle(r.X + 8, bandY, spineW - 8, 1), Gold * alpha);
			}

			// Vertical spine text, the way a real book is lettered.
			try
			{
				DynamicSpriteFont font = FontAssets.MouseText.Value;
				Vector2 size = font.MeasureString("BILTONB");
				float scale = Math.Min(0.72f, (r.Height - 150f) / Math.Max(1f, size.X));
				sb.DrawString(font, "BILTONB", new Vector2(r.X + 4 + spineW / 2f, r.Y + r.Height / 2f),
					Gold * alpha, MathHelper.PiOver2, size * 0.5f, scale, SpriteEffects.None, 0f);
			}
			catch { }

			// Tooled face.
			Rectangle face = new(r.X + spineW + 22, r.Y + 22, r.Width - spineW - 44, r.Height - 44);
			Fill(sb, new Rectangle(face.X + 6, face.Y + 6, face.Width - 12, face.Height - 12), new Color(88, 54, 34) * alpha);

			const int g = 2;
			Fill(sb, new Rectangle(face.X, face.Y, face.Width, g), Gold * alpha);
			Fill(sb, new Rectangle(face.X, face.Bottom - g, face.Width, g), Gold * alpha);
			Fill(sb, new Rectangle(face.X, face.Y, g, face.Height), Gold * alpha);
			Fill(sb, new Rectangle(face.Right - g, face.Y, g, face.Height), Gold * alpha);
			Fill(sb, new Rectangle(face.X + 8, face.Y + 8, face.Width - 16, 1), GoldDim * alpha);
			Fill(sb, new Rectangle(face.X + 8, face.Bottom - 9, face.Width - 16, 1), GoldDim * alpha);

			// Second hairline rule, then corner diamonds: the quiet double
			// tooling that separates a hand-bound board from a brown rectangle.
			const int ruleInset = 14;
			Rectangle inner = new(face.X + ruleInset, face.Y + ruleInset,
				face.Width - ruleInset * 2, face.Height - ruleInset * 2);
			Fill(sb, new Rectangle(inner.X, inner.Y, inner.Width, 1), GoldDim * (0.62f * alpha));
			Fill(sb, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), GoldDim * (0.62f * alpha));
			Fill(sb, new Rectangle(inner.X, inner.Y, 1, inner.Height), GoldDim * (0.62f * alpha));
			Fill(sb, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), GoldDim * (0.62f * alpha));
			DrawDiamond(sb, inner.X, inner.Y, 3, Gold * (0.8f * alpha));
			DrawDiamond(sb, inner.Right - 1, inner.Y, 3, Gold * (0.8f * alpha));
			DrawDiamond(sb, inner.X, inner.Bottom - 1, 3, Gold * (0.8f * alpha));
			DrawDiamond(sb, inner.Right - 1, inner.Bottom - 1, 3, Gold * (0.8f * alpha));

			// A soft leather sheen across the top and a shadow along the spine.
			for (int i = 0; i < 10; i++)
				Fill(sb, new Rectangle(face.X + 18, face.Y + 20 + i * 2, face.Width - 36, 1),
					new Color(255, 232, 190, (byte)(10 - i)) * alpha);
			Fill(sb, new Rectangle(face.X - 7, face.Y + 4, 5, face.Height - 8),
				new Color(22, 12, 7, 90) * alpha);
			Fill(sb, new Rectangle(face.X - 2, face.Y + 4, 2, face.Height - 8),
				new Color(150, 101, 62, 55) * alpha);

			int cx = face.X + face.Width / 2;

			// Volume line above the stamp.
			string volume = "VOLUME " + Roman(YoyoGuidebookBookIndex());
			float volW = TrackWidth(volume, 0.62f, 3f);
			DrawTracked(sb, volume, new Vector2(cx - volW / 2f, face.Y + 18), GoldDim * alpha, 0.62f, 3f);

			// Sigil stamp: double brass ring around the volume's item.
			int cy = face.Y + (int)(face.Height * 0.33f);
			DrawDisc(sb, cx, cy, 44, GoldDim * alpha);
			DrawDisc(sb, cx, cy, 41, new Color(70, 44, 28) * alpha);
			DrawRing(sb, cx, cy, 37, 2, Gold * alpha);
			DrawDisc(sb, cx, cy, 33, new Color(38, 24, 15) * alpha);
			if (sigil != null)
			{
				float size = Math.Min(1f, 46f / Math.Max(sigil.Width, sigil.Height));
				sb.Draw(sigil, new Vector2(cx, cy), null, Color.White * alpha, 0f,
					sigil.Size() * 0.5f, size, SpriteEffects.None, 0f);
			}

			// Serif title, centred in the remaining face.
			DrawSerifCentered(sb, title, new Vector2(cx, cy + 92), face.Width - 56, 58f, Gold * alpha,
				new Color(24, 13, 7, (byte)(190 * alpha)),
				new Color(255, 238, 190, (byte)(62 * alpha)));

			int ruleY = cy + 134;
			int ruleW = Math.Max(80, Math.Min(240, face.Width - 36));
			Fill(sb, new Rectangle(cx - ruleW / 2, ruleY, ruleW, 1), GoldDim * alpha);
			DrawDiamond(sb, cx, ruleY, 3, Gold * alpha);

			float subScale = 0.58f;
			while (subScale > 0.40f
				&& TrackWidth(subtitle.ToUpperInvariant(), subScale, 1.6f) > face.Width - 24f)
				subScale -= 0.02f;
			float subW = TrackWidth(subtitle.ToUpperInvariant(), subScale, 1.6f);
			DrawTracked(sb, subtitle.ToUpperInvariant(), new Vector2(cx - subW / 2f, ruleY + 16),
				GoldDim * alpha, subScale, 1.6f);

			// Bottom ornament balances the stamp and the type block.
			int ornamentY = face.Bottom - 96;
			int ornamentW = Math.Max(24, Math.Min(58, (face.Width - 40) / 2));
			Fill(sb, new Rectangle(cx - 28 - ornamentW, ornamentY, ornamentW, 1), GoldDim * alpha);
			Fill(sb, new Rectangle(cx + 28, ornamentY, ornamentW, 1), GoldDim * alpha);
			DrawDiamond(sb, cx, ornamentY, 4, Gold * alpha);

			// Brass clasp on the fore-edge, sitting over the page block.
			int claspX = r.Right - 24;
			int claspY = r.Y + r.Height / 2 - 13;
			Fill(sb, new Rectangle(claspX, claspY, 10, 26), GoldDim * alpha);
			Fill(sb, new Rectangle(claspX + 1, claspY + 1, 8, 24), new Color(146, 112, 54) * alpha);
			Fill(sb, new Rectangle(claspX + 2, claspY + 2, 6, 1), Gold * alpha);
			Fill(sb, new Rectangle(claspX + 4, claspY + 9, 3, 8), new Color(70, 46, 24) * alpha);
			Fill(sb, new Rectangle(claspX + 5, claspY + 10, 1, 6), new Color(222, 182, 102, 100) * alpha);

			// Red ribbon marker hanging from the top of the face.
			Rectangle marker = new(inner.Right - 32, inner.Y - 8, 7, 58);
			Fill(sb, marker, new Color(150, 44, 52) * (0.9f * alpha));
			Fill(sb, new Rectangle(marker.X, marker.Y, 1, marker.Height),
				new Color(66, 16, 20, 100) * alpha);
			Fill(sb, new Rectangle(marker.Right - 1, marker.Y, 1, marker.Height),
				new Color(255, 225, 200, 45) * alpha);
			Fill(sb, new Rectangle(marker.X + 1, marker.Bottom - 5, 5, 3),
				new Color(150, 44, 52) * (0.55f * alpha));

			// Wax seal with two short tails, low on the spine side.
			int sealX = inner.X + 34;
			int sealY = inner.Bottom - 34;
			Fill(sb, new Rectangle(sealX - 5, sealY + 7, 3, 15), new Color(126, 34, 42) * (0.85f * alpha));
			Fill(sb, new Rectangle(sealX + 3, sealY + 7, 3, 13), new Color(112, 30, 38) * (0.75f * alpha));
			DrawDisc(sb, sealX, sealY, 12, new Color(116, 30, 36) * alpha);
			DrawRing(sb, sealX, sealY, 9, 1, GoldDim * (0.8f * alpha));
			DrawDiamond(sb, sealX, sealY, 3, new Color(222, 182, 102, 150) * alpha);

			// Brass protectors on the two corners away from the spine.
			DrawBrassCorner(sb, r.Right - 6, r.Y + 6, -1, 1, alpha);
			DrawBrassCorner(sb, r.Right - 6, r.Bottom - 6, -1, -1, alpha);

			if (prompt && Main.GameUpdateCount % 40 < 26)
			{
				const string Text = "Click to open";
				Vector2 size = FontAssets.MouseText.Value.MeasureString(Text) * 0.78f;
				sb.DrawString(FontAssets.MouseText.Value, Text,
					new Vector2(face.X + (face.Width - size.X) / 2f, face.Bottom - 56),
					Paper * alpha, 0f, Vector2.Zero, 0.78f, SpriteEffects.None, 0f);
			}
		}

		private static int YoyoGuidebookBookIndex()
		{
			// Kept local to the skin: only used to print "VOLUME II" on the board.
			return YoyoGuidebookUISystem.BookIndex;
		}

		public static string Roman(int value)
		{
			string[] numerals = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
			if (value < 0 || value >= numerals.Length)
				return (value + 1).ToString();
			return numerals[value];
		}

		// ------------------------------------------------------------- contents

		/// <summary>One contents row on the left page: ink, gold rule when selected.</summary>
		public static void DrawContentsRow(SpriteBatch sb, Rectangle row, bool selected, bool hovered,
			float alpha, Color accent, float leaderStart, int leaderY)
		{
			if (selected)
			{
				Fill(sb, new Rectangle(row.X, row.Y, row.Width, row.Height), new Color(0, 0, 0, 22) * alpha);
				Fill(sb, new Rectangle(row.X, row.Y, 3, row.Height), Color.Lerp(Gold, accent, 0.4f) * alpha);
			}
			else if (hovered)
			{
				Fill(sb, new Rectangle(row.X, row.Y, row.Width, row.Height), new Color(255, 255, 255, 40) * alpha);
				Fill(sb, new Rectangle(row.X, row.Y, 1, row.Height), GoldDim * (0.75f * alpha));
				Fill(sb, new Rectangle(row.X + 4, row.Bottom - 1, row.Width - 8, 1),
					GoldDim * (0.34f * alpha));
			}

			// Dotted leader, on the title's baseline and only in the free space
			// after the title - not a row of dots under the text.
			if (leaderStart < row.Right - 10)
			{
				for (float x = leaderStart; x < row.Right - 8; x += 7f)
					Fill(sb, new Rectangle((int)x, leaderY, 2, 1), new Color(150, 132, 100, 120) * alpha);
			}
		}

		/// <summary>Icon slot drawn as ink-and-paper rather than a steel inventory slot.</summary>
		public static void DrawBookSlot(SpriteBatch sb, Rectangle frame, bool hovered, float alpha)
		{
			Fill(sb, frame, new Color(228, 219, 194, 200) * alpha);
			Fill(sb, new Rectangle(frame.X + 2, frame.Y + 2, frame.Width - 4, frame.Height - 4),
				new Color(244, 238, 221, 130) * alpha);

			Color edge = hovered ? Gold : GoldDim;
			Fill(sb, new Rectangle(frame.X, frame.Y, frame.Width, 1), edge * alpha);
			Fill(sb, new Rectangle(frame.X, frame.Bottom - 1, frame.Width, 1), edge * alpha);
			Fill(sb, new Rectangle(frame.X, frame.Y, 1, frame.Height), edge * alpha);
			Fill(sb, new Rectangle(frame.Right - 1, frame.Y, 1, frame.Height), edge * alpha);

			if (hovered)
				Fill(sb, new Rectangle(frame.X + 1, frame.Bottom - 3, frame.Width - 2, 2), new Color(222, 182, 102, 60) * alpha);
		}

		// ---------------------------------------------------------------- ribbon

		/// <summary>
		/// The leaf that sweeps across the reading page during a turn, t going
		/// 1 -> 0. The curl is built from thin vertical strips with a curved
		/// top and bottom edge, a visible back side after the halfway point,
		/// a rolled lip, a cast shadow, and two ghost leaves for motion. The
		/// print is already hidden under the sheet, so the text swap never
		/// shows through the curl.
		/// </summary>
		public static void DrawTurningSheet(SpriteBatch sb, Rectangle page, float t, int dir, float alpha,
			Texture2D content = null)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			float p = 1f - t;                    // 0 at start, 1 at finish
			float e = Smooth01(p);               // eased progress
			if (e >= 0.995f)
				return;

			float width = page.Width * (1f - e);
			if (width < 2f)
				return;

			// Bound at the gutter: right-page turns fold to the left, and the
			// mirror case folds to the right.
			int left = dir > 0 ? page.X : page.Right - (int)width;
			int right = left + (int)width;
			float arc = (float)Math.Sin(Math.PI * e);   // peaks mid-turn
			float lift = 27f * arc;
			float roll = 4f + 12f * arc;
			float back = Smooth01(MathHelper.Clamp((e - 0.34f) / 0.40f, 0f, 1f));

			// A broad cast shadow sweeps across the destination page. FadeH is
			// opaque at its left edge, so mirror it for a leftward turn.
			Texture2D fade = UiArt.FadeH;
			int shadowWidth = (int)(page.Width * 0.24f * (0.35f + 0.65f * arc));
			if (fade != null && shadowWidth > 2) {
				Rectangle shadow = dir > 0
					? new Rectangle(right, page.Y + 5, shadowWidth, page.Height - 11)
					: new Rectangle(left - shadowWidth, page.Y + 5, shadowWidth, page.Height - 11);
				sb.Draw(fade, shadow, null, new Color(34, 23, 13) * (0.26f * arc * alpha),
					0f, Vector2.Zero, dir > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
			}

			// Ghost leaves: a whisper of paper ahead of the curl, not a stack of
			// solid bars. Two silhouettes are enough to imply motion.
			for (int i = 2; i >= 1; i--) {
				int gx = dir > 0 ? right + i * 5 : left - i * 5;
				Fill(sb, new Rectangle(gx, page.Y + 12 - (int)(lift * 0.7f), 2,
					page.Height - 24 + (int)(lift * 0.2f)),
					new Color(242, 232, 208, (byte)(15 - i * 3)) * alpha);
			}

			// Page thickness behind the moving leaf.
			for (int i = 1; i <= 4; i++) {
				int sx = dir > 0 ? right - i * 2 : left + i * 2;
				Fill(sb, new Rectangle(sx, page.Y + 5, 1, page.Height - 10),
					PaperEdge * (0.24f * arc * alpha));
			}

			// Body: one gradient strip per 2px column. The vertical paper
			// gradient supplies the top-to-bottom shading, so there are no
			// horizontal band seams - the old sheet's "stack of bars" look.
			Texture2D gradient = UiArt.PageGradient;
			for (int x = left; x < right; x += 2) {
				float u = MathHelper.Clamp((x - left + 1f) / Math.Max(1f, width), 0f, 1f);
				float uu = u * u;
				float liftTop = lift * uu;
				float liftBot = lift * uu * 0.52f;
				int yTop = page.Y - (int)liftTop;
				int yBot = page.Bottom - (int)liftBot;
				int h = Math.Max(1, yBot - yTop);
				int w = Math.Min(2, right - x);

				float shade = 1f - 0.11f * u - 0.035f * (float)Math.Sin(u * Math.PI);
				Color front = new Color(238, 230, 208);
				Color rear = new Color(211, 200, 175);
				Color body = Color.Lerp(front, rear, back);
				body = new Color((byte)(body.R * shade), (byte)(body.G * shade),
					(byte)(body.B * shade), (byte)248);

				if (gradient != null) {
					sb.Draw(gradient, new Rectangle(x, yTop, w, h), null, body * alpha,
						0f, Vector2.Zero, SpriteEffects.None, 0f);
				}
				else {
					Fill(sb, new Rectangle(x, yTop, w, h), body * alpha);
				}

				// The old printed page, sliced and compressed with the paper.
				// It fades as the back of the leaf rotates into view.
				if (content != null && content.Width > 1 && content.Height > 1) {
					float span = Math.Max(1f, width);
					int srcX = Math.Clamp((int)((x - left) / span * content.Width), 0, content.Width - 1);
					int srcW = Math.Clamp((int)Math.Ceiling(2f / span * content.Width) + 1,
						1, content.Width - srcX);
					Rectangle src = new(srcX, 0, srcW, content.Height);
					sb.Draw(content, new Rectangle(x, yTop, w, h), src,
						Color.White * (alpha * (1f - back * 0.94f)), 0f, Vector2.Zero,
						SpriteEffects.None, 0f);
				}

				// Cut edges only on the silhouette; the old per-strip horizontal
				// seam is gone because the gradient handles interior shading.
				Fill(sb, new Rectangle(x, yTop, w, 1), PaperEdge * (0.52f * alpha));
				Fill(sb, new Rectangle(x, yBot - 1, w, 1), PaperEdge * (0.40f * alpha));

				// Sparse laid fibres, never a regular screen door.
				if ((x - left) % 23 == 0)
					Fill(sb, new Rectangle(x, yTop + 16, 1, Math.Max(1, h - 32)),
						new Color(170, 154, 122, 14) * alpha);
			}

			// Rolled lip at the free edge. Build a cylinder from 1px columns:
			// dark valley, hot glint, then a pale return. The heights follow the
			// same curved silhouette as the sheet.
			int lipW = Math.Max(2, (int)roll);
			int freeTop = page.Y - (int)(lift * 0.98f);
			int freeBot = page.Bottom - (int)(lift * 0.51f);
			for (int i = 0; i < lipW; i++) {
				float k = i / (float)Math.Max(1, lipW - 1);
				int x = dir > 0 ? right - lipW + i : left + i;
				float cylinder = (float)Math.Sin(k * Math.PI);
				int top = freeTop + (int)(2f * cylinder);
				int bot = freeBot - (int)(2f * cylinder);
				Color c = Color.Lerp(new Color(102, 82, 55), new Color(252, 246, 228), k);
				float glint = k > 0.58f && k < 0.76f ? 0.18f : 0f;
				c = new Color(
					(byte)MathHelper.Clamp(c.R + 255 * glint, 0, 255),
					(byte)MathHelper.Clamp(c.G + 246 * glint, 0, 255),
					(byte)MathHelper.Clamp(c.B + 210 * glint, 0, 255),
					(byte)226);
				Fill(sb, new Rectangle(x, top, 1, Math.Max(1, bot - top)), c * alpha);
			}

			// Bright leading edge and a soft shadow just under the lip.
			int edgeX = dir > 0 ? right - 1 : left;
			Fill(sb, new Rectangle(edgeX, freeTop + 2, 1, Math.Max(1, freeBot - freeTop - 4)),
				new Color(255, 250, 232, 110) * alpha);
			Fill(sb, new Rectangle(dir > 0 ? right : left - 1, page.Y + 8, 1, page.Height - 16),
				new Color(56, 38, 23, 80) * alpha);

			// Gutter shading: the fold is darkest right against the binding.
			int gutterX = dir > 0 ? page.X : page.Right - 1;
			for (int i = 0; i < 10; i++) {
				byte a = (byte)(44 * (1f - i / 10f));
				int gx = gutterX + (dir > 0 ? i : -i);
				Fill(sb, new Rectangle(gx, page.Y + 1, 1, page.Height - 2),
					new Color(88, 68, 44, a) * alpha);
			}
			Fill(sb, new Rectangle(gutterX, page.Y + 1, 1, page.Height - 2),
				new Color(60, 43, 28, 120) * alpha);

			// A few dust motes lift off the fold at the start and settle at the end.
			if (e < 0.82f) {
				for (int i = 0; i < 7; i++) {
					float seed = i * 0.7548777f;
					float mx = left + (seed * 7919f % 1f) * width;
					float my = page.Y + page.Height * (0.18f + 0.64f * ((seed * 3571f) % 1f));
					float drift = (float)Math.Sin(e * 4f + i) * 6f;
					Fill(sb, new Rectangle((int)(mx + drift), (int)(my - e * 22f), 1, 1),
						new Color(248, 238, 210, (byte)(46 * (1f - e))) * alpha);
				}
			}
		}

		private static float Smooth01(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t * t * (3f - 2f * t);
		}

		/// <summary>Index-ribbon tab on the fore-edge of the book (two-page / contents toggle).</summary>
		public static void DrawEdgeTab(SpriteBatch sb, Rectangle tab, string label, bool rightSide,
			float alpha, bool hover)
		{
			if (tab.Width <= 0 || tab.Height <= 0)
				return;

			Color fill = hover ? new Color(181, 60, 66) : new Color(148, 44, 50);
			Fill(sb, tab, fill * alpha);
			Fill(sb, new Rectangle(tab.X, tab.Y, 1, tab.Height), new Color(0, 0, 0, 60) * alpha);
			Fill(sb, new Rectangle(tab.Right - 1, tab.Y, 1, tab.Height), new Color(255, 238, 206, 45) * alpha);
			Fill(sb, new Rectangle(tab.X, tab.Y, tab.Width, 1), GoldDim * alpha);
			Fill(sb, new Rectangle(tab.X, tab.Bottom - 1, tab.Width, 1), GoldDim * alpha);
			if (hover) {
				int outer = rightSide ? tab.Right - 2 : tab.X + 1;
				Fill(sb, new Rectangle(outer, tab.Y + 2, 1, tab.Height - 4),
					Gold * (0.72f * alpha));
				Fill(sb, new Rectangle(tab.X + 1, tab.Y + 2, tab.Width - 2, 1),
					new Color(255, 232, 190, 70) * alpha);
			}

			// Ribbon notch: a couple of stepped bites out of the bottom end.
			int bx = tab.X;
			int by = tab.Bottom;
			for (int i = 0; i < 3; i++)
			{
				Fill(sb, new Rectangle(bx + i, by + i, tab.Width - i * 2, 1), fill * (0.65f * alpha));
			}

			try
			{
				DynamicSpriteFont font = FontAssets.MouseText.Value;
				Vector2 size = font.MeasureString(label);
				float scale = Math.Min(0.52f, (tab.Height - 22f) / Math.Max(1f, size.X));
				float rot = rightSide ? MathHelper.PiOver2 : -MathHelper.PiOver2;
				sb.DrawString(font, label, new Vector2(tab.Center.X, tab.Center.Y),
					Paper * alpha, rot, size * 0.5f, scale, SpriteEffects.None, 0f);
			}
			catch { }

			DrawDiamond(sb, tab.Center.X, tab.Y + 10, 2, Gold * alpha);
		}

		/// <summary>A library-style progress stamp: double rule plus tracked caps.</summary>
		public static void DrawStamp(SpriteBatch sb, Rectangle box, string label, bool done, float alpha)
		{
			if (label.Length == 0 || box.Width <= 0)
				return;

			Color ink = done ? new Color(52, 94, 58) : new Color(118, 98, 74);
			float a = alpha * (done ? 0.86f : 0.55f);

			Fill(sb, new Rectangle(box.X, box.Y, box.Width, 1), ink * a);
			Fill(sb, new Rectangle(box.X, box.Bottom - 1, box.Width, 1), ink * a);
			Fill(sb, new Rectangle(box.X, box.Y, 1, box.Height), ink * a);
			Fill(sb, new Rectangle(box.Right - 1, box.Y, 1, box.Height), ink * a);
			Fill(sb, new Rectangle(box.X + 2, box.Y + 2, box.Width - 4, 1), ink * (a * 0.55f));
			Fill(sb, new Rectangle(box.X + 2, box.Bottom - 3, box.Width - 4, 1), ink * (a * 0.55f));

			const float scale = 0.5f;
			const float track = 1.35f;
			float w = TrackWidth(label, scale, track);
			DrawTracked(sb, label, new Vector2(box.X + (box.Width - w) / 2f, box.Y + 5),
				ink * a, scale, track);
		}

		/// <summary>Ribbon marker hanging over the gutter at the top of a bookmarked page.</summary>
		public static void DrawBookmarkRibbon(SpriteBatch sb, Rectangle r, float alpha, bool hover)
		{
			int sway = hover ? (int)Math.Round(Math.Sin(Main.GameUpdateCount * 0.14f) * 1.2f) : 0;
			Color c = hover ? new Color(198, 66, 72) : Ribbon;
			Fill(sb, new Rectangle(r.X + sway, r.Y, r.Width, r.Height), c * alpha);
			Fill(sb, new Rectangle(r.X + sway, r.Y, 1, r.Height), new Color(66, 16, 20, 120) * alpha);
			Fill(sb, new Rectangle(r.Right - 1 + sway, r.Y, 1, r.Height), new Color(255, 225, 200, 55) * alpha);
			Fill(sb, new Rectangle(r.X + sway, r.Bottom - 5, r.Width, 1), GoldDim * alpha);
			Fill(sb, new Rectangle(r.X + 1 + sway, r.Bottom - 3, r.Width - 2, 1), GoldDim * (0.6f * alpha));
			if (hover) {
				Fill(sb, new Rectangle(r.X + 2 + sway, r.Y + 4, 1, r.Height - 12),
					new Color(255, 220, 190, 45) * alpha);
			}
		}

		// --------------------------------------------------------------- widgets

		/// <summary>Brass-on-paper button for the book skin (used for the close tab).</summary>
		public static void DrawButton(SpriteBatch sb, Rectangle b, string label, bool disabled, float alpha,
			bool hover)
		{
			Color fill = disabled
				? new Color(206, 196, 170, 200) * alpha
				: hover ? new Color(234, 222, 190, 240) * alpha : new Color(220, 210, 182, 225) * alpha;
			Fill(sb, b, fill);
			if (hover && !disabled) {
				float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * 0.12f);
				UiArt.DrawGlow(sb, b.Center.ToVector2(), 26f, Gold * (0.12f * pulse * alpha));
				Fill(sb, new Rectangle(b.X + 3, b.Bottom - 3, b.Width - 6, 1),
					Gold * (0.62f * pulse * alpha));
			}
			Color edge = disabled ? new Color(150, 132, 100, 120) * alpha : GoldDim * alpha;
			Color edgeTop = disabled ? edge : (hover ? Gold : Color.Lerp(Gold, GoldDim, 0.35f)) * alpha;
			Fill(sb, new Rectangle(b.X, b.Y, b.Width, 1), edgeTop);
			Fill(sb, new Rectangle(b.X, b.Bottom - 1, b.Width, 1), edge);
			Fill(sb, new Rectangle(b.X, b.Y, 1, b.Height), edge);
			Fill(sb, new Rectangle(b.Right - 1, b.Y, 1, b.Height), edge);

			Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * 0.8f;
			Color text = disabled ? new Color(InkDim.R, InkDim.G, InkDim.B, 140) * alpha : Ink * alpha;
			int lift = hover && !disabled ? 1 : 0;
			sb.DrawString(FontAssets.MouseText.Value, label,
				new Vector2(b.X + (b.Width - size.X) / 2f, b.Y + (b.Height - size.Y) / 2f - lift),
				text, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
		}

		/// <summary>Small brass page-corner arrow used instead of chunky Prev/Next plates.</summary>
		public static void DrawPageArrow(SpriteBatch sb, Rectangle b, bool right, bool disabled, float alpha)
		{
			bool hover = !disabled && b.Contains(UiViewport.MousePoint);
			Color fill = disabled ? new Color(212, 202, 176, 175)
				: hover ? new Color(238, 226, 194, 240) : new Color(225, 214, 186, 220);
			Fill(sb, b, fill * alpha);

			if (hover) {
				float pulse = 0.72f + 0.28f * (float)Math.Sin(Main.GameUpdateCount * 0.11f);
				UiArt.DrawGlow(sb, b.Center.ToVector2(), 34f, Gold * (0.17f * pulse * alpha));
				Fill(sb, new Rectangle(b.X + 4, b.Bottom - 3, b.Width - 8, 1),
					Gold * (0.55f * pulse * alpha));
			}

			Color edge = disabled ? new Color(160, 144, 112, 110) : hover ? Gold : GoldDim;
			Fill(sb, new Rectangle(b.X, b.Y, b.Width, 1), edge * alpha);
			Fill(sb, new Rectangle(b.X, b.Bottom - 1, b.Width, 1), edge * alpha);
			Fill(sb, new Rectangle(b.X, b.Y, 1, b.Height), edge * alpha);
			Fill(sb, new Rectangle(b.Right - 1, b.Y, 1, b.Height), edge * alpha);

			Color ink = disabled ? new Color(InkDim.R, InkDim.G, InkDim.B, 110) : Ink;
			int cx = b.Center.X + (hover ? (right ? 1 : -1) : 0);
			int cy = b.Center.Y;
			for (int i = 0; i < 5; i++)
			{
				int x = right ? cx - 5 + i : cx + 3 - i;
				Fill(sb, new Rectangle(x, cy - 6 + i, 2, 2), ink * alpha);
				Fill(sb, new Rectangle(x, cy + 4 - i, 2, 2), ink * alpha);
			}
		}
	}
}
