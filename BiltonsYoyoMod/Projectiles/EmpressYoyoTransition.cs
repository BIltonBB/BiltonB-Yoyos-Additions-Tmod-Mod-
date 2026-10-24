using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class EmpressYoyoTransition : ModProjectile
    {
        private const int RingDuration = 70;
        private const int TotalDuration = 90;
        private const int A4Frame = 60;

        public override void SetDefaults()
        {
            Projectile.width = 256;
            Projectile.height = 256;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
            Projectile.hide = true;
        }

        public override void AI()
        {
            int timer = (int)Projectile.localAI[0];
            Projectile.localAI[0]++;

            int parentIndex = (int)Projectile.ai[0];

            if (parentIndex >= 0 && parentIndex < Main.maxProjectiles)
            {
                Projectile parent = Main.projectile[parentIndex];
                if (parent.active)
                    Projectile.Center = parent.Center;
            }

            // OPENING RAINBOW BURST

            if (timer < 12)
            {
                for (int i = 0; i < 16; i++)
                {
                    float angle =
                        Main.rand.NextFloat(MathHelper.TwoPi);

                    Vector2 dir =
                        angle.ToRotationVector2();

                    Dust spark = Dust.NewDustPerfect(
                        Projectile.Center + dir * Main.rand.NextFloat(4f, 18f),
                        DustID.PinkTorch,
                        dir * Main.rand.NextFloat(2.5f, 6f),
                        100,
                        GetRainbowColor(Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.9f, 1.5f)
                    );

                    spark.noGravity = true;
                }
            }

            // RAINBOW SHELL + SMOKE

            if (timer < RingDuration)
            {
                float progress = MathHelper.Clamp(
                    timer / (float)RingDuration,
                    0f,
                    1f
                );

                float shellRadius = MathHelper.Lerp(18f, 145f, progress);

                for (int i = 0; i < 8; i++)
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 radial = angle.ToRotationVector2();

                    Dust shell = Dust.NewDustPerfect(
                        Projectile.Center + radial * (
                            shellRadius + Main.rand.NextFloat(-12f, 12f)
                        ),
                        DustID.PinkTorch,
                        radial * Main.rand.NextFloat(0.25f, 1.25f),
                        125,
                        GetRainbowColor(
                            angle / MathHelper.TwoPi + timer * 0.022f
                        ),
                        Main.rand.NextFloat(0.8f, 1.35f)
                    );

                    shell.noGravity = true;
                }

                // Real smoke, never MagicPixel: the smoke curls inward.
                if (timer % 2 == 0)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float angle =
                            timer * 0.095f +
                            i * MathHelper.TwoPi / 3f;

                        Vector2 radial = angle.ToRotationVector2();
                        Vector2 tangent = new Vector2(-radial.Y, radial.X);

                        float radius = MathHelper.Lerp(120f, 16f, progress);

                        Dust smoke = Dust.NewDustPerfect(
                            Projectile.Center +
                            radial * (radius + Main.rand.NextFloat(-12f, 12f)),
                            DustID.Smoke,
                            tangent * Main.rand.NextFloat(0.45f, 1.1f) -
                            radial * Main.rand.NextFloat(0.15f, 0.5f),
                            175,
                            Color.White,
                            Main.rand.NextFloat(0.9f, 1.6f)
                        );

                        smoke.noGravity = true;
                        smoke.fadeIn = 1.15f;
                    }
                }
            }

            // SPIRAL SMOKE CORE

            if (timer >= 12 && timer < 70 && timer % 2 == 0)
            {
                float progress = MathHelper.Clamp(
                    (timer - 12f) / 58f,
                    0f,
                    1f
                );

                float radius = MathHelper.Lerp(88f, 8f, progress);

                for (int i = 0; i < 4; i++)
                {
                    float angle =
                        timer * 0.13f +
                        i * MathHelper.TwoPi / 4f;

                    Vector2 radial = angle.ToRotationVector2();
                    Vector2 tangent = new Vector2(-radial.Y, radial.X);

                    Dust smoke = Dust.NewDustPerfect(
                        Projectile.Center + radial * radius,
                        DustID.Smoke,
                        tangent * (1f + progress * 0.8f) - radial * 0.45f,
                        165,
                        Color.White,
                        Main.rand.NextFloat(0.9f, 1.7f)
                    );

                    smoke.noGravity = true;
                    smoke.fadeIn = 1.2f;
                }
            }

            // RAINBOW STARBURST

            if (timer < 55 && timer % 3 == 0)
            {
                float p = MathHelper.Clamp(timer / 55f, 0f, 1f);
                float radius = MathHelper.Lerp(24f, 170f, p);

                for (int i = 0; i < 10; i++)
                {
                    float angle =
                        MathHelper.TwoPi * i / 10f +
                        timer * 0.055f;

                    Vector2 dir = angle.ToRotationVector2();

                    Dust spark = Dust.NewDustPerfect(
                        Projectile.Center + dir * radius,
                        DustID.PinkTorch,
                        dir * Main.rand.NextFloat(0.8f, 1.8f),
                        100,
                        GetRainbowColor(i / 10f + timer * 0.02f),
                        Main.rand.NextFloat(0.75f, 1.3f)
                    );

                    spark.noGravity = true;
                }
            }

            // SMOKE EXPLOSION JUST BEFORE A4

            if (timer >= 52 && timer <= 59 && timer % 2 == 0)
            {
                float p = (timer - 52f) / 7f;

                for (int i = 0; i < 8; i++)
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dir = angle.ToRotationVector2();

                    Dust smoke = Dust.NewDustPerfect(
                        Projectile.Center + dir * Main.rand.NextFloat(8f, 28f),
                        DustID.Smoke,
                        dir * MathHelper.Lerp(0.5f, 3.3f, p),
                        150,
                        Color.White,
                        Main.rand.NextFloat(1f, 1.8f)
                    );

                    smoke.noGravity = true;
                    smoke.fadeIn = 1.25f;
                }
            }

            // A4

            if (timer == A4Frame && Main.myPlayer == Projectile.owner)
            {
                NPC target = FindNearestEnemy(
                    Projectile.Center,
                    1600f
                );

                if (target != null &&
                    parentIndex >= 0 &&
                    parentIndex < Main.maxProjectiles)
                {
                    Projectile parent = Main.projectile[parentIndex];

                    if (parent.active)
                    {
                        EmpressYoyoAttackHelperA.PerformA4(
                            parent,
                            target
                        );
                    }
                }
            }

            if (timer >= TotalDuration)
                Projectile.Kill();
        }

        private NPC FindNearestEnemy(
            Vector2 center,
            float maxDistance)
        {
            NPC nearest = null;
            float bestDistanceSquared = maxDistance * maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active ||
                    npc.friendly ||
                    npc.dontTakeDamage ||
                    npc.life <= 0)
                    continue;

                float distanceSquared =
                    Vector2.DistanceSquared(center, npc.Center);

                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    nearest = npc;
                }
            }

            return nearest;
        }

        private Color GetRainbowColor(float t)
        {
            t -= MathF.Floor(t);

            Color[] colors =
            {
                new Color(255, 65, 115),
                new Color(255, 150, 65),
                new Color(255, 235, 75),
                new Color(90, 255, 165),
                new Color(80, 180, 255),
                new Color(175, 95, 255)
            };

            float scaled = t * colors.Length;
            int index = (int)MathF.Floor(scaled) % colors.Length;
            int next = (index + 1) % colors.Length;
            float blend = scaled - MathF.Floor(scaled);

            return Color.Lerp(
                colors[index],
                colors[next],
                blend
            );
        }

        private void DrawLine(
            Vector2 start,
            Vector2 end,
            Color color,
            float thickness)
        {
            Vector2 delta = end - start;
            float length = delta.Length();

            if (length <= 0.01f)
                return;

            Vector2 midpoint = (start + end) * 0.5f;

            Main.spriteBatch.Draw(
                TextureAssets.MagicPixel.Value,
                midpoint - Main.screenPosition,
                new Rectangle(0, 0, 1, 1),
                color,
                delta.ToRotation(),
                new Vector2(0.5f, 0.5f),
                new Vector2(length, thickness),
                SpriteEffects.None,
                0f
            );
        }

        private void DrawRing(
            float radius,
            float rotationOffset,
            float alpha,
            float thickness,
            int segments)
        {
            Vector2 previous =
                Projectile.Center +
                rotationOffset.ToRotationVector2() * radius;

            for (int i = 1; i <= segments; i++)
            {
                float angle =
                    rotationOffset +
                    MathHelper.TwoPi * i / segments;

                Vector2 current =
                    Projectile.Center +
                    angle.ToRotationVector2() * radius;

                Vector2 delta = current - previous;
                float length = delta.Length();

                if (length > 0.01f)
                {
                    Color color =
                        GetRainbowColor(
                            i / (float)segments +
                            Projectile.localAI[0] * 0.006f
                        ) * alpha;

                    Main.spriteBatch.Draw(
                        TextureAssets.MagicPixel.Value,
                        previous - Main.screenPosition,
                        new Rectangle(0, 0, 1, 1),
                        color,
                        delta.ToRotation(),
                        new Vector2(0f, 0.5f),
                        new Vector2(length, thickness),
                        SpriteEffects.None,
                        0f
                    );
                }

                previous = current;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float timer = Projectile.localAI[0];

            float progress = MathHelper.Clamp(
                timer / RingDuration,
                0f,
                1f
            );

            float eased =
                1f - (1f - progress) * (1f - progress);

            float outerRadius = MathHelper.Lerp(
                22f,
                145f,
                eased
            );

            float innerRadius = MathHelper.Lerp(
                10f,
                92f,
                eased
            );

            float fade =
                timer < RingDuration
                    ? 1f - progress * 0.5f
                    : MathHelper.Clamp(
                        1f -
                        (timer - RingDuration) /
                        (TotalDuration - RingDuration),
                        0f,
                        1f
                    );

            // Pulsing outer ring.
            if (timer < RingDuration)
            {
                float pulse =
                    0.5f +
                    0.5f *
                    (float)Math.Sin(timer * 0.22f);

                float haloRadius =
                    outerRadius +
                    12f +
                    pulse * 20f;

                DrawRing(
                    haloRadius,
                    -timer * 0.028f,
                    0.20f * fade,
                    1.1f,
                    64
                );

                for (int i = 0; i < 16; i++)
                {
                    float angle =
                        MathHelper.TwoPi * i / 16f -
                        timer * 0.02f;

                    Vector2 dir = angle.ToRotationVector2();

                    float rayLength =
                        24f + pulse * 68f;

                    Vector2 start =
                        Projectile.Center +
                        dir * (haloRadius - 9f);

                    Vector2 end =
                        start + dir * rayLength;

                    DrawLine(
                        start,
                        end,
                        GetRainbowColor(
                            i / 16f + timer * 0.025f
                        ) * (0.18f * fade),
                        1.0f
                    );
                }
            }

            DrawRing(
                outerRadius,
                timer * 0.015f,
                0.60f * fade,
                2.35f,
                96
            );

            DrawRing(
                innerRadius,
                -timer * 0.022f,
                0.32f * fade,
                1.5f,
                72
            );

            float thirdRadius =
                MathHelper.Lerp(6f, 54f, eased);

            DrawRing(
                thirdRadius,
                timer * 0.034f,
                0.23f * fade,
                1.0f,
                56
            );

            // Tiny rotating inner ring to give the center more depth.
            DrawRing(
                MathHelper.Lerp(4f, 32f, eased),
                -timer * 0.07f,
                0.20f * fade,
                0.9f,
                40
            );

            // A4 charge flash around the final moment.
            if (timer >= 54 && timer <= 63)
            {
                float pulse =
                    0.5f +
                    0.5f *
                    (float)Math.Sin(
                        (timer - 54f) * 0.8f
                    );

                DrawRing(
                    165f + pulse * 20f,
                    timer * 0.05f,
                    0.10f * fade,
                    1f,
                    64
                );
            }

            return false;
        }
    }
}
