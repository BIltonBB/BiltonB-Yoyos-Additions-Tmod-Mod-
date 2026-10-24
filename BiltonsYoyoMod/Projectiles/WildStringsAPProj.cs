using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class WildStringsAPProj : WildStringBase
    {
        // TEXTURE

        public override string Texture =>
            "BiltonsYoyoMod/Projectiles/WildStringsAPProj";

        // COLOR

        protected override Color StringColor =>
            Color.Blue;

        // ORBIT SETTINGS

        protected override int MinimumDistance =>
            90;

        protected override int MaximumDistance =>
            150;

        protected override float MinimumSpeedDegrees =>
            3f;

        protected override float MaximumSpeedDegrees =>
            5f;

        // DEFAULTS

        public override void SetDefaults()
        {
            base.SetDefaults();

            /*
             * Increased from 5 to 20.
             */
            Projectile.penetrate = 20;

            /*
             * Keep AP alive for a full 30 seconds.
             */
            Projectile.timeLeft = 30 * 60;
        }

        // POLYESTER EFFECT

        public override void ModifyHitNPC(
            NPC target,
            ref NPC.HitModifiers modifiers)
        {
            /*
             * Polyester:
             * +3 armor penetration on every hit.
             */

            modifiers.ArmorPenetration += 3;
        }
    }
}