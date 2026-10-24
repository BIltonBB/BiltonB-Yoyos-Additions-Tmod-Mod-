using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class BeeYoyoRadiusProj : ModProjectile
    {
        public int parentIndex = -1;

        // SETTINGS

        // Outer damage radius:
        // Diameter = 512 pixels
        private const float CircleRadius = 256f;

        private const float RingThickness = 5f;
        private const int RingSegments = 160;

        // Inner visual field:
        // Diameter = 507 pixels
        // Radius = 253.5 pixels
        private const float InnerCircleRadius = 253.5f;

        // 75% transparent = 25% visible.
        private const float InnerCircleOpacity = 0.25f;

        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 512;
            Projectile.height = 512;

            // Visual / damage helper only.
            Projectile.friendly = false;
            Projectile.hostile = false;

            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }

        // AI

        public override void AI()
        {
            // FIND PARENT

            if (parentIndex == -1)
            {
                parentIndex =
                    (int)Projectile.ai[0];
            }

            // VALIDATE PARENT

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

            if (!parent.active)
            {
                Projectile.Kill();
                return;
            }

            // Follow Bee Yoyo.
            Projectile.Center =
                parent.Center;

            // DAMAGE

            Projectile.localAI[0]++;

            if (Projectile.localAI[0] >= 6)
            {
                Projectile.localAI[0] = 0;

                float radiusSq =
                    CircleRadius *
                    CircleRadius;

                foreach (NPC npc in Main.npc)
                {
                    if (
                        !npc.active ||
                        npc.friendly ||
                        npc.dontTakeDamage
                    )
                    {
                        continue;
                    }

                    Vector2 closestPoint =
                        Vector2.Clamp(
                            Projectile.Center,
                            npc.position,
                            npc.position + npc.Size
                        );

                    if (
                        Vector2.DistanceSquared(
                            Projectile.Center,
                            closestPoint
                        ) > radiusSq
                    )
                    {
                        continue;
                    }

                    NPC.HitInfo hitInfo =
                        new NPC.HitInfo()
                        {
                            Damage = 1,
                            Knockback = 0f,
                            HitDirection = 0,
                            Crit = false
                        };

                    npc.StrikeNPC(hitInfo);

                    npc.AddBuff(
                        BuffID.Poisoned,
                        6
                    );

                    npc.AddBuff(
                        BuffID.Slow,
                        6
                    );
                }
            }

            // RANDOM YELLOW DUST

            // Same style as the Deer SW inner frost particles.
            //
            // One particle every 0.1 seconds (6 ticks).
            //
            // Randomly distributed throughout the
            // entire 512px diameter circle.
            //
            // No outer ring / oval particle spawning.

            Projectile.localAI[1]++;

            if (Projectile.localAI[1] >= 6)
            {
                Projectile.localAI[1] = 0;

                Vector2 direction =
                    Main.rand.NextVector2Unit();

                Vector2 position =
                    Projectile.Center +
                    direction *
                    Main.rand.NextFloat(
                        5f,
                        CircleRadius - 3f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        DustID.YellowTorch,
                        Main.rand.NextVector2Circular(
                            0.35f,
                            0.35f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.4f,
                            0.9f
                        )
                    );

                dust.noGravity = true;
            }

            // LIGHT

            Lighting.AddLight(
                Projectile.Center,
                0.45f,
                0.32f,
                0.04f
            );
        }

        // FINITE LINE

        private void DrawFiniteLine(
            Texture2D pixel,
            Vector2 start,
            Vector2 end,
            Color color,
            float thickness)
        {
            Vector2 difference =
                end - start;

            float length =
                difference.Length();

            if (
                length <= 0.01f ||
                length > 1000f
            )
            {
                return;
            }

            Vector2 midpoint =
                (start + end) * 0.5f;

            float rotation =
                difference.ToRotation();

            Main.spriteBatch.Draw(
                pixel,
                midpoint,
                new Rectangle(
                    0,
                    0,
                    1,
                    1
                ),
                color,
                rotation,
                new Vector2(
                    0.5f,
                    0.5f
                ),
                new Vector2(
                    length,
                    thickness
                ),
                SpriteEffects.None,
                0f
            );
        }

        // DRAW RING

        private void DrawRing(
            Texture2D pixel,
            Vector2 center,
            float radius,
            float thickness,
            Color color,
            int segments)
        {
            float step =
                MathHelper.TwoPi /
                segments;

            for (int i = 0; i < segments; i++)
            {
                float angle1 =
                    step * i;

                float angle2 =
                    step * (i + 1);

                Vector2 point1 =
                    center +
                    angle1.ToRotationVector2() *
                    radius;

                Vector2 point2 =
                    center +
                    angle2.ToRotationVector2() *
                    radius;

                DrawFiniteLine(
                    pixel,
                    point1,
                    point2,
                    color,
                    thickness
                );
            }
        }

        // DRAW FILLED CIRCLE

        private void DrawFilledCircle(
            Texture2D pixel,
            Vector2 center,
            float radius,
            Color color)
        {
            int minY =
                (int)Math.Floor(-radius);

            int maxY =
                (int)Math.Ceiling(radius);

            for (int y = minY; y <= maxY; y++)
            {
                float ySquared =
                    y * y;

                float remaining =
                    radius * radius -
                    ySquared;

                if (remaining <= 0f)
                    continue;

                float halfWidth =
                    (float)Math.Sqrt(
                        remaining
                    );

                Main.spriteBatch.Draw(
                    pixel,
                    center +
                    new Vector2(
                        -halfWidth,
                        y
                    ),
                    new Rectangle(
                        0,
                        0,
                        1,
                        1
                    ),
                    color,
                    0f,
                    Vector2.Zero,
                    new Vector2(
                        halfWidth * 2f,
                        1f
                    ),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D pixel =
                TextureAssets.MagicPixel.Value;

            Vector2 center =
                Projectile.Center -
                Main.screenPosition;

            // 507px LIGHT YELLOW INNER FIELD

            // 75% transparent.
            // 25% visible.

            Color innerCircleColor =
                new Color(
                    255,
                    240,
                    120,
                    255
                );

            DrawFilledCircle(
                pixel,
                center,
                InnerCircleRadius,
                innerCircleColor *
                InnerCircleOpacity
            );

            // SUBTLE INNER TEXTURE

            // These rings are also 75% transparent.

            for (int i = 1; i <= 7; i++)
            {
                float currentRadius =
                    InnerCircleRadius *
                    (i / 8f);

                DrawRing(
                    pixel,
                    center,
                    currentRadius,
                    1f,
                    new Color(
                        255,
                        245,
                        150,
                        255
                    ) *
                    0.25f,
                    100
                );
            }

            // MOVING INNER TEXTURE

            float textureRotation =
                Main.GameUpdateCount *
                0.006f;

            for (int i = 0; i < 12; i++)
            {
                float angle =
                    textureRotation +
                    MathHelper.TwoPi *
                    i / 12f;

                float distance =
                    InnerCircleRadius *
                    (
                        0.35f +
                        0.08f *
                        (float)Math.Sin(
                            Main.GameUpdateCount *
                            0.015f +
                            i
                        )
                    );

                Vector2 direction =
                    angle.ToRotationVector2();

                Vector2 position =
                    center +
                    direction *
                    distance;

                Main.spriteBatch.Draw(
                    pixel,
                    position,
                    new Rectangle(
                        0,
                        0,
                        1,
                        1
                    ),
                    new Color(
                        255,
                        245,
                        160,
                        255
                    ) *
                    0.25f,
                    angle,
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    new Vector2(
                        3f,
                        1f
                    ),
                    SpriteEffects.None,
                    0f
                );
            }

            // ORIGINAL OUTER GLOW

            DrawRing(
                pixel,
                center,
                CircleRadius + 3f,
                8f,
                new Color(
                    255,
                    220,
                    70,
                    35
                ),
                RingSegments
            );

            // MAIN YELLOW RING

            DrawRing(
                pixel,
                center,
                CircleRadius,
                RingThickness,
                new Color(
                    255,
                    215,
                    45,
                    235
                ),
                RingSegments
            );

            // MOVING HIGHLIGHTS

            float rotation =
                Main.GameUpdateCount *
                0.012f;

            for (int i = 0; i < 8; i++)
            {
                float angle =
                    rotation +
                    MathHelper.TwoPi *
                    i /
                    8f;

                Vector2 direction =
                    angle.ToRotationVector2();

                Vector2 start =
                    center +
                    direction *
                    (CircleRadius - 1f);

                Vector2 end =
                    center +
                    direction *
                    (CircleRadius + 7f);

                DrawFiniteLine(
                    pixel,
                    start,
                    end,
                    new Color(
                        255,
                        245,
                        150,
                        170
                    ),
                    2f
                );
            }

            return false;
        }
    }
}