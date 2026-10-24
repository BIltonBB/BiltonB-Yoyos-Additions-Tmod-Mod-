using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjPE : ModProjectile
    {
        private readonly List<Vector2> afterimages = new List<Vector2>();
        private const float Speed = 10f;
        private const float MaxTurn = MathHelper.Pi / 6f;

        private int TargetIndex => (int)Projectile.ai[0];

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 360;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            NPC target = GetTarget();
            Vector2 current = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            if (target != null)
            {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(current);
                float delta = MathHelper.WrapAngle(desired.ToRotation() - current.ToRotation());
                float clamped = MathHelper.Clamp(delta, -MaxTurn, MaxTurn);

                // Smooth steering while never exceeding the 30° cap.
                current = Vector2.Lerp(current, current.RotatedBy(clamped), 0.18f).SafeNormalize(Vector2.UnitX);
            }

            Projectile.velocity = current * Speed;
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            LordYoyoVisualHelper.CachePosition(afterimages, Projectile.Center, 4f, 24);
            LordYoyoVisualHelper.AddBlueLight(Projectile.Center, 1.1f);
        }

        private NPC GetTarget()
        {
            if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[TargetIndex];
            return npc.active && !npc.friendly && !npc.dontTakeDamage && npc.life > 0 ? npc : null;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            LordYoyoVisualHelper.DrawNeonTrail(texture, afterimages, Projectile.rotation, 0.75f, lightColor, Main.screenPosition, 1.35f);
            LordYoyoVisualHelper.DrawNeonSprite(texture, Projectile.Center, Projectile.rotation, 0.75f, lightColor, Main.screenPosition, 1.35f);
            return false;
        }
    }
}
