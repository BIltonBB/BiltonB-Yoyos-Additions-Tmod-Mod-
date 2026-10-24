using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class FleshYoyoBeamProj : ModProjectile
    {
        private const int TrailCount = 9;

        private bool positionInitialized;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.aiStyle = 0;
        }

        public override void AI()
        {
            /*
             * ------------------------------------------------------------
             * LOCK EXACT FIRING POSITION
             * ------------------------------------------------------------
             *
             * ai[0] and ai[1] contain the position where the yoyo
             * created this laser.
             *
             * This happens only once.
             */

            if (!positionInitialized)
            {
                Projectile.Center =
                    new Vector2(
                        Projectile.ai[0],
                        Projectile.ai[1]
                    );

                positionInitialized = true;
            }

            Projectile.rotation =
                Projectile.velocity.ToRotation();

            // Bright purple magical particles.
            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.PurpleTorch,
                    -Projectile.velocity * 0.035f +
                    Main.rand.NextVector2Circular(1.2f, 1.2f),
                    100,
                    default,
                    1.1f
                );

                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture =
                ModContent.Request<Texture2D>(
                    "BiltonsYoyoMod/Projectiles/FleshYoyoBeamProj"
                ).Value;

            /*
             * ------------------------------------------------------------
             * BRIGHT PURPLE GLOW + TRAIL
             * ------------------------------------------------------------
             *
             * Batch safety: this used to call spriteBatch.End()/Begin() by hand
             * with NO transform matrix, and it left that window (Identity matrix,
             * LinearClamp) open for every projectile drawn after this beam in the
             * same pass. AdditiveDraw runs the same additive pass but always
             * re-opens a vanilla-identical world batch afterwards, so nothing
             * downstream inherits a foreign window.
             *
             * ALPHA MUST BE 255 HERE. FNA's Additive state is
             * (SourceAlpha, SourceAlpha, One, One), so an additive draw
             * contributes srcRGB * (srcA / 255). Colours written as
             * `new Color(r, g, b, 0)` therefore contributed NOTHING and this
             * whole glow/trail stack rendered as nothing at all - only the
             * opaque white laser below was ever visible. Fade is carried by the
             * RGB multiplication, never by the alpha channel.
             */

            MiniYoyoFx.AdditiveDraw(() =>
            {
                /*
                 * --------------------------------------------------------
                 * STRONG TRAIL
                 * --------------------------------------------------------
                 */

                for (int i = TrailCount; i >= 1; i--)
                {
                    Vector2 trailPosition =
                        Projectile.Center -
                        Projectile.velocity * i * 0.65f;

                    float fade =
                        0.22f *
                        (1f - i / (float)(TrailCount + 1));

                    Main.spriteBatch.Draw(
                        texture,
                        trailPosition - Main.screenPosition,
                        null,
                        new Color(200, 40, 255) * fade,
                        Projectile.rotation,
                        texture.Size() / 2f,
                        Projectile.scale * 1.65f,
                        SpriteEffects.None,
                        0f
                    );
                }

                /*
                 * --------------------------------------------------------
                 * HUGE OUTER PURPLE GLOW
                 * --------------------------------------------------------
                 */

                Main.spriteBatch.Draw(
                    texture,
                    Projectile.Center - Main.screenPosition,
                    null,
                    new Color(170, 30, 255) * 0.75f,
                    Projectile.rotation,
                    texture.Size() / 2f,
                    Projectile.scale * 3.2f,
                    SpriteEffects.None,
                    0f
                );

                /*
                 * --------------------------------------------------------
                 * BRIGHTER MIDDLE GLOW
                 * --------------------------------------------------------
                 */

                Main.spriteBatch.Draw(
                    texture,
                    Projectile.Center - Main.screenPosition,
                    null,
                    new Color(220, 70, 255) * 0.85f,
                    Projectile.rotation,
                    texture.Size() / 2f,
                    Projectile.scale * 2.0f,
                    SpriteEffects.None,
                    0f
                );

                /*
                 * --------------------------------------------------------
                 * HOT PURPLE CORE
                 * --------------------------------------------------------
                 */

                Main.spriteBatch.Draw(
                    texture,
                    Projectile.Center - Main.screenPosition,
                    null,
                    new Color(245, 150, 255),
                    Projectile.rotation,
                    texture.Size() / 2f,
                    Projectile.scale * 1.25f,
                    SpriteEffects.None,
                    0f
                );
            });

            /*
             * ------------------------------------------------------------
             * MAIN LASER
             * ------------------------------------------------------------
             */

            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White,
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