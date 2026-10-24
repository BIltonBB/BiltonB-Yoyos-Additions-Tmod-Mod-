using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class SpezmatismTwinsYoyoSpitProj : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;

            Projectile.timeLeft = 180;

            Projectile.tileCollide = true;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }


        public override void AI()
        {
            // SLOW DOWN

            Projectile.velocity *= 0.97f;


            // GREEN / RADIOACTIVE LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.15f,
                0.9f,
                0.15f
            );


            // ROTATION

            if (
                Projectile.velocity.LengthSquared() >
                0.01f
            )
            {
                Projectile.rotation =
                    Projectile.velocity.ToRotation();
            }


            // RADIOACTIVE DUST

            if (Main.rand.NextBool(3))
            {
                Vector2 offset =
                    Main.rand.NextVector2Circular(
                        5f,
                        5f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        offset,
                        DustID.GreenFairy,
                        Projectile.velocity *
                        -0.10f,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.5f,
                            1.0f
                        )
                    );


                dust.noGravity = true;
            }


            // OCCASIONAL SMALL BURST

            if (Main.rand.NextBool(12))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.GreenTorch,
                        Main.rand.NextVector2Circular(
                            1.5f,
                            1.5f
                        ),
                        100,
                        default,
                        0.7f
                    );


                dust.noGravity = true;
            }
        }


        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            target.AddBuff(
                BuffID.CursedInferno,
                180
            );


            // Extra radioactive burst on hit.

            for (int i = 0; i < 8; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        target.Center,
                        DustID.GreenFairy,
                        Main.rand.NextVector2Circular(
                            3f,
                            3f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1.2f
                        )
                    );


                dust.noGravity = true;
            }
        }
    }
}