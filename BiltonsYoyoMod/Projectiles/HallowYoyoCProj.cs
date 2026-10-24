using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class HallowYoyoCProj : ModProjectile
    {
        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;

            Projectile.tileCollide = true;

            Projectile.timeLeft = 180;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.scale = 0.5f;
        }


        // AI

        public override void AI()
        {
            // GRAVITY

            Projectile.velocity.Y +=
                0.25f;


            // FACE MOVEMENT DIRECTION

            if (
                Projectile.velocity.LengthSquared() >
                0.01f
            )
            {
                Projectile.rotation =
                    Projectile.velocity.ToRotation();
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.55f,
                0.30f,
                0.65f
            );
        }


        // TILE LANDING

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            CreateLandingEffect();

            return true;
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
        }


        // LANDING EFFECT

        private void CreateLandingEffect()
        {
            // CRYSTAL PARTICLES

            for (int i = 0; i < 16; i++)
            {
                Vector2 velocity =
                    new Vector2(
                        Main.rand.NextFloat(
                            -3.5f,
                            3.5f
                        ),
                        Main.rand.NextFloat(
                            -3.5f,
                            -0.5f
                        )
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkCrystalShard,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1.1f
                        )
                    );


                dust.noGravity = true;
            }


            // SMALL PINK SPARKS

            for (int i = 0; i < 8; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        2.5f,
                        2.5f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkTorch,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            0.5f,
                            0.9f
                        )
                    );


                dust.noGravity = true;
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.9f,
                0.2f,
                0.65f
            );


            // SOUND

            SoundEngine.PlaySound(
                SoundID.Item10 with
                {
                    Volume = 0.30f,
                    Pitch = 0.25f
                },
                Projectile.Center
            );
        }


        // NPC HIT EFFECT

        private void CreateHitEffect()
        {
            // PINK BURST

            for (int i = 0; i < 16; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        3.5f,
                        3.5f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkTorch,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1.2f
                        )
                    );


                dust.noGravity = true;
            }


            // CRYSTAL SPARKS

            for (int i = 0; i < 8; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        2.5f,
                        2.5f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkCrystalShard,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            0.5f,
                            1.0f
                        )
                    );


                dust.noGravity = true;
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                1.0f,
                0.25f,
                0.75f
            );


            // SOUND

            SoundEngine.PlaySound(
                SoundID.Item14 with
                {
                    Volume = 0.35f,
                    Pitch = 0.20f
                },
                Projectile.Center
            );
        }
    }
}