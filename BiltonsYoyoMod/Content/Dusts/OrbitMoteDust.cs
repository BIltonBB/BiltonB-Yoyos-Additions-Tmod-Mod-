using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Dusts
{
    // Orbit mote: circles a world point stored in dust.customData (Vector2),
    // tightening as it shrinks. Used for ritual kindles and charge inha­les.
    public class OrbitMoteDust : ModDust
    {
        public override string Texture => null; // vanilla mote sheet

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.frame = new Rectangle(0, 10, 8, 8);
            dust.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (dust.scale < 0.6f)
                dust.scale = 0.6f;
        }

        public override bool Update(Dust dust)
        {
            if (dust.customData is Vector2 center)
            {
                dust.rotation += 0.18f;
                float radius = dust.scale * 34f;
                dust.position = center + dust.rotation.ToRotationVector2() * radius - new Vector2(4f, 4f);
            }
            dust.scale -= 0.02f;
            if (dust.scale < 0.25f)
                dust.active = false;
            return false;
        }
    }
}
