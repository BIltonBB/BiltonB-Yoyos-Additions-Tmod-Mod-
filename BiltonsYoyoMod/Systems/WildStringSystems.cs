using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using BiltonsYoyoMod.Projectiles;

namespace BiltonsYoyoMod.Systems
{
    public class WildStringSystems : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // Per-type tint, resolved once (was 5 ModContent lookups per string
        // per frame plus reflection per draw).
        private static readonly Dictionary<int, Color> tintCache = new();

        private static Color TintFor(int type)
        {
            if (!tintCache.TryGetValue(type, out Color c)) {
                c = type == ModContent.ProjectileType<WildStringsAPCProj>() ? new Color(70, 220, 90)
                    : type == ModContent.ProjectileType<WildStringsAPProj>() ? new Color(80, 150, 255)
                    : type == ModContent.ProjectileType<WildStringsANProj>() ? new Color(255, 220, 70)
                    : type == ModContent.ProjectileType<WildStringsAKProj>() ? new Color(139, 90, 43)
                    : Color.White;
                tintCache[type] = c;
            }
            return c;
        }

        // APPLY TO WILD STRINGS

        public override bool AppliesToEntity(
            Projectile projectile,
            bool lateInstantiation)
        {
            return projectile.type ==
                       ModContent.ProjectileType<WildStringsACProj>() ||
                   projectile.type ==
                       ModContent.ProjectileType<WildStringsAPCProj>() ||
                   projectile.type ==
                       ModContent.ProjectileType<WildStringsAPProj>() ||
                   projectile.type ==
                       ModContent.ProjectileType<WildStringsANProj>() ||
                   projectile.type ==
                       ModContent.ProjectileType<WildStringsAKProj>();
        }

        // DRAW (single render path: WildStringBase.PreDraw skips its own so
        // segments are never drawn twice).

        public override void PostDraw(
            Projectile projectile,
            Color lightColor)
        {
            Vector2[] ropePositions =
                (projectile.ModProjectile as WildStringBase)
                    ?.GetRopePositions();

            if (
                ropePositions == null ||
                ropePositions.Length < 2
            )
            {
                return;
            }

            Color stringColor = TintFor(projectile.type);

            // Lighting is smooth: sample every 2nd segment and reuse the
            // previous color in between, halving GetColor calls invisibly.
            Color cachedLight = Color.White;
            bool hasCached = false;

            for (
                int i = 0;
                i < ropePositions.Length - 1;
                i++
            )
            {
                Vector2 start =
                    ropePositions[i];

                Vector2 end =
                    ropePositions[i + 1];

                Vector2 midpoint =
                    (
                        start +
                        end
                    ) * 0.5f;

                Color surroundingLight;
                if (!hasCached || (i & 1) == 0) {
                    surroundingLight = GetLightingColor(midpoint);
                    cachedLight = surroundingLight;
                    hasCached = true;
                }
                else {
                    surroundingLight = cachedLight;
                }

                Color affectedColor =
                    MultiplyColor(
                        stringColor,
                        surroundingLight
                    );

                // 50% affected by surrounding lighting.
                Color finalColor =
                    Color.Lerp(
                        stringColor,
                        affectedColor,
                        0.5f
                    );

                DrawStringSegment(
                    start,
                    end,
                    1.25f,
                    finalColor
                );
            }
        }

        // LIGHTING

        private static Color GetLightingColor(
            Vector2 position)
        {
            int x =
                (int)(
                    position.X /
                    16f
                );

            int y =
                (int)(
                    position.Y /
                    16f
                );

            if (
                x < 0 ||
                y < 0 ||
                x >= Main.maxTilesX ||
                y >= Main.maxTilesY
            )
            {
                return Color.White;
            }

            return Lighting.GetColor(
                x,
                y
            );
        }

        // COLOR MULTIPLICATION

        private static Color MultiplyColor(
            Color baseColor,
            Color lightColor)
        {
            return new Color(
                baseColor.R *
                    lightColor.R /
                    255,

                baseColor.G *
                    lightColor.G /
                    255,

                baseColor.B *
                    lightColor.B /
                    255,

                255
            );
        }

        // DRAW SEGMENT

        private static void DrawStringSegment(
            Vector2 start,
            Vector2 end,
            float width,
            Color color)
        {
            Vector2 difference =
                end -
                start;

            float length =
                difference.Length();

            if (
                length <=
                0.5f
            )
            {
                return;
            }

            Texture2D pixel =
                TextureAssets
                    .MagicPixel
                    .Value;

            Main.EntitySpriteDraw(
                pixel,
                (
                    start +
                    end
                ) *
                0.5f -
                Main.screenPosition,
                new Rectangle(
                    0,
                    0,
                    1,
                    1
                ),
                color,
                difference.ToRotation(),
                new Vector2(
                    0.5f,
                    0.5f
                ),
                new Vector2(
                    length,
                    width
                ),
                SpriteEffects.None,
                0f
            );
        }
    }
}