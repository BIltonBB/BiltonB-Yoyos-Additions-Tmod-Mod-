using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class DestroyerYoyoProjClone : ModProjectile
    {
        // WORM SETTINGS

        private const int MaxClones = 10;

        private const float SegmentDistance = 40f;

        private const float MaximumSegmentDistance = 55f;

        private const float HardCatchupDistance = 80f;

        private const float OrbitRadius = 55f;

        private const float OrbitSpeed = 0.025f;


        // RETURN STATE

        private const float StateOrbit = 0f;

        private const float StateChase = 1f;

        private const float StateReturning = 2f;

        private const float ReturnSpeed = 6f;

        private const float ReturnAcceleration = 0.10f;

        private const float OrbitSnapDistance = 8f;


        // CHASE SETTINGS

        private const float ChaseMaxSpeed = 7f;

        private const float ChaseAcceleration = 0.065f;

        /*
         * Maximum 30 degree turning.
         */
        private const float MaximumTurn =
            MathHelper.Pi / 6f;


        // FOLLOW SETTINGS

        private const float FollowAcceleration = 0.22f;

        private const float FollowMaxSpeed = 8.5f;


        // LASERS

        private const int LaserCooldown = 90;

        private int laserTimer;


        // AFTERIMAGES

        private const int MaxAfterimages = 8;

        private const float AfterimageSpacing = 9f;

        private Vector2[] afterimagePositions =
            new Vector2[MaxAfterimages];

        private bool[] afterimageValid =
            new bool[MaxAfterimages];

        private int afterimageCount;

        private Vector2 lastHistoryPosition;

        private bool historyInitialized;


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

            Projectile.timeLeft = 999999;

            ProjectileID.Sets.TrailingMode[
                Projectile.type
            ] = 0;

            ProjectileID.Sets.TrailCacheLength[
                Projectile.type
            ] = 8;
        }


        // AI

        public override void AI()
        {
            int parentIndex =
                (int)Projectile.ai[0];


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
                    DestroyerYoyoProj>() ||
                parent.owner != Projectile.owner
            )
            {
                Projectile.Kill();
                return;
            }


            Projectile.timeLeft = 10;


            int cloneIndex =
                (int)Projectile.ai[1];


            // INITIALIZE HISTORY

            if (!historyInitialized)
            {
                lastHistoryPosition =
                    Projectile.Center;

                historyInitialized = true;
            }


            // TARGET

            DestroyerYoyoProj mainYoyo =
                parent.ModProjectile
                as DestroyerYoyoProj;


            bool chasing =
                mainYoyo != null &&
                mainYoyo.CloneTarget >= 0 &&
                mainYoyo.CloneChaseTimer > 0;


            // MOVEMENT STATE

            int currentState =
                (int)Projectile.localAI[0];


            // CHASING

            if (chasing)
            {
                Projectile.localAI[0] =
                    StateChase;


                ChaseState(
                    parent,
                    cloneIndex
                );
            }


            // CHASE ENDED

            else
            {
                /*
                 * Don't instantly start orbiting.
                 *
                 * Instead enter a RETURNING state.
                 */
                if (
                    currentState ==
                    (int)StateChase
                )
                {
                    Projectile.localAI[0] =
                        StateReturning;
                }


                if (
                    Projectile.localAI[0] ==
                    StateReturning
                )
                {
                    ReturnToOrbit(
                        parent,
                        cloneIndex
                    );
                }
                else
                {
                    OrbitState(
                        parent,
                        cloneIndex
                    );
                }
            }


            // AFTERIMAGE HISTORY

            UpdateAfterimageHistory();


            // LASER

            laserTimer++;


            if (
                laserTimer >= LaserCooldown &&
                Main.myPlayer == Projectile.owner
            )
            {
                laserTimer = 0;

                FireLaser(parent);
            }


            // RED LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.70f,
                0.025f,
                0.025f
            );
        }


        // AFTERIMAGE HISTORY

        private void UpdateAfterimageHistory()
        {
            float distance =
                Vector2.Distance(
                    Projectile.Center,
                    lastHistoryPosition
                );


            if (
                distance <
                AfterimageSpacing
            )
            {
                return;
            }


            lastHistoryPosition =
                Projectile.Center;


            for (
                int i = MaxAfterimages - 1;
                i > 0;
                i--
            )
            {
                afterimagePositions[i] =
                    afterimagePositions[i - 1];

                afterimageValid[i] =
                    afterimageValid[i - 1];
            }


            afterimagePositions[0] =
                Projectile.Center;

            afterimageValid[0] =
                true;


            afterimageCount =
                Math.Min(
                    afterimageCount + 1,
                    MaxAfterimages
                );
        }


        // GET ORBIT POSITION

        private Vector2 GetOrbitPosition(
            Projectile parent,
            int cloneIndex)
        {
            float angle =
                Main.GameUpdateCount *
                OrbitSpeed;


            angle +=
                MathHelper.TwoPi *
                cloneIndex /
                MaxClones;


            return parent.Center +
                   new Vector2(
                       OrbitRadius,
                       0f
                   ).RotatedBy(angle);
        }


        // ORBIT STATE

        private void OrbitState(
            Projectile parent,
            int cloneIndex)
        {
            Vector2 desiredPosition =
                GetOrbitPosition(
                    parent,
                    cloneIndex
                );


            Vector2 difference =
                desiredPosition -
                Projectile.Center;


            float distance =
                difference.Length();


            float strength;


            if (distance > 120f)
            {
                strength = 0.30f;
            }
            else if (distance > 75f)
            {
                strength = 0.18f;
            }
            else
            {
                strength = 0.10f;
            }


            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    difference * strength,
                    0.16f
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


        // RETURN TO ORBIT

        private void ReturnToOrbit(
            Projectile parent,
            int cloneIndex)
        {
            // CLONE A

            if (cloneIndex == 0)
            {
                Vector2 orbitPosition =
                    GetOrbitPosition(
                        parent,
                        cloneIndex
                    );


                Vector2 difference =
                    orbitPosition -
                    Projectile.Center;


                float distance =
                    difference.Length();


                if (
                    distance <=
                    OrbitSnapDistance
                )
                {
                    Projectile.Center =
                        orbitPosition;

                    Projectile.velocity =
                        Vector2.Zero;

                    Projectile.localAI[0] =
                        StateOrbit;

                    return;
                }


                MoveTowardPosition(
                    orbitPosition,
                    ReturnSpeed,
                    ReturnAcceleration
                );


                return;
            }


            // OTHER CLONES

            int previousIndex =
                FindClone(
                    cloneIndex - 1
                );


            if (previousIndex == -1)
            {
                return;
            }


            Projectile previous =
                Main.projectile[
                    previousIndex
                ];


            /*
             * A segment follows its predecessor while the predecessor
             * is still returning.
             */
            bool previousStillReturning =
                (int)previous.localAI[0] ==
                (int)StateReturning;


            if (previousStillReturning)
            {
                FollowPreviousClone(
                    previousIndex
                );

                return;
            }


            /*
             * Once the predecessor has reached its orbital state,
             * this clone is allowed to settle into its own orbit.
             */
            if (
                (int)previous.localAI[0] ==
                (int)StateOrbit
            )
            {
                Vector2 orbitPosition =
                    GetOrbitPosition(
                        parent,
                        cloneIndex
                    );


                Vector2 difference =
                    orbitPosition -
                    Projectile.Center;


                float distance =
                    difference.Length();


                if (
                    distance <=
                    OrbitSnapDistance
                )
                {
                    Projectile.Center =
                        orbitPosition;

                    Projectile.velocity =
                        Vector2.Zero;

                    Projectile.localAI[0] =
                        StateOrbit;

                    return;
                }


                MoveTowardPosition(
                    orbitPosition,
                    ReturnSpeed,
                    ReturnAcceleration
                );
            }
        }


        // MOVE TOWARD POSITION

        private void MoveTowardPosition(
            Vector2 destination,
            float maxSpeed,
            float acceleration)
        {
            Vector2 difference =
                destination -
                Projectile.Center;


            float distance =
                difference.Length();


            if (distance < 0.01f)
                return;


            Vector2 direction =
                difference /
                distance;


            float speed =
                MathHelper.Clamp(
                    distance / 30f,
                    0.5f,
                    maxSpeed
                );


            Vector2 desiredVelocity =
                direction *
                speed;


            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    desiredVelocity,
                    acceleration
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


        // CHASE STATE

        private void ChaseState(
            Projectile parent,
            int cloneIndex)
        {
            if (cloneIndex == 0)
            {
                ChaseAsHead(parent);
            }
            else
            {
                FollowPreviousClone(
                    FindClone(
                        cloneIndex - 1
                    )
                );
            }
        }


        // HEAD CHASE

        private void ChaseAsHead(
            Projectile parent)
        {
            DestroyerYoyoProj mainYoyo =
                parent.ModProjectile
                as DestroyerYoyoProj;


            if (
                mainYoyo == null ||
                mainYoyo.CloneTarget < 0
            )
            {
                return;
            }


            int targetIndex =
                mainYoyo.CloneTarget;


            if (
                targetIndex < 0 ||
                targetIndex >= Main.maxNPCs
            )
            {
                return;
            }


            NPC target =
                Main.npc[targetIndex];


            if (!ValidTarget(target))
                return;


            Vector2 targetPosition =
                target.Center;


            Vector2 toTarget =
                targetPosition -
                Projectile.Center;


            if (
                toTarget.LengthSquared() <
                0.01f
            )
            {
                return;
            }


            Vector2 desiredDirection =
                toTarget.SafeNormalize(
                    Vector2.UnitX
                );


            Vector2 currentDirection;


            if (
                Projectile.velocity.LengthSquared() >
                0.01f
            )
            {
                currentDirection =
                    Projectile.velocity.SafeNormalize(
                        Vector2.UnitX
                    );
            }
            else
            {
                currentDirection =
                    desiredDirection;
            }


            // TURNING

            float currentAngle =
                currentDirection.ToRotation();


            float desiredAngle =
                desiredDirection.ToRotation();


            float angleDifference =
                MathHelper.WrapAngle(
                    desiredAngle -
                    currentAngle
                );


            /*
             * Maximum 30 degrees per tick.
             */
            float turn =
                MathHelper.Clamp(
                    angleDifference,
                    -MaximumTurn,
                    MaximumTurn
                );


            float turnRatio =
                Math.Abs(turn) /
                MaximumTurn;


            float speedMultiplier =
                MathHelper.Lerp(
                    1f,
                    0.50f,
                    turnRatio
                );


            Vector2 newDirection =
                currentDirection.RotatedBy(
                    turn
                );


            Vector2 desiredVelocity =
                newDirection *
                ChaseMaxSpeed *
                speedMultiplier;


            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    desiredVelocity,
                    ChaseAcceleration
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


        // FOLLOW PREVIOUS

        private void FollowPreviousClone(
            int previousIndex)
        {
            if (previousIndex == -1)
                return;


            Projectile previous =
                Main.projectile[
                    previousIndex
                ];


            Vector2 difference =
                previous.Center -
                Projectile.Center;


            float distance =
                difference.Length();


            if (distance < 0.01f)
                return;


            Vector2 direction =
                difference /
                distance;


            Vector2 desiredPosition =
                previous.Center -
                direction *
                SegmentDistance;


            Vector2 correction =
                desiredPosition -
                Projectile.Center;


            // CATCH-UP

            if (
                distance >=
                HardCatchupDistance
            )
            {
                Projectile.velocity =
                    Vector2.Lerp(
                        Projectile.velocity,
                        direction *
                        FollowMaxSpeed,
                        0.38f
                    );
            }

            else if (
                distance >
                MaximumSegmentDistance
            )
            {
                Projectile.velocity =
                    Vector2.Lerp(
                        Projectile.velocity,
                        direction *
                        FollowMaxSpeed,
                        0.32f
                    );
            }

            else
            {
                Projectile.velocity =
                    Vector2.Lerp(
                        Projectile.velocity,
                        correction *
                        0.20f,
                        FollowAcceleration
                    );
            }


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


        // FIND CLONE

        private int FindClone(
            int cloneIndex)
        {
            int cloneType =
                ModContent.ProjectileType<
                    DestroyerYoyoProjClone>();


            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile projectile =
                    Main.projectile[i];


                if (
                    projectile.active &&
                    projectile.type == cloneType &&
                    projectile.owner == Projectile.owner &&
                    (int)projectile.ai[0] ==
                        (int)Projectile.ai[0] &&
                    (int)projectile.ai[1] ==
                        cloneIndex
                )
                {
                    return i;
                }
            }


            return -1;
        }


        // VALID TARGET

        private bool ValidTarget(
            NPC npc)
        {
            return
                npc != null &&
                npc.active &&
                !npc.friendly &&
                !npc.dontTakeDamage &&
                npc.lifeMax > 5;
        }


        // LASER

        private void FireLaser(
            Projectile parent)
        {
            DestroyerYoyoProj mainYoyo =
                parent.ModProjectile
                as DestroyerYoyoProj;


            int targetIndex;


            if (
                mainYoyo == null ||
                mainYoyo.CloneTarget < 0
            )
            {
                return;
            }


            targetIndex =
                mainYoyo.CloneTarget;


            if (
                targetIndex < 0 ||
                targetIndex >= Main.maxNPCs
            )
            {
                return;
            }


            NPC target =
                Main.npc[targetIndex];


            if (!ValidTarget(target))
                return;


            Vector2 direction =
                target.Center -
                Projectile.Center;


            if (
                direction.LengthSquared() <
                0.01f
            )
            {
                return;
            }


            direction.Normalize();


            // RANDOM AIM OFFSET

            /*
             * Slightly inaccurate shooting.
             *
             * ±7 degrees.
             */
            float randomAngle =
                Main.rand.NextFloat(
                    -MathHelper.ToRadians(7f),
                    MathHelper.ToRadians(7f)
                );


            direction =
                direction.RotatedBy(
                    randomAngle
                );


            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction * 14f,
                ModContent.ProjectileType<
                    DestroyerYoyoProjLaser>(),
                Projectile.damage / 2,
                0f,
                Projectile.owner
            );
        }


        // HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            Projectile parent =
                Main.projectile[
                    (int)Projectile.ai[0]
                ];


            if (!parent.active)
                return;


            if (
                parent.ModProjectile
                    is DestroyerYoyoProj mainYoyo
            )
            {
                mainYoyo.SetCloneHitTarget(
                    target.whoAmI
                );
            }
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            int cloneIndex =
                (int)Projectile.ai[1];


            // STRING

            if (cloneIndex > 0)
            {
                int previousIndex =
                    FindClone(
                        cloneIndex - 1
                    );


                if (previousIndex != -1)
                {
                    Projectile previous =
                        Main.projectile[
                            previousIndex
                        ];


                    Vector2 start =
                        previous.Center -
                        Main.screenPosition;


                    Vector2 end =
                        Projectile.Center -
                        Main.screenPosition;


                    Vector2 difference =
                        end -
                        start;


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
                            TextureAssets
                                .MagicPixel
                                .Value;


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
                                70,
                                70,
                                70,
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
                                150,
                                150,
                                150,
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
                }
            }


            // TEXTURE

            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/DestroyerYoyoProjClone"
                ).Value;


            // DISTANCE-BASED AFTERIMAGES

            for (
                int i = afterimageCount - 1;
                i >= 0;
                i--
            )
            {
                if (!afterimageValid[i])
                    continue;


                Vector2 position =
                    afterimagePositions[i];


                float age =
                    i /
                    (float)MaxAfterimages;


                float alpha =
                    MathHelper.Lerp(
                        0.28f,
                        0.035f,
                        age
                    );


                Color worldLight =
                    Lighting.GetColor(
                        (int)(position.X / 16f),
                        (int)(position.Y / 16f)
                    );


                Color afterimageColor =
                    Color.Lerp(
                        Color.White,
                        worldLight,
                        0.35f
                    );


                afterimageColor =
                    Color.Lerp(
                        afterimageColor,
                        new Color(
                            255,
                            70,
                            55
                        ),
                        0.15f
                    );


                afterimageColor *=
                    alpha;


                // Shadow.
                Main.spriteBatch.Draw(
                    texture,
                    position -
                    Main.screenPosition +
                    new Vector2(
                        2f,
                        2f
                    ),
                    null,
                    new Color(
                        25,
                        0,
                        0,
                        150
                    ) * alpha,
                    Projectile.rotation,
                    texture.Size() * 0.5f,
                    Projectile.scale,
                    SpriteEffects.None,
                    0f
                );


                // Lit afterimage.
                Main.spriteBatch.Draw(
                    texture,
                    position -
                    Main.screenPosition,
                    null,
                    afterimageColor,
                    Projectile.rotation,
                    texture.Size() * 0.5f,
                    Projectile.scale,
                    SpriteEffects.None,
                    0f
                );
            }


            // MAIN SHADOW

            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition +
                new Vector2(
                    2f,
                    2f
                ),
                null,
                new Color(
                    15,
                    0,
                    0,
                    180
                ),
                Projectile.rotation,
                texture.Size() * 0.5f,
                Projectile.scale * 1.05f,
                SpriteEffects.None,
                0f
            );


            // MAIN CLONE

            Color cloneColor =
                Color.Lerp(
                    Color.White,
                    lightColor,
                    0.20f
                );


            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                cloneColor,
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