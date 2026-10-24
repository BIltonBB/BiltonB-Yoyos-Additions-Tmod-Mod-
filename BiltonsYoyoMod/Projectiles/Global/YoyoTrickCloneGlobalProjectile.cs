using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    public class YoyoTrickCloneGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public int BounceCooldown;

        public void Initialize(
            int yoyoType,
            int damage
        )
        {
            BounceCooldown = 0;
        }

        public override void PostAI(
            Projectile projectile
        )
        {
            if (projectile.ModProjectile
                is not YoyoTrickCloneProjectile)
            {
                return;
            }

            if (BounceCooldown > 0)
                BounceCooldown--;
        }
    }
}