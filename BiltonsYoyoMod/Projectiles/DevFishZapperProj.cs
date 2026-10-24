using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Disco spark the developer fishing rod zaps while its bobber is out.
    // Star-sprite bolt (own YoyoStar art), straight and fast, local immunity.
    public class DevFishZapperProj : ModProjectile
    {
        public override string Texture => "BiltonsYoyoMod/Projectiles/YoyoStar";

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 3;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 180;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            Projectile.rotation += 0.3f;
            Color disco = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            Lighting.AddLight(Projectile.Center, disco.R / 255f, disco.G / 255f, disco.B / 255f);
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.RainbowMk2, -Projectile.velocity * 0.08f, 100, default, 0.85f);
                spark.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color disco = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f, disco, 0.45f);
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Color disco = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            MiniYoyoFx.HitShake(damageDone);
            MiniYoyoFx.KillConfirm(target, disco, Projectile.Center, false);
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust pop = Dust.NewDustPerfect(target.Center, DustID.RainbowMk2, angle.ToRotationVector2() * 2.6f, 100, default, 1f);
                pop.noGravity = true;
            }
            Lighting.AddLight(target.Center, disco.R / 255f, disco.G / 255f, disco.B / 255f);
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust pop = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, angle.ToRotationVector2() * 2.4f, 100, default, 0.9f);
                pop.noGravity = true;
            }
        }
    }
}
