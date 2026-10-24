using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace BiltonsYoyoMod.Projectiles
{
    public class EyeYoyoProj : ModProjectile
    {
        int dashCooldownTimer;
        int dashTimer;
        float trailAlpha; // Smoothly fades out afterimages after dash ends
        Vector2 dashDirection;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 10;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.scale = 1f;

            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 8f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 150f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 10f;

            // Enable position & rotation trail caching
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void AI()
        {
            // Cooldown Timer (5 seconds = 300 ticks)
            dashCooldownTimer++;

            if (dashCooldownTimer >= 300)
            {
                dashCooldownTimer = 0;

                if (Main.myPlayer == Projectile.owner)
                {
                    NPC target = FindNearestEnemy(5000f);
                    if (target != null)
                    {
                        dashTimer = 60; // 1 second dash (60 ticks)
                        dashDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                        trailAlpha = 1f;
                        Projectile.netUpdate = true;
                    }
                }
            }

            // Dashing State
            if (dashTimer > 0)
            {
                dashTimer--;
                trailAlpha = 1f;

                // Smoothly home towards target so it doesn't overshoot
                NPC target = FindNearestEnemy(500f);
                if (target != null)
                {
                    Vector2 targetDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    dashDirection = Vector2.Normalize(Vector2.Lerp(dashDirection, targetDir, 0.15f));
                }

                if (dashDirection != Vector2.Zero)
                {
                    Projectile.velocity = dashDirection * 26f;
                }

                // Prevent Vanilla Yoyo AI from force-retracting during dash
                if (Projectile.ai[0] < 0f)
                {
                    Projectile.ai[0] = 0f;
                }

                // When dash naturally finishes (timer hits 0), dampen velocity to stop dancing
                if (dashTimer == 0)
                {
                    Projectile.velocity *= 0.1f;
                }

                // Dark red dash dust trail
                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 100, default, 1.2f);
                    dust.noGravity = true;
                    dust.velocity *= 0.3f;
                }
            }
            else if (trailAlpha > 0f)
            {
                // Smoothly fade out trail over ~12 frames after dash ends or on hit
                trailAlpha -= 0.08f;
                if (trailAlpha < 0f)
                    trailAlpha = 0f;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Deal double damage while dashing
            if (dashTimer > 0)
            {
                modifiers.SourceDamage *= 2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Cancel dash on hit & restore standard yoyo control smoothly
            if (dashTimer > 0)
            {
                dashTimer = 0;
                Projectile.velocity *= 0.1f; // Instantly dampens speed so it doesn't dance around cursor
                Projectile.ai[0] = 0f; // Return control back to mouse
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() * 0.5f;

            // Render distance-based afterimages & dark shadows while trailAlpha > 0
            if (trailAlpha > 0f)
            {
                for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;

                    Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                    float distFromCurrent = Vector2.Distance(Projectile.Center, oldCenter);

                    // Skip drawing afterimages that haven't moved far enough (distance-based threshold)
                    if (distFromCurrent < 4f)
                        continue;

                    Vector2 drawPos = oldCenter - Main.screenPosition;

                    // Fade trail opacity based on position in trail & overall distance moved
                    float progress = (float)i / Projectile.oldPos.Length;
                    float distanceFade = MathHelper.Clamp(distFromCurrent / 120f, 0.2f, 1f);
                    float alpha = (1f - progress) * 0.6f * trailAlpha * distanceFade;

                    float rotation = Projectile.oldRot[i];

                    // 1. Dark Shadow Layer (rendered underneath, reacts to night lighting)
                    Color shadowColor = Color.Black * alpha * 0.45f;
                    Vector2 shadowOffset = new Vector2(2f, 2f); // Subtle offset for depth
                    Main.EntitySpriteDraw(texture, drawPos + shadowOffset, null, shadowColor, rotation, drawOrigin, Projectile.scale * 1.05f, SpriteEffects.None, 0);

                    // 2. Main Red Shadow Trail (blends with ambient lightColor so it's not blindingly bright at night)
                    Color trailColor = Color.Lerp(lightColor, new Color(180, 40, 40), 0.5f) * alpha;
                    Main.EntitySpriteDraw(texture, drawPos, null, trailColor, rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            return true; // Draw main yoyo sprite on top
        }

        private NPC FindNearestEnemy(float maxRange)
        {
            NPC nearestNPC = null;
            float maxRangeSq = maxRange * maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy())
                {
                    float distanceSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                    if (distanceSq <= maxRangeSq)
                    {
                        maxRangeSq = distanceSq;
                        nearestNPC = npc;
                    }
                }
            }

            return nearestNPC;
        }
    }
}
