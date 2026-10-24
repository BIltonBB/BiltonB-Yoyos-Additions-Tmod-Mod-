using Terraria;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProjPS : ModProjectile
    {
        private readonly List<Vector2> afterimages = new List<Vector2>();

        private int GroupId => (int)Projectile.ai[0];
        private int ReleaseTick => (int)Projectile.ai[1];

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
        }

        public override void AI()
        {
            // Setup: each PS is launched outward randomly, then decelerates to a stop.
            if (Main.GameUpdateCount < ReleaseTick)
            {
                Projectile.velocity *= 0.92f;
                if (Projectile.velocity.LengthSquared() < 0.01f)
                    Projectile.velocity = Vector2.Zero;
            }
            else
            {
                // One projectile in the group chooses the shared firing direction.
                // Every member then uses that exact same direction.
                EnsureSharedGroupDirection();

                if (Projectile.ai[2] >= 0f)
                    Projectile.velocity = Projectile.ai[2].ToRotationVector2() * 4f;
                else
                    Projectile.velocity = Vector2.Zero;
            }

            CacheAndLight();
        }

        private void EnsureSharedGroupDirection()
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            int leaderWhoAmI = int.MaxValue;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile other = Main.projectile[i];
                if (!other.active ||
                    other.owner != Projectile.owner ||
                    other.type != Projectile.type)
                    continue;

                if ((int)other.ai[0] != GroupId)
                    continue;

                leaderWhoAmI = Math.Min(leaderWhoAmI, other.whoAmI);
            }

            if (leaderWhoAmI == int.MaxValue)
                return;

            if (Projectile.whoAmI == leaderWhoAmI && Projectile.ai[2] < 0f)
            {
                float sharedAngle = Main.rand.NextFloat(MathHelper.TwoPi);

                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile other = Main.projectile[i];
                    if (!other.active ||
                        other.owner != Projectile.owner ||
                        other.type != Projectile.type)
                        continue;

                    if ((int)other.ai[0] != GroupId)
                        continue;

                    other.ai[2] = sharedAngle;
                    other.netUpdate = true;
                }
            }
        }

        private void CacheAndLight()
        {
            LordYoyoVisualHelper.CachePosition(
                afterimages,
                Projectile.Center,
                4f,
                24
            );

            LordYoyoVisualHelper.AddBlueLight(
                Projectile.Center,
                1.25f
            );
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture =
                TextureAssets.Projectile[Projectile.type].Value;

            LordYoyoVisualHelper.DrawNeonTrail(
                texture,
                afterimages,
                Projectile.rotation,
                0.72f,
                lightColor,
                Main.screenPosition,
                1.65f
            );

            LordYoyoVisualHelper.DrawNeonSprite(
                texture,
                Projectile.Center,
                Projectile.rotation,
                0.72f,
                lightColor,
                Main.screenPosition,
                1.55f
            );

            return false;
        }
    }
}
