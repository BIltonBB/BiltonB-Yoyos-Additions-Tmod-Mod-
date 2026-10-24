using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System; // Needed for Math functions

namespace BiltonsYoyoMod.Projectiles
{
    public class SkeletronYoyoProj : ModProjectile
    {
        private int spawnTimer; // class-level timer for spawning bones

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;

            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            // Yoyo properties
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 40f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 210f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 10f;
        }

        public override void AI()
        {
            spawnTimer++;

            if (spawnTimer >= 30) // every 0.5 seconds
            {
                spawnTimer = 0;

                int maxBones = 2;
                int currentBones = 0;

                // Count existing orbit bones for THIS yoyo
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];

                    if (p.active &&
                        p.type == ModContent.ProjectileType<SkeletronYoyoBoneProj>() &&
                        (int)p.ai[0] == Projectile.whoAmI &&
                        p.owner == Projectile.owner)
                    {
                        currentBones++;
                    }
                }

                int bonesToSpawn = maxBones - currentBones;

                if (bonesToSpawn <= 0)
                    return;

                // Spawn enough bones to reach exactly 2.
                for (int i = 0; i < bonesToSpawn; i++)
                {
                    float startingAngle;

                    // The two bones start exactly opposite each other.
                    if (currentBones + i == 0)
                        startingAngle = 0f;
                    else
                        startingAngle = MathHelper.Pi;

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<SkeletronYoyoBoneProj>(),
                        Projectile.damage / 2,
                        1f,
                        Projectile.owner,
                        Projectile.whoAmI, // ai[0] = parent yoyo index
                        startingAngle       // ai[1] = assigned orbit angle
                    );
                }
            }
        }

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            // Tell every bone belonging to THIS yoyo to chase this NPC.
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile bone = Main.projectile[i];

                if (bone.active &&
                    bone.type == ModContent.ProjectileType<SkeletronYoyoBoneProj>() &&
                    (int)bone.ai[0] == Projectile.whoAmI &&
                    bone.owner == Projectile.owner)
                {
                    // ai[2] = target NPC index
                    bone.ai[2] = target.whoAmI;

                    // localAI[0] = state
                    // 0 = orbit
                    // 1 = chasing
                    // 2 = returning to orbit
                    // 3 = stopped after hitting target

                    bone.localAI[0] = 1f;

                    // localAI[1] = chase timer
                    bone.localAI[1] = 0f;

                    // localAI[2] is left alone so the bone's spin
                    // can smoothly continue from its current rotation.
                }
            }

            base.OnHitNPC(target, hit, damageDone);
        }
    }
}