using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace BiltonsYoyoMod.Items.Accessories
{
    public class YoyoTrickABB : YoyoTrickItem
    {
        public override YoyoTrickRank Rank =>
            YoyoTrickRank.MediumT3;

        public override int CooldownSeconds =>
            25;

        public override bool UsesMovementMinigame =>
            true;

        public override void SetDefaults()
        {
            base.SetDefaults();

            Item.rare =
                ItemRarityID.Lime;
        }

        public override void StartTrick(
            Player player)
        {
            // GET THE ACTUAL HELD YOYO

            Projectile yoyo =
                Systems.YoyoTrickSystem.GetHeldYoyo(
                    player
                );

            if (yoyo == null)
            {
                Main.NewText(
                    "BOINGY BOING FAILED: NO HELD YOYO",
                    Color.Red
                );

                return;
            }

            // SOURCE YOYO ITEM

            int yoyoItemType =
                player.HeldItem.type;

            if (yoyoItemType <= 0)
            {
                Main.NewText(
                    "BOINGY BOING FAILED: INVALID YOYO ITEM",
                    Color.Red
                );

                return;
            }

            // DAMAGE

            int damage =
                (int)(
                    yoyo.damage *
                    0.5f
                );

            // LAUNCH EFFECT

            for (int i = 0;
                 i < 18;
                 i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2Circular(
                        4f,
                        4f
                    );

                Dust dust =
                    Dust.NewDustPerfect(
                        yoyo.Center,
                        DustID.GoldFlame,
                        velocity
                    );

                dust.noGravity = true;

                dust.scale =
                    Main.rand.NextFloat(
                        0.8f,
                        1.1f
                    );
            }

            // FIVE COPIES

            for (int i = 0;
                 i < 5;
                 i++)
            {
                Vector2 velocity =
                    Main.rand.NextVector2CircularEdge(
                        8f,
                        8f
                    );

                if (velocity.LengthSquared() < 1f)
                {
                    velocity =
                        Vector2.UnitX *
                        8f;
                }

                velocity *=
                    Main.rand.NextFloat(
                        0.9f,
                        1.1f
                    );

                int projectileIndex =
                    Projectile.NewProjectile(
                        player.GetSource_FromThis(),
                        yoyo.Center,
                        velocity,
                        ModContent.ProjectileType<
                            global::BiltonsYoyoMod.Projectiles
                                .YoyoTrickBounceProjectile>(),
                        damage,
                        yoyo.knockBack,
                        player.whoAmI
                    );

                if (projectileIndex < 0 ||
                    projectileIndex >= Main.maxProjectiles)
                {
                    continue;
                }

                Projectile copy =
                    Main.projectile[
                        projectileIndex
                    ];

                // GIVE COPY THE SOURCE YOYO ITEM

                if (copy.ModProjectile
                    is global::BiltonsYoyoMod.Projectiles
                        .YoyoTrickBounceProjectile bounce)
                {
                    bounce.Initialize(
                        yoyoItemType
                    );
                }

                // IMPORTANT:
                // Do NOT do:
                //
                // copy.scale = yoyo.scale;
                //
                // The bounce projectile already has its own controlled
                // sprite scale so the item texture doesn't become huge.

                // COPY SPAWN DUST

                for (int dustIndex = 0;
                     dustIndex < 5;
                     dustIndex++)
                {
                    Dust dust =
                        Dust.NewDustPerfect(
                            copy.Center,
                            DustID.GoldFlame,
                            -copy.velocity *
                            0.08f
                        );

                    dust.noGravity = true;

                    dust.scale =
                        Main.rand.NextFloat(
                            0.7f,
                            1f
                        );
                }
            }

            Main.NewText(
                "BOINGY BOING: 5 YOYO COPIES LAUNCHED",
                Color.Lime
            );

            Main.NewText(
                "BOINGY BOING: EACH COPY DEALS 50% DAMAGE",
                Color.Yellow
            );
        }
    }
}