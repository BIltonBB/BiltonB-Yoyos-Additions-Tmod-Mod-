using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Players
{
    public class ElevatorPlayer : ModPlayer
    {
        // ELEVATOR STATE

        public bool Active;

        private int Timer;

        // Exact projectile slot of the yoyo that started Elevator.
        private int PhaseYoyoIndex = -1;

        // Exact projectile identity of the yoyo that started Elevator.
        private int PhaseYoyoIdentity = -1;

        // AFTEREFFECT STATE

        // Only one enemy hit can trigger the aftereffect.
        private bool AfterEffectTriggered;

        // True while the hit ring is expanding.
        private bool AfterEffectExpanding;

        // 30 frames = 0.5 seconds.
        private int AfterEffectTimer;

        // Exact position where the yoyo hit.
        private Vector2 AfterEffectPosition;

        // CAPTURED YOYO DATA

        // Captured before the main Elevator phase ends so the
        // aftereffect can still create the five clones.
        private int AfterEffectYoyoType;

        private int AfterEffectDamage;

        private float AfterEffectKnockBack;

        private float AfterEffectScale;

        // SETTINGS

        // Main Elevator phase.
        private const int Duration =
            20 * 60;

        // Hit ring duration.
        private const int RingExpansionDuration =
            30;

        private const float InitialRingRadius =
            18f;

        private const float ExpandedRingRadius =
            70f;

        // Five falling yoyo copies.
        private const int CloneCount =
            5;

        // Clones appear 500 pixels above the hit.
        private const float CloneSpawnHeight =
            500f;

        // Random horizontal spawn offset.
        private const float CloneHorizontalOffset =
            100f;

        // Random falling speed.
        private const float CloneMinimumFallSpeed =
            7f;

        private const float CloneMaximumFallSpeed =
            12f;

        // START ELEVATOR

        public void StartElevator()
        {
            Projectile yoyo =
                YoyoTrickSystem.GetHeldYoyo(
                    Player
                );

            if (yoyo == null ||
                !yoyo.active)
            {
                Main.NewText(
                    "ELEVATOR FAILED: NO YOYO FOUND",
                    Color.Red
                );

                Active = false;

                return;
            }

            // START MAIN PHASE

            Active = true;

            Timer =
                Duration;

            // LOCK EXACT YOYO

            PhaseYoyoIndex =
                yoyo.whoAmI;

            PhaseYoyoIdentity =
                yoyo.identity;

            // RESET AFTEREFFECT

            AfterEffectTriggered =
                false;

            AfterEffectExpanding =
                false;

            AfterEffectTimer =
                0;

            AfterEffectPosition =
                Vector2.Zero;

            AfterEffectYoyoType =
                -1;

            AfterEffectDamage =
                0;

            AfterEffectKnockBack =
                0f;

            AfterEffectScale =
                1f;

            Main.NewText(
                "THE ELEVATOR ACTIVATED",
                Color.Cyan
            );

            Main.NewText(
                "ELEVATOR: 20 SECOND PHASE STARTED",
                Color.LightBlue
            );

            Main.NewText(
                "ELEVATOR: YOYO PHASE LOCKED",
                Color.LightBlue
            );
        }

        // UPDATE

        public override void PostUpdate()
        {
            // NOTHING TO UPDATE

            if (!Active &&
                !AfterEffectExpanding)
            {
                return;
            }

            // MAIN ELEVATOR PHASE

            if (Active)
            {
                Timer--;

                // GET EXACT PHASE YOYO

                Projectile yoyo =
                    GetPhaseYoyo();

                // ORIGINAL YOYO WAS RETHROWN / LOST

                if (yoyo == null)
                {
                    Main.NewText(
                        "ELEVATOR: YOYO RETHROWN - PHASE ENDED",
                        Color.Orange
                    );

                    EndElevator();

                    return;
                }

                // NORMAL BLUE RING
                //
                // Only exists while the 20-second phase is still active
                // and before the hit aftereffect begins.

                if (!AfterEffectTriggered)
                {
                    DrawPhaseRing(
                        yoyo
                    );
                }

                // 20 SECOND TIMEOUT

                if (Timer <= 0)
                {
                    Main.NewText(
                        "ELEVATOR: 20 SECOND PHASE FINISHED",
                        Color.Gray
                    );

                    EndElevator();

                    return;
                }
            }

            // AFTEREFFECT
            //
            // This is deliberately outside the Active block.
            //
            // When the yoyo hits:
            //
            // Active = false
            //
            // but AfterEffectExpanding remains true, allowing the
            // 0.5 second hit ring and clone spawn to finish.

            if (AfterEffectExpanding)
            {
                UpdateAfterEffectRing();
            }
        }

        // GET EXACT PHASE YOYO

        private Projectile GetPhaseYoyo()
        {
            if (PhaseYoyoIndex < 0 ||
                PhaseYoyoIndex >= Main.maxProjectiles)
            {
                return null;
            }

            Projectile yoyo =
                Main.projectile[
                    PhaseYoyoIndex
                ];

            if (yoyo == null ||
                !yoyo.active)
            {
                return null;
            }

            if (yoyo.owner !=
                Player.whoAmI)
            {
                return null;
            }

            // The slot must still contain the exact projectile
            // that started the Elevator.
            if (yoyo.identity !=
                PhaseYoyoIdentity)
            {
                return null;
            }

            if (!YoyoTrickSystem.IsYoyoProjectile(
                    yoyo))
            {
                return null;
            }

            return yoyo;
        }

        // PHASE YOYO CHECK

        public bool IsPhaseYoyo(
            Projectile projectile
        )
        {
            if (!Active)
                return false;

            if (projectile == null ||
                !projectile.active)
            {
                return false;
            }

            if (projectile.owner !=
                Player.whoAmI)
            {
                return false;
            }

            return projectile.whoAmI ==
                       PhaseYoyoIndex &&
                   projectile.identity ==
                       PhaseYoyoIdentity;
        }

        // NORMAL BLUE PHASE RING

        private void DrawPhaseRing(
            Projectile yoyo
        )
        {
            if (!Main.rand.NextBool(2))
                return;

            const int RingParticles =
                8;

            for (int i = 0;
                 i < RingParticles;
                 i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    RingParticles;

                Vector2 offset =
                    angle.ToRotationVector2() *
                    InitialRingRadius;

                Dust dust =
                    Dust.NewDustPerfect(
                        yoyo.Center + offset,
                        DustID.IceTorch,
                        Vector2.Zero
                    );

                dust.noGravity =
                    true;

                dust.velocity =
                    Vector2.Zero;

                dust.scale =
                    Main.rand.NextFloat(
                        0.7f,
                        1.05f
                    );
            }
        }

        // TRIGGER AFTEREFFECT
        //
        // This must be called when the exact phase yoyo hits an enemy.
        //
        // The main Elevator phase ends IMMEDIATELY here.
        // The ring expansion continues separately for 0.5 seconds.

        public void TriggerAfterEffect(
            Projectile yoyo,
            Vector2 hitPosition
        )
        {
            if (!Active)
                return;

            if (AfterEffectTriggered)
                return;

            // Only the exact locked yoyo can trigger it.
            if (!IsPhaseYoyo(yoyo))
                return;

            // CAPTURE YOYO INFORMATION BEFORE ENDING THE PHASE

            AfterEffectYoyoType =
                yoyo.type;

            AfterEffectDamage =
                yoyo.damage;

            AfterEffectKnockBack =
                yoyo.knockBack;

            AfterEffectScale =
                yoyo.scale;

            // SAVE HIT POSITION

            AfterEffectPosition =
                hitPosition;

            // START AFTEREFFECT

            AfterEffectTriggered =
                true;

            AfterEffectExpanding =
                true;

            AfterEffectTimer =
                0;

            // END MAIN ELEVATOR PHASE NOW
            //
            // The main 20-second phase is over as soon as the yoyo
            // makes the qualifying enemy hit.
            //
            // We do NOT call EndElevator(), because that would erase
            // the captured aftereffect data.

            Active =
                false;

            Timer =
                0;

            PhaseYoyoIndex =
                -1;

            PhaseYoyoIdentity =
                -1;

            Main.NewText(
                "ELEVATOR: YOYO HIT - MAIN PHASE ENDED",
                Color.Cyan
            );

            Main.NewText(
                "ELEVATOR: BLUE RING EXPANDING",
                Color.LightBlue
            );
        }

        // UPDATE AFTEREFFECT RING

        private void UpdateAfterEffectRing()
        {
            AfterEffectTimer++;

            float progress =
                MathHelper.Clamp(
                    AfterEffectTimer /
                    (float)RingExpansionDuration,
                    0f,
                    1f
                );

            float radius =
                MathHelper.Lerp(
                    InitialRingRadius,
                    ExpandedRingRadius,
                    progress
                );

            float alpha =
                1f - progress;

            const int RingParticles =
                16;

            // EXPANDING BLUE RING

            for (int i = 0;
                 i < RingParticles;
                 i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    RingParticles;

                Vector2 offset =
                    angle.ToRotationVector2() *
                    radius;

                Dust dust =
                    Dust.NewDustPerfect(
                        AfterEffectPosition + offset,
                        DustID.IceTorch,
                        Vector2.Zero
                    );

                dust.noGravity =
                    true;

                dust.velocity =
                    Vector2.Zero;

                dust.scale =
                    1.15f * alpha;
            }

            // CENTER BURST

            if (AfterEffectTimer % 2 == 0)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        AfterEffectPosition,
                        DustID.IceTorch,
                        Vector2.Zero
                    );

                dust.noGravity =
                    true;

                dust.velocity =
                    Vector2.Zero;

                dust.scale =
                    0.8f * alpha;
            }

            // FINISHED

            if (AfterEffectTimer <
                RingExpansionDuration)
            {
                return;
            }

            AfterEffectExpanding =
                false;

            // SPAWN CLONES ONCE

            SpawnAfterEffectClones();

            // CLEAR AFTEREFFECT STATE

            AfterEffectTimer =
                0;
        }

        // SPAWN FIVE YOYO CLONES

        private void SpawnAfterEffectClones()
        {
            if (AfterEffectYoyoType <= 0)
            {
                Main.NewText(
                    "ELEVATOR: CLONES FAILED - INVALID YOYO TYPE",
                    Color.Red
                );

                return;
            }

            // FIVE CLONES

            for (int i = 0;
                 i < CloneCount;
                 i++)
            {
                float horizontalOffset =
                    Main.rand.NextFloat(
                        -CloneHorizontalOffset,
                        CloneHorizontalOffset
                    );

                float fallSpeed =
                    Main.rand.NextFloat(
                        CloneMinimumFallSpeed,
                        CloneMaximumFallSpeed
                    );

                // Exactly 500 pixels above the hit position.
                Vector2 spawnPosition =
                    AfterEffectPosition +
                    new Vector2(
                        horizontalOffset,
                        -CloneSpawnHeight
                    );

                // Slight random sideways movement.
                float horizontalVelocity =
                    Main.rand.NextFloat(
                        -0.8f,
                        0.8f
                    );

                Vector2 velocity =
                    new Vector2(
                        horizontalVelocity,
                        fallSpeed
                    );

                int projectileIndex =
                    Projectile.NewProjectile(
                        Player.GetSource_FromThis(),
                        spawnPosition,
                        velocity,
                        ModContent.ProjectileType<
                            ElevatorCloneProjectile
                        >(),
                        AfterEffectDamage,
                        AfterEffectKnockBack,
                        Player.whoAmI,
                        AfterEffectYoyoType
                    );

                if (projectileIndex < 0 ||
                    projectileIndex >= Main.maxProjectiles)
                {
                    continue;
                }

                Projectile clone =
                    Main.projectile[
                        projectileIndex
                    ];

                clone.scale =
                    AfterEffectScale;
            }

            // SPAWN LOCATION BURST

            Vector2 cloneOrigin =
                AfterEffectPosition +
                new Vector2(
                    0f,
                    -CloneSpawnHeight
                );

            for (int i = 0;
                 i < 18;
                 i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        cloneOrigin +
                        Main.rand.NextVector2Circular(
                            70f,
                            20f
                        ),
                        DustID.IceTorch,
                        Main.rand.NextVector2Circular(
                            2f,
                            2f
                        )
                    );

                dust.noGravity =
                    true;

                dust.scale =
                    Main.rand.NextFloat(
                        0.8f,
                        1.2f
                    );
            }

            Main.NewText(
                "ELEVATOR: 5 YOYO CLONES DROPPED",
                Color.Cyan
            );

            // SHAFT FX (visual + audio only): the drop shaft flashes white.
            YoyoParticles.SpawnSlashLine(
                cloneOrigin,
                -MathHelper.PiOver2,
                130f,
                Color.White,
                3f,
                10
            );

            YoyoParticles.SpawnRing(
                cloneOrigin,
                12f,
                64f,
                22,
                new Color(
                    170,
                    230,
                    255
                ),
                2.5f
            );

            SoundEngine.PlaySound(
                SoundID.Item8,
                cloneOrigin
            );
        }

        // END ELEVATOR

        private void EndElevator()
        {
            Active = false;

            Timer = 0;

            PhaseYoyoIndex = -1;

            PhaseYoyoIdentity = -1;

            AfterEffectTriggered = false;

            AfterEffectExpanding = false;

            AfterEffectTimer = 0;

            AfterEffectPosition =
                Vector2.Zero;

            AfterEffectYoyoType =
                -1;

            AfterEffectDamage =
                0;

            AfterEffectKnockBack =
                0f;

            AfterEffectScale =
                1f;
        }
    }
}