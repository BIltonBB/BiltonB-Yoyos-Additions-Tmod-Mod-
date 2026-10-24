using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using BiltonsYoyoMod.Common;
using BiltonsYoyoMod.Content.NPCs.Town;
using BiltonsYoyoMod.Content.NPCs.Town.Pets;
using BiltonsYoyoMod.Content.Systems;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Content.UI.YoyoNPC
{
	// Per-tab content, part B: trade, mood, mods, spells, travelers, pets.
	public static partial class YoyoNPCPanelTabs
	{
		// Trade (Yoyo Master)

		private static void DrawTrade(SpriteBatch sb, NPC npc, Rectangle content)
		{
			Player player = Main.LocalPlayer;
			float y = content.Y + 6;
			Terraria.Utils.DrawBorderString(sb, "Goods for goods. One swap per click.", new Vector2(content.X + 8, y), Color.LightGray, 0.8f);
			var pairs = YoyoNPCTrade.GetPairs();
			// Scrollable rows: 44px each, convert bar reserves the bottom.
			// Buttons (not the wheel) drive the offset so the hotbar stays put.
			const int tradeRowH = 44;
			int visibleRows = System.Math.Max(1, ((int)(content.Bottom - 70 - (y + 34))) / tradeRowH);
			int maxTradeOffset = System.Math.Max(0, pairs.Count - visibleRows);
			YoyoNPCUISystem.TradeScroll = System.Math.Clamp(YoyoNPCUISystem.TradeScroll, 0, maxTradeOffset);
			int tradeOffset = YoyoNPCUISystem.TradeScroll;
			Rectangle allBtn = new(content.Right - 150, content.Y + 2, 140, 28);
			if (YoyoNPCPanel.Button(sb, allBtn, "Trade All", "Swap everything convertible once", false, false, 0.8f))
				YoyoNPCActions.TradeAll(player);
			if (maxTradeOffset > 0) {
				Rectangle tUp = new(content.Right - 236, content.Y + 2, 40, 28);
				Rectangle tDown = new(content.Right - 192, content.Y + 2, 40, 28);
				if (YoyoNPCPanel.Button(sb, tUp, "^", "Scroll up", false, false, 0.8f))
					YoyoNPCUISystem.TradeScroll = System.Math.Max(0, tradeOffset - 1);
				if (YoyoNPCPanel.Button(sb, tDown, "v", "Scroll down", false, false, 0.8f))
					YoyoNPCUISystem.TradeScroll = System.Math.Min(maxTradeOffset, tradeOffset + 1);
			}
			y += 34;
			for (int pi = 0; pi < pairs.Count; pi++) {
				int row = pi - tradeOffset;
				if (row < 0 || row >= visibleRows)
					continue;
				var pair = pairs[pi];
				Rectangle rowBox = new(content.X + 4, (int)y + row * tradeRowH, content.Width - 8, 40);
				sb.Draw(TextureAssets.MagicPixel.Value, rowBox, new Color(26, 32, 54, 220));
				int haveA = YoyoTownShared.CountInInventory(player, pair.A);
				int haveB = YoyoTownShared.CountInInventory(player, pair.B);
				YoyoNPCPanel.DrawItemIcon(sb, pair.A, new Vector2(rowBox.X + 26, rowBox.Y + 20), 30f, Color.White);
				YoyoNPCPanel.DrawItemIcon(sb, pair.B, new Vector2(rowBox.X + 86, rowBox.Y + 20), 30f, Color.White);
				Terraria.Utils.DrawBorderString(sb, $"x{haveA}", new Vector2(rowBox.X + 44, rowBox.Y + 20), Color.LightGray, 0.75f);
				Terraria.Utils.DrawBorderString(sb, $"x{haveB}", new Vector2(rowBox.X + 104, rowBox.Y + 20), Color.LightGray, 0.75f);
				Terraria.Utils.DrawBorderString(sb, pair.Label, new Vector2(rowBox.X + 140, rowBox.Y + 11), Color.White, 0.8f);
				Rectangle toB = new(rowBox.Right - 170, rowBox.Y + 7, 78, 26);
				Rectangle toA = new(rowBox.Right - 84, rowBox.Y + 7, 78, 26);
				if (YoyoNPCPanel.Button(sb, toB, "A > B", null, false, haveA <= 0, 0.75f))
					YoyoNPCActions.SwapTrade(player, pair.A, pair.B);
				if (YoyoNPCPanel.Button(sb, toA, "B > A", null, false, haveB <= 0, 0.75f))
					YoyoNPCActions.SwapTrade(player, pair.B, pair.A);
			}
			// Held accessory -> potions.
			Item held = player.HeldItem;
			string heldName = held != null && !held.IsAir ? held.Name : "(empty hand)";
			YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, content.Bottom - 58), $"Held: {heldName} - spare modded yoyo accessories become healing potions.", Color.LightGray, 0.75f, content.Width - 200);
			Rectangle conv = new(content.Right - 170, content.Bottom - 60, 160, 30);
			if (YoyoNPCPanel.Button(sb, conv, "Convert Held", "Trade your held accessory for healing potions", false, false, 0.8f))
				YoyoNPCActions.ConvertHeld(player);
		}

		// Mood (happiness)

	 private static void DrawHappiness(SpriteBatch sb, NPC npc, Rectangle content)
		{
			float y = content.Y + 6;
			Player player = Main.LocalPlayer;
			YoyoNPCMood.MoodInfo mood = npc.type == ModContent.NPCType<YoyoCultist>()
				? YoyoNPCMood.ComputeVisitor(player, npc, "Yoyo Cultist")
				: YoyoNPCMood.Compute(player, npc);
			// Current Mood readout.
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y), "Current Mood:", Color.Gray, 0.75f, content.Width - 16);
			Terraria.Utils.DrawBorderString(sb, mood.Label, new Vector2(content.X + 8, y), mood.Color, 1.1f);
			y += 30f;
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y), mood.Summary, Color.White, 0.85f, content.Width - 16) + 4;
			foreach (string factor in mood.Factors)
				y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y), "- " + factor, Color.LightGray, 0.8f, content.Width - 16) + 2;
			y += 8;
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y), "What stirs these feelings:", Color.Gray, 0.75f, content.Width - 16) + 2;
			var moodRows = new System.Collections.Generic.List<MoodRow>();
			if (npc.type == ModContent.NPCType<YoyoCultist>()) {
				y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y),
					"The cultist only visits, so mood never changes his prices. Still, he has opinions:", Color.LightGray, 0.85f, content.Width - 16) + 6;
				Color love = new(255, 150, 200);
				moodRows.Add(new MoodRow { Affection = "Loves", Subject = "the Dungeon", Quote = "The old stones are full of forgotten knowledge.", Color = love });
				moodRows.Add(new MoodRow { Affection = "Loves", Subject = "the Wizard", Quote = "He studies forces ordinary people cannot comprehend.", Color = love });
				moodRows.Add(new MoodRow { Affection = "Likes", Subject = "Underground, Forest, Goblin Tinkerer", Quote = "Secrets, quiet, and someone who understands mechanisms.", Color = Color.LightGreen });
				moodRows.Add(new MoodRow { Affection = "Dislikes", Subject = "Ocean, Snow, Tax Collector", Quote = "Too noisy, too cold, too many questions.", Color = Color.Orange });
				moodRows.Add(new MoodRow { Affection = "Hates", Subject = "Demolitionist, Jungle, Underworld", Quote = "Explosions destroy evidence. The rest should stay asleep.", Color = Color.Tomato });
			}
			else {
				moodRows.AddRange(HappinessLines(npc.type));
			}
			// Scrollable rows: 40px slots so long mood lists never spill
			// out of the box. Buttons drive it (never the wheel).
			const int moodRowH = 40;
			int listTop = (int)y;
			int viewH = System.Math.Max(moodRowH, content.Bottom - 8 - listTop);
			int maxMoodScroll = System.Math.Max(0, moodRows.Count * moodRowH - viewH);
			YoyoNPCUISystem.MoodScroll = System.Math.Clamp(YoyoNPCUISystem.MoodScroll, 0, (maxMoodScroll + 19) / 20);
			int moodOff = YoyoNPCUISystem.MoodScroll * 20;
			if (maxMoodScroll > 0) {
				Rectangle mUp = new(content.Right - 96, listTop - 26, 40, 24);
				Rectangle mDown = new(content.Right - 52, listTop - 26, 40, 24);
				if (YoyoNPCPanel.Button(sb, mUp, "^", "Scroll up", false, false, 0.8f))
					YoyoNPCUISystem.MoodScroll = System.Math.Max(0, YoyoNPCUISystem.MoodScroll - 1);
				if (YoyoNPCPanel.Button(sb, mDown, "v", "Scroll down", false, false, 0.8f))
					YoyoNPCUISystem.MoodScroll = System.Math.Min((maxMoodScroll + 19) / 20, YoyoNPCUISystem.MoodScroll + 1);
			}
			for (int mi = 0; mi < moodRows.Count; mi++) {
				int ry = listTop + mi * moodRowH - moodOff;
				if (ry < listTop || ry > content.Bottom - moodRowH)
					continue;
				MoodRow row = moodRows[mi];
				MoodLine(sb, content, ry, row.Affection, row.Subject, row.Quote, row.Color);
			}
		}

		private class MoodRow
		{
			public string Affection;
			public string Subject;
			public string Quote;
			public Color Color;
		}

		private static List<MoodRow> HappinessLines(int npcType)
		{
			Color love = new(255, 150, 200);
			Color like = Color.LightGreen;
			Color dislike = Color.Orange;
			Color hate = Color.Tomato;
			if (npcType == ModContent.NPCType<YoyoMaster>()) return new List<MoodRow> {
				new() { Affection = "Loves", Subject = "Forest", Quote = "Quiet, balanced, and perfect for training.", Color = love },
				new() { Affection = "Loves", Subject = "Tavernkeep", Quote = "Great deals, and we trade training ideas!", Color = love },
				new() { Affection = "Loves", Subject = "Yoyo Tinkerer", Quote = "Nobody understands yoyo equipment better.", Color = love },
				new() { Affection = "Likes", Subject = "Snow, Jungle", Quote = "Cold practice air; a perfect combat test.", Color = like },
				new() { Affection = "Likes", Subject = "Goblin Tinkerer, Dye Trader", Quote = "A fellow modifier; a relaxing chat.", Color = like },
				new() { Affection = "Dislikes", Subject = "Ocean, Angler", Quote = "Too many fish, in the water and in conversation.", Color = dislike },
				new() { Affection = "Hates", Subject = "Underworld", Quote = "Too chaotic for proper training.", Color = hate },
				new() { Affection = "Hates", Subject = "Tax Collector, Stylist, Clothier", Quote = "Interruptions and fashion. Wonderful.", Color = hate },
			};
			if (npcType == ModContent.NPCType<YoyoTinkerer>()) return new List<MoodRow> {
				new() { Affection = "Loves", Subject = "Goblin Tinkerer, Mechanic", Quote = "Fellow inventors who actually understand my work.", Color = love },
				new() { Affection = "Loves", Subject = "Snow", Quote = "Cold keeps my machines cool.", Color = love },
				new() { Affection = "Likes", Subject = "Stylist", Quote = "Too much fashion, but occasionally useful ideas.", Color = like },
				new() { Affection = "Dislikes", Subject = "Desert, Arms Dealer", Quote = "Too hot for precision; he tests things randomly!", Color = dislike },
				new() { Affection = "Hates", Subject = "Underworld, Jungle, Painter", Quote = "Overheats, overcrowds, and gets paint on my gears.", Color = hate },
			};
			return new List<MoodRow> {
				new() { Affection = "Loves", Subject = "Wizard, Forest", Quote = "Someone who understands magic; nature strengthens it.", Color = love },
				new() { Affection = "Likes", Subject = "Jungle, Witch Doctor, Clothier, Underground", Quote = "Magical energy, strange magic, cursed wisdom, test halls.", Color = like },
				new() { Affection = "Dislikes", Subject = "Tax Collector, Snow, Underworld", Quote = "Interruptions, strange cold magic, far too hot.", Color = dislike },
				new() { Affection = "Hates", Subject = "Demolitionist, Ocean", Quote = "Explosions ruin spellcasting; the sea makes a mess.", Color = hate },
			};
		}

		private static float MoodLine(SpriteBatch sb, Rectangle content, float y, string affection, string subject, string quote, Color color)
		{
			float used = YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y), $"{affection} {subject}: \"{quote}\"", color, 0.8f, content.Width - 16);
			return used;
		}

		// Mods (Tinkerer, scrollable)

		private const int ModRowH = 30;

		// Shared held-yoyo card with icon. Returns height used.
		private static float DrawHeldCard(SpriteBatch sb, Rectangle content, float y, bool showStrip = true)
		{
			Player player = Main.LocalPlayer;
			Item held = YoyoNPCActions.HeldYoyo(player);
			Rectangle slot = new(content.X + 8, (int)y + 2, 44, 44);
			float bgScale = 44f / TextureAssets.InventoryBack.Value.Width;
			sb.Draw(TextureAssets.InventoryBack.Value, new Vector2(slot.X, slot.Y), null, Color.White * 0.9f, 0f, Vector2.Zero, bgScale, SpriteEffects.None, 0f);
			if (held != null) {
				YoyoNPCPanel.DrawItemIcon(sb, held.type, new Vector2(slot.X + 22, slot.Y + 22), 36f, Color.White);
				var g = held.GetGlobalItem<YoyoEnhancementGlobalItem>();
				held.TryGetGlobalItem<Content.Customization.YoyoCustomizationGlobalItem>(out var cust);
				Terraria.Utils.DrawBorderString(sb, held.Name, new Vector2(slot.X + 50, y + 2), Color.White, 0.8f);
				Terraria.Utils.DrawBorderString(sb, $"Mod: {g.GetModificationName()}   Spell: {g.GetSpellName()}", new Vector2(slot.X + 50, y + 22), Color.LightGray, 0.7f);
				string custom = $"Tuning: {(cust != null ? cust.TuningName() : "N/A")}  Calib: {(cust != null ? cust.CalibrationName() : "N/A")}  Mastery: {(cust != null ? cust.MasteryName() : "N/A")}  Curse: {(cust != null ? cust.CurseName() : "N/A")}";
				if (custom.Length > 90) custom = custom.Substring(0, 89) + ".";
				Terraria.Utils.DrawBorderString(sb, custom, new Vector2(slot.X + 50, y + 36), Color.LightGray, 0.65f);
				bool hasEnh = g.ModificationId >= 0 || g.SpellId >= 0;
				Rectangle strip = new(content.Right - 150, (int)y + 10, 140, 28);
				if (showStrip && hasEnh && YoyoNPCPanel.Button(sb, strip, "Strip mod", "Remove the modification from your held yoyo (1 gold)", false, g.ModificationId < 0, 0.8f))
					YoyoNPCActions.StripEnhancements(player, stripMod: true, stripSpell: false);
			}
			else {
				Terraria.Utils.DrawBorderString(sb, "Hold a yoyo in your hand.", new Vector2(slot.X + 50, y + 12), Color.Yellow, 0.85f);
			}
			return 62f;
		}

		// Scroll footer with ^/v buttons. Returns the clamped offset.
		private static int DrawScrollFooter(SpriteBatch sb, Rectangle content, float footerY, int total, int visible, string label)
		{
			int maxScroll = System.Math.Max(0, total - visible);
			int offset = System.Math.Min(YoyoNPCUISystem.ListScroll, maxScroll);
			if (offset < 0)
				offset = 0;
			YoyoNPCUISystem.ListScroll = offset;
			Rectangle up = new(content.Right - 190, (int)footerY, 40, 24);
			Rectangle down = new(content.Right - 144, (int)footerY, 40, 24);
			if (YoyoNPCPanel.Button(sb, up, "^", "Scroll up (mouse wheel works too)", false, false, 0.8f))
				YoyoNPCUISystem.ListScroll = System.Math.Max(0, offset - 1);
			if (YoyoNPCPanel.Button(sb, down, "v", "Scroll down (mouse wheel works too)", false, false, 0.8f))
				YoyoNPCUISystem.ListScroll = System.Math.Min(maxScroll, offset + 1);
			string text = total == 0 ? $"{label}: none" : $"{label} {offset + 1}-{System.Math.Min(offset + visible, total)} of {total} (wheel scrolls)";
			Terraria.Utils.DrawBorderString(sb, text, new Vector2(content.X + 8, footerY + 3), Color.Gray, 0.75f);
			return offset;
		}

		private static void DrawMods(SpriteBatch sb, NPC npc, Rectangle content)
		{
			Player player = Main.LocalPlayer;
			float y = content.Y + 4;
			y += DrawHeldCard(sb, content, y);
			var all = YoyoModificationDefs.All;
			int listTop = (int)y + 4;
			int footerH = 28;
			int listH = content.Bottom - listTop - footerH;
			int visible = System.Math.Max(1, listH / ModRowH);
			int maxScroll = System.Math.Max(0, all.Count - visible);
			int offset = System.Math.Min(YoyoNPCUISystem.ListScroll, maxScroll);
			if (offset < 0)
				offset = 0;
			YoyoNPCUISystem.ListScroll = offset;
			for (int r = 0; r < visible; r++) {
				int idx = offset + r;
				if (idx >= all.Count)
					break;
				DrawModRow(sb, player, all[idx], new Rectangle(content.X + 4, listTop + r * ModRowH, content.Width - 8, ModRowH - 2));
			}
			DrawScrollFooter(sb, content, content.Bottom - footerH + 2, all.Count, visible, "Mods");
		}

		private static void DrawModRow(SpriteBatch sb, Player player, YoyoModificationDef def, Rectangle rowBox)
		{
			bool locked = def.Hardmode && !Main.hardMode;
			bool hasYoyo = YoyoNPCActions.HeldYoyo(player) != null;
			sb.Draw(TextureAssets.MagicPixel.Value, rowBox, locked ? new Color(22, 24, 36, 220) : new Color(34, 42, 68, 220));
			Terraria.Utils.DrawBorderString(sb, def.Name, new Vector2(rowBox.X + 8, rowBox.Y + 5), locked ? Color.Gray : Color.White, 0.8f);
			if (rowBox.Contains(Main.MouseScreen.ToPoint())) {
				Main.LocalPlayer.mouseInterface = true;
				Main.hoverItemName = $"{def.Name}\n{def.Description}";
			}
			var tinker = player.GetModPlayer<Players.YoyoTinkerPlayer>();
			int price = tinker.GetModificationPrice(def.Id, def.BaseCostCopper);
			YoyoNPCPanel.DrawCoins(sb, new Vector2(rowBox.X + 220, rowBox.Y + 5), price, 0.7f);
			Rectangle reroll = new(rowBox.Right - 168, rowBox.Y + 3, 76, 22);
			Rectangle install = new(rowBox.Right - 84, rowBox.Y + 3, 78, 22);
			if (locked) {
				Terraria.Utils.DrawBorderString(sb, "Hardmode", new Vector2(rowBox.Right - 160, rowBox.Y + 5), Color.Gray, 0.75f);
			}
			else {
				if (YoyoNPCPanel.Button(sb, reroll, "Reroll", $"Reroll this installation price (fee: {YoyoNPCPanel.FormatCoins(YoyoNPCActions.RerollFee(def))})", false, false, 0.7f))
					YoyoNPCActions.RerollMod(player, def);
				if (YoyoNPCPanel.Button(sb, install, "Install", "Install onto your held yoyo", false, !hasYoyo, 0.7f))
					YoyoNPCActions.InstallMod(player, def);
			}
		}

		// Spells (Ancient) + scrollable grimoire

		private const int SpellRowH = 24;

		private static void DrawSpells(SpriteBatch sb, NPC npc, Rectangle content)
		{
			Player player = Main.LocalPlayer;
			float y = content.Y + 6;
			// Tier radio row.
			int tw = (content.Width - 16 - 4 * 6) / 5;
			for (int t = 0; t < 5; t++) {
				Rectangle br = new(content.X + 8 + t * (tw + 6), (int)y, tw, 28);
				if (YoyoNPCPanel.Button(sb, br, YoyoSpellDefs.TierNames[t], YoyoNPCPanel.FormatCoins(YoyoSpellDefs.TierPrices[t]), YoyoNPCUISystem.SelectedTier == t, false, 0.8f)) {
					YoyoNPCUISystem.SelectedTier = t;
					YoyoNPCUISystem.Message = "";
				}
			}
			y += 32;
			int tier = YoyoNPCUISystem.SelectedTier;
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y),
				$"{YoyoSpellDefs.TierNames[tier]} ritual: {YoyoNPCPanel.FormatCoins(YoyoSpellDefs.TierPrices[tier])}. Richer rituals find rarer spells.", Color.LightGray, 0.8f, content.Width - 200) + 2;
			Rectangle cast = new(content.Right - 180, (int)y - 2, 172, 30);
			bool hasYoyo = YoyoNPCActions.HeldYoyo(player) != null;
			if (YoyoNPCPanel.Button(sb, cast, $"Cast {YoyoSpellDefs.TierNames[tier]}", "Roll a spell onto your held yoyo", false, !hasYoyo, 0.85f))
				YoyoNPCActions.CastSpell(player, tier);
			y += DrawHeldCard(sb, content, y + 2, showStrip: false);
			// Spell grimoire: every known spell, scrollable.
			Terraria.Utils.DrawBorderString(sb, "Grimoire (scroll to browse):", new Vector2(content.X + 8, y + 2), Color.Gray, 0.75f);
			Rectangle spellStrip = new(content.Right - 150, (int)y, 140, 22);
			if (YoyoNPCPanel.Button(sb, spellStrip, "Strip spell", "Remove the spell from your held yoyo (1 gold)", false, YoyoNPCActions.HeldYoyo(player) == null, 0.75f))
				YoyoNPCActions.StripEnhancements(player, stripMod: false, stripSpell: true);
			y += 22;
			var all = YoyoSpellDefs.All;
			int listH = content.Bottom - (int)y - 30;
			int visible = System.Math.Max(1, listH / SpellRowH);
			int maxScroll = System.Math.Max(0, all.Count - visible);
			int offset = System.Math.Min(YoyoNPCUISystem.ListScroll, maxScroll);
			if (offset < 0)
				offset = 0;
			YoyoNPCUISystem.ListScroll = offset;
			for (int r = 0; r < visible; r++) {
				int idx = offset + r;
				if (idx >= all.Count)
					break;
				var spell = all[idx];
				bool locked = spell.Hardmode && !Main.hardMode;
				Rectangle rowBox = new(content.X + 4, (int)y + r * SpellRowH, content.Width - 8, SpellRowH - 2);
				sb.Draw(TextureAssets.MagicPixel.Value, rowBox, locked ? new Color(22, 24, 36, 220) : new Color(34, 42, 68, 220));
				Color nameColor = locked ? Color.Gray : spell.Id == 19 ? Color.Red : spell.Combined ? Color.Gold : Color.White;
				string line = $"{spell.Name} - {string.Join(", ", SpellDebuffNames(spell))}";
				if (line.Length > 62)
					line = line.Substring(0, 61) + ".";
				Terraria.Utils.DrawBorderString(sb, line, new Vector2(rowBox.X + 8, rowBox.Y + 3), nameColor, 0.75f);
				if (!locked && rowBox.Contains(Main.MouseScreen.ToPoint())) {
					Main.LocalPlayer.mouseInterface = true;
					Main.hoverItemName = $"{spell.Name}\n{spell.Description}";
				}
				if (locked)
					Terraria.Utils.DrawBorderString(sb, "HM", new Vector2(rowBox.Right - 34, rowBox.Y + 3), Color.Gray, 0.75f);
			}
			DrawScrollFooter(sb, content, content.Bottom - 26, all.Count, visible, "Spells");
		}

		private static List<string> SpellDebuffNames(YoyoSpellDef spell)
		{
			var names = new List<string>();
			foreach (int buff in spell.Debuffs) {
				try {
					names.Add(Lang.GetBuffName(buff));
				}
				catch {
					names.Add("Debuff " + buff);
				}
			}
			if (names.Count == 0)
				names.Add("Special");
			return names;
		}

		// Six Stringer extras

		private static void DrawDeals(SpriteBatch sb, NPC npc, Rectangle content)
		{
			float y = content.Y + 10;
			bool rare = YoyoNPCShopData.SessionRareStock;
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y),
				rare ? "RARE STOCK TONIGHT: the gold-trimmed goods are on the table. Buy them before dawn." : "Common stock only tonight. The rare goods stayed hidden. Come back another night.",
				rare ? Color.Gold : Color.LightGray, 0.9f, content.Width - 24) + 8;
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y),
				"How the black market works:\n- I appear on nights after the Cowboys of Yoyos and the Eye of Cthulhu fall.\n- Only one visitor per night: me, the cultist, or the vanilla merchant.\n- A Traveller's Caller can summon me on any night.\n- Rare stock shows up roughly one night in three. Hardmode unlocks the back shelves.",
				Color.White, 0.85f, content.Width - 24);
		}

		private static void DrawRumors(SpriteBatch sb, NPC npc, Rectangle content)
		{
			float y = content.Y + 10;
			// Cached line: re-rolling every frame made the text flicker like gibberish.
			if (string.IsNullOrEmpty(YoyoNPCUISystem.RumorLine))
				YoyoNPCUISystem.RumorLine = YoyoNPCDialogue.RandomTalk(npc.type);
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y), "\"" + YoyoNPCUISystem.RumorLine + "\"", Color.White, 0.9f, content.Width - 24) + 8;
			Rectangle again = new(content.X + 12, (int)y, 170, 28);
			if (YoyoNPCPanel.Button(sb, again, "Another rumor", "Hear a different rumor", false, false, 0.8f))
				YoyoNPCUISystem.RumorLine = YoyoNPCDialogue.RandomTalk(npc.type);
			y += 36;
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y),
				"Word on the street:\n- The Master wants normal gear; I want the weird stuff.\n- Need mods? Find the Tinkerer. Need magic? Find the old hermit.\n- Don't flash expensive gear around. Some things aren't sold during the day.",
				Color.LightGray, 0.85f, content.Width - 24);
		}

		// Cultist lore

		private static void DrawLore(SpriteBatch sb, NPC npc, Rectangle content)
		{
			float y = content.Y + 10;
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y),
				"\"Perhaps the yoyo is not returning. Perhaps it is being summoned.\"\n\nThe cultist believes yoyos predate every civilization: a spinning weapon from the old stones. He followed it here, and he only visits on rare nights after Skeletron falls (more often in Hardmode). Try shimmering a yoyo sometime. He is curious what happens.",
				Color.White, 0.9f, content.Width - 24) + 8;
			if (YoyoNPCDialogue.AnyActive<AncientYoyoist>())
				YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y), "\"The Ancient Yo-yoist knows more than he reveals. Perhaps he remembers the first yoyo.\"", new Color(200, 170, 255), 0.85f, content.Width - 24);
		}

		// Billy

		private static void DrawPet(SpriteBatch sb, NPC npc, Rectangle content)
		{
			if (npc.type == ModContent.NPCType<MagicalYoyoPet>()) {
				float y = content.Y + 30;
				y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y), "\"" + YoyoNPCUISystem.TalkLine + "\"", Color.White, 1f, content.Width - 24) + 16;
				Rectangle btn = new(content.X + 12, (int)y, 200, 34);
				if (YoyoNPCPanel.Button(sb, btn, "Pet the yoyo", "Gain Magical Essence: +5% yoyo damage, 8 hours", false, false, 0.9f))
					YoyoNPCActions.PetMagical(Main.LocalPlayer);
				return;
			}
			float y2 = content.Y + 30;
			y2 += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y2), "\"" + YoyoNPCUISystem.TalkLine + "\"", Color.White, 1f, content.Width - 24) + 16;
			Rectangle btn2 = new(content.X + 12, (int)y2, 200, 34);
			if (YoyoNPCPanel.Button(sb, btn2, "Pet Billy", "Bearing noises.", false, false, 0.9f))
				YoyoNPCActions.PetBilly();
			y2 += 48;
			YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y2), $"Upgrades: {BillyTheBearing.UpgradeCount}/{BillyTheBearing.MaxUpgrades}. During bosses Billy orbits whoever carries the most yoyos.", Color.LightGray, 0.85f, content.Width - 24);
		}

		private static void DrawUpgrade(SpriteBatch sb, NPC npc, Rectangle content)
		{
			Player player = Main.LocalPlayer;
			float y = content.Y + 6;
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y), "One of each bearing type installs permanently. Click a missing one to install it, click a fitted one to switch it off and on.", Color.LightGray, 0.8f, content.Width - 250) + 2;
			Rectangle allBtn = new(content.Right - 150, content.Y + 4, 140, 28);
			if (YoyoNPCPanel.Button(sb, allBtn, "Install All", "Install every type you carry", false, false, 0.8f))
				YoyoNPCActions.InstallAllBilly(player);
			y += 30;
			var types = BillyTheBearing.UpgradeTypeList();
			int cols = 6;
			const int rowH = 74;
			int cw = (content.Width - 16 - 5 * 8) / cols;
			// Scrollable grid: button-driven row offset, clamped to content.
			int rows = (types.Count + cols - 1) / cols;
			int visibleRows = System.Math.Max(1, ((int)(content.Bottom - y) - 8) / rowH);
			int maxOffset = System.Math.Max(0, rows - visibleRows);
			YoyoNPCUISystem.UpgradeScroll = System.Math.Clamp(YoyoNPCUISystem.UpgradeScroll, 0, maxOffset);
			int rowOffset = YoyoNPCUISystem.UpgradeScroll;
			if (maxOffset > 0) {
				Rectangle uUp = new(content.Right - 236, content.Y + 4, 40, 28);
				Rectangle uDown = new(content.Right - 192, content.Y + 4, 40, 28);
				if (YoyoNPCPanel.Button(sb, uUp, "^", "Scroll up", false, false, 0.8f))
					YoyoNPCUISystem.UpgradeScroll = System.Math.Max(0, rowOffset - 1);
				if (YoyoNPCPanel.Button(sb, uDown, "v", "Scroll down", false, false, 0.8f))
					YoyoNPCUISystem.UpgradeScroll = System.Math.Min(maxOffset, rowOffset + 1);
			}
			for (int i = 0; i < types.Count; i++) {
				int row = i / cols - rowOffset;
				if (row < 0 || row >= visibleRows)
					continue;
				int cx = content.X + 8 + (i % cols) * (cw + 8);
				int cy = (int)y + row * rowH;
				Rectangle cell = new(cx, cy, cw, 70);
				sb.Draw(TextureAssets.MagicPixel.Value, cell, new Color(30, 38, 62, 220));
				bool installed = YoyoTownSystem.BillyUpgrades.Contains(types[i]);
				bool switchedOff = installed && YoyoTownSystem.BillyInactive.Contains(types[i]);
				int have = YoyoTownShared.CountInInventory(player, types[i]);
				Item item = new(types[i]);
				YoyoNPCPanel.DrawItemIcon(sb, types[i], new Vector2(cell.X + cell.Width / 2f, cell.Y + 24), 32f, switchedOff ? Color.DarkGray : installed ? Color.White : Color.Gray);
				if (cell.Contains(Main.MouseScreen.ToPoint())) {
					Main.LocalPlayer.mouseInterface = true;
					Item tmp = item;
					ItemSlot.MouseHover(ref tmp, ItemSlot.Context.InventoryItem);
					if (Main.mouseLeft && Main.mouseLeftRelease) {
						Main.mouseLeftRelease = false;
						if (installed)
							YoyoNPCActions.ToggleBillyType(types[i]);
						else
							YoyoNPCActions.InstallBillyType(player, types[i]);
					}
				}
				string state = !installed ? $"Have: {have}" : switchedOff ? "Off" : "Fitted";
				Color stateColor = !installed ? Color.Yellow : switchedOff ? Color.Tomato : Color.LightGreen;
				Terraria.Utils.DrawBorderString(sb, state, new Vector2(cell.X + 6, cell.Y + 48), stateColor, 0.7f);
			}
		}

		private static void DrawStatus(SpriteBatch sb, NPC npc, Rectangle content)
		{
			int up = BillyTheBearing.UpgradeCount;
			int max = BillyTheBearing.MaxUpgrades;
			float y = content.Y + 10;
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y),
				$"Billy status:\n- Upgrades fitted: {up}/{max}\n- Contact damage: {10 + up * 4}\n- Follow speed: {(6f + up * 0.5f):0.0}  Orbit speed: {(0.03f + up * 0.004f):0.000}\n- Bearing shots: {(BillyTheBearing.AllUpgrades ? "TWIN shots (BTB + BTBU)" : BillyTheBearing.HalfUpgrades ? "Single shots (BTB)" : "Locked (reach half upgrades)")}\n- Fires every {(BillyTheBearing.AllUpgrades ? 45 : 70)} ticks at enemies within 600px during boss fights.",
				Color.White, 0.9f, content.Width - 24);
		}

		// Magical Yoyo extras

		private static void DrawEssence(SpriteBatch sb, NPC npc, Rectangle content)
		{
			Player player = Main.LocalPlayer;
			int buffType = ModContent.BuffType<Content.Buffs.MagicalEssence>();
			int idx = player.FindBuffIndex(buffType);
			float y = content.Y + 20;
			if (idx >= 0) {
				int seconds = player.buffTime[idx] / 60;
				y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y), $"Magical Essence active: +5% yoyo damage for {seconds / 3600}h {(seconds % 3600) / 60}m {seconds % 60}s more.", Color.LightGreen, 0.9f, content.Width - 24) + 12;
			}
			else {
				y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y), "No blessing right now. Pet the yoyo to gain +5% yoyo damage for 8 hours.", Color.Yellow, 0.9f, content.Width - 24) + 12;
			}
			Rectangle btn = new(content.X + 12, (int)y, 220, 32);
			if (YoyoNPCPanel.Button(sb, btn, idx >= 0 ? "Refresh blessing" : "Gain blessing", null, false, false, 0.85f))
				YoyoNPCActions.PetMagical(player);
		}

		private static void DrawAbout(SpriteBatch sb, NPC npc, Rectangle content)
		{
			YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, content.Y + 16),
				"A yoyo soaked in raw magic. It moved in after an evil boss fell, and it never truly leaves. Petting shares a little of that power: Magical Essence, +5% yoyo damage, 8 hours at a time.",
				Color.White, 0.9f, content.Width - 24);
		}

		private static void DrawFarewell(SpriteBatch sb, NPC npc, Rectangle content)
		{
			float y = content.Y + 30;
			string text = npc.type == ModContent.NPCType<BillyTheBearing>() ? "*Billy curls up with a happy clunk.*" : "The magic hums quietly as you step back.";
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 12, y), text, Color.White, 0.95f, content.Width - 24) + 16;
			Rectangle btn = new(content.X + 12, (int)y, 200, 32);
			if (YoyoNPCPanel.Button(sb, btn, "Say goodbye", "Close this panel", false, false, 0.85f))
				YoyoNPCUISystem.ClosePanel();
		}

		// Curses (Ancient Yoyo-ist). Separate tab from Spells. Scrollable, paginated,
		// resolution-aware (uses content bounds, never fixed off-screen), readable text.
		private const int CurseRowH = 24;

		private static void DrawCurses(SpriteBatch sb, NPC npc, Rectangle content)
		{
			Player player = Main.LocalPlayer;
			float y = content.Y + 4;
			y += DrawHeldCard(sb, content, y, showStrip: false);
			// Plantera gate.
			if (!NPC.downedPlantBoss) {
				y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y),
					"Curses require Plantera defeated. The old magic will not bargain before then.", Color.Orange, 0.85f, content.Width - 16) + 4;
				return;
			}
			int essenceType = ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.MagicalEssence>();
			int have = Content.NPCs.Town.YoyoTownShared.CountInInventory(player, essenceType);
			Terraria.Utils.DrawBorderString(sb, $"Magical Essence: x{have}", new Vector2(content.X + 8, y + 2), Color.Pink, 0.85f);
			Rectangle strip = new(content.Right - 150, (int)y, 140, 24);
			if (YoyoNPCPanel.Button(sb, strip, "Cleanse curse", "Remove the curse from your held yoyo (1 gold). Keeps all other modifiers.", false, YoyoNPCActions.HeldYoyo(player) == null, 0.75f))
				Content.Customization.YoyoCurseActions.StripCurse(player);
			y += 26;
			// Essence selector: 1 / 3 / 5 / 10. Odds shown before confirmation.
			int[] options = { 1, 3, 5, 10 };
			int ew = (content.Width - 16 - 3 * 6) / 4;
			for (int i = 0; i < options.Length; i++) {
				Rectangle br = new(content.X + 8 + i * (ew + 6), (int)y, ew, 26);
				Content.Customization.YoyoCurseDefs.OddsForEssence(options[i], out float chance, out float high);
				string tip = $"{options[i]} essence: {chance:0%} curse, {high:0%} of curses are High ({chance * high:0.0%} High overall).";
				if (YoyoNPCPanel.Button(sb, br, $"{options[i]} Ess", tip, Content.Customization.YoyoCurseActions.SelectedEssence == options[i], have < options[i], 0.8f))
					Content.Customization.YoyoCurseActions.SelectedEssence = options[i];
			}
			y += 30;
			int sel = Content.Customization.YoyoCurseActions.SelectedEssence;
			Content.Customization.YoyoCurseDefs.OddsForEssence(Content.Customization.YoyoCurseDefs.NormalizedEssence(sel), out float cChance, out float hShare);
			Item held = YoyoNPCActions.HeldYoyo(player);
			string cur = "none";
			if (held != null && held.TryGetGlobalItem<Content.Customization.YoyoCustomizationGlobalItem>(out var cust) && cust.CurseId >= 0)
				cur = Content.Customization.YoyoCurseDefs.ById(cust.CurseId)?.Name ?? "none";
			y += YoyoNPCPanel.DrawTextBlock(sb, new Vector2(content.X + 8, y),
				$"Ritual: {sel} Magical Essence. Curse chance {cChance:0%} (High {cChance * hShare:0.0%}, Low {cChance * (1 - hShare):0.0%}). New curse replaces: {cur}. Other modifiers kept. Fizzle keeps the old curse.",
				Color.LightGray, 0.8f, content.Width - 200) + 2;
			Rectangle roll = new(content.Right - 180, (int)y - 2, 84, 30);
			Rectangle cancel = new(content.Right - 90, (int)y - 2, 82, 30);
			bool hasYoyo = held != null;
			if (YoyoNPCPanel.Button(sb, roll, "Roll", $"Spend {sel} essence and roll. Odds above match the actual roll.", false, !hasYoyo || have < sel, 0.85f))
				Content.Customization.YoyoCurseActions.RollCurse(player);
			if (YoyoNPCPanel.Button(sb, cancel, "Cancel", "Spend nothing.", false, false, 0.85f))
				YoyoNPCUISystem.SetMessage("Cancelled. Nothing spent.");
			y += 34;
			// Grimoire: all curses, scrollable, rarity-colored, readable scale.
			Terraria.Utils.DrawBorderString(sb, "Grimoire (scroll to browse, Low then High):", new Vector2(content.X + 8, y + 2), Color.Gray, 0.75f);
			y += 20;
			var all = Content.Customization.YoyoCurseDefs.All;
			int listH = content.Bottom - (int)y - 28;
			int visible = System.Math.Max(1, listH / CurseRowH);
			int maxScroll = System.Math.Max(0, all.Count - visible);
			int offset = System.Math.Min(YoyoNPCUISystem.ListScroll, maxScroll);
			if (offset < 0) offset = 0;
			YoyoNPCUISystem.ListScroll = offset;
			for (int r = 0; r < visible; r++) {
				int idx = offset + r;
				if (idx >= all.Count) break;
				var curse = all[idx];
				Rectangle rowBox = new(content.X + 4, (int)y + r * CurseRowH, content.Width - 8, CurseRowH - 2);
				sb.Draw(TextureAssets.MagicPixel.Value, rowBox, curse.High ? new Color(52, 26, 30, 220) : new Color(40, 34, 26, 220));
				Color nameColor = curse.High ? Color.Red : Color.Orange;
				string line = $"{curse.Name} [{curse.Tier}]";
				if (line.Length > 44) line = line.Substring(0, 43) + ".";
				Terraria.Utils.DrawBorderString(sb, line, new Vector2(rowBox.X + 8, rowBox.Y + 3), nameColor, 0.75f);
				if (rowBox.Contains(Main.MouseScreen.ToPoint())) {
					Main.LocalPlayer.mouseInterface = true;
					Main.hoverItemName = $"{curse.Name} ({curse.Tier})\n{curse.Description}";
				}
			}
			DrawScrollFooter(sb, content, content.Bottom - 26, all.Count, visible, "Curses");
		}
	}
}
