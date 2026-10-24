using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.ID;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class DukeYoyoProj : ModProjectile
    {
        // PERMANENT WHIRLPOOL

        private bool whirlpoolSpawned;


        // 5 SECOND EVENT

        // 5 seconds = 300 ticks.
        private const int EventInterval = 300;

        private int eventTimer;


        // MINI VORTEX SETTINGS

        // Normal mini vortex:
        // 2x smaller than the permanent vortex.
        private const float NormalMiniSpeed = 3.5f;

        // Tiny vortex:
        // 4x smaller than the permanent vortex,
        // and twice as fast.
        private const float TinyMiniSpeed = 7f;


        // BUBBLE SETTINGS

        private const int BubbleCount = 12;
        private const float BubbleSpeed = 6f;


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

            Projectile.aiStyle =
                ProjAIStyleID.Yoyo;

            Projectile.timeLeft = 9999;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;


            ProjectileID.Sets.YoyosMaximumRange[
                Projectile.type
            ] = 350f;


            ProjectileID.Sets.YoyosTopSpeed[
                Projectile.type
            ] = 15f;


            ProjectileID.Sets.YoyosLifeTimeMultiplier[
                Projectile.type
            ] = -1f;
        }


        // AI

        public override void AI()
        {
            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.20f,
                0.45f,
                0.65f
            );


            // PERMANENT WHIRLPOOL

            if (
                !whirlpoolSpawned &&
                Main.myPlayer == Projectile.owner
            )
            {
                whirlpoolSpawned = true;


                Projectile whirlpool =
                    Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<
                            DukeYoyoProjW>(),
                        Math.Max(
                            1,
                            Projectile.damage / 5
                        ),
                        0f,
                        Projectile.owner,
                        Projectile.whoAmI
                    );


                whirlpool.netUpdate = true;
            }


            // 5 SECOND TIMER

            eventTimer++;


            if (
                eventTimer >=
                EventInterval
            )
            {
                eventTimer = 0;


                /*
                 * Only the owner creates the projectiles.
                 *
                 * This prevents every client from spawning its own
                 * copy of the vortex/bubbles.
                 */

                if (
                    Main.myPlayer ==
                    Projectile.owner
                )
                {
                    FireFiveSecondEvent();
                }
            }
        }


        // 5 SECOND EVENT

        private void FireFiveSecondEvent()
        {
            // 50 / 50 VORTEX CHOICE

            bool spawnTinyPair =
                Main.rand.NextBool(2);


            if (spawnTinyPair)
            {
                /*
                 * 50% chance:
                 *
                 * TWO tiny fast vortices.
                 *
                 * Each gets its own random direction.
                 */

                SpawnMiniVortex(
                    1,
                    TinyMiniSpeed
                );


                SpawnMiniVortex(
                    1,
                    TinyMiniSpeed
                );
            }
            else
            {
                /*
                 * 50% chance:
                 *
                 * ONE normal slower mini vortex.
                 */

                SpawnMiniVortex(
                    0,
                    NormalMiniSpeed
                );
            }


            // BUBBLE BURST

            ShootBubbleRing();
        }


        // MINI VORTEX

        private void SpawnMiniVortex(
            int variant,
            float speed)
        {
            /*
             * Random direction.
             *
             * Vector2.UnitX is only a fallback and should almost
             * never be necessary.
             */

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


            Vector2 velocity =
                direction * speed;


            Projectile mini =
                Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    ModContent.ProjectileType<
                        DukeYoyoProjV>(),
                    Math.Max(
                        1,
                        Projectile.damage / 5
                    ),
                    Projectile.knockBack,
                    Projectile.owner,
                    variant
                );


            mini.netUpdate = true;
        }


        // BUBBLE RING

        private void ShootBubbleRing()
        {
            for (
                int i = 0;
                i < BubbleCount;
                i++
            )
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    BubbleCount;


                Vector2 direction =
                    angle.ToRotationVector2();


                Projectile bubble =
                    Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        direction * BubbleSpeed,
                        ModContent.ProjectileType<
                            DukeYoyoProjBubble>(),
                        Math.Max(
                            1,
                            Projectile.damage / 3
                        ),
                        Projectile.knockBack,
                        Projectile.owner
                    );


                bubble.netUpdate = true;
            }
        }
    }
}