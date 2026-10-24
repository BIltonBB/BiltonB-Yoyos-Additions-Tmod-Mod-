using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Systems;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class YoyoTrickAWTD : YoyoTrickItem
    {
        public override YoyoTrickRank Rank =>
            YoyoTrickRank.LowT1;

        public override int CooldownSeconds =>
            20;

        public override bool UsesPositionMinigame =>
            true;

        public override void SetDefaults()
        {
            base.SetDefaults();

            Item.rare =
                ItemRarityID.Green;
        }

        public override void StartTrick(
            Player player
        )
        {
            // GET THE ACTUAL MAIN YOYO

            Projectile yoyo =
                YoyoTrickSystem.GetHeldYoyo(
                    player
                );

            if (
                yoyo == null ||
                !yoyo.active
            )
            {
                return;
            }

            // SPAWN CLONE

            int projectileIndex =
                Projectile.NewProjectile(
                    player.GetSource_FromThis(),
                    yoyo.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<
                        YoyoTrickCloneProjectile
                    >(),
                    0,
                    yoyo.knockBack,
                    player.whoAmI
                );

            if (
                projectileIndex < 0 ||
                projectileIndex >= Main.maxProjectiles
            )
            {
                return;
            }

            // GET CLONE MODPROJECTILE

            YoyoTrickCloneProjectile clone =
                Main.projectile[
                    projectileIndex
                ].ModProjectile
                as YoyoTrickCloneProjectile;

            if (clone == null)
            {
                Main.projectile[
                    projectileIndex
                ].Kill();

                return;
            }

            // CAPTURE SOURCE YOYO
            //
            // The clone receives the exact projectile type and
            // damage of the yoyo that existed when the trick fired.

            clone.Initialize(
                yoyo.type,
                yoyo.damage
            );

            Main.NewText(
                "WALK THE DOG: CLONE CREATED",
                Color.Cyan
            );
        }
    }
}