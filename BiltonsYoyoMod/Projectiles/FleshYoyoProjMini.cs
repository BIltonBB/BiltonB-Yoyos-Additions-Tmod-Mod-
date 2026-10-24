using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class FleshYoyoProjMini : ModProjectile
    {
        public int parentIndex = -1;

        private float designatedAngle;

        private float oscillationSpeed;
        private float oscillationDistance;

        private bool initialized;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            /*
             * ------------------------------------------------------------
             * SCALE PROTECTION
             * ------------------------------------------------------------
             *
             * Prevent accidental projectile scale changes from making
             * the minion or its rendering shrink.
             */

            Projectile.scale = 1f;

            /*
             * ------------------------------------------------------------
             * GET PARENT
             * ------------------------------------------------------------
             */

            if (parentIndex == -1)
                parentIndex = (int)Projectile.ai[0];

            if (parentIndex < 0 ||
                parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent =
                Main.projectile[parentIndex];

            if (!parent.active ||
                parent.type != ModContent.ProjectileType<FleshYoyoProj>() ||
                parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 300;

            /*
             * ------------------------------------------------------------
             * INITIALIZE
             * ------------------------------------------------------------
             */

            if (!initialized)
            {
                designatedAngle = Projectile.ai[1];

                int variation = Projectile.whoAmI % 3;

                oscillationSpeed =
                    MathHelper.TwoPi / 120f +
                    (variation - 1) * 0.0025f;

                oscillationDistance =
                    7f +
                    variation * 2f;

                initialized = true;

                // Start exactly at the designated position.
                Vector2 startingDirection =
                    designatedAngle.ToRotationVector2();

                Projectile.Center =
                    parent.Center +
                    startingDirection * 55f;
            }

            /*
             * ------------------------------------------------------------
             * DESIGNATED POSITION
             * ------------------------------------------------------------
             */

            const float radius = 55f;

            Vector2 radialDirection =
                designatedAngle.ToRotationVector2();

            Vector2 designatedPosition =
                parent.Center +
                radialDirection * radius;

            /*
             * ------------------------------------------------------------
             * OSCILLATION
             * ------------------------------------------------------------
             */

            Vector2 oscillationDirection =
                new Vector2(
                    -radialDirection.Y,
                    radialDirection.X
                );

            float oscillation =
                (float)Math.Sin(
                    Main.GameUpdateCount * oscillationSpeed
                );

            Vector2 targetPosition =
                designatedPosition +
                oscillationDirection *
                oscillation *
                oscillationDistance;

            /*
             * ------------------------------------------------------------
             * MOVE
             * ------------------------------------------------------------
             */

            Vector2 difference =
                targetPosition - Projectile.Center;

            // Prevent any absurd movement from causing visual issues.
            if (difference.LengthSquared() > 1000000f)
            {
                Projectile.Center = targetPosition;
            }
            else
            {
                Projectile.Center += difference * 0.18f;
            }

            /*
             * ------------------------------------------------------------
             * FACE AWAY FROM YOYO
             * ------------------------------------------------------------
             *
             * radialDirection points FROM the yoyo TO the minion.
             *
             * We want the front of the minion to point in exactly
             * that direction.
             *
             * The texture's natural orientation is rotated by 90°,
             * so compensate by -90°.
             */

            Vector2 facingDirection =
                Projectile.Center - parent.Center;

            if (facingDirection.LengthSquared() > 0.01f)
            {
                Projectile.rotation =
                    facingDirection.ToRotation()
                    - MathHelper.PiOver2;
            }
        }

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 180);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (parentIndex < 0 ||
                parentIndex >= Main.maxProjectiles)
            {
                return false;
            }

            Projectile parent =
                Main.projectile[parentIndex];

            if (!parent.active)
                return false;

            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/FleshYoyoProjMini"
                ).Value;

            /*
             * ------------------------------------------------------------
             * RED STRING
             * ------------------------------------------------------------
             *
             * Draw a finite rectangle whose CENTER is exactly halfway
             * between the yoyo and minion.
             *
             * This prevents the "infinite rectangle" problem caused by
             * stretching a pixel from one end.
             */

            Vector2 start =
                parent.Center - Main.screenPosition;

            Vector2 end =
                Projectile.Center - Main.screenPosition;

            Vector2 difference =
                end - start;

            float length =
                difference.Length();

            if (length > 2f && length < 1000f)
            {
                Vector2 midpoint =
                    (start + end) * 0.5f;

                float rotation =
                    difference.ToRotation();

                Texture2D pixel =
                    Terraria.GameContent.TextureAssets.MagicPixel.Value;

                Main.spriteBatch.Draw(
                    pixel,
                    midpoint,
                    new Rectangle(0, 0, 1, 1),
                    new Color(180, 20, 20, 255),
                    rotation,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(length, 2f),
                    SpriteEffects.None,
                    0f
                );
            }

            /*
             * ------------------------------------------------------------
             * MINION
             * ------------------------------------------------------------
             */

            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                texture.Size() * 0.5f,
                1f,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}