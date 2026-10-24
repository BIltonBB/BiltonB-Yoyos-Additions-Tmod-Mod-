using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class EyeYoyoRedProj : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 9;
            Projectile.height = 10;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.aiStyle = 0;
        }
    }
}