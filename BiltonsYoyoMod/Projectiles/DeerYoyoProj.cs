using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class DeerYoyoProj : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            // Yoyo properties
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 10f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 200f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 5f;
        }

        // This triggers *every time the yoyo hits an enemy*
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.myPlayer == Projectile.owner)
            {
                // Spawn a shockwave at the enemy
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    target.Center,
                    Vector2.Zero, // stationary
                    ModContent.ProjectileType<DeerYoyoSWProj>(), // your shockwave projectile
                    Projectile.damage / 2, // optional: weaker than yoyo
                    2f, // knockback
                    Projectile.owner
                );
            }

            // Apply fire debuff for 3 seconds
            target.AddBuff(BuffID.Frostburn, 180);
        }
    }
}