using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Players
{
    public class DNAPlayer : ModPlayer
    {
        // SETTINGS

        private const int EffectDuration =
            20 * 60;

        // 0.1 seconds = 6 frames.
        private const int DamageInterval =
            6;

        private const int DamageAmount =
            10;

        // Main DNA damage radius.
        private const float DamageRadius =
            128f;

        // OCTAGON

        private const float MinimumVisualRadius =
            112f;

        private const float MaximumVisualRadius =
            128f;

        private const float RotationSpeed =
            0.035f;

        private const float DustSpacing =
            7f;

        private const float CornerRingRadius =
            7f;

        // DNA YOYO COPIES

        // Distance from the main yoyo.
        private const float OrbitRadius =
            28f;

        // Fast swirling speed.
        private const float OrbitSpeed =
            0.25f;

        // STATE

        private bool dnaActive;

        private int dnaTimer;

        private int damageTimer;

        private float rotation;

        // Exact main yoyo.
        private int yoyoWhoAmI =
            -1;

        private int yoyoIdentity =
            -1;

        // START DNA

        public void StartDNA(
            Projectile yoyo
        )
        {
            if (yoyo == null ||
                !yoyo.active)
            {
                return;
            }

            KillOrbitingYoyos();

            dnaActive = true;

            dnaTimer = 0;
            damageTimer = 0;
            rotation = 0f;

            yoyoWhoAmI =
                yoyo.whoAmI;

            yoyoIdentity =
                yoyo.identity;

            // INITIAL BURST

            for (int i = 0; i < 45; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        yoyo.Center,
                        DustID.GoldFlame,
                        Main.rand.NextVector2CircularEdge(
                            6f,
                            6f
                        )
                    );

                dust.noGravity =
                    true;

                dust.scale =
                    Main.rand.NextFloat(
                        0.7f,
                        1.15f
                    );
            }

            // 3 ORBITING YOYOS
            //
            // 0°
            // 120°
            // 240°
            //
            // This gives an even 360° distribution.

            SpawnOrbitingYoyos(
                yoyo
            );

            // HELIX SNAP FX (visual + audio only): ring + flash over the burst.
            YoyoParticles.SpawnRing(
                yoyo.Center,
                10f,
                80f,
                26,
                new Color(
                    150,
                    255,
                    190
                ),
                2.5f
            );

            YoyoParticles.SpawnBloom(
                yoyo.Center,
                6f,
                30f,
                20,
                new Color(
                    150,
                    255,
                    190
                ),
                0.55f
            );

            SoundEngine.PlaySound(
                SoundID.Item29 with { Pitch = 0.3f },
                yoyo.Center
            );
        }

        // SPAWN ORBITING YOYOS

        private void SpawnOrbitingYoyos(
            Projectile yoyo
        )
        {
            int projectileType =
                ModContent.ProjectileType<
                    DNAOrbitYoyoProjectile
                >();

            for (int i = 0; i < 3; i++)
            {
                // 0°, 120°, 240°.
                float startingAngle =
                    MathHelper.TwoPi *
                    i /
                    3f;

                int damage =
                    Math.Max(
                        1,
                        yoyo.damage / 3
                    );

                int projectileIndex =
                    Projectile.NewProjectile(
                        Player.GetSource_Misc(
                            "YoyoTrickDNA"
                        ),
                        yoyo.Center,
                        Vector2.Zero,
                        projectileType,
                        damage,
                        0f,
                        Player.whoAmI,
                        yoyo.whoAmI,
                        startingAngle
                    );

                if (
                    projectileIndex >= 0 &&
                    projectileIndex <
                        Main.maxProjectiles
                )
                {
                    Projectile orbit =
                        Main.projectile[
                            projectileIndex
                        ];

                    orbit.originalDamage =
                        damage;

                    orbit.netUpdate =
                        true;
                }
            }
        }

        // UPDATE

        public override void PostUpdate()
        {
            if (!dnaActive)
                return;

            Projectile yoyo =
                GetLockedYoyo();

            if (yoyo == null)
            {
                StopDNA();
                return;
            }

            dnaTimer++;
            damageTimer++;

            rotation +=
                RotationSpeed;

            // VISUALS

            CreateDNAVisuals(
                yoyo.Center
            );

            // DAMAGE FIELD

            if (
                damageTimer >=
                DamageInterval
            )
            {
                damageTimer = 0;

                DamageCircle(
                    yoyo.Center
                );
            }

            // END

            if (
                dnaTimer >=
                EffectDuration
            )
            {
                StopDNA();
            }
        }

        // GET EXACT YOYO

        private Projectile GetLockedYoyo()
        {
            if (
                yoyoWhoAmI < 0 ||
                yoyoWhoAmI >=
                    Main.maxProjectiles
            )
            {
                return null;
            }

            Projectile yoyo =
                Main.projectile[
                    yoyoWhoAmI
                ];

            if (
                yoyo == null ||
                !yoyo.active
            )
            {
                return null;
            }

            if (
                yoyo.owner !=
                Player.whoAmI
            )
            {
                return null;
            }

            if (
                yoyo.identity !=
                yoyoIdentity
            )
            {
                return null;
            }

            if (
                !YoyoTrickSystem.IsYoyoProjectile(
                    yoyo
                )
            )
            {
                return null;
            }

            return yoyo;
        }

        // DNA VISUALS

        private void CreateDNAVisuals(
            Vector2 center
        )
        {
            if (Main.dedServ)
                return;

            float pulse =
                (float)Math.Sin(
                    dnaTimer * 0.07f
                );

            float pulse01 =
                (pulse + 1f) *
                0.5f;

            float radius =
                MathHelper.Lerp(
                    MinimumVisualRadius,
                    MaximumVisualRadius,
                    pulse01
                );

            const int sides = 8;

            for (
                int side = 0;
                side < sides;
                side++
            )
            {
                float angleA =
                    rotation +
                    MathHelper.TwoPi *
                    side /
                    sides;

                float angleB =
                    rotation +
                    MathHelper.TwoPi *
                    (side + 1) /
                    sides;

                Vector2 pointA =
                    center +
                    angleA.ToRotationVector2() *
                    radius;

                Vector2 pointB =
                    center +
                    angleB.ToRotationVector2() *
                    radius;

                float length =
                    Vector2.Distance(
                        pointA,
                        pointB
                    );

                int particleCount =
                    Math.Max(
                        1,
                        (int)(
                            length /
                            DustSpacing
                        )
                    );

                for (
                    int i = 0;
                    i <= particleCount;
                    i++
                )
                {
                    float progress =
                        i /
                        (float)particleCount;

                    Vector2 position =
                        Vector2.Lerp(
                            pointA,
                            pointB,
                            progress
                        );

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
                        Main.rand.NextFloat(
                            0.65f,
                            1.05f
                        );
                }

                CreateCornerRing(
                    pointA
                );
            }
        }

        // CORNER RING

        private void CreateCornerRing(
            Vector2 center
        )
        {
            const int ringParticles = 8;

            for (
                int i = 0;
                i < ringParticles;
                i++
            )
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    ringParticles;

                Vector2 position =
                    center +
                    angle.ToRotationVector2() *
                    CornerRingRadius;

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
                    0.8f;
            }
        }

        // DNA DAMAGE CIRCLE

        private void DamageCircle(
            Vector2 center
        )
        {
            if (
                Main.netMode ==
                NetmodeID.MultiplayerClient &&
                Main.myPlayer !=
                Player.whoAmI
            )
            {
                return;
            }

            foreach (NPC npc in Main.npc)
            {
                if (!npc.active)
                    continue;

                if (npc.life <= 0)
                    continue;

                if (npc.dontTakeDamage)
                    continue;

                bool targetDummy =
                    npc.type ==
                    NPCID.TargetDummy;

                if (
                    !targetDummy &&
                    npc.friendly
                )
                {
                    continue;
                }

                if (
                    Vector2.Distance(
                        npc.Center,
                        center
                    ) > DamageRadius
                )
                {
                    continue;
                }

                NPC.HitInfo hit =
                    new NPC.HitInfo
                    {
                        Damage =
                            DamageAmount,

                        Knockback =
                            0f,

                        HitDirection =
                            npc.Center.X >=
                            center.X
                                ? 1
                                : -1,

                        Crit =
                            false
                    };

                npc.StrikeNPC(
                    hit
                );
            }
        }

        // REMOVE COPIES

        private void KillOrbitingYoyos()
        {
            int projectileType =
                ModContent.ProjectileType<
                    DNAOrbitYoyoProjectile
                >();

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
                    projectile.owner ==
                        Player.whoAmI &&
                    projectile.type ==
                        projectileType
                )
                {
                    projectile.Kill();
                }
            }
        }

        // STOP

        private void StopDNA()
        {
            KillOrbitingYoyos();

            dnaActive = false;

            dnaTimer = 0;
            damageTimer = 0;

            rotation = 0f;

            yoyoWhoAmI =
                -1;

            yoyoIdentity =
                -1;
        }
    }
}