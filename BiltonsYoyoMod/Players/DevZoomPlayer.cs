using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
    // Safety net for the Developer yoyo's Rude Buster zoom premiere: if the camera
    // is still zoomed but no zooming RB owned by the local player is alive (killed
    // mid-premiere, world exit edge cases), give the zoom back immediately.
    public class DevZoomPlayer : ModPlayer
    {
        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;
            Projectiles.DevZoomHelper.SafetyRestore(Player);
        }
    }
}
