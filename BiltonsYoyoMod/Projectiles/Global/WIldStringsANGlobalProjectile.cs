using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class WildStringsANGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity =>
            true;

        public override void ModifyHitNPC(
            Projectile projectile,
            NPC target,
            ref NPC.HitModifiers modifiers)
        {
            // Only yoyo projectiles.
            if (projectile.aiStyle != ProjAIStyleID.Yoyo)
                return;

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return;
            }

            Player player =
                Main.player[projectile.owner];

            if (!player.active)
                return;

            WildStringsPlayer modPlayer =
                player.GetModPlayer<WildStringsPlayer>();

            if (!modPlayer.Nylon)
                return;

            /*
             * +10 armor penetration belongs to the yoyo projectile
             * itself while Nylon is equipped.
             */
            modifiers.ArmorPenetration += 10f;
        }
    }
}