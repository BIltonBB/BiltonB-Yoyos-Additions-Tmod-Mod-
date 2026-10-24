using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public static class EmpressYoyoAttackHelperA
    {
        private const float A1Distance = 300f;
        private const float A2Distance = 500f;
        private const float A3Distance = 500f;
        private const int A2GapFrames = 6;
        private const int A4GapFrames = 6;

        public static void PerformA1(Projectile parent, NPC target)
        {
            if (!CanAttack(parent, target))
                return;

            float spawnAngle =
                Main.rand.NextFloat(MathHelper.TwoPi);

            StartLanceController(
                parent,
                target,
                EmpressYoyoLanceController.Mode.A1,
                A1Distance,
                spawnAngle,
                0,
                1,
                1f
            );
        }

        public static void PerformA2(
            Projectile parent,
            NPC target,
            float damageMultiplier)
        {
            if (!CanAttack(parent, target))
                return;

            // Exactly 5 ELs, 4° apart:
            // -8°, -4°, 0°, +4°, +8°.
            float baseAngle =
                Main.rand.NextFloat(MathHelper.TwoPi);

            StartLanceController(
                parent,
                target,
                EmpressYoyoLanceController.Mode.A2,
                A2Distance,
                baseAngle,
                A2GapFrames,
                5,
                damageMultiplier
            );
        }

        public static void PerformA3(
            Projectile parent,
            NPC target,
            float damageMultiplier)
        {
            if (!CanAttack(parent, target))
                return;

            // 7 EL formation, 12° between neighboring lances.
            int directionIndex = Main.rand.Next(8);
            float directionAngle =
                MathHelper.ToRadians(
                    directionIndex * 45f
                );

            StartLanceController(
                parent,
                target,
                EmpressYoyoLanceController.Mode.A3,
                A3Distance,
                directionAngle,
                0,
                7,
                damageMultiplier
            );
        }

        public static void PerformA4(
            Projectile parent,
            NPC target)
        {
            if (!CanAttack(parent, target))
                return;

            StartLanceController(
                parent,
                target,
                EmpressYoyoLanceController.Mode.A4,
                500f,
                0f,
                A4GapFrames,
                72,
                1f
            );
        }

        private static void StartLanceController(
            Projectile parent,
            NPC target,
            EmpressYoyoLanceController.Mode mode,
            float distance,
            float baseAngle,
            int gapFrames,
            int count,
            float damageMultiplier)
        {
            int damage = Math.Max(
                1,
                (int)Math.Round(
                    parent.damage * damageMultiplier
                )
            );

            int controllerType =
                ModContent.ProjectileType<EmpressYoyoLanceController>();

            int controllerIndex =
                Projectile.NewProjectile(
                    parent.GetSource_FromThis(),
                    target.Center,
                    Vector2.Zero,
                    controllerType,
                    damage,
                    parent.knockBack,
                    parent.owner,
                    (float)mode,
                    target.whoAmI,
                    baseAngle
                );

            if (controllerIndex < 0 ||
                controllerIndex >= Main.maxProjectiles)
                return;

            var controller =
                Main.projectile[controllerIndex].ModProjectile
                    as EmpressYoyoLanceController;

            float angleGap = mode switch
            {
                EmpressYoyoLanceController.Mode.A2 => 4f,
                EmpressYoyoLanceController.Mode.A3 => 12f,
                EmpressYoyoLanceController.Mode.A4 => 5f,
                _ => 0f
            };

            controller?.ConfigureExternal(
                distance,
                gapFrames,
                count,
                angleGap
            );

            Main.projectile[controllerIndex].netUpdate = true;
        }

        private static bool CanAttack(
            Projectile parent,
            NPC target)
        {
            return parent != null &&
                   parent.active &&
                   target != null &&
                   target.active &&
                   !target.friendly &&
                   !target.dontTakeDamage &&
                   target.life > 0;
        }
    }
}
