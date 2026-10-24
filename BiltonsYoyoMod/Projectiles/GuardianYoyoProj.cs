using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Projectiles
{
    // Base logic shared by GuardianYoyoProj and CGuardianYoyoProj.
    // Subclasses only tune interval / damage / visuals.
    public abstract class GuardianYoyoProjBase : ModProjectile
    {
        // Ticks between volleys: 60 (1s) normal, 30 (0.5s) charged.
        protected virtual int AttackInterval => 60;
        // Damage multiplier applied to spawned sub-projectiles.
        protected virtual float SubDamageMult => 0.25f;
        protected virtual float TargetRange => 900f;

        private readonly List<Vector2> trail = new List<Vector2>();
        private int starTimer;
        private int teleTimer;

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.tileCollide = false;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 320f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 14f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = -1f;
        }

        public override void AI()
        {
            Lighting.AddLight(Projectile.Center, 0.45f, 0.15f, 0.8f);
            GYPurpleTrail.Cache(trail, Projectile.Center);

            // Vanilla yoyo AI owns ai[] — timers live in fields, and only the
            // owner spawns helpers.
            if (Main.myPlayer != Projectile.owner)
                return;

            starTimer++;
            teleTimer++;

            if (starTimer >= AttackInterval)
            {
                starTimer = 0;
                TryFireStar();
            }

            if (teleTimer >= AttackInterval)
            {
                teleTimer = 0;
                FireTelegraph();
            }
        }

        private void TryFireStar()
        {
            NPC target = FindNearestEnemy(TargetRange);
            if (target == null)
                return;

            Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
            int dmg = (int)(Projectile.damage * SubDamageMult);

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, dir * 11f,
                ModContent.ProjectileType<GYStarProj>(), dmg, 2f, Projectile.owner);
        }

        private void FireTelegraph()
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            int dmg = (int)(Projectile.damage * SubDamageMult);

            int p = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<GYTelegraphProj>(), dmg, 0f, Projectile.owner, angle);
            if (p >= 0 && p < Main.maxProjectiles)
                Main.projectile[p].timeLeft = GYTelegraphProj.Lifetime;
        }

        private NPC FindNearestEnemy(float maxRange)
        {
            NPC best = null;
            float bestDistSq = maxRange * maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy())
                    continue;

                float d = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                if (d < bestDistSq)
                {
                    bestDistSq = d;
                    best = npc;
                }
            }

            return best;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            GYPurpleTrail.Draw(trail);
            return true;
        }
    }

    public class GuardianYoyoProj : GuardianYoyoProjBase
    {
        public override string Texture => "BiltonsYoyoMod/Projectiles/GuardianYoyoProj";
    }

    public class CGuardianYoyoProj : GuardianYoyoProjBase
    {
        public override string Texture => "BiltonsYoyoMod/Projectiles/CGuardianYoyoProj";

        protected override int AttackInterval => 30; // 0.5s
        protected override float SubDamageMult => 1f;
    }
}
