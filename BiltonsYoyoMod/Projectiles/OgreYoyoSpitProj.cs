using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class OgreYoyoSpitProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;
        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = true;

            Projectile.timeLeft = 180;

            Projectile.scale = 0.7f;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M12: rot lift that respects the pre-death fade.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(170, 235, 170), 0.3f) * Projectile.Opacity;
        }

        public override void AI()
        {
            // Sour-seed variant (ai[0] == 1): bigger, floatier, splashier. Same damage/buff.
            bool seed = Projectile.ai[0] == 1f;
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                if (seed)
                    Projectile.scale = 1.05f;
                int pop = seed ? 7 : 5;
                for (int i = 0; i < pop; i++)
                {
                    float angle = MathHelper.TwoPi * i / pop;
                    Dust flash = Dust.NewDustPerfect(Projectile.Center, DustID.Poisoned, angle.ToRotationVector2() * 2f, 100, default, seed ? 1.2f : 1.1f);
                    flash.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.35f, 1f, 0.35f);
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.7f, 0.2f);

            // Gradually slow (seeds hang floatier).
            Projectile.velocity *= seed ? 0.992f : 0.985f;

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Fade before death
            if (Projectile.timeLeft < 60)
                Projectile.alpha = (int)(255f * (1f - Projectile.timeLeft / 60f));

            // Lean spit trail: one directional glob every other tick (slows mid-air).
            // Seeds shed a twin trail.
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Poisoned, -Projectile.velocity * 0.08f, 120, default, seed ? 1f : 0.9f);
                dust.noGravity = true;
                if (seed)
                {
                    Dust twin = Dust.NewDustPerfect(Projectile.Center, DustID.JungleSpore, -Projectile.velocity * 0.05f, 120, default, 0.8f);
                    twin.noGravity = true;
                }
            }

            // M13: seeds glitter as they hang — a rare bright glint off the twin trail.
            if (seed && Main.rand.NextBool(8))
            {
                Dust glint = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f), DustID.JungleSpore, -Projectile.velocity * 0.03f, 120, default, 0.9f);
                glint.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Poisoned, 180);

            // Directional spatter (was 8 random).
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.Poisoned, angle.ToRotationVector2() * 2.2f, 100, default, 1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(target.Center, 0.3f, 0.9f, 0.3f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi * i / 8f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Poisoned, angle.ToRotationVector2() * 2.4f, 100, default, 1.1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.3f, 0.9f, 0.3f);
            return true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Comet read: stretched sprite + venom glow + tail knot, bigger on seeds.
            bool seed = Projectile.ai[0] == 1f;
            float fade = Projectile.Opacity;
            MiniYoyoFx.DrawGlow(Projectile.Center, seed ? 22f : 16f, new Color(90, 220, 110), 0.4f * fade);
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(Projectile.Center - back * 8f, (seed ? 12f : 9f) * fade + 1f, new Color(70, 180, 90), 0.3f * fade);
            }
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                (GetAlpha(lightColor) ?? lightColor), Projectile.rotation, origin, new Vector2(1f, 1.6f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // Seeds burst bigger (same non-damaging fade pop).
            bool seed = Projectile.ai[0] == 1f;
            int pop = seed ? 9 : 6;
            for (int i = 0; i < pop; i++)
            {
                float angle = MathHelper.TwoPi * i / pop;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Poisoned, angle.ToRotationVector2() * (seed ? 2.6f : 2f), 120, default, seed ? 1.1f : 1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.3f, 0.9f, 0.3f);
        }
    }
}
