using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class PlanteraYoyoProjSeedler : ModProjectile
    {
        private bool hasExploded;

        private const int SeedCount = 10;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = true;
            Projectile.penetrate = 1;

            Projectile.timeLeft = 300;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            // Gravity
            Projectile.velocity.Y += 0.2f;

            // Face movement direction
            if (Projectile.velocity.LengthSquared() > 0.01f)
            {
                Projectile.rotation =
                    Projectile.velocity.ToRotation()
                    - MathHelper.PiOver2;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Explode();
            return false;
        }

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            target.AddBuff(BuffID.Poisoned, 180);
            target.AddBuff(BuffID.Slow, 180);

            Explode();
        }

        private void Explode()
        {
            if (hasExploded)
                return;

            hasExploded = true;

            // SOUND

            // Only the Seedler explosion makes a sound.
            SoundEngine.PlaySound(
                SoundID.Item14,
                Projectile.Center
            );

            // MAIN EXPLOSION

            // Central burst
            for (int i = 0; i < 18; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        4.5f,
                        4.5f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Plantera_Green,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(1.0f, 1.6f)
                    );

                dust.noGravity = true;
            }

            // Larger outward ring
            for (int i = 0; i < 12; i++)
            {
                float angle =
                    MathHelper.TwoPi * i / 12f;

                Vector2 velocity =
                    angle.ToRotationVector2() *
                    Main.rand.NextFloat(3.5f, 5.5f);

                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Plantera_Green,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(1.1f, 1.5f)
                    );

                dust.noGravity = true;
            }

            // THROW SEEDS

            if (Main.myPlayer == Projectile.owner)
            {
                for (int i = 0; i < SeedCount; i++)
                {
                    float angle =
                        MathHelper.TwoPi * i / SeedCount;

                    angle += Main.rand.NextFloat(
                        -0.18f,
                        0.18f
                    );

                    Vector2 direction =
                        angle.ToRotationVector2();

                    float speed =
                        Main.rand.NextFloat(3f, 6f);

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        direction * speed,
                        ModContent.ProjectileType<
                            PlanteraYoyoProjSeed>(),
                        Math.Max(
                            1,
                            Projectile.damage / 20
                        ),
                        Projectile.knockBack,
                        Projectile.owner
                    );
                }
            }

            Projectile.Kill();
        }

        public override void OnKill(int timeLeft)
        {
            /*
             * If the projectile expires naturally, still create
             * the explosion.
             */
            if (!hasExploded)
            {
                Explode();
            }
        }
    }
}