using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Content.Projectiles;

namespace BiltonsYoyoMod.Content.NPCs.SubBosses
{
    public abstract class CowboyOfYoyosBase : ModNPC
    {
        protected abstract int BaseLife { get; }
        protected abstract int BaseDamage { get; }
        protected abstract int BaseDefense { get; }
        protected abstract float PreferredDistance { get; }

        public abstract float CounterweightOrbitRadius { get; }
        public abstract float CounterweightOrbitSpeed { get; }

        protected abstract int TotalWalkFrames { get; }

        protected virtual int SpriteFrameWidth => 44;
        protected virtual int SpriteFrameHeight => 54;

        private const int YoyoLifetime = 300;
        private const int ThrowCooldown = 120;

        private int cycleTimer;
        private bool yoyoOut;
        private int ownedYoyoIndex = -1;
        private int counterweightCooldown;

        // Every yoyo hit hurls a counterweight straight back at the thrower.
        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            if (projectile == null || !projectile.active)
                return;
            if (projectile.aiStyle != ProjAIStyleID.Yoyo)
                return;
            if (counterweightCooldown > 0)
                return;
            counterweightCooldown = 45;
            Player thrower = null;
            if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers) {
                Player p = Main.player[projectile.owner];
                if (p != null && p.active && !p.dead)
                    thrower = p;
            }
            if (thrower == null) {
                NPC.TargetClosest();
                if (NPC.target >= 0 && NPC.target < Main.maxPlayers)
                    thrower = Main.player[NPC.target];
            }
            if (thrower == null || !thrower.active || thrower.dead)
                return;
            Vector2 dir = (thrower.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int idx = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * 12f,
                    ModContent.ProjectileType<Content.Projectiles.HostileCounterweightProj>(),
                    (int)(NPC.damage * 0.8f), 3f, Main.myPlayer);
                if (idx >= 0)
                    Main.projectile[idx].netUpdate = true;
            }
            Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Item1, NPC.Center);
        }

        public override void SetStaticDefaults()
        {
            NPCID.Sets.MPAllowedEnemies[NPC.type] = true;
            Main.npcFrameCount[NPC.type] = TotalWalkFrames;
        }

        public override void SetDefaults()
        {
            NPC.width = 34;
            NPC.height = 46;
            NPC.aiStyle = -1;
            NPC.damage = BaseDamage;
            NPC.defense = BaseDefense;
            NPC.lifeMax = BaseLife;
            NPC.knockBackResist = 0.4f;
            NPC.noGravity = false;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.boss = true;
            NPC.netAlways = true;
        }

        public override void ApplyDifficultyAndPlayerScaling(
            int numPlayers,
            float balance,
            float bossAdjustment)
        {
            float mult =
                Main.masterMode ? 1.7f :
                Main.expertMode ? 1.4f :
                1f;

            NPC.lifeMax = (int)(BaseLife * mult);
            NPC.life = NPC.lifeMax;
        }

        // Duel-at-high-noon atmosphere: dust kicked up while moving, faint
        // ember glow and occasional gold glints off the spurs.
        public override void DrawEffects(ref Color drawColor)
        {
            float speed = NPC.velocity.Length();
            Lighting.AddLight(NPC.Center, 0.14f, 0.09f, 0.05f);
            int chance = speed > 3f ? 6 : 40;
            if (Main.rand.NextBool(chance)) {
                int dtype = Main.rand.NextBool(5) ? DustID.GoldCoin : DustID.Smoke;
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, dtype,
                    -NPC.velocity.X * 0.25f, -1f, 120, default, Main.rand.NextFloat(0.7f, 1.1f));
                dust.noGravity = dtype != DustID.Smoke;
            }
        }

        public override void OnSpawn(
            Terraria.DataStructures.IEntitySource source)
        {
            Color lineColor =
                Color.Lerp(
                    Color.White,
                    new Color(150, 100, 60),
                    0.5f
                );

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Main.NewText(
                    "Looking for a quick challenge?",
                    lineColor.R,
                    lineColor.G,
                    lineColor.B
                );
            }
        }

        public override bool? CanBeHitByProjectile(
            Projectile projectile)
        {
            if (projectile.aiStyle == ProjAIStyleID.Yoyo)
                return null;

            if (projectile.damage > 500)
                return null;

            return false;
        }

        public override bool? CanBeHitByItem(
            Player player,
            Item item)
        {
            return false;
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

            if (target == null ||
                !target.active ||
                target.dead)
            {
                // Everyone is dead: the duel is over, despawn at once.
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    Main.NewText("Cowboy has despawned.", 220, 180, 120);
                NPC.active = false;
                return;
            }

            ApplySurvivorBoost();
            RunMovement(target);
            UpdateYoyoCycle(target);
            if (counterweightCooldown > 0)
                counterweightCooldown--;

            NPC.spriteDirection =
                NPC.velocity.X < 0f ? -1 : 1;
        }

        protected abstract void RunMovement(Player target);

        protected float PreferredDistanceValue =>
            PreferredDistance;

        protected Vector2? FindNearestHostileYoyo(
            out float distance)
        {
            float closestDist = float.MaxValue;
            Vector2? result = null;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];

                if (p.active &&
                    p.type ==
                    ModContent.ProjectileType<CowboyYoyoProjectile>())
                {
                    float dist =
                        Vector2.Distance(
                            NPC.Center,
                            p.Center
                        );

                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        result = p.Center;
                    }
                }
            }

            distance = closestDist;
            return result;
        }

        private void UpdateYoyoCycle(Player target)
        {
            if (!yoyoOut)
            {
                cycleTimer++;

                if (cycleTimer >= ThrowCooldown)
                {
                    cycleTimer = 0;
                    ThrowYoyo(target);
                    yoyoOut = true;
                }
            }
            else
            {
                bool stillActive =
                    ownedYoyoIndex >= 0 &&
                    ownedYoyoIndex < Main.maxProjectiles &&
                    Main.projectile[ownedYoyoIndex].active &&
                    Main.projectile[ownedYoyoIndex].type ==
                    ModContent.ProjectileType<CowboyYoyoProjectile>();

                if (!stillActive)
                {
                    yoyoOut = false;
                    cycleTimer = 0;
                    KillOwnedCounterweight();
                }
            }
        }

        private void ThrowYoyo(Player target)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 vel =
                (target.Center - NPC.Center)
                .SafeNormalize(Vector2.UnitX) * 4f;

            int p = Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                NPC.Center,
                vel,
                ModContent.ProjectileType<CowboyYoyoProjectile>(),
                NPC.damage,
                1f,
                Main.myPlayer
            );

            if (p < 0 || p >= Main.maxProjectiles)
                return;

            Main.projectile[p].ai[1] = NPC.whoAmI;
            Main.projectile[p].timeLeft = YoyoLifetime;
            Main.projectile[p].netUpdate = true;

            ownedYoyoIndex = p;
        }

        private void KillOwnedCounterweight()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];

                if (p.active &&
                    p.type ==
                    ModContent.ProjectileType<HostileCounterweight>() &&
                    (int)p.ai[0] == NPC.whoAmI)
                {
                    p.Kill();
                }
            }
        }

        private void ApplySurvivorBoost()
        {
            int deadSiblings = 0;

            if (NPC.type != ModContent.NPCType<CowboyOfYoyos1>() &&
                !IsTypeAlive(
                    ModContent.NPCType<CowboyOfYoyos1>()))
            {
                deadSiblings++;
            }

            if (NPC.type != ModContent.NPCType<CowboyOfYoyos2>() &&
                !IsTypeAlive(
                    ModContent.NPCType<CowboyOfYoyos2>()))
            {
                deadSiblings++;
            }

            if (NPC.type != ModContent.NPCType<CowboyOfYoyos3>() &&
                !IsTypeAlive(
                    ModContent.NPCType<CowboyOfYoyos3>()))
            {
                deadSiblings++;
            }

            float mult = 1f;

            for (int i = 0; i < deadSiblings; i++)
                mult *= 1.5f;

            // Capped: a lone last cowboy hits hard, never triple digits.
            if (mult > 1.5f)
                mult = 1.5f;

            NPC.damage = (int)(BaseDamage * mult);
            NPC.defense = (int)(BaseDefense * mult);
        }

        private static bool IsTypeAlive(int type)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active &&
                    Main.npc[i].type == type)
                {
                    return true;
                }
            }

            return false;
        }

        public override void FindFrame(int frameHeight)
        {
            NPC.frameCounter++;

            int frameSpeed =
                NPC.velocity.X != 0f ? 5 : 20;

            if (NPC.frameCounter >= frameSpeed)
            {
                NPC.frameCounter = 0;

                int currentFrame =
                    NPC.frame.Y / SpriteFrameHeight;

                currentFrame++;

                if (currentFrame >= TotalWalkFrames)
                    currentFrame = 0;

                NPC.frame = new Rectangle(
                    0,
                    currentFrame * SpriteFrameHeight,
                    SpriteFrameWidth,
                    SpriteFrameHeight
                );
            }
            else
            {
                int currentFrame =
                    NPC.frame.Y / SpriteFrameHeight;

                if (currentFrame < 0 ||
                    currentFrame >= TotalWalkFrames)
                {
                    currentFrame = 0;
                }

                NPC.frame = new Rectangle(
                    0,
                    currentFrame * SpriteFrameHeight,
                    SpriteFrameWidth,
                    SpriteFrameHeight
                );
            }
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 4; i++)
            {
                Dust.NewDust(
                    NPC.position,
                    NPC.width,
                    NPC.height,
                    DustID.Blood
                );
            }
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
        }
    }
}