using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using BiltonsYoyoMod.Content.NPCs.Town;

namespace BiltonsYoyoMod.Common
{
	// Stores the extra revamp properties on yoyo items so that a normal reforge,
	// an Ancient Yo-yoist spell and a Tinkerer modification can all exist on the
	// same weapon, e.g. Quick [Poisoned] {Weighted Zinc} Wooden Yoyo.
	public class YoyoEnhancementGlobalItem : GlobalItem
	{
		public int ModificationId = -1; // -1 = none, otherwise YoyoModificationDefs id.
		public int SpellId = -1; // -1 = none, otherwise YoyoSpellDefs id.

		public override bool InstancePerEntity => true;

		public override bool AppliesToEntity(Item item, bool lateInstantiation)
		{
			return YoyoTownShared.IsYoyo(item);
		}

		public string GetModificationName()
		{
			var def = YoyoModificationDefs.ById(ModificationId);
			return def != null ? def.ShortName : "N/A";
		}

		public string GetSpellName()
		{
			var def = YoyoSpellDefs.ById(SpellId);
			return def != null ? def.Name : "N/A";
		}

		// Title assembly now funnels through the unified builder (customization
		// item), so spells, mods, reforges and every custom family stack
		// instead of wiping each other. Example:
		// Quick [Poisoned] {Weighted Zinc} Wooden Yoyo (Snapback) <Crosscut> +Encore+ !Recoil Thread!
		public static void RefreshNameOverride(Item item)
		{
			global::BiltonsYoyoMod.Content.Customization.YoyoCustomizationGlobalItem.RefreshFullTitle(item);
		}

		public override void PostReforge(Item item)
		{
			RefreshNameOverride(item);
		}

		public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
		{
			if (ModificationId < 0 && SpellId < 0)
				return;
			var def = YoyoModificationDefs.ById(ModificationId);
			var spell = YoyoSpellDefs.ById(SpellId);
			string text = "";
			if (spell != null)
				text += $"[{spell.Name}] ";
			if (def != null)
				text += $"{{{def.ShortName}}}";
			if (text.Length > 0) {
				tooltips.Add(new TooltipLine(Mod, "YoyoEnhancement", text.Trim()) {
					OverrideColor = Color.LightGoldenrodYellow
				});
			}
			// Full spell/modification descriptions live in the SHIFT-expanded
			// view (customization item) so normal hovers stay compact.
		}

		public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
		{
			var def = YoyoModificationDefs.ById(ModificationId);
			if (def == null)
				return;
			if (def.Id == 8) // Thrust Vector Thrusters: +5% damage.
				damage += 0.05f;
		}

		public override void ModifyWeaponCrit(Item item, Player player, ref float crit)
		{
			// Shredder Flaps crit ramps on the projectile while deployed (see
			// YoyoEnhancementGlobalProjectile); nothing flat is granted here.
		}

		public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone)
		{
			var spell = YoyoSpellDefs.ById(SpellId);
			if (spell == null)
				return;
			foreach (int buff in spell.Debuffs) {
				target.AddBuff(buff, 300);
			}
		}

		public override void OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo)
		{
			var spell = YoyoSpellDefs.ById(SpellId);
			if (spell == null)
				return;
			foreach (int buff in spell.Debuffs) {
				target.AddBuff(buff, 300);
			}
		}

		public override void SaveData(Item item, TagCompound tag)
		{
			tag["modification"] = ModificationId;
			tag["spell"] = SpellId;
		}

		public override void NetSend(Item item, System.IO.BinaryWriter writer)
		{
			writer.Write((short)ModificationId);
			writer.Write((short)SpellId);
		}

		public override void NetReceive(Item item, System.IO.BinaryReader reader)
		{
			ModificationId = reader.ReadInt16();
			SpellId = reader.ReadInt16();
			RefreshNameOverride(item);
		}

		public override void OnCreated(Item item, ItemCreationContext context)
		{
			// Belt-and-suspenders: any path that materializes an item outside
			// LoadData (clones, shop purchases, shimmer results) rebuilds tags.
			RefreshNameOverride(item);
		}

		public override void LoadData(Item item, TagCompound tag)
		{
			ModificationId = tag.GetInt("modification");
			SpellId = tag.GetInt("spell");
			// Back-compat: missing keys read as 0, which would wrongly mean an entry.
			if (!tag.ContainsKey("modification"))
				ModificationId = -1;
			if (!tag.ContainsKey("spell"))
				SpellId = -1;
			RefreshNameOverride(item);
		}
	}
}
