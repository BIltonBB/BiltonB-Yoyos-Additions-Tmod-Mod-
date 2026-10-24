using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.UI
{
	/// <summary>
	/// Tiny procedural art cache for the interface. The mod ships no UI
	/// textures, so soft discs, rings, glows and a vignette are generated once
	/// on first use and reused by the yoyo wheel and the guidebook. All of them
	/// are white with an alpha mask so callers can tint them freely.
	/// </summary>
	internal static class UiArt
	{
		private const int Size = 256;

		private static Texture2D disc;
		private static Texture2D ring;
		private static Texture2D glow;
		private static Texture2D vignette;
		private static Texture2D pageGradient;
		private static Texture2D fadeH;
		private static Texture2D grain;
		private static bool failed;

		public static Texture2D Disc => disc != null && !disc.IsDisposed ? disc : (disc = Build(AlphaDisc));
		public static Texture2D Ring => ring != null && !ring.IsDisposed ? ring : (ring = Build(AlphaRing));
		public static Texture2D Glow => glow != null && !glow.IsDisposed ? glow : (glow = Build(AlphaGlow));
		public static Texture2D Vignette => vignette != null && !vignette.IsDisposed ? vignette : (vignette = Build(AlphaVignette));

		/// <summary>Vertical paper gradient for the turning leaf. The texture is
		/// white with a gentle luminance falloff, so callers tint it with the
		/// page/front/back colour and get smooth shading without horizontal
		/// strip seams.</summary>
		public static Texture2D PageGradient => pageGradient != null && !pageGradient.IsDisposed
			? pageGradient : (pageGradient = BuildPageGradient());

		/// <summary>Horizontal soft fade (opaque at the left, transparent at the
		/// right) used for cast shadows that sweep across a page.</summary>
		public static Texture2D FadeH => fadeH != null && !fadeH.IsDisposed
			? fadeH : (fadeH = BuildFadeH());

		/// <summary>Tiling paper grain. Draw at native scale in a few tiles rather
		/// than stretching it, so the fibre stays crisp at 100% and 150% UI.</summary>
		public static Texture2D Grain => grain != null && !grain.IsDisposed
			? grain : (grain = BuildGrain());

		/// <summary>Draws a one-pixel-wide soft disc stretched to the requested radius.</summary>
		public static void DrawDisc(SpriteBatch sb, Vector2 center, float radius, Color color)
		{
			Texture2D tex = Disc;
			if (tex == null || radius <= 0f)
				return;
			sb.Draw(tex, center, null, color, 0f, tex.Size() * 0.5f,
				radius * 2f / tex.Width, SpriteEffects.None, 0f);
		}

		public static void DrawRing(SpriteBatch sb, Vector2 center, float radius, Color color)
		{
			Texture2D tex = Ring;
			if (tex == null || radius <= 0f)
				return;
			// AlphaRing is authored with its crisp ring at 78% of the texture
			// radius so the outer bloom has room. Scale that radius back up:
			// callers mean "draw a ring at this radius", and without the
			// correction every bezel sat noticeably inside its dark plate.
			const float ringCenter = 0.78f;
			float scale = radius * 2f / (tex.Width * ringCenter);
			sb.Draw(tex, center, null, color, 0f, tex.Size() * 0.5f,
				scale, SpriteEffects.None, 0f);
		}

		public static void DrawGlow(SpriteBatch sb, Vector2 center, float radius, Color color)
		{
			Texture2D tex = Glow;
			if (tex == null || radius <= 0f)
				return;
			sb.Draw(tex, center, null, color, 0f, tex.Size() * 0.5f,
				radius * 2f / tex.Width, SpriteEffects.None, 0f);
		}

		private static float Smooth(float edge0, float edge1, float x)
		{
			float t = MathHelper.Clamp((x - edge0) / Math.Max(0.0001f, edge1 - edge0), 0f, 1f);
			return t * t * (3f - 2f * t);
		}

		private static Texture2D Build(Func<float, float, float> alpha)
		{
			if (failed)
				return null;
			try {
				GraphicsDevice gd = Main.instance?.GraphicsDevice;
				if (gd == null)
					return null;
				var data = new Color[Size * Size];
				float half = Size * 0.5f;
				for (int y = 0; y < Size; y++) {
					for (int x = 0; x < Size; x++) {
						float dx = (x + 0.5f - half) / half;
						float dy = (y + 0.5f - half) / half;
						float r = (float)Math.Sqrt(dx * dx + dy * dy);
						float a = MathHelper.Clamp(alpha(dx, dy) * (1f - Smooth(0.985f, 1f, r)), 0f, 1f);
						// SpriteBatch runs with premultiplied alpha. A white
						// texel with alpha 0 still contributes 255 to RGB and
						// turns every soft disc into a bright square. Pre-
						// multiply the mask so transparent pixels are truly 0.
						byte v = (byte)(a * 255f);
						data[y * Size + x] = new Color(v, v, v, v);
					}
				}
				var tex = new Texture2D(gd, Size, Size, false, SurfaceFormat.Color);
				tex.SetData(data);
				return tex;
			}
			catch {
				failed = true;
				return null;
			}
		}

		private static float AlphaDisc(float x, float y)
		{
			float r = (float)Math.Sqrt(x * x + y * y);
			// A plate, not a soft orb: solid almost to the requested radius,
			// then a short antialiased rim. The old half-radius falloff made
			// the wheel shell look foggy and let bright medallions halo out.
			return 1f - Smooth(0.88f, 1f, r);
		}

		private static float AlphaRing(float x, float y)
		{
			float r = (float)Math.Sqrt(x * x + y * y);
			// A crisp main ring with a faint outer bloom, so brass edges read
			// as metal instead of a one-pixel circle.
			float ringA = 1f - Smooth(0f, 0.045f, Math.Abs(r - 0.78f));
			float bloom = (1f - Smooth(0f, 0.14f, Math.Abs(r - 0.78f))) * 0.18f;
			return MathHelper.Clamp(ringA + bloom, 0f, 1f);
		}

		private static float AlphaGlow(float x, float y)
		{
			float r = (float)Math.Sqrt(x * x + y * y);
			float k = 1f - MathHelper.Clamp(r, 0f, 1f);
			return k * k * k;
		}

		private static float AlphaVignette(float x, float y)
		{
			float r = (float)Math.Sqrt(x * x + y * y);
			return Smooth(0.34f, 0.92f, r) * 0.92f;
		}

		private static Texture2D BuildPageGradient()
		{
			if (failed)
				return null;
			try {
				GraphicsDevice gd = Main.instance?.GraphicsDevice;
				if (gd == null)
					return null;
				const int height = 256;
				var data = new Color[height];
				for (int y = 0; y < height; y++) {
					float t = y / (float)(height - 1);
					// A bright top edge, a soft sheen around the upper third,
					// and a warm falloff toward the tail of the leaf.
					float sheen = 0.035f * (float)Math.Exp(-Math.Pow((t - 0.23f) / 0.12f, 2));
					float value = 1f - 0.12f * t + sheen;
					byte b = (byte)MathHelper.Clamp(value * 255f, 0f, 255f);
					data[y] = new Color(b, b, b, (byte)255);
				}
				var tex = new Texture2D(gd, 1, height, false, SurfaceFormat.Color);
				tex.SetData(data);
				return tex;
			}
			catch {
				failed = true;
				return null;
			}
		}

		private static Texture2D BuildFadeH()
		{
			if (failed)
				return null;
			try {
				GraphicsDevice gd = Main.instance?.GraphicsDevice;
				if (gd == null)
					return null;
				const int width = 256;
				var data = new Color[width];
				for (int x = 0; x < width; x++) {
					float t = x / (float)(width - 1);
					float a = 1f - t;
					a = a * a * (3f - 2f * a);
					byte v = (byte)MathHelper.Clamp(a * 255f, 0f, 255f);
					data[x] = new Color(v, v, v, v);
				}
				var tex = new Texture2D(gd, width, 1, false, SurfaceFormat.Color);
				tex.SetData(data);
				return tex;
			}
			catch {
				failed = true;
				return null;
			}
		}

		private static Texture2D BuildGrain()
		{
			if (failed)
				return null;
			try {
				GraphicsDevice gd = Main.instance?.GraphicsDevice;
				if (gd == null)
					return null;
				const int size = 256;
				var data = new Color[size * size];
				uint seed = 0x9E3779B9;
				for (int i = 0; i < data.Length; i++) {
					seed = seed * 1664525u + 1013904223u;
					int a = (int)(seed >> 24) & 0x3F;
					byte alpha = (byte)(a < 8 ? 0 : a);
					data[i] = new Color(alpha, alpha, alpha, alpha);
				}
				var tex = new Texture2D(gd, size, size, false, SurfaceFormat.Color);
				tex.SetData(data);
				return tex;
			}
			catch {
				failed = true;
				return null;
			}
		}

		public static void Dispose()
		{
			try { disc?.Dispose(); } catch { }
			try { ring?.Dispose(); } catch { }
			try { glow?.Dispose(); } catch { }
			try { vignette?.Dispose(); } catch { }
			try { pageGradient?.Dispose(); } catch { }
			try { fadeH?.Dispose(); } catch { }
			try { grain?.Dispose(); } catch { }
			disc = ring = glow = vignette = null;
			pageGradient = fadeH = grain = null;
			failed = false;
		}
	}

	internal sealed class UiArtSystem : ModSystem
	{
		public override void Unload()
		{
			UiArt.Dispose();
		}
	}
}
