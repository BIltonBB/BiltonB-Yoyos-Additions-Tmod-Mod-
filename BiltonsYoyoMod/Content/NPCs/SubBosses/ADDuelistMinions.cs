using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Content.NPCs.SubBosses
{
	// AD_A2: a slow homing yoyo-mob with 100 HP. In phase 2 it sometimes
	// accelerates in bursts, making it less predictable.
	public class ADDartYoyo : ModNPC
	{
		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
		}

		public override void SetDefaults()
		{
			NPC.width = 26;
			NPC.height = 26;
			NPC.aiStyle = -1;
			NPC.damage = 45;
			NPC.defense = 5;
			NPC.lifeMax = 100;
			NPC.life = 100;
			NPC.knockBackResist = 0.5f;
			NPC.noGravity = true;
			NPC.noTileCollide = true; // Homing darts phase through platforms.
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath14;
			NPC.value = 0f;
		}

		public override void AI()
		{
			// ai[0] = burst cooldown, ai[1] = burst ticks left, ai[2] = enraged (phase 2).
			NPC.TargetClosest();
			Player target = Main.player[NPC.target];
			if (NPC.target < 0 || target == null || !target.active || target.dead) {
				NPC.velocity *= 0.95f;
				return;
			}
			NPC.ai[0]++;
			if (NPC.ai[2] == 1f && NPC.ai[1] <= 0f && NPC.ai[0] >= 120 && Main.rand.NextBool(3)) {
				NPC.ai[1] = 40f; // Unpredictable acceleration burst.
				NPC.ai[0] = 0f;
				for (int i = 0; i < 8; i++) {
					Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0f, 0f, 120, default, 0.9f);
					dust.noGravity = true;
				}
			}
			float speed = NPC.ai[1] > 0f ? 4.8f : 2.5f;
			if (NPC.ai[1] > 0f)
				NPC.ai[1]--;
			Vector2 want = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * speed;
			NPC.velocity = Vector2.Lerp(NPC.velocity, want, 0.06f);
			NPC.rotation += 0.25f;
			Lighting.AddLight(NPC.Center, 0.3f, 0.15f, 0.08f);
			if (NPC.collideX || NPC.collideY)
				NPC.velocity *= 0.9f;
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int i = 0; i < 4; i++) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, 0f, 0f, 120, default, 0.9f);
				dust.noGravity = true;
			}
			if (NPC.life <= 0) {
				for (int i = 0; i < 14; i++) {
					Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Smoke, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 120, default, 1f);
					dust.noGravity = true;
				}
			}
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Texture2D tex = TextureAssets.Npc[Type].Value;
			spriteBatch.Draw(tex, NPC.Center - screenPos, null, drawColor, NPC.rotation, tex.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0f);
			return false;
		}
	}

	// AD phase-2 guard: 20K HP stationary spinning shield. The boss is
	// invulnerable while any shield lives.
	public class ADShieldYoyo : ModNPC
	{
		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 1;
		}

		public override void SetDefaults()
		{
			NPC.width = 40;
			NPC.height = 40;
			NPC.aiStyle = -1;
			NPC.damage = 50;
			NPC.defense = 20;
			NPC.lifeMax = 20000;
			NPC.life = 20000;
			NPC.knockBackResist = 1f; // Immovable.
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.HitSound = SoundID.NPCHit4;
			NPC.DeathSound = SoundID.NPCDeath14;
			NPC.value = 0f;
		}

		public override void AI()
		{
			// Stationary spinner. ai[0] = boss whoAmI for leash checks.
			NPC.velocity = Vector2.Zero;
			NPC.rotation += 0.12f;
			Lighting.AddLight(NPC.Center, 0.35f, 0.18f, 0.3f);
			if (Main.rand.NextBool(8)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, 0f, -0.5f, 120, default, 0.9f);
				dust.noGravity = true;
			}
			int bossIndex = (int)NPC.ai[0];
			if (bossIndex < 0 || bossIndex >= Main.maxNPCs || !Main.npc[bossIndex].active || Main.npc[bossIndex].type != ModContent.NPCType<AncientDuelist>()) {
				// Orphaned guard withers away.
				NPC.life -= NPC.lifeMax / 600;
				if (NPC.life <= 0) {
					NPC.active = false;
				}
			}
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int i = 0; i < 5; i++) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, 0f, 0f, 120, default, 1f);
				dust.noGravity = true;
			}
			if (NPC.life <= 0) {
				for (int i = 0; i < 24; i++) {
					Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.2f);
					dust.noGravity = true;
				}
				SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
			}
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Texture2D tex = TextureAssets.Npc[Type].Value;
			spriteBatch.Draw(tex, NPC.Center - screenPos, null, drawColor, NPC.rotation, tex.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0f);
			return false;
		}
	}
}
