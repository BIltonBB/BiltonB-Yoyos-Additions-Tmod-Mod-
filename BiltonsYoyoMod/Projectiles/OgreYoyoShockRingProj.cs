using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Expanding shockwave ring for the Ogre yoyo. Carries the eruption damage
    // (damage/2, 6f knockback at spawn) through the standard combat pipeline so
    // hits feel like normal yoyo hits — defense, crits, bonuses and MP sync all
    // apply. Hitbox grows with the visual ring; per-enemy local immunity longer
    // than the 24-tick life keeps it to one hit per enemy, as before.
    public class OgreYoyoShockRingProj : ModProjectile
    {
        private const int Lifetime = 24;
        private bool spawnDone;

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

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;

            // Hitbox tracks the visual ring (20 -> 160px radius, same formula as PreDraw).
            float progress = 1f - Projectile.timeLeft / (float)Lifetime;
            float radius = 20f + progress * 140f;
            int d = (int)(radius * 2f);
            Vector2 c = Projectile.Center;
            Projectile.width = d;
            Projectile.height = d;
            Projectile.position = c - new Vector2(d / 2f, d / 2f);

            // M15: eruption dust ring on the first tick (all clients; visual only).
            if (!spawnDone)
            {
                spawnDone = true;
                // Code-drawn eruption burst (a screen shader lived here — it broke
                // packaging, so the drama is pure particles now, zero load risk).
                YoyoParticles.SpawnBurst(Projectile.Center, 12, 7f, new Color(170, 240, 170), 1.1f, 26);
                YoyoParticles.SpawnBloom(Projectile.Center, 12f, 70f, 26, new Color(150, 235, 150), 0.55f);
                YoyoParticles.SpawnBloom(Projectile.Center, 12f, 70f, 26, new Color(150, 235, 150), 0.55f);
                for (int i = 0; i < 8; i++)
                {
                    float angle = MathHelper.TwoPi * i / 8f;
                    Vector2 vel = angle.ToRotationVector2() * 6f;
                    vel.Y *= 0.4f;
                    Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, vel, 120, default, 1.1f);
                    ring.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.4f, 1f, 0.4f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // MAJOR C: ground-pound choreography — outer crisp ring + inner hot ring +
            // ground-hugging ellipse + orbiting clods + rising power column, all on one
            // progress value so the eruption expands, cools, and settles as one event.
            float progress = 1f - Projectile.timeLeft / (float)Lifetime;
            float radius = 20f + progress * 140f;
            float alpha = 1f - progress;

            // M14: inner hot ring under the outer crisp one.
            // Was DrawGlow(Projectile.Center, radius * 2f, ..., 0.25f * alpha):
            // a 320px flat disc. See DutchmanBlastRingProj for the full note -
            // a fixed-size mega disc lights up a screen region instead of
            // reading as a shockwave. The core shrinks as the ring expands.
            MiniYoyoFx.DrawGlow(Projectile.Center, 48f * alpha + 12f, new Color(160, 240, 160), 0.42f * alpha);
            MiniYoyoFx.DrawRing(Projectile.Center, radius, 10f * (1f - progress) + 2f, new Color(150, 235, 150) * alpha, 32);
            MiniYoyoFx.DrawRing(Projectile.Center, radius * 0.6f, 6f * (1f - progress) + 1.5f, new Color(200, 255, 180) * alpha, 24);

            // Ground-hugging ellipse: wide soft glow squashed onto the floor line.
            Vector2 ground = Projectile.Center + new Vector2(0f, 10f);
            for (int i = -2; i <= 2; i++)
            {
                Vector2 puff = ground + new Vector2(i * radius * 0.3f, 0f);
                MiniYoyoFx.DrawGlow(puff, 30f * (1f - progress) + 8f, new Color(110, 200, 110), 0.18f * alpha);
            }

            // Orbiting clods riding the front.
            for (int i = 0; i < 10; i++)
            {
                float ang = progress * 3f + MathHelper.TwoPi * i / 10f;
                Vector2 clodPos = Projectile.Center + ang.ToRotationVector2() * radius * 0.95f + new Vector2(0f, -progress * 14f);
                MiniYoyoFx.DrawGlow(clodPos, 9f, new Color(150, 235, 150), 0.4f * alpha);
            }

            // Rising power column: two stacked glows venting upward as it erupts.
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, -20f), 30f * (1f - progress) + 6f, new Color(150, 235, 150), 0.22f * alpha);
            MiniYoyoFx.DrawGlow(Projectile.Center + new Vector2(0f, -40f), 20f * (1f - progress) + 4f, new Color(180, 255, 180), 0.15f * alpha);
            return false;
        }
    }
}
