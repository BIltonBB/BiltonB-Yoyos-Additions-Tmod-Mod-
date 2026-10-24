using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// Intercepts yoyo use while a tech is active and converts it to the tech's
	/// projectile. Vanilla yoyos are cancelled (return false) so only the tech
	/// projectile exists — no duplicate logic, no conflicting modes.
	/// When no tech is active, everything passes through untouched.
	/// </summary>
	public class YoyoTechGlobalItem : GlobalItem
	{
		public override bool AppliesToEntity(Item item, bool lateInstantiation)
		{
			return YoyoTechCommon.IsYoyo(item);
		}

		public override bool CanUseItem(Item item, Player player)
		{
			if (!YoyoTechCommon.TechActiveFor(player, out YoyoTechType tech))
				return base.CanUseItem(item, player);

			// Summon tech uses minion slots: block the throw when capped so the
			// player gets clear feedback instead of a silent fizzle.
			if (tech == YoyoTechType.Summon) {
				int minionType = ModContent.ProjectileType<Projectiles.Tech.SummonYoyoMinion>();
				int owned = YoyoTechCommon.CountOwnedSummons(player, minionType);
				int cap = YoyoTechCommon.SummonCap(player);
				if (owned >= cap) {
					if (player.whoAmI == Main.myPlayer && !Main.dedServ) {
						Main.NewText($"Summon Tech: at cap ({owned}/{cap}).", 255, 200, 150);
						SoundEngine.PlaySound(SoundID.MenuClose, player.Center);
					}
					return false;
				}
				// Also respect vanilla slot availability.
				if (player.numMinions >= player.maxMinions + 4) {
					if (player.whoAmI == Main.myPlayer && !Main.dedServ)
						Main.NewText("No minion slots free.", 255, 200, 150);
					return false;
				}
			}

			// Magical tech needs mana: feedback when empty (audio/visual, never chat).
			if (tech == YoyoTechType.Magical) {
				var tp = player.GetModPlayer<YoyoTechPlayer>();
				// One click = one orbit yoyo. Holds and repeat attempts fizzle
				// silently. The press is only spent once every gate below has
				// passed, so a legitimate click can never be swallowed.
				if (!tp.MagicalPressPending)
					return false;
				// Let other globals/the item veto first: keep the click armed if
				// something else blocks this use, so the next ready tick uses it.
				if (!base.CanUseItem(item, player))
					return false;
				// Orbit cap: 3 base, 6 with a Yoyo Glove/Bag (1 per click, doubled).
				MagicalOrbit.CapStats(player, out _, out int cap);
				int owned = YoyoTechCommon.CountOwnedType(player, ModContent.ProjectileType<Projectiles.Tech.MagicalYoyoProj>());
				if (owned >= cap) {
					tp.ClearMagicalPress();
					if (player.whoAmI == Main.myPlayer && !Main.dedServ)
						MagicalOrbit.CueCapped(player);
					return false;
				}
				const int manaCost = 4;
				if (player.statMana < manaCost) {
					tp.ClearMagicalPress();
					if (player.whoAmI == Main.myPlayer && !Main.dedServ)
						MagicalOrbit.CueNoMana(player);
					return false;
				}
				tp.ClearMagicalPress();
				return true;
			}

			return base.CanUseItem(item, player);
		}

		public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
			Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			if (!YoyoTechCommon.TechActiveFor(player, out YoyoTechType tech))
				return base.Shoot(item, player, source, position, velocity, type, damage, knockback);

			// Only the item's owner spawns (standard tModLoader MP rule).
			if (player.whoAmI != Main.myPlayer && Main.netMode == NetmodeID.MultiplayerClient)
				return false;

			YoyoTechCommon.GetSourceEnhancement(item, out int modId, out int spellId);
			float mult = tech.DamageMultiplier();
			int techDamage = System.Math.Max(1, (int)(damage * mult));

			switch (tech) {
				case YoyoTechType.Throwable:
					SpawnThrowable(player, item, position, velocity, type, techDamage, knockback, modId, spellId);
					PacePlayer(player, 14);
					return false;
				case YoyoTechType.Swing:
					SpawnSwing(player, item, position, velocity, type, techDamage, knockback, modId, spellId);
					PacePlayer(player, 18);
					return false;
				case YoyoTechType.Magical:
					SpawnMagical(player, item, position, velocity, type, techDamage, knockback, modId, spellId);
					PacePlayer(player, 7);
					return false;
				case YoyoTechType.Summon:
					SpawnSummon(player, item, position, velocity, type, techDamage, knockback, modId, spellId);
					PacePlayer(player, 20);
					return false;
			}

			return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
		}

		/// <summary>Shorten the post-use cooldown without mutating the item (MP-safe).</summary>
		private static void PacePlayer(Player player, int ticks)
		{
			if (player.itemTime > ticks)
				player.itemTime = ticks;
			if (player.itemAnimation > ticks)
				player.itemAnimation = ticks;
		}

		private static void SpawnThrowable(Player player, Item item, Vector2 position, Vector2 velocity,
			int origProjType, int damage, float knockback, int modId, int spellId)
		{
			Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
			float speed = MathHelper.Clamp(velocity.Length() * 1.1f, 10f, 18f);
			Vector2 vel = dir * speed;
			int idx = Projectile.NewProjectile(player.GetSource_ItemUse(item), position, vel,
				ModContent.ProjectileType<Projectiles.Tech.ThrowableYoyoProj>(), damage, knockback, player.whoAmI,
				ai0: item.type, ai1: origProjType);
			ApplyTechIds(idx, item, modId, spellId);
			// Heavy triple fan with Glove/Bag: center + two embering sides.
			MaybeGloveSpread(player, item, position, vel, damage, knockback, modId, spellId,
				ModContent.ProjectileType<Projectiles.Tech.ThrowableYoyoProj>(),
				new[] { (-0.2f, 0.7f), (0.2f, 0.7f) });
			SoundEngine.PlaySound(SoundID.Item1, position);
			// Throw flash: iron ring bursting off the hand.
			for (int i = 0; i < 10; i++) {
				Vector2 puff = new Vector2(60f, 0f).RotatedBy(i / 10f * MathHelper.TwoPi);
				Dust d = Dust.NewDustDirect(position - new Vector2(4, 4), 8, 8,
					DustID.Torch, puff.X * 0.06f, puff.Y * 0.06f, 120, default, 1f);
				d.noGravity = true;
			}
		}

		private static void SpawnSwing(Player player, Item item, Vector2 position, Vector2 velocity,
			int origProjType, int damage, float knockback, int modId, int spellId)
		{
			// Aim from player toward cursor (falls back to facing when overlapping).
			Vector2 aim = Main.MouseWorld - player.Center;
			if (aim.LengthSquared() < 8f * 8f)
				aim = new Vector2(player.direction * 60f, 0f);
			float aimRot = aim.ToRotation();
			int idx = Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, Vector2.Zero,
				ModContent.ProjectileType<Projectiles.Tech.SwingYoyoProj>(), damage, knockback, player.whoAmI,
				ai0: item.type, ai1: origProjType, ai2: aimRot);
			ApplyTechIds(idx, item, modId, spellId);
			player.itemRotation = aimRot;
			SoundEngine.PlaySound(SoundID.Item1, position);
		}

		private static void SpawnMagical(Player player, Item item, Vector2 position, Vector2 velocity,
			int origProjType, int damage, float knockback, int modId, int spellId)
		{
			const int manaCost = 4;
			player.statMana = System.Math.Max(0, player.statMana - manaCost);
			player.manaRegenDelay = System.Math.Max(player.manaRegenDelay, 20);
			Vector2 dir = velocity.SafeNormalize(Vector2.UnitX);
			float speed = MathHelper.Clamp(velocity.Length() * 1.25f, 16f, 26f);
			// Slight spread so spam reads as magical, not mechanical.
			dir = dir.RotatedBy(Main.rand.NextFloat(-0.06f, 0.06f));

			// Orbiting mirror guards: 1 per click (2 with a glove), cap 3/6.
			// Never exceed the cap: with one slot free, only one spawns.
			MagicalOrbit.CapStats(player, out int deploy, out int cap);
			int runeType = ModContent.ProjectileType<Projectiles.Tech.MagicalYoyoProj>();
			int owned = YoyoTechCommon.CountOwnedType(player, runeType);
			deploy = System.Math.Min(deploy, System.Math.Max(0, cap - owned));

			int throwDamage = damage;

			for (int k = 0; k < deploy; k++) {
				Vector2 vel = (k == 0 ? dir : dir.RotatedBy(k * 0.15f)) * speed;
				int idx = Projectile.NewProjectile(player.GetSource_ItemUse(item), position, vel,
					runeType, throwDamage, knockback * 0.5f, player.whoAmI,
					ai0: item.type, ai1: origProjType);
				ApplyTechIds(idx, item, modId, spellId);
				if (idx >= 0 && idx < Main.maxProjectiles)
					Main.projectile[idx].netUpdate = true;
			}
			PacePlayer(player, 7);
			SoundEngine.PlaySound(SoundID.Item8, position);
		}

		private static void SpawnSummon(Player player, Item item, Vector2 position, Vector2 velocity,
			int origProjType, int damage, float knockback, int modId, int spellId)
		{
			Vector2 spawn = player.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), -40f);
			int idx = Projectile.NewProjectile(player.GetSource_ItemUse(item), spawn, Vector2.Zero,
				ModContent.ProjectileType<Projectiles.Tech.SummonYoyoMinion>(), damage, knockback, player.whoAmI,
				ai0: item.type, ai1: origProjType);
			ApplyTechIds(idx, item, modId, spellId);
			SoundEngine.PlaySound(SoundID.Item44, position);
		}

		private static void ApplyTechIds(int idx, Item src, int modId, int spellId)
		{
			if (idx < 0 || idx >= Main.maxProjectiles)
				return;
			Projectile p = Main.projectile[idx];
			// OnSpawn already ran during NewProjectile (ids were still -1 there),
			// so apply the enhancement now that the true ids are known.
			if (p.ModProjectile is Projectiles.Tech.TechProjBase tech) {
				tech.Init(src.type, modId, spellId);
				YoyoTechCommon.ApplyEnhancementToSpawn(p, src, modId);
			}
			// Ancient Glove guides EVERY throw: +1 pierce when finite.
			if (p.owner >= 0 && p.owner < Main.maxPlayers) {
				Player owner = Main.player[p.owner];
				if (owner != null && owner.active && p.penetrate != -1
					&& owner.GetModPlayer<Players.MarketPlayer>().AncientGlove)
					p.penetrate++;
			}
			p.netUpdate = true;
		}

		/// <summary>
		/// Yoyo Glove / Bag still matter under tech: side throws fanning off the
		/// main one (angle offset + damage fraction per entry). Capped so glove
		/// + spam never floods the projectile pool.
		/// </summary>
		private static void MaybeGloveSpread(Player player, Item item, Vector2 position, Vector2 velocity,
			int damage, float knockback, int modId, int spellId, int techProjType, (float angle, float mult)[] fans)
		{
			if (!YoyoTechCommon.HasYoyoGloveOrBag(player))
				return;
			int owned = 0;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == player.whoAmI && p.type == techProjType) {
					owned++;
					if (owned >= 6)
						return;
				}
			}
			foreach (var fan in fans) {
				Vector2 vel = velocity.RotatedBy(fan.angle) * 0.95f;
				int idx = Projectile.NewProjectile(player.GetSource_ItemUse(item), position, vel,
					techProjType, System.Math.Max(1, (int)(damage * fan.mult)), knockback, player.whoAmI,
					ai0: item.type, ai1: item.shoot);
				ApplyTechIds(idx, item, modId, spellId);
			}
		}
	}
}
