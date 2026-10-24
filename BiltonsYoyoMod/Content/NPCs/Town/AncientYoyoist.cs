using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles.TownNPC;

namespace BiltonsYoyoMod.Content.NPCs.Town
{
	// Town NPC sprites (tn_ay.png) live next to this file as AncientYoyoist.png.
	// Spritesheet is 44x1350 = 25 frames of 44x54 (vanilla town-NPC layout).
	// Spells roll through the custom 5-button panel (Spells tab).
	[AutoloadHead]
	public class AncientYoyoist : ModNPC
	{
		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 25;
			NPCID.Sets.ExtraFramesCount[Type] = 0;
			NPCID.Sets.AttackFrameCount[Type] = 4;
			NPCID.Sets.DangerDetectRange[Type] = 700;
			NPCID.Sets.AttackType[Type] = 0;
			NPCID.Sets.AttackTime[Type] = 90;
			NPCID.Sets.AttackAverageChance[Type] = 30;
			NPCID.Sets.HatOffsetY[Type] = 4;
			NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Velocity = 1f };
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

			NPC.Happiness
				.SetNPCAffection(NPCID.Wizard, AffectionLevel.Love)
				.SetNPCAffection(NPCID.WitchDoctor, AffectionLevel.Like)
				.SetNPCAffection(NPCID.Clothier, AffectionLevel.Like)
				.SetNPCAffection(NPCID.TaxCollector, AffectionLevel.Dislike)
				.SetNPCAffection(NPCID.Demolitionist, AffectionLevel.Hate)
				.SetBiomeAffection<ForestBiome>(AffectionLevel.Love)
				.SetBiomeAffection<JungleBiome>(AffectionLevel.Like)
				.SetBiomeAffection<UndergroundBiome>(AffectionLevel.Like)
				.SetBiomeAffection<SnowBiome>(AffectionLevel.Dislike)
				.SetBiomeAffection<OceanBiome>(AffectionLevel.Hate);
		}

		public override void SetDefaults()
		{
			NPC.townNPC = true;
			NPC.friendly = true;
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = NPCAIStyleID.Passive;
			NPC.damage = 25;
			NPC.defense = 20;
			NPC.lifeMax = 300;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.knockBackResist = 0.5f;
			AIType = NPCID.Guide;
			AnimationType = NPCID.Guide;
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
				new FlavorTextBestiaryInfoElement("The Ancient Yo-yoist binds elemental magic into yoyos. His rarest spell is spoken of in whispers."),
			});
		}

		public override bool CanTownNPCSpawn(int numTownNPCs)
		{
			// Skeletron defeated + the player owns at least 3 different yoyos.
			return NPC.downedBoss3 && YoyoTownShared.AnyPlayerHasDistinctYoyos(3);
		}

		public override bool CanGoToStatue(bool toKingStatue) => true;

		// Vanilla 2-button chat is bypassed: right-click opens the 5-button panel.
		public override bool CanChat() => false;

		public override List<string> SetNPCNameList() => new() { "Merlin", "Elder String", "Runes", "Old Coil", "Sage" };

		// Idle flavor: violet rune wisps + strong pulsing light.
		public override void DrawEffects(ref Color drawColor)
		{
			float pulse = 0.25f + 0.12f * (float)System.Math.Sin(Main.GameUpdateCount * 0.05f);
			Lighting.AddLight(NPC.Center, pulse * 0.7f, pulse * 0.4f, pulse);
			if (Main.rand.NextBool(18)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, 0f, -0.6f, 120, default, 0.9f);
				dust.noGravity = true;
			}
		}

		// The Yoyo Cultist's attack, copied fully: a private timer (NOT the
		// vanilla town-attack hooks, which rescale the throw and made it
		// slow) fires the same ring-cast with the same projectiles, damage,
		// speed and timing. Movement/housing stay vanilla Passive.
		private int cultistAttackTimer;

		public override void PostAI()
		{
			AttackLikeCultist();
		}

		private void AttackLikeCultist()
		{
			cultistAttackTimer++;
			if (cultistAttackTimer < 120) // Every 2 seconds.
				return;
			cultistAttackTimer = 0;
			NPC target = NearestEnemy(750f);
			if (target == null)
				return;
			// Spawn from a random position on a ring around the caster.
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

		#region Dialogue
		internal static readonly string[] Greetings = {
			"I can help cast a spell upon your yoyos.",
			"Only yoyos. Nothing else.",
			"Is that a yoyo or a ball?",
			"The art of yoyo sorcery is ancient.",
			"Every yoyo can carry magical potential.",
			"I have spent centuries studying the connection between strings and magic.",
			"Do not underestimate a magically enhanced yoyo.",
			"A proper spell can completely change a weapon.",
		};

		internal static readonly string[] Talk = {
			"Spells are quite fun to cast, aren't they?",
			"I learned these partially from the Wizard and Witch Doctor.",
			"The Forest is a nice place, you know?",
			"Magic and yoyos have more in common than you would expect.",
			"Never cast a spell before checking what it does.",
			"Some enchantments are much rarer than others.",
			"A yoyo is an excellent vessel for elemental magic.",
			"I once attempted to enchant a hammer.",
			"It did not end well.",
			"Fortunately, hammers are replaceable.",
			"Daybroken is not something you should expect to see often.",
			"Ancient yoyo magic is not for impatient people.",
		};

		internal static readonly string[] Help = {
			"The more expensive casting options give you better chances at rare spells.",
			"There are 23 spells.",
			"You can combine a normal reforge and a spell on the same yoyo.",
			"Spells only work on yoyos.",
			"Cheap casting options favor common spells.",
			"Expensive casting options improve your chances of rare spells.",
			"Combined spells are considerably rarer.",
			"Daybroken has an exceptionally tiny chance.",
			"The spell changes what effects your yoyo applies to enemies.",
			"Ask the Yoyo Tinkerer about mechanical modifications.",
			"Spells are selected through my casting interface.",
			"You don't need to carry spell items in your inventory.",
		};

		internal static readonly string[] PairWithMasterAncient = {
			"He teaches the modern interpretation of the yoyo.",
			"His knowledge is respectable.",
			"The Master understands the weapon. I understand what lies behind it.",
			"Our teachings are different, but not incompatible.",
		};

		internal static readonly string[] PairWithTinkererAncient = {
			"He believes every problem can be solved with machinery.",
			"He is occasionally correct.",
			"His machines are impressive for someone so skeptical of magic.",
			"I doubt he could understand a spell by dismantling it.",
		};

		internal static readonly string[] PairWithCultistAncient = {
			"His theories are surprisingly close to the truth.",
			"I would rather not discuss the oldest teachings.",
			"He has read texts that even I have forgotten.",
			"Some of his conclusions are... concerning.",
		};
		#endregion
	}
}
