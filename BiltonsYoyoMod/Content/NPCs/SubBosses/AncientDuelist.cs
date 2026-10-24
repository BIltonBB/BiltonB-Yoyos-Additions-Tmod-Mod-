using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles;

namespace BiltonsYoyoMod.Content.NPCs.SubBosses
{
	// The Ancient Duelist: 120K HP yoyo duelist on a town-NPC spritesheet
	// (25 frames of 44x54). Summoned with the Ancient Wanted Paper.
	// Non-cycling kit, all timers independent:
	//   AD_A1: thrown AD yoyo + orbiting counterweight, 10s out, 3s to rethrow.
	//   AD_A2: slow homing yoyo-mob (100 HP) every 5s (bursts in phase 2).
	//   AD_A3: telegraphed overshoot dashes (3 / 5 in phase 2) dropping trail
	//          yoyos every 5px that fire after 1s. 30s cooldown.
	// Phase 2 (50% HP): constant dust aura + 5 shield yoyos (20K HP each);
	// the boss is invulnerable while any shield lives.
	// ai[0] = phase (0/1), ai[1] = mode (0 hover, 1 telegraph, 2 dashing),
	// ai[2] = mode timer, ai[3] = dashes left.
	// localAI[0..2] = A1/A2/A3 cooldowns.
	public class AncientDuelist : ModNPC
	{
		private const int A1Cooldown = 780; // ~10s yoyo + ~3s rethrow gap.
		private const int A2Cooldown = 300; // 5s homing yoyo.
		private const int A3Cooldown = 1800; // 30s dash sequence.
		private const int TelegraphTime = 45;
		private const int DashTime = 22;
		private const int DashBreather = 18;

		private float dashTrailAcc;

		public override string BossHeadTexture => "BiltonsYoyoMod/Content/NPCs/SubBosses/AncientDuelist_Head_Boss";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 25;
			NPCID.Sets.MPAllowedEnemies[NPC.type] = true;
			NPCID.Sets.BossBestiaryPriority.Add(NPC.type);
		}

		public override void SetDefaults()
		{
			NPC.width = 30;
			NPC.height = 46;
			NPC.aiStyle = -1;
			NPC.damage = 70;
			NPC.defense = 22;
			NPC.lifeMax = 80000;
			NPC.life = NPC.lifeMax;
			NPC.knockBackResist = 0f;
			NPC.boss = true;
			NPC.noGravity = true;
			NPC.noTileCollide = true; // Dashes and hovers phase through platforms.
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath14;
			NPC.value = Item.buyPrice(gold: 8);
			NPC.netAlways = true;
		}

