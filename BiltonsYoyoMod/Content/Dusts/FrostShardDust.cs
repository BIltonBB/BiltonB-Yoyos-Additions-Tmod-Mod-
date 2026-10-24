using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Dusts
{
    // Falling ice crystal: gravity-pulled, spins, shatters out.
    // Used for IceQueen shard trails and burst shrapnel.
    public class FrostShardDust : ModDust
    {
        public override string Texture => null; // vanilla mote sheet

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = false;
            dust.frame = new Rectangle(0, 0, 8, 8);
            if (dust.scale < 0.6f)
                dust.scale = 0.6f;
        }

        public override bool Update(Dust dust)
        {
            dust.velocity *= 0.985f;
            dust.scale -= 0.03f;
            dust.rotation += 0.15f;
            if (dust.scale < 0.25f)
                dust.active = false;
            return false;
        }
    }
}
