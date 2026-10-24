using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;

namespace BiltonsYoyoMod.Projectiles
{
    public class WildStringsACProj :
        WildStringBase
    {
        // TEXTURE

        public override string Texture =>
            "BiltonsYoyoMod/Projectiles/WildStringsACProj";

        // COLOR

        protected override Color StringColor =>
            Color.White;

        // ORBIT SETTINGS

        protected override int MinimumDistance =>
            92;

        protected override int MaximumDistance =>
            122;

        protected override float MinimumSpeedDegrees =>
            1f;

        protected override float MaximumSpeedDegrees =>
            2f;

        // STATE

        private bool firstHitOccurred;

        private float normalAngularSpeed;

        // DEFAULTS

        public override void SetDefaults()
        {
            base.SetDefaults();

            Projectile.penetrate =
                1;

            Projectile.timeLeft =
                30 * 60;
        }

        // AI

        public override void AI()
        {
            /*
             * After the first hit, force the stored Wild String
             * angular speed to exactly 50% of its original value.
             *
             * WildStringBase reads Projectile.localAI[1] every AI
             * frame, so this is the persistent value that matters.
             */

            if (firstHitOccurred)
            {
                Projectile.localAI[1] =
                    normalAngularSpeed * 0.5f;
            }

            base.AI();
        }

        // HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            // SLOW

            target.AddBuff(
                BuffID.Slow,
                3 * 60
            );

            // FIRST HIT

            if (firstHitOccurred)
            {
                return;
            }

            firstHitOccurred =
                true;

            /*
             * Save the ORIGINAL speed before reducing it.
             */
            normalAngularSpeed =
                Projectile.localAI[1];

            /*
             * Immediately store the half-speed value.
             */
            Projectile.localAI[1] =
                normalAngularSpeed * 0.5f;

            // Visible downshift cue: the string sheds a puff as it halves
            // orbit speed, so the slowdown reads instead of feeling silent.
            if (!Main.dedServ)
            {
                for (int i = 0; i < 10; i++)
                {
                    Dust dust = Dust.NewDustDirect(
                        Projectile.position,
                        Projectile.width,
                        Projectile.height,
                        DustID.Cloud,
                        Main.rand.NextFloat(-2f, 2f),
                        Main.rand.NextFloat(-2f, 2f),
                        100,
                        default,
                        1f);
                    dust.noGravity = true;
                }
            }

            // HALF DAMAGE

            Projectile.damage =
                System.Math.Max(
                    1,
                    Projectile.damage / 2
                );

            // PROJECTILE PENETRATION

            Projectile.penetrate =
                10;

            Projectile.netUpdate =
                true;
        }
    }
}