using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class SantaYoyoBulletProj : ModProjectile, IYoyoInstantHit
    {
        private readonly List<Vector2> ribbonTrail = new List<Vector2>();
        private bool spawnFxDone;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 3;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 200;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M10: festive lift tinted toward the tracer's own tip color.
        public override Color? GetAlpha(Color lightColor)
        {
            int tip = Projectile.whoAmI % 3;
            Color tipTint = tip == 0 ? new Color(255, 170, 160) : tip == 1 ? new Color(170, 255, 190) : new Color(255, 230, 180);
            return Color.Lerp(lightColor, tipTint, 0.25f) * Projectile.Opacity;
        }

        public override void AI()
        {
            // Christmas-light tip by slot: red / green / gold. Deterministic
            // across clients (whoAmI syncs), no flag channel needed.
            int tip = Projectile.whoAmI % 3;
            int tipDust = tip == 0 ? DustID.RedTorch : tip == 1 ? DustID.GreenTorch : DustID.GoldFlame;
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Dust fx = Dust.NewDustPerfect(Projectile.Center, tipDust, angle.ToRotationVector2() * 2.2f, 0, default, 0.9f);
                    fx.noGravity = true;
                }
                Dust core = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, Vector2.Zero, 0, default, 1f);
                core.noGravity = true;
                // M11 (nose half): streak thrown ahead along the firing bearing.
                Dust nose = Dust.NewDustPerfect(Projectile.Center, tipDust, Projectile.velocity * 0.4f, 0, default, 0.9f);
                nose.noGravity = true;
                Lighting.AddLight(Projectile.Center, 1f, 0.4f, 0.4f);
            }

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation();

            MiniYoyoFx.RibbonPush(ribbonTrail, Projectile.Center);

            Lighting.AddLight(Projectile.Center, 0.8f, 0.25f, 0.25f);

            // Lean tracer in its tip color: one directional mote every 3rd tick.
            if (Projectile.timeLeft % 3 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, tipDust, -Projectile.velocity * 0.08f, 0, default, 0.75f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Ribbon + head in the tracer's tip color.
            int tip = Projectile.whoAmI % 3;
            Color edge = tip == 0 ? new Color(220, 60, 50) : tip == 1 ? new Color(70, 200, 110) : new Color(230, 170, 60);
            Color core = tip == 0 ? new Color(255, 200, 180) : tip == 1 ? new Color(200, 255, 210) : new Color(255, 235, 190);
            MiniYoyoFx.RibbonDrawSoft(ribbonTrail, edge, core, 5f);
            Vector2 drawPoint = Projectile.Center + new Vector2(0f, Projectile.gfxOffY);
            MiniYoyoFx.DrawGlow(drawPoint, 16f, core, 0.45f);
            // M11 (knot half): tail bead so long tracers read as comets.
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(drawPoint - back * 9f, 10f, edge, 0.3f);
            }
            return true;
        }

        public override void OnKill(int timeLeft)
        {
            int tip = Projectile.whoAmI % 3;
            int tipDust = tip == 0 ? DustID.RedTorch : tip == 1 ? DustID.GreenTorch : DustID.GoldFlame;
            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, i == 0 ? DustID.GoldFlame : tipDust, angle.ToRotationVector2() * 2.4f, 0, default, 0.9f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 3; i++)
            {
                int sparkleType = i % 2 == 0 ? tipDust : DustID.Confetti;
                Dust sparkle = Dust.NewDustPerfect(Projectile.Center, sparkleType, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1f);
                sparkle.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.5f, 0.5f);
        }
    }
}
