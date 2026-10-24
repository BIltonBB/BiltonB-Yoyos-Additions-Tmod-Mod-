using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Stars Above-style heavy-hit slash arc (reimplemented, original art):
    // visual-only crescent spawned on Ogre heavy swings, Pumpking moon
    // blades and Betsy dash impacts. Grows and fades over 14 ticks.
    // ai[0] = facing rotation, ai[1] = packed RGB tint.
    public class YoyoSlashArcProj : ModProjectile
    {
        public override string Texture => "BiltonsYoyoMod/Projectiles/YoyoSlashArc";

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 14;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
            Projectile.damage = 0;
            Projectile.rotation = Projectile.ai[0];
            Projectile.scale = 0.6f + 0.7f * (1f - Projectile.timeLeft / 14f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Swept arc strip (Spirit PrimitiveSlashArc-style, CPU quads):
            // 90-degree sweep unfolding over life, radius growing, width
            // tapering to the tail, alpha fading. ai[0] = base rotation.
            float t = 1f - Projectile.timeLeft / 14f;
            float ease = 1f - (1f - t) * (1f - t);
            int packed = (int)Projectile.ai[1];
            Color tint = new Color((packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
            // Same hue burned towards white: the cut reads as light with a
            // bright inner core instead of one flat fill.
            Color hot = Color.Lerp(tint, Color.White, 0.72f);
            float baseRot = Projectile.ai[0];
            float sweep = MathHelper.PiOver2 * ease;
            float radius = (20f + 46f * ease) * Projectile.scale;
            int segs = 10;
            float life = 1f - t;
            Vector2 tip = Projectile.Center + (baseRot + sweep).ToRotationVector2() * radius;
            // Per segment: three stacked radial lenses - wide dim halo, tinted
            // body, white-hot core. Soft gradient edges everywhere, so the arc
            // never shows the hard aliased edge of a stretched 1px quad, and the
            // taper (fat hot leading edge -> thin dim tail) gives it direction.
            for (int i = 0; i < segs; i++)
            {
                float f0 = (float)i / segs;
                Vector2 p0 = Projectile.Center + (baseRot + sweep * f0).ToRotationVector2() * radius;
                Vector2 p1 = Projectile.Center + (baseRot + sweep * (i + 1) / segs).ToRotationVector2() * radius;
                Vector2 seg = p1 - p0;
                float segLen = seg.Length();
                if (segLen < 0.01f)
                    continue;
                Vector2 mid = (p0 + p1) * 0.5f;
                float segAng = seg.ToRotation();
                float taper = 1f - f0;                 // width: fat near the pivot, thin at the tip
                float heat = 0.45f + 0.55f * f0;       // brightness rides the leading edge (comet read)
                float w = (2f + 9f * taper) * Projectile.scale;
                MiniYoyoFx.DrawSoftRay(mid, segAng, segLen + w * 2.2f, w * 3.4f, tint, life * 0.20f * heat);
                MiniYoyoFx.DrawSoftRay(mid, segAng, segLen + w * 0.9f, w * 1.30f, tint, life * 0.55f * heat);
                MiniYoyoFx.DrawSoftRay(mid, segAng, segLen + w * 0.4f, w * 0.50f, hot, life * 0.75f * heat);
            }
            MiniYoyoFx.DrawGlow(tip, 14f * Projectile.scale, new Color(255, 255, 255), 0.5f * life);
            return false;
        }
    }
}
