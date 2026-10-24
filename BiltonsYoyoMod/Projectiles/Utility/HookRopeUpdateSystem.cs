using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles.Utility
{
	/// <summary>
	/// Steps the hook's persistent rope after players and projectiles have
	/// finished moving for the tick, but before drawing. Doing it here means
	/// the drawn rope is attached to the player's current position, while the
	/// player's next movement pass still reads the shape from this step.
	/// </summary>
	public sealed class HookRopeUpdateSystem : ModSystem
	{
		public override void PostUpdateEverything()
		{
			if (Main.dedServ)
				return;

			YoyoHookProj.UpdateVisibleRopes();
		}
	}
}
