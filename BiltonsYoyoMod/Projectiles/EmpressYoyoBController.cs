using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class EmpressYoyoBController : ModProjectile
    {
        private int timer;
        private int spawned;
        private bool initialized;
        private int targetIndex = -1;
        private int parentIndex = -1;
        private int maxCount;
        private float speedMultiplier;
        private int shotsPerTick;
        private int durationFrames;

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
            Projectile.hide = true;
        }

        public void ConfigureParent(int index)
        {
            parentIndex = index;
        }

        public override void AI()
        {
            if (!initialized)
            {
                initialized = true;
                targetIndex = (int)Projectile.ai[0];
                maxCount = Math.Max(1, (int)Projectile.ai[1]);
                speedMultiplier = Math.Max(0.1f, Projectile.ai[2]);

                shotsPerTick = maxCount >= 60 ? 2 : 1;
                durationFrames = maxCount >= 60 ? 180 : maxCount * 6;
            }

            if (Main.myPlayer != Projectile.owner)
                return;

            Projectile parent = GetParent();
            if (parent == null)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = parent.Center;

            // A B attack is allowed to exist without a nearby target.
            // PBs simply remain on their post-turn straight trajectory.
            NPC target = GetTarget();

            if (timer >= durationFrames || spawned >= maxCount)
            {
                Projectile.Kill();
                return;
            }

            if (timer % 6 == 0)
            {
                for (int i = 0;
                     i < shotsPerTick && spawned < maxCount;
                     i++)
                {
                    SpawnPB(parent, target);
                    spawned++;
                }
            }

            timer++;
        }

        private Projectile GetParent()
        {
            if (parentIndex < 0 ||
                parentIndex >= Main.maxProjectiles)
                return null;

            Projectile parent = Main.projectile[parentIndex];

            if (!parent.active ||
                parent.owner != Projectile.owner ||
                parent.type != ModContent.ProjectileType<EmpressYoyoProj>())
                return null;

            return parent;
        }

        private NPC GetTarget()
        {
            if (targetIndex < 0 ||
                targetIndex >= Main.maxNPCs)
                return null;

            NPC target = Main.npc[targetIndex];

            if (!target.active ||
                target.friendly ||
                target.dontTakeDamage ||
                target.life <= 0)
                return null;

            return target;
        }

        private void SpawnPB(
            Projectile parent,
            NPC target)
        {
            // The PB ALWAYS launches in a completely random direction.
            // Its 5-15 degree turn is handled by PB itself during its
            // first second.
            float randomAngle =
                Main.rand.NextFloat(MathHelper.TwoPi);

            Vector2 initialDirection =
                randomAngle.ToRotationVector2();

            const float initialSpeed = 18f;

            int colorMode = Main.dayTime
                ? 2
                : (Main.rand.NextBool() ? 0 : 1);

            // -1 means there is no locked target.
            int targetIndexForPB =
                target?.whoAmI ?? -1;

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                parent.Center,
                initialDirection *
                    initialSpeed *
                    speedMultiplier,
                ModContent.ProjectileType<EmpressYoyoProjPB>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner,
                targetIndexForPB,
                14f * speedMultiplier,
                colorMode
            );
        }
    }
}
