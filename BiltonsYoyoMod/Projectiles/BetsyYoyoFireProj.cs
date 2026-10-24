using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Falling Betsy flame dropped during FireDrop. Accelerates downward, inflicts OnFire!
    /// for 180 ticks, fades out over its 200-tick life.
    /// </summary>
    public class BetsyYoyoFireProj : ModProjectile, IYoyoInstantHit
    {
        private const int Lifetime = 200;
        private bool spawnFxDone;
        private readonly List<Vector2> fallTrail = new List<Vector2>();

        // M10: ember lift that respects the shrink-and-fade lifecycle.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(255, 205, 160), 0.3f) * Projectile.Opacity;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = Lifetime;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            // Spawn flash: tight ignition (was 10 random).
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                Lighting.AddLight(Projectile.Center, 1.3f, 0.6f, 0.2f);
                for (int i = 0; i < 5; i++)
                {
                    float angle = MathHelper.TwoPi * i / 5f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 2.4f, 100, default, 1f);
                    dust.noGravity = true;
                }
                // M11: nose streak falling ahead along the drop bearing.
                Dust nose = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, Projectile.velocity * 0.35f, 100, default, 0.9f);
                nose.noGravity = true;
            }

            Projectile.velocity.Y += 0.08f;
            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Projectile.alpha = (int)(255 * (1f - (float)Projectile.timeLeft / Lifetime));
            Projectile.scale = 0.35f + 0.35f * ((float)Projectile.timeLeft / Lifetime);

            Lighting.AddLight(Projectile.Center, 1.0f, 0.5f, 0.15f);

            // Lean drop trail: one ember every 3rd tick (was 2/tick x ~80 alive).
            if (Projectile.timeLeft % 3 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, new Vector2(0f, -0.8f), 120, default, 0.85f);
                dust.noGravity = true;
                Dust cinder = Dust.NewDustDirect(Projectile.Center, 8, 8, ModContent.DustType<global::BiltonsYoyoMod.Content.Dusts.CinderMoteDust>(), 0f, -1.2f, 120, default, 0.9f);
                cinder.noGravity = true;
            }
            MiniYoyoFx.RibbonPush(fallTrail, Projectile.Center, 8f, 10);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            MiniYoyoFx.RibbonDrawSoft(fallTrail, new Color(180, 80, 30), new Color(255, 210, 140), 5f);
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f, new Color(255, 160, 80), 0.45f);
            // Art-center draw (measured bright-mass centroid 16.5,34.7 of the 34x90
            // canvas — the flame burns top-heavy, so vanilla's canvas-middle pivot
            // left the glow sitting below the visible fire).
            Microsoft.Xna.Framework.Graphics.Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                GetAlpha(lightColor) ?? lightColor, Projectile.rotation, new Vector2(16.5f, 34.7f), Projectile.scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 180);
            YoyoParticles.SpawnBurst(Projectile.Center, 5, 2.5f, new Color(255, 170, 90), 0.9f, 20);

            // Tight cinder ring (was 6 random).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 2.4f, 100, default, 1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1.2f, 0.55f, 0.2f);
        }

        public override void OnKill(int timeLeft)
        {
            // Ground scorch bloom + tight fading puff (was 8 random).
            YoyoParticles.SpawnBloom(Projectile.Center, 4f, 20f, 22, new Color(255, 150, 80), 0.5f);
            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, angle.ToRotationVector2() * 2f, 120, default, 0.9f);
                dust.noGravity = true;
            }
        }
    }
}
