using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class PrimeYoyoProj : ModProjectile
    {
        private bool spawned;

        public int lastHitNPC = -1;


        // ABILITY SETTINGS

        // 20 seconds.
        private const int AbilityCooldown = 1200;

        // 5 seconds.
        private const int AbilityDuration = 300;

        // Ability spins 2x faster.
        private const float AbilitySpinMultiplier = 2f;


        // ABILITY STATE

        private bool AbilityActive =>
            Projectile.localAI[1] > 0f;


        private int AbilityTimer
        {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }


        // AFTERIMAGES

        private Vector2[] afterimages =
            new Vector2[PrimeYoyoVisualHelper.YoyoAfterimageCount];

        private float afterimageDistance;
        private bool afterimagesInitialized;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle =
                ProjAIStyleID.Yoyo;

            Projectile.friendly = true;
            Projectile.penetrate = -1;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosMaximumRange[
                Projectile.type
            ] = 300f;

            ProjectileID.Sets.YoyosTopSpeed[
                Projectile.type
            ] = 16f;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[
                Projectile.type
            ] = -1f;
        }


        // AI

        public override void AI()
        {
            // ABILITY TIMER

            if (AbilityActive)
            {
                Projectile.localAI[1]--;

                if (Projectile.localAI[1] <= 0f)
                {
                    Projectile.localAI[1] = 0f;

                    // Reset cooldown after ability ends.
                    AbilityTimer = 0;

                    // Clear old afterimages.
                    afterimagesInitialized = false;
                    afterimageDistance = 0f;

                    // Ability END explosion.
                    if (Main.myPlayer == Projectile.owner)
                    {
                        AbilityEndEffect();
                    }

                    Projectile.netUpdate = true;
                }
            }
            else
            {
                AbilityTimer++;

                if (AbilityTimer >= AbilityCooldown)
                {
                    Projectile.localAI[1] =
                        AbilityDuration;

                    AbilityTimer =
                        AbilityCooldown;

                    // Reset afterimage history.
                    afterimagesInitialized = false;
                    afterimageDistance = 0f;

                    // Ability START explosion.
                    if (Main.myPlayer == Projectile.owner)
                    {
                        AbilityStartEffect();
                    }

                    Projectile.netUpdate = true;
                }
            }


            // DOUBLE DAMAGE

            /*
             * Store the original damage so we don't continuously
             * multiply it every frame.
             */

            if (Projectile.localAI[2] <= 0f)
            {
                Projectile.localAI[2] =
                    Projectile.damage;
            }

            int normalDamage =
                (int)Projectile.localAI[2];


            if (AbilityActive)
            {
                Projectile.damage =
                    normalDamage * 2;
            }
            else
            {
                Projectile.damage =
                    normalDamage;
            }


            // SPAWN PARTS

            if (!spawned &&
                Main.myPlayer == Projectile.owner)
            {
                spawned = true;

                SpawnPart(
                    ModContent.ProjectileType<PrimeYoyoProjPL>(),
                    Projectile.damage / 2,
                    0f
                );

                SpawnPart(
                    ModContent.ProjectileType<PrimeYoyoProjPC>(),
                    Projectile.damage / 2,
                    1f
                );

                SpawnPart(
                    ModContent.ProjectileType<PrimeYoyoProjPS>(),
                    Projectile.damage / 2,
                    2f
                );

                SpawnPart(
                    ModContent.ProjectileType<PrimeYoyoProjPV>(),
                    Projectile.damage,
                    3f
                );
            }


            // ABILITY VISUALS

            if (AbilityActive)
            {
                PrimeYoyoVisualHelper.TrackYoyoAfterimages(
                    afterimages,
                    ref afterimageDistance,
                    ref afterimagesInitialized,
                    Projectile.Center
                );


                // Strong warm Terraria lighting.
                Lighting.AddLight(
                    Projectile.Center,
                    1.15f,
                    0.80f,
                    0.25f
                );
            }


            // EXTRA SPIN

            /*
             * Vanilla yoyo movement remains unchanged.
             *
             * We add an extra rotation amount to make the visible
             * yoyo spin approximately 2x faster during the ability.
             */

            if (AbilityActive &&
                Projectile.velocity.LengthSquared() > 0.01f)
            {
                Projectile.rotation +=
                    Projectile.velocity.Length() *
                    0.08f *
                    (AbilitySpinMultiplier - 1f);
            }
        }


        // SPAWN PART

        private void SpawnPart(
            int type,
            int damage,
            float role)
        {
            Projectile part =
                Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    type,
                    damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    Projectile.whoAmI,
                    role
                );

            part.timeLeft = 10;
            part.netUpdate = true;
        }


        // ABILITY START EFFECT

        private void AbilityStartEffect()
        {
            Vector2 center =
                Projectile.Center;


            // SOUND

            SoundEngine.PlaySound(
                SoundID.Item14,
                center
            );


            // LIGHT

            Lighting.AddLight(
                center,
                1.5f,
                1.0f,
                0.3f
            );


            // FIRE BURST

            for (int i = 0; i < 42; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        6.5f,
                        6.5f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Torch,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.9f,
                            1.6f
                        )
                    );

                dust.noGravity = true;
            }


            // ELECTRIC BURST

            for (int i = 26; i > 0; i--)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        7f,
                        7f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Electric,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.2f
                        )
                    );

                dust.noGravity = true;
            }


            // SMOKE

            for (int i = 0; i < 18; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Smoke,
                        Main.rand.NextVector2Circular(
                            3.5f,
                            3.5f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.9f,
                            1.5f
                        )
                    );

                dust.noGravity = false;
            }
        }


        // ABILITY END EFFECT

        private void AbilityEndEffect()
        {
            Vector2 center =
                Projectile.Center;


            // SOUND

            SoundEngine.PlaySound(
                SoundID.Item93,
                center
            );


            // LIGHT

            Lighting.AddLight(
                center,
                1.2f,
                0.75f,
                0.2f
            );


            // ELECTRIC BURST

            for (int i = 0; i < 36; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        7f,
                        7f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Electric,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.6f,
                            1.15f
                        )
                    );

                dust.noGravity = true;
            }


            // FIRE BURST

            for (int i = 0; i < 24; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        5f,
                        5f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Torch,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.7f,
                            1.3f
                        )
                    );

                dust.noGravity = true;
            }


            // SMOKE

            for (int i = 0; i < 12; i++)
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Smoke,
                        Main.rand.NextVector2Circular(
                            2.5f,
                            2.5f
                        ),
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.3f
                        )
                    );

                dust.noGravity = false;
            }
        }


        // HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            lastHitNPC =
                target.whoAmI;
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/PrimeYoyoProj"
                ).Value;


            // ABILITY AFTERIMAGES

            if (AbilityActive)
            {
                PrimeYoyoVisualHelper.DrawYoyoAfterimages(
                    texture,
                    afterimages,
                    Projectile.rotation,
                    Projectile.scale
                );


                // ACTIVE ABILITY RED AURA

                /*
                 * Red aura behind the yoyo while the ability is active.
                 *
                 * Smaller than the previous yellow aura.
                 */

                PrimeYoyoVisualHelper.DrawGlow(
                    texture,
                    Projectile.Center,
                    Projectile.rotation,
                    Projectile.scale * 0.88f,
                    new Color(
                        255,
                        50,
                        40
                    ),
                    0.75f
                );
            }


            // MAIN YOYO

            Main.spriteBatch.Draw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                texture.Size() / 2f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}