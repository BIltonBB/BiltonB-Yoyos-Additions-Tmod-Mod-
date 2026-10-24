using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Projectiles
{
    public class StarYG : ModProjectile
    {
        private const float HomingSpeed = 11f;
        private const float MaxTurnDegrees = 15f;

        public override string Texture => "BiltonsYoyoMod/Content/Projectiles/StarYG";

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.aiStyle = -1;
            Projectile.light = 1f;
        }

        public override void AI()
        {
            Projectile.rotation += 0.2f;
            Lighting.AddLight(Projectile.Center, 1f, 1f, 1f);

            if (Projectile.ai[2] < 60f)
            {
                Projectile.ai[2] += 1f;
                Projectile.velocity *= 0.985f;
                return;
            }

            int playerIndex = (int)Projectile.ai[0];

            if (playerIndex >= 0 && playerIndex < Main.maxPlayers && Main.player[playerIndex].active && !Main.player[playerIndex].dead)
            {
                Player target = Main.player[playerIndex];
                Vector2 toTarget = target.Center - Projectile.Center;

                float desiredAngle = toTarget.ToRotation();
                float currentAngle = Projectile.velocity.ToRotation();
                float maxTurnRad = MathHelper.ToRadians(MaxTurnDegrees);
                float newAngle = MathHelper.WrapAngle(currentAngle + MathHelper.Clamp(
                    MathHelper.WrapAngle(desiredAngle - currentAngle), -maxTurnRad, maxTurnRad));

                Projectile.velocity = newAngle.ToRotationVector2() * HomingSpeed;
            }
            else
            {
                if (Projectile.velocity != Vector2.Zero)
                    Projectile.velocity = Vector2.Normalize(Projectile.velocity) * HomingSpeed;
            }
        }
    }
}