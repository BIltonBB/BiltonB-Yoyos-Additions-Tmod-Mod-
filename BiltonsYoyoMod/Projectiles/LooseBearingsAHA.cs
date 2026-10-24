using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class LooseBearingsAHA : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.aiStyle = 0;
        }

        public override void AI()
        {
            // 50% normal gravity.
            Projectile.velocity.Y += 0.175f;
            if (Projectile.velocity.Y > 12f)
                Projectile.velocity.Y = 12f;

            Projectile.rotation += Projectile.velocity.X * 0.14f;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Projectile.Kill();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Projectile.Kill();
        }
    }
}
