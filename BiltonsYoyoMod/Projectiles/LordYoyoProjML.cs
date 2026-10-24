using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjML : ModProjectile
    {
        private const int ChaseDuration = 180;
        private const int LatchDuration = 300;
        private const int DamageInterval = 6;
        private const float ChaseSpeed = 6f;
        private const float CatchDistance = 18f;

        private int age;
        private int targetIndex;
        private bool latched;
        private int latchTimer;
        private int damageTimer;
        private readonly List<Vector2> afterimages = new List<Vector2>();

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ChaseDuration + LatchDuration + 10;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            age++;
            NPC target = GetTarget();

            if (target == null)
            {
                Projectile.Kill();
                return;
            }

            if (!latched)
            {
                Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Projectile.velocity = Vector2.Lerp(
                    Projectile.velocity,
                    direction * ChaseSpeed,
                    0.16f
                );
                Projectile.Center += Projectile.velocity;

                if (Vector2.DistanceSquared(Projectile.Center, target.Center) <= CatchDistance * CatchDistance)
                {
                    latched = true;
                    latchTimer = 0;
                    damageTimer = 0;
                    Projectile.velocity = Vector2.Zero;
                }
                else if (age >= ChaseDuration)
                {
                    Projectile.Kill();
                    return;
                }
            }
            else
            {
                latchTimer++;
                Projectile.Center = target.Center;
                Projectile.velocity = Vector2.Zero;

                damageTimer++;
                if (damageTimer >= DamageInterval)
                {
                    damageTimer = 0;
                    DamageLatchedTarget(target);
                }

                if (latchTimer >= LatchDuration)
                {
                    Projectile.Kill();
                    return;
                }
            }

            LordYoyoVisualHelper.CachePosition(afterimages, Projectile.Center, 4f, 22);
            LordYoyoVisualHelper.AddBlueLight(Projectile.Center, latched ? 1.25f : 1.0f);
        }

        private void DamageLatchedTarget(NPC target)
        {
            Projectile parent = FindParent();
            int damage = parent != null ? Math.Max(1, parent.damage / 5) : Math.Max(1, Projectile.damage);
            int direction = target.Center.X >= Projectile.Center.X ? 1 : -1;

            target.SimpleStrikeNPC(
                damage,
                direction,
                false,
                0f,
                DamageClass.Melee
            );
        }

        private Projectile FindParent()
        {
            int parentIndex = (int)Projectile.ai[1];
            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
                return null;

            Projectile parent = Main.projectile[parentIndex];
            if (!parent.active || parent.owner != Projectile.owner || parent.type != ModContent.ProjectileType<LordYoyoProj>())
                return null;

            return parent;
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
            LordYoyoVisualHelper.DrawNeonTrail(texture, afterimages, Projectile.rotation, 0.72f, lightColor, Main.screenPosition, 1.25f);
            LordYoyoVisualHelper.DrawNeonSprite(texture, Projectile.Center, Projectile.rotation, 0.78f, lightColor, Main.screenPosition, 1.35f);
            return false;
        }

        public void SetTarget(int npcIndex)
        {
            targetIndex = npcIndex;
        }
    }
}
