using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Ice shard fired by IceQueenYoyoProj. Falls with slight gravity, rotation faces velocity,
    /// inflicts Frostburn for 180 ticks.
    /// </summary>
    public class IceQueenYoyoShardProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;

        // M10: frost lift so shards read against night skies.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(190, 230, 255), 0.3f) * Projectile.Opacity;
        }

        public override void SetStaticDefaults()
        {
            // Texture is a vertical strip of 5 shard frames (12x28 each).
            Main.projFrames[Projectile.type] = 5;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 20;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 300;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 5; i++)
                {
                    float angle = MathHelper.TwoPi * i / 5f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, angle.ToRotationVector2() * 2.2f, 100, default, 0.9f);
                    dust.noGravity = true;
                }
                // M11: nose streak thrown ahead along the launch bearing.
                Dust nose = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, Projectile.velocity * 0.35f, 100, default, 0.9f);
                nose.noGravity = true;
                Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1f);
            }

            Projectile.velocity.Y += 0.15f;

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            // Spin personality: 1 of 3 wobbles by slot (visual only).
            Projectile.rotation += (float)System.Math.Sin(Main.GameUpdateCount * 0.2f + Projectile.whoAmI) * 0.04f * ((Projectile.whoAmI % 3) + 1);

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 6)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % 5;
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.8f);

            // Lean crystal trail: one directional mote every 3rd tick (was ~80/s).
            if (Projectile.timeLeft % 3 == 0)
            {
                Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, -Projectile.velocity * 0.06f, 120, default, 0.8f);
                trail.noGravity = true;
                Dust shard = Dust.NewDustDirect(Projectile.Center, 8, 8, ModContent.DustType<global::BiltonsYoyoMod.Content.Dusts.FrostShardDust>(), 0f, -0.5f, 120, default, 0.9f);
                shard.noGravity = true;
            }

            // MAJOR C (falling half): rare ice sparkle settling off the shard.
            if (Main.rand.NextBool(14))
            {
                Dust sparkle = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.IceTorch, new Vector2(0f, -0.5f), 120, default, 0.75f);
                sparkle.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // MAJOR C (facet half): frame-accurate glow whose hue shifts per frame
            // (frameCounter runs on all clients, so the shimmer stays agreed).
            Color facet = Projectile.frame % 2 == 0 ? new Color(180, 235, 255) : new Color(150, 220, 255);
            MiniYoyoFx.DrawGlow(Projectile.Center, 18f, facet, 0.4f);
            // M12: pale core + head tick along the fall line.
            MiniYoyoFx.DrawGlow(Projectile.Center, 10f, new Color(225, 245, 255), 0.4f);
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 head = Projectile.Center - Main.screenPosition;
                Vector2 tail = Projectile.Center - back * 10f - Main.screenPosition;
                MiniYoyoFx.DrawLine(head, tail, 1.5f, new Color(210, 240, 255, 140) * 0.55f);
            }
            // Frame-history ghosts: past crystal poses fading behind the fall.
            if (Projectile.oldPos != null && Projectile.oldPos.Length > 2)
            {
                for (int k = 2; k >= 1; k--)
                {
                    if (k >= Projectile.oldPos.Length)
                        continue;
                    Vector2 oldCenter = Projectile.oldPos[k] + new Vector2(Projectile.width, Projectile.height) * 0.5f;
                    if (oldCenter.LengthSquared() < 1f)
                        continue;
                    Rectangle ghostRect = TextureAssets.Projectile[Projectile.type].Value.Frame(1, 5, 0, Projectile.frame);
                    Main.EntitySpriteDraw(TextureAssets.Projectile[Projectile.type].Value, oldCenter - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                        ghostRect, lightColor * Projectile.Opacity * 0.22f, Projectile.rotation, ghostRect.Size() * 0.5f, Projectile.scale, SpriteEffects.None, 0);
                }
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Frostburn, 180);
            // M13: frost burst at contact (buff kept; dust is new).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust burst = Dust.NewDustPerfect(target.Center, DustID.Frost, angle.ToRotationVector2() * 2.4f, 100, default, 1f);
                burst.noGravity = true;
            }
            Lighting.AddLight(target.Center, 0.4f, 0.7f, 1f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, angle.ToRotationVector2() * 2.4f, 100, default, 0.9f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 3; i++)
            {
                float angle = MathHelper.TwoPi * i / 3f + MathHelper.PiOver4;
                Dust shard = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, angle.ToRotationVector2() * 2.8f, 100, default, 1f);
                shard.noGravity = true;
            }
            // M14: snow that falls and settles off the shatter.
            for (int i = 0; i < 2; i++)
            {
                Dust snow = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 4f), DustID.Frost, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -1f), 120, default, 0.85f);
                snow.noGravity = false;
            }
            Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1f);

            return true;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, angle.ToRotationVector2() * 2f, 120, default, 0.8f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                Dust shatter = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, angle.ToRotationVector2() * 2.8f, 100, default, 1f);
                shatter.noGravity = true;
            }
            // M15: two risers over the shatter + MAJOR C (ground half): flat ring.
            for (int i = 0; i < 2; i++)
            {
                Dust riser = Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, new Vector2(0f, -1.5f), 120, default, 0.85f);
                riser.noGravity = true;
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Vector2 vel = angle.ToRotationVector2() * 2.6f;
                vel.Y *= 0.4f;
                Dust ground = Dust.NewDustPerfect(Projectile.Center, DustID.Frost, vel, 100, default, 0.9f);
                ground.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1f);
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);
        }
    }
}
