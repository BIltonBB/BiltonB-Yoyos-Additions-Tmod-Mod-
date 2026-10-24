using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjTEOC : ModProjectile
    {
        private const float OrbitDistance = 300f;
        private const float MaxMoveSpeed = 12f;
        private const int AttackInterval = 300;
        private const int DashDuration = 60;
        private const float DashDistance = 500f;

        private int attackTimer;
        private int dashTimer;
        private int targetIndex = -1;
        private Vector2 desiredPosition;
        private Vector2 lastTargetCenter;
        private bool hasPosition;
        private bool dashing;
        private Vector2 dashDirection;
        private int queuedPSShots;
        private int queuedPSTimer;
        private int queuedGroupId;
        private int queuedPSReleaseTick;
        private bool psAttackActive;
        private bool ts2OpeningAttackDone;
        private readonly List<Vector2> afterimages = new List<Vector2>();

        // ai[0] = parent yoyo index.
        // ai[1] = 1 when this TEOC was CREATED while the owner was in TS2.
        private bool HasTS2Attack => Projectile.ai[1] > 0.5f;

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 9999;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            // ai[0] = parent main-yoyo index when TEOC is spawned by a Core.
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex >= 0 && parentIndex < Main.maxProjectiles)
            {
                Projectile parentYoyo = Main.projectile[parentIndex];
                if (!parentYoyo.active || parentYoyo.owner != Projectile.owner || parentYoyo.type != ModContent.ProjectileType<LordYoyoProj>())
                {
                    Projectile.Kill();
                    Projectile.netUpdate = true;
                    return;
                }
            }
            else if (!HasTS2Attack)
            {
                // A normal TEOC must have a valid parent yoyo. The TS2
                // special TEOC is allowed to exist independently because
                // TS2 is player-owned and can trigger while the yoyo is
                // unequipped.
            }

            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
            {
                Projectile.Kill();
                return;
            }

            // Existing TEOCs are removed when TS2 begins. A TEOC created
            // during TS2 is explicitly marked with ai[1] and is allowed to
            // remain so it can execute the TS2-specific DR attack.
            LordYoyoPlayerState ownerState =
                player.GetModPlayer<LordYoyoPlayerState>();

            if (ownerState.TS2Active && !HasTS2Attack)
            {
                Projectile.Kill();
                return;
            }

            NPC target = GetTarget();

            // A TEOC created while TS2 is active performs its special DR
            // decision immediately. It does not wait for the normal 5-second
            // attack interval before showing the TS2 behavior.
            if (HasTS2Attack && !ts2OpeningAttackDone && Main.myPlayer == Projectile.owner)
            {
                ts2OpeningAttackDone = true;

                if (Main.rand.NextBool())
                    SpawnTS2DRGroup();
                else
                    SpawnSingleDR(target, true);
            }

            // This TEOC exists only to carry the TS2 special attack.
            // Do not let it fall into the normal 5-second attack cycle.
            if (HasTS2Attack && ownerState.TS2Active)
            {
                attackTimer = 0;
            }
            if (target == null)
            {
                targetIndex = FindNearestEnemy(Projectile.Center, 1400f);
                target = GetTarget();
                hasPosition = false;
            }

            if (target != null)
            {
                if (!hasPosition || Vector2.DistanceSquared(target.Center, lastTargetCenter) > 500f * 500f)
                {
                    targetIndex = target.whoAmI;
                    lastTargetCenter = target.Center;
                    desiredPosition = target.Center + Main.rand.NextVector2Unit() * OrbitDistance;
                    hasPosition = true;
                }
            }

            if (dashing)
            {
                Projectile.velocity = dashDirection * (DashDistance / DashDuration);
                Projectile.Center += Projectile.velocity;

                if (Main.myPlayer == Projectile.owner)
                {
                    if (Vector2.DistanceSquared(Projectile.Center, afterimages.Count > 0 ? afterimages[afterimages.Count - 1] : Projectile.Center) >= 12f * 12f)
                    {
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            Projectile.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<LordYoyoProjTR>(),
                            0,
                            0f,
                            Projectile.owner
                        );
                    }
                }

                dashTimer++;
                if (dashTimer >= DashDuration)
                {
                    dashing = false;
                    dashTimer = 0;
                    hasPosition = false;
                }
            }
            else if (hasPosition)
            {
                Vector2 toPosition = desiredPosition - Projectile.Center;
                float distance = toPosition.Length();
                Vector2 desiredVelocity = toPosition.SafeNormalize(Vector2.Zero) * MathHelper.Clamp(distance * 0.055f, 0f, MaxMoveSpeed);

                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.16f);
                Projectile.Center += Projectile.velocity;

                if (distance < 8f)
                    Projectile.velocity *= 0.88f;
            }

            if (Main.myPlayer == Projectile.owner)
            {
                attackTimer++;

                if (!psAttackActive && attackTimer >= AttackInterval)
                {
                    attackTimer = 0;
                    BeginRandomAttack();
                }

                if (player.statLife < player.statLifeMax2 * 0.5f && !dashing && !psAttackActive && Main.GameUpdateCount % 600 == 0)
                {
                    if (Main.rand.NextBool())
                    {
                        Vector2 dashTarget = target != null
                            ? target.Center
                            : Projectile.Center + Main.rand.NextVector2Unit() * DashDistance;

                        dashDirection = (dashTarget - Projectile.Center).SafeNormalize(Vector2.UnitX);
                        dashing = true;
                        dashTimer = 0;
                    }
                }
            }

            if (psAttackActive)
                HandlePSAttack();

            LordYoyoVisualHelper.CachePosition(afterimages, Projectile.Center, 4f, 24);
            LordYoyoVisualHelper.AddBlueLight(Projectile.Center, 1.4f);
        }

        private void BeginRandomAttack()
        {
            int roll = Main.rand.Next(100);
            NPC target = GetTarget();
            Player player = Main.player[Projectile.owner];
            bool ts2Active = HasTS2Attack;

            if (roll < 20)
            {
                if (ts2Active && Main.rand.NextBool())
                {
                    SpawnTS2DRGroup();
                }
                else if (ts2Active)
                {
                    // During TS2, the single-DR branch always spins.
                    SpawnSingleDR(target, true);
                }
                else
                {
                    // Normal DR is always spinning; no stationary DR before TS2.
                    SpawnSingleDR(target, true);
                }
            }
            else if (roll < 50)
            {
                psAttackActive = true;
                queuedPSTimer = 0;
                queuedPSShots = 0;
                queuedPSReleaseTick = (int)Main.GameUpdateCount + 120;
                queuedGroupId = Math.Max(1, (int)(Main.GameUpdateCount + Projectile.whoAmI * 1009));
            }
            else if (roll < 75)
            {
                for (int i = 0; i < 12; i++)
                {
                    Vector2 direction = (MathHelper.TwoPi * i / 12f).ToRotationVector2();
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        direction * 7f,
                        ModContent.ProjectileType<LordYoyoProjPB>(),
                        Math.Max(1, Projectile.damage / 2),
                        Projectile.knockBack,
                        Projectile.owner
                    );
                }
            }
            else
            {
                int targetWhoAmI = target?.whoAmI ?? -1;
                for (int i = 0; i < 6; i++)
                {
                    Vector2 direction = (MathHelper.TwoPi * i / 6f).ToRotationVector2();
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        direction * 8f,
                        ModContent.ProjectileType<LordYoyoProjPE>(),
                        Math.Max(1, Projectile.damage / 2),
                        Projectile.knockBack,
                        Projectile.owner,
                        targetWhoAmI
                    );
                }
            }
        }

        private void SpawnSingleDR(NPC target, bool forceSpin)
        {
            Vector2 direction = target != null
                ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                : Main.rand.NextVector2Unit();

            float spin = forceSpin
                ? (Main.rand.NextBool() ? 1f : -1f)
                : (Main.rand.NextBool()
                    ? (Main.rand.NextBool() ? 1f : -1f)
                    : 0f);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<LordYoyoProjDR>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner,
                direction.ToRotation(),
                spin,
                Projectile.whoAmI
            );
        }

        private void SpawnTS2DRGroup()
        {
            int count = Main.rand.NextBool() ? 3 : 5;
            float gap = MathHelper.TwoPi / count;
            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            float spinSpeed = Main.rand.NextFloat(0.35f, 1.35f);
            if (Main.rand.NextBool())
                spinSpeed *= -1f;

            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + gap * i;
                Projectile dr = Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<LordYoyoProjDR>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    angle,
                    spinSpeed,
                    Projectile.whoAmI
                );

                if (dr != null)
                {
                    // localAI[1] marks grouped spin. localAI[2] is the exact
                    // one-second remaining spin time.
                    dr.localAI[1] = 1f;
                    dr.localAI[2] = 60f;
                    dr.netUpdate = true;
                }
            }
        }

        private void HandlePSAttack()
        {
            queuedPSTimer++;

            if (queuedPSTimer % 6 == 0 && queuedPSShots < 12)
            {
                int releaseTick = queuedPSReleaseTick;

                Vector2 initialDirection = Main.rand.NextVector2Unit();
                Projectile ps = Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    initialDirection * Main.rand.NextFloat(4f, 7f),
                    ModContent.ProjectileType<LordYoyoProjPS>(),
                    Math.Max(1, Projectile.damage / 2),
                    Projectile.knockBack,
                    Projectile.owner,
                    queuedGroupId
                );

                ps.ai[1] = releaseTick;
                ps.ai[2] = -1f;
                ps.netUpdate = true;
                queuedPSShots++;
            }

            if (queuedPSShots >= 12 && queuedPSTimer >= 72)
                psAttackActive = false;
        }

        private int FindNearestEnemy(Vector2 center, float maxDistance)
        {
            int best = -1;
            float bestDistance = maxDistance * maxDistance;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.life <= 0)
                    continue;

                float distance = Vector2.DistanceSquared(center, npc.Center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        private NPC GetTarget()
        {
            if (targetIndex < 0 || targetIndex >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[targetIndex];
            return npc.active && !npc.friendly && !npc.dontTakeDamage && npc.life > 0 ? npc : null;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            // One draw for the main TEOC. The trail is deliberately faint so
            // the idle TEOC cannot look like a second copy of itself.
            if (Projectile.velocity.LengthSquared() > 0.5f)
                LordYoyoVisualHelper.DrawNeonTrail(texture, afterimages, Projectile.rotation, 0.78f, lightColor, Main.screenPosition, 0.70f);

            LordYoyoVisualHelper.DrawNeonSprite(texture, Projectile.Center, Projectile.rotation, 0.80f, lightColor, Main.screenPosition, 1.25f);
            return false;
        }
    }
}
