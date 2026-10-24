using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public class HallowYoyoProjS : ModProjectile
    {
        // ORBIT

        private const float OrbitRadius = 60f;
        private const float OrbitSpeed = 0.035f;

        private const float RadialOscillation = 14f;
        private const float TangentOscillation = 18f;

        private const float RadialOscillationSpeed = 0.085f;
        private const float TangentOscillationSpeed = 0.105f;


        // FOLLOW

        private const float FollowSpeed = 7f;
        private const float FollowAcceleration = 0.08f;
        private const float MovementThreshold = 0.5f;


        // ATTACK

        private const float AttackSpeed = 11f;
        private const float AttackAcceleration = 0.10f;

        private const float HitDistance = 24f;


        // SKY CS

        private const float SkyCSSpeed = 9f;

        // 45 degree spread:
        //
        //       \   |   /
        //        \  |  /
        //         \ | /
        //          \|/
        //           ↑
        //
        // Left  = -45 degrees
        // Middle = 0 degrees
        // Right = +45 degrees

        private const float SkyCSSpread =
            MathHelper.PiOver4;


        // AFTERIMAGES

        private const float AfterimageSpacing = 8f;
        private const int MaxAfterimages = 10;

        private readonly List<Vector2> afterimagePositions =
            new List<Vector2>();


        // STATES

        private const int StateOrbit = 0;
        private const int StateFollowing = 1;
        private const int StateAttacking = 2;

        private int state = StateOrbit;

        private int targetNPC = -1;


        // DATA

        private int parentIndex = -1;
        private int slot = -1;

        private float radialPhase;
        private float tangentPhase;

        private bool initialized;
        private bool hasExploded;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.tileCollide = false;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.timeLeft = 9999;

            Projectile.scale = 0.75f;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }


        // AI

        public override void AI()
        {
            if (parentIndex == -1)
            {
                parentIndex =
                    (int)Projectile.ai[0];
            }

            if (slot == -1)
            {
                slot =
                    (int)Projectile.ai[1];
            }


            // PARENT

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
                ModContent.ProjectileType<HallowYoyoProj>() ||
                parent.owner != Projectile.owner
            )
            {
                Projectile.Kill();
                return;
            }


            Projectile.timeLeft = 10;


            // INITIALIZATION

            if (!initialized)
            {
                radialPhase =
                    (Projectile.whoAmI % 7) *
                    0.8f;

                tangentPhase =
                    (Projectile.whoAmI % 11) *
                    0.55f;


                float startingAngle =
                    MathHelper.TwoPi *
                    slot /
                    3f;


                Projectile.Center =
                    parent.Center +
                    startingAngle.ToRotationVector2() *
                    OrbitRadius;


                initialized = true;

                afterimagePositions.Clear();
            }


            // ATTACK

            if (state == StateAttacking)
            {
                AttackTarget(parent);
            }
            else
            {
                bool yoyoMoving =
                    parent.velocity.Length() >
                    MovementThreshold;


                if (yoyoMoving)
                {
                    state =
                        StateFollowing;

                    FollowYoyo(parent);
                }
                else
                {
                    state =
                        StateOrbit;

                    OrbitYoyo(parent);
                }
            }


            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.65f,
                0.85f,
                0.95f
            );


            // AFTERIMAGES

            RecordAfterimageDistance();
        }


        // ORBIT

        private void OrbitYoyo(
            Projectile parent)
        {
            float baseAngle =
                Main.GameUpdateCount *
                OrbitSpeed;


            baseAngle +=
                MathHelper.TwoPi *
                slot /
                3f;


            Vector2 radialDirection =
                baseAngle.ToRotationVector2();


            Vector2 tangentDirection =
                new Vector2(
                    -radialDirection.Y,
                    radialDirection.X
                );


            float radial =
                (float)Math.Sin(
                    Main.GameUpdateCount *
                    RadialOscillationSpeed +
                    radialPhase
                );


            float tangent =
                (float)Math.Sin(
                    Main.GameUpdateCount *
                    TangentOscillationSpeed +
                    tangentPhase
                );


            Vector2 targetPosition =
                parent.Center +
                radialDirection *
                (
                    OrbitRadius +
                    radial *
                    RadialOscillation
                ) +
                tangentDirection *
                (
                    tangent *
                    TangentOscillation
                );


            SmoothMove(
                targetPosition,
                FollowSpeed,
                FollowAcceleration
            );


            FaceAwayFromYoyo(parent);
        }


        // FOLLOW

        private void FollowYoyo(
            Projectile parent)
        {
            float angle =
                MathHelper.TwoPi *
                slot /
                3f;


            Vector2 desiredPosition =
                parent.Center +
                angle.ToRotationVector2() *
                OrbitRadius;


            SmoothMove(
                desiredPosition,
                FollowSpeed,
                FollowAcceleration
            );


            FaceAwayFromYoyo(parent);
        }


        // SMOOTH MOVEMENT

        private void SmoothMove(
            Vector2 destination,
            float maxSpeed,
            float acceleration)
        {
            Vector2 difference =
                destination -
                Projectile.Center;


            if (
                difference.LengthSquared() <
                0.01f
            )
            {
                Projectile.velocity *= 0.90f;
                return;
            }


            Vector2 direction =
                difference.SafeNormalize(
                    Vector2.UnitX
                );


            float distance =
                difference.Length();


            float desiredSpeed =
                Math.Min(
                    maxSpeed,
                    distance * 0.12f
                );


            Vector2 desiredVelocity =
                direction *
                desiredSpeed;


            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    desiredVelocity,
                    acceleration
                );


            Projectile.Center +=
                Projectile.velocity;
        }


        // FACE AWAY

        private void FaceAwayFromYoyo(
            Projectile parent)
        {
            Vector2 direction =
                Projectile.Center -
                parent.Center;


            if (
                direction.LengthSquared() >
                0.01f
            )
            {
                Projectile.rotation =
                    direction.ToRotation();
            }
        }


        // START ATTACK

        public void StartAttack(
            int target)
        {
            if (hasExploded)
                return;


            if (
                target < 0 ||
                target >= Main.maxNPCs
            )
            {
                return;
            }


            NPC npc =
                Main.npc[target];


            if (!ValidTarget(npc))
                return;


            targetNPC =
                target;

            state =
                StateAttacking;


            Projectile.netUpdate =
                true;
        }


        // ATTACK

        private void AttackTarget(
            Projectile parent)
        {
            if (
                targetNPC < 0 ||
                targetNPC >= Main.maxNPCs
            )
            {
                ReturnToOrbit();
                return;
            }


            NPC target =
                Main.npc[targetNPC];


            if (!ValidTarget(target))
            {
                ReturnToOrbit();
                return;
            }


            Vector2 difference =
                target.Center -
                Projectile.Center;


            Vector2 direction =
                difference.SafeNormalize(
                    Vector2.UnitX
                );


            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    direction *
                    AttackSpeed,
                    AttackAcceleration
                );


            Projectile.Center +=
                Projectile.velocity;


            Projectile.rotation =
                direction.ToRotation();


            float distanceAfterMove =
                Vector2.Distance(
                    Projectile.Center,
                    target.Center
                );


            if (
                distanceAfterMove <=
                HitDistance
            )
            {
                ExplodeIntoClusters(target);
            }
        }


        // NPC HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            if (hasExploded)
                return;


            if (!ValidTarget(target))
                return;


            ExplodeIntoClusters(target);
        }


        // EXPLOSION

        private void ExplodeIntoClusters(
            NPC target)
        {
            if (hasExploded)
                return;


            hasExploded = true;


            if (
                Main.myPlayer !=
                Projectile.owner
            )
            {
                Projectile.Kill();
                return;
            }


            // CS DAMAGE

            int clusterDamage =
                Math.Max(
                    1,
                    Projectile.damage / 2
                );


            Vector2 baseDirection =
                (
                    target.Center -
                    Projectile.Center
                ).SafeNormalize(
                    Vector2.UnitX
                );


            int csType =
                ModContent.ProjectileType<
                    HallowYoyoCSProj>();


            // 2 CS TOWARD TARGET

            for (int i = 0; i < 2; i++)
            {
                Vector2 direction =
                    baseDirection.RotatedBy(
                        i == 0
                            ? -0.50f
                            : 0.50f
                    );


                /*
                 * ai[0] = 1 means this CS was spawned
                 * by the Hallow slime.
                 *
                 * This activates its 2-second safe window.
                 */

                Projectile cs =
                    Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        direction * 5f,
                        csType,
                        clusterDamage,
                        Projectile.knockBack,
                        Projectile.owner,
                        1f
                    );


                cs.netUpdate = true;
            }


            // 3 ADDITIONAL CS INTO THE SKY

            Vector2 upward =
                -Vector2.UnitY;


            for (int i = 0; i < 3; i++)
            {
                float angle;

                if (i == 0)
                {
                    angle =
                        -SkyCSSpread;
                }
                else if (i == 1)
                {
                    angle =
                        0f;
                }
                else
                {
                    angle =
                        SkyCSSpread;
                }


                Vector2 direction =
                    upward.RotatedBy(angle);


                /*
                 * These are additional CS projectiles.
                 *
                 * Speed = 9 instead of the normal 5.
                 *
                 * All three also receive ai[0] = 1,
                 * so they receive the same slime-only
                 * 2-second safe window.
                 */

                Projectile skyCS =
                    Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        direction * SkyCSSpeed,
                        csType,
                        clusterDamage,
                        Projectile.knockBack,
                        Projectile.owner,
                        1f
                    );


                skyCS.netUpdate = true;
            }


            // PINK EXPLOSION

            for (int i = 0; i < 32; i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    32f;


                Vector2 velocity =
                    angle.ToRotationVector2() *
                    Main.rand.NextFloat(
                        4f,
                        6f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.PinkTorch,
                        velocity,
                        0,
                        default,
                        Main.rand.NextFloat(
                            1.0f,
                            1.6f
                        )
                    );


                dust.noGravity = true;
            }


            for (int i = 0; i < 20; i++)
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
                            0.8f,
                            1.4f
                        )
                    );


                dust.noGravity = true;
            }


            Lighting.AddLight(
                Projectile.Center,
                1.8f,
                0.30f,
                0.85f
            );


            SoundEngine.PlaySound(
                SoundID.Item27 with
                {
                    Volume = 0.55f,
                    Pitch = 0.10f
                },
                Projectile.Center
            );


            // REGENERATION

            if (
                parentIndex >= 0 &&
                parentIndex < Main.maxProjectiles
            )
            {
                Projectile parent =
                    Main.projectile[parentIndex];


                if (
                    parent.active &&
                    parent.ModProjectile
                    is HallowYoyoProj mainYoyo
                )
                {
                    mainYoyo.NotifySlimeDestroyed(
                        slot
                    );
                }
            }


            Projectile.Kill();
        }


        // RETURN TO ORBIT

        private void ReturnToOrbit()
        {
            targetNPC = -1;

            state = StateOrbit;

            Projectile.velocity =
                Vector2.Zero;
        }


        // VALID TARGET

        private bool ValidTarget(
            NPC npc)
        {
            if (npc == null)
                return false;

            if (!npc.active)
                return false;

            if (npc.friendly)
                return false;

            if (npc.dontTakeDamage)
                return false;

            return true;
        }


        // AFTERIMAGE RECORDING

        private void RecordAfterimageDistance()
        {
            if (afterimagePositions.Count == 0)
            {
                afterimagePositions.Add(
                    Projectile.Center
                );

                return;
            }


            Vector2 latest =
                afterimagePositions[0];


            float distance =
                Vector2.Distance(
                    Projectile.Center,
                    latest
                );


            if (
                distance <
                AfterimageSpacing
            )
            {
                return;
            }


            Vector2 direction =
                (
                    Projectile.Center -
                    latest
                ).SafeNormalize(
                    Vector2.UnitX
                );


            int safety = 0;


            while (
                distance >= AfterimageSpacing &&
                safety < 20
            )
            {
                latest +=
                    direction *
                    AfterimageSpacing;


                afterimagePositions.Insert(
                    0,
                    latest
                );


                if (
                    afterimagePositions.Count >
                    MaxAfterimages
                )
                {
                    afterimagePositions.RemoveAt(
                        afterimagePositions.Count - 1
                    );
                }


                distance =
                    Vector2.Distance(
                        Projectile.Center,
                        latest
                    );


                safety++;
            }
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/HallowYoyoProjS"
                ).Value;


            for (
                int i =
                    afterimagePositions.Count - 1;
                i >= 0;
                i--
            )
            {
                Vector2 position =
                    afterimagePositions[i];


                float fade =
                    1f -
                    (
                        i /
                        (float)MaxAfterimages
                    );


                fade =
                    MathHelper.Clamp(
                        fade,
                        0f,
                        1f
                    );


                Main.spriteBatch.Draw(
                    texture,
                    position -
                    Main.screenPosition +
                    new Vector2(
                        2f,
                        3f
                    ),
                    null,
                    new Color(
                        25,
                        5,
                        30,
                        130
                    ) *
                    (fade * 0.45f),
                    Projectile.rotation,
                    texture.Size() * 0.5f,
                    Projectile.scale,
                    SpriteEffects.None,
                    0f
                );


                Main.spriteBatch.Draw(
                    texture,
                    position -
                    Main.screenPosition,
                    null,
                    new Color(
                        255,
                        120,
                        210,
                        255
                    ) *
                    (fade * 0.35f),
                    Projectile.rotation,
                    texture.Size() * 0.5f,
                    Projectile.scale,
                    SpriteEffects.None,
                    0f
                );
            }


            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition +
                new Vector2(
                    2f,
                    3f
                ),
                null,
                new Color(
                    25,
                    5,
                    30,
                    130
                ),
                Projectile.rotation,
                texture.Size() * 0.5f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );


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