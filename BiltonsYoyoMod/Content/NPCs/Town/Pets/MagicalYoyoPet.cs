using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.NPCs.Town.Pets
{
	// Magical Yoyo town pet. Pet it through the custom 5-button panel
	// to gain Magical Essence (+5% yoyo damage). Moves in post-evil, any time.
	[AutoloadHead]
	public class MagicalYoyoPet : ModNPC
	{
		internal static readonly string[] Lines = {
			"You are granted with magic.",
			"The magic is being absorbed into your body.",
		};

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
			NPC.width = 24;
			NPC.height = 24;
			NPC.aiStyle = NPCAIStyleID.Passive;
			NPC.damage = 0;
			NPC.defense = 10;
			NPC.lifeMax = 250;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.knockBackResist = 0.5f;
			AIType = NPCID.TownCat;
			AnimationType = NPCID.TownCat;
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
				new FlavorTextBestiaryInfoElement("A yoyo soaked in raw magic. Petting it shares a little of that power with you."),
			});
		}

		public override bool CanTownNPCSpawn(int numTownNPCs)
		{
			// Post-evil (Eater of Worlds / Brain of Cthulhu), any time.
			return NPC.downedBoss2;
		}

		public override bool CanGoToStatue(bool toKingStatue) => false;

		// Vanilla chat is bypassed: right-click opens the 5-button panel.
		public override bool CanChat() => false;

		public override List<string> SetNPCNameList() => new() { "Magical Yoyo" };

		// Idle flavor: mana sparkles + soft arcane light.
		public override void DrawEffects(ref Color drawColor)
		{
			Lighting.AddLight(NPC.Center, 0.06f, 0.1f, 0.16f);
			if (Main.rand.NextBool(45)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.ManaRegeneration, 0f, -0.6f, 120, default, 0.8f);
				dust.noGravity = true;
			}
		}

		public override void PostAI()
		{
			// Anti-stuck: float up if the drift wedged the pet inside solid tiles.
			if (!NPC.noTileCollide && IsEmbedded(NPC.Center)) {
				NPC.noTileCollide = true;
				NPC.noGravity = true;
				NPC.velocity = new Vector2(0f, -3f);
				return;
			}
			NPC.noTileCollide = false;
			NPC.noGravity = false;
			// Drift after the nearest player like a curious pet, never straying far.
			Player best = null;
			float bestDist = 500f * 500f;
			for (int i = 0; i < Main.maxPlayers; i++) {
				Player player = Main.player[i];
				if (player == null || !player.active || player.dead)
					continue;
				float dist = Vector2.DistanceSquared(NPC.Center, player.Center);
				if (dist < bestDist) {
					bestDist = dist;
					best = player;
				}
			}
			if (best == null || bestDist < 160f * 160f)
				return;
			float dir = System.Math.Sign(best.Center.X - NPC.Center.X);
			NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, dir * 2f, 0.06f);
			if (NPC.collideX && NPC.collideY)
				NPC.velocity.Y = -4.5f;
		}

		private static bool IsEmbedded(Vector2 worldPos)
		{
			Point p = worldPos.ToTileCoordinates();
			Tile tile = Framing.GetTileSafely(p.X, p.Y);
			return tile.HasTile && Main.tileSolid[tile.TileType] && !tile.IsActuated && !TileID.Sets.Platforms[tile.TileType];
		}
	}
}
