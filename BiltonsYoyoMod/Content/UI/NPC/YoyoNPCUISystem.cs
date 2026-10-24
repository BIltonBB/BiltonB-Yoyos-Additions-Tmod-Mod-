using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace BiltonsYoyoMod.Content.UI.YoyoNPC
{
	// Tab ids per NPC. Every NPC gets 5 direct-access tabs (no merged cycling).
	public static class YoyoNPCTabs
	{
		public static string[] GetTabs(int npcType)
		{
			if (npcType == ModContent.NPCType<Content.NPCs.Town.YoyoMaster>())
				return new[] { "shop", "help", "talk", "trade", "happiness" };
			if (npcType == ModContent.NPCType<Content.NPCs.Town.YoyoTinkerer>())
				return new[] { "shop", "mods", "talk", "help", "happiness" };
			if (npcType == ModContent.NPCType<Content.NPCs.Town.AncientYoyoist>())
				return new[] { "shop", "spells", "curses", "talk", "help", "happiness" };
			if (npcType == ModContent.NPCType<Content.NPCs.Town.SixStringer>())
				return new[] { "shop", "talk", "help", "deals", "rumors" };
			if (npcType == ModContent.NPCType<Content.NPCs.Town.YoyoCultist>())
				return new[] { "shop", "talk", "help", "lore", "happiness" };
			if (npcType == ModContent.NPCType<Content.NPCs.Town.Pets.BillyTheBearing>())
				return new[] { "pet", "upgrade", "status", "help", "farewell" };
			if (npcType == ModContent.NPCType<Content.NPCs.Town.Pets.MagicalYoyoPet>())
				return new[] { "pet", "essence", "help", "about", "farewell" };
			return new string[0];
		}

		public static string TabTitle(string tabId)
		{
			return tabId switch {
				"shop" => "Shop",
				"help" => "Help",
				"talk" => "Talk",
				"trade" => "Trade",
				"happiness" => "Mood",
				"mods" => "Mods",
				"spells" => "Spells",
				"curses" => "Curses",
				"deals" => "Deals",
				"rumors" => "Rumors",
				"lore" => "Lore",
				"pet" => "Pet",
				"upgrade" => "Upgrade",
				"status" => "Status",
				"essence" => "Essence",
				"about" => "About",
				"farewell" => "Leave",
				_ => tabId,
			};
		}

		public static string DefaultTab(int npcType)
		{
			if (npcType == ModContent.NPCType<Content.NPCs.Town.Pets.BillyTheBearing>()) return "pet";
			if (npcType == ModContent.NPCType<Content.NPCs.Town.Pets.MagicalYoyoPet>()) return "pet";
			if (npcType == ModContent.NPCType<Content.NPCs.Town.YoyoTinkerer>()) return "mods";
			if (npcType == ModContent.NPCType<Content.NPCs.Town.AncientYoyoist>()) return "spells";
			return "shop";
		}
	}

	// Owns the custom 5-button NPC panel: open/close state, right-click detection,
	// per-session rolls, transient messages, and the interface layer hookup.
	public class YoyoNPCUISystem : ModSystem
	{
		public static bool Open;
		public static int NpcType = -1;
		public static int NpcWho = -1;
		public static string TabId = "shop";
		public static string Sub = "";
		public static int Page;
		public static int SelectedTier = 2;
		public static string Message = "";
		public static int MessageTimer;
		public static string TalkLine = "";
		public static string RumorLine = "";
		public static int ListScroll;
		public static int TradeScroll;
		public static int MoodScroll;
		public static int UpgradeScroll;
		public static int HelpStage = -1; // -1 = current stage.
		public static List<YoyoShopOffer> CachedOffers = new();

		private static bool prevRight;
		private static bool prevEsc;
		private static int prevWheel;

		public static bool IsYoyoNPC(int type)
		{
			return YoyoNPCTabs.GetTabs(type).Length > 0;
		}

		public static void OpenPanel(NPC npc)
		{
			Open = true;
			NpcType = npc.type;
			NpcWho = npc.whoAmI;
			TabId = YoyoNPCTabs.DefaultTab(npc.type);
			Sub = "";
			Page = 0;
			Message = "";
			MessageTimer = 0;
			TalkLine = YoyoNPCDialogue.RandomGreeting(npc.type);
			RumorLine = YoyoNPCDialogue.RandomTalk(npc.type);
			ListScroll = 0;
			TradeScroll = 0;
			MoodScroll = 0;
			UpgradeScroll = 0;
			HelpStage = -1;
			YoyoNPCShopData.SessionRareStock = Main.rand.NextBool(3);
			YoyoNPCShopData.SessionLuckyStock = Main.rand.NextBool(8);
			YoyoNPCShopData.SessionMaterialStock = Main.rand.NextBool(2);
			CachedOffers = YoyoNPCShopData.GetOffers(npc.type, Main.LocalPlayer);
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuOpen);
		}

		public static void ClosePanel()
		{
			if (!Open)
				return;
			Open = false;
			NpcType = -1;
			NpcWho = -1;
			Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuClose);
		}

		public static void SetMessage(string text, int time = 300)
		{
			Message = text;
			MessageTimer = time;
		}

		public static void RefreshOffers()
		{
			if (NpcType >= 0)
				CachedOffers = YoyoNPCShopData.GetOffers(NpcType, Main.LocalPlayer);
		}

		public static NPC TargetNPC()
		{
			if (!Open || NpcWho < 0 || NpcWho >= Main.maxNPCs)
				return null;
			NPC npc = Main.npc[NpcWho];
			if (!npc.active || npc.type != NpcType)
				return null;
			return npc;
		}

		public override void PostUpdateInput()
		{
			if (Main.gameMenu || Main.dedServ)
				return;
			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead) {
				prevRight = Main.mouseRight;
				return;
			}

			// Right-click an NPC to open its panel (replaces vanilla 2-button chat).
			bool right = Main.mouseRight;
			if (right && !prevRight && !Main.mapFullscreen && !Main.blockMouse) {
				NPC hit = FindNPCUnderMouse();
				if (hit != null && player.Distance(hit.Center) < 560f) {
					if (Open && NpcWho == hit.whoAmI)
						ClosePanel();
					else
						OpenPanel(hit);
					Main.mouseRightRelease = false;
				}
				else if (Open && !YoyoNPCPanel.MouseOverPanel()) {
					ClosePanel();
				}
			}
			prevRight = right;

			// Esc closes the panel.
			bool esc = Main.keyState.IsKeyDown(Keys.Escape);
			if (esc && !prevEsc && Open) {
				ClosePanel();
			}
			prevEsc = esc;

			// Mouse wheel scrolls long lists (mods, grimoire) while hovering the panel.
			// Uses the raw cumulative wheel value so the delta math is unambiguous.
			int wheelNow = Mouse.GetState().ScrollWheelValue;
			int wheel = wheelNow - prevWheel;
			prevWheel = wheelNow;
			if (Open && wheel != 0 && YoyoNPCPanel.MouseOverPanel()) {
				ListScroll -= System.Math.Sign(wheel) * 2;
				if (ListScroll < 0)
					ListScroll = 0;
				// Eat the wheel so it doesn't also flip the hotbar.
				Main.LocalPlayer.mouseInterface = true;
			}
		}

		public override void UpdateUI(GameTime gameTime)
		{
			if (!Open)
				return;
			if (MessageTimer > 0 && --MessageTimer <= 0)
				Message = "";
			NPC npc = TargetNPC();
			Player player = Main.LocalPlayer;
			if (npc == null || player == null || !player.active || player.dead || player.Distance(npc.Center) > 900f)
				ClosePanel();
		}

		private static NPC FindNPCUnderMouse()
		{
			Point mouse = Main.MouseScreen.ToPoint();
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || !IsYoyoNPC(npc.type))
					continue;
				Rectangle box = new(
					(int)(npc.position.X - Main.screenPosition.X) - 14,
					(int)(npc.position.Y - Main.screenPosition.Y) - 14,
					npc.width + 28,
					npc.height + 28);
				if (box.Contains(mouse))
					return npc;
			}
			return null;
		}

		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
			layers.Insert(index < 0 ? layers.Count : index,
				new LegacyGameInterfaceLayer("BiltonsYoyoMod: Yoyo NPC Panel",
					() => {
						YoyoNPCPanel.Draw(Main.spriteBatch);
						return true;
					},
					InterfaceScaleType.UI));
		}
	}
}
