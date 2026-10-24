using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Dusts
{
    // Ice crystal shard: falls, bounces off solid tiles, shrinks out.
    // Used for IceQueen hits and bursts (Spirit CryoDust-style, original art).
    public class IceCrystalDust : ModDust
    {
        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = false;
            dust.frame = new Rectangle(0, 0, 10, 12);
            if (dust.scale < 0.7f)
                dust.scale = 0.7f;
        }

        public override Color? GetAlpha(Dust dust, Color lightColor)
        {
            return new Color(220, 240, 255, 120);
        }

        public override bool Update(Dust dust)
        {
            dust.position += dust.velocity;
            dust.velocity.Y += 0.2f;
            int tx = (int)dust.position.X / 16;
            int ty = (int)dust.position.Y / 16;
            if (tx >= 0 && ty >= 0 && tx < Main.maxTilesX && ty < Main.maxTilesY)
            {
                if (Main.tile[tx, ty].HasTile && Main.tile[tx, ty].HasUnactuatedTile)
                    dust.velocity *= -0.5f;
            }
            dust.rotation = dust.velocity.ToRotation();
            dust.scale *= 0.985f;
            if (dust.scale < 0.2f)
                dust.active = false;
            return false;
        }
    }
}
