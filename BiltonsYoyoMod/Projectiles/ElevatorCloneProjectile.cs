using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class ElevatorCloneProjectile : ModProjectile
    {
        // SOURCE YOYO
        //
        // ai[0] stores the projectile type of the original yoyo.
        //
        // The clone itself does NOT use its own texture.
        // PreDraw() draws the actual source yoyo texture instead.

        private int SourceYoyoType =>
            (int)Projectile.ai[0];

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

            Projectile.timeLeft = 600;

            Projectile.DamageType =
                DamageClass.Melee;

            ProjectileID.Sets.TrailingMode[
                Projectile.type
            ] = 2;

            ProjectileID.Sets.TrailCacheLength[
                Projectile.type
            ] = 6;
        }

        // AI

        public override void AI()
        {
            // FALLING

            Projectile.velocity.Y += 0.15f;

            // Keep the falling speed reasonable.
            if (Projectile.velocity.Y > 16f)
            {
                Projectile.velocity.Y = 16f;
            }

            // ROTATION

            Projectile.rotation +=
                Projectile.velocity.X * 0.08f;

            // ICE PARTICLES

            if (Main.rand.NextBool(3))
            {
                Dust dust =
                    Dust.NewDustPerfect(
                        Projectile.Center,
                        DustID.IceTorch,
                        Vector2.Zero
                    );

                dust.noGravity = true;
                dust.velocity = Vector2.Zero;

                dust.scale =
                    Main.rand.NextFloat(
                        0.45f,
                        0.75f
                    );
            }
        }

        // TILE COLLISION

        public override bool OnTileCollide(
            Vector2 oldVelocity
        )
        {
            // HORIZONTAL BOUNCE

            if (Projectile.velocity.X !=
                oldVelocity.X)
            {
                Projectile.velocity.X =
                    -oldVelocity.X * 0.9f;
            }

            // VERTICAL BOUNCE

            if (Projectile.velocity.Y !=
                oldVelocity.Y)
            {
                Projectile.velocity.Y =
                    -oldVelocity.Y * 0.9f;
            }

            return false;
        }

        // GET SOURCE YOYO TEXTURE

        private Texture2D GetSourceTexture()
        {
            int type =
                SourceYoyoType;

            // No valid source projectile was supplied.
            if (type <= 0)
                return null;

            try
            {
                // Ask Terraria to load the projectile texture.
                Main.instance.LoadProjectile(type);

                // Use the texture belonging to the ORIGINAL YOYO.
                return TextureAssets.Projectile[
                    type
                ].Value;
            }
            catch
            {
                // Never fall back to an arrow or placeholder.
                return null;
            }
        }

        // DRAW

        public override bool PreDraw(
            ref Color lightColor
        )
        {
            Texture2D texture =
                GetSourceTexture();

            // If the original yoyo texture cannot be found,
            // don't draw anything.
            //
            // This prevents the clone's own texture from appearing.
            if (texture == null)
                return false;

            Vector2 origin =
                texture.Size() / 2f;

            // AFTERIMAGES

            for (int i = 5;
                 i >= 0;
                 i--)
            {
                if (i >= Projectile.oldPos.Length)
                    continue;

                Vector2 position =
                    Projectile.oldPos[i] +
                    Projectile.Size / 2f;

                // oldPos can contain an uninitialized position.
                if (position == Vector2.Zero)
                    continue;

                // FADE WITH DISTANCE

                float distance =
                    Vector2.Distance(
                        Projectile.Center,
                        position
                    );

                float distanceFade =
                    1f -
                    MathHelper.Clamp(
                        distance / 100f,
                        0f,
                        1f
                    );

                // FADE WITH AGE

                float ageFade =
                    1f -
                    i / 6f;

                float alpha =
                    0.4f *
                    distanceFade *
                    ageFade;

                if (alpha <= 0.01f)
                    continue;

                // WORLD LIGHTING

                int tileX =
                    (int)(position.X / 16f);

                int tileY =
                    (int)(position.Y / 16f);

                Color lighting =
                    Lighting.GetColor(
                        tileX,
                        tileY
                    );

                float brightness =
                    (
                        lighting.R +
                        lighting.G +
                        lighting.B
                    ) / 765f;

                brightness =
                    MathHelper.Clamp(
                        brightness,
                        0.4f,
                        1f
                    );

                Color drawColor =
                    Color.White *
                    (alpha * brightness);

                // ROTATION

                float rotation =
                    Projectile.rotation;

                if (i <
                    Projectile.oldRot.Length)
                {
                    rotation =
                        Projectile.oldRot[i];
                }

                // DRAW SOURCE YOYO AFTERIMAGE

                Main.EntitySpriteDraw(
                    texture,
                    position -
                    Main.screenPosition,
                    null,
                    drawColor,
                    rotation,
                    origin,
                    Projectile.scale,
                    SpriteEffects.None
                );
            }

            // CURRENT CLONE

            int currentTileX =
                (int)(Projectile.Center.X / 16f);

            int currentTileY =
                (int)(Projectile.Center.Y / 16f);

            Color currentLighting =
                Lighting.GetColor(
                    currentTileX,
                    currentTileY
                );

            float currentBrightness =
                (
                    currentLighting.R +
                    currentLighting.G +
                    currentLighting.B
                ) / 765f;

            currentBrightness =
                MathHelper.Clamp(
                    currentBrightness,
                    0.4f,
                    1f
                );

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center -
                Main.screenPosition,
                null,
                Color.White *
                currentBrightness,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None
            );

            // PREVENT DEFAULT DRAW
            //
            // The clone's own texture is NEVER drawn.
            // Only the original yoyo texture above is rendered.

            return false;
        }
    }
}