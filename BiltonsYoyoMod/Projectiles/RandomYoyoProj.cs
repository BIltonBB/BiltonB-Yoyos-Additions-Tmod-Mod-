using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class RandomYoyoProj : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            // Wooden-like handling: very slow, short string, very long life.
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 69f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 120f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 5f;
        }
    }
}
