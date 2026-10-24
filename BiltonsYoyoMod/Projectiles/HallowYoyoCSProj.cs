using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class HallowYoyoCSProj : ModProjectile
    {
        // SLIME SAFE WINDOW

        private const int SlimeSafeTime = 30;


        // SPIN

        private const float SpinSpeed = 0.20f;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;

            Projectile.tileCollide = true;

            Projectile.timeLeft = 300;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.scale = 0.5f;
        }


        // AI

        public override void AI()
        {
            /*
             * ai[0] == 1:
             *
             * Spawned by the Hallow slime.
             * Gets the 2-second safe window.
             *
             * ai[0] == 0:
             *
             * Spawned normally.
             * No safe window.
             */

            bool slimeSpawned =
                Projectile.ai[0] == 1f;


            // SLIME-ONLY SAFE WINDOW

            if (slimeSpawned)
            {
                Projectile.localAI[0]++;


                if (
                    Projectile.localAI[0] <=
                    SlimeSafeTime
                )
                {
                    /*
                     * Completely ignore tiles.
                     */
                    Projectile.tileCollide = false;

                    /*
                     * Cannot damage NPCs yet.
                     */
                    Projectile.friendly = false;
                }
                else
                {
                    /*
                     * Safe window has ended.
                     */
                    Projectile.tileCollide = true;

                    Projectile.friendly = true;
                }
            }
            else
            {
                /*
                 * Normally spawned CS has no safe window.
                 */
                Projectile.tileCollide = true;
                Projectile.friendly = true;
            }


            // GRAVITY

            Projectile.velocity.Y +=
                0.30f;


            // ALWAYS SPIN

            Projectile.rotation +=
                SpinSpeed;


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.8f,
                0.45f,
                0.9f
            );
        }


        // TILE LANDING

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            CreateLandingEffect();

            Explode();

            return false;
        }


        // NPC HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            target.AddBuff(
                BuffID.BrokenArmor,
                180
            );


            CreateHitEffect();

            Explode();
        }


        // TILE LANDING EFFECT

        private void CreateLandingEffect()
        {
            // CRYSTAL RING

            for (int i = 0; i < 24; i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    24f;


                Vector2 velocity =
                    angle.ToRotationVector2() *
                    Main.rand.NextFloat(
                        2f,
                        5f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkCrystalShard,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.5f
                        )
                    );


                dust.noGravity = true;
            }


            // PINK GROUND PARTICLES

            for (int i = 0; i < 12; i++)
            {
                Vector2 velocity =
                    new Vector2(
                        Main.rand.NextFloat(
                            -4f,
                            4f
                        ),
                        Main.rand.NextFloat(
                            -3f,
                            -0.5f
                        )
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkTorch,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.3f
                        )
                    );


                dust.noGravity = true;
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                1.5f,
                0.3f,
                0.9f
            );


            // SOUND

            SoundEngine.PlaySound(
                SoundID.Item27 with
                {
                    Volume = 0.65f,
                    Pitch = -0.15f
                },
                Projectile.Center
            );
        }


        // NPC HIT EFFECT

        private void CreateHitEffect()
        {
            // PINK TORCH BURST

            for (int i = 0; i < 30; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        5f,
                        5f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkTorch,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            0.9f,
                            1.6f
                        )
                    );


                dust.noGravity = true;
            }


            // CRYSTAL PARTICLES

            for (int i = 0; i < 12; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        3f,
                        3f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkCrystalShard,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.2f
                        )
                    );


                dust.noGravity = true;
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                1.8f,
                0.4f,
                1.0f
            );


            // SOUND

            SoundEngine.PlaySound(
                SoundID.Item14 with
                {
                    Volume = 0.55f,
                    Pitch = 0.15f
                },
                Projectile.Center
            );
        }


        // EXPLOSION

        private void Explode()
        {
            /*
             * Prevent multiple explosions from the same projectile.
             */
            if (Projectile.ai[1] == 1f)
                return;


            Projectile.ai[1] = 1f;


            if (
                Main.myPlayer !=
                Projectile.owner
            )
            {
                Projectile.Kill();
                return;
            }


            // SPAWN 10 C PROJECTILES

            int count = 10;


            int cType =
                ModContent.ProjectileType<
                    HallowYoyoCProj>();


            int cDamage =
                Math.Max(
                    1,
                    Projectile.damage / 2
                );


            for (int i = 0; i < count; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        6f,
                        6f
                    );


                Projectile c =
                    Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        velocity,
                        cType,
                        cDamage,
                        Projectile.knockBack,
                        Projectile.owner
                    );


                c.netUpdate = true;
            }


            // BIG CRYSTAL BURST

            for (int i = 0; i < 28; i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    28f;


                Vector2 velocity =
                    angle.ToRotationVector2() *
                    Main.rand.NextFloat(
                        3f,
                        6f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkCrystalShard,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.4f
                        )
                    );


                dust.noGravity = true;
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                1.5f,
                0.35f,
                0.9f
            );


            // SOUND

            SoundEngine.PlaySound(
                SoundID.Item10 with
                {
                    Volume = 0.45f,
                    Pitch = 0.05f
                },
                Projectile.Center
            );


            Projectile.Kill();
        }
    }
}