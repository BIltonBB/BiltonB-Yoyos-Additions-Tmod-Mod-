using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class CrimsonYoyoProj : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.scale = 1f;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 10f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 175f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 12f;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            // Calculate total missing health relative to Max HP
            int missingHP = owner.statLifeMax2 - owner.statLife;
            if (missingHP < 0) missingHP = 0;

            // 1 clone for every 50 HP missing (capped at 15 max clones)
            int targetCloneCount = missingHP / 50;
            if (targetCloneCount > 15) targetCloneCount = 15;

            // Count current active clones belonging strictly to THIS yoyo
            int cloneType = ModContent.ProjectileType<CrimsonYoyoProjClone>();
            int activeClones = 0;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == cloneType && (int)p.ai[1] == Projectile.whoAmI)
                {
                    activeClones++;
                }
            }

            // Spawn missing clones up to targetCloneCount
            while (activeClones < targetCloneCount)
            {
                if (Main.myPlayer == Projectile.owner)
                {
                    // Random offset angle (0 to 2PI) and distance (40 to 90 pixels)
                    float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(40f, 90f);

                    // Pass offset angle via ai[0] and parent index via ai[1]
                    Projectile clone = Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        new Vector2(dist, 0f), // Store distance in velocity.X
                        cloneType,
                        Projectile.damage / 2, // 50% damage
                        2f,
                        Projectile.owner,
                        ai0: angle,
                        ai1: Projectile.whoAmI
                    );

                    clone.alpha = 128; // 50% transparent
                    clone.netUpdate = true;
                }

                activeClones++;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Apply Ichor debuff for 5 seconds (300 ticks)
            target.AddBuff(BuffID.Ichor, 300);
        }
    }
}
