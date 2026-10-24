using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using System.Collections.Generic;

namespace BiltonsYoyoMod.Projectiles
{
    public class WildStringsAPCProj : WildStringBase
    {
        // TEXTURE

        public override string Texture =>
            "BiltonsYoyoMod/Projectiles/WildStringsAPCProj";

        // ROPE COLOR

        protected override Color StringColor =>
            Color.Green;

        // ORBIT SETTINGS

        protected override int MinimumDistance =>
            160;

        protected override int MaximumDistance =>
            220;

        protected override float MinimumSpeedDegrees =>
            2f;

        protected override float MaximumSpeedDegrees =>
            3f;

        // TARGET TRACKING

        private int lastTarget =
            -1;

        private readonly HashSet<int>
            visitedTargets =
                new HashSet<int>();

        // DEFAULTS

        public override void SetDefaults()
        {
            base.SetDefaults();

            Projectile.timeLeft =
                25 * 60;

            Projectile.penetrate =
                -1;
        }

        // NPC FILTER

        public override bool? CanHitNPC(
            NPC target)
        {
            if (
                target.boss ||
                target.friendly ||
                target.dontTakeDamage
            )
            {
                return false;
            }

            return null;
        }

        // HIT

        public override void OnHitNPC(
            NPC target,
            NPC.HitInfo hit,
            int damageDone)
        {
            if (
                target.boss ||
                target.friendly ||
                target.dontTakeDamage
            )
            {
                return;
            }

            int current =
                target.whoAmI;

            /*
             * Prevent the same NPC from repeatedly generating
             * another Poly-Cotton link.
             */

            if (
                !visitedTargets.Add(
                    current
                )
            )
            {
                return;
            }

            // LINK PREVIOUS TARGET

            if (
                lastTarget >= 0 &&
                lastTarget < Main.maxNPCs &&
                lastTarget != current
            )
            {
                NPC previous =
                    Main.npc[lastTarget];

                if (
                    previous.active &&
                    !previous.friendly &&
                    !previous.boss &&
                    !previous.dontTakeDamage
                )
                {
                    if (
                        Main.netMode !=
                        NetmodeID.MultiplayerClient
                    )
                    {
                        int linkProjectile =
                            Projectile.NewProjectile(
                                Projectile.GetSource_FromAI(),
                                (
                                    previous.Center +
                                    target.Center
                                ) *
                                0.5f,
                                Vector2.Zero,
                                ModContent.ProjectileType<
                                    WildStringLinkProj>(),
                                0,
                                0f,
                                Projectile.owner,
                                previous.whoAmI,
                                current
                            );

                        if (
                            linkProjectile >= 0 &&
                            linkProjectile <
                            Main.maxProjectiles
                        )
                        {
                            Main.projectile[
                                linkProjectile
                            ].netUpdate = true;
                        }
                    }
                }
            }

            // STORE TARGET

            lastTarget =
                current;

            if (
                Main.netMode !=
                NetmodeID.MultiplayerClient
            )
            {
                Projectile.netUpdate =
                    true;
            }
        }
    }
}