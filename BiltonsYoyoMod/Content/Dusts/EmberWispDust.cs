using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Dusts
{
    // Rising heart-ember: accelerates upward, flickers, shrinks out.
    // Used for furnace breaths (Mourning exhale, Betsy volleys, Torch muzzles).
    public class EmberWispDust : ModDust
    {
        public override string Texture => null; // vanilla mote sheet

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.frame = new Rectangle(0, 0, 8, 8);
            dust.velocity.Y = MathHelper.Min(dust.velocity.Y, -0.5f);
            if (dust.scale < 0.6f)
                dust.scale = 0.6f;
        }

        public override bool Update(Dust dust)
        {
            dust.velocity *= 0.97f;
            dust.velocity.Y -= 0.03f;
            dust.scale -= 0.025f;
            dust.rotation += 0.1f;
            if (dust.scale < 0.25f)
                dust.active = false;
            return false;
        }
    }
}
