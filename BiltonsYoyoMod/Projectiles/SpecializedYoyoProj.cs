using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class SpecializedYoyoProj : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.penetrate = 15;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 30f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 220f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 13f;
        }

        public override void ModifyHitNPC(
            NPC target,
            ref NPC.HitModifiers modifiers)
        {
            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
                return;

            Player owner = Main.player[Projectile.owner];

            if (owner == null || !owner.active)
                return;

            // Knockback blasts away from the player at 15 (never drags
            // hitboxes toward anyone).
            modifiers.HitDirectionOverride =
                target.Center.X < owner.Center.X ? -1 : 1;
            modifiers.Knockback.Base = 15f;
        }
    }
}