using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.NPCs.Critters
{
	// Catchable with a Bug Net (critter). Placeholder textures are copies of
	// existing sprites until final art lands next to each .cs file.

	public abstract class YoyoCritterBase : ModNPC
	{
		public virtual int CritterItemType => -1;

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
			NPCID.Sets.CountsAsCritter[Type] = true;
		}

		public override void SetDefaults()
		{
			NPC.width = 20;
			NPC.height = 20;
			NPC.aiStyle = NPCAIStyleID.Passive; // Passive critter wander.
			AIType = NPCID.Bunny;
			NPC.damage = 0;
			NPC.defense = 0;
			NPC.lifeMax = 5;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.npcSlots = 0f;
			if (CritterItemType >= 0)
				NPC.catchItem = (short)CritterItemType;
		}
	}

	// Gold Bearing: caverns, rare. Aggressive hopper, drops ~1 gold.
	public class GoldBearingCritter : YoyoCritterBase
	{
		public override int CritterItemType => ModContent.ItemType<GoldBearingItem>();

		public override void SetDefaults()
		{
			base.SetDefaults();
			NPC.damage = 20;
			NPC.lifeMax = 40;
			NPC.value = Item.buyPrice(gold: 1);
			NPC.rarity = 2; // Shows on the Lifeform Analyzer.
		}

		public override void AI()
		{
			base.AI();
			NPC.rotation += NPC.velocity.X * 0.05f;
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
		{
			if (spawnInfo.PlayerInTown || !spawnInfo.Player.ZoneRockLayerHeight)
				return 0f;
			// 10x more common during the Bearing Party.
			if (Content.Systems.BearingPartyEvent.Active)
				return 0.2f;
			return 0.02f;
		}
	}

	// Golden Strings: desert, rare. Faster than loose strings, drops ~1 gold.
	public class GoldenStringsCritter : YoyoCritterBase
	{
		public override int CritterItemType => ModContent.ItemType<GoldenStringsItem>();

		public override void SetDefaults()
		{
			base.SetDefaults();
			NPC.damage = 15;
			NPC.lifeMax = 30;
			NPC.value = Item.buyPrice(gold: 1);
			NPC.rarity = 1; // Shows on the Lifeform Analyzer.
		}

		public override void AI()
		{
			// Constant runner: one direction at high speed, flips on walls,
			// hops over single blocks. Never uses the passive wander.
			if (NPC.localAI[0] == 0f)
				NPC.localAI[0] = Main.rand.NextBool() ? 1f : -1f;
			float dir = NPC.localAI[0] >= 0f ? 1f : -1f;
			NPC.velocity.Y += 0.35f;
			if (NPC.velocity.Y > 10f)
				NPC.velocity.Y = 10f;
			NPC.velocity.X = dir * 6.5f;
			if (NPC.collideX) {
				NPC.localAI[0] = -NPC.localAI[0];
				NPC.velocity.X = -dir * 6.5f;
				NPC.velocity.Y = -6f;
			}
			else if (NPC.collideY && NPC.velocity.Y == 0f && Main.rand.NextBool(20)) {
				NPC.velocity.Y = -7f; // Bunny-hop over gaps.
			}
			NPC.rotation += 0.35f;
			NPC.spriteDirection = dir >= 0f ? 1 : -1;
			Lighting.AddLight(NPC.Center, 0.25f, 0.2f, 0.05f);
			if (Main.rand.NextBool(4)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GoldCoin, 0f, 0f, 120, default, 0.7f);
				dust.noGravity = true;
			}
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
		{
			if (spawnInfo.PlayerInTown)
				return 0f;
			// Surface only: never below the dirt layer.
			if (spawnInfo.Player.ZoneRockLayerHeight || !spawnInfo.Player.ZoneOverworldHeight)
				return 0f;
			// Common in the desert, rare anywhere else on the surface.
			if (spawnInfo.Player.ZoneDesert)
				return 0.06f;
			return 0.005f;
		}
	}

	// Gem Bearing: caverns, rare. Glows with dust particles.
	public class GemBearingCritter : YoyoCritterBase
	{
		public override int CritterItemType => ModContent.ItemType<GemBearingItem>();

		public override void SetDefaults()
		{
			base.SetDefaults();
			NPC.damage = 20;
			NPC.lifeMax = 40;
			NPC.color = new Color(150, 220, 255);
			NPC.value = Item.buyPrice(gold: 2);
			NPC.rarity = 2; // Shows on the Lifeform Analyzer.
		}

		public override void AI()
		{
			base.AI();
			NPC.rotation += NPC.velocity.X * 0.05f;
			Lighting.AddLight(NPC.Center, 0.3f, 0.5f, 0.7f);
			if (Main.rand.NextBool(8))
				Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GemSapphire, 0f, 0f, 100, default, 0.8f);
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
		{
			if (spawnInfo.PlayerInTown || !spawnInfo.Player.ZoneRockLayerHeight)
				return 0f;
			// 10x more common during the Bearing Party.
			if (Content.Systems.BearingPartyEvent.Active)
				return 0.15f;
			return 0.015f;
		}
	}

	// All Knowing Worm: anywhere, 0.0001%. Sells for 5 platinum.
	public class AllKnowingWormCritter : YoyoCritterBase
	{
		public override int CritterItemType => ModContent.ItemType<AllKnowingWormItem>();

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 2; // 14x12 sheet: 2 frames of 14x6.
			NPCID.Sets.CountsAsCritter[Type] = true;
		}

		public override void FindFrame(int frameHeight)
		{
			// 10 frames a second while moving, idle frame when still.
			if (NPC.velocity.LengthSquared() > 0.25f)
				NPC.frame.Y = ((int)(Main.GameUpdateCount / 6) % 2) * frameHeight;
			else
				NPC.frame.Y = 0;
		}

		public override void SetDefaults()
		{
			base.SetDefaults();
			NPC.lifeMax = 5;
			NPC.value = 0;
			NPC.rarity = 3; // Shows on the Lifeform Analyzer.
		}

		public override void ModifyNPCLoot(NPCLoot npcLoot)
		{
			npcLoot.Add(new CommonDrop(ItemID.GoldCoin, 1, 3, 3));
		}

		public override float SpawnChance(NPCSpawnInfo spawnInfo)
		{
			if (spawnInfo.PlayerInTown)
				return 0f;
			return 0.000001f; // 0.0001%.
		}
	}
}
