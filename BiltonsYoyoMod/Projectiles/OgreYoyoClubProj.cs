using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BiltonsYoyoMod.Projectiles
{
    public class OgreYoyoClubProj : ModProjectile, IYoyoInstantHit
    {
        private bool spawnFxDone;
        private bool heavy; // every 3rd swing: broader arc, harder impact (visual weight)
        public override void SetDefaults()
        {
            Projectile.width = 26;
            Projectile.height = 28;

            Projectile.friendly = true;
            Projectile.hostile = false;

            Projectile.penetrate = -1;
            Projectile.tileCollide = false;

            Projectile.timeLeft = 30;

            Projectile.DamageType = DamageClass.MeleeNoSpeed;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        // M9: rot lift so the club reads in dark arenas.
        public override Color? GetAlpha(Color lightColor)
        {
            return Color.Lerp(lightColor, new Color(190, 240, 190), 0.3f) * Projectile.Opacity;
        }

        public override void AI()
        {
            // Spawn pop: tight spore kick, bigger for heavies.
            if (!spawnFxDone)
            {
                spawnFxDone = true;
                heavy = Projectile.ai[1] == 1f;
                int pop = heavy ? 9 : 6;
                for (int i = 0; i < pop; i++)
                {
                    float angle = MathHelper.TwoPi * i / pop;
                    Dust flash = Dust.NewDustPerfect(Projectile.Center, DustID.Poisoned, angle.ToRotationVector2() * (heavy ? 3f : 2.4f), 100, default, heavy ? 1.2f : 1.1f);
                    flash.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, 0.35f, 1f, 0.35f);
            }

            Lighting.AddLight(Projectile.Center, 0.2f, 0.7f, 0.2f);

            // Parent-anchored swing: the club sweeps an arc AROUND the yoyo.
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent = Main.projectile[parentIndex];
            if (!parent.active || parent.type != ModContent.ProjectileType<OgreYoyoProj>() || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            Projectile.localAI[0]++;
            if (Projectile.localAI[0] == 1f)
            {
                // Aim the swing from the yoyo toward the nearest enemy.
                float aim = Main.rand.NextFloat(MathHelper.TwoPi);
                float bestDistSq = 300f * 300f;
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
                Projectile.localAI[1] = aim;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = heavy ? -0.25f : 0f }, parent.Center); // swing whoosh (heavies growl lower)
            }

            float swingProgress = Projectile.localAI[0] / 30f;
            float swingBase = Projectile.localAI[1];
            float swingAngle = swingBase + MathHelper.Lerp(-1.2f, 1.4f, swingProgress);
            Projectile.Center = parent.Center + swingAngle.ToRotationVector2() * 40f;
            // Radial held-club orientation: long axis points outward from the yoyo.
            // (Club head leads outward; +3/4PI keeps the heavy end away from the hand.)
            Projectile.rotation = swingAngle + MathHelper.PiOver4 * 3f;

            // Lean swing wake: one spore per tick + rare churn (was 2/tick cloud).
            Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Poisoned, new Vector2(0f, -0.5f), 120, default, 1f);
            dust.noGravity = true;
            if (Main.rand.NextBool(4))
            {
                Dust churn = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), DustID.Smoke, Vector2.Zero, 120, default, 0.8f);
                churn.noGravity = true;
            }

            // M10: swing-tip sparkles shed off the club head at full snap (mid-swing).
            if (swingProgress > 0.3f && swingProgress < 0.75f && Main.rand.NextBool(3))
            {
                Vector2 tip = parent.Center + swingAngle.ToRotationVector2() * 56f;
                Dust tipSpark = Dust.NewDustPerfect(tip, DustID.JungleSpore, swingAngle.ToRotationVector2() * 1.5f, 120, default, 0.85f);
                tipSpark.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            SoundEngine.PlaySound(SoundID.NPCHit4, Projectile.Center);

            // Synced vanilla slash sparkles on the hit (free MP sync).
            Terraria.GameContent.Drawing.ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, Terraria.GameContent.Drawing.ParticleOrchestraType.Excalibur,
                new Terraria.GameContent.Drawing.ParticleOrchestraSettings { PositionInWorld = target.Center }, Projectile.owner);
            YoyoParticles.SpawnBloom(target.Center, 3f, 12f, 16, new Color(150, 230, 150), 0.6f);

            // Heavy impact: directional fan (never a circle) + tinted spores.
            int hits = heavy ? 12 : 8;
            Vector2 clubDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            Vector2 clubSide = new Vector2(-clubDir.Y, clubDir.X);
            for (int i = 0; i < hits; i++)
            {
                float spread = (i / (float)(hits - 1) - 0.5f) * 1.2f;
                Vector2 jet = (clubDir + clubSide * spread).SafeNormalize(Vector2.UnitX);
                Dust impact = Dust.NewDustPerfect(target.Center, DustID.Poisoned, jet * (heavy ? 4.2f : 3.6f), 100, default, heavy ? 1.3f : 1.2f);
                impact.noGravity = true;
            }
            // Splintered wood chips (textured, gravity-heavy).
            for (int i = 0; i < 3; i++)
            {
                Dust chip = Dust.NewDustDirect(target.Center, 8, 8, ModContent.DustType<global::BiltonsYoyoMod.Content.Dusts.WoodChipDust>(), Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, -1f), 100, default, 1f);
                chip.noGravity = false;
            }
            // Base slash on every club hit, scaled by damage; heavies add a
            // mirrored follow-through plus the spore extras below.
            float clubSlash = MathHelper.Clamp(0.9f + damageDone / 150f, 0.9f, 2.2f);
            MiniYoyoFx.SlashFX(target.Center, clubDir, new Color(190, 240, 190), clubSlash, Projectile.owner);
            if (heavy)
            {
                // Green spore choke drifting up (tinted: no gray balls).
                for (int i = 0; i < 6; i++)
                {
                    Dust choke = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(6f, 6f), DustID.Smoke, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.5f, -1f)), 120, new Color(150, 230, 150), 1f);
                    choke.noGravity = true;
                }
                // M11: heavies burst a star of bright spores on contact.
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.TwoPi * i / 4f;
                    Dust star = Dust.NewDustPerfect(target.Center, DustID.JungleSpore, angle.ToRotationVector2() * 3f, 120, default, 1f);
                    star.noGravity = true;
                }
                if (Main.myPlayer == Projectile.owner)
                    MiniYoyoFx.SlashFX(target.Center, (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX).RotatedBy(0.5f), new Color(120, 200, 120), clubSlash * 0.6f, Projectile.owner);
                ImpactStaggerSystem.Stagger(target, 4);
            }
            Lighting.AddLight(target.Center, 0.5f, 1f, 0.5f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // MAJOR B: motion-true swing arc around the PARENT yoyo at the real swing radius.
            // Two-tone: deep rot body + bright cutting edge, tapered to the head.
            // Back-swing ghost trails the live arc; the head wears a snap-scaled comet.
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
                return true;

            Projectile parent = Main.projectile[parentIndex];
            if (!parent.active)
                return true;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 center = parent.Center - Main.screenPosition;

            float swingBase = Projectile.localAI[1];
            float swingNow = swingBase + MathHelper.Lerp(-1.2f, 1.4f, Projectile.localAI[0] / 30f);
            float swingStart = swingBase - 1.2f;
            float lifeFade = Projectile.timeLeft / 30f;

            // Live arc.
            DrawArcStrip(pixel, center, swingStart, swingNow, lifeFade, 1f);
            // Back-swing ghost: same geometry lagging half a radian behind, dimmer.
            DrawArcStrip(pixel, center, swingStart, swingNow - 0.5f, lifeFade, 0.35f);

            // Bright leading edge at the club head + head glow.
            // Peak-bright mid-swing (anticipation -> snap -> follow-through).
            // Heavies snap 1.25x harder.
            float progress = 1f - Projectile.timeLeft / 30f;
            float snap = (0.6f + 0.4f * (float)System.Math.Sin(progress * System.Math.PI)) * (heavy ? 1.25f : 1f);
            Vector2 edgeDir = swingNow.ToRotationVector2();
            Vector2 edgeBase = center + edgeDir * 24f;
            Vector2 edgeTip = center + edgeDir * 56f;
            DrawThickLine(pixel, edgeBase, edgeTip, 3f * snap, new Color(220, 255, 220, 200) * lifeFade * snap);
            MiniYoyoFx.DrawGlow(Projectile.Center, 24f * snap, new Color(120, 230, 120), 0.4f * lifeFade);
            // Star flare snapping with the club head at full swing.
            MiniYoyoFx.DrawStarSparkle(Projectile.Center, new Color(200, 255, 200), 1.2f * snap, Projectile.rotation, lifeFade);

            // Head cross ticks at full snap.
            Vector2 headScreen = Projectile.Center - Main.screenPosition;
            Vector2 side = new Vector2(-edgeDir.Y, edgeDir.X);
            Color tick = new Color(220, 255, 220, 150) * (0.5f * lifeFade * snap);
            DrawThickLine(pixel, headScreen - edgeDir * 6f, headScreen + edgeDir * 6f, 1.5f, tick);
            DrawThickLine(pixel, headScreen - side * 6f, headScreen + side * 6f, 1.5f, tick);

            return true; // draw sprite on top
        }

        private void DrawArcStrip(Texture2D pixel, Vector2 center, float arcFrom, float arcTo, float lifeFade, float strength)
        {
            if (arcTo <= arcFrom)
                return;

            // Shared implementation (was a private copy). `center` here is screen
            // space; the shared band works in world space. Heavies sweep broader.
            Vector2 world = center + Main.screenPosition;
            float wMax = heavy ? 18f : 12f;
            float taper = 1f - 2f / wMax;                       // 2px at the swing start
            float alpha = lifeFade * strength;
            Color body = new Color(55, 140, 60, 130);
            Color core = new Color(170, 240, 170, 140);
            MiniYoyoFx.DrawArcBand(world, 40f, arcFrom, arcTo, wMax * 1.5f, body, alpha, 14, taper, 0.2f, 0.9f);
            MiniYoyoFx.DrawArcBand(world, 40f, arcFrom, arcTo, wMax * 0.7f, core, alpha, 14, taper, 0.2f, 0.9f);
        }

        private static void DrawThickLine(Texture2D pixel, Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 diff = end - start;
            if (diff.LengthSquared() < 0.01f)
                return;
            Main.spriteBatch.Draw(pixel, (start + end) * 0.5f, new Rectangle(0, 0, 1, 1), color,
                diff.ToRotation(), new Vector2(0.5f, 0.5f), new Vector2(diff.Length(), width), SpriteEffects.None, 0f);
        }

        public override void OnKill(int timeLeft)
        {
            // Swing-end thud: single mixed ring (was 14).
            for (int i = 0; i < 10; i++)
            {
                float angle = MathHelper.TwoPi * i / 10f;
                int dustType = i % 2 == 0 ? DustID.Poisoned : DustID.Smoke;
                Dust dust = Dust.NewDustPerfect(Projectile.Center, dustType, angle.ToRotationVector2() * 3f, 120, default, 1.1f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.35f, 1f, 0.35f);
            SoundEngine.PlaySound(SoundID.NPCHit4, Projectile.Center);
        }
    }
}
