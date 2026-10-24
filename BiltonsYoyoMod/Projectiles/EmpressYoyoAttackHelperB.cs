using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public static class EmpressYoyoAttackHelperB
    {
        public static void StartB(
            Projectile parent,
            int count,
            float damageMultiplier,
            float speedMultiplier)
        {
            if (parent == null || !parent.active || Main.myPlayer != parent.owner)
                return;

            NPC target = FindNearestEnemy(parent.Center, 1400f);
            int targetIndex = target?.whoAmI ?? -1;

            int damage = Math.Max(
                1,
                (int)Math.Round(parent.damage * damageMultiplier)
            );

            int controllerIndex = Projectile.NewProjectile(
                parent.GetSource_FromThis(),
                parent.Center,
                Vector2.Zero,
                ModContent.ProjectileType<EmpressYoyoBController>(),
                damage,
                parent.knockBack,
                parent.owner,
                targetIndex,
                count,
                speedMultiplier
            );

            if (controllerIndex < 0 || controllerIndex >= Main.maxProjectiles)
                return;

            EmpressYoyoBController controller =
                Main.projectile[controllerIndex].ModProjectile as EmpressYoyoBController;

            controller?.ConfigureParent(parent.whoAmI);
            Main.projectile[controllerIndex].netUpdate = true;
        }

        public static void StartC(
            Projectile parent,
            float damageMultiplier)
        {
            int damage = Math.Max(
                1,
                (int)Math.Round(parent.damage * damageMultiplier)
            );

            int projectileType = ModContent.ProjectileType<EmpressYoyoProjER>();

            for (int i = 0; i < 12; i++)
            {
                float angle = MathHelper.ToRadians(i * 30f);
                Vector2 velocity = Vector2.UnitX.RotatedBy(angle) * 7f;

                int colorMode = Main.dayTime
                    ? 2
                    : (Main.rand.NextBool() ? 0 : 1);

                Projectile.NewProjectile(
                    parent.GetSource_FromThis(),
                    parent.Center,
                    velocity,
                    projectileType,
                    damage,
                    parent.knockBack,
                    parent.owner,
                    colorMode
                );
            }
        }

        private static NPC FindNearestEnemy(Vector2 center, float maxDistance)
        {
            NPC nearest = null;
            float bestDistanceSquared = maxDistance * maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.life <= 0)
                    continue;

                float distanceSquared = Vector2.DistanceSquared(center, npc.Center);
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    nearest = npc;
                }
            }

            return nearest;
        }
    }
}
