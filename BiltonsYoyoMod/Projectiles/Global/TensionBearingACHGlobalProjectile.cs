// TensionBearingACHGlobalProjectile.cs
// CERAMIC HYBRID

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using BiltonsYoyoMod.Items.Players;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class TensionBearingACHGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // STATE

        private bool initialized;
        private bool forcedReturn;
        private bool explosionChecked;

        private int originalTimeLeft;
        private int baseDamage;
        private float baseKnockback;

        // MAX SPIN

        private int maxSpinTimer;
        private bool maxSpinActive;

        // EFFECT TIMERS

        private int startEffectTimer;
        private int endEffectTimer;

        // POSITION HISTORY

        private const int HistoryLength = 90;

        private readonly Vector2[] positionHistory =
            new Vector2[HistoryLength];

        private int historyCount;

        // STATS

        private const float LifetimeMultiplier = 1.05f;
        private const float KnockbackMultiplier = 1.05f;
        private const float DamageMultiplier = 1.05f;

        private const float MaxSpinDamageMultiplier = 1.15f;

        // MAX SPIN TIMING

        private const int MaxSpinInterval = 600; // 10 seconds
        private const int MaxSpinDuration = 300; // 5 seconds

        // RETURN

        private const float ReturnSpeedMultiplier = 2f;
        private const float ReturnDistance = 20f;

        private const float ExplosionChance = 0.15f;
        private const int ExplosionDamage = 100;

        // AFTERIMAGE SETTINGS

        // Exact world-space distance between afterimages.
        private const float AfterimageDistance = 12f;

        private const int MaxAfterimages = 8;

        // Increased so they are actually visible.
        private const float AfterimageAlpha = 0.65f;

        // MAX SPIN EFFECT SETTINGS

        // Much smaller than before.
        private const int StartEffectDuration = 14;
        private const int EndEffectDuration = 12;

        private const int StartDustCount = 10;
        private const int EndDustCount = 8;

        private const float StartEffectRadius = 20f;
        private const float EndEffectRadius = 16f;

        // SPAWN

        public override void OnSpawn(
            Projectile projectile,
            IEntitySource source)
        {
            if (projectile.aiStyle != ProjAIStyleID.Yoyo)
                return;

            originalTimeLeft = projectile.timeLeft;
            baseDamage = projectile.damage;
            baseKnockback = projectile.knockBack;

            maxSpinTimer = 0;
            maxSpinActive = false;

            startEffectTimer = 0;
            endEffectTimer = 0;

            historyCount = 1;
            positionHistory[0] = projectile.Center;
        }

        // AI

        public override void AI(
            Projectile projectile)
        {
            if (!projectile.active)
                return;

            if (projectile.aiStyle != ProjAIStyleID.Yoyo)
                return;

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return;
            }

            Player player =
                Main.player[projectile.owner];

            if (!player.active)
                return;

            TensionBearingACHPlayer modPlayer =
                player.GetModPlayer<TensionBearingACHPlayer>();

            if (!modPlayer.TensionBearingACH)
                return;

            // INITIALIZATION

            if (!initialized)
            {
                initialized = true;

                // +5% lifetime.
                if (originalTimeLeft > 0)
                {
                    projectile.timeLeft = Math.Max(
                        1,
                        (int)Math.Round(
                            originalTimeLeft *
                            LifetimeMultiplier
                        )
                    );
                }

                // +5% knockback.
                projectile.knockBack =
                    baseKnockback *
                    KnockbackMultiplier;

                // +5% damage.
                projectile.damage =
                    GetNormalDamage();
            }

            // EFFECT TIMER DECAY

            if (startEffectTimer > 0)
                startEffectTimer--;

            if (endEffectTimer > 0)
                endEffectTimer--;

            // MAX SPIN TIMER

            if (!forcedReturn)
            {
                if (!maxSpinActive)
                {
                    maxSpinTimer++;

                    if (maxSpinTimer >= MaxSpinInterval)
                    {
                        maxSpinTimer = 0;
                        maxSpinActive = true;

                        startEffectTimer =
                            StartEffectDuration;

                        TriggerMaxSpinStart(
                            projectile
                        );
                    }
                }
                else
                {
                    maxSpinTimer--;

                    if (maxSpinTimer <= 0)
                    {
                        maxSpinTimer = 0;
                        maxSpinActive = false;

                        endEffectTimer =
                            EndEffectDuration;

                        TriggerMaxSpinEnd(
                            projectile
                        );
                    }
                }
            }

            // DAMAGE

            if (maxSpinActive)
            {
                projectile.damage = Math.Max(
                    1,
                    (int)Math.Round(
                        baseDamage *
                        DamageMultiplier *
                        MaxSpinDamageMultiplier
                    )
                );
            }
            else
            {
                projectile.damage =
                    GetNormalDamage();
            }

            // POSITION HISTORY

            StorePosition(projectile);

            // RETURN

            if (forcedReturn)
            {
                Vector2 toPlayer =
                    player.MountedCenter -
                    projectile.Center;

                float distance =
                    toPlayer.Length();

                if (distance <= ReturnDistance)
                {
                    TrySnapExplosion(
                        projectile,
                        player
                    );

                    projectile.Kill();
                    return;
                }

                if (distance > 0.01f)
                {
                    toPlayer /= distance;

                    projectile.velocity =
                        toPlayer *
                        GetCurrentYoyoSpeed(player) *
                        ReturnSpeedMultiplier;

                    projectile.netUpdate = true;
                }
            }
        }

        // NORMAL DAMAGE

        private int GetNormalDamage()
        {
            return Math.Max(
                1,
                (int)Math.Round(
                    baseDamage *
                    DamageMultiplier
                )
            );
        }

        // CURRENT YOYO SPEED

        private float GetCurrentYoyoSpeed(
            Player player)
        {
            Item heldItem =
                player.HeldItem;

            if (!heldItem.IsAir &&
                heldItem.shootSpeed > 0f)
            {
                return heldItem.shootSpeed;
            }

            return 1f;
        }

        // TILE COLLISION

        public override bool OnTileCollide(
            Projectile projectile,
            Vector2 oldVelocity)
        {
            if (!projectile.active ||
                projectile.aiStyle != ProjAIStyleID.Yoyo)
            {
                return true;
            }

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return true;
            }

            Player player =
                Main.player[projectile.owner];

            if (!player.active)
                return true;

            if (!player.GetModPlayer<
                    TensionBearingACHPlayer>()
                .TensionBearingACH)
            {
                return true;
            }

            forcedReturn = true;

            Vector2 toPlayer =
                player.MountedCenter -
                projectile.Center;

            if (toPlayer.LengthSquared() > 0.001f)
            {
                toPlayer.Normalize();

                projectile.velocity =
                    toPlayer *
                    GetCurrentYoyoSpeed(player) *
                    ReturnSpeedMultiplier;
            }

            projectile.netUpdate = true;

            return false;
        }

        // SNAP EXPLOSION

        private void TrySnapExplosion(
            Projectile projectile,
            Player player)
        {
            if (explosionChecked)
                return;

            explosionChecked = true;

            if (Main.netMode ==
                NetmodeID.MultiplayerClient &&
                projectile.owner != Main.myPlayer)
            {
                return;
            }

            if (Main.rand.NextFloat() >=
                ExplosionChance)
            {
                return;
            }

            CreateSnapExplosion(
                player.Center
            );

            player.Hurt(
                PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(player.name + " was caught by the ceramic bearing snap.")),
                ExplosionDamage,
                0
            );
        }

        private void CreateSnapExplosion(
            Vector2 center)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(
                SoundID.Item14,
                center
            );

            for (int i = 0; i < 32; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        5f,
                        5f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Smoke,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            1.1f,
                            1.8f
                        )
                    );

                dust.noGravity = true;
            }

            for (int i = 0; i < 20; i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        7f,
                        7f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        center,
                        DustID.Torch,
                        velocity,
                        100,
                        default,
                        Main.rand.NextFloat(
                            0.8f,
                            1.3f
                        )
                    );

                dust.noGravity = true;
            }

            Lighting.AddLight(
                center,
                1.2f,
                0.75f,
                0.3f
            );
        }

        // POSITION HISTORY

        private void StorePosition(
            Projectile projectile)
        {
            if (historyCount < HistoryLength)
            {
                for (int i = historyCount;
                    i > 0;
                    i--)
                {
                    positionHistory[i] =
                        positionHistory[i - 1];
                }

                positionHistory[0] =
                    projectile.Center;

                historyCount++;
                return;
            }

            for (int i = HistoryLength - 1;
                i > 0;
                i--)
            {
                positionHistory[i] =
                    positionHistory[i - 1];
            }

            positionHistory[0] =
                projectile.Center;
        }

        // MAX SPIN START

        private void TriggerMaxSpinStart(
            Projectile projectile)
        {
            if (Main.dedServ)
                return;

            if (projectile.owner != Main.myPlayer)
                return;

            // Small, sharp activation sound.
            SoundEngine.PlaySound(
                SoundID.Item29,
                projectile.Center
            );

            CreateStartEffect(projectile);

            Lighting.AddLight(
                projectile.Center,
                0.65f,
                0.45f,
                0.15f
            );
        }

        // MAX SPIN END

        private void TriggerMaxSpinEnd(
            Projectile projectile)
        {
            if (Main.dedServ)
                return;

            if (projectile.owner != Main.myPlayer)
                return;

            SoundEngine.PlaySound(
                SoundID.Item4,
                projectile.Center
            );

            CreateEndEffect(projectile);

            Lighting.AddLight(
                projectile.Center,
                0.15f,
                0.3f,
                0.55f
            );
        }

        // START EFFECT

        private void CreateStartEffect(
            Projectile projectile)
        {
            for (int i = 0;
                i < StartDustCount;
                i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    StartDustCount;

                Vector2 direction =
                    angle.ToRotationVector2();

                Dust dust =
                    Dust.NewDustPerfect(
                        projectile.Center +
                        direction *
                        4f,
                        DustID.GemTopaz,
                        direction *
                        1.5f,
                        100,
                        default,
                        0.9f
                    );

                dust.noGravity = true;
            }
        }

        // END EFFECT

        private void CreateEndEffect(
            Projectile projectile)
        {
            for (int i = 0;
                i < EndDustCount;
                i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    EndDustCount;

                Vector2 direction =
                    angle.ToRotationVector2();

                Dust dust =
                    Dust.NewDustPerfect(
                        projectile.Center +
                        direction *
                        3f,
                        DustID.GemSapphire,
                        direction *
                        1.2f,
                        100,
                        default,
                        0.8f
                    );

                dust.noGravity = true;
            }
        }

        // DRAW

        public override void PostDraw(
            Projectile projectile,
            Color lightColor)
        {
            if (!projectile.active)
                return;

            if (projectile.aiStyle != ProjAIStyleID.Yoyo)
                return;

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return;
            }

            Player player =
                Main.player[projectile.owner];

            if (!player.active)
                return;

            if (!player.GetModPlayer<
                    TensionBearingACHPlayer>()
                .TensionBearingACH)
            {
                return;
            }

            // AFTERIMAGES ONLY DURING MAX SPIN

            if (maxSpinActive)
            {
                DrawDistanceBasedAfterimages(
                    projectile
                );
            }

            // START RING

            if (startEffectTimer > 0)
            {
                float progress =
                    1f -
                    startEffectTimer /
                    (float)StartEffectDuration;

                DrawSmallEffectRing(
                    projectile,
                    progress,
                    true
                );
            }

            // END RING

            if (endEffectTimer > 0)
            {
                float progress =
                    1f -
                    endEffectTimer /
                    (float)EndEffectDuration;

                DrawSmallEffectRing(
                    projectile,
                    progress,
                    false
                );
            }
        }

        // DISTANCE-BASED AFTERIMAGES

        private void DrawDistanceBasedAfterimages(
            Projectile projectile)
        {
            if (historyCount < 2)
                return;

            Texture2D texture =
                TextureAssets.Projectile[
                    projectile.type
                ].Value;

            Rectangle source =
                texture.Frame();

            Vector2 origin =
                source.Size() / 2f;

            float accumulatedDistance = 0f;

            Vector2 previous =
                positionHistory[0];

            int imageCount = 0;

            for (int i = 1;
                i < historyCount;
                i++)
            {
                Vector2 current =
                    positionHistory[i];

                Vector2 segment =
                    current -
                    previous;

                float segmentLength =
                    segment.Length();

                if (segmentLength <= 0.001f)
                {
                    previous = current;
                    continue;
                }

                Vector2 direction =
                    segment /
                    segmentLength;

                float remaining =
                    segmentLength;

                while (
                    accumulatedDistance +
                    remaining >=
                    AfterimageDistance)
                {
                    float requiredDistance =
                        AfterimageDistance -
                        accumulatedDistance;

                    Vector2 imagePosition =
                        previous +
                        direction *
                        requiredDistance;

                    float alpha =
                        AfterimageAlpha -
                        imageCount *
                        0.07f;

                    if (alpha <= 0f)
                        return;

                    // ACTUAL SURROUNDING LIGHTING

                    Color lighting =
                        Lighting.GetColor(
                            imagePosition.ToTileCoordinates()
                        );

                    /*
                     * Keep the lighting influence, but guarantee
                     * visibility in very dark areas.
                     *
                     * This means:
                     * bright area -> bright afterimage
                     * dark area   -> dim afterimage
                     * pitch dark  -> still faintly visible
                     */
                    lighting.R =
                        Math.Max(
                            lighting.R,
                            (byte)65
                        );

                    lighting.G =
                        Math.Max(
                            lighting.G,
                            (byte)65
                        );

                    lighting.B =
                        Math.Max(
                            lighting.B,
                            (byte)65
                        );

                    Color drawColor =
                        lighting *
                        alpha;

                    Main.EntitySpriteDraw(
                        texture,
                        imagePosition -
                        Main.screenPosition,
                        source,
                        drawColor,
                        projectile.rotation,
                        origin,
                        projectile.scale,
                        SpriteEffects.None,
                        0
                    );

                    imageCount++;

                    if (imageCount >=
                        MaxAfterimages)
                    {
                        return;
                    }

                    remaining -=
                        requiredDistance;

                    previous =
                        imagePosition;

                    accumulatedDistance = 0f;
                }

                accumulatedDistance +=
                    remaining;

                previous = current;
            }
        }

        // SMALL EFFECT RING

        private void DrawSmallEffectRing(
            Projectile projectile,
            float progress,
            bool starting)
        {
            if (Main.dedServ)
                return;

            int count =
                starting
                    ? 8
                    : 6;

            float maxRadius =
                starting
                    ? StartEffectRadius
                    : EndEffectRadius;

            float radius =
                4f +
                progress *
                maxRadius;

            float alpha =
                1f -
                progress;

            if (alpha <= 0f)
                return;

            for (int i = 0;
                i < count;
                i++)
            {
                float angle =
                    MathHelper.TwoPi *
                    i /
                    count;

                Vector2 position =
                    projectile.Center +
                    angle.ToRotationVector2() *
                    radius;

                Dust dust =
                    Dust.NewDustPerfect(
                        position,
                        starting
                            ? DustID.GemTopaz
                            : DustID.GemSapphire,
                        angle.ToRotationVector2() *
                        0.15f,
                        100,
                        default,
                        0.7f +
                        alpha * 0.35f
                    );

                dust.noGravity = true;
            }

            Lighting.AddLight(
                projectile.Center,
                starting
                    ? 0.35f * alpha
                    : 0.20f * alpha,
                starting
                    ? 0.25f * alpha
                    : 0.35f * alpha,
                starting
                    ? 0.08f * alpha
                    : 0.65f * alpha
            );
        }
    }
} 