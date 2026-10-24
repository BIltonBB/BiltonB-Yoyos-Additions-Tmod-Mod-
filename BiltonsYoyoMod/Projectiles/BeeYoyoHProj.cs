using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class BeeYoyoHProj : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 10;                 // Hitbox width
            Projectile.height = 10;                // Hitbox height

            Projectile.friendly = true;            // Can hit enemies
            Projectile.hostile = false;            // Not hostile

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.aiStyle = ProjAIStyleID.Arrow; // Arrow behavior

            Projectile.penetrate = 1;              // Hits one enemy
            Projectile.timeLeft = 360;             // 5 seconds active + 1 second fade = 360 ticks (6s total)
        }

        public override void AI()
        {
            // Apply half gravity manually
            Projectile.velocity.Y += 0.1f; // default arrow ~0.2f, so half gravity

            // After 5 seconds (60 ticks / 1 second remaining), smoothly fade out over 1 second
            if (Projectile.timeLeft <= 60)
            {
                float fadeProgress = 1f - ((float)Projectile.timeLeft / 60f);
                Projectile.alpha = (int)MathHelper.Lerp(0f, 255f, fadeProgress);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Poisoned, 300); // 5 seconds
        }
    }
}
