using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class EmpressYoyoProjER : ModProjectile
    {
        private const int LifetimeFrames = 432;
        private const int TurnIntervalFrames = 6;
        private const float TurnDegrees = 5f;

        private int age;
        private float movementAngle;
        private float movementSpeed;
        private bool initialized;
        private const int FadeFrames = 60;

        public override void SetDefaults()
        {
            Projectile.width = 7;
            Projectile.height = 7;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = LifetimeFrames;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override void AI()
        {
            if (!initialized)
            {
                initialized = true;
                movementAngle = Projectile.velocity.ToRotation();
                movementSpeed = Math.Max(1f, Projectile.velocity.Length());
            }

            age++;

            // Continuous forward motion. The old return/reversal phase is removed.
            if (age % TurnIntervalFrames == 0)
                movementAngle -= MathHelper.ToRadians(TurnDegrees);

            Projectile.velocity = movementAngle.ToRotationVector2() * movementSpeed;

            // Projectiles in this mod use their movement angle directly.
            Projectile.rotation = movementAngle;

            Lighting.AddLight(
                Projectile.Center,
                GetGlowColor().ToVector3() * 1.45f
            );
        }

        private Color GetGlowColor()
        {
            int mode = (int)Projectile.ai[0];
            if (mode == 2)
                return new Color(255, 225, 90);

            return mode == 0
                ? new Color(255, 100, 210)
                : new Color(100, 180, 255);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Color glow = GetGlowColor();

            float fade = 1f;
            if (Projectile.timeLeft <= FadeFrames)
            {
                fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);
            }

            // Clean neon projectile. Rotation is intentionally untouched.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor * fade,
                Projectile.rotation,
                origin,
                0.5f,
                SpriteEffects.None,
                0f
            );

            // Wider colored aura.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                glow * (0.20f * fade),
                Projectile.rotation,
                origin,
                0.66f,
                SpriteEffects.None,
                0f
            );

            // Tight neon band.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                glow * (0.42f * fade),
                Projectile.rotation,
                origin,
                0.54f,
                SpriteEffects.None,
                0f
            );

            // Emissive white core to keep it readable even in daylight.
            Main.spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White * (0.40f * fade),
                Projectile.rotation,
                origin,
                0.50f,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
