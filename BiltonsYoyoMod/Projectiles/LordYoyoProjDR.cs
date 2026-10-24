using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjDR : ModProjectile
    {
        private const float BeamLength = 1800f;
        private const float BeamHitWidth = 18f;
        private const float SpinDegreesPerTick = 1.0f;

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        private float BeamAngle => Projectile.ai[0];
        private bool Spin => Math.Abs(Projectile.ai[1]) > 0.001f;
        private int ParentTEOCIndex => (int)Projectile.ai[2];

        public override void AI()
        {
            // DR is a beam attached to the TEOC, not a flying projectile.
            Projectile.velocity = Vector2.Zero;

            if (ParentTEOCIndex >= 0 && ParentTEOCIndex < Main.maxProjectiles)
            {
                Projectile parent = Main.projectile[ParentTEOCIndex];

                bool validTEOC =
                    parent.active &&
                    parent.type == ModContent.ProjectileType<LordYoyoProjTEOC>();

                bool validTS2 =
                    parent.active &&
                    parent.type == ModContent.ProjectileType<LordYoyoProjTS2>();

                if (validTEOC || validTS2)
                {
                    Projectile.Center = parent.Center;
                }
                else
                {
                    Projectile.Kill();
                    return;
                }
            }
            else
            {
                Projectile.Kill();
                return;
            }

            if (Spin)
            {
                float signedSpeed = Math.Abs(Projectile.ai[1]) > 0.001f
                    ? Projectile.ai[1]
                    : (Projectile.ai[1] >= 0f ? 1f : -1f);

                bool groupedSpin = Projectile.localAI[1] >= 1f;
                if (groupedSpin)
                {
                    // Grouped DRs spin for exactly one second, then remain still.
                    if (Projectile.localAI[2] > 0f)
                    {
                        Projectile.ai[0] += MathHelper.ToRadians(
                            SpinDegreesPerTick * signedSpeed
                        );
                        Projectile.localAI[2]--;
                    }
                }
                else
                {
                    float sign = Projectile.ai[1] >= 0f ? 1f : -1f;
                    Projectile.ai[0] += MathHelper.ToRadians(SpinDegreesPerTick) * sign;
                }
            }

            Vector2 beamDirection = BeamAngle.ToRotationVector2();
            Vector2 sideways = new Vector2(-beamDirection.Y, beamDirection.X);

            // The beam keeps a dense stream of icy particles around it.
            // This is intentionally much more frequent than the old single
            // 50% chance particle so the long beam does not look static.
            for (int i = 0; i < 5; i++)
            {
                float along = Main.rand.NextFloat(40f, BeamLength - 40f);
                Vector2 particlePosition =
                    Projectile.Center +
                    beamDirection * along +
                    sideways * Main.rand.NextFloat(-14f, 14f);

                Dust dust = Dust.NewDustPerfect(
                    particlePosition,
                    DustID.IceTorch,
                    Main.rand.NextVector2Circular(0.9f, 0.9f) + sideways * Main.rand.NextFloat(-0.15f, 0.15f),
                    80,
                    Color.White,
                    Main.rand.NextFloat(0.7f, 1.25f)
                );
                dust.noGravity = true;

                if (i == 0 || Main.rand.NextBool(3))
                {
                    Dust spark = Dust.NewDustPerfect(
                        particlePosition + sideways * Main.rand.NextFloat(-10f, 10f),
                        DustID.BlueTorch,
                        beamDirection * Main.rand.NextFloat(-0.25f, 0.25f) +
                        sideways * Main.rand.NextFloat(-0.7f, 0.7f),
                        70,
                        Color.White,
                        Main.rand.NextFloat(0.45f, 0.9f)
                    );
                    spark.noGravity = true;
                }
            }

            LordYoyoVisualHelper.AddBlueLight(Projectile.Center, 1.35f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 start = Projectile.Center;
            Vector2 end = start + BeamAngle.ToRotationVector2() * BeamLength;
            float collisionPoint = 0f;

            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(),
                targetHitbox.Size(),
                start,
                end,
                BeamHitWidth,
                ref collisionPoint
            );
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 direction = BeamAngle.ToRotationVector2();
            Vector2 start = Projectile.Center - Main.screenPosition;
            Vector2 end = start + direction * BeamLength;
            Vector2 delta = end - start;
            Vector2 midpoint = (start + end) * 0.5f;

            float lightFactor = 0.35f + Lighting.GetColor(
                (int)(Projectile.Center.X / 16f),
                (int)(Projectile.Center.Y / 16f)
            ).R / 255f * 0.65f;

            Main.spriteBatch.Draw(
                pixel,
                midpoint,
                new Rectangle(0, 0, 1, 1),
                new Color(55, 190, 255, 128) * lightFactor,
                delta.ToRotation(),
                new Vector2(0.5f, 0.5f),
                new Vector2(delta.Length(), BeamHitWidth),
                SpriteEffects.None,
                0f
            );

            Main.spriteBatch.Draw(
                pixel,
                midpoint,
                new Rectangle(0, 0, 1, 1),
                new Color(210, 245, 255, 64) * lightFactor,
                delta.ToRotation(),
                new Vector2(0.5f, 0.5f),
                new Vector2(delta.Length(), 7f),
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
