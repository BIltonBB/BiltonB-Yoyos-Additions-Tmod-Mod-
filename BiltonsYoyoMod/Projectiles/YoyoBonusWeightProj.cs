using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Yoyo Bag bonus weight: one family-tinted orbiter while a mini-boss
    // yoyo is deployed with the bag equipped. ai[0] = packed RGB, localAI[0]
    // = orbit angle. Light contact damage, local immunity, dies with the yoyo.
    public class YoyoBonusWeightProj : ModProjectile, IYoyoInstantHit
    {
        public override string Texture => "BiltonsYoyoMod/Projectiles/YoyoStar";
        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 300;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override Color? GetAlpha(Color lightColor)
        {
            return Color.White * Projectile.Opacity;
        }

        public override void AI()
        {
            Projectile nearest = null;
            float best = 900f * 900f;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.whoAmI != Projectile.whoAmI
                    && YoyoStringHookSystem.TryGetStringIndex(p.type, out _))
                {
                    float dSq = Vector2.DistanceSquared(Projectile.Center, p.Center);
                    if (dSq < best)
                    {
                        best = dSq;
                        nearest = p;
                    }
                }
            }
            if (nearest == null)
            {
                Projectile.Kill();
                return;
            }
            Projectile.localAI[0] += 0.09f;
            Projectile.Center = nearest.Center + Projectile.localAI[0].ToRotationVector2() * 52f;
            Projectile.rotation += 0.2f;

            int packed = (int)Projectile.ai[0];
            Color tint = new Color((packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
            Lighting.AddLight(Projectile.Center, tint.R / 255f * 0.4f, tint.G / 255f * 0.4f, tint.B / 255f * 0.4f);
            if (Main.GameUpdateCount % 5 == 0)
            {
                Dust mote = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, Vector2.Zero, 120, default, 0.7f);
                mote.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int packed = (int)Projectile.ai[0];
            Color tint = new Color((packed >> 16) & 255, (packed >> 8) & 255, packed & 255);
            MiniYoyoFx.DrawGlow(Projectile.Center, 14f, tint, 0.4f);
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                tint * Projectile.Opacity, Projectile.rotation, tex.Size() * 0.5f, 0.7f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            YoyoParticles.SpawnBurst(target.Center, 4, 2.5f, new Color(255, 230, 180), 0.9f, 18);
            Lighting.AddLight(target.Center, 0.5f, 0.45f, 0.35f);
        }
    }
}
