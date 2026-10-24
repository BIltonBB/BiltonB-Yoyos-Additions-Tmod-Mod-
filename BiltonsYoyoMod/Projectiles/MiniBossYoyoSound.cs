using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Projectiles
{
    // H-series family voices, trimmed on feedback: per-spawn ticks and the
    // per-hit ladder were removed because any sound attached to a repeating
    // fire event is noise pollution. What remains is the death voice (one
    // blip per family, throttled to one per 1/2 s per player so clustered
    // deaths speak once) and Volley, used only on one-off dramatic beats
    // (e.g. the Dutchman's hull break, not its cannon volleys).
    // Everything respects Visuals.FamilyVoices, scales with ParticleAmount
    // (H6: the audio budget is part of the particle budget), and family 8/9
    // (Everscream / IceQueen) stay deliberately quiet (H5 silence discipline).
    internal static class MiniBossYoyoSound
    {
        private static SoundStyle[] spawnStyles;
        private static SoundStyle[] deathStyles;

        private static readonly int[] lastDeathTick = new int[Main.maxPlayers];

        private static void EnsureTables()
        {
            if (spawnStyles != null)
                return;
            spawnStyles = new SoundStyle[]
            {
                SoundID.Item8,   // 0 DarkMage: arcane blip
                SoundID.Item17,  // 1 TorchGod: flame toss
                SoundID.Item14,  // 2 Dutchman: distant powder thump
                SoundID.Item1,   // 3 Ogre: heavy heave
                SoundID.Item35,  // 4 Dreadnautilus: wet pull
                SoundID.Item8,   // 5 MourningWood: hollow moan blip
                SoundID.Item29,  // 6 Santa: gift chime
                SoundID.Item17,  // 7 Pumpking: lantern flare
                SoundID.Item16,  // 8 Everscream: (kept quiet below)
                SoundID.Item16,  // 9 IceQueen: (kept quiet below)
                SoundID.Item15,  // 10 Martian: servo zap
                SoundID.Item14,  // 11 Betsy: draconic pressure pop
                SoundID.Item29,  // 12 Developer: debug chime
            };
            deathStyles = new SoundStyle[]
            {
                SoundID.Item8,   // 0
                SoundID.Item17,  // 1
                SoundID.Item16,  // 2
                SoundID.Item1,   // 3
                SoundID.Item35,  // 4
                SoundID.Item8,   // 5
                SoundID.Item29,  // 6
                SoundID.Item17,  // 7
                SoundID.Item16,  // 8
                SoundID.Item16,  // 9
                SoundID.Item15,  // 10
                SoundID.Item16,  // 11
                SoundID.Item29,  // 12
            };
        }

        private static bool Allowed(out float volumeScale)
        {
            volumeScale = 1f;
            var cfg = ModContent.GetInstance<Configuration.YoyoConfig>();
            if (cfg != null && !cfg.Visuals.FamilyVoices)
                return false;
            if (cfg != null)
                volumeScale = MathHelper.Clamp(cfg.Visuals.ParticleAmount, 0.35f, 1.4f);
            return Main.netMode != NetmodeID.Server;
        }

        // Sub-projectile death voice; the ONLY automatic voice left. Throttled
        // hard (one per half second per player) and quiet, so clustered deaths
        // (presents landing, shards popping) fade into the mix instead of
        // stacking into noise. Family 8/9 stay silent by doctrine.
        internal static void DeathVoice(int owner, int family, Vector2 pos, bool quiet)
        {
            if (family < 0 || family > 12)
                return;
            EnsureTables();
            if (family == 8 || family == 9)
                return;                               // doctrine: stay quiet
            if (!Allowed(out float vol))
                return;
            if (owner >= 0 && owner < Main.maxPlayers)
            {
                int now = (int)Main.GameUpdateCount;
                if (now - lastDeathTick[owner] < 30)
                    return;                            // clustered deaths speak once
                lastDeathTick[owner] = now;
            }
            float pitch = -0.2f + (family % 5) * 0.05f + (quiet ? -0.15f : 0f);
            SoundEngine.PlaySound(deathStyles[family] with { Volume = (quiet ? 0.10f : 0.2f) * vol, Pitch = pitch }, pos);
        }

        // H3: rare one-off beats only (e.g. the Dutchman's hull break) reuse the
        // family's own voice at event scale. NEVER call this from a repeating
        // fire site - that is exactly the noise-pollution pattern that got the
        // spawn/hit/barrage sounds removed.
        internal static void Volley(int owner, int family, Vector2 pos, float volume = 0.5f, float pitch = -0.2f)
        {
            if (family < 0 || family > 12)
                return;
            EnsureTables();
            if (!Allowed(out float vol))
                return;
            SoundEngine.PlaySound(spawnStyles[family] with { Volume = volume * vol, Pitch = pitch }, pos);
        }
    }
}
