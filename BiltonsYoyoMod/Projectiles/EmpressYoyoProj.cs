using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class EmpressYoyoProj : ModProjectile
    {
        private bool healthInitialized;
        private bool wasAboveHalf;
        private bool transitionUsed;

        private int a2Cooldown;
        private int a3Cooldown;

        private int cycleTimer;
        private bool cycleInitialized;
        private bool nextCycleIsB;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.timeLeft = 9999;

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 380f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 22f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = -1f;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active || player.dead)
                return;

            if (a2Cooldown > 0)
                a2Cooldown--;

            if (a3Cooldown > 0)
                a3Cooldown--;

            HandleHealthTransition(player);
            HandleCycle(player);
            DrawYoyoGlow(Projectile.Center);
        }

        private void HandleHealthTransition(Player player)
        {
            bool aboveHalf = player.statLife > player.statLifeMax2 * 0.5f;

            if (!healthInitialized)
            {
                healthInitialized = true;
                wasAboveHalf = aboveHalf;
                transitionUsed = false;
                return;
            }

            // Re-arm after the player has genuinely returned above 50%.
            if (aboveHalf)
                transitionUsed = false;

            // Only the actual crossing ABOVE -> BELOW triggers the special.
            if (wasAboveHalf && !aboveHalf && !transitionUsed && player.statLife > 0 && Main.myPlayer == Projectile.owner)
            {
                transitionUsed = true;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<EmpressYoyoTransition>(),
                    0,
                    0f,
                    Projectile.owner,
                    Projectile.whoAmI
                );
            }

            wasAboveHalf = aboveHalf;
        }

        private void HandleCycle(Player player)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            if (!cycleInitialized)
            {
                cycleInitialized = true;
                nextCycleIsB = true;
                cycleTimer = 0;

                // B starts immediately.
                StartCurrentCycleAttack(player);
                return;
            }

            cycleTimer++;
            if (cycleTimer < 600)
                return;

            cycleTimer = 0;
            StartCurrentCycleAttack(player);
        }

        private void StartCurrentCycleAttack(Player player)
        {
            bool aboveHalf = player.statLife > player.statLifeMax2 * 0.5f;
            bool day = Main.dayTime;

            if (nextCycleIsB)
            {
                int count;
                float damageMultiplier;
                float speedMultiplier;

                if (!day && aboveHalf)
                {
                    count = 20;
                    damageMultiplier = 1f;
                    speedMultiplier = 1f;
                }
                else if (!day && !aboveHalf)
                {
                    count = 25;
                    damageMultiplier = 1f;
                    speedMultiplier = 1.20f;
                }
                else if (day && aboveHalf)
                {
                    count = 30;
                    damageMultiplier = 1.1f;
                    speedMultiplier = 1.25f;
                }
                else
                {
                    count = 60;
                    damageMultiplier = 1.1f;
                    speedMultiplier = 1.35f;
                }

                EmpressYoyoAttackHelperB.StartB(
                    Projectile,
                    count,
                    damageMultiplier,
                    speedMultiplier
                );
            }
            else
            {
                float damageMultiplier = day ? 1.1f : 1f;
                EmpressYoyoAttackHelperB.StartC(
                    Projectile,
                    damageMultiplier
                );
            }

            nextCycleIsB = !nextCycleIsB;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            Player player = Main.player[Projectile.owner];
            bool aboveHalf = player.statLife > player.statLifeMax2 * 0.5f;

            // NIGHT / ABOVE 50% -> A1, no cooldown.
            if (!Main.dayTime && aboveHalf)
            {
                EmpressYoyoAttackHelperA.PerformA1(Projectile, target);
                return;
            }

            // NIGHT / BELOW 50% -> A2, 1 second cooldown.
            if (!Main.dayTime && !aboveHalf)
            {
                if (a2Cooldown <= 0)
                {
                    EmpressYoyoAttackHelperA.PerformA2(Projectile, target, 1f);
                    a2Cooldown = 60;
                }
                return;
            }

            // DAY / ABOVE 50% -> A2, 1 second cooldown, 1.1x damage.
            if (Main.dayTime && aboveHalf)
            {
                if (a2Cooldown <= 0)
                {
                    EmpressYoyoAttackHelperA.PerformA2(Projectile, target, 1.1f);
                    a2Cooldown = 60;
                }
                return;
            }

            // DAY / BELOW 50% -> 75% A2 / 25% A3.
            if (a2Cooldown > 0 || a3Cooldown > 0)
                return;

            if (Main.rand.NextFloat() < 0.75f)
            {
                EmpressYoyoAttackHelperA.PerformA2(Projectile, target, 1f);
                a2Cooldown = 60;
            }
            else
            {
                EmpressYoyoAttackHelperA.PerformA3(Projectile, target, 1f);
                a3Cooldown = 60;
            }
        }

        private void DrawYoyoGlow(Vector2 position)
        {
            float t = (float)(Main.GameUpdateCount % 90) / 90f;
            float wave = (float)Math.Sin(t * MathHelper.TwoPi) * 0.5f + 0.5f;

            Color glowColor = Color.Lerp(Color.HotPink, Color.Yellow, wave);

            Lighting.AddLight(
                position,
                glowColor.ToVector3() * (1.75f + wave * 0.35f)
            );

            if (Main.rand.NextBool(3))
            {
                int dustIndex = Dust.NewDust(
                    position - Vector2.One * 4f,
                    8,
                    8,
                    DustID.PinkTorch
                );

                Main.dust[dustIndex].noGravity = true;
                Main.dust[dustIndex].velocity *= 0.1f;
                Main.dust[dustIndex].scale = 1.15f + wave * 0.7f;
                Main.dust[dustIndex].color = glowColor;
            }
        }
    }
}
