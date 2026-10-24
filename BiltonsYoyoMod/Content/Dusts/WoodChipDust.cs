using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Dusts
{
    // Splintered wood chunk: gravity-heavy, bounces off tiles, fades out.
    // Used for Ogre club impacts (Spirit ClubDust-style, original art).
    public class WoodChipDust : ModDust
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
            return new Color(200, 150, 100, 160);
        }

        public override bool Update(Dust dust)
        {
            dust.velocity.Y += 0.25f;
            dust.rotation += 0.2f;
            dust.scale -= 0.02f;
            if (dust.scale < 0.25f)
                dust.active = false;
            return false;
        }
    }
}
