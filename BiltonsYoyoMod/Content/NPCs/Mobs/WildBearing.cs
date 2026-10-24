using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Systems;

namespace BiltonsYoyoMod.Content.NPCs.Mobs
{
    public class WildBearing : ModNPC
    {
        public override void SetStaticDefaults()
        {
            NPCID.Sets.ShouldBeCountedAsBoss[Type] = false;
        }

        public override void SetDefaults()
        {
            NPC.width = 24;
            NPC.height = 24;

            NPC.aiStyle = -1;

            NPC.damage = 50;
            NPC.defense = 4;
            NPC.lifeMax = 60;

            NPC.knockBackResist = 0.6f;
            NPC.value = 200f;

            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;

            NPC.boss = false;

            Music = -1;
            SceneEffectPriority = SceneEffectPriority.None;
        }

        public override void BossHeadSlot(ref int index)
        {
            index = -1;
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            if (spawnInfo.Player == null)
                return 0f;

            if (!spawnInfo.Player.active || spawnInfo.Player.dead)
                return 0f;

            if (!NPC.downedBoss3)
                return 0f;

            if (!spawnInfo.Player.ZoneRockLayerHeight)
                return 0f;

            if (!BearingPartyEvent.Active)
                return 0.005f;

            return 5f;
        }

        public override void AI()
        {
            Player target = Main.player[NPC.target];

            if (NPC.target < 0 ||
                target == null ||
                !target.active ||
                target.dead)
            {
                NPC.TargetClosest();
                target = Main.player[NPC.target];
            }

            if (target == null || !target.active || target.dead)
            {
                NPC.EncourageDespawn(10);
                return;
            }

            float driftDir =
                target.Center.X < NPC.Center.X ? -1f : 1f;

            NPC.velocity.X = MathHelper.Lerp(
                NPC.velocity.X,
                driftDir * 1.2f,
                0.05f
            );

            if (NPC.velocity.Y == 0f)
                NPC.velocity.Y = -6.5f;

            NPC.ai[0]++;

            if (NPC.ai[0] >= 120)
            {
                NPC.ai[0] = 0;

                Vector2 lungeDir =
                    (target.Center - NPC.Center)
                    .SafeNormalize(Vector2.UnitX);

                NPC.velocity =
                    lungeDir * 6f +
                    new Vector2(0f, -7f);
            }
        }

        public override void OnKill()
        {
            BearingPartyEvent.RegisterKill();
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // Bearing salvage: often scraps, rarely a whole part.
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoScraps>(), 2, 1, 3));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<global::BiltonsYoyoMod.Items.Materials.YoyoParts>(), 5, 1, 1));
        }
    }
}