using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class ElevatorHitGlobalProjectile
        : GlobalProjectile
    {
        public override bool InstancePerEntity =>
            false;

        public override void OnHitNPC(
            Projectile projectile,
            NPC target,
            NPC.HitInfo hit,
            int damageDone
        )
        {
            if (!YoyoTrickSystem.IsYoyoProjectile(
                    projectile))
            {
                return;
            }

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return;
            }

            Player player =
                Main.player[
                    projectile.owner
                ];

            if (player == null ||
                !player.active)
            {
                return;
            }

            ElevatorPlayer elevator =
                player.GetModPlayer<
                    ElevatorPlayer
                >();

            if (!elevator.Active)
                return;

            if (!elevator.IsPhaseYoyo(
                    projectile))
            {
                return;
            }

            elevator.TriggerAfterEffect(
                projectile,
                projectile.Center
            );
        }
    }
}