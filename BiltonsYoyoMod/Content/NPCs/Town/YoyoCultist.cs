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
	// Drifts near where he arrived, hurls spectral yoyos from a ring around
	// himself, and leaves at dawn. Shops through the custom 5-button panel.
	public class YoyoCultist : ModNPC
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
			NPC.dontTakeDamage = false; // Damageable, but dissolves instead of dying.
			NPC.lifeMax = 2500;
			NPC.GivenName = "Yoyo Cultist";
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
				new FlavorTextBestiaryInfoElement("The Yoyo Cultist wandered in following the spinning circle. He sells relics no one else dares to stock."),
			});
		}

		// Vanilla 2-button chat is bypassed: right-click opens the 5-button panel.
		public override bool CanChat() => false;

		public override void AI()
		{
			// Leaves at dawn with a wisp burst.
			if (Main.dayTime) {
				Depart();
				return;
			}

			// ai[1] = attack timer (ai[0]/ai[2] drive VisitorMovement.Wander).
			NPC.TargetClosest(false);
			Player player = Main.player[NPC.target];

			VisitorMovement.ApplyGravity(npc: NPC);
			VisitorMovement.Wander(NPC, player, walkSpeed: 1.1f, stickRange: 160f);
			VisitorMovement.Face(NPC, player);
			AttackNearby();

			// Idle flavor: shadow wisps + dim violet light.
			Lighting.AddLight(NPC.Center, 0.09f, 0.04f, 0.12f);
			if (Main.rand.NextBool(36)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, 0f, -0.8f, 120, default, 0.9f);
				dust.noGravity = true;
			}
		}

		private void Depart()
		{
			for (int i = 0; i < 14; i++)
				Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 0f), 120, default, 1f);
			NPC.active = false;
		}

		// Beaten cultists don't die: they dissolve and return another night.
		public override bool CheckDead()
		{
			CombatText.NewText(NPC.getRect(), new Color(200, 150, 255), "The Yoyo Cultist dissolves!");
			Depart();
			return false;
		}

		private void AttackNearby()
		{
			NPC.ai[1]++;
			if (NPC.ai[1] < 120) // Every 2 seconds.
				return;
			NPC.ai[1] = 0;
			NPC target = NearestEnemy(750f);
			if (target == null)
				return;
			// Spawn from a random position on a ring around the cultist.
			float angle = Main.rand.NextFloat(MathHelper.TwoPi);
			Vector2 spawn = NPC.Center + angle.ToRotationVector2() * 60f;
			Vector2 dir = (target.Center - spawn).SafeNormalize(Vector2.UnitX);
			for (int i = 0; i < 8; i++)
				Dust.NewDustDirect(spawn - new Vector2(8, 8), 16, 16, DustID.Shadowflame, dir.X * 2f, dir.Y * 2f, 120, default, 1f);
			Lighting.AddLight(spawn, 0.5f, 0.25f, 0.55f);
			SoundEngine.PlaySound(SoundID.Item8, spawn);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;
			int damage = Main.hardMode ? 80 : 35;
			float speed = Main.hardMode ? 13f : 10f;
			int proj = Main.hardMode ? ModContent.ProjectileType<TSuperMagicYoyoProj>() : ModContent.ProjectileType<TMagicYoyoProj>();
			Projectile.NewProjectile(NPC.GetSource_FromAI(), spawn, dir * speed, proj, damage, 0f, Main.myPlayer, ai0: NPC.whoAmI);
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
			// Slow hover bob when idle: alternate the first two frames.
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
				NPC.frame.Y = (NPC.frameCounter / 40) % 2 == 0 ? 0 : frameHeight;
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
			"I'm not sure how I got here...",
			"I have stumbled upon this place.",
			"Perhaps the yoyos brought me here.",
			"There is power in the spinning circle.",
			"The ancient civilizations knew this.",
			"Do not underestimate what yoyos can become.",
			"I have seen things that would make ordinary people run.",
			"Yoyos may be far older than anyone realizes.",
		};

		internal static readonly string[] Talk = {
			"I believe that yoyos are the only type of weapon that will be superior in the future.",
			"I'm sure that yoyos came from an ancient civilization that held immense power.",
			"Have you ever wondered why the string always returns?",
			"Perhaps the yoyo is not returning. Perhaps it is being summoned.",
			"The ancient texts mention a spinning weapon.",
			"I cannot prove it, but the similarities are remarkable.",
			"Some things should not be written down.",
			"The Yoyo Master knows the modern teachings.",
			"The Ancient Yo-yoist knows the older teachings.",
			"The Tinkerer only knows how to take things apart.",
			"I prefer not to discuss what I saw underground.",
			"One day, yoyos may surpass every other weapon.",
		};

		internal static readonly string[] Help = {
			"Try shimmering some yoyos. I'm curious.",
			"Don't interrupt us.",
			"Ask the Yoyo Tinkerer.",
			"Ancient yoyo technology may still be hidden underground.",
			"The Master teaches control.",
			"The Tinkerer teaches machinery.",
			"The Ancient Yo-yoist teaches magic.",
			"Some equipment reacts strangely to shimmer.",
			"You should experiment with different yoyo accessories.",
			"Not everything ancient is safe.",
			"Some knowledge is better discovered gradually.",
			"If you find something strange, bring it to the specialists.",
		};

		internal static readonly string[] PairWithAncientCultist = {
			"The Ancient Yo-yoist knows more than he reveals.",
			"Perhaps he remembers the first yoyo.",
			"His knowledge is older than most civilizations.",
			"I wonder how much of the ancient history he actually remembers.",
		};
		#endregion
	}
}
