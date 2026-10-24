using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class YoyoTrickBounceProjectile : ModProjectile
    {
        // SOURCE YOYO ITEM

        // Actual ITEM type of the yoyo that created this copy.
        // Works with vanilla and modded yoyo items.
        private int StoredYoyoItemType = -1;

        // LIFETIME

        // 10 seconds fully visible.
        private const int ActiveLifetime = 600;

        // 1 second fade-out.
        private const int FadeDuration = 60;

        // Total lifetime = 11 seconds.
        private const int TotalLifetime =
            ActiveLifetime +
            FadeDuration;

        // AFTERIMAGES

        private const int TrailLength = 8;

        // SPRITE SIZE

        // The original yoyo projectile is much smaller than a normal
        // inventory item icon, so the item texture is drawn smaller.
        //
        // This prevents ABB copies from becoming oversized.
        private const float SpriteScale = 0.65f;

        public override string Texture =>
            "Terraria/Images/Projectile_1";

        // DEFAULTS

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;

            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;

            Projectile.timeLeft =
                TotalLifetime;

            Projectile.DamageType =
                DamageClass.Melee;

            // Cache projectile positions for afterimages.
            ProjectileID.Sets.TrailingMode[
                Projectile.type
            ] = 2;

            ProjectileID.Sets.TrailCacheLength[
                Projectile.type
            ] = TrailLength;
        }

        // INITIALIZE

        public void Initialize(
            int yoyoItemType)
        {
            StoredYoyoItemType =
                yoyoItemType;
        }

        // AI

        public override void AI()
        {
            // Gravity.
            Projectile.velocity.Y +=
                0.15f;

            // Keep some bounce movement after repeated bounces.
            if (Projectile.velocity.LengthSquared() > 0.01f &&
                Projectile.velocity.Length() < 3f)
            {
                Projectile.velocity *=
                    1.02f;
            }

            Projectile.rotation +=
                Projectile.velocity.X *
                0.08f;
        }

        // TILE BOUNCE

        public override bool OnTileCollide(
            Vector2 oldVelocity)
        {
            if (Projectile.velocity.X !=
                oldVelocity.X)
            {
                Projectile.velocity.X =
                    -oldVelocity.X *
                    0.9f;
            }

            if (Projectile.velocity.Y !=
                oldVelocity.Y)
            {
                Projectile.velocity.Y =
                    -oldVelocity.Y *
                    0.9f;
            }

            // BOING FX (visual + audio only): alternating-side ring + pitch
            // ladder per bounce. localAI[0] is a free visual counter here.
            Projectile.localAI[0]++;

            float boingPitch =
                -0.4f +
                (Projectile.localAI[0] % 6) *
                0.12f;

            SoundEngine.PlaySound(
                SoundID.Item1 with { Pitch = boingPitch },
                Projectile.Center
            );

            YoyoParticles.SpawnRing(
                Projectile.Center,
                6f,
                16f + (Projectile.localAI[0] % 6) * 3f,
                16,
                new Color(
                    255,
                    255,
                    255
                ),
                2f
            );

            return false;
        }

        // SOURCE TEXTURE

        private Texture2D GetSourceTexture()
        {
            if (StoredYoyoItemType <= 0)
                return null;

            if (StoredYoyoItemType >=
                TextureAssets.Item.Length)
            {
                return null;
            }

            // TextureAssets.Item contains modded item textures too.
            Main.instance.LoadItem(
                StoredYoyoItemType
            );

            return TextureAssets.Item[
                StoredYoyoItemType
            ].Value;
        }

        // LIFETIME ALPHA

        private float GetLifetimeAlpha()
        {
            if (Projectile.timeLeft >
                FadeDuration)
            {
                return 1f;
            }

            return MathHelper.Clamp(
                Projectile.timeLeft /
                (float)FadeDuration,
                0f,
                1f
            );
        }

        // WORLD LIGHTING

        private float GetLightingBrightness(
            Vector2 position)
        {
            int tileX =
                (int)(position.X / 16f);

            int tileY =
                (int)(position.Y / 16f);

            Color lighting =
                Lighting.GetColor(
                    tileX,
                    tileY
                );

            // Use the surrounding light as brightness only.
            //
            // This prevents a blue/purple light source from turning
            // the entire yoyo into a blue duplicate.
            float brightness =
                (
                    lighting.R +
                    lighting.G +
                    lighting.B
                ) /
                765f;

            return MathHelper.Clamp(
                brightness,
                0.45f,
                1f
            );
        }

        // DRAW

        public override bool PreDraw(
            ref Color lightColor)
        {
            Texture2D texture =
                GetSourceTexture();

            if (texture == null)
            {
                return false;
            }

            Vector2 origin =
                texture.Size() / 2f;

            float lifetimeAlpha =
                GetLifetimeAlpha();

            // AFTERIMAGES

            for (int i = TrailLength - 1;
                 i >= 0;
                 i--)
            {
                Vector2 oldPosition =
                    Projectile.oldPos[i] +
                    Projectile.Size / 2f;

                // Ignore uninitialized trail positions.
                if (oldPosition == Vector2.Zero)
                    continue;

                // DISTANCE FROM CURRENT POSITION

                float distance =
                    Vector2.Distance(
                        Projectile.Center,
                        oldPosition
                    );

                // Farther from the current copy = weaker.
                float distanceAlpha =
                    1f -
                    MathHelper.Clamp(
                        distance / 150f,
                        0f,
                        1f
                    );

                // Oldest image is weakest.
                float trailAlpha =
                    (TrailLength - i) /
                    (float)TrailLength;

                float finalAlpha =
                    lifetimeAlpha *
                    distanceAlpha *
                    trailAlpha *
                    0.65f;

                if (finalAlpha <= 0.02f)
                    continue;

                // LIGHTING AT THIS AFTERIMAGE

                float brightness =
                    GetLightingBrightness(
                        oldPosition
                    );

                Color drawColor =
                    Color.White;

                drawColor *=
                    brightness *
                    finalAlpha;

                // HISTORICAL ROTATION

                float rotation =
                    Projectile.rotation;

                if (i < Projectile.oldRot.Length)
                {
                    rotation =
                        Projectile.oldRot[i];
                }

                // DRAW AFTERIMAGE

                Main.EntitySpriteDraw(
                    texture,
                    oldPosition -
                        Main.screenPosition,
                    null,
                    drawColor,
                    rotation,
                    origin,
                    SpriteScale,
                    SpriteEffects.None
                );
            }

            // MAIN COPY

            float currentBrightness =
                GetLightingBrightness(
                    Projectile.Center
                );

            Color currentColor =
                Color.White;

            currentColor *=
                currentBrightness *
                lifetimeAlpha;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                    Main.screenPosition,
                null,
                currentColor,
                Projectile.rotation,
                origin,
                SpriteScale,
                SpriteEffects.None
            );

            return false;
        }
    }
}