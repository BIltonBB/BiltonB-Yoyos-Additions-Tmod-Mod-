using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // Shared presentation pass for all 13 mini-boss families (mains + their
    // sub-projectiles), kept in ONE GlobalProjectile so the same behaviour does
    // not have to be hand-pasted into 46 files:
    //   mains:  ground shadow (F1) / contrast anchor (C2) / body language
    //           squash-stretch + idle breath (D1/D2/D4) / spin blur (D6) /
    //           charge bank rings (J2/J3)
    //   subs:   tracer taper for >16px/tick flyers (K8)
    //   both:   fade shapes on death (B2)
    // Purely visual, runs on every client wherever a batch exists.
    public class MiniBossYoyoGfx : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private const int ScanInterval = 4;        // ground rescan cadence (ticks)
        private const float MaxShadowHeight = 420f;

        private bool isMain;
        private Color familyColor = Color.White;

        // Ground shadow state (mains)
        private float groundY;
        private int scanTimer;
        private float shadow;    // eased 0..1 presence (never pops on lost scans)
        private bool hasGround;

        // Tracer taper state (subs): last 5 flight positions.
        private readonly Vector2[] trace = new Vector2[5];
        private int traceCount;
        private uint traceTick;

        // J1/K3 state (subs): spawn age, reclaim eligibility, owning yoyo.
        private uint spawnTick;
        private bool reclaimable;
        private int hubWhoAmI = -1;

        // Body language state (mains)
        private int deformTimer;      // impact squash countdown
        private float lastRotation;
        private float spinRate;       // smoothed |rotation delta| per tick

        // Charge bank (mains): hits charge, a full bank spends on the next hit.
        private int bank;
        private uint lastBankTick;
        private int spendTimer;
        private const int BankMax = 12;
        private const int BankSpendAt = 8;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            // Mains: the same identity registry the strings use.
            if (YoyoStringHookSystem.TryGetStyle(entity.type, out _, out _, out _))
            {
                isMain = true;
                MiniYoyoFx.TryGetKillStyle(entity.type, out familyColor, out _);
                return true;
            }
            // Subs: anything the central kill-style table knows.
            if (MiniYoyoFx.TryGetKillStyle(entity.type, out familyColor, out _))
            {
                isMain = false;
                reclaimable = IsReclaimable(entity.type);
                return true;
            }
            return false;
        }

        // J1 allow-list: ranged flyers whose expiry should read as a recall.
        // Contact/melee children (clubs, blades, scythes, bouncing balls),
        // homers, beams and orbit entities keep their own reads.
        private static HashSet<int> reclaimTypes;

        private static bool IsReclaimable(int type)
        {
            if (reclaimTypes == null)
            {
                reclaimTypes = new HashSet<int>();
                try
                {
                    void Add<T>() where T : ModProjectile => reclaimTypes.Add(ModContent.ProjectileType<T>());
                    Add<DarkMageYoyoMagicProj>();
                    Add<TorchGodYoyoTorchProj>();
                    Add<DutchManYoyoCannonProj>();
                    Add<OgreYoyoSpitProj>();
                    Add<DreadnautilusYoyoBloodShardProj>();
                    Add<MourningWoodYoyoFireProj>();
                    Add<SantaYoyoBulletProj>();
                    Add<SantaYoyoPresentsProj>();
                    Add<PumpkingYoyoFireProj>();
                    Add<EverscreamYoyoNeedleProj>();
                    Add<EverscreamYoyoBallProj>();
                    Add<EverscreamYoyoBallProj2>();
                    Add<IceQueenYoyoShardProj>();
                    Add<BetsyYoyoFireProj>();
                    Add<BetsyYoyoFireBallProj>();
                }
                catch { }
            }
            return reclaimTypes.Contains(type);
        }

        // Nearest active mini-boss main of the same owner = the "hub".
        private static int FindHub(Projectile sub)
        {
            int best = -1;
            float bestDist = 900f * 900f;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != sub.owner || p.whoAmI == sub.whoAmI)
                    continue;
                if (!YoyoStringHookSystem.TryGetStyle(p.type, out _, out _, out _))
                    continue;
                float d = Vector2.DistanceSquared(p.Center, sub.Center);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p.whoAmI;
                }
            }
            return best;
        }

        public override void AI(Projectile projectile)
        {
            if (isMain)
            {
                UpdateMain(projectile);
                return;
            }

            // J1/K3: age + reclaim (final ticks bend home to the family's hub).
            uint nowTick = (uint)Main.GameUpdateCount;
            if (spawnTick == 0u)
                spawnTick = nowTick != 0u ? nowTick : 1u;
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            if (reclaimable && (cfg == null || cfg.Visuals.Reclaim) && projectile.timeLeft <= 12)
            {
                if (hubWhoAmI < 0 || hubWhoAmI >= Main.maxProjectiles || !Main.projectile[hubWhoAmI].active)
                    hubWhoAmI = FindHub(projectile);
                if (hubWhoAmI >= 0)
                {
                    Projectile hub = Main.projectile[hubWhoAmI];
                    if (hub.Center != projectile.Center)
                    {
                        Vector2 toHub = (hub.Center - projectile.Center).SafeNormalize(Vector2.UnitX);
                        projectile.velocity += toHub * 0.55f;
                        projectile.velocity *= 0.965f;
                    }
                }
            }

            // Tracer taper history: only worth keeping while really flying.
            if (projectile.velocity.LengthSquared() > 256f)
            {
                traceTick = (uint)Main.GameUpdateCount;
                for (int i = trace.Length - 1; i > 0; i--)
                    trace[i] = trace[i - 1];
                trace[0] = projectile.Center;
                if (traceCount < trace.Length)
                    traceCount++;
            }
            else if (traceCount > 0)
            {
                traceCount = 0;
            }
        }

        private void UpdateMain(Projectile projectile)
        {
            // ---- F1: ground scan -------------------------------------------
            if (--scanTimer <= 0)
            {
                scanTimer = ScanInterval;
                hasGround = MiniYoyoFx.FindGroundY(projectile.Center, out Vector2 ground);
                groundY = ground.Y;
            }
            float want = hasGround ? 1f : 0f;
            shadow = MathHelper.Lerp(shadow, want, 0.18f);

            // ---- D6: spin rate (smoothed rotation delta) -------------------
            float delta = MathHelper.WrapAngle(projectile.rotation - lastRotation);
            lastRotation = projectile.rotation;
            spinRate = MathHelper.Lerp(spinRate, Math.Abs(delta), 0.25f);

            // ---- D2/D4: body language (squash / stretch / idle breath) -----
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            if (cfg != null && cfg.Visuals.BodyLanguage)
            {
                if (deformTimer > 0)
                    deformTimer--;
                float spd = projectile.velocity.Length();
                float target;
                if (deformTimer > 0)
                    target = 0.92f + 0.02f * (deformTimer / 8f);        // impact squash
                else if (spd > 16f)
                    target = 1.06f;                                     // launch stretch
                else if (spd < 2f)
                    target = 1f + 0.015f * (float)System.Math.Sin(Main.GameUpdateCount * 0.06f + projectile.whoAmI); // idle breath
                else
                    target = 1f + MathHelper.Min(spd * 0.004f, 0.04f);  // speed lean
                if (target < 1f)
                    projectile.scale = MathHelper.Lerp(projectile.scale, target, 0.35f);
                else
                    projectile.scale = MathHelper.Lerp(projectile.scale, target, 0.12f);
            }

            // ---- J2: bank decay when idle ----------------------------------
            if (bank > 0 && Main.GameUpdateCount - lastBankTick > 240u && Main.GameUpdateCount % 30 == 0)
                bank--;
            if (spendTimer > 0)
                spendTimer--;

            // ---- D3: verlet appendages -------------------------------------
            UpdateAppendages(projectile, cfg);
        }

        // D3: one pinned chain per strip (2-6 points each) - refreshed here,
        // drawn behind the body. Only 4 families carry appendages.
        private const int AppMaxStrips = 3;
        private const int AppSegs = 4;                     // points per strip = AppSegs + 1
        private readonly Vector2[] appPos = new Vector2[AppMaxStrips * (AppSegs + 1)];
        private readonly Vector2[] appPrev = new Vector2[AppMaxStrips * (AppSegs + 1)];
        private bool appInit;
        private int appFamily = -1;

        private static int AppendageFamily(Projectile projectile)
        {
            if (!YoyoStringHookSystem.TryGetFamilyIndex(projectile.type, out int fam))
                return -1;
            switch (fam)
            {
                case 1:   // TorchGod burning wick
                case 2:   // Dutchman pennant
                case 4:   // Dreadnautilus tendrils
                case 7:   // Pumpking cloth
                    return fam;
                default:
                    return -1;
            }
        }

        private static int AppStrips(int fam) => fam == 4 ? 3 : fam == 7 ? 2 : 1;
        private static float AppSegLen(int fam) => fam == 2 ? 10f : fam == 4 ? 8f : fam == 1 ? 7f : 8f;
        private static float AppGravity(int fam) => fam == 1 ? -0.16f : fam == 2 ? 0.05f : fam == 4 ? 0.10f : 0.14f;
        private static float AppFlap(int fam) => fam == 2 ? 0.55f : fam == 4 ? 0.45f : fam == 7 ? 0.32f : 0.30f;

        private void UpdateAppendages(Projectile projectile, Configuration.YoyoConfig cfg)
        {
            if (cfg != null && !cfg.Visuals.BodyAppendages)
            {
                appFamily = -1;
                return;
            }
            appFamily = AppendageFamily(projectile);
            if (appFamily < 0)
                return;
            int strips = AppStrips(appFamily);
            Vector2 dir = projectile.velocity.LengthSquared() > 0.5f
                ? projectile.velocity.SafeNormalize(Vector2.UnitX)
                : projectile.rotation.ToRotationVector2();
            Vector2 side = new Vector2(-dir.Y, dir.X);
            float restLen = AppSegLen(appFamily);
            float gravity = AppGravity(appFamily);
            float flap = AppFlap(appFamily);
            float wind = Main.windSpeedCurrent * 0.05f;
            for (int s = 0; s < strips; s++)
            {
                int baseIdx = s * (AppSegs + 1);
                Vector2 anchor = projectile.Center - dir * (projectile.width * 0.4f) + side * ((s - (strips - 1) * 0.5f) * 9f);
                if (!appInit)
                {
                    for (int i = 0; i <= AppSegs; i++)
                        appPos[baseIdx + i] = appPrev[baseIdx + i] = anchor + dir * (i * restLen);
                }
                for (int i = 0; i <= AppSegs; i++)
                {
                    int idx = baseIdx + i;
                    if (i == 0)
                    {
                        appPrev[idx] = appPos[idx];
                        appPos[idx] = anchor;
                        continue;
                    }
                    Vector2 v = appPos[idx] - appPrev[idx];
                    float wiggle = (float)System.Math.Sin(Main.GameUpdateCount * 0.11f + s * 1.7f + i * 0.9f + projectile.whoAmI) * flap;
                    v += new Vector2(wind + wiggle * 0.35f, gravity * restLen);
                    v *= 0.86f;
                    Vector2 next = appPos[idx] + v;
                    Vector2 delta = next - appPos[baseIdx + i - 1];
                    float dLen = delta.Length();
                    if (dLen < 0.001f)
                        delta = dir;
                    else
                        delta /= dLen;
                    next = appPos[baseIdx + i - 1] + delta * restLen;
                    appPrev[idx] = appPos[idx];
                    appPos[idx] = next;
                }
            }
            appInit = true;
        }

        private void DrawAppendages(Projectile projectile, Configuration.YoyoConfig cfg)
        {
            if (cfg != null && !cfg.Visuals.BodyAppendages)
                return;
            if (appFamily < 0 || !appInit || !MiniYoyoFx.EffectsEnabled())
                return;
            int strips = AppStrips(appFamily);
            for (int s = 0; s < strips; s++)
            {
                int baseIdx = s * (AppSegs + 1);
                Vector2 prev = appPos[baseIdx] - Main.screenPosition;
                for (int i = 1; i <= AppSegs; i++)
                {
                    Vector2 pos = appPos[baseIdx + i] - Main.screenPosition;
                    float t = i / (float)AppSegs;
                    MiniYoyoFx.DrawLine(prev, pos, MathHelper.Lerp(3.2f, 0.9f, t), familyColor * MathHelper.Lerp(0.75f, 0.25f, t));
                    prev = pos;
                }
                Vector2 tip = appPos[baseIdx + AppSegs];
                if (appFamily == 1)
                {
                    // TorchGod: the wick keeps a burning curl at its tip.
                    Vector2 tipDir = (appPos[baseIdx + AppSegs] - appPos[baseIdx + AppSegs - 1]).SafeNormalize(Vector2.UnitX);
                    MiniBossShapeBank.Draw(ShapeId.EmberCurl, tip, tipDir.ToRotation() + Main.GameUpdateCount * 0.15f, 0.22f, new Color(255, 175, 95) * 0.85f);
                }
                else
                {
                    MiniYoyoFx.DrawGlow(tip, 3.5f, familyColor, 0.5f);
                }
            }
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (!isMain)
            {
                DrawOwnershipTether(projectile);
                DrawTracerTaper(projectile);
                return true;
            }
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            DrawContrastAnchor(projectile, cfg);
            DrawGroundShadow(projectile, cfg);
            DrawSwingSmear(projectile, cfg);
            DrawAppendages(projectile, cfg);
            DrawSpinBlur(projectile, cfg);
            DrawBankRings(projectile, cfg);
            return true;
        }

        // C2: a dark opaque under-layer below every additive glow keeps the
        // hot layers from washing into bright backgrounds (value separation).
        private void DrawContrastAnchor(Projectile projectile, Configuration.YoyoConfig cfg)
        {
            if (cfg != null && !cfg.Visuals.ContrastAnchors)
                return;
            float size = MathHelper.Max(projectile.width, projectile.height) * 1.45f * projectile.scale;
            MiniYoyoFx.DrawSolidEllipse(projectile.Center, size, size * 0.9f, 0f, new Color(6, 8, 14), 0.28f);
        }

        private void DrawGroundShadow(Projectile projectile, Configuration.YoyoConfig cfg)
        {
            if (shadow <= 0.02f)
                return;
            if (cfg != null && !cfg.Visuals.GroundShadows)
                return;

            float height = groundY - (projectile.position.Y + projectile.height);
            if (height < 2f || height > MaxShadowHeight)
                return;

            float t = height / MaxShadowHeight;   // 0 hugged ground .. 1 far out
            float fade = 1f - MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutQuad, t);
            float width = projectile.width * (1.3f + 1.5f * t);
            float alpha = 0.34f * fade * shadow;
            if (cfg != null && cfg.Visuals.Quality == Configuration.YoyoVisualQuality.Low)
                alpha *= 0.6f;   // Low keeps the read, just softer

            Vector2 centre = new Vector2(projectile.Center.X, groundY - 1f);
            MiniYoyoFx.DrawSolidEllipse(centre, width, width * 0.32f, 0f, new Color(8, 10, 16), alpha);
        }

        // L2/B7: one baked crescent dragged behind a fast main - the six
        // "minimal overhaul" beats (dash/fly speeds) read as real motion.
        private void DrawSwingSmear(Projectile projectile, Configuration.YoyoConfig cfg)
        {
            if (cfg != null && !cfg.Visuals.SwingSmears)
                return;
            float speed = projectile.velocity.Length();
            if (speed < 6.5f)
                return;
            float t = MathHelper.Clamp((speed - 6.5f) / 9f, 0f, 1f);
            Vector2 dir = projectile.velocity.SafeNormalize(Vector2.UnitX);
            float rot = dir.ToRotation() + (float)System.Math.Sin(Main.GameUpdateCount * 0.09f + projectile.whoAmI) * 0.22f;
            float body = MathHelper.Clamp(projectile.scale, 0.7f, 1.5f) * (0.9f + speed * 0.03f);
            Vector2 scale = new Vector2(0.12f + 0.14f * t, 0.45f + 0.35f * t) * body;
            MiniBossShapeBank.Draw(ShapeId.SmearCrescent, projectile.Center - dir * (6f + 6f * t), rot, scale, familyColor * (0.20f + 0.28f * t));
        }

        // D6: two lagging rotation echoes at the body's own position - cheap
        // "it is really spinning" blur for bodies that spin fast.
        private void DrawSpinBlur(Projectile projectile, Configuration.YoyoConfig cfg)
        {
            if (cfg != null && !cfg.Visuals.SpinBlur)
                return;
            if (spinRate < 0.06f || !MiniYoyoFx.EffectsEnabled())
                return;
            Texture2D tex = TextureAssets.Projectile[projectile.type].Value;
            Vector2 pos = projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float dir = System.Math.Sign(MathHelper.WrapAngle(projectile.rotation - lastRotation));
            if (dir == 0f)
                dir = 1f;
            Main.EntitySpriteDraw(tex, pos, null, familyColor * 0.22f,
                projectile.rotation - dir * 0.38f, origin, projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, familyColor * 0.11f,
                projectile.rotation - dir * 0.72f, origin, projectile.scale, SpriteEffects.None, 0);
        }

        // J2/J3: hit-charged orbit rings; a full bank spends inward (bank rings
        // are the "ammo" read of the economy loop).
        private void DrawBankRings(Projectile projectile, Configuration.YoyoConfig cfg)
        {
            if (cfg != null && !cfg.Visuals.ChargeBank)
                return;
            if (spendTimer > 0)
            {
                float t = 1f - spendTimer / 8f;
                float radius = MathHelper.Lerp(34f, 10f, MiniYoyoFx.Ease(MiniYoyoFx.FxEase.OutCubic, t));
                float alpha = 0.7f * (spendTimer / 8f);
                MiniYoyoFx.DrawRing(projectile.Center, radius, 2.5f, familyColor * alpha, 26);
                MiniYoyoFx.DrawDashedRing(projectile.Center, radius * 0.62f, 1.5f, Color.White * (alpha * 0.7f), 8, Main.GameUpdateCount * 0.2f);
            }
            if (bank <= 0)
                return;
            int rings = Math.Min(3, (bank + 3) / 4);                 // 1..3
            float pulse = 0.6f + 0.4f * (0.5f + 0.5f * (float)System.Math.Sin(Main.GameUpdateCount * 0.08f));
            for (int i = 0; i < rings; i++)
            {
                float radius = 23f + i * 7f;
                float a = (0.10f + 0.045f * bank) * pulse;
                MiniYoyoFx.DrawDashedRing(projectile.Center, radius, 1.5f, familyColor * a, 10 + i * 2, Main.GameUpdateCount * (0.05f + 0.03f * i));
            }
        }

        // K3: a fresh sub drags a faint leash from its owning yoyo for ~0.2s,
        // so swarm fights read "this came from that yoyo" at a glance.
        private void DrawOwnershipTether(Projectile projectile)
        {
            if (spawnTick == 0u)
                return;
            uint age = Main.GameUpdateCount - spawnTick;
            if (age > 14u)
                return;
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            if (cfg != null && !cfg.Visuals.OwnershipTether)
                return;
            if (hubWhoAmI < 0 || hubWhoAmI >= Main.maxProjectiles || !Main.projectile[hubWhoAmI].active)
                hubWhoAmI = FindHub(projectile);
            if (hubWhoAmI < 0)
                return;
            Projectile hub = Main.projectile[hubWhoAmI];
            float a = 0.35f * (1f - age / 14f);
            MiniYoyoFx.DrawLine(hub.Center - Main.screenPosition, projectile.Center - Main.screenPosition, 2f, familyColor * a);
            MiniYoyoFx.DrawGlow(hub.Center, 7f, familyColor, a * 0.8f);
        }

        private void DrawTracerTaper(Projectile projectile)
        {
            if (traceCount < 2 || Main.GameUpdateCount - traceTick > 2u)
                return;
            if (!MiniYoyoFx.EffectsEnabled())
                return;
            Vector2 prev = projectile.Center - Main.screenPosition;
            for (int i = 1; i < traceCount; i++)
            {
                float t = i / (float)(trace.Length - 1);   // 0 near .. 1 far
                float width = MathHelper.Lerp(3.2f, 0.6f, t) * MathHelper.Clamp(projectile.scale, 0.6f, 1.6f);
                float alpha = 0.5f * (1f - t);
                Vector2 pos = trace[i] - Main.screenPosition;
                if (Vector2.DistanceSquared(prev, pos) < 4f)
                    continue;
                MiniYoyoFx.DrawLine(prev, pos, width, familyColor * alpha);
                prev = pos;
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Sub hits stay silent: the H2 per-hit ticks were removed as noise
            // pollution (they re-armed every 5 ticks during sustained fire).
            if (!isMain)
                return;
            // D2: every landed hit compresses the body for a beat.
            deformTimer = 8;
            // J2: hits charge the bank; J3: a full bank spends on the next hit.
            lastBankTick = (uint)Main.GameUpdateCount;
            if (bank >= BankSpendAt)
            {
                spendTimer = 8;
                bank = 0;
                // L2: the spend rips a contract rune out of the ring stack.
                MiniBossShapeBank.Spawn(ShapeId.SealRune, projectile.Center, Main.rand.NextFloat(MathHelper.TwoPi),
                    0.35f * MathHelper.Clamp(projectile.scale, 0.6f, 1.8f), familyColor * 0.7f, 18, Vector2.Zero, 0.05f, -0.015f);
            }
            else if (bank < BankMax)
            {
                bank++;
            }
        }

        // NOTE: the H1 per-spawn launch voice was removed on feedback - a sub
        // spawn is a fire event, and fire events repeat, so any per-spawn sound
        // reads as noise pollution. Only the heavily-throttled death voice and
        // the rare-beat Volley remain.

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            if (!MiniYoyoFx.TryGetKillStyle(projectile.type, out Color color, out _))
                return;

            MiniYoyoFx.FadeShape shape;
            if (isMain)
            {
                // Every main exit is a recall/swap: yank-home read.
                shape = MiniYoyoFx.FadeShape.Collapse;
            }
            else if (reclaimable && (cfg == null || cfg.Visuals.Reclaim))
            {
                // J1: expiry is now a recall - heartbeat collapsing home.
                shape = MiniYoyoFx.FadeShape.Collapse;
            }
            else if (projectile.tileCollide
                && Collision.SolidCollision(projectile.position - new Vector2(2f), projectile.width + 4, projectile.height + 4))
            {
                shape = MiniYoyoFx.FadeShape.Shatter;
            }
            else if (timeLeft <= 0)
            {
                shape = MiniYoyoFx.FadeShape.Dissolve;
            }
            else
            {
                shape = MiniYoyoFx.FadeShape.Pop;
            }
            if (cfg == null || cfg.Visuals.FadeShapes)
            {
                MiniYoyoFx.FadeShapeFX(projectile.Center, color, shape, projectile.scale);
                // L2: shape receipts - the bank gives every fade verb a silhouette.
                float s = MathHelper.Clamp(projectile.scale, 0.6f, 1.8f);
                switch (shape)
                {
                    case MiniYoyoFx.FadeShape.Shatter:
                        MiniBossShapeBank.SpawnFan(ShapeId.FlakeSix, projectile.Center, color, 5, 2.4f, 26, 0.42f * s);
                        MiniBossShapeBank.Spawn(ShapeId.RingCracked, projectile.Center, Main.rand.NextFloat(MathHelper.TwoPi), 0.5f * s, color * 0.5f, 20);
                        break;
                    case MiniYoyoFx.FadeShape.Collapse:
                        MiniBossShapeBank.Spawn(ShapeId.SealRune, projectile.Center, Main.rand.NextFloat(MathHelper.TwoPi), 0.4f * s, color * 0.6f, 22, Vector2.Zero, 0.045f, -0.012f);
                        break;
                    case MiniYoyoFx.FadeShape.Dissolve:
                        MiniBossShapeBank.SpawnFan(ShapeId.WispTaper, projectile.Center, color * 0.7f, 4, 1.5f, 30, 0.5f * s);
                        break;
                    default:
                        MiniBossShapeBank.SpawnFan(ShapeId.SplatStar, projectile.Center, color, 5, 2.2f, 16, 0.34f * s);
                        break;
                }
            }
            // H1: the family's death voice (8/9 stay quiet by doctrine).
            if (!isMain && YoyoStringHookSystem.TryGetFamilyIndex(projectile.type, out int fam))
                MiniBossYoyoSound.DeathVoice(projectile.owner, fam, projectile.Center, shape == MiniYoyoFx.FadeShape.Dissolve);
        }
    }
}
