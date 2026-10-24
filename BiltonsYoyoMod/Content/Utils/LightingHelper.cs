using Microsoft.Xna.Framework;
using Terraria;

namespace BiltonsYoyoMod.Content.Utils
{
    // Fix #1: the arena (and other Guardian-related light sources) used to light every point
    // indiscriminately, including points that fall inside solid tiles. SafeAddLight checks tile
    // solidity first (ignoring actuated/deactivated tiles, which shouldn't block light) and simply
    // skips lighting that point instead of poking light through walls.
    public static class LightingHelper
    {
        public static void SafeAddLight(Vector2 worldPos, float r, float g, float b)
        {
            int tileX = (int)(worldPos.X / 16f);
            int tileY = (int)(worldPos.Y / 16f);

            if (tileX < 0 || tileY < 0 || tileX >= Main.maxTilesX || tileY >= Main.maxTilesY)
                return;

            Tile tile = Main.tile[tileX, tileY];
            if (tile != null && tile.HasTile && Main.tileSolid[tile.TileType] && !tile.IsActuated)
                return; // inside a solid, non-actuated tile — don't place light here

            Lighting.AddLight(worldPos, r, g, b);
        }
    }
}
