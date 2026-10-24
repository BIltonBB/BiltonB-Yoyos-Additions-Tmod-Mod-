using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Dusts
{
    // Burning ember chunk: drifts up, flickers bright, burns out fast.
    // Used for Betsy fire hits and scorch pops (original art).
    public class EmberChunkDust : ModDust
    {
        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.frame = new Rectangle(0, 0, 10, 12);
            dust.velocity.Y = MathHelper.Min(dust.velocity.Y, -0.5f);
            if (dust.scale < 0.7f)
                dust.scale = 0.7f;
        }

        public override Color? GetAlpha(Dust dust, Color lightColor)
        {
            float flicker = 0.75f + 0.25f * (float)System.Math.Sin(Main.GameUpdateCount * 0.5f + dust.position.X);
            return new Color(255, 170, 90) * flicker;
        }

        public override bool Update(Dust dust)
        {
            dust.velocity *= 0.96f;
            dust.velocity.Y -= 0.02f;
            dust.scale -= 0.04f;
            dust.rotation += 0.1f;
            if (dust.scale < 0.25f)
                dust.active = false;
            return false;
        }
    }
}
