using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class GolemFistYoyoProj : ModProjectile
    {
        // ORBIT

        private const float OrbitDistance = 55f;
        private const float OrbitSpeed = 0.045f;

        // SIZE

        private const float HandScale = 0.50f;

        // ATTACK

        private const float AttackSpeed = 12f;

        /*
         * Maximum turning angle per frame.
         */
        private const float TurnSpeed =
            MathHelper.Pi / 18f;

        // RETURN

        /*
         * Used ONLY while the hand is actually returning
         * from an attack.
         */
        private const float ReturnMaxSpeed = 14f;
        private const float ReturnAcceleration = 0.18f;

        /*
         * Once the hand is this close to its orbit position,
         * it is considered fully returned.
         */
        private const float ReturnCompleteDistance = 5f;

        // SIDE

        /*
         * Projectile.ai[0]
         *
         * 0 = LEFT
         * 1 = RIGHT
         */
        private int OrbitSide
        {
            get
            {
                return (int)Projectile.ai[0];
            }
        }

        // STATE

        /*
         * True only when the hand is currently travelling
         * back toward its orbit position.
         *
         * When false, the hand HARD LOCKS to its orbit.
         */
        private bool returningToOrbit;

        /*
         * Prevents the attack-start effect from repeatedly
         * triggering.
         */
        private bool attackStarted;

        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.tileCollide = false;

            Projectile.timeLeft = 9999;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;

            Projectile.localNPCHitCooldown = 12;

            ProjectileID.Sets.TrailCacheLength[
                Projectile.type
            ] = 8;

            ProjectileID.Sets.TrailingMode[
                Projectile.type
            ] = 2;
        }

        // AI

        public override void AI()
        {
            Projectile.scale =
                HandScale;

            Projectile mainYoyo =
                FindMainYoyo();

            if (mainYoyo == null)
            {
                Projectile.Kill();
                return;
            }

            GolemYoyoProj yoyo =
                mainYoyo.ModProjectile
                as GolemYoyoProj;

            if (yoyo == null)
            {
                Projectile.Kill();
                return;
            }

            GolemYoyoProj.HandAttackState state =
                yoyo.GetHandState();

            // HAND ALREADY HIT

            /*
             * This hand has successfully hit its target.
             *
             * It must smoothly return to orbit.
             */
            if (
                yoyo.HasHandHit(
                    OrbitSide
                )
            )
            {
                attackStarted = false;
                returningToOrbit = true;

                MoveSmoothlyToOrbit(
                    mainYoyo
                );

                return;
            }

            // DETERMINE ATTACK STATE

            bool shouldAttack = false;

            // LEFT HAND

            /*
             * Left hand starts immediately.
             */
            if (
                OrbitSide == 0 &&
                (
                    state ==
                        GolemYoyoProj.HandAttackState.LeftAttacking ||

                    state ==
                        GolemYoyoProj.HandAttackState.BothAttacking
                )
            )
            {
                shouldAttack = true;
            }

            // RIGHT HAND

            /*
             * Right hand starts after the one-second delay.
             */
            if (
                OrbitSide == 1 &&
                state ==
                    GolemYoyoProj.HandAttackState.BothAttacking
            )
            {
                shouldAttack = true;
            }

            // NOT ATTACKING

            if (!shouldAttack)
            {
                attackStarted = false;

                /*
                 * IMPORTANT:
                 *
                 * If the hand is currently returning from
                 * an attack, continue the smooth return.
                 *
                 * Otherwise, this is normal idle orbit,
                 * so HARD SNAP to orbit.
                 */
                if (returningToOrbit)
                {
                    MoveSmoothlyToOrbit(
                        mainYoyo
                    );
                }
                else
                {
                    SnapToOrbit(
                        mainYoyo
                    );
                }

                return;
            }

            // ATTACK START

            if (!attackStarted)
            {
                attackStarted = true;

                /*
                 * The hand is no longer returning.
                 */
                returningToOrbit = false;

                SpawnAttackExplosion(
                    Projectile.Center
                );

                NPC target =
                    yoyo.GetAttackTarget();

                if (target != null)
                {
                    Vector2 direction =
                        target.Center -
                        Projectile.Center;

                    if (
                        direction.LengthSquared() >
                        0.001f
                    )
                    {
                        Projectile.velocity =
                            direction.SafeNormalize(
                                Vector2.UnitX
                            ) *
                            AttackSpeed;
                    }
                }
            }

            // LOCKED TARGET

            NPC lockedTarget =
                yoyo.GetAttackTarget();

            // TARGET DIED / DISAPPEARED

            if (
                lockedTarget == null ||
                !lockedTarget.active ||
                !lockedTarget.CanBeChasedBy()
            )
            {
                /*
                 * DO NOT RETARGET.
                 *
                 * End the entire attack sequence.
                 */
                yoyo.EndHandAttack();

                attackStarted = false;
                returningToOrbit = true;

                /*
                 * Begin smooth return immediately.
                 */
                MoveSmoothlyToOrbit(
                    mainYoyo
                );

                return;
            }

            // TRACK LOCKED TARGET

            Vector2 desired =
                lockedTarget.Center -
                Projectile.Center;

            if (
                desired.LengthSquared() >
                0.001f
            )
            {
                float desiredAngle =
                    desired.ToRotation();

                float currentAngle;

                if (
                    Projectile.velocity.LengthSquared() >
                    0.01f
                )
                {
                    currentAngle =
                        Projectile.velocity.ToRotation();
                }
                else
                {
                    currentAngle =
                        desiredAngle;
                }

                float difference =
                    MathHelper.WrapAngle(
                        desiredAngle -
                        currentAngle
                    );

                float turn =
                    MathHelper.Clamp(
                        difference,
                        -TurnSpeed,
                        TurnSpeed
                    );

                float newAngle =
                    currentAngle +
                    turn;

                /*
                 * Constant attack speed.
                 */
                Projectile.velocity =
                    newAngle.ToRotationVector2()
                    *
                    AttackSpeed;

                Projectile.rotation =
                    newAngle;
            }
        }

        // SNAP TO ORBIT

        private void SnapToOrbit(
            Projectile mainYoyo)
        {
            /*
             * Normal idle behavior.
             *
             * The hand is HARD LOCKED to the orbit.
             *
             * This means the hand will perfectly follow
             * the yoyo even if the yoyo moves suddenly.
             */

            float angle =
                Main.GameUpdateCount *
                OrbitSpeed;

            if (OrbitSide == 1)
            {
                angle +=
                    MathHelper.Pi;
            }

            Vector2 orbitPosition =
                mainYoyo.Center +
                angle.ToRotationVector2()
                *
                OrbitDistance;

            Projectile.Center =
                orbitPosition;

            Projectile.velocity =
                Vector2.Zero;

            Projectile.rotation =
                angle +
                MathHelper.PiOver2;
        }

        // SMOOTH RETURN TO ORBIT

        private void MoveSmoothlyToOrbit(
            Projectile mainYoyo)
        {
            /*
             * Calculate the CURRENT orbit position.
             *
             * We do not lock the hand here because it is
             * specifically returning from an attack.
             */

            float angle =
                Main.GameUpdateCount *
                OrbitSpeed;

            if (OrbitSide == 1)
            {
                angle +=
                    MathHelper.Pi;
            }

            Vector2 orbitPosition =
                mainYoyo.Center +
                angle.ToRotationVector2()
                *
                OrbitDistance;

            Vector2 difference =
                orbitPosition -
                Projectile.Center;

            float distance =
                difference.Length();

            // RETURN COMPLETE

            if (
                distance <=
                ReturnCompleteDistance
            )
            {
                /*
                 * The hand has successfully returned.
                 *
                 * From now on, use HARD SNAP mode again.
                 */
                returningToOrbit = false;

                SnapToOrbit(
                    mainYoyo
                );

                return;
            }

            // MOVE TOWARD ORBIT

            Vector2 desiredVelocity =
                difference.SafeNormalize(
                    Vector2.Zero
                ) *
                ReturnMaxSpeed;

            /*
             * Smooth acceleration toward the orbit.
             */
            Projectile.velocity =
                Vector2.Lerp(
                    Projectile.velocity,
                    desiredVelocity,
                    ReturnAcceleration
                );

            // ROTATION

            if (
                Projectile.velocity.LengthSquared() >
                0.05f
            )
            {
                Projectile.rotation =
                    Projectile.velocity.ToRotation();
            }
            else
            {
                Projectile.rotation =
                    angle +
                    MathHelper.PiOver2;
            }
        }

        // FIND MAIN YOYO

        private Projectile FindMainYoyo()
        {
            int type =
                ModContent.ProjectileType<
                    GolemYoyoProj
                >();

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

        // NPC HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            Projectile mainYoyo =
                FindMainYoyo();

            if (mainYoyo == null)
            {
                return;
            }

            GolemYoyoProj yoyo =
                mainYoyo.ModProjectile
                as GolemYoyoProj;

            if (yoyo == null)
            {
                return;
            }

            NPC lockedTarget =
                yoyo.GetAttackTarget();

            /*
             * Only the originally locked target
             * counts as a successful hit.
             */
            if (
                lockedTarget == null ||
                lockedTarget.whoAmI !=
                    target.whoAmI
            )
            {
                return;
            }

            target.AddBuff(
                BuffID.OnFire,
                300
            );

            SpawnAttackExplosion(
                target.Center
            );

            /*
             * Tell the main yoyo that this hand
             * successfully hit.
             */
            yoyo.ReportHandHit(
                OrbitSide
            );

            /*
             * Begin smooth return.
             */
            returningToOrbit = true;
            attackStarted = false;
        }

        // EXPLOSION

        private void SpawnAttackExplosion(
            Vector2 position)
        {
            for (
                int i = 0;
                i < 18;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Torch,
                        Main.rand.NextVector2Circular(
                            4f,
                            4f
                        ),
                        80,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.4f
                        )
                    );

                dust.noGravity = true;
            }

            for (
                int i = 0;
                i < 8;
                i++
            )
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.Smoke,
                        Main.rand.NextVector2Circular(
                            2f,
                            2f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.5f,
                            0.9f
                        )
                    );

                dust.noGravity = true;
            }

            Terraria.Audio.SoundEngine.PlaySound(
                SoundID.Item14,
                position
            );
        }

        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                TextureAssets.Projectile[
                    Projectile.type
                ].Value;

            // AFTERIMAGES

            for (
                int i = 0;
                i < Projectile.oldPos.Length;
                i++
            )
            {
                Vector2 position =
                    Projectile.oldPos[i] +
                    Projectile.Size * 0.5f -
                    Main.screenPosition;

                float fade =
                    1f -
                    (
                        i /
                        (float)Projectile.oldPos.Length
                    );

                byte alpha =
                    (byte)MathHelper.Clamp(
                        38f * fade,
                        0f,
                        255f
                    );

                Main.EntitySpriteDraw(
                    texture,
                    position,
                    null,
                    new Color(
                        255,
                        245,
                        190,
                        alpha
                    ),
                    Projectile.rotation,
                    texture.Size() * 0.5f,
                    HandScale,
                    SpriteEffects.None,
                    0f
                );
            }

            // SUBTLE GLOW

            Texture2D sun =
                TextureAssets.Sun2.Value;

            Main.EntitySpriteDraw(
                sun,
                Projectile.Center -
                Main.screenPosition,
                null,
                new Color(
                    255,
                    220,
                    90,
                    25
                ),
                0f,
                sun.Size() * 0.5f,
                0.035f,
                SpriteEffects.None,
                0f
            );

            // HAND

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                Color.White,
                Projectile.rotation,
                texture.Size() * 0.5f,
                HandScale,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}