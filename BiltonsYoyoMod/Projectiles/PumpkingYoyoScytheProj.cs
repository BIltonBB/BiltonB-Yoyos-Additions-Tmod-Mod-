using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class PumpkingYoyoScytheProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;
        private readonly List<Vector2> reaping = new List<Vector2>();

        // M12: harvest lift so the reaping reads in dark fields.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(190, 240, 190), 0.3f) * Projectile.Opacity;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 300;

            Projectile.scale = 0.4f;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void AI()
        {
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                for (int i = 0; i < 6; i++)
                {
                    float angle = MathHelper.TwoPi * i / 6f;
                    int pick = i % 3;
                    int spawnType = pick == 0 ? DustID.GoldFlame : pick == 1 ? DustID.OrangeTorch : DustID.PurpleTorch;
                    Dust fx = Dust.NewDustPerfect(Projectile.Center, spawnType, angle.ToRotationVector2() * 2.6f, 0, default, 1f);
                    fx.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.75f, 0.3f);
            }

            Projectile.velocity *= 0.985f;
            if (Projectile.velocity.Length() < 1f)
            {
                Projectile.Kill();
                return;
            }

            // Shepherd's bend: up to 2 degrees per tick toward a nearby enemy.
            // Launches still read random; the reaping just connects more often.
            float speed = Projectile.velocity.Length();
            NPC shepherd = null;
            float bestSq = 600f * 600f;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (MiniYoyoFx.IsAttackable(npc))
                {
                    float dSq = Vector2.DistanceSquared(Projectile.Center, npc.Center);
                    if (dSq < bestSq)
                    {
                        bestSq = dSq;
                        shepherd = npc;
                    }
                }
            }
            if (shepherd != null)
            {
                float cur = Projectile.velocity.ToRotation();
                float want = (shepherd.Center - Projectile.Center).ToRotation();
                float turn = MathHelper.Clamp(MathHelper.WrapAngle(want - cur), MathHelper.ToRadians(-2f), MathHelper.ToRadians(2f));
                Projectile.velocity = (cur + turn).ToRotationVector2() * speed;
            }

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, 1f, 0.7f, 0.25f);

            // MAJOR C (ribbon half): speed-reactive reaping ribbon (all clients).
            MiniYoyoFx.RibbonPush(reaping, Projectile.Center, 8f, 12);

            // M13: death-rattle wisps as the spin bleeds out toward the pop.
            if (speed < 3f && Main.GameUpdateCount % 3 == 0)
            {
                Dust rattle = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.GoldFlame, new Vector2(0f, -1f), 0, default, 0.9f);
                rattle.noGravity = true;
            }

            // Trail thins as she slows: fast streaks, dying wisps.
            // (Gated by speed so the lantern fade reads instead of fizzling.)
            if (speed > 6f ? Projectile.timeLeft % 2 == 0 : Main.GameUpdateCount % 4 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, -Projectile.velocity * 0.08f, 0, default, speed > 6f ? 0.9f : 0.7f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                int popType = i % 3 == 0 ? DustID.GoldFlame : i % 3 == 1 ? DustID.OrangeTorch : DustID.PurpleTorch;
                Dust pop = Dust.NewDustPerfect(Projectile.Center, popType, angle.ToRotationVector2() * 2.8f, 0, default, 1f);
                pop.noGravity = true;
            }
            // MAJOR C (pop half): pale reaping sparks + grave-smoke ring over the pop.
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                Dust pale = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, angle.ToRotationVector2() * 3.2f, 0, default, 1f);
                pale.noGravity = true;
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, angle.ToRotationVector2() * 2f, 0, default, 1f);
                ring.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.75f, 0.3f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() * 0.5f;

            // MAJOR C (wisp half): reaping ribbon + will-o'-wisp escorts flanking
            // the spin, pulsing wider as she slows into the lantern.
            float speedNow = Projectile.velocity.Length();
            MiniYoyoFx.RibbonDrawSoft(reaping, new Color(150, 95, 35), new Color(255, 225, 170), MathHelper.Clamp(3f + speedNow * 0.2f, 3f, 6f));
            Vector2 fwd = speedNow * speedNow > 0.01f ? Projectile.velocity.SafeNormalize(Vector2.UnitY) : Vector2.UnitY;
            Vector2 flank = new Vector2(-fwd.Y, fwd.X);
            float wispSpread = 10f + 8f * MathHelper.Clamp(1f - speedNow / 13f, 0f, 1f);
            MiniYoyoFx.DrawGlow(Projectile.Center + flank * wispSpread, 10f, new Color(255, 205, 130), 0.35f);
            MiniYoyoFx.DrawGlow(Projectile.Center - flank * wispSpread, 10f, new Color(255, 205, 130), 0.35f);
            // Harvest crescent: short fading slash trailing the spin, breathing with speed.
            // Spin blur: perpendicular speed streaks, longer when she spins fast.
            if (speedNow * speedNow > 1f)
            {
                Vector2 blurBack = -fwd * (10f + speedNow * 0.9f);
                Vector2 blurSide = flank * 6f;
                Vector2 c0 = Projectile.Center - Main.screenPosition;
                MiniYoyoFx.DrawLine(c0, c0 + (blurBack + blurSide) * 0.5f, 2f, new Color(255, 220, 160, 120) * 0.5f);
                MiniYoyoFx.DrawLine(c0, c0 + (blurBack - blurSide) * 0.5f, 2f, new Color(255, 220, 160, 120) * 0.5f);
            }
            if (speedNow * speedNow > 1f)
            {
                for (int i = 0; i < 8; i++)
                {
                    float ct = (float)i / 8f;
                    Vector2 cp = Projectile.Center - fwd * (6f + ct * 22f * MathHelper.Clamp(speedNow / 13f, 0.3f, 1f));
                    MiniYoyoFx.DrawGlow(cp, 10f * (1f - ct) + 3f, new Color(255, 205, 130), 0.35f * (1f - ct));
                }
            }

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;
                Vector2 oldCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f;
                float dist = Vector2.Distance(Projectile.Center, oldCenter);
                if (dist < 4f)
                    continue;
                Vector2 drawPos = oldCenter - Main.screenPosition;
                float progress = (float)i / Projectile.oldPos.Length;
                float distanceFade = MathHelper.Clamp(dist / 120f, 0.2f, 1f);
                float alpha = (1f - progress) * 0.6f * distanceFade;

                Color tileLight = Lighting.GetColor((int)(oldCenter.X / 16f), (int)(oldCenter.Y / 16f));
                Color shadowColor = Color.Black * alpha * 0.45f;
                Main.EntitySpriteDraw(texture, drawPos + new Vector2(2f, 2f), null, shadowColor, Projectile.oldRot[i], drawOrigin, Projectile.scale, SpriteEffects.None, 0);

                Color trailColor = Color.Lerp(tileLight, new Color(230, 170, 80), 0.6f) * alpha;
                Main.EntitySpriteDraw(texture, drawPos, null, trailColor, Projectile.oldRot[i], drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);

            // Dying lantern: as speed bleeds out, a gold bloom swells around
            // the streak, then pops at Kill. Fast scythes show almost none.
            float lantern = MathHelper.Clamp(1f - Projectile.velocity.Length() / 13f, 0f, 1f);
            if (lantern > 0.05f)
                MiniYoyoFx.DrawGlow(Projectile.Center, 14f + 26f * lantern, new Color(255, 200, 120), 0.25f + 0.3f * lantern);
            return false;
        }
    }
}