		public override void AI()
		{
			NPC.TargetClosest();
			if (NPC.target < 0 || NPC.target >= Main.maxPlayers) {
				NPC.EncourageDespawn(60);
				return;
			}
			Player target = Main.player[NPC.target];
			if (target == null || !target.active || target.dead) {
				NPC.EncourageDespawn(60);
				return;
			}

			bool phase2 = NPC.ai[0] == 1f;
			float hpPercent = (float)NPC.life / NPC.lifeMax;

			// Phase transition.
			if (!phase2 && hpPercent <= 0.5f) {
				NPC.ai[0] = 1f;
				phase2 = true;
				NPC.netUpdate = true;
				CombatText.NewText(NPC.getRect(), new Color(200, 120, 255), "The Duelist calls its guard!");
				SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
				for (int i = 0; i < 40; i++) {
					Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.4f);
					dust.noGravity = true;
				}
				if (Main.netMode != NetmodeID.MultiplayerClient) {
					for (int s = 0; s < 5; s++) {
						float angle = s / 5f * MathHelper.TwoPi;
						Vector2 pos = NPC.Center + angle.ToRotationVector2() * 130f;
						int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)pos.X, (int)pos.Y, ModContent.NPCType<ADShieldYoyo>());
						if (idx >= 0)
							Main.npc[idx].ai[0] = NPC.whoAmI;
					}
				}
			}

			// Shield guard: invulnerable while any shield yoyo lives.
			bool shieldsAlive = false;
			if (phase2) {
				for (int i = 0; i < Main.maxNPCs; i++) {
					NPC npc = Main.npc[i];
					if (npc.active && npc.type == ModContent.NPCType<ADShieldYoyo>()) {
						shieldsAlive = true;
						break;
					}
				}
			}
			NPC.dontTakeDamage = shieldsAlive;

			NPC.spriteDirection = target.Center.X < NPC.Center.X ? -1 : 1;

			int mode = (int)NPC.ai[1];
			if (mode == 1)
				RunTelegraph(target, phase2);
			else if (mode == 2)
				RunDashSequence(target, phase2);
			else
				RunHoverCombat(target, phase2);
		}

		private void RunHoverCombat(Player target, bool phase2)
		{
			// Drift to a dueling ring ~260px out.
			Vector2 toTarget = target.Center - NPC.Center;
			float dist = toTarget.Length();
			Vector2 goal = target.Center - toTarget.SafeNormalize(Vector2.UnitX) * 260f + new Vector2(0, -60f);
			Vector2 toGoal = goal - NPC.Center;
			float speed = phase2 ? 6f : 4f;
			NPC.velocity = Vector2.Lerp(NPC.velocity, toGoal.SafeNormalize(Vector2.Zero) * MathHelper.Min(speed, toGoal.Length() * 0.05f), 0.08f);

			NPC.localAI[0]++;
			NPC.localAI[1]++;
			NPC.localAI[2]++;

			if (NPC.localAI[0] >= A1Cooldown)
				TryThrowYoyo(target, phase2);
			if (NPC.localAI[1] >= A2Cooldown)
				TrySpawnDart(target, phase2);
			if (NPC.localAI[2] >= A3Cooldown) {
				NPC.ai[1] = 1f; // Telegraph.
				NPC.ai[2] = 0f;
				NPC.localAI[2] = 0f;
				NPC.netUpdate = true;
				SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
			}
		}

		private void TryThrowYoyo(Player target, bool phase2)
		{
			// One yoyo at a time: 10s out, ~3s gap handled by the cooldown.
			for (int i = 0; i < Main.maxProjectiles; i++) {
				Projectile proj = Main.projectile[i];
				if (proj.active && (proj.type == ModContent.ProjectileType<ADYoyoProj>() || proj.type == ModContent.ProjectileType<ADYoyoProjFast>()) && (int)proj.ai[0] == NPC.whoAmI) {
					NPC.localAI[0] = A1Cooldown - 60; // Still out: check again in a second.
					return;
				}
			}
			NPC.localAI[0] = 0f;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			Vector2 dir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
			int yoyoType = phase2 ? ModContent.ProjectileType<ADYoyoProjFast>() : ModContent.ProjectileType<ADYoyoProj>();
			int damage = phase2 ? 70 : 50;
			float speed = phase2 ? 13f : 10f;
			int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * speed, yoyoType, damage, 4f, Main.myPlayer, ai0: NPC.whoAmI);
			if (p >= 0) {
				Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
					ModContent.ProjectileType<ADCounterweightProj>(), phase2 ? 45 : 30, 3f, Main.myPlayer, ai0: p);
			}
			SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
			NPC.netUpdate = true;
		}

		private void TrySpawnDart(Player target, bool phase2)
		{
			int darts = 0;
			for (int i = 0; i < Main.maxNPCs; i++) {
				if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<ADDartYoyo>())
					darts++;
			}
			NPC.localAI[1] = 0f;
			if (darts >= 6)
				return;
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			Vector2 pos = NPC.Center + new Vector2(Main.rand.NextFloat(-60f, 60f), -50f);
			int idx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)pos.X, (int)pos.Y, ModContent.NPCType<ADDartYoyo>());
			if (idx >= 0) {
				Main.npc[idx].ai[2] = phase2 ? 1f : 0f; // Phase-2 darts burst unpredictably.
				Main.npc[idx].netUpdate = true;
			}
			SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
		}

		private void RunTelegraph(Player target, bool phase2)
		{
			NPC.velocity *= 0.9f;
			NPC.ai[2]++;
			// Aim preview: dust streams toward the player's current spot.
			Vector2 aimDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
			for (int i = 0; i < 2; i++) {
				Vector2 at = NPC.Center + aimDir * Main.rand.NextFloat(30f, 160f) + Main.rand.NextVector2Circular(24f, 24f);
				Dust dust = Dust.NewDustDirect(at - new Vector2(4, 4), 8, 8, phase2 ? DustID.PurpleTorch : DustID.Torch, -aimDir.X * 3f, -aimDir.Y * 3f, 120, default, 1.1f);
				dust.noGravity = true;
			}
			if (Main.rand.NextBool(3)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0f, -1f, 120, default, 1.1f);
				dust.noGravity = true;
			}
			if (NPC.ai[2] >= TelegraphTime) {
				NPC.ai[1] = 2f; // Dashing.
				NPC.ai[2] = 0f;
				NPC.ai[3] = phase2 ? 5f : 3f;
				dashTrailAcc = 0f;
				NPC.netUpdate = true;
			}
		}

		private void RunDashSequence(Player target, bool phase2)
		{
			NPC.ai[2]++;
			int cycle = (int)NPC.ai[2] % (DashTime + DashBreather);
			if (cycle == 0 && NPC.ai[2] > 0) {
				// Dash finished: consume one, brief hover, or end the sequence.
				NPC.ai[3]--;
				NPC.velocity *= 0.4f;
				if (NPC.ai[3] <= 0) {
					NPC.ai[1] = 0f;
					NPC.ai[2] = 0f;
					NPC.netUpdate = true;
					return;
				}
			}
			if (cycle == 1 || (NPC.ai[2] == 1f)) {
				// Launch this dash slightly ahead of the player: it overshoots.
				Vector2 aim = target.Center + target.velocity * 12f;
				NPC.velocity = (aim - NPC.Center).SafeNormalize(Vector2.Zero) * 15f;
				SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
				dashTrailAcc = 0f;
				NPC.netUpdate = true;
			}
			if (cycle < DashTime) {
				// Drop a trail yoyo every 5px of dash travel.
				dashTrailAcc += NPC.velocity.Length();
				// Fire streaks + hot light so the dash reads at full speed.
				Lighting.AddLight(NPC.Center, 0.8f, 0.35f, 0.15f);
				for (int i = 0; i < 3; i++) {
					Dust streak = Dust.NewDustDirect(NPC.Center - new Vector2(8, 8), 16, 16,
						phase2 ? DustID.PurpleTorch : DustID.Torch,
						-NPC.velocity.X * 0.25f + Main.rand.NextFloat(-1f, 1f),
						-NPC.velocity.Y * 0.25f + Main.rand.NextFloat(-1f, 1f), 100, default, 1.4f);
					streak.noGravity = true;
				}
				while (dashTrailAcc >= 5f) {
					dashTrailAcc -= 5f;
					for (int i = 0; i < 3; i++) {
						Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0f, 0f, 120, default, 1f);
						dust.noGravity = true;
					}
					if (Main.netMode != NetmodeID.MultiplayerClient) {
						int t = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
							ModContent.ProjectileType<ADTrailYoyoProj>(), phase2 ? 50 : 35, 2f, Main.myPlayer, ai0: 0f, ai1: phase2 ? 12f : 9f);
						if (t >= 0)
							Main.projectile[t].netUpdate = true;
					}
				}
			}
			else {
				NPC.velocity *= 0.92f;
			}
		}

		public override void FindFrame(int frameHeight)
		{
			NPC.frameCounter++;
			int mode = (int)NPC.ai[1];
			if (mode != 0) {
				// Attack frames while telegraphing or dashing.
				if (NPC.frameCounter >= 8) {
					NPC.frameCounter = 0;
					int f = NPC.frame.Y / frameHeight;
					f = f < 18 || f > 21 ? 18 : f + 1;
					if (f > 21)
						f = 18;
					NPC.frame.Y = f * frameHeight;
				}
				return;
			}
			bool moving = System.Math.Abs(NPC.velocity.X) > 1f;
			if (moving) {
				if (NPC.frameCounter >= 6) {
					NPC.frameCounter = 0;
					int f = NPC.frame.Y / frameHeight + 1;
					if (f < 1 || f > 6)
						f = 1;
					NPC.frame.Y = f * frameHeight;
				}
			}
			else {
				NPC.frame.Y = 0;
				NPC.frameCounter = 0;
			}
		}

		public override void DrawEffects(ref Color drawColor)
		{
			// Phase 2 aura: constant dust so it reads as a real phase change.
			if (NPC.ai[0] == 1f) {
				Lighting.AddLight(NPC.Center, 0.25f, 0.1f, 0.35f);
				if (Main.rand.NextBool(3)) {
					Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, 0f, -1f, 120, default, 1f);
					dust.noGravity = true;
				}
			}
			else if (Main.rand.NextBool(20)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0f, -0.5f, 150, default, 0.8f);
				dust.noGravity = true;
			}
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int i = 0; i < 5; i++) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, hit.HitDirection * 2f, -2f, 120, default, 1f);
				dust.noGravity = true;
			}
			if (NPC.life <= 0) {
				for (int i = 0; i < 40; i++) {
					Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Smoke, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 2f), 120, default, 1.5f);
					dust.noGravity = false;
				}
			}
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			// Ancient Yoyo + Yoyo Wisdom now drop only via the kill table
			// (see BossChecklistSupport); scraps stay on the classic rule too.
			npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.AncientScraps>(), 1, 4, 8));
		}
	}
}
