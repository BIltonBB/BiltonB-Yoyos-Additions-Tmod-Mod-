using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class RetinazerTwinsYoyoCompanion : ModProjectile
    {
        // ORBIT

        private const float OrbitRadius = 70f;
        private const float OrbitSpeed = 0.045f;


        // LASER BURST

        // 7.5 seconds between bursts
        private const int BurstCooldown = 450;

        // 8 ticks between lasers
        private const int BurstShotDelay = 8;

        // 10 lasers per burst
        private const int BurstCount = 10;

        // Targeting range
        private const float TargetRange = 700f;

        // Slower than main Retinazer
        private const float LaserSpeed = 9f;


        private int burstCooldown;
        private int burstTimer;
        private int shotsLeft;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.tileCollide = false;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            // Companion is manually kept alive.
            Projectile.timeLeft = 10;

            Projectile.scale = 1f;
        }


        // AI

        public override void AI()
        {
            int parentIndex =
                (int)Projectile.ai[0];


            // CHECK PARENT

            if (
                parentIndex < 0 ||
                parentIndex >= Main.maxProjectiles
            )
            {
                Projectile.Kill();
                return;
            }


            Projectile parent =
                Main.projectile[parentIndex];


            if (
                !parent.active ||
                parent.owner != Projectile.owner ||
                (
                    parent.type !=
                    ModContent.ProjectileType<
                        SpezmatismTwinsYoyoProj>()
                    &&
                    parent.type !=
                    ModContent.ProjectileType<
                        RetinazerTwinsYoyoProj>()
                )
            )
            {
                Projectile.Kill();
                return;
            }


            // Keep companion alive.
            Projectile.timeLeft = 10;


            // CONTACT DAMAGE

            Projectile.damage =
                Math.Max(
                    1,
                    parent.damage / 2
                );


            // ORBIT

            float angle =
                Main.GameUpdateCount *
                OrbitSpeed;

            angle += MathHelper.Pi;


            Vector2 desiredPosition =
                parent.Center +
                new Vector2(
                    OrbitRadius,
                    0f
                ).RotatedBy(angle);


            Vector2 difference =
                desiredPosition -
                Projectile.Center;


            if (
                difference.LengthSquared() >
                0.01f
            )
            {
                Projectile.velocity =
                    Vector2.Lerp(
                        Projectile.velocity,
                        difference * 0.18f,
                        0.20f
                    );

                Projectile.Center +=
                    Projectile.velocity;


                if (
                    Projectile.velocity.LengthSquared() >
                    0.01f
                )
                {
                    Projectile.rotation =
                        Projectile.velocity.ToRotation();
                }
            }


            // RETINAZER LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.9f,
                0.15f,
                0.1f
            );


            // ONLY OWNER FIRES

            if (Main.myPlayer != Projectile.owner)
                return;


            // BURST COOLDOWN

            burstCooldown++;


            // START BURST

            if (
                burstCooldown >= BurstCooldown &&
                shotsLeft <= 0
            )
            {
                burstCooldown = 0;
                burstTimer = 0;
                shotsLeft = BurstCount;
            }


            // FIRE BURST

            if (shotsLeft > 0)
            {
                burstTimer++;


                if (
                    burstTimer >= BurstShotDelay
                )
                {
                    burstTimer = 0;


                    // Only consume a shot if
                    // we successfully fired.
                    if (FireLaser(parent))
                    {
                        shotsLeft--;
                    }
                }
            }
        }


        // FIND CLOSEST TARGET

        private NPC FindClosestTarget()
        {
            NPC closest = null;

            float closestDistanceSquared =
                TargetRange * TargetRange;


            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc =
                    Main.npc[i];


                // VALID TARGET

                if (!npc.active)
                    continue;

                if (npc.friendly)
                    continue;

                if (npc.dontTakeDamage)
                    continue;

                if (npc.life <= 0)
                    continue;


                // DISTANCE

                float distanceSquared =
                    Vector2.DistanceSquared(
                        Projectile.Center,
                        npc.Center
                    );


                if (
                    distanceSquared <
                    closestDistanceSquared
                )
                {
                    closestDistanceSquared =
                        distanceSquared;

                    closest = npc;
                }
            }


            return closest;
        }


        // FIRE LASER

        private bool FireLaser(
            Projectile parent)
        {
            NPC target =
                FindClosestTarget();


            if (target == null)
                return false;


            Vector2 direction =
                target.Center -
                Projectile.Center;


            if (
                direction.LengthSquared() <=
                0.01f
            )
            {
                return false;
            }


            direction.Normalize();


            // DAMAGE

            int laserDamage =
                Math.Max(
                    1,
                    parent.damage / 2
                );


            // CREATE LASER

            int laser =
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    direction * LaserSpeed,
                    ModContent.ProjectileType<
                        TwinsYoyoProjLaser>(),
                    laserDamage,
                    0f,
                    Projectile.owner
                );


            if (laser < 0)
                return false;


            // SPAWN EFFECT

            for (int i = 0; i < 6; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.RedTorch,
                        Main.rand.NextVector2Circular(
                            1.5f,
                            1.5f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1.0f
                        )
                    );

                dust.noGravity = true;
            }


            return true;
        }


        // STRING + DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            int parentIndex =
                (int)Projectile.ai[0];


            if (
                parentIndex < 0 ||
                parentIndex >= Main.maxProjectiles
            )
            {
                return false;
            }


            Projectile parent =
                Main.projectile[parentIndex];


            if (!parent.active)
                return false;


            // STRING

            Vector2 start =
                parent.Center -
                Main.screenPosition;

            Vector2 end =
                Projectile.Center -
                Main.screenPosition;

            Vector2 difference =
                end - start;

            float length =
                difference.Length();


            if (
                length > 2f &&
                length < 1000f
            )
            {
                Vector2 midpoint =
                    (start + end) *
                    0.5f;

                float rotation =
                    difference.ToRotation();

                Texture2D pixel =
                    TextureAssets.MagicPixel.Value;


                // Dark outer string
                Main.spriteBatch.Draw(
                    pixel,
                    midpoint,
                    new Rectangle(
                        0,
                        0,
                        1,
                        1
                    ),
                    new Color(
                        55,
                        55,
                        55,
                        255
                    ),
                    rotation,
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    new Vector2(
                        length,
                        3f
                    ),
                    SpriteEffects.None,
                    0f
                );


                // Bright inner string
                Main.spriteBatch.Draw(
                    pixel,
                    midpoint,
                    new Rectangle(
                        0,
                        0,
                        1,
                        1
                    ),
                    new Color(
                        180,
                        180,
                        180,
                        255
                    ),
                    rotation,
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    new Vector2(
                        length,
                        1f
                    ),
                    SpriteEffects.None,
                    0f
                );
            }


            // SPRITE

            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/RetinazerTwinsYoyoProj"
                ).Value;


            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                texture.Size() * 0.5f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}