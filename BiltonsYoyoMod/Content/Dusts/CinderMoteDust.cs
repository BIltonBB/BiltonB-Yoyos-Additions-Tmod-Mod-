using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Dusts
{
    // Hot cinder: pops upward fast, drags hard, winks out.
    // Used for Betsy fire-drop wakes and scorch puffs.
    public class CinderMoteDust : ModDust
    {
        public override string Texture => null; // vanilla mote sheet

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.frame = new Rectangle(0, 0, 8, 8);
            dust.velocity.Y = MathHelper.Min(dust.velocity.Y, -1f);
            if (dust.scale < 0.6f)
                dust.scale = 0.6f;
        }

        public override bool Update(Dust dust)
        {
            dust.velocity *= 0.94f;
            dust.scale -= 0.035f;
            dust.rotation -= 0.12f;
            if (dust.scale < 0.25f)
                dust.active = false;
            return false;
        }
    }
}
