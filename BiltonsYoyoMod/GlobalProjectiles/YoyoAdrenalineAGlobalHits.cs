using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.GlobalProjectiles
{
    public class YoyoAdrenalineAGlobalHits : GlobalProjectile
    {
        public override bool InstancePerEntity => false;

        public override void ModifyHitNPC(
            Projectile projectile,
            NPC target,
            ref NPC.HitModifiers modifiers
        )
        {
            // Only yoyos
            bool isYoyo =
                projectile.aiStyle == ProjAIStyleID.Yoyo &&
                ProjectileID.Sets.YoyosLifeTimeMultiplier[projectile.type] != 0f;

            if (!isYoyo)
                return;

            Player player = Main.player[projectile.owner];
            if (!player.active)
                return;

            var mp = player.GetModPlayer<Players.YoyoAdrenalineAPlayer>();
            if (!mp.enabled)
                return;

            // Fill bar by 5%
            mp.AddCharge(0.05f);

            // Apply 5x ONLY to empowered hit
            if (mp.empoweredHit) // must be public
            {
                modifiers.SourceDamage *= 5f;
                mp.empoweredHit = false; // reset immediately
            }
        }
    }
}