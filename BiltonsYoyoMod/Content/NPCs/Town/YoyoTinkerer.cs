using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles.TownNPC;

namespace BiltonsYoyoMod.Content.NPCs.Town
{
	// Town NPC sprites (tn_yt.png) live next to this file as YoyoTinkerer.png.
	// Spritesheet is 44x1350 = 25 frames of 44x54 (vanilla town-NPC layout).
	// Modification modules install through the custom 5-button panel (Mods tab).
	[AutoloadHead]
	public class YoyoTinkerer : ModNPC
	{
		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 25;
			NPCID.Sets.ExtraFramesCount[Type] = 0;
			NPCID.Sets.AttackFrameCount[Type] = 4;
			NPCID.Sets.DangerDetectRange[Type] = 700;
			NPCID.Sets.AttackType[Type] = 3; // Swinging visual.
			NPCID.Sets.AttackTime[Type] = 90;
			NPCID.Sets.AttackAverageChance[Type] = 30;
			NPCID.Sets.HatOffsetY[Type] = 4;
			NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Velocity = 1f };
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

			NPC.Happiness
				.SetNPCAffection(NPCID.GoblinTinkerer, AffectionLevel.Love)
				.SetNPCAffection(NPCID.Mechanic, AffectionLevel.Love)
				.SetNPCAffection(NPCID.Stylist, AffectionLevel.Like)
				.SetNPCAffection(NPCID.ArmsDealer, AffectionLevel.Dislike)
				.SetNPCAffection(NPCID.Painter, AffectionLevel.Hate)
				.SetBiomeAffection<SnowBiome>(AffectionLevel.Love)
				.SetBiomeAffection<DesertBiome>(AffectionLevel.Dislike)
				.SetBiomeAffection<JungleBiome>(AffectionLevel.Hate);
		}

		public override void SetDefaults()
		{
			NPC.townNPC = true;
			NPC.friendly = true;
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = NPCAIStyleID.Passive;
			NPC.damage = 35;
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
				new FlavorTextBestiaryInfoElement("The Yoyo Tinkerer installs mechanical modifications directly onto yoyos. Gears over magic, always."),
			});
		}

		public override bool CanTownNPCSpawn(int numTownNPCs)
		{
			// Goblin Tinkerer rescued + the player has used a yoyo at least once
			// (approximated by carrying one).
			return NPC.savedGoblin && YoyoTownShared.AnyPlayerHasYoyo();
		}

		public override bool CanGoToStatue(bool toKingStatue) => true;

		// Vanilla 2-button chat is bypassed: right-click opens the 5-button panel.
		public override bool CanChat() => false;

		public override List<string> SetNPCNameList() => new() { "Cog", "Ratchet", "Gizmo", "Socket", "Tinker" };

		// Idle flavor: occasional orange gear sparks + faint warm light.
		public override void DrawEffects(ref Color drawColor)
		{
			Lighting.AddLight(NPC.Center, 0.12f, 0.08f, 0.04f);
			if (Main.rand.NextBool(40)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0f, -0.8f, 120, default, 0.8f);
				dust.noGravity = true;
			}
		}

		public override void TownNPCAttackStrength(ref int damage, ref float knockback)
		{
			damage = Main.hardMode ? 65 : 35;
			knockback = 4f;
		}

		public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown)
		{
			// Close-range swings: every 1s pre-hardmode, every 0.5s in hardmode.
			cooldown = Main.hardMode ? 30 : 60;
			randExtraCooldown = 10;
		}

		public override void TownNPCAttackProj(ref int projType, ref int attackDelay)
		{
			projType = Main.hardMode ? ModContent.ProjectileType<MChlorophyteYoyoProj>() : ModContent.ProjectileType<MWoodenYoyoProj>();
			attackDelay = 1;
		}

		public override void TownNPCAttackSwing(ref int itemWidth, ref int itemHeight)
		{
			itemWidth = 30;
			itemHeight = 30;
		}

		public override void TownNPCAttackProjSpeed(ref float multiplier, ref float gravityCorrection, ref float randomOffset)
		{
			// Point-blank swings: speed barely matters (the projectile positions itself).
			multiplier = 1f;
			gravityCorrection = 0f;
			randomOffset = 0f;
		}

		#region Dialogue
		internal static readonly string[] Greetings = {
			"Look at these cool modifications!",
			"Would you perhaps like a modification?",
			"I can make your yoyo faster, heavier, stranger, or considerably more dangerous.",
			"Every yoyo has room for improvement.",
			"I don't modify ordinary weapons. Only yoyos.",
			"Bring me a yoyo and I'll show you what it can really do.",
			"Mechanical precision is everything.",
			"I've made modifications that should not physically work. They work anyway.",
		};

		internal static readonly string[] Talk = {
			"Rate my modifications!",
			"Hey, ever heard of yoyos? Well yeah, obviously!",
			"Should I say something?",
			"You should try my modifications!",
			"Fix, fix, fix.",
			"I once made a yoyo so fast it became impossible to catch.",
			"That was probably a safety issue.",
			"Precision is more useful than brute force.",
			"I can make almost anything fit inside a yoyo.",
			"Almost.",
			"Some of my inventions were rejected for being completely impractical.",
			"I considered that a compliment.",
		};

		internal static readonly string[] Help = {
			"Ask the Yoyo Master. Not me!",
			"I recommend also asking the Ancient Yo-yoist.",
			"Normal reforges and modifications are separate.",
			"One yoyo can have both a reforge and a modification.",
			"You select modifications through my modification interface.",
			"You don't need to carry modification items around.",
			"Hardmode unlocks five additional modifications.",
			"The mouse remains your primary yoyo control with the thrusters.",
			"WASD only nudges the yoyo.",
			"Some modifications change the physical behavior of the yoyo.",
			"More advanced modifications cost considerably more.",
			"You can reroll the installation price if you don't like it.",
		};

		internal static readonly string[] PairWithMasterTinkerer = {
			"He teaches theory. I make the theory physically possible.",
			"He'd be a lot less interesting without my modifications.",
			"The Master knows how to use a yoyo. I know how to improve one.",
			"We make a pretty good team.",
		};

		internal static readonly string[] PairWithTinkererAncient = {
			"Magic? Just another thing to put inside a yoyo, I suppose.",
			"I still prefer gears.",
			"He calls it magic. I call it unexplained engineering.",
			"I'd like to take one of his spells apart someday.",
		};

		#endregion
	}
}
