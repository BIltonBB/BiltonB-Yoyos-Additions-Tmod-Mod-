using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Tech
{
	/// <summary>
	/// Tracks the single active yoyo tech for each player.
	/// Rules (bug-proof):
	/// - Only one tech active at a time (ActiveTech enum).
	/// - Tech requires its item somewhere in inventory; if missing, tech auto-turns OFF with a message.
	/// - Death does NOT force OFF (projectiles despawn naturally), but respawn re-validates inventory.
	/// - Multiplayer: ActiveTech syncs via NetSend/NetReceive + manual packet.
	/// - Original yoyo behaviour preserved when ActiveTech == None.
	/// </summary>
	/// <summary>
	/// Tracks the single active yoyo tech for each player.
	/// Hold-to-use model (never accessory, never consumed):
	/// - Hold a Tech item and use it (either click) to toggle ON/OFF.
	/// - Cycle Tech hotkey rotates inventory-carried techs, yoyo in hand ok.
	/// - Only one tech active at a time; missing item = OFF.
	/// - Summons persist 5 min after tech off/switch; buff icon dismisses all.
	/// - Multiplayer: ActiveTech syncs via manual packet.
	/// - Original yoyo behaviour preserved when ActiveTech == None.
	/// </summary>
	public class YoyoTechPlayer : ModPlayer
	{
		public YoyoTechType ActiveTech = YoyoTechType.None;

		// Last tick's value, for SendClientChanges.
		private YoyoTechType lastSynced = YoyoTechType.None;

		private bool summonBuffWasActive;

		// ---- Magical tech input (local player only) ----
		// One click = one deploy: a fresh left-press arms exactly one spawn
		// attempt; holds and repeat attempts stay silent. Right-click is
		// HOLD-MAINTAINED: while the button is down the ability keeps its own
		// charge alive (started and restarted by the hold itself, never by a
		// press edge), so an expired orbit can never leave the tech dead.
		public bool MagicalPressArmed;
		public ulong MagicalPressTick;
		public bool MagicalRightHeld;
		/// <summary>True after a successful cast in the current right-click hold.
		/// With orbiters alive it is ignored (normal hold-to-repeat still works);
		/// once the ring is empty it allows only one hand-cast, so "hold
		/// right-click with no orbiters" cannot recast forever. Release the
		/// button to arm another hand-cast.</summary>
		public bool MagicalHandCastLatch;
		public int MagicalRestartCd;   // short pause after a cancel before re-charging
		public int MagicalAbilityCooldown; // post-release lockout (minimum 1s)
		public int MagicalCueCd;       // no-mana cue throttle
		private bool mouseLeftWasDown;

		// A click stays armed long enough to survive the item's own post-spawn
		// cadence, but never long enough to fire from a stale press.
		private const ulong PressGraceTicks = 20;

		public bool TechActive => ActiveTech != YoyoTechType.None;

		public override void ResetEffects()
		{
			// ActiveTech persists (not an equip flag). Nothing to reset.
		}

		/// <summary>A fresh left-click is waiting to spawn one orbit yoyo.</summary>
		public bool MagicalPressPending =>
			MagicalPressArmed && Main.GameUpdateCount - MagicalPressTick <= PressGraceTicks;

		public void ClearMagicalPress() => MagicalPressArmed = false;

		public override void PostUpdate()
		{
			if (MagicalRestartCd > 0)
				MagicalRestartCd--;
			if (MagicalAbilityCooldown > 0)
				MagicalAbilityCooldown--;
			if (MagicalCueCd > 0)
				MagicalCueCd--;
			if (Player.dead) {
				MagicalPressArmed = false;
				MagicalRightHeld = false;
				MagicalHandCastLatch = false;
				mouseLeftWasDown = false;
				return;
			}
			ValidateInventoryPresence();
			UpdateSummonBuff();
			if (Player.whoAmI == Main.myPlayer)
				UpdateMagicalInput();
		}

		/// <summary>
		/// Samples raw mouse edges once per tick (PostUpdate runs at the end of
		/// Player.Update, so item use reads last tick's state). Left-click arms a
		/// single orbit spawn; right-click is polled as a HELD state and maintains
		/// the charge for as long as it stays down.
		/// </summary>
		private void UpdateMagicalInput()
		{
			bool left = Main.mouseLeft && !Main.gameMenu && !Main.playerInventory;
			if (left && !mouseLeftWasDown) {
				MagicalPressArmed = true;
				MagicalPressTick = Main.GameUpdateCount;
			}
			mouseLeftWasDown = left;

			bool right = Main.mouseRight && MagicalOrbit.CanMouseAct(Player) && MagicalOrbit.AbilitiesEnabled;
			// Hold-maintained while the ring lives: polled every tick, so a
			// charge/beam can re-form after one ends. Once the ring is empty the
			// hold gets exactly one hand-cast (latch in TryMaintainController),
			// then the button must be released and pressed again. Mana and the
			// restart cooldown are the limiters, not a press edge.
			// No orbit requirement at cast time: with the yoyo in hand the
			// ability can charge from that yoyo's identity, so a right-click
			// never silently does nothing just because the guards timed out.
			if (right && YoyoTechCommon.TechActiveFor(Player, out YoyoTechType tech) && tech == YoyoTechType.Magical)
				MagicalOrbit.TryMaintainController(Player);
			// Only a real button release re-arms the zero-orbit hand-cast.
			// Clearing on !right would let opening/closing the inventory while
			// holding the button sneak in another hand-cast without a new press.
			if (!Main.mouseRight)
				MagicalHandCastLatch = false;
			MagicalRightHeld = right;
		}

		/// <summary>
		/// Summon buff: applied while any minions are out (even with tech off,
		/// during their 5-min linger). Right-click cancels it, and a cancel
		/// dismisses every summon instantly. 30-tick refresh so natural expiry
		/// can never false-trigger a dismiss.
		/// </summary>
		private void UpdateSummonBuff()
		{
			int buffType;
			try { buffType = ModContent.BuffType<Content.Buffs.SummonTechBuff>(); }
			catch { return; }
			bool has = Player.HasBuff(buffType);
			if (summonBuffWasActive && !has) {
				// Player right-clicked the buff off: dismiss everything.
				summonBuffWasActive = false;
				DismissSummons();
				return;
			}
			int owned = YoyoTechCommon.CountOwnedSummons(Player, ModContent.ProjectileType<Projectiles.Tech.SummonYoyoMinion>());
			if (owned > 0) {
				Player.AddBuff(buffType, 30);
				summonBuffWasActive = true;
			}
			else {
				summonBuffWasActive = false;
			}
		}

		/// <summary>Kill every owned summon minion with a puff + message.</summary>
		public void DismissSummons()
		{
			if (Player.whoAmI != Main.myPlayer)
				return;
			int minionType = ModContent.ProjectileType<Projectiles.Tech.SummonYoyoMinion>();
			bool any = false;
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == Player.whoAmI && p.type == minionType) {
					for (int d = 0; d < 8; d++) {
						Dust dust = Dust.NewDustDirect(p.position, p.width, p.height,
							DustID.Smoke, 0f, -1f, 120, default, 0.8f);
						dust.noGravity = true;
					}
					p.Kill();
					any = true;
				}
			}
			if (any && !Main.dedServ) {
				Main.NewText("Summons dismissed.", 200, 200, 200);
				SoundEngine.PlaySound(SoundID.MenuClose, Player.Center);
			}
		}

		/// <summary>
		/// Kill every orbiting guard and the ability controller this player owns.
		/// The Magical tech's flyers are tied to the tech, not to a buff: the
		/// instant the tech goes OFF (or is swapped) they must vanish, instead of
		/// hanging around on their own multi-minute lifetime.
		/// </summary>
		public void DismissMagical()
		{
			// EVERYTHING the Magical tech owns leaves with the tech. This used to
			// kill only the orbit shells and the convergence controller, so a
			// Shield deployable (which literally orbits the owner), a wall, a
			// portal, a brand or a tether could outlive the OFF switch and read
			// as an "orbiter that ignored the key". Every magic-tech projectile
			// is named Magical*, while the Summon/Throwable/Swing techs are not,
			// so a lingering summon from another tech is never collaterally
			// dismissed by this sweep.
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile p = Main.projectile[i];
				if (!p.active || p.owner != Player.whoAmI)
					continue;
				ModProjectile mp = p.ModProjectile;
				if (mp == null || !mp.GetType().Name.StartsWith("Magical", StringComparison.Ordinal))
					continue;
				if (!Main.dedServ) {
					for (int d = 0; d < 6; d++) {
						Dust dust = Dust.NewDustDirect(p.position, p.width, p.height,
							DustID.MagicMirror, 0f, -1f, 120, default, 0.7f);
						dust.noGravity = true;
					}
				}
				p.Kill();
			}
			MagicalPressArmed = false;
			MagicalRightHeld = false;
			MagicalHandCastLatch = false;
			MagicalRestartCd = 8;
		}

		/// <summary>
		/// If the active tech's item is no longer in inventory, go idle (OFF).
		/// Shells already flying finish out; summons keep their 5-min linger.
		/// </summary>
		public void ValidateInventoryPresence()
		{
			if (ActiveTech == YoyoTechType.None)
				return;
			if (HasTechItem(Player, ActiveTech))
				return;
			if (ActiveTech == YoyoTechType.Magical)
				DismissMagical();
			ActiveTech = YoyoTechType.None;
			if (Player.whoAmI == Main.myPlayer && !Main.dedServ) {
				Main.NewText("Yoyo Tech idle: item missing, turned OFF.", 200, 200, 200);
				SoundEngine.PlaySound(SoundID.MenuClose, Player.Center);
			}
		}

		public override void OnRespawn()
		{
			MagicalRestartCd = 0;
			MagicalAbilityCooldown = 0;
			MagicalCueCd = 0;
			MagicalPressArmed = false;
			MagicalHandCastLatch = false;
			ValidateInventoryPresence();
		}

		/// <summary>Turn a tech ON (turning any other OFF), or toggle. Returns the new state.</summary>
		public void SetTech(YoyoTechType tech, bool announce = true)
		{
			YoyoTechType previous = ActiveTech;
			if (tech == YoyoTechType.None) {
				ActiveTech = YoyoTechType.None;
			}
			else if (ActiveTech == tech) {
				// Toggle OFF when re-selecting the same tech.
				ActiveTech = YoyoTechType.None;
			}
			else {
				ActiveTech = tech;
			}
			// Leaving the Magical tech must take its orbiters with it. Without
			// this they kept circling for their full lifetime after the tech was
			// already OFF - the "orbitters still exist" report.
			if (previous == YoyoTechType.Magical && ActiveTech != YoyoTechType.Magical)
				DismissMagical();
			if (!announce)
				return;
			if (Player.whoAmI == Main.myPlayer && !Main.dedServ) {
				if (ActiveTech == YoyoTechType.None)
					Main.NewText("Yoyo Tech: OFF", 200, 200, 200);
				else
					Main.NewText($"Yoyo Tech: {ActiveTech.TechName()} ON", 150, 255, 150);
				SoundEngine.PlaySound(ActiveTech == YoyoTechType.None ? SoundID.MenuClose : SoundID.MenuOpen, Player.Center);
				// Small toggle burst so the state change reads at a glance.
				for (int i = 0; i < 10; i++) {
					Dust d = Dust.NewDustDirect(Player.Center - new Microsoft.Xna.Framework.Vector2(8, 8), 16, 16,
						ActiveTech == YoyoTechType.None ? DustID.Smoke : DustID.MagicMirror, 0f, -1f, 120, default, 0.9f);
					d.noGravity = true;
				}
			}
		}

		/// <summary>
		/// Hotkey cycle: None -&gt; each CARRIED tech in fixed order -&gt; None.
		/// Works while a yoyo is held; inventory techs only, never worn checks.
		/// </summary>
		public void CycleTech()
		{
			if (Player.dead || !Player.active)
				return;
			var carried = new System.Collections.Generic.List<YoyoTechType> {
				YoyoTechType.Throwable, YoyoTechType.Swing, YoyoTechType.Magical, YoyoTechType.Summon,
			};
			carried.RemoveAll(t => !HasTechItem(Player, t));
			if (carried.Count == 0) {
				if (Player.whoAmI == Main.myPlayer && !Main.dedServ) {
					Main.NewText("Carry a Tech item to attune.", 200, 200, 200);
					SoundEngine.PlaySound(SoundID.MenuClose, Player.Center);
				}
				return;
			}
			int cur = carried.IndexOf(ActiveTech);
			YoyoTechType next = cur < 0 ? carried[0]
				: cur + 1 >= carried.Count ? YoyoTechType.None : carried[cur + 1];
			SetTech(next);
		}

		/// <summary>
		/// Dedicated Magical-tech ON/OFF (default V). Toggling it OFF also
		/// dismisses every orbiter instantly - the same path as a swap.
		/// </summary>
		public void ToggleMagicalTech()
		{
			if (Player.dead || !Player.active)
				return;
			if (ActiveTech == YoyoTechType.Magical) {
				SetTech(YoyoTechType.None);
				return;
			}
			if (!HasTechItem(Player, YoyoTechType.Magical)) {
				if (Player.whoAmI == Main.myPlayer && !Main.dedServ) {
					Main.NewText("Carry the Magical Yoyo Tech to attune.", 200, 200, 200);
					SoundEngine.PlaySound(SoundID.MenuClose, Player.Center);
				}
				return;
			}
			SetTech(YoyoTechType.Magical);
		}

		public static bool HasTechItem(Player player, YoyoTechType tech)
		{
			if (player == null || !player.active)
				return false;
			int want = TechItemType(tech);
			if (want <= 0)
				return false;
			for (int i = 0; i < player.inventory.Length; i++) {
				Item it = player.inventory[i];
				if (it != null && !it.IsAir && it.type == want)
					return true;
			}
			return false;
		}

		public static int TechItemType(YoyoTechType tech)
		{
			return tech switch {
				YoyoTechType.Throwable => ModContent.ItemType<global::BiltonsYoyoMod.Items.Tech.ThrowableYoyoTech>(),
				YoyoTechType.Swing => ModContent.ItemType<global::BiltonsYoyoMod.Items.Tech.SwingYoyoTech>(),
				YoyoTechType.Magical => ModContent.ItemType<global::BiltonsYoyoMod.Items.Tech.MagicalYoyoTech>(),
				YoyoTechType.Summon => ModContent.ItemType<global::BiltonsYoyoMod.Items.Tech.SummonYoyoTech>(),
				_ => 0,
			};
		}

		public static bool IsTechItem(int itemType)
		{
			if (itemType <= 0)
				return false;
			return itemType == ModContent.ItemType<global::BiltonsYoyoMod.Items.Tech.ThrowableYoyoTech>()
				|| itemType == ModContent.ItemType<global::BiltonsYoyoMod.Items.Tech.SwingYoyoTech>()
				|| itemType == ModContent.ItemType<global::BiltonsYoyoMod.Items.Tech.MagicalYoyoTech>()
				|| itemType == ModContent.ItemType<global::BiltonsYoyoMod.Items.Tech.SummonYoyoTech>();
		}

		// ---- Multiplayer sync ----
		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)100); // YoyoTech sync id (see BiltonsYoyoMod.HandlePacket).
			packet.Write((byte)Player.whoAmI);
			packet.Write((byte)ActiveTech);
			packet.Send(toWho, fromWho);
		}

		public override void SendClientChanges(ModPlayer clientPlayer)
		{
			var clone = (YoyoTechPlayer)clientPlayer;
			if (clone.ActiveTech != lastSynced || ActiveTech != lastSynced) {
				SyncPlayer(-1, Player.whoAmI, false);
				lastSynced = ActiveTech;
			}
		}

		public override void CopyClientState(ModPlayer targetCopy)
		{
			var clone = (YoyoTechPlayer)targetCopy;
			clone.ActiveTech = ActiveTech;
		}

		public override void SaveData(TagCompound tag)
		{
			tag["yoyoTech"] = (byte)ActiveTech;
		}

		public override void LoadData(TagCompound tag)
		{
			if (tag.ContainsKey("yoyoTech"))
				ActiveTech = (YoyoTechType)tag.GetByte("yoyoTech");
			else
				ActiveTech = YoyoTechType.None;
			// Loaded value re-validated on first PostUpdate (item may be gone).
			lastSynced = (YoyoTechType)255; // Force a sync next tick.
		}

		/// <summary>Server/client packet entry for tech sync.</summary>
		public static void HandlePacket(BinaryReader reader, int whoAmI)
		{
			int playerWho = reader.ReadByte();
			YoyoTechType tech = (YoyoTechType)reader.ReadByte();
			if (playerWho < 0 || playerWho >= Main.maxPlayers)
				return;
			Player p = Main.player[playerWho];
			if (p == null || !p.active)
				return;
			p.GetModPlayer<YoyoTechPlayer>().ActiveTech = tech;
			p.GetModPlayer<YoyoTechPlayer>().lastSynced = tech;
			// Relay from server to other clients.
			if (Main.netMode == NetmodeID.Server) {
				ModPacket packet = ModContent.GetInstance<BiltonsYoyoMod>().GetPacket();
				packet.Write((byte)100);
				packet.Write((byte)playerWho);
				packet.Write((byte)tech);
				packet.Send(-1, whoAmI);
			}
		}
	}
}
