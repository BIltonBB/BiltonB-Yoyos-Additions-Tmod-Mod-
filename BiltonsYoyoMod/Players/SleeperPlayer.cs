using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
    public class SleeperPlayer : ModPlayer
    {
        // SETTINGS

        // Sleeper damage boost duration:
        // 5 seconds = 300 frames.
        private const int SleeperDuration = 300;

        // STATE

        private int damageTimer;

        private int sleeperYoyoIndex = -1;

        // Original damage captured when Sleeper starts.
        private int originalDamage;

        // START SLEEPER

        public void StartSleeper(
            Projectile yoyo)
        {
            if (yoyo == null)
                return;

            if (!yoyo.active)
                return;

            if (yoyo.owner != Player.whoAmI)
                return;

            // STORE EXACT YOYO

            sleeperYoyoIndex =
                yoyo.whoAmI;

            // STORE ORIGINAL DAMAGE

            originalDamage =
                yoyo.damage;

            // ADD 5 SECONDS TO LIFETIME
            //
            // Infinite-lifetime yoyos use negative timeLeft,
            // so they remain infinite.

            if (yoyo.timeLeft > 0)
            {
                yoyo.timeLeft += SleeperDuration;
            }

            // START 5-SECOND DAMAGE STATE

            damageTimer =
                SleeperDuration;

            // Apply immediately instead of waiting one frame.
            yoyo.damage =
                (int)(
                    originalDamage *
                    1.2f
                );

            // SPIN-UP FX (visual + audio only): the yoyo ignites awake.
            Projectiles.MiniYoyoFx.FlashFX(
                yoyo.Center,
                new Color(
                    170,
                    255,
                    170
                ),
                1f
            );

            Projectiles.YoyoParticles.SpawnRing(
                yoyo.Center,
                8f,
                52f,
                22,
                new Color(
                    170,
                    255,
                    170
                ),
                2.5f
            );

            SoundEngine.PlaySound(
                SoundID.Item8,
                yoyo.Center
            );

            Main.NewText(
                "SLEEPER ACTIVATED: YOYO DAMAGE +20% FOR 5 SECONDS",
                Microsoft.Xna.Framework.Color.Lime
            );

            Main.NewText(
                "SLEEPER: YOYO LIFETIME +5 SECONDS",
                Microsoft.Xna.Framework.Color.Yellow
            );
        }

        // UPDATE

        public override void PostUpdate()
        {
            if (damageTimer <= 0)
                return;

            // FIND THE EXACT YOYO USED BY SLEEPER

            if (sleeperYoyoIndex < 0 ||
                sleeperYoyoIndex >= Main.maxProjectiles)
            {
                StopSleeper();
                return;
            }

            Projectile yoyo =
                Main.projectile[
                    sleeperYoyoIndex
                ];

            if (!yoyo.active ||
                yoyo.owner != Player.whoAmI)
            {
                StopSleeper();
                return;
            }

            // DAMAGE BOOST

            yoyo.damage =
                (int)(
                    originalDamage *
                    1.2f
                );

            damageTimer--;

            // SLEEP BREATH (visual only): slow motes while the boost burns.
            if (damageTimer % 20 == 0 && yoyo.active)
            {
                Projectiles.YoyoParticles.SpawnSpark(
                    yoyo.Center + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(0f, -1.2f),
                    new Color(170, 255, 170),
                    0.8f,
                    30,
                    0.97f
                );
            }

            // END STATE

            if (damageTimer <= 0)
            {
                StopSleeper();
            }
        }

        // STOP SLEEPER

        private void StopSleeper()
        {
            if (sleeperYoyoIndex >= 0 &&
                sleeperYoyoIndex < Main.maxProjectiles)
            {
                Projectile yoyo =
                    Main.projectile[
                        sleeperYoyoIndex
                    ];

                if (yoyo.active &&
                    yoyo.owner == Player.whoAmI)
                {
                    // Restore the damage that existed when
                    // Sleeper was activated.
                    yoyo.damage =
                        originalDamage;
                }
            }

            damageTimer = 0;

            sleeperYoyoIndex = -1;

            originalDamage = 0;

            // WAKE-DOWN FX (visual only): the boost exhales out.
            Projectiles.YoyoParticles.SpawnBloom(
                Player.Center,
                8f,
                36f,
                20,
                new Color(
                    170,
                    255,
                    170
                ),
                0.45f
            );

            Main.NewText(
                "SLEEPER ENDED",
                Microsoft.Xna.Framework.Color.Yellow
            );
        }

        // RESET

        public override void OnRespawn()
        {
            damageTimer = 0;

            sleeperYoyoIndex = -1;

            originalDamage = 0;
        }
    }
}