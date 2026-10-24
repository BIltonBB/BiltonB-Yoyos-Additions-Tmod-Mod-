using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class FingerLoopGlobalProjectile
        : GlobalProjectile
    {
        // INSTANCE

        public override bool InstancePerEntity =>
            true;

        // SNAP

        private const int SnapInterval =
            5 * 60;

        private int snapTimer;

        // EXTRA BEARING EVENT 1

        private bool bearingEventOneActive;
        private bool bearingEventOneResolved;

        private int bearingEventOneTimer;
        private float bearingEventOneChance;

        // EXTRA BEARING EVENT 2

        private bool bearingEventTwoActive;
        private bool bearingEventTwoResolved;

        private int bearingEventTwoTimer;
        private float bearingEventTwoChance;

        // INITIALIZED

        private bool initialized;

        // SPAWN

        public override void OnSpawn(
            Projectile projectile,
            IEntitySource source)
        {
            /*
             * IMPORTANT:
             *
             * Finger Loop identifies yoyos by aiStyle == ProjAIStyleID.Yoyo.
             *
             * It does NOT inspect YoyosLifeTimeMultiplier.
             *
             * Therefore an infinite-lifetime yoyo is still valid.
             */

            if (
                projectile.aiStyle != ProjAIStyleID.Yoyo
            )
            {
                return;
            }

            if (
                projectile.owner < 0 ||
                projectile.owner >=
                Main.maxPlayers
            )
            {
                return;
            }

            Player player =
                Main.player[
                    projectile.owner
                ];

            if (!player.active)
                return;

            FingerLoopPlayer finger =
                player.GetModPlayer<
                    FingerLoopPlayer>();

            if (
                finger.GetTier() <= 0
            )
            {
                return;
            }

            initialized =
                true;

            snapTimer = 0;

            // APPLY YOYO EFFECTS

            finger.ApplyYoyoEffects(
                projectile
            );

            // EXPERTLY WRAPPED

            if (
                finger.ExpertlyWrapped &&
                !finger.MasterfullyWrapped
            )
            {
                bearingEventOneActive =
                    true;

                bearingEventOneResolved =
                    false;

                bearingEventOneTimer =
                    Main.rand.Next(
                        3 * 60,
                        10 * 60 + 1
                    );

                bearingEventOneChance =
                    0.50f;
            }

            // MASTERFULLY WRAPPED

            if (
                finger.MasterfullyWrapped
            )
            {
                /*
                 * FIRST:
                 *
                 * 3-5 seconds
                 * 75%
                 */

                bearingEventOneActive =
                    true;

                bearingEventOneResolved =
                    false;

                bearingEventOneTimer =
                    Main.rand.Next(
                        3 * 60,
                        5 * 60 + 1
                    );

                bearingEventOneChance =
                    0.75f;

                /*
                 * SECOND EVENT:
                 *
                 * Not running yet.
                 *
                 * It starts after the first event resolves.
                 *
                 * Then waits 2-20 seconds and gets its own 50% roll.
                 */

                bearingEventTwoActive =
                    false;

                bearingEventTwoResolved =
                    false;
            }
        }

        // AI

        public override void AI(
            Projectile projectile)
        {
            if (
                !initialized ||
                !projectile.active ||
                projectile.aiStyle != ProjAIStyleID.Yoyo
            )
            {
                return;
            }

            if (
                projectile.owner < 0 ||
                projectile.owner >=
                Main.maxPlayers
            )
            {
                return;
            }

            Player player =
                Main.player[
                    projectile.owner
                ];

            if (!player.active)
                return;

            /*
             * Owner/server performs random rolls.
             */

            bool authority =
                Main.netMode ==
                NetmodeID.Server ||
                projectile.owner ==
                Main.myPlayer;

            if (!authority)
                return;

            FingerLoopPlayer finger =
                player.GetModPlayer<
                    FingerLoopPlayer>();

            // EVENT 1

            if (
                bearingEventOneActive &&
                !bearingEventOneResolved
            )
            {
                bearingEventOneTimer--;

                if (
                    bearingEventOneTimer <= 0
                )
                {
                    bearingEventOneResolved =
                        true;

                    // ROLL EVENT 1

                    if (
                        Main.rand.NextFloat() <
                        bearingEventOneChance
                    )
                    {
                        /*
                         * SPAWN FROM THE YOYO.
                         */

                        player
                            .GetModPlayer<
                                Common.LooseBearingPlayer>()
                            .TriggerBearingFromFingerLoop(
                                projectile.Center
                            );
                    }

                    // MASTER SECOND EVENT

                    if (
                        finger.MasterfullyWrapped
                    )
                    {
                        bearingEventTwoActive =
                            true;

                        bearingEventTwoResolved =
                            false;

                        bearingEventTwoTimer =
                            Main.rand.Next(
                                2 * 60,
                                20 * 60 + 1
                            );

                        bearingEventTwoChance =
                            0.50f;
                    }
                }
            }

            // EVENT 2

            if (
                bearingEventTwoActive &&
                !bearingEventTwoResolved
            )
            {
                bearingEventTwoTimer--;

                if (
                    bearingEventTwoTimer <= 0
                )
                {
                    bearingEventTwoResolved =
                        true;

                    /*
                     * Completely independent of Event 1.
                     */

                    if (
                        Main.rand.NextFloat() <
                        bearingEventTwoChance
                    )
                    {
                        player
                            .GetModPlayer<
                                Common.LooseBearingPlayer>()
                            .TriggerBearingFromFingerLoop(
                                projectile.Center
                            );
                    }
                }
            }

            // SNAP TIMER

            snapTimer++;

            if (
                snapTimer <
                SnapInterval
            )
            {
                return;
            }

            snapTimer = 0;

            /*
             * SNAP CHANCE:
             *
             * Terribly    50%
             * Nicely      25%
             * Expertly     5%
             * Masterfully  0%
             */

            float chance =
                finger.GetSnapChance();

            if (chance <= 0f)
                return;

            if (
                Main.rand.NextFloat() >=
                chance
            )
            {
                return;
            }

            /*
             * SUCCESS:
             *
             * Current yoyo -> FingerLoopCloneProjectile
             * Current velocity copied
             * Current position copied
             * Current type copied
             * Current damage copied
             * Current max distance copied
             * Clone damage = 2x
             * Original yoyo deleted
             */

            finger.TrySnap(
                projectile
            );
        }
    }
}