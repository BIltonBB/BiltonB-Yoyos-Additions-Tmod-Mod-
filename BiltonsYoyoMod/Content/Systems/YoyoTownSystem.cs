using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using BiltonsYoyoMod.Content;
using BiltonsYoyoMod.Content.NPCs.Town;
using BiltonsYoyoMod.Items.TierEmblems;

namespace BiltonsYoyoMod.Content.Systems
{
	// World state for the revamp Town NPCs:
	// - Tier Emblem shop unlocks (Yoyo Master sells a Tier Emblem only after the
	//   player has possessed 3 copies of that same Tier Emblem simultaneously).
	// - Nightly spawning of Six Stringer / Yoyo Cultist (Traveling-Merchant style).
	// - Billy the Bearing upgrade tracking.
	public class YoyoTownSystem : ModSystem
	{
		// Emblem type -> unlocked.
		private static readonly Dictionary<int, bool> EmblemUnlocked = new();

		// Billy the Bearing: which bearing item types have been installed.
		public static readonly HashSet<int> BillyUpgrades = new();

		// Installed bearings the player switched off (click again to refit).
		public static readonly HashSet<int> BillyInactive = new();

		private static int spawnCheckTimer;

		public override void OnWorldLoad()
		{
			EmblemUnlocked.Clear();
			BillyUpgrades.Clear();
			BillyInactive.Clear();
			spawnCheckTimer = 0;
		}

		public override void SaveWorldData(TagCompound tag)
		{
			var unlocked = new List<int>();
			foreach (var kv in EmblemUnlocked) {
				if (kv.Value)
					unlocked.Add(kv.Key);
			}
			tag["emblemUnlocks"] = unlocked;
			tag["billyUpgrades"] = new List<int>(BillyUpgrades);
			tag["billyInactive"] = new List<int>(BillyInactive);
		}

		public override void LoadWorldData(TagCompound tag)
		{
			EmblemUnlocked.Clear();
			if (tag.ContainsKey("emblemUnlocks")) {
				foreach (int type in tag.Get<List<int>>("emblemUnlocks"))
					EmblemUnlocked[type] = true;
			}
			BillyUpgrades.Clear();
			if (tag.ContainsKey("billyUpgrades")) {
				foreach (int type in tag.Get<List<int>>("billyUpgrades"))
					BillyUpgrades.Add(type);
			}
			BillyInactive.Clear();
			if (tag.ContainsKey("billyInactive")) {
				foreach (int type in tag.Get<List<int>>("billyInactive"))
					BillyInactive.Add(type);
			}
		}

		public static bool IsEmblemUnlocked(int itemType)
		{
			return EmblemUnlocked.TryGetValue(itemType, out bool unlocked) && unlocked;
		}

		private static readonly int[] TrackedEmblems = new int[11];

		private static bool emblemTypesCached;

		private static void CacheEmblemTypes()
		{
			if (emblemTypesCached)
				return;
			emblemTypesCached = true;
			TrackedEmblems[0] = ModContent.ItemType<T0Emblem>();
			TrackedEmblems[1] = ModContent.ItemType<T1Emblem>();
			TrackedEmblems[2] = ModContent.ItemType<T2Emblem>();
			TrackedEmblems[3] = ModContent.ItemType<T3Emblem>();
			TrackedEmblems[4] = ModContent.ItemType<T4Emblem>();
			TrackedEmblems[5] = ModContent.ItemType<T5Emblem>();
			TrackedEmblems[6] = ModContent.ItemType<T6Emblem>();
			TrackedEmblems[7] = ModContent.ItemType<T7Emblem>();
			TrackedEmblems[8] = ModContent.ItemType<T8Emblem>();
			TrackedEmblems[9] = ModContent.ItemType<T9Emblem>();
			TrackedEmblems[10] = ModContent.ItemType<T10Emblem>();
		}

