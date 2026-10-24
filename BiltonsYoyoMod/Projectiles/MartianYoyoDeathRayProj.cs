using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

using BiltonsYoyoMod.Content.Systems;
using BiltonsYoyoMod.Content.Projectiles.Tech;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Saucer death ray: long teal beam (drawn with Beam v2: hard slab body,
    /// soft muzzle) that rides the yoyo it is anchored to and sweeps with it.
    /// Damage is a LINE along the whole 800px ray - the beam damages what it
    /// crosses, not just the point the yoyo sits on. Inflicts Electrified for
    /// 120 ticks.
    /// </summary>
    public class MartianYoyoDeathRayProj : ModProjectile, IYoyoInstantHit
    {
        private const float BeamLength = 800f;
        private bool spawnFxDone;
        private float grow; // beam v2 charge-in factor 0->1

        // Drawn as its own muzzle bloom (see PreDraw) instead of the old sprite.
        // hide=true so the 64x64 MiniYoyoGlow disc is NOT drawn a second time under
        // the beam (Vexel B1: PreDraw returned true with no hide, doubling the glow).
        public override string Texture => "BiltonsYoyoMod/Projectiles/MiniYoyoGlow";

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 150;
            // Vanilla sprite never draws: texture overridden to the soft glow below,
            // so the beam origin blooms instead of showing the old orange chunk.

            Projectile.scale = 0.6f;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            // Yoyo-attached beam: snap to the yoyo every tick so the beam sweeps
            // as the player moves it. No travelling sprite.
            int yoyoIndex = (int)Projectile.ai[0];
            if (yoyoIndex < 0 || yoyoIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile yoyo = Main.projectile[yoyoIndex];
            // Whatever it is anchored to has to be a deployed WEAPON of the same
            // owner: the real yoyo, or a tech shell standing in for it. This used
            // to demand MartianYoyoProj by type, which killed the ray on its
            // first tick the moment anything else held it - the whole reason a
            // beam could flash into existence and never hit anything.
            bool anchored = yoyo.active
                && yoyo.owner == Projectile.owner
                && (yoyo.aiStyle == ProjAIStyleID.Yoyo || yoyo.ModProjectile is TechProjBase);
            if (!anchored)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = yoyo.Center;
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = Projectile.ai[1];

            Vector2 beamDir = Projectile.ai[1].ToRotationVector2();

            // Spawn flash: tight charge + ignite crackle (was 12 random).
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                Lighting.AddLight(Projectile.Center, 0.5f, 1.5f, 1.2f);
                Vector2 dir0 = beamDir;
                for (int i = 0; i < 5; i++)
                {
                    float angle = MathHelper.TwoPi * i / 5f;
                    Vector2 pos = Projectile.Center + dir0 * Main.rand.NextFloat(0f, 100f);
                    Dust dust = Dust.NewDustPerfect(pos, DustID.Electric, angle.ToRotationVector2() * 2f, 100, default, 1f);
                    dust.noGravity = true;
                }
                for (int i = 0; i < 3; i++)
                {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, Main.rand.NextVector2Circular(2f, 2f), 100, default, 1f);
                    dust.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
                MetaballSystem.SpawnMetaball(Projectile.Center, beamDir * 2f, 1.2f, 0.8f, 30, new Color(140, 255, 200));
                // Screen shockwave (L5): the ray igniting cracks the frame once.
                YoyoScreenShockwaveSystem.Trigger(Projectile.Center, 0.3f, new Color(140, 255, 200));
            }

            // Beam v2 charge-in: grow out over 8 ticks, collapse over the last 8.
            grow = MathHelper.Clamp((150 - Projectile.timeLeft) / 8f, 0f, 1f);
            if (Projectile.timeLeft < 8)
                grow = MathHelper.Min(grow, Projectile.timeLeft / 8f);

            Lighting.AddLight(Projectile.Center, 0.2f, 1.0f, 0.8f);

            Vector2 dir = beamDir;
            float liveLen = BeamLength * MathHelper.Max(grow, 0.05f);

            // Lean beam dust: one crackle per tick along the ray (was 2.5/tick).
            {
                Vector2 pos = Projectile.Center + dir * Main.rand.NextFloat(0f, liveLen);
                Dust dust = Dust.NewDustPerfect(pos, DustID.Electric, -dir * 2f, 120, default, 0.8f);
                dust.noGravity = true;
            }

            // MAJOR B (arc half): side arcs bleeding off the ray.
            // Ray impact: pooled burst where the beam ends, every 15 ticks.
            if (Projectile.timeLeft % 15 == 0)
            {
                Vector2 end = Projectile.Center + dir * liveLen;
                YoyoParticles.SpawnBloom(end, 6f, 26f, 20, new Color(100, 255, 210), 0.5f);
                YoyoParticles.SpawnBurst(end, 5, 3f, new Color(150, 255, 220), 0.9f, 20);
            }
            if (Main.rand.NextBool(6))
            {
                Vector2 pos = Projectile.Center + dir * Main.rand.NextFloat(60f, liveLen);
                Vector2 side = new Vector2(-dir.Y, dir.X);
                Dust arc = Dust.NewDustPerfect(pos, DustID.MagicMirror, side * Main.rand.NextFloat(-3f, 3f), 100, default, 0.9f);
                arc.noGravity = true;
            }

            if (Main.rand.NextBool(8))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, Main.rand.NextVector2Circular(2f, 2f), 100, default, 1f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Tight beam collapse (was 18 random): sparks along base + crisp ring.
            Lighting.AddLight(Projectile.Center, 0.5f, 1.4f, 1.1f);
            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            for (int i = 0; i < 7; i++)
            {
                Vector2 pos = Projectile.Center + dir * Main.rand.NextFloat(0f, 160f);
                Dust dust = Dust.NewDustPerfect(pos, DustID.Electric, new Vector2(0f, -1f), 120, default, 0.9f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 5; i++)
            {
                float angle = MathHelper.TwoPi * i / 5f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, angle.ToRotationVector2() * 3.4f, 100, default, 1f);
                dust.noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            // True beam collision along the full 800px ray, not just the origin.
            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(
                targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + dir * BeamLength, 12f, ref collisionPoint);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Electrified, 120);
            // Abduction: ray kills lift a green wisp skyward off the victim.
            if (target.life <= 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    Dust wisp = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Electric, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-4f, -2.5f)), 100, default, 1f);
                    wisp.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 dir = Projectile.ai[1].ToRotationVector2();
            float width = 6f + (float)Math.Sin(Main.GameUpdateCount * 0.5f + Projectile.whoAmI) * 2f;
            Vector2 start = Projectile.Center;
            Vector2 end = Projectile.Center + dir * BeamLength;

            // Muzzle bloom.
            MiniYoyoFx.DrawGlowAdditive(Projectile.Center, 28f, new Color(0, 255, 200), 0.4f);

            // Beam v2 (P1.3): charge-in grow + striated flow + end-cap cross-flare.
            MiniYoyoFx.DrawBeamV2(start, end, new Color(220, 255, 245), new Color(0, 255, 200), width, grow, 1.4f);

            return false;
        }

    }
}
