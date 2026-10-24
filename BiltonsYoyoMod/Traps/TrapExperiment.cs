using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using YoyoPanel = BiltonsYoyoMod.Content.UI.YoyoNPC.YoyoNPCPanel;

namespace BiltonsYoyoMod.Traps
{
	// Shared experiment workbench for defensive blocks (sentry / link / tripwire).
	//
	// Instead of scrolling a shop list, the player picks real stacks straight out
	// of their own inventory through the block UI: tap a grid cell to load slot
	// A or B, tune amounts, preview the reaction, then Combine. Combos ("this +
	// this = that") use real mod materials and produce real block effects out of
	// EXISTING trap state only (energy, cooldown, stress, fray, condition, uses)
	// so nothing foreign is bolted on. The widget is shared and can later back
	// engine/station panels too; payment follows the mod's client-pays pattern
	// while the effect itself is applied server-side via TrapNet.
	public static class TrapExperiment
	{
		public static int SlotA = -1;
		public static int SlotB = -1;
		public static int AmtA = 1;
		public static int AmtB = 1;
		public static int PageA;
		public static int PageB;
		public static bool FillA = true; // which slot the grid fills next

		public const int PageSize = 20;
		public const int Cols = 10;

		public static void Clear()
		{
			SlotA = SlotB = -1;
			AmtA = AmtB = 1;
			PageA = PageB = 0;
			FillA = true;
		}

		private static int PageCount => (58 + PageSize - 1) / PageSize;

		private static bool IsWire(int t) => t == ItemID.Wire;
		private static bool IsOilWax(int t) => t == ModContent.ItemType<Items.Consumables.BearingOil>() || t == ModContent.ItemType<Items.Consumables.StringWax>();
		private static bool IsModMat(int t) =>
			t == ModContent.ItemType<Items.Materials.YoyoScraps>() ||
			t == ModContent.ItemType<Items.Materials.YoyoParts>() ||
			t == ModContent.ItemType<Items.Materials.AdvancedYoyoParts>() ||
			t == ModContent.ItemType<Items.Materials.GuardianCoreM>() ||
			t == ModContent.ItemType<Items.Materials.AncientScraps>() ||
			t == ModContent.ItemType<Items.BlackMarket.IllegalYoyoParts>();

		// Effect ids. -1 = no known reaction (refuse, consume nothing).
		// Shared take computation so preview and consume always agree,
		// including two takes from the same stack.
		private static bool TakeAmounts(Player player, out int na, out int nb)
		{
			na = nb = 0;
			Item a = SlotItem(player, SlotA);
			Item b = SlotItem(player, SlotB);
			if (a == null || b == null)
				return false;
			na = System.Math.Min(AmtA, a.stack);
			nb = System.Math.Min(AmtB, b.stack);
			if (SlotA == SlotB) {
				int take = System.Math.Min(na + nb, a.stack);
				na = take;
				nb = 0;
			}
			return na > 0;
		}

		public static int PreviewEffect(Player player, out string label)
		{
			return PreviewEffectFor(player, new Point(-9999, -9999), out label);
		}

		// Head-research unlock mixes (specific beats generic): Stinger x5,
		// Gold Bar x5, Ectoplasm x3, Power Cell x1, Lunar Bar x5, Black Lens
		// x1, Ice x50, Vertebrae x10 — each + anything, each unlocks one
		// exclusive head/tether on THIS trap. Effect ids 10..35 (bit = id - 10).
		public struct ExpUnlock
		{
			public int Bit;
			public string Name;
			public int Mat;
			public int Count;
		}

