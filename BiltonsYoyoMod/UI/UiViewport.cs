using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace BiltonsYoyoMod.UI
{
	/// <summary>
	/// One coordinate space for every custom interface.
	///
	/// Terraria changes <c>Main.screenWidth</c> and <c>Main.MouseScreen</c>
	/// depending on which zoom context is active. During UI drawing they are
	/// already divided by <c>Main.UIScale</c>; during world input they are raw
	/// pixels and may additionally be transformed by the game zoom. Reading
	/// those values in both phases is what made the Splicer's boards and tabs
	/// draw in one place while their hitboxes lived somewhere else.
	///
	/// This helper deliberately ignores the current zoom context. It always
	/// reconstructs the same virtual UI viewport from the raw backbuffer and
	/// the raw mouse position, so layout, drawing and hit-testing agree.
	/// </summary>
	internal static class UiViewport
	{
		public static float Scale
		{
			get {
				float scale = Main.UIScale;
				return scale <= 0.01f ? 1f : scale;
			}
		}

		public static int Width => ViewSize.X;
		public static int Height => ViewSize.Y;

		/// <summary>Pointer position in the virtual (InterfaceScaleType.UI)
		/// coordinate space.</summary>
		public static Vector2 Mouse
			=> new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Scale;

		public static Point MousePoint => Mouse.ToPoint();

		private static Point ViewSize
		{
			get {
				// The backbuffer is unaffected by SetZoom_UI/SetZoom_World.
				// Falling back to Main.screenWidth keeps this safe during the
				// brief moments where the graphics device is being recreated.
				int rawW = Main.screenWidth;
				int rawH = Main.screenHeight;
				try {
					var device = Main.instance?.GraphicsDevice;
					if (device != null) {
						rawW = device.PresentationParameters.BackBufferWidth;
						rawH = device.PresentationParameters.BackBufferHeight;
					}
				}
				catch {
					// Keep the screen values above.
				}
				float scale = Scale;
				return new Point(
					System.Math.Max(1, (int)(rawW / scale)),
					System.Math.Max(1, (int)(rawH / scale)));
			}
		}
	}
}
