using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using BiltonsYoyoMod.Content.NPCs.Town;

namespace BiltonsYoyoMod.Content.Customization
{
	// Per-yoyo storage for Tuning + Calibration + Mastery-family + Curse.
	// Coexists with prefix + Spell + Modification: each category has its own slot.
	// Tuning normal/extended share TuningId, Calibration normal/extended share CalibrationId,
	// Mastery/TrueMastery share MasteryId, Curse has its own slot (new roll replaces old).
	public class YoyoCustomizationGlobalItem : GlobalItem
	{
		public int TuningId = -1;
		public int CalibrationId = -1;
		public int MasteryId = -1;
		public int CurseId = -1;

		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(Item item, bool lateInstantiation)
		{
			return Content.NPCs.Town.YoyoTownShared.IsYoyo(item);
		}

		public bool HasAny => TuningId >= 0 || CalibrationId >= 0 || MasteryId >= 0 || CurseId >= 0;

		public string TuningName() => YoyoTuningDefs.ById(TuningId)?.Name ?? "N/A";
		public string CalibrationName() => YoyoCalibrationDefs.ById(CalibrationId)?.Name ?? "N/A";
		public string MasteryName() => YoyoMasteryDefs.ById(MasteryId)?.Name ?? "N/A";
		public string CurseName() => YoyoCurseDefs.ById(CurseId)?.Name ?? "N/A";

		// One builder for the WHOLE title, used by every system (spells, mods,
		// reforges, tuning, calibration, mastery, curses). Because everything
		// funnels through here, applying one modifier can never delete another's
		// tag, and repeated refreshes never stack duplicates.
		// Layout: [spell] {mod} (tuning) <calib> +mastery+ !curse! BaseName,
		// ALWAYS in compact code form - (T)/(TB), <C>/<CT>, +M+/+EC+, !C!/!RD!,
		// [P]/[CF], {WZ}/{HTS} - so titles are short, stable and identical on
		// every code path (apply, reforge, load, net, clone). Full names live
		// in tooltips, station rows and pickers, never in the title itself.
		// The reforge prefix is deliberately NOT baked in: the game prepends
		// it at display time (AffixName), so baking it would print it twice
		// ("Quick Quick ...").
		public static string Code(string name)
		{
			if (string.IsNullOrEmpty(name))
				return "?";
			var words = new System.Collections.Generic.List<string>();
			foreach (string chunk in name.Split(new[] { ' ', '-', '\'' }, System.StringSplitOptions.RemoveEmptyEntries)) {
				string clean = "";
				foreach (char ch in chunk) {
					if (char.IsLetter(ch))
						clean += ch;
				}
				if (clean.Length > 0)
					words.Add(clean);
			}
			if (words.Count == 0)
				return "?";
			if (words.Count == 1)
				return words[0].Substring(0, System.Math.Min(2, words[0].Length)).ToUpperInvariant();
			string code = "";
			foreach (string w in words)
				code += char.ToUpperInvariant(w[0]);
			if (code.Length > 3)
				code = code.Substring(0, 3);
			return code;
		}

		public static void RefreshFullTitle(Item item)
		{
			if (item == null || item.IsAir)
				return;
			item.TryGetGlobalItem<Common.YoyoEnhancementGlobalItem>(out var enh);
			item.TryGetGlobalItem<YoyoCustomizationGlobalItem>(out var cust);
			var spell = enh != null ? YoyoSpellDefs.ById(enh.SpellId) : null;
			var mod = enh != null ? YoyoModificationDefs.ById(enh.ModificationId) : null;
			var t = cust != null ? YoyoTuningDefs.ById(cust.TuningId) : null;
			var c = cust != null ? YoyoCalibrationDefs.ById(cust.CalibrationId) : null;
			var m = cust != null ? YoyoMasteryDefs.ById(cust.MasteryId) : null;
			var k = cust != null ? YoyoCurseDefs.ById(cust.CurseId) : null;
			if (spell == null && mod == null && t == null && c == null && m == null && k == null) {
				item.SetNameOverride(null);
				return;
			}
			// NOTE: clear any existing override FIRST, because AffixName includes
			// the current override in item.Name - otherwise tags stack up
			// ("[On Fire!] [On Fire!] ...") on every refresh. The override itself
			// never carries the reforge prefix (see above).
			string baseName = Lang.GetItemNameValue(item.type);
			int tagCount = (spell != null ? 1 : 0) + (mod != null ? 1 : 0)
				+ (t != null ? 1 : 0) + (c != null ? 1 : 0) + (m != null ? 1 : 0) + (k != null ? 1 : 0);
			if (tagCount > 5) {
				// Fully loaded (all six slots): collapse to a count badge.
				// Full names stay one hover (SHIFT) away.
				item.SetNameOverride($"[{tagCount} mods] {baseName}");
				return;
			}
			string tags = "";
			if (spell != null)
				tags += $"[{Code(spell.Name)}] ";
			if (mod != null)
				tags += $"{{{Code(mod.ShortName)}}} ";
			string tail = "";
			if (t != null) tail += $"({Code(t.Name)}) ";
			if (c != null) tail += $"<{Code(c.Name)}> ";
			if (m != null) tail += $"+{Code(m.Name)}+ ";
			if (k != null) tail += $"!{Code(k.Name)}! ";
			string title = $"{tags}{tail}{baseName}";
			const int max = 64;
			if (title.Length > max) {
				// Last resort: trim the base weapon name. Tags are never cut.
				int keep = System.Math.Max(8, max - tags.Length - tail.Length - 3);
				if (keep < baseName.Length)
					baseName = baseName.Substring(0, keep) + "...";
				title = $"{tags}{tail}{baseName}";
			}
			item.SetNameOverride(title);
		}

