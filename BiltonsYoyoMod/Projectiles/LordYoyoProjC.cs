using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjC : ModProjectile
    {
        private int hitCount;
        private int attackTimer;
        private int hostileHitCooldown;
        private bool initialized;
        private float radius = 58f;
        private float orbitPhase;
        private readonly List<Vector2> afterimages = new List<Vector2>();

        private int Slot => (int)Projectile.ai[0];
        private int ParentIndex => (int)Projectile.ai[1];
        private int FormationCount => Math.Max(1, (int)Projectile.ai[2]);

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 9999;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            Projectile parent = FindParent();
            if (parent == null)
            {
                Projectile.Kill();
                return;
            }

            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
            {
                Projectile.Kill();
                return;
            }

            if (!initialized)
            {
                initialized = true;
                orbitPhase = 0f;
                Projectile.Center = parent.Center;
            }

            if (hostileHitCooldown > 0)
                hostileHitCooldown--;

            if (hitCount >= 3)
                return;

            CheckHostileProjectiles();
            if (!Projectile.active || hitCount >= 3)
                return;

            int activeCount = GetActiveCoreCount();
            int rank = GetRankAmongActiveCores();

            // Fixed orbital speed. The gap is redistributed automatically
            // when cores disappear.
            float angle =
                orbitPhase +
                Main.GameUpdateCount * 0.018f +
                MathHelper.TwoPi * rank / activeCount;

            Vector2 desired = parent.Center + angle.ToRotationVector2() * radius;
            Vector2 difference = desired - Projectile.Center;

            // Smooth spring-like movement instead of hard snapping.
            Projectile.velocity = Vector2.Lerp(
                Projectile.velocity,
                difference * 0.20f,
                0.14f
            );
            Projectile.Center += Projectile.velocity;

            LordYoyoVisualHelper.CachePosition(afterimages, Projectile.Center, 4f, 24);

            if (Main.myPlayer == Projectile.owner && IsLowestActiveCore())
            {
                attackTimer++;
                if (attackTimer >= 180)
                {
                    attackTimer = 0;
                    TrySharedCoreAttack(parent);
                }
            }

            LordYoyoVisualHelper.AddBlueLight(Projectile.Center, 1.2f);
        }

        private void TrySharedCoreAttack(Projectile parent)
        {
            List<Projectile> cores = new List<Projectile>();
            int lowestWhoAmI = int.MaxValue;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Projectile.owner || p.type != Projectile.type)
                    continue;

                if ((int)p.ai[1] != ParentIndex)
                    continue;

                if (p.ModProjectile is LordYoyoProjC core && core.hitCount < 3)
                {
                    cores.Add(p);
                    lowestWhoAmI = Math.Min(lowestWhoAmI, p.whoAmI);
                }
            }

            if (cores.Count == 0 || Projectile.whoAmI != lowestWhoAmI)
                return;

            Vector2 combined = Vector2.Zero;
            foreach (Projectile core in cores)
            {
                combined += Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
            }

            float sharedAngle = combined.LengthSquared() > 0.001f
                ? combined.ToRotation()
                : Main.rand.NextFloat(MathHelper.TwoPi);

            foreach (Projectile core in cores)
            {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    core.Center,
                    sharedAngle.ToRotationVector2() * 8f,
                    ModContent.ProjectileType<LordYoyoProjPB>(),
                    Math.Max(1, core.damage / 2),
                    core.knockBack,
                    core.owner
                );
            }
        }


        private bool IsLowestActiveCore()
        {
            int lowest = int.MaxValue;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Projectile.owner || p.type != Projectile.type)
                    continue;
                if ((int)p.ai[1] != ParentIndex)
                    continue;
                if (p.ModProjectile is LordYoyoProjC core && core.hitCount < 3)
                    lowest = Math.Min(lowest, p.whoAmI);
            }
            return Projectile.whoAmI == lowest;
        }

        private void CheckHostileProjectiles()
        {
            if (Main.myPlayer != Projectile.owner || hostileHitCooldown > 0)
                return;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile hostile = Main.projectile[i];
                if (!hostile.active || hostile.whoAmI == Projectile.whoAmI || !hostile.hostile || hostile.friendly)
                    continue;

                if (!Projectile.Hitbox.Intersects(hostile.Hitbox))
                    continue;

                hostileHitCooldown = 30; // 0.5 seconds of Core i-frames.
                RegisterHostileHit();
                return;
            }
        }

        public void RegisterHostileHit()
        {
            hitCount++;

            int particleCount = hitCount >= 3 ? 32 : 10;
            for (int i = 0; i < particleCount; i++)
            {
                Vector2 direction = Main.rand.NextVector2Unit();
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    DustID.IceTorch,
                    direction * Main.rand.NextFloat(2f, hitCount >= 3 ? 8f : 4f),
                    70,
                    Color.White,
                    Main.rand.NextFloat(0.7f, hitCount >= 3 ? 1.8f : 1.2f)
                );
                d.noGravity = true;
            }

            if (hitCount >= 3 && Main.myPlayer == Projectile.owner)
            {
                Vector2 deathPosition = Projectile.Center;
                Player ownerPlayer = Main.player[Projectile.owner];
                bool createdDuringTS2 = ownerPlayer != null && ownerPlayer.active &&
                    ownerPlayer.GetModPlayer<LordYoyoPlayerState>().TS2Active;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    deathPosition,
                    Vector2.Zero,
                    ModContent.ProjectileType<LordYoyoProjTEOC>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    ParentIndex,
                    createdDuringTS2 ? 1f : 0f
                );

                Projectile.Kill();
            }
        }

        private int GetActiveCoreCount()
        {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == Projectile.type && (int)p.ai[1] == ParentIndex)
                {
                    if (p.ModProjectile is LordYoyoProjC core && core.hitCount < 3)
                        count++;
                }
            }
            return Math.Max(1, count);
        }

        private int GetRankAmongActiveCores()
        {
            List<int> slots = new List<int>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == Projectile.type && (int)p.ai[1] == ParentIndex)
                {
                    if (p.ModProjectile is LordYoyoProjC core && core.hitCount < 3)
                        slots.Add((int)p.ai[0]);
                }
            }

            slots.Sort();
            int rank = slots.IndexOf(Slot);
            return rank < 0 ? 0 : rank;
        }

        private Projectile FindParent()
        {
            if (ParentIndex < 0 || ParentIndex >= Main.maxProjectiles)
                return null;

            Projectile parent = Main.projectile[ParentIndex];
            if (!parent.active || parent.owner != Projectile.owner || parent.type != ModContent.ProjectileType<LordYoyoProj>())
                return null;

            return parent;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Projectile parent = FindParent();
            if (parent == null)
                return false;

            Texture2D texture = TextureAssets.Projectile[ModContent.ProjectileType<LordYoyoProj>()].Value;
            Vector2 center = Projectile.Center - Main.screenPosition;

            Vector2 start = parent.Center - Main.screenPosition;
            Vector2 end = Projectile.Center - Main.screenPosition;
            Vector2 delta = end - start;
            float length = delta.Length();

            if (length > 2f && length < 1000f)
            {
                Vector2 midpoint = (start + end) * 0.5f;
                Vector2 worldMid = (parent.Center + Projectile.Center) * 0.5f;
                Color light = Lighting.GetColor((int)(worldMid.X / 16f), (int)(worldMid.Y / 16f));
                Color line = Color.Lerp(light, new Color(80, 190, 255), 0.65f);

                Main.spriteBatch.Draw(
                    TextureAssets.MagicPixel.Value,
                    midpoint,
                    new Rectangle(0, 0, 1, 1),
                    line * 0.72f,
                    delta.ToRotation(),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(length, 2.2f),
                    SpriteEffects.None,
                    0f
                );
            }

            LordYoyoVisualHelper.DrawNeonTrail(texture, afterimages, Projectile.rotation, 0.78f, lightColor, Main.screenPosition, 1.2f);
            LordYoyoVisualHelper.DrawNeonSprite(texture, Projectile.Center, Projectile.rotation, 0.78f, lightColor, Main.screenPosition, 1.15f);
            return false;
        }
    }
}
