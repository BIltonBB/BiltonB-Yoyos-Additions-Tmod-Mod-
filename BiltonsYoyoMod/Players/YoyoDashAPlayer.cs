using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
    public class YoyoDashAPlayer : ModPlayer
    {
        // True only when dash accessory equipped
        public bool enabled;

        // Cooldown timer
        public int dashCooldown;
        // Dash freeze timer (optional)
        public int dashFreeze;

        public override void ResetEffects()
        {
            enabled = false; // Reset each tick
        }
    }
}