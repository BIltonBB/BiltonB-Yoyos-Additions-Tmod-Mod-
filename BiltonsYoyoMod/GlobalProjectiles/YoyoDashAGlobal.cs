using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
 // I cant get this to work wth
namespace BiltonsYoyoMod.GlobalProjectiles
{
    public class YoyoDashAGlobal : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // localAI usage:
        // [0] = dash timer
        // [1] = retrigger lock

        private const float DashSpeed = 28f;
        private const int DashDuration = 15;

        public override void AI(Projectile projectile)
        {
            // Only yoyos
            if (projectile.aiStyle != ProjAIStyleID.Yoyo)
                return;

            // Owner only (MP-safe)
            if (projectile.owner != Main.myPlayer)
                return;

            Player player = Main.player[projectile.owner];
            var mp = player.GetModPlayer<Players.YoyoDashAPlayer>();

            // Must have accessory enabled
            if (!mp.enabled)
                return;

            // DASH ACTIVE
            if (projectile.localAI[0] > 0f)
            {
                projectile.localAI[0]--;

                Vector2 dir = (Main.MouseWorld - projectile.Center);
                if (dir.LengthSquared() > 1f)
                    dir.Normalize();

                projectile.velocity = dir * DashSpeed;

                SpawnDust(projectile, dir);
                UpdateAfterimages(projectile);

                // End dash
                if (projectile.localAI[0] <= 0f)
                {
                    mp.dashCooldown = 60; // 1 second
                    mp.dashFreeze = 15;   // 0.25s freeze
                    projectile.localAI[1] = 1f; // retrigger lock
                }

                return; // override vanilla yoyo AI
            }

            // COOLDOWN TICK
            if (mp.dashFreeze > 0)
                mp.dashFreeze--;
            else if (mp.dashCooldown > 0)
                mp.dashCooldown--;

            // RETRIGGER LOCK
            if (projectile.localAI[1] > 0f)
            {
                if (!player.controlUseTile) // right-click released
                    projectile.localAI[1] = 0f;

                return;
            }

            // DASH START (RIGHT CLICK)
            if (mp.dashCooldown > 0)
                return;

            if (!player.controlUseTile)
                return;

            Vector2 toMouse = Main.MouseWorld - projectile.Center;
            if (toMouse.LengthSquared() < 24f * 24f)
                return;

            toMouse.Normalize();
            projectile.velocity = toMouse * DashSpeed;
            projectile.localAI[0] = DashDuration;
        }

        public override void ModifyHitNPC(
            Projectile projectile,
            NPC target,
            ref NPC.HitModifiers modifiers)
        {
            if (projectile.aiStyle == ProjAIStyleID.Yoyo &&
                projectile.localAI[0] > 0f)
            {
                modifiers.SourceDamage *= 2f;
            }
        }

        private void SpawnDust(Projectile projectile, Vector2 dir)
        {
            for (int i = 0; i < 2; i++)
            {
                Dust d = Dust.NewDustDirect(
                    projectile.position,
                    projectile.width,
                    projectile.height,
                    DustID.Electric,
                    -dir.X * 2f,
                    -dir.Y * 2f,
                    0,
                    default,
                    1.1f
                );
                d.noGravity = true;
            }
        }

        private void UpdateAfterimages(Projectile projectile)
        {
            if (projectile.oldPos == null || projectile.oldPos.Length < 8)
                projectile.oldPos = new Vector2[8];

            for (int i = projectile.oldPos.Length - 1; i > 0; i--)
                projectile.oldPos[i] = projectile.oldPos[i - 1];

            projectile.oldPos[0] = projectile.position;
        }
    }
}