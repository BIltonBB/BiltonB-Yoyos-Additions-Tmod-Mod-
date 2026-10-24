using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class EmpressYoyoLanceController : ModProjectile
    {
        public enum Mode
        {
            A1 = 1,
            A2 = 2,
            A3 = 3,
            A4 = 4
        }

        private bool configuredExternally;
        private float externalDistance;
        private int externalGapFrames;
        private int externalCount;
        private float externalAngleGapDegrees;

        private int spawnTimer;
        private int spawnedCount;
        private bool initialized;

        private Mode AttackMode => (Mode)(int)Projectile.ai[0];
        private int TargetIndex => (int)Projectile.ai[1];
        private float BaseAngle => Projectile.ai[2];

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            Projectile.hide = true;
        }

        public void ConfigureExternal(
            float distance,
            int gapFrames,
            int count,
            float angleGapDegrees)
        {
            externalDistance = distance;
            externalGapFrames = Math.Max(0, gapFrames);
            externalCount = Math.Max(1, count);
            externalAngleGapDegrees = angleGapDegrees;
            configuredExternally = true;
        }

        public override void AI()
        {
            if (!configuredExternally)
                ConfigureFromMode();

            if (!initialized)
            {
                initialized = true;
                Projectile.netUpdate = true;
            }

            if (Main.myPlayer != Projectile.owner)
                return;

            NPC target = GetTarget();
            if (target == null)
            {
                Projectile.Kill();
                return;
            }

            // Always follow the target so patterns stay centered on the NPC.
            Projectile.Center = target.Center;

            switch (AttackMode)
            {
                case Mode.A1:
                    if (spawnedCount == 0)
                    {
                        SpawnSingleLance(
                            target,
                            BaseAngle,
                            externalDistance,
                            true
                        );
                        spawnedCount = 1;
                    }

                    Projectile.Kill();
                    return;

                case Mode.A2:
                case Mode.A4:
                    HandleOrbitalPattern(target);
                    return;

                case Mode.A3:
                    SpawnA3Formation(target);
                    Projectile.Kill();
                    return;
            }
        }

        private void HandleOrbitalPattern(NPC target)
        {
            if (spawnedCount >= externalCount)
            {
                Projectile.Kill();
                return;
            }

            if (spawnTimer > 0)
            {
                spawnTimer--;
                return;
            }

            float angle;

            if (AttackMode == Mode.A2)
            {
                // Exactly 5 ELs:
                // -8°, -4°, 0°, +4°, +8°.
                int step = spawnedCount - 2;

                angle =
                    BaseAngle +
                    MathHelper.ToRadians(
                        step * externalAngleGapDegrees
                    );
            }
            else
            {
                // Exactly 72 ELs, every 5°, complete ring.
                angle =
                    BaseAngle +
                    MathHelper.ToRadians(
                        spawnedCount * externalAngleGapDegrees
                    );
            }

            SpawnSingleLance(
                target,
                angle,
                externalDistance,
                false
            );

            spawnedCount++;
            spawnTimer = externalGapFrames;
        }

        private void ConfigureFromMode()
        {
            switch (AttackMode)
            {
                case Mode.A1:
                    ConfigureExternal(300f, 0, 1, 0f);
                    break;

                case Mode.A2:
                    ConfigureExternal(500f, 6, 5, 4f);
                    break;

                case Mode.A3:
                    // Wider than A2 so the formation is visually obvious.
                    ConfigureExternal(500f, 0, 7, 12f);
                    break;

                case Mode.A4:
                    ConfigureExternal(500f, 6, 72, 5f);
                    break;
            }
        }

        private NPC GetTarget()
        {
            if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[TargetIndex];

            if (!npc.active ||
                npc.friendly ||
                npc.dontTakeDamage ||
                npc.life <= 0)
            {
                return null;
            }

            return npc;
        }

        private void SpawnSingleLance(
            NPC target,
            float angle,
            float radius,
            bool isA1)
        {
            Vector2 offset =
                Vector2.UnitX.RotatedBy(angle) * radius;

            Vector2 spawnPosition =
                target.Center + offset;

            Vector2 direction =
                target.Center - spawnPosition;

            if (direction.LengthSquared() < 0.001f)
            {
                direction = -Vector2.UnitX;
            }
            else
            {
                direction.Normalize();
            }

            // A1 is intentionally imperfect.
            if (isA1 && Main.rand.NextBool(2))
            {
                float offsetDegrees =
                    Main.rand.NextFloat(1f, 2f);

                if (Main.rand.NextBool())
                    offsetDegrees *= -1f;

                direction = direction.RotatedBy(
                    MathHelper.ToRadians(offsetDegrees)
                );
            }

            const float speed = 18f;

            int colorMode = Main.dayTime
                ? 2
                : (Main.rand.NextBool() ? 0 : 1);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                spawnPosition,
                direction * speed,
                ModContent.ProjectileType<EmpressYoyoProjEL>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner,
                TargetIndex,
                speed,
                colorMode
            );
        }

        private void SpawnA3Formation(NPC target)
        {
            // A3 = one center EL at the normal orbit distance, plus
            // three ELs 10px to each side. The seven ELs are NOT placed
            // around the orbit. They form a straight firing formation.
            // All seven use exactly the same firing direction and fire
            // simultaneously.

            Vector2 radial = BaseAngle.ToRotationVector2();

            Vector2 centerPosition =
                target.Center +
                radial * externalDistance;

            Vector2 direction =
                target.Center - centerPosition;

            if (direction.LengthSquared() < 0.001f)
                direction = -radial;
            else
                direction.Normalize();

            Vector2 perpendicular =
                new Vector2(
                    -direction.Y,
                    direction.X
                );

            const float spacing = 10f;

            for (int i = -3; i <= 3; i++)
            {
                Vector2 spawnPosition =
                    centerPosition +
                    perpendicular * (i * spacing);

                SpawnSingleLanceAtPosition(
                    target,
                    spawnPosition,
                    direction
                );
            }
        }

        private void SpawnSingleLanceAtPosition(
            NPC target,
            Vector2 spawnPosition,
            Vector2 direction)
        {
            const float speed = 18f;

            int colorMode =
                Main.dayTime
                    ? 2
                    : (Main.rand.NextBool() ? 0 : 1);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                spawnPosition.X,
                spawnPosition.Y,
                direction.X * speed,
                direction.Y * speed,
                ModContent.ProjectileType<EmpressYoyoProjEL>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner,
                TargetIndex,
                speed,
                colorMode
            );
        }
    }
}
