using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Applies MiniYoyoFx trauma as a local camera offset. Visual only, client
    // only, never synced. Honors the photo-safe toggle in YoyoConfig.
    public class YoyoCameraSystem : ModSystem
    {
        public override void ModifyScreenPosition()
        {
            if (Main.netMode == Terraria.ID.NetmodeID.Server)
                return;
            if (Main.gameMenu)
                return;
            try
            {
                var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                if (cfg != null && !cfg.Feedback.Screenshake)
                {
                    // Drain trauma so re-enabling starts clean.
                    MiniYoyoFx.ConsumeShakeOffset();
                    return;
                }
            }
            catch
            {
                // Config unavailable: shake anyway (default-on behavior).
            }
            Vector2 offset = MiniYoyoFx.ConsumeShakeOffset();
            if (offset != Vector2.Zero)
                Main.screenPosition += offset;
        }
    }
}
