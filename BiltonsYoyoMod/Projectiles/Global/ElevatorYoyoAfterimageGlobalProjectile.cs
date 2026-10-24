using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using BiltonsYoyoMod.Players;
using BiltonsYoyoMod.Systems;

namespace BiltonsYoyoMod.Projectiles.Global
{
    public class ElevatorYoyoAfterimageGlobalProjectile
        : GlobalProjectile
    {
        public override bool InstancePerEntity => false;

        // AFTERIMAGE SETTINGS

        private const int TrailLength = 6;

        private const float MaximumDistance = 90f;

        private const float MaximumAlpha = 0.45f;

        // DRAW

        public override bool PreDraw(
            Projectile projectile,
            ref Color lightColor
        )
        {
            // ONLY DRAW FOR THE PLAYER'S ACTIVE ELEVATOR YOYO

            if (!projectile.active)
                return base.PreDraw(
                    projectile,
                    ref lightColor
                );

            if (projectile.owner < 0 ||
                projectile.owner >= Main.maxPlayers)
            {
                return base.PreDraw(
                    projectile,
                    ref lightColor
                );
            }

            Player player =
                Main.player[
                    projectile.owner
                ];

            if (player == null ||
                !player.active)
            {
                return base.PreDraw(
                    projectile,
                    ref lightColor
                );
            }

            ElevatorPlayer elevator =
                player.GetModPlayer<
                    ElevatorPlayer
                >();

            if (!elevator.Active)
            {
                return base.PreDraw(
                    projectile,
                    ref lightColor
                );
            }

            // Only affect actual yoyos.
            if (!YoyoTrickSystem.IsYoyoProjectile(
                    projectile))
            {
                return base.PreDraw(
                    projectile,
                    ref lightColor
                );
            }

            // Only the currently held/main yoyo gets the trail.
            Projectile mainYoyo =
                YoyoTrickSystem.GetHeldYoyo(
                    player
                );

            if (mainYoyo == null ||
                mainYoyo.whoAmI != projectile.whoAmI)
            {
                return base.PreDraw(
                    projectile,
                    ref lightColor
                );
            }

            // GET YOYO TEXTURE

            int projectileType =
                projectile.type;

            if (projectileType <= 0 ||
                projectileType >=
                    TextureAssets.Projectile.Length)
            {
                return base.PreDraw(
                    projectile,
                    ref lightColor
                );
            }

            Main.instance.LoadProjectile(
                projectileType
            );

            Texture2D texture =
                TextureAssets.Projectile[
                    projectileType
                ].Value;

            if (texture == null)
            {
                return base.PreDraw(
                    projectile,
                    ref lightColor
                );
            }

            Vector2 origin =
                texture.Size() / 2f;

            // AFTERIMAGES

            for (int i = TrailLength - 1;
                 i >= 0;
                 i--)
            {
                if (i >= projectile.oldPos.Length)
                    continue;

                Vector2 oldPosition =
                    projectile.oldPos[i] +
                    projectile.Size / 2f;

                if (oldPosition == Vector2.Zero)
                    continue;

                float distance =
                    Vector2.Distance(
                        projectile.Center,
                        oldPosition
                    );

                if (distance >
                    MaximumDistance)
                {
                    continue;
                }

                // Closer = stronger.
                float distanceAlpha =
                    1f -
                    MathHelper.Clamp(
                        distance /
                        MaximumDistance,
                        0f,
                        1f
                    );

                // Older = weaker.
                float ageAlpha =
                    1f -
                    i /
                    (float)TrailLength;

                float alpha =
                    MaximumAlpha *
                    distanceAlpha *
                    ageAlpha;

                if (alpha <= 0.01f)
                    continue;

                // LIGHTING AT TRAIL POSITION

                int tileX =
                    (int)(
                        oldPosition.X /
                        16f
                    );

                int tileY =
                    (int)(
                        oldPosition.Y /
                        16f
                    );

                Color lighting =
                    Lighting.GetColor(
                        tileX,
                        tileY
                    );

                // Use lighting as brightness rather than importing
                // its hue and turning the yoyo blue.
                float brightness =
                    (
                        lighting.R +
                        lighting.G +
                        lighting.B
                    ) /
                    765f;

                brightness =
                    MathHelper.Clamp(
                        brightness,
                        0.4f,
                        1f
                    );

                Color drawColor =
                    Color.White;

                drawColor *=
                    alpha *
                    brightness;

                // HISTORICAL ROTATION

                float rotation =
                    projectile.rotation;

                if (i < projectile.oldRot.Length)
                {
                    rotation =
                        projectile.oldRot[i];
                }

                // DRAW

                Main.EntitySpriteDraw(
                    texture,
                    oldPosition -
                        Main.screenPosition,
                    null,
                    drawColor,
                    rotation,
                    origin,
                    projectile.scale,
                    SpriteEffects.None
                );
            }

            // Allow the normal yoyo projectile to draw itself too.
            return base.PreDraw(
                projectile,
                ref lightColor
            );
        }
    }
}