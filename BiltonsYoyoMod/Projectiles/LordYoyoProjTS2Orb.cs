using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjTS2Orb : ModProjectile
    {
        private readonly List<Vector2> afterimages = new List<Vector2>();
        private Vector2 fallbackCenter;
        private int age;

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            Projectile parentSequence = null;
            int sequenceIndex = (int)Projectile.ai[0];
            if (sequenceIndex >= 0 && sequenceIndex < Main.maxProjectiles)
            {
                Projectile p = Main.projectile[sequenceIndex];
                if (p.active && p.type == ModContent.ProjectileType<LordYoyoProjTS2>())
                    parentSequence = p;
            }

            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
            {
                Projectile.Kill();
                return;
            }

            fallbackCenter = player.Center;
            age++;

            float travelProgress = MathHelper.Clamp(age / 36f, 0f, 1f);
            float speed = MathHelper.Lerp(2f, 18f, MathHelper.SmoothStep(0f, 1f, travelProgress));
            Vector2 targetDirection = (player.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDirection * speed, 0.16f);
            Projectile.Center += Projectile.velocity;

            // The TS2 orb's front is horizontally oriented, so it follows
            // its actual travel direction directly.
            if (Projectile.velocity.LengthSquared() > 0.001f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Fade in for 0.5s, then fade out sharply near the center.
            float fadeIn = MathHelper.Clamp(age / 30f, 0f, 1f);
            float distanceToPlayer = Vector2.Distance(Projectile.Center, player.Center);
            float fadeOut = MathHelper.Clamp(distanceToPlayer / 80f, 0f, 1f);
            float visibility = fadeIn * fadeOut;
            float alpha = MathHelper.Clamp(255f * (1f - visibility), 0f, 255f);
            Projectile.alpha = (int)alpha;

            LordYoyoVisualHelper.CachePosition(afterimages, Projectile.Center, 4f, 24);
            LordYoyoVisualHelper.AddBlueLight(Projectile.Center, 1.5f);

            if (distanceToPlayer < 12f || parentSequence == null)
            {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[ModContent.ProjectileType<LordYoyoProjPB>()].Value;
            LordYoyoVisualHelper.DrawNeonTrail(texture, afterimages, Projectile.rotation, 0.62f, lightColor, Main.screenPosition, 1.55f);

            Color core = new Color(190, 245, 255, (byte)(255 - Projectile.alpha));
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                core,
                Projectile.rotation,
                texture.Size() * 0.5f,
                0.62f,
                SpriteEffects.None,
                0f
            );

            return false;
        }
    }
}
