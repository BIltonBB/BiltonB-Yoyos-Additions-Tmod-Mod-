using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using BiltonsYoyoMod.Common;

namespace BiltonsYoyoMod.Projectiles
{
    // Friendly homing star fired by the Guardian yoyos.
    // Mirrors the hostile StarYG feel: brief spin-up, then turn-limited homing.
    public class GYStarProj : ModProjectile
    {
        public override string Texture => "BiltonsYoyoMod/Projectiles/GYStarProj";

        private const float HomingSpeed = 11f;
        private const float MaxTurnDegrees = 15f;
        private const int SpinUpTicks = 20;
        private const float AcquireRange = 800f;

        private readonly List<Vector2> trail = new List<Vector2>();
        private int age;

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.aiStyle = -1;
            Projectile.light = 0.8f;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            Projectile.rotation += 0.2f;
            Lighting.AddLight(Projectile.Center, 0.5f, 0.2f, 0.9f);
            GYPurpleTrail.Cache(trail, Projectile.Center);

            age++;

            if (age < SpinUpTicks)
            {
                Projectile.velocity *= 0.985f;
                return;
            }

            NPC target = FindNearestEnemy(AcquireRange);
            if (target == null)
            {
                if (Projectile.velocity != Vector2.Zero && Projectile.velocity.Length() < HomingSpeed)
                    Projectile.velocity = Vector2.Normalize(Projectile.velocity) * HomingSpeed;
                return;
            }

            float desired = (target.Center - Projectile.Center).ToRotation();
            float current = Projectile.velocity.Length() > 0.01f ? Projectile.velocity.ToRotation() : desired;
            float maxTurn = MathHelper.ToRadians(MaxTurnDegrees);
            float next = current + MathHelper.Clamp(MathHelper.WrapAngle(desired - current), -maxTurn, maxTurn);

            Projectile.velocity = next.ToRotationVector2() * HomingSpeed;
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
}
