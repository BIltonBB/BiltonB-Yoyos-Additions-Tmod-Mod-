using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using BiltonsYoyoMod.Content.NPCs.SubBosses;

namespace BiltonsYoyoMod.Content.Systems
{
    // Bearing Party:
    // - Can naturally start in the Cavern layer after Skeletron.
    // - Lasts 10 minutes.
    // - Wild Bearings become much more common while active.
    // - Killing 100 Wild Bearings spawns Giant Bearing.
    public class BearingPartyEvent : ModSystem
    {
        public const int EventDurationTicks = 10 * 60 * 60;
        public const int RequiredKills = 100;

        private const int CheckIntervalTicks = 60;
        private const float NaturalStartChanceNotDowned = 1f / 3600f;
        private const float NaturalStartChanceDowned = 1f / 21600f;

        public static bool Active { get; private set; }
        public static int TimeLeft { get; private set; }
        public static int Kills { get; private set; }

        private static int checkTimer;

        public override void OnWorldLoad()
        {
            Active = false;
            TimeLeft = 0;
            Kills = 0;
            checkTimer = 0;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["bearingPartyActive"] = Active;
            tag["bearingPartyTimeLeft"] = TimeLeft;
            tag["bearingPartyKills"] = Kills;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Active = tag.GetBool("bearingPartyActive");
            TimeLeft = tag.GetInt("bearingPartyTimeLeft");
            Kills = tag.GetInt("bearingPartyKills");
        }

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (Active)
            {
                TimeLeft--;

                if (TimeLeft <= 0)
                    EndEvent(false);

                return;
            }

            checkTimer++;

            if (checkTimer < CheckIntervalTicks)
                return;

            checkTimer = 0;
            TryNaturalStart();
        }

        private void TryNaturalStart()
        {
            if (!AnyEligiblePlayerPresent())
                return;

            float chance = YoyoGuardianWorld.downedGiantBearing
                ? NaturalStartChanceDowned
                : NaturalStartChanceNotDowned;

            if (Main.rand.NextFloat() < chance)
                StartEvent();
        }

        private static bool AnyEligiblePlayerPresent()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (player.active &&
                    !player.dead &&
                    player.ZoneRockLayerHeight &&
                    NPC.downedBoss3)
                {
                    return true;
                }
            }

            return false;
        }

        public static void StartEvent(bool force = false)
        {
            if (Active)
                return;

            if (!force && !AnyEligiblePlayerPresent())
                return;

            Active = true;
            TimeLeft = EventDurationTicks;
            Kills = 0;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Main.NewText(
                    "You hear distant metallic rattling... a Bearing Party has started!",
                    220,
                    180,
                    80
                );
            }
        }

        private static void EndEvent(bool spawnedBoss)
        {
            Active = false;
            TimeLeft = 0;
            Kills = 0;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (!spawnedBoss)
            {
                Main.NewText(
                    "The Bearing Party has ended.",
                    220,
                    180,
                    80
                );
            }
        }

        public static void RegisterKill()
        {
            if (!Active)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

			Kills++;

			if (Kills == 25 || Kills == 50 || Kills == 75) {
				Main.NewText($"Bearing Party frenzy: {Kills}/100 bearings smashed!", 220, 180, 80);
				Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Roar);
			}
			else if (Kills == 90) {
				Main.NewText("The rattling is deafening... something HUGE is coming!", 255, 120, 60);
				Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Roar);
			}

			if (Kills < RequiredKills)
				return;

            Main.NewText(
                "The Bearing Party has reached its peak!",
                220,
                180,
                80
            );

            SpawnGiantBearingNearAnyPlayer();
            EndEvent(true);
        }

        private static void SpawnGiantBearingNearAnyPlayer()
        {
            int targetPlayer = -1;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (!player.active || player.dead)
                    continue;

                if (!player.ZoneRockLayerHeight)
                    continue;

                targetPlayer = i;
                break;
            }

            if (targetPlayer == -1)
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (Main.player[i].active && !Main.player[i].dead)
                    {
                        targetPlayer = i;
                        break;
                    }
                }
            }

            if (targetPlayer == -1)
                return;

            Player target = Main.player[targetPlayer];

            NPC.NewNPC(
                target.GetSource_ItemUse(target.HeldItem),
                (int)target.Center.X,
                (int)target.Center.Y - 200,
                ModContent.NPCType<GiantBearing>()
            );
        }
    }
}