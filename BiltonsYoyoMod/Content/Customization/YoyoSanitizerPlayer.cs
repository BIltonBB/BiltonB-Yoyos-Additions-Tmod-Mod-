using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Customization
{
	// Stale-modifier sanitizer: old mod versions could leave Tuning /
	// Calibration / Mastery / Curse / Spell / Modification data (and title
	// tags like [SL]) on weapons that are NOT yoyos (e.g. Meowmere).
	// Apply paths are guarded now (HeldYoyo requires yoyo + yoyo-shape),
	// so this only ever fires on leftovers. Once per second it scans the
	// carrier's own stashes; anything carrying our data (or our stale title
	// tags with no data) that fails EVERY yoyo check gets wiped and its
	// display name rebuilt (RefreshFullTitle clears the override when all
	// slots are empty, which also fixes tooltips since those are data
	// driven). Definite-non-yoyo means: not a yoyo by projectile set, not
	// a yoyo by shape, and not a yoyo by the chest name check — a real
	// yoyo always passes at least one, so this can never false-fire.
	public class YoyoSanitizerPlayer : ModPlayer
	{
		private static readonly Dictionary<int, string> baseNames = new();
		private static int notifyCooldown;

		public override void PostUpdate()
		{
			if (Main.GameUpdateCount % 60 != 0)
				return;
			// Server sanitizes everyone (gameplay truth); clients only
			// touch their own stashes (display + save truth).
			if (!Main.dedServ && Player.whoAmI != Main.myPlayer)
				return;
			int stripped = 0;
			stripped += SanitizeStores(Player.inventory);
			stripped += SanitizeStores(Player.armor);
			stripped += SanitizeItem(Main.mouseItem);
			stripped += SanitizeStores(Player.bank.item);
			stripped += SanitizeStores(Player.bank2.item);
			stripped += SanitizeStores(Player.bank3.item);
			stripped += SanitizeStores(Player.bank4.item);
			if (stripped > 0 && Player.whoAmI == Main.myPlayer && !Main.dedServ && notifyCooldown <= 0) {
				Main.NewText($"Revoked stale yoyo modifiers from {stripped} non-yoyo item{(stripped == 1 ? "" : "s")}.", Color.LightGreen);
				notifyCooldown = 300;
			}
			if (notifyCooldown > 0)
				notifyCooldown -= 60;
		}

		private static int SanitizeStores(Item[] items)
		{
			if (items == null)
				return 0;
			int n = 0;
			for (int i = 0; i < items.Length; i++)
				n += SanitizeItem(items[i]);
			return n;
		}

		private static int SanitizeItem(Item item)
		{
			if (item == null || item.IsAir)
				return 0;
			// Weapons only: damage dealers that are not accessories. Legit
			// carriers are always yoyos, so anything else is pollution.
			if (item.damage <= 0 || item.accessory)
				return 0;
			if (Storage.YoyoChestSystem.IsYoyo(item))
				return 0;
			if (NPCs.Town.YoyoTownShared.IsYoyo(item))
				return 0;
			if (NPCs.Town.YoyoTownShared.IsYoyoShape(item))
				return 0;
			bool dirty = false;
			if (item.TryGetGlobalItem<YoyoCustomizationGlobalItem>(out var cust) && cust.HasAny) {
				cust.TuningId = -1;
				cust.CalibrationId = -1;
				cust.MasteryId = -1;
				cust.CurseId = -1;
				dirty = true;
			}
			if (item.TryGetGlobalItem<Common.YoyoEnhancementGlobalItem>(out var enh)
				&& (enh.SpellId >= 0 || enh.ModificationId >= 0)) {
				enh.SpellId = -1;
				enh.ModificationId = -1;
				dirty = true;
			}
			if (dirty) {
				YoyoCustomizationGlobalItem.RefreshFullTitle(item);
				return 1;
			}
			// No data but our stale title tags survived (old saves baked
			// overrides directly): rebuild only if the name is base name +
			// our tag head, so foreign renames are never touched.
			if (HasStaleOurTags(item)) {
				item.ClearNameOverride();
				return 1;
			}
			return 0;
		}

		private static bool HasStaleOurTags(Item item)
		{
			string baseName;
			if (!baseNames.TryGetValue(item.type, out baseName)) {
				try {
					baseName = Lang.GetItemNameValue(item.type);
				}
				catch {
					return false;
				}
				baseNames[item.type] = baseName;
			}
			if (string.IsNullOrEmpty(baseName))
				return false;
			string shown;
			try {
				shown = item.Name;
			}
			catch {
				return false;
			}
			if (shown == baseName || !shown.EndsWith(baseName))
				return false;
			string head = shown.Substring(0, shown.Length - baseName.Length).Trim();
			if (head.Length == 0)
				return false;
			// Our builder vocabulary only: optional single-word reforge
			// prefix, then 1+ tags ([XX] {XX} (XX) <XX> +XX+ !XX! [N mods]).
			try {
				return System.Text.RegularExpressions.Regex.IsMatch(head,
					@"^([A-Za-z]+ )?((\[[A-Z?]{1,3}\]|\{[A-Z?]{1,3}\}|\([A-Z?]{1,3}\)|<[A-Z?]{1,3}>|\+[A-Z?]{1,3}\+|![A-Z?]{1,3}!|\[[0-9]+ mods\]) )+$");
			}
			catch {
				return false;
			}
		}

		public static void ClearBaseNameCache() => baseNames.Clear();
	}
}
