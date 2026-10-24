using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace BiltonsYoyoMod.Content.Placeables
{
	// Per-player Rift Anchor selection. Only one active anchor per player;
	// activating another replaces the previous. Stored per player, not globally.
	public class RiftAnchorPlayer : ModPlayer
	{
		public bool HasActive;
		public Point ActiveTile;
		public int SnapCooldown;
		public int BurstMode; // 0 Disrupt (Slow), 1 Concuss (knockback)
		public int Pace; // 0 Steady (1.0x), 1 Swift (1.25x)
		public int HoldMode; // 0 None, 1 Brief (45t), 2 Long (90t) pin at anchor

		public const int MaxRangePixels = 960; // 60 tiles
		public const int CooldownTicks = 1200; // 20s

		public override void ResetEffects()
		{
		}

		public override void PostUpdate()
		{
			if (SnapCooldown > 0)
				SnapCooldown--;
			// Active-anchor beacon so it never gets lost (client fx only).
			if (HasActive && Main.myPlayer == Player.whoAmI && !Main.dedServ && Main.GameUpdateCount % 45 == 0
				&& RiftAnchorHelpers.IsValidAnchor(ActiveTile)) {
				Vector2 c = RiftAnchorHelpers.AnchorCenter(ActiveTile);
				for (int i = 0; i < 6; i++) {
					Dust d = Dust.NewDustDirect(c - new Vector2(4, 4) + new Vector2(Main.rand.NextFloat(-12f, 12f), 0f), 8, 8, DustID.PurpleTorch, 0f, -2f, 120, default, 1f);
					d.noGravity = true;
				}
			}
			// Anchor Blessing combo (Ward + Rift Anchor share one mesh):
			// standing near the anchor toughens you up.
			if (HasActive && RiftAnchorHelpers.IsValidAnchor(ActiveTile) && StationNetwork.MeshHasWard(ActiveTile)) {
				if (Vector2.DistanceSquared(Player.Center, RiftAnchorHelpers.AnchorCenter(ActiveTile)) <= 240f * 240f)
					Player.AddBuff(ModContent.BuffType<global::BiltonsYoyoMod.Content.Buffs.AnchorBlessingBuff>(), 300);
			}
		}

		public void SetActive(Point tile)
		{
			HasActive = true;
			ActiveTile = tile;
		}

		public void ClearActive()
		{
			HasActive = false;
		}

		public override void SaveData(TagCompound tag)
		{
			tag["riftHas"] = HasActive;
			tag["riftX"] = HasActive ? ActiveTile.X : 0;
			tag["riftY"] = HasActive ? ActiveTile.Y : 0;
			tag["riftCd"] = SnapCooldown;
			tag["riftBurst"] = BurstMode;
			tag["riftPace"] = Pace;
			tag["riftHold"] = HoldMode;
		}

		public override void LoadData(TagCompound tag)
		{
			HasActive = tag.ContainsKey("riftHas") && tag.GetBool("riftHas");
			int x = tag.ContainsKey("riftX") ? tag.GetInt("riftX") : 0;
			int y = tag.ContainsKey("riftY") ? tag.GetInt("riftY") : 0;
			ActiveTile = new Point(x, y);
			SnapCooldown = tag.ContainsKey("riftCd") ? tag.GetInt("riftCd") : 0;
			BurstMode = tag.ContainsKey("riftBurst") ? System.Math.Clamp(tag.GetInt("riftBurst"), 0, 1) : 0;
			Pace = tag.ContainsKey("riftPace") ? System.Math.Clamp(tag.GetInt("riftPace"), 0, 1) : 0;
			HoldMode = tag.ContainsKey("riftHold") ? System.Math.Clamp(tag.GetInt("riftHold"), 0, 2) : 1;
		}

		public static int HoldTicks(int holdMode) => holdMode switch {
			2 => 90,
			0 => 0,
			_ => 45,
		};

		public static string HoldName(int holdMode) => holdMode switch {
			2 => "Long",
			0 => "None",
			_ => "Brief",
		};

		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
		{
			ModPacket p = Mod.GetPacket();
			p.Write((byte)PlaceableNet.Msg.RiftSync);
			p.Write((byte)Player.whoAmI);
			p.Write(HasActive);
			p.Write(ActiveTile.X);
			p.Write(ActiveTile.Y);
			p.Write(SnapCooldown);
			p.Write(BurstMode);
			p.Write(Pace);
			p.Write(HoldMode);
			p.Send(toWho, fromWho);
		}

		public void NetReceive(BinaryReader reader)
		{
			HasActive = reader.ReadBoolean();
			ActiveTile = new Point(reader.ReadInt32(), reader.ReadInt32());
			SnapCooldown = reader.ReadInt32();
			try { BurstMode = System.Math.Clamp(reader.ReadInt32(), 0, 1); }
			catch { BurstMode = 0; }
			try { Pace = System.Math.Clamp(reader.ReadInt32(), 0, 1); }
			catch { Pace = 0; }
			try { HoldMode = System.Math.Clamp(reader.ReadInt32(), 0, 2); }
			catch { HoldMode = 1; }
		}

		public void NetSend(BinaryWriter writer)
		{
			writer.Write(HasActive);
			writer.Write(ActiveTile.X);
			writer.Write(ActiveTile.Y);
			writer.Write(SnapCooldown);
			writer.Write(BurstMode);
			writer.Write(Pace);
			writer.Write(HoldMode);
		}
	}

	public static class RiftAnchorHelpers
	{
		public static bool IsValidAnchor(Point tile)
		{
			if (!WorldGen.InWorld(tile.X, tile.Y))
				return false;
			Tile t = Framing.GetTileSafely(tile.X, tile.Y);
			return t.HasTile && t.TileType == ModContent.TileType<Tiles.Placeables.RiftAnchorTile>();
		}

		public static Vector2 AnchorCenter(Point tile)
		{
			// 2x3 anchor: center of the multitile.
			return new Vector2(tile.X * 16f + 16f, tile.Y * 16f + 24f);
		}

		public static bool IsUsable(Player player, Point tile, out string why)
		{
			why = "";
			if (!IsValidAnchor(tile)) {
				why = "Anchor destroyed or invalid.";
				return false;
			}
			Vector2 center = AnchorCenter(tile);
			float dist = Vector2.Distance(player.Center, center);
			// Deep Snap capstone: full-mesh anchors reach 50% further.
			float maxR = RiftAnchorPlayer.MaxRangePixels * (StationNetwork.IsR30(tile) ? 1.5f : 1f);
			if (dist > maxR) {
				why = $"Too far ({dist / 16f:0} tiles, max {maxR / 16f:0}).";
				return false;
			}
			return true;
		}
	}

	// Rift Snap projectile state: controlled redirect toward the anchor,
	// brief hold + AoE on arrival, then return to normal AI. Snap speed
	// scales with the yoyo's own top speed and the launch distance so fast
	// yoyos visibly outrun themselves to the anchor; a short pin at the
	// anchor sells the hit before vanilla AI resumes. Velocities clamped;
	// owner-only; per-NPC cooldown prevents duplicate hits.
	public class RiftSnapGlobalProjectile : GlobalProjectile
	{
		public override bool InstancePerEntity => true;

		private bool snapping;
		private Vector2 snapTarget;
		private int snapTimer;
		private int holdTimer;
		private bool burstDone;
		private bool needsInit = true;
		private float snapSpeed = 16f;
		private readonly Dictionary<int, int> burstCd = new();

		public const int SnapDuration = 120; // fallback travel cap (ticks)
		public const float SnapSpeed = 16f; // absolute minimum snap speed

		public void StartSnap(Vector2 target)
		{
			snapping = true;
			snapTarget = target;
			snapTimer = SnapDuration;
			holdTimer = 0;
			burstDone = false;
			needsInit = true;
			burstCd.Clear();
		}

		public override bool AppliesToEntity(Projectile projectile, bool lateInstantiation)
		{
			return projectile.aiStyle == ProjAIStyleID.Yoyo;
		}

		private static float YoyoTopSpeed(Projectile projectile)
		{
			try {
				if (projectile.type >= ProjectileID.None && projectile.type < ProjectileID.Sets.YoyosTopSpeed.Length) {
					float top = Common.YoyoStaticBoosts.BaseTopSpeed(projectile.type);
					if (top > 1f)
						return top;
				}
			}
			catch { }
			return 11f;
		}

		public override bool PreAI(Projectile projectile)
		{
			// Burst immunity always ticks, even when not snapping, so no
			// stale entries survive the snap that created them.
			if (burstCd.Count > 0) {
				var expired = new List<int>();
				foreach (var kv in burstCd) {
					if (kv.Value - 1 <= 0)
						expired.Add(kv.Key);
					else
						burstCd[kv.Key] = kv.Value - 1;
				}
				foreach (int k in expired)
					burstCd.Remove(k);
			}
			if (!snapping)
				return true;
			if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
				return true;
			// Never leave an orphaned snap behind: if the owner died or
			// stopped channeling, hand back to vanilla AI (which retires it).
			Player owner = Main.player[projectile.owner];
			if (owner == null || !owner.active || owner.dead || !owner.channel) {
				snapping = false;
				holdTimer = 0;
				return true;
			}
			// Sustain flight time while vanilla AI is skipped: without its
			// per-tick refresh the yoyo would starve and pop mid-snap.
			// Covers exactly the remaining snap + hold, nothing banked.
			int need = snapTimer + holdTimer + 30;
			if (projectile.timeLeft < need) {
				projectile.timeLeft = need;
				projectile.netUpdate = true;
			}
			// First tick: tune the flight to this yoyo and this distance.
			// Faster yoyos snap faster (always above their own top speed);
			// far anchors grant more travel time so the snap can arrive.
			if (needsInit) {
				needsInit = false;
				float dist0 = Vector2.Distance(projectile.Center, snapTarget);
				float paceMult = 1f;
				try { paceMult = System.Math.Clamp(owner.GetModPlayer<RiftAnchorPlayer>().Pace, 0, 1) == 1 ? 1.25f : 1f; }
				catch { }
				float byYoyo = YoyoTopSpeed(projectile) * 2f;
				float byDist = dist0 / 30f;
				snapSpeed = System.Math.Clamp(System.Math.Max(byYoyo, byDist) * paceMult, 18f, 32f);
				snapTimer = System.Math.Clamp((int)(dist0 / 8f), 90, 240);
			}
			// Pinned at the anchor: hover, sparkle, then release to vanilla.
			if (holdTimer > 0) {
				holdTimer--;
				projectile.Center = Vector2.Lerp(projectile.Center, snapTarget, 0.5f);
				projectile.velocity *= 0.8f;
				projectile.netUpdate = true;
				if (!Main.dedServ && Main.rand.NextBool(2)) {
					Dust d = Dust.NewDustDirect(projectile.Center - new Vector2(4, 4), 8, 8, DustID.PurpleTorch, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 120, default, 1f);
					d.noGravity = true;
				}
				if (holdTimer <= 0) {
					snapping = false;
					return true;
				}
				return false;
			}
			if (snapTimer-- <= 0) {
				snapping = false;
				return true;
			}
			Vector2 toTarget = snapTarget - projectile.Center;
			float dist = toTarget.Length();
			if (dist < 28f) {
				if (!burstDone) {
					burstDone = true;
					RiftBurst(projectile);
				}
				int hold = 45;
				try { hold = RiftAnchorPlayer.HoldTicks(System.Math.Clamp(owner.GetModPlayer<RiftAnchorPlayer>().HoldMode, 0, 2)); }
				catch { }
				if (hold <= 0) {
					snapping = false;
					return true;
				}
				holdTimer = hold;
				return false;
			}
			// Controlled movement: steer toward anchor at the tuned speed.
			// Does not teleport; respects a sane velocity cap.
			Vector2 want = toTarget.SafeNormalize(Vector2.Zero) * snapSpeed;
			projectile.velocity = Vector2.Lerp(projectile.velocity, want, 0.5f);
			if (projectile.velocity.Length() > snapSpeed)
				projectile.velocity = projectile.velocity.SafeNormalize(Vector2.Zero) * snapSpeed;
			projectile.netUpdate = true;
			// Violet contrail while snapping so the redirect reads at a glance.
			if (!Main.dedServ && Main.rand.NextBool(2)) {
				Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.PurpleTorch, -projectile.velocity.X * 0.15f, -projectile.velocity.Y * 0.15f, 120, default, 1f);
				d.noGravity = true;
			}
			return false; // Skip vanilla yoyo AI while snapping.
		}

		private void RiftBurst(Projectile projectile)
		{
			int mode = 0;
			if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers && Main.player[projectile.owner].active)
				mode = System.Math.Clamp(Main.player[projectile.owner].GetModPlayer<RiftAnchorPlayer>().BurstMode, 0, 1);
			// Meshed anchors hit harder (#12 wider dims) + combo lookup
			// for the arrival effects below (Snap Surge, conduction).
			float meshMult = 1f;
			Point burstAnchor = default;
			bool conduct = false;
			Point conductTile = default;
			ResonatorData conductData = null;
			if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers && Main.player[projectile.owner].active) {
				var rp = Main.player[projectile.owner].GetModPlayer<RiftAnchorPlayer>();
				if (rp != null && rp.HasActive && RiftAnchorHelpers.IsValidAnchor(rp.ActiveTile)) {
					burstAnchor = rp.ActiveTile;
					if (StationNetwork.BestTier(burstAnchor) > 0)
						meshMult = 1.25f;
					conduct = StationNetwork.TryMeshResonator(burstAnchor, out conductTile, out conductData);
				}
			}
			int dustType = mode == 1 ? DustID.Torch : DustID.MagicMirror;
			for (int i = 0; i < (mode == 1 ? 20 : 14); i++) {
				Dust d = Dust.NewDustDirect(projectile.Center - new Vector2(4, 4), 8, 8, dustType, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.1f);
				d.noGravity = true;
			}
			if (!Main.dedServ)
				Lighting.AddLight(projectile.Center, mode == 1 ? 0.6f : 0.3f, 0.2f, mode == 1 ? 0.2f : 0.6f);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item94, projectile.Center);
			if (Main.myPlayer != projectile.owner)
				return;
			const float radius = 110f;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5 || npc.life <= 0)
					continue;
				if (burstCd.ContainsKey(i))
					continue;
				if (Vector2.DistanceSquared(projectile.Center, npc.Center) > radius * radius)
					continue;
				burstCd[i] = 30;
				// Disrupt: 75% + Slow. Concuss: 60% + heavy shove, no slow.
				int dmg = mode == 1
					? System.Math.Max(1, (int)(projectile.damage * 0.6f * meshMult))
					: System.Math.Max(1, (int)(projectile.damage * 0.75f * meshMult));
				NPC.HitInfo hit = new() {
					Damage = dmg,
					Knockback = mode == 1 ? 9f : 4f,
					HitDirection = System.Math.Sign(npc.Center.X - projectile.Center.X),
					Crit = false
				};
				npc.StrikeNPC(hit, false, false);
				if (mode == 0)
					npc.AddBuff(BuffID.Slow, 120);
				// Snap conduction combo (Resonator + Rift share one mesh):
				// arrivals weaken, R30 slows on top.
				if (conduct && conductData != null) {
					int bonus = StationNetwork.ResonatorBonus(conductTile);
					float pct = ResonatorSystem.EffWeakenPct(conductData, bonus);
					int ticks = ResonatorSystem.WeakenTicks(System.Math.Min(conductData.Duration + bonus, ResonatorSystem.MaxDuration));
					var wg = npc.GetGlobalNPC<ResonatorGlobalNPC>();
					wg.ApplyWeaken(pct, ticks);
					if (StationNetwork.IsR30(conductTile))
						npc.AddBuff(BuffID.Slow, ticks);
				}
				npc.netUpdate = true;
			}
			// Snap Surge combo: arrivals leave you surging.
			if (conduct && projectile.owner >= 0 && projectile.owner < Main.maxPlayers && Main.player[projectile.owner].active)
				Main.player[projectile.owner].AddBuff(ModContent.BuffType<global::BiltonsYoyoMod.Content.Buffs.SnapSurgeBuff>(), 480);
		}
	}
}
