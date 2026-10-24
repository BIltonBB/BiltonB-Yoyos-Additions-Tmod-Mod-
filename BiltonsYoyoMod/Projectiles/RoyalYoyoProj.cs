using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    public class RoyalYoyoProj : ModProjectile
    {
        int shootTimer;
        int downwardPullTimer;
        int crushCooldownTimer;
        int crushTimer;
        float trailAlpha;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.scale = 1f;

            // 6f multiplier = 30 seconds lifetime (5s base * 6 = 30s)
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 6f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 160f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 8f;

            // Enable position & rotation trail caching for afterimages
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void AI()
        {
            // Existing Spike Shooting (Every 1 second / 60 ticks)
            shootTimer++;

            if (shootTimer >= 60) // 1 second
            {
                shootTimer = 0;

                if (Main.myPlayer == Projectile.owner)
                {
                    int spikeCount = 10;

                    for (int i = 0; i < spikeCount; i++)
                    {
                        Vector2 velocity = Main.rand.NextVector2Circular(10f, 5f); // x or y

                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            Projectile.Center,
                            velocity,
                            ModContent.ProjectileType<RoyalYoyoSpikesProj>(),
                            20,
                            1f,
                            Projectile.owner
                        );
                    }
                }
            }

            // Cooldown timer tick (600 ticks = 10 seconds)
            if (crushCooldownTimer > 0)
            {
                crushCooldownTimer--;
            }

            // Detect top-to-bottom downward swipe (velocity.Y >= 3.5f)
            if (Projectile.velocity.Y >= 3.5f && crushCooldownTimer == 0 && crushTimer == 0)
            {
                downwardPullTimer++;

                // Triggers after ~0.33s (20 ticks) of downward movement - easily fits in a top-to-bottom swipe
                if (downwardPullTimer >= 20)
                {
                    downwardPullTimer = 0;
                    crushTimer = 120; // Activate 2-second Crush State (120 ticks)
                    crushCooldownTimer = 600; // Start 10-second cooldown
                    trailAlpha = 1f;

                    // Trigger explosion on Crush State START
                    CreateCrushExplosion();

                    Projectile.netUpdate = true;
                }
            }
            else
            {
                downwardPullTimer = 0; // Reset pull timer if downward movement stops
            }

            // Active Crush State Logic
            if (crushTimer > 0)
            {
                crushTimer--;
                trailAlpha = 1f;

                // Trigger explosion when Crush State ENDS naturally
                if (crushTimer == 0)
                {
                    CreateCrushExplosion();
                }
            }
            else if (trailAlpha > 0f)
            {
                // Smoothly fade out trail when Crush state ends
                trailAlpha -= 0.08f;
                if (trailAlpha < 0f)
                    trailAlpha = 0f;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Deal double damage while in Crush state
            if (crushTimer > 0)
            {
                modifiers.SourceDamage *= 2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Instantly cancel Crush state upon hitting an enemy & trigger explosion
            if (crushTimer > 0)
            {
                crushTimer = 0;
                CreateCrushExplosion(); // Trigger explosion on Crush State END (Hit)
            }
        }

        private void CreateCrushExplosion()
        {
            // Play explosion sound effect
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, PitchVariance = 0.2f }, Projectile.Center);

            // Radial burst of gold flame dust particles
            for (int i = 0; i < 25; i++)
            {
                Vector2 speed = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustDirect(Projectile.Center - new Vector2(8, 8), 16, 16, DustID.GoldFlame, speed.X, speed.Y, 100, default, 1.4f);
                dust.noGravity = true;
                dust.velocity *= 1.2f;
            }

            // Ring of sparkling star dust
            for (int i = 0; i < 12; i++)
            {
                Vector2 speed = Main.rand.NextVector2CircularEdge(5f, 5f);
                Dust dust = Dust.NewDustDirect(Projectile.Center - new Vector2(8, 8), 16, 16, DustID.YellowStarDust, speed.X, speed.Y, 100, default, 1.2f);
                dust.noGravity = true;
            }

            // Warm gold light burst
            Lighting.AddLight(Projectile.Center, 1f, 0.85f, 0.2f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() * 0.5f;

            // Render distance-based afterimages & dark shadows during Crush state
            if (trailAlpha > 0f)
            {
                for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;

                    Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                    float distFromCurrent = Vector2.Distance(Projectile.Center, oldCenter);

                    // Skip afterimages if position hasn't moved far enough
                    if (distFromCurrent < 4f)
                        continue;

                    Vector2 drawPos = oldCenter - Main.screenPosition;

                    // Opacity fades along trail & scales with distance moved
                    float progress = (float)i / Projectile.oldPos.Length;
                    float distanceFade = MathHelper.Clamp(distFromCurrent / 120f, 0.2f, 1f);
                    float alpha = (1f - progress) * 0.65f * trailAlpha * distanceFade;

                    float rotation = Projectile.oldRot[i];

                    // 1. Dark Shadow Layer (rendered underneath, reacts to night lighting)
                    Color shadowColor = Color.Black * alpha * 0.45f;
                    Vector2 shadowOffset = new Vector2(2f, 2f);
                    Main.EntitySpriteDraw(texture, drawPos + shadowOffset, null, shadowColor, rotation, drawOrigin, Projectile.scale * 1.05f, SpriteEffects.None, 0);

                    // 2. Royal Gold Afterimage Trail (blends with ambient lightColor)
                    Color trailColor = Color.Lerp(lightColor, new Color(255, 215, 0), 0.6f) * alpha;
                    Main.EntitySpriteDraw(texture, drawPos, null, trailColor, rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            return true; // Draw standard yoyo sprite on top
        }
    }
}
