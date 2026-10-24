using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Visual-only destruction ring for the Dutchman yoyo hull breach.
    // Damage (none) and the forced return are handled by DutchmanYoyoProj.
    public class DutchmanBlastRingProj : ModProjectile
    {
        private const int Lifetime = 28;

        public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = Lifetime;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            Projectile.damage = 0;
            // Breach bloom on the first tick (all clients; visual only).
            if (Projectile.timeLeft == Lifetime)
            {
                YoyoParticles.SpawnBloom(Projectile.Center, 10f, 60f, 26, new Color(255, 190, 130), 0.5f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // MAJOR C (blast half): layered hull-breach shockwave.
            // Outer crisp ring + inner hot ring + 8 orbiting ember shards + hot core,
            // all driven by one progress value so the burst expands and cools as one.
            float progress = 1f - Projectile.timeLeft / (float)Lifetime;
            float radius = 16f + progress * 150f;
            float alpha = 1f - progress;

            MiniYoyoFx.DrawGlow(Projectile.Center, 30f * (1f - progress) + 10f, new Color(255, 220, 170), 0.5f * alpha);
            // Was DrawGlow(Projectile.Center, radius * 2f, ..., 0.25f * alpha):
            // at full expansion that is a 332px flat disc. A huge soft disc
            // does not read as a blast ring - it reads as a lit REGION of the
            // screen, which is the "random rectangle that lights up the
            // background in a large area" family. A blast core must SHRINK as
            // the wave expands and fades, never hold a fixed 300px footprint.
            MiniYoyoFx.DrawGlow(Projectile.Center, 46f * alpha + 12f, new Color(255, 215, 155), 0.44f * alpha);
            MiniYoyoFx.DrawRing(Projectile.Center, radius, 10f * (1f - progress) + 2f, new Color(255, 200, 130) * alpha, 32);
            MiniYoyoFx.DrawRing(Projectile.Center, radius * 0.6f, 6f * (1f - progress) + 1.5f, new Color(255, 150, 90) * alpha, 24);
            for (int i = 0; i < 8; i++)
            {
                float ang = progress * 4f + MathHelper.TwoPi * i / 8f;
                Vector2 shardPos = Projectile.Center + ang.ToRotationVector2() * radius * 0.9f;
                MiniYoyoFx.DrawGlow(shardPos, 10f, new Color(255, 180, 110), 0.5f * alpha);
            }
            return false;
        }
    }
}
