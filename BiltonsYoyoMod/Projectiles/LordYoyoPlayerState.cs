using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using System;
using System.IO;

namespace BiltonsYoyoMod.Projectiles
{
    public class LordYoyoPlayerState : ModPlayer
    {
        public bool MNUsedThisLife;
        public bool TS2UsedThisLife;
        public bool TS2Active;
        public bool PostTS2CorePhase;

        private bool thresholdInitialized;
        private bool wasAbove10;

        public override void Initialize()
        {
            MNUsedThisLife = false;
            TS2UsedThisLife = false;
            TS2Active = false;
            PostTS2CorePhase = false;
            thresholdInitialized = false;
            wasAbove10 = true;
        }

        public override void PostUpdate()
        {
            if (!Player.active || Player.statLife <= 0 || Player.statLifeMax2 <= 0)
                return;

            bool above10 = Player.statLife > Player.statLifeMax2 * 0.10f;

            if (!thresholdInitialized)
            {
                thresholdInitialized = true;
                wasAbove10 = above10;
                return;
            }

            // TS2 belongs to the player, not the currently equipped yoyo.
            // It triggers exactly when HP crosses from above 10% to below 10%.
            // TS2 only triggers on a real HP crossing while the Lord Yoyo is
            // actively released as a projectile. Having the yoyo merely in
            // the inventory is not enough.
            bool lordYoyoReleased = FindActiveLordYoyo() != null;

            if (!TS2UsedThisLife && !TS2Active && wasAbove10 && !above10 &&
                lordYoyoReleased && Main.myPlayer == Player.whoAmI)
            {
                TS2UsedThisLife = true;
                TS2Active = true;

                // TS2 begins: remove the current Core/MN/TEOC systems.
                KillOwnedProjectileType(ModContent.ProjectileType<LordYoyoProjMN>());
                KillOwnedProjectileType(ModContent.ProjectileType<LordYoyoProjC>());
                KillOwnedProjectileType(ModContent.ProjectileType<LordYoyoProjTEOC>());

                // The TS2 projectile itself now owns the TS2 DR sequence.
                // No TEOC is created during TS2.
                Projectile.NewProjectile(
                    Player.GetSource_Misc("LordYoyoTS2"),
                    Player.Center,
                    Microsoft.Xna.Framework.Vector2.Zero,
                    ModContent.ProjectileType<LordYoyoProjTS2>(),
                    0,
                    0f,
                    Player.whoAmI
                );
            }

            wasAbove10 = above10;
        }

        private Projectile FindActiveLordYoyo()
        {
            int yoyoType = ModContent.ProjectileType<LordYoyoProj>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];

                if (!projectile.active ||
                    projectile.owner != Player.whoAmI ||
                    projectile.type != yoyoType)
                {
                    continue;
                }

                return projectile;
            }

            return null;
        }

        private void KillOwnedProjectileType(int type)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile projectile = Main.projectile[i];

                if (!projectile.active || projectile.owner != Player.whoAmI || projectile.type != type)
                    continue;

                projectile.Kill();
                projectile.netUpdate = true;
            }
        }

        public override void OnRespawn()
        {
            MNUsedThisLife = false;
            TS2UsedThisLife = false;
            TS2Active = false;
            PostTS2CorePhase = false;
            thresholdInitialized = false;
            wasAbove10 = true;
        }

    }
}
