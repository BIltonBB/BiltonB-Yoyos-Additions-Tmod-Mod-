using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Dusts
{
    // Blood chunk: heavy gore that falls, sticks where it lands
    // (Overhaul GoreStay-style), then soaks away. Tinted per victim.
    public class BloodChunkDust : ModDust
    {
        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = false;
            dust.frame = new Rectangle(0, 0, 10, 12);
            dust.customData = 0f;
            if (dust.scale < 0.7f)
                dust.scale = 0.7f;
        }

        public override Color? GetAlpha(Dust dust, Color lightColor)
        {
            Color base_ = dust.color.A == 0 && dust.color.R == 0 && dust.color.G == 0 && dust.color.B == 0
                ? new Color(210, 70, 80)
                : dust.color;
            return new Color(base_.R, base_.G, base_.B, 170);
        }

        public override bool Update(Dust dust)
        {
            int tx = (int)(dust.position.X / 16);
            int ty = (int)((dust.position.Y + 6) / 16);
            bool grounded = tx >= 0 && ty >= 0 && tx < Main.maxTilesX && ty < Main.maxTilesY
                && Main.tile[tx, ty].HasTile && Main.tile[tx, ty].HasUnactuatedTile;
            if (grounded)
            {
                dust.velocity = Vector2.Zero;
                dust.customData = (dust.customData is float t ? t : 0f) + 1f;
                dust.scale -= 0.004f;
            }
            else
            {
                dust.velocity.Y += 0.3f;
                dust.position += dust.velocity;
                dust.rotation += 0.2f;
            }
            if (dust.scale < 0.25f || (dust.customData is float t2 && t2 > 600))
                dust.active = false;
            return false;
        }
    }
}
