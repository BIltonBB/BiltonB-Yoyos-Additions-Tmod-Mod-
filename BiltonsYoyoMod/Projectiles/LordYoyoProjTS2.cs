using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjTS2 : ModProjectile
    {
        private const int TotalDuration = 180;
        private const int SpawnInterval = 6;
        private int healTimer;
        private bool specialDRAttackStarted;

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration + 5;
            Projectile.hide = true;
        }

        public override void AI()
        {
            int timer = (int)Projectile.localAI[0]++;
            Player player = Main.player[Projectile.owner];

            if (player == null || !player.active)
            {
                Projectile.Kill();
                return;
            }

            // TS2 owns the special DR replacement attack while all TEOCs are
            // absent. Trigger it exactly once at the start of the phase.
            if (!specialDRAttackStarted && Main.myPlayer == Projectile.owner)
            {
                specialDRAttackStarted = true;
                SpawnSpecialDRAttack(player);
            }

            // Refresh temporary immortality.
            player.immune = true;
            player.immuneTime = Math.Max(2, (int)player.immuneTime);
            player.noFallDmg = true;

            // Heal 10 HP every 0.1 seconds during the TS2 transition.
            healTimer++;
            if (healTimer >= 6)
            {
                healTimer = 0;
                int targetLife = Math.Max(1, player.statLifeMax2 / 2);
                if (player.statLife < targetLife)
                {
                    int amount = Math.Min(10, targetLife - player.statLife);
                    player.statLife += amount;
                    player.HealEffect(amount, true);
                }
            }

            if (Main.myPlayer == Projectile.owner && timer % SpawnInterval == 0 && timer < 150)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 direction = angle.ToRotationVector2();
                Vector2 spawn = player.Center + direction * 500f;

                Projectile orb = Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    spawn,
                    direction * -1f,
                    ModContent.ProjectileType<LordYoyoProjTS2Orb>(),
                    0,
                    0f,
                    Projectile.owner
                );

                if (orb != null)
                    orb.ai[0] = Projectile.whoAmI;
            }

            if (timer % 3 == 0)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 position = player.Center + angle.ToRotationVector2() * Main.rand.NextFloat(30f, 95f);
                Dust d = Dust.NewDustPerfect(
                    position,
                    DustID.IceTorch,
                    Main.rand.NextVector2Circular(1.5f, 1.5f),
                    100,
                    Color.White,
                    Main.rand.NextFloat(0.8f, 1.5f)
                );
                d.noGravity = true;
            }

            if (timer >= TotalDuration)
            {
                FinishSequence(player);
                Projectile.Kill();
            }
        }

        private void SpawnSpecialDRAttack(Player player)
        {
            // 50%: 3/5 synchronized rotating DR group.
            if (Main.rand.NextBool())
            {
                int count = Main.rand.NextBool() ? 3 : 5;
                float gap = MathHelper.TwoPi / count;
                float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                float spinSpeed = Main.rand.NextFloat(0.35f, 1.35f);

                if (Main.rand.NextBool())
                    spinSpeed *= -1f;

                int damage = 1;
                int yoyoType = ModContent.ProjectileType<LordYoyoProj>();

                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile yoyo = Main.projectile[i];
                    if (yoyo.active && yoyo.owner == player.whoAmI && yoyo.type == yoyoType)
                    {
                        damage = Math.Max(1, yoyo.damage);
                        break;
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    float angle = baseAngle + gap * i;

                    Projectile dr = Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<LordYoyoProjDR>(),
                        damage,
                        0f,
                        Projectile.owner,
                        angle,
                        spinSpeed,
                        Projectile.whoAmI
                    );

                    if (dr != null)
                    {
                        dr.localAI[1] = 1f;
                        dr.localAI[2] = 60f;
                        dr.netUpdate = true;
                    }
                }
            }
            else
            {
                // 50%: one always-spinning DR.
                NPC target = FindNearestEnemy(player.Center, 1400f);
                Vector2 direction = target != null
                    ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                    : Main.rand.NextVector2Unit();

                float spin = Main.rand.NextBool() ? 1f : -1f;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<LordYoyoProjDR>(),
                    1,
                    0f,
                    Projectile.owner,
                    direction.ToRotation(),
                    spin,
                    Projectile.whoAmI
                );
            }
        }

        private NPC FindNearestEnemy(Vector2 center, float maxDistance)
        {
            NPC nearest = null;
            float bestDistanceSquared = maxDistance * maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.life <= 0)
                    continue;

                float distanceSquared = Vector2.DistanceSquared(center, npc.Center);

                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    nearest = npc;
                }
            }

            return nearest;
        }

        private void FinishSequence(Player player)
        {
            LordYoyoPlayerState state = player.GetModPlayer<LordYoyoPlayerState>();

            // TS2 completion belongs to the player, not to a specific yoyo
            // projectile. This survives unequipping/re-equipping.
            state.TS2Active = false;
            state.PostTS2CorePhase = true;

            player.immune = false;
            player.immuneTime = 0;
            player.noFallDmg = false;

            // If the yoyo is equipped right now, replace any remaining cores
            // with exactly five. A later re-equip also reads PostTS2CorePhase
            // and creates five through LordYoyoProj.OnSpawn.
            if (Main.myPlayer == player.whoAmI)
            {
                int otType = ModContent.ProjectileType<LordYoyoProjOT>();
                bool hasOT = false;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile existing = Main.projectile[i];
                    if (existing.active && existing.owner == player.whoAmI && existing.type == otType)
                    {
                        hasOT = true;
                        break;
                    }
                }

                if (!hasOT)
                {
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        player.Center,
                        Vector2.Zero,
                        otType,
                        0,
                        0f,
                        player.whoAmI
                    );
                }

                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile other = Main.projectile[i];

                    if (!other.active || other.owner != player.whoAmI)
                        continue;

                    if (other.type == ModContent.ProjectileType<LordYoyoProjTEOC>() ||
                        other.type == ModContent.ProjectileType<LordYoyoProjC>())
                    {
                        other.Kill();
                        other.netUpdate = true;
                    }
                }

                int yoyoType = ModContent.ProjectileType<LordYoyoProj>();
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile other = Main.projectile[i];
                    if (!other.active || other.owner != player.whoAmI || other.type != yoyoType)
                        continue;

                    if (other.ModProjectile is LordYoyoProj yoyo)
                    {
                        yoyo.ForcePostTS2FiveCores();
                    }
                }
            }
        }
    }
}
