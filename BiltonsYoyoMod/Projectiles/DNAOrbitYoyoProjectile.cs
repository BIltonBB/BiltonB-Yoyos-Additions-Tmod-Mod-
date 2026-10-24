using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using System;
using Terraria.ModLoader;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Projectiles
{
    public class DNAOrbitYoyoProjectile : ModProjectile
    {
        // SETTINGS

        // Close to the main yoyo.
        private const float OrbitRadius =
            28f;

        // Fast swirling speed.
        private const float OrbitSpeed =
            0.25f;

        // Distance between afterimages.
        private const float AfterimageSpacing =
            8f;

        // Maximum number of afterimages.
        private const int MaximumAfterimages =
            10;

        // STATE

        private float startingAngle;

        // Previous position used to determine how far the
        // orbiting yoyo travelled since the previous frame.
        private Vector2 previousPosition;

        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width =
                32;

            Projectile.height =
                32;

            // The clones can damage NPCs.
            Projectile.friendly =
                true;

            Projectile.hostile =
                false;

            Projectile.tileCollide =
                false;

            Projectile.ignoreWater =
                true;

            Projectile.penetrate =
                -1;

            Projectile.timeLeft =
                20 * 60;

            // 0.1 second hit cooldown.
            Projectile.usesLocalNPCImmunity =
                true;

            Projectile.localNPCHitCooldown =
                6;

            // We draw the yoyo ourselves.
            Projectile.hide =
                false;
        }

        // SPAWN

        public override void OnSpawn(
            Terraria.DataStructures.IEntitySource source
        )
        {
            // DNAPlayer supplies:
            //
            // Clone 1 =   0°
            // Clone 2 = 120°
            // Clone 3 = 240°
            //
            // Together they cover the complete 360°.
            startingAngle =
                Projectile.ai[1];

            previousPosition =
                Projectile.Center;
        }

        // AI

        public override void AI()
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
                Projectile.Kill();
                return;
            }

            // FIND THE ACTUAL MAIN YOYO
            //
            // The clone always follows the yoyo returned by
            // YoyoTrickSystem.
            //
            // No distance searching.
            // No projectile-type guessing.

            Projectile original =
                YoyoTrickSystem.GetMainYoyo(
                    player
                );

            if (original == null)
            {
                Projectile.Kill();
                return;
            }

            // SWIRL

            float angle =
                startingAngle +
                Main.GameUpdateCount *
                OrbitSpeed;

            Vector2 newPosition =
                original.Center +
                angle.ToRotationVector2() *
                OrbitRadius;

            // Store movement distance for the afterimage system.
            previousPosition =
                Projectile.Center;

            Projectile.Center =
                newPosition;

            Projectile.velocity =
                Vector2.Zero;

            // Rotate the actual clone.
            Projectile.rotation =
                angle;

            // CLONE DAMAGE

            Projectile.damage =
                original.damage / 3;

            if (Projectile.damage < 1)
            {
                Projectile.damage = 1;
            }
        }

        // DRAW

        public override bool PreDraw(
            ref Color lightColor
        )
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

            // FIND THE ACTUAL MAIN YOYO

            Projectile original =
                YoyoTrickSystem.GetMainYoyo(
                    player
                );

            if (original == null)
                return false;

            int yoyoType =
                original.type;

            if (
                yoyoType < 0 ||
                yoyoType >=
                    TextureAssets.Projectile.Length
            )
            {
                return false;
            }

            Texture2D texture =
                TextureAssets.Projectile[
                    yoyoType
                ].Value;

            Rectangle source =
                texture.Bounds;

            Vector2 origin =
                source.Size() *
                0.5f;

            // DISTANCE-BASED AFTERIMAGES

            float distanceTravelled =
                Vector2.Distance(
                    previousPosition,
                    Projectile.Center
                );

           int afterimageCount =
    Math.Clamp(
        (int)(
            distanceTravelled /
            AfterimageSpacing
        ) + 1,
        1,
        MaximumAfterimages
    );

            // DRAW AFTERIMAGES

            for (
                int i = afterimageCount - 1;
                i >= 0;
                i--
            )
            {
                float progress =
                    (i + 1f) /
                    (afterimageCount + 1f);

                Vector2 afterimagePosition =
                    Vector2.Lerp(
                        previousPosition,
                        Projectile.Center,
                        progress
                    );

                // LIGHTING AT THIS EXACT AFTERIMAGE POSITION

                Point tilePosition =
                    afterimagePosition.ToTileCoordinates();

                Color lighting =
                    Lighting.GetColor(
                        tilePosition.X,
                        tilePosition.Y
                    );

                // Fade farther afterimages.
                float fade =
                    1f -
                    progress;

                fade *= 0.55f;

                if (fade < 0.05f)
                    fade = 0.05f;

                Color afterimageColor =
                    lighting *
                    fade;

                Main.EntitySpriteDraw(
                    texture,
                    afterimagePosition -
                        Main.screenPosition,
                    source,
                    afterimageColor,
                    original.rotation,
                    origin,
                    original.scale,
                    SpriteEffects.None,
                    0
                );
            }

            // DRAW ACTUAL CLONE

            Color currentLighting =
                Lighting.GetColor(
                    Projectile.Center.ToTileCoordinates()
                );

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                    Main.screenPosition,
                source,
                currentLighting,
                original.rotation,
                origin,
                original.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}