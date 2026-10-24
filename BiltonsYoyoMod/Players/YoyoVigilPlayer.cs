using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Players
{
    // Death vigil: dying while holding a vibe yoyo leaves it spinning slow
    // and dim over the corpse until it fades. Vibe roster in YoyoRoster.
    public class YoyoVigilPlayer : ModPlayer
    {
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            if (Main.myPlayer != Player.whoAmI)
                return;
            if (Player.HeldItem == null)
                return;
            int shot = Player.HeldItem.shoot;
            if (shot <= 0 || !Projectiles.YoyoRoster.HasVibe(shot))
                return;
            Color tint = Color.White;
            if (Projectiles.YoyoThrowMarquee.YoyoMarqueeNames(shot, out _, out Color named))
                tint = named;
            int packed = (tint.R << 16) | (tint.G << 8) | tint.B;
            int idx = Projectile.NewProjectile(Player.GetSource_Death(), Player.Center, Vector2.Zero,
                ModContent.ProjectileType<Projectiles.YoyoVigilProj>(), 0, 0f, Player.whoAmI, packed, 0f);
            if (idx >= 0 && idx < Main.maxProjectiles)
                Main.projectile[idx].netUpdate = true;
        }
    }
}
