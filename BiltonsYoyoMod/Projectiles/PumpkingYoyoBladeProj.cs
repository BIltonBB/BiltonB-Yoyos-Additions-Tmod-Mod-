using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class PumpkingYoyoBladeProj : ModProjectile, IYoyoInstantHit
    {
        private Vector2 lastCenter;
        private bool spawnFxDone;
        private bool moon; // every 3rd swing: pale harvest-moon arc, harder read

        // M10: harvest lift, paling toward moon-white on harvest moons.
        public override Color? GetAlpha(Color lightColor)
        {
            Color tint = moon ? new Color(255, 240, 220) : new Color(255, 205, 160);
            return Color.Lerp(lightColor, tint, 0.3f) * Projectile.Opacity;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 40;

            Projectile.scale = 0.5f;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void AI()
        {
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent = Main.projectile[parentIndex];
            if (!parent.active || parent.type != ModContent.ProjectileType<PumpkingYoyoProj>() || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                // Harvest-moon flag arrives in ai[1]; read it before ai[1] becomes the aim.
                moon = Projectile.ai[1] == 1f;

                // Aim the swing at the nearest enemy. Moons sweep mirrored to normal
                // swings (deterministic per flag, synced at spawn — no random flip).
                float aim = Main.rand.NextFloat(MathHelper.TwoPi);
                float bestDistSq = 400f * 400f;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (MiniYoyoFx.IsAttackable(npc))
                    {
                        float dSq = Vector2.DistanceSquared(parent.Center, npc.Center);
                        if (dSq < bestDistSq)
                        {
                            bestDistSq = dSq;
                            aim = (npc.Center - parent.Center).ToRotation();
                        }
                    }
                }

                Projectile.ai[1] = aim;
                Projectile.localAI[1] = moon ? -1f : 1f;
            }

            // Single heavy swing arc over the 40-tick life (not a full orbit).
            float swingProgress = 1f - Projectile.timeLeft / 40f;
            float swingSide = Projectile.localAI[1] >= 0f ? 1f : -1f;
            float angle = Projectile.ai[1] + swingSide * MathHelper.Lerp(-1.3f, 1.5f, swingProgress);
            Vector2 prevCenter = lastCenter;
            Projectile.Center = parent.Center + angle.ToRotationVector2() * 55f;
            Vector2 motion = Projectile.Center - prevCenter;
            if (motion.LengthSquared() > 4f)
                Projectile.rotation = motion.ToRotation() + MathHelper.PiOver2; // edge leads travel
            else
                Projectile.rotation = angle + MathHelper.PiOver4;
            lastCenter = Projectile.Center;

            Lighting.AddLight(Projectile.Center, 0.85f, 0.4f, 0.1f);

            if (!spawnFxDone)
            {
                spawnFxDone = true;
                int pop = moon ? 8 : 6;
                for (int i = 0; i < pop; i++)
                {
                    float a = MathHelper.TwoPi * i / pop;
                    int spawnType = moon ? DustID.MagicMirror : (i % 2 == 0 ? DustID.OrangeTorch : DustID.PurpleTorch);
                    Dust fx = Dust.NewDustPerfect(Projectile.Center, spawnType, a.ToRotationVector2() * (moon ? 3f : 2.6f), 0, default, moon ? 1.1f : 1f);
                    fx.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 1f, 0.55f, 0.2f);
            }

            // Lean swing wake: one ember every other tick + rare edge streak.
            if (Projectile.timeLeft % 2 == 0)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f), DustID.OrangeTorch, Vector2.Zero, 0, default, 0.9f);
                dust.noGravity = true;
            }
            if (Main.rand.NextBool(6))
            {
                Vector2 tangent = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
                Dust streak = Dust.NewDustPerfect(Projectile.Center, DustID.OrangeTorch, tangent * Main.rand.NextFloat(-2f, 2f), 0, default, 0.9f);
                streak.noGravity = true;
            }
            // M11: edge-tip glints winking off the blade at full snap.
            float snapProgress = 1f - Projectile.timeLeft / 40f;
            if (snapProgress > 0.35f && snapProgress < 0.65f && Main.rand.NextBool(4))
            {
                Vector2 tip = parent.Center + angle.ToRotationVector2() * 72f;
                Dust glint = Dust.NewDustPerfect(tip, moon ? DustID.MagicMirror : DustID.GoldFlame, angle.ToRotationVector2() * 1.5f, 0, default, 0.9f);
                glint.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                float angle = MathHelper.TwoPi * i / 6f;
                int slashType = i % 2 == 0 ? DustID.OrangeTorch : DustID.PurpleTorch;
                Dust slash = Dust.NewDustPerfect(Projectile.Center, slashType, angle.ToRotationVector2() * 3.4f, 0, default, 1.1f);
                slash.noGravity = true;
            }
            // MAJOR B (slash half): parting streaks along the travel edge.
            Vector2 travel = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();
            for (int i = 0; i < 4; i++)
            {
                Dust parting = Dust.NewDustPerfect(Projectile.Center, moon ? DustID.MagicMirror : DustID.GoldFlame, travel * Main.rand.NextFloat(2f, 4f) + Main.rand.NextVector2Circular(0.8f, 0.8f), 0, default, 1f);
                parting.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 1f, 0.5f, 0.2f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Heavy harvest impact at contact: ring + light.
            // Moons land a pale double ring (same damage, harder read).
            SoundEngine.PlaySound(SoundID.NPCHit4, target.Center);
            // Synced vanilla slash sparkles on the hit (free MP sync).
            Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = target.Center }, Projectile.owner);
            YoyoParticles.SpawnBloom(target.Center, 3f, 14f, 16, new Color(255, 190, 120), 0.6f);
            // Directional harvest fan (never a circle).
            Vector2 bladeDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            Vector2 bladeSide = new Vector2(-bladeDir.Y, bladeDir.X);
            int hits = moon ? 10 : 8;
            for (int i = 0; i < hits; i++)
            {
                float spread = (i / (float)(hits - 1) - 0.5f) * 1.2f;
                Vector2 jet = (bladeDir + bladeSide * spread).SafeNormalize(Vector2.UnitX);
                int dustType = moon ? DustID.MagicMirror : (i % 2 == 0 ? DustID.OrangeTorch : DustID.PurpleTorch);
                Dust impact = Dust.NewDustPerfect(target.Center, dustType, jet * (moon ? 4f : 3.6f), 100, default, moon ? 1.3f : 1.2f);
                impact.noGravity = true;
            }
            // Base slash on every blade hit, scaled by damage; moons add a
            // mirrored follow-through plus the harvest extras below.
            float bladeSlash = MathHelper.Clamp(0.9f + damageDone / 150f, 0.9f, 2.2f);
            MiniYoyoFx.SlashFX(target.Center, bladeDir, new Color(255, 240, 210), bladeSlash, Projectile.owner);
            if (moon)
            {
                // Pale harvest streaks along the cut (never a circle).
                for (int i = 0; i < 6; i++)
                {
                    float spread = (i / 5f - 0.5f) * 1f;
                    Vector2 jet = (bladeDir + bladeSide * spread).SafeNormalize(Vector2.UnitX);
                    Dust halo = Dust.NewDustPerfect(target.Center, DustID.GoldFlame, jet * 2.2f, 100, default, 1f);
                    halo.noGravity = true;
                }
                if (Main.myPlayer == Projectile.owner)
                    MiniYoyoFx.SlashFX(target.Center, (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX).RotatedBy(0.5f), new Color(255, 220, 180), bladeSlash * 0.6f, Projectile.owner);
                ImpactStaggerSystem.Stagger(target, 4);
            }
            Lighting.AddLight(target.Center, 1f, 0.5f, 0.2f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            // Art-center origin (measured art centroid 52,35 of the 106x86 canvas):
            // the oversized sprite pivots on its visible blade, not canvas middle.
            Vector2 drawOrigin = new Vector2(52.1f, 34.9f);

            // Motion-true swing arc around the parent yoyo: band from the swing's
            // start angle to the blade's current angle at the real orbit radius.
            // MAJOR B (arc half): back-swing ghost trails the live cut; harvest
            // moons wear a pale halo ring around the blade head.
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex >= 0 && parentIndex < Main.maxProjectiles)
            {
                Projectile parent = Main.projectile[parentIndex];
                if (parent.active)
                {
                    Texture2D pixel = TextureAssets.MagicPixel.Value;
                    Vector2 center = parent.Center - Main.screenPosition;

                    float swingSide = Projectile.localAI[1] >= 0f ? 1f : -1f;
                    float swept = swingSide * MathHelper.Lerp(-1.3f, 1.5f, 1f - Projectile.timeLeft / 40f);
                    float arcStart = Projectile.ai[1];
                    float arcNow = Projectile.ai[1] + swept;

                    DrawArcBand(pixel, center, arcStart, arcNow, 1f);
                    // Back-swing ghost: same band lagging a quarter-radian, dimmer.
                    DrawArcBand(pixel, center, arcStart, arcNow - swingSide * 0.45f, 0.35f);

                    // Bright leading edge at the blade, snapping mid-swing.
                    // Moons edge wider and paler.
                    float progress = 1f - Projectile.timeLeft / 40f;
                    float snap = 0.6f + 0.4f * (float)System.Math.Sin(progress * System.Math.PI);
                    Vector2 edgeDir = arcNow.ToRotationVector2();
                    Color edgeCol = moon ? new Color(255, 240, 210, 220) : new Color(255, 225, 160, 210);
                    DrawThickLine(pixel, center + edgeDir * 38f, center + edgeDir * 72f, (moon ? 4f : 3f) * snap, edgeCol * snap);
                    // Star flare snapping with the blade tip at full swing.
                    MiniYoyoFx.DrawStarSparkle(Projectile.Center, moon ? new Color(255, 240, 210) : new Color(255, 220, 160), 1.2f * snap, Projectile.rotation, snap);

                    // Moon halo ring riding the blade head.
                    if (moon)
                        MiniYoyoFx.DrawRing(Projectile.Center, 20f, 2f, new Color(255, 240, 210) * (0.4f + 0.3f * snap), 20);
                }
            }

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, GetAlpha(lightColor) ?? lightColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        private void DrawArcBand(Texture2D pixel, Vector2 center, float arcFrom, float arcTo, float strength)
        {
            if ((arcTo - arcFrom) * (Projectile.localAI[1] >= 0f ? 1f : -1f) <= 0f)
                return;

            // Shared implementation (was a private copy). `center` here is screen
            // space; the shared band works in world space.
            Vector2 world = center + Main.screenPosition;
            float wMax = moon ? 16f : 14f;
            float taper = 1f - 3f / wMax;                       // 3px at the swing start
            Color under = moon ? new Color(170, 130, 255, 70) : new Color(150, 60, 220, 70);
            Color body = moon ? new Color(255, 225, 170, 130) : new Color(255, 140, 30, 120);
            MiniYoyoFx.DrawArcBand(world, 55f, arcFrom, arcTo, wMax * 1.6f, under, strength, 16, taper, 0.2f, 1f);
            MiniYoyoFx.DrawArcBand(world, 55f, arcFrom, arcTo, wMax, body, strength, 16, taper, 0.2f, 1f);
        }

        private static void DrawThickLine(Texture2D pixel, Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 diff = end - start;
            if (diff.LengthSquared() < 0.01f)
                return;
            Main.spriteBatch.Draw(pixel, (start + end) * 0.5f, new Rectangle(0, 0, 1, 1), color,
                diff.ToRotation(), new Vector2(0.5f, 0.5f), new Vector2(diff.Length(), width), SpriteEffects.None, 0f);
        }
    }
}
