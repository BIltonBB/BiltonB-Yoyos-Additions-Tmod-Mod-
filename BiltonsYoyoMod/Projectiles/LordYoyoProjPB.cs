using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjPB : ModProjectile
    {
        private readonly List<Vector2> afterimages = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.timeLeft = 180;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            LordYoyoVisualHelper.CachePosition(afterimages, Projectile.Center, 4f, 24);

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            LordYoyoVisualHelper.AddBlueLight(Projectile.Center, 1.1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            LordYoyoVisualHelper.DrawNeonTrail(texture, afterimages, Projectile.rotation, 0.72f, lightColor, Main.screenPosition, 1.4f);
            LordYoyoVisualHelper.DrawNeonSprite(texture, Projectile.Center, Projectile.rotation, 0.72f, lightColor, Main.screenPosition, 1.35f);
            return false;
        }
    }
}
