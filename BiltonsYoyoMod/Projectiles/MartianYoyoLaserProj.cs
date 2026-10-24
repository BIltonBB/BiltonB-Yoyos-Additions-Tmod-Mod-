using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Saucer laser: fired at the target position recorded AT FIRE TIME (no tracking after),
    /// fast (speed 16), green beam dust + strong light, dies with a zap burst.
    /// </summary>
    public class MartianYoyoLaserProj : ModProjectile, IYoyoInstantHit
    {
        private readonly List<Vector2> ribbonTrail = new List<Vector2>();
        private bool spawnFxDone;

        // M10: saucer lift so bolts read against the night sky.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(200, 255, 225), 0.3f) * Projectile.Opacity;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 3;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 120;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            // Spawn flash: tight green ignition (was 10 random).
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                Lighting.AddLight(Projectile.Center, 0.7f, 1.4f, 0.6f);
                for (int i = 0; i < 5; i++)
                {
                    float angle = MathHelper.TwoPi * i / 5f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, angle.ToRotationVector2() * 2.8f, 100, default, 1f);
                    dust.noGravity = true;
                }
                // M11: nose streak thrown ahead along the recorded bearing.
                Dust nose = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, Projectile.velocity * 0.35f, 100, default, 1f);
                nose.noGravity = true;
            }

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation();

            MiniYoyoFx.RibbonPush(ribbonTrail, Projectile.Center);

            Lighting.AddLight(Projectile.Center, 0.4f, 1.0f, 0.4f);

            // Lean beam trail: one directional spark every other tick (was 1.5/tick x50).
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, -Projectile.velocity * 0.08f, 120, default, 0.75f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            MiniYoyoFx.RibbonDrawSoft(ribbonTrail, new Color(60, 255, 150), new Color(220, 255, 230), 5f);
            float flicker = 1f + (float)System.Math.Sin(Main.GameUpdateCount * 0.45f + Projectile.whoAmI) * 0.1f;
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f * flicker, new Color(120, 255, 170), 0.5f);
            // M12: tail knot + head ticks so the 16spd bolts streak.
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 9f, 11f, new Color(80, 220, 130), 0.35f);
                Vector2 head = Projectile.Center - Main.screenPosition;
                Vector2 tail = Projectile.Center - back * 12f - Main.screenPosition;
                MiniYoyoFx.DrawLine(head, tail, 1.5f, new Color(200, 255, 225, 140) * 0.55f);
            }
            return true;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, angle.ToRotationVector2() * 2.8f, 100, default, 0.9f);
                dust.noGravity = true;
            }

            // Tight zap ring (was 8 random).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, angle.ToRotationVector2() * 3f, 100, default, 0.9f);
                dust.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.7f, 1.3f, 0.6f);

            // Synced vanilla slash sparkles over the zap (free MP sync).
            Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = Projectile.Center }, Projectile.owner);
            YoyoParticles.SpawnBloom(Projectile.Center, 5f, 20f, 20, new Color(150, 255, 190), 0.55f);
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.4f }, Projectile.Center);
        }
    }
}
