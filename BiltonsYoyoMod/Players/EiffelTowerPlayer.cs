using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Players
{
    public class EiffelTowerPlayer : ModPlayer
    {
        // STATE

        public bool Active;

        private int Timer;

        private float Rotation;

        private int DamageCooldown;

        // SETTINGS

        // Effect lasts 10 seconds.
        private const int Duration =
            10 * 60;

        // Damage every 0.1 second.
        // 60 FPS / 6 = 10 checks per second.
        private const int DamageInterval =
            6;

        // TRIANGLE

        private const float TriangleRadius =
            70f;

        // DAMAGE CIRCLE
        //
        // The damage area is a CIRCLE.
        //
        // Center = center of the main yoyo.
        // Radius = 128 pixels.
        //
        // The triangle does NOT affect damage detection.

        private const float DamageRadius =
            128f;

        // DUST

        // Smaller value = denser triangle.
        private const float DustSpacing =
            6f;

        private const float TraceRadius =
            16f;

        // CORNER RINGS

        private const float CornerRingMinimum =
            4f;

        private const float CornerRingMaximum =
            10f;

        private const float CornerRingSpeed =
            0.12f;

        // PARTICLES

        private Vector2[] ParticleOffsets;

        private bool[] ParticleTraced;

        private int ParticleCount;

        // START

        public void StartEiffelTower()
        {
            Active =
                true;

            Timer =
                Duration;

            Rotation =
                0f;

            DamageCooldown =
                0;

            BuildTriangleParticles();

            // IGNITION FX (visual + audio only): the tower snaps together.
            Projectile held =
                Systems.YoyoTrickSystem.GetHeldYoyo(
                    Player
                );

            if (held != null && held.active)
            {
                Projectiles.MiniYoyoFx.FlashFX(
                    held.Center,
                    new Color(
                        255,
                        230,
                        170
                    ),
                    1.1f
                );

                Projectiles.YoyoParticles.SpawnRing(
                    held.Center,
                    10f,
                    70f,
                    24,
                    new Color(
                        255,
                        230,
                        170
                    ),
                    2.5f
                );
            }

            SoundEngine.PlaySound(
                SoundID.Item8,
                Player.Center
            );
        }

        // UPDATE

        public override void PostUpdate()
        {
            if (!Active)
                return;

            Timer--;

            // GET MAIN YOYO

            Projectile yoyo =
                YoyoTrickSystem.GetHeldYoyo(
                    Player
                );

            if (yoyo == null ||
                !yoyo.active)
            {
                EndEffect();
                return;
            }

            // ROTATE

            Rotation +=
                0.035f;

            if (Rotation >=
                MathHelper.TwoPi)
            {
                Rotation -=
                    MathHelper.TwoPi;
            }

            // GET TRIANGLE

            Vector2[] corners =
                GetTriangleCorners(
                    yoyo.Center
                );

            // TRACE

            TraceParticles(
                yoyo
            );

            // DRAW

            DrawTriangle(
                yoyo.Center
            );

            DrawCornerRings(
                corners
            );

            // DAMAGE

            if (DamageCooldown > 0)
            {
                DamageCooldown--;
            }
            else
            {
                DamageCircle(
                    yoyo
                );

                DamageCooldown =
                    DamageInterval - 1;
            }

            // END

            if (Timer <= 0)
            {
                EndEffect();
            }
        }

        // GET TRIANGLE CORNERS

        private Vector2[] GetTriangleCorners(
            Vector2 center
        )
        {
            Vector2 top =
                new Vector2(
                    0f,
                    -TriangleRadius
                );

            Vector2 left =
                new Vector2(
                    -TriangleRadius,
                    TriangleRadius * 0.58f
                );

            Vector2 right =
                new Vector2(
                    TriangleRadius,
                    TriangleRadius * 0.58f
                );

            top =
                top.RotatedBy(
                    Rotation
                );

            left =
                left.RotatedBy(
                    Rotation
                );

            right =
                right.RotatedBy(
                    Rotation
                );

            return new Vector2[]
            {
                center + top,
                center + left,
                center + right
            };
        }

        // BUILD TRIANGLE PARTICLES

        private void BuildTriangleParticles()
        {
            Vector2 top =
                new Vector2(
                    0f,
                    -TriangleRadius
                );

            Vector2 left =
                new Vector2(
                    -TriangleRadius,
                    TriangleRadius * 0.58f
                );

            Vector2 right =
                new Vector2(
                    TriangleRadius,
                    TriangleRadius * 0.58f
                );

            float longestSide =
                Vector2.Distance(
                    top,
                    left
                );

            int pointsPerSide =
                (int)(
                    longestSide /
                    DustSpacing
                ) + 2;

            int capacity =
                pointsPerSide * 3;

            ParticleOffsets =
                new Vector2[
                    capacity
                ];

            ParticleTraced =
                new bool[
                    capacity
                ];

            ParticleCount =
                0;

            AddParticleEdge(
                top,
                left
            );

            AddParticleEdge(
                left,
                right
            );

            AddParticleEdge(
                right,
                top
            );
        }

        // ADD PARTICLE EDGE

        private void AddParticleEdge(
            Vector2 start,
            Vector2 end
        )
        {
            Vector2 difference =
                end - start;

            float length =
                difference.Length();

            if (length <= 0.01f)
                return;

            for (
                float distance = 0f;
                distance <= length;
                distance += DustSpacing
            )
            {
                if (ParticleCount >=
                    ParticleOffsets.Length)
                {
                    return;
                }

                float progress =
                    MathHelper.Clamp(
                        distance / length,
                        0f,
                        1f
                    );

                ParticleOffsets[
                    ParticleCount
                ] =
                    Vector2.Lerp(
                        start,
                        end,
                        progress
                    );

                ParticleTraced[
                    ParticleCount
                ] =
                    false;

                ParticleCount++;
            }
        }

        // TRACE PARTICLES

        private void TraceParticles(
            Projectile yoyo
        )
        {
            Vector2 center =
                yoyo.Center;

            for (
                int i = 0;
                i < ParticleCount;
                i++
            )
            {
                if (ParticleTraced[i])
                    continue;

                Vector2 position =
                    center +
                    ParticleOffsets[i]
                        .RotatedBy(
                            Rotation
                        );

                if (
                    Vector2.Distance(
                        center,
                        position
                    ) > TraceRadius
                )
                {
                    continue;
                }

                ParticleTraced[i] =
                    true;
            }
        }

        // DRAW TRIANGLE

        private void DrawTriangle(
            Vector2 center
        )
        {
            if (ParticleOffsets == null)
                return;

            for (
                int i = 0;
                i < ParticleCount;
                i++
            )
            {
                Vector2 position =
                    center +
                    ParticleOffsets[i]
                        .RotatedBy(
                            Rotation
                        );

                int dustType =
                    i % 2 == 0
                        ? DustID.WhiteTorch
                        : DustID.YellowTorch;

                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        dustType,
                        Vector2.Zero
                    );

                dust.noGravity =
                    true;

                dust.velocity =
                    Vector2.Zero;

                dust.scale =
                    0.68f;

                dust.alpha =
                    ParticleTraced[i]
                        ? 128
                        : 0;
            }
        }

        // CORNER RINGS

        private void DrawCornerRings(
            Vector2[] corners
        )
        {
            for (
                int corner = 0;
                corner < 3;
                corner++
            )
            {
                float pulse =
                    (
                        (float)System.Math.Sin(
                            Timer *
                            CornerRingSpeed +
                            corner *
                            2.1f
                        ) +
                        1f
                    ) *
                    0.5f;

                float radius =
                    MathHelper.Lerp(
                        CornerRingMinimum,
                        CornerRingMaximum,
                        pulse
                    );

                const int RingParticles =
                    8;

                for (
                    int i = 0;
                    i < RingParticles;
                    i++
                )
                {
                    float angle =
                        MathHelper.TwoPi *
                        i /
                        RingParticles;

                    Vector2 position =
                        corners[corner] +
                        angle.ToRotationVector2() *
                        radius;

                    Dust dust =
                        Dust.NewDustPerfect(
                            position,
                            DustID.GoldFlame,
                            Vector2.Zero
                        );

                    dust.noGravity =
                        true;

                    dust.velocity =
                        Vector2.Zero;

                    dust.scale =
                        0.65f;
                }
            }
        }

        // CIRCLE DAMAGE
        //
        // ONLY the circle matters.
        //
        // Center:
        //     main yoyo center
        //
        // Radius:
        //     128 pixels
        //
        // A Target Dummy is NOT rejected by CanBeChasedBy().

        private void DamageCircle(
            Projectile yoyo
        )
        {
            float radiusSquared =
                DamageRadius *
                DamageRadius;

            Vector2 center =
                yoyo.Center;

            foreach (NPC npc in Main.npc)
            {
                // NPC MUST EXIST

                if (!npc.active)
                    continue;

                // CIRCLE CHECK

                Vector2 difference =
                    npc.Center -
                    center;

                if (
                    difference.LengthSquared() >
                    radiusSquared
                )
                {
                    continue;
                }

                // DEAL 2 DAMAGE
                //
                // Do NOT use CanBeChasedBy().
                //
                // This allows Target Dummies to be hit.

                Player.ApplyDamageToNPC(
                    npc,
                    2,
                    0f,
                    Player.direction,
                    false
                );
            }
        }

        // END

        private void EndEffect()
        {
            Active =
                false;

            Timer =
                0;

            Rotation =
                0f;

            DamageCooldown =
                0;

            ParticleOffsets =
                null;

            ParticleTraced =
                null;

            ParticleCount =
                0;

            // DISSOLVE FX (visual only): corners wink out together.
            Projectiles.YoyoParticles.SpawnBloom(
                Player.Center,
                8f,
                40f,
                22,
                new Color(
                    255,
                    230,
                    170
                ),
                0.5f
            );
        }
    }
}