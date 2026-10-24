using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.NPCs.Town;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// Shared tech helpers: damage mults, accessory/mod/spell forwarding, slot math, glove checks.
	/// Tech projectiles are NOT aiStyle Yoyo on purpose (full movement control), so every
	/// yoyo bonus that vanilla would have applied via aiStyle must be re-applied here explicitly.
	/// </summary>
	public static class YoyoTechCommon
	{
		public static bool IsYoyo(Item item) => YoyoTownShared.IsYoyoShape(item);

		/// <summary>Modded shape check OR vanilla yoyo set (vanilla yoyos are Melee, not MeleeNoSpeed).</summary>
		public static bool IsYoyoLike(Item item)
		{
			if (item == null || item.IsAir)
				return false;
			if (YoyoTownShared.IsYoyoShape(item))
				return true;
			try {
				if (item.type > ItemID.None && item.type < ItemID.Sets.Yoyo.Length && ItemID.Sets.Yoyo[item.type])
					return true;
			}
			catch { }
			return false;
		}

		public static bool TechActiveFor(Player player, out YoyoTechType tech)
		{
			tech = YoyoTechType.None;
			if (player == null || !player.active || player.dead)
				return false;
			var tp = player.GetModPlayer<YoyoTechPlayer>();
			if (tp == null || tp.ActiveTech == YoyoTechType.None)
				return false;
			// Gate: the tech's item must still be in inventory.
			if (!YoyoTechPlayer.HasTechItem(player, tp.ActiveTech))
				return false;
			tech = tp.ActiveTech;
			return true;
		}

		public static void GetSourceEnhancement(Item src, out int modId, out int spellId)
		{
			modId = -1;
			spellId = -1;
			if (src == null || src.IsAir)
				return;
			if (src.TryGetGlobalItem<Common.YoyoEnhancementGlobalItem>(out var enh)) {
				modId = enh.ModificationId;
				spellId = enh.SpellId;
			}
		}

		/// <summary>Apply Tinkerer-mod damage/launch/size + Ancient spell debuffs generically.</summary>
		public static void ApplyEnhancementToSpawn(Projectile proj, Item src, int modId)
		{
			if (modId < 0)
				return;
			var def = YoyoModificationDefs.ById(modId);
			if (def == null)
				return;
			if (def.DamageMultiplier != 1f)
				proj.damage = (int)(proj.damage * def.DamageMultiplier);
			if (def.LaunchSpeedMultiplier != 1f)
				proj.velocity *= def.LaunchSpeedMultiplier;
			if (def.Id == 1) {
				proj.scale *= def.SizeMultiplier;
				proj.width = (int)(proj.width * def.SizeMultiplier);
				proj.height = (int)(proj.height * def.SizeMultiplier);
				proj.damage += def.BonusDamage;
			}
			if (def.Id == 9)
				proj.tileCollide = false;
		}

		public static void ApplySpellOnHit(NPC target, int spellId)
		{
			if (spellId < 0 || target == null || !target.active)
				return;
			var spell = YoyoSpellDefs.ById(spellId);
			if (spell == null)
				return;
			foreach (int buff in spell.Debuffs)
				target.AddBuff(buff, buff == BuffID.Daybreak ? 600 : 300);
		}

		public static void StringDust(Vector2 from, Vector2 to, Player owner)
		{
			if (owner == null || !owner.active)
				return;
			var market = owner.GetModPlayer<Players.MarketPlayer>();
			if ((market.GoldenString || market.AncientString) && Main.rand.NextBool(6)) {
				float t = Main.rand.NextFloat(0.1f, 0.95f);
				Vector2 pos = Vector2.Lerp(from, to, t);
				Dust d = Dust.NewDustDirect(pos - new Vector2(4, 4), 8, 8,
					market.AncientString ? DustID.MagicMirror : DustID.GoldCoin, 0f, -0.4f, 120, default, 0.7f);
				d.noGravity = true;
			}
		}

		public static void AncientMagnet(Projectile proj, Player owner)
		{
			if (owner == null || !owner.active)
				return;
			var market = owner.GetModPlayer<Players.MarketPlayer>();
			if (!market.AncientString)
				return;
			for (int i = 0; i < Main.maxItems; i++) {
				Item item = Main.item[i];
				if (item == null || !item.active || item.IsAir)
					continue;
				if (!IsPickup(item.type))
					continue;
				Vector2 toProj = proj.Center - item.Center;
				float dist = toProj.Length();
				if (dist > 192f)
					continue;
				if (dist < 26f) {
					item.Center = owner.Center;
					item.velocity = Vector2.Zero;
					continue;
				}
				item.velocity += toProj.SafeNormalize(Vector2.Zero) * 0.9f;
				if (item.velocity.Length() > 10f)
					item.velocity = item.velocity.SafeNormalize(Vector2.Zero) * 10f;
			}
		}

		public static bool IsPickup(int type)
		{
			return type == ItemID.CopperCoin || type == ItemID.SilverCoin || type == ItemID.GoldCoin || type == ItemID.PlatinumCoin
				|| type == ItemID.Heart || type == ItemID.Star;
		}

		public static bool HasEquipped(Player player, int itemId)
		{
			if (player == null)
				return false;
			for (int i = 0; i < player.armor.Length; i++) {
				Item it = player.armor[i];
				if (it != null && !it.IsAir && it.type == itemId)
					return true;
			}
			return false;
		}

		public static bool HasYoyoGloveOrBag(Player player)
		{
			// Vanilla flags (Yoyo Bag sets yoyoGlove too; Ancient Glove sets it as well).
			return player.yoyoGlove;
		}

		public static bool HasCounterweightAcc(Player player)
		{
			var mp = player.GetModPlayer<Players.MarketPlayer>();
			if (mp.GoldenCounterweight || mp.AncientCounterweight || mp.CelestialBlessing)
				return true;
			// Fallback: name check for modded counterweights in armor.
			for (int i = 0; i < player.armor.Length; i++) {
				Item it = player.armor[i];
				if (it == null || it.IsAir)
					continue;
				string n = it.ModItem?.GetType().Name ?? string.Empty;
				if (n.Contains("Counterweight"))
					return true;
			}
			return false;
		}

		public static bool HasStringAcc(Player player)
		{
			var mp = player.GetModPlayer<Players.MarketPlayer>();
			var rp = player.GetModPlayer<Players.RevampPlayer>();
			if (mp.GoldenString || mp.AncientString || rp.Barbed || rp.Tangled)
				return true;
			for (int i = 0; i < player.armor.Length; i++) {
				Item it = player.armor[i];
				if (it == null || it.IsAir)
					continue;
				string n = it.ModItem?.GetType().Name ?? string.Empty;
				if (n.Contains("String") || n.Contains("Strings"))
					return true;
				if (it.accessory && it.stringColor != 0)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Summon cap: 1 base + vanilla minion slots + yoyo-gear bonuses.
		/// Glove/Bag, counterweights and strings each +1. Duplicators stay
		/// out of techs entirely. Clamped 2..8.
		/// </summary>
		public static int SummonCap(Player player)
		{
			int cap = 1 + player.maxMinions;
			if (HasYoyoGloveOrBag(player))
				cap += 1;
			if (HasCounterweightAcc(player))
				cap += 1;
			if (HasStringAcc(player))
				cap += 1;
			if (cap < 2)
				cap = 2;
			if (cap > 8)
				cap = 8;
			return cap;
		}

		public static int CountOwnedSummons(Player player, int minionType)
		{
			return CountOwnedType(player, minionType);
		}

		public static int CountOwnedType(Player player, int type)
		{
			int n = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == player.whoAmI && p.type == type)
					n++;
			}
			return n;
		}

		/// <summary>
		/// Orbiting guard scaling: 1 per click / cap 3; Yoyo Glove or Bag doubles
		/// both (2 per click / cap 6). Duplicators stay out of techs. Delegates to
		/// <see cref="MagicalOrbit.CapStats"/> so caps live in one place.
		/// </summary>
		public static void MagicalGuardStats(Player player, out int deploy, out int cap)
		{
			MagicalOrbit.CapStats(player, out deploy, out cap);
		}

		public static NPC FindTarget(Vector2 from, float range)
		{
			NPC best = null;
			float bestDist = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC n = Main.npc[i];
				if (n == null || !n.active || n.friendly || n.dontTakeDamage || n.lifeMax <= 5 || n.life <= 0)
					continue;
				if (!n.CanBeChasedBy())
					continue;
				float d = Vector2.DistanceSquared(from, n.Center);
				if (d < bestDist) {
					bestDist = d;
					best = n;
				}
			}
			return best;
		}
	}
}