		public static readonly ExpUnlock[] Unlocks = {
			new ExpUnlock { Bit = 0, Name = "Venom counterweight", Mat = ItemID.Stinger, Count = 5 },
			new ExpUnlock { Bit = 1, Name = "Gilded counterweight", Mat = ItemID.GoldBar, Count = 5 },
			new ExpUnlock { Bit = 2, Name = "Warden counterweight", Mat = ItemID.Ectoplasm, Count = 3 },
			new ExpUnlock { Bit = 3, Name = "Orbit counterweight", Mat = ItemID.LihzahrdPowerCell, Count = 1 },
			new ExpUnlock { Bit = 4, Name = "Lunar counterweight", Mat = ItemID.LunarBar, Count = 5 },
			new ExpUnlock { Bit = 5, Name = "Dusk yoyo head", Mat = ItemID.BlackLens, Count = 1 },
			new ExpUnlock { Bit = 6, Name = "Glacier yoyo head", Mat = ItemID.IceBlock, Count = 50 },
			new ExpUnlock { Bit = 7, Name = "Blood Tithe yoyo head", Mat = ItemID.Vertebrae, Count = 10 },
			new ExpUnlock { Bit = 25, Name = "Ancient counterweight", Mat = ItemID.Bone, Count = 50 },
			new ExpUnlock { Bit = 8, Name = "Cyan tether", Mat = 3299, Count = 1 }, // wiki: Cyan String
			new ExpUnlock { Bit = 9, Name = "Ancient tether", Mat = ModContent.ItemType<Items.Materials.AncientScraps>(), Count = 5 },
			new ExpUnlock { Bit = 10, Name = "Crimson tether", Mat = ItemID.CrimtaneBar, Count = 5 },
			new ExpUnlock { Bit = 11, Name = "Red tether", Mat = 3293, Count = 1 },
			new ExpUnlock { Bit = 12, Name = "Orange tether", Mat = 3294, Count = 1 },
			new ExpUnlock { Bit = 13, Name = "Yellow tether", Mat = 3295, Count = 1 },
			new ExpUnlock { Bit = 14, Name = "Lime tether", Mat = 3296, Count = 1 },
			new ExpUnlock { Bit = 15, Name = "Green tether", Mat = 3297, Count = 1 },
			new ExpUnlock { Bit = 16, Name = "Teal tether", Mat = 3298, Count = 1 },
			new ExpUnlock { Bit = 17, Name = "Sky Blue tether", Mat = 3300, Count = 1 },
			new ExpUnlock { Bit = 18, Name = "Blue tether", Mat = 3301, Count = 1 },
			new ExpUnlock { Bit = 19, Name = "Purple tether", Mat = 3302, Count = 1 },
			new ExpUnlock { Bit = 20, Name = "Violet tether", Mat = 3303, Count = 1 },
			new ExpUnlock { Bit = 21, Name = "Pink tether", Mat = 3304, Count = 1 },
			new ExpUnlock { Bit = 22, Name = "Black tether", Mat = 3308, Count = 1 },
			new ExpUnlock { Bit = 23, Name = "Brown tether", Mat = 3305, Count = 1 },
			new ExpUnlock { Bit = 24, Name = "Rainbow tether", Mat = 3307, Count = 1 },
		};

				// Unlock relevance per block kind: sentry weights, tripwire heads +
		// tethers; link blocks research only the generic bench recipes.
		public static bool UnlockApplies(int bit, TrapKind kind)
		{
			if (kind == TrapKind.Sentry)
			return bit <= 4 || bit == 25;
			if (kind == TrapKind.Tripwire)
			return bit >= 5 && bit <= 24;
			return false;
		}

