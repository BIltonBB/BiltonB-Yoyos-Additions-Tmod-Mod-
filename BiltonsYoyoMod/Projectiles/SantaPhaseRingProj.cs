using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Visual-only phase-change ring for the Santa yoyo (no damage, no collision).
    // ai0: 0 = bullet phase (candy red), 1 = missile phase (pine green).
    public class SantaPhaseRingProj : ModProjectile
    {
        private const int Lifetime = 26;

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
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color ringColor = Projectile.ai[0] >= 1f ? new Color(90, 220, 130) : new Color(255, 110, 110);
            Color hot = Color.Lerp(ringColor, Color.White, 0.55f);
            float progress = 1f - Projectile.timeLeft / (float)Lifetime;
            float alpha = 1f - progress;
            float elapsed = progress * Lifetime;

            // This used to open with DrawGlow(radius * 2f): a 250px flat disc of
            // light sitting behind the ring at 0.25 alpha, which is exactly the
            // "large area of background lights up" read. It also stamped a
            // 32-segment ring, which goes visibly angular at radius 120. A phase
            // change is a WAVE, so it is drawn as one now: a travelling rim, a
            // small core that decays instead of a big disc that grows, and motes
            // riding the front.
            MiniYoyoFx.DrawShockwaveRing(Projectile.Center, 14f, 128f, Lifetime, elapsed, ringColor * 0.85f, 5f);
            MiniYoyoFx.DrawShockwaveRing(Projectile.Center, 8f, 100f, Lifetime, elapsed, hot * 0.55f, 2f);

            // Core bloom: shrinks as it fades, so nothing large is ever lit flat.
            MiniYoyoFx.DrawGlow(Projectile.Center, 28f * alpha + 10f, ringColor, 0.42f * alpha * alpha);

            float radius = 14f + progress * 110f;
            for (int i = 0; i < 10; i++)
            {
                float ang = progress * 4f + MathHelper.TwoPi * i / 10f;
                Vector2 at = Projectile.Center + ang.ToRotationVector2() * radius;
                MiniYoyoFx.DrawGlow(at, 11f * alpha + 3f, i % 2 == 0 ? hot : ringColor, 0.5f * alpha);
                MiniYoyoFx.DrawSoftRay(at, ang, 16f * alpha + 4f, 5f, ringColor, 0.32f * alpha);
            }
            return false;
        }
    }
}
