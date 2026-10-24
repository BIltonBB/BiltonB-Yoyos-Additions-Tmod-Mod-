using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Systems;
using BiltonsYoyoMod.Items.Accessories;

namespace BiltonsYoyoMod.Content.NPCs.Town.Pets
{
	// Billy the Bearing town pet. Normally idle; during boss fights he orbits the
	// player with the most yoyos at 128px and deals contact damage. Bearings
	// install through the custom 5-button panel (Upgrade tab).
	[AutoloadHead]
	public class BillyTheBearing : ModNPC
	{
		// All upgradeable bearing item types (loose + tension).
		private static List<int> upgradeTypes;
		private static bool upgradeTypesCached;

		internal static List<int> UpgradeTypeList()
		{
			if (upgradeTypesCached)
				return upgradeTypes;
			upgradeTypesCached = true;
			upgradeTypes = new List<int> {
				ModContent.ItemType<LooseBearingsAL>(), ModContent.ItemType<LooseBearingsAM>(),
				ModContent.ItemType<LooseBearingsAW>(), ModContent.ItemType<LooseBearingsACH>(),
				ModContent.ItemType<LooseBearingsAHA>(), ModContent.ItemType<LooseBearingsAHE>(),
				ModContent.ItemType<LooseBearingsAMI>(), ModContent.ItemType<TensionBearingAL>(),
				ModContent.ItemType<TensionBearingAD>(), ModContent.ItemType<TensionBearingAP>(),
				ModContent.ItemType<TensionBearingACH>(), ModContent.ItemType<TensionBearingAHPSS>(),
			};
			return upgradeTypes;
		}

		public static int UpgradeCount
		{
			get {
				int count = 0;
				foreach (int type in UpgradeTypeList()) {
					if (YoyoTownSystem.BillyUpgrades.Contains(type) && !YoyoTownSystem.BillyInactive.Contains(type))
						count++;
				}
				return count;
			}
		}

		public static int MaxUpgrades => UpgradeTypeList().Count;
		public static bool HalfUpgrades => UpgradeCount * 2 >= MaxUpgrades;
		public static bool AllUpgrades => UpgradeCount >= MaxUpgrades;

