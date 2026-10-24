using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Players
{
    public class FingerLoopPlayer : ModPlayer
    {
        // FINGER LOOP TIERS

        public bool TerriblyWrapped;
        public bool NicelyWrapped;
        public bool ExpertlyWrapped;
        public bool MasterfullyWrapped;

        // CONSTANTS

        private const int TerriblyWrappedLifetime =
            20 * 60;

        // RESET

        public override void ResetEffects()
        {
            TerriblyWrapped = false;
            NicelyWrapped = false;
            ExpertlyWrapped = false;
            MasterfullyWrapped = false;
        }

        // TIER

        public int GetTier()
        {
            if (MasterfullyWrapped)
                return 4;

            if (ExpertlyWrapped)
                return 3;

            if (NicelyWrapped)
                return 2;

            if (TerriblyWrapped)
                return 1;

            return 0;
        }

        // SNAP CHANCE

        /*
         * SNAP = the yoyo-cloning event.
         *
         * Terribly   = 50%
         * Nicely     = 25%
         * Expertly   = 5%
         * Masterfully = 0%
         */

        public float GetSnapChance()
        {
            switch (GetTier())
            {
                case 4:
                    return 0f;

                case 3:
                    return 0.05f;

                case 2:
                    return 0.25f;

                case 1:
                    return 0.50f;

                default:
                    return 0f;
            }
        }

        // DAMAGE

        public float GetDamageMultiplier()
        {
            switch (GetTier())
            {
                case 4:
                    return 1.15f;

                case 3:
                    return 1.10f;

                case 2:
                    return 1.05f;

                default:
                    return 1f;
            }
        }

        // LIFETIME BONUS

        public int GetLifetimeBonus()
        {
            switch (GetTier())
            {
                case 4:
                    return 12 * 60;

                case 3:
                    return 8 * 60;

                case 2:
                    return 5 * 60;

                default:
                    return 0;
            }
        }

        // EFFECTIVE WRAPPED LIFETIME

        /*
         * Terribly Wrapped establishes a 20-second minimum lifetime.
         *
         * Higher tiers add:
         *
         * Nicely      +5s
         * Expertly    +8s
         * Masterfully +12s
         *
         * Infinite / effectively infinite yoyos are left untouched.
         */

        public void ApplyYoyoLifetime(
            Projectile yoyo)
        {
            if (
                yoyo == null ||
                !yoyo.active ||
                yoyo.aiStyle != ProjAIStyleID.Yoyo
            )
            {
                return;
            }

            /*
             * Prevent overflow on infinite-lifetime yoyos.
             *
             * Some of your custom yoyos use a huge timeLeft value.
             */

            if (
                yoyo.timeLeft >=
                int.MaxValue - 3600
            )
            {
                return;
            }

            /*
             * Terribly Wrapped establishes 20 seconds.
             */

            if (TerriblyWrapped)
            {
                if (
                    yoyo.timeLeft <
                    TerriblyWrappedLifetime
                )
                {
                    yoyo.timeLeft =
                        TerriblyWrappedLifetime;
                }
            }

            /*
             * Higher-tier additions.
             */

            int bonus =
                GetLifetimeBonus();

            if (bonus <= 0)
                return;

            if (
                yoyo.timeLeft >=
                int.MaxValue - bonus
            )
            {
                return;
            }

            yoyo.timeLeft += bonus;
        }

        // DAMAGE

        public void ApplyYoyoDamage(
            Projectile yoyo)
        {
            if (
                yoyo == null ||
                !yoyo.active ||
                yoyo.aiStyle != ProjAIStyleID.Yoyo
            )
            {
                return;
            }

            float multiplier =
                GetDamageMultiplier();

            if (multiplier <= 1f)
                return;

            yoyo.damage =
                System.Math.Max(
                    1,
                    (int)(
                        yoyo.damage *
                        multiplier
                    )
                );
        }

        // APPLY ALL FINGER LOOP EFFECTS

        public void ApplyYoyoEffects(
            Projectile yoyo)
        {
            if (
                yoyo == null ||
                !yoyo.active ||
                yoyo.aiStyle != ProjAIStyleID.Yoyo
            )
            {
                return;
            }

            ApplyYoyoDamage(
                yoyo
            );

            ApplyYoyoLifetime(
                yoyo
            );
        }

        // SNAP

        public bool TrySnap(
            Projectile yoyo)
        {
            if (
                yoyo == null ||
                !yoyo.active ||
                yoyo.aiStyle != ProjAIStyleID.Yoyo
            )
            {
                return false;
            }

            float chance =
                GetSnapChance();

            if (chance <= 0f)
                return false;

            if (
                Main.rand.NextFloat() >=
                chance
            )
            {
                return false;
            }

            // CURRENT YOYO STATE

            int sourceType =
                yoyo.type;

            Vector2 sourcePosition =
                yoyo.Center;

            Vector2 sourceVelocity =
                yoyo.velocity;

            int sourceDamage =
                yoyo.damage;

            float sourceKnockBack =
                yoyo.knockBack;

            float sourceScale =
                yoyo.scale;

            float sourceRotation =
                yoyo.rotation;

            // CURRENT MAX DISTANCE

            float sourceMaxDistance =
                0f;

            if (
                sourceType >= 0 &&
                sourceType <
                ProjectileID
                    .Sets
                    .YoyosMaximumRange
                    .Length
            )
            {
                sourceMaxDistance =
                    ProjectileID
                        .Sets
                        .YoyosMaximumRange[
                            sourceType
                        ];
            }

            /*
             * Fallback if the set has no usable value.
             *
             * The clone still works; it simply has no meaningful
             * string-length limit.
             */

            if (
                sourceMaxDistance < 1f
            )
            {
                sourceMaxDistance =
                    16f;
            }

            // FIND SAFE POSITION

            Vector2 clonePosition =
                FindSafeClonePosition(
                    sourcePosition,
                    sourceVelocity,
                    yoyo.width,
                    yoyo.height
                );

            // 2X DAMAGE

            int cloneDamage =
                System.Math.Max(
                    1,
                    sourceDamage * 2
                );

            // SPAWN

            int cloneIndex =
                Projectile.NewProjectile(
                    yoyo.GetSource_FromThis(),
                    clonePosition,
                    sourceVelocity,
                    ModContent.ProjectileType<
                        FingerLoopCloneProjectile>(),
                    cloneDamage,
                    sourceKnockBack,
                    yoyo.owner,
                    sourceType,
                    sourceMaxDistance
                );

            if (
                cloneIndex >= 0 &&
                cloneIndex <
                Main.maxProjectiles
            )
            {
                Projectile clone =
                    Main.projectile[
                        cloneIndex
                    ];

                clone.damage =
                    cloneDamage;

                clone.knockBack =
                    sourceKnockBack;

                clone.scale =
                    sourceScale;

                clone.rotation =
                    sourceRotation;

                clone.netUpdate =
                    true;
            }

            // ORIGINAL YOYO IS DELETED

            yoyo.Kill();

            return true;
        }

        // SAFE CLONE POSITION

        private static Vector2
            FindSafeClonePosition(
                Vector2 position,
                Vector2 velocity,
                int width,
                int height)
        {
            Vector2 topLeft =
                position -
                new Vector2(
                    width * 0.5f,
                    height * 0.5f
                );

            if (
                !Collision.SolidCollision(
                    topLeft,
                    width,
                    height
                )
            )
            {
                return position;
            }

            Vector2 direction =
                velocity.SafeNormalize(
                    Vector2.UnitY
                );

            /*
             * Try positions backward along the yoyo's velocity.
             */

            for (
                int i = 1;
                i <= 24;
                i++
            )
            {
                Vector2 test =
                    position -
                    direction *
                    (i * 4f);

                Vector2 testTopLeft =
                    test -
                    new Vector2(
                        width * 0.5f,
                        height * 0.5f
                    );

                if (
                    !Collision.SolidCollision(
                        testTopLeft,
                        width,
                        height
                    )
                )
                {
                    return test;
                }
            }

            /*
             * Last fallback.
             *
             * OnTileCollide will immediately resolve the situation.
             */

            return position;
        }
    }
}