		public static int PreviewEffectFor(Player player, Point tile, out string label)
		{
			label = "Pick two stacks above, then Combine.";
			Item a = SlotItem(player, SlotA);
			Item b = SlotItem(player, SlotB);
			if (a == null || b == null) {
				label = "Load both slots from your inventory first.";
				return -1;
			}
			if (!TakeAmounts(player, out int na, out int nb)) {
				label = "Load both slots from your inventory first.";
				return -1;
			}
			if (TrapSystem.Traps.TryGetValue(tile, out var st)) {
				foreach (var u in Unlocks) {
					if (!UnlockApplies(u.Bit, st.Kind))
						continue; // wrong block kind for this unlock
					if ((st.ExpHeads & (1 << u.Bit)) != 0)
						continue; // already researched here
					bool hit = (a.type == u.Mat && na >= u.Count) || (b.type == u.Mat && nb >= u.Count);
					if (hit) {
						string matName;
						try { matName = Lang.GetItemNameValue(u.Mat); }
						catch { matName = "materials"; }
						label = $"Unlocks {u.Name}! Needs {u.Count}x {matName} + anything.";
						return 10 + u.Bit;
					}
				}
			}
			bool oilWax = (IsOilWax(a.type) && na >= 1) || (IsOilWax(b.type) && nb >= 1);
			bool wire = (IsWire(a.type) && na >= 5) || (IsWire(b.type) && nb >= 5);
			bool mats = IsModMat(a.type) && IsModMat(b.type) && na + nb >= 4;
			bool attune = ((a.type == ItemID.Cobweb || a.type == ItemID.Silk) && na >= 10) ||
				((b.type == ItemID.Cobweb || b.type == ItemID.Silk) && nb >= 10);
			if (oilWax) { label = "Oil & Wax Service: +25% condition."; return 2; }
			if (wire) { label = "Rewire Kit: clears stress + fray, +40 power."; return 1; }
			if (mats) { label = "Field Recharge: full power, cooldown cleared."; return 0; }
			if (attune) { label = "Attunement: +10 field uses."; return 3; }
			label = "No known reaction for that mix (see list below). Nothing will be consumed.";
			return -1;
		}

		private static Item SlotItem(Player player, int slot)
		{
			if (player == null || slot < 0 || slot >= player.inventory.Length)
				return null;
			Item it = player.inventory[slot];
			if (it == null || it.IsAir || it.stack <= 0)
				return null;
			return it;
		}

		public static bool TryCombine(Player player, Point tile)
		{
			if (SlotA < 0 || SlotB < 0)
				return false;
			int fx = PreviewEffectFor(player, tile, out _);
			if (fx < 0)
				return false;
			if (!TakeAmounts(player, out int na, out int nb))
				return false;
			if (SlotA == SlotB) {
				Item one = SlotItem(player, SlotA);
				if (one == null || na + nb <= 0 || na + nb > one.stack)
					return false;
				one.stack -= na + nb;
				if (one.stack <= 0)
					one.TurnToAir();
			}
			else {
				Item a = SlotItem(player, SlotA);
				Item b = SlotItem(player, SlotB);
				if (a == null || b == null)
					return false;
				a.stack -= na;
				if (a.stack <= 0)
					a.TurnToAir();
				b.stack -= nb;
				if (b.stack <= 0)
					b.TurnToAir();
			}
			if (Main.netMode == NetmodeID.MultiplayerClient)
				TrapNet.SendExperiment(tile, fx);
			else
				TrapActions.ApplyExperiment(tile, fx);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item37, player.Center);
			return true;
		}