		private int shootTimer;
		private float orbitAngle; // Dedicated field: vanilla town AI owns ai[0].
		private bool protecting; // 1-HP protect mode: invincible until fully healed.
		private int protectHealTimer;

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 25; // 32x600 sheet: 25 copies of the 32x24 head.
			NPCID.Sets.IsTownPet[Type] = true;
			NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Velocity = 1f };
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
		}

		public override void SetDefaults()
		{
			NPC.townNPC = true;
			NPC.friendly = true;
			NPC.width = 30;
			NPC.height = 30;
			NPC.aiStyle = NPCAIStyleID.Passive;
			NPC.damage = 10;
			NPC.defense = 14;
			NPC.lifeMax = 600;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.knockBackResist = 0.3f;
			AIType = NPCID.TownCat;
			AnimationType = NPCID.TownCat;
		}

		// Lethal hits never kill Billy outright: he drops to 1 HP and enters
		// protect mode (invincible + regen) instead. This is what makes him
		// actually protect instead of dying to the first boss swipe.
		public override bool CheckDead()
		{
			if (!protecting) {
				NPC.life = 1;
				NPC.active = true;
				NPC.netUpdate = true;
				return false;
			}
			return true;
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Underground,
				new FlavorTextBestiaryInfoElement("Billy the Bearing. Feed him loose and tension bearings and he will fight beside you when bosses appear."),
			});
		}

		public override bool CanTownNPCSpawn(int numTownNPCs)
		{
			// Giant Bearing defeated + someone has braved the caverns (rescue proxy).
			if (!Content.YoyoGuardianWorld.downedGiantBearing)
				return false;
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (player != null && player.active && !player.dead && player.ZoneRockLayerHeight)
					return true;
			}
			return false;
		}

		public override bool CanGoToStatue(bool toKingStatue) => false;

		// Vanilla 2-button chat is bypassed: right-click opens the 5-button panel.
		public override bool CanChat() => false;

		public override List<string> SetNPCNameList() => new() { "Billy" };

		// Idle flavor: metallic glints.
		public override void DrawEffects(ref Color drawColor)
		{
			if (Main.rand.NextBool(60)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.SilverCoin, 0f, -0.5f, 150, default, 0.6f);
				dust.noGravity = true;
			}
		}

		public override void PostAI()
		{
			int upgrades = UpgradeCount;
			NPC.damage = 10 + upgrades * 4;
			// Protect mode: knocked to 1 HP -> stop helping, go invincible, and
			// weld back together (1 HP every 0.1s) before returning to duty.
			if (!protecting && NPC.life <= 1 && NPC.lifeMax > 1) {
				protecting = true;
				protectHealTimer = 0;
				NPC.dontTakeDamage = true;
				CombatText.NewText(NPC.getRect(), new Color(255, 200, 80), "Billy is down!");
			}
			if (protecting) {
				NPC.noTileCollide = true;
				NPC.noGravity = true;
				NPC.velocity *= 0.9f;
				if (++protectHealTimer >= 6) {
					protectHealTimer = 0;
					NPC.life = System.Math.Min(NPC.lifeMax, NPC.life + 1);
					if (Main.rand.NextBool(3)) {
						Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0f, -1f, 120, default, 0.8f);
						dust.noGravity = true;
					}
				}
				if (NPC.life >= NPC.lifeMax) {
					protecting = false;
					NPC.dontTakeDamage = false;
					NPC.noTileCollide = false;
					NPC.noGravity = false;
					CombatText.NewText(NPC.getRect(), new Color(140, 255, 140), "Billy is patched up!");
				}
				return;
			}
			// Anti-stuck: if Billy ended up embedded inside solid tiles (e.g. phasing
			// switched off while passing through a platform), float up until free.
			// Platforms and actuated tiles never trigger this.
			if (!NPC.noTileCollide && IsEmbedded(NPC.Center)) {
				NPC.noTileCollide = true;
				NPC.noGravity = true;
				NPC.velocity = new Vector2(0f, -3f);
				return;
			}
			// No boss: hold position like a proper town pet instead of
			// trailing the player across the world.
			if (!AnyBossActive()) {
				NPC.velocity *= 0.8f;
				NPC.noTileCollide = false;
				NPC.noGravity = false;
				return;
			}
			// Orbiting a boss: phase through platforms/tiles so the ring never snags.
			NPC.noTileCollide = true;
			NPC.noGravity = true;
			Player target = RichestYoyoPlayer();
			if (target == null)
				return;
			float orbitRadius = 160f;
			float orbitSpeed = 0.03f + upgrades * 0.004f;
			float followSpeed = 6f + upgrades * 0.5f;
			orbitAngle += orbitSpeed;
			Vector2 goal = target.Center + new Vector2(orbitRadius, 0f).RotatedBy(orbitAngle);
			Vector2 toGoal = goal - NPC.Center;
			if (toGoal.Length() > 8f)
				NPC.velocity = toGoal.SafeNormalize(Vector2.Zero) * MathHelper.Min(toGoal.Length() * 0.15f, followSpeed);
			else
				NPC.velocity *= 0.9f;
			// Bodyguard duty: swat weak enemy shots out of the air on contact.
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile shot = Main.projectile[i];
				if (shot == null || !shot.active || !shot.hostile || shot.damage >= 50)
					continue;
				if (Vector2.DistanceSquared(shot.Center, NPC.Center) > 34f * 34f)
					continue;
				for (int d = 0; d < 5; d++) {
					Dust dust = Dust.NewDustDirect(shot.position, shot.width, shot.height, DustID.SilverCoin, 0f, 0f, 120, default, 0.8f);
					dust.noGravity = true;
				}
				if (Main.netMode != NetmodeID.MultiplayerClient)
					shot.Kill();
			}
			// Fire bearing shots at nearby enemies once upgraded.
			if (!HalfUpgrades)
				return;
			shootTimer++;
			int interval = AllUpgrades ? 45 : 70;
			if (shootTimer < interval)
				return;
			shootTimer = 0;
			NPC enemy = NearestEnemy(600f);
			if (enemy == null)
				return;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			Vector2 dir = (enemy.Center - NPC.Center).SafeNormalize(Vector2.Zero);
			int owner = target.whoAmI;
			Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 10f, ModContent.ProjectileType<Projectiles.TownNPC.BTBBearing>(), NPC.damage, 3f, owner);
			if (AllUpgrades)
				Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir.RotatedBy(0.15f) * 13f, ModContent.ProjectileType<Projectiles.TownNPC.BTBUBearing>(), NPC.damage + 10, 4f, owner);
		}

		private static bool IsEmbedded(Vector2 worldPos)
		{
			Point p = worldPos.ToTileCoordinates();
			Tile tile = Framing.GetTileSafely(p.X, p.Y);
			return tile.HasTile && Main.tileSolid[tile.TileType] && !tile.IsActuated && !TileID.Sets.Platforms[tile.TileType];
		}

		private static bool AnyBossActive()
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (npc.active && npc.boss && !npc.friendly)
					return true;
			}
			return false;
		}

		private static Player RichestYoyoPlayer()
		{
			Player best = null;
			int bestCount = -1;
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (player == null || !player.active || player.dead)
					continue;
				int count = YoyoTownShared.CountYoyos(player);
				if (count > bestCount) {
					bestCount = count;
					best = player;
				}
			}
			return bestCount > 0 ? best : null;
		}

		private NPC NearestEnemy(float range)
		{
			NPC best = null;
			float bestDist = range * range;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC npc = Main.npc[i];
				if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.lifeMax <= 5)
					continue;
				float dist = Vector2.DistanceSquared(NPC.Center, npc.Center);
				if (dist < bestDist) {
					bestDist = dist;
					best = npc;
				}
			}
			return best;
		}
	}
}
