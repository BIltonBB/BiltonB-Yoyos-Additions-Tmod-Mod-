using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Projectiles
{
    public class WildStringsAKProj :
        WildStringBase
    {
        // TEXTURE

        public override string Texture =>
            "BiltonsYoyoMod/Projectiles/WildStringsAKProj";

        // STRING SETTINGS

        protected override int MinimumDistance =>
            180;

        protected override int MaximumDistance =>
            245;

        protected override float MinimumSpeedDegrees =>
            5f;

        protected override float MaximumSpeedDegrees =>
            6f;

        private const int StringLifetime =
            35 * 60;

        // RETRACTION

        private const int RetractionDuration =
            12;

        private bool retracting;

        private int retractTimer;

        private Vector2 retractStartPosition;

        // KEVLAR

        private const float LooseBearingChance =
            0.10f;

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
            if (retracting)
            {
                UpdateRetraction();
                return;
            }

            if (
                Projectile.timeLeft <=
                1
            )
            {
                BeginRetraction();
                return;
            }

            /*
             * Normal Wild String rope physics.
             */

            base.AI();
        }

        // KEVLAR HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            if (
                Projectile.owner < 0 ||
                Projectile.owner >= Main.maxPlayers
            )
            {
                return;
            }

            Player player =
                Main.player[
                    Projectile.owner
                ];

            if (
                player == null ||
                !player.active
            )
            {
                return;
            }

            /*
             * Only the owner performs the random roll.
             */

            if (
                Main.netMode ==
                    NetmodeID.MultiplayerClient &&
                Main.myPlayer !=
                    Projectile.owner
            )
            {
                return;
            }

            // KEVLAR 10%

            if (
                Main.rand.NextFloat() >=
                LooseBearingChance
            )
            {
                return;
            }

            /*
             * Bearing comes from the KEVLAR STRING ENDPOINT.
             *
             * It does not spawn from the player.
             */

            player.GetModPlayer<
                LooseBearingPlayer>()
                .TriggerBearingFromPosition(
                    Projectile.Center
                );
        }

        // BEGIN RETRACTION

        private void BeginRetraction()
        {
            if (retracting)
                return;

            retracting =
                true;

            retractTimer =
                0;

            retractStartPosition =
                Projectile.Center;

            Projectile.timeLeft =
                999999;

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
                Projectile.Kill();
                return;
            }
        }

        // TILE COLLISION

        /*
         * IMPORTANT:
         *
         * Tile contact DOES NOT start Kevlar's retraction anymore.
         *
         * Kevlar still retracts when its normal lifetime expires,
         * exactly as intended.
         */

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            return base.OnTileCollide(
                oldVelocity
            );
        }
    }
}