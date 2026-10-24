using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using Terraria.Audio;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class SolarYoyoProjW : ModProjectile
    {
        // SETTINGS

        private const float Speed = 3.5f;

        // 30 degrees = PI / 6 radians.
        private const float MaxTurnPerTick = 0.5235988f;

        // 2x larger than before.
        private const int ProjectileSize = 36;

        private const int AfterimageCount = 7;

        private const float AfterimageSpacing = 7f;

        private const float SpriteScale = 1.04f;

        private const int ExplosionDuration = 18;


        // STATE

        private int targetIndex = -1;

        private bool initialized;

        private bool exploding;

        private int explosionTimer;

        private float distanceAccumulator;

        private bool damageAdjusted;

        private Vector2[] afterimagePositions =
            new Vector2[AfterimageCount];


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = ProjectileSize;
            Projectile.height = ProjectileSize;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.penetrate = 1;

            Projectile.timeLeft = 900;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }


        // AI

        public override void AI()
        {
            // INITIALIZE

            if (!initialized)
            {
                initialized = true;

                targetIndex =
                    (int)Projectile.ai[0];

                for (
                    int i = 0;
                    i < AfterimageCount;
                    i++
                )
                {
                    afterimagePositions[i] =
                        Projectile.Center;
                }
            }


            // HALF DAMAGE

            if (!damageAdjusted)
            {
                damageAdjusted = true;

                Projectile.damage =
                    Math.Max(
                        1,
                        Projectile.damage / 2
                    );
            }


            // EXPLOSION

            if (exploding)
            {
                HandleExplosion();
                return;
            }


            // TARGET

            NPC target =
                GetLockedTarget();


            if (target != null)
            {
                TurnTowardTarget(
                    target
                );
            }
            else
            {
                Projectile.velocity =
                    Projectile.velocity.SafeNormalize(
                        Vector2.UnitX
                    ) *
                    Speed;
            }


            // HOSTILE PROJECTILE CHECK

            if (
                Main.myPlayer ==
                Projectile.owner
            )
            {
                CheckHostileProjectiles();
            }


            // AFTERIMAGES

            UpdateAfterimages();


            // RED FIRE EFFECT

            SpawnFireParticles();


            // RED LIGHT

            float pulse =
                (float)Math.Sin(
                    Main.GameUpdateCount *
                    0.16f
                ) *
                0.5f +
                0.5f;


            Lighting.AddLight(
                Projectile.Center,
                0.55f +
                0.20f * pulse,
                0.06f +
                0.05f * pulse,
                0.01f
            );
        }


        // GET LOCKED TARGET

        private NPC GetLockedTarget()
        {
            if (
                targetIndex < 0 ||
                targetIndex >= Main.maxNPCs
            )
            {
                return null;
            }


            NPC target =
                Main.npc[targetIndex];


            if (
                !target.active ||
                !target.CanBeChasedBy()
            )
            {
                return null;
            }


            return target;
        }


        // TURN TOWARD TARGET

        private void TurnTowardTarget(
            NPC target)
        {
            Vector2 desired =
                target.Center -
                Projectile.Center;


            if (
                desired.LengthSquared() <
                0.001f
            )
            {
                return;
            }


            desired.Normalize();


            Vector2 current =
                Projectile.velocity;


            if (
                current.LengthSquared() <
                0.001f
            )
            {
                current =
                    desired *
                    Speed;
            }


            float currentAngle =
                current.ToRotation();


            float desiredAngle =
                desired.ToRotation();


            float difference =
                MathHelper.WrapAngle(
                    desiredAngle -
                    currentAngle
                );


            difference =
                MathHelper.Clamp(
                    difference,
                    -MaxTurnPerTick,
                    MaxTurnPerTick
                );


            float newAngle =
                currentAngle +
                difference;


            Projectile.velocity =
                newAngle.ToRotationVector2() *
                Speed;
        }


        // HOSTILE PROJECTILE CHECK

        private void CheckHostileProjectiles()
        {
            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile other =
                    Main.projectile[i];


                if (
                    !other.active ||
                    other.whoAmI ==
                    Projectile.whoAmI
                )
                {
                    continue;
                }


                if (
                    !other.hostile ||
                    other.friendly
                )
                {
                    continue;
                }


                if (
                    !Projectile.Hitbox.Intersects(
                        other.Hitbox
                    )
                )
                {
                    continue;
                }


                TriggerExplosion();

                return;
            }
        }


        // AFTERIMAGES

        private void UpdateAfterimages()
        {
            float movement =
                Projectile.velocity.Length();


            distanceAccumulator +=
                movement;


            if (
                distanceAccumulator <
                AfterimageSpacing
            )
            {
                return;
            }


            distanceAccumulator = 0f;


            for (
                int i = AfterimageCount - 1;
                i > 0;
                i--
            )
            {
                afterimagePositions[i] =
                    afterimagePositions[i - 1];
            }


            afterimagePositions[0] =
                Projectile.Center;
        }


        // FIRE PARTICLES

        private void SpawnFireParticles()
        {
            if (Main.rand.NextBool(2))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            8f,
                            8f
                        ),
                        DustID.Torch,
                        -Projectile.velocity *
                        Main.rand.NextFloat(
                            0.03f,
                            0.08f
                        ) +
                        Main.rand.NextVector2Circular(
                            0.8f,
                            0.8f
                        ),
                        60,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.4f
                        )
                    );


                dust.noGravity = true;
            }


            if (Main.rand.NextBool(3))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            10f,
                            10f
                        ),
                        DustID.SolarFlare,
                        -Projectile.velocity *
                        0.05f,
                        70,
                        default,
                        Main.rand.NextFloat(
                            0.5f,
                            1.0f
                        )
                    );


                dust.noGravity = true;
            }


            if (Main.rand.NextBool(5))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            12f,
                            12f
                        ),
                        DustID.Firework_Red,
                        Main.rand.NextVector2Circular(
                            1.2f,
                            1.2f
                        ),
                        80,
                        default,
                        Main.rand.NextFloat(
                            0.5f,
                            0.9f
                        )
                    );


                dust.noGravity = true;
            }
        }


        // NPC HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            target.AddBuff(
                BuffID.OnFire,
                300
            );


            TriggerExplosion();
        }


        // TRIGGER EXPLOSION

        private void TriggerExplosion()
        {
            if (exploding)
            {
                return;
            }


            exploding = true;

            explosionTimer = 0;


            Projectile.velocity =
                Vector2.Zero;


            Projectile.timeLeft =
                ExplosionDuration;


            CreateExplosionEffect();
        }


        // EXPLOSION EFFECT

        private void CreateExplosionEffect()
        {
            Vector2 center =
                Projectile.Center;


            // RED FIRE BURST

            for (
                int i = 0;
                i < 40;
                i++
            )
            {
                Vector2 velocity =
                    Main.rand.NextVector2Unit() *
                    Main.rand.NextFloat(
                        2.5f,
                        7f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Torch,
                        velocity,
                        40,
                        default,
                        Main.rand.NextFloat(
                            0.9f,
                            1.6f
                        )
                    );


                dust.noGravity = true;
            }


            // SOLAR SPARKS

            for (
                int i = 0;
                i < 20;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.SolarFlare,
                        Main.rand.NextVector2Unit() *
                        Main.rand.NextFloat(
                            2f,
                            5f
                        ),
                        50,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1.2f
                        )
                    );


                dust.noGravity = true;
            }


            Lighting.AddLight(
                center,
                1.2f,
                0.10f,
                0.01f
            );


            SoundEngine.PlaySound(
                SoundID.Item14,
                center
            );
        }


        // EXPLOSION UPDATE

        private void HandleExplosion()
        {
            explosionTimer++;


            float progress =
                explosionTimer /
                (float)ExplosionDuration;


            progress =
                MathHelper.Clamp(
                    progress,
                    0f,
                    1f
                );


            float fade =
                1f -
                progress;


            Lighting.AddLight(
                Projectile.Center,
                1.1f * fade,
                0.08f * fade,
                0.01f
            );


            if (
                explosionTimer >=
                ExplosionDuration
            )
            {
                Projectile.Kill();
            }
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            if (exploding)
            {
                return false;
            }


            Texture2D texture =
                TextureAssets.Projectile[
                    Projectile.type
                ].Value;


            Vector2 center =
                Projectile.Center -
                Main.screenPosition;


            // DISTANCE-BASED AFTERIMAGES

            for (
                int i = AfterimageCount - 1;
                i >= 1;
                i--
            )
            {
                Vector2 worldPosition =
                    afterimagePositions[i];


                if (
                    Vector2.DistanceSquared(
                        worldPosition,
                        Projectile.Center
                    ) >
                    1000000f
                )
                {
                    continue;
                }


                // Actual lighting from the tile where
                // this afterimage exists.
                Color lighting =
                    Lighting.GetColor(
                        (int)(
                            worldPosition.X /
                            16f
                        ),
                        (int)(
                            worldPosition.Y /
                            16f
                        )
                    );


                Color afterColor =
                    Color.Lerp(
                        lighting,
                        new Color(
                            255,
                            70,
                            25
                        ),
                        0.40f
                    );


                float opacity =
                    (
                        1f -
                        i /
                        (float)(AfterimageCount + 1)
                    ) *
                    0.28f;


                afterColor *=
                    opacity;


                Main.EntitySpriteDraw(
                    texture,
                    worldPosition -
                    Main.screenPosition,
                    null,
                    afterColor,
                    Projectile.rotation,
                    texture.Size() *
                    0.5f,
                    SpriteScale *
                    (
                        1f -
                        i *
                        0.03f
                    ),
                    SpriteEffects.None,
                    0f
                );
            }


            // RED FIRE GLOW

            float pulse =
                (float)Math.Sin(
                    Main.GameUpdateCount *
                    0.16f
                ) *
                0.5f +
                0.5f;


            Main.EntitySpriteDraw(
                TextureAssets.Sun2.Value,
                center,
                null,
                new Color(
                    255,
                    55,
                    20,
                    (byte)(
                        55f +
                        25f * pulse
                    )
                ),
                0f,
                TextureAssets.Sun2.Value.Size() *
                0.5f,
                0.055f,
                SpriteEffects.None,
                0f
            );


            // SECOND INNER GLOW

            Main.EntitySpriteDraw(
                TextureAssets.Sun2.Value,
                center,
                null,
                new Color(
                    255,
                    150,
                    60,
                    (byte)(
                        35f +
                        20f * pulse
                    )
                ),
                0f,
                TextureAssets.Sun2.Value.Size() *
                0.5f,
                0.030f,
                SpriteEffects.None,
                0f
            );


            // 50% TRANSPARENT BODY

            Color bodyLighting =
                Lighting.GetColor(
                    (int)(
                        Projectile.Center.X /
                        16f
                    ),
                    (int)(
                        Projectile.Center.Y /
                        16f
                    )
                );


            Color bodyColor =
                Color.Lerp(
                    bodyLighting,
                    new Color(
                        255,
                        80,
                        35
                    ),
                    0.55f
                );


            bodyColor.A = 128;


            Main.EntitySpriteDraw(
                texture,
                center,
                null,
                bodyColor,
                Projectile.rotation,
                texture.Size() *
                0.5f,
                SpriteScale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}