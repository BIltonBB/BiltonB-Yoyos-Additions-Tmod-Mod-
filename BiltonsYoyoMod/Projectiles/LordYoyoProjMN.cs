using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using System;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjMN : ModProjectile
    {
        private const int GrowthDuration = 180;
        private const int BarrageDuration = 300;
        private const int TotalDuration = 540;
        private readonly List<Vector2> afterimages = new List<Vector2>();
        private int timer;

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration + 10;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            timer++;

            Projectile parent = FindParent();
            if (parent != null && timer < GrowthDuration)
                Projectile.Center = Vector2.Lerp(Projectile.Center, parent.Center, 0.22f);

            float growth = MathHelper.Clamp(timer / (float)GrowthDuration, 0f, 1f);
            Projectile.scale = MathHelper.SmoothStep(0.04f, 1f, growth);

            // Simple static-mode moon particles: small icy dust around the moon.
            if (timer >= GrowthDuration && Main.rand.NextBool(2))
            {
                Vector2 direction = Main.rand.NextVector2Unit();
                Vector2 particlePosition = Projectile.Center + direction * Main.rand.NextFloat(90f, 135f);

                Dust dust = Dust.NewDustPerfect(
                    particlePosition,
                    DustID.IceTorch,
                    -direction * Main.rand.NextFloat(0.05f, 0.25f) + Main.rand.NextVector2Circular(0.25f, 0.25f),
                    110,
                    Color.White,
                    Main.rand.NextFloat(0.7f, 1.15f)
                );
                dust.noGravity = true;
            }

            LordYoyoVisualHelper.CachePosition(afterimages, Projectile.Center, 4f, 24);
            LordYoyoVisualHelper.AddBlueLight(Projectile.Center, 1.8f);

            // Five seconds of barrage, one wave every 0.1 seconds.
            if (timer >= GrowthDuration && timer < GrowthDuration + BarrageDuration && Main.myPlayer == Projectile.owner && timer % 6 == 0)
            {
                int wave = (timer - GrowthDuration) / 6;
                float baseAngle = MathHelper.ToRadians(wave);

                for (int i = 0; i < 12; i++)
                {
                    Vector2 direction = (baseAngle + MathHelper.TwoPi * i / 12f).ToRotationVector2();
                    Vector2 spawnPosition = Projectile.Center + direction * 128f;
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        spawnPosition,
                        direction * 7f,
                        ModContent.ProjectileType<LordYoyoProjPB>(),
                        Math.Max(1, Projectile.damage / 2),
                        Projectile.knockBack,
                        Projectile.owner
                    );
                }
            }

            if (timer >= TotalDuration)
                Projectile.Kill();
        }

        private Projectile FindParent()
        {
            int index = (int)Projectile.ai[0];
            if (index < 0 || index >= Main.maxProjectiles)
                return null;

            Projectile parent = Main.projectile[index];
            if (!parent.active || parent.owner != Projectile.owner || parent.type != ModContent.ProjectileType<LordYoyoProj>())
                return null;

            return parent;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>("BiltonsYoyoMod/Projectiles/LordYoyoProjMN").Value;
            LordYoyoVisualHelper.DrawNeonTrail(texture, afterimages, 0f, Math.Max(0.04f, Projectile.scale * 0.95f), lightColor, Main.screenPosition, 0.9f);

            Color local = Lighting.GetColor(
                (int)(Projectile.Center.X / 16f),
                (int)(Projectile.Center.Y / 16f)
            );

            Color moonColor = Color.Lerp(
                local,
                Color.White,
                0.25f
            );

            // One actual moon sprite: no large blue duplicate overlay.
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                moonColor,
                0f,
                texture.Size() * 0.5f,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // 256px-diameter icy explosion: 128px outer radius.
            const float outerRadius = 128f;
            const float innerRadius = 64f;

            // Outer ring.
            for (int i = 0; i < 160; i++)
            {
                float angle = MathHelper.TwoPi * i / 160f;
                Vector2 radial = angle.ToRotationVector2();

                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + radial * outerRadius,
                    DustID.IceTorch,
                    radial * Main.rand.NextFloat(2f, 5.5f),
                    60,
                    Color.White,
                    Main.rand.NextFloat(0.9f, 1.5f)
                );

                d.noGravity = true;
            }

            // Inner ring keeps the center from looking hollow after enlarging
            // the effect, without simply scaling the previous particles.
            for (int i = 0; i < 96; i++)
            {
                float angle = MathHelper.TwoPi * i / 96f;
                Vector2 radial = angle.ToRotationVector2();

                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + radial * innerRadius,
                    DustID.BlueTorch,
                    radial * Main.rand.NextFloat(1.5f, 4.5f),
                    70,
                    Color.White,
                    Main.rand.NextFloat(0.7f, 1.25f)
                );

                d.noGravity = true;
            }

            // A compact core burst.
            for (int i = 0; i < 48; i++)
            {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(24f, 24f),
                    DustID.IceTorch,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f),
                    70,
                    Color.White,
                    Main.rand.NextFloat(0.7f, 1.2f)
                );

                d.noGravity = true;
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 1.1f, 2.0f);
        }
    }
}
