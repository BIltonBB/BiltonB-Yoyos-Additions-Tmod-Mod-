using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using BiltonsYoyoMod.Content.NPCs.Town;
using BiltonsYoyoMod.Content.NPCs.Town.Pets;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Content.UI.YoyoNPC
{
	// Per-tab content for the 5-button NPC panel. Part A: dispatch, shop, help, talk.
	public static partial class YoyoNPCPanelTabs
	{
		private static readonly Dictionary<int, Item> itemCache = new();

		private static Item CachedItem(int type)
		{
			if (!itemCache.TryGetValue(type, out Item item) || item == null) {
				item = new Item(type);
				itemCache[type] = item;
			}
			return item;
		}

		public static void DrawContent(SpriteBatch sb, NPC npc, Rectangle content)
		{
			string tab = YoyoNPCUISystem.TabId;
			switch (tab) {
				case "shop": DrawShop(sb, npc, content); break;
				case "help": DrawHelp(sb, npc, content); break;
				case "talk": DrawTalk(sb, npc, content); break;
				case "trade": DrawTrade(sb, npc, content); break;
				case "happiness": DrawHappiness(sb, npc, content); break;
				case "mods": DrawMods(sb, npc, content); break;
				case "spells": DrawSpells(sb, npc, content); break;
				case "curses": DrawCurses(sb, npc, content); break;
				case "deals": DrawDeals(sb, npc, content); break;
				case "rumors": DrawRumors(sb, npc, content); break;
				case "lore": DrawLore(sb, npc, content); break;
				case "pet": DrawPet(sb, npc, content); break;
				case "upgrade": DrawUpgrade(sb, npc, content); break;
				case "status": DrawStatus(sb, npc, content); break;
				case "essence": DrawEssence(sb, npc, content); break;
				case "about": DrawAbout(sb, npc, content); break;
				case "farewell": DrawFarewell(sb, npc, content); break;
			}
		}

		// Shared bits

		private static void DrawPager(SpriteBatch sb, Rectangle footer, int pageCount)
		{
			if (pageCount <= 1)
				return;
			Rectangle prev = new(footer.X, footer.Y, 90, 26);
			Rectangle next = new(footer.Right - 90, footer.Y, 90, 26);
			string label = $"Page {YoyoNPCUISystem.Page + 1}/{pageCount}";
			Vector2 size = FontAssets.MouseText.Value.MeasureString(label) * 0.8f;
			Terraria.Utils.DrawBorderString(sb, label, new Vector2(footer.X + (footer.Width - size.X) / 2f, footer.Y + 4), Color.LightGray, 0.8f);
			if (YoyoNPCPanel.Button(sb, prev, "< Prev", null, false, YoyoNPCUISystem.Page <= 0, 0.8f)) {
				YoyoNPCUISystem.Page--;
				YoyoNPCUISystem.Message = "";
			}
			if (YoyoNPCPanel.Button(sb, next, "Next >", null, false, YoyoNPCUISystem.Page >= pageCount - 1, 0.8f)) {
				YoyoNPCUISystem.Page++;
				YoyoNPCUISystem.Message = "";
			}
		}

		private static long WalletCopper(Player player)
		{
			long total = 0;
			foreach (Item item in player.inventory) {
				if (item == null || item.IsAir)
					continue;
				if (item.type == ItemID.CopperCoin) total += item.stack;
				else if (item.type == ItemID.SilverCoin) total += item.stack * 100L;
				else if (item.type == ItemID.GoldCoin) total += item.stack * 10000L;
				else if (item.type == ItemID.PlatinumCoin) total += item.stack * 1000000L;
			}
			return total;
		}

		// Shop

		private const int ShopPerPage = 10;

		private static void DrawShop(SpriteBatch sb, NPC npc, Rectangle content)
		{
			Player player = Main.LocalPlayer;
			var offers = YoyoNPCUISystem.CachedOffers;
			int pages = (offers.Count + ShopPerPage - 1) / ShopPerPage;
			if (YoyoNPCUISystem.Page >= pages && pages > 0)
				YoyoNPCUISystem.Page = pages - 1;

			float discount = 1f - (float)player.currentShoppingSettings.PriceAdjustment;
			string info = YoyoNPCShopData.IsTownMerchant(npc.type) && discount > 0.005f
				? $"Happy prices! Discount: {(int)(discount * 100f)}%"
				: npc.type == ModContent.NPCType<SixStringer>() || npc.type == ModContent.NPCType<YoyoCultist>()
					? "Black-market prices. No discounts, no questions."
					: "Standard prices.";
			Terraria.Utils.DrawBorderString(sb, info, new Vector2(content.X + 4, content.Y + 2), Color.LightGray, 0.8f);
			string wallet = $"Wallet: {YoyoNPCPanel.FormatCoins((int)System.Math.Min(WalletCopper(player), int.MaxValue))}";
			Vector2 wsize = FontAssets.MouseText.Value.MeasureString(wallet) * 0.8f;
			Terraria.Utils.DrawBorderString(sb, wallet, new Vector2(content.Right - wsize.X - 4, content.Y + 2), Color.Gold, 0.8f);

			int cols = 5, rows = 2;
			int gap = 8;
			int cw = (content.Width - gap * (cols - 1)) / cols;
			int ch = 128;
			int top = content.Y + 26;
			for (int i = 0; i < ShopPerPage; i++) {
				int idx = YoyoNPCUISystem.Page * ShopPerPage + i;
				if (idx >= offers.Count)
					break;
				int cx = content.X + (i % cols) * (cw + gap);
				int cy = top + (i / cols) * (ch + gap);
				DrawShopCell(sb, npc, player, offers[idx], new Rectangle(cx, cy, cw, ch));
			}
			DrawPager(sb, new Rectangle(content.X, top + rows * (ch + gap) + 2, content.Width, 26), pages);
		}

		private static void DrawShopCell(SpriteBatch sb, NPC npc, Player player, YoyoShopOffer offer, Rectangle cell)
		{
			string locked = offer.LockReason?.Invoke(player);
			bool dimmed = !string.IsNullOrEmpty(locked);
			// Card background.
			sb.Draw(TextureAssets.MagicPixel.Value, cell, dimmed ? new Color(22, 24, 36, 220) : new Color(34, 42, 68, 220));
			Rectangle slot = new(cell.X + (cell.Width - 44) / 2, cell.Y + 4, 44, 44);
			float bgScale = 44f / TextureAssets.InventoryBack.Value.Width;
			sb.Draw(TextureAssets.InventoryBack.Value, new Vector2(slot.X, slot.Y), null, Color.White * 0.85f, 0f, Vector2.Zero, bgScale, SpriteEffects.None, 0f);
			Item item = CachedItem(offer.ItemType);
			YoyoNPCPanel.DrawItemIcon(sb, offer.ItemType, new Vector2(slot.X + 22, slot.Y + 22), 36f, dimmed ? Color.Gray : Color.White);
			// Hover tooltip / lock reason.
			if (cell.Contains(Main.MouseScreen.ToPoint())) {
				Main.LocalPlayer.mouseInterface = true;
				if (!dimmed)
					ItemSlot.MouseHover(ref item, ItemSlot.Context.ShopItem);
				else
					Main.hoverItemName = $"{item.Name}\n{locked}";
				if (!dimmed && Main.mouseLeft && Main.mouseLeftRelease) {
					Main.mouseLeftRelease = false;
					if (YoyoNPCShopData.TryBuy(player, npc.type, offer, out string result)) {
						YoyoNPCUISystem.RefreshOffers();
						YoyoNPCUISystem.SetMessage("");
					}
					else if (!string.IsNullOrEmpty(result)) {
						YoyoNPCUISystem.SetMessage(result);
					}
				}
				else if (dimmed && Main.mouseLeft && Main.mouseLeftRelease) {
					Main.mouseLeftRelease = false;
					YoyoNPCUISystem.SetMessage(locked);
				}
			}
			// Name (up to 2 short lines).
			string name = item.Name;
			if (name.Length > 18)
				name = name.Substring(0, 17) + ".";
			Terraria.Utils.DrawBorderString(sb, name, new Vector2(cell.X + 4, slot.Bottom + 2), dimmed ? Color.Gray : Color.White, 0.72f);
			if (offer.CyclePool != null && offer.CyclePool.Count > 1) {
				string preview = YoyoNPCShopData.CyclePreview(offer.CyclePool, offer.ItemType, 3);
				if (preview.Length > 34)
					preview = preview.Substring(0, 33) + ".";
				Terraria.Utils.DrawBorderString(sb, "Also: " + preview, new Vector2(cell.X + 4, slot.Bottom + 16), Color.LightBlue, 0.6f);
				Terraria.Utils.DrawBorderString(sb, "Cycles daily with progression", new Vector2(cell.X + 4, slot.Bottom + 28), Color.Gray, 0.6f);
			}
			else if (!string.IsNullOrEmpty(offer.Note)) {
				Terraria.Utils.DrawBorderString(sb, offer.Note, new Vector2(cell.X + 4, slot.Bottom + 16), Color.LightBlue, 0.65f);
			}
			int price = YoyoNPCShopData.AdjustedPrice(player, npc.type, offer.PriceCopper);
			YoyoNPCPanel.DrawCoins(sb, new Vector2(cell.X + 4, cell.Y + cell.Height - 22), price, 0.7f);
		}

		// Help

		private static void DrawHelp(SpriteBatch sb, NPC npc, Rectangle content)
		{
			if (npc.type == ModContent.NPCType<YoyoMaster>()) {
				DrawMasterHelp(sb, npc, content);
				return;
			}
			List<string> lines = YoyoNPCDialogue.HelpLines(npc.type);
			const int perPage = 8;
			int pages = (lines.Count + perPage - 1) / perPage;
			if (YoyoNPCUISystem.Page >= pages && pages > 0)
				YoyoNPCUISystem.Page = pages - 1;
			float y = content.Y + 6;
			for (int i = 0; i < perPage; i++) {
				int idx = YoyoNPCUISystem.Page * perPage + i;
				if (idx >= lines.Count)
					break;
				y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y), $"- {lines[idx]}", Color.White, 0.85f, content.Width - 16) + 4;
			}
			DrawPager(sb, new Rectangle(content.X, content.Bottom - 28, content.Width, 26), pages);
		}

		private static void DrawMasterHelp(SpriteBatch sb, NPC npc, Rectangle content)
		{
			string sub = YoyoNPCUISystem.Sub;
			if (string.IsNullOrEmpty(sub))
				sub = "specific";
			Rectangle bGen = new(content.X, content.Y + 2, 150, 28);
			Rectangle bSpec = new(content.X + 158, content.Y + 2, 150, 28);
			if (YoyoNPCPanel.Button(sb, bGen, "General Help", null, sub == "general", false, 0.8f)) {
				YoyoNPCUISystem.Sub = "general";
				YoyoNPCUISystem.Page = 0;
			}
			if (YoyoNPCPanel.Button(sb, bSpec, "Specific Help", "Guidance for your current progression stage", sub == "specific", false, 0.8f)) {
				YoyoNPCUISystem.Sub = "specific";
				YoyoNPCUISystem.Page = 0;
				YoyoNPCUISystem.HelpStage = -1;
			}
			Rectangle body = new(content.X, content.Y + 36, content.Width, content.Height - 36 - 30);
			if (sub == "general")
				DrawGeneralHelp(sb, body);
			else
				DrawSpecificHelp(sb, body);
			DrawPager(sb, new Rectangle(content.X, content.Bottom - 28, content.Width, 26), pagerPages);
		}

		private static int pagerPages = 1;

		private static void DrawGeneralHelp(SpriteBatch sb, Rectangle body)
		{
			string[] list = Main.hardMode ? YoyoMaster.GeneralHardmode : YoyoMaster.GeneralPreHardmode;
			const int perPage = 7;
			pagerPages = (list.Length + perPage - 1) / perPage;
			if (YoyoNPCUISystem.Page >= pagerPages)
				YoyoNPCUISystem.Page = pagerPages - 1;
			float y = body.Y + 4;
			for (int i = 0; i < perPage; i++) {
				int idx = YoyoNPCUISystem.Page * perPage + i;
				if (idx >= list.Length)
					break;
				y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(body.X + 8, y), $"- {list[idx]}", Color.White, 0.85f, body.Width - 16) + 4;
			}
		}

		private static void DrawSpecificHelp(SpriteBatch sb, Rectangle body)
		{
			Player player = Main.LocalPlayer;
			var guide = player.GetModPlayer<YoyoGuidePlayer>();
			int current = YoyoMaster.GetStage();
			// Browse any unlocked stage (0..current); -1 means "current".
			int viewed = YoyoNPCUISystem.HelpStage < 0 ? current : YoyoNPCUISystem.HelpStage;
			viewed = System.Math.Clamp(viewed, 0, current);
			YoyoNPCUISystem.HelpStage = viewed;
			string[] lines = YoyoMaster.SpecificForStage(viewed);
			bool isNew = !guide.StageSeen(viewed);
			if (isNew) {
				guide.MarkStageSeen(viewed);
				YoyoNPCUISystem.SetMessage("New guidance! (?)", 240);
			}
			pagerPages = 1;
			Rectangle prev = new(body.X + 8, body.Y + 2, 36, 24);
			Rectangle next = new(body.X + 48, body.Y + 2, 36, 24);
			if (YoyoNPCPanel.Button(sb, prev, "<", "Older guidance", false, viewed <= 0, 0.8f))
				YoyoNPCUISystem.HelpStage = viewed - 1;
			if (YoyoNPCPanel.Button(sb, next, ">", "Newer guidance", false, viewed >= current, 0.8f))
				YoyoNPCUISystem.HelpStage = viewed + 1;
			string title = $"Stage {viewed}/11 - {StageTitle(viewed)}";
			Color titleColor = isNew ? Color.Yellow : Color.Gold;
			Terraria.Utils.DrawBorderString(sb, (isNew ? "? " : "") + title, new Vector2(body.X + 96, body.Y + 4), titleColor, 0.9f);
			string seenInfo = $"Guidance viewed: {CountSeenStages(guide)}/12 stages";
			Terraria.Utils.DrawBorderString(sb, seenInfo, new Vector2(body.X + 8, body.Y + 26), Color.Gray, 0.7f);
			// Paginate long stages so lines never overflow the body.
			const int linesPerPage = 6;
			pagerPages = (lines.Length + linesPerPage - 1) / linesPerPage;
			if (YoyoNPCUISystem.Page >= pagerPages)
				YoyoNPCUISystem.Page = pagerPages - 1;
			float y = body.Y + 48;
			for (int i = 0; i < linesPerPage; i++) {
				int idx = YoyoNPCUISystem.Page * linesPerPage + i;
				if (idx >= lines.Length)
					break;
				y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(body.X + 8, y), $"- {lines[idx]}", Color.White, 0.9f, body.Width - 16) + 6;
			}
		}

		private static int CountSeenStages(YoyoGuidePlayer guide)
		{
			int count = 0;
			for (int i = 0; i <= 11; i++) {
				if (guide.StageSeen(i))
					count++;
			}
			return count;
		}

		private static string StageTitle(int stage)
		{
			return stage switch {
				0 => "The Cowboys",
				1 => "King Slime",
				2 => "Eye of Cthulhu",
				3 => "Evil Boss",
				4 => "Queen Bee",
				5 => "Skeletron",
				6 => "Hardmode",
				7 => "Mechanical Bosses",
				8 => "Plantera",
				9 => "Golem",
				10 => "Lunatic Cultist",
				_ => "Moon Lord",
			};
		}

		// Talk

		private static void DrawTalk(SpriteBatch sb, NPC npc, Rectangle content)
		{
			if (string.IsNullOrEmpty(YoyoNPCUISystem.TalkLine))
				YoyoNPCUISystem.TalkLine = YoyoNPCDialogue.RandomTalk(npc.type);
			Rectangle portrait = new(content.X + 12, content.Y + 24, 64, 64);
			sb.Draw(TextureAssets.InventoryBack.Value, new Vector2(portrait.X, portrait.Y), null, Color.White * 0.85f, 0f, Vector2.Zero, 64f / TextureAssets.InventoryBack.Value.Width, SpriteEffects.None, 0f);
			YoyoNPCPanel.DrawHeadIcon(sb, npc, new Vector2(portrait.X + 32, portrait.Y + 32), 56f);
			YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 88, content.Y + 30), $"\"{YoyoNPCUISystem.TalkLine}\"", Color.White, 1f, content.Width - 100);
			Rectangle btn = new(content.X + 12, content.Bottom - 70, 170, 32);
			if (YoyoNPCPanel.Button(sb, btn, "Talk again", "Hear another line", false, false, 0.85f))
				YoyoNPCUISystem.TalkLine = YoyoNPCDialogue.RandomTalk(npc.type);
			Terraria.Utils.DrawBorderString(sb, "Stand near the other specialists to hear crossover gossip.", new Vector2(content.X + 12, content.Bottom - 30), Color.Gray, 0.7f);
		}
	}
}
