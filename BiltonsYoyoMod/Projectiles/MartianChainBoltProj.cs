using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Martian chain bolt: short-lived teal fang fired at chained victims.
    // Dumb and fast (recorded bearing, no tracking), local immunity.
    public class MartianChainBoltProj : ModProjectile, IYoyoInstantHit
    {
        private readonly List<Vector2> ribbonTrail = new List<Vector2>();
        private bool spawnFxDone;

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 45;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                Lighting.AddLight(Projectile.Center, 0.4f, 1f, 0.6f);
            }
            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation();
            MiniYoyoFx.RibbonPush(ribbonTrail, Projectile.Center);
            Lighting.AddLight(Projectile.Center, 0.3f, 0.9f, 0.5f);
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, -Projectile.velocity * 0.08f, 120, default, 0.75f);
                spark.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            MiniYoyoFx.RibbonDrawSoft(ribbonTrail, new Color(60, 220, 140), new Color(220, 255, 230), 4f);
            MiniYoyoFx.DrawGlow(Projectile.Center, 16f, new Color(120, 255, 170), 0.5f);
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Electrified, 60);
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust pop = Dust.NewDustPerfect(target.Center, DustID.Electric, angle.ToRotationVector2() * 2.6f, 100, default, 0.9f);
                pop.noGravity = true;
            }
            Lighting.AddLight(target.Center, 0.4f, 1f, 0.6f);
        }
    }
}
