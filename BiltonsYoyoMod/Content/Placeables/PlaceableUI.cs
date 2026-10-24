using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using YoyoPanel = BiltonsYoyoMod.Content.UI.YoyoNPC.YoyoNPCPanel;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Shared widgets for the four placeable-block panels: word-wrapped text
	// (kills single-line overflow on narrow screens), section dividers,
	// level pips and circumference-scaled in-world rings.
	public static class PlaceableUI
	{
		// Greedy word wrap. Returns the drawn height in pixels.
		public static float DrawWrapped(SpriteBatch sb, string text, Vector2 pos, Color color, float scale, float maxWidth)
		{
			var font = FontAssets.MouseText.Value;
			float lineH = font.LineSpacing * scale;
			float y = pos.Y;
			string line = "";
			foreach (string word in text.Split(' ')) {
				string test = line.Length == 0 ? word : line + " " + word;
				if (line.Length > 0 && font.MeasureString(test).X * scale > maxWidth) {
					Terraria.Utils.DrawBorderString(sb, line, new Vector2(pos.X, y), color, scale);
					y += lineH;
					line = word;
				}
				else {
					line = test;
				}
			}
			if (line.Length > 0) {
				Terraria.Utils.DrawBorderString(sb, line, new Vector2(pos.X, y), color, scale);
				y += lineH;
			}
			return y - pos.Y;
		}

		public static void Divider(SpriteBatch sb, int x, int y, int w, Color color)
		{
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x, y, w, 1), color);
		}

		// Wrapped text with a line cap: handles \n paragraphs, wraps each
		// at maxWidth, draws at most maxLines (last line gets "…" when cut).
		// Returns the drawn height in pixels. For info cards that must
		// always fit their panel.
		public static float DrawWrappedCapped(SpriteBatch sb, string text, Vector2 pos, Color color, float scale, float maxWidth, int maxLines)
		{
			var font = FontAssets.MouseText.Value;
			float lineH = font.LineSpacing * scale;
			var lines = new System.Collections.Generic.List<string>();
			foreach (string paragraph in (text ?? "").Split('\n')) {
				string line = "";
				foreach (string word in paragraph.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)) {
					string test = line.Length == 0 ? word : line + " " + word;
					if (line.Length > 0 && font.MeasureString(test).X * scale > maxWidth) {
						lines.Add(line);
						line = word;
					}
					else {
						line = test;
					}
				}
				lines.Add(line);
			}
			bool cut = lines.Count > maxLines;
			int show = System.Math.Min(lines.Count, System.Math.Max(1, maxLines));
			float y = pos.Y;
			for (int i = 0; i < show; i++) {
				string line = lines[i];
				if (cut && i == show - 1)
					line += "…";
				Terraria.Utils.DrawBorderString(sb, line, new Vector2(pos.X, y), color, scale);
				y += lineH;
			}
			return y - pos.Y;
		}

		// Pixel-clipped single-line text: the full string draws inside a
		// GPU scissor rect, so a half-offscreen glyph shows as a clean
		// half-letter (same vanilla pattern as UIElement OverflowHidden:
		// End -> set ScissorRectangle -> Begin with ScissorTestEnable ->
		// draw -> End -> restore). Boxes already clip per-pixel via
		// ClipFill; this makes labels match instead of popping whole
		// letters in and out.
		// Refs: tModLoader UIElement.Draw OverflowHidden (End/Scissor/Begin
		// with Main.UIScaleMatrix), MonoGame RasterizerState.ScissorTestEnable
		// docs, MonoGame SpriteBatch.Begin(rasterizerState, transformMatrix).
		private static readonly RasterizerState ClipRasterizer = new RasterizerState { ScissorTestEnable = true };

		private static Rectangle UIToDeviceRect(Rectangle ui, GraphicsDevice gd)
		{
			Vector2 tl = Vector2.Transform(new Vector2(ui.X, ui.Y), Main.UIScaleMatrix);
			Vector2 br = Vector2.Transform(new Vector2(ui.Right, ui.Bottom), Main.UIScaleMatrix);
			int x0 = (int)System.Math.Floor(System.Math.Min(tl.X, br.X));
			int y0 = (int)System.Math.Floor(System.Math.Min(tl.Y, br.Y));
			int x1 = (int)System.Math.Ceiling(System.Math.Max(tl.X, br.X));
			int y1 = (int)System.Math.Ceiling(System.Math.Max(tl.Y, br.Y));
			Rectangle dev = new(x0, y0, System.Math.Max(0, x1 - x0), System.Math.Max(0, y1 - y0));
			try {
				dev = Rectangle.Intersect(dev, gd.Viewport.Bounds);
			}
			catch { }
			return dev;
		}

		public static void DrawClippedText(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, string text, float centerX, float y, float scale, Color color, Rectangle view)
		{
			DrawClippedRun(sb, text, centerX, y, scale, color, view, centered: true);
		}

		// Shared pixel-clipped single-line text for every scrolled panel
		// (tech tree, trap UI, ...): centered=true treats x as the center,
		// false treats it as the left edge. Partial glyphs draw as partial
		// glyphs — never whole-letter popping.
		public static void DrawClippedRun(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, string text, float x, float y, float scale, Color color, Rectangle view, bool centered)
		{
			if (string.IsNullOrEmpty(text))
				return;
			var font = FontAssets.MouseText.Value;
			float lineH = font.LineSpacing * scale;
			if (y + lineH < view.Y || y > view.Bottom)
				return;
			float total = font.MeasureString(text).X * scale;
			Vector2 pos = centered ? new(x - total / 2f, y) : new(x, y);
			// Fast path: fully inside (2px padding like the old per-char
			// version) — no state change, no Begin/End churn.
			if (pos.X >= view.X + 2 && pos.X + total <= view.Right - 2) {
				Terraria.Utils.DrawBorderString(sb, text, pos, color, scale);
				return;
			}
			// Clip to strictly inside the viewport (1px frame margin) so
			// text never draws over the frame, matching DrawAxisBar.
			Rectangle inner = new(view.X + 1, view.Y + 1, System.Math.Max(0, view.Width - 2), System.Math.Max(0, view.Height - 2));
			GraphicsDevice gd = sb.GraphicsDevice;
			Rectangle prevScissor;
			RasterizerState prevRaster;
			try {
				prevScissor = gd.ScissorRectangle;
			}
			catch {
				Terraria.Utils.DrawBorderString(sb, text, pos, color, scale);
				return;
			}
			try {
				prevRaster = gd.RasterizerState;
			}
			catch {
				prevRaster = RasterizerState.CullCounterClockwise;
			}
			Rectangle deviceClip = UIToDeviceRect(inner, gd);
			Rectangle adjusted;
			try {
				adjusted = Rectangle.Intersect(deviceClip, prevScissor);
				if (adjusted.IsEmpty && !deviceClip.IsEmpty) {
					// Inherited scissor state is degenerate (another mod's
					// batch left garbage behind — see client.log SpriteBatch
					// errors): fall back to the known-good viewport bounds
					// instead of culling text that is actually visible.
					adjusted = Rectangle.Intersect(deviceClip, gd.Viewport.Bounds);
				}
			}
			catch {
				adjusted = deviceClip;
			}
			if (adjusted.Width <= 0 || adjusted.Height <= 0) {
				// Fully clipped: draw nothing, batch untouched.
				return;
			}
			try {
				sb.End();
			}
			catch {
				// Batch wasn't begun (shouldn't happen in UI layers):
				// fall back to unclipped draw so text never vanishes.
				Terraria.Utils.DrawBorderString(sb, text, pos, color, scale);
				return;
			}
			try {
				gd.ScissorRectangle = adjusted;
				// Vanilla UIElement.Draw sets BOTH device states before Begin
				// and passes the rasterizer in — match it exactly.
				gd.RasterizerState = ClipRasterizer;
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, ClipRasterizer, null, Main.UIScaleMatrix);
				Terraria.Utils.DrawBorderString(sb, text, pos, color, scale);
				sb.End();
			}
			finally {
				// Restore exactly (vanilla UIElement.Draw does the same:
				// ScissorRectangle + RasterizerState, then Begin normal).
				try { gd.ScissorRectangle = prevScissor; } catch { }
				try { gd.RasterizerState = prevRaster; } catch { }
				sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
			}
		}
		public static void DrawTruncated(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, string text, Vector2 pos, Color color, float scale, float maxWidth)
		{
			var font = FontAssets.MouseText.Value;
			if (font.MeasureString(text).X * scale <= maxWidth) {
				Terraria.Utils.DrawBorderString(sb, text, pos, color, scale);
				return;
			}
			string t = text;
			while (t.Length > 4 && font.MeasureString(t + "...").X * scale > maxWidth)
				t = t[..^1];
			Terraria.Utils.DrawBorderString(sb, t + "...", pos, color, scale);
		}
		public static void Pips(SpriteBatch sb, Vector2 pos, int level, int max)
		{
			for (int i = 0; i < max; i++) {
				Color c = i < level ? Color.Gold : new Color(50, 56, 84, 220);
				sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)pos.X + i * 14, (int)pos.Y, 10, 10), c);
			}
		}

		// Evenly spaced ring. Dust count should scale with circumference
		// (pass count ~= radius/5); each call rotates its phase so rapid
		// re-spawns interleave into a solid band. Callers must gate
		// !Main.dedServ and throttle (every 4-8 ticks is plenty).
		public static void DustRing(Vector2 center, float radius, int dustType, int count, float scale = 1.2f)
		{
			float off = Main.GameUpdateCount * 0.07f;
			for (int i = 0; i < count; i++) {
				float ang = off + i / (float)count * MathHelper.TwoPi;
				Vector2 pos = center + ang.ToRotationVector2() * radius;
				Dust d = Dust.NewDustDirect(pos - new Vector2(2, 2), 4, 4, dustType, 0f, 0f, 120, default, scale);
				d.noGravity = true;
				d.velocity = Vector2.Zero;
			}
		}

		public static int RingCount(float radiusPixels)
		{
			return System.Math.Clamp((int)(radiusPixels / 5f), 28, 110);
		}

		// Panel header strip with a slow breathing pulse so panels feel alive.
		public static void HeaderStrip(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, int x, int y, int w, Color baseColor)
		{
			float pulse = 0.75f + 0.25f * (float)System.Math.Sin(Main.GameUpdateCount * 0.06f);
			sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(x + 6, y + 5, w - 12, 3),
				new Color((byte)(baseColor.R * pulse), (byte)(baseColor.G * pulse), (byte)(baseColor.B * pulse), 200));
		}

		// Tab row. Returns the (possibly clicked) selected index.
		public static int TabRow(SpriteBatch sb, int x, int y, int width, string[] names, int selected)
		{
			float bw = (width - (names.Length - 1) * 6) / (float)names.Length;
			for (int i = 0; i < names.Length; i++) {
				Rectangle b = new(x + (int)(i * (bw + 6)), y, (int)bw, 28);
				if (YoyoPanel.Button(sb, b, names[i], names[i], selected == i, false, 0.8f))
					selected = i;
			}
			return selected;
		}

		// Paged tab row (#2): a fixed window of tabs with < > arrows, so
		// tab buttons never shrink or overlap no matter how many tabs a
		// panel grows. Arrows move the SELECTION (never just the offset),
		// so they always visibly work; the window follows the selection.
		// Entries named "|" render as a fixed divider gap (not clickable,
		// never selected). Returns the (possibly clicked) selected index.
		public static int TabRowPaged(SpriteBatch sb, int x, int y, int width, string[] names, int selected, ref int offset, int visible = 3)
		{
			if (names == null || names.Length == 0)
				return selected;
			if (visible < 1)
				visible = 1;
			bool hasSep = false;
			foreach (string entry in names) {
				if (entry == "|") {
					hasSep = true;
					break;
				}
			}
			if (names.Length <= visible && !hasSep) {
				offset = 0;
				return TabRow(sb, x, y, width, names, selected);
			}
			bool IsSep(int i) => i >= 0 && i < names.Length && names[i] == "|";
			// Selection always lands on a real tab.
			selected = System.Math.Clamp(selected, 0, names.Length - 1);
			if (IsSep(selected)) {
				selected = 0;
				while (selected < names.Length && IsSep(selected))
					selected++;
			}
			offset = System.Math.Clamp(offset, 0, System.Math.Max(0, names.Length - visible));
			if (selected < offset)
				offset = selected;
			if (selected > offset + visible - 1)
				offset = selected - visible + 1;
			const int arrowW = 30;
			// Neighbor selections, skipping dividers.
			int prev = selected - 1;
			while (prev > 0 && IsSep(prev))
				prev--;
			int next = selected + 1;
			while (next < names.Length - 1 && IsSep(next))
				next++;
			Rectangle left = new(x, y, arrowW, 28);
			if (YoyoPanel.Button(sb, left, "<", "Previous tab", false, prev < 0 || IsSep(prev), 0.9f))
				selected = System.Math.Max(0, prev);
			Rectangle right = new(x + width - arrowW, y, arrowW, 28);
			if (YoyoPanel.Button(sb, right, ">", "Next tab", false, next >= names.Length || IsSep(next), 0.9f))
				selected = System.Math.Min(names.Length - 1, next);
			// Window follows a possibly-moved selection.
			if (selected < offset)
				offset = selected;
			if (selected > offset + visible - 1)
				offset = selected - visible + 1;
			offset = System.Math.Clamp(offset, 0, System.Math.Max(0, names.Length - visible));
			// Layout: dividers take a fixed gap, tabs split the rest.
			int seps = 0;
			int tabs = 0;
			for (int i = offset; i < offset + visible && i < names.Length; i++) {
				if (IsSep(i))
					seps++;
				else
					tabs++;
			}
			if (tabs <= 0)
				return selected;
			const int sepW = 14;
			float bw = (width - arrowW * 2 - (visible - 1) * 6 - seps * sepW) / (float)tabs;
			float cx = x + arrowW;
			for (int i = offset; i < offset + visible && i < names.Length; i++) {
				if (IsSep(i)) {
					int midY = y + 4;
					sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)cx + 4, midY, 2, 20), new Color(90, 98, 140, 160));
					cx += sepW + 6;
					continue;
				}
				Rectangle b = new((int)cx, y, (int)bw, 28);
				if (YoyoPanel.Button(sb, b, names[i], names[i], selected == i, false, 0.8f))
					selected = i;
				cx += bw + 6;
			}
			return selected;
		}
	}
}
