using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // True vanilla-string replacement WITHOUT YoyoStringLib: that lib ships
    // its draw detour inside a Mod class, which never loads from a raw
    // dllReference, so its hook never installed (vanilla string drew
    // unchanged). Instead we detour vanilla Main.DrawProj_DrawYoyoString
    // (private void (Projectile, Vector2), verified in tModLoader.dll)
    // ourselves and tile our own original strip PNGs along the string.
    // String-swap concept credit: MooMoo's Ultimate Yoyo Revamp (MIT).
    public class YoyoStringHookSystem : ModSystem
    {
        private delegate void OrigDrawYoyo(Main self, Projectile proj, Vector2 mountedCenter);

        private Hook hook;
        private Action<OrigDrawYoyo, Main, Projectile, Vector2> detour;
        private Hook colorHook;
        private static bool s_liveLogged;

        // Reserved stringColor values (vanilla string items use small ints;
        // 1000+ can never collide). Fixed index order, matches theme colors.
        private static readonly Color[] s_stringColors = new Color[]
        {
            new Color(190, 130, 255), // 0 DarkMage
            new Color(255, 180, 110), // 1 TorchGod
            new Color(150, 200, 190), // 2 Dutchman
            new Color(150, 230, 150), // 3 Ogre
            new Color(220, 80, 90),   // 4 Dreadnautilus
            new Color(255, 180, 110), // 5 MourningWood
            new Color(255, 110, 110), // 6 Santa
            new Color(255, 190, 120), // 7 Pumpking
            new Color(150, 230, 150), // 8 Everscream
            new Color(170, 225, 255), // 9 IceQueen
            new Color(150, 255, 190), // 10 Martian
            new Color(255, 170, 90),  // 11 Betsy
            Color.White,              // 12 Developer (disco-cycled live)
        };
        private static Dictionary<int, int> s_stringIndex;

        // Family palette access for other FX systems (marks, sigils, sounds).
        internal static Color FamilyColor(int idx)
        {
            if (idx < 0 || idx >= s_stringColors.Length)
                return Color.White;
            return s_stringColors[idx];
        }

        // type -> (strip asset path or null, tint, designed)
        private static Dictionary<int, (string, Color, bool)> styles;

        public override void Load()
        {
            try
            {
                MethodInfo target = typeof(Main).GetMethod("DrawProj_DrawYoyoString", BindingFlags.NonPublic | BindingFlags.Instance);
                if (target != null)
                {
                    // Explicit delegate: method-group conversion against Hook's
                    // overloads is ambiguous and can bind silently wrong.
                    detour = (orig, self, proj, mc) => DrawDetour(orig, self, proj, mc);
                    hook = new Hook(target, detour);
                    s_liveLogged = false;
                    Mod.Logger.Info("[BiltonsYoyoMod] Yoyo string detour installed on Main.DrawProj_DrawYoyoString.");
                }
                else
                {
                    Mod.Logger.Warn("[BiltonsYoyoMod] Yoyo string detour target Main.DrawProj_DrawYoyoString NOT FOUND — vanilla strings will draw.");
                }
                // Color path (zero overdraw): vanilla draws the string itself,
                // we only supply colors for reserved stringColor values via
                // Main.TryApplyingPlayerStringColor (verified static in tModLoader.dll).
                MethodInfo colorTarget = typeof(Main).GetMethod("TryApplyingPlayerStringColor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (colorTarget != null)
                {
                    colorHook = new Hook(colorTarget, (Func<OrigStringColor, int, Color, Color>)ColorDetour);
                    Mod.Logger.Info("[BiltonsYoyoMod] Yoyo string color hook installed on Main.TryApplyingPlayerStringColor.");
                }
                else
                {
                    Mod.Logger.Warn("[BiltonsYoyoMod] Yoyo string color target Main.TryApplyingPlayerStringColor NOT FOUND.");
                }
            }
            catch (Exception e)
            {
                Mod.Logger.Warn("Yoyo string detour install failed: " + e.Message);
            }
        }

        public override void Unload()
        {
            hook?.Dispose();
            hook = null;
            detour = null;
            colorHook?.Dispose();
            colorHook = null;
            styles = null;
            chains.Clear();
            s_volleyEnd = null;
            s_drawPts = null;
            s_liveLogged = false;
        }

        private delegate Color OrigStringColor(int value, Color baseColor);

        private static Color ColorDetour(OrigStringColor orig, int value, Color baseColor)
        {
            if (value >= 1000 && value < 1013)
            {
                if (value == 1012)
                    return new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB);
                return s_stringColors[value - 1000];
            }
            return orig(value, baseColor);
        }

        // Fixed type -> reserved-index map for the stringColor path.
        internal static bool TryGetStringIndex(int type, out int idx)
        {
            if (s_stringIndex == null)
            {
                s_stringIndex = new Dictionary<int, int>();
                try
                {
                    s_stringIndex[ModContent.ProjectileType<DarkMageYoyoProj>()] = 0;
                    s_stringIndex[ModContent.ProjectileType<TorchGodYoyoProj>()] = 1;
                    s_stringIndex[ModContent.ProjectileType<DutchmanYoyoProj>()] = 2;
                    s_stringIndex[ModContent.ProjectileType<OgreYoyoProj>()] = 3;
                    s_stringIndex[ModContent.ProjectileType<DreadnautilusYoyoProj>()] = 4;
                    s_stringIndex[ModContent.ProjectileType<MourningWoodYoyoProj>()] = 5;
                    s_stringIndex[ModContent.ProjectileType<SantaYoyoProj>()] = 6;
                    s_stringIndex[ModContent.ProjectileType<PumpkingYoyoProj>()] = 7;
                    s_stringIndex[ModContent.ProjectileType<EverscreamYoyoProj>()] = 8;
                    s_stringIndex[ModContent.ProjectileType<IceQueenYoyoProj>()] = 9;
                    s_stringIndex[ModContent.ProjectileType<MartianYoyoProj>()] = 10;
                    s_stringIndex[ModContent.ProjectileType<BetsyYoyoProj>()] = 11;
                    s_stringIndex[ModContent.ProjectileType<DeveloperYoyoProj>()] = 12;
                }
                catch
                {
                    s_stringIndex = null;
                    idx = -1;
                    return false;
                }
            }
            return s_stringIndex.TryGetValue(type, out idx);
        }

        // Full family map (mains + subs) -> same reserved index space as the
        // string colors. Strings only need the 13 mains, but marks, sounds,
        // plucks and sigils need every sub-projectile to know its family.
        private static Dictionary<int, int> familyIndex;
        internal static bool TryGetFamilyIndex(int type, out int idx)
        {
            if (familyIndex == null)
            {
                familyIndex = new Dictionary<int, int>();
                try
                {
                    void Add(int i, params int[] types)
                    {
                        for (int t = 0; t < types.Length; t++)
                            familyIndex[types[t]] = i;
                    }
                    Add(0, ModContent.ProjectileType<DarkMageYoyoProj>(), ModContent.ProjectileType<DarkMageYoyoMagicProj>(), ModContent.ProjectileType<DarkMageYoyoMagicRitual>());
                    Add(1, ModContent.ProjectileType<TorchGodYoyoProj>(), ModContent.ProjectileType<TorchGodYoyoTorchProj>());
                    Add(2, ModContent.ProjectileType<DutchmanYoyoProj>(), ModContent.ProjectileType<DutchManYoyoCannonProj>());
                    Add(3, ModContent.ProjectileType<OgreYoyoProj>(), ModContent.ProjectileType<OgreYoyoClubProj>(), ModContent.ProjectileType<OgreYoyoSpitProj>());
                    Add(4, ModContent.ProjectileType<DreadnautilusYoyoProj>(), ModContent.ProjectileType<DreadnautilusYoyoBloodShardProj>(), ModContent.ProjectileType<DreadnautilusBloodOrbProj>(), ModContent.ProjectileType<DreadnautilusTearsProj>(), ModContent.ProjectileType<DreadnautilusHomingBloodSquids>());
                    Add(5, ModContent.ProjectileType<MourningWoodYoyoProj>(), ModContent.ProjectileType<MourningWoodYoyoFireProj>(), ModContent.ProjectileType<MourningWoodYoyoLingeringProj>());
                    Add(6, ModContent.ProjectileType<SantaYoyoProj>(), ModContent.ProjectileType<SantaYoyoBulletProj>(), ModContent.ProjectileType<SantaYoyoSpikyBallProj>(), ModContent.ProjectileType<SantaYoyoMissilesProj>(), ModContent.ProjectileType<SantaYoyoPresentsProj>());
                    Add(7, ModContent.ProjectileType<PumpkingYoyoProj>(), ModContent.ProjectileType<PumpkingYoyoBladeProj>(), ModContent.ProjectileType<PumpkingYoyoScytheProj>(), ModContent.ProjectileType<PumpkingYoyoFireProj>());
                    Add(8, ModContent.ProjectileType<EverscreamYoyoProj>(), ModContent.ProjectileType<EverscreamYoyoNeedleProj>(), ModContent.ProjectileType<EverscreamYoyoBallProj>(), ModContent.ProjectileType<EverscreamYoyoBallProj2>());
                    Add(9, ModContent.ProjectileType<IceQueenYoyoProj>(), ModContent.ProjectileType<IceQueenYoyoShardProj>());
                    Add(10, ModContent.ProjectileType<MartianYoyoProj>(), ModContent.ProjectileType<MartianYoyoLaserProj>(), ModContent.ProjectileType<MartianYoyoDeathRayProj>(), ModContent.ProjectileType<MartianYoyoMissilesProj>());
                    Add(11, ModContent.ProjectileType<BetsyYoyoProj>(), ModContent.ProjectileType<BetsyYoyoFireProj>(), ModContent.ProjectileType<BetsyYoyoFireBallProj>(), ModContent.ProjectileType<BetsyYoyoBreathProj>());
                    Add(12, ModContent.ProjectileType<DeveloperYoyoProj>(), ModContent.ProjectileType<DevYoyoRBProj>());
                }
                catch
                {
                    familyIndex = null;
                    idx = -1;
                    return false;
                }
            }
            return familyIndex.TryGetValue(type, out idx);
        }

        // E4: pluck the owner's deployed main of a family (called from hit FX;
        // subs have no rope of their own, so the family's main rope answers).
        internal static void PluckFamily(int owner, int familyIdx)
        {
            if (owner < 0)
                return;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != owner)
                    continue;
                if (TryGetStringIndex(p.type, out int idx) && idx == familyIdx)
                {
                    PluckString(p.whoAmI);
                    return;
                }
            }
        }

        // ---- E1 volley announcements (tension tells) ----
        // Families announce a scheduled release ~10 ticks out; the rope reads
        // the countdown (tightens + lifts, a bead races to the tip). Purely
        // draw-time: an announcement never gates or delays the attack itself.
        private static int[,] s_volleyEnd;
        private static Vector2[] s_drawPts;
        private const int VolleyWindow = 10;

        internal static void AnnounceVolley(Projectile proj, int ticks)
        {
            if (proj == null)
                return;
            if (!TryGetFamilyIndex(proj.type, out int family))
                return;
            AnnounceVolley(proj.owner, family, ticks);
        }

        internal static void AnnounceVolley(int owner, int family, int ticks)
        {
            if (owner < 0 || owner >= Main.maxPlayers || family < 0 || family > 12 || ticks <= 0)
                return;
            if (s_volleyEnd == null)
                s_volleyEnd = new int[Main.maxPlayers, 13];
            int end = (int)Main.GameUpdateCount + ticks;
            if (end > s_volleyEnd[owner, family])
                s_volleyEnd[owner, family] = end;
        }

        // 0 = slack, rises to 1 at the announced release tick, then 0 again.
        internal static float VolleyTension(int owner, int family)
        {
            if (s_volleyEnd == null || owner < 0 || owner >= Main.maxPlayers || family < 0 || family > 12)
                return 0f;
            int rem = s_volleyEnd[owner, family] - (int)Main.GameUpdateCount;
            if (rem <= 0)
                return 0f;
            return 1f - MathHelper.Clamp(rem / (float)VolleyWindow, 0f, 1f);
        }

        internal static bool TryGetStyle(int type, out string texPath, out Color tint, out bool designed)        {
            if (styles == null)
            {
                styles = new Dictionary<int, (string, Color, bool)>();
                try
                {
                    string p = "BiltonsYoyoMod/Projectiles/YoyoStrings/";
                    styles[ModContent.ProjectileType<DreadnautilusYoyoProj>()] = (p + "StringBlood", Color.White, true);
                    styles[ModContent.ProjectileType<SantaYoyoProj>()] = (p + "StringCandy", Color.White, true);
                    styles[ModContent.ProjectileType<IceQueenYoyoProj>()] = (p + "StringFrost", Color.White, true);
                    styles[ModContent.ProjectileType<MartianYoyoProj>()] = (p + "StringCircuit", Color.White, true);
                    styles[ModContent.ProjectileType<BetsyYoyoProj>()] = (p + "StringEmber", Color.White, true);
                    styles[ModContent.ProjectileType<DarkMageYoyoProj>()] = (p + "StringRune", Color.White, true);
                    styles[ModContent.ProjectileType<DeveloperYoyoProj>()] = (p + "StringRainbow", Color.White, true);
                    styles[ModContent.ProjectileType<PumpkingYoyoProj>()] = (p + "StringHarvest", Color.White, true);
                    styles[ModContent.ProjectileType<TorchGodYoyoProj>()] = (null, new Color(255, 190, 120), false);
                    styles[ModContent.ProjectileType<DutchmanYoyoProj>()] = (null, new Color(150, 200, 190), false);
                    styles[ModContent.ProjectileType<OgreYoyoProj>()] = (null, new Color(120, 200, 120), false);
                    styles[ModContent.ProjectileType<MourningWoodYoyoProj>()] = (null, new Color(255, 180, 110), false);
                    styles[ModContent.ProjectileType<EverscreamYoyoProj>()] = (null, new Color(120, 200, 130), false);
                }
                catch
                {
                    styles = null;
                    texPath = null;
                    tint = Color.White;
                    designed = false;
                    return false;
                }
            }
            if (styles.TryGetValue(type, out var s))
            {
                texPath = s.Item1;
                tint = s.Item2;
                designed = s.Item3;
                return true;
            }
            texPath = null;
            tint = Color.White;
            designed = false;
            return false;
        }

        // ---- Verlet string chain (T10/L12: "verlet rope + twang") ----
        // Pinned-end rope in world space: point 0 pinned to the player's hand,
        // last point pinned to the yoyo. Gravity + spool slack make the sag;
        // momentum + snap make the twang. Purely visual, client-side, stepped
        // once per game tick (repeat calls in the same tick are free).
        private class StringChain
        {
            public const int Points = 9;
            public readonly Vector2[] Pos = new Vector2[Points];
            public readonly Vector2[] Prev = new Vector2[Points];
            public uint LastStep = uint.MaxValue;
            public float LastLen;
            public float SpoolLen; // unreeled length: pays out instantly, reels in slowly
            public float Twang;    // brightness pulse strength 1 -> 0
            public float PulseT;   // pulse travel position along the string
        }

        private static readonly Dictionary<int, StringChain> chains = new Dictionary<int, StringChain>();

        private static StringChain GetChain(int id, Vector2 start, Vector2 end)
        {
            // Hygiene: drop chains that stopped drawing long ago (dead yoyos).
            if (chains.Count > 96)
            {
                uint nowTick = (uint)Main.GameUpdateCount;
                List<int> stale = new List<int>();
                foreach (var kv in chains)
                    if (kv.Value.LastStep != uint.MaxValue && nowTick - kv.Value.LastStep > 600)
                        stale.Add(kv.Key);
                for (int i = 0; i < stale.Count; i++)
                    chains.Remove(stale[i]);
            }
            if (!chains.TryGetValue(id, out StringChain c))
            {
                c = new StringChain();
                chains[id] = c;
                ResetChain(c, start, end);
                return c;
            }
            // Teleport / wild discontinuity: snap the chain back onto the line.
            if (Vector2.DistanceSquared(c.Pos[0], start) > 4096f || Math.Abs(c.LastLen - Vector2.Distance(start, end)) > 300f)
                ResetChain(c, start, end);
            return c;
        }

        private static void ResetChain(StringChain c, Vector2 start, Vector2 end)
        {
            float len = Vector2.Distance(start, end);
            for (int i = 0; i < StringChain.Points; i++)
                c.Pos[i] = c.Prev[i] = Vector2.Lerp(start, end, i / (float)(StringChain.Points - 1));
            c.LastStep = uint.MaxValue;
            c.LastLen = len;
            c.SpoolLen = len;
            c.Twang = 0f;
            c.PulseT = 0f;
        }

        // One Verlet step per game tick: integrate -> distance constraints -> twang.
        private static void StepChain(StringChain c, Vector2 start, Vector2 end, float len)
        {
            uint now = (uint)Main.GameUpdateCount;
            if (c.LastStep == now)
                return;
            c.LastStep = now;

            // Spool: pays out instantly, reels in gradually -> leftover slack droops.
            c.SpoolLen = MathHelper.Max(len, c.SpoolLen * 0.97f);

            const float gravity = 0.24f;
            const float damping = 0.88f;
            for (int i = 1; i < StringChain.Points - 1; i++)
            {
                Vector2 velocity = (c.Pos[i] - c.Prev[i]) * damping;
                c.Prev[i] = c.Pos[i];
                c.Pos[i] += velocity;
                c.Pos[i].Y += gravity;
            }

            // Distance constraints (Verlet/ApplyConstraint pattern): total rest =
            // spool length, so slack strings bend and taut strings stay straight.
            float rest = MathHelper.Max(c.SpoolLen / (StringChain.Points - 1), 1.5f);
            for (int it = 0; it < 4; it++)
            {
                c.Pos[0] = start;
                for (int i = 0; i < StringChain.Points - 1; i++)
                {
                    Vector2 delta = c.Pos[i + 1] - c.Pos[i];
                    float d = delta.Length();
                    if (d < 0.001f)
                        continue;
                    Vector2 corr = delta * ((d - rest) / d);
                    if (i != 0)
                        c.Pos[i] += corr * 0.5f;
                    c.Pos[i + 1] -= corr * (i == 0 ? 1f : 0.5f);
                }
                c.Pos[StringChain.Points - 1] = end;
            }

            // Twang: plucked by a snap in length (recall / hit); pulse rides the rope.
            float dLen = len - c.LastLen;
            c.LastLen = len;
            if (Math.Abs(dLen) > 4f)
            {
                c.Twang = 1f;
                c.PulseT = 0f;
            }
            if (c.Twang > 0f)
            {
                c.PulseT += 0.085f;
                c.Twang -= 0.03f;
                if (c.PulseT > 1.15f || c.Twang <= 0f)
                {
                    c.Twang = 0f;
                    c.PulseT = 0f;
                }
            }
        }

        // Arc-length sample of the chain polyline (position + local tangent).
        private static bool SampleChain(Vector2[] pts, float s, out Vector2 world, out float tangent)
        {
            world = Vector2.Zero;
            tangent = 0f;
            float acc = 0f;
            for (int i = 0; i < StringChain.Points - 1; i++)
            {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
                float seg = Vector2.Distance(a, b);
                if (seg < 0.001f)
                    continue;
                if (s <= acc + seg || i == StringChain.Points - 2)
                {
                    float t = MathHelper.Clamp((s - acc) / seg, 0f, 1f);
                    world = Vector2.Lerp(a, b, t);
                    tangent = (b - a).ToRotation();
                    return true;
                }
                acc += seg;
            }
            return false;
        }

        private void DrawDetour(OrigDrawYoyo orig, Main self, Projectile proj, Vector2 mountedCenter)
        {
            if (!TryGetStyle(proj.type, out string texPath, out Color tint, out bool designed))
            {
                orig(self, proj, mountedCenter);
                return;
            }
            if (proj.owner < 0 || proj.owner >= Main.maxPlayers)
            {
                orig(self, proj, mountedCenter);
                return;
            }
            Player owner = Main.player[proj.owner];
            if (owner == null || !owner.active)
            {
                orig(self, proj, mountedCenter);
                return;
            }
            // Liveness proof: one log line per session when a styled string
            // actually draws. If this never appears, the hook binds a dead
            // method and the PostDraw fallback owns strings instead.
            if (!s_liveLogged)
            {
                s_liveLogged = true;
                Mod.Logger.Info("[BiltonsYoyoMod] Yoyo string detour live draw, type=" + proj.type);
            }

            Vector2 start = mountedCenter;
            Vector2 end = proj.Center;
            float len = Vector2.Distance(start, end);
            if (len < 1f)
                return;

            // Live strings: width + brightness follow yoyo speed.
            float spd = proj.velocity.Length();
            float liveW = 5f + Math.Min(spd * 0.15f, 4f);
            float liveBright = 0.85f + Math.Min(spd * 0.01f, 0.3f);

            // Verlet chain (T10): sag from gravity + spool slack, twang on recall.
            StringChain chain = GetChain(proj.whoAmI, start, end);
            StepChain(chain, start, end, len);
            Vector2[] pts = chain.Pos;
            float chainLen = 0f;
            for (int i = 0; i < StringChain.Points - 1; i++)
                chainLen += Vector2.Distance(pts[i], pts[i + 1]);
            if (chainLen < 1f)
                return;

            // E5: max-range strain - the rope brightens as it runs out of slack.
            float strain = MathHelper.Clamp((len - 220f) / 140f, 0f, 1f);
            liveBright += strain * 0.3f;

            // E1: volley tension - draw-only taut read as an announced release
            // approaches (the chain itself keeps its physics; we shape a copy).
            float volley = 0f;
            try
            {
                var volleyCfg = ModContent.GetInstance<Configuration.YoyoConfig>();
                if ((volleyCfg == null || volleyCfg.Visuals.TensionTells) && TryGetFamilyIndex(proj.type, out int fam))
                    volley = VolleyTension(proj.owner, fam);
            }
            catch { }
            Vector2[] drawPts = pts;
            if (volley > 0.01f)
            {
                if (s_drawPts == null || s_drawPts.Length != pts.Length)
                    s_drawPts = new Vector2[pts.Length];
                for (int i = 0; i < pts.Length; i++)
                {
                    float t = i / (float)(pts.Length - 1);
                    Vector2 straight = Vector2.Lerp(start, end, t);
                    drawPts[i] = Vector2.Lerp(pts[i], straight, volley * 0.8f);
                    drawPts[i].Y -= 6f * volley * (float)System.Math.Sin(t * Math.PI);
                }
            }

            if (designed && texPath != null)
            {
                // Bulletproofed: ANY failure (bad texture, wild length, disposed
                // asset) falls back to vanilla - a string can never break drawing.
                try
                {
                    if (len > 4000f)
                    {
                        orig(self, proj, mountedCenter);
                        return;
                    }
                    Texture2D tex = ModContent.Request<Texture2D>(texPath).Value;
                    if (tex == null || tex.IsDisposed || tex.Width < 2 || tex.Height < 1)
                    {
                        orig(self, proj, mountedCenter);
                        return;
                    }
                    float drawW = liveW + 2f;
                    float segLen = drawW * tex.Height / (float)tex.Width;
                    if (segLen < 2f)
                        segLen = 2f;
                    Vector2 texOrigin = tex.Size() * 0.5f;
                    Vector2 scale = new Vector2(drawW / tex.Width, segLen / tex.Height);
                    // String flow: tiling origin drifts with time so strips crawl.
                    float flow = (Main.GameUpdateCount * 1.5f) % segLen;
                    int tiles = 0;
                    for (float s = segLen * 0.5f - segLen + flow; s < chainLen && tiles < 400; s += segLen, tiles++)
                    {
                        if (!SampleChain(drawPts, s, out Vector2 world, out float tang))
                            break;
                        // Twang pulse: a bright band travels player -> yoyo after a snap.
                        float pulseBoost = 0f;
                        if (chain.Twang > 0f)
                        {
                            float tileT = MathHelper.Clamp(s / chainLen, 0f, 1f);
                            pulseBoost = 0.9f * chain.Twang * MathHelper.Clamp(1f - Math.Abs(tileT - chain.PulseT) / 0.16f, 0f, 1f);
                        }
                        Color stripTint = Color.White * (liveBright + pulseBoost);
                        Main.EntitySpriteDraw(tex, world - Main.screenPosition, null, stripTint,
                            tang + MathHelper.PiOver2, texOrigin, scale, SpriteEffects.None, 0);
                    }
                    // Taut-white core at full stretch (recall read).
                    if (len > 200f)
                    {
                        float taut = MathHelper.Clamp((len - 200f) / 120f, 0f, 1f);
                        if (taut > 0.05f)
                            for (int i = 0; i < StringChain.Points - 1; i++)
                                MiniYoyoFx.DrawLine(drawPts[i] - Main.screenPosition, drawPts[i + 1] - Main.screenPosition, 1f, Color.White * (0.5f * taut));
                    }
                }
                catch
                {
                    try { orig(self, proj, mountedCenter); } catch { }
                    return;
                }
            }
            else
            {
                try
                {
                    Texture2D pixel = TextureAssets.MagicPixel.Value;
                    for (int i = 0; i < StringChain.Points - 1; i++)
                    {
                        Vector2 a = drawPts[i];
                        Vector2 b = drawPts[i + 1];
                        Vector2 segDiff = b - a;
                        float seg = segDiff.Length();
                        if (seg < 0.02f)
                            continue;
                        Main.EntitySpriteDraw(pixel, (a + b) * 0.5f - Main.screenPosition, new Rectangle(0, 0, 1, 1), tint * liveBright,
                            segDiff.ToRotation(), new Vector2(0.5f, 0.5f), new Vector2(seg + 1f, 2f), SpriteEffects.None, 0);
                    }
                    // Taut-white core at full stretch, same read as the strips.
                    if (len > 200f)
                    {
                        float taut = MathHelper.Clamp((len - 200f) / 120f, 0f, 1f);
                        if (taut > 0.05f)
                            for (int i = 0; i < StringChain.Points - 1; i++)
                                MiniYoyoFx.DrawLine(drawPts[i] - Main.screenPosition, drawPts[i + 1] - Main.screenPosition, 1f, Color.White * (0.5f * taut));
                    }
                }
                catch
                {
                    try { orig(self, proj, mountedCenter); } catch { }
                    return;
                }
            }

            // E2/E3/E5: material pips, pluck whip and strain shimmer ride on
            // top of whichever branch drew the rope itself.
            try { DrawStringMaterial(proj.type, drawPts, chainLen, liveW, chain, strain, volley); } catch { }
        }

        // ---- E-series string layers (E2 whip / E3 materials / E5 strain) ----
        // One compact dispatch: each family gets a coloured bead flow along its
        // rope (direction + count differ), a fresh-pluck whip overlay, and a
        // strain tick at mid-rope near max range. Max 3-4 soft glows per string
        // per frame, so thirteen deployed strings stay cheap.
        private static void DrawStringMaterial(int type, Vector2[] pts, float chainLen, float liveW, StringChain chain, float strain, float volley)
        {
            if (chainLen < 10f)
                return;
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            if (cfg != null && !cfg.Visuals.StringMaterial)
                return;
            if (!TryGetStringIndex(type, out int idx))
                return;

            // E2: fresh >4px snap (recall yank / hit pluck): whole-rope whip.
            if (chain.Twang > 0.72f)
            {
                float a = (chain.Twang - 0.72f) / 0.28f;
                for (int i = 0; i < StringChain.Points - 1; i++)
                    MiniYoyoFx.DrawLine(pts[i] - Main.screenPosition, pts[i + 1] - Main.screenPosition, liveW * 0.45f, Color.White * (0.5f * a));
                MiniYoyoFx.DrawStarSparkle(pts[StringChain.Points / 2], Color.White, 0.8f + 0.5f * a, 0f, 0.8f * a);
            }

            Color pipCol;
            float pipSize;
            int pips;
            switch (idx)
            {
                case 0: pipCol = new Color(190, 130, 255); pipSize = 4f; pips = 2; break;   // DarkMage runes
                case 1: pipCol = new Color(255, 170, 90); pipSize = 5f; pips = 2; break;   // TorchGod fuse spark
                case 2: pipCol = new Color(170, 210, 200); pipSize = 4f; pips = 1; break;  // Dutchman mist
                case 3: pipCol = new Color(170, 230, 150); pipSize = 4f; pips = 1; break;  // Ogre grease bead
                case 4: pipCol = new Color(230, 90, 100); pipSize = 4.5f; pips = 2; break; // Dread blood bead
                case 5: pipCol = new Color(255, 180, 110); pipSize = 4f; pips = 1; break;  // MourningWood ember
                case 6: pipCol = new Color(255, 230, 190); pipSize = 4f; pips = 1; break;  // Santa bell glint
                case 7: pipCol = new Color(255, 190, 120); pipSize = 4f; pips = 1; break;  // Pumpking smolder
                case 8: pipCol = new Color(170, 230, 160); pipSize = 4f; pips = 1; break;  // Everscream needle fleck
                case 9: pipCol = new Color(190, 235, 255); pipSize = 4f; pips = 1; break;  // IceQueen frost glint
                case 10: pipCol = new Color(150, 255, 190); pipSize = 4f; pips = 3; break; // Martian data pulses
                case 11: pipCol = new Color(255, 170, 90); pipSize = 4.5f; pips = 2; break;// Betsy heat bead
                case 12: pipCol = new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB); pipSize = 5f; pips = 3; break; // Developer disco
                default: pipCol = Color.White; pipSize = 4f; pips = 1; break;
            }

            float flow = (Main.GameUpdateCount * 0.012f) % 1f;
            for (int i = 0; i < pips; i++)
            {
                float t = (flow + i / (float)pips) % 1f;
                if (!SampleChain(pts, t * chainLen, out Vector2 world, out _))
                    continue;
                MiniYoyoFx.DrawGlow(world, pipSize, pipCol, 0.5f);
            }

            if (strain > 0.6f && Main.GameUpdateCount % 8 < 3)
            {
                if (SampleChain(pts, chainLen * 0.5f, out Vector2 mid, out _))
                    MiniYoyoFx.DrawGlow(mid + new Vector2(0f, -2f), 6f, Color.White, 0.4f * strain);
            }

            // E1: the tell itself - a bright bead races to the yoyo ahead of the
            // release, and the tip halo swells just before it fires.
            if (volley > 0.05f)
            {
                if (SampleChain(pts, chainLen * (0.3f + 0.65f * volley), out Vector2 tw, out _))
                    MiniYoyoFx.DrawGlow(tw, 5f + 5f * volley, Color.White, 0.3f + 0.45f * volley);
                if (chainLen > 8f && SampleChain(pts, chainLen - 2f, out Vector2 tip, out _))
                    MiniYoyoFx.DrawGlow(tip, 9f + 9f * volley, pipCol, 0.25f + 0.4f * volley);
            }
        }

        // E4: external pluck (YoyoKillFX calls this on every landed hit).
        internal static void PluckString(int projectileWhoAmI)
        {
            if (chains.TryGetValue(projectileWhoAmI, out StringChain c))
            {
                c.Twang = 1f;
                c.PulseT = 0f;
            }
        }
    }

    // String color driver: after vanilla equips run, a deployed mini-boss
    // yoyo claims the player's stringColor with a reserved value (1000+idx).
    // Vanilla then draws the string in our color via TryApplyingPlayerStringColor
    // (color hook above) - zero extra draws, zero overdraw. A player's own
    // equipped string accessory (nonzero stringColor) always wins.
    // Fishing crossover: holding one of our yoyo items tints the fishing line
    // too (vanilla uses stringColor for rods), so disco fishing just works.
    public class YoyoStringPlayer : ModPlayer
    {
        public override void PostUpdateEquips()
        {
            if (Player.stringColor != 0)
                return;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Player.whoAmI && YoyoStringHookSystem.TryGetStringIndex(p.type, out int idx))
                {
                    Player.stringColor = 1000 + idx;
                    return;
                }
            }
            if (Player.HeldItem != null && YoyoStringHookSystem.TryGetStringIndex(Player.HeldItem.shoot, out int heldIdx))
                Player.stringColor = 1000 + heldIdx;
        }
    }
}
