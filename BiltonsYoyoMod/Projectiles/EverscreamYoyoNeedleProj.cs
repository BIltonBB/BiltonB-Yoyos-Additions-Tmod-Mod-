using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class EverscreamYoyoNeedleProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;
        private bool apexFlashed; // stall flash at the top of the arc (visual only)

        // Pine streak ribbon (a TrailProfile for this type was missing -> added).
        private readonly List<Vector2> wake = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 300;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M10: grove lift that respects the pre-death fade.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(190, 240, 190), 0.3f) * Projectile.Opacity;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 5; i++)
                {
                    float angle = MathHelper.TwoPi * i / 5f;
                    int dustType = i % 2 == 0 ? DustID.GreenTorch : DustID.Torch;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, dustType, angle.ToRotationVector2() * 2f, 0, default, 0.9f);
                    dust.noGravity = true;
                }
                // M11: nose streak thrown ahead along the volley bearing (16spd hose).
                Dust nose = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, Projectile.velocity * 0.4f, 0, default, 0.9f);
                nose.noGravity = true;
                Lighting.AddLight(Projectile.Center, 0.6f, 0.9f, 0.4f);
            }

            Projectile.velocity.Y += 0.15f;
            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;

            // MAJOR B (apex half): stall flash where the climb dies into the fall.
            if (!apexFlashed && Projectile.velocity.Y >= 0f)
            {
                apexFlashed = true;
                for (int i = 0; i < 3; i++)
                {
                    float angle = MathHelper.TwoPi * i / 3f;
                    Dust stall = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, angle.ToRotationVector2() * 1.8f, 0, default, 0.9f);
                    stall.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.7f, 1f, 0.5f);
            }

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            MiniYoyoFx.RibbonPush(wake, Projectile.Center, 9f, 7);

            if (Projectile.timeLeft < 60)
            {
                Projectile.alpha += 4;
                if (Projectile.alpha > 255)
                    Projectile.alpha = 255;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.85f, 0.35f);

            // Lean pine-needle trail: green lead every 3rd tick + rare ember.
            // Downwind drift: shed motes lean +X so volleys breathe sideways.
            Vector2 pineWind = new Vector2(0.8f, 0f);
            if (Projectile.timeLeft % 3 == 0)
            {
                Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, -Projectile.velocity * 0.06f + pineWind, 0, default, 0.8f);
                trail.noGravity = true;
            }
            if (Main.rand.NextBool(8))
            {
                Dust ember = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, -Projectile.velocity * 0.04f + pineWind, 0, default, 0.7f);
                ember.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // M12: pine-needle comet — head + tail bead + speed-line (1 draw keeps
            // the 10/s hose cheap where a ribbon would cost 24 draws per needle).
            // Streak upgrade: longer speed-line + brighter head for fast needles.
            float spd = Projectile.velocity.Length();
            MiniYoyoFx.DrawGlow(Projectile.Center, 16f, new Color(120, 220, 130), 0.45f * Projectile.Opacity);
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 8f, 10f, new Color(90, 180, 100), 0.3f * Projectile.Opacity);
                MiniYoyoFx.DrawGlow(Projectile.Center, 8f + spd * 0.4f, new Color(200, 255, 200), 0.35f * Projectile.Opacity);
                Vector2 head = Projectile.Center - Main.screenPosition;
                Vector2 tail = Projectile.Center - back * (12f + spd * 0.8f) - Main.screenPosition;
                // Straight speed-line only until the ribbon has a path to ride.
                if (wake.Count < 2)
                    MiniYoyoFx.DrawLine(head, tail, 2f, new Color(190, 250, 190, 140) * 0.6f * Projectile.Opacity);
            }
            // Wired pine streak: real ribbon replaces the straight line (short
            // 7-point tail keeps the 10/s hose cheap; VertexStrip = one draw).
            if (wake.Count >= 2)
                MiniYoyoFx.RibbonDrawSoft(wake, Projectile.type);
            return true;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, angle.ToRotationVector2() * 2.2f, 0, default, 0.9f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 3; i++)
            {
                float angle = MathHelper.TwoPi * i / 3f + MathHelper.PiOver4;
                Dust pine = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, angle.ToRotationVector2() * 1.8f, 0, default, 0.8f);
                pine.noGravity = true;
            }
            // MAJOR B (burst half): snow crowns the pine burst.
            for (int i = 0; i < 3; i++)
            {
                Dust snow = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.WhiteTorch, new Vector2(0f, -1f), 0, default, 0.9f);
                snow.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.55f, 0.15f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 90);
        }
    }
}
