using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class BeeYoyoProj : ModProjectile
    {
        private int shootTimer;
        private int radiusWhoAmI = -1; // Use custom variable instead of corrupting Projectile.ai[0]

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 15f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 180f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 15f;
        }

        public override void AI()
        {
            shootTimer++;

            // Shoot honey every 0.5s
            if (shootTimer >= 30)
            {
                shootTimer = 0;
                if (Main.myPlayer == Projectile.owner)
                {
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Main.rand.NextVector2Circular(10f, 5f),
                        ModContent.ProjectileType<BeeYoyoHProj>(),
                        40,
                        1f,
                        Projectile.owner
                    );
                }
            }

            // Spawn and track the radius projectile belonging strictly to THIS yoyo
            if (Main.myPlayer == Projectile.owner)
            {
                int radiusType = ModContent.ProjectileType<BeeYoyoRadiusProj>();

                bool exists = false;

                if (radiusWhoAmI >= 0 && radiusWhoAmI < Main.maxProjectiles)
                {
                    Projectile p = Main.projectile[radiusWhoAmI];

                    if (p.active &&
                        p.type == radiusType &&
                        p.owner == Projectile.owner)
                    {
                        exists = true;

                        p.Center = Projectile.Center;
                        p.timeLeft = 2;
                    }
                }

                // If no valid radius is attached to this yoyo, spawn a new one
                if (!exists)
                {
                    Projectile radius = Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        radiusType,
                        0,
                        0f,
                        Projectile.owner,
                        ai0: Projectile.whoAmI
                    );

                    radiusWhoAmI = radius.whoAmI;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Poisoned, 300); // 5s
            target.AddBuff(BuffID.Slow, 300); // 5s slow
        }
    }
}