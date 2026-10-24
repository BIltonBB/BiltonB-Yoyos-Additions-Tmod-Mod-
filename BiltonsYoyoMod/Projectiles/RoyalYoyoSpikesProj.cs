using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class RoyalYoyoSpikesProj : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;
            Projectile.timeLeft = 240; // 3 seconds active + 1 second fade away = 240 ticks total

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.aiStyle = ProjAIStyleID.Arrow;
        }

        public override void AI()
        {
            // After 3 seconds (when 60 ticks / 1 second remains), smoothly fade out over 1 second
            if (Projectile.timeLeft <= 60)
            {
                float fadeProgress = 1f - ((float)Projectile.timeLeft / 60f);
                Projectile.alpha = (int)MathHelper.Lerp(0f, 255f, fadeProgress);
            }
        }
    }
}
