using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles.TownNPC;

namespace BiltonsYoyoMod.Content.NPCs.Town
{
	// Non-town night visitor (NOT a town NPC: no house, no happiness, no statue).
	// Wanders near where he arrived, throws gold yoyos at nearby enemies,
	// and vanishes at dawn. Shops through the custom 5-button panel.
	public class SixStringer : ModNPC
	{
		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 25;
			NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Velocity = 1f };
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
		}

		public override void SetDefaults()
		{
			NPC.townNPC = false;
			NPC.friendly = true;
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = -1; // Custom visitor AI below.
			NPC.damage = 0;
			NPC.defense = 20;
			NPC.lifeMax = 300;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.knockBackResist = 0.5f;
			NPC.dontTakeDamage = false; // Damageable, but slips away instead of dying.
			NPC.lifeMax = 2500;
			NPC.GivenName = "Six Stringer";
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
				new FlavorTextBestiaryInfoElement("The Six Stringer deals in parts respectable merchants won't touch. No receipts. No questions."),
			});
		}

		// Vanilla 2-button chat is bypassed: right-click opens the 5-button panel.
		public override bool CanChat() => false;

		public override void AI()
		{
			// Leaves at dawn with a puff.
			if (Main.dayTime) {
				Depart();
				return;
			}

			// ai[1] = attack timer (ai[0]/ai[2] drive VisitorMovement.Wander).
			NPC.TargetClosest(false);
			Player player = Main.player[NPC.target];

			VisitorMovement.ApplyGravity(npc: NPC);
			VisitorMovement.Wander(NPC, player, walkSpeed: 1.4f, stickRange: 120f);
			VisitorMovement.Face(NPC, player);
			AttackNearby();

			// Idle flavor: gold glint + faint warm light.
			Lighting.AddLight(NPC.Center, 0.1f, 0.08f, 0.03f);
			if (Main.rand.NextBool(50)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GoldCoin, 0f, -0.6f, 150, default, 0.7f);
				dust.noGravity = true;
			}
		}

		private void Depart()
		{
			for (int i = 0; i < 12; i++)
				Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Smoke, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 0f), 120, default, 1f);
			NPC.active = false;
		}

		// Beaten merchants don't die: they slip away and return another night.
		public override bool CheckDead()
		{
			CombatText.NewText(NPC.getRect(), new Color(255, 220, 130), "The Six Stringer slips away!");
			Depart();
			return false;
		}

		private void AttackNearby()
		{
			NPC.ai[1]++;
			if (NPC.ai[1] < 120) // Every 2 seconds.
				return;
			NPC.ai[1] = 0;
			NPC target = NearestEnemy(700f);
			if (target == null)
				return;
		Vector2 hand = NPC.Center + new Vector2(NPC.direction * 10f, -6f);
		Vector2 dir = (target.Center - hand).SafeNormalize(Vector2.UnitX);
		// Throw dust + coin sfx on all sides.
		for (int i = 0; i < 6; i++)
			Dust.NewDustDirect(hand - new Vector2(8, 8), 16, 16, DustID.GoldCoin, dir.X * 2f, dir.Y * 2f, 120, default, 0.8f);
		SoundEngine.PlaySound(SoundID.CoinPickup, hand);
		if (Main.netMode == NetmodeID.MultiplayerClient)
			return;
		// Pre-Hardmode: plain Gold throw (weaker, gravity, 3 bounces).
		// Hardmode: Super Gold throw (no gravity, really fast, bounces off tiles, inf pierce).
		// Both: 10s lifetime (600 ticks, set on projectile), coin sfx + gold effect.
		if (Main.hardMode) {
			Vector2 vel = dir * 16f;
			Projectile.NewProjectile(NPC.GetSource_FromAI(), hand, vel, ModContent.ProjectileType<TSuperGoldYoyoProj>(), 55, 4f, Main.myPlayer, ai0: NPC.whoAmI);
		}
		else {
			Vector2 vel = dir * 11f;
			Projectile.NewProjectile(NPC.GetSource_FromAI(), hand, vel, ModContent.ProjectileType<TGoldYoyoProj>(), 25, 4f, Main.myPlayer, ai0: NPC.whoAmI);
		}
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

		public override void FindFrame(int frameHeight)
		{
			bool moving = System.Math.Abs(NPC.velocity.X) > 0.3f && NPC.collideY;
			NPC.frameCounter++;
			if (moving) {
				if (NPC.frameCounter >= 6) {
					NPC.frameCounter = 0;
					NPC.frame.Y += frameHeight;
					if (NPC.frame.Y >= frameHeight * 6)
						NPC.frame.Y = frameHeight;
				}
			}
			else {
				NPC.frame.Y = 0;
				NPC.frameCounter = 0;
			}
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Texture2D tex = TextureAssets.Npc[Type].Value;
			// Bottom-center origin plants the feet on the ground tile.
			Vector2 origin = new(tex.Width / 2f, NPC.frame.Height);
			Vector2 pos = NPC.Bottom - screenPos;
			SpriteEffects fx = NPC.spriteDirection < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
			spriteBatch.Draw(tex, pos, NPC.frame, drawColor, NPC.rotation, origin, NPC.scale, fx, 0f);
			return false;
		}

		#region Dialogue
		internal static readonly string[] Greetings = {
			"Heya, looking for deals?",
			"Psst. Careful. These are illegal!",
			"Don't ask where I got this stuff.",
			"No receipts.",
			"No questions.",
			"If you need something unusual, I might have it.",
			"I've got parts that respectable merchants wouldn't touch.",
			"Come closer. I don't want the guards hearing this.",
		};

		internal static readonly string[] Talk = {
			"What?", "Huh?", "Whatcha say?", "Did ya say something?",
			"I wasn't listening.", "You buying or talking?",
			"These parts definitely came from a totally legitimate supplier.",
			"Stop looking at the guards.",
			"I don't know anything about the illegal yoyo trade.",
			"Why are you asking me so many questions?",
			"I sell perfectly legal merchandise.",
			"...Mostly.",
		};

		internal static readonly string[] Help = {
			"I don't do advice.",
			"Ask someone respectable.",
			"Don't flash your expensive equipment around.",
			"Some things simply aren't sold during the day.",
			"If you need ordinary yoyo equipment, find the Master.",
			"For modifications, find the Tinkerer.",
			"For magic, find the old guy.",
			"And don't tell anyone I sent you.",
		};

		internal static readonly string[] PairWithMasterSixStringer = {
			"Don't tell the Master about the parts I sell.",
			"He doesn't approve of my business.",
			"The Master asks too many questions.",
			"Let's just keep our business between us.",
		};
		#endregion
	}
}
