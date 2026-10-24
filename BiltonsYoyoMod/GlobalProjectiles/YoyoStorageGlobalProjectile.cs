using BiltonsYoyoMod.Players;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.GlobalProjectiles
{
    public class YoyoStorageGlobalProjectile : GlobalProjectile
    {
        public override void AI(Projectile projectile)
        {
            if (projectile.aiStyle == ProjAIStyleID.Yoyo && projectile.owner >= 0 && projectile.owner < Main.maxPlayers && Main.player[projectile.owner].GetModPlayer<YoyoStoragePlayer>().YoyosPassThroughBlocks)
                projectile.tileCollide = false;
        }
    }
}
