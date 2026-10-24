using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.ID;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class SolarYoyoProj : ModProjectile
    {
        // SETTINGS

        private const int FSpawnInterval = 15;     // 0.25 seconds
        private const int WSpawnInterval = 75;    // 1.25 seconds

        private const float WSearchRange = 800f;

        // W starts exactly 300 pixels away from its target.
        private const float WSpawnDistance = 300f;


        // STATE

        private int fSpawnTimer;
        private int wSpawnTimer;

        private bool initialized;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;

            Projectile.aiStyle =
                ProjAIStyleID.Yoyo;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;


            ProjectileID.Sets.YoyosMaximumRange[
                Projectile.type
            ] = 380f;


            ProjectileID.Sets.YoyosTopSpeed[
                Projectile.type
            ] = 18f;


            ProjectileID.Sets.YoyosLifeTimeMultiplier[
                Projectile.type
            ] = -1f;
        }


        // AI

        public override void AI()
        {
            Lighting.AddLight(
                Projectile.Center,
                0.65f,
                0.25f,
                0.05f
            );


            // INITIALIZE

            if (!initialized)
            {
                initialized = true;

                fSpawnTimer = 0;
                wSpawnTimer = 0;
            }


            // OWNER

            if (
                Main.myPlayer !=
                Projectile.owner
            )
            {
                return;
            }


            // F PROJECTILE

            fSpawnTimer++;


            if (
                fSpawnTimer >=
                FSpawnInterval
            )
            {
                fSpawnTimer = 0;

                SpawnFProjectile();
            }


            // W PROJECTILE

            wSpawnTimer++;


            if (
                wSpawnTimer >=
                WSpawnInterval
            )
            {
                NPC target =
                    FindNearestEnemy(
                        WSearchRange
                    );


                if (target != null)
                {
                    wSpawnTimer = 0;

                    SpawnWProjectile(
                        target
                    );
                }
            }
        }


        // FIND NEAREST ENEMY

        private NPC FindNearestEnemy(
            float range)
        {
            NPC nearest = null;

            float bestDistance =
                range *
                range;


            for (
                int i = 0;
                i < Main.maxNPCs;
                i++
            )
            {
                NPC npc =
                    Main.npc[i];


                if (
                    !npc.active ||
                    !npc.CanBeChasedBy()
                )
                {
                    continue;
                }


                float distance =
                    Vector2.DistanceSquared(
                        Projectile.Center,
                        npc.Center
                    );


                if (
                    distance <
                    bestDistance
                )
                {
                    bestDistance =
                        distance;

                    nearest =
                        npc;
                }
            }


            return nearest;
        }


        // SPAWN F

        private void SpawnFProjectile()
        {
            int fType =
                ModContent.ProjectileType<
                    SolarYoyoProjF
                >();


            // RANDOM SKY ANGLE

            /*
             * Terraria's Y axis increases downward.
             *
             * Therefore negative angles point upward.
             *
             * -45°  = upper-right
             * -90°  = straight up
             * -135° = upper-left
             *
             * Random angle between -45° and -135°.
             */

            float angleDegrees =
                Main.rand.NextFloat(
                    -135f,
                    -45f
                );


            float angle =
                MathHelper.ToRadians(
                    angleDegrees
                );


            Vector2 direction =
                angle.ToRotationVector2();


            // RANDOM SPEED

            float speed =
                Main.rand.NextFloat(
                    7f,
                    12f
                );


            // SPAWN

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),

                Projectile.Center,

                direction * speed,

                fType,

                Math.Max(
                    1,
                    Projectile.damage
                ),

                Projectile.knockBack,

                Projectile.owner
            );
        }


        // SPAWN W

        private void SpawnWProjectile(
            NPC target)
        {
            int wType =
                ModContent.ProjectileType<
                    SolarYoyoProjW
                >();


            // RANDOM POSITION AROUND ENEMY

            float spawnAngle =
                Main.rand.NextFloat(
                    MathHelper.TwoPi
                );


            Vector2 spawnDirection =
                spawnAngle.ToRotationVector2();


            Vector2 spawnPosition =
                target.Center +
                spawnDirection *
                WSpawnDistance;


            // AIM TOWARD ENEMY

            Vector2 direction =
                target.Center -
                spawnPosition;


            if (
                direction.LengthSquared() <
                0.001f
            )
            {
                direction =
                    Vector2.UnitY;
            }
            else
            {
                direction.Normalize();
            }


            const float InitialSpeed =
                3.5f;


            // SPAWN

            /*
             * ai[0] =
             * locked target NPC index.
             */

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),

                spawnPosition,

                direction *
                InitialSpeed,

                wType,

                Math.Max(
                    1,
                    Projectile.damage
                ),

                Projectile.knockBack,

                Projectile.owner,

                target.whoAmI
            );
        }
    }
}