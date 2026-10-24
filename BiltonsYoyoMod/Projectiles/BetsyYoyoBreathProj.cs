using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Betsy fire breath. Animated strip texture (7 frames of 90x90); vanilla drawing renders
    /// the current frame centered on the projectile. Short-lived (40 ticks), scales up over
    /// life, heavy fire dust + strong light, inflicts OnFire! for 120 ticks.
    /// </summary>
    public class BetsyYoyoBreathProj : ModProjectile, IYoyoInstantHit
    {
        private const int Lifetime = 40;
        private bool spawnFxDone;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 7;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;

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
            // Spawn flash: tight cone ignition (was 12 random).
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                Lighting.AddLight(Projectile.Center, 1.6f, 0.7f, 0.2f);
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 2.6f, 100, default, 1.1f);
                    dust.noGravity = true;
                }
                for (int i = 0; i < 2; i++)
                {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1f), 120, default, 0.9f);
                    dust.noGravity = true;
                }
            }

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % 7;
            }

            Projectile.scale = 0.5f + (1f - (float)Projectile.timeLeft / Lifetime) * 0.4f;

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, 1.4f, 0.6f, 0.2f);

            // Lean cone body: the 7-frame sprite carries the shape; one ember
            // every other tick + rare gold rise (was 4/tick cloud covering it).
            // Scorch drip: falling cinders where the breath will land.
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, -Projectile.velocity * 0.05f, 120, default, 1f);
                dust.noGravity = true;
            }
            if (Projectile.timeLeft % 6 == 0)
            {
                Dust cinder = Dust.NewDustDirect(Projectile.Center + Projectile.velocity, 8, 8, ModContent.DustType<global::BiltonsYoyoMod.Content.Dusts.CinderMoteDust>(), 0f, 1.5f, 120, default, 0.9f);
                cinder.noGravity = false;
            }
            if (Main.rand.NextBool(6))
            {
                Dust rise = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, new Vector2(0f, -1.5f), 120, default, 0.85f);
                rise.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // M14: frame-following seat — swells and pales as the cone matures
            // (frameCounter runs on all clients, so the bloom stays agreed).
            MiniYoyoFx.DrawGlow(Projectile.Center, 16f + Projectile.frame * 2f, new Color(255, 170, 90), 0.35f);
            // M15: cone edge ticks flanking the breath direction.
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 fwd = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 side = new Vector2(-fwd.Y, fwd.X);
                Vector2 base_ = Projectile.Center - Main.screenPosition;
                Color tick = new Color(255, 200, 130, 140) * 0.55f;
                MiniYoyoFx.DrawLine(base_ - side * 8f, base_ - side * 8f + fwd * 20f, 1.5f, tick);
                MiniYoyoFx.DrawLine(base_ + side * 8f, base_ + side * 8f + fwd * 20f, 1.5f, tick);
            }
            // Frame-history ghosts: two past cone poses fading behind the breath.
            if (Projectile.oldPos != null && Projectile.oldPos.Length > 2)
            {
                Microsoft.Xna.Framework.Graphics.Texture2D coneTex = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
                Vector2 halfBox = new Vector2(Projectile.width, Projectile.height) * 0.5f;
                for (int k = 2; k >= 1; k--)
                {
                    if (k >= Projectile.oldPos.Length)
                        continue;
                    Vector2 oldCenter = Projectile.oldPos[k] + halfBox;
                    if (oldCenter.LengthSquared() < 1f)
                        continue;
                    Rectangle coneRect = coneTex.Frame(1, 7, 0, Projectile.frame);
                    Main.EntitySpriteDraw(coneTex, oldCenter - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                        coneRect, lightColor * Projectile.Opacity * 0.22f, Projectile.rotation, coneRect.Size() * 0.5f, Projectile.scale, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
                }
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 120);

            // Tight splash (was 5 random).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 2.4f, 100, default, 1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1.5f, 0.65f, 0.2f);
        }

        public override void OnKill(int timeLeft)
        {
            // Tight final bloom where the cone dies (was 14).
            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, angle.ToRotationVector2() * 2.4f, 100, default, 1f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 3; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, new Vector2(0f, -1f), 120, default, 0.9f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1.4f, 0.6f, 0.2f);
        }
    }
}
