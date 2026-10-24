using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace BiltonsYoyoMod.Projectiles
{
    public class AncientYoyoProj : ModProjectile
    {
        private const float AuraRadius = 90f;
        private const int AuraDamage = 5;
        private const int AuraInterval = 6;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            // Fast, long range, endless spin like other endgame yoyos.
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = -1f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 300f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 16f;
        }

        public override void AI()
        {
            Lighting.AddLight(Projectile.Center, 0.35f, 0.25f, 0.05f);

            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(AuraRadius, AuraRadius) * 0.5f,
                    DustID.MagicMirror,
                    Vector2.Zero,
                    120,
                    default,
                    Main.rand.NextFloat(0.6f, 1.1f)
                );
                dust.noGravity = true;
            }

            Projectile.localAI[0] += 1f;
            if (Projectile.localAI[0] < AuraInterval)
                return;

            Projectile.localAI[0] = 0f;

            if (Projectile.owner < 0 || Projectile.owner >= Main.maxPlayers)
                return;

            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active)
                return;

            float radiusSquared = AuraRadius * AuraRadius;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
                    continue;

                if (!npc.CanBeChasedBy())
                    continue;

                if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radiusSquared)
                    continue;

                int direction = npc.Center.X >= Projectile.Center.X ? 1 : -1;
                owner.ApplyDamageToNPC(npc, AuraDamage, 0f, direction, false, DamageClass.MeleeNoSpeed);

                // Short immunity so the aura ticks roughly every 0.1s.
                if (npc.immune[Projectile.owner] > AuraInterval)
                    npc.immune[Projectile.owner] = AuraInterval;

                npc.netUpdate = true;
            }
        }
    }
}
