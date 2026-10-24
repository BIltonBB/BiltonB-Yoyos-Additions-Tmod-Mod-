using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using System;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoProj : ModProjectile
    {
        private int hitCounter;
        private bool healthInitialized;
        private bool wasAbove50;
        private bool wasAbove25;
        private int mlTimer;
        private int postTS2Timer;
        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.timeLeft = 9999;

            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 400f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 25f;
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = -1f;
        }

        public override void OnSpawn(IEntitySource source)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            Player ownerPlayer = Main.player[Projectile.owner];
            LordYoyoPlayerState ownerState = ownerPlayer != null ? ownerPlayer.GetModPlayer<LordYoyoPlayerState>() : null;
            SpawnCores(ownerState != null && ownerState.PostTS2CorePhase ? 5 : 3);
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (player == null || !player.active)
                return;

            LordYoyoPlayerState state = player.GetModPlayer<LordYoyoPlayerState>();

            if (state.PostTS2CorePhase)
                postTS2Timer++;
            else
                postTS2Timer = 0;

            if (!healthInitialized)
            {
                healthInitialized = true;
                wasAbove50 = player.statLife > player.statLifeMax2 * 0.5f;
                wasAbove25 = player.statLife > player.statLifeMax2 * 0.25f;
            }

            bool above50 = player.statLife > player.statLifeMax2 * 0.5f;
            bool above25 = player.statLife > player.statLifeMax2 * 0.25f;

            // 50% threshold crossing.
            if (!state.TS2Active && wasAbove50 && !above50 && player.statLife > 0 && Main.myPlayer == Projectile.owner)
            {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    player.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<LordYoyoProjTS>(),
                    0,
                    0f,
                    Projectile.owner,
                    Projectile.whoAmI
                );
            }

            // MN: below 25%, once per life. Never during TS2.
            if (!state.TS2Active && state.PostTS2CorePhase && postTS2Timer >= 600 && !state.MNUsedThisLife && wasAbove25 && !above25 && player.statLife > 0 && Main.myPlayer == Projectile.owner)
            {
                state.MNUsedThisLife = true;
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<LordYoyoProjMN>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    Projectile.whoAmI
                );
            }

            // TS2 protection is also mirrored here while this yoyo exists.
            // The TS2 projectile owns the actual gradual healing and phase
            // completion, so unequipping the yoyo cannot interrupt it.
            if (state.TS2Active)
            {
                player.immune = true;
                player.immuneTime = Math.Max(10, (int)player.immuneTime);
                player.noFallDmg = true;
            }

            wasAbove50 = above50;
            wasAbove25 = above25;

            // ML: every 10 seconds if an enemy is nearby.
            mlTimer++;
            if (mlTimer >= 600 && Main.myPlayer == Projectile.owner)
            {
                mlTimer = 0;
                int targetIndex = FindNearestEnemy(Projectile.Center, 900f);
                if (targetIndex >= 0)
                {
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<LordYoyoProjML>(),
                        Math.Max(1, Projectile.damage / 5),
                        Projectile.knockBack,
                        Projectile.owner,
                        targetIndex,
                        Projectile.whoAmI
                    );
                }
            }

            if (Main.rand.NextBool(2))
                LordYoyoVisualHelper.SpawnBlueSpark(Projectile.Center, 1.5f, 0.7f);

            LordYoyoVisualHelper.AddBlueLight(Projectile.Center, 1.2f);
        }

        private int FindNearestEnemy(Vector2 center, float maxDistance)
        {
            int best = -1;
            float bestDistance = maxDistance * maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.life <= 0)
                    continue;

                float distance = Vector2.DistanceSquared(center, npc.Center);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            hitCounter++;

            if (hitCounter >= 20)
            {
                hitCounter = 0;
                if (Main.myPlayer == Projectile.owner)
                {
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        target.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<LordYoyoProjIB>(),
                        Math.Max(1, (int)(Projectile.damage * 2f)),
                        Projectile.knockBack,
                        Projectile.owner
                    );
                }
            }
        }

        private void SpawnCores(int count)
        {
            KillChildrenByType(ModContent.ProjectileType<LordYoyoProjC>());

            if (Main.myPlayer != Projectile.owner)
                return;

            for (int i = 0; i < count; i++)
            {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<LordYoyoProjC>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    i,
                    Projectile.whoAmI,
                    count
                );
            }
        }

        public void ForcePostTS2FiveCores()
        {
            SpawnCores(5);
        }

        private void KillChildrenByType(int type)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Projectile.owner || p.type != type)
                    continue;

                if ((int)p.ai[1] == Projectile.whoAmI)
                    p.Kill();
            }
        }

        private void KillAllOwnedProjectiles(int type)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Projectile.owner || p.type != type)
                    continue;

                p.Kill();
                p.netUpdate = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            KillChildrenByType(ModContent.ProjectileType<LordYoyoProjC>());
            KillChildrenByType(ModContent.ProjectileType<LordYoyoProjMN>());

            // TEOC stores the parent yoyo index in ai[0], not ai[1].
            int teocType = ModContent.ProjectileType<LordYoyoProjTEOC>();
            int otType = ModContent.ProjectileType<LordYoyoProjOT>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != Projectile.owner)
                    continue;

                if (p.type == teocType)
                {
                    if ((int)p.ai[0] == Projectile.whoAmI)
                    {
                        p.Kill();
                        p.netUpdate = true;
                    }
                    continue;
                }

                if (p.type == otType &&
                    (int)p.ai[0] == Projectile.whoAmI)
                {
                    p.Kill();
                    p.netUpdate = true;
                }
            }
        }
    }
}