		public static void RefreshNameOverride(Item item) => RefreshFullTitle(item);

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			// Compact by default (one line, never overflows); hold SHIFT for the
			// full breakdown of customs AND spells/modifications. The item name
			// already shows which mods are fitted.
			bool detailed = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
				|| Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
			if (!HasAny && !detailed)
				return;
			if (detailed) {
				// Drop the compact "[Spell] {Mod}" header line: the item name
				// already shows every tag, and the full lines below restate
				// each name once ("[Midas]" + "Midas - ..." would echo).
				tooltips.RemoveAll(l => l.Name == "YoyoEnhancement");
			}
			if (!detailed) {
				tooltips.Add(new TooltipLine(Mod, "YoyoCustomHint", "Hold Shift to Display Modifiers") {
					OverrideColor = Color.Gray
				});
				return;
			}
			if (item.TryGetGlobalItem<Common.YoyoEnhancementGlobalItem>(out var enhShift)) {
				var modShift = YoyoModificationDefs.ById(enhShift.ModificationId);
				if (modShift != null)
					tooltips.Add(new TooltipLine(Mod, "YoyoModificationDesc", "Modification: " + modShift.Description));
				var spellShift = YoyoSpellDefs.ById(enhShift.SpellId);
				if (spellShift != null)
					tooltips.Add(new TooltipLine(Mod, "YoyoSpellDesc", "Spell: " + spellShift.Description));
			}
			var t = YoyoTuningDefs.ById(TuningId);
			if (t != null)
				tooltips.Add(new TooltipLine(Mod, "YoyoTuningDesc", $"{t.Tier}: {t.Name} - {t.Description}") { OverrideColor = Color.LightSkyBlue });
			var c = YoyoCalibrationDefs.ById(CalibrationId);
			if (c != null)
				tooltips.Add(new TooltipLine(Mod, "YoyoCalibrationDesc", $"{c.Tier}: {c.Name} - {c.Description}") { OverrideColor = Color.PaleGreen });
			var m = YoyoMasteryDefs.ById(MasteryId);
			if (m != null)
				tooltips.Add(new TooltipLine(Mod, "YoyoMasteryDesc", $"{m.Tier}: {m.Name} - {m.Description}") { OverrideColor = Color.Gold });
			var k = YoyoCurseDefs.ById(CurseId);
			if (k != null)
				tooltips.Add(new TooltipLine(Mod, "YoyoCurseDesc", $"{k.Tier}: {k.Name} - {k.Description}") { OverrideColor = k.High ? Color.Red : Color.Orange });
		}

		public override void SaveData(Item item, TagCompound tag)
		{
			tag["tuning"] = TuningId;
			tag["calib"] = CalibrationId;
			tag["mastery"] = MasteryId;
			tag["curse"] = CurseId;
		}

		public override void LoadData(Item item, TagCompound tag)
		{
			TuningId = tag.ContainsKey("tuning") ? tag.GetInt("tuning") : -1;
			CalibrationId = tag.ContainsKey("calib") ? tag.GetInt("calib") : -1;
			MasteryId = tag.ContainsKey("mastery") ? tag.GetInt("mastery") : -1;
			CurseId = tag.ContainsKey("curse") ? tag.GetInt("curse") : -1;
			if (YoyoTuningDefs.ById(TuningId) == null && TuningId != -1) TuningId = -1;
			if (YoyoCalibrationDefs.ById(CalibrationId) == null && CalibrationId != -1) CalibrationId = -1;
			if (YoyoMasteryDefs.ById(MasteryId) == null && MasteryId != -1) MasteryId = -1;
			if (YoyoCurseDefs.ById(CurseId) == null && CurseId != -1) CurseId = -1;
			RefreshNameOverride(item);
		}

		public override void NetSend(Item item, BinaryWriter writer)
		{
			writer.Write((short)TuningId);
			writer.Write((short)CalibrationId);
			writer.Write((short)MasteryId);
			writer.Write((short)CurseId);
		}

		public override void NetReceive(Item item, BinaryReader reader)
		{
			TuningId = reader.ReadInt16();
			CalibrationId = reader.ReadInt16();
			MasteryId = reader.ReadInt16();
			CurseId = reader.ReadInt16();
			RefreshNameOverride(item);
		}

		public override void OnCreated(Item item, ItemCreationContext context)
		{
			// Belt-and-suspenders: any path that materializes an item outside
			// LoadData (clones, shop purchases, shimmer results) rebuilds tags.
			RefreshNameOverride(item);
		}

		public override void PostReforge(Item item)
		{
			RefreshNameOverride(item);
		}
	}
}
