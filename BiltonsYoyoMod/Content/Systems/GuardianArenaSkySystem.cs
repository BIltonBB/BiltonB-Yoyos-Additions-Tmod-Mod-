using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles;
using BiltonsYoyoMod.Content.Sky;

namespace BiltonsYoyoMod.Content.Systems
{
    // Owns the arena sky state: on while any arena box lives, off otherwise
    // (covers death, despawn, Desperation drop and world exit — no per-system
    // cleanup needed anywhere else).
    internal class GuardianArenaSkySystem : ModSystem
    {
        public override void PostUpdateEverything()
        {
            if (Main.gameMenu || Main.dedServ)
                return;
            string key = GuardianArenaSky.Key;
            var sky = SkyManager.Instance[key];
            if (sky == null)
                return;
            // Grace period mirrors the sky: brief boxless gaps (kill/restore
            // cycles) keep the sky solid instead of flickering its fade.
            bool up = GuardianArenaBox.AnyLiveBox() || Main.GameUpdateCount - GuardianArenaBox.LastBoxSeenTick < 180;
            if (up)
            {
                if (!sky.IsActive())
                    SkyManager.Instance.Activate(key);
            }
            else if (sky.IsActive())
            {
                SkyManager.Instance.Deactivate(key);
            }
        }
    }
}
