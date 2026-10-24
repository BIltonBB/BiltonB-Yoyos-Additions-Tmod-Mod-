using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class GolemYoyoProjC : ModProjectile
    {
        // CORE ORBIT

        private const float OrbitDistance = 28f;

        private const float OrbitAcceleration = 0.15f;

        private const float MaxOrbitSpeed = 8f;


        // SPECIAL ATTACK TIMER

        // 5 seconds = 300 ticks.
        private const int SpecialInterval = 180;

        private int specialTimer;


        // SPECIAL SEQUENCES

        private bool firingB;
        private bool firingL;

        private int shotsRemaining;
        private int shotTimer;


        // B:
        // 3 shots
        // 1 second apart
        private const int BShotCount = 10;
        private const int BShotDelay = 30;


        // L:
        // 5 shots
        // 0.5 seconds apart
        private const int LShotCount = 10;
        private const int LShotDelay = 30;


        // CORE DAMAGE

        private const int RequiredHits = 10;

        private int projectileHits;

        /*
         * Uses hitbox intersection instead of center distance.
         */
        private const int CoreHitPadding = 8;


        // CORE I-FRAMES

        // 30 ticks = 0.5 seconds.
        private const int CoreHitIFrames = 30;

        private int coreHitIFrameTimer;


        // EXPLOSION

        private const float ExplosionRadius = 110f;

        private const int ExplosionDuration = 30;

        private int explosionTimer;

        private bool exploding;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = false;
            Projectile.hostile = false;

            Projectile.tileCollide = false;

            Projectile.penetrate = -1;

            Projectile.timeLeft = 9999;

            Projectile.DamageType =
                DamageClass.Melee;
        }


        // AI

        public override void AI()
        {
            Projectile mainYoyo =
                FindMainYoyo();


            if (
                mainYoyo == null
            )
            {
                Projectile.Kill();
                return;
            }


            // EXPLOSION STATE

            if (exploding)
            {
                HandleExplosion();

                return;
            }


            // CORE I-FRAMES

            if (
                coreHitIFrameTimer >
                0
            )
            {
                coreHitIFrameTimer--;
            }


            // ORBIT

            float angle =
                Main.GameUpdateCount *
                0.14f;


            Vector2 desiredPosition =
                mainYoyo.Center +
                angle.ToRotationVector2() *
                OrbitDistance;


            Vector2 difference =
                desiredPosition -
                Projectile.Center;


            Vector2 desiredVelocity =
                difference *
                OrbitAcceleration;


            if (
                desiredVelocity.Length() >
                MaxOrbitSpeed
            )
            {
                desiredVelocity =
                    desiredVelocity.SafeNormalize(
                        Vector2.UnitX
                    ) *
                    MaxOrbitSpeed;
            }


            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    desiredVelocity,
                    0.15f
                );


            // SPECIAL ATTACKS

            if (
                Main.myPlayer ==
                Projectile.owner
            )
            {
                HandleSpecialAttacks();

                CheckHostileProjectiles();
            }


            // CORE LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.55f,
                0.42f,
                0.07f
            );
        }


        // SPECIAL ATTACK CONTROLLER

        private void HandleSpecialAttacks()
        {
            // ACTIVE SEQUENCE

            if (
                shotsRemaining >
                0
            )
            {
                shotTimer++;


                int delay =
                    firingB
                        ? BShotDelay
                        : LShotDelay;


                if (
                    shotTimer >=
                    delay
                )
                {
                    shotTimer = 0;


                    FireSpecialShot();


                    shotsRemaining--;


                    if (
                        shotsRemaining <=
                        0
                    )
                    {
                        shotsRemaining = 0;

                        firingB = false;
                        firingL = false;
                    }
                }


                return;
            }


            // 5 SECOND TIMER

            specialTimer++;


            if (
                specialTimer <
                SpecialInterval
            )
            {
                return;
            }


            specialTimer = 0;

            shotTimer = 0;


            // 75% B

            if (
                Main.rand.NextFloat() <
                0.75f
            )
            {
                firingB = true;
                firingL = false;

                shotsRemaining =
                    BShotCount;


                // First shot immediately.
                FireSpecialShot();

                shotsRemaining--;

                return;
            }


            // 25% L

            firingB = false;
            firingL = true;

            shotsRemaining =
                LShotCount;


            // First shot immediately.
            FireSpecialShot();

            shotsRemaining--;
        }


        // FIRE SPECIAL

        private void FireSpecialShot()
        {
            if (firingB)
            {
                FireB();

                return;
            }


            if (firingL)
            {
                FireL();
            }
        }


        // B PROJECTILE

        private void FireB()
        {
            Vector2 direction =
                Main.rand.NextVector2Unit();


            if (
                direction.LengthSquared() <
                0.001f
            )
            {
                direction =
                    Vector2.UnitX;
            }


            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction * 6f,
                ModContent.ProjectileType<
                    GolemYoyoProjB>(),
                Math.Max(
                    1,
                    Projectile.damage
                ),
                Projectile.knockBack,
                Projectile.owner
            );
        }


        // L PROJECTILE

        private void FireL()
        {
            NPC target =
                FindNearestTarget();


            if (
                target == null
            )
            {
                return;
            }


            Vector2 direction =
                target.Center -
                Projectile.Center;


            if (
                direction.LengthSquared() <
                0.001f
            )
            {
                direction =
                    Vector2.UnitX;
            }
            else
            {
                direction.Normalize();
            }


            // Slight random firing offset.
            direction =
                direction.RotatedBy(
                    Main.rand.NextFloat(
                        -0.10f,
                        0.10f
                    )
                );


            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction * 16f,
                ModContent.ProjectileType<
                    GolemYoyoProjL>(),
                Math.Max(
                    1,
                    Projectile.damage
                ),
                Projectile.knockBack,
                Projectile.owner
            );
        }


        // FIND MAIN YOYO

        private Projectile FindMainYoyo()
        {
            int type =
                ModContent.ProjectileType<
                    GolemYoyoProj>();


            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile p =
                    Main.projectile[i];


                if (
                    p.active &&
                    p.type == type &&
                    p.owner ==
                    Projectile.owner
                )
                {
                    return p;
                }
            }


            return null;
        }


        // HOSTILE PROJECTILE INTERCEPTION

        private void CheckHostileProjectiles()
        {
            /*
             * Core cannot be hit again while its
             * 30-tick iframe is active.
             */
            if (
                coreHitIFrameTimer >
                0
            )
            {
                return;
            }


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


                /*
                 * Slightly enlarged Core hitbox.
                 */
                Rectangle coreHitbox =
                    Projectile.Hitbox;


                coreHitbox.Inflate(
                    CoreHitPadding,
                    CoreHitPadding
                );


                if (
                    !coreHitbox.Intersects(
                        other.Hitbox
                    )
                )
                {
                    continue;
                }


                // HIT

                /*
                 * The hostile projectile is intentionally NOT killed.
                 *
                 * It continues moving normally after hitting
                 * the Core.
                 */


                projectileHits++;


                /*
                 * Start 30-tick Core invulnerability.
                 */
                coreHitIFrameTimer =
                    CoreHitIFrames;


                CreateCoreHitEffect(
                    Projectile.Center
                );


                /*
                 * Stronger visual feedback at each hit.
                 */
                if (
                    projectileHits <
                    RequiredHits
                )
                {
                    CreateHitFlash(
                        Projectile.Center
                    );
                }


                // 10 HITS

                if (
                    projectileHits >=
                    RequiredHits
                )
                {
                    BeginExplosion();

                    return;
                }


                /*
                 * Only one projectile can register a hit
                 * during this frame.
                 */
                return;
            }
        }


        // CORE HIT EFFECT

        private void CreateCoreHitEffect(
            Vector2 position)
        {
            for (
                int i = 0;
                i < 24;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Electric,
                        Main.rand.NextVector2Circular(
                            4f,
                            4f
                        ),
                        70,
                        default,
                        Main.rand.NextFloat(
                            0.55f,
                            1.15f
                        )
                    );


                dust.noGravity = true;
            }


            Terraria.Audio.SoundEngine.PlaySound(
                SoundID.Item93,
                position
            );
        }


        // SMALL HIT FLASH

        private void CreateHitFlash(
            Vector2 position)
        {
            for (
                int i = 0;
                i < 12;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Torch,
                        Main.rand.NextVector2Circular(
                            3f,
                            3f
                        ),
                        80,
                        default,
                        Main.rand.NextFloat(
                            0.12f,
                            1.25f
                        )
                    );


                dust.noGravity = true;
            }
        }


        // BEGIN EXPLOSION

        private void BeginExplosion()
        {
            if (exploding)
            {
                return;
            }


            exploding = true;

            explosionTimer = 0;

            Projectile.velocity =
                Vector2.Zero;


            /*
             * Stop all further B/L activity.
             */
            shotsRemaining = 0;

            firingB = false;
            firingL = false;


            specialTimer = 0;
            shotTimer = 0;


            /*
             * Don't let the explosion instantly disappear.
             */
            Projectile.timeLeft =
                ExplosionDuration + 5;


            // HUGE INITIAL BURST

            for (
                int i = 0;
                i < 120;
                i++
            )
            {
                Vector2 velocity =
                    Main.rand.NextVector2Unit() *
                    Main.rand.NextFloat(
                        4f,
                        12f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Torch,
                        velocity,
                        35,
                        default,
                        Main.rand.NextFloat(
                            1.5f,
                            2.8f
                        )
                    );


                dust.noGravity = true;
            }


            // ELECTRIC BURST

            for (
                int i = 0;
                i < 80;
                i++
            )
            {
                Vector2 velocity =
                    Main.rand.NextVector2Unit() *
                    Main.rand.NextFloat(
                        3f,
                        10f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Electric,
                        velocity,
                        40,
                        default,
                        Main.rand.NextFloat(
                            1.0f,
                            2.0f
                        )
                    );


                dust.noGravity = true;
            }


            // EXTRA INNER BURST

            for (
                int i = 0;
                i < 45;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.Torch,
                        Main.rand.NextVector2Circular(
                            4f,
                            4f
                        ),
                        20,
                        default,
                        Main.rand.NextFloat(
                            2f,
                            3.2f
                        )
                    );


                dust.noGravity = true;
            }


            Terraria.Audio.SoundEngine.PlaySound(
                SoundID.Item14,
                Projectile.Center
            );


            /*
             * Do the actual 2× damage exactly once.
             */
            DealExplosionDamage();
        }


        // EXPLOSION ANIMATION

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


            // STRONG EXPLOSION LIGHT

            Lighting.AddLight(
                Projectile.Center,
                1.5f,
                1.05f,
                0.30f
            );


            // CONTINUING OUTWARD SPARKS

            if (
                Main.rand.NextBool(1)
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            12f,
                            12f
                        ),
                        DustID.Torch,
                        Main.rand.NextVector2Unit() *
                        Main.rand.NextFloat(
                            4f,
                            9f
                        ),
                        25,
                        default,
                        Main.rand.NextFloat(
                            1.0f,
                            2.0f
                        )
                    );


                dust.noGravity = true;
            }


            // ELECTRIC SPARKS

            if (
                Main.rand.NextBool(2)
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        Main.rand.NextVector2Circular(
                            18f,
                            18f
                        ),
                        DustID.Electric,
                        Main.rand.NextVector2Unit() *
                        Main.rand.NextFloat(
                            3f,
                            7f
                        ),
                        30,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.7f
                        )
                    );


                dust.noGravity = true;
            }


            if (
                explosionTimer >=
                ExplosionDuration
            )
            {
                Projectile.Kill();
            }
        }


        // EXPLOSION DAMAGE

        private void DealExplosionDamage()
        {
            Projectile mainYoyo =
                FindMainYoyo();


            int yoyoDamage =
                mainYoyo != null
                    ? mainYoyo.damage
                    : Projectile.damage;


            int explosionDamage =
                Math.Max(
                    1,
                    yoyoDamage * 2
                );


            for (
                int i = 0;
                i < Main.maxNPCs;
                i++
            )
            {
                NPC npc =
                    Main.npc[i];


                if (
                    !npc.active ||
                    npc.friendly ||
                    npc.dontTakeDamage
                )
                {
                    continue;
                }


                if (
                    Vector2.DistanceSquared(
                        npc.Center,
                        Projectile.Center
                    )
                    >
                    ExplosionRadius *
                    ExplosionRadius
                )
                {
                    continue;
                }


                int hitDirection =
                    npc.Center.X >=
                    Projectile.Center.X
                        ? 1
                        : -1;


                npc.SimpleStrikeNPC(
                    explosionDamage,
                    hitDirection,
                    false,
                    8f,
                    DamageClass.Melee
                );
            }
        }


        // TARGET

        private NPC FindNearestTarget()
        {
            NPC best = null;


            float bestDistance =
                600f *
                600f;


            for (
                int i = 0;
                i < Main.maxNPCs;
                i++
            )
            {
                NPC npc =
                    Main.npc[i];


                if (
                    !npc.active ||
                    !npc.CanBeChasedBy()
                )
                {
                    continue;
                }


                float distance =
                    Vector2.DistanceSquared(
                        Projectile.Center,
                        npc.Center
                    );


                if (
                    distance <
                    bestDistance
                )
                {
                    bestDistance =
                        distance;

                    best =
                        npc;
                }
            }


            return best;
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                TextureAssets.Sun2.Value;


            // EXPLOSION

            if (exploding)
            {
                float progress =
                    explosionTimer /
                    (float)ExplosionDuration;


                progress =
                    MathHelper.Clamp(
                        progress,
                        0f,
                        1f
                    );


                // HUGE OUTER RING

                float outerScale =
                    MathHelper.Lerp(
                        0.25f,
                        1.65f,
                        progress
                    );


                byte outerAlpha =
                    (byte)MathHelper.Clamp(
                        255f *
                        (1f - progress),
                        0f,
                        255f
                    );


                Main.EntitySpriteDraw(
                    texture,
                    Projectile.Center -
                    Main.screenPosition,
                    null,
                    new Color(
                        255,
                        150,
                        25,
                        outerAlpha
                    ),
                    Main.GameUpdateCount *
                    0.04f,
                    texture.Size() * 0.5f,
                    outerScale,
                    SpriteEffects.None,
                    0f
                );


                // SECOND EXPANDING RING

                float secondScale =
                    MathHelper.Lerp(
                        0.12f,
                        1.15f,
                        progress
                    );


                byte secondAlpha =
                    (byte)MathHelper.Clamp(
                        230f *
                        (1f - progress),
                        0f,
                        255f
                    );


                Main.EntitySpriteDraw(
                    texture,
                    Projectile.Center -
                    Main.screenPosition,
                    null,
                    new Color(
                        255,
                        210,
                        50,
                        secondAlpha
                    ),
                    Main.GameUpdateCount *
                    -0.055f,
                    texture.Size() * 0.5f,
                    secondScale,
                    SpriteEffects.None,
                    0f
                );


                // BRIGHT INNER CORE

                float innerScale =
                    MathHelper.Lerp(
                        0.28f,
                        0.72f,
                        progress
                    );


                byte innerAlpha =
                    (byte)MathHelper.Clamp(
                        255f *
                        (
                            1f -
                            progress * 0.85f
                        ),
                        0f,
                        255f
                    );


                Main.EntitySpriteDraw(
                    texture,
                    Projectile.Center -
                    Main.screenPosition,
                    null,
                    new Color(
                        255,
                        250,
                        180,
                        innerAlpha
                    ),
                    Main.GameUpdateCount *
                    -0.08f,
                    texture.Size() * 0.5f,
                    innerScale,
                    SpriteEffects.None,
                    0f
                );


                return false;
            }


            // NORMAL CORE

            float pulse =
                1f +
                (
                    (float)Math.Sin(
                        Main.GameUpdateCount *
                        0.10f
                    )
                    *
                    0.08f
                );


            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                new Color(
                    255,
                    220,
                    70,
                    75
                ),
                Main.GameUpdateCount *
                0.02f,
                texture.Size() * 0.5f,
                0.30f *
                pulse,
                SpriteEffects.None,
                0f
            );


            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                new Color(
                    255,
                    240,
                    120,
                    220
                ),
                Main.GameUpdateCount *
                -0.025f,
                texture.Size() * 0.5f,
                0.06f *
                pulse,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}