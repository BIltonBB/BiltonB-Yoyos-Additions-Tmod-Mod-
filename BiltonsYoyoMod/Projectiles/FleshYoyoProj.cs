using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class FleshYoyoProj : ModProjectile
    {
        int spawnTimer;

        // 0 = right
        // 1 = bottom
        // 2 = left
        // 3 = top
        private int laserSide;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 20f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 200f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 20f;
        }

        public override void AI()
        {
            /*
             * ------------------------------------------------------------
             * SPAWN MINIONS IMMEDIATELY
             * ------------------------------------------------------------
             */

            if (Main.myPlayer == Projectile.owner)
            {
                int eyeCount = 3;
                int currentEyes = 0;

                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];

                    if (p.active &&
                        p.type == ModContent.ProjectileType<FleshYoyoProjMini>() &&
                        (int)p.ai[0] == Projectile.whoAmI &&
                        p.owner == Projectile.owner)
                    {
                        currentEyes++;
                    }
                }

                int eyesToSpawn = eyeCount - currentEyes;

                for (int i = 0; i < eyesToSpawn; i++)
                {
                    float startingAngle =
                        MathHelper.TwoPi / eyeCount *
                        (currentEyes + i);

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<FleshYoyoProjMini>(),
                        Projectile.damage / 2,
                        Projectile.knockBack,
                        Projectile.owner,
                        Projectile.whoAmI,
                        startingAngle
                    );
                }
            }

            /*
             * ------------------------------------------------------------
             * LASER TIMER
             * ------------------------------------------------------------
             */

            spawnTimer++;

            if (spawnTimer >= 300)
            {
                spawnTimer = 0;

                if (Main.myPlayer == Projectile.owner)
                {
                    /*
                     * ----------------------------------------------------
                     * CAPTURE FIRING POSITION
                     * ----------------------------------------------------
                     *
                     * This position is stored directly inside each beam.
                     * The beam therefore does not need to ask the yoyo
                     * where it spawned from later.
                     */

                    Vector2 firingPosition = Projectile.Center;

                    /*
                     * ----------------------------------------------------
                     * LASERS
                     * ----------------------------------------------------
                     */

                    int laserCount = 5;

                    float spread =
                        MathHelper.ToRadians(50f);

                    float centerAngle;

                    switch (laserSide)
                    {
                        // RIGHT
                        case 0:
                            centerAngle = 0f;
                            break;

                        // BOTTOM
                        case 1:
                            centerAngle = MathHelper.PiOver2;
                            break;

                        // LEFT
                        case 2:
                            centerAngle = MathHelper.Pi;
                            break;

                        // TOP
                        default:
                            centerAngle = -MathHelper.PiOver2;
                            break;
                    }

                    for (int i = 0; i < laserCount; i++)
                    {
                        float angle =
                            centerAngle +
                            MathHelper.Lerp(
                                -spread,
                                spread,
                                i / (float)(laserCount - 1)
                            );

                        Vector2 direction =
                            angle.ToRotationVector2();

                        /*
                         * ai[0] = exact firing X
                         * ai[1] = exact firing Y
                         *
                         * These values are synchronized with the beam.
                         */

                        int laser = Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            firingPosition,
                            direction * 12f,
                            ModContent.ProjectileType<FleshYoyoBeamProj>(),
                            20,
                            1f,
                            Projectile.owner,
                            firingPosition.X,
                            firingPosition.Y
                        );

                        if (laser >= 0 &&
                            laser < Main.maxProjectiles)
                        {
                            Main.projectile[laser].netUpdate = true;
                        }
                    }

                    // Move to the next side.
                    laserSide++;

                    if (laserSide >= 4)
                        laserSide = 0;
                }
            }
        }

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 180);
        }
    }
}