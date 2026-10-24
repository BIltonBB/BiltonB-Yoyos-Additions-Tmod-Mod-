using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Bosses;

namespace BiltonsYoyoMod.Content.Systems
{
    public class YoyoGuardianCameraSystem : ModSystem
    {
        private bool returningToPlayer;

        public override void ModifyScreenPosition()
        {
            int idx = YoyoGuardian.CameraFocusNPCIndex;

            if (idx >= 0 && idx < Main.maxNPCs && Main.npc[idx].active)
            {
                returningToPlayer = true;
                Vector2 desired = Main.npc[idx].Center - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
                Main.screenPosition = Vector2.Lerp(Main.screenPosition, desired, 0.25f);
                return;
            }

            if (returningToPlayer && Main.LocalPlayer != null && Main.LocalPlayer.active)
            {
                Vector2 desired = Main.LocalPlayer.Center - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
                Main.screenPosition = Vector2.Lerp(Main.screenPosition, desired, 1f);

                if (Vector2.DistanceSquared(Main.screenPosition, desired) < 4f)
                    returningToPlayer = false;
            }
        }
    }
}
