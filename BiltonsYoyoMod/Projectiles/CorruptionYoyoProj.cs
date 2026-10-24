using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    public class CorruptionYoyoProj : ModProjectile
    {
        private int shootTimer;
        private Vector2 lastTrailPosition; // Stores the last position where a trail projectile spawned

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 8f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 170f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 10f;

            // Enable position & rotation trail caching for afterimages
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void AI()
        {
            shootTimer++;

            // Shoot spikes every 0.25s (15 ticks)
            if (shootTimer >= 15)
            {
                shootTimer = 0;
                if (Main.myPlayer == Projectile.owner)
                {
                    // Random directional velocity with speed varied between 5f and 12f (less aggressive, no mach speed)
                    Vector2 spikeVelocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(5f, 12f);

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        spikeVelocity,
                        ModContent.ProjectileType<CorruptionYoyoSpikesProj>(),
                        5,
                        1f,
                        Projectile.owner
                    );
                }
            }

            // Distance-based trail projectile spawning (spawns every 16 pixels moved)
            if (lastTrailPosition == Vector2.Zero || Vector2.Distance(Projectile.Center, lastTrailPosition) >= 16f)
            {
                lastTrailPosition = Projectile.Center;

                if (Main.myPlayer == Projectile.owner)
                {
                    Projectile trail = Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<CorruptionYoyoProjTrail>(),
                        15,
                        1f,
                        Projectile.owner
                    );
                    trail.timeLeft = 60; // 1 second
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.CursedInferno, 180); // 3s debuff
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() * 0.5f;

            // Distance-based afterimages with dark shadows
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                float distFromCurrent = Vector2.Distance(Projectile.Center, oldCenter);

                // Skip drawing afterimages if the yoyo hasn't moved far enough
                if (distFromCurrent < 4f)
                    continue;

                Vector2 drawPos = oldCenter - Main.screenPosition;

                // Opacity fades along the trail and scales with movement distance
                float progress = (float)i / Projectile.oldPos.Length;
                float distanceFade = MathHelper.Clamp(distFromCurrent / 100f, 0.2f, 1f);
                float alpha = (1f - progress) * 0.65f * distanceFade;

                float rotation = Projectile.oldRot[i];

                // 1. Dark Shadow Layer (rendered behind, reacts to environment lighting)
                Color shadowColor = Color.Black * alpha * 0.5f;
                Vector2 shadowOffset = new Vector2(2f, 2f);
                Main.EntitySpriteDraw(texture, drawPos + shadowOffset, null, shadowColor, rotation, drawOrigin, Projectile.scale * 1.05f, SpriteEffects.None, 0);

                // 2. Corruption Purple/Cursed Afterimage Trail (blends with ambient lightColor for night lighting)
                Color trailColor = Color.Lerp(lightColor, new Color(130, 45, 190), 0.55f) * alpha;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            return true; // Draw standard yoyo sprite on top
        }
    }
}
