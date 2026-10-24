using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
    public class YoyoAxlePlayer : ModPlayer
    {
        // EQUIPPED AXLE

        public bool UnderWrapped;
        public bool OverWrapped;
        public bool LooseWrapped;
        public bool TightWrapped;

        // RESET

        public override void ResetEffects()
        {
            UnderWrapped = false;
            OverWrapped = false;
            LooseWrapped = false;
            TightWrapped = false;
        }

        // HELPERS

        public bool HasAnyAxle =>
            UnderWrapped ||
            OverWrapped ||
            LooseWrapped ||
            TightWrapped;

        // TIGHT WRAPPED

        /*
         * This timer is handled independently for every yoyo
         * projectile by the GlobalProjectile.
         */

        public void ClearAxleState()
        {
            // Intentionally empty.
            //
            // Per-yoyo state belongs to GlobalProjectile because
            // multiple yoyos/projectiles can exist independently.
        }
    }
}