using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Items;
using System;

namespace BiltonsYoyoMod.Content.NPCs.SubBosses
{
    public class GiantBearing : ModNPC
    {
        private const int BaseContactDamage = 30;
        private const int DoubleContactDamage = BaseContactDamage * 2;

        // Timings
        private const int AttackInterval = 300;
        private const int DashDuration = 24;
        private const int RollDuration = 150;

        // Wild Bearing spawning
        private const int BearingSpawnInterval = 300;
        private const int WildBearingDoubleLife = 120;

        private int BearingSpawnTimer;

        // Movement
        private const float NormalMoveSpeed = 2.2f;
        private const float NormalAcceleration = 0.08f;

        private const float DashSpeed = 11f;
        private const float JumpSpeed = 14f;
        private const float RollSpeed = 8f;

        private const float NormalGravity = 0.3f;
        private const float DoubleGravity = 0.6f;

        // ai[0] = timer
        // ai[1] = attack state
        // ai[2] = next attack
        // ai[3] = roll direction
        //
        // 0 = normal
        // 1 = dash
        // 2 = jump
        // 3 = roll

        private int AttackTimer
        {
            get => (int)NPC.ai[0];
            set => NPC.ai[0] = value;
        }

        private int AttackState
        {
            get => (int)NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        private int NextAttack
        {
            get => (int)NPC.ai[2];
            set => NPC.ai[2] = value;
        }

        private int RollDirection
        {
            get => NPC.ai[3] >= 0f ? 1 : -1;
            set => NPC.ai[3] = value;
        }

        public override string BossHeadTexture =>
            "BiltonsYoyoMod/Content/NPCs/SubBosses/GiantBearing_Head_Boss";

        public override void SetStaticDefaults()
        {
            NPCID.Sets.MPAllowedEnemies[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(NPC.type);
        }

        public override void SetDefaults()
        {
            NPC.width = 80;
            NPC.height = 80;

            NPC.aiStyle = -1;

            NPC.damage = BaseContactDamage;
            NPC.defense = 14;

            NPC.lifeMax = 4000;
            NPC.life = NPC.lifeMax;

            NPC.knockBackResist = 0.1f;

            NPC.noGravity = true;
            NPC.noTileCollide = false;

            NPC.boss = true;

            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath9;

            NPC.netAlways = true;
            NPC.value = 0f;

			AttackState = 0;
			AttackTimer = AttackInterval;
			NextAttack = 0;
			RollDirection = 1;

			BearingSpawnTimer = BearingSpawnInterval;
		}

		public override void DrawEffects(ref Color drawColor)
		{
			// Rolling/dashing throws metallic sparks; enraged (sub-half HP) glows hot.
			bool raging = NPC.life < NPC.lifeMax / 2;
			float speed = NPC.velocity.Length();
			float pulse = raging ? 0.3f + 0.12f * (float)System.Math.Sin(Main.GameUpdateCount * 0.15f) : 0.12f;
			Lighting.AddLight(NPC.Center, pulse + (raging ? 0.1f : 0f), raging ? 0.08f : 0.1f, 0.05f);
			int chance = speed > 6f ? 3 : speed > 2f ? 8 : 30;
			if (Main.rand.NextBool(chance)) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
					raging ? DustID.Torch : DustID.Iron, -NPC.velocity.X * 0.3f, -NPC.velocity.Y * 0.3f, 120, default, 1f);
				dust.noGravity = true;
			}
			// Fast rolls trail an orbiting glint so the spin reads at speed.
			if (speed > 7f && Main.rand.NextBool(4)) {
				float a = Main.rand.NextFloat(MathHelper.TwoPi);
				Vector2 edge = NPC.Center + a.ToRotationVector2() * (NPC.width * 0.6f);
				Dust glint = Dust.NewDustDirect(edge - new Vector2(4, 4), 8, 8,
					DustID.GoldCoin, -NPC.velocity.X * 0.2f, -NPC.velocity.Y * 0.2f, 120, default, 0.9f);
				glint.noGravity = true;
			}
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int i = 0; i < 6; i++) {
				Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Iron, hit.HitDirection * 2f, -2f, 100, default, 1f);
				dust.noGravity = true;
			}
			if (NPC.life <= 0) {
				for (int i = 0; i < 30; i++) {
					Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Smoke, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 2f), 120, default, 1.4f);
					dust.noGravity = false;
				}
				for (int i = 0; i < 16; i++) {
					Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Torch, Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 6f), 100, default, 1.2f);
					dust.noGravity = true;
				}
			}
		}

        public override void AI()
        {
            if (NPC.target < 0 ||
                NPC.target >= Main.maxPlayers ||
                !Main.player[NPC.target].active ||
                Main.player[NPC.target].dead)
            {
                NPC.TargetClosest();
            }

            if (NPC.target < 0 ||
                NPC.target >= Main.maxPlayers ||
                !Main.player[NPC.target].active ||
                Main.player[NPC.target].dead)
            {
                NPC.EncourageDespawn(10);
                return;
            }

            Player target = Main.player[NPC.target];

            SpawnWildBearing();

            switch (AttackState)
            {
                case 0:
                    RunNormalMovement(target);
                    RunAttackCooldown(target);
                    break;

                case 1:
                    RunDashAttack();
                    break;

                case 2:
                    RunJumpAttack(target);
                    break;

                case 3:
                    RunRollAttack();
                    break;
            }

            NPC.rotation += NPC.velocity.X * 0.022f;

            if (Math.Abs(NPC.velocity.X) > 0.2f)
                NPC.spriteDirection = NPC.velocity.X > 0f ? 1 : -1;

            SpawnAttackDust();
        }

        private void SpawnWildBearing()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            BearingSpawnTimer--;

            if (BearingSpawnTimer > 0)
                return;

            BearingSpawnTimer = BearingSpawnInterval;

            int wildBearingType =
                ModContent.NPCType<Content.NPCs.Mobs.WildBearing>();

            Vector2 spawnPosition =
                NPC.Center +
                new Vector2(
                    Main.rand.NextFloat(-120f, 120f),
                    Main.rand.NextFloat(-100f, 20f)
                );

            int index = NPC.NewNPC(
                NPC.GetSource_FromAI(),
                (int)spawnPosition.X,
                (int)spawnPosition.Y,
                wildBearingType
            );

            if (index < 0 || index >= Main.maxNPCs)
                return;

            NPC bearing = Main.npc[index];

            bearing.lifeMax = WildBearingDoubleLife;
            bearing.life = WildBearingDoubleLife;

            bearing.netUpdate = true;
        }

        private void RunNormalMovement(Player target)
        {
            NPC.noGravity = false;
            NPC.damage = BaseContactDamage;

            ApplyNormalGravity();

            float direction =
                target.Center.X < NPC.Center.X ? -1f : 1f;

            float targetSpeed = direction * NormalMoveSpeed;

            NPC.velocity.X = MathHelper.Lerp(
                NPC.velocity.X,
                targetSpeed,
                NormalAcceleration
            );

            if (IsOnGround())
            {
                NPC.velocity.Y = -9f;
            }
        }

        private void RunAttackCooldown(Player target)
        {
            AttackTimer--;

            if (AttackTimer > 0)
                return;

            switch (NextAttack)
            {
                case 0:
                    StartDashAttack(target);
                    break;

                case 1:
                    StartJumpAttack(target);
                    break;

                case 2:
                    StartRollAttack();
                    break;
            }

            NextAttack = (NextAttack + 1) % 3;

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        private void StartDashAttack(Player target)
        {
            AttackState = 1;
            AttackTimer = DashDuration;

            NPC.noGravity = true;
            NPC.damage = BaseContactDamage;

            Vector2 direction =
                (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);

            NPC.velocity = direction * DashSpeed;

            SpawnDashDust();

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        private void RunDashAttack()
        {
            NPC.noGravity = true;
            NPC.damage = BaseContactDamage;

            if (NPC.velocity.LengthSquared() > 0.01f)
            {
                NPC.velocity =
                    NPC.velocity.SafeNormalize(Vector2.UnitX) * DashSpeed;
            }

            Vector2 oldVelocity = NPC.velocity;

            ApplyTileCollision();

            if (Math.Abs(oldVelocity.X) > 2f &&
                Math.Abs(NPC.velocity.X) < 0.5f)
            {
                SpawnWallImpactDust();

                AttackState = 0;
                AttackTimer = AttackInterval;
                NPC.velocity.X = 0f;

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.netUpdate = true;

                return;
            }

            AttackTimer--;

            if (AttackTimer > 0)
                return;

            AttackState = 0;
            AttackTimer = AttackInterval;

            NPC.velocity *= 0.35f;

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        private void StartJumpAttack(Player target)
        {
            AttackState = 2;

            NPC.noGravity = false;
            NPC.damage = DoubleContactDamage;

            Vector2 direction =
                (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);

            NPC.velocity.X = direction.X * 5f;
            NPC.velocity.Y = -JumpSpeed;

            SpawnJumpDust();

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        private void RunJumpAttack(Player target)
        {
            NPC.noGravity = false;
            NPC.damage = DoubleContactDamage;

            NPC.velocity.Y += DoubleGravity;

            if (NPC.velocity.Y < 0f)
            {
                float direction =
                    target.Center.X < NPC.Center.X ? -1f : 1f;

                NPC.velocity.X = MathHelper.Lerp(
                    NPC.velocity.X,
                    direction * 4f,
                    0.025f
                );
            }
            else
            {
                NPC.velocity.X *= 0.985f;
            }

            ApplyTileCollision();

            if (NPC.velocity.Y == 0f &&
                IsStandingOnSurface())
            {
                SpawnLandingDust();

                for (int i = 0; i < 12; i++)
                {
                    Dust dust = Dust.NewDustPerfect(
                        NPC.Bottom + Main.rand.NextVector2Circular(30f, 4f),
                        DustID.Smoke,
                        new Vector2(
                            Main.rand.NextFloat(-5f, 5f),
                            Main.rand.NextFloat(-4f, -1f)
                        ),
                        0,
                        Color.White,
                        1.35f
                    );

                    dust.noGravity = true;
                }

                NPC.damage = BaseContactDamage;
                NPC.velocity.Y = 0f;

                AttackState = 0;
                AttackTimer = AttackInterval;

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.netUpdate = true;
            }
        }

        private void StartRollAttack()
        {
            AttackState = 3;
            AttackTimer = RollDuration;

            NPC.noGravity = false;
            NPC.damage = DoubleContactDamage;

            RollDirection = Main.rand.NextBool() ? -1 : 1;

            NPC.velocity.X = RollDirection * RollSpeed;
            NPC.velocity.Y = 0f;

            SpawnRollDust();

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        private void RunRollAttack()
        {
            NPC.noGravity = false;
            NPC.damage = DoubleContactDamage;

            NPC.velocity.X = RollDirection * RollSpeed;

            Vector2 oldVelocity = NPC.velocity;

            ApplyTileCollision();

            if (Math.Abs(oldVelocity.X) > 2f &&
                Math.Abs(NPC.velocity.X) < 0.5f)
            {
                RollDirection *= -1;
                NPC.velocity.X = RollDirection * RollSpeed;

                SpawnWallImpactDust();

                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.netUpdate = true;
            }

            AttackTimer--;

            if (AttackTimer > 0)
                return;

            AttackState = 0;
            AttackTimer = AttackInterval;

            NPC.damage = BaseContactDamage;
            NPC.velocity.X *= 0.3f;

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.netUpdate = true;
        }

        private void ApplyNormalGravity()
        {
            NPC.velocity.Y += NormalGravity;

            if (NPC.velocity.Y > 12f)
                NPC.velocity.Y = 12f;
        }

        private void ApplyTileCollision()
        {
            NPC.velocity = Collision.TileCollision(
                NPC.position,
                NPC.velocity,
                NPC.width,
                NPC.height,
                fallThrough: false,
                fall2: false
            );
        }

        private bool IsOnGround()
        {
            Vector2 checkPosition = NPC.position;
            checkPosition.Y += 2f;

            return Collision.SolidCollision(
                checkPosition,
                NPC.width,
                NPC.height
            );
        }

        private bool IsStandingOnSurface()
        {
            int left = (int)NPC.position.X + 4;
            int right = (int)(NPC.position.X + NPC.width - 4);
            int y = (int)(NPC.position.Y + NPC.height);

            for (int x = left; x <= right; x += 4)
            {
                Point tilePosition = new Point(x / 16, y / 16);

                if (!WorldGen.InWorld(
                    tilePosition.X,
                    tilePosition.Y,
                    1))
                {
                    continue;
                }

                Tile tile = Main.tile[
                    tilePosition.X,
                    tilePosition.Y
                ];

                if (tile == null || !tile.HasTile)
                    continue;

                if (Main.tileSolid[tile.TileType] &&
                    !Main.tileSolidTop[tile.TileType])
                {
                    return true;
                }

                if (Main.tileSolidTop[tile.TileType])
                    return true;
            }

            return false;
        }

        private void SpawnDashDust()
        {
            for (int i = 0; i < 24; i++)
            {
                Dust dust = Dust.NewDustPerfect(
                    NPC.Center + Main.rand.NextVector2Circular(32f, 32f),
                    DustID.Smoke,
                    -NPC.velocity * 0.15f +
                    Main.rand.NextVector2Circular(2f, 2f),
                    0,
                    Color.White,
                    1.3f
                );

                dust.noGravity = true;
            }
        }

        private void SpawnJumpDust()
        {
            for (int i = 22; i > 0; i--)
            {
                Dust dust = Dust.NewDustPerfect(
                    NPC.Bottom +
                    Main.rand.NextVector2Circular(34f, 8f),
                    DustID.Smoke,
                    Main.rand.NextVector2Circular(3f, 2f),
                    0,
                    Color.White,
                    1.3f
                );

                dust.noGravity = true;
            }
        }

        private void SpawnLandingDust()
        {
            for (int i = 0; i < 34; i++)
            {
                Dust dust = Dust.NewDustPerfect(
                    NPC.Bottom +
                    Main.rand.NextVector2Circular(34f, 5f),
                    DustID.Smoke,
                    new Vector2(
                        Main.rand.NextFloat(-7f, 7f),
                        Main.rand.NextFloat(-6f, -1f)
                    ),
                    0,
                    Color.White,
                    1.45f
                );

                dust.noGravity = true;
            }
        }

        private void SpawnRollDust()
        {
            for (int i = 0; i < 26; i++)
            {
                Dust dust = Dust.NewDustPerfect(
                    NPC.Bottom +
                    Main.rand.NextVector2Circular(32f, 8f),
                    DustID.Smoke,
                    new Vector2(
                        -RollDirection * Main.rand.NextFloat(2f, 5f),
                        Main.rand.NextFloat(-2f, 0f)
                    ),
                    0,
                    Color.White,
                    1.3f
                );

                dust.noGravity = true;
            }
        }

        private void SpawnWallImpactDust()
        {
            for (int i = 0; i < 20; i++)
            {
                Dust dust = Dust.NewDustPerfect(
                    NPC.Center +
                    Main.rand.NextVector2Circular(25f, 25f),
                    DustID.Smoke,
                    Main.rand.NextVector2Circular(5f, 5f),
                    0,
                    Color.White,
                    1.25f
                );

                dust.noGravity = true;
            }
        }

        private void SpawnAttackDust()
        {
            if (!Main.rand.NextBool(3))
                return;

            switch (AttackState)
            {
                case 1:
                {
                    Dust dust = Dust.NewDustPerfect(
                        NPC.Center,
                        DustID.Smoke,
                        -NPC.velocity * 0.08f +
                        Main.rand.NextVector2Circular(2f, 2f),
                        0,
                        Color.White,
                        1.15f
                    );

                    dust.noGravity = true;
                    break;
                }

                case 2:
                {
                    Dust dust = Dust.NewDustPerfect(
                        NPC.Bottom,
                        DustID.Smoke,
                        Main.rand.NextVector2Circular(2f, 1f),
                        0,
                        Color.White,
                        1.15f
                    );

                    dust.noGravity = true;
                    break;
                }

                case 3:
                {
                    Dust dust = Dust.NewDustPerfect(
                        NPC.Bottom,
                        DustID.Smoke,
                        new Vector2(
                            -RollDirection *
                            Main.rand.NextFloat(1f, 3f),
                            -Main.rand.NextFloat(0.5f, 2f)
                        ),
                        0,
                        Color.White,
                        1.15f
                    );

                    dust.noGravity = true;
                    break;
                }
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // Placeholder drops remain unchanged.
            // npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GiantBearingHide>(), 1, 3, 6));
            // npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GiantBearingTrophy>(), 10));
            // npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<GiantBearingMask>(), 7));
            // npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<GiantBearingBag>()));
        }
    }
}