using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace BiltonsYoyoMod.Projectiles
{
    public class WildStringsANProj :
        WildStringBase
    {
        // STRING SETTINGS

        public override string Texture =>
            "BiltonsYoyoMod/Projectiles/WildStringsANProj";

        protected override int MinimumDistance =>
            135;

        protected override int MaximumDistance =>
            185;

        protected override float MinimumSpeedDegrees =>
            4f;

        protected override float MaximumSpeedDegrees =>
            5f;

        private const int StringLifetime =
            35 * 60;

        private const int NormalMaxHits =
            8;

        // RETRACTION

        private const int RetractionDuration =
            12;

        private bool retracting;

        private int retractTimer;

        private Vector2 retractStartPosition;

        // TENSION

        private int tensionPoints;

        private int normalHits;

        private const int MaximumTension =
            5;

        // REBOUND

        private bool rebounding;

        public bool IsRebounding =>
            rebounding;

        private int reboundTimer;

        private int reboundHits;

        private int reboundDuration;

        private float reboundDistance;

        private float reboundAngularSpeed;

        // REBOUND SETTINGS

        private const int ReboundMaxHits =
            10;

        private const float MinimumReboundDistance =
            125f;

        private const float MaximumReboundDistance =
            185f;

        /*
         * Every rebound hit slows the spin by this multiplier.
         *
         * 100% = no slowdown.
         * 85% = 15% slowdown per hit.
         */

        private const float ReboundHitSpeedMultiplier =
            0.85f;

        /*
         * Never allow the rebound to completely stop from hits.
         */

        private const float MinimumReboundSpeed =
            0.08f;

        // DEFAULTS

        public override void SetDefaults()
        {
            base.SetDefaults();

            Projectile.penetrate =
                -1;

            Projectile.timeLeft =
                StringLifetime;
        }

        // AI

        public override void AI()
        {
            // REBOUND

            if (rebounding)
            {
                UpdateRebound();
                return;
            }

            // RETRACTION

            if (retracting)
            {
                UpdateRetraction();
                return;
            }

            // NORMAL LIFETIME

            if (
                Projectile.timeLeft <=
                1
            )
            {
                BeginRetraction();
                return;
            }

            // NORMAL WILD STRING

            base.AI();
        }

        // HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            // REBOUND HIT

            if (rebounding)
            {
                reboundHits++;

                /*
                 * Every rebound hit slows its spin.
                 */

                reboundAngularSpeed *=
                    ReboundHitSpeedMultiplier;

                if (
                    reboundAngularSpeed <
                    MinimumReboundSpeed
                )
                {
                    reboundAngularSpeed =
                        MinimumReboundSpeed;
                }

                /*
                 * IMPORTANT:
                 *
                 * WildStringBase reads Projectile.localAI[1]
                 * every frame, so use the helper instead of only
                 * changing the local variable.
                 */

                SetWildStringSpin(
                    reboundAngularSpeed
                );

                if (
                    reboundHits >=
                    ReboundMaxHits
                )
                {
                    EndRebound();
                }

                return;
            }

            // RETRACTION HIT

            if (retracting)
            {
                return;
            }

            // NORMAL HIT

            normalHits++;

            if (
                tensionPoints <
                MaximumTension
            )
            {
                tensionPoints++;
            }

            Projectile.netUpdate =
                true;

            if (
                normalHits >=
                NormalMaxHits
            )
            {
                BeginRetraction();
            }
        }

        // BEGIN RETRACTION

        private void BeginRetraction()
        {
            if (
                retracting ||
                rebounding
            )
            {
                return;
            }

            retracting =
                true;

            retractTimer =
                0;

            retractStartPosition =
                Projectile.Center;

            Projectile.timeLeft =
                999999;

            Projectile.velocity =
                Vector2.Zero;

            Projectile.scale =
                1f;

            Projectile.netUpdate =
                true;
        }

        // RETRACTION

        private void UpdateRetraction()
        {
            if (
                Projectile.owner < 0 ||
                Projectile.owner >= Main.maxPlayers
            )
            {
                Projectile.Kill();
                return;
            }

            Player player =
                Main.player[
                    Projectile.owner
                ];

            if (
                player == null ||
                !player.active ||
                player.dead
            )
            {
                Projectile.Kill();
                return;
            }

            retractTimer++;

            float progress =
                MathHelper.Clamp(
                    retractTimer /
                    (float)RetractionDuration,
                    0f,
                    1f
                );

            Projectile.Center =
                Vector2.Lerp(
                    retractStartPosition,
                    player.MountedCenter,
                    progress
                );

            Projectile.velocity =
                Vector2.Zero;

            Projectile.scale =
                1f -
                progress *
                0.85f;

            if (
                progress >=
                1f
            )
            {
                Projectile.Center =
                    player.MountedCenter;

                Projectile.scale =
                    1f;

                BeginRebound();
            }
        }

        // BEGIN REBOUND

        private void BeginRebound()
        {
            if (rebounding)
            {
                return;
            }

            Player player =
                Main.player[
                    Projectile.owner
                ];

            if (
                player == null ||
                !player.active ||
                player.dead
            )
            {
                Projectile.Kill();
                return;
            }

            // STATE

            rebounding =
                true;

            retracting =
                false;

            reboundTimer =
                0;

            reboundHits =
                0;

            // TENSION

            int storedTension =
                tensionPoints;

            tensionPoints =
                0;

            // RANDOM REBOUND DISTANCE

            reboundDistance =
                Main.rand.NextFloat(
                    MinimumReboundDistance,
                    MaximumReboundDistance
                );

            // REBOUND SPEED

            float reboundSpeedDegrees =
                1f +
                storedTension * 3f;

            reboundAngularSpeed =
                MathHelper.ToRadians(
                    reboundSpeedDegrees
                );

            // REBOUND DURATION

            int reboundSeconds =
                1 +
                storedTension * 3;

            reboundDuration =
                reboundSeconds * 60;

            // RANDOM START ANGLE

            float reboundAngle =
                Main.rand.NextFloat(
                    0f,
                    MathHelper.TwoPi
                );

            /*
             * IMPORTANT:
             *
             * Rebound now uses the SAME WildStringBase physical
             * rope simulation.
             *
             * The helper resets the physical rope to the random
             * rebound configuration instead of creating a separate
             * fake projectile orbit.
             */

            ResetWildStringPhysics(
                reboundDistance,
                reboundAngularSpeed,
                reboundAngle
            );

            Projectile.timeLeft =
                999999;

            Projectile.scale =
                1f;

            Projectile.netUpdate =
                true;
        }

        // UPDATE REBOUND

        private void UpdateRebound()
        {
            if (
                Projectile.owner < 0 ||
                Projectile.owner >= Main.maxPlayers
            )
            {
                Projectile.Kill();
                return;
            }

            Player player =
                Main.player[
                    Projectile.owner
                ];

            if (
                player == null ||
                !player.active ||
                player.dead
            )
            {
                Projectile.Kill();
                return;
            }

            reboundTimer++;

            /*
             * WildStringBase now handles the actual rope motion.
             *
             * We temporarily give it the rebound speed/distance.
             */

            base.AI();

            /*
             * Stop based on the custom rebound timer.
             */

            if (
                reboundTimer >=
                reboundDuration
            )
            {
                EndRebound();
                return;
            }

            if (
                reboundHits >=
                ReboundMaxHits
            )
            {
                EndRebound();
                return;
            }
        }

        // END REBOUND

        private void EndRebound()
        {
            rebounding =
                false;

            reboundTimer =
                0;

            reboundHits =
                0;

            reboundDistance =
                0f;

            reboundAngularSpeed =
                0f;

            Projectile.Kill();
        }
    }
}