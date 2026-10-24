using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Content.Utils
{
    public static class YoyoDrawing
    {
        // Tints a color by the surrounding lighting at a world position,
        // with a small floor so strings stay faintly visible in the dark.
        public static Color ShadeByLight(Color color, Vector2 worldPos)
        {
            Point tile = worldPos.ToTileCoordinates();
            // Clamp to world bounds: GetColor outside the world returns black.
            tile.X = System.Math.Clamp(tile.X, 0, Main.maxTilesX - 1);
            tile.Y = System.Math.Clamp(tile.Y, 0, Main.maxTilesY - 1);
            Color light = Lighting.GetColor(tile.X, tile.Y);
            float r = (color.R / 255f) * (0.15f + 0.85f * light.R / 255f);
            float g = (color.G / 255f) * (0.15f + 0.85f * light.G / 255f);
            float b = (color.B / 255f) * (0.15f + 0.85f * light.B / 255f);
            return new Color(r, g, b, color.A / 255f);
        }

        public static void DrawString(Vector2 worldStart, Vector2 worldEnd, float width = 1f)
        {
            DrawString(worldStart, worldEnd, Color.White, width);
        }

		public static void DrawString(Vector2 worldStart, Vector2 worldEnd, Color color, float width = 1f)
		{
			Vector2 start = worldStart - Main.screenPosition;
			Vector2 end = worldEnd - Main.screenPosition;

			Vector2 difference = end - start;
			float length = difference.Length();
			if (length < 1f) return;

			float rotation = difference.ToRotation();
			Texture2D pixel = TextureAssets.MagicPixel.Value;

			// Sample lighting at the string midpoint so it reacts to torches/darkness.
			Color lit = ShadeByLight(color, (worldStart + worldEnd) * 0.5f);

			Main.spriteBatch.Draw(
				pixel, start, new Rectangle(0, 0, 1, 1), lit, rotation,
				new Vector2(0f, 0.5f), new Vector2(length, width), SpriteEffects.None, 0f
			);
		}
    }
}
