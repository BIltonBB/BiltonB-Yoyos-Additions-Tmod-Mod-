using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class StringCutterEmberProjectile
        : ModProjectile
    {
        // SETTINGS

        private const int Lifetime = 20;

        // DEFAULTS

        public override string Texture =>
            "Terraria/Images/MagicPixel";

        public override void SetDefaults()
        {
            /*
             * Invisible carrier projectile.
             *
             * The visual is entirely produced with Torch particles.
             */
            Projectile.width = 6;
            Projectile.height = 6;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.penetrate = 1;

            Projectile.timeLeft =
                Lifetime;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }

        // AI

        public override void AI()
        {
            /*
             * Embers gradually lose horizontal momentum.
             */
            Projectile.velocity *= 0.94f;

            /*
             * Small upward drift creates a hot ember effect.
             */
            Projectile.velocity.Y -= 0.025f;

            // MAIN EMBER PARTICLE

            if (Main.rand.NextBool(2))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Torch,
                        Projectile.velocity * 0.15f,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1.1f
                        )
                    );

                dust.noGravity = true;
            }

            // EXTRA SMALL SPARKS

            if (Main.rand.NextBool(3))
            {
                Dust spark =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            3f,
                            3f
                        ),
                        DustID.Torch,
                        Main.rand.NextVector2Circular(
                            0.5f,
                            0.8f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.3f,
                            0.7f
                        )
                    );

                spark.noGravity = true;
            }

            Projectile.rotation =
                Projectile.velocity.ToRotation();
        }

        // HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            target.AddBuff(
                BuffID.OnFire,
                180
            );

            /*
             * Small burst on impact.
             */
            for (int i = 0; i < 5; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        target.Center,
                        DustID.Torch,
                        Main.rand.NextVector2Circular(
                            1.5f,
                            1.5f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1f
                        )
                    );

                dust.noGravity = true;
            }
        }

        // NO CUSTOM DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            /*
             * No line rendering.
             * The ember exists visually through Dust particles.
             */
            return false;
        }
    }
}