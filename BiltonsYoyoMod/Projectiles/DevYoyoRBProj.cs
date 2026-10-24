using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    /// <summary>
    /// Owner-local camera premiere state for the Rude Buster. The zoom is a real
    /// GameViewMatrix zoom (verified get/set in the tModLoader docs), captured from
    /// the live value at premiere start and restored at the end, on kill, and via
    /// the DevZoomPlayer safety net — so it can never stick, and remotes are never
    /// touched (everything here runs owner-client only).
    /// </summary>
    internal static class DevZoomHelper
    {
        public static bool Active;
        public static int Owner = -1;
        public static Vector2 Base = Vector2.One;

        public static void Begin(int owner)
        {
            if (Active)
                return;
            Active = true;
            Owner = owner;
            Base = Main.GameViewMatrix.Zoom;
        }

        public static void Apply(float ease)
        {
            if (!Active)
                return;
            Main.GameViewMatrix.Zoom = Base * (1f + 0.25f * ease);
        }

        public static void End()
        {
            if (!Active)
                return;
            Main.GameViewMatrix.Zoom = Base;
            Active = false;
            Owner = -1;
        }

        public static void SafetyRestore(Player player)
        {
            if (!Active || Owner != player.whoAmI)
                return;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && p.ModProjectile is DevYoyoRBProj rb && rb.ZoomRunning)
                    return;
            }
            End();
        }

        public static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// Goofy rainbow projectile thrown by DeveloperYoyoProj. Deals ZERO damage but has
    /// extreme knockback (20). Rude Buster premiere on spawn (owner client only): eased
    /// zoom-in with fade-in (15 ticks) -> hold with ramped shake (20 ticks) -> eased
    /// zoom-out with fade-out (25 ticks), plus the confetti screen flood and punch sound.
    ///
    /// Camera safety: zoom/shake run owner-client only; the base zoom is captured at
    /// premiere start and restored at premiere end, in OnKill, and by the DevZoomPlayer
    /// safety net. Main.screenPosition is rebuilt from the player every frame anyway,
    /// so the shake can never persist either. The giant DevYoyoRBProj.png atlas is NOT
    /// used: Texture is overridden to the small MartianYoyoLaserProj texture instead.
    /// </summary>
    public class DevYoyoRBProj : ModProjectile, IYoyoInstantHit
    {
        public override string Texture => "BiltonsYoyoMod/Projectiles/MartianYoyoLaserProj";

        private const int ZoomIn = 15;   // fade-in: zoom 1.0 -> 1.25x, shake ramps in
        private const int ZoomHold = 35; // hold peak with full shake
        private const int ZoomEnd = 60;  // fade-out: zoom back + shake out, then restore

        private bool spawnFxDone;
        private int zoomT = -1; // -1 = no premiere (remote clients stay here)
        private readonly List<Vector2> rainbowTrail = new List<Vector2>();

        // Read by the DevZoomPlayer safety net (same assembly).
        internal bool ZoomRunning => zoomT >= 0 && zoomT < ZoomEnd;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;

            Projectile.friendly = true;
            Projectile.hostile = false;

            // Pierce 5: one bolt shreds a small crowd, then it pops.
            Projectile.penetrate = 5;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 200;

            // Big goofy bolt (hitbox untouched — scale is draw-only).
            Projectile.scale = 2.2f;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            // Like every other yoyo child that must hit point-blank
            // (MartianYoyoLaserProj, BetsyYoyoFireBallProj): instant hits +
            // local immunity. Without this the spawn-grace makes the bolt
            // harmless for its first second, so it only ever pops on walls.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M10: goofy white lift so the rainbow bolt pops at night.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, Color.White, 0.3f) * Projectile.Opacity;
        }

        public override void AI()
        {
            // Near-zero damage contract: real damage 1 (so hits connect and
            // knockback applies) with the "1" number hidden in OnHitNPC.
            // Pierce 5: shreds a small crowd, local immunity throttles re-hits.
            Projectile.damage = 1;
            Projectile.knockBack = 20f;

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Color disco = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            Lighting.AddLight(Projectile.Center, disco.R / 255f, disco.G / 255f, disco.B / 255f);

            MiniYoyoFx.RibbonPushLegacy(rainbowTrail, Projectile.Center, 8f, 10); // dev yoyo: tight 8 px history points

            // Lean rainbow trail: every other tick + confetti (was ~80/s).
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.RainbowMk2, -Projectile.velocity * 0.08f, 100, default, 0.85f);
                dust.noGravity = true;
            }
            if (Main.rand.NextBool(6))
            {
                // M15: deterministic confetti/rainbow alternation (same rate, more parade).
                int glitterType = Projectile.timeLeft % 4 < 2 ? DustID.Confetti : DustID.RainbowMk2;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, glitterType, Main.rand.NextVector2Circular(1f, 1f), 100, glitterType == DustID.Confetti ? disco : default, 0.9f);
                dust.noGravity = true;
            }
            // (rainbow lane bloom removed - was a growing/shrinking circle)

            // First-tick punch burst (dust/sound run per-client; shake + zoom are owner-only).
            // Tightened: screen flood 40->24 + center ring (was 64 dust at once).
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                Projectile.localAI[0] = 1f;
                Projectile.localAI[1] = 20f; // shake ticks

                Lighting.AddLight(Projectile.Center, 1.2f, 1.2f, 1.2f);
                Vector2 screenCenter = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
                for (int i = 0; i < 24; i++)
                {
                    float angle = MathHelper.TwoPi * i / 24f;
                    Dust dust = Dust.NewDustPerfect(screenCenter + angle.ToRotationVector2() * Main.rand.NextFloat(60f, 180f), i % 2 == 0 ? DustID.RainbowMk2 : DustID.Confetti, angle.ToRotationVector2() * 2f, 100, default, 1.1f);
                    dust.noGravity = true;
                }
                for (int i = 0; i < 8; i++)
                {
                    float angle = MathHelper.TwoPi * i / 8f;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.RainbowMk2, angle.ToRotationVector2() * 3.4f, 100, default, 1.1f);
                    dust.noGravity = true;
                }
                // Rude Buster starburst: 8 violet spikes at 45° offsets.
                for (int i = 0; i < 8; i++)
                {
                    float angle = MathHelper.PiOver4 + MathHelper.TwoPi * i / 8f;
                    Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, angle.ToRotationVector2() * 5f, 100, default, 1.2f);
                    burst.noGravity = true;
                }
                // Signature orbit motes wheeling the premiere (custom ModDust).
                for (int i = 0; i < 4; i++)
                {
                    Dust mote = Dust.NewDustDirect(Projectile.Center, 8, 8, ModContent.DustType<global::BiltonsYoyoMod.Content.Dusts.OrbitMoteDust>(), 0f, 0f, 100, default, 1.1f);
                    mote.noGravity = true;
                    mote.customData = Projectile.Center;
                }
                // MAJOR C (ring half): fast inner premiere ring over the burst.
                for (int i = 0; i < 8; i++)
                {
                    float angle = MathHelper.TwoPi * i / 8f + MathHelper.PiOver4;
                    Dust ring = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, angle.ToRotationVector2() * 5f, 100, disco, 1f);
                    ring.noGravity = true;
                }

                // MAJOR A (premiere half): owner-only zoom + shake premiere.
                if (Main.myPlayer == Projectile.owner)
                {
                    DevZoomHelper.Begin(Projectile.owner);
                    zoomT = 0;
                }
                YoyoParticles.SpawnBurst(Projectile.Center, 12, 6f, new Color(190, 120, 245), 1.1f, 26);
                // Launch shockwave: a thin expanding ring. (The old squashed bloom
                // drew a flat oval; a ring reads as an impact and never as a disc.)
                YoyoParticles.SpawnRing(Projectile.Center, 10f, 58f, 22, new Color(190, 120, 245), 3f);

                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = -0.2f }, Projectile.Center);
            }

            bool mine = Main.myPlayer == Projectile.owner;

            // Transient screen shake only, faded in with the zoom; Main.screenPosition
            // is rebuilt from the player each frame, so nothing persists.
            // M13: remotes are never shaken by someone else's premiere.
            if (Projectile.localAI[1] > 0f)
            {
                Projectile.localAI[1] -= 1f;
                if (mine)
                {
                    float ramp = 1f - Projectile.localAI[1] / 20f;
                    Main.screenPosition += Main.rand.NextVector2Circular(4f, 4f) * ramp;
                }
            }

            // MAJOR A (choreography half): eased zoom-in -> hold -> eased zoom-out.
            if (mine && ZoomRunning)
            {
                zoomT++;
                float ease;
                if (zoomT < ZoomIn)
                    ease = DevZoomHelper.Smooth(zoomT / (float)ZoomIn);
                else if (zoomT < ZoomHold)
                    ease = 1f;
                else if (zoomT < ZoomEnd)
                    ease = 1f - DevZoomHelper.Smooth((zoomT - ZoomHold) / (float)(ZoomEnd - ZoomHold));
                else
                {
                    DevZoomHelper.End();
                    zoomT = ZoomEnd;
                    ease = 0f;
                }
                DevZoomHelper.Apply(ease);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Insane FAKE damage numbers (the joke) over a real hidden 1.
            // Rare heckles replace the digits entirely.
            int heck = Main.rand.Next(100);
            string fake = heck == 0 ? "NICE" : heck == 1 ? "BONK" : "12903123";
            CombatText.NewText(new Rectangle((int)target.Center.X - 20, (int)target.Center.Y - 20, 40, 40), new Color(220, 130, 255), fake);
            // Hide the real "1": kill its damage number the same tick it spawns
            // (the fake 12903123 stays as the joke).
            for (int i = 0; i < Main.combatText.Length; i++)
            {
                CombatText t = Main.combatText[i];
                if (t.active && t.text == "1" && Vector2.DistanceSquared(t.position, target.Center) < 80f * 80f)
                {
                    t.active = false;
                    break;
                }
            }
            // Real damage is 1 (handled by vanilla StrikeNPC above, including
            // knockback); only the number is hidden. No manual life edit —
            // that would double-dip to 2.
            // Tight goofy pop (was 16 random).
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, angle.ToRotationVector2() * 2.8f, 100, default, 1f);
                dust.noGravity = true;
            }
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.RainbowMk2, angle.ToRotationVector2() * 2.4f, 100, default, 0.9f);
                dust.noGravity = true;
            }
            // Rude Buster spikes over the pop.
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f;
                Dust spike = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, angle.ToRotationVector2() * 3.2f, 100, default, 1f);
                spike.noGravity = true;
            }
            // M14: premiere star winking over the pop.
            for (int i = 0; i < 2; i++)
            {
                float angle = MathHelper.TwoPi * i / 2f;
                Dust star = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, angle.ToRotationVector2() * 3f, 100, default, 1f);
                star.noGravity = true;
            }
            Color disco = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            Lighting.AddLight(Projectile.Center, disco.R / 255f, disco.G / 255f, disco.B / 255f);
        }

        // Tile premiere: the number + burst show even when the bolt eats a wall
        // (vanilla would kill it silently — same outcome, visible payoff).
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            CombatText.NewText(new Rectangle((int)Projectile.Center.X - 20, (int)Projectile.Center.Y - 20, 40, 40), new Color(220, 130, 255), "12903123");
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, angle.ToRotationVector2() * 3f, 100, default, 1.1f);
                burst.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1.5f, 1.5f, 1.5f);
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.RainbowMk2, angle.ToRotationVector2() * 2.4f, 100, default, 0.9f);
                dust.noGravity = true;
            }
            // Confetti pop finale (visual only).
            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4f + MathHelper.PiOver4;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Confetti, angle.ToRotationVector2() * 2.8f, 100, default, 1f);
                dust.noGravity = true;
            }
            // Rude Buster finale: violet 8-burst over the confetti.
            for (int i = 0; i < 8; i++)
            {
                float angle = MathHelper.TwoPi * i / 8f;
                Dust finale = Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, angle.ToRotationVector2() * 3.4f, 100, default, 1.1f);
                finale.noGravity = true;
            }
            // MAJOR C (finale half): white-hot crown flash over the pop.
            for (int i = 0; i < 2; i++)
            {
                float angle = MathHelper.TwoPi * i / 2f + MathHelper.PiOver4;
                Dust crown = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, angle.ToRotationVector2() * 3.4f, 100, default, 1.1f);
                crown.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1.5f, 1.5f, 1.5f);

            // Camera safety: a mid-premiere kill still gives the zoom back.
            if (Main.myPlayer == Projectile.owner && ZoomRunning)
                DevZoomHelper.End();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Rude Buster bolt: violet-crescent ribbon + violet head + comet knot.
            // All orbs pinned to the sprite draw point (gfxOffY included).
            Vector2 drawPoint = Projectile.Center + new Vector2(0f, Projectile.gfxOffY);
            MiniYoyoFx.RibbonDrawSoft(rainbowTrail, new Color(110, 50, 200), new Color(235, 220, 255), 9f);
            MiniYoyoFx.DrawGlow(drawPoint, 12f, new Color(170, 90, 230), 0.25f);
            // M12: tail knot + head ticks so the 12spd bolt streaks.
            if (Projectile.velocity.LengthSquared() > 1f)
            {
                Vector2 back = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                MiniYoyoFx.DrawGlow(drawPoint - back * 9f, 6f, new Color(150, 110, 200), 0.2f);
                Vector2 head = drawPoint - Main.screenPosition;
                Vector2 tail = drawPoint - back * 12f - Main.screenPosition;
                MiniYoyoFx.DrawLine(head, tail, 1.5f, new Color(255, 255, 255, 140) * 0.55f);
                // Rude Buster crescent: dotted arc riding ahead of the bolt.
                Vector2 face = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                float baseAng = face.ToRotation();
                for (int i = -2; i <= 2; i++)
                {
                    float a = baseAng + i * 0.35f;
                    Vector2 arcPos = drawPoint + a.ToRotationVector2() * 18f;
                    MiniYoyoFx.DrawGlow(arcPos, 4f, new Color(170, 90, 230), 0.25f);
                }
            }
            // Premiere star flashing over the first 10 ticks (timeLeft is synced).
            if (Projectile.timeLeft > 190)
            {
                Vector2 c = Projectile.Center - Main.screenPosition;
                Color star = new Color(255, 255, 255, 150) * 0.8f;
                MiniYoyoFx.DrawLine(c + new Vector2(-9f, 0f), c + new Vector2(9f, 0f), 1.5f, star);
                MiniYoyoFx.DrawLine(c + new Vector2(0f, -9f), c + new Vector2(0f, 9f), 1.5f, star);
                MiniYoyoFx.DrawStarSparkle(Projectile.Center, new Color(220, 170, 255), 1.6f, 0f, 0.8f);
            }
            // Own the bolt draw so it reads rainbow, not a green Martian copy:
            // disco-tinted body + white-hot core, centered on the hitbox.
            Texture2D boltTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 boltOrigin = boltTex.Size() * 0.5f;
            Vector2 boltPos = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);
            Color discoBolt = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
            Main.EntitySpriteDraw(boltTex, boltPos, null, discoBolt * Projectile.Opacity, Projectile.rotation, boltOrigin, Projectile.scale * 1.1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(boltTex, boltPos, null, Color.White * Projectile.Opacity * 0.85f, Projectile.rotation, boltOrigin, Projectile.scale * 0.55f, SpriteEffects.None, 0);
            return false;
        }
    }
}
