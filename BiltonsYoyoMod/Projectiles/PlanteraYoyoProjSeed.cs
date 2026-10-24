using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class PlanteraYoyoProjSeed : ModProjectile
    {
        private bool hasExploded;

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = true;
            Projectile.penetrate = 1;

            Projectile.timeLeft = 180;

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

            // SMALL VISUAL BURST

            for (int i = 0; i < 5; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        1.8f,
                        1.8f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Plantera_Green,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(0.45f, 0.8f)
                    );

                dust.noGravity = true;
            }

            // Tiny outward ring
            for (int i = 0; i < 4; i++)
            {
                float angle =
                    MathHelper.TwoPi * i / 4f;

                Vector2 velocity =
                    angle.ToRotationVector2() *
                    1.5f;

                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Plantera_Green,
                        velocity,
                        0,
                        default,
                        0.6f
                    );

                dust.noGravity = true;
            }

            // NO SOUND HERE

            Projectile.Kill();
        }

        public override void OnKill(int timeLeft)
        {
            if (!hasExploded)
            {
                Explode();
            }
        }
    }
}