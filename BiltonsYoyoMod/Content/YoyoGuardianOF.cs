using System.ComponentModel;
using Microsoft.Xna.Framework.Audio;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;
using BiltonsYoyoMod.Content.NPCs.Bosses;
using BiltonsYoyoMod.Configuration;
using ReLogic.Content;

namespace BiltonsYoyoMod.Content.Systems
{
    // Fix #5: AfterMusicMode enum + PlayDefeatAfterMusic now live in
    // Configuration.YoyoConfig (merged configs). Player persistence below.

    // Fix #5 (part 2): per-player persistence for the "On" mode's one-time play, and for
    // detecting a mode switch (which resets that counter).
    public class YoyoGuardianMusicPlayer : ModPlayer
    {
        public bool HasPlayedOnMode;
        public AfterMusicMode LastSeenMode = AfterMusicMode.Always;

        public override void SaveData(TagCompound tag)
        {
            tag["ygHasPlayedOnMode"] = HasPlayedOnMode;
            tag["ygLastSeenMode"] = (int)LastSeenMode;
        }

        public override void LoadData(TagCompound tag)
        {
            HasPlayedOnMode = tag.GetBool("ygHasPlayedOnMode");
            LastSeenMode = tag.ContainsKey("ygLastSeenMode") ? (AfterMusicMode)tag.GetInt("ygLastSeenMode") : AfterMusicMode.Always;
        }
    }

    // Fix #10: the aftermusic (and the Phase 3 death one-shot fired from YoyoGuardian.OnKill)
    // are both played via SoundEngine.PlaySound as plain one-shot sounds rather than through
    // the Music-slot system (see the no-loop note on YoyoGuardianAfterMusic below). That means
    // the underlying SoundEffect asset would otherwise get decoded/loaded the FIRST time it's
    // ever played — which is exactly the moment the boss dies. That synchronous load is the
    // lag spike. Pre-touching the asset once here, at mod load (off the gameplay critical
    // path), means it's already resident in memory by the time the fight can possibly need it.
    public class YoyoGuardianAudioPreloadSystem : ModSystem
    {
        public const string AfterMusicPath = "BiltonsYoyoMod/Content/Music/YGAM";

		public override void Load()
		{
			if (Main.dedServ) return;
			// Async preload (not ImmediateLoad): the asset is still resident long
			// before any fight can need it, without blocking mod loading.
			ModContent.Request<SoundEffect>(AfterMusicPath, AssetRequestMode.AsyncLoad);
		}
    }

    // Fix #5 (part 3): rewritten from a looping ModSceneEffect track to a single one-shot
    // SoundEngine.PlaySound call. A Music-slot track is, by design, looped by Terraria's music
    // engine for as long as its scene effect reports itself active — there's no clean way to
    // have a Music-slot track play through once and then stop from inside that system. Playing
    // it as a plain sound effect instead guarantees it plays exactly once, start to finish, and
    // never loops.
    public static class YoyoGuardianAfterMusic
    {
        public static void Start()
        {
            if (Main.dedServ || Main.myPlayer < 0) return;

            var config = ModContent.GetInstance<YoyoConfig>();
            var musicPlayer = Main.LocalPlayer.GetModPlayer<YoyoGuardianMusicPlayer>();

            // Fix #5: switching modes (Always/On/Off, in either direction) resets the
            // "played once" counter used by On.
            if (musicPlayer.LastSeenMode != config.PlayDefeatAfterMusic)
            {
                musicPlayer.HasPlayedOnMode = false;
                musicPlayer.LastSeenMode = config.PlayDefeatAfterMusic;
            }

            switch (config.PlayDefeatAfterMusic)
            {
                case AfterMusicMode.Off:
                    return;
                case AfterMusicMode.On:
                    if (musicPlayer.HasPlayedOnMode) return;
                    musicPlayer.HasPlayedOnMode = true;
                    break;
                case AfterMusicMode.Always:
                    break;
            }

            SoundEngine.PlaySound(new SoundStyle(YoyoGuardianAudioPreloadSystem.AfterMusicPath) { Volume = 1f });
        }
    }

    public class YoyoGuardianAfterMusicGlobalNPC : GlobalNPC
    {
        public override void OnKill(NPC npc)
        {
            if (npc.type != ModContent.NPCType<YoyoGuardian>())
                return;

            if (npc.ModNPC is not YoyoGuardian guardian)
                return;

            if (!guardian.IsRealBoss)
                return;

            YoyoGuardianAfterMusic.Start();
        }
    }
}