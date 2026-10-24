using System;
using Terraria;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public static class PrimeYoyoHelper
    {
        public const float DetectionRadius = 600f;

        // TARGET FINDING

        public static int FindNearbyTarget(
            Vector2 center,
            float radius)
        {
            int bestTarget = -1;
            float bestDistance = radius * radius;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active ||
                    npc.friendly ||
                    npc.dontTakeDamage ||
                    npc.life <= 0 ||
                    npc.immortal)
                {
                    continue;
                }

                float distance =
                    Vector2.DistanceSquared(
                        center,
                        npc.Center
                    );

                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = i;
                }
            }

            return bestTarget;
        }

        public static bool ValidTarget(
            int targetIndex)
        {
            if (targetIndex < 0 ||
                targetIndex >= Main.maxNPCs)
            {
                return false;
            }

            NPC npc =
                Main.npc[targetIndex];

            return npc.active &&
                   !npc.friendly &&
                   !npc.dontTakeDamage &&
                   npc.life > 0 &&
                   !npc.immortal;
        }


        // DIRECTIONAL IDLE ROTATION

        /*
         * Hand orientation:
         *
         * Moving right  -> +45°
         * Moving left   -> -45°
         * Moving mostly vertically -> 0°
         *
         * This is intended for hands that are idle.
         */
        public static float GetHandRotation(
            Vector2 velocity)
        {
            if (velocity.LengthSquared() < 0.25f)
                return 0f;

            if (Math.Abs(velocity.X) >
                Math.Abs(velocity.Y))
            {
                if (velocity.X > 0f)
                    return MathHelper.PiOver4;

                return -MathHelper.PiOver4;
            }

            return 0f;
        }


        // HAND SCALE

        /*
         * All Prime hands are rendered at half size.
         */
        public const float HandScale = 0.5f;
    }
}