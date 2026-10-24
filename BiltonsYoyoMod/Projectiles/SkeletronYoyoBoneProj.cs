using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class SkeletronYoyoBoneProj : ModProjectile
    {
        // AFTERIMAGE SETTINGS

        private const float AfterimageSpacing = 8f;
        private const int AfterimageCount = 6;

        private Vector2[] afterimagePositions =
            new Vector2[AfterimageCount];

        private float afterimageDistance;
        private bool afterimagesInitialized;


        // BLUE DUST SETTINGS

        private float dustDistance;

        private const float DustSpacing = 12f;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.tileCollide = false;

            Projectile.timeLeft = 1200;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }


        // AI

        public override void AI()
        {
            // ai[0] = parent yoyo index
            // ai[1] = permanent orbit angle
            // ai[2] = current chase target NPC index

            int parentIndex =
                (int)Projectile.ai[0];


            // SAFE PARENT INDEX CHECK

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
                parent.type !=
                    ModContent.ProjectileType<
                        SkeletronYoyoProj
                    >() ||
                parent.owner != Projectile.owner
            )
            {
                Projectile.Kill();
                return;
            }


            Projectile.timeLeft = 1200;


            // SETTINGS

            const float radius = 40f;

            const float orbitSpeed = 0.07f;

            const float maxChaseSpeed = 7f;
            const float chaseAcceleration = 0.10f;

            const float returnSpeed = 7f;
            const float returnAcceleration = 0.10f;

            const float spinSpeed = 0.10f;


            // BLUE LIGHT

            // This is actual Terraria lighting rather than a copied
            // blue version of the bone texture.
            Lighting.AddLight(
                Projectile.Center,
                0.08f,
                0.32f,
                0.75f
            );


            // STATE

            // 0 = orbit
            // 1 = chase
            // 2 = return to orbit
            // 3 = stopped after hitting target

            float state =
                Projectile.localAI[0];


            // ORBIT STATE

            if (state == 0f)
            {
                float orbitAngle =
                    Projectile.ai[1];


                orbitAngle +=
                    orbitSpeed;


                if (
                    orbitAngle >=
                    MathHelper.TwoPi
                )
                {
                    orbitAngle -=
                        MathHelper.TwoPi;
                }


                Projectile.ai[1] =
                    orbitAngle;


                Vector2 orbitOffset =
                    new Vector2(
                        (float)Math.Cos(
                            orbitAngle
                        ),
                        (float)Math.Sin(
                            orbitAngle
                        )
                    ) *
                    radius;


                Vector2 orbitPosition =
                    parent.Center +
                    orbitOffset;


                Projectile.Center =
                    orbitPosition;


                Projectile.rotation +=
                    spinSpeed;


                return;
            }


            // CHASE STATE

            if (state == 1f)
            {
                Projectile.localAI[1]++;


                // 30 seconds.
                if (
                    Projectile.localAI[1] >=
                    1800f
                )
                {
                    Projectile.localAI[0] =
                        2f;

                    return;
                }


                int targetIndex =
                    (int)Projectile.ai[2];


                if (
                    targetIndex >= 0 &&
                    targetIndex < Main.maxNPCs
                )
                {
                    NPC target =
                        Main.npc[targetIndex];


                    if (
                        target.active &&
                        !target.friendly
                    )
                    {
                        Vector2 direction =
                            target.Center -
                            Projectile.Center;


                        if (
                            direction.LengthSquared() >
                            1f
                        )
                        {
                            direction.Normalize();


                            Vector2 desiredVelocity =
                                direction *
                                maxChaseSpeed;


                            Projectile.velocity =
                                Vector2.Lerp(
                                    Projectile.velocity,
                                    desiredVelocity,
                                    chaseAcceleration
                                );


                            Projectile.Center +=
                                Projectile.velocity;
                        }
                    }
                }


                Projectile.rotation +=
                    spinSpeed;


                return;
            }


            // STOPPED AFTER HIT

            if (state == 3f)
            {
                Projectile.localAI[1]++;


                Projectile.velocity *=
                    0.90f;


                Projectile.rotation +=
                    spinSpeed *
                    (
                        Projectile.velocity.Length() /
                        12f
                    );


                // 2 second pause.
                if (
                    Projectile.localAI[1] >=
                    120f
                )
                {
                    Projectile.localAI[0] =
                        1f;
                }


                return;
            }


            // RETURN TO ORBIT

            if (state == 2f)
            {
                float assignedAngle =
                    Projectile.ai[1];


                Vector2 assignedOffset =
                    new Vector2(
                        (float)Math.Cos(
                            assignedAngle
                        ),
                        (float)Math.Sin(
                            assignedAngle
                        )
                    ) *
                    radius;


                Vector2 assignedPosition =
                    parent.Center +
                    assignedOffset;


                Vector2 toOrbit =
                    assignedPosition -
                    Projectile.Center;


                if (
                    toOrbit.Length() >
                    4f
                )
                {
                    Vector2 desiredVelocity =
                        toOrbit;


                    desiredVelocity.Normalize();

                    desiredVelocity *=
                        returnSpeed;


                    Projectile.velocity =
                        Vector2.Lerp(
                            Projectile.velocity,
                            desiredVelocity,
                            returnAcceleration
                        );


                    Projectile.Center +=
                        Projectile.velocity;
                }
                else
                {
                    Projectile.Center =
                        assignedPosition;

                    Projectile.velocity =
                        Vector2.Zero;

                    Projectile.localAI[0] =
                        0f;
                }


                Projectile.rotation +=
                    spinSpeed;


                return;
            }
        }


        // POST AI

        public override void PostAI()
        {
            // INITIALIZE AFTERIMAGES

            if (!afterimagesInitialized)
            {
                for (
                    int i = 0;
                    i < AfterimageCount;
                    i++
                )
                {
                    afterimagePositions[i] =
                        Projectile.Center;
                }


                afterimagesInitialized = true;

                afterimageDistance = 0f;
                dustDistance = 0f;

                return;
            }


            // DISTANCE TRACKING

            float movementDistance =
                Vector2.Distance(
                    Projectile.Center,
                    afterimagePositions[0]
                );


            afterimageDistance +=
                movementDistance;


            dustDistance +=
                movementDistance;


            // AFTERIMAGE POSITION UPDATE

            if (
                afterimageDistance >=
                AfterimageSpacing
            )
            {
                for (
                    int i =
                        AfterimageCount - 1;
                    i > 0;
                    i--
                )
                {
                    afterimagePositions[i] =
                        afterimagePositions[i - 1];
                }


                afterimagePositions[0] =
                    Projectile.Center;


                afterimageDistance = 0f;
            }


            // SMALL BLUE MAGICAL DUST

            if (
                dustDistance >=
                DustSpacing &&
                Projectile.velocity.Length() >
                0.5f
            )
            {
                dustDistance = 0f;


                Vector2 dustPosition =
                    Projectile.Center +
                    Main.rand.NextVector2Circular(
                        3f,
                        3f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        dustPosition,
                        DustID.BlueTorch,
                        -Projectile.velocity *
                        0.08f,
                        120,
                        new Color(
                            80,
                            180,
                            255
                        ),
                        0.9f
                    );


                dust.noGravity = true;

                dust.fadeIn = 1.1f;


                dust.velocity +=
                    Main.rand.NextVector2Circular(
                        0.35f,
                        0.35f
                    );
            }
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/SkeletronYoyoBoneProj"
                ).Value;


            // DISTANCE-BASED AFTERIMAGES

            if (afterimagesInitialized)
            {
                for (
                    int i =
                        AfterimageCount - 1;
                    i >= 0;
                    i--
                )
                {
                    Vector2 position =
                        afterimagePositions[i];


                    if (
                        position ==
                        Vector2.Zero
                    )
                    {
                        continue;
                    }


                    float fade =
                        0.38f *
                        (
                            1f -
                            (float)i /
                            AfterimageCount
                        );


                    Color lighting =
                        Lighting.GetColor(
                            (int)(
                                position.X /
                                16f
                            ),
                            (int)(
                                position.Y /
                                16f
                            )
                        );


                    Color afterimageColor =
                        lighting *
                        fade;


                    Main.spriteBatch.Draw(
                        texture,
                        position -
                        Main.screenPosition,
                        null,
                        afterimageColor,
                        Projectile.rotation,
                        texture.Size() / 2f,
                        Projectile.scale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }


            // MAIN BONE

            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                texture.Size() / 2f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );


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
                120
            );


            /*
             * When a bone hits its current target:
             *
             * 1. Stop.
             * 2. Decelerate.
             * 3. Wait 2 seconds.
             * 4. Resume chasing.
             *
             * The main yoyo controls the target.
             */

            if (
                Projectile.localAI[0] ==
                1f
            )
            {
                Projectile.localAI[0] =
                    3f;

                Projectile.localAI[1] =
                    0f;
            }
        }
    }
}