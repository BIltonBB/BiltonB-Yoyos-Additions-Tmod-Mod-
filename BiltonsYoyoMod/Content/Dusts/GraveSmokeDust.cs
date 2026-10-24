using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Dusts
{
    // Grave smoke: heavy, slow, swells then dissipates. Used for muzzles,
    // breaches and missile pops (Dutchman broadsides, Santa crackers, Ogre).
    public class GraveSmokeDust : ModDust
    {
        public override string Texture => null; // vanilla mote sheet

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.frame = new Rectangle(0, 20, 8, 8);
            dust.velocity *= 0.4f;
            dust.alpha = 120;
            if (dust.scale < 0.8f)
                dust.scale = 0.8f;
        }

        public override bool Update(Dust dust)
        {
            dust.velocity *= 0.94f;
            dust.velocity.Y -= 0.015f;
            dust.scale += 0.02f;
            dust.alpha += 4;
            if (dust.alpha >= 220 || dust.scale > 2.2f)
                dust.active = false;
            return false;
        }
    }
}
