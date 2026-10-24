using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class LunaticYoyoProjClone : ModProjectile
    {
        // SETTINGS

        private const int MinimumLifetime = 300;
        private const int MaximumLifetime = 900;


        // Normal 2-clone formation.
        private const float CloneAngle1 =
            MathHelper.TwoPi / 3f;

        private const float CloneAngle2 =
            MathHelper.TwoPi * 2f / 3f;


        // Low HP formation.
        //
        // Main   = 0°
        // Clone1 = 72°
        // Clone2 = 144°
        // Clone3 = 216°
        // Clone4 = 288°
        //
        // 360 / 5 = 72°.
        private const float LowHpAngleStep =
            MathHelper.TwoPi / 5f;


        private const byte CloneAlpha = 128;
        private const byte StringAlpha = 128;


        // EXPLOSION

        // Actual NPC damage radius.
        private const float ExplosionDamageRadius = 110f;


        // Visual ring radius.
        //
        // MUCH larger than the damage radius.
        private const float ExplosionVisualRadius = 440f;


        // How long the ring expands.
        private const int ExplosionDuration = 30;


        // More particles are required because the ring is now huge.
        private const int ExplosionDustCount = 128;


        // Size of individual blue dust particles.
        private const float ExplosionDustScale = 1.35f;


        private int explosionTimer;

        private bool exploding;

        private bool explosionDamageDone;


        // STATE

        private bool lifetimeInitialized;


        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.tileCollide = false;

            Projectile.penetrate = -1;

            Projectile.timeLeft =
                MaximumLifetime;

            Projectile.alpha =
                CloneAlpha;

            Projectile.DamageType =
                DamageClass.MeleeNoSpeed;
        }


        // AI

        public override void AI()
        {
            // INITIALIZE SHARED LIFETIME

            if (!lifetimeInitialized)
            {
                lifetimeInitialized = true;

                int sharedLifetime =
                    (int)Projectile.ai[2];


                if (
                    sharedLifetime < MinimumLifetime ||
                    sharedLifetime > MaximumLifetime
                )
                {
                    sharedLifetime =
                        Main.rand.Next(
                            MinimumLifetime,
                            MaximumLifetime + 1
                        );

                    Projectile.ai[2] =
                        sharedLifetime;
                }


                Projectile.timeLeft =
                    sharedLifetime;
            }


            // EXPLOSION

            if (exploding)
            {
                HandleExplosion();

                return;
            }


            // FIND PARENT

            Projectile mainYoyo =
                FindMainYoyo();


            if (mainYoyo == null)
            {
                Projectile.Kill();

                return;
            }


            Player player =
                Main.player[
                    Projectile.owner
                ];


            if (
                player == null ||
                !player.active
            )
            {
                Projectile.Kill();

                return;
            }


            // HP CHECK

            bool lowHp =
                player.statLife <=
                player.statLifeMax2 * 0.5f;


            int cloneSlot =
                (int)Projectile.ai[0];


            // Clone 3 and 4 only exist below 50% HP.
            if (
                cloneSlot >= 3 &&
                !lowHp
            )
            {
                Projectile.Kill();

                return;
            }


            // HOSTILE PROJECTILE CHECK

            if (
                Main.myPlayer ==
                Projectile.owner
            )
            {
                CheckHostileProjectiles();
            }


            // MAIN YOYO RADIUS

            Vector2 mainOffset =
                mainYoyo.Center -
                player.Center;


            float radius =
                mainOffset.Length();


            if (radius < 8f)
            {
                radius = 8f;

                mainOffset =
                    Vector2.UnitX *
                    radius;
            }


            float mainAngle =
                mainOffset.ToRotation();


            // CLONE ANGLE

            float cloneAngle;


            if (!lowHp)
            {
                // NORMAL 2-CLONE FORMATION

                if (cloneSlot == 1)
                {
                    cloneAngle =
                        mainAngle +
                        CloneAngle1;
                }
                else
                {
                    cloneAngle =
                        mainAngle +
                        CloneAngle2;
                }
            }
            else
            {
                // LOW HP 4-CLONE FORMATION

                cloneAngle =
                    mainAngle +
                    LowHpAngleStep *
                    cloneSlot;
            }


            // TARGET POSITION

            Vector2 desiredPosition =
                player.Center +
                cloneAngle.ToRotationVector2() *
                radius;


            Vector2 difference =
                desiredPosition -
                Projectile.Center;


            if (
                difference.LengthSquared() >
                1000000f
            )
            {
                Projectile.Center =
                    desiredPosition;
            }
            else
            {
                Projectile.Center +=
                    difference *
                    0.25f;
            }


            // ROTATION

            Projectile.rotation =
                mainYoyo.rotation;


            // TRANSPARENCY

            Projectile.alpha =
                CloneAlpha;


            // LIGHTING

            Lighting.AddLight(
                Projectile.Center,
                0.05f,
                0.02f,
                0.07f
            );
        }


        // HOSTILE PROJECTILE CHECK

        private void CheckHostileProjectiles()
        {
            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile other =
                    Main.projectile[i];


                if (
                    !other.active ||
                    other.whoAmI ==
                    Projectile.whoAmI
                )
                {
                    continue;
                }


                if (
                    !other.hostile ||
                    other.friendly
                )
                {
                    continue;
                }


                if (
                    !Projectile.Hitbox.Intersects(
                        other.Hitbox
                    )
                )
                {
                    continue;
                }


                TriggerCloneExplosion();

                return;
            }
        }


        // TRIGGER CLONE EXPLOSION

        private void TriggerCloneExplosion()
        {
            if (exploding)
            {
                return;
            }


            exploding = true;

            explosionTimer = 0;


            Projectile.timeLeft =
                ExplosionDuration + 2;


            // DAMAGE

            if (!explosionDamageDone)
            {
                explosionDamageDone = true;


                Projectile mainYoyo =
                    FindMainYoyo();


                int damage =
                    mainYoyo != null
                        ? Math.Max(
                            1,
                            mainYoyo.damage
                        )
                        : Math.Max(
                            1,
                            Projectile.damage * 4
                        );


                for (
                    int i = 0;
                    i < Main.maxNPCs;
                    i++
                )
                {
                    NPC npc =
                        Main.npc[i];


                    if (
                        !npc.active ||
                        npc.friendly ||
                        npc.dontTakeDamage
                    )
                    {
                        continue;
                    }


                    if (
                        Vector2.DistanceSquared(
                            npc.Center,
                            Projectile.Center
                        )
                        >
                        ExplosionDamageRadius *
                        ExplosionDamageRadius
                    )
                    {
                        continue;
                    }


                    int hitDirection =
                        npc.Center.X >=
                        Projectile.Center.X
                            ? 1
                            : -1;


                    npc.SimpleStrikeNPC(
                        damage,
                        hitDirection,
                        false,
                        8f,
                        DamageClass.Melee
                    );
                }
            }


            // START BLUE RING

            CreateExplosionEffect(
                Projectile.Center
            );


            Terraria.Audio.SoundEngine.PlaySound(
                SoundID.Item14,
                Projectile.Center
            );


            // CHAIN OTHER CLONES

            ExplodeOtherClones();
        }


        // EXPLODE OTHER CLONES

        private void ExplodeOtherClones()
        {
            int cloneType =
                ModContent.ProjectileType<
                    LunaticYoyoProjClone
                >();


            int parentIndex =
                (int)Projectile.ai[1];


            for (
                int i = 0;
                i < Main.maxProjectiles;
                i++
            )
            {
                Projectile other =
                    Main.projectile[i];


                if (
                    !other.active ||
                    other.whoAmI ==
                    Projectile.whoAmI
                )
                {
                    continue;
                }


                if (
                    other.owner !=
                    Projectile.owner
                )
                {
                    continue;
                }


                if (
                    other.type !=
                    cloneType
                )
                {
                    continue;
                }


                if (
                    (int)other.ai[1] !=
                    parentIndex
                )
                {
                    continue;
                }


                LunaticYoyoProjClone clone =
                    other.ModProjectile
                    as LunaticYoyoProjClone;


                if (clone != null)
                {
                    clone.StartChainExplosion();
                }
            }
        }


        // CHAIN EXPLOSION

        private void StartChainExplosion()
        {
            if (exploding)
            {
                return;
            }


            exploding = true;

            explosionTimer = 0;


            Projectile.timeLeft =
                ExplosionDuration + 2;


            // Chain clones display the ring
            // but do not deal explosion damage.
            explosionDamageDone = true;


            CreateExplosionEffect(
                Projectile.Center
            );
        }


        // BLUE EXPANDING CIRCLE

        private void CreateExplosionEffect(
            Vector2 position)
        {
            /*
             * Initial dense blue ring.
             */
            for (
                int i = 0;
                i < ExplosionDustCount;
                i++
            )
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    ExplosionDustCount;


                Vector2 direction =
                    angle.ToRotationVector2();


                Dust dust =
                    Dust.NewDustPerfect(
                        position +
                        direction * 8f,
                        DustID.BlueTorch,
                        direction * 2.5f,
                        40,
                        default,
                        ExplosionDustScale
                    );


                dust.noGravity = true;
            }


            Lighting.AddLight(
                position,
                0.2f,
                0.8f,
                1.5f
            );
        }


        // EXPLOSION ANIMATION

        private void HandleExplosion()
        {
            explosionTimer++;


            float progress =
                explosionTimer /
                (float)ExplosionDuration;


            progress =
                MathHelper.Clamp(
                    progress,
                    0f,
                    1f
                );


            // EXPANDING BLUE RING

            float radius =
                MathHelper.Lerp(
                    8f,
                    ExplosionVisualRadius,
                    progress
                );


            /*
             * The ring expands outward.
             *
             * 128 particles keep the huge circumference dense.
             */
            for (
                int i = 0;
                i < ExplosionDustCount;
                i++
            )
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    ExplosionDustCount;


                Vector2 direction =
                    angle.ToRotationVector2();


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        direction * radius,
                        DustID.BlueTorch,
                        Vector2.Zero,
                        255,
                        default,
                        ExplosionDustScale
                    );


                dust.noGravity = true;
            }


            // EXTRA BLUE PARTICLES

            /*
             * A few particles are added between the main ring
             * particles to make the circle feel less artificial.
             */
            for (
                int i = 0;
                i < 8;
                i++
            )
            {
                float angle =
                    Main.rand.NextFloat(
                        0f,
                        MathHelper.TwoPi
                    );


                Vector2 direction =
                    angle.ToRotationVector2();


                float randomRadius =
                    radius +
                    Main.rand.NextFloat(
                        -8f,
                        8f
                    );


                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center +
                        direction *
                        randomRadius,
                        DustID.BlueTorch,
                        direction *
                        Main.rand.NextFloat(
                            0.5f,
                            2f
                        ),
                        255,
                        default,
                        Main.rand.NextFloat(
                            0.9f,
                            1.5f
                        )
                    );


                dust.noGravity = true;
            }


            // LIGHT

            float lightFade =
                1f - progress;


            Lighting.AddLight(
                Projectile.Center,
                0.2f * lightFade,
                0.8f * lightFade,
                1.5f * lightFade
            );


            // END

            if (
                explosionTimer >=
                ExplosionDuration
            )
            {
                Projectile.Kill();
            }
        }


        // FIND PARENT YOYO

        private Projectile FindMainYoyo()
        {
            int parentIndex =
                (int)Projectile.ai[1];


            if (
                parentIndex >= 0 &&
                parentIndex <
                Main.maxProjectiles
            )
            {
                Projectile parent =
                    Main.projectile[
                        parentIndex
                    ];


                if (
                    parent.active &&
                    parent.type ==
                    ModContent.ProjectileType<
                        LunaticYoyoProj>() &&
                    parent.owner ==
                    Projectile.owner
                )
                {
                    return parent;
                }
            }


            return null;
        }


        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Player player =
                Main.player[
                    Projectile.owner
                ];


            if (
                player == null ||
                !player.active
            )
            {
                return false;
            }


            Texture2D texture =
                TextureAssets.Projectile[
                    ModContent.ProjectileType<
                        LunaticYoyoProj>()
                ].Value;


            Texture2D pixel =
                TextureAssets.MagicPixel.Value;


            Vector2 screenCenter =
                Projectile.Center -
                Main.screenPosition;


            // EXPLOSION

            if (exploding)
            {
                /*
                 * Hide the clone during the explosion.
                 *
                 * The expanding blue dust ring is the explosion.
                 */
                return false;
            }


            // STRING

            Vector2 start =
                player.Center -
                Main.screenPosition;


            Vector2 end =
                Projectile.Center -
                Main.screenPosition;


            Vector2 difference =
                end - start;


            float length =
                difference.Length();


            if (
                length > 2f &&
                length < 2000f
            )
            {
                Vector2 midpoint =
                    (start + end) *
                    0.5f;


                float rotation =
                    difference.ToRotation();


                Vector2 point1 =
                    Vector2.Lerp(
                        player.Center,
                        Projectile.Center,
                        0.25f
                    );


                Vector2 point2 =
                    Vector2.Lerp(
                        player.Center,
                        Projectile.Center,
                        0.50f
                    );


                Vector2 point3 =
                    Vector2.Lerp(
                        player.Center,
                        Projectile.Center,
                        0.75f
                    );


                Color light1 =
                    Lighting.GetColor(
                        (int)(
                            point1.X /
                            16f
                        ),
                        (int)(
                            point1.Y /
                            16f
                        )
                    );


                Color light2 =
                    Lighting.GetColor(
                        (int)(
                            point2.X /
                            16f
                        ),
                        (int)(
                            point2.Y /
                            16f
                        )
                    );


                Color light3 =
                    Lighting.GetColor(
                        (int)(
                            point3.X /
                            16f
                        ),
                        (int)(
                            point3.Y /
                            16f
                        )
                    );


                Color stringLight =
                    new Color(
                        (
                            light1.R +
                            light2.R +
                            light3.R
                        ) / 3,

                        (
                            light1.G +
                            light2.G +
                            light3.G
                        ) / 3,

                        (
                            light1.B +
                            light2.B +
                            light3.B
                        ) / 3
                    );


                float brightness =
                    (
                        stringLight.R +
                        stringLight.G +
                        stringLight.B
                    ) /
                    (3f * 255f);


                brightness =
                    MathHelper.Clamp(
                        brightness,
                        0.08f,
                        1f
                    );


                Color stringColor =
                    new Color(
                        (byte)(
                            255f *
                            brightness
                        ),

                        (byte)(
                            255f *
                            brightness
                        ),

                        (byte)(
                            255f *
                            brightness
                        ),

                        StringAlpha
                    );


                Main.spriteBatch.Draw(
                    pixel,
                    midpoint,
                    new Rectangle(
                        0,
                        0,
                        1,
                        1
                    ),
                    stringColor,
                    rotation,
                    new Vector2(
                        0.5f,
                        0.5f
                    ),
                    new Vector2(
                        length,
                        2f
                    ),
                    SpriteEffects.None,
                    0f
                );
            }


            // CLONE LIGHTING

            Color tileLight =
                Lighting.GetColor(
                    (int)(
                        Projectile.Center.X /
                        16f
                    ),
                    (int)(
                        Projectile.Center.Y /
                        16f
                    )
                );


            // SHADOW

            Color shadowColor =
                new Color(
                    0,
                    0,
                    0,
                    (byte)(
                        CloneAlpha *
                        0.45f
                    )
                );


            Main.EntitySpriteDraw(
                texture,
                screenCenter +
                new Vector2(
                    1.5f,
                    2f
                ),
                null,
                shadowColor,
                Projectile.rotation,
                texture.Size() *
                0.5f,
                1f,
                SpriteEffects.None,
                0f
            );


            // CLONE

            Color cloneColor =
                Color.Lerp(
                    tileLight,
                    new Color(
                        210,
                        190,
                        255
                    ),
                    0.25f
                );


            cloneColor.A =
                CloneAlpha;


            Main.EntitySpriteDraw(
                texture,
                screenCenter,
                null,
                cloneColor,
                Projectile.rotation,
                texture.Size() *
                0.5f,
                1f,
                SpriteEffects.None,
                0f
            );


            return false;
        }
    }
}