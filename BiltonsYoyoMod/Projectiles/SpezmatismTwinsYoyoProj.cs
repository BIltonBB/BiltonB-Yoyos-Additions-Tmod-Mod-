using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class SpezmatismTwinsYoyoProj : ModProjectile
    {
        private const int SprayCooldown = 300;
        private const int SprayProjectileCount = 72;

        private int sprayCooldown;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosMaximumRange[
                Projectile.type
            ] = 300f;

            ProjectileID.Sets.YoyosTopSpeed[
                Projectile.type
            ] = 14f;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[
                Projectile.type
            ] = -1f;
        }

        public override void AI()
        {
            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.75f,
                0.15f,
                0.20f
            );


            // SPAWN RETINAZER COMPANION

            // localAI[1] is used instead of localAI[0]
            // because vanilla yoyo AI may use localAI[0].
            if (
                Projectile.localAI[1] == 0f &&
                Main.myPlayer == Projectile.owner
            )
            {
                Projectile.localAI[1] = 1f;

                int companionType =
                    ModContent.ProjectileType<
                        RetinazerTwinsYoyoCompanion>();

                int companion =
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        companionType,
                        Math.Max(
                            1,
                            Projectile.damage / 2
                        ),
                        0f,
                        Projectile.owner,
                        Projectile.whoAmI
                    );

                // If spawning failed, allow another attempt.
                if (companion < 0)
                {
                    Projectile.localAI[1] = 0f;
                }
            }


            // SPRAY COOLDOWN

            sprayCooldown++;

            if (
                sprayCooldown >= SprayCooldown &&
                Main.myPlayer == Projectile.owner
            )
            {
                sprayCooldown = 0;

                FireSpray();
            }
        }


        // FULL 360 DEGREE SPRAY

        private void FireSpray()
        {
            float startAngle =
                MathHelper.PiOver2;

            float angleStep =
                MathHelper.TwoPi /
                SprayProjectileCount;

            int spitDamage =
                Math.Max(
                    1,
                    Projectile.damage / 10
                );

            for (
                int i = 0;
                i < SprayProjectileCount;
                i++
            )
            {
                float angle =
                    startAngle +
                    angleStep * i;

                Vector2 direction =
                    Vector2.UnitX.RotatedBy(angle);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    direction * 8f,
                    ModContent.ProjectileType<
                        SpezmatismTwinsYoyoSpitProj>(),
                    spitDamage,
                    0f,
                    Projectile.owner
                );
            }


            // RADIOACTIVE EFFECT

            for (int i = 0; i < 35; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        4f,
                        4f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.GreenFairy,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.4f
                        )
                    );

                dust.noGravity = true;
            }
        }
    }
}