using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles.TownNPC;
using BiltonsYoyoMod.Players;

namespace BiltonsYoyoMod.Content.NPCs.Town
{
	// Town NPC sprites (tn_ym.png) live next to this file as YoyoMaster.png.
	// Spritesheet is 44x1350 = 25 frames of 44x54 (vanilla town-NPC layout).
	// Interaction runs through the custom 5-button panel (right-click the NPC).
	[AutoloadHead]
	public class YoyoMaster : ModNPC
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
				.SetNPCAffection(NPCID.DD2Bartender, AffectionLevel.Love)
				.SetNPCAffection(ModContent.NPCType<YoyoTinkerer>(), AffectionLevel.Love)
				.SetNPCAffection(NPCID.GoblinTinkerer, AffectionLevel.Like)
				.SetNPCAffection(NPCID.DyeTrader, AffectionLevel.Like)
				.SetNPCAffection(NPCID.Angler, AffectionLevel.Dislike)
				.SetNPCAffection(NPCID.TaxCollector, AffectionLevel.Hate)
				.SetNPCAffection(NPCID.Stylist, AffectionLevel.Hate)
				.SetNPCAffection(NPCID.Clothier, AffectionLevel.Hate)
				.SetBiomeAffection<ForestBiome>(AffectionLevel.Love)
				.SetBiomeAffection<SnowBiome>(AffectionLevel.Like)
				.SetBiomeAffection<JungleBiome>(AffectionLevel.Like)
				.SetBiomeAffection<OceanBiome>(AffectionLevel.Dislike);
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
				new FlavorTextBestiaryInfoElement("The Yoyo Master teaches control. He sells strings, cases, trick books and tier emblems."),
			});
		}

		public override bool CanTownNPCSpawn(int numTownNPCs)
		{
			// Has obtained at least one yoyo + Eye of Cthulhu defeated.
			return NPC.downedBoss1 && YoyoTownShared.AnyPlayerHasYoyo();
		}

		public override bool CanGoToStatue(bool toKingStatue) => true;

		// Vanilla 2-button chat is bypassed: right-click opens the 5-button panel.
		public override bool CanChat() => false;

		public override List<string> SetNPCNameList() => new() { "Bilton", "Yoyo", "Master Spin", "Loop", "Sleeper" };

		// Idle flavor: faint string-colored sparkles drift off his hands.
		public override void DrawEffects(ref Color drawColor)
		{
			Lighting.AddLight(NPC.Center, 0.08f, 0.1f, 0.12f);
			if (Main.rand.NextBool(28)) {
				Dust dust = Dust.NewDustDirect(NPC.position + new Vector2(0, 8), NPC.width, NPC.height - 8, DustID.SilverCoin, 0f, -1.2f, 150, default, 0.7f);
				dust.noGravity = true;
				dust.velocity.X *= 0.3f;
			}
		}

		// Yellow "?" marker while the current help stage is still unseen.
		public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Player player = Main.LocalPlayer;
			if (player == null || !player.active)
				return;
			int stage = GetStage();
			if (player.GetModPlayer<YoyoGuidePlayer>().StageSeen(stage))
				return;
			float bob = (float)System.Math.Sin(Main.GameUpdateCount * 0.08f) * 3f;
			Vector2 pos = NPC.Top - screenPos + new Vector2(0, -18 + bob);
			Terraria.Utils.DrawBorderString(spriteBatch, "?", pos, Color.Yellow, 1.1f);
		}

		// Progression stage used by Specific Help. Only the current stage shows.
		public static int GetStage()
		{
			if (NPC.downedMoonlord) return 11;
			if (NPC.downedAncientCultist) return 10;
			if (NPC.downedGolemBoss) return 9;
			if (NPC.downedPlantBoss) return 8;
			int mechs = 0;
			if (NPC.downedMechBoss1) mechs++;
			if (NPC.downedMechBoss2) mechs++;
			if (NPC.downedMechBoss3) mechs++;
			if (mechs >= 2) return 7;
			if (Main.hardMode) return 6;
			if (NPC.downedBoss3) return 5;
			if (NPC.downedQueenBee) return 4;
			if (NPC.downedBoss2) return 3;
			if (NPC.downedBoss1) return 2;
			if (NPC.downedSlimeKing) return 1;
			return 0;
		}

		internal static string[] SpecificForStage(int stage)
		{
			return stage switch {
				0 => new[] {
					"You should try fighting the cowboys through the old wanted paper in the desert.",
					"Old wanted papers are found in underground desert chests or through crafting!",
				},
				1 => new[] {
					"You can get your first wild strings by crafting!",
					"Yoyo cases are pretty cool, they can be obtained through crafting!",
					"Keep an eye out at night for the Six Stringer!",
				},
				2 => new[] {
					"Yoyo tricks are available, I'm sure.",
					"Fingerloops, yoyo axles, loose bearings and more!",
					"Take your yoyo case to an upgrade!",
				},
				3 => new[] { "Not much to say, just upgrade!" },
				4 => new[] {
					"You can upgrade more of your old stuff!",
					"To remind you, bosses have a 25% chance to drop their own yoyos!",
				},
				5 => new[] {
					"Try getting string cutters! They need a keybind too.",
					"Tension bearings are available!",
					"Yoyo Tinkerer should be available now!",
					"Giant bearing can spawn manually or by killing 100 Wild bearings!",
				},
				6 => new[] {
					"Much more is able to be upgraded!",
					"What about the yoyo duplicators?",
					"Try shimmering some of your accessories. Some of them are shimmerable!",
					"Ancient Yo-yoist could move in now!",
				},
				7 => new[] { "Keep upgrading!" },
				8 => new[] {
					"The yoyo cultist should be able to occasionally come now!",
					"Second upgrade for yoyo duplicator!",
				},
				9 => new[] {
					"You can use the trial of guardians to fight the Yoyo Guardian boss!",
					"Yoyo guardian boss is a trial, so be prepared to fight it underground!",
					"Fighting the guardian in the underworld isn't recommended.",
					"The Ancient duelist can be fought now!",
				},
				10 => new[] { "Well, not too much new here, but the upgrades are nice aren't they?" },
				_ => new[] { "Nice job!", "There's nothing else for me to help you with." },
			};
		}

		public override void TownNPCAttackStrength(ref int damage, ref float knockback)
		{
			damage = Main.hardMode ? 90 : 25;
			knockback = 4f;
		}

		public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown)
		{
			cooldown = Main.hardMode ? 120 : 180; // 2s / 3s between throws.
			randExtraCooldown = 20;
		}

		public override void TownNPCAttackProj(ref int projType, ref int attackDelay)
		{
			projType = Main.hardMode ? ModContent.ProjectileType<NPCAdamantiteYoyoProj>() : ModContent.ProjectileType<NPCWoodenYoyoProj>();
			attackDelay = 1;
		}

		public override void TownNPCAttackProjSpeed(ref float multiplier, ref float gravityCorrection, ref float randomOffset)
		{
			// Confident aimed throws: faster in Hardmode, no lob, no spread.
			multiplier = Main.hardMode ? 1.5f : 1.2f;
			gravityCorrection = 0f;
			randomOffset = 0f;
		}

		#region Dialogue
		internal static readonly string[] Greetings = {
			"I'm here to guide you!",
			"You can ask me for help.",
			"A good yoyo is only half the battle. The other half is knowing how to use it.",
			"Every yoyo has a purpose. You just need to find it.",
			"Training with a yoyo is harder than it looks.",
			"I've spent years studying yoyo techniques.",
			"A master never stops learning.",
			"Need advice, equipment, or a little yoyo wisdom?",
		};

		internal static readonly string[] Talk = {
			"I'm here to guide you!",
			"A yoyo isn't just a toy. In the right hands, it is a weapon.",
			"I've seen terrible yoyos used brilliantly, and amazing yoyos used terribly.",
			"The trick to yoyo combat is knowing when to pull back.",
			"Keep practicing. Precision beats wild swinging.",
			"Your equipment matters, but technique matters too.",
			"There are tricks that even I haven't mastered yet.",
			"Maybe one day you'll teach me something.",
			"Have you noticed how differently every yoyo moves?",
			"If you're serious about yoyos, talk to the other specialists too.",
		};

		internal static readonly string[] GeneralPreHardmode = {
			"Start with a simple yoyo and learn how long you can keep it out.",
			"White String gives your yoyo more reach.",
			"Counterweights can improve your yoyo attacks.",
			"Try fighting enemies from different distances.",
			"Don't panic when an enemy gets close. Your yoyo can still defend you.",
			"A yoyo is especially good at controlling space.",
			"Try different yoyos before deciding which one you prefer.",
			"Tier Emblems represent different levels of yoyo mastery.",
			"The Goblin Tinkerer can handle ordinary reforging.",
			"The other specialists can teach you things I cannot.",
		};

		internal static readonly string[] GeneralHardmode = {
			"Hardmode demands better control and better equipment.",
			"Your yoyo accessories become much more important now.",
			"Learn enemy attack patterns instead of constantly attacking.",
			"Hardmode yoyos can reach impressive distances.",
			"Try combining yoyo accessories with your reforges.",
			"The Yoyo Tinkerer can install mechanical modifications.",
			"The Ancient Yo-yoist can cast spells onto yoyos.",
			"Your equipment should complement the way you fight.",
			"Some advanced yoyo techniques require patience.",
			"There is no single perfect yoyo setup.",
		};

		internal static readonly string[] PairWithTinkererMaster = {
			"He modifies my equipment so I can focus on teaching.",
			"His inventions are strange, but useful.",
			"I don't understand half of what he builds.",
			"At least he knows his way around a yoyo.",
		};

		internal static readonly string[] PairWithAncientMaster = {
			"His methods are ancient, but surprisingly effective.",
			"Magic isn't my specialty.",
			"I prefer training without spells.",
			"Still, I can respect ancient knowledge.",
		};

		internal static readonly string[] PairWithSixStringerMaster = {
			"I don't trust that man.",
			"His merchandise is questionable at best.",
			"I wonder where he gets those items.",
			"Keep an eye on your wallet around him.",
		};
		#endregion
	}
}
