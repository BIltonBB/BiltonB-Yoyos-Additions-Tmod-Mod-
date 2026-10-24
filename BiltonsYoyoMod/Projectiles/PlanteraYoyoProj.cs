using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class PlanteraYoyoProj : ModProjectile
    {
        private int seedTimer;
        private bool p2Spawned;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 250f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 16f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = -1f;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            // Phase determination
            bool phase2 = player.statLife < player.statLifeMax2 * 0.75f;

            // Phase 1: Shoot seeds
            seedTimer++;
            if (!phase2 && seedTimer >= 30) // every 0.5s
            {
                seedTimer = 0;
                SpawnSeed();
            }

            // Phase 2: Spawn 10 P2 projectiles if not spawned
            if (phase2)
            {
                if (!p2Spawned)
                {
                    p2Spawned = true;

                    for (int i = 0; i < 10; i++)
                    {
                        // Scatter in a circle around the main yoyo
                        float angle = MathHelper.TwoPi / 10 * i;
                        Vector2 offset = new Vector2(60, 0).RotatedBy(angle);

                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            Projectile.Center + offset,
                            Vector2.Zero,
                            ModContent.ProjectileType<PlanteraYoyoProjP2>(),
                            Projectile.damage,
                            Projectile.knockBack,
                            Projectile.owner,
                            i // store index in ai[0] if needed
                        );
                    }
                }

                // Still shoot main seeds every 0.5s
                if (seedTimer >= 30)
                {
                    seedTimer = 0;
                    SpawnSeed();
                }
            }
        }

        private void SpawnSeed()
        {
            Vector2 direction = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-6f, -2f));
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction,
                ModContent.ProjectileType<PlanteraYoyoProjSeed>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner
            );
        }
    }
}