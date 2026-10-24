using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Common
{
    public class LooseBearingsSystems : GlobalProjectile
    {
        public override bool PreDraw(
            Projectile projectile,
            ref Color lightColor)
        {
            if (!IsLooseBearing(projectile.type))
                return base.PreDraw(
                    projectile,
                    ref lightColor);

            // Keep 50% of the normal lighting effect.
            // This makes Loose Bearings remain brighter
            // when travelling through dark areas.
            lightColor =
                Color.Lerp(
                    Color.White,
                    lightColor,
                    0.5f);

            return base.PreDraw(
                projectile,
                ref lightColor);
        }

        // CHECK LOOSE BEARING PROJECTILE

        private bool IsLooseBearing(
            int projectileType)
        {
            return projectileType ==
                ModContent.ProjectileType<
                    Projectiles.LooseBearingsAW>()
                ||
                projectileType ==
                ModContent.ProjectileType<
                    Projectiles.LooseBearingsAHE>()
                ||
                projectileType ==
                ModContent.ProjectileType<
                    Projectiles.LooseBearingsAM>()
                ||
                projectileType ==
                ModContent.ProjectileType<
                    Projectiles.LooseBearingsAHA>()
                ||
                projectileType ==
                ModContent.ProjectileType<
                    Projectiles.LooseBearingsAL>()
                ||
                projectileType ==
                ModContent.ProjectileType<
                    Projectiles.LooseBearingsAMI>()
                ||
                projectileType ==
                ModContent.ProjectileType<
                    Projectiles.LooseBearingsACH>();
        }
    }
}