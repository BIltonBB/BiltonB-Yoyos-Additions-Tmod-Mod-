using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class DestroyerYoyoProjClone2 : ModProjectile
    {
        // WORM SETTINGS

        private const int MaxClones = 10;

        private const float SegmentDistance = 40f;

        private const float MaximumSegmentDistance = 55f;

        private const float HardCatchupDistance = 80f;


        // ORBIT

        /*
         * Worm 2 stays farther away from the main yoyo than Worm 1.
         *
         * The 180 degree phase offset places Worm 2 opposite
         * Worm 1 while orbiting.
         */

        private const float OrbitRadius = 95f;

        private const float OrbitSpeed = 0.025f;

        private const float OrbitPhaseOffset =
            MathHelper.Pi;


        // FOLLOW SETTINGS

        /*
         * These are deliberately the same style as Worm 1.
         */

        private const float FollowAcceleration = 0.22f;

        private const float FollowMaxSpeed = 8.5f;


        // WORM 1 STYLE CHASE SETTINGS

        /*
         * Same basic movement values as Worm 1.
         */

        private const float ChaseMaxSpeed = 7f;

        private const float ChaseAcceleration = 0.065f;

        /*
         * Maximum turning angle per tick.
         *
         * 30 degrees.
         */

        private const float MaximumTurn =
            MathHelper.Pi / 6f;


        // TARGETING

        /*
         * Search around the MAIN YOYO.
         *
         * 1000 pixels = 62.5 tiles.
         */

        private const float TargetDetectionRadius = 1000f;


        // ATTACK

        /*
         * Move to exactly 300 pixels ABOVE the target.
         */

        private const float AttackHeight = 300f;


        /*
         * How close the head must get before beginning the
         * downward strike.
         */

        private const float AttackPositionTolerance = 10f;


        /*
         * After passing 100 pixels below the target, the strike
         * is complete.
         */

        private const float StrikeOvershoot = 100f;


        /*
         * Prevent the head from getting stuck trying to reach
         * the positioning point forever.
         */

        private const int PositionTimeout = 240;


        // STATES

        private const int StateOrbit = 0;

        private const int StatePositioning = 1;

        private const int StateStriking = 2;


        // ATTACK DATA

        /*
         * These values are only meaningful for Clone 0.
         */

        private int attackStateTimer;

        private int targetNPC = -1;


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

            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
        }


        // AI

        public override void AI()
        {
            // GET MAIN YOYO

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
                ModContent.ProjectileType<DestroyerYoyoProj>() ||
                parent.owner != Projectile.owner
            )
            {
                Projectile.Kill();
                return;
            }


            /*
             * Stay alive as long as the main yoyo exists.
             */

            Projectile.timeLeft = 10;


            // CLONE INDEX

            int cloneIndex =
                (int)Projectile.ai[1];


            // INITIALIZE HISTORY

            if (!historyInitialized)
            {
                lastHistoryPosition =
                    Projectile.Center;

                historyInitialized = true;
            }


            // HEAD / BODY

            if (cloneIndex == 0)
            {
                UpdateHeadAI(parent);
            }
            else
            {
                UpdateBodyAI(
                    parent,
                    cloneIndex
                );
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

                FireLaser();
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.70f,
                0.025f,
                0.025f
            );
        }


        // HEAD AI

        private void UpdateHeadAI(
            Projectile parent)
        {
            int state =
                (int)Projectile.localAI[0];


            // FIND NEAREST ENEMY

            int nearestTarget =
                FindTarget(
                    parent.Center,
                    TargetDetectionRadius
                );


            // NO ENEMIES

            if (nearestTarget == -1)
            {
                /*
                 * No enemy around the main yoyo.
                 *
                 * Snap the head directly into its orbital position.
                 */

                SnapToOrbit(
                    parent,
                    0
                );


                targetNPC = -1;

                attackStateTimer = 0;

                Projectile.localAI[0] =
                    StateOrbit;

                return;
            }


            // ORBIT

            if (state == StateOrbit)
            {
                /*
                 * Enemy found.
                 *
                 * Always choose the nearest enemy.
                 */

                targetNPC =
                    nearestTarget;

                attackStateTimer = 0;

                Projectile.localAI[0] =
                    StatePositioning;

                Projectile.netUpdate = true;

                return;
            }


            // POSITIONING

            if (state == StatePositioning)
            {
                attackStateTimer++;


                /*
                 * Keep selecting the nearest enemy while positioning.
                 */

                targetNPC =
                    nearestTarget;


                NPC target =
                    Main.npc[targetNPC];


                /*
                 * Target point is 300 pixels above the enemy.
                 */

                Vector2 attackPosition =
                    target.Center +
                    new Vector2(
                        0f,
                        -AttackHeight
                    );


                Vector2 toTarget =
                    attackPosition -
                    Projectile.Center;


                if (
                    toTarget.LengthSquared() >
                    0.01f
                )
                {
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


                    // SAME TURNING SYSTEM AS WORM 1

                    float currentAngle =
                        currentDirection.ToRotation();


                    float desiredAngle =
                        desiredDirection.ToRotation();


                    float angleDifference =
                        MathHelper.WrapAngle(
                            desiredAngle -
                            currentAngle
                        );


                    float turn =
                        MathHelper.Clamp(
                            angleDifference,
                            -MaximumTurn,
                            MaximumTurn
                        );


                    float turnRatio =
                        Math.Abs(turn) /
                        MaximumTurn;


                    /*
                     * Same slight slowing while turning sharply
                     * as Worm 1.
                     */

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


                // REACHED 300 PIXELS ABOVE

                float distance =
                    Vector2.Distance(
                        Projectile.Center,
                        attackPosition
                    );


                if (
                    distance <=
                    AttackPositionTolerance ||
                    attackStateTimer >= PositionTimeout
                )
                {
                    /*
                     * Recalculate one last time so a moving target
                     * doesn't cause a bad attack height.
                     */

                    if (ValidStoredTarget())
                    {
                        target =
                            Main.npc[targetNPC];

                        attackPosition =
                            target.Center +
                            new Vector2(
                                0f,
                                -AttackHeight
                            );
                    }


                    /*
                     * Snap only the head to its attack point.
                     */

                    Projectile.Center =
                        attackPosition;


                    /*
                     * Begin turning directly downward.
                     *
                     * We do NOT instantly teleport the whole worm.
                     */

                    Projectile.velocity =
                        Vector2.UnitY *
                        ChaseMaxSpeed;


                    Projectile.rotation =
                        MathHelper.PiOver2;


                    attackStateTimer = 0;

                    Projectile.localAI[0] =
                        StateStriking;

                    Projectile.netUpdate = true;

                    return;
                }


                return;
            }


            // STRIKING

            if (state == StateStriking)
            {
                attackStateTimer++;


                /*
                 * Strike direction is DOWN.
                 *
                 * We use the SAME turning system as Worm 1 so the
                 * head doesn't make an impossible instant turn.
                 */

                Vector2 desiredDirection =
                    Vector2.UnitY;


                Vector2 currentDirection;


                if (
                    Projectile.velocity.LengthSquared() >
                    0.01f
                )
                {
                    currentDirection =
                        Projectile.velocity.SafeNormalize(
                            Vector2.UnitY
                        );
                }
                else
                {
                    currentDirection =
                        Vector2.UnitY;
                }


                // SAME 30 DEGREE TURN LIMIT

                float currentAngle =
                    currentDirection.ToRotation();


                float desiredAngle =
                    desiredDirection.ToRotation();


                float angleDifference =
                    MathHelper.WrapAngle(
                        desiredAngle -
                        currentAngle
                    );


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


                // TARGET DISAPPEARED

                if (!ValidStoredTarget())
                {
                    int nextTarget =
                        FindTarget(
                            parent.Center,
                            TargetDetectionRadius
                        );


                    if (nextTarget != -1)
                    {
                        targetNPC =
                            nextTarget;

                        attackStateTimer = 0;

                        Projectile.localAI[0] =
                            StatePositioning;

                        Projectile.netUpdate = true;

                        return;
                    }


                    /*
                     * Nothing left to attack.
                     */

                    SnapToOrbit(
                        parent,
                        0
                    );


                    targetNPC = -1;

                    attackStateTimer = 0;

                    Projectile.localAI[0] =
                        StateOrbit;

                    Projectile.netUpdate = true;

                    return;
                }


                // OVERSHOOT CHECK

                NPC target =
                    Main.npc[targetNPC];


                float distancePastTarget =
                    Projectile.Center.Y -
                    target.Center.Y;


                if (
                    distancePastTarget >=
                    StrikeOvershoot
                )
                {
                    /*
                     * The strike is complete.
                     *
                     * Immediately find the nearest target again.
                     */

                    int nextTarget =
                        FindTarget(
                            parent.Center,
                            TargetDetectionRadius
                        );


                    if (nextTarget != -1)
                    {
                        targetNPC =
                            nextTarget;

                        attackStateTimer = 0;

                        Projectile.localAI[0] =
                            StatePositioning;

                        Projectile.netUpdate = true;

                        return;
                    }


                    /*
                     * No enemies.
                     *
                     * Snap the head back into orbit.
                     */

                    SnapToOrbit(
                        parent,
                        0
                    );


                    targetNPC = -1;

                    attackStateTimer = 0;

                    Projectile.localAI[0] =
                        StateOrbit;

                    Projectile.netUpdate = true;

                    return;
                }


                return;
            }
        }


        // BODY AI

        private void UpdateBodyAI(
            Projectile parent,
            int cloneIndex)
        {
            /*
             * If there are no enemies around the main yoyo,
             * immediately restore the complete orbital formation.
             */

            int nearestTarget =
                FindTarget(
                    parent.Center,
                    TargetDetectionRadius
                );


            if (nearestTarget == -1)
            {
                SnapToOrbit(
                    parent,
                    cloneIndex
                );

                return;
            }


            // FIND HEAD

            int headIndex =
                FindClone(0);


            if (headIndex == -1)
            {
                /*
                 * Missing head.
                 *
                 * Do not attempt Main.projectile[-1].
                 */

                SnapToOrbit(
                    parent,
                    cloneIndex
                );

                return;
            }


            Projectile head =
                Main.projectile[headIndex];


            int headState =
                (int)head.localAI[0];


            // HEAD ORBIT

            if (headState == StateOrbit)
            {
                /*
                 * Maintain the normal worm formation.
                 */

                FollowPreviousClone(
                    cloneIndex
                );

                return;
            }


            // HEAD ATTACKING

            /*
             * Positioning and striking both use normal worm following.
             */

            FollowPreviousClone(
                cloneIndex
            );
        }


        // SNAP TO ORBIT

        private void SnapToOrbit(
            Projectile parent,
            int cloneIndex)
        {
            Vector2 orbitPosition =
                GetOrbitPosition(
                    parent,
                    cloneIndex
                );


            Projectile.Center =
                orbitPosition;


            Projectile.velocity =
                Vector2.Zero;


            /*
             * Face along the orbital formation.
             */

            Vector2 nextPosition =
                GetOrbitPosition(
                    parent,
                    cloneIndex + 1
                );


            Vector2 direction =
                nextPosition -
                orbitPosition;


            if (
                direction.LengthSquared() >
                0.01f
            )
            {
                Projectile.rotation =
                    direction.ToRotation();
            }
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
                OrbitPhaseOffset;


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


        // FOLLOW PREVIOUS CLONE

        private void FollowPreviousClone(
            int cloneIndex)
        {
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


            Vector2 difference =
                previous.Center -
                Projectile.Center;


            float distance =
                difference.Length();


            if (distance < 0.01f)
            {
                return;
            }


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


            // HARD CATCHUP

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

            // NORMAL CATCHUP

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

            // NORMAL FOLLOW

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
                    DestroyerYoyoProjClone2>();


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


        // FIND TARGET

        private int FindTarget(
            Vector2 center,
            float radius)
        {
            int closest =
                -1;


            float closestDistance =
                radius;


            for (
                int i = 0;
                i < Main.maxNPCs;
                i++
            )
            {
                NPC npc =
                    Main.npc[i];


                if (!ValidTarget(npc))
                {
                    continue;
                }


                float distance =
                    Vector2.Distance(
                        center,
                        npc.Center
                    );


                /*
                 * ALWAYS keep the closest valid NPC.
                 */

                if (
                    distance <
                    closestDistance
                )
                {
                    closestDistance =
                        distance;

                    closest =
                        i;
                }
            }


            return closest;
        }


        // VALID STORED TARGET

        private bool ValidStoredTarget()
        {
            if (
                targetNPC < 0 ||
                targetNPC >= Main.maxNPCs
            )
            {
                return false;
            }


            return ValidTarget(
                Main.npc[targetNPC]
            );
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


        // FIRE LASER

        private void FireLaser()
        {
            /*
             * Worm 2 fires at the nearest enemy around its current
             * position, like the previous Worm 2 implementation.
             */

            int targetIndex =
                FindTarget(
                    Projectile.Center,
                    700f
                );


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
            {
                return;
            }


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
            /*
             * Hitting a target does not interrupt the strike.
             *
             * The head continues downward until the 100px overshoot,
             * then chooses the next nearest target.
             */
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
                    "BiltonsYoyoMod/Projectiles/DestroyerYoyoProjClone2"
                ).Value;


            // AFTERIMAGES

            for (
                int i = afterimageCount - 1;
                i >= 0;
                i--
            )
            {
                if (!afterimageValid[i])
                {
                    continue;
                }


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


                // AFTERIMAGE SHADOW

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


                // LIT AFTERIMAGE

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