		public override void PostUpdatePlayers()
		{
			CacheEmblemTypes();
			// If any player currently holds 3+ copies of an emblem, unlock it forever.
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (player == null || !player.active)
					continue;
				foreach (int emblemType in TrackedEmblems) {
					if (IsEmblemUnlocked(emblemType))
						continue;
					if (YoyoTownShared.CountInInventory(player, emblemType) >= 3)
						EmblemUnlocked[emblemType] = true;
				}
			}
		}

		public override void PostUpdateWorld()
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			spawnCheckTimer++;
			if (spawnCheckTimer < 600) // Check every 10 seconds.
				return;
			spawnCheckTimer = 0;
			if (Main.dayTime)
				return;
			TrySpawnTraveller<SixStringer>(ShouldSpawnSixStringer(), 1f / 25f);
			TrySpawnTraveller<YoyoCultist>(ShouldSpawnCultist(), Main.hardMode ? 1f / 40f : 1f / 60f);
		}

		private static bool AnyTravellerPresent()
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc.active && (npc.type == ModContent.NPCType<SixStringer>() || npc.type == ModContent.NPCType<YoyoCultist>() || npc.type == NPCID.TravellingMerchant))
					return true;
			}
			return false;
		}

		private static bool ShouldSpawnSixStringer()
		{
			// Cowboys of Yoyos + Eye of Cthulhu defeated.
			return YoyoGuardianWorld.downedCowboyOfYoyos && NPC.downedBoss1;
		}

		private static bool ShouldSpawnCultist()
		{
			// Skeletron defeated + at least one town NPC living here.
			if (!NPC.downedBoss3)
				return false;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc.active && npc.townNPC && npc.type != ModContent.NPCType<SixStringer>() && npc.type != ModContent.NPCType<YoyoCultist>())
					return true;
			}
			return false;
		}

		private static void TrySpawnTraveller<T>(bool conditionsMet, float chance) where T : ModNPC
		{
			if (!conditionsMet || AnyTravellerPresent())
				return;
			if (Main.rand.NextFloat() > chance)
				return;
			SpawnTraveller<T>();
		}

		public static bool SpawnTraveller<T>() where T : ModNPC
		{
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (player == null || !player.active || player.dead)
					continue;
				// Land in the town area (average town-NPC position), not on
				// top of the player - fall back to the player if homeless.
				Vector2 anchor = player.Center;
				Vector2 sum = Vector2.Zero;
				int homes = 0;
				for (int n = 0; n < Main.maxNPCs; n++) {
					NPC npc = Main.npc[n];
					if (npc.active && npc.townNPC && !npc.homeless) {
						sum += npc.Center;
						homes++;
					}
				}
				if (homes > 0)
					anchor = sum / homes;
				int x = (int)anchor.X + Main.rand.Next(-160, 161);
				int y = (int)anchor.Y - 200;
				int index = NPC.NewNPC(player.GetSource_Misc("yoyo_traveller_spawn"), x, y, ModContent.NPCType<T>());
				if (index >= 0) {
					if (Main.netMode == NetmodeID.Server)
						NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);
					AnnounceArrival<T>();
					return true;
				}
			}
			return false;
		}

		private static void AnnounceArrival<T>() where T : ModNPC
		{
			string name = typeof(T) == typeof(SixStringer) ? "Six Stringer" : "The Yoyo Cultist";
			Terraria.Chat.ChatHelper.BroadcastChatMessage(Terraria.Localization.NetworkText.FromLiteral($"{name} has arrived!"), new Color(220, 190, 120));
		}

		// Drops a given number of lesser life potions scaled by accessory value.
		public static void GiveLifePotions(Player player, int valueCopper)
		{
			int count = Terraria.Utils.Clamp(valueCopper / 5000, 1, 99); // ~5 silver each.
			Item.NewItem(player.GetSource_Misc("yoyo_trade"), player.Center, ItemID.LesserHealingPotion, count);
		}
	}
}