		// Compact picker: header + inventory grid + pager/stepper on one flowing
		// row each. All positions derive from pw (no hardcoded overflow), so
		// narrow panels wrap instead of overlapping. Grids dim when not filling.
		public static float DrawPicker(SpriteBatch sb, Player player, int x, int pw, float cy, string title, ref int slot, ref int amt, ref int page, bool isFillTarget, System.Action pickTarget)
		{
			
			TrapUI.CText(sb, title, new Vector2(x + 14, cy), Color.White, 0.8f);
			Rectangle tgt = new Rectangle(x + pw - 150, (int)cy - 2, 136, 22);
			if (tgt.X < x + 160)
				tgt.X = x + 160;
			if (TrapUI.CButton(sb, tgt, isFillTarget ? "[filling]" : "fill here", "Grid taps load this slot", isFillTarget, false, 0.7f))
				pickTarget();
			cy += 24;
			// Responsive grid: cells shrink on narrow panels, never overflow.
			int cell = System.Math.Clamp((pw - 28) / Cols, 28, 36);
			int gap = 4;
			int start = page * PageSize;
			for (int k = 0; k < PageSize; k++) {
				int sidx = start + k;
				if (sidx >= player.inventory.Length)
					break;
				int col = k % Cols;
				int row = k / Cols;
				Rectangle cellR = new Rectangle(x + 14 + col * (cell + gap), (int)cy + row * (cell + gap), cell, cell);
				Item it = player.inventory[sidx];
				bool empty = it == null || it.IsAir;
				bool sel = sidx == slot;
				Color dim = !isFillTarget ? new Color(160, 160, 170) : Color.White;
				bool cellFull = cellR.Top >= TrapUI.ClipView.Y && cellR.Bottom <= TrapUI.ClipView.Bottom;
				bool tapped = TrapUI.CButton(sb, cellR, "", empty ? "Empty pocket" : it.Name, sel, empty, 0.7f);
				if (!empty && cellFull) {
					// Same icon method as the modded yoyo NPC shops
					// (YoyoNPCPanel.DrawItemIcon): own cached vanilla request,
					// sheet guard, "?" fallback — never raw shared slots.
					YoyoPanel.DrawItemIcon(sb, it.type, cellR.Center.ToVector2(), cell - 4f, dim);
					if (it.stack > 1)
						TrapUI.CText(sb, it.stack.ToString(), new Vector2(cellR.Right - 18, cellR.Bottom - 16), Color.White, 0.7f);
				}
				if (tapped) {
					if (!empty) {
						slot = sidx;
						amt = System.Math.Clamp(amt, 1, System.Math.Max(1, it.stack));
						Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick, player.Center);
					}
				}
			}
			cy += (PageSize / Cols) * (cell + gap) + 4;
			// Pager + stepper on one flowing row (wraps on narrow panels).
			float rx = x + 14;
			float rowY = cy;
			
			Rectangle pl = new Rectangle((int)rx, (int)rowY, 44, 22);
			if (TrapUI.CButton(sb, pl, "<", "Previous page", page <= 0, page <= 0, 0.75f)) { page = System.Math.Max(0, page - 1); }
			rx += 50;
			Rectangle pn = new Rectangle((int)rx, (int)rowY, 44, 22);
			if (TrapUI.CButton(sb, pn, ">", "Next page", page >= PageCount - 1, page >= PageCount - 1, 0.75f)) { page = System.Math.Min(PageCount - 1, page + 1); }
			rx += 50;
			TrapUI.CText(sb, $"{page + 1}/{PageCount}", new Vector2(rx, rowY + 2), Color.Gray, 0.75f);
			rx += 44;
			// Amount stepper for the loaded stack.
			Item cur = SlotItem(player, slot);
			int max = cur == null ? 1 : cur.stack;
			amt = System.Math.Clamp(amt, 1, System.Math.Max(1, max));
			Rectangle mn = new Rectangle((int)rx, (int)rowY, 38, 22);
			if (mn.Right > x + pw - 120) { rowY += 26; mn.X = x + 14; mn.Y = (int)rowY; }
			if (TrapUI.CButton(sb, mn, "-", "Less", amt <= 1, amt <= 1, 0.8f)) amt = System.Math.Max(1, amt - 1);
			Rectangle ps = new Rectangle(mn.Right + 6, (int)rowY, 38, 22);
			if (TrapUI.CButton(sb, ps, "+", "More", amt >= max, amt >= max, 0.8f)) amt = System.Math.Min(max, amt + 1);
			Rectangle mx = new Rectangle(ps.Right + 6, (int)rowY, 46, 22);
			if (TrapUI.CButton(sb, mx, "max", "Take the whole stack", amt >= max, amt >= max || cur == null, 0.8f)) amt = max;
			float nameW = System.Math.Max(60, x + pw - (mx.Right + 8) - 14);
			TrapUI.CText(sb, cur == null ? "empty" : $"{cur.Name} x{amt}", new Vector2(mx.Right + 8, rowY + 2), Color.LightGreen, 0.75f);
			cy = rowY + 28;
			return cy;
		}
	}
}
