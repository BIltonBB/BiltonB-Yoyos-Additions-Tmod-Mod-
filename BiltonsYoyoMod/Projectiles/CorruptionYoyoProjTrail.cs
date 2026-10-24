using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class CorruptionYoyoProjTrail : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120; // 2 seconds (120 ticks)
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.alpha = 100; // Starts mostly visible
        }

        public override void AI()
        {
            // Smoothly fade transparency from initial alpha (100) to fully transparent (255) over 2 seconds
            float fadeProgress = 1f - ((float)Projectile.timeLeft / 120f);
            Projectile.alpha = (int)MathHelper.Lerp(100f, 255f, fadeProgress);

            // Emit subtle purple light that fades alongside the trail
            float lightMult = (float)Projectile.timeLeft / 120f;
            Lighting.AddLight(Projectile.Center, 0.5f * lightMult, 0.1f * lightMult, 0.5f * lightMult);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.CursedInferno, 180); // 3 seconds debuff
        }
    }
}
