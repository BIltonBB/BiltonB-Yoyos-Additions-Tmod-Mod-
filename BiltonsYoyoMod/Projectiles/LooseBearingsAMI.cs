using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class LooseBearingsAMI : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;

            Projectile.DamageType =
                DamageClass.Melee;

            Projectile.aiStyle = 0;
        }

        public override void AI()
        {
            Projectile.velocity.Y += 0.35f;

            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;

            Projectile.rotation +=
                Projectile.velocity.X * 0.08f;
        }

        // GROUND

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            CreateMeteorExplosion();

            Projectile.Kill();

            return false;
        }

        // ENEMY

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            CreateMeteorExplosion();

            Projectile.Kill();
        }

        // METEORITE IMPACT

        private void CreateMeteorExplosion()
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(
                SoundID.Item14,
                Projectile.Center
            );

            // Small impact flash.
            for (int i = 0; i < 3; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        1.5f,
                        1.5f
                    );

                Dust flash =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Torch,
                        velocity,
                        100,
                        default,
                        1.15f
                    );

                flash.noGravity = true;
            }

            // EXACTLY 3 FIERY SPARKS

            for (int i = 0; i < 3; i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i / 3f +
                    Main.rand.NextFloat(
                        -0.25f,
                        0.25f
                    );

                Vector2 direction =
                    angle.ToRotationVector2();

                Vector2 velocity =
                    direction *
                    Main.rand.NextFloat(
                        3.5f,
                        5f
                    );

                Dust spark =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Torch,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            1.1f,
                            1.5f
                        )
                    );

                spark.noGravity = true;
                spark.fadeIn = 1.2f;
            }

            Lighting.AddLight(
                Projectile.Center,
                0.9f,
                0.45f,
                0.08f
            );
        }
    }